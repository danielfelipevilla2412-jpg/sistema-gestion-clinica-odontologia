# Requisitos funcionales de profesionales y citas

**Proyecto:** SmileTrack  
**Estado:** propuesta actualizada para aprobación funcional  
**Alcance de revisión:** implementación local de los módulos de profesionales y citas

## Alcance y límites de la verificación

Este documento describe los requisitos que se busca cumplir con el código actual. La revisión incluye controladores, servicios, vistas, JavaScript, scripts SQL y pruebas automatizadas. La revisión visual autenticada de sesiones anteriores cubrió pantallas representativas de los cinco roles y fue de solo lectura.

Las pruebas automatizadas no equivalen a una prueba end-to-end. En esta revisión no se enviaron formularios con datos reales, no se verificó la entrega de correo, no se aplicó la migración a una base de datos real y no se repitió la navegación autenticada después de los cambios funcionales. Por ello, “implementado” describe el código y sus pruebas disponibles, no una aceptación integral del producto.

Los encabezados originales usaban `RF-15` a `RF-32` mientras que el campo identificador iba de `RF-10` a `RF-27`. Para evitar duplicados, se mantiene como identificador canónico el campo `RF-10` a `RF-27`; la etiqueta original se conserva en la segunda columna.

## Requisitos funcionales en formato de especificación

Las prioridades y criterios siguientes son una propuesta basada en impacto operativo y en el comportamiento observado. La aprobación final de prioridad corresponde al dueño del producto. En todos los casos, el responsable de desarrollo indicado es Johan Santamaria.

### RF-10 — Registrar profesional y tarjeta profesional

**Identificador:** RF-10 (etiqueta original RF-15)  
**Nombre:** Registro de profesionales  
**Descripción:** El sistema deberá permitir a Administración registrar los datos personales del profesional, distinguir su tarjeta profesional del Registro Médico y crear la cuenta de usuario asociada. Deberá rechazar correos y tarjetas profesionales duplicados.  
**Prioridad:** Alta  
**Actor(es):** Administrador  
**Precondición:** El administrador inició sesión y los datos requeridos, incluido un número de tarjeta profesional válido, están disponibles.  
**Postcondición (criterio de aceptación):** Se persiste el profesional y su cuenta vinculada; un intento de repetir correo o tarjeta profesional es rechazado sin duplicar registros.  
**Responsable de desarrollo:** Johan Santamaria

### RF-11 — Asignar especialidades

**Identificador:** RF-11 (etiqueta original RF-16)  
**Nombre:** Gestión de especialidades del profesional  
**Descripción:** El sistema deberá permitir a Administración asignar una o varias especialidades a un profesional sin crear relaciones duplicadas.  
**Prioridad:** Alta  
**Actor(es):** Administrador  
**Precondición:** El administrador inició sesión y el profesional existe.  
**Postcondición (criterio de aceptación):** Las especialidades seleccionadas quedan asociadas una sola vez; si no se elige ninguna, el sistema conserva Odontología General como especialidad de respaldo.  
**Responsable de desarrollo:** Johan Santamaria

### RF-12 — Configurar disponibilidad semanal

**Identificador:** RF-12 (etiqueta original RF-17)  
**Nombre:** Horarios semanales del profesional  
**Descripción:** El sistema deberá permitir a Administración definir varios bloques de atención por día de la semana y generar disponibilidad a partir de ellos.  
**Prioridad:** Alta  
**Actor(es):** Administrador  
**Precondición:** El administrador inició sesión, el profesional existe y cada bloque tiene un día y horas válidas.  
**Postcondición (criterio de aceptación):** Se guardan los bloques y se calculan sus espacios disponibles; los bloques solapados se rechazan y los consecutivos se aceptan.  
**Responsable de desarrollo:** Johan Santamaria

### RF-13 — Consultar agenda propia

**Identificador:** RF-13 (etiqueta original RF-18)  
**Nombre:** Agenda del profesional  
**Descripción:** El sistema deberá permitir al profesional consultar sus propias citas en vistas diaria y semanal, ordenadas cronológicamente.  
**Prioridad:** Alta  
**Actor(es):** Profesional  
**Precondición:** El profesional inició sesión y tiene una cuenta vinculada a su perfil profesional.  
**Postcondición (criterio de aceptación):** La agenda muestra solo las citas de ese profesional en el período seleccionado; si no existen citas, la vista informa que la agenda está vacía.  
**Responsable de desarrollo:** Johan Santamaria

### RF-14 — Cambiar estado del profesional

