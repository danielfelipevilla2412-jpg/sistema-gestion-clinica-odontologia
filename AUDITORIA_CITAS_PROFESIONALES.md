# Auditoría de gestión de citas y profesionales

**Fecha:** 13 de noviembre de 2026  
**Alcance:** vistas, navegación, controles y contratos relacionados con citas y profesionales.

## Resumen ejecutivo

- Se inventariaron 19 vistas funcionales y 20 scripts JavaScript de los módulos.
- Las 19 rutas de vista respondieron HTTP 200 al probarlas con sus roles autorizados.
- Se reprodujeron y corrigieron tres fallos: un endpoint de citas que devolvía 500, una carga duplicada de `ToastService` y el botón de recordatorios de facturas vencidas.
- Se corrigieron cuatro pruebas unitarias cuya fecha dinámica podía caer en domingo, día en que la clínica no atiende.
- El banner de próxima cita en ST-ODO-01 ahora usa el mismo color primario del elemento activo del sidebar.
- Se corrigió la edición de profesionales que no tienen una cuenta de acceso vinculada.
- Se optimizaron las búsquedas de disponibilidad para reducir consultas repetidas al revisar profesionales y horarios.
- La suite completa terminó con **449 pruebas aprobadas, 0 fallidas**.

No se crearon, editaron ni cancelaron citas, profesionales o facturas, ni se enviaron correos. Las acciones con efectos persistentes o externos se revisaron sin ejecutarlas.

## Cobertura de vistas

Todas las rutas de la tabla se abrieron con el rol correspondiente y devolvieron HTTP 200.

| Rol | Vista | Ruta |
|---|---|---|
| Administrador | ST-ADM-01 Dashboard de citas | `/gestion-de-citas/st-adm-01-dashboard` |
| Administrador | ST-ADM-08 Agenda | `/gestion-de-citas/st-adm-08-agenda` |
| Administrador | ST-ADM-09 Gestión integral de citas | `/gestion-de-citas/st-adm-09-citas` |
| Administrador | ST-ADM-07 Gestión de profesionales | `/gestion-de-profesionales/st-adm-07-gestion-profesionales` |
| Administrador | ST-ADM-07 Servicios asignados | `/gestion-de-profesionales/servicios-asignados` |
| Administrador | ST-ADM-14 Reportes clínicos | `/gestion-de-profesionales/st-adm-14-reportes-clinicos` |
| Profesional | ST-ODO-01 Dashboard | `/gestion-de-profesionales/st-odo-01-dashboard` |
| Profesional | ST-ODO-02 Agenda | `/gestion-de-citas/st-odo-02-agenda` |
| Profesional | ST-ODO-09 Perfil profesional | `/gestion-de-profesionales/st-odo-09-perfil-profesional` |
| Recepción | ST-REC-01 Dashboard | `/gestion-de-citas/st-rec-01-dashboard` |
| Recepción | ST-REC-03 Gestión de citas | `/gestion-de-citas/st-rec-03-gestion-citas` |
| Recepción | ST-REC-05 Recordatorios | `/gestion-de-citas/st-rec-05-recordatorios` |
| Paciente | ST-PAC-01 Mis citas | `/gestion-de-citas/st-pac-01-mis-citas` |
| Paciente | ST-PAC-03 Notificaciones | `/gestion-de-citas/st-pac-03-notificaciones` |
| Auxiliar | ST-AUX-01 Panel operativo | `/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo` |
| Auxiliar | ST-AUX-02 Agenda de apoyo | `/gestion-de-citas/st-aux-02-agenda-apoyo` |
| Auxiliar | ST-AUX-06 Asistencia de procedimiento | `/gestion-de-citas/st-aux-06-asistencia-procedi` |
| Auxiliar | ST-AUX-09 Estado del consultorio | `/gestion-de-citas/st-aux-09-estado-consultorio` |
| Auxiliar | ST-AUX-10 Citas finalizadas | `/gestion-de-citas/st-aux-10-citas-finalizadas` |

También se comprobó el selector de profesionales de ST-ADM-08 con datos existentes: para el servicio probado se mostraron opciones y se pudo seleccionar un profesional. No se guardó una cita.

## Hallazgos

### F-01 — Próximas citas de recepción devolvía HTTP 500 — corregido

`GET /api/citas/proximas` usaba `EsEstadoCancelado`, un método C# propio, dentro de una consulta EF Core. SQL Server no puede traducir ese método y el dashboard registraba un error cada vez que consultaba las próximas citas.

**Corrección:** se limita primero la consulta en SQL al intervalo de tiempo solicitado y se filtran los estados cancelados y se formatea la respuesta después de materializar ese conjunto acotado.

**Validación:** en una instancia temporal compilada, el endpoint devolvió HTTP 200 y `success: true`; el perfil de recepción cargó sin el error de consulta.

### F-02 — Script de `ToastService` duplicado en el perfil profesional — corregido

El layout compartido ya carga `shared/toasts.js`, pero ST-ODO-09 volvía a incluirlo. El navegador reportaba `Identifier 'ToastService' has already been declared`.

**Corrección:** se quitó la inclusión duplicada de [Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml](./Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml).

**Validación:** la vista compilada contiene una sola referencia al script compartido y no produjo errores JavaScript al abrir el perfil.

### F-03 — Recordatorios de facturas vencidas — corregido

