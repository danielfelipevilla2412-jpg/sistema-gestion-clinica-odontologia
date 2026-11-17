SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
-- ============================================================
-- SEED_USUARIOS_PRUEBA.sql
-- SmileTrack — Usuarios de prueba para todos los roles
-- Contraseña de todos los usuarios: 123456
-- Hashes generados con BCrypt workFactor=11 ($2b$11$)
-- ============================================================
USE SmileTrackDB;
GO

-- ============================================================
-- 1. RESET DEL ADMIN: limpiar intentos fallidos + actualizar hash a 123456
-- ============================================================
UPDATE dbo.Usuario
SET
    contrasena        = '$2b$11$ORA9We2BOeO1lIQmePo6QeQora3IlvkMahtEj00kem6JZPV4I8fhi',
    intentos_fallidos = 0,
    estado            = 'activo'
WHERE correo = 'admin@smiletrack.co';

PRINT 'Admin reseteado. Contraseña ahora es: 123456';

-- ============================================================
-- 2. USUARIO PROFESIONAL
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'prof@smiletrack.co')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_nacimiento, fecha_creacion, intentos_fallidos)
    VALUES (
        'Carlos',
        'Méndez',
        'prof@smiletrack.co',
        '$2b$11$FKmbaqA3NiHjqOjQchPWoufamT2IXS/uHmSBz5GMojLJby6PLYIWO',
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Profesional'),
        'activo',
        '1985-03-15',
        GETDATE(),
        0
    );
    PRINT 'Usuario Profesional creado: prof@smiletrack.co';
END
ELSE
BEGIN
    UPDATE dbo.Usuario
    SET contrasena = '$2b$11$FKmbaqA3NiHjqOjQchPWoufamT2IXS/uHmSBz5GMojLJby6PLYIWO',
        intentos_fallidos = 0,
        estado = 'activo'
    WHERE correo = 'prof@smiletrack.co';
    PRINT 'Usuario Profesional actualizado: prof@smiletrack.co';
END

-- ============================================================
-- 3. USUARIO PACIENTE
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'pac@smiletrack.co')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_nacimiento, fecha_creacion, intentos_fallidos)
    VALUES (
        'Pedro',
        'García',
        'pac@smiletrack.co',
        '$2b$11$mZ.VTPgoT8e3Z6wkxBJn2.jmXIFOlY8QHOiGpYLFjL/Umw6QQXyUG',
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Paciente'),
        'activo',
        '1992-07-20',
        GETDATE(),
        0
    );
    PRINT 'Usuario Paciente creado: pac@smiletrack.co';
END
ELSE
BEGIN
    UPDATE dbo.Usuario
    SET contrasena = '$2b$11$mZ.VTPgoT8e3Z6wkxBJn2.jmXIFOlY8QHOiGpYLFjL/Umw6QQXyUG',
        intentos_fallidos = 0,
        estado = 'activo'
    WHERE correo = 'pac@smiletrack.co';
    PRINT 'Usuario Paciente actualizado: pac@smiletrack.co';
END

-- ============================================================
-- 4. USUARIO RECEPCIONISTA
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'recep@smiletrack.co')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_nacimiento, fecha_creacion, intentos_fallidos)
    VALUES (
        'Laura',
        'Torres',
        'recep@smiletrack.co',
        '$2b$11$6X26NAxzj0UW/uYl5/ASUeZN8z9TjzjKrEqXVA32oShZfzTgSiae2',
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Recepcionista'),
        'activo',
        '1990-11-05',
        GETDATE(),
        0
    );
    PRINT 'Usuario Recepcionista creado: recep@smiletrack.co';
END
ELSE
BEGIN
    UPDATE dbo.Usuario
    SET contrasena = '$2b$11$6X26NAxzj0UW/uYl5/ASUeZN8z9TjzjKrEqXVA32oShZfzTgSiae2',
        intentos_fallidos = 0,
        estado = 'activo'
    WHERE correo = 'recep@smiletrack.co';
    PRINT 'Usuario Recepcionista actualizado: recep@smiletrack.co';
