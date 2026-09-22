-- =============================================================================
-- SCRIPT: create_prof_smiletrack.sql
-- Fecha:  2026-09-21
-- Autor:  Antigravity IDE (solicitud de administración del sistema)
-- Motivo: Creación de cuenta profesional prof@smiletrack.co con contraseña
--         123456 (contraseña obligatoria por asignación de rol; excepción
--         deliberada a la política de complejidad de contraseñas).
--         La contraseña está hasheada con BCrypt workFactor=11, igual que
--         el resto del sistema. No se almacena en texto plano en ningún punto.
--
-- TRAZABILIDAD:
--   - La contraseña 123456 NO cumple la política estándar del sistema
--     (mínimo 8 chars, mayúscula, minúscula, dígito y símbolo).
--   - Esta excepción se aplica exclusivamente a esta cuenta por requerimiento
--     de negocio explícito documentado.
--   - El hash fue generado con BCrypt.Net-Next v4.0.3 / workFactor=11.
--   - Referencia: mismo patrón usado en Database/reset_admin.sql.
--
-- REVERSIÓN:
--   DELETE FROM Auditoria     WHERE descripcion LIKE '%prof@smiletrack.co%';
--   DELETE FROM Profesional   WHERE id_usuario = (SELECT id_usuario FROM Usuario WHERE correo = 'prof@smiletrack.co');
--   DELETE FROM Usuario       WHERE correo = 'prof@smiletrack.co';
-- =============================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY

    -- ─── 1. Verificar que el correo no exista (idempotencia) ──────────────────
    IF EXISTS (SELECT 1 FROM Usuario WHERE correo = 'prof@smiletrack.co')
    BEGIN
        PRINT 'INFO: El usuario prof@smiletrack.co ya existe. Script omitido (idempotente).';
        ROLLBACK;
        RETURN;
    END

    -- ─── 2. Obtener IdRol del rol "Profesional" ───────────────────────────────
    DECLARE @idRol INT;
    SELECT @idRol = id_rol FROM Rol WHERE nombre_rol = 'Profesional';

    IF @idRol IS NULL
    BEGIN
        RAISERROR('ERROR: No se encontró el rol "Profesional" en la tabla Rol. Verifica la BD.', 16, 1);
        ROLLBACK;
        RETURN;
    END

    -- ─── 3. Insertar Usuario ──────────────────────────────────────────────────
    -- Hash BCrypt workFactor=11 de la contraseña "123456"
    -- Generado con BCrypt.Net-Next v4.0.3 el 2026-09-21
    -- Verificado: BCrypt.Verify("123456", hash) = True
    DECLARE @hashContrasena NVARCHAR(256) =
        '$2a$11$zsF6jIihLv3sUMXJ.BYMAufPET1d06zl0fAFjGFcfAwaiGP4Ubp5C';

    INSERT INTO Usuario
        (nombre, apellidos, correo, contrasena, id_rol, estado, intentos_fallidos, fecha_creacion)
    VALUES
        ('Profesional', 'SmileTrack', 'prof@smiletrack.co', @hashContrasena,
         @idRol, 'activo', 0, GETUTCDATE());

    DECLARE @idUsuario INT = SCOPE_IDENTITY();
    PRINT CONCAT('INFO: Usuario creado. IdUsuario=', @idUsuario);

    -- ─── 4. Insertar Profesional ──────────────────────────────────────────────
    INSERT INTO Profesional
        (id_usuario, nombres, apellidos, registro_medico, estado, fecha_ingreso)
    VALUES
        (@idUsuario, 'Profesional', 'SmileTrack', 'REG-PROF-001', 'activo', CAST(GETUTCDATE() AS DATE));

    DECLARE @idProfesional INT = SCOPE_IDENTITY();
    PRINT CONCAT('INFO: Profesional creado. IdProfesional=', @idProfesional);

    -- ─── 5. Registrar en Auditoría ────────────────────────────────────────────
    INSERT INTO Auditoria
        (id_usuario, tabla_afectada, id_registro, accion, ip_origen, datos_nuevos, descripcion, fecha)
    VALUES
        (NULL, 'Usuario', @idUsuario, 'INSERT', '127.0.0.1',
         CONCAT('{"IdUsuario":', @idUsuario, ',"Correo":"prof@smiletrack.co","Rol":"Profesional","Nota":"Cuenta creada por script de administración. Excepción de contraseña por requerimiento de rol."}'),
         'Cuenta prof@smiletrack.co creada vía script de administración. Contraseña 123456 hasheada con BCrypt/11. Excepción a política de complejidad aprobada.',
         GETUTCDATE());

    COMMIT;
    PRINT 'OK: Cuenta prof@smiletrack.co creada exitosamente.';

    -- ─── 6. Verificación final ────────────────────────────────────────────────
    SELECT
        u.id_usuario         AS IdUsuario,
        u.nombre             AS Nombre,
        u.apellidos          AS Apellidos,
        u.correo             AS Correo,
        r.nombre_rol         AS Rol,
        u.estado             AS Estado,
        LEFT(u.contrasena, 10) AS InicioHash,
        LEN(u.contrasena)    AS LargoHash,
        p.id_profesional     AS IdProfesional,
        p.registro_medico    AS RegistroMedico
    FROM Usuario u
    INNER JOIN Rol r ON u.id_rol = r.id_rol
    LEFT JOIN Profesional p ON p.id_usuario = u.id_usuario
    WHERE u.correo = 'prof@smiletrack.co';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    DECLARE @msg NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT CONCAT('ERROR: ', @msg);
    RAISERROR(@msg, 16, 1);
END CATCH;
