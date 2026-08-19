# Base de datos — SmileTrack (guía multiplataforma)

Antes el proyecto solo conectaba con **SQL Server LocalDB**, que únicamente existe en Windows.
Ahora cualquier miembro del equipo (Windows, macOS o Linux) puede levantar la misma base de
datos con Docker, sin instalar SQL Server directamente en su máquina.

## Requisito único: Docker Desktop

Instalar Docker Desktop (Windows, Mac o Linux): https://www.docker.com/products/docker-desktop/

## Pasos (los mismos en cualquier sistema operativo)

1. Desde la carpeta del proyecto, levantar la base de datos:
   ```
   docker compose up -d
   ```
   Esto crea un contenedor `smiletrack-sqlserver` con SQL Server 2022, escuchando en el puerto
   `1433`, con los datos guardados en un volumen (persisten aunque apagues el contenedor).

2. Copiar la plantilla de configuración local (este archivo NO se sube a git, cada quien tiene
   la suya):
   - Windows (PowerShell): `copy appsettings.Local.json.example appsettings.Local.json`
   - Mac/Linux: `cp appsettings.Local.json.example appsettings.Local.json`

3. Ejecutar la aplicación normalmente (`dotnet run` o desde Visual Studio/Rider). Al iniciar,
   `Program.cs` detecta `appsettings.Local.json` y lo usa en lugar de la cadena de conexión de
   LocalDB. La primera vez creará el esquema y los datos de prueba automáticamente.

## ¿Y si alguien del equipo prefiere seguir usando LocalDB en Windows?

Puede seguir haciéndolo: si NO crea `appsettings.Local.json`, la app cae de vuelta a la cadena
de conexión de LocalDB definida en `appsettings.json`, tal como funcionaba antes. Docker es
opcional para quien ya tiene LocalDB funcionando, pero es la única opción para Mac/Linux.

## Apagar / reiniciar la base de datos

```
docker compose stop        # apaga el contenedor, conserva los datos
docker compose up -d       # lo vuelve a encender
docker compose down -v     # borra el contenedor Y los datos (empezar de cero)
```

## Usuarios de prueba (se crean automáticamente al iniciar)

| Rol            | Correo                  | Contraseña |
|----------------|--------------------------|------------|
| Administrador  | admin@smiletrack.co      | 123456     |
| Profesional    | prof@smiletrack.co       | 123456     |
| Recepcionista  | recep@smiletrack.co      | 123456     |
| Auxiliar       | aux@smiletrack.co        | 123456     |
| Paciente       | pac@smiletrack.co        | 123456     |
