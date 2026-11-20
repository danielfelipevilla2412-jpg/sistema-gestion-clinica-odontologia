# Plan ejecutivo actualizado — SmileTrack

**Actualizado:** 2026-11-20<br>
**Alcance:** Gestión de Citas y Gestión de Profesionales<br>
**Estado:** mejoras funcionales prioritarias aplicadas; auditoría general y refactor estructural continúan.

## Conclusión

La revisión del análisis de enero de 2025 contra el código actual confirmó que varias recomendaciones ya están resueltas y otras eran especulativas. Se han reforzado las reglas de agenda, especialidades, ausencias, cancelación, lista de espera y trazabilidad de estados. No se recomienda implementar patrones avanzados por defecto ni usar las estimaciones/ROI del documento anterior: no tienen evidencia.

## Estado por prioridad

| Prioridad | Trabajo | Estado actual |
|---|---|---|
| P0 | Integridad del historial de estados de citas | Aplicada en los flujos principales: historial y cambio se guardan juntos; prueba SQL confirma rollback ante fallo |
| P0 | Disponibilidad/solapamientos de agenda | Aplicada: horario individual requerido y bloques del mismo día no pueden solaparse |
| P0 | Cancelación | Aplicada: motivo requerido con límites, historial/notificación y correo best-effort |
| P0 | Especialidades y servicios | Aplicada: múltiples especialidades, valor predeterminado y servicios derivados de especialidades |
| P1 | Ausencias | Aplicada: se consultan y advierten citas afectadas; la reasignación automática existe como ruta separada |
| P1 | Lista de espera | Asignación automática inmediata confirmada por producto |
| P1 | Actor de transición | API/MVC propagan usuario e IP; finalización desde historia clínica resuelve el ID de usuario profesional |
| P1 | Auditoría general y snapshots | Centralizada en flujos principales de citas con snapshots y actor/IP; integración SQL valida el cambio de estado. Otros flujos/eventos aún requieren validar transacciones |
| P2 | Refactor de hotspots | En curso: dashboards de Recepción/panel auxiliar, consultas de Facturación/Citas, disponibilidad, horarios y ausencias de Profesionales, comisiones y recordatorios se separaron o consolidaron en servicios específicos. GestionCitasController, CitaService y ProfesionalService aún requieren extracciones graduales |
| P2 | Validación de integración con SQL Server | Parcial: prueba real de `CambiarEstadoAsync` valida rollback, historial, auditoría y notificación; otros flujos y entornos siguen pendientes |

## Capacidades existentes que no deben reimplementarse

- Políticas de transición de estado centralizadas en `EstadoCitaHelper`.
- Validación de conflictos de profesional/paciente/consultorio y reglas de disponibilidad.
- Reasignación por ausencia, métricas de profesionales y caché de especialidades.
- Manejo global de excepciones/request ID y protecciones antiforgery en rutas revisadas.
- Servicios, DTOs y EF Core como unidad de trabajo; no se necesita un repositorio/UoW paralelo sin un caso de uso concreto.

## Políticas funcionales confirmadas

- **Auxiliar:** puede cambiar una cita solo a En sala de espera o En proceso; la política común sigue validando que la transición sea válida desde el estado actual.
- **Inicio de atención:** se realiza mediante una transición explícita del flujo de atención; no hay una tarea automática que intente iniciar citas al alcanzar su hora.
- **Lista de espera:** al liberarse la franja, el primer candidato compatible se asigna automáticamente.
- **Cancelación por Profesional:** se permite únicamente sobre citas propias, con motivo y validación de fecha/estado.
- Los catálogos RF/CU de origen deben actualizarse para reflejar estas decisiones, especialmente RF-24 y CU-CIT-05.

## Plan de ejecución

### Fase 1 — cerrar trazabilidad general

- [x] Unificar en el servicio la auditoría de creación, actualización/reagenda, cambio de estado, cancelación, solicitud, confirmación y rechazo.
- [x] Propagar actor e IP desde MVC/API y retirar las auditorías duplicadas de esas rutas.
- [x] Registrar snapshots anterior/nuevo para crear y actualizar/reagendar, confirmar/rechazar, cambio de estado y cancelar; en cambios de estado, conservar atomicidad con el historial.
- [x] Añadir pruebas de snapshots y actor/IP para los flujos principales.
- [x] Hacer que el cierre automático de citas vencidas use `ICitaService.CambiarEstadoAsync`, para registrar historial y auditoría en vez de actualizar columnas directamente.
- [x] Verificar y probar la auditoría de recordatorios enviados; los fallos quedan en el registro del recordatorio y en el log.
- [x] Probar contra SQL Server el cambio de estado: ante un fallo de FK se revierte el estado, historial y auditoría; un cambio válido persiste también la notificación.
- [ ] Validar atomicidad/recuperación de los otros flujos de aplicación y sus restricciones contra SQL Server.

