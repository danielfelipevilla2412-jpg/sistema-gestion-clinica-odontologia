-- ============================================================
-- SCRIPT_SQL_UNICO_SMILETRACK.sql
-- Esquema unificado de SmileTrack sin datos ficticios de negocio.
-- Mantiene estructura, relaciones e indices necesarios.
-- Mantiene catalogos funcionales y unicamente el administrador inicial.
-- ============================================================

USE [SmileTrackDB];
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
        CONSTRAINT CK_AuditoriaRecuperacion_Accion CHECK (accion IN ('solicitud','codigo_verificado','codigo_fallido','password_restablecida','bloqueo_por_intentos','rate_limit_excedido')),
        CONSTRAINT FK_AuditoriaRecuperacion_Usuario FOREIGN KEY (id_usuario) REFERENCES dbo.Usuario(id_usuario) ON DELETE SET NULL
    );
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

<<<<<<< HEAD
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

-- 1. TRIGGER: Auditoría automática en cambios de estado o asignación de cita
IF OBJECT_ID('dbo.TR_Cita_Auditoria_Estado', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Cita_Auditoria_Estado;
GO

CREATE TRIGGER dbo.TR_Cita_Auditoria_Estado
ON dbo.Cita
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(estado) OR UPDATE(id_estado) OR UPDATE(id_profesional) OR UPDATE(fecha_hora)
    BEGIN
        INSERT INTO dbo.Auditoria (
            id_usuario,
            tabla_afectada,
            id_registro,
            accion,
            ip_origen,
            datos_anteriores,
            datos_nuevos,
            descripcion,
            fecha
        )
        SELECT
            i.creado_por,
            'Cita',
            i.id_cita,
            'UPDATE',
            'TRIGGER_DB',
            'Estado: ' + ISNULL(d.estado, 'NULL') + ', Prof: ' + ISNULL(CAST(d.id_profesional AS VARCHAR(20)), 'NULL'),
            'Estado: ' + ISNULL(i.estado, 'NULL') + ', Prof: ' + ISNULL(CAST(i.id_profesional AS VARCHAR(20)), 'NULL'),
            'Actualización de estado o asignación de profesional en cita ID ' + CAST(i.id_cita AS VARCHAR(20)),
            GETDATE()
        FROM inserted i
        INNER JOIN deleted d ON i.id_cita = d.id_cita
        WHERE ISNULL(i.estado, '') <> ISNULL(d.estado, '')
           OR ISNULL(i.id_estado, 0) <> ISNULL(d.id_estado, 0)
           OR ISNULL(i.id_profesional, 0) <> ISNULL(d.id_profesional, 0)
           OR i.fecha_hora <> d.fecha_hora;
    END
END
GO
PRINT 'Trigger TR_Cita_Auditoria_Estado creado correctamente.';
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
          AND DATEADD(MINUTE, @DuracionMinutos, fecha_hora) > @FechaHoraInicio
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
    @Estado VARCHAR(30) = 'Programada',
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

    IF NOT EXISTS (SELECT 1 FROM dbo.Paciente WHERE id_paciente = @IdPaciente AND estado = 'activo')
    BEGIN
        SET @MensajeError = 'El paciente especificado no existe o no se encuentra activo.';
        RETURN -1;
    END

    IF @IdProfesional IS NOT NULL AND @IdProfesional > 0
    BEGIN
        IF dbo.fn_VerificarDisponibilidadProfesional(@IdProfesional, @FechaHora, @DuracionMinutos, NULL) = 0
        BEGIN
            SET @MensajeError = 'El profesional seleccionado no tiene disponibilidad en la fecha y horario solicitados.';
            RETURN -2;
        END
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
            @Estado,
            @IdEstado,
            @Notas,
            GETDATE(),
            @CreadoPor
        );

        SET @IdCitaCreada = SCOPE_IDENTITY();
        SET @MensajeError = 'Cita registrada exitosamente.';
        RETURN 0;
    END TRY
    BEGIN CATCH
        SET @MensajeError = ERROR_MESSAGE();
        RETURN -99;
    END CATCH
END
GO
PRINT 'Procedimiento almacenado sp_RegistrarCitaConValidacion creado correctamente.';
GO

PRINT 'Script ejecutado correctamente.';
=======
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
>>>>>>> 0370ea7bd1bcb77ff995ddcfb2e98e1608ed5231
GO