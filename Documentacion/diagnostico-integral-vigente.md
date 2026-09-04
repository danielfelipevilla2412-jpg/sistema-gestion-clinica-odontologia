# Diagnóstico integral vigente

Fecha: 2026-09-04
Estado: Fase 0 completada mediante inspección estática
Alcance: Citas, Profesionales y sus dependencias directas

## 1. Resumen ejecutivo

La aplicación es un monolito ASP.NET Core MVC sobre .NET 9 con Entity Framework Core y SQL Server. Combina vistas Razor/SSR, JavaScript con `fetch`, rutas MVC y APIs REST parciales. La autenticación usa Cookie y JWT, con una política `ApiOrCookie` y antiforgery para operaciones mutables.

La arquitectura objetivo es viable, pero actualmente existen varias entradas para Citas y Profesionales. El servicio de Citas ya concentra parte importante de las validaciones, aunque hay lógica MVC duplicada y contratos que deben verificarse antes de consolidar. Las entidades de disponibilidad profesional existen en el modelo, pero su uso efectivo en la creación/actualización de citas debe comprobarse en la implementación.

No se modificó código de aplicación durante este diagnóstico. Este documento es la fuente vigente; los documentos anteriores se consideran históricos.

## 2. Inventario técnico

### Capas principales

- Entrada y configuración: `Program.cs`, `SmileTrack_MVC.csproj`, `appsettings.json`, `appsettings.Local.json`, `docker-compose.yml`, `Dockerfile`.
- Controllers MVC: Acceso y Seguridad, Centro de Ayuda, Facturación y Pagos, Gestión de Citas, Gestión de Pacientes, Gestión de Usuarios, Historia Clínica, Perfiles, PQR, Profesionales, Público, Reportes, Servicios y Recursos y ViewProxy.
- API Controllers: Citas, Pacientes, Profesionales, Usuarios, Historia Clínica y Reportes.
- Servicios e interfaces: Auth, Citas, Pacientes, Profesionales, Usuarios, Reportes, Historia Clínica y Email.
- Datos: `Data/AppDbContext.cs`, entidades en `Models/Entities`, DTOs en `Models/DTOs` y `Models/Api`, ViewModels y modelos compartidos de paginación/estadísticas.
- Cliente: scripts por módulo en `wwwroot/js`, utilidades compartidas, `apiRequest.js`, formularios Razor y llamadas SSR/MVC.
- Pruebas: proyecto `SmileTrack_MVC.Tests`, con pruebas activas limitadas y pruebas históricamente deshabilitadas mediante `#if false`.
- Base de datos: `Database/SCRIPT_SQL_UNICO_SMILETRACK.sql`, EF Core y configuración SQL Server.

### Dependencias de Citas

`Cita` se relaciona con `Paciente`, `Profesional`, `Servicio`, `Consultorio` y `EstadoCita`. `Profesional` se relaciona con `Usuario`, `Especialidad`, `HorarioProfesional`, `AusenciaProfesional`, `BloqueoProfesional` y `ProfesionalServicio`.

El contexto declara DbSets para estas entidades, además de configuraciones de usuario, pacientes, profesionales, servicios, consultorios, estados, auditoría y configuración general.

### Citas y servicios

`ICitaService` declara operaciones de listado paginado, consulta por ID, creación, actualización, cambio de estado, notas, cancelación, conflictos, duración y validación de horario clínico. `CitaService` persiste mediante `AppDbContext` y contiene validaciones de entidades y conflictos que deben compararse con las rutas MVC.

### Profesionales y servicios

`IProfesionalService`/`ProfesionalService` soportan CRUD, estado, especialidades, disponibilidad y una vista MVC paginada con estadísticas. `ProfesionalesController` expone la vista administrativa y `ProfesionalesApiController` expone `/api/profesionales`.

## 3. Inventario de las 19 vistas asignadas

