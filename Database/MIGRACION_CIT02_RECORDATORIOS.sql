/*
  CIT-02 - Reserva distribuida y reintentos de recordatorios.

  Migración incremental SQL Server. El repositorio no tiene historial/snapshot de
  migraciones EF Core y despliega bases mediante scripts SQL. No ejecutar al iniciar.

  No elimina filas ni actualiza columnas legadas. Las filas de Recordatorio_Cita
  existentes conservan valores y reciben NULL en las columnas nuevas. El worker no
  reclama recordatorios sin ProgramacionId; una reprogramación posterior los invalida.
  La identidad nueva de Cita se agrega con default SQL para cubrir citas existentes.
  Ejecutar PRECHECK_CIT02_RECORDATORIOS.sql antes y revisar envíos legados ambiguos.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Cita', 'programacion_id') IS NOT NULL
    THROW 51002, 'CIT-02 ya fue aplicado parcialmente o por completo en Cita.', 1;
IF COL_LENGTH('dbo.Recordatorio_Cita', 'programacion_id') IS NOT NULL
    THROW 51002, 'CIT-02 ya fue aplicado parcialmente o por completo en Recordatorio_Cita.', 1;

ALTER TABLE dbo.Cita
    ADD programacion_id uniqueidentifier NOT NULL
        CONSTRAINT DF_Cita_ProgramacionId DEFAULT (NEWID());

ALTER TABLE dbo.Recordatorio_Cita ADD
    programacion_id uniqueidentifier NULL,
    proximo_intento_en datetime2(7) NULL,
    reserva_id uniqueidentifier NULL,
    reserva_hasta datetime2(7) NULL;
GO

ALTER TABLE dbo.Recordatorio_Cita WITH CHECK ADD CONSTRAINT CK_Recordatorio_Cita_Reserva
    CHECK ((reserva_id IS NULL AND reserva_hasta IS NULL) OR
           (reserva_id IS NOT NULL AND reserva_hasta IS NOT NULL));
ALTER TABLE dbo.Recordatorio_Cita WITH CHECK ADD CONSTRAINT CK_Recordatorio_Cita_PendienteProximoIntento
    CHECK (programacion_id IS NULL OR estado <> 'pendiente' OR proximo_intento_en IS NOT NULL);

/* El filtro conserva legado NULL y evita duplicados para todas las nuevas programaciones. */
CREATE UNIQUE NONCLUSTERED INDEX UX_Recordatorio_Cita_Programacion
    ON dbo.Recordatorio_Cita(id_cita, canal, programacion_id)
    WHERE programacion_id IS NOT NULL;

/* Se conserva IX_Recordatorio_Estado_Fecha, usado por el esquema desplegado actual. */
CREATE NONCLUSTERED INDEX IX_Recordatorio_Cita_Reclamo
    ON dbo.Recordatorio_Cita(estado, proximo_intento_en, reserva_hasta, id_recordatorio);

COMMIT TRANSACTION;
GO

/* Debe devolver 0 filas; NULL corresponde a registros legados que no se mutaron. */
SELECT id_recordatorio FROM dbo.Recordatorio_Cita
WHERE programacion_id IS NOT NULL
  AND ((estado = 'pendiente' AND proximo_intento_en IS NULL)
       OR (reserva_id IS NULL AND reserva_hasta IS NOT NULL)
       OR (reserva_id IS NOT NULL AND reserva_hasta IS NULL));

SELECT id_cita, canal, programacion_id, COUNT(*) AS duplicados
FROM dbo.Recordatorio_Cita
WHERE programacion_id IS NOT NULL
GROUP BY id_cita, canal, programacion_id
HAVING COUNT(*) > 1;
