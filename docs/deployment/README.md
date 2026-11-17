# Paquete de despliegue de SmileTrack

Este paquete ejecuta la imagen publicada `johanmaa2/smiletrack-web:latest` junto con SQL Server Express. La primera inicializacion crea el esquema y los catalogos basicos de roles y estados.

## Contenido

- `docker-compose.prod.yml`: servicios web, SQL Server e inicializador.
- `.env.production.example`: plantilla de variables; no contiene credenciales reales.
- `Database/SCRIPT_SQL_DEPLOYMENT_SMILETRACK.sql`: esquema limpio y catalogos minimos.
- `Database/init-db.sh`: inicializa la base de datos una vez.
- `Database/MIGRATION_Consentimiento.sql` y `Database/MIGRATION_Reporte_Generado.sql`: actualizan bases existentes.
- `docs/Documentacion/AUTENTICACION_REGISTRO_Y_SESIONES.md`: flujos, controles de seguridad y pruebas de autenticacion.

El SQL de despliegue no copia usuarios, pacientes, historias clinicas, citas, auditorias, codigos de recuperacion, pagos, PQR, servicios/precios ni el catalogo CIE-10 del entorno de desarrollo. No crea una cuenta de acceso. Antes de usar la aplicacion, prepara un proceso seguro para crear la cuenta administradora y configurar los datos propios de la organizacion.

El esquema de despliegue incluye la estructura de `Diagnostico_CIE10`, `RegistroConsentimiento` y `Reporte_Generado`, pero no incluye datos CIE-10 ni cuentas de prueba. El seed CIE-10 y los seeds de usuarios con contraseña `123456` son exclusivamente para desarrollo/demo; no los ejecutes en producción.

La portada lee del SQL los servicios activos, precios, categorias y duraciones. La base limpia inicia sin servicios, por lo que la portada indicara que no hay servicios publicados hasta que el administrador configure el catalogo.

## Requisitos

- Servidor Linux con Docker Engine y Docker Compose v2.
- Acceso de red a Docker Hub y a las imagenes de Microsoft.
- Dominio con sus registros DNS apuntando a la IP publica del servidor.
- Un proxy inverso con HTTPS. El Compose incluido solo publica HTTP en el puerto 80; no configura TLS.

## Arranque local y diagnóstico

En Windows, abre Docker Desktop y espera a que el motor indique que está en ejecución. En Linux, inicia el servicio Docker con el gestor de servicios de la distribución. Comprueba el daemon antes de iniciar la aplicación:

```sh
docker info
```

Para el Compose local desde la raíz del repositorio:

```powershell
Copy-Item .env.example .env
# Completa MSSQL_SA_PASSWORD y JWT_KEY en .env; no compartas el archivo.
docker compose config --quiet
docker compose up -d --build
docker compose ps
curl.exe -fsS http://127.0.0.1:5259/health
```

Espera a que `sqlserver` esté `healthy`, `db-init` termine con código 0 y `web` esté `Up`. Si alguno falla:

```sh
docker compose logs --tail=100 sqlserver db-init web
```

Si la compilacion muestra `Read-only file system` o el daemon no puede leer/escribir su cache, revisa el espacio libre del disco del host (`Get-PSDrive C` en PowerShell y `docker system df`). Libera espacio desde Windows y reinicia Docker Desktop antes de reconstruir. No borres el disco virtual de Docker ni uses `docker compose down -v`: los volumenes contienen la base de datos, las claves de proteccion y los archivos clinicos. `docker system df` tambien puede fallar mientras el almacenamiento de Docker esta en modo de solo lectura.

El SQL de autenticación añade `bloqueado_hasta` y los índices únicos. En bases preexistentes, el inicializador no vuelve a ejecutar el esquema semilla; tras respaldar, aplica `Database/upgrade-auth-identifiers.sql` una sola vez:

```sh
docker exec -i smiletrack-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d SmileTrackDB -i /dev/stdin' < Database/upgrade-auth-identifiers.sql
```

En PowerShell, usa esta variante para enviar el archivo:

```powershell
Get-Content .\Database\upgrade-auth-identifiers.sql -Raw |
  docker exec -i smiletrack-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d SmileTrackDB -i /dev/stdin'
```

Antes de desplegar una versión que use el consentimiento o los reportes, respalda la base existente y aplica también las migraciones idempotentes correspondientes desde la raíz del repositorio:

```powershell
Get-Content .\Database\MIGRATION_Consentimiento.sql -Raw |
  docker exec -i smiletrack-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d SmileTrackDB -i /dev/stdin'
Get-Content .\Database\MIGRATION_Reporte_Generado.sql -Raw |
  docker exec -i smiletrack-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d SmileTrackDB -i /dev/stdin'
```

No uses `docker compose down -v` para solucionar fallos de arranque: borra la base y los demás volúmenes persistentes. `docker compose down` detiene los servicios sin borrar los datos.

## Persistencia y copias de seguridad

La base, las claves de ASP.NET Data Protection y los archivos clínicos están en volúmenes nombrados. Los volúmenes sobreviven a `docker compose down`, pero no protegen contra la pérdida del servidor o del disco. Programa copias de seguridad de SQL y guarda una copia fuera del host Docker. Ejemplo Linux:

```sh
BACKUP_FILE="SmileTrackDB-$(date +%Y%m%d-%H%M%S).bak"
docker exec -i smiletrack-sqlserver bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -d master -i /dev/stdin' <<SQL
BACKUP DATABASE [SmileTrackDB] TO DISK=N'/var/opt/mssql/data/${BACKUP_FILE}' WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK=N'/var/opt/mssql/data/${BACKUP_FILE}' WITH CHECKSUM;
SQL
mkdir -p ./backups
CONTAINER_ID=$(docker compose ps -q sqlserver)
docker cp "${CONTAINER_ID}:/var/opt/mssql/data/${BACKUP_FILE}" ./backups/
```

Restringe el acceso al directorio `backups`, cifra las copias fuera del host y prueba periódicamente la restauración en otra instancia. No guardes las únicas copias dentro del volumen de SQL.

## Instalacion

1. Copia el contenido del ZIP al servidor y entra a esa carpeta.
2. Copia la plantilla y edita los valores:

   ```sh
   cp .env.production.example .env
   chmod 600 .env
   ```

3. Configura `MSSQL_SA_PASSWORD` y `JWT_KEY` con secretos unicos. Configura SMTP con credenciales de aplicacion si la clinica enviara correos. No compartas ni subas el `.env`.
4. Mantén `REDIRECT_HTTP_TO_HTTPS=true` para produccion. Configura `KNOWN_PROXY_IP` con la IP del proxy inverso que conecta directamente a la aplicacion; esto permite validar `X-Forwarded-Proto` y emitir cookies seguras cuando el navegador usa HTTPS.
5. Verifica la configuracion sin mostrar los secretos:

   ```sh
   docker compose -f docker-compose.prod.yml config --quiet
   ```

6. Si actualizas una base existente, respáldala y sigue la sección **Actualización de bases existentes** en la [guía de autenticación](../docs/Documentacion/AUTENTICACION_REGISTRO_Y_SESIONES.md). No ejecutes el script de actualización antes de revisar posibles duplicados.
7. Inicia los servicios:

   ```sh
   docker compose -f docker-compose.prod.yml up -d
   docker compose -f docker-compose.prod.yml ps
   ```

8. Revisa el inicio si algun servicio no queda activo:

   ```sh
   docker compose -f docker-compose.prod.yml logs --tail=100 web db-init sqlserver
   ```

## Dominio, HTTPS y datos

- El contenedor web escucha HTTP en el puerto 80. Configura un proxy inverso con certificado TLS para aceptar HTTPS del dominio, reenviar el trafico a ese puerto y establecer `X-Forwarded-Proto` y `X-Forwarded-For`. La aplicacion redirige HTTP a HTTPS en produccion por defecto.
- La cookie de autenticacion usa `SameAsRequest`: se marca `Secure` al atender una peticion HTTPS (incluido HTTPS terminado en proxy confiable) y permite HTTP en el Compose local. No desactives la redireccion HTTP en un sitio publico.
- El Compose local sirve HTTP en `http://127.0.0.1:5259`; sus cookies se adaptan al esquema local para que el inicio de sesion funcione sin certificado.
- No publiques el puerto 1433 de SQL Server en Internet. El Compose lo mantiene dentro de la red Docker.
- SQL Server Express y los archivos clinicos usan volumenes persistentes. Configura copias de seguridad y prueba la restauracion.
- No ejecutes `docker compose down -v` en un entorno con datos que deban conservarse: elimina los volumenes.
- La imagen publicada es para `linux/amd64`.

El inicializador solo crea una base nueva. Si `SmileTrackDB` ya existe pero no tiene la marca de inicializacion completa, detiene el despliegue para evitar modificarla automaticamente.