| Vista | Ruta de vista | Superficie observada | Tratamiento |
|---|---|---|---|
| st-adm-01-dashboard | `Views/Gestion_De_Citas/st-adm-01-dashboard` | Dashboard MVC/JS | Congelada |
| st-adm-08-agenda | `Views/Gestion_De_Citas/st-adm-08-agenda` | Agenda y consumo API | Congelada |
| st-adm-09-citas | `Views/Gestion_De_Citas/st-adm-09-citas` | Razor, formularios, `form.submit()` | Congelada; revisar compatibilidad |
| st-aux-01-panel-operativo | `Views/Gestion_De_Citas/st-aux-01-panel-operativo` | Panel operativo MVC/JS | Congelada |
| st-aux-02-agenda-apoyo | `Views/Gestion_De_Citas/st-aux-02-agenda-apoyo` | Agenda auxiliar | Congelada |
| st-aux-05-historial-parcial | `Views/Gestion_De_Citas/st-aux-05-historial-parcial` | Historial parcial | Congelada |
| st-aux-06-asistencia-procedi | `Views/Gestion_De_Citas/st-aux-06-asistencia-procedi` | Asistencia | Congelada |
| st-aux-09-estado-consultorio | `Views/Gestion_De_Citas/st-aux-09-estado-consultorio` | Estado de consultorios | Congelada |
| st-aux-10-citas-finalizadas | `Views/Gestion_De_Citas/st-aux-10-citas-finalizadas` | Citas finalizadas | Congelada |
| st-odo-02-agenda | `Views/Gestion_De_Citas/st-odo-02-agenda` | Agenda profesional y API | Congelada |
| st-pac-01-mis-citas | `Views/Gestion_De_Citas/st-pac-01-mis-citas` | Consulta/cancelación paciente | Congelada |
| st-pac-03-notificaciones | `Views/Gestion_De_Citas/st-pac-03-notificaciones` | Notificaciones | Congelada |
| st-rec-01-dashboard | `Views/Gestion_De_Citas/st-rec-01-dashboard` | Dashboard recepción | Congelada |
| st-rec-03-gestion-citas | `Views/Gestion_De_Citas/st-rec-03-gestion-citas` | `GET /api/citas`, JS operativo | Editable |
| st-rec-05-recordatorios | `Views/Gestion_De_Citas/st-rec-05-recordatorios` | Recordatorios | Congelada |
| st-adm-07-gestion-profesionales | `Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales` | CRUD por `/api/profesionales` | Editable |
| st-adm-14-reportes-clinicos | `Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos` | Reportes/JS | Congelada |
| st-odo-01-dashboard | `Views/Gestion_De_Profesionales/st-odo-01-dashboard` | Dashboard profesional | Congelada |
| st-odo-09-perfil-profesional | `Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional` | Perfil/JS | Congelada |

Las otras 17 vistas no son foco principal, pero pueden modificarse cuando sea necesario dentro de Citas/Profesionales para corregir o completar funcionalidad relacionada. No se hará modernización arbitraria.

## 4. Rutas y consumidores relevantes

### Citas API

Se identificó `Api/Controllers/CitasApiController.cs` con rutas de consulta y escritura. El contrato que debe verificarse y preservarse es:

```text
GET    /api/citas
GET    /api/citas/{id}
POST   /api/citas o ruta de compatibilidad existente
PUT    /api/citas/{id}
PUT    /api/citas/{id}/estado
PUT    /api/citas/{id}/notas
DELETE /api/citas/{id}
```

También existe un consumidor de creación que usa `POST /api/appointments`; no debe eliminarse sin adaptar primero el consumidor.

Consumidores conocidos: agenda administrativa, agenda profesional, mis citas del paciente, `st-rec-03-gestion-citas` y otros scripts de citas. Cada método HTTP y payload debe confirmarse directamente contra el controller y el JavaScript antes de cambiar contratos.

### Citas MVC

`GestionCitasController` expone, entre otras, `POST /gestion-de-citas/guardar-cita` y `POST /gestion-de-citas/eliminar-cita`. La búsqueda estática actual solo encontró la definición de `CrearCitaDesdeAgendaInterna` en `GestionCitasController.cs`; no encontró consumidores en Views, JS, Controllers ni API. Esto es evidencia de posible código muerto, pero antes de eliminarlo debe confirmarse que no exista integración externa conocida y registrarse el impacto.

### Profesionales API

`Controllers/Api/ProfesionalesApiController.cs` expone `/api/profesionales`, incluyendo listado, detalle, alta, edición, cambio de estado, desactivación, especialidades y operaciones de disponibilidad. `st-adm-07-gestion-profesionales` consume estos endpoints desde `wwwroot/js/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/app.js`.

### SSR y formularios

El proyecto combina endpoints MVC que retornan vistas con scripts que consumen APIs. Se encontró `form.submit()` en la superficie administrativa de Citas; debe preservarse mientras siga siendo consumidor real y solo cambiarse si la fase de consolidación lo requiere directamente.

## 5. Autenticación, autorización y antiforgery

`Program.cs` configura Cookie como esquema MVC y JWT Bearer para APIs, junto con `ApiOrCookie`, expiración de cookies, validación de sesión contra base de datos, revocación mediante logout y rate limiting de acceso.

Los claims relevantes creados por Acceso y Seguridad incluyen `NameIdentifier`, `Name`, `Email`, `Role`, `IdPaciente` e `IdProfesional`.

Se observan `[Authorize]` por roles en controllers MVC/API y validación antiforgery para operaciones mutables. Citas tiene lógica especial para distinguir Cookie/JWT y `X-CSRF-TOKEN`; Profesionales usa `AutoValidateAntiforgeryToken`. Esta diferencia es un riesgo contractual que debe comprobarse con clientes reales antes de unificarla.

