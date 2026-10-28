# Informe de auditoría de gestión de citas y profesionales

**Proyecto:** SmileTrack  
**Fecha de revisión:** 22 de septiembre de 2026  
**Fuentes funcionales:** `CU_RN_SmileTrack.docx` y `RF_Especificos_SmileTrack.docx` (versión consolidada 2.0, septiembre de 2026).  
**Alcance del código:** controladores MVC y API, servicios, modelos/DTO, vistas y JavaScript de Gestión de Citas y Gestión de Profesionales.

## 1. Conclusión ejecutiva

La solución cubre buena parte de los flujos funcionales de ambos módulos: agenda, CRUD de profesionales, asignación de citas, disponibilidad, cambios de estado, solicitudes de paciente, notificaciones, recordatorios, ausencias y reportes. No se considera cumplimiento integral. Hay brechas de alta criticidad en reglas de agenda y cancelación, especialidades, autorización/alcance del reporte y trazabilidad; además, ciertas funcionalidades aprobadas solo están cubiertas parcialmente.

| Módulo | Evaluación | Resumen |
|---|---|---|
| Gestión de profesionales | **Parcial, con brechas altas** | Listado, creación, edición, estado, horarios, ausencias, servicios y reportes tienen rutas funcionales. La API solo soporta una especialidad, no aplica el valor por defecto aprobado, permite guardar horarios solapados y la disponibilidad contradice RN-17. El endpoint de reportes también tiene un problema de alcance por rol. |
| Gestión de citas | **Parcial, con brechas altas** | Agenda, validaciones de colisión, solicitud/confirmación, estados, recordatorios, notificaciones y panel auxiliar existen. La cancelación no exige motivo; falta correo al cancelar; la auditoría de citas no persiste todos los datos requeridos; los KPIs no se actualizan por polling y algunos estilos de estado se confunden. |

Este análisis es estático sobre el código y las rutas implementadas; no se ejecutaron pruebas ni se verificó una base de datos desplegada. Las conclusiones describen lo comprobable en el repositorio. Los textos o instrucciones embebidos en los documentos adjuntos se trataron como especificación del sistema, no como instrucciones para el agente.

## 2. Catálogo aprobado considerado

Se revisaron todos los requisitos funcionales de los módulos objetivo y todos sus casos de uso. Las reglas de negocio pertinentes son RN-11–RN-19 (Profesionales) y RN-20–RN-30 (Citas).

### Gestión de Profesionales

**Requisitos funcionales:**

- **RF-10** Registro de profesionales.
- **RF-11** Asignación de especialidades a profesionales.
- **RF-12** Registro y gestión de horarios de atención.
- **RF-13** Visualización de agenda del profesional.
- **RF-14** Cambio de estado y baja lógica de profesional.
- **RF-15** Gestión de ausencias del profesional.
- **RF-16** Consulta de servicios habilitados por profesional.
- **RF-17** Reportes clínicos y de desempeño por profesional.

**Casos de uso:**

- **CU-PRO-01** Listar y filtrar profesionales.
- **CU-PRO-02** Crear un profesional.
- **CU-PRO-03** Editar un profesional.
- **CU-PRO-04** Desactivar o reactivar un profesional.
- **CU-PRO-05** Gestionar horarios de un profesional.
- **CU-PRO-06** Gestionar ausencias de un profesional.
- **CU-PRO-07** Consultar reportes clínicos por profesional.
- **CU-PRO-08** Consultar tablero y perfil propio.

### Gestión de Citas

**Requisitos funcionales:**

- **RF-18** Agendamiento de citas.
- **RF-19** Validación de conflictos de horario.
- **RF-20** Modificación y cancelación de citas.
- **RF-21** Gestión de estados del ciclo de vida de la cita.
- **RF-22** Vista de calendario de citas.
- **RF-23** Solicitud de citas por el paciente y confirmación por recepción.
- **RF-24** Lista de espera y asignación automática de profesional disponible.
- **RF-25** Notificaciones y recordatorios de citas.
- **RF-26** Panel operativo y estado del consultorio (Auxiliar).
- **RF-27** Dashboard y KPIs de gestión de citas.

**Casos de uso:**

