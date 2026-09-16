-- ============================================================
-- SCRIPT_SQL_UNICO_SMILETRACK.sql
-- Esquema unificado de SmileTrack sin datos ficticios de negocio.
-- Mantiene estructura, relaciones e indices necesarios.
-- Mantiene catalogos funcionales y unicamente el administrador inicial.
-- ============================================================

USE [SmileTrackDB];
GO

-- Opciones requeridas por SQL Server para índices filtrados e índices
-- dependientes de expresiones, independientemente del cliente SQL utilizado.
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
GO

-- ============================================================
-- 1) ACCESO Y SEGURIDAD
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Rol') AND type = N'U')
BEGIN
    CREATE TABLE Rol (
        id_rol INT IDENTITY(1,1) PRIMARY KEY,
        nombre_rol VARCHAR(50) NOT NULL UNIQUE,
        descripcion VARCHAR(200) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Menu') AND type = N'U')
BEGIN
    CREATE TABLE Menu (
        id_menu INT IDENTITY(1,1) PRIMARY KEY,
        nombre VARCHAR(100) NOT NULL,
        url VARCHAR(200) NOT NULL,
        icono VARCHAR(100) NULL,
        orden INT NOT NULL DEFAULT 0,
        id_menu_padre INT NULL,
        modulo VARCHAR(50) NULL,
        activo BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_Menu_Padre FOREIGN KEY (id_menu_padre) REFERENCES Menu(id_menu)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Rol_Menu_Permiso') AND type = N'U')
BEGIN
    CREATE TABLE Rol_Menu_Permiso (
        id_rol INT NOT NULL,
        id_menu INT NOT NULL,
        puede_ver BIT NOT NULL DEFAULT 0,
        puede_crear BIT NOT NULL DEFAULT 0,
        puede_editar BIT NOT NULL DEFAULT 0,
        puede_eliminar BIT NOT NULL DEFAULT 0,
        puede_exportar BIT NOT NULL DEFAULT 0,
        PRIMARY KEY (id_rol, id_menu),
        CONSTRAINT FK_RMP_Rol FOREIGN KEY (id_rol) REFERENCES Rol(id_rol),
        CONSTRAINT FK_RMP_Menu FOREIGN KEY (id_menu) REFERENCES Menu(id_menu)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Usuario') AND type = N'U')
BEGIN
    CREATE TABLE Usuario (
        id_usuario INT IDENTITY(1,1) PRIMARY KEY,
        creado_por INT NULL,
        nombre VARCHAR(100) NOT NULL,
        apellidos VARCHAR(100) NOT NULL,
        correo VARCHAR(150) NOT NULL UNIQUE,
        contrasena VARCHAR(255) NOT NULL,
        id_rol INT NOT NULL,
        estado VARCHAR(10) NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo','inactivo')),
        fecha_nacimiento DATE NULL,
        fecha_creacion DATETIME NOT NULL DEFAULT GETDATE(),
        ultimo_login DATETIME NULL,
        codigo_recuperacion VARCHAR(10) NULL,
        fecha_expiracion_codigo DATETIME NULL,
        intentos_fallidos INT NOT NULL DEFAULT 0,
        ultimo_logout DATETIME NULL,
        CONSTRAINT FK_Usuario_Rol FOREIGN KEY (id_rol) REFERENCES Rol(id_rol),
        CONSTRAINT FK_Usuario_Creador FOREIGN KEY (creado_por) REFERENCES Usuario(id_usuario)
    );
END
GO

IF COL_LENGTH(N'dbo.Usuario', N'codigo_recuperacion') IS NULL
    ALTER TABLE Usuario ADD codigo_recuperacion VARCHAR(10) NULL;
GO
IF COL_LENGTH(N'dbo.Usuario', N'fecha_expiracion_codigo') IS NULL
    ALTER TABLE Usuario ADD fecha_expiracion_codigo DATETIME NULL;
GO
IF COL_LENGTH(N'dbo.Usuario', N'intentos_fallidos') IS NULL
    ALTER TABLE Usuario ADD intentos_fallidos INT NOT NULL DEFAULT 0;
GO
IF COL_LENGTH(N'dbo.Usuario', N'ultimo_logout') IS NULL
    ALTER TABLE Usuario ADD ultimo_logout DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Auditoria') AND type = N'U')
BEGIN
    CREATE TABLE Auditoria (
        id_auditoria INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario INT NULL,
        tabla_afectada VARCHAR(100) NOT NULL,
        id_registro INT NULL,
        accion VARCHAR(45) NOT NULL CHECK (accion IN ('INSERT','UPDATE','DELETE')),
        ip_origen VARCHAR(45) NULL,
        datos_anteriores VARCHAR(MAX) NULL,
        datos_nuevos VARCHAR(MAX) NULL,
        descripcion VARCHAR(255) NULL,
        fecha DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Auditoria_Usuario FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario)
    );
END
GO

-- ============================================================
-- 2) RECUPERACION DE CONTRASEÑA Y PACIENTES
-- ============================================================
IF OBJECT_ID(N'dbo.CodigoRecuperacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodigoRecuperacion (
        id_codigo INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario INT NOT NULL,
        codigo_hash VARCHAR(255) NOT NULL,
        fecha_creacion DATETIME NOT NULL CONSTRAINT DF_CodigoRecuperacion_FechaCreacion DEFAULT (GETDATE()),
        fecha_expiracion DATETIME NOT NULL,
        intentos_fallidos INT NOT NULL CONSTRAINT DF_CodigoRecuperacion_IntentosFallidos DEFAULT (0),
        usado BIT NOT NULL CONSTRAINT DF_CodigoRecuperacion_Usado DEFAULT (0),
        ip_origen VARCHAR(45) NULL,
        CONSTRAINT FK_CodigoRecuperacion_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.AuditoriaRecuperacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditoriaRecuperacion (
        id_auditoria INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario INT NULL,
        correo_solicitado VARCHAR(150) NOT NULL,
        accion VARCHAR(30) NOT NULL,
        ip_origen VARCHAR(45) NULL,
        fecha DATETIME NOT NULL CONSTRAINT DF_AuditoriaRecuperacion_Fecha DEFAULT (GETDATE()),
        CONSTRAINT CK_AuditoriaRecuperacion_Accion CHECK (accion IN ('solicitud','codigo_verificado','codigo_fallido','password_restablecida','bloqueo_por_intentos','rate_limit_excedido','envio_fallido')),
        CONSTRAINT FK_AuditoriaRecuperacion_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
END
GO

-- Corrección: 'envio_fallido' faltaba en el CHECK original y AuthService.cs lo usa
-- cuando el envío del correo de recuperación falla. Si la tabla ya existía con la
-- restricción vieja, la actualizamos aquí (idempotente: solo actúa si falta el valor).
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_AuditoriaRecuperacion_Accion'
      AND definition NOT LIKE '%envio_fallido%'
)
BEGIN
    ALTER TABLE dbo.AuditoriaRecuperacion DROP CONSTRAINT CK_AuditoriaRecuperacion_Accion;

    ALTER TABLE dbo.AuditoriaRecuperacion
        ADD CONSTRAINT CK_AuditoriaRecuperacion_Accion
        CHECK (accion IN ('solicitud','codigo_verificado','codigo_fallido','password_restablecida','bloqueo_por_intentos','rate_limit_excedido','envio_fallido'));

    PRINT 'CK_AuditoriaRecuperacion_Accion actualizado: se agregó ''envio_fallido''.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CodigoRecuperacion_Usuario_Usado_Expiracion' AND object_id = OBJECT_ID(N'dbo.CodigoRecuperacion'))
    CREATE INDEX IX_CodigoRecuperacion_Usuario_Usado_Expiracion ON dbo.CodigoRecuperacion (id_usuario, usado, fecha_expiracion);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditoriaRecuperacion_Correo_Fecha' AND object_id = OBJECT_ID(N'dbo.AuditoriaRecuperacion'))
    CREATE INDEX IX_AuditoriaRecuperacion_Correo_Fecha ON dbo.AuditoriaRecuperacion (correo_solicitado, fecha);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditoriaRecuperacion_Usuario_Fecha' AND object_id = OBJECT_ID(N'dbo.AuditoriaRecuperacion'))
    CREATE INDEX IX_AuditoriaRecuperacion_Usuario_Fecha ON dbo.AuditoriaRecuperacion (id_usuario, fecha);
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Paciente') AND type = N'U')
BEGIN
    CREATE TABLE Paciente (
        id_paciente INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario INT NULL,
        tipo_documento VARCHAR(5) NOT NULL CHECK (tipo_documento IN ('CC','TI','CE','PAS','NIT')),
        documento VARCHAR(20) NOT NULL UNIQUE,
        nombres VARCHAR(100) NOT NULL,
        apellidos VARCHAR(100) NOT NULL,
        fecha_nacimiento DATE NOT NULL,
        genero VARCHAR(5) NULL CHECK (genero IN ('M','F','O')),
        telefono VARCHAR(20) NULL,
        correo VARCHAR(150) NULL,
        direccion VARCHAR(255) NULL,
        ciudad VARCHAR(100) NULL,
        grupo_sanguineo VARCHAR(5) NULL,
        alergias VARCHAR(MAX) NULL,
        antecedentes_medicos VARCHAR(MAX) NULL,
        contacto_emergencia VARCHAR(100) NULL,
        telefono_emergencia VARCHAR(20) NULL,
        fecha_registro DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        estado VARCHAR(10) NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo','inactivo','retirado')),
        archivo_adjunto VARCHAR(255) NULL,
        CONSTRAINT FK_Paciente_Usuario FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario)
    );
END
GO

-- ============================================================
-- 3) PROFESIONALES
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Especialidad') AND type = N'U')
BEGIN
    CREATE TABLE Especialidad (
        id_especialidad INT IDENTITY(1,1) PRIMARY KEY,
        nombre VARCHAR(100) NOT NULL,
        descripcion VARCHAR(255) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Profesional') AND type = N'U')
BEGIN
    CREATE TABLE Profesional (
        id_profesional INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario INT NULL,
        nombres VARCHAR(100) NOT NULL,
        apellidos VARCHAR(100) NOT NULL,
        registro_medico VARCHAR(50) NOT NULL,
        descripcion VARCHAR(255) NULL,
        categoria VARCHAR(100) NULL,
        telefono VARCHAR(20) NULL,
        estado VARCHAR(15) NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo','vacaciones','inactivo')),
        fecha_ingreso DATE NULL,
        CONSTRAINT FK_Profesional_Usuario FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario) ON DELETE SET NULL
    );
END
GO

IF COL_LENGTH(N'dbo.Profesional', N'nombres') IS NULL
    ALTER TABLE Profesional ADD nombres VARCHAR(100) NOT NULL CONSTRAINT DF_Profesional_Nombres DEFAULT '';
GO
IF COL_LENGTH(N'dbo.Profesional', N'apellidos') IS NULL
    ALTER TABLE Profesional ADD apellidos VARCHAR(100) NOT NULL CONSTRAINT DF_Profesional_Apellidos DEFAULT '';
GO
IF COL_LENGTH(N'dbo.Profesional', N'registro_medico') IS NULL
    ALTER TABLE Profesional ADD registro_medico VARCHAR(50) NOT NULL CONSTRAINT DF_Profesional_RegistroMedico DEFAULT '';
GO
IF COL_LENGTH(N'dbo.Profesional', N'descripcion') IS NULL
    ALTER TABLE Profesional ADD descripcion VARCHAR(255) NULL;
GO
IF COL_LENGTH(N'dbo.Profesional', N'categoria') IS NULL
    ALTER TABLE Profesional ADD categoria VARCHAR(100) NULL;
GO
IF COL_LENGTH(N'dbo.Profesional', N'telefono') IS NULL
    ALTER TABLE Profesional ADD telefono VARCHAR(20) NULL;
