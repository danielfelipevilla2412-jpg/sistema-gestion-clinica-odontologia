SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Paciente', N'U') IS NULL
    BEGIN
        THROW 51000, 'Inicialice primero el esquema base de SmileTrack.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.Usuario
        GROUP BY LOWER(LTRIM(RTRIM(correo)))
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51001, 'Hay correos duplicados despues de normalizar. Resuelvalos antes de migrar.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.Paciente
        GROUP BY UPPER(LTRIM(RTRIM(tipo_documento))),
                 UPPER(LTRIM(RTRIM(documento)))
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51002, 'Hay documentos duplicados despues de normalizar. Resuelvalos antes de migrar.', 1;
    END;

    UPDATE dbo.Usuario
    SET correo = LOWER(LTRIM(RTRIM(correo)))
    WHERE correo COLLATE Latin1_General_100_BIN2
        <> LOWER(LTRIM(RTRIM(correo))) COLLATE Latin1_General_100_BIN2;

    UPDATE dbo.Paciente
    SET tipo_documento = UPPER(LTRIM(RTRIM(tipo_documento))),
        documento = UPPER(LTRIM(RTRIM(documento)))
    WHERE tipo_documento COLLATE Latin1_General_100_BIN2
              <> UPPER(LTRIM(RTRIM(tipo_documento))) COLLATE Latin1_General_100_BIN2
       OR documento COLLATE Latin1_General_100_BIN2
              <> UPPER(LTRIM(RTRIM(documento))) COLLATE Latin1_General_100_BIN2;

    IF COL_LENGTH(N'dbo.Usuario', N'bloqueado_hasta') IS NULL
    BEGIN
        ALTER TABLE dbo.Usuario
            ADD bloqueado_hasta DATETIME2(7) NULL;
    END;

    EXEC sys.sp_executesql N'
        UPDATE dbo.Usuario
        SET bloqueado_hasta = DATEADD(MINUTE, 15, SYSUTCDATETIME())
        WHERE intentos_fallidos >= 3
          AND bloqueado_hasta IS NULL;';

    IF EXISTS (
        SELECT 1
        FROM sys.indexes AS i
        WHERE i.object_id = OBJECT_ID(N'dbo.Usuario')
          AND i.name = N'UX_Usuario_Correo'
          AND (
              i.is_unique = 0
              OR i.is_disabled = 1
              OR i.has_filter = 1
              OR (SELECT COUNT(*)
                  FROM sys.index_columns AS ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal > 0) <> 1
              OR NOT EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 1
                    AND ic.column_id = COLUMNPROPERTY(
                        OBJECT_ID(N'dbo.Usuario'),
                        N'correo',
                        'ColumnId')
              )
          )
    )
    BEGIN
        THROW 51003, 'El indice UX_Usuario_Correo existe con una definicion incompatible.', 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Usuario')
          AND name = N'UX_Usuario_Correo'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_Usuario_Correo
            ON dbo.Usuario(correo);
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes AS i
        WHERE i.object_id = OBJECT_ID(N'dbo.Paciente')
          AND i.name = N'UX_Paciente_TipoDocumento_Documento'
          AND (
              i.is_unique = 0
              OR i.is_disabled = 1
              OR i.has_filter = 1
              OR (SELECT COUNT(*)
                  FROM sys.index_columns AS ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal > 0) <> 2
              OR NOT EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 1
                    AND ic.column_id = COLUMNPROPERTY(
                        OBJECT_ID(N'dbo.Paciente'),
                        N'tipo_documento',
                        'ColumnId')
              )
              OR NOT EXISTS (
                  SELECT 1
                  FROM sys.index_columns AS ic
                  WHERE ic.object_id = i.object_id
                    AND ic.index_id = i.index_id
                    AND ic.key_ordinal = 2
                    AND ic.column_id = COLUMNPROPERTY(
                        OBJECT_ID(N'dbo.Paciente'),
                        N'documento',
                        'ColumnId')
              )
          )
    )
    BEGIN
        THROW 51004, 'El indice UX_Paciente_TipoDocumento_Documento existe con una definicion incompatible.', 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Paciente')
          AND name = N'UX_Paciente_TipoDocumento_Documento'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_Paciente_TipoDocumento_Documento
            ON dbo.Paciente(tipo_documento, documento);
    END;

    COMMIT TRANSACTION;
    PRINT 'Migracion de identificadores y bloqueo de acceso completada.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
