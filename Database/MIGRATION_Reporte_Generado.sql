USE [SmileTrackDB];
GO

IF OBJECT_ID(N'dbo.Reporte_Generado', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Reporte_Generado](
        [id_reporte] [int] IDENTITY(1,1) NOT NULL,
        [nombre] [nvarchar](120) NOT NULL,
        [categoria] [nvarchar](30) NOT NULL,
        [periodo] [char](7) NOT NULL,
        [notas] [nvarchar](1000) NULL,
        [fecha_generacion_utc] [datetime2](7) NOT NULL,
        [generado_por] [int] NULL,
        [datos_json] [nvarchar](max) NOT NULL,
        CONSTRAINT [PK_Reporte_Generado] PRIMARY KEY CLUSTERED ([id_reporte] ASC),
        CONSTRAINT [FK_Reporte_Generado_Usuario_generado_por]
            FOREIGN KEY ([generado_por]) REFERENCES [dbo].[Usuario]([id_usuario]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Reporte_Generado_categoria_fecha_generacion_utc'
      AND [object_id] = OBJECT_ID(N'dbo.Reporte_Generado')
)
BEGIN
    CREATE INDEX [IX_Reporte_Generado_categoria_fecha_generacion_utc]
        ON [dbo].[Reporte_Generado]([categoria], [fecha_generacion_utc]);
END;
GO