**Identificador:** RF-14 (etiqueta original RF-19)  
**Nombre:** Estado operativo del profesional  
**Descripción:** El sistema deberá permitir a Administración cambiar el estado del profesional entre Activo, Vacaciones e Inactivo.  
**Prioridad:** Alta  
**Actor(es):** Administrador  
**Precondición:** El administrador inició sesión y el profesional existe.  
**Postcondición (criterio de aceptación):** El nuevo estado queda registrado y auditado; antes de inactivar se validan citas futuras y se sincroniza el estado de la cuenta vinculada.  
**Responsable de desarrollo:** Johan Santamaria

### RF-15 — Gestionar ausencias

**Identificador:** RF-15 (etiqueta original RF-20)  
**Nombre:** Registro de ausencias profesionales  
**Descripción:** El sistema deberá permitir a Administración y Recepción registrar, consultar, editar y eliminar ausencias de profesionales, con advertencia y confirmación cuando existan citas afectadas.  
**Prioridad:** Alta  
**Actor(es):** Administrador, Recepcionista  
**Precondición:** El actor inició sesión, tiene uno de los roles autorizados y seleccionó un profesional existente y un rango de fechas válido.  
**Postcondición (criterio de aceptación):** La ausencia bloquea disponibilidad durante el rango; si hay citas coincidentes, se muestran para revisión y, tras confirmar, el sistema intenta reasignarlas y cancela las que no puede reasignar.  
**Responsable de desarrollo:** Johan Santamaria

### RF-16 — Consultar y asignar servicios compatibles

**Identificador:** RF-16 (etiqueta original RF-21)  
**Nombre:** Compatibilidad entre servicios y especialidades  
**Descripción:** El sistema deberá ofrecer únicamente servicios activos compatibles con las especialidades del profesional y exigir una asignación activa del servicio para permitir el agendamiento.  
**Prioridad:** Alta  
**Actor(es):** Administrador, Recepcionista, Paciente  
**Precondición:** El servicio y el profesional están activos y se consulta una opción de asignación o agendamiento.  
**Postcondición (criterio de aceptación):** Los servicios incompatibles o no asignados no se pueden agendar; Odontología General se usa como respaldo solo cuando el profesional no tiene especialidad explícita.  
**Responsable de desarrollo:** Johan Santamaria

### RF-17 — Consultar reportes clínicos

**Identificador:** RF-17 (etiqueta original RF-22)  
**Nombre:** Reportes clínicos por profesional y período  
**Descripción:** El sistema deberá permitir al profesional consultar sus notas e indicadores clínicos y a Administración filtrar los reportes por profesional y período.  
**Prioridad:** Media  
**Actor(es):** Profesional, Administrador  
**Precondición:** El actor inició sesión y existen datos clínicos en el período consultado.  
**Postcondición (criterio de aceptación):** Los resultados corresponden al profesional y período solicitados; el profesional autenticado no puede consultar datos de otro profesional.  
**Responsable de desarrollo:** Johan Santamaria

### RF-18 — Registrar cita desde la agenda

**Identificador:** RF-18 (etiqueta original RF-23)  
**Nombre:** Creación de citas por Recepción y Administración  
**Descripción:** El sistema deberá permitir a Recepción o Administración registrar una cita con paciente, servicio, profesional, consultorio, fecha y hora.  
**Prioridad:** Crítica  
**Actor(es):** Recepcionista, Administrador  
**Precondición:** El actor inició sesión; los recursos existen y están activos; la fecha, horario y datos de la cita son válidos.  
**Postcondición (criterio de aceptación):** La cita se guarda solo si hay disponibilidad; la duración y hora final se calculan con la configuración del servicio y se rechazan datos u horarios inválidos.  
**Responsable de desarrollo:** Johan Santamaria

### RF-19 — Evitar conflictos de citas

**Identificador:** RF-19 (etiqueta original RF-24)  
**Nombre:** Validación de conflictos de recursos  
**Descripción:** El sistema deberá validar conflictos de profesional, paciente y consultorio al crear o reagendar una cita.  
**Prioridad:** Crítica  
**Actor(es):** Recepcionista, Administrador  
**Precondición:** El actor inició sesión y envió una cita con recursos y horario definidos.  
**Postcondición (criterio de aceptación):** Si un recurso está ocupado, la operación no se confirma y el mensaje identifica el conflicto; si no hay conflicto, la cita se persiste.  
**Responsable de desarrollo:** Johan Santamaria

### RF-20 — Cancelar y reagendar citas

