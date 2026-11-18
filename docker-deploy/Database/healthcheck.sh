#!/bin/bash
set -eo pipefail

# ================================================================
# SmileTrack — Script de Healthcheck para SQL Server
# Verifica disponibilidad del motor y que la base de datos esté inicializada
# ================================================================

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [ ! -x "$SQLCMD" ]; then
  if [ -x /opt/mssql-tools/bin/sqlcmd ]; then
    SQLCMD=/opt/mssql-tools/bin/sqlcmd
  else
    SQLCMD=$(command -v sqlcmd || echo "sqlcmd")
  fi
fi

if [ -z "${MSSQL_SA_PASSWORD:-}" ]; then
  exit 1
fi

# Verifica conexión y existencia del registro de bootstrap en SmileTrackDB
"$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d SmileTrackDB -Q "SELECT 1 FROM dbo.SmileTrackBootstrap WHERE id = 1" >/dev/null 2>&1
