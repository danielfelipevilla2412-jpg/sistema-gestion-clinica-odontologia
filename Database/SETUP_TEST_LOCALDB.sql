-- ============================================================
-- SETUP_TEST_LOCALDB.sql
-- Crea la BD SmileTrackTest en LocalDB y aplica
-- todo el esquema de SmileTrack con datos anonimizados.
--
-- INSTRUCCIONES:
--   1. Abrir SSMS y conectarse a: (localdb)\MSSQLLocalDB
--   2. Ejecutar este archivo completo (F5 / Execute)
--   3. La BD SmileTrackTest quedará lista para pruebas
--
-- NO contiene datos reales de SmileTrackDB.
-- Todos los correos, teléfonos y documentos son ficticios.
-- ============================================================

USE [master];
GO

-- ── Crear BD si no existe ────────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SmileTrackTest')
BEGIN
    CREATE DATABASE [SmileTrackTest];
    PRINT 'Base de datos SmileTrackTest creada.';
END
ELSE
    PRINT 'SmileTrackTest ya existe - continuando con esquema.';
GO

USE [SmileTrackTest];
GO

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO

-- ============================================================
-- 1) ESQUEMA COMPLETO
-- Ejecutar SCRIPT_SQL_UNICO_SMILETRACK.sql contra SmileTrackTest
-- cambiando "USE [SmileTrackDB]" por "USE [SmileTrackTest]"
-- o bien pegar el contenido de ese script aquí.
--
-- Para simplificar el proceso, este script asume que ya
-- ejecutaste el esquema base. Si no lo hiciste, ejecuta primero:
--
--   SCRIPT_SQL_UNICO_SMILETRACK.sql (cambiando USE a SmileTrackTest)
--
-- y luego vuelve a ejecutar este archivo desde la sección SEED.
-- ============================================================

PRINT '=== SEED: Datos de prueba anonimizados para SmileTrackTest ===';
GO

-- ── SEED: Consultorios ───────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Consultorio WHERE nombre = 'Consultorio 1 - Test')
BEGIN
    INSERT INTO dbo.Consultorio (nombre, ubicacion, estado)
    VALUES
        ('Consultorio 1 - Test', 'Consultorio de pruebas A', 'disponible'),
        ('Consultorio 2 - Test', 'Consultorio de pruebas B', 'disponible');
    PRINT 'Consultorios de prueba insertados.';
END
GO

-- ── SEED: Servicios ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Servicio WHERE nombre = 'Limpieza Dental - Test')
BEGIN
    INSERT INTO dbo.Servicio (nombre, descripcion, precio, duracion_minutos, estado, categoria)
    VALUES
        ('Limpieza Dental - Test',   'Profilaxis dental de prueba',         80000.00, 45,  'activo', 'Preventivo'),
        ('Consulta General - Test',  'Consulta odontológica de prueba',     50000.00, 30,  'activo', 'General'),
        ('Extracción Simple - Test', 'Extracción dental simple de prueba', 120000.00, 60,  'activo', 'Cirugía Oral'),
        ('Blanqueamiento - Test',    'Blanqueamiento dental de prueba',    200000.00, 90,  'activo', 'Estética');
    PRINT 'Servicios de prueba insertados.';
END
GO

-- ── SEED: Usuarios de prueba ─────────────────────────────────
-- Contraseñas: todas son "Test1234!" hasheadas con BCrypt $2a$11$
-- Hash generado offline — no contiene datos reales.

-- Profesional de prueba
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'prof.test@smiletrack.test')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES (
        'Carlos',
        'Ruiz Test',
        'prof.test@smiletrack.test',
        '$2a$11$wLOzTFJhq0AgwU5DNH12.uoCAaRNnT2WWIJeS.XRP3PJE63qw69TG', -- Test1234!
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Profesional'),
        'activo',
        GETDATE()
    );
    PRINT 'Usuario Profesional de prueba insertado.';
END
GO

-- Recepcionista de prueba
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'recep.test@smiletrack.test')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES (
        'María',
        'López Test',
        'recep.test@smiletrack.test',
        '$2a$11$wLOzTFJhq0AgwU5DNH12.uoCAaRNnT2WWIJeS.XRP3PJE63qw69TG', -- Test1234!
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Recepcionista'),
        'activo',
        GETDATE()
    );
    PRINT 'Usuario Recepcionista de prueba insertado.';
END
GO

-- Paciente de prueba (usuario)
DECLARE @idUsuarioPaciente INT;
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'paciente.test@smiletrack.test')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES (
        'Juan',
        'Pérez Test',
        'paciente.test@smiletrack.test',
        '$2a$11$wLOzTFJhq0AgwU5DNH12.uoCAaRNnT2WWIJeS.XRP3PJE63qw69TG', -- Test1234!
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Paciente'),
        'activo',
        GETDATE()
    );
    PRINT 'Usuario Paciente de prueba insertado.';
END
GO

