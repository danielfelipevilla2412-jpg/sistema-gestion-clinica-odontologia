-- =============================================================================
-- MIGRATION: Consentimiento de Datos (Ley 1581/2012)
-- SmileTrack v2.5.0 - Noviembre 2026
-- =============================================================================
-- Aplica sobre la base de datos SmileTrackDB existente.
-- Ejecutar una sola vez. Es idempotente: usa IF NOT EXISTS en cada bloque.
-- =============================================================================

USE SmileTrackDB;
GO

-- -----------------------------------------------------------------------------
-- 1. Columnas de consentimiento en dbo.Usuario
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
BEGIN
    THROW 51000, 'No existe dbo.Usuario. Inicialice primero la base SmileTrackDB.', 1;
END
GO

IF COL_LENGTH(N'dbo.Usuario', N'version_consentimiento') IS NULL
    ALTER TABLE dbo.Usuario ADD [version_consentimiento] NVARCHAR(10) NULL;
GO

IF COL_LENGTH(N'dbo.Usuario', N'fecha_consentimiento') IS NULL
    ALTER TABLE dbo.Usuario ADD [fecha_consentimiento] DATETIME2(7) NULL;
GO

IF COL_LENGTH(N'dbo.Usuario', N'ip_consentimiento') IS NULL
    ALTER TABLE dbo.Usuario ADD [ip_consentimiento] NVARCHAR(45) NULL;
GO

-- -----------------------------------------------------------------------------
-- 2. Tabla de registro de consentimiento (auditoría Habeas Data)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('dbo.RegistroConsentimiento') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[RegistroConsentimiento] (
        [id_registro]           INT IDENTITY(1,1) NOT NULL,
        [id_usuario]            INT              NOT NULL,   -- FK → dbo.Usuario
        [version_politica]      NVARCHAR(10)     NOT NULL,  -- e.g. 'DEMO 0.1'
        [fecha_consentimiento]  DATETIME2(7)     NOT NULL,
        [ip_origen]             NVARCHAR(45)     NULL,
        [canal]                 NVARCHAR(30)     NOT NULL DEFAULT 'web-registro',
        -- Canal: web-registro | web-perfil | api | backoffice
        [finalidad]             NVARCHAR(255)    NOT NULL DEFAULT 'atencion-clinica',
        [accion]                NVARCHAR(20)     NOT NULL DEFAULT 'otorgar',
        -- accion: otorgar | revocar | actualizar
        [tipo_datos]            NVARCHAR(100)    NOT NULL DEFAULT 'datos-salud,datos-personales',
        [responsable]           NVARCHAR(100)    NOT NULL DEFAULT 'DEMO-NO-VALIDADO',
        CONSTRAINT [PK_RegistroConsentimiento] PRIMARY KEY CLUSTERED ([id_registro] ASC),
        CONSTRAINT [FK_RegistroConsentimiento_Usuario]
            FOREIGN KEY ([id_usuario]) REFERENCES [dbo].[Usuario]([id_usuario])
            ON DELETE CASCADE
    );

    PRINT 'Tabla dbo.RegistroConsentimiento creada';
END
ELSE
    PRINT 'Tabla dbo.RegistroConsentimiento ya existe — sin cambios';
GO

IF OBJECT_ID(N'dbo.RegistroConsentimiento', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE [name] = N'IX_RegistroConsentimiento_Usuario'
         AND [object_id] = OBJECT_ID(N'dbo.RegistroConsentimiento')
   )
BEGIN
    CREATE INDEX [IX_RegistroConsentimiento_Usuario]
        ON [dbo].[RegistroConsentimiento] ([id_usuario], [fecha_consentimiento] DESC);
END
GO

PRINT '=== MIGRATION_Consentimiento.sql completada ===';
GO
