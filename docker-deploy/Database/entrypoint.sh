#!/bin/bash
set -e

# ================================================================
# SmileTrack — Entrypoint personalizado para SQL Server 2022
# Ejecuta la inicialización de la BD en segundo plano y el motor SQL en primer plano
# ================================================================

# Iniciar el script de inicialización en segundo plano
/docker-entrypoint-initdb.d/init-db.sh &

# Iniciar SQL Server en primer plano (hereda PID 1 para gestión de señales de parada)
exec /opt/mssql/bin/sqlservr "$@"