-- Administrador de prueba
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'admin.test@smiletrack.test')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES (
        'Admin',
        'Prueba',
        'admin.test@smiletrack.test',
        '$2a$11$wLOzTFJhq0AgwU5DNH12.uoCAaRNnT2WWIJeS.XRP3PJE63qw69TG', -- Test1234!
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Administrador'),
        'activo',
        GETDATE()
    );
    PRINT 'Usuario Administrador de prueba insertado.';
END
GO

UPDATE dbo.Usuario
SET contrasena = '$2a$11$wLOzTFJhq0AgwU5DNH12.uoCAaRNnT2WWIJeS.XRP3PJE63qw69TG',
    intentos_fallidos = 0
WHERE correo IN (
    'prof.test@smiletrack.test',
    'recep.test@smiletrack.test',
    'paciente.test@smiletrack.test',
    'admin.test@smiletrack.test'
);
GO

-- ── SEED: Profesional (entidad clínica) ──────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001')
BEGIN
    INSERT INTO dbo.Profesional (
        id_usuario, nombres, apellidos, registro_medico, telefono,
        descripcion, estado, fecha_ingreso
    )
    VALUES (
        (SELECT id_usuario FROM dbo.Usuario WHERE correo = 'prof.test@smiletrack.test'),
        'Profesional',
        'Prueba',
        'REG-TEST-001',
        '3001234567',
        'Profesional de entorno de pruebas - datos ficticios',
        'activo',
        GETDATE()
    );
    PRINT 'Profesional de prueba insertado.';
END
GO

-- ── Especialidad del profesional de prueba ───────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.Profesional_Especialidad
    WHERE id_profesional = (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001')
)
BEGIN
    INSERT INTO dbo.Profesional_Especialidad (id_profesional, id_especialidad)
    SELECT
        (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001'),
        (SELECT id_especialidad FROM dbo.Especialidad WHERE nombre = 'Odontología General');
    PRINT 'Especialidad del profesional de prueba asignada.';
END
GO

-- ── Horario del profesional de prueba (L-V 8:00-17:00) ──────
DECLARE @idProfTest INT = (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001');

IF NOT EXISTS (SELECT 1 FROM dbo.Horario_Profesional WHERE id_profesional = @idProfTest)
BEGIN
    INSERT INTO dbo.Horario_Profesional (id_profesional, dia_semana, hora_inicio, hora_fin, activo)
    VALUES
        (@idProfTest, 'Lunes', '08:00', '17:00', 1),
        (@idProfTest, 'Martes', '08:00', '17:00', 1),
        (@idProfTest, 'Miercoles', '08:00', '17:00', 1),
        (@idProfTest, 'Jueves', '08:00', '17:00', 1),
        (@idProfTest, 'Viernes', '08:00', '17:00', 1);
    PRINT 'Horario del profesional de prueba insertado.';
END
GO

-- ── Servicios del profesional de prueba ─────────────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.Profesional_Servicio
    WHERE id_profesional = (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001')
)
BEGIN
    INSERT INTO dbo.Profesional_Servicio (id_profesional, id_servicio)
    SELECT
        (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001'),
        id_servicio
    FROM dbo.Servicio WHERE nombre LIKE '%-Test';
    PRINT 'Servicios del profesional de prueba asignados.';
END
GO

-- ── SEED: Paciente (entidad clínica) ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Paciente WHERE documento = '1000TEST001')
BEGIN
    INSERT INTO dbo.Paciente (
        id_usuario, tipo_documento, documento, nombres, apellidos,
        telefono, direccion, fecha_nacimiento,
        genero, grupo_sanguineo, eps_aseguradora, alergias,
        medicamentos_actuales, antecedentes_medicos, estado
    )
    VALUES (
        (SELECT id_usuario FROM dbo.Usuario WHERE correo = 'paciente.test@smiletrack.test'),
        'CC',
        '1000TEST001',
        'Paciente',
        'Prueba',
        '3109876543',
        'Carrera Prueba 456',
        '1990-05-15',
        'M',
        'O+',
        'EPS Test',
        'Sin alergias conocidas (TEST)',
        'Ninguno (TEST)',
        'Ninguna (TEST)',
        'activo'
    );
    PRINT 'Paciente de prueba insertado.';
END
GO

-- ── SEED: Citas de prueba ────────────────────────────────────
-- 8 citas en distintos estados para cubrir todos los flujos de la auditoría

DECLARE @idProf    INT = (SELECT id_profesional FROM dbo.Profesional WHERE registro_medico = 'REG-TEST-001');
DECLARE @idPac     INT = (SELECT id_paciente    FROM dbo.Paciente      WHERE documento = '1000TEST001');
DECLARE @idCons    INT = (SELECT TOP 1 id_consultorio FROM dbo.Consultorio WHERE nombre LIKE '%Test%');
DECLARE @idSvc     INT = (SELECT TOP 1 id_servicio FROM dbo.Servicio WHERE nombre = 'Consulta General - Test');

DECLARE @estadoPendiente   INT = (SELECT id_estado FROM dbo.Estado_Cita WHERE nombre_estado = 'Agendada');
DECLARE @estadoConfirmada  INT = (SELECT id_estado FROM dbo.Estado_Cita WHERE nombre_estado = 'Confirmada');
DECLARE @estadoAtendida    INT = (SELECT id_estado FROM dbo.Estado_Cita WHERE nombre_estado = 'Atendida');
DECLARE @estadoCancelada   INT = (SELECT id_estado FROM dbo.Estado_Cita WHERE nombre_estado = 'Cancelada');
DECLARE @estadoNoAsistio   INT = (SELECT id_estado FROM dbo.Estado_Cita WHERE nombre_estado = 'No asistio');

-- Cita futura - Pendiente
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-01')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoPendiente, 'Agendada', @idSvc,
            DATEADD(DAY, 7, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '09:00:00',
            30, 'TEST-CITA-01', GETDATE());

-- Cita futura - Confirmada
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-02')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoConfirmada, 'Confirmada', @idSvc,
            DATEADD(DAY, 10, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '10:00:00',
            30, 'TEST-CITA-02', GETDATE());

-- Cita hoy - Confirmada (para dashboard)
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-03')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoConfirmada, 'Confirmada', @idSvc,
            CAST(CAST(GETDATE() AS DATE) AS DATETIME) + '14:00:00',
            30, 'TEST-CITA-03', GETDATE());

-- Cita pasada - Atendida
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-04')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoAtendida, 'Atendida', @idSvc,
            DATEADD(DAY, -5, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '11:00:00',
            30, 'TEST-CITA-04', GETDATE());

-- Cita pasada - Cancelada
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-05')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, motivo_cancelacion, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoCancelada, 'Cancelada', @idSvc,
            DATEADD(DAY, -10, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '09:00:00',
            30, 'TEST-CITA-05', 'Cancelada por pruebas de auditoría', GETDATE());

-- Cita pasada - No asistió
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-06')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoNoAsistio, 'No asistio', @idSvc,
            DATEADD(DAY, -15, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '15:00:00',
            30, 'TEST-CITA-06', GETDATE());

-- Cita futura - Pendiente (segunda cita futura para paginar)
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-07')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoPendiente, 'Agendada', @idSvc,
            DATEADD(DAY, 14, CAST(CAST(GETDATE() AS DATE) AS DATETIME)) + '11:00:00',
            30, 'TEST-CITA-07', GETDATE());

