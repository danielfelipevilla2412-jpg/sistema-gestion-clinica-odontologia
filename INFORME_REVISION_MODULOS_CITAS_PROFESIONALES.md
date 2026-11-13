# Informe de revisión de gestión de citas y profesionales

## Resultado ejecutivo

Se revisaron las vistas, scripts asociados, rutas MVC/API y servicios de negocio de los módulos de citas y profesionales. Se corrigió un defecto funcional en las pestañas de estado de la vista de recepción: en el modo SSR, las pestañas no tenían manejadores y no reflejaban el filtro activo.

La suite automatizada terminó con **199 pruebas aprobadas, 0 fallidas**. Esto verifica compilación y regresión automatizada, pero no equivale a certificar los flujos en navegador ni el comportamiento de SQL Server en producción.

**Estado final: revisión estática completada; aceptación E2E y de producción pendiente.** No se debe presentar el sistema como funcional de extremo a extremo hasta validar los escenarios autenticados y la persistencia contra la base desplegada.

## Revisión vista por vista

El inventario contiene 14 vistas funcionales de citas y 5 de profesionales. `_ViewStart.cshtml` es configuración de layout, no una pantalla operativa.

| Vista | Acciones y conexión examinadas | Resultado de revisión |
|---|---|---|
| Citas — dashboard administrativo (`st-adm-01`) | KPIs, ocupación, distribución por estado y descarga de PDF. El botón conecta con una ruta autorizada que genera el PDF en el servidor. | Conectado en código. No se descargó el archivo en navegador. |
| Citas — agenda general (`st-adm-08`) | Navegación semanal, filtros por profesional/consultorio, consulta de disponibilidad y modales de alta/edición; usa API y recarga SSR de semanas. | Conectado en código; disponibilidad y concurrencia requieren prueba con SQL Server y dos sesiones. |
| Citas — gestión integral (`st-adm-09`) | Filtros GET, paginación SSR, alta/edición, detalles, cambio de estado y cancelación con confirmación. | Rutas y formularios conectados. Los flujos de navegador no se recorrieron. |
| Citas — dashboard de recepción (`st-rec-01`) | Resumen diario, próximas citas, acciones rápidas y alertas. | Pantalla de operación conectada a API; no se verificó entrega real de notificaciones/facturas. |
| Citas — gestión de recepción (`st-rec-03`) | Listado SSR/API, búsqueda/filtros, solicitudes pendientes, detalle, edición, cancelación y paginación. | Se corrigió el flujo de pestañas SSR: ahora filtra por canceladas/inasistencias, conserva los demás filtros y anuncia visualmente el estado seleccionado. El resto queda pendiente de interacción E2E. |
| Citas — recordatorios (`st-rec-05`) | Selección individual/masiva y envío mediante API; consulta de historial. | Conectado en código. El envío efectivo depende de servicios/correo configurados y no se ejecutó. |
| Citas — agenda profesional (`st-odo-02`) | Vistas semanal/diaria/lista, filtros, búsqueda, impresión, detalle y transiciones de atención. | La actualización de estado valida propiedad del profesional en API. No se ejecutó una sesión de ese rol. |
| Citas — panel operativo auxiliar (`st-aux-01`) | KPIs y exportación PDF. | El botón conecta a la ruta de exportación; se comprobó la autorización declarada. Descarga no probada. |
| Citas — agenda de apoyo (`st-aux-02`) | Vistas día/semana, navegación y filtros por profesional/tipo; muestra alergias y datos de atención. | Renderizado y eventos revisados en código. No se comprobó visualmente ni con datos reales. |
| Citas — asistencia en procedimiento (`st-aux-06`) | Selector de cita, cronómetro, checklist y sincronización de estado. | Conectado a endpoints de asistencia; hay respaldo local. No se midió persistencia después de reiniciar ni sincronización multiusuario. |
| Citas — estado del consultorio (`st-aux-09`) | Selector de consultorio, checklist, observaciones, estado e historial. | Conectado a API con token antifalsificación. No se probó escritura contra la base desplegada. |
| Citas — finalizadas (`st-aux-10`) | Filtros por estado, paginación de cliente y descarga CSV. | Exportación presente; descarga revisada en código, no en navegador. El alcance del archivo es el conjunto de datos entregado a la vista, no una exportación general del módulo CRUD. |
| Citas — mis citas (`st-pac-01`) | Filtros, búsqueda, detalle, cancelación con motivo y solicitud de cita; la solicitud exige perfil completo. | El API aplica propiedad del paciente y validación de perfil. No se probó con cuentas de paciente reales. |
| Citas — notificaciones (`st-pac-03`) | Búsqueda, filtro por tipo/no leídas, detalle, marcar leída/todas y eliminar. | Acciones conectadas a endpoints; la autorización real por usuario requiere E2E. |
| Profesionales — gestión administrativa (`st-adm-07`) | CRUD por API, filtros/paginación, cambio de estado, detalle, horarios/ausencias y revisión de credenciales. | Conectado en código. El servidor aplica autorización de administrador y validaciones; no se recorrió el flujo con cuenta real. |
| Profesionales — servicios asignados | Selección, asignación/actualización de precio, activar/desactivar. | Acceso de administrador, antifalsificación y validación servidor de estado/especialidad/precio. No se ejecutó contra BD. |
| Profesionales — reportes clínicos (`st-adm-14`) | Búsqueda, filtros por mes/profesional y paginación. | El controlador fuerza el alcance del profesional autenticado; no ofrece importación ni exportación en esta vista. |
| Profesionales — dashboard (`st-odo-01`) | Métricas, próxima cita, accesos a agenda/reportes/perfil y alertas. | Pantalla de consulta y enlaces revisados; no se probaron datos vivos. |
| Profesionales — perfil (`st-odo-09`) | Datos propios, horarios, contraseña y carga/retiro de credenciales. | El controlador restringe el perfil/credenciales al profesional autenticado y resguarda archivos fuera de contenido público. Cambio de contraseña y carga no se ejecutaron. |