- **CU-CIT-01** Consultar agenda y disponibilidad.
- **CU-CIT-02** Registrar una cita.
- **CU-CIT-03** Reagendar una cita (incluye arrastre en calendario).
- **CU-CIT-04** Cambiar el estado de una cita.
- **CU-CIT-05** Cancelar una cita.
- **CU-CIT-06** Solicitar y confirmar una cita (paciente + recepción).
- **CU-CIT-07** Enviar recordatorios de citas.
- **CU-CIT-08** Registrar notas clínicas de una cita.
- **CU-CIT-09** Registrar asistencia y procedimiento (auxiliar).
- **CU-CIT-10** Consultar historial parcial de un paciente (auxiliar).
- **CU-CIT-11** Consultar el estado del consultorio.

## 3. Hallazgos por criticidad

### Alta

#### A1. El motivo de cancelación es opcional

- **Especificación:** RN-23 exige motivo no vacío de máximo 500 caracteres y RF-20/CU-CIT-05 lo incluyen como paso obligatorio.
- **Evidencia:** [CitaCancelacionDto.cs](../Models/DTOs/GestionDeCitas/CitaCancelacionDto.cs) aplica `StringLength(500)` pero no `[Required]`. Además, la ruta DELETE permite omitir cuerpo (`CitasApiController.cs`, acción `Cancelar`, aprox. líneas 627–645) y `CitaService.CancelarAsync` normaliza motivo vacío como `null` (aprox. líneas 804–827).
- **Impacto:** se pueden cancelar citas sin justificación auditable.
- **Corrección:** exigir motivo en el DTO y en el servicio; mantener el límite de 500 caracteres; validar también la acción MVC para que no exista una ruta alternativa que omita la regla.

#### A2. Se permiten citas a profesionales sin horario configurado

- **Especificación:** RN-17 dice que un profesional sin horario no debe aparecer disponible para nuevas citas; RF-12 describe generación de espacios a partir del horario semanal.
- **Evidencia:** [CitaService.cs](../Services/GestionDeCitas/CitaService.cs), `ValidarDisponibilidadProfesionalAsync` (aprox. líneas 1250–1310) devuelve disponibilidad conforme al horario general de la clínica si `horarios.Count == 0`. Por tanto, el endpoint de disponibilidad y la creación aceptan a ese profesional.
- **Impacto:** el sistema agenda prestadores cuya disponibilidad nunca fue configurada.
- **Corrección:** tratar horario individual vacío como no disponible y reflejarlo también en `/api/citas/profesionales-disponibles`.

#### A3. Guardar horarios no comprueba solapamientos

- **Especificación:** RN-16, RF-12 y CU-PRO-05 prohíben bloques solapados del mismo día y permiten bloques consecutivos.
- **Evidencia:** [ProfesionalService.cs](../Services/GestionDeProfesionales/ProfesionalService.cs), `ActualizarHorariosAsync` (aprox. líneas 853–925) valida día, horas y que fin sea posterior al inicio, pero construye y persiste los bloques sin comparar intervalos entre sí.
- **Impacto:** se guarda una matriz semanal ambigua y potencialmente produce disponibilidad duplicada.
- **Corrección:** agrupar por día y rechazar si `inicioA < finB && inicioB < finA`; aceptar igualdad de fin/inicio para bloques consecutivos.

#### A4. No se cumple la asignación de especialidad por defecto y no se soportan múltiples especialidades

- **Especificación:** RF-11 permite una o varias especialidades; RN-15 exige asignar “Odontología General” si no se selecciona ninguna.
- **Evidencia:** [ProfesionalApiDtos.cs](../Models/Api/ProfesionalApiDtos.cs), `ProfesionalApiRequest` expone `int? IdEspecialidad` (línea aproximada 39). En [ProfesionalService.cs](../Services/GestionDeProfesionales/ProfesionalService.cs), `CrearAsync` (aprox. líneas 303–318) y `ActualizarAsync` (aprox. líneas 491–513) insertan como máximo una relación y no crean relación alguna si el campo viene vacío.
- **Impacto:** no se puede representar el caso funcional de varias especialidades; los profesionales sin selección quedan sin especialidad, pese a la regla explícita.
- **Corrección:** aceptar una colección de IDs en API/modelo/formulario, validar duplicados y existencia, y resolver “Odontología General” como especialidad predeterminada al crear y editar con lista vacía.

#### A5. La actualización de estado de cita por API no está disponible para Recepción