Riesgos a verificar en Fases 1, 5 y 6:

- ownership aplicado de forma desigual entre controller y service;
- actualización de Cita con posible over-posting de recursos y fecha;
- permisos distintos para paciente, profesional, recepción y administración;
- acceso cruzado mediante IDs válidos de otro usuario;
- comportamiento antiforgery de clientes JWT;
- respuestas de error que expongan detalles internos.

## 6. Hallazgos priorizados

### Alta prioridad

1. Existen rutas MVC y API para Citas; debe quedar una fuente única de validación y persistencia.
2. La actualización de Citas requiere matriz de permisos por rol y DTOs específicos para impedir over-posting.
3. Debe comprobarse si horario individual, ausencias, bloqueos y Profesional-Servicio forman parte de la regla actual o son solo estructuras disponibles.
4. Debe eliminarse la divergencia entre duración configurable y cálculos hardcodeados, solo después de rastrear todos sus consumidores.
5. Los contratos reales de `POST /api/citas` y `POST /api/appointments` deben preservarse o migrarse con evidencia de consumidores.

### Prioridad media

1. Confirmar duplicaciones de regex/estado y carga MVC en Profesionales.
2. Revisar consistencia EF/SQL de FK, índices, nullability y transacciones en dependencias directas.
3. Reactivar o adaptar pruebas relevantes; actualmente la cobertura de Citas es insuficiente.
4. Verificar `AsNoTracking`, paginación y consultas N+1 después de estabilizar comportamiento.

### Decisiones verificadas durante la implementación

- `POST /api/citas` se añadió como alias del flujo existente de agenda; se conservaron `/api/appointments` y `/api/citas/agenda` porque tienen consumidores.
- `GET /api/citas/{id}` se añadió con `ApiOrCookie`, respuesta controlada y validación de ownership.
- `CitaService` valida Profesional-Servicio, ausencias, bloqueos y horario individual solo cuando existen registros aplicables; sin configuración de disponibilidad no se inventa una indisponibilidad.
- La validación de disponibilidad considera la duración configurada y el bloque completo de la cita, no solo su hora de inicio.
- La búsqueda de consumidores confirmó que Profesional usa únicamente `/estado` y `/notas`, y Paciente usa únicamente `DELETE`; por ello `PUT /api/citas/{id}` quedó restringido a Administrador/Recepcionista para impedir que un DTO amplio permita modificar recursos desde otros roles.
- Se añadieron pruebas aisladas de `CitaService` para aceptar una cita dentro del horario profesional y rechazar una cita durante un bloqueo; la suite pasó de 2 a 4 pruebas correctas.
- `CargarDatosProfesionales` ahora delega filtros, paginación, estadísticas y carga de profesionales en `IProfesionalService.ObtenerVistaMVCAsync`, conservando el `ViewData` existente; la suite continúa con 4 pruebas correctas.
- Se eliminó únicamente el bloque `RegistroMedicoRegex` sin consumidores de `GestionProfesionalesController`; las validaciones activas permanecen en el servicio/helper.
- `CitaAgendaDto` ya no exige exactamente 60 minutos; solo exige que la hora final sea posterior a la inicial, permitiendo que `CitaService` aplique la duración configurada. La prueba de contrato y la suite completa pasan con 7 pruebas correctas.
- `CargarDatosAgenda` dejó de usar `AddMinutes(60)` y obtiene la duración desde `ICitaService`; el build y los tests pasan después del cambio.
- `st-rec-03-gestion-citas` pasó a consumir el listado API como fuente primaria; `GET /api/citas` admite búsqueda, estado, fecha, profesional y paginación mediante `CitaService`.
- El mapeo JS de `st-rec-03` acepta camelCase/PascalCase, conserva IDs reales y muestra el consultorio recibido del backend; se eliminó el fallback de consultorio fijo `C1`.
- Alta y edición de `st-rec-03` envían a `/api/citas/agenda` con `X-CSRF-TOKEN`; las validaciones de disponibilidad, conflictos, duración y estados permanecen en `CitaService`.
- La vista mantiene sus formularios Razor y catálogos SSR, pero las operaciones de Citas se resuelven mediante API y recarga posterior para reflejar persistencia confirmada.
- `st-adm-07-gestion-profesionales` conserva CRUD, categoría, teléfono opcional, PATCH condicional de estado, desactivación por API y especialidades; su listado API mantiene filtros y paginación reales.
- La API de Profesionales reutiliza el validador antiforgery compatible con Cookie/JWT de Citas, evitando que clientes JWT legítimos sean bloqueados por `AutoValidateAntiforgeryToken`.
- `loadProfessionals` ahora controla loading, errores y respuestas vacías sin dejar fallos silenciosos; el mapeo de la API permanece compatible con el JSON camelCase actual.
- La suite final quedó en 7 pruebas correctas y el editor no reporta errores en las superficies modificadas.

