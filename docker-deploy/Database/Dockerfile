# ================================================================
# SmileTrack — Dockerfile para SQL Server 2022 con inicialización
# Unifica el motor de SQL Server y el db-init en un solo contenedor
# ================================================================

FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

# Crear directorio para scripts y esquema SQL
RUN mkdir -p /docker-entrypoint-initdb.d

# Copiar scripts y esquema SQL a la imagen
COPY SCRIPT_SQL_DEPLOYMENT_SMILETRACK.sql /docker-entrypoint-initdb.d/SCRIPT_SQL_UNICO_SMILETRACK.sql
COPY init-db.sh /docker-entrypoint-initdb.d/init-db.sh
COPY entrypoint.sh /docker-entrypoint-initdb.d/entrypoint.sh
COPY healthcheck.sh /docker-entrypoint-initdb.d/healthcheck.sh

# Otorgar permisos de ejecución y asignar propiedad al usuario mssql
RUN chmod +x /docker-entrypoint-initdb.d/init-db.sh \
             /docker-entrypoint-initdb.d/entrypoint.sh \
             /docker-entrypoint-initdb.d/healthcheck.sh && \
    chown -R mssql:0 /docker-entrypoint-initdb.d

# Ejecutar bajo el usuario estándar mssql
USER mssql

# Healthcheck integrado: verifica motor + BD SmileTrackDB inicializada
HEALTHCHECK --interval=10s --timeout=10s --retries=20 --start-period=60s \
  CMD /docker-entrypoint-initdb.d/healthcheck.sh

ENTRYPOINT ["/docker-entrypoint-initdb.d/entrypoint.sh"]
