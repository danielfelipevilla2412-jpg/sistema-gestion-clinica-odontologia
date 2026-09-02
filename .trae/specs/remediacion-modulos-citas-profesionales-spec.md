# Especificación — Remediación Integral Hallazgos Módulos Gestión de Citas y Gestión de Profesionales

## 1. Problema y Contexto

Tras una auditoría exhaustiva de ambos módulos se confirmó que varias correcciones de capa de servicio/datos ya existen (CitaService validación triple recurso, Duración configurable desde ConfigGeneral, entidades y DbSets para Horario/Ausencia/Bloqueo/ProfesionalServicio, endpoints API para disponibilidad, Helpers centralizados, índices SQL en script idempotente, JS st-adm-07 consumiendo API). Sin embargo, **los controladores MVC no han sido integrados a esas correcciones**, generando dos vías de entrada con comportamiento inconsistente:

- Flujo API (bueno) → usa Services → validación robusta (3 recursos, duración configurable, horario clínica, execution strategy)
- Flujo MVC (deficiente) → reimplementa lógica contra _context → validación débil (solo profesional, duración hardcodeada 60, sin horario clínica)

## 2. Usuarios y Objetivos

| Rol | Objetivo |
|---|---|
| Administrador / Recepcionista | Al usar la interfaz clásica MVC (guardar-cita / eliminar-cita / st-adm-07) debe obtener la MISMA validación que la API: sin conflictos paciente/consultorio, duración leída de ConfigGeneral, horario de la clínica respetado. |
| Mantenedores del sistema | DRY: la lógica de negocio vive en los Services. Los controladores MVC son "thin controllers" que delegan. Cambiar la política de citas = tocar un solo lugar. |
| Auditor / QA | Trazabilidad de cada hallazgo del informe: estado Resuelto / Pendiente con justificación, con evidencias de compilación y pruebas unitarias pasando. |

## 3. Alcance (In-Scope)

SOLO los dos módulos, solo los archivos que actualmente tienen duplicación o brechas:

1. `Controllers/GestionCitasController.cs` — refactor GuardarCita + EliminarCita + eliminar constante DuracionCitaMinutos
2. `Controllers/ProfesionalesController.cs` (alias GestionProfesionalesController) — desacoplar CargarDatosProfesionales hacia IProfesionalService y reemplazar regex/NormalizarEstado por Helpers centralizados
3. `Services/ProfesionalService.cs` — unificar PasswordRegex hacia ProfesionalEstadoHelper
4. `Data/AppDbContext.cs` — declarar HasIndex NONCLUSTERED en entidad Cita (Fluent API sincronizado con script SQL)
5. `Services/IProfesionalService.cs` + `ProfesionalService.cs` — agregar método ObtenerVistaMVCAsync con filtros paginados + estadísticas (Stats: total/activos/vacaciones/inactivos)

## 4. No-Goals (Fuera de Alcance)

- ❌ No tocar rutas HTTP ni contratos de la API (rutas existentes preservadas)
- ❌ No reescribir vistas Razor ni CSS; solo cambios mínimos en emojis si así se decide
- ❌ No tocar módulos de Pacientes, Facturación, Acceso y Seguridad
- ❌ No cambiar entidades Profesional/Cita existentes ni sus relaciones
- ❌ No reescribir ProfesionalService.CreateExecutionStrategy, transacciones ni auditoría (excelente hoy)

## 5. Requisitos Funcionales

### 5.1 Gestión de Citas

| ID | Descripción |
|---|---|
| FR-CIT-01 | `GuardarCita` (crear) debe invocar `_citaService.CrearAsync(CitaApiRequest)`. Ya NO hará AnyAsync propios para existencia ni conflicto. |
| FR-CIT-02 | `GuardarCita` (actualizar) debe invocar `_citaService.ActualizarAsync(int, CitaApiUpdateDto)`. Ya NO hará validaciones duplicadas. |
| FR-CIT-03 | Los errores de InvalidOperationException lanzados por el servicio se mapean a `TempData["ErrorValidacion"]` con el mismo mensaje del servicio (conflicto Profesional / Paciente / Consultorio / horario clínica). |
| FR-CIT-04 | `EliminarCita` debe invocar `_citaService.CancelarAsync(int)`. Mantener comportamiento redirect + TempData + auditoría + email existente. |
| FR-CIT-05 | Eliminar `private const int DuracionCitaMinutos = 60` y toda referencia a ella. El servicio es la única fuente de verdad. |
| FR-CIT-06 | AppDbContext debe declarar 4 índices NONCLUSTERED en la entidad Cita con nombres alineados al script SQL (`IX_Cita_Profesional_Fecha`, `IX_Cita_Paciente_Fecha`, `IX_Cita_Consultorio_Fecha`, `IX_Cita_Estado_Fecha`) para que el modelo EF coincida con la base. |