-- Cita hoy - Pendiente (para pruebas de agenda de hoy)
IF NOT EXISTS (SELECT 1 FROM dbo.Cita WHERE notas = 'TEST-CITA-08')
    INSERT INTO dbo.Cita (id_paciente, id_profesional, id_consultorio, id_estado, estado, id_servicio,
                          fecha_hora, duracion_minutos, notas, fecha_creacion)
    VALUES (@idPac, @idProf, @idCons, @estadoPendiente, 'Agendada', @idSvc,
            CAST(CAST(GETDATE() AS DATE) AS DATETIME) + '16:00:00',
            30, 'TEST-CITA-08', GETDATE());

PRINT 'Citas de prueba insertadas.';
GO

-- ── SEED: Historia Clínica del paciente de prueba ────────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.Historia_Clinica
    WHERE id_paciente = (SELECT id_paciente FROM dbo.Paciente WHERE documento = '1000TEST001')
)
BEGIN
    INSERT INTO dbo.Historia_Clinica (
        id_paciente, observaciones_generales, fecha_apertura
    )
    VALUES (
        (SELECT id_paciente FROM dbo.Paciente WHERE documento = '1000TEST001'),
        'Historia clínica de prueba - datos ficticios',
        GETDATE()
    );
    PRINT 'Historia clínica de prueba insertada.';
END
GO

-- ── Verificación final ───────────────────────────────────────
PRINT '';
PRINT '=== RESUMEN SmileTrackTest ===';
SELECT 'Roles'          AS entidad, COUNT(*) AS total FROM dbo.Rol
UNION ALL SELECT 'Usuarios',       COUNT(*) FROM dbo.Usuario
UNION ALL SELECT 'Profesionales',  COUNT(*) FROM dbo.Profesional
UNION ALL SELECT 'Pacientes',      COUNT(*) FROM dbo.Paciente
UNION ALL SELECT 'Consultorios',   COUNT(*) FROM dbo.Consultorio
UNION ALL SELECT 'Servicios',      COUNT(*) FROM dbo.Servicio
UNION ALL SELECT 'Citas',          COUNT(*) FROM dbo.Cita
UNION ALL SELECT 'Historia Clin.', COUNT(*) FROM dbo.Historia_Clinica;
GO

PRINT 'SmileTrackTest lista para auditoría visual, de navegador y CRUD.';
GO