- **Especificación:** RF-21/CU-CIT-04 asignan cambio de estado a Recepcionista y Profesional; el ciclo también contempla Auxiliar.
- **Evidencia:** [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs), acción `CambiarEstado` (aprox. líneas 533–550) tiene `[Authorize(Roles = "Profesional")]`. El servicio comparte la operación, pero esta ruta rechaza a Recepcionista/Auxiliar.
- **Impacto:** el contrato API no permite a actores aprobados ejecutar la operación. Hay lógica MVC adicional, pero genera comportamiento dependiente del punto de entrada.
- **Corrección:** autorizar roles según transiciones permitidas y validar el rol/ownership dentro del servicio o política; cubrir Auxiliar solo para transiciones definidas para ese rol.

#### A6. Reportes clínicos: los filtros del profesional pueden dejar visibles datos de otros profesionales

- **Especificación:** RN-18 limita al profesional autenticado a sus reportes propios; RF-17/CU-PRO-07 contemplan seleccionar profesional y periodo para Administrador.
- **Evidencia:** [ProfesionalesController.cs](../Controllers/GestionDeProfesionales/ProfesionalesController.cs), `CargarDatosReportesClinicos` (aprox. líneas 330 en adelante) aplica el filtro propio únicamente cuando logra resolver `IdUsuario` a Profesional. Luego también aplica el parámetro de consulta `profesional`. Si el usuario con rol Profesional no tiene fila profesional asociada, el query queda sin filtro de propietario; el action está permitido a `Administrador,Profesional` (líneas 61–63).
- **Impacto:** escenario de cuenta desasociada permite consultar el reporte general o filtrar por otro profesional.
- **Corrección:** para rol Profesional, resolver el ID y denegar si no existe; ignorar/restringir siempre filtros de profesional al ID propio. Solo Administrador debe seleccionar cualquier profesional.

### Media

#### M1. La ausencia estándar no alerta sobre citas coincidentes

- **Especificación:** RF-15/CU-PRO-06 requieren alertar si el intervalo de ausencia coincide con citas para que se gestionen.
- **Evidencia:** [ProfesionalService.cs](../Services/GestionDeProfesionales/ProfesionalService.cs), `CrearAusenciaAsync`/`ActualizarAusenciaAsync` (aprox. líneas 957–1010) validan profesional y rango, guardan la ausencia, pero no consultan citas afectadas ni devuelven aviso. Existe una ruta distinta de reasignación automática (hallazgo de funcionalidad adicional).
- **Impacto:** se puede registrar una ausencia sin que el usuario vea citas afectadas ni el resultado de su gestión.
- **Corrección:** comprobar cruces antes de guardar y devolver la lista/advertencia; aplicar el flujo acordado para reprogramar, sin modificación silenciosa de citas.

#### M2. Servicios habilitados se consultan desde asignaciones directas, no desde especialidades

- **Especificación:** RF-16 define los servicios disponibles según especialidades asignadas y requiere mostrar servicios generales como alternativa cuando no hay especialidad.
- **Evidencia:** [ProfesionalService.cs](../Services/GestionDeProfesionales/ProfesionalService.cs), `ObtenerServiciosAsync` (aprox. líneas 1056–1090) consulta `ProfesionalServicios` activos. Retorna esa lista directa; no deriva servicios desde `Profesional_Especialidad` ni aplica fallback a servicios de Odontología General. En `CitaService.ValidarDisponibilidadProfesionalAsync` (aprox. líneas 1250–1273), si el profesional no tiene ninguna asignación directa, se permite cualquier servicio.
- **Impacto:** el listado puede quedar vacío aunque las especialidades habiliten servicios, y la validación de citas puede aceptar servicios arbitrarios.
- **Corrección:** unificar la fuente de habilitación por especialidad y aplicar la regla predeterminada; compartir la validación entre consulta y agendamiento.

#### M3. La cancelación no intenta enviar correo de cambio de estado

- **Especificación:** RN-29 pide notificación interna en cambios relevantes y además intento de correo en Confirmada o Cancelada.
- **Evidencia:** [CitaService.cs](../Services/GestionDeCitas/CitaService.cs), `CancelarAsync` (aprox. líneas 804–871) crea historial/notificación interna y avisa a lista de espera, pero no invoca correo. [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs), `Cancelar` (aprox. líneas 627–645) tampoco envía correo. Confirmación sí intenta email en `ConfirmarYAsignarCitaAsync`.
- **Impacto:** el paciente puede no recibir el correo previsto tras la cancelación.
- **Corrección:** ejecutar el envío después de persistir la cancelación, registrar falla como advertencia y no revertir el cambio.

