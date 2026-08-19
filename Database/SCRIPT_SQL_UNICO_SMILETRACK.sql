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
        estado VARCHAR(15) NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo','inactivo')),
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
        id_profesional INT NOT NULL,
        id_servicio INT NOT NULL,
        PRIMARY KEY (id_profesional, id_servicio),
        CONSTRAINT FK_PS_Profesional FOREIGN KEY (id_profesional) REFERENCES Profesional(id_profesional),
        CONSTRAINT FK_PS_Servicio FOREIGN KEY (id_servicio) REFERENCES Servicio(id_servicio)
    );
END
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