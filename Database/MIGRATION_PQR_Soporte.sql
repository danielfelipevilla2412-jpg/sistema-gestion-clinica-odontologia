SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE dbo.PQR ALTER COLUMN id_paciente int NULL;

IF COL_LENGTH('dbo.PQR', 'id_profesional') IS NULL
    ALTER TABLE dbo.PQR ADD id_profesional int NULL;

IF COL_LENGTH('dbo.PQR', 'fecha_servicio') IS NULL
    ALTER TABLE dbo.PQR ADD fecha_servicio date NULL;

IF COL_LENGTH('dbo.PQR', 'modulo_software') IS NULL
    ALTER TABLE dbo.PQR ADD modulo_software nvarchar(100) NULL;

IF OBJECT_ID('dbo.FK_PQR_Profesional_Soporte', 'F') IS NULL
    EXEC(N'ALTER TABLE dbo.PQR ADD CONSTRAINT FK_PQR_Profesional_Soporte
        FOREIGN KEY (id_profesional) REFERENCES dbo.Profesional (id_profesional) ON DELETE SET NULL');

COMMIT TRANSACTION;
GO