GO
IF COL_LENGTH(N'dbo.Profesional', N'estado') IS NULL
    ALTER TABLE Profesional ADD estado VARCHAR(15) NOT NULL CONSTRAINT DF_Profesional_Estado DEFAULT 'activo';
GO
IF COL_LENGTH(N'dbo.Profesional', N'fecha_ingreso') IS NULL
    ALTER TABLE Profesional ADD fecha_ingreso DATE NULL;
GO

-- ── Corrección: agregar 'vacaciones' al CHECK constraint de Profesional.estado ─────
-- El CHECK original solo permitía 'activo' e 'inactivo', pero el sistema admite
-- 'vacaciones' como tercer estado válido (ProfesionalService.CambiarEstadoAsync).
-- Se elimina el constraint anterior y se recrea con los 3 valores.
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Profesional')
      AND definition LIKE '%activo%'
      AND definition NOT LIKE '%vacaciones%'
)
BEGIN
    DECLARE @ck_name NVARCHAR(256);
    SELECT @ck_name = name
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Profesional')
      AND definition LIKE '%activo%';

    IF @ck_name IS NOT NULL
        EXEC('ALTER TABLE Profesional DROP CONSTRAINT [' + @ck_name + ']');

    ALTER TABLE Profesional
        ADD CONSTRAINT CK_Profesional_Estado
        CHECK (estado IN ('activo', 'vacaciones', 'inactivo'));
END
ELSE IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Profesional')
)
BEGIN
    ALTER TABLE Profesional
        ADD CONSTRAINT CK_Profesional_Estado
        CHECK (estado IN ('activo', 'vacaciones', 'inactivo'));
END
GO

-- ── Índice único sobre registro_medico ──────────────────────────────────────────
-- Previene duplicados a nivel de BD (la capa C# ya verifica unicidad, pero sin
-- este índice dos peticiones concurrentes podrían insertar el mismo registro médico).
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Profesional_RegistroMedico'
      AND object_id = OBJECT_ID(N'dbo.Profesional')
)
    CREATE UNIQUE INDEX UQ_Profesional_RegistroMedico
        ON dbo.Profesional (registro_medico);
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Profesional_Especialidad') AND type = N'U')
BEGIN
    CREATE TABLE Profesional_Especialidad (
        id_profesional INT NOT NULL,
        id_especialidad INT NOT NULL,
        principal BIT NOT NULL DEFAULT 0,
        PRIMARY KEY (id_profesional, id_especialidad),
        CONSTRAINT FK_PE_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional),
        CONSTRAINT FK_PE_Especialidad FOREIGN KEY (id_especialidad) REFERENCES Especialidad(id_especialidad)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Horario_Profesional') AND type = N'U')
BEGIN
    CREATE TABLE Horario_Profesional (
        id_horario INT IDENTITY(1,1) PRIMARY KEY,
        id_profesional INT NOT NULL,
        dia_semana VARCHAR(12) NOT NULL CHECK (dia_semana IN ('Lunes','Martes','Miercoles','Jueves','Viernes','Sabado','Domingo')),
        hora_inicio TIME NOT NULL,
        hora_fin TIME NOT NULL,
        activo BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_HP_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Bloqueo_Profesional') AND type = N'U')
BEGIN
    CREATE TABLE Bloqueo_Profesional (
        id_bloqueo INT IDENTITY(1,1) PRIMARY KEY,
        id_profesional INT NOT NULL,
        fecha_inicio DATETIME NOT NULL,
        fecha_fin DATETIME NOT NULL,
        motivo VARCHAR(150) NULL,
        aprobado_por INT NULL,
        CONSTRAINT FK_BP_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Ausencia_Profesional') AND type = N'U')
BEGIN
    CREATE TABLE Ausencia_Profesional (
        id_ausencia INT IDENTITY(1,1) PRIMARY KEY,
        id_profesional INT NOT NULL,
        tipo VARCHAR(15) NOT NULL CHECK (tipo IN ('vacaciones','incapacidad','permiso','otro')),
        fecha_inicio DATE NOT NULL,
        fecha_fin DATE NOT NULL,
        duracion INT NULL,
        observaciones VARCHAR(MAX) NULL,
        aprobado_por INT NULL,
        CONSTRAINT FK_AP_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional)
    );
END
GO

-- ============================================================
-- 4) SERVICIOS Y CONSULTORIOS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Servicio') AND type = N'U')
BEGIN
    CREATE TABLE Servicio (
        id_servicio INT IDENTITY(1,1) PRIMARY KEY,
        nombre VARCHAR(150) NOT NULL,
        descripcion VARCHAR(500) NULL,
        precio DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        estado VARCHAR(10) NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo','inactivo'))
    );
END
GO

IF COL_LENGTH(N'dbo.Servicio', N'precio') IS NULL
    ALTER TABLE Servicio ADD precio DECIMAL(12,2) NOT NULL CONSTRAINT DF_Servicio_Precio DEFAULT 0.00;
GO
IF COL_LENGTH(N'dbo.Servicio', N'estado') IS NULL
    ALTER TABLE Servicio ADD estado VARCHAR(10) NOT NULL CONSTRAINT DF_Servicio_Estado DEFAULT 'activo';
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Profesional_Servicio') AND type = N'U')
BEGIN
    CREATE TABLE Profesional_Servicio (
        id_profesional        INT NOT NULL,
        id_servicio           INT NOT NULL,
        precio_personalizado  DECIMAL(12,2) NULL,
        activo                BIT NOT NULL DEFAULT 1,
        PRIMARY KEY (id_profesional, id_servicio),
        CONSTRAINT FK_PS_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional),
        CONSTRAINT FK_PS_Servicio    FOREIGN KEY (id_servicio)    REFERENCES Servicio(id_servicio)
    );
END
GO

-- Columnas extendidas para BD existentes que ya tienen la tabla sin estas columnas (idempotentes)
IF COL_LENGTH(N'dbo.Profesional_Servicio', N'precio_personalizado') IS NULL
    ALTER TABLE dbo.Profesional_Servicio ADD precio_personalizado DECIMAL(12,2) NULL;
GO
IF COL_LENGTH(N'dbo.Profesional_Servicio', N'activo') IS NULL
    ALTER TABLE dbo.Profesional_Servicio ADD activo BIT NOT NULL CONSTRAINT DF_PS_Activo DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Consultorio') AND type = N'U')
BEGIN
    CREATE TABLE Consultorio (
        id_consultorio INT IDENTITY(1,1) PRIMARY KEY,
        nombre VARCHAR(100) NOT NULL,
        ubicacion VARCHAR(150) NULL,
        tipo VARCHAR(50) NULL,
        nombre_estado VARCHAR(50) NULL,
        capacidad INT NULL,
        estado VARCHAR(15) NOT NULL DEFAULT 'disponible' CHECK (estado IN ('disponible','ocupado','mantenimiento'))
    );
END
GO

-- ============================================================
-- 5) CITAS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Estado_Cita') AND type = N'U')
BEGIN
    CREATE TABLE Estado_Cita (
        id_estado INT IDENTITY(1,1) PRIMARY KEY,
        nombre_estado VARCHAR(50) NOT NULL UNIQUE,
        descripcion VARCHAR(150) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Cita') AND type = N'U')
BEGIN
    CREATE TABLE Cita (
        id_cita INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente INT NOT NULL,
        id_profesional INT NULL,
        id_servicio INT NULL,
        fecha_hora DATETIME NOT NULL,
        estado VARCHAR(30) NOT NULL DEFAULT 'Agendada',
        notas VARCHAR(MAX) NULL,
        CONSTRAINT FK_Cita_Paciente FOREIGN KEY (id_paciente) REFERENCES Paciente(id_paciente),
        CONSTRAINT FK_Cita_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional) ON DELETE SET NULL,
        CONSTRAINT FK_Cita_Servicio FOREIGN KEY (id_servicio) REFERENCES Servicio(id_servicio) ON DELETE SET NULL
    );
END
GO

IF COL_LENGTH(N'dbo.Cita', N'id_servicio') IS NULL
BEGIN
    ALTER TABLE Cita ADD id_servicio INT NULL;
    ALTER TABLE Cita ADD CONSTRAINT FK_Cita_Servicio FOREIGN KEY (id_servicio) REFERENCES Servicio(id_servicio) ON DELETE SET NULL;
END
GO
IF COL_LENGTH(N'dbo.Cita', N'fecha_hora') IS NULL
    ALTER TABLE Cita ADD fecha_hora DATETIME NOT NULL CONSTRAINT DF_Cita_FechaHora DEFAULT GETDATE();
GO
IF COL_LENGTH(N'dbo.Cita', N'estado') IS NULL
    ALTER TABLE Cita ADD estado VARCHAR(30) NOT NULL CONSTRAINT DF_Cita_Estado DEFAULT 'Agendada';
GO
IF COL_LENGTH(N'dbo.Cita', N'notas') IS NULL
    ALTER TABLE Cita ADD notas VARCHAR(MAX) NULL;
GO

-- ============================================================
-- 6) HISTORIA CLINICA, FACTURACION Y OTROS MODULOS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Historia_Clinica') AND type = N'U')
BEGIN
    CREATE TABLE Historia_Clinica (
        id_historia INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente INT NOT NULL UNIQUE,
        fecha_apertura DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        observaciones_generales VARCHAR(MAX) NULL,
        activa BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_HC_Paciente FOREIGN KEY (id_paciente) REFERENCES Paciente(id_paciente)
    );
END
GO

-- ============================================================
-- 6.1) AMPLIACION DE CITAS
-- Solo estructura y catalogo funcional. Sin datos de negocio demo.
-- ============================================================
SET XACT_ABORT ON;
GO

DECLARE @vals TABLE (nombre VARCHAR(50), descripcion VARCHAR(150));
INSERT INTO @vals (nombre, descripcion) VALUES
('Solicitada', 'Cita solicitada por el paciente pendiente de confirmacion y asignacion'),
('Agendada', 'Cita programada y pendiente'),
('Confirmada', 'Cita confirmada por paciente o clinica'),
('En consulta', 'Paciente en consulta'),
('Atendida', 'Cita atendida y finalizada'),
('Cancelada', 'Cita cancelada'),
('No asistio', 'Paciente no asistio');

DECLARE @n VARCHAR(50), @d VARCHAR(150);
DECLARE cur_estados CURSOR LOCAL FAST_FORWARD FOR SELECT nombre, descripcion FROM @vals;
OPEN cur_estados;
FETCH NEXT FROM cur_estados INTO @n, @d;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Estado_Cita WHERE nombre_estado = @n)
        INSERT INTO dbo.Estado_Cita (nombre_estado, descripcion) VALUES (@n, @d);
    FETCH NEXT FROM cur_estados INTO @n, @d;
END
CLOSE cur_estados;
DEALLOCATE cur_estados;
GO

IF OBJECT_ID('dbo.Cita', 'U') IS NULL
    THROW 50000, 'La tabla dbo.Cita no existe. Confirma que estas ejecutando contra SmileTrackDB.', 1;
GO

IF COL_LENGTH(N'dbo.Cita', N'fecha') IS NULL
    ALTER TABLE dbo.Cita ADD fecha DATE NULL;
IF COL_LENGTH(N'dbo.Cita', N'hora_inicio') IS NULL
    ALTER TABLE dbo.Cita ADD hora_inicio TIME NULL;
IF COL_LENGTH(N'dbo.Cita', N'hora_fin') IS NULL
    ALTER TABLE dbo.Cita ADD hora_fin TIME NULL;
IF COL_LENGTH(N'dbo.Cita', N'motivo_consulta') IS NULL
    ALTER TABLE dbo.Cita ADD motivo_consulta VARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Cita', N'notas_previas') IS NULL
    ALTER TABLE dbo.Cita ADD notas_previas VARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.Cita', N'tipo_cita') IS NULL
    ALTER TABLE dbo.Cita ADD tipo_cita VARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.Cita', N'id_consultorio') IS NULL
    ALTER TABLE dbo.Cita ADD id_consultorio INT NULL;