**Identificador:** RF-20 (etiqueta original RF-25)  
**Nombre:** Cancelación y reprogramación  
**Descripción:** El sistema deberá permitir cancelar citas elegibles con un motivo obligatorio de 5 a 500 caracteres y reagendar citas elegibles a una fecha futura.  
**Prioridad:** Alta  
**Actor(es):** Paciente, Recepcionista, Administrador  
**Precondición:** El actor está autenticado y la cita existe; para cancelaciones iniciadas por pacientes, deben faltar al menos dos horas para la cita.  
**Postcondición (criterio de aceptación):** La cancelación guarda el motivo y libera los recursos; el backend rechaza cancelaciones fuera del límite del paciente y rechaza reagendar citas de origen pasadas o canceladas, o a una fecha no futura.  
**Responsable de desarrollo:** Johan Santamaria

### RF-21 — Gestionar estados y atención

**Identificador:** RF-21 (etiqueta original RF-26)  
**Nombre:** Flujo operativo de estados de cita  
**Descripción:** El sistema deberá conservar separados los estados Cancelada y No asistió y aplicar transiciones permitidas según el rol: Recepción registra llegada/sala de espera, el profesional inicia y finaliza la atención de sus citas y Administración gestiona las transiciones autorizadas.  
**Prioridad:** Crítica  
**Actor(es):** Recepcionista, Profesional, Administrador  
**Precondición:** El actor inició sesión, tiene autorización para la cita y la transición está permitida desde el estado actual.  
**Postcondición (criterio de aceptación):** La transición válida actualiza estado e historial y produce las notificaciones aplicables; una transición inválida o no autorizada se rechaza sin modificar la cita. Los filtros y KPI distinguen inasistencia de cancelación.  
**Responsable de desarrollo:** Johan Santamaria

### RF-22 — Navegar y filtrar agendas

**Identificador:** RF-22 (etiqueta original RF-27)  
**Nombre:** Navegación de agendas de citas  
**Descripción:** El sistema deberá permitir navegar y filtrar las agendas por los criterios disponibles para cada rol y comunicar errores de carga.  
**Prioridad:** Alta  
**Actor(es):** Administrador, Recepcionista, Profesional, Auxiliar  
**Precondición:** El actor inició sesión y seleccionó un período o filtro permitido para su rol.  
**Postcondición (criterio de aceptación):** Se muestra la agenda correspondiente al período y filtros; si no se puede cargar, la interfaz informa el error sin presentarlo como una agenda vacía.  
**Responsable de desarrollo:** Johan Santamaria

### RF-23 — Solicitar cita como paciente

**Identificador:** RF-23 (etiqueta original RF-28)  
**Nombre:** Solicitud de cita pendiente de confirmación  
**Descripción:** El sistema deberá permitir al paciente solicitar una cita para un servicio y horario futuro, con profesional preferido opcional, sin reservar recursos antes de la revisión de Recepción.  
**Prioridad:** Alta  
**Actor(es):** Paciente, Recepcionista  
**Precondición:** El paciente inició sesión, completó su perfil y seleccionó un servicio activo y un horario válido.  
**Postcondición (criterio de aceptación):** La solicitud queda en estado pendiente, sin profesional ni consultorio asignados, conserva la preferencia opcional y Recepción puede confirmarla completando la asignación.  
**Responsable de desarrollo:** Johan Santamaria

### RF-24 — Inscribirse en lista de espera

**Identificador:** RF-24 (etiqueta original RF-29)  
**Nombre:** Lista de espera y asignación de franja liberada  
**Descripción:** El sistema deberá permitir al paciente inscribirse en lista de espera para un servicio y rango de fechas, con profesional preferido opcional; al liberarse una cita, deberá intentar asignar la franja al primer candidato compatible que siga disponible.  
**Prioridad:** Media  
**Actor(es):** Paciente, sistema  
**Precondición:** El paciente inició sesión y el servicio y rango de fechas de la inscripción son válidos.  
**Postcondición (criterio de aceptación):** La inscripción queda registrada; ante una cancelación, el sistema comprueba servicio, preferencia, horario y conflictos, crea la cita para el primer candidato elegible y notifica la asignación.  
**Responsable de desarrollo:** Johan Santamaria

### RF-25 — Gestionar notificaciones y recordatorios

**Identificador:** RF-25 (etiqueta original RF-30)  
**Nombre:** Notificaciones internas y recordatorios  
**Descripción:** El sistema deberá generar notificaciones internas para eventos relevantes, permitir consultarlas y marcarlas como leídas, y permitir enviar recordatorios individuales o masivos.  
**Prioridad:** Alta  
**Actor(es):** Paciente, Recepcionista, Administrador, sistema  
**Precondición:** Existe un evento notificable o un usuario autorizado inicia un envío de recordatorio.  
**Postcondición (criterio de aceptación):** Las notificaciones se almacenan y el paciente solo consulta las propias; los recordatorios muestran resultados exitosos y fallidos sin ocultar errores de envío.  
**Responsable de desarrollo:** Johan Santamaria

### RF-26 — Apoyar la atención del auxiliar

**Identificador:** RF-26 (etiqueta original RF-31)  
**Nombre:** Agenda y apoyo operativo del auxiliar  
**Descripción:** El sistema deberá permitir al auxiliar consultar una agenda navegable y un historial parcial, registrar asistencia/procedimiento y consultar el estado operativo del consultorio.  
**Prioridad:** Alta  
**Actor(es):** Auxiliar  
**Precondición:** El auxiliar inició sesión y tiene acceso a la cita o consultorio correspondiente.  
**Postcondición (criterio de aceptación):** El auxiliar consulta la información autorizada, puede guardar los datos de asistencia/procedimiento y ve el estado actualizado del consultorio.  
**Responsable de desarrollo:** Johan Santamaria

### RF-27 — Consultar indicadores de los dashboards

**Identificador:** RF-27 (etiqueta original RF-32)  
**Nombre:** Indicadores diarios de gestión  
**Descripción:** El sistema deberá mostrar en los dashboards los indicadores diarios calculados por el servidor, incluyendo un indicador independiente de citas No asistió.  
**Prioridad:** Media  
**Actor(es):** Administrador, Recepcionista, Profesional, Auxiliar  
**Precondición:** El actor inició sesión y existen datos del día para su dashboard.  
**Postcondición (criterio de aceptación):** Las tarjetas muestran los valores calculados para el día y distinguen las inasistencias de las cancelaciones; al recargar la vista, los valores vuelven a consultarse.  
**Responsable de desarrollo:** Johan Santamaria

### Observación sobre el ejemplo RF8

El ejemplo “Gestión de citas — RF8 — permitir crear una orden de compra” mezcla módulos: no se encontró funcionalidad ni código de órdenes de compra en este repositorio y una orden de compra no pertenece al flujo de citas. No se incorpora a este catálogo; debe asignarse al módulo de compras/inventario y validarse contra su implementación antes de darle prioridad o aceptación.

## Estado de verificación

Validación automatizada de esta revisión: **492 pruebas aprobadas, 0 fallidas**; `node --check` para los JavaScript modificados y `git diff --check` sin errores. Se encontró `sqlcmd`, pero no había una instancia SQL Server accesible en `localhost`; por tanto, no se aplicó ni ejecutó la migración.

| ID | Resultado del código y pruebas disponibles | Límite que permanece |
|---|---|---|
| RF-10 | Implementado: tarjeta profesional requerida al crear/editar y validada por unicidad; se muestra en gestión. | Registros antiguos no reciben tarjetas inventadas: deben completarse manualmente. |
| RF-11 | Implementado: asignación de varias especialidades y fallback General. | Verificar formularios con datos de prueba en una instalación desplegada. |
| RF-12 | Implementado: la UI permite varios bloques y el backend rechaza solapamientos. | Aplicar y verificar en una base migrada. |
| RF-13 | Implementado en agenda y consultas con filtro por profesional autenticado. | La revisión visual posterior a estos cambios está pendiente. |
| RF-14 | Implementado en servicio/controlador, con validación de citas futuras y auditoría. | No se simuló la inactivación en un entorno con datos de prueba. |
| RF-15 | Implementado: Administración y Recepción disponen de gestión de ausencias; la vista de Recepción consulta, crea, edita y elimina mediante la API autorizada. El registro con citas coincidentes muestra la confirmación de reasignación/cancelación. | No se ejecutaron altas/ediciones en una sesión autenticada posterior a los cambios. |
| RF-16 | Implementado: lista, asignación y agendamiento usan especialidades compatibles y asignación activa; servicios sin especialidad no se ofrecen como asignables. | Verificar la configuración real del catálogo de servicios en cada base. |
| RF-17 | Implementado en consultas: profesional y período filtran los resultados; se conserva el aislamiento del profesional autenticado. | No se ejecutó el reporte con identidades distintas en una prueba end-to-end. |
| RF-18 | Implementado en backend, con validaciones de agenda y duración. | Los formularios no se enviaron durante esta revisión. |
| RF-19 | Implementado y cubierto por pruebas de conflictos de recursos. | La concurrencia real depende de aplicar y validar la base de datos de destino. |
| RF-20 | Implementado en servicio/API: motivo, límites, anticipación de cancelación del paciente y guardas de reagendamiento. | No se enviaron solicitudes autenticadas de cancelación o reagendamiento. |
| RF-21 | Implementado en helper, controladores MVC/API y vistas: transición de estado por rol; las rutas de edición también validan permisos para no saltarse el control de estado. | No se probó manualmente el rechazo HTTP por cada rol en el navegador. |
| RF-22 | Implementado en vistas y endpoints de agenda, con manejo de error. | No se simuló una caída de servicio en la comprobación visual. |
| RF-23 | Implementado: solicitud pendiente sin profesional ni consultorio asignados, profesional preferido opcional y confirmación de Recepción. | La confirmación no se probó en una sesión autenticada posterior a los cambios. |
| RF-24 | Implementado en inscripción, persistencia y asignación transaccional al cancelar, con comprobación de compatibilidad y conflictos. | Requiere validar con una base migrada y casos concurrentes. |
| RF-25 | Implementado en el código de notificaciones y recordatorios. | No se verificó la entrega real por correo. |
| RF-26 | Implementado en los módulos existentes. | No se guardaron registros de asistencia/procedimiento en esta revisión. |
| RF-27 | Implementado: KPI de inasistencias separado y renderizado en el dashboard. | Los KPI se actualizan al recargar; el polling no forma parte del requisito vigente. |