### 5.2 Gestión de Profesionales

| ID | Descripción |
|---|---|
| FR-PROF-01 | `IProfesionalService` expone `ObtenerVistaMVCAsync(PaginationQuery q, CancellationToken ct)` retornando `(List<Profesional> Items, PagedResult Paginacion, ProfesionalesStats Stats)`. |
| FR-PROF-02 | `CargarDatosProfesionales` reemplaza su query _context directa por la llamada a `_profesionalService.ObtenerVistaMVCAsync`. Stats ViewBag (StatTotal / StatActivos / StatVacaciones / StatInactivos) se llenan desde `Stats`. |
| FR-PROF-03 | `GestionProfesionalesController` elimina su `PasswordAccesoRegex`, `RegistroMedicoRegex()` y `NormalizarEstado()`. Pasa a usar `ProfesionalEstadoHelper.PasswordRegex()`, `ProfesionalEstadoHelper.NormalizarEstado()`. El controlador no debe contener regex de password propia. |
| FR-PROF-04 | `ProfesionalService` elimina cualquier `PasswordRegex` duplicado y usa `ProfesionalEstadoHelper.EsPasswordValida()` / `.PasswordRegex()` en su lugar. |
| FR-PROF-05 | `CargarDatosReportesClinicos` NO se migra (deja una tarea futura). Se documenta como pendiente con justificación: requiere crear un DTO propio y mapeos de reporte que exceden esta fase. |

## 6. Requisitos No Funcionales

| ID | Naturaleza | Descripción |
|---|---|---|
| NFR-01 | Compatibilidad | Rutas HTTP MVC preservadas byte-a-byte: `gestion-de-citas/guardar-cita`, `gestion-de-citas/eliminar-cita`, `gestion-de-profesionales/st-adm-07-gestion-profesionales`. |
| NFR-02 | Rendimiento | Los HasIndex NONCLUSTERED declarados en Fluent API NO generan nueva migración si el script SQL idempotente ya los creó. Deben coincidir nombres exactos. |
| NFR-03 | DRY | Después de la corrección, GestionCitasController NO contiene lógica de validación de existencia (AnyAsync) ni conflicto. Todo viene de _citaService. |
| NFR-04 | Security | Todos los POST MVC siguen usando `[ValidateAntiForgeryToken]`. Sin abrir agujeros CSRF. |
| NFR-05 | Logging | Mantener los ILogger inyectados y mensajes existentes. Solo agregar LogWarning/LogError donde el servicio ahora lance una excepción que antes era controlada inline. |
| NFR-06 | Compilación | `dotnet build SmileTrack_MVC.slnx` debe terminar con 0 errores y 0 warnings nuevos. |

## 7. Restricciones y Dependencias

- **Hard Constraint U-07** del Project Memory: `HayConflictoHorarioAsync` (ahora en su variante completa) debe concurrentemente validar Profesional + Paciente + Consultorio. NO se acepta una variante que solo valide uno.
- **Hard Constraint M-17**: DuracionCitaMinutos debe recuperarse de `ConfiguracionGeneral` clave `cita_duracion_minutos`. Fallback 60 cuando la clave no existe o no parsea. El controlador NO puede tenerla como `const`.
- **Hard Constraint M-15**: `GuardarCita` MVC debe fallar con TempData["ErrorValidacion"] cuando la fecha/hora caiga fuera del rango `horario_apertura` → `horario_cierre` o en un día no listado en `dias_atencion`.
- **Hard Constraint Service Delegation**: Controllers no reimplementan validación o persistencia. Solo arman ViewModels/DTOs, invocan Service, mapean TempData y retornan Redirect/View.

## 8. Supuestos y Preguntas Abiertas (Resueltas)

| Pregunta | Resolución adoptada |
|---|---|
| ¿Qué pasa si GuardarCita crea la cita pero falla el envío de email? | Comportamiento actual: la cita queda guardada y el try/catch outer loguea el error. El servicio no envía email (responsabilidad del controller). Se preserva exactamente la misma estructura try/catch estratificada del controller, pero el bloque de persistencia ahora es una sola llamada al servicio. |
| ¿Retorna Redirect aunque el servicio falle? | Sí, como hoy. El mensaje se muestra via TempData["ErrorValidacion"] igual que en el flujo actual inline. Sin cambiar UX. |
| ¿Qué pasa con ProfesionalesController → CargarDatosReportesClinicos? | Fuera de esta iteración (ver FR-PROF-05). Reportar como PENDIENTE con justificación en el informe final. |
| ¿PasswordRegex en ProfesionalService está definido dónde? | Si el partial class no lo define en el archivo leído, se reemplaza el uso inline por ProfesionalEstadoHelper.EsPasswordValida() para evitar el warning/error de compilación. |