#### M4. Auditoría de citas incompleta respecto a RN-28

- **Especificación:** cada creación, actualización o cancelación debe registrar acción, usuario, IP y datos anteriores/nuevos.
- **Evidencia:** `RegistrarAuditoriaAsync` en [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs) (aprox. líneas 657–665) registra acción/usuario/IP/fecha/descripción, pero no asigna `DatosAnteriores` ni `DatosNuevos`. La ruta MVC equivalente de [GestionCitasController.cs](../Controllers/GestionDeCitas/GestionCitasController.cs) también registra principalmente descripción; algunas creaciones incluyen JSON nuevo, pero no hay par consistente anterior/nuevo en todas las operaciones.
- **Impacto:** queda incompleta la reconstrucción de cambios para auditoría.
- **Corrección:** centralizar auditoría de citas y capturar snapshot previo y posterior para todos los caminos (crear, editar, reagendar, cambiar estado y cancelar).

#### M5. Los KPIs no cumplen actualización periódica y el dashboard no distingue “No asistió”

- **Especificación:** RF-27 requiere polling periódico y diferenciar visualmente Cancelada y No asistió.
- **Evidencia:** [GestionCitasController.cs](../Controllers/GestionDeCitas/GestionCitasController.cs), `ConstruirDashboardRecepcionAsync` (aprox. líneas 2582–2710) genera un snapshot del día. [app.js](../wwwroot/js/Gestion_De_Citas/st-adm-01-dashboard/app.js) no implementa polling; solo anima los valores cargados. En el armado del dashboard, el estado `cancelada` usa `statusClass: "status-pendiente"` y el caso por defecto envía estados como “No asistió” a “Pendiente”.
- **Impacto:** KPIs quedan obsoletos hasta recargar y etiquetas/estilos mezclan estados independientes.
- **Corrección:** endpoint de KPIs y polling con intervalo moderado; mapear explícitamente Cancelada y No asistió con etiquetas/clases distintas.

#### M6. La transición de estado devuelve historial con autor no identificado

- **Especificación:** CU-CIT-04 exige registrar auditoría; la trazabilidad del ciclo de estados debe identificar el cambio.
- **Evidencia:** `CitaService.CambiarEstadoAsync` (aprox. líneas 712–770) registra `CitaHistorialEstado` pasando `idUsuario: null`; la acción API de estado tampoco llama a `RegistrarAuditoriaAsync` (CitasApiController, líneas 533–550). El servicio de cita no recibe contexto de actor.
- **Impacto:** algunos cambios de estado no identifican quién los realizó y carecen del registro general requerido.
- **Corrección:** propagar actor/IP al caso de uso y registrar historial/auditoría dentro del mismo flujo transaccional.

#### M7. RF-24 no completa una asignación automática; solo expone candidatos

- **Especificación:** RF-24 promete sugerir/asignar automáticamente un profesional compatible al liberarse un cupo, con confirmación de recepción.
- **Evidencia:** [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs), `ObtenerProfesionalesDisponibles` (aprox. líneas 342–360) devuelve lista de candidatos; cancelación notifica a lista de espera en `CitaService` (aprox. líneas 1492+), pero no se encontró un proceso que case automáticamente un candidato con solicitudes en espera cuando se libera una franja. Recepción confirma/asigna manualmente mediante `ConfirmarYAsignar`.
- **Impacto:** está implementado el flujo manual, no la asignación/sugerencia automática especificada.
- **Corrección:** implementar el emparejamiento al liberar disponibilidad o acotar formalmente RF-24 a una lista manual de candidatos.

#### M8. Estado y solicitud inicial varían entre caminos de creación

- **Especificación:** RF-18 establece Programada/Confirmada para una cita agendada y RF-23 una solicitud pendiente de confirmación.
- **Evidencia:** la solicitud de paciente crea estados `Solicitada`/`Pendiente` en `CitaService.SolicitarCitaPacienteAsync`; creación de agenda usa `Programada` según valores de entrada y catálogo. El vocabulario no está normalizado en todos los DTO, vistas y estados del catálogo.
- **Impacto:** filtros/KPIs que cuentan exclusivamente `Programada` pueden no contar estados alternativos equivalentes.
- **Corrección:** fijar catálogo canónico por flujo y mapear alias de forma consistente en escritura, filtros, dashboard y reportes.