## 7.1 Clasificación de consumidores de `HoraFin`

La búsqueda actual encontró 57 coincidencias en 18 archivos. No todas representan duración de una cita: varias son propiedades de bloques laborales, DTOs, modelos de presentación o pruebas.

| Categoría | Consumidores | Conclusión |
|---|---|---|
| Lógica de negocio | `CitaService` usa `HorarioProfesional.HoraFin` para validar bloques; `GestionCitasController.CargarDatosAgenda` calculaba `AddMinutes(60)` | El cálculo de agenda era inconsistente y ya fue corregido. La hora del bloque laboral no representa duración de cita. |
| DTO/API | `CitasApiController` calcula `HoraFin` con `ObtenerDuracionCitaMinutosAsync`; `CitaAgendaDto` transporta fin solicitado; `CitaViewModel` recibe fin de formulario | La API ya usa la configuración. La validación fija de 60 en `CitaAgendaDto` fue eliminada. |
| Views/SSR | Agenda administrativa muestra `AgendaCitaViewModel.HoraFin`; `st-adm-09-citas` usa `Cita.HoraFin` para precargar edición | Agenda ya es coherente. La precarga basada en la entidad requiere entregar configuración al SSR. |
| JavaScript | Agendamiento administrativo envía `HoraFin`; `st-adm-09-citas` calcula fin; agenda profesional consume/calcula horas; `appointment-utils.js` solo valida que fin sea posterior | No deben decidir la duración oficial. Deben consumir duración del backend cuando calculen fin. |
| Legacy/tests | Comentarios, prueba deshabilitada de `GestionCitasTests` y pruebas nuevas | No son consumidores productivos. |
| No relacionados | `HorarioProfesional.HoraFin`, configuración EF y endpoint de horarios | Representan fin de bloque profesional, no duración de cita. |

**Conclusión:** se corrigieron los dos consumidores con inconsistencia funcional confirmada: `CitaAgendaDto` y la agenda administrativa. `Models/Entities/Cita.HoraFin` permanece sin cambios porque la entidad no puede consultar asíncronamente la configuración; sus consumidores SSR/JS se adaptarán mediante un valor suministrado por backend cuando corresponda.

### Riesgos de datos/configuración

- `appsettings.Local.json` contiene configuración sensible que debe revisarse para evitar publicación o uso productivo.
- La base de prueba/seed y la disponibilidad real de SQL Server aún deben fijarse para validación integral.
- No se debe modificar la base sin justificarlo contra una relación o flujo objetivo.

## 7. Decisiones pendientes que bloquean reglas

1. Si horario individual, ausencias, bloqueos y Profesional-Servicio deben bloquear creación/actualización o solo servir para consulta.
2. Qué estados ocupan agenda y cuáles deben excluirse de conflictos.
3. Qué roles pueden modificar fecha, paciente, profesional, servicio, consultorio, estado y notas.
4. Si el paciente puede únicamente cancelar o también reprogramar. No se debe inferir del endpoint actual.
5. Si `CrearCitaDesdeAgendaInterna` está muerto y puede eliminarse tras la búsqueda de consumidores.
6. Qué datos seed o entorno SQL Server serán la fuente de verdad de las pruebas.

## 8. Secuencia de implementación

1. Fase 1: consolidar contratos y controller/API sin romper consumidores.
2. Fase 2: trasladar únicamente reglas trazables a `CitaService`.
3. Fase 3: integrar las rutas MVC legacy y decidir sobre código no consumido.
4. Fase 4: conectar dependencias existentes sin modificar sus módulos.
5. Fase 5: consolidar Profesionales y su integración con Citas.
6. Fase 6: seguridad, integridad y las dos vistas editables.
7. Fase 7: validación manual, pruebas disponibles, optimización y cierre.

## 9. Evidencia de inspección

- Inventario estático actual: 103 archivos C#, 67 vistas Razor y 62 scripts JavaScript.
- Búsqueda estática de rutas, autenticación, antiforgery, `fetch` y `form.submit()`: 469 coincidencias en 51 archivos; el resultado requiere revisión por archivo para clasificar falsos positivos y consumidores.
- Archivos de entrada confirmados: `Program.cs`, `Data/AppDbContext.cs`, `Controllers/GestionCitasController.cs`, `Api/Controllers/CitasApiController.cs`, `Controllers/ProfesionalesController.cs`, `Controllers/Api/ProfesionalesApiController.cs`, `Services/ICitaService.cs`, `Services/CitaService.cs`, `Services/IProfesionalService.cs` y `Services/ProfesionalService.cs`.

Este documento no autoriza por sí solo cambios de reglas pendientes. Cada implementación debe actualizar la matriz de hallazgos y añadir evidencia de prueba.
