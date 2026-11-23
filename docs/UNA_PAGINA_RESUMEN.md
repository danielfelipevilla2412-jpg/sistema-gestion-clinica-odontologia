# SmileTrack — estado técnico actual

**Actualizado:** 2026-11-20<br>
**Alcance:** Gestión de Citas y Gestión de Profesionales
**Fuente:** revisión del código y suite automatizada; no sustituye una auditoría sobre la base de datos desplegada.

## Resumen ejecutivo

La aplicación mantiene una arquitectura MVC por capas con servicios, EF Core y controladores API. Las reglas de agenda y varias mejoras funcionales del análisis anterior ya están implementadas. La deuda principal que permanece es de mantenibilidad y verificación operativa: hay controladores y servicios extensos, la validación transaccional SQL cubre un flujo crítico y faltan otros flujos y eventos de fondo.

| Archivo | Líneas actuales | Lectura |
|---|---:|---|
| `GestionCitasController.cs` | 4.006 | Hotspot; consultas de dashboard/notificaciones extraídas, quedan acciones por responsabilidades |
| `ProfesionalesController.cs` | 1.105 | Revisar responsabilidades, sin asumir que el tamaño por sí solo es un defecto |
| `CitaService.cs` | 1.796 | Queries, disponibilidad y recordatorios delegados; comandos y transiciones requieren revisión gradual |
| `ProfesionalService.cs` | 1.268 | Comisiones, horarios y ausencias delegados; CRUD principal pendiente de revisión |
| `Program.cs` | 1.387 | Arranque/configuración extensa |
| `AppDbContext.cs` | 1.335 | Mapeos EF extensos; configuración duplicada de historial ya consolidada |

Los conteos son una señal para inspección, no una calificación de calidad ni un límite de aceptación.

## Cambios y capacidades verificados

- Cambios del estado de cita e historial se persisten en el mismo guardado en los flujos principales de `CitaService`; se añadió prueba de fallo de persistencia.
- Se propagó el actor desde API/MVC al cambio de estado. Al finalizar una consulta desde historia clínica, se resuelve el ID de usuario asociado al profesional en lugar de guardar el ID profesional como usuario.
- Se validan horarios solapados; intervalos consecutivos siguen permitidos.
- Un profesional sin horario individual no se ofrece para nuevas citas.
- La asignación de especialidades admite varias y aplica Odontología General cuando corresponde; los servicios disponibles se derivan de las especialidades.
- Los flujos de ausencia consultan citas afectadas y alertan; cancelación exige motivo y contempla intento de correo sin revertir la cancelación si falla.
- La liberación de una franja puede intentar asignar una solicitud compatible de lista de espera automáticamente.
- La auditoría de creación, actualización/reagenda, cambios de estado, cancelación, solicitud, confirmación/rechazo y asignación automática desde lista de espera se centralizó en `CitaService`; se propagan actor/IP y snapshots en los flujos principales, sin duplicar la auditoría en controladores.
- El dashboard auxiliar y el envío manual de recordatorios comparten ahora sus servicios de dashboard y recordatorios; el fallback del HTML del dashboard codifica caracteres especiales.
- Hay manejo global de excepciones/request ID; la configuración antiforgery está presente en las rutas revisadas.

## Validación

La suite completa obtuvo **521 pruebas aprobadas y 1 omitida de 522 casos** en `net9.0`, incluida una prueba real de `CitaService.CambiarEstadoAsync` en SQL Server. La prueba valida rollback ante fallo de FK y persistencia de estado, historial, auditoría y notificación en el camino válido. La migración idempotente eliminó una restricción antigua incompatible y la prueba limpió sus fixtures. La línea base Coverlet de `SmileTrack_MVC` es **42,98 % de líneas (13.178/30.654)** y **25,52 % de ramas (3.385/13.262)**; aún no se fija umbral. El build reportó advertencias nullable existentes en `ViewComponentsTests.cs`; no se atribuyen a estos cambios. Los demás flujos SQL y la configuración de producción siguen pendientes de validación.

## Deudas y límites pendientes

1. **Mantenibilidad:** dashboards, lecturas de Facturación/Citas, disponibilidad, comisiones, horarios y ausencias de Profesionales, notificaciones del paciente y recordatorios ya tienen servicios específicos. Falta continuar con acciones de `GestionCitasController`, comandos de `CitaService` y CRUD principal de Profesionales mediante cambios incrementales y pruebas.
2. **Auditoría restante:** extender la prueba transaccional SQL Server a otros flujos y revisar eventos informativos de fondo.
3. **Pruebas de entorno:** una prueba SQL crítica ya valida el flujo de cambio de estado; falta validar los demás flujos y la configuración de producción.

**Políticas confirmadas por producto (2026-11-20):** Auxiliar solo puede cambiar a En sala de espera o En proceso; la lista de espera asigna automáticamente al primer candidato compatible; Profesional puede cancelar solo citas propias.

## Afirmaciones del análisis anterior que no deben usarse como hechos

- No se demostró una vulnerabilidad SQLi por consultas LINQ parametrizadas.
- BCrypt genera y verifica su salt; la falta de un salt configurado manualmente no prueba una vulnerabilidad.
- La línea base de cobertura medida no constituye un umbral ni una calificación de calidad.
- Las cifras de ROI, costos, horas y puntuaciones publicadas en 2025 no tienen respaldo verificable y se retiran.
- State Machine adicional, CQRS, Event Sourcing, repositorio explícito, Unit of Work separado y versionado de API son opciones sujetas a necesidad; no son defectos por el solo hecho de no existir.

## Orden recomendado

1. Mantener cubiertas por pruebas las políticas funcionales confirmadas.
2. Completar la verificación de auditoría restante: más transacciones reales con SQL Server y eventos de fondo.
3. Refactorizar hotspots en cambios pequeños, manteniendo rutas y contratos actuales.
4. Ampliar las pruebas de integración con SQL Server a otros flujos críticos y medir cobertura antes de fijar metas.

**Documentos de referencia:** [deudas técnicas](DEUDAS_TECNICAS_PROBLEMAS_CODIGO.md), [patrones arquitectónicos](ANALISIS_PATRONES_ARQUITECTONICOS.md), [plan de mejoras](RESUMEN_EJECUTIVO_PLAN_MEJORAS.md), [análisis completo](ANALISIS_COMPLETO_SMILETRACK_2025.md) e [índice](INDICE_ANALISIS_SMILETRACK.md).