IF COL_LENGTH(N'dbo.Cita', N'id_estado') IS NULL
    ALTER TABLE dbo.Cita ADD id_estado INT NULL;
IF COL_LENGTH(N'dbo.Cita', N'fecha_creacion') IS NULL
    ALTER TABLE dbo.Cita ADD fecha_creacion DATETIME NULL CONSTRAINT DF_Cita_FechaCreacion DEFAULT (GETDATE());
IF COL_LENGTH(N'dbo.Cita', N'creado_por') IS NULL
    ALTER TABLE dbo.Cita ADD creado_por INT NULL;
IF COL_LENGTH(N'dbo.Cita', N'archivo_adjunto') IS NULL
    ALTER TABLE dbo.Cita ADD archivo_adjunto VARCHAR(255) NULL;
IF COL_LENGTH(N'dbo.Cita', N'duracion_minutos') IS NULL
    ALTER TABLE dbo.Cita ADD duracion_minutos INT NOT NULL
        CONSTRAINT DF_Cita_DuracionMinutos DEFAULT 60;
GO

UPDATE dbo.Cita
SET estado = 'Agendada'
WHERE estado = 'programada';
GO

UPDATE c
SET c.id_estado = ec.id_estado
FROM dbo.Cita c
INNER JOIN dbo.Estado_Cita ec ON ec.nombre_estado = c.estado
WHERE c.id_estado IS NULL OR c.id_estado <> ec.id_estado;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Cita_Consultorio')
BEGIN
    ALTER TABLE dbo.Cita
    ADD CONSTRAINT FK_Cita_Consultorio
        FOREIGN KEY (id_consultorio)
        REFERENCES dbo.Consultorio(id_consultorio);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Cita_EstadoCita')
BEGIN
    ALTER TABLE dbo.Cita
    ADD CONSTRAINT FK_Cita_EstadoCita
        FOREIGN KEY (id_estado)
        REFERENCES dbo.Estado_Cita(id_estado);
END
GO

-- ============================================================
-- ÍNDICES NONCLUSTERED EN TABLA Cita (Hallazgo A-07 / C-03)
-- Eliminan table-scans en las consultas más frecuentes:
--   1. Agenda por profesional + fecha (filtro principal de la agenda semanal)
--   2. Citas por paciente + fecha     (historial del paciente)
--   3. Citas por consultorio + fecha  (verificación de disponibilidad)
--   4. Estado + fecha                 (dashboards y KPIs de gestión)
-- Todos son idempotentes: solo se crean si no existen.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Profesional_Fecha' AND object_id = OBJECT_ID('dbo.Cita'))
    CREATE NONCLUSTERED INDEX IX_Cita_Profesional_Fecha
        ON dbo.Cita (id_profesional, fecha_hora)
        INCLUDE (id_paciente, id_consultorio, estado)
        WHERE id_profesional IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Paciente_Fecha' AND object_id = OBJECT_ID('dbo.Cita'))
    CREATE NONCLUSTERED INDEX IX_Cita_Paciente_Fecha
        ON dbo.Cita (id_paciente, fecha_hora)
        INCLUDE (id_profesional, id_consultorio, estado);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Consultorio_Fecha' AND object_id = OBJECT_ID('dbo.Cita'))
    CREATE NONCLUSTERED INDEX IX_Cita_Consultorio_Fecha
        ON dbo.Cita (id_consultorio, fecha_hora)
        INCLUDE (id_profesional, id_paciente, estado)
        WHERE id_consultorio IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Estado_Fecha' AND object_id = OBJECT_ID('dbo.Cita'))
    CREATE NONCLUSTERED INDEX IX_Cita_Estado_Fecha
        ON dbo.Cita (estado, fecha_hora)
        INCLUDE (id_paciente, id_profesional, id_consultorio);
GO

-- Filtros por rango de fecha usados por agendas, dashboards y recordatorios.
-- Los índices por profesional/estado ya cubren sus respectivas columnas como
-- primera clave; este índice agrega la búsqueda por fecha como predicado inicial.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_FechaHora' AND object_id = OBJECT_ID('dbo.Cita'))
    CREATE NONCLUSTERED INDEX IX_Cita_FechaHora
        ON dbo.Cita (fecha_hora)
        INCLUDE (id_paciente, id_profesional, id_consultorio, estado, id_servicio);
GO

IF OBJECT_ID(N'dbo.Notificacion_Leida', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notificacion_Leida (
        id_notificacion_leida INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente INT NOT NULL,
        id_cita INT NOT NULL,
        fecha_lectura DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_NotificacionLeida_Paciente_Cita UNIQUE (id_paciente, id_cita),
        CONSTRAINT FK_NotificacionLeida_Paciente FOREIGN KEY (id_paciente) REFERENCES dbo.Paciente(id_paciente) ON DELETE CASCADE,
        CONSTRAINT FK_NotificacionLeida_Cita FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Factura') AND type = N'U')
BEGIN
    CREATE TABLE Factura (
        id_factura INT IDENTITY(1,1) PRIMARY KEY,
        numero_factura VARCHAR(20) NOT NULL UNIQUE,
        fecha_factura DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
        subtotal DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        total DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        estado VARCHAR(10) NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente','parcial','pagada','anulada')),
        id_paciente INT NOT NULL,
        notas VARCHAR(MAX) NULL,
        generada_por INT NOT NULL,
        CONSTRAINT FK_Factura_Paciente FOREIGN KEY (id_paciente) REFERENCES Paciente(id_paciente),
        CONSTRAINT FK_Factura_GeneradaPor FOREIGN KEY (generada_por) REFERENCES Usuario(id_usuario)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Detalle_Factura') AND type = N'U')
BEGIN
   CREATE TABLE Detalle_Factura (
      id_detalle INT IDENTITY(1,1) PRIMARY KEY,
       id_factura INT NOT NULL,
     id_servicio INT NULL,
      descripcion VARCHAR(200) NOT NULL,
       cantidad INT NOT NULL DEFAULT 1 CHECK (cantidad > 0),
        precio_unitario DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        subtotal_linea DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        CONSTRAINT FK_DetalleFactura_Factura FOREIGN KEY (id_factura) REFERENCES Factura(id_factura) ON DELETE CASCADE,
        CONSTRAINT FK_DetalleFactura_Servicio FOREIGN KEY (id_servicio) REFERENCES Servicio(id_servicio)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Factura') AND name = 'monto_pagado')
BEGIN
    ALTER TABLE dbo.Factura ADD monto_pagado DECIMAL(12,2) NOT NULL DEFAULT 0.00;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Factura') AND name = 'fecha_pago')
BEGIN
    ALTER TABLE dbo.Factura ADD fecha_pago DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Ticket_Soporte') AND type = N'U')
BEGIN
    CREATE TABLE Ticket_Soporte (
        id_ticket INT IDENTITY(1,1) PRIMARY KEY,
        referencia VARCHAR(20) NOT NULL UNIQUE,
        id_usuario INT NOT NULL,
        asunto VARCHAR(200) NOT NULL,
        categoria VARCHAR(20) NOT NULL CHECK (categoria IN ('incidente','consulta','solicitud','otro')),
        modulo_afectado VARCHAR(20) NOT NULL CHECK (modulo_afectado IN ('citas','pacientes','facturacion','reportes','sistema')),
        severidad VARCHAR(10) NOT NULL DEFAULT 'media' CHECK (severidad IN ('baja','media','alta')),
        descripcion VARCHAR(MAX) NOT NULL,
        captura_pantalla VARCHAR(255) NULL,
        estado VARCHAR(20) NOT NULL DEFAULT 'abierto' CHECK (estado IN ('abierto','en_proceso','resuelto','cerrado')),
        fecha_creacion DATETIME NOT NULL DEFAULT GETDATE(),
        fecha_respuesta DATETIME NULL,
        respuesta VARCHAR(MAX) NULL,
        atendido_por INT NULL,
        CONSTRAINT FK_TicketSoporte_Usuario FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario),
        CONSTRAINT FK_TicketSoporte_Atendido FOREIGN KEY (atendido_por) REFERENCES Usuario(id_usuario)
    );
END
GO


IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Servicio') AND name = 'categoria')
BEGIN
    ALTER TABLE dbo.Servicio ADD categoria VARCHAR(50) NOT NULL DEFAULT 'general';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Servicio') AND name = 'duracion_minutos')
BEGIN
    ALTER TABLE dbo.Servicio ADD duracion_minutos INT NOT NULL DEFAULT 30;
END
GO


IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.PQR') AND type = N'U')
BEGIN
    CREATE TABLE PQR (
        id_pqr INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente INT NOT NULL,
        id_usuario INT NULL,
        tipo VARCHAR(20) NOT NULL CHECK (tipo IN ('peticion','queja','reclamo','sugerencia')),
        asunto VARCHAR(200) NOT NULL,
        descripcion VARCHAR(MAX) NOT NULL,
        estado VARCHAR(20) NOT NULL DEFAULT 'recibida' CHECK (estado IN ('recibida','en_proceso','resuelta','cerrada','rechazada')),
        prioridad VARCHAR(10) NOT NULL DEFAULT 'media' CHECK (prioridad IN ('baja','media','alta','urgente')),
        fecha_creacion DATETIME NOT NULL DEFAULT GETDATE(),
        fecha_respuesta DATETIME NULL,
        respuesta VARCHAR(MAX) NULL,
        atendida_por INT NULL,
        evidencia_adjunto VARCHAR(255) NULL,
        CONSTRAINT FK_PQR_Paciente FOREIGN KEY (id_paciente) REFERENCES Paciente(id_paciente),
        CONSTRAINT FK_PQR_Usuario FOREIGN KEY (id_usuario) REFERENCES Usuario(id_usuario),
        CONSTRAINT FK_PQR_Atendida FOREIGN KEY (atendida_por) REFERENCES Usuario(id_usuario)
    );
END
GO

-- ============================================================
-- 6.2) CONFIGURACION GENERAL, EQUIPOS E INVENTARIO
-- Solo estructura. Sin datos demo.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Configuracion_General') AND type = N'U')
BEGIN
    CREATE TABLE Configuracion_General (
        id_configuracion INT IDENTITY(1,1) PRIMARY KEY,
        clave VARCHAR(100) NOT NULL UNIQUE,
        valor VARCHAR(500) NOT NULL,
        descripcion VARCHAR(255) NULL,
        modulo VARCHAR(50) NOT NULL DEFAULT 'general'
    );
END
GO

-- Datos de configuración base (idempotentes)
IF NOT EXISTS (SELECT 1 FROM dbo.Configuracion_General WHERE clave = 'cita_duracion_minutos')
    INSERT INTO dbo.Configuracion_General (clave, valor, descripcion, modulo)
    VALUES ('cita_duracion_minutos', '60',
            'Duración predeterminada de cada cita en minutos. Usado para calcular bloques de agenda y detectar conflictos de horario.',
            'citas');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Configuracion_General WHERE clave = 'horario_apertura')
    INSERT INTO dbo.Configuracion_General (clave, valor, descripcion, modulo)
    VALUES ('horario_apertura', '07:00',
            'Hora de apertura de la clínica (formato HH:mm). Las citas no pueden agendarse antes de este horario.',
            'citas');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Configuracion_General WHERE clave = 'horario_cierre')
    INSERT INTO dbo.Configuracion_General (clave, valor, descripcion, modulo)
    VALUES ('horario_cierre', '18:00',
            'Hora de cierre de la clínica (formato HH:mm). Las citas no pueden agendarse después de este horario.',
            'citas');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Configuracion_General WHERE clave = 'dias_atencion')
    INSERT INTO dbo.Configuracion_General (clave, valor, descripcion, modulo)
    VALUES ('dias_atencion', '1,2,3,4,5,6',
            'Días de atención separados por coma (1=Lunes … 7=Domingo). Valor predeterminado: lunes a sábado.',
            'citas');
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Equipo') AND type = N'U')
BEGIN
    CREATE TABLE Equipo (
        id_equipo INT IDENTITY(1,1) PRIMARY KEY,
        nombre VARCHAR(150) NOT NULL,
        modelo VARCHAR(100) NULL,
        serie VARCHAR(100) NULL UNIQUE,
        status VARCHAR(30) NOT NULL DEFAULT 'operativo' CHECK (status IN ('operativo','mantenimiento','fuera_servicio')),
        ultimo_mantenimiento DATETIME NULL,
        proximo_mantenimiento DATETIME NULL,
        ubicacion VARCHAR(150) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Inventario') AND type = N'U')
BEGIN
    CREATE TABLE Inventario (
        id_item INT IDENTITY(1,1) PRIMARY KEY,
        codigo VARCHAR(50) NOT NULL UNIQUE,
        nombre VARCHAR(200) NOT NULL,
        categoria VARCHAR(100) NULL,
        stock_actual INT NOT NULL DEFAULT 0,
        stock_minimo INT NOT NULL DEFAULT 0,
        unidad_medida VARCHAR(50) NULL,
        precio_unitario DECIMAL(12,2) NOT NULL DEFAULT 0.00,
        fecha_vencimiento DATE NULL,
        estado VARCHAR(30) NOT NULL DEFAULT 'disponible'
    );
END
GO

-- ============================================================
-- 7) CATALOGOS BASE
-- Estos son catalogos funcionales, no datos de negocio ficticios.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Rol WHERE nombre_rol='Administrador')
    INSERT INTO Rol (nombre_rol, descripcion) VALUES ('Administrador','Acceso total');
