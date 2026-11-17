# Autenticación, registro y sesiones

## Alcance

La aplicación autentica cuentas almacenadas en SQL Server. El formulario permite seleccionar el rol esperado, pero el servidor lo contrasta con el rol guardado en la cuenta; los claims y la redirección siempre se derivan de la base de datos. Un rol manipulado o que no corresponda se rechaza.

El registro público crea exclusivamente cuentas de tipo **Paciente**. Las cuentas de Administrador, Profesional, Recepcionista y Auxiliar deben ser creadas o asignadas por un proceso administrativo autorizado; no se deben habilitar roles privilegiados en el formulario público.

## Inventario de cuentas semilla y credenciales de prueba

El archivo `Database/SCRIPT_SQL_UNICO_SMILETRACK.sql` contiene nueve cuentas semilla. El inventario por identificador y estado es:

| Rol | Usuarios semilla | Estado |
|---|---|---|
| Administrador | ID 1 | Activo |
| Profesional | ID 3, 6, 8, 9 | IDs 3, 6 y 8 activos; ID 9 inactivo |
| Paciente | ID 2, 7 | Activos |
| Recepcionista | ID 4 | Activo |
| Auxiliar | ID 5 | Activo |

Estos son los registros del script, no una consulta a la base que esté ejecutándose. El script principal de desarrollo incluye las cinco cuentas de prueba siguientes con hashes BCrypt verificados:

| Rol | Correo | Contraseña de prueba |
|---|---|---|
| Administrador | `admin@smiletrack.co` | `123456` |
| Profesional | `prof@smiletrack.co` | `123456` |
| Paciente | `pac@smiletrack.co` | `123456` |
| Recepcionista | `recep@smiletrack.co` | `123456` |
| Auxiliar | `aux@smiletrack.co` | `123456` |

`Database/seed-usuarios-prueba.sql` y `Database/SEED_USUARIOS_PRUEBA.sql` también crean o actualizan estas cuentas para desarrollo/demo. Ambos restablecen sus credenciales a `123456`; el segundo además prepara fichas de prueba. Ejecútalos solo en una base ficticia/de desarrollo, nunca en producción.

Las pruebas automatizadas verifican los cinco accesos en EF InMemory, la coincidencia del rol y los hashes presentes en los scripts. No alteran cuentas ni datos de SQL.

## Flujos

### Registro

1. El servidor valida correo, nombre, apellidos, documento, fecha de nacimiento, teléfono y aceptación del tratamiento de datos.
2. El correo se recorta y se normaliza a minúsculas. El documento y su tipo se normalizan antes de comprobar duplicados.
3. La aplicación rechaza correos o documentos ya registrados y la base de datos refuerza esa regla con índices únicos.
4. Se genera un hash BCrypt (work factor 11); nunca se almacena la contraseña en texto plano.
5. Se crea el usuario con rol Paciente y su ficha vinculada. La cuenta puede iniciar sesión inmediatamente.

### Inicio de sesión

1. El servidor normaliza el correo y busca la cuenta independientemente del rol.
2. La contraseña y el estado de la cuenta se validan en `AuthService`.
3. El servidor genera la identidad, los claims de rol y, si aplica, el identificador de perfil de Paciente o Profesional a partir de SQL.
4. El destino inicial se decide con el rol de la cuenta; un rol o destino aportado por el cliente no sustituye esa decisión.
5. Ante credenciales inválidas se presenta un mensaje genérico, evitando revelar si el correo está registrado.

### Recuperación y cambio de contraseña

- La recuperación requiere el código enviado al correo y un token temporal validado por el servidor.
- El cambio autenticado requiere la contraseña actual.
- Registro, recuperación y cambio de contraseña exigen entre 8 y 100 caracteres, al menos una mayúscula ASCII, un dígito y un símbolo.
- Al recuperar o cambiar una contraseña se actualiza `UltimoLogout`, se limpia el bloqueo y se invalidan las sesiones anteriores. El usuario debe volver a iniciar sesión.

## Protección de cuenta y sesión

