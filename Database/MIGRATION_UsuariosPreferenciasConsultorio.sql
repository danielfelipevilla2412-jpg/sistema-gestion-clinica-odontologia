-- ============================================
-- SmileTrack — Migración: UsuariosPreferenciasConsultorio
-- ============================================
-- PROPÓSITO:
-- Crea la tabla para almacenar las preferencias de consultorio por usuario.
-- Permite al auxiliar recordar el último consultorio que estaba gestionando.
-- ============================================
-- Fecha: 16 de septiembre de 2026
-- ============================================

USE [SmileTrack];
GO

-- Verificar si la tabla ya existe
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UsuariosPreferenciasConsultorio]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UsuariosPreferenciasConsultorio] (
        [IdPreferencia]   INT           IDENTITY(1,1) PRIMARY KEY,
        [IdUsuario]       INT           NOT NULL,
        [IdConsultorio]   INT           NOT NULL,
        [ActualizadoEn]   DATETIME2(7)  NOT NULL DEFAULT GETUTCDATE(),
        
        -- Foreign Keys
        CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Usuario] 
            FOREIGN KEY ([IdUsuario]) REFERENCES [dbo].[Usuarios]([IdUsuario]) ON DELETE CASCADE,
        
        CONSTRAINT [FK_UsuariosPreferenciasConsultorio_Consultorio] 
            FOREIGN KEY ([IdConsultorio]) REFERENCES [dbo].[Consultorios]([IdConsultorio]) ON DELETE CASCADE,
        
        -- Constraint: Un usuario solo puede tener una preferencia
        CONSTRAINT [UQ_UsuariosPreferenciasConsultorio_Usuario] UNIQUE ([IdUsuario])
    );

    PRINT 'Tabla UsuariosPreferenciasConsultorio creada exitosamente.';
END
ELSE
BEGIN
    PRINT 'La tabla UsuariosPreferenciasConsultorio ya existe.';
END
GO

-- Índice para mejorar búsquedas por usuario
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_UsuariosPreferenciasConsultorio_Usuario' AND object_id = OBJECT_ID('UsuariosPreferenciasConsultorio'))
BEGIN
    CREATE INDEX [IX_UsuariosPreferenciasConsultorio_Usuario] 
        ON [dbo].[UsuariosPreferenciasConsultorio]([IdUsuario]);
    
    PRINT 'Índice IX_UsuariosPreferenciasConsultorio_Usuario creado exitosamente.';
END
GO

PRINT 'Migración UsuariosPreferenciasConsultorio completada.';
GO