IF NOT EXISTS (SELECT 1 FROM Rol WHERE nombre_rol='Profesional')
    INSERT INTO Rol (nombre_rol, descripcion) VALUES ('Profesional','Gestion clinica');
IF NOT EXISTS (SELECT 1 FROM Rol WHERE nombre_rol='Auxiliar')
    INSERT INTO Rol (nombre_rol, descripcion) VALUES ('Auxiliar','Apoyo clinico');
IF NOT EXISTS (SELECT 1 FROM Rol WHERE nombre_rol='Recepcionista')
    INSERT INTO Rol (nombre_rol, descripcion) VALUES ('Recepcionista','Gestion de citas');
IF NOT EXISTS (SELECT 1 FROM Rol WHERE nombre_rol='Paciente')
    INSERT INTO Rol (nombre_rol, descripcion) VALUES ('Paciente','Consulta propia');
GO

IF NOT EXISTS (SELECT 1 FROM Menu WHERE url='/Publico/homepage.html')
    INSERT INTO Menu (nombre,url,icono,orden,modulo)
    VALUES ('Inicio','/Publico/homepage.html','🏠',1,'publico');
GO

IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Odontología General')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Odontología General','Atención dental primaria y preventiva');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Endodoncia')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Endodoncia','Tratamiento de conductos radiculares');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Ortodoncia')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Ortodoncia','Corrección de la posición dental');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Periodoncia')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Periodoncia','Enfermedades de encías y tejidos de soporte');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Rehabilitación Oral y Estética Dental')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Rehabilitación Oral y Estética Dental','Restauración funcional y estética');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Cirugía Oral')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Cirugía Oral','Procedimientos quirúrgicos orales');
IF NOT EXISTS (SELECT 1 FROM Especialidad WHERE nombre='Odontopediatría')
    INSERT INTO Especialidad (nombre, descripcion)
    VALUES ('Odontopediatría','Odontología para niños y adolescentes');
GO



-- ============================================================
-- 7.1) ADMINISTRADOR INICIAL
-- Unico usuario creado automaticamente para poder iniciar sesion.
-- No crea pacientes, profesionales ni usuarios de demostracion.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE correo='admin@smiletrack.co')
BEGIN
    INSERT INTO Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES (
        'Admin',
        'SmileTrack',
        'admin@smiletrack.co',
        '$2a$11$u.Lp05p02n3H8i1j/3CgkuM9Vl8y7D2pXfG7zT66.qG4q/3.X9G1a',
        (SELECT id_rol FROM Rol WHERE nombre_rol='Administrador'),
        'activo',
        GETDATE()
    );
END
GO

PRINT 'SCRIPT_SQL_UNICO_SMILETRACK ejecutado: esquema y catalogos base listos, sin datos ficticios de negocio.';
GO

-- ============================================================
-- SCRIPT: Agregar tabla Registro_Odontograma  (yeray)
-- Propósito: Reemplazar el JSON en Historia_Clinica.observaciones_generales
--            por registros estructurados por diente, con trazabilidad real.
-- Ejecutar contra: SmileTrackDB
-- ============================================================


GO

-- ── 1. Crear tabla Registro_Odontograma ──────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.Registro_Odontograma') AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.Registro_Odontograma (
        id_registro       INT IDENTITY(1,1) PRIMARY KEY,

        -- Relación con Historia Clínica (1 HC puede tener muchos registros)
        id_historia       INT NOT NULL,

        -- Número FDI del diente (11-48 adulto, 51-85 niño)
        numero_fdi        VARCHAR(5)   NOT NULL,

        -- Nombre legible del diente (ej: "Incisivo central superior derecho")
        nombre_pieza      VARCHAR(150) NULL,

        -- Estado clínico registrado (sano, caries, endodoncia, corona, etc.)
        estado            VARCHAR(50)  NOT NULL,

        -- Observación libre del profesional sobre ese diente
        observacion       VARCHAR(MAX) NULL,

        -- Fecha y hora exactas del registro
        fecha_registro    DATETIME NOT NULL DEFAULT GETDATE(),

        -- Profesional que hizo el registro (trazabilidad)
        id_profesional    INT NULL,

        -- Cita en la que se realizó el tratamiento (opcional pero recomendado)
        id_cita           INT NULL,

        CONSTRAINT FK_RO_Historia     FOREIGN KEY (id_historia)
            REFERENCES dbo.Historia_Clinica(id_historia),

        CONSTRAINT FK_RO_Profesional  FOREIGN KEY (id_profesional)
            REFERENCES dbo.Profesional(id_profesional) ON DELETE SET NULL,

        CONSTRAINT FK_RO_Cita         FOREIGN KEY (id_cita)
            REFERENCES dbo.Cita(id_cita) ON DELETE SET NULL
    );

    PRINT 'Tabla Registro_Odontograma creada correctamente.';
END
ELSE
    PRINT 'Tabla Registro_Odontograma ya existe — sin cambios.';
GO

-- ── 2. Índices para consultas frecuentes ─────────────────────

-- Buscar todos los registros de una historia clínica
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RO_Historia' AND object_id = OBJECT_ID(N'dbo.Registro_Odontograma')
)
    CREATE INDEX IX_RO_Historia
        ON dbo.Registro_Odontograma (id_historia);
GO

-- Buscar registros de un diente específico dentro de una historia
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RO_Historia_FDI' AND object_id = OBJECT_ID(N'dbo.Registro_Odontograma')
)
    CREATE INDEX IX_RO_Historia_FDI
        ON dbo.Registro_Odontograma (id_historia, numero_fdi);
GO

-- Buscar por profesional (para reportes y auditoría)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RO_Profesional' AND object_id = OBJECT_ID(N'dbo.Registro_Odontograma')
)
    CREATE INDEX IX_RO_Profesional
        ON dbo.Registro_Odontograma (id_profesional);
GO

PRINT 'Índices de Registro_Odontograma creados correctamente.';
GO

-- ── 3. Verificación final ─────────────────────────────────────
SELECT
    t.name AS tabla,
    c.name AS columna,
    tp.name AS tipo,
    c.is_nullable AS acepta_null
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types tp  ON tp.user_type_id = c.user_type_id
WHERE t.name = 'Registro_Odontograma'
ORDER BY c.column_id;
GO

-- ============================================================
-- SCRIPT: Agregar tablas Nota_Clinica y Control_Postoperatorio  (yeray)
-- Mismo criterio que Registro_Odontograma: reemplazan los campos JSON
-- "notasClinicas" y "controlesPostoperatorios" de Historia_Clinica por
-- tablas reales, consultables con SQL en vez de texto libre parseado en C#.
-- ============================================================

-- ── 1. Crear tabla Nota_Clinica ──────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.Nota_Clinica') AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.Nota_Clinica (
        id_nota        INT IDENTITY(1,1) PRIMARY KEY,
        id_historia    INT NOT NULL,
        id_profesional INT NULL,
        fecha          DATETIME NOT NULL DEFAULT GETDATE(),
        diagnostico    VARCHAR(MAX) NULL,
        procedimiento  VARCHAR(MAX) NULL,
        proxima_cita   VARCHAR(50) NULL,
        estado         VARCHAR(20) NOT NULL DEFAULT 'Realizado',
        CONSTRAINT FK_Nota_Historia
            FOREIGN KEY (id_historia) REFERENCES dbo.Historia_Clinica(id_historia)
            ON DELETE CASCADE,
        CONSTRAINT FK_Nota_Profesional
            FOREIGN KEY (id_profesional) REFERENCES dbo.Profesional(id_profesional)
            ON DELETE SET NULL
    );
    PRINT 'Tabla Nota_Clinica creada correctamente.';
END
ELSE
BEGIN
    PRINT 'Tabla Nota_Clinica ya existe — sin cambios.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_NC_Historia' AND object_id = OBJECT_ID(N'dbo.Nota_Clinica')
)
    CREATE INDEX IX_NC_Historia
        ON dbo.Nota_Clinica (id_historia);
GO

-- ── 2. Crear tabla Control_Postoperatorio ────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.Control_Postoperatorio') AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.Control_Postoperatorio (
        id_control         INT IDENTITY(1,1) PRIMARY KEY,
        id_cita            INT NOT NULL UNIQUE,
        status             VARCHAR(20) NOT NULL DEFAULT 'stable',
        instrucciones_json VARCHAR(MAX) NULL,
        observaciones      VARCHAR(MAX) NULL,
        fecha_registro     DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_ControlPostop_Cita
            FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita)
            ON DELETE CASCADE
    );
    PRINT 'Tabla Control_Postoperatorio creada correctamente.';
END
ELSE
BEGIN
    PRINT 'Tabla Control_Postoperatorio ya existe — sin cambios.';
END
GO

PRINT 'Script de Nota_Clinica y Control_Postoperatorio ejecutado correctamente.';
GO

-- ============================================================
-- Yeray - Tabla Documento_Clinico (2025)
--
-- MOTIVO: la vista st-aux-08-documentos-clinicos devolvía un array vacío
-- porque no había tabla de documentos. Esta tabla almacena los metadatos
-- de cada archivo clínico subido (radiografías, PDFs, consentimientos, etc.).
-- El archivo físico se guarda en wwwroot/uploads/documentos-clinicos/<idHistoria>/.
--
-- RELACIONES:
--   - id_historia → Historia_Clinica (CASCADE delete)
--   - subido_por  → Usuario          (SET NULL)
--
-- ÍNDICE IX_DC_Historia: agiliza el listado de documentos por historia clínica.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Documento_Clinico') AND type = N'U')
BEGIN
    CREATE TABLE Documento_Clinico (
        id_documento    INT IDENTITY(1,1) PRIMARY KEY,
        id_historia     INT NOT NULL,
        subido_por      INT NULL,
        tipo            VARCHAR(100) NOT NULL,
        nombre_original VARCHAR(255) NOT NULL,
        ruta_relativa   VARCHAR(500) NOT NULL,
        content_type    VARCHAR(100) NOT NULL,
        tamano_bytes    BIGINT NOT NULL DEFAULT 0,
        fecha_subida    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        observacion     VARCHAR(500) NULL,

        CONSTRAINT FK_DocClinico_Historia
            FOREIGN KEY (id_historia) REFERENCES Historia_Clinica(id_historia)
            ON DELETE CASCADE,

        CONSTRAINT FK_DocClinico_Usuario
            FOREIGN KEY (subido_por) REFERENCES Usuario(id_usuario)
            ON DELETE SET NULL
    );

    CREATE INDEX IX_DC_Historia ON Documento_Clinico(id_historia);

    PRINT 'Tabla Documento_Clinico creada correctamente.';
