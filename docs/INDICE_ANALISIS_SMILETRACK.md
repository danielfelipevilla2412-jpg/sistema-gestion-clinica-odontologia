# Índice de análisis técnico — SmileTrack

**Actualizado:** 2026-11-20<br>
**Alcance principal:** Gestión de Citas y Gestión de Profesionales

## Estado actual

El análisis de 2025 se contrastó con la implementación actual. Las mejoras prioritarias en agenda, especialidades, ausencias, cancelación, lista de espera e historial de estados ya tienen implementación. Se extrajeron consultas, disponibilidad, dashboards, horarios y ausencias a servicios específicos y se unificó el envío manual de recordatorios. La auditoría de los flujos principales de citas, incluida la asignación automática desde lista de espera, se centralizó; siguen pendientes cambios graduales en acciones/comandos y ampliar la validación transaccional SQL Server más allá del cambio de estado.

La suite completa pasó **522/522 pruebas** en `net9.0`, incluida una prueba SQL Server que valida rollback y persistencia de estado, historial, auditoría y notificación de citas. La línea base Coverlet de `SmileTrack_MVC` es 42,98 % de líneas y 25,52 % de ramas; no se ha fijado un umbral ni validado el despliegue de producción. La integración SQL se limita a este flujo y no acredita los demás casos ni producción.

## Documentos vigentes

| Documento | Para qué sirve |
|---|---|
| [UNA_PAGINA_RESUMEN.md](UNA_PAGINA_RESUMEN.md) | Estado actual, logros, deudas y orden recomendado |
| [RESUMEN_EJECUTIVO_PLAN_MEJORAS.md](RESUMEN_EJECUTIVO_PLAN_MEJORAS.md) | Prioridades, criterios de aceptación y fases de trabajo |
| [DEUDAS_TECNICAS_PROBLEMAS_CODIGO.md](DEUDAS_TECNICAS_PROBLEMAS_CODIGO.md) | Registro detallado de deudas, con nota de vigencia y estados actualizados |
| [ANALISIS_PATRONES_ARQUITECTONICOS.md](ANALISIS_PATRONES_ARQUITECTONICOS.md) | Patrones presentes y decisiones que no deben tratarse como requisitos |
| [ANALISIS_COMPLETO_SMILETRACK_2025.md](ANALISIS_COMPLETO_SMILETRACK_2025.md) | Inventario histórico con adenda de vigencia |
| [Documentacion/INFORME_AUDITORIA_CITAS_PROFESIONALES_2026-09-22.md](Documentacion/INFORME_AUDITORIA_CITAS_PROFESIONALES_2026-09-22.md) | Auditoría funcional basada en requisitos; incluye estado de correcciones posteriores |

## Lectura recomendada

### Dirección / producto

1. [UNA_PAGINA_RESUMEN.md](UNA_PAGINA_RESUMEN.md)
2. Políticas funcionales confirmadas y su seguimiento en [RESUMEN_EJECUTIVO_PLAN_MEJORAS.md](RESUMEN_EJECUTIVO_PLAN_MEJORAS.md)

### Arquitectura / desarrollo

1. [DEUDAS_TECNICAS_PROBLEMAS_CODIGO.md](DEUDAS_TECNICAS_PROBLEMAS_CODIGO.md)
2. [ANALISIS_PATRONES_ARQUITECTONICOS.md](ANALISIS_PATRONES_ARQUITECTONICOS.md)
3. [ANALISIS_COMPLETO_SMILETRACK_2025.md](ANALISIS_COMPLETO_SMILETRACK_2025.md)

### QA

1. Criterios de aceptación del [plan ejecutivo](RESUMEN_EJECUTIVO_PLAN_MEJORAS.md)
2. Hallazgos y requisitos de la [auditoría funcional 2026](Documentacion/INFORME_AUDITORIA_CITAS_PROFESIONALES_2026-09-22.md)
3. Resultado más reciente de `dotnet test SmileTrack_MVC.Tests\SmileTrack_MVC.Tests.csproj --no-restore`

## Hallazgos de 2025 retirados o corregidos

- Los riesgos SQLi basados únicamente en consultas EF/LINQ no están demostrados.
- El uso de BCrypt no requiere gestionar un salt explícito para cada contraseña.
- No existe una medición que respalde porcentajes de cobertura, puntuaciones globales o metas de calidad publicadas.
- Se retiran estimaciones de 285 horas, costos, ROI, payback y cronogramas fijos por falta de datos.
- La falta de CQRS, Event Sourcing, una State Machine adicional, Specification, Repository/UoW explícitos o API versioning no es por sí sola una deuda.

## Deudas activas

1. Auditoría de eventos de fondo no uniforme y falta ampliar la validación de snapshots/transacciones de citas con SQL Server.
2. Controladores y servicios extensos: refactor por responsabilidades, con pruebas de caracterización.
3. Las políticas antes ambiguas (Auxiliar, asignación de lista de espera y cancelación por Profesional) fueron confirmadas el 20-11-2026 y deben mantenerse cubiertas por pruebas.
4. Más pruebas contra SQL Server y medición actualizada de cobertura pendientes.

Los conteos de líneas se conservan como señales para priorizar inspección, no como criterio de aceptación. Los ejemplos y planes históricos dentro de los documentos originales deben leerse a la luz de las adendas vigentes.
