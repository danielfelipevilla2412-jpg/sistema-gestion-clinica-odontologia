-- NULL en Servicio.id_especialidad representa Odontología General y permite
-- el servicio a cualquier profesional. La categoría textual no se usa para autorizar.
-- No se infieren relaciones desde nombres/categorías existentes.
IF OBJECT_ID(N'dbo.Servicio', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Servicio.', 1;
GO

IF OBJECT_ID(N'dbo.Especialidad', N'U') IS NULL
    THROW 50000, 'No existe la tabla dbo.Especialidad.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Servicio', N'id_especialidad') IS NULL
        ALTER TABLE dbo.Servicio ADD id_especialidad INT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_Servicio_Especialidad'
          AND parent_object_id = OBJECT_ID(N'dbo.Servicio')
    )
        ALTER TABLE dbo.Servicio
            ADD CONSTRAINT FK_Servicio_Especialidad
            FOREIGN KEY (id_especialidad) REFERENCES dbo.Especialidad(id_especialidad)
            ON DELETE NO ACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_Servicio_IdEspecialidad'
          AND object_id = OBJECT_ID(N'dbo.Servicio')
    )
        CREATE INDEX IX_Servicio_IdEspecialidad ON dbo.Servicio(id_especialidad);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
