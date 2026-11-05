IF OBJECT_ID(N'dbo.Nota_Clinica', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Nota_Clinica.', 1;
GO

IF COL_LENGTH(N'dbo.Nota_Clinica', N'id_cita') IS NULL
    ALTER TABLE dbo.Nota_Clinica ADD id_cita INT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_NC_Cita'
      AND parent_object_id = OBJECT_ID(N'dbo.Nota_Clinica')
)
    ALTER TABLE dbo.Nota_Clinica
        ADD CONSTRAINT FK_NC_Cita
        FOREIGN KEY (id_cita) REFERENCES dbo.Cita(id_cita)
        ON DELETE SET NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_NC_Cita'
      AND object_id = OBJECT_ID(N'dbo.Nota_Clinica')
)
    CREATE UNIQUE INDEX UX_NC_Cita
        ON dbo.Nota_Clinica(id_cita)
        WHERE id_cita IS NOT NULL;
GO