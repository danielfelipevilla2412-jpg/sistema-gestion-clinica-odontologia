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
  if "$SQLCMD" \
    -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
    -C -b -d "$DATABASE" \
    -Q "IF NOT EXISTS (SELECT 1 FROM $BOOTSTRAP_TABLE WHERE id = $BOOTSTRAP_ID) THROW 51000, 'Database exists but bootstrap is incomplete', 1;" \
    >/dev/null 2>&1; then
    echo "✅ La base de datos ya estaba inicializada."
    exit 0
  fi

  echo "❌ La base de datos existe, pero no tiene una inicialización completa. No se modificará."
  exit 1
fi

if [ "$DB_EXISTS" != "0" ]; then
  echo "❌ SQL Server devolvió un estado inesperado para la base de datos."
  exit 1
fi

echo "🚀 Creando la base de datos desde el script SQL..."
if ! "$SQLCMD" \
  -S "$SQL_SERVER" -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -b -i /docker-entrypoint-initdb.d/SCRIPT_SQL_UNICO_SMILETRACK.sql; then
  echo "❌ Error al ejecutar el script SQL."
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

echo "✅ Base de datos inicializada correctamente."