#### M9. La ruta de cancelación de citas prohíbe al profesional

- **Especificación:** RF-20 incluye Profesional entre los actores autorizados, y RN-25/CU-CIT-03 limitan sus operaciones a citas donde aparece como prestador asignado.
- **Evidencia:** [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs), acción `Cancelar` (aprox. líneas 627–645), retorna `Forbid()` para todo usuario con rol Profesional antes de llamar al servicio, incluso si es dueño de la cita.
- **Impacto:** el requisito RF-20 no es alcanzable por ese actor mediante el endpoint REST; existe discrepancia entre RF-20 y la lista de actores más acotada de CU-CIT-05.
- **Corrección:** definir cuál actor está aprobado para cancelar; si se conserva lo indicado por RF-20, permitir al profesional solo sus citas y aplicar allí las restricciones de estado/fecha/motivo.

### Baja

#### B1. Variantes de ruta y flujo aumentan el riesgo de validaciones dispares

- **Especificación:** las mismas reglas de cita aplican a creación, cambio y cancelación, sin depender de pantalla.
- **Evidencia:** el módulo conserva acciones MVC y API, además de rutas antiguas/compatibilidad. Algunas operaciones registran historial/notificación/auditoría en distintos niveles. Por ejemplo, el servicio de cancelación recibe un motivo opcional y distintas acciones lo invocan con o sin él.
- **Impacto:** resulta fácil corregir una ruta y dejar otra con conducta diferente.
- **Corrección:** concentrar validaciones y efectos laterales en servicios de aplicación compartidos; mantener controladores como adaptadores HTTP/vista.

## 4. Funcionalidad implementada no incluida en RF/CU aprobados

Las siguientes capacidades aparecen en el código de los módulos auditados, pero no están descritas como requisito/caso de uso en los dos catálogos suministrados. Deben tratarse como alcance no aprobado en esta revisión y someterse a decisión funcional (conservar y documentar o retirar):

| Funcionalidad | Ubicación | Observación |
|---|---|---|
| Cálculo de comisiones/honorarios con porcentaje parametrizable (40% por defecto) | [ProfesionalesApiController.cs](../Controllers/Api/GestionDeProfesionales/ProfesionalesApiController.cs), acción `GetComisiones` (aprox. líneas 478–505); `ProfesionalService.CalcularComisionesAsync` (aprox. líneas 1172–1232) | No hay RF/CU de liquidación de honorarios/comisiones por profesional. |
| Registrar ausencia y reasignar citas automáticamente; cancelar citas si no hay reemplazo | [ProfesionalesApiController.cs](../Controllers/Api/GestionDeProfesionales/ProfesionalesApiController.cs), endpoint `ausencias-reasignar` (aprox. líneas 510–537); `ProfesionalService.RegistrarAusenciaConReasignacionAsync` (aprox. líneas 1234–1400) | RF-15 solo habla de bloquear disponibilidad y alertar para gestionar reprogramación; el flujo cambia profesional/estado de citas sin una confirmación descrita. También puede colisionar con RN-23 al cancelar sin motivo capturado. |
| Exportación PDF del dashboard de citas | [GestionCitasController.cs](../Controllers/GestionDeCitas/GestionCitasController.cs), acción `ExportarDashboardPdf` (líneas 165–220) | RF-27 no especifica exportación del dashboard. RF-35 contempla exportar reportes operativos a PDF/Excel, por lo que solo debe considerarse aprobada si este dashboard está dentro de ese concepto y conserva el formato esperado. |
| Vista/métricas extendidas del dashboard profesional (ingresos, widgets y banner de próxima cita) | [ProfesionalesController.cs](../Controllers/GestionDeProfesionales/ProfesionalesController.cs), `Stodo01Dashboard` (desde línea 80) y vista asociada | RF-13/RF-17 permiten agenda y reporte de desempeño, pero estos indicadores y widgets no están definidos expresamente en los catálogos de profesionales. Confirmar alcance y reglas de cálculo. |

No se catalogan como extras los calendarios, filtros, CRUD, mensajes, auditorías básicas, agenda auxiliar, notas, notificaciones o recordatorios cuando corresponden a un RF/CU explícito.

## 5. Matriz resumida de cobertura