- Tras tres contraseñas incorrectas, la cuenta se bloquea por 15 minutos. Un inicio de sesión correcto o una contraseña actualizada reinicia los intentos.
- Los límites por IP configurados para los endpoints de inicio de sesión y registro también aplican; no los sustituye el bloqueo por cuenta.
- La cookie de autenticación es `HttpOnly`, `SameSite=Lax` y `Secure` según el esquema de la petición. Su duración es de 8 horas, con expiración deslizante. La opción “Recordarme” determina si es persistente.
- Las vistas MVC usan cookie; las APIs aceptan JWT y verifican contra la base de datos que el usuario siga activo y que el token no sea anterior a la última revocación.
- Las acciones que modifican estado deben conservar la protección antiforgery cuando usan autenticación por cookie. No registrar contraseñas, códigos de recuperación, tokens ni claves en logs.
- En producción, servir la aplicación únicamente mediante HTTPS y conservar de forma persistente y protegida las claves de ASP.NET Data Protection; si se ejecutan varias réplicas, compartir ese almacén entre ellas.

## Configuración antes de desplegar

1. Definir una clave JWT aleatoria y privada de al menos 32 caracteres, junto con emisor y audiencia. Usar User Secrets en desarrollo y variables de entorno o un gestor de secretos en producción; no almacenar secretos en el repositorio.
2. Configurar la conexión SQL Server y, para recuperación, los parámetros SMTP. Verificar el envío desde una cuenta de prueba antes de publicar el flujo.
3. En despliegues detrás de proxy, configurar el proxy confiable y los encabezados reenviados conforme a `deployment/README.md`. No exponer SQL Server a Internet.
4. Hacer una copia de seguridad de la base productiva antes de modificar el esquema.

## Actualización de bases existentes

Para instalaciones existentes, realizar una copia de seguridad y ejecutar las migraciones requeridas antes de desplegar. `Database/MIGRATION_Consentimiento.sql` agrega idempotentemente las columnas que requiere `Usuario` y la tabla de auditoría de consentimiento; `Database/MIGRATION_Reporte_Generado.sql` crea la tabla de reportes y su índice. También revisar y ejecutar `Database/upgrade-auth-identifiers.sql` antes de desplegar la versión que depende de sus nuevos campos e índices.

El script:

- Detecta correos y documentos duplicados después de normalizar y aborta la transacción si encuentra alguno.
- Normaliza correo y documento, agrega `bloqueado_hasta` cuando falta y crea índices únicos.
- Inicializa un bloqueo temporal para cuentas que ya acumulan tres o más intentos.

No ejecutar el script a ciegas en producción: primero respaldar, revisar los duplicados que reporte y resolverlos según las reglas de negocio. Probarlo antes en una copia de la base. Los scripts completos para instalaciones nuevas contienen estos esquemas. El script principal conserva la definición de `Diagnostico_CIE10`, pero su carga de datos está separada en `Database/SEED_CIE10_SISPRO.sql`.

## Verificación

Ejecutar pruebas automatizadas:

```powershell
dotnet test .\SmileTrack_MVC.Tests\SmileTrack_MVC.Tests.csproj --filter "FullyQualifiedName~AccesoSeguridadControllerTests" --verbosity minimal
dotnet test .\SmileTrack_MVC.Tests\SmileTrack_MVC.Tests.csproj --verbosity minimal
```

En un entorno de pruebas con cuentas ficticias, comprobar además:

- Inicio de sesión de varias cuentas del mismo rol y de distintos roles, confirmando que cada una recibe sus claims y datos de perfil.
- Rechazo de cuenta inactiva, contraseña inválida, rol manipulado y `returnUrl` externo.
- Registro de Paciente, inicio de sesión posterior y rechazo de correo/documento duplicados tras normalizar.
- Bloqueo y expiración después de tres fallos, recuperación mediante código de un solo uso y rechazo de contraseñas débiles.
- Cambio y recuperación de contraseña: la contraseña anterior deja de funcionar, la nueva funciona y las sesiones/JWT anteriores son rechazados.
- Aislamiento entre dos usuarios al navegar y llamar a APIs; ninguna consulta debe derivar la identidad de un identificador de usuario suministrado por el cliente.

No usar cuentas reales ni datos clínicos en pruebas. La verificación automatizada local no sustituye las pruebas contra una base efímera con SQL Server ni la validación del correo y del proxy HTTPS del entorno de despliegue.

## Criterios de salida

- No hay roles privilegiados disponibles en el registro público.
- Identidad, rol y perfil proceden de la cuenta autenticada en la base.
- Las restricciones de correo/documento están aplicadas tanto en el servicio como en SQL Server.
- Las reglas de contraseña, bloqueo temporal, antiforgery y expiración/revocación de sesión están probadas.
- Secretos y claves de Data Protection persisten correctamente en producción; HTTPS y SMTP se verifican antes de habilitar el servicio públicamente.