**Criterio de cierre restante:** los eventos cubiertos dejan un único registro general y, cuando aplica, un historial de estado. Está validado el cambio de estado en SQL Server; faltan los otros flujos y eventos de fondo.

### Fase 2 — refactor seguro

- [x] Extraer el modelo de dashboard de Recepción y sus KPI de `GestionCitasController` a `CitasDashboardService`; mantener contratos del endpoint y la vista.
- [x] Separar lecturas y proyecciones de Facturación en `FacturacionQueryService`, preservando `IFacturacionService` y sus respuestas.
- [x] Separar KPI, listado paginado y detalle de Citas en `CitaQueryService`, conservando `ICitaService` y su alcance por rol.
- [x] Mover la consulta filtrada/paginada de Gestión de Citas fuera del controlador; mantener allí la preparación de `ViewData` y catálogos de la vista.
- [x] Extraer duración, horarios, elegibilidad profesional y disponibilidad de Citas a `CitaDisponibilidadService`, manteniendo las operaciones públicas de `ICitaService`.
- [x] Separar el cálculo de comisiones de `ProfesionalService` en `ProfesionalComisionService`, manteniendo el contrato de `IProfesionalService`.
- [x] Extraer la composición y el conteo de notificaciones del paciente de `GestionCitasController` a `PacienteNotificacionQueryService`, preservando rutas y claves JSON.
- [x] Extraer las métricas mensuales del panel operativo de `GestionCitasController` a `CitasDashboardService`, conservando las claves `ViewData` consumidas por la vista auxiliar.
- [x] Extraer la lectura y actualización de horarios semanales de `ProfesionalService` a `ProfesionalHorarioService`, manteniendo el contrato público y sus reglas de autorización, solapamiento y citas futuras.
- [x] Extraer la gestión de ausencias, validación de rangos y reasignación automática desde `ProfesionalService` a `ProfesionalAusenciaService`, manteniendo los métodos de `IProfesionalService`.
- [x] Unificar el envío manual de recordatorios de `CitaService` con `RecordatorioCitaService`, evitando duplicar filtros, auditoría e idempotencia.
- [x] Asegurar el fallback local de escape HTML en el dashboard de Recepción para que los datos del servidor nunca se interpolen sin codificar.
- [x] Retirar `CitasEnCursoBackgroundService`, una tarea no registrada que intentaba una transición no permitida y cuya única cobertura afirmaba que no debía producir cambios.
- [ ] Extraer otras responsabilidades cohesivas de los controladores de Citas y Profesionales.
- [ ] Reducir las responsabilidades de `CitaService` y `ProfesionalService` por caso de uso, conservando contratos públicos.
- Separar configuración EF/arranque solo cuando facilite mantenimiento real.
- Mantener rutas, permisos y formatos de respuesta existentes; añadir pruebas de caracterización antes de mover lógica.

**Criterio de aceptación:** cambios sin regresiones en rutas, contratos ni reglas; suite completa y pruebas relevantes verdes.

### Fase 3 — verificación operativa

- [x] Ejecutar una prueba de integración SQL Server para transacciones, relaciones y restricciones del cambio de estado.
- [ ] Ampliar la cobertura de integración SQL Server a otros flujos críticos.
- [x] Medir y registrar una línea base de cobertura; no se fija umbral sin análisis adicional.
- Medir rendimiento antes de fijar umbrales.
- Documentar las decisiones funcionales y operativas adoptadas.

## Validación observada

La última ejecución completa reportó **521 pruebas aprobadas y 1 omitida de 522 casos** en `net9.0`, incluida una prueba de integración SQL Server para `CitaService.CambiarEstadoAsync`. Esta verifica rollback del estado, historial y auditoría ante un fallo de FK, y persistencia de estado, historial, auditoría y notificación en el cambio válido. La migración idempotente se aplicó a la base de pruebas desechable y eliminó una restricción antigua incompatible; la prueba limpió sus fixtures. Coverlet midió en `SmileTrack_MVC` **42,98 % de líneas (13.178/30.654)** y **25,52 % de ramas (3.385/13.262)**; es una línea base anterior, no un umbral de calidad. Las demás pruebas cubren idempotencia de recordatorios, ausencias/reasignación, métricas, DI, auditoría y contratos. El build mostró advertencias nullable existentes en pruebas de componentes visuales. La integración aún no cubre todos los flujos ni constituye validación de producción.

## Fuera de alcance automático

CQRS, Event Sourcing, State Machine adicional, Specification, FluentValidation, repositorios explícitos y versionado de API solo se consideran si una necesidad concreta y medible lo justifica. No se aprueba una migración arquitectónica por comparación genérica con “mejores prácticas”.

## Correcciones a estimaciones anteriores

Se retiran las puntuaciones de calidad, 285 horas, costos, ROI del 164%, payback y metas de cobertura publicadas en 2025: no hay datos reproducibles que las sustenten.