### Criterios transversales de rol

- Recepción puede confirmar solicitudes y marcar llegada/sala de espera; no puede iniciar ni finalizar una atención.
- El profesional solo opera sus citas; sus transiciones clínicas son iniciar y finalizar la atención.
- Los controles de rol se aplican tanto a las rutas de cambio de estado como a las rutas de edición que también reciben un estado.
- El catálogo de estados conserva Cancelada y No asistió como valores distintos. No se debe equiparar cancelación con inasistencia en reportes o filtros.

## Acciones pendientes para aceptación integral

1. Aplicar `Database/MIGRATION_BRECHAS_PROFESIONALES_CITAS.sql` a una base existente y comprobar que la versión de SQL Server y sus nombres de objetos coincidan con el entorno destino. No se ejecutó aquí.
2. Completar tarjetas profesionales faltantes en registros ya existentes.
3. Hacer pruebas end-to-end con datos de prueba para altas/ediciones, solicitudes, lista de espera, ausencias, cancelaciones, reagendamientos y transiciones permitidas/denegadas por rol.
4. Verificar la entrega de notificaciones con la configuración SMTP/servicio correspondiente.

No se consideran brechas obligatorias el calendario mensual, polling de KPIs o deshabilitar el botón antes de completar campos; solo deben incorporarse si el dueño del producto los aprueba.

## Evidencia

- Servicios de profesionales: [ProfesionalService.cs](../../../Services/GestionDeProfesionales/ProfesionalService.cs).
- Servicios de citas: [CitaService.cs](../../../Services/GestionDeCitas/CitaService.cs).
- Controladores MVC y API: [GestionCitasController.cs](../../../Controllers/GestionDeCitas/GestionCitasController.cs), [CitasApiController.cs](../../../Api/Controllers/GestionDeCitas/CitasApiController.cs).
- Vistas y scripts: [vistas de citas](../../../Views/Gestion_De_Citas/), [gestión de profesionales](../../../Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/).
- Gestión de ausencias de Recepción: [vista](../../../Views/Gestion_De_Profesionales/st-rec-07-ausencias/index.cshtml), [JavaScript](../../../wwwroot/js/Gestion_De_Profesionales/st-rec-07-ausencias/ausencias.js).
- Migración para bases existentes: [MIGRATION_BRECHAS_PROFESIONALES_CITAS.sql](../../../Database/MIGRATION_BRECHAS_PROFESIONALES_CITAS.sql).
- Pruebas: [pruebas unitarias](../../../SmileTrack_MVC.Tests/Unit/).
- Auditoría anterior, mantenida sin cambios: [INFORME_AUDITORIA_CITAS_PROFESIONALES_2026-09-22.md](../INFORME_AUDITORIA_CITAS_PROFESIONALES_2026-09-22.md).

La aceptación del documento corresponde al dueño del producto. No implica que se hayan ejecutado pruebas operativas en producción ni que los pendientes de aceptación integral estén cerrados.