## Correcciones aplicadas

| Módulo | Defecto | Corrección |
|---|---|---|
| Citas — recepción | La lista SSR dejaba inactivas las pestañas de cancelaciones e inasistencias; el estado inicial tampoco representaba el filtro aplicado. | Se añadieron manejadores SSR que actualizan el filtro y envían el formulario GET, además de estado `active`/`aria-pressed` derivado del filtro en la vista. |
| Citas — recepción | Acciones Ver/Editar SSR dependían de datos que no estaban cargados en memoria; había un manejador de detalle duplicado que interpolaba datos en HTML. | Se enlazaron las acciones SSR con los datos renderizados y se retiró el manejador duplicado inseguro. |
| Citas — asistencia en procedimiento | El acceso a `localStorage` podía lanzar una excepción antes de aplicar el respaldo cuando el navegador bloquea el almacenamiento. | Se incluyó la lectura y el parseo dentro del manejo de errores para usar valores por defecto y registrar el fallo. |
| Profesionales | La acción Editar SSR dependía de `onclick` y la representación dinámica no escapaba de manera segura algunos valores. | Se pasó a delegación por ID y se reforzó el escape HTML de la tabla. |
| API MVC | Solicitudes API no autenticadas o sin permisos podían recibir redirección HTML de cookie en vez de 401/403. | Se configuró la respuesta de autenticación/autorización para mantener semántica API. |

## Resultado por requisitos transversales