END
ELSE
BEGIN
    PRINT 'Tabla Documento_Clinico ya existe — sin cambios.';
END
GO

-- ============================================================
-- CÓDIGO Y CALIDAD — TABLA AUDITORIA, TRIGGERS, FUNCIONES Y PROCEDIMIENTOS
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Auditoria') AND type = N'U')
BEGIN
    CREATE TABLE dbo.Auditoria (
        id_auditoria     INT IDENTITY(1,1) PRIMARY KEY,
        id_usuario       INT NULL,
        tabla_afectada   VARCHAR(100) NOT NULL,
        id_registro      INT NULL,
        accion           VARCHAR(30) NOT NULL,
        ip_origen        VARCHAR(45) NULL,
        datos_anteriores NVARCHAR(MAX) NULL,
        datos_nuevos     NVARCHAR(MAX) NULL,
        descripcion      NVARCHAR(500) NULL,
        fecha            DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_Auditoria_Usuario
            FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario)
            ON DELETE SET NULL
    );
    CREATE INDEX IX_Auditoria_Tabla_Registro ON dbo.Auditoria(tabla_afectada, id_registro);
    PRINT 'Tabla Auditoria creada correctamente.';
END
GO

-- 1. Auditoría de Cita
-- La aplicación registra la auditoría con el usuario autenticado y su IP. No se
-- duplica aquí, porque un trigger no conoce al usuario real que hizo el cambio.
IF OBJECT_ID('dbo.TR_Cita_Auditoria_Estado', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Cita_Auditoria_Estado;
GO
PRINT 'TR_Cita_Auditoria_Estado eliminado: la auditoría se registra en la aplicación.';
GO

-- 2. FUNCIÓN: Verificación de disponibilidad de profesional
IF OBJECT_ID('dbo.fn_VerificarDisponibilidadProfesional', 'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_VerificarDisponibilidadProfesional;
GO

CREATE FUNCTION dbo.fn_VerificarDisponibilidadProfesional
(
    @IdProfesional INT,
    @FechaHoraInicio DATETIME,
    @DuracionMinutos INT = 60,
    @IdCitaExcluir INT = NULL
)
RETURNS BIT
AS
BEGIN
    DECLARE @FechaHoraFin DATETIME = DATEADD(MINUTE, @DuracionMinutos, @FechaHoraInicio);
    DECLARE @Fecha DATE = CAST(@FechaHoraInicio AS DATE);

    -- Verificar que el profesional esté activo
    IF EXISTS (SELECT 1 FROM dbo.Profesional WHERE id_profesional = @IdProfesional AND estado <> 'activo')
        RETURN 0;

    -- Si el profesional tiene horarios configurados, la cita debe caber en uno.
    DECLARE @DiaSemana VARCHAR(12) = CASE ((DATEDIFF(DAY, '19000101', @Fecha) % 7 + 7) % 7)
        WHEN 0 THEN 'Lunes' WHEN 1 THEN 'Martes' WHEN 2 THEN 'Miercoles'
        WHEN 3 THEN 'Jueves' WHEN 4 THEN 'Viernes' WHEN 5 THEN 'Sabado' ELSE 'Domingo' END;
    IF EXISTS (SELECT 1 FROM dbo.Horario_Profesional WHERE id_profesional = @IdProfesional)
       AND NOT EXISTS (
           SELECT 1 FROM dbo.Horario_Profesional
           WHERE id_profesional = @IdProfesional AND activo = 1
             AND dia_semana = @DiaSemana
             AND hora_inicio <= CAST(@FechaHoraInicio AS TIME)
             AND hora_fin >= CAST(@FechaHoraFin AS TIME)
       )
        RETURN 0;

    -- Verificar ausencias registradas
    IF EXISTS (
        SELECT 1 FROM dbo.Ausencia_Profesional
        WHERE id_profesional = @IdProfesional
          AND fecha_inicio <= @Fecha
          AND fecha_fin >= @Fecha
    )
        RETURN 0;

    -- Verificar bloqueos de agenda
    IF EXISTS (
        SELECT 1 FROM dbo.Bloqueo_Profesional
        WHERE id_profesional = @IdProfesional
          AND fecha_inicio < @FechaHoraFin
          AND fecha_fin > @FechaHoraInicio
    )
        RETURN 0;

    -- Verificar citas solapadas existentes (no canceladas)
    IF EXISTS (
        SELECT 1 FROM dbo.Cita
        WHERE id_profesional = @IdProfesional
          AND (@IdCitaExcluir IS NULL OR id_cita <> @IdCitaExcluir)
          AND LOWER(ISNULL(estado, '')) NOT IN ('cancelada', 'cancelado')
          AND fecha_hora < @FechaHoraFin
          AND DATEADD(MINUTE, ISNULL(duracion_minutos, @DuracionMinutos), fecha_hora) > @FechaHoraInicio
    )
        RETURN 0;

    RETURN 1;
END
GO
PRINT 'Función fn_VerificarDisponibilidadProfesional creada correctamente.';
GO

-- 3. PROCEDIMIENTO ALMACENADO: Registro transaccional de cita con validación previa
IF OBJECT_ID('dbo.sp_RegistrarCitaConValidacion', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RegistrarCitaConValidacion;
GO

CREATE PROCEDURE dbo.sp_RegistrarCitaConValidacion
(
    @IdPaciente INT,
    @IdProfesional INT = NULL,
    @IdServicio INT = NULL,
    @IdConsultorio INT = NULL,
    @FechaHora DATETIME,
    @DuracionMinutos INT = 60,
    @Estado VARCHAR(30) = 'Agendada',
    @Notas VARCHAR(MAX) = NULL,
    @CreadoPor INT = NULL,
    @IdCitaCreada INT OUTPUT,
    @MensajeError VARCHAR(250) OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @IdCitaCreada = 0;
    SET @MensajeError = '';

    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    IF @IdServicio IS NOT NULL
    BEGIN
        SELECT @DuracionMinutos = duracion_minutos
        FROM dbo.Servicio
        WHERE id_servicio = @IdServicio AND duracion_minutos > 0;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.Paciente WHERE id_paciente = @IdPaciente AND estado = 'activo')
    BEGIN
        SET @MensajeError = 'El paciente especificado no existe o no se encuentra activo.';
        ROLLBACK TRANSACTION;
        RETURN -1;
    END

    IF @IdProfesional IS NOT NULL AND @IdProfesional > 0
    BEGIN
        IF dbo.fn_VerificarDisponibilidadProfesional(@IdProfesional, @FechaHora, @DuracionMinutos, NULL) = 0
        BEGIN
            SET @MensajeError = 'El profesional seleccionado no tiene disponibilidad en la fecha y horario solicitados.';
            ROLLBACK TRANSACTION;
            RETURN -2;
        END
    END

    IF @IdConsultorio IS NOT NULL AND EXISTS (
        SELECT 1 FROM dbo.Cita
        WHERE id_consultorio = @IdConsultorio
          AND estado NOT IN ('Cancelada', 'Cancelado', 'cancelada', 'cancelado')
          AND fecha_hora < DATEADD(MINUTE, @DuracionMinutos, @FechaHora)
          AND DATEADD(MINUTE, ISNULL(duracion_minutos, @DuracionMinutos), fecha_hora) > @FechaHora
    )
    BEGIN
        SET @MensajeError = 'El consultorio seleccionado ya está ocupado en ese horario.';
        ROLLBACK TRANSACTION;
        RETURN -3;
    END

    DECLARE @IdEstado INT = NULL;
    SELECT TOP 1 @IdEstado = id_estado FROM dbo.Estado_Cita WHERE LOWER(nombre_estado) = LOWER(@Estado);

    BEGIN TRY
        INSERT INTO dbo.Cita (
            id_paciente,
            id_profesional,
            id_servicio,
            id_consultorio,
            fecha_hora,
            duracion_minutos,
            estado,
            id_estado,
            notas,
            fecha_creacion,
            creado_por
        )
        VALUES (
            @IdPaciente,
            @IdProfesional,
            @IdServicio,
            @IdConsultorio,
            @FechaHora,
            @DuracionMinutos,
            @Estado,
            @IdEstado,
            @Notas,
            GETDATE(),
            @CreadoPor
        );

        SET @IdCitaCreada = SCOPE_IDENTITY();
        SET @MensajeError = 'Cita registrada exitosamente.';
        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        SET @MensajeError = ERROR_MESSAGE();
        RETURN -99;
    END CATCH
END
GO
PRINT 'Procedimiento almacenado sp_RegistrarCitaConValidacion creado correctamente.';
GO

PRINT 'Script ejecutado correctamente.';
GO

-- ============================================================
-- Yeray (2025) - Tabla Alergia_Paciente
--
-- MOTIVO: convierte el campo de texto libre Paciente.Alergias en filas
-- consultables con severidad, tipo y reacción. Coexiste con el campo
-- de texto (no lo reemplaza) para no romper el código existente.
--
-- RELACIONES:
--   id_paciente → Paciente (CASCADE delete)
--
-- CHECKS:
--   tipo     : medicamento | alimento | ambiental | latex | otro
--   severidad: leve | moderada | grave
--
-- ÍNDICE IX_AP_Paciente: lista alergias de un paciente sin full-scan.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.objects
               WHERE object_id = OBJECT_ID(N'dbo.Alergia_Paciente') AND type = N'U')
BEGIN
    CREATE TABLE Alergia_Paciente (
        id_alergia      INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente     INT NOT NULL,
        sustancia       VARCHAR(150) NOT NULL,
        tipo            VARCHAR(15)  NOT NULL DEFAULT 'otro'
                        CHECK (tipo IN ('medicamento','alimento','ambiental','latex','otro')),
        severidad       VARCHAR(10)  NOT NULL DEFAULT 'leve'
                        CHECK (severidad IN ('leve','moderada','grave')),
        reaccion        VARCHAR(300) NULL,
        fecha_registro  DATETIME2   NOT NULL DEFAULT GETUTCDATE(),
        activa          BIT         NOT NULL DEFAULT 1,

        CONSTRAINT FK_AP_Paciente
            FOREIGN KEY (id_paciente) REFERENCES Paciente(id_paciente)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_AP_Paciente ON Alergia_Paciente(id_paciente);

    PRINT 'Tabla Alergia_Paciente creada correctamente.';
END
ELSE
BEGIN
    PRINT 'Tabla Alergia_Paciente ya existe — sin cambios.';
END
GO

PRINT 'Script completo ejecutado correctamente.';
GO

-- ============================================================
-- 8) AMPLIACION OPERATIVA DE CITAS Y PROFESIONALES
-- Estructuras idempotentes para agenda, trazabilidad y avisos.
-- ============================================================

-- La duración queda congelada en la cita para que un cambio posterior del
-- servicio no altere retrospectivamente las citas ya creadas.
IF COL_LENGTH(N'dbo.Cita', N'duracion_minutos') IS NULL
    ALTER TABLE dbo.Cita ADD duracion_minutos INT NOT NULL
        CONSTRAINT DF_Cita_DuracionMinutos DEFAULT 60;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Cita_DuracionMinutos')
    ALTER TABLE dbo.Cita ADD CONSTRAINT CK_Cita_DuracionMinutos CHECK (duracion_minutos > 0 AND duracion_minutos <= 1440);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cita_CreadoPor')
    ALTER TABLE dbo.Cita ADD CONSTRAINT FK_Cita_CreadoPor
        FOREIGN KEY (creado_por) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL;
GO

-- Un usuario solo puede representar a un profesional.
;WITH ProfesionalesDuplicados AS
(
    SELECT id_profesional,
           ROW_NUMBER() OVER (PARTITION BY id_usuario ORDER BY id_profesional) AS orden
    FROM dbo.Profesional
    WHERE id_usuario IS NOT NULL
)
UPDATE p
   SET id_usuario = NULL
FROM dbo.Profesional p
INNER JOIN ProfesionalesDuplicados d ON d.id_profesional = p.id_profesional
WHERE d.orden > 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Profesional_Usuario')
    CREATE UNIQUE INDEX UQ_Profesional_Usuario
        ON dbo.Profesional(id_usuario) WHERE id_usuario IS NOT NULL;
GO

;WITH EspecialidadesDuplicadas AS
(
    SELECT id_especialidad,
           ROW_NUMBER() OVER (PARTITION BY nombre ORDER BY id_especialidad) AS orden
    FROM dbo.Especialidad
)
DELETE e
FROM dbo.Especialidad e
INNER JOIN EspecialidadesDuplicadas d ON d.id_especialidad = e.id_especialidad
WHERE d.orden > 1
  AND NOT EXISTS (SELECT 1 FROM dbo.Profesional_Especialidad pe WHERE pe.id_especialidad = e.id_especialidad);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Especialidad_Nombre')
    CREATE UNIQUE INDEX UQ_Especialidad_Nombre ON dbo.Especialidad(nombre);
GO

-- Solo una especialidad principal por profesional.
;WITH EspecialidadPrincipalDuplicada AS
(
    SELECT id_profesional, id_especialidad,
           ROW_NUMBER() OVER (PARTITION BY id_profesional ORDER BY id_especialidad) AS orden
    FROM dbo.Profesional_Especialidad
    WHERE principal = 1
)
UPDATE pe
   SET principal = 0
FROM dbo.Profesional_Especialidad pe
INNER JOIN EspecialidadPrincipalDuplicada d
    ON d.id_profesional = pe.id_profesional
   AND d.id_especialidad = pe.id_especialidad
WHERE d.orden > 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Profesional_Especialidad_Principal')
    CREATE UNIQUE INDEX UQ_Profesional_Especialidad_Principal
        ON dbo.Profesional_Especialidad(id_profesional) WHERE principal = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_HorarioProfesional_Rango')
    ALTER TABLE dbo.Horario_Profesional ADD CONSTRAINT CK_HorarioProfesional_Rango
        CHECK (hora_fin > hora_inicio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AusenciaProfesional_Rango')
    ALTER TABLE dbo.Ausencia_Profesional ADD CONSTRAINT CK_AusenciaProfesional_Rango
        CHECK (fecha_fin >= fecha_inicio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_BloqueoProfesional_Rango')
    ALTER TABLE dbo.Bloqueo_Profesional ADD CONSTRAINT CK_BloqueoProfesional_Rango
        CHECK (fecha_fin > fecha_inicio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AusenciaProfesional_AprobadoPor')
    ALTER TABLE dbo.Ausencia_Profesional ADD CONSTRAINT FK_AusenciaProfesional_AprobadoPor
        FOREIGN KEY (aprobado_por) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BloqueoProfesional_AprobadoPor')
    ALTER TABLE dbo.Bloqueo_Profesional ADD CONSTRAINT FK_BloqueoProfesional_AprobadoPor
        FOREIGN KEY (aprobado_por) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL;
GO

-- La interfaz usa no-disponible; se conserva mantenimiento como estado válido.
DECLARE @ck_consultorio NVARCHAR(256);
SELECT @ck_consultorio = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Consultorio')
  AND definition LIKE '%estado%';
IF @ck_consultorio IS NOT NULL
    EXEC(N'ALTER TABLE dbo.Consultorio DROP CONSTRAINT [' + @ck_consultorio + N']');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Consultorio_Estado')
    ALTER TABLE dbo.Consultorio ADD CONSTRAINT CK_Consultorio_Estado
        CHECK (estado IN ('disponible','ocupado','mantenimiento','no-disponible'));
GO

-- Historial funcional de cambios de estado, asistencia y reasignaciones.
IF OBJECT_ID(N'dbo.Cita_Historial_Estado', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cita_Historial_Estado (
        id_historial INT IDENTITY(1,1) PRIMARY KEY,
        id_cita INT NOT NULL,
        id_estado INT NULL,
        estado_texto VARCHAR(50) NOT NULL,
        id_usuario INT NULL,
        motivo VARCHAR(500) NULL,
        fecha_cambio DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CitaHistorial_Cita FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita) ON DELETE CASCADE,
        CONSTRAINT FK_CitaHistorial_Estado FOREIGN KEY (id_estado) REFERENCES dbo.Estado_Cita(id_estado) ON DELETE SET NULL,
        CONSTRAINT FK_CitaHistorial_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
    CREATE INDEX IX_CitaHistorial_Cita_Fecha ON dbo.Cita_Historial_Estado(id_cita, fecha_cambio DESC);
END
GO

IF OBJECT_ID(N'dbo.TR_Cita_Historial_Estado', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Cita_Historial_Estado;
GO
CREATE TRIGGER dbo.TR_Cita_Historial_Estado ON dbo.Cita
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Cita_Historial_Estado (id_cita, id_estado, estado_texto, id_usuario, motivo)
    SELECT i.id_cita, i.id_estado, i.estado, NULL,
           CASE WHEN d.id_cita IS NULL THEN 'Creación de la cita' ELSE 'Cambio registrado en la cita' END
    FROM inserted i
    LEFT JOIN deleted d ON d.id_cita = i.id_cita
    WHERE d.id_cita IS NULL
       OR ISNULL(i.estado, '') <> ISNULL(d.estado, '')
       OR ISNULL(i.id_estado, 0) <> ISNULL(d.id_estado, 0);
END
GO

-- Mantiene compatibles las integraciones antiguas que todavía envían estado
-- textual, sin permitir que difiera del catálogo Estado_Cita.
IF OBJECT_ID(N'dbo.TR_Cita_SincronizarEstado', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Cita_SincronizarEstado;
GO
CREATE TRIGGER dbo.TR_Cita_SincronizarEstado ON dbo.Cita
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF TRIGGER_NESTLEVEL() > 1 RETURN;

    -- No permitir que escrituras directas dejen estado textual e id_estado
    -- divergentes o valores que no existan en el catálogo.
    IF EXISTS (
        SELECT 1
        FROM inserted i
        LEFT JOIN dbo.Estado_Cita por_id ON por_id.id_estado = i.id_estado
        LEFT JOIN dbo.Estado_Cita por_nombre ON LOWER(por_nombre.nombre_estado) = LOWER(i.estado)
        WHERE (i.id_estado IS NULL AND por_nombre.id_estado IS NULL)
           OR (i.id_estado IS NOT NULL AND por_id.id_estado IS NULL)
           OR (i.id_estado IS NOT NULL AND por_nombre.id_estado IS NOT NULL AND por_id.id_estado <> por_nombre.id_estado)
    )
        THROW 50001, 'El estado de la cita debe existir en Estado_Cita y coincidir con id_estado.', 1;

    UPDATE c
       SET estado = ec.nombre_estado
    FROM dbo.Cita c
    INNER JOIN inserted i ON i.id_cita = c.id_cita
    INNER JOIN dbo.Estado_Cita ec ON ec.id_estado = i.id_estado
    WHERE i.id_estado IS NOT NULL AND ISNULL(c.estado, '') <> ec.nombre_estado;

    UPDATE c
       SET id_estado = ec.id_estado
    FROM dbo.Cita c
    INNER JOIN inserted i ON i.id_cita = c.id_cita
    INNER JOIN dbo.Estado_Cita ec ON LOWER(ec.nombre_estado) = LOWER(i.estado)
    WHERE i.id_estado IS NULL OR i.id_estado <> ec.id_estado;
END
GO

-- Notificaciones internas y trazabilidad de recordatorios externos.
IF OBJECT_ID(N'dbo.Notificacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notificacion (
        id_notificacion INT IDENTITY(1,1) PRIMARY KEY,
        id_paciente INT NULL,
        id_cita INT NULL,
        tipo VARCHAR(30) NOT NULL CHECK (tipo IN ('recordatorio','confirmacion','cancelacion','mensaje','alerta')),
        titulo VARCHAR(200) NOT NULL,
        contenido VARCHAR(MAX) NOT NULL,
        canal VARCHAR(20) NOT NULL DEFAULT 'interno' CHECK (canal IN ('interno','email','sms','whatsapp')),
        estado VARCHAR(20) NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente','enviada','leida','fallida','cancelada')),
        fecha_programada DATETIME2 NULL,
        fecha_envio DATETIME2 NULL,
        fecha_lectura DATETIME2 NULL,
        intentos INT NOT NULL DEFAULT 0,
        ultimo_error VARCHAR(500) NULL,
        creada_en DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Notificacion_Paciente FOREIGN KEY (id_paciente) REFERENCES dbo.Paciente(id_paciente) ON DELETE CASCADE,
        CONSTRAINT FK_Notificacion_Cita FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita) ON DELETE SET NULL
    );
    CREATE INDEX IX_Notificacion_Paciente_Estado ON dbo.Notificacion(id_paciente, estado, creada_en DESC);
    CREATE INDEX IX_Notificacion_Programada ON dbo.Notificacion(estado, fecha_programada);
END
GO

IF OBJECT_ID(N'dbo.Recordatorio_Cita', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Recordatorio_Cita (
        id_recordatorio INT IDENTITY(1,1) PRIMARY KEY,
        id_cita INT NOT NULL,
        canal VARCHAR(20) NOT NULL CHECK (canal IN ('email','sms','whatsapp','interno')),
        estado VARCHAR(20) NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente','enviado','fallido','cancelado')),
        programado_para DATETIME2 NOT NULL,
        enviado_en DATETIME2 NULL,
        intentos INT NOT NULL DEFAULT 0,
        ultimo_error VARCHAR(500) NULL,
        creado_en DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Recordatorio_Cita FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita) ON DELETE CASCADE
    );
    CREATE INDEX IX_Recordatorio_Estado_Fecha ON dbo.Recordatorio_Cita(estado, programado_para);
END
GO

-- Historial del estado físico del consultorio.
IF OBJECT_ID(N'dbo.Consultorio_Historial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consultorio_Historial (
        id_historial INT IDENTITY(1,1) PRIMARY KEY,
        id_consultorio INT NOT NULL,
        estado VARCHAR(20) NOT NULL,
        id_usuario INT NULL,
        motivo VARCHAR(500) NULL,
        fecha_cambio DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ConsultorioHistorial_Consultorio FOREIGN KEY (id_consultorio) REFERENCES dbo.Consultorio(id_consultorio) ON DELETE CASCADE,
        CONSTRAINT FK_ConsultorioHistorial_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
    CREATE INDEX IX_ConsultorioHistorial_Consultorio_Fecha ON dbo.Consultorio_Historial(id_consultorio, fecha_cambio DESC);
END
GO

-- Persistencia de asistencia procedural y estado operativo del consultorio.
IF OBJECT_ID(N'dbo.Asistencia_Procedimiento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Asistencia_Procedimiento (
        id_asistencia INT IDENTITY(1,1) PRIMARY KEY,
        id_cita INT NOT NULL,
        minutos INT NOT NULL DEFAULT 0,
        inicio DATETIME2 NOT NULL,
        limpieza BIT NOT NULL DEFAULT 0,
        esterilizacion BIT NOT NULL DEFAULT 0,
        equipos BIT NOT NULL DEFAULT 0,
        actualizado_por INT NULL,
        actualizado_en DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AsistenciaProcedimiento_Cita UNIQUE (id_cita),
        CONSTRAINT FK_AsistenciaProcedimiento_Cita FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita) ON DELETE CASCADE,
        CONSTRAINT FK_AsistenciaProcedimiento_Usuario FOREIGN KEY (actualizado_por) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
END
GO

IF OBJECT_ID(N'dbo.Consultorio_Estado_Operativo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Consultorio_Estado_Operativo (
        id_estado_operativo INT IDENTITY(1,1) PRIMARY KEY,
        id_consultorio INT NOT NULL,
        checklist_json NVARCHAR(MAX) NOT NULL DEFAULT N'[]',
        observaciones NVARCHAR(2000) NULL,
        actualizado_por INT NULL,
        actualizado_en DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ConsultorioEstadoOperativo_Consultorio UNIQUE (id_consultorio),
        CONSTRAINT FK_ConsultorioEstadoOperativo_Consultorio FOREIGN KEY (id_consultorio) REFERENCES dbo.Consultorio(id_consultorio) ON DELETE CASCADE,
        CONSTRAINT FK_ConsultorioEstadoOperativo_Usuario FOREIGN KEY (actualizado_por) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
END
GO

PRINT 'Ampliación operativa de citas y profesionales aplicada correctamente.';
GO

-- ============================================================
-- VISTAS DE OPTIMIZACIÓN PARA GESTIÓN DE CITAS Y PROFESIONALES
-- Fecha de creación: 2025-01-XX
-- Propósito: Optimizar consultas frecuentes mediante vistas pre-calculadas
-- ============================================================

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   CREACIÓN DE VISTAS DE OPTIMIZACIÓN';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO

-- ============================================================
-- VISTA 1: vw_Citas_Dashboard
-- Propósito: Optimizar consultas del dashboard de citas
-- Uso: CitaService, GestionCitasController
-- Beneficio: Reduce 6 JOINs repetitivos en cada consulta
-- ============================================================

IF OBJECT_ID('dbo.vw_Citas_Dashboard', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Citas_Dashboard;
GO

CREATE VIEW dbo.vw_Citas_Dashboard
AS
SELECT 
    -- Cita
    c.id_cita,
    c.fecha_hora,
    c.estado,
    c.duracion_minutos,
    c.notas,
    c.motivo_consulta,
    c.notas_previas,
    c.tipo_cita,
    c.fecha_creacion,
    
    -- Paciente
    pac.id_paciente,
    pac.nombres + ' ' + pac.apellidos AS nombre_paciente,
    pac.documento,
    pac.tipo_documento,
    pac.telefono AS telefono_paciente,
    pac.correo AS correo_paciente,
    pac.genero,
    pac.fecha_nacimiento,
    DATEDIFF(YEAR, pac.fecha_nacimiento, GETDATE()) AS edad_paciente,
    
    -- Profesional
    prof.id_profesional,
    prof.nombres + ' ' + prof.apellidos AS nombre_profesional,
    prof.registro_medico,
    prof.estado AS estado_profesional,
    prof.telefono AS telefono_profesional,
    
    -- Especialidad principal del profesional
    (SELECT TOP 1 e.nombre 
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = prof.id_profesional AND pe.principal = 1
    ) AS especialidad_profesional,
    
    -- Servicio
    s.id_servicio,
    s.nombre AS nombre_servicio,
    s.descripcion AS descripcion_servicio,
    s.precio AS precio_servicio,
    s.duracion_minutos AS duracion_servicio,
    s.categoria AS categoria_servicio,
    
    -- Consultorio
    cons.id_consultorio,
    cons.nombre AS nombre_consultorio,
    cons.ubicacion AS ubicacion_consultorio,
    cons.tipo AS tipo_consultorio,
    cons.estado AS estado_consultorio,
    
    -- Estado catálogo
    ec.id_estado AS id_estado_catalogo,
    ec.nombre_estado AS nombre_estado_catalogo,
    ec.descripcion AS descripcion_estado,
    
    -- Usuario que creó
    u.id_usuario AS creado_por_id,
    u.nombre + ' ' + u.apellidos AS creado_por_nombre,
    u.correo AS correo_creador,
    
    -- Indicadores calculados
    CASE 
        WHEN c.fecha_hora < GETDATE() AND c.estado IN ('Agendada', 'Confirmada', 'Programada') 
        THEN 1 ELSE 0 
    END AS es_atrasada,
    
    CASE 
        WHEN c.fecha_hora BETWEEN GETDATE() AND DATEADD(HOUR, 24, GETDATE()) 
        AND c.estado IN ('Agendada', 'Confirmada', 'Programada')
        THEN 1 ELSE 0 
    END AS es_proxima_24h,
    
    DATEDIFF(MINUTE, GETDATE(), c.fecha_hora) AS minutos_hasta_cita

FROM Cita c
INNER JOIN Paciente pac ON c.id_paciente = pac.id_paciente
LEFT JOIN Profesional prof ON c.id_profesional = prof.id_profesional
LEFT JOIN Servicio s ON c.id_servicio = s.id_servicio
LEFT JOIN Consultorio cons ON c.id_consultorio = cons.id_consultorio
LEFT JOIN Estado_Cita ec ON c.id_estado = ec.id_estado
LEFT JOIN Usuario u ON c.creado_por = u.id_usuario;
GO

PRINT '✓ Vista vw_Citas_Dashboard creada exitosamente';
GO

-- ============================================================
-- VISTA 2: vw_Profesionales_Completo
-- Propósito: Optimizar listados de profesionales con relaciones
-- Uso: ProfesionalService, GestionProfesionalesController
-- Beneficio: Pre-calcula contadores de horarios, ausencias, citas
-- ============================================================

IF OBJECT_ID('dbo.vw_Profesionales_Completo', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Profesionales_Completo;
GO

CREATE VIEW dbo.vw_Profesionales_Completo
AS
SELECT 
    -- Profesional
    p.id_profesional,
    p.nombres,
    p.apellidos,
    p.nombres + ' ' + p.apellidos AS nombre_completo,
    p.registro_medico,
    p.categoria,
    p.telefono,
    p.descripcion,
    p.estado,
    p.fecha_ingreso,
    
    -- Usuario asociado
    u.id_usuario,
    u.correo,
    u.estado AS estado_usuario,
    u.fecha_creacion AS fecha_creacion_usuario,
    u.ultimo_login,
    u.intentos_fallidos,
    
    -- Rol
    r.id_rol,
    r.nombre_rol,
    
    -- Especialidad principal
    (SELECT TOP 1 e.id_especialidad
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = p.id_profesional AND pe.principal = 1
    ) AS id_especialidad_principal,
    
    (SELECT TOP 1 e.nombre
     FROM Profesional_Especialidad pe
     INNER JOIN Especialidad e ON pe.id_especialidad = e.id_especialidad
     WHERE pe.id_profesional = p.id_profesional AND pe.principal = 1
    ) AS nombre_especialidad_principal,
    
    -- Contadores de disponibilidad
    (SELECT COUNT(*) 
     FROM Horario_Profesional hp 
     WHERE hp.id_profesional = p.id_profesional AND hp.activo = 1
    ) AS total_horarios_activos,
    
    (SELECT COUNT(*) 
     FROM Ausencia_Profesional ap 
     WHERE ap.id_profesional = p.id_profesional 
       AND ap.fecha_fin >= CAST(GETDATE() AS DATE)
    ) AS total_ausencias_vigentes,
    
    (SELECT COUNT(*) 
     FROM Bloqueo_Profesional bp 
     WHERE bp.id_profesional = p.id_profesional 
       AND bp.fecha_fin > GETDATE()
    ) AS total_bloqueos_activos,
    
    -- Contadores de citas
    (SELECT COUNT(*) 
     FROM Cita c 
     WHERE c.id_profesional = p.id_profesional 
       AND c.fecha_hora >= CAST(GETDATE() AS DATE)
       AND c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
    ) AS total_citas_pendientes,
    
    (SELECT COUNT(*) 
     FROM Cita c 
     WHERE c.id_profesional = p.id_profesional 
       AND YEAR(c.fecha_hora) = YEAR(GETDATE())
       AND MONTH(c.fecha_hora) = MONTH(GETDATE())
    ) AS total_citas_mes_actual,
    
    -- Servicios que ofrece
    (SELECT COUNT(*) 
     FROM Profesional_Servicio ps 
     WHERE ps.id_profesional = p.id_profesional AND ps.activo = 1
    ) AS total_servicios_activos,
    
    -- Indicadores de disponibilidad
    CASE 
        WHEN p.estado = 'activo' 
         AND NOT EXISTS (
             SELECT 1 FROM Ausencia_Profesional ap 
             WHERE ap.id_profesional = p.id_profesional 
               AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
         )
         AND NOT EXISTS (
             SELECT 1 FROM Bloqueo_Profesional bp 
             WHERE bp.id_profesional = p.id_profesional 
               AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
         )
        THEN 1 ELSE 0 
    END AS disponible_ahora

FROM Profesional p
LEFT JOIN Usuario u ON p.id_usuario = u.id_usuario
LEFT JOIN Rol r ON u.id_rol = r.id_rol;
GO

PRINT '✓ Vista vw_Profesionales_Completo creada exitosamente';
GO

-- ============================================================
-- VISTA 3: vw_Disponibilidad_Profesional
-- Propósito: Consultar disponibilidad completa de profesionales
-- Uso: CitaService.ValidarDisponibilidadProfesionalAsync
-- Beneficio: Centraliza validación de horarios, ausencias y bloqueos
-- ============================================================

IF OBJECT_ID('dbo.vw_Disponibilidad_Profesional', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Disponibilidad_Profesional;
GO

CREATE VIEW dbo.vw_Disponibilidad_Profesional
AS
SELECT 
    -- Profesional
    p.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,
    p.estado AS estado_profesional,
    p.registro_medico,
    
    -- Horarios semanales
    hp.id_horario,
    hp.dia_semana,
    hp.hora_inicio,
    hp.hora_fin,
    hp.activo AS horario_activo,
    
    -- Tiene ausencia hoy
    CASE WHEN EXISTS (
        SELECT 1 FROM Ausencia_Profesional ap 
        WHERE ap.id_profesional = p.id_profesional 
          AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
    ) THEN 1 ELSE 0 END AS tiene_ausencia_hoy,
    
    -- Tipo de ausencia actual
    (SELECT TOP 1 ap.tipo 
     FROM Ausencia_Profesional ap 
     WHERE ap.id_profesional = p.id_profesional 
       AND CAST(GETDATE() AS DATE) BETWEEN ap.fecha_inicio AND ap.fecha_fin
    ) AS tipo_ausencia_actual,
    
    -- Tiene bloqueo ahora
    CASE WHEN EXISTS (
        SELECT 1 FROM Bloqueo_Profesional bp 
        WHERE bp.id_profesional = p.id_profesional 
          AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
    ) THEN 1 ELSE 0 END AS tiene_bloqueo_ahora,
    
    -- Motivo del bloqueo actual
    (SELECT TOP 1 bp.motivo 
     FROM Bloqueo_Profesional bp 
     WHERE bp.id_profesional = p.id_profesional 
       AND GETDATE() BETWEEN bp.fecha_inicio AND bp.fecha_fin
    ) AS motivo_bloqueo_actual,
    
    -- Próxima cita
    (SELECT MIN(c.fecha_hora)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.fecha_hora > GETDATE()
       AND c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
    ) AS proxima_cita,
    
    -- Última cita atendida
    (SELECT MAX(c.fecha_hora)
     FROM Cita c
     WHERE c.id_profesional = p.id_profesional
       AND c.estado IN ('Atendida', 'Completada')
    ) AS ultima_cita_atendida

FROM Profesional p
LEFT JOIN Horario_Profesional hp ON p.id_profesional = hp.id_profesional
WHERE p.estado IN ('activo', 'vacaciones');
GO

PRINT '✓ Vista vw_Disponibilidad_Profesional creada exitosamente';
GO

-- ============================================================
-- VISTA 4: vw_Citas_Conflictos
-- Propósito: Detectar solapamientos de agenda
-- Uso: CitaService.VerificarConflictoCompletoAsync
-- Beneficio: Previene doble agendamiento de recursos
-- ============================================================

IF OBJECT_ID('dbo.vw_Citas_Conflictos', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Citas_Conflictos;
GO

CREATE VIEW dbo.vw_Citas_Conflictos
AS
WITH CitasConFin AS (
    SELECT 
        c.id_cita,
        c.id_profesional,
        c.id_paciente,
        c.id_consultorio,
        c.fecha_hora AS inicio,
        DATEADD(MINUTE, ISNULL(c.duracion_minutos, 60), c.fecha_hora) AS fin,
        c.estado
    FROM Cita c
    WHERE c.estado NOT IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado')
      AND c.fecha_hora >= CAST(GETDATE() AS DATE)
)
SELECT 
    c1.id_cita AS cita1_id,
    c2.id_cita AS cita2_id,
    
    -- Datos del conflicto
    CASE 
        WHEN c1.id_profesional = c2.id_profesional AND c1.id_profesional IS NOT NULL 
        THEN 'Profesional'
        WHEN c1.id_paciente = c2.id_paciente THEN 'Paciente'
        WHEN c1.id_consultorio = c2.id_consultorio AND c1.id_consultorio IS NOT NULL 
        THEN 'Consultorio'
    END AS tipo_conflicto,
    
    -- Profesional
    c1.id_profesional,
    p.nombres + ' ' + p.apellidos AS nombre_profesional,
    
    -- Paciente
    c1.id_paciente,
    pac.nombres + ' ' + pac.apellidos AS nombre_paciente,
    
    -- Consultorio
    c1.id_consultorio,
    cons.nombre AS nombre_consultorio,
    
    -- Fechas de las citas
    c1.inicio AS cita1_inicio,
    c1.fin AS cita1_fin,
    c2.inicio AS cita2_inicio,
    c2.fin AS cita2_fin,
    
    -- Minutos de solapamiento
    DATEDIFF(
        MINUTE, 
        CASE WHEN c1.inicio > c2.inicio THEN c1.inicio ELSE c2.inicio END,
        CASE WHEN c1.fin < c2.fin THEN c1.fin ELSE c2.fin END
    ) AS minutos_solapamiento,
    
    -- Estados
    c1.estado AS cita1_estado,
    c2.estado AS cita2_estado

FROM CitasConFin c1
INNER JOIN CitasConFin c2 
    ON c1.id_cita < c2.id_cita
   AND (
       (c1.id_profesional = c2.id_profesional AND c1.id_profesional IS NOT NULL)
       OR c1.id_paciente = c2.id_paciente
       OR (c1.id_consultorio = c2.id_consultorio AND c1.id_consultorio IS NOT NULL)
   )
   AND c1.inicio < c2.fin
   AND c2.inicio < c1.fin
LEFT JOIN Profesional p ON c1.id_profesional = p.id_profesional
LEFT JOIN Paciente pac ON c1.id_paciente = pac.id_paciente
LEFT JOIN Consultorio cons ON c1.id_consultorio = cons.id_consultorio;
GO

PRINT '✓ Vista vw_Citas_Conflictos creada exitosamente';
GO

-- ============================================================
-- VISTA 5: vw_KPI_Citas_Resumen
-- Propósito: Pre-calcular KPIs mensuales
-- Uso: CitasDashboardService, Reportes
-- Beneficio: Evita recalcular agregaciones en cada consulta
-- ============================================================

IF OBJECT_ID('dbo.vw_KPI_Citas_Resumen', 'V') IS NOT NULL
    DROP VIEW dbo.vw_KPI_Citas_Resumen;
GO

CREATE VIEW dbo.vw_KPI_Citas_Resumen
AS
SELECT 
    YEAR(c.fecha_hora) AS anio,
    MONTH(c.fecha_hora) AS mes,
    CAST(DATEFROMPARTS(YEAR(c.fecha_hora), MONTH(c.fecha_hora), 1) AS DATE) AS primer_dia_mes,
    
    -- Totales generales
    COUNT(*) AS total_citas,
    COUNT(DISTINCT c.id_paciente) AS total_pacientes_unicos,
    COUNT(DISTINCT c.id_profesional) AS total_profesionales_activos,
    COUNT(DISTINCT c.id_consultorio) AS total_consultorios_usados,
    
    -- Por Estado normalizado
    SUM(CASE 
        WHEN c.estado IN ('Agendada', 'Confirmada', 'Programada', 'agendada', 'confirmada', 'programada') 
        THEN 1 ELSE 0 
    END) AS total_programadas,
    
    SUM(CASE 
        WHEN c.estado IN ('Atendida', 'Completada', 'atendida', 'completada') 
        THEN 1 ELSE 0 
    END) AS total_atendidas,
    
    SUM(CASE 
        WHEN c.estado IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado') 
        THEN 1 ELSE 0 
    END) AS total_canceladas,
    
    SUM(CASE 
        WHEN c.estado IN ('No Asistió', 'no asistio', 'inasistencia') 
        THEN 1 ELSE 0 
    END) AS total_inasistencias,
    
    -- Tasas calculadas
    CAST(
        SUM(CASE WHEN c.estado IN ('Cancelada', 'cancelada', 'Cancelado', 'cancelado') THEN 1 ELSE 0 END) * 100.0 
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_cancelacion,
    
    CAST(
        SUM(CASE WHEN c.estado IN ('Atendida', 'Completada', 'atendida', 'completada') THEN 1 ELSE 0 END) * 100.0 
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_asistencia,
    
    CAST(
        SUM(CASE WHEN c.estado IN ('No Asistió', 'no asistio', 'inasistencia') THEN 1 ELSE 0 END) * 100.0 
        / NULLIF(COUNT(*), 0)
    AS DECIMAL(5,2)) AS tasa_inasistencia,
    
    -- Duraciones
    AVG(ISNULL(c.duracion_minutos, 60)) AS duracion_promedio_minutos,
    SUM(ISNULL(c.duracion_minutos, 60)) AS duracion_total_minutos,
    
    -- Servicio más demandado
    (SELECT TOP 1 s.nombre 
     FROM Cita c2 
     INNER JOIN Servicio s ON c2.id_servicio = s.id_servicio
     WHERE YEAR(c2.fecha_hora) = YEAR(c.fecha_hora) 
       AND MONTH(c2.fecha_hora) = MONTH(c.fecha_hora)
     GROUP BY s.nombre
     ORDER BY COUNT(*) DESC
    ) AS servicio_mas_demandado,
    
    -- Profesional más activo
    (SELECT TOP 1 p.nombres + ' ' + p.apellidos
     FROM Cita c2 
     INNER JOIN Profesional p ON c2.id_profesional = p.id_profesional
     WHERE YEAR(c2.fecha_hora) = YEAR(c.fecha_hora) 
       AND MONTH(c2.fecha_hora) = MONTH(c.fecha_hora)
     GROUP BY p.id_profesional, p.nombres, p.apellidos
     ORDER BY COUNT(*) DESC
    ) AS profesional_mas_activo

FROM Cita c
GROUP BY YEAR(c.fecha_hora), MONTH(c.fecha_hora);
GO

PRINT '✓ Vista vw_KPI_Citas_Resumen creada exitosamente';
GO

-- ============================================================
-- ÍNDICES DE OPTIMIZACIÓN PARA VISTAS
-- ============================================================

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   CREACIÓN DE ÍNDICES DE OPTIMIZACIÓN';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO

-- Índice 1: Citas por fecha y estado (vw_Citas_Dashboard)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_FechaHora_Estado_Covering' AND object_id = OBJECT_ID('Cita'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_FechaHora_Estado_Covering
    ON Cita(fecha_hora, estado)
    INCLUDE (id_paciente, id_profesional, id_servicio, id_consultorio, duracion_minutos);
    
    PRINT '✓ Índice IX_Cita_FechaHora_Estado_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Cita_FechaHora_Estado_Covering ya existe';
GO

-- Índice 2: Citas activas por profesional (vw_Disponibilidad_Profesional)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_Profesional_FechaHora_NoCancel' AND object_id = OBJECT_ID('Cita'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_Profesional_FechaHora_NoCancel
    ON Cita(id_profesional, fecha_hora)
        WHERE estado <> 'Cancelada'
            AND estado <> 'cancelada'
            AND estado <> 'Cancelado'
            AND estado <> 'cancelado';
    
    PRINT '✓ Índice IX_Cita_Profesional_FechaHora_NoCancel creado';
END
ELSE
    PRINT '○ Índice IX_Cita_Profesional_FechaHora_NoCancel ya existe';
GO

-- Índice 3: Horarios activos por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_HorarioProfesional_IdProf_Activo_Covering' AND object_id = OBJECT_ID('Horario_Profesional'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_HorarioProfesional_IdProf_Activo_Covering
    ON Horario_Profesional(id_profesional, activo)
    INCLUDE (dia_semana, hora_inicio, hora_fin);
    
    PRINT '✓ Índice IX_HorarioProfesional_IdProf_Activo_Covering creado';
END
ELSE
    PRINT '○ Índice IX_HorarioProfesional_IdProf_Activo_Covering ya existe';
GO

-- Índice 4: Ausencias vigentes por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AusenciaProfesional_IdProf_Fechas_Covering' AND object_id = OBJECT_ID('Ausencia_Profesional'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AusenciaProfesional_IdProf_Fechas_Covering
    ON Ausencia_Profesional(id_profesional, fecha_inicio, fecha_fin)
    INCLUDE (tipo, observaciones);
    
    PRINT '✓ Índice IX_AusenciaProfesional_IdProf_Fechas_Covering creado';
END
ELSE
    PRINT '○ Índice IX_AusenciaProfesional_IdProf_Fechas_Covering ya existe';
GO

-- Índice 5: Bloqueos activos por profesional
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BloqueoProfesional_IdProf_Fechas_Covering' AND object_id = OBJECT_ID('Bloqueo_Profesional'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_BloqueoProfesional_IdProf_Fechas_Covering
    ON Bloqueo_Profesional(id_profesional, fecha_inicio, fecha_fin)
    INCLUDE (motivo);
    
    PRINT '✓ Índice IX_BloqueoProfesional_IdProf_Fechas_Covering creado';
END
ELSE
    PRINT '○ Índice IX_BloqueoProfesional_IdProf_Fechas_Covering ya existe';
GO

-- Índice 6: Profesional-Especialidad con principal
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProfesionalEspecialidad_Principal_Covering' AND object_id = OBJECT_ID('Profesional_Especialidad'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProfesionalEspecialidad_Principal_Covering
    ON Profesional_Especialidad(id_profesional, principal)
    INCLUDE (id_especialidad);
    
    PRINT '✓ Índice IX_ProfesionalEspecialidad_Principal_Covering creado';
END
ELSE
    PRINT '○ Índice IX_ProfesionalEspecialidad_Principal_Covering ya existe';
GO

-- Índice 7: Profesionales por estado
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Profesional_Estado_Covering' AND object_id = OBJECT_ID('Profesional'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Profesional_Estado_Covering
    ON Profesional(estado)
    INCLUDE (nombres, apellidos, registro_medico, id_usuario);
    
    PRINT '✓ Índice IX_Profesional_Estado_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Profesional_Estado_Covering ya existe';
GO

-- Índice 8: Citas del día (dashboard diario)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Cita_FechaSola_Covering' AND object_id = OBJECT_ID('Cita'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Cita_FechaSola_Covering
    ON Cita(fecha_hora)
    INCLUDE (id_profesional, id_paciente, id_consultorio, estado);
    
    PRINT '✓ Índice IX_Cita_FechaSola_Covering creado';
END
ELSE
    PRINT '○ Índice IX_Cita_FechaSola_Covering ya existe';
GO

-- Actualizar estadísticas
UPDATE STATISTICS Cita WITH FULLSCAN;
UPDATE STATISTICS Profesional WITH FULLSCAN;
UPDATE STATISTICS Horario_Profesional WITH FULLSCAN;
UPDATE STATISTICS Ausencia_Profesional WITH FULLSCAN;
UPDATE STATISTICS Bloqueo_Profesional WITH FULLSCAN;

PRINT '✓ Estadísticas actualizadas para optimización de consultas';
GO

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '   RESUMEN: 5 vistas y 8 índices creados/verificados';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
GO