El botón «Notificar» de la alerta de facturas vencidas en ST-REC-05 enviaba `POST /api/facturas/recordatorios-vencidas`, pero no existía una acción para atenderlo.

**Corrección:** se agregó el endpoint protegido para administrador y recepción, con validación antiforgery. Selecciona facturas en estado pendiente emitidas hace más de 15 días, agrupa las facturas por paciente y envía un correo con sus saldos. Los pacientes sin correo y los fallos SMTP se reportan como no enviados. La interfaz ahora exige una respuesta JSON válida y diferencia éxitos, fallos parciales y errores.

**Validación:** una prueba con servicio de correo simulado verifica el umbral, la exclusión de facturas pagadas/no vencidas, la agrupación por paciente y el caso de correo ausente. No se enviaron correos reales.

Referencia: [wwwroot/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js](./wwwroot/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js).

### F-04 — Fechas de pruebas podían caer en domingo — corregido

Cuatro pruebas generaban su cita con `DateTime.Today.AddDays(2)`. Si la suite se ejecutaba un viernes, intentaban agendar el domingo y fallaban por la regla válida de cierre dominical.

**Corrección:** esos casos desplazan la fecha al lunes cuando el día calculado es domingo. Se mantiene la regla funcional: el sábado está habilitado y el domingo no.

### F-05 — Color distinto en el banner de próxima cita — corregido

La tarjeta `.next-appt-card.upcoming` usaba un gradiente morado, distinto del azul del elemento activo del sidebar.

**Corrección:** usa `var(--primary)` y sombras azules para coincidir con el estado activo del sidebar. Los estilos semánticos de cita urgente y de agenda vacía se mantienen.

Referencia: [wwwroot/css/Gestion_De_Profesionales/st-odo-01-dashboard/styles.css](./wwwroot/css/Gestion_De_Profesionales/st-odo-01-dashboard/styles.css).

### F-06 — No se podía editar el perfil de un profesional sin usuario asociado — corregido

En ST-ADM-07, un profesional con `idUsuario` nulo no podía guardar cambios: el cliente exigía correo y el servicio rechazaba la actualización por falta de cuenta de acceso.

**Corrección:** se permite actualizar los datos del perfil sin crear credenciales de forma implícita. El correo se muestra como no editable cuando no hay una cuenta vinculada, y se retiró el control de contraseña que no se enviaba en este flujo.

**Validación:** se agregó una prueba unitaria de edición de perfil sin cuenta vinculada; queda incluida en la suite completa aprobada.

### F-07 — Consulta lenta de profesionales disponibles en ST-ADM-08 — optimizada, pendiente de medición en servidor actualizado

La consulta probada para «Limpieza dental» respondía HTTP 200 con dos profesionales, pero tardaba **10.388 ms**. El servicio recorría profesionales y ejecutaba sus validaciones de horario, asignación, ausencia, bloqueo y cita en forma secuencial.

**Corrección:** se filtran profesionales elegibles en lote y se consultan horarios, ausencias, bloqueos y conflictos en grupos, manteniendo las reglas de servicio activo, especialidad, horario y citas que ocupan el profesional.

**Validación:** las pruebas unitarias de `CitaServiceTests` y la suite completa pasan. No se repitió la medición HTTP de rendimiento después del cambio: el proceso activo en 5259 conserva el binario anterior y no se detuvo.

### F-08 — Búsqueda de horarios del paciente repetía consultas por intervalo — optimizada

La generación de opciones consultaba repetidamente la configuración de clínica, la disponibilidad del profesional y los conflictos de cada consultorio para cada intervalo horario.

**Corrección:** se cargan una vez por fecha la elegibilidad del profesional, ausencias, bloqueos, consultorios y citas relevantes; los intervalos se verifican en memoria, conservando el descarte de citas solapadas y la selección de consultorios libres.

**Validación:** se agregó una prueba que verifica que un bloqueo de 09:00 a 10:00 quite ese intervalo sin eliminar el horario libre de las 10:00. Las pruebas unitarias y la suite completa pasan.

## Validaciones y límites

- `dotnet test SmileTrack_MVC.Tests --no-restore` no pudo sobrescribir el ejecutable del servidor activo en el puerto 5259. Se repitió con `-p:UseAppHost=false` y `-p:OutDir=<salida-temporal>` para aislar la compilación.
- Para esta revisión se ejecutó la suite completa con una carpeta de salida temporal. Resultado actual: **449/449 pruebas aprobadas**. La compilación mostró advertencias de nulabilidad en `SmileTrack_MVC.Tests/Unit/ViewComponentsTests.cs`, fuera de los cambios funcionales de esta revisión.
- `git diff --check` terminó correctamente.
- El servidor que ya estaba ejecutándose en 5259 no se reinició; debe reiniciarse y recargarse para ver las correcciones de backend y el nuevo estilo.
- La navegación de las 19 vistas y los flujos de lectura se comprobaron. No se ejecutaron altas, ediciones, cancelaciones, cambios de estado ni envíos de notificaciones/correos sobre los datos existentes. Esos casos requieren un entorno de pruebas aislado para validar de extremo a extremo sin afectar registros reales.
- La suite usa pruebas automatizadas; todavía se requiere validar en un entorno integrado con SQL Server y repetir la medición de ST-ADM-08 con el binario actualizado. Por ello, estos resultados no certifican que todos los escenarios de despliegue estén libres de defectos ni que el servidor activo ya tenga los cambios.
