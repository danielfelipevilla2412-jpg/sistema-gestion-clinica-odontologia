USE [SmileTrackDB];
GO

IF COL_LENGTH(N'dbo.Profesional', N'credenciales_aprobadas') IS NULL
    ALTER TABLE [dbo].[Profesional]
        ADD [credenciales_aprobadas] [bit] NOT NULL
            CONSTRAINT [DF_Profesional_CredencialesAprobadas] DEFAULT ((1)) WITH VALUES;
GO

IF COL_LENGTH(N'dbo.Profesional', N'tarjeta_profesional') IS NULL
    ALTER TABLE [dbo].[Profesional]
        ADD [tarjeta_profesional] [nvarchar](50) NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_Profesional_TarjetaProfesional'
      AND [object_id] = OBJECT_ID(N'dbo.Profesional')
)
    CREATE UNIQUE INDEX [UX_Profesional_TarjetaProfesional]
        ON [dbo].[Profesional]([tarjeta_profesional])
        WHERE [tarjeta_profesional] IS NOT NULL;
GO

IF COL_LENGTH(N'dbo.Cita', N'id_profesional_preferido') IS NULL
    ALTER TABLE [dbo].[Cita]
        ADD [id_profesional_preferido] [int] NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_Cita_ProfesionalPreferido'
      AND [parent_object_id] = OBJECT_ID(N'dbo.Cita')
)
    ALTER TABLE [dbo].[Cita] WITH CHECK
        ADD CONSTRAINT [FK_Cita_ProfesionalPreferido]
        FOREIGN KEY ([id_profesional_preferido])
        REFERENCES [dbo].[Profesional]([id_profesional])
        ON DELETE NO ACTION;
GO

