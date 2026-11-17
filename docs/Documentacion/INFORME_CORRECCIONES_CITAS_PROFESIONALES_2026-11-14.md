# Informe de Correcciones y Pruebas — Gestión de Citas y Gestión de Profesionales

**Fecha:** 2026-11-14
**Alcance:** Módulos *Gestión de Citas* y *Gestión de Profesionales* (SmileTrack_MVC).
**Motivo:** Los datos almacenados en la base de datos no se mostraban correctamente en las vistas (endpoint JSON con HTTP 500 y errores SQL en tiempo de ejecución).

---

## 1. Causa raíz

El esquema de la base de datos estaba **desincronizado respecto a las entidades EF Core**
(`Data/AppDbContext.cs`). Las entidades mapean columnas y tablas que no existían en SQL Server, lo que
provocaba en tiempo de ejecución:

| Error SQL | Descripción | Origen |
|-----------|-------------|--------|
| **207** | Columna no válida | `Profesional.credenciales_aprobadas` (mapeada, inexistente) |
| **207** | Columna no válida | `Cita.motivo_cancelacion` (mapeada, inexistente) |
| **208** | Objeto no válido | Tabla `Credencial_Profesional` (mapeada, inexistente) |
| **208** | Objeto no válido | Tabla `Lista_Espera_Cita` (mapeada, inexistente) |

El endpoint `GET /api/profesionales` devolvía **HTTP 500** porque `ProfesionalService.ObtenerAsync`
consulta la entidad `Profesional` (`SELECT ... credenciales_aprobadas ...`), fallando con error 207.

> Se descartó un problema de datos: la BD contiene información válida
> (25 citas, 9 profesionales `activo`, 16 pacientes `activo`, 8 consultorios `disponible`).
> El defecto estaba en la **capa de esquema/mapeo**, no en los datos.

---

## 2. Correcciones aplicadas

### 2.1. Sincronización de esquema (causa raíz)

Se añadieron los objetos faltantes de forma **idempotente** en el script canónico
`Database/SCRIPT_SQL_UNICO_SMILETRACK.sql` (sección 12) y se aplicaron a la base de datos en vivo:

- Columna `Profesional.credenciales_aprobadas BIT NOT NULL DEFAULT 1`.
- Columna `Cita.motivo_cancelacion VARCHAR(500) NULL`.
- Tabla `Credencial_Profesional` (+ índice `IX_Credencial_Profesional_Revision` y FKs).
- Tabla `Lista_Espera_Cita` (+ FKs a `Paciente`, `Servicio`, `Profesional`).

### 2.2. Configuración duplicada/contradictoria en `AppDbContext`

- **Vistas SQL** (`vw_Citas_Dashboard`, `vw_Profesionales_Completo`): se eliminó la llamada
  `HasNoKey()` contradictoria con `HasKey()` (las entidades ya declaran `[Key]`).
- **`PagoFactura`**: se consolidó la configuración duplicada en un único bloque
  (incluyendo la propiedad `Observaciones` → columna `observaciones`).

### 2.3. Prueba unitaria fallida

- `CrearCita_CreatesAppointment` fallaba porque elegía "mañana" sin considerar que la clínica
  no atiende domingos (`dias_atencion` por defecto = 1..6).
  Se corrigió el test para desplazar la fecha cuando cae en domingo.

### 2.4. Hallazgos verificados que **no** requerían cambio

- `GET api/profesionales/especialidades`: protegido por el `[Authorize]` a nivel de clase
  (rol + política `ApiOrCookie`). No es un endpoint abierto.
- `CargarDatosProfesionales` / `CargarDatosCitas`: sus bloques `catch` **registran logs**
  (`LogCritical`/`LogError`/`LogWarning`); no son capturas silenciosas.
- Comparaciones de estado (`p.Estado == "activo"`): la columna almacena valores canónicos en
  minúsculas (CHECK constraint), por lo que no hay sensibilidad a mayúsculas en la práctica.

---

## 3. Pruebas y resultados

### 3.1. Compilación

```
dotnet build SmileTrack_MVC.csproj  ->  Compilación correcta. 0 Advertencia(s), 0 Errores
```

### 3.2. Pruebas automatizadas

```
dotnet test SmileTrack_MVC.Tests  ->  Correctas! Con error: 0, Superado: 453, Total: 453
```
(Antes de las correcciones: 1 fallo — `CrearCita_CreatesAppointment`.)

### 3.3. Pruebas funcionales / integración (HTTP, aplicación en ejecución)

Login real con `admin@smiletrack.co` (cookie + antiforgery), y consumo de endpoints/páginas:

| Recurso | Antes | Después |
|---------|-------|---------|
| `GET /api/profesionales?page=1&pageSize=3` | **500** | **200** |
| `GET /api/profesionales/stats` | — | 200 |
| `GET /api/profesionales/dashboard-stats` | — | 200 |
| `GET /api/profesionales/especialidades` | — | 200 |
| `GET /api/profesionales/1` | — | 200 |
| `GET /gestion-de-profesionales/st-adm-07-gestion-profesionales` | — | 200 |
| `GET /gestion-de-profesionales/st-adm-14-reportes-clinicos` | — | 200 |
| `GET /gestion-de-citas/st-rec-03-gestion-citas` | — | 200 |
| `GET /gestion-de-citas/st-rec-01-dashboard` | — | 200 |
| `GET /gestion-de-profesionales/st-odo-01-dashboard` | — | 200 |

Tras las peticiones, los logs del servidor **no registran errores 207/208 ni excepciones SQL**.

---

## 4. Archivos modificados

- `Database/SCRIPT_SQL_UNICO_SMILETRACK.sql` — sección 12 (corrección de drift de esquema).
- `Data/AppDbContext.cs` — vistas (`HasNoKey`/`HasKey`) y consolidación de `PagoFactura`.
- `SmileTrack_MVC.Tests/Unit/GestionCitasTests.cs` — fecha válida (evita domingo).

## 5. Notas

- `Lista_Espera_Cita` y `Credencial_Profesional` se recrean también en instalaciones nuevas
  vía el script canónico; los bloques son idempotentes (`IF COL_LENGTH` / `IF OBJECT_ID`).
- Las FKs `subido_por`/`revisado_por` de `Credencial_Profesional` se definieron **sin cascada**
  (`NO ACTION`) porque SQL Server rechaza múltiples rutas de cascada hacia `Usuario`.
