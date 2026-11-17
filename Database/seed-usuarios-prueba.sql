-- ================================================================
-- seed-usuarios-prueba.sql
-- Usuarios de prueba para desarrollo/demo
-- Contraseña de todos: 123456
-- ================================================================
USE [SmileTrackDB];
GO

-- Hash BCrypt workFactor=11 de "123456"
DECLARE @hash VARCHAR(255) = '$2a$11$/981k020nf9bG/OpQaRMRO7akr5fuejimqahtba7/rc3kzlV..NDq';

-- Actualizar admin existente con hash correcto
UPDATE Usuario SET contrasena = @hash WHERE correo = 'admin@smiletrack.co';

-- Profesional
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE correo = 'prof@smiletrack.co')
    INSERT INTO Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES ('Profesional', 'Demo', 'prof@smiletrack.co', @hash,
            (SELECT id_rol FROM Rol WHERE nombre_rol = 'Profesional'), 'activo', GETDATE());
ELSE
    UPDATE Usuario SET contrasena = @hash WHERE correo = 'prof@smiletrack.co';

-- Paciente
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE correo = 'pac@smiletrack.co')
    INSERT INTO Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES ('Paciente', 'Demo', 'pac@smiletrack.co', @hash,
            (SELECT id_rol FROM Rol WHERE nombre_rol = 'Paciente'), 'activo', GETDATE());
ELSE
    UPDATE Usuario SET contrasena = @hash WHERE correo = 'pac@smiletrack.co';

-- Recepcionista
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE correo = 'recep@smiletrack.co')
    INSERT INTO Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES ('Recepcionista', 'Demo', 'recep@smiletrack.co', @hash,
            (SELECT id_rol FROM Rol WHERE nombre_rol = 'Recepcionista'), 'activo', GETDATE());
ELSE
    UPDATE Usuario SET contrasena = @hash WHERE correo = 'recep@smiletrack.co';

-- Auxiliar
IF NOT EXISTS (SELECT 1 FROM Usuario WHERE correo = 'aux@smiletrack.co')
    INSERT INTO Usuario (nombre, apellidos, correo, contrasena, id_rol, estado, fecha_creacion)
    VALUES ('Auxiliar', 'Demo', 'aux@smiletrack.co', @hash,
            (SELECT id_rol FROM Rol WHERE nombre_rol = 'Auxiliar'), 'activo', GETDATE());
ELSE
    UPDATE Usuario SET contrasena = @hash WHERE correo = 'aux@smiletrack.co';

-- Verificar
SELECT correo, estado, (SELECT nombre_rol FROM Rol WHERE id_rol = u.id_rol) AS rol
FROM Usuario u
WHERE correo IN ('admin@smiletrack.co','prof@smiletrack.co','pac@smiletrack.co','recep@smiletrack.co','aux@smiletrack.co');
GO