IF OBJECT_ID(N'dbo.Lista_Espera_Cita', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Lista_Espera_Cita](
        [id_lista_espera] [int] IDENTITY(1,1) NOT NULL,
        [id_paciente] [int] NOT NULL,
        [id_servicio] [int] NULL,
        [id_profesional_preferido] [int] NULL,
        [id_cita_asignada] [int] NULL,
        [fecha_deseada_inicio] [datetime2](7) NOT NULL,
        [fecha_deseada_fin] [datetime2](7) NOT NULL,
        [estado] [nvarchar](20) NOT NULL CONSTRAINT [DF_Lista_Espera_Cita_estado] DEFAULT (N'pendiente'),
        [fecha_registro] [datetime2](7) NOT NULL CONSTRAINT [DF_Lista_Espera_Cita_fecha_registro] DEFAULT (SYSUTCDATETIME()),
        [notas] [nvarchar](500) NULL,
        CONSTRAINT [PK_Lista_Espera_Cita] PRIMARY KEY CLUSTERED ([id_lista_espera]),
        CONSTRAINT [CK_Lista_Espera_Cita_fechas]
            CHECK ([fecha_deseada_fin] >= [fecha_deseada_inicio]),
        CONSTRAINT [FK_Lista_Espera_Cita_Paciente]
            FOREIGN KEY ([id_paciente]) REFERENCES [dbo].[Paciente]([id_paciente]) ON DELETE CASCADE,
        CONSTRAINT [FK_Lista_Espera_Cita_Servicio]
            FOREIGN KEY ([id_servicio]) REFERENCES [dbo].[Servicio]([id_servicio]) ON DELETE SET NULL,
        CONSTRAINT [FK_Lista_Espera_Cita_Profesional]
            FOREIGN KEY ([id_profesional_preferido]) REFERENCES [dbo].[Profesional]([id_profesional]) ON DELETE SET NULL,
        CONSTRAINT [FK_Lista_Espera_Cita_Cita]
            FOREIGN KEY ([id_cita_asignada]) REFERENCES [dbo].[Cita]([id_cita]) ON DELETE SET NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.Lista_Espera_Cita', N'id_cita_asignada') IS NULL
    ALTER TABLE [dbo].[Lista_Espera_Cita] ADD [id_cita_asignada] [int] NULL;
GO

IF COL_LENGTH(N'dbo.Lista_Espera_Cita', N'id_profesional_preferido') IS NULL
    ALTER TABLE [dbo].[Lista_Espera_Cita] ADD [id_profesional_preferido] [int] NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_Lista_Espera_Cita_Profesional'
      AND [parent_object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita')
)
    ALTER TABLE [dbo].[Lista_Espera_Cita] WITH CHECK
        ADD CONSTRAINT [FK_Lista_Espera_Cita_Profesional]
        FOREIGN KEY ([id_profesional_preferido])
        REFERENCES [dbo].[Profesional]([id_profesional])
        ON DELETE SET NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_Lista_Espera_Cita_Cita'
      AND [parent_object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita')
)
    ALTER TABLE [dbo].[Lista_Espera_Cita] WITH CHECK
        ADD CONSTRAINT [FK_Lista_Espera_Cita_Cita]
        FOREIGN KEY ([id_cita_asignada]) REFERENCES [dbo].[Cita]([id_cita])
        ON DELETE SET NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ListaEspera_Estado_Franja'
      AND [object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita')
)
    CREATE INDEX [IX_ListaEspera_Estado_Franja]
        ON [dbo].[Lista_Espera_Cita]([estado], [fecha_deseada_inicio], [fecha_deseada_fin]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'UX_ListaEspera_CitaAsignada'
      AND [object_id] = OBJECT_ID(N'dbo.Lista_Espera_Cita')
)
    CREATE UNIQUE INDEX [UX_ListaEspera_CitaAsignada]
        ON [dbo].[Lista_Espera_Cita]([id_cita_asignada])
        WHERE [id_cita_asignada] IS NOT NULL;
GO

DECLARE @RestriccionTipoNotificacion sysname;
DECLARE @SqlEliminarRestriccion nvarchar(300);
SELECT TOP (1) @RestriccionTipoNotificacion = [name]
FROM sys.check_constraints
WHERE [parent_object_id] = OBJECT_ID(N'dbo.Notificacion')
  AND CHARINDEX(N'[tipo]', [definition]) > 0
  AND LOWER([definition]) NOT LIKE N'%cambio_estado%';

WHILE @RestriccionTipoNotificacion IS NOT NULL
BEGIN
    SET @SqlEliminarRestriccion =
        N'ALTER TABLE [dbo].[Notificacion] DROP CONSTRAINT ' +
        QUOTENAME(@RestriccionTipoNotificacion);
    EXEC sp_executesql @SqlEliminarRestriccion;
    SET @RestriccionTipoNotificacion = NULL;

    SELECT TOP (1) @RestriccionTipoNotificacion = [name]
    FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'dbo.Notificacion')
      AND CHARINDEX(N'[tipo]', [definition]) > 0
      AND LOWER([definition]) NOT LIKE N'%cambio_estado%';
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = N'CK_Notificacion_tipo'
      AND [parent_object_id] = OBJECT_ID(N'dbo.Notificacion')
)
    ALTER TABLE [dbo].[Notificacion] WITH CHECK
        ADD CONSTRAINT [CK_Notificacion_tipo]
        CHECK ([tipo] IN (
            'pago', 'factura', 'alerta', 'mensaje',
            'cancelacion', 'confirmacion', 'recordatorio', 'cambio_estado'
        ));
GO

DECLARE @Estados TABLE ([nombre_estado] [nvarchar](50) NOT NULL);
INSERT INTO @Estados ([nombre_estado]) VALUES
    (N'Solicitada'), (N'Programada'), (N'Confirmada'),
    (N'En sala de espera'), (N'En atención'), (N'Atendida'),
    (N'Cancelada'), (N'No asistió'), (N'Rechazada');

INSERT INTO [dbo].[Estado_Cita] ([nombre_estado])
SELECT e.[nombre_estado]
FROM @Estados e
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Estado_Cita] actual
    WHERE LOWER(actual.[nombre_estado]) = LOWER(e.[nombre_estado])
);
GO