## 9. Criterios de Aceptación (AC)

### 9.1 Criterios de tipo `rule` (binarios, observables)

- **AC-R-01**: En GestionCitasController.GuardarCita, `grep` por `_context.Pacientes.AnyAsync` / `_context.Profesionales.AnyAsync` / `_context.Servicios.AnyAsync` / `_context.Consultorios.AnyAsync` / `_context.EstadosCita.AnyAsync` dentro del cuerpo de GuardarCita RETORNA 0 coincidencias. (Medida: Grep tool)
- **AC-R-02**: En GestionCitasController.GuardarCita, `grep` por `_context.Citas.AnyAsync` (bloques conflicto) dentro de GuardarCita RETORNA 0. (Medida: Grep)
- **AC-R-03**: GestionCitasController ya NO declara `private const int DuracionCitaMinutos`. (Medida: Grep del archivo)
- **AC-R-04**: `EliminarCita` MVC invoca `_citaService.CancelarAsync(IdCita, ct)` y usa su retorno para el flujo éxito/error. (Medida: Read del método)
- **AC-R-05**: AppDbContext OnModelCreating → entidad Cita declara 4 `entity.HasIndex(...).HasDatabaseName(...)` con nombres `IX_Cita_Profesional_Fecha`, `IX_Cita_Paciente_Fecha`, `IX_Cita_Consultorio_Fecha`, `IX_Cita_Estado_Fecha`. (Medida: Grep + Read)
- **AC-R-06**: GestionProfesionalesController no tiene `PasswordAccesoRegex` ni `RegistroMedicoRegex` propios. Usa ProfesionalEstadoHelper. (Medida: Grep)
- **AC-R-07**: ProfesionalService no invoca un `PasswordRegex` propio; usa ProfesionalEstadoHelper.EsPasswordValida o su PasswordRegex. (Medida: Grep + Read)
- **AC-R-08**: IProfesionalService declara el método Task `ObtenerVistaMVCAsync(PaginationQuery, CancellationToken)` con tupla Items+Paginacion+Stats, y ProfesionalService lo implementa. (Medida: Read interfaces y class)
- **AC-R-09**: `dotnet build SmileTrack_MVC.slnx --no-incremental` → exit code 0. (Medida: RunCommand)
- **AC-R-10**: Si existen tests (SmileTrack_MVC.Tests), `dotnet test` → exit code 0. (Medida: RunCommand)

### 9.2 Criterios de tipo `rubric` (evaluativos, escala 0-2)

- **AC-RB-01 — Fidelidad Thin Controller (0-2, aprobado ≥1)**:
  - 0: GuardarCita / CargarDatosProfesionales siguen teniendo queries _context inline mezclados con lógica de negocio más allá de armar DTO.
  - 1: La persistencia y validación vienen 100% del Service; queda algo de validación de ModelState o mapeo ViewModel→DTO inline razonable.
  - 2: Todo controller es thin: ModelState.IsValid → mapea ViewModel a request DTO → 1 llamada Service → TempData → Redirect/View. Zero lógica de negocio en controller.
- **AC-RB-02 — Completitud Remediación Hallazgos (0-2, aprobado ≥1)**:
  - 0: Solo 1-2 de los 5 items de ALTA resueltos.
  - 1: Todos los ALTA (C-01/C-02 integracion GuardarCita servicio + C-03 Fluent API Indices + C-04 Duracion) resueltos; algunos MEDIA/BAJA quedan para después.
  - 2: Todos los ALTA + MEDIA (C-06 CancelarCancelarAsync, P-03 ObtenerVistaMVCAsync, P-04/P-05 Helpers) y P-04/P-05 ProfesionalService Password centralizado. Rubros documentados como Pendiente tienen justificación explícita.
- **AC-RB-03 — Calidad de Evidencias (0-2, aprobado ≥1)**:
  - 0: Sin registros de pruebas/compilación ni capturas.
  - 1: Existen extractos de build exitoso y (si aplica) test exitoso. Cada tarea completada tiene Completion Evidence con links a líneas/archivos.
  - 2: Cada AC rule tiene evidencia en tasks.md + referencias clickeables file:/// a las líneas exactas del código cambiado, y el informe final enumera cada hallazgo del informe técnico con estado y explicación.