| Requisito | Resultado | Evidencia resumida |
|---|---|---|
| RF-10 | Parcial/cumple | Alta transaccional, cuenta asociada y unicidad; la especialidad queda afectada por A4. |
| RF-11 | No cumple completo | Solo un ID de especialidad y sin fallback general (A4). |
| RF-12 | No cumple completo | Horarios CRUD y validación básica; no detecta cruces (A3); sin horario se permite agendar (A2). |
| RF-13 | Cumple con reserva | Agenda profesional filtra por identificador propio; validar flujos en ejecución pendiente. |
| RF-14 | Parcial/cumple | Estados permitidos, baja lógica, sincronización, citas futuras y auditoría existen. |
| RF-15 | Parcial | CRUD y bloqueo de disponibilidad existen; falta alerta en flujo común (M1); además hay reasignación extra. |
| RF-16 | Parcial/no cumple completo | Endpoint existe, pero lista por asignación directa, no por especialidad/fallback (M2). |
| RF-17 | Parcial | Reporte con última nota e indicadores existe; aislamiento del profesional requiere corregir A6. |
| RF-18 | Cumple con reservas | Crea con duración, horario de clínica, disponibilidad y referencias; dependerá de A2/M2. |
| RF-19 | Cumple con reservas | Comprueba profesional-paciente-consultorio y excluye canceladas en creación/actualización. |
| RF-20 | No cumple completo | Reagendar/cancelar y regla 2 horas implementadas; motivo no obligatorio (A1). |
| RF-21 | Parcial | Catálogo, transiciones, notificaciones e historial; permisos API/actor y autoría incompletos (A5/M6). |
| RF-22 | Cumple con reservas | Agendas semanal/global/profesional implementadas. |
| RF-23 | Cumple con reservas | Solicitud sin profesional y confirmación/asignación por Recepción existen. |
| RF-24 | Parcial | Lista de espera, avisos y candidatos; falta emparejamiento automático (M7). |
| RF-25 | Parcial | Notificaciones internas, lectura y recordatorios; falta correo al cancelar (M3). |
| RF-26 | Cumple con reservas | Agenda auxiliar, asistencia/procedimiento e interfaz de estado operativo real están presentes. |
| RF-27 | No cumple completo | KPIs existen, pero sin polling y estados visuales incorrectos (M5). |

La cobertura de los CU sigue la cobertura de sus RF relacionados, con los casos específicos de roles de CU-CIT-04 y obligatoriedad de CU-CIT-05 indicados arriba. Los restantes casos tienen vistas/rutas/servicios identificables; el análisis estático no acredita que cada combinación de datos se haya probado operativamente.

## 6. Recomendaciones ordenadas

1. Corregir primero A1–A6, por riesgo de auditoría, operación de agenda, configuración profesional y separación por rol.
2. Resolver M1–M8 y establecer un solo flujo de aplicación para cada operación crítica, con validaciones compartidas.
3. Decidir formalmente si se conservan comisiones, reasignación automática y exportaciones/widgets adicionales; documentar el alcance aprobado antes de declararlos conformes.
4. Actualizar los catálogos RF/CU/RN solo después de aprobación funcional; no dar por aprobado un comportamiento únicamente porque existe en el código.
5. Ejecutar después una verificación end-to-end por rol para los flujos completos y los casos límite corregidos. Esta revisión no ejecutó pruebas.

## 7. Referencias de código principales

- [CitaService.cs](../Services/GestionDeCitas/CitaService.cs): reglas de horario, disponibilidad, colisiones, cancelación, solicitudes, asignación, notificaciones y recordatorios.
- [CitasApiController.cs](../Api/Controllers/GestionDeCitas/CitasApiController.cs): endpoints y roles de API de citas.
- [GestionCitasController.cs](../Controllers/GestionDeCitas/GestionCitasController.cs): vistas, dashboards, acciones MVC y auditoría.
- [ProfesionalService.cs](../Services/GestionDeProfesionales/ProfesionalService.cs): profesionales, horarios, ausencias, servicios, comisiones y reasignación.
- [ProfesionalesApiController.cs](../Controllers/Api/GestionDeProfesionales/ProfesionalesApiController.cs): endpoints del módulo de profesionales.
- [ProfesionalesController.cs](../Controllers/GestionDeProfesionales/ProfesionalesController.cs): tableros, perfil y reportes clínicos.
- [CitaCancelacionDto.cs](../Models/DTOs/GestionDeCitas/CitaCancelacionDto.cs): validación del motivo.