| Área | Estado | Hallazgo / límite |
|---|---|---|
| Botones, formularios y feedback | Revisado en código | Se comprobó el enlace visible entre vistas, scripts y rutas para las pantallas listadas. La visibilidad, estado por viewport, foco, mensajes y respuesta efectiva necesitan navegador autenticado. |
| Reglas de agenda | Revisado en servicio y pruebas | `CitaService` valida entidades activas, horario clínico/individual y conflictos de profesional, paciente y consultorio; alta/actualización usan transacción serializable. No se emuló una carrera concurrente contra SQL Server. |
| Estados de cita | Revisado en código | API aplica validaciones de flujo y ownership donde corresponde. No se ejecutó la matriz de transiciones por rol en HTTP real. |
| Autorización y datos propios | Revisado en código | Rutas declaran roles; API de citas limita listados por paciente/profesional y valida ownership en operaciones individuales. No equivale a una prueba de manipulación de URL/solicitud con cinco cuentas reales. |
| Persistencia e integridad SQL | Parcial | Lógica de servicio y modelos revisados. No se inspeccionó una base activa en esta pasada ni se reiniciaron contenedores; integridad referencial, índices reales y persistencia post-reinicio quedan pendientes de entorno. |
| HTTP y errores | Revisado en código | Hay respuestas 200/201/400/401/403/404/409 en rutas pertinentes. No se verificaron todas las respuestas mediante cliente HTTP autenticado; la cancelación es lógica/soft-delete y retorna éxito normal, no 204. |
| Filtros, paginación y búsqueda | Revisado en código | Están implementados en las vistas correspondientes; no se recorrieron todas las combinaciones con datasets extensos. |
| Ordenamiento | Parcial | Hay ordenamientos por defecto del servidor y cronología en agendas, pero no se identificó un control general de ordenamiento interactivo para los listados CRUD. |
| Importación/exportación | Parcial | Se encontraron PDF del dashboard administrativo/operativo y CSV de citas finalizadas. No se encontró una función general de importación ni exportación de los listados CRUD de citas/profesionales. |
| Rendimiento y volumen | Pendiente | No se ejecutó una prueba de carga. El listado administrativo de profesionales también carga credenciales para revisión en la misma vista; su comportamiento con gran volumen debe medirse. |
| JavaScript y navegador | Parcial | Código y bindings inspeccionados; la URL local `http://localhost:5000` rechazó conexión, por lo que no había aplicación ejecutándose para prueba visual/E2E. |

## Verificación automatizada

Ejecutado después de las correcciones:

```text
dotnet test SmileTrack_MVC.Tests\SmileTrack_MVC.Tests.csproj --no-restore --verbosity minimal
Correctas: 199; fallidas: 0; omitidas: 0
```

La prueba compila el proyecto y pasa la suite existente. No simula clics, sesiones autenticadas, persistencia real, proveedores de correo ni concurrencia SQL.

## Pendientes antes de declarar los módulos listos

1. Levantar la aplicación y SQL Server con datos de prueba; recorrer alta, lectura, edición, cancelación, disponibilidad, horarios/ausencias, recordatorios y exportaciones.
2. Repetir pruebas de acceso con Administrador, Recepcionista, Profesional, Auxiliar y Paciente; intentar IDs ajenos y solicitudes directas.
3. Probar colisiones simultáneas, límites de horario, fechas inválidas/pasadas y cambios de profesional/fecha contra SQL Server.
4. Verificar claves, relaciones, restricciones, índices, duplicados/huérfanos y persistencia después de reiniciar el contenedor/base.
5. Medir latencia y volumen alto de citas, profesionales y credenciales; observar consola del navegador y logs del servidor.
6. Confirmar si importación, exportación de listados y ordenamiento interactivo son criterios obligatorios del alcance; actualmente no están expuestos de forma general.

## Conclusión

Las vistas y sus conexiones principales fueron revisadas estáticamente, se corrigieron defectos identificados y la regresión automatizada está verde. **No se confirma que absolutamente todo esté funcional ni se aprueba para producción** mientras sigan pendientes las pruebas autenticadas, de base activa, concurrencia, rendimiento y reinicio.
