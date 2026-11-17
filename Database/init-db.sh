#!/bin/bash
# ================================================================
# SmileTrack — Inicialización automática de la base de datos
# Se ejecuta una sola vez cuando el contenedor SQL Server arranca
# ================================================================

set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
SQL_SERVER=${SQL_SERVER_HOST:-localhost}
DATABASE=SmileTrackDB
BOOTSTRAP_TABLE=dbo.SmileTrackBootstrap
BOOTSTRAP_ID=1
MIGRATION_DIRECTORY=/docker-entrypoint-initdb.d

if [ -z "${MSSQL_SA_PASSWORD:-}" ]; then
  echo "❌ MSSQL_SA_PASSWORD no está configurada."
  exit 1
fi

echo "⏳ Esperando que SQL Server esté listo..."
SQL_READY=0
for attempt in {1..60}; do
  if "$SQLCMD" -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" -C -b -Q "SELECT 1" >/dev/null 2>&1; then
    SQL_READY=1
    break
  fi
  sleep 2
done

if [ "$SQL_READY" -ne 1 ]; then
  echo "❌ SQL Server no estuvo disponible después de 120 segundos."
  exit 1
fi

echo "🔄 Verificando el estado de la base de datos..."
if ! DB_EXISTS=$("$SQLCMD" \
  -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -b -h -1 -W \
  -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DATABASE') IS NULL THEN 0 ELSE 1 END" \
  2>/dev/null); then
  echo "❌ No se pudo consultar el estado de la base de datos."
  exit 1
fi
DB_EXISTS=$(printf '%s' "$DB_EXISTS" | tr -d '[:space:]')

if [ "$DB_EXISTS" = "1" ]; then
  if ! "$SQLCMD" \
    -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
    -C -b -d "$DATABASE" \
    -Q "IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL OR OBJECT_ID(N'dbo.Profesional', N'U') IS NULL OR OBJECT_ID(N'dbo.Diagnostico_CIE10', N'U') IS NULL OR OBJECT_ID(N'dbo.Paciente', N'U') IS NULL THROW 51000, 'Database has no complete SmileTrack base schema; refusing to migrate it', 1;"; then
    echo "❌ La base existente no tiene el esquema base de SmileTrack; no se modificará."
    exit 1
  fi

  echo "🔄 Aplicando migraciones compatibles a la base existente..."
else
  if [ "$DB_EXISTS" != "0" ]; then
    echo "❌ SQL Server devolvió un estado inesperado para la base de datos."
    exit 1
  fi

  echo "🚀 Creando la base de datos desde el script SQL..."
  if ! "$SQLCMD" \
    -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
    -C -b -i "$MIGRATION_DIRECTORY/SCRIPT_SQL_UNICO_SMILETRACK.sql"; then
    echo "❌ Error al ejecutar el script SQL."
    exit 1
  fi
fi

for migration in MIGRATION_Consentimiento.sql MIGRATION_Reporte_Generado.sql upgrade-auth-identifiers.sql; do
  if ! "$SQLCMD" \
    -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
    -C -b -d "$DATABASE" \
    -i "$MIGRATION_DIRECTORY/$migration"; then
    echo "❌ Error al ejecutar $migration."
    exit 1
  fi
  echo "✅ Migración aplicada: $migration"
done

if ! SCHEMA_READY=$("$SQLCMD" \
  -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -b -d "$DATABASE" -h -1 -W \
  -Q "
    SET NOCOUNT ON;
    SELECT CASE
      WHEN OBJECT_ID(N'dbo.Usuario', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Profesional', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Diagnostico_CIE10', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Paciente', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.RegistroConsentimiento', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Reporte_Generado', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.Usuario', N'version_consentimiento') IS NOT NULL
       AND COL_LENGTH(N'dbo.Usuario', N'fecha_consentimiento') IS NOT NULL
       AND COL_LENGTH(N'dbo.Usuario', N'ip_consentimiento') IS NOT NULL
       AND COL_LENGTH(N'dbo.Usuario', N'bloqueado_hasta') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.Usuario')
             AND name = N'UX_Usuario_Correo'
             AND is_unique = 1
             AND is_disabled = 0
       )
       AND EXISTS (
           SELECT 1
           FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.Paciente')
             AND name = N'UX_Paciente_TipoDocumento_Documento'
             AND is_unique = 1
             AND is_disabled = 0
       )
      THEN 1
      ELSE 0
    END"
  ); then
  echo "❌ No se pudo validar el esquema de SmileTrack."
  exit 1
fi
SCHEMA_READY=$(printf '%s' "$SCHEMA_READY" | tr -d '[:space:]')
if [ "$SCHEMA_READY" != "1" ]; then
  echo "❌ El esquema de SmileTrack sigue incompleto después de las migraciones."
  exit 1
fi

echo "🔖 Registrando la inicialización completada..."
if ! "$SQLCMD" \
  -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -b -d "$DATABASE" \
  -Q "IF OBJECT_ID(N'$BOOTSTRAP_TABLE', N'U') IS NULL BEGIN CREATE TABLE $BOOTSTRAP_TABLE (id tinyint NOT NULL PRIMARY KEY, completed_at_utc datetime2(0) NOT NULL); END; IF NOT EXISTS (SELECT 1 FROM $BOOTSTRAP_TABLE WHERE id = $BOOTSTRAP_ID) INSERT INTO $BOOTSTRAP_TABLE (id, completed_at_utc) VALUES ($BOOTSTRAP_ID, SYSUTCDATETIME());"; then
  echo "❌ No se pudo registrar la inicialización completada."
  exit 1
fi

echo "✅ Base de datos inicializada o actualizada correctamente."