END

-- ============================================================
-- 5. USUARIO AUXILIAR
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM dbo.Usuario WHERE correo = 'aux@smiletrack.co')
BEGIN
    INSERT INTO dbo.Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_nacimiento, fecha_creacion, intentos_fallidos)
    VALUES (
        'María',
        'López',
        'aux@smiletrack.co',
        '$2b$11$MRJUJ/QlpalGs2ERBSnY5OXqsLVSVHvN52dyZd.i.vH56QTpGtXkS',
        (SELECT id_rol FROM dbo.Rol WHERE nombre_rol = 'Auxiliar'),
        'activo',
        '1995-02-28',
        GETDATE(),
        0
    );
    PRINT 'Usuario Auxiliar creado: aux@smiletrack.co';
END
ELSE
BEGIN
    UPDATE dbo.Usuario
    SET contrasena = '$2b$11$MRJUJ/QlpalGs2ERBSnY5OXqsLVSVHvN52dyZd.i.vH56QTpGtXkS',
        intentos_fallidos = 0,
        estado = 'activo'
    WHERE correo = 'aux@smiletrack.co';
    PRINT 'Usuario Auxiliar actualizado: aux@smiletrack.co';
END

-- ============================================================
-- 6. PERFIL PROFESIONAL (vinculado al usuario Profesional)
-- ============================================================
DECLARE @id_usuario_prof INT = (SELECT id_usuario FROM dbo.Usuario WHERE correo = 'prof@smiletrack.co');

IF @id_usuario_prof IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Profesional WHERE id_usuario = @id_usuario_prof)
BEGIN
    INSERT INTO dbo.Profesional (id_usuario, nombres, apellidos, registro_medico, descripcion, categoria, telefono, estado, fecha_ingreso, credenciales_aprobadas)
    VALUES (
        @id_usuario_prof,
        'Carlos',
        'Méndez',
        'RM-2025-001',
        'Odontólogo General con experiencia en tratamientos estéticos y preventivos.',
        'Odontología General',
        '3001234567',
        'activo',
        '2023-01-15',
        1
    );
    PRINT 'Perfil Profesional creado para: Carlos Méndez';
END

-- ============================================================
-- 7. PERFIL PACIENTE (vinculado al usuario Paciente)
-- ============================================================
DECLARE @id_usuario_pac INT = (SELECT id_usuario FROM dbo.Usuario WHERE correo = 'pac@smiletrack.co');
DECLARE @id_prof_asignado INT = (SELECT TOP 1 id_profesional FROM dbo.Profesional WHERE estado = 'activo');

IF @id_usuario_pac IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Paciente WHERE id_usuario = @id_usuario_pac)
BEGIN
    INSERT INTO dbo.Paciente (
        id_usuario, tipo_documento, documento, nombres, apellidos,
        fecha_nacimiento, genero, telefono, correo, direccion, ciudad,
        grupo_sanguineo, estado_civil, id_profesional_asignado, fecha_registro, estado
    )
    VALUES (
        @id_usuario_pac,
        'CC',
        '1234567890',
        'Pedro',
        'García',
        '1992-07-20',
        'Masculino',
        '3109876543',
        'pac@smiletrack.co',
        'Calle 10 #5-20',
        'Bogotá',
        'O+',
        'Soltero',
        @id_prof_asignado,
        CAST(GETDATE() AS DATE),
        'activo'
    );
    PRINT 'Perfil Paciente creado para: Pedro García';
END

-- ============================================================
-- 8. VERIFICACIÓN FINAL
-- ============================================================
SELECT
    u.id_usuario,
    u.nombre + ' ' + u.apellidos AS nombre_completo,
    u.correo,
    r.nombre_rol AS rol,
    u.estado,
    u.intentos_fallidos
FROM dbo.Usuario u
JOIN dbo.Rol r ON u.id_rol = r.id_rol
ORDER BY u.id_usuario;
GO
