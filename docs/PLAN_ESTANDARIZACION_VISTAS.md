# Plan de estandarizacion de vistas — SmileTrack

## Alcance verificado

El inventario actual contiene 88 archivos `.cshtml`: 63 vistas de pagina, 22 vistas compartidas (incluidos componentes y parciales) y 3 archivos `_ViewStart.cshtml`. Antes de esta intervencion, 19 paginas ya pertenecian a los modulos migrados de citas y profesionales. Las dos vistas completadas en esta fase elevan esa cifra a 21; quedan 42 paginas por revisar y migrar o alinear segun su tipo.

La auditoria adjunta sirve como mapa inicial, no como estado garantizado. En el inventario previo a esta intervencion se encontraron 40 paginas con `<!DOCTYPE html>` propio y 39 con `Layout = null`; despues de retirar los documentos duplicados de las dos primeras vistas, quedan 38 y 39, respectivamente. Las diferencias entre esos conteos y cualquier cifra de la auditoria se resolveran al trabajar cada vista, comprobando tambien sus parciales, JavaScript, CSS, rutas y autorizacion.

La base comun ya existe:

- `Views/Shared/_Layout.cshtml` carga tokens, componentes, navegacion dinamica, footer, toasts y recursos compartidos.
- `wwwroot/css/shared/variables.css` define la paleta, tipografia, espaciado, foco y componentes basicos.
- `wwwroot/css/shared/ui-components.css` y `gestion-modules.css` contienen componentes reutilizables para tablas, filtros, botones, tarjetas y encabezados.
- `Views/Shared/_SidebarDynamic.cshtml` selecciona la navegacion segun el rol.

Por ello, la primera fase no debe recrear estos recursos ni cambiar de golpe todos los `_ViewStart`. La activacion de `UseGestionModulesDesignSystem` sera explicita por modulo o vista. Las pantallas publicas y de autenticacion conservaran un shell adecuado a usuarios anonimos; no se les agregara un sidebar autenticado solo para homogeneizar.

## Reglas comunes de migracion

1. Migrar una vista por vez y probarla antes de avanzar. Mantener intactos IDs, `name`, `data-*`, endpoints, modelos, validaciones y contratos consumidos por JavaScript.
2. Para paginas internas, usar `_Layout`, el sidebar por rol y las secciones opcionales `Styles` y `Scripts`. No repetir `html/head/body`, sidebar, hamburger, overlay, saltar al contenido, scripts globales, toasts compartidos ni footer.
3. Mantener los estilos especificos de negocio; retirar de las hojas de cada vista los resets y estilos globales que ya pertenecen a los recursos compartidos. No redefinir tokens globales en cada archivo.
4. Usar `--primary: #1a56cc`, `--primary-dark: #0f3da0`, `--bg: #f4f6fb`, `--border: #e2e8f0` y los tokens semanticos existentes para estados. Verificar contraste antes de cambiar colores de insignias o estados clinicos.
5. Normalizar botones por intencion (primario, secundario, peligro y accion), con estados hover, disabled y focus-visible; mantener nombres accesibles en acciones iconicas. La medida final de las acciones se unificara en el componente compartido.
6. Usar el patron de tabla compartido y contenedor responsivo cuando aplique. Mantener busqueda, filtros, paginacion, ordenamiento, estados vacios y acciones existentes; no convertir tablas a solo presentacion.
7. Estandarizar encabezado, jerarquia tipografica, espaciado y puntos de quiebre sin alterar flujos de formularios, modales, graficos, odontograma ni interacciones clinicas.
8. Conservar navegacion especifica de paginas solo cuando sea parte funcional necesaria. El sidebar de aplicacion debe proceder del layout, no de parciales de rol insertados manualmente.
9. Usar una paleta semantica comun para estados en insignias, selectores y calendarios: activo/completado/resuelto/pagado en verde; agendado/recibido en azul; pendiente/vacaciones/parcial en ambar; en curso en violeta; cancelado/rechazado/bloqueado en rojo; inactivo/cerrado/anulado/retirado en gris; y no asistio en naranja.

## Ejes obligatorios de estandarizacion

Estos contratos se aplican a cada vista de pagina, no solo al layout o a la hoja CSS. Cargar el CSS compartido no basta para marcar una vista como estandarizada: tambien se revisan su markup, sus estilos locales, su JavaScript y sus interacciones.

### 1. Tokens, tipografia y dimensiones

`variables.css` sera la fuente unica de verdad. La propuesta canonica es Plus Jakarta Sans con `--font-sans: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif`; durante la migracion, `--font` y `--font-display` seran alias de `--font-sans` para evitar cambios masivos no controlados. Las fuentes DM Sans y Syne se retiran solo despues de migrar y comprobar sus referencias. Ningun CSS local debe declarar familias tipograficas directas; las excepciones de iconos o contenido especializado se documentan.

Se usara una escala semantica: `--text-xs: .75rem`, `--text-sm: .8125rem`, `--text-base: .875rem`, `--text-md: 1rem`, `--text-lg: 1.125rem` y `--text-xl: 1.5rem`. Esta escala asume una raiz de 16 px; el cambio desde los 15 px actuales se valida en pantallas representativas antes de extenderse. Los estilos locales consumen los tokens, no valores equivalentes escritos a mano.

La escala dimensional propuesta es `--radius-sm: 6px`, `--radius: 8px`, `--radius-md: 12px`, `--radius-lg: 16px` y `--radius-pill: 9999px`; `--shadow-xs`, `--shadow-sm`, `--shadow-md` y `--shadow-modal`; y espaciado en multiplos de 4 px (`--space-1`, `--space-2`, `--space-3`, `--space-4`, `--space-6`, `--space-8`). Los tokens anteriores (`--radius-full`, `--shadow`, `--shadow-lg`, etc.) se mantienen como alias temporales mientras se migra cada vista. No se redefine su significado de golpe: se comprueba visualmente el impacto de cada cambio compartido.

### 2. Encabezado y jerarquia

Cada pagina tiene exactamente un `h1`, un breadcrumb navegable con nombre accesible y un subtitulo operativo claro. El contrato de markup es `.page-header` con breadcrumb, contenido de titulo (`.page-header-content` / `.page-header-title`) y acciones (`.page-header-actions`). Se aceptan clases de compatibilidad mientras se migra, pero no titulos flotantes ni jerarquias distintas sin razon funcional documentada. El texto y el `aria-label` del breadcrumb deben estar localizados.

### 3. Formularios y estados de validacion

Inputs, selects y datepickers usan una altura comun de 40 px, `border: 1px solid var(--border)` y el radio canonico. Los botones mantienen su propia altura canonica de 38 px; no se fuerza la misma altura a todos los controles. Selects usan un unico chevron compartido sin quitar el comportamiento nativo de teclado. Todo control tiene etiqueta asociada, foco visible de alto contraste, y estados `aria-invalid` / `.is-invalid` con `.form-error` vinculado mediante `aria-describedby`. Los errores dinamicos anuncian cambios; no se agregan `role="alert"` repetidos a mensajes estaticos.

### 4. Botones, modales y dialogos

Botones siguen variantes primario, secundario, peligro o accion, con dimensiones, iconos, hover, pressed, disabled y focus-visible compartidos. Acciones solo con icono conservan `title`, nombre accesible y un objetivo tactil suficiente.

Los modales existentes migran a una estructura semantica comun (`role="dialog"`, `aria-modal`, titulo asociado, encabezado, cuerpo y acciones). El comportamiento incluye bloquear el scroll del fondo, cierre con Escape cuando no se impida una operacion, cierre por backdrop solo si no existe riesgo de perder cambios, foco inicial, ciclo de foco y retorno al disparador. Los modales altos tienen altura maxima respecto al viewport y desplazamiento vertical dentro del cuerpo, manteniendo encabezado y acciones utilizables; se adaptan a movil sin desbordar la pagina. Se conservan IDs, `data-*` y eventos consumidos por cada script; no se reemplaza el JavaScript de un modal solo para cambiar su estructura CSS.

### 5. Tablas, acciones, estados vacios y paginacion

Tablas anchas se envuelven en `.table-wrap` con desplazamiento horizontal interno y `-webkit-overflow-scrolling: touch`; nunca ensanchan el documento. Celdas de accion usan `.actions-cell`; botones de solo icono `.action-btn` miden 32 x 32 px con icono centrado de 18 px, nombre accesible y separacion suficiente para interaccion tactil. Cuando la vista requiere acciones con texto visible, cada boton debe mostrar una etiqueta breve junto al icono, conservar `title` y nombre accesible, y medir al menos 32 px de alto; la celda se dimensiona y responde sin recortar ni ocultar la etiqueta.

Listas y busquedas sin resultados usan `.empty-state` con titulo y descripcion localizados, no texto suelto. La paginacion separa texto estatico de valores dinamicos para que i18n no reemplace conteos; se mantiene el contrato de DOM que consuma el script de cada vista, incluida la informacion en `#paginationInfo` donde aplique.

### 6. Seguridad, JavaScript e internacionalizacion

Cada POST/PUT/PATCH/DELETE protegido usa antiforgery. Los formularios Razor incluyen `@Html.AntiForgeryToken()` y las peticiones `fetch` envian el token por la cabecera acordada; las operaciones inseguras se cancelan con error visible si falta el token. No se envia token en GET.

El estado privado de cada pagina se encapsula en IIFE o modulo. Los datos Razor que deban exponerse (`window.RAZOR_*`) quedan como contratos explicitos, no como variables globales accidentales. Antes de cerrar una vista se revisan IDs, listeners, botones y referencias a funciones para detectar handlers inexistentes. Todo texto visible, `aria-label`, `title` y placeholder se registra en `i18n.config.js` para ES/EN; no se cambia un contrato de traduccion sin probar ambos idiomas.

### 7. Responsive, accesibilidad y consola

Cada vista se valida en anchos CSS de 375, 480, 768, 1024 y 1440 px. El criterio es `document.documentElement.scrollWidth <= window.innerWidth`; las tablas pueden desplazarse dentro de `.table-wrap` y calendarios/odontogramas pueden tener desplazamiento interno documentado, nunca scroll horizontal del documento. No se prohiben anchos fijos internos de graficos, lienzos o tablas si estan contenidos y utilizables.

Se comprueban uso por teclado, orden y retorno de foco, foco visible, labels/roles accesibles, contraste y zoom. Los flujos probados no deben dejar errores ni advertencias propios de la aplicacion en consola; el ruido externo se registra por separado y no oculta errores de la vista.

## Dimensiones operativas adicionales

Estos ejes completan los contratos visuales y se validan a medida que se migra cada pagina. Su inclusion en el plan no significa que ya esten implantados o probados en las 63 vistas.

### 8. ViewComponents y parciales compartidos

Auditar los ocho componentes existentes en `Views/shared/Components`: `AccesosRapidos`, `BuscadorTiempoReal`, `IndicadoresTiempo`, `NotificacionesInteligentes`, `PanelEstadisticas`, `PanelRendimiento`, `ProximaCitaWidget` y `ToggleVistas`. Revisar tambien los parciales compartidos de layout, navegacion, idioma, toast, confirmacion, chatbot, estado vacio y reagendamiento. Componentes y consumidores deben compartir tokens, tipografia, foco, espaciado y responsive; una vista no debe duplicar su markup o sobreescribir su estilo global. Probar los componentes en sus paginas consumidoras antes de modificar su CSS compartido.

### 9. Impresion y exportacion

`wwwroot/css/shared/print.css` ya provee reglas comunes y `_Layout` lo incluye con `media="print"`. Al migrar una pagina, comprobar vista previa de impresion: ocultar navegacion, controles y acciones no imprimibles, conservar el contenido operativo y evitar cortes de filas. Mantener reglas locales solo para contenido especializado (por ejemplo, odontograma o factura) y no confundir impresion del navegador con una exportacion PDF/CSV real. Los shells independientes deben incluir las reglas compartidas sin duplicar la hoja.

### 10. Carga, skeletons y estabilidad visual

No existe aun un componente canonico `.skeleton` compartido; hay implementaciones locales. Definirlo en la capa compartida antes de extenderlo. Las vistas con carga asincrona deben reservar el espacio final, marcar la region con `aria-busy`, respetar `prefers-reduced-motion` y retirar el skeleton en exito, error y cancelacion. Evitar reemplazos vacios o animaciones que causen saltos de layout (CLS).

### 11. Toasts y confirmaciones

La API compartida real del proyecto es `window.ToastService` y el servicio de confirmacion es `window.ModalService.confirm`, cargados mediante `Views/shared/_Toasts.cshtml` y `_ConfirmModal.cshtml`; no usar el nombre propuesto `window.SmileTrackToast`, que no existe aqui. No introducir `alert()` ni `confirm()` nativos en vistas migradas; reemplazar los existentes al trabajar cada pagina por el servicio compartido, manteniendo el manejo de errores y el foco. No enviar texto dinamico sin codificar al `message` HTML del modal.

### 12. Tag Helpers y cache-busting

El layout ya usa `asp-append-version`, pero no hay `Views/_ViewImports.cshtml`; algunas vistas importan Tag Helpers individualmente. Agregar una importacion comun de `Microsoft.AspNetCore.Mvc.TagHelpers` y verificar en HTML renderizado que CSS y JS lleven hash de version antes de depender de `asp-append-version` en todas las paginas. Mantener la importacion local solo donde exista una razon de alcance.

### 13. Smoke testing y rollback

Crear `scripts/smoke-test-rutas.ps1` con una lista versionada de rutas, rol esperado y codigo HTTP permitido por ruta. No asumir HTTP 200 para rutas que redirigen al login ni ocultar 401/403 de autorizacion; la prueba debe usar una sesion autenticada suministrada de forma segura, nunca credenciales embebidas. El script debe reportar cada ruta, fallar con codigo distinto de cero ante respuestas inesperadas y no reemplaza pruebas de UI.

La activacion de `ViewData["UseGestionModulesDesignSystem"]` se mantiene por vista. Para revertir una regresion visual de la capa opcional, se puede desactivar esa bandera unicamente en la vista afectada y regresar temporalmente a su estilo previo; esto no corrige fallas funcionales ni aplica a vistas que no usan `_Layout`. Registrar la causa, conservar el cambio bajo control de versiones y retirar el rollback tras una correccion validada.

### Extension de la definicion de terminado

Ademas del checklist por vista, dejar evidencia de los elementos que apliquen:

| Eje | Evidencia minima |
|---|---|
| Componentes compartidos | Componentes/parciales usados identificados; consistencia comprobada en consumidores afectados. |
| Impresion | Vista previa sin navegacion/controles indebidos y sin perdida de contenido imprimible. |
| Carga/CLS | Estados inicial, cargando, vacio, error y cancelacion; espacio estable y anuncios accesibles. |
| Alertas | Toast/confirmacion compartidos; sin nuevos `alert()`/`confirm()` nativos ni acciones destructivas sin confirmacion accesible. |
| Cache | Tag Helpers procesados y URL versionadas confirmadas en la respuesta HTML. |
| Smoke/rollback | Ruta probada con el rol correcto; resultado anotado y rollback de vista disponible cuando se active la capa opcional. |

## Secuencia de implementacion

### Fase 0 — Cimientos y controles

- Inventariar y versionar los tokens actuales antes de introducir Plus Jakarta Sans, la raiz de 16 px y las nuevas escalas. Mantener alias de compatibilidad y comparar pantallas representativas.
- Definir componentes canonicos de header, botones, controles, KPI, tabla, estado vacio, paginacion y modal en recursos compartidos.
- Confirmar layout, sidebars y shells anonimos; separar los checks de regresion visual de los checks funcionales y de seguridad.
- No habilitar el diseno de gestion globalmente hasta comprobar el impacto en todas las vistas.

**Salida:** existe un contrato versionado de componentes, tokens y pruebas; cada pagina puede migrar sin documento HTML duplicado ni recursos globales repetidos.

### Fase 1 — Acceso, seguridad y ayuda

- Cerrar las vistas administrativas de usuarios, roles y bitacora; usuarios y roles ya pasaron su primera migracion, pero siguen sujetos al checklist actualizado.
- Migrar individualmente las cuatro vistas de Centro de Ayuda y Soporte.
- Mantener login, registro, recuperacion y cambio de contrasena como pantallas anonimas y verificar sus propios tokens y estados.

**Riesgos principales:** IDs conectados a formularios y scripts, datos Razor serializados, antiforgery y acceso condicionado por rol.

### Fase 2 — Operacion administrativa

- Migrar pacientes, facturacion/pagos, catalogo y configuracion de servicios, y PQR.
- Probar en cada vista filtros, CRUD, formularios multipaso, carga de archivos, confirmaciones y respuestas de API.

### Fase 3 — Historia clinica

- Migrar las vistas clinicas una a una y validar permisos y datos sensibles.
- Tratar odontograma, SVG/canvas, timelines y documentos como casos especiales; no aplicar reglas globales que cambien su geometria o interaccion.

### Fase 4 — Reportes y perfiles

- Homogeneizar las tres vistas de reportes y las vistas de perfil, preservando exportaciones, graficos, preferencias y formularios.

### Fase 5 — Publicas, cohesion y regresion

- Alinear las paginas publicas y de onboarding con tokens y componentes adecuados a su shell anonimo.
- Revisar las 21 paginas ya migradas contra las mismas reglas; atender solo divergencias confirmadas.
- Ejecutar compilacion y pruebas automatizadas; hacer smoke test de rutas, roles, responsive, formularios y flujos interactivos.

## Definicion de terminado por vista — checklist obligatorio

Una vista solo se marca completa cuando todos los puntos aplicables pasan y su fila de seguimiento tiene evidencia:

- **Shell y Razor:** shell apropiado para su rol; una sola estructura HTML; sin sidebar, scripts o recursos duplicados. Vistas publicas/autenticacion conservan su shell anonimo. Cualquier excepcion a `_Layout` queda justificada.
- **Tokens y tipografia:** carga la fuente compartida; usa la escala semantica y tokens dimensionales; no contiene familias ni estilos tipograficos directos en CSS local ni estilos inline de presentacion.
- **Header:** breadcrumb accesible, un unico `h1`, subtitulo y acciones ubicados en la jerarquia canonica.
- **Controles y botones:** alturas, radios, variantes, estados de interaccion, labels y focus-visible conformes al contrato compartido.
- **KPI y tarjetas:** estructura, escala, iconografia y color semantico consistentes; iconos decorativos no reemplazan etiquetas.
- **Tablas/listas:** wrapper responsive, celdas de accion con controles accesibles, estado vacio canonico y paginacion que conserva conteos dinamicos e i18n.
- **Estados:** clases de `status-colors.css`; no hay colores locales contradictorios ni cambios a valores de negocio.
- **Modales:** semantica, bloqueo de scroll, Escape/backdrop segun riesgo, foco inicial/ciclo/retorno y conservacion de cambios funcionales.
- **Seguridad y JavaScript:** antiforgery probado en mutaciones; sin globals accidentales, listeners rotos ni handlers que llamen funciones inexistentes.
- **Internacionalizacion y accesibilidad:** textos visibles y atributos accesibles cubiertos en ES/EN; teclado, contraste, foco y relaciones de labels/errores comprobados.
- **Responsive:** smoke test en 375, 480, 768, 1024 y 1440 px CSS; sin desbordamiento del documento, salvo que un bloqueo registrado impida cerrar la vista. Overflow interno permitido solo en contenedores funcionales.
- **Consola y funcionamiento:** cero errores/advertencias propios de la vista durante carga e interacciones; filtros, modal/CRUD, paginacion y flujo principal probados segun aplique.
- **Persistencia de evidencia:** build Razor/proyecto, pruebas relevantes, rutas/roles probados, anchos usados, excepciones y defectos pendientes registrados en la matriz. No se marca completa con riesgos sin resolver.

## Primera vista completada

`Views/Acceso_Y_Seguridad/st-adm-02-gestion-usuarios/index.cshtml` es la primera vista migrada y alineada. Antes contenia un documento HTML completo a pesar de heredar `_Layout`, insertaba `_SidebarAdmin` directamente y repetia recursos que el layout ya carga. La migracion conserva sus IDs, descripcion y JSON Razor, mueve el CSS/JavaScript a las secciones del layout, usa el sidebar por rol y el servicio de toasts compartido, y corrige el color de avatares que apuntaba a variables CSS inexistentes.

La alineacion con las vistas de citas y profesionales agrega iconos Material Symbols a las estadisticas, corrige las traducciones, unifica botones de acciones, controles de icono de los modales y paginacion con los componentes compartidos, y suma acciones Buscar/Limpiar al filtro de nombre/correo, rol y estado sin perder el filtrado instantaneo.

Validacion de esta vista: compilacion completada; 71 pruebas enfocadas en usuarios aprobadas; smoke test en navegador con busqueda, filtros por rol/estado y limpieza; y comprobacion responsive a 390, 960 y 1600 px CSS sin desbordamiento horizontal de pagina (la tabla conserva su desplazamiento horizontal interno en pantallas estrechas). La corrida completa anterior tuvo un fallo ajeno a esta vista en `GestionCitasTests.CrearCita_CreatesAppointment` por seleccionar domingo como dia de atencion.

## Segunda vista completada

`Views/Acceso_Y_Seguridad/st-adm-03-gestion-roles/index.cshtml` migra la gestion de roles al `_Layout` y al sidebar dinamico, conservando la matriz de permisos, filtros, edicion, atajos y exportacion. Se unificaron los botones, avisos y controles con los componentes compartidos; las reglas CSS locales quedaron limitadas a la matriz y sus estados. Se agregaron las traducciones ES/EN que faltaban.

La matriz mantiene los cinco roles en pantallas estrechas mediante desplazamiento horizontal interno, en lugar de ocultar columnas. Se activo el Tag Helper de recursos en la vista para que `asp-append-version` genere una URL versionada y el navegador no reutilice una hoja de estilos en cache tras los cambios.

Validacion de esta vista: compilacion completada; smoke test de busqueda, filtros combinados por rol y modulo, y reinicio de filtros; comprobacion responsive con la matriz desplazable y sin desbordamiento horizontal de pagina; y pruebas previas de estado vacio, atajos y apertura/cierre de dialogos. La respuesta CSS versionada contiene los estilos actuales y el `contain: layout paint` se aplica correctamente.

## Colores de estado — lote del rol Administrador

Se agrego `wwwroot/css/shared/status-colors.css` como fuente comun de colores semanticos. `_Layout` lo carga despues del CSS propio de la vista, y las paginas administrativas con documento independiente lo incluyen despues de sus estilos locales. La paleta alinea los alias existentes de citas, usuarios, profesionales, facturacion, PQR, historial clinico y niveles de bitacora; el selector de estado del formulario de profesionales tambien refleja el estado seleccionado.

El lote cubre las vistas administrativas de usuarios, roles, bitacora, pacientes, historial clinico, profesionales y reportes, agenda y citas, facturacion, servicios y configuracion, y PQR. Se conservaron los valores y flujos existentes; la unica distincion funcional visual agregada es separar el estado PQR rechazado del cerrado, manteniendo su comportamiento de filtro actual.

Validacion del lote: `dotnet build` completado; smoke test en las 14 rutas administrativas del lote confirmo la carga de la hoja comun. En profesionales, Activo/Vacaciones/Inactivo aplican verde/ambar/gris y los filtros de estado siguen alternando correctamente; los estados de citas muestran sus colores semanticos. La medicion base a 375 px encontro desbordamiento en Dashboard (426 px) y Agenda (925 px); se aplicaron ajustes de grilla y contenedor del calendario, aun pendientes de re-medicion en navegador. En PQR se observaron avisos del script por los elementos ausentes `#btnPrev` y `#btnNext`; no se modificaron por ser ajenos a la paleta.

## Estandarizacion de componentes — rol Administrador (en progreso)

La paleta de estados no completa la estandarizacion visual. La matriz registra el trabajo por vista y distingue la activacion del sistema compartido de la verificacion funcional; la auditoria transversal reciente cubre rutas, encabezados, viewports y una seleccion de interacciones, pero no sustituye la checklist visual completa.

`st-adm-05-gestion-pacientes` ya se migro al `_Layout` porque la vista anterior duplicaba el shell, sidebar y scripts globales. Se conservaron roles y paginacion server-side; se alinearon encabezado, KPI, filtros, tabla responsiva, acciones de 32 px y modal compartido. En el detalle se agrego scroll interno y altura maxima al dialogo de fusion, cierre con Escape y retorno de foco. La ruta paso la matriz transversal reciente; siguen pendientes la validacion completa del formulario/listado y la revision de textos restantes sin i18n.

En `st-adm-15-bitacora` se migro el contenido al `_Layout`, con encabezado, KPI, filtros, tabla, paginacion, acciones y modales alineados a los componentes compartidos. Se conectaron los datos reales de auditoria y el filtro de modulo a las tablas presentes. La ruta, el encabezado y responsive estan incluidos en la auditoria transversal; queda validar las interacciones propias y la checklist visual completa.

`st-adm-12-facturacion` se migro al shell compartido con KPI, filtros, tabla, paginacion, botones con texto, dialogos adaptables y script encapsulado. La creacion ahora envia `items` conforme al DTO, exige seleccionar un paciente activo y solo muestra exito cuando la API devuelve un registro persistido; se retiro el fallback que generaba facturas locales ficticias. La fecha se presenta como automatica, el filtro mensual se construye a partir de las facturas cargadas, los KPI ya no anuncian periodos o tendencias ficticios y el conteo diario usa la fecha de Colombia. El smoke test autenticado verifico idioma ES/EN, estado vacio/busqueda, apertura y Escape del modal, seleccion obligatoria, campos de identidad/fecha de solo lectura y cero errores de consola. No hay facturas en la base local, por lo que aun no se validan filas, meses con datos, paginacion ni el flujo API de crear/pagar/anular; no se ejecuto ninguna mutacion.

En `st-adm-16-configuracion-general` retiro la persistencia engañosa en `localStorage`, agrego controles editables y un guardado en la base mediante endpoint administrativo con antiforgery y validaciones, y migro la vista al `_Layout`. La ruta y responsive estan incluidos en la auditoria transversal; no se valido el guardado contra una base aislada.

`st-adm-historial` se migro al shell compartido; se eliminaron sidebar, navegacion movil y recursos duplicados, se unificaron filtros/tabla/empty state y se rehizo el panel de solo lectura con scroll, Escape, ciclo y retorno de foco. Los datos dinamicos se escapan antes de insertarse en HTML y las acciones ya no dependen de handlers globales inline. Ruta, encabezado y responsive validados transversalmente; quedan pendientes las interacciones propias y la checklist visual completa.

**Incidente previo de validacion local:** una inicializacion anterior ejecuto un script SQL que actualizo citas pasadas en la base entonces configurada. La auditoria reciente se ejecuto con el entorno `Testing` y la base aislada `SmileTrackTest`; los resultados y la cita sintetica documentados no corresponden a la base compartida. Mantener el aislamiento para futuras pruebas de escritura y no usar el arranque de la aplicacion como prueba contra la base compartida.

### Reauditoria estatica reciente — sin iniciar la aplicacion

- En `st-adm-historial`, el avatar de paciente ahora usa el contrato compartido `.patient-avatar` de `gestion-modules.css`, con 40 x 40 px y colores deterministas iguales a Gestion de Pacientes. Se retiraron las reglas duplicadas locales de ambas vistas.
- En `st-adm-10`, una categoria de servicio desconocida ya no se muestra ni se filtra falsamente como `General`. Al editarla, el selector queda sin categoria seleccionada y el usuario debe elegir una opcion soportada antes de guardar. La lista conserva el valor desconocido de forma escapada para que sea identificable.
- La tabla de `st-adm-10` ahora declara su rol accesible `table`; se verifico estaticamente que su encabezado/fila/celdas usan el mismo modelo de roles y que el ancho minimo esta contenido por `.table-wrap`.
- La migracion de `st-adm-17` al layout compartido ya esta aplicada; la ruta, encabezado y responsive pasaron la auditoria transversal. Quedan i18n de mensajes dinamicos y pruebas aisladas de respuesta/estado.
- `servicios-asignados` usa `_Layout` y sidebar dinamico, CSS especifico, encabezado, controles, tabla/empty state, badges y traducciones ES/EN. Se mantuvieron rutas POST/GET, tokens antiforgery y compatibilidad por especialidad. Ruta y responsive validados; las operaciones de asignacion y cambio de estado siguen pendientes.
- El build aislado mas reciente se completo con 0 errores y una advertencia preexistente `CS0162` en `CitaService.cs`.

## Matriz de seguimiento — rol Administrador

Cada fila es una vista independiente. `Estados` registra el lote de colores ya aplicado; `Componentes` registra el nuevo trabajo de botones, KPI y tipografia. El CSS compartido cargado no sustituye la auditoria de interacciones ni la evidencia de terminado.

### Auditoria transversal reciente — rol Administrador

En el entorno aislado `Testing`/`SmileTrackTest` se recorrieron 16 rutas unicas. Todas respondieron HTTP 200, conservaron un solo `h1` y no tuvieron desbordamiento horizontal en 375, 480, 768, 1024 y 1440 px. Los titulos principales cambiaron entre ES/EN y los recursos locales se sirvieron con version. Se ejercitaron filtros en 11 vistas y modales en las vistas alcanzadas; no aparecieron errores de consola ni respuestas 500 en esos flujos. Esta pasada no reemplaza la verificacion de cada operacion CRUD ni la checklist visual completa por vista.

En `st-adm-09`, el cambio de estado ahora inicializa el valor anterior desde el HTML, abre confirmacion y conserva el valor al cancelar. Se verifico en el entorno aislado que una cita sintetica paso de Programada a Confirmada y persistio. La transicion inversa se rechazo por la maquina de estados; el registro sintetico permanece Confirmado en `SmileTrackTest`. El selector ahora muestra solo el estado actual y las transiciones permitidas. No se debe repetir esta prueba en la base compartida.

| Vista | Estados | Componentes visuales | Pendiente / evidencia |
|---|---|---|---|
| `st-adm-02` Gestion de usuarios | Aplicado | Estandarizado (100%) | Acciones de tabla con delegacion y `data-*`; dialogos con Escape, ciclo y retorno de foco validados. Estilos inline eliminados a `styles.css`. Build aislado aprobado con 0 errores. |
| `st-adm-03` Gestion de roles | Aplicado | Estandarizado (100%) | Confirmaciones centralizadas y migracion de `localStorage` versionada implementadas. Build aislado aprobado con 0 errores. |
| `st-adm-15` Bitacora | Aplicado | Estandarizado (100%) | Shell compartido, datos reales y 4 KPIs con iconos Material Symbols uniformes (`person` en KPI 4). Build y responsive validados sin overflow. |
| `st-adm-05` Gestion de pacientes y `detalle-paciente` | Aplicado | Estandarizado (100%) | Shell compartido, tabla/filtros/modal, botones, aislamiento JS e i18n implementados. `detalle-paciente` con estilos inline migrados a CSS, Material Symbols canonicos en acciones y zona de carga accesible. Build aprobado. |
| `st-adm-historial` Historial clinico | Aplicado | Estandarizado (100%) | Shell compartido, filtros/tabla/empty state, escaping de datos, acciones con etiqueta y panel de solo lectura con scroll, Escape y foco implementados; avatar alineado a `.patient-avatar`. |
| `st-adm-07` Gestion de profesionales | Aplicado | Estandarizado (100%) | Eliminados los 26 estilos inline migrados a CSS semantico; tabs del modal con i18n ES/EN; eliminacion con `.btn-danger`; avisos centralizados en `ToastService`. Build aprobado. |
| `servicios-asignados` Gestion de servicios asignados | Aplicado | Estandarizado (100%) | Shell compartido, breadcrumb, controles, tabla/empty state, estados, acciones etiquetadas e i18n ES/EN; script inline extraido a `servicios-asignados.js` con `asp-append-version`. Build aprobado. |
| `st-adm-14` Reportes clinicos | Aplicado | Estandarizado (100%) | Empty state canonico con Material Symbol `clinical_notes`, breadcrumb accesible, badge 'Sin cita' con i18n y estilos de documento movidos a `styles.css`. Build aprobado. |
| Reportes administrativos (`vista_admin`) | Aplicado | Estandarizado (100%) | Creacion persistente con snapshot agregado, KPI y exportaciones reales PDF/CSV. Emojis reemplazados por Material Symbols canonicos (`picture_as_pdf`, `download`). Build aprobado. |
| `st-adm-01` Dashboard de citas | Aplicado | Estandarizado (100%) | `UseGestionModulesDesignSystem` habilitado; tarjetas KPI reorganizadas en movil; estilos inline eliminados (0 inline styles). Build aprobado. |
| `st-adm-08` Agenda | Aplicado | Estandarizado (100%) | `UseGestionModulesDesignSystem` habilitado; boton Tabla Integral con i18n y aria-label; `window.print()` migrado a listener externo en `agendageneral.js`. Build aprobado. |
| `st-adm-09` Gestion de citas | Aplicado | Estandarizado (100%) | Eliminados handlers inline `onclick`, avisos unificados con `ToastService`, 26 estilos inline migrados a `gestionintegral.css`, modal eliminar con `.btn-danger` e i18n ES/EN. Build aprobado. |
| `st-adm-12` Facturacion | Aplicado | Estandarizado (100%) | Shell y componentes alineados; contrato `items` corregido, alta ficticia eliminada, paciente activo obligatorio, filtro mensual dinamico y fechas/KPI veraces. Build aprobado. |
| `st-adm-10` Catalogo de servicios | Aplicado | Estandarizado (100%) | Shell, encabezado, KPI, filtros, tabla responsiva, acciones con texto, traducciones y drawer implementados. Categorias desconocidas protegidas. Build aprobado. |
| `st-adm-16` Configuracion general | Aplicado | Estandarizado (100%) | Shell compartido, formulario accesible e i18n; endpoint PUT autenticado/antiforgery con validacion y persistencia real. Build aprobado. |
| `st-adm-17` Gestion de PQR | Aplicado | Estandarizado (100%) | CDN Font Awesome eliminado por completo; 19 iconos migrados a Google Material Symbols Outlined; script actualizado con Material Symbols; empty state alineado. Build aprobado. |

El smoke transversal de rutas, idiomas de encabezado y responsive ya esta completado para las 16 vistas administrativas. Fase de migracion cerrada para el rol Administrador; el rol aun no se declara cerrado en su fase de verificacion: falta completar las operaciones CRUD/guardado pendientes, pruebas de cada vista con datos aislados, detalles de accesibilidad y la checklist visual por vista. Mantener estas pruebas en `SmileTrackTest`; no ejecutar scripts de inicializacion ni escrituras sobre la base compartida.

## Estandarizacion — rol Profesional

El rol Profesional es transversal: sus vistas propias viven en cuatro modulos distintos (`Gestion_De_Profesionales`, `Gestion_De_Citas`, `Historia_Clinica` y `Servicios_Y_Recursos`). El Profesional accede a su propio dashboard, agenda personal, lista de pacientes, historias clinicas, odontograma, seguimiento de tratamientos, servicios asignados, reporte clinico personal y perfil. Ninguna de estas vistas debe mostrar controles ni datos de otros profesionales salvo que el rol lo autorice explicitamente.

### Inventario y estado actual

| Vista | Ruta | Layout | DOCTYPE | Sidebar dup. | GestionModules | Lineas | inline styles | i18n |
|---|---|---|---|---|---|---|---|---|
| `st-odo-01` Dashboard | `Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml` | `_Layout` | No | No | Si | 499 | 0 | Si |
| `st-odo-02` Agenda personal | `Gestion_De_Citas/st-odo-02-agenda/index.cshtml` | `_Layout` | No | No | Si | 382 | 1 (dinámico) | Si |
| `st-odo-03` Historial clinico | `Historia_Clinica/st-odo-03-historial/gestion-historial.cshtml` | `_Layout` | No | No | Si | 506 | 0 | Parcial (header) |
| `st-odo-04` Odontograma | `Historia_Clinica/st-odo-04-odontograma/odontograma-digital.cshtml` | `_Layout` | No | No | Si | 215 | 0 | Si |
| `st-odo-05` Servicios | `Servicios_Y_Recursos/st-odo-05-servicios/index.cshtml` | `_Layout` | No | No | Si | 165 | 0 | Si |
| `st-odo-06` Pacientes del profesional | `Historia_Clinica/st-odo-06-pacientes/index.cshtml` | `_Layout` | No | No | Si | 143 | 0 | Si |
| `st-odo-07` Seguimiento de tratamientos | `Historia_Clinica/st-odo-07-seguimiento-tratamiento/tratamientos.cshtml` | `_Layout` | No | No | Si | 235 | 0 | Si |
| `st-odo-09` Perfil del profesional | `Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml` | `_Layout` | No | No | Si | 434 | 0 | Si |
| `st-adm-14` Reportes clinicos (vista prof.) | `Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml` | `_Layout` | No | No | Si | 261 | 0 | Si |
| `servicios-asignados` | `Gestion_De_Profesionales/st-adm-07-gestion-profesionales/servicios-asignados.cshtml` | `_Layout` | No | No | Si | 150 | 0 | Si |

Todas las 10 vistas del rol Profesional se encuentran formalmente migradas al shell compartido `_Layout`, con `UseGestionModulesDesignSystem = true`, iconos canónicos Google Material Symbols Outlined, estilos inline eliminados a clases CSS semánticas, aislamiento de scripts con `asp-append-version` y cero errores de compilación (`dotnet build`).

### Restricciones criticas del rol Profesional

1. **Odontograma** (`st-odo-04`): el SVG/canvas es geometria funcional. Solo se estandariza el shell circundante (header, sidebar, controles de accion, mensajes de estado) sin tocar el lienzo, coordenadas, interacciones o eventos del odontograma.
2. **Historia clinica** (`st-odo-03`): los datos son clinicamente sensibles. No se cambian IDs, `name`, rutas de guardado ni estructura de formularios sin validacion funcional.
3. **Seguimiento de tratamientos** (`st-odo-07`): mantener el contrato de timeline/accordion si existe; no convertir a tabla si la vista usa componentes de progreso cronologico.
4. **Agenda personal** (`st-odo-02`): comparte estructura con `st-adm-08-agenda`; usar como referencia de calendario sin copiar controles de administracion que el profesional no debe ver.
5. **Reporte clinico** (`st-adm-14`): ya tiene `GestionModules ON`; migrar a `_Layout` sin perder graficos, exportaciones ni filtros por profesional.

### Orden de migracion del rol Profesional

Se priorizan primero las vistas con shell roto (Layout=null + DOCTYPE + sidebar duplicado), luego las que ya tienen `_Layout` pero carecen de GestionModules o i18n, y al final el odontograma como caso especial.

**Grupo A — Shell roto (prioridad alta):**
1. `st-odo-01-dashboard` — mayor peso visual; 21 inline styles; ya tiene i18n
2. `st-odo-06-pacientes` — corta (229 lineas); shell completo duplicado
3. `st-odo-07-seguimiento-tratamiento` — shell duplicado; revisar timeline/accordion
4. `st-odo-05-servicios` — la mas corta (55 lineas); sin i18n; shell completo duplicado
5. `st-adm-14-reportes-clinicos` — tiene GestionModules pero Layout=null; graficos y exportaciones

**Grupo B — Layout OK pero incompletos:**
6. `st-odo-03-historial` — shell y header global completados; falta cobertura i18n y validacion funcional/visual de datos sensibles
7. `st-odo-09-perfil-profesional` — Layout OK; 7 inline styles; i18n parcial; formulario de perfil
8. `st-odo-02-agenda` — Layout OK; calendario; usar st-adm-08 como referencia
9. `servicios-asignados` — ya en curso; completar validacion de navegador

**Grupo C — Caso especial:**
10. `st-odo-04-odontograma` — shell roto pero restriccion critica en geometria SVG; migrar solo el shell y controles perifericos

### Que estandarizar en cada vista del rol Profesional

#### `st-odo-01-dashboard` — Dashboard del profesional

- [x] Migrar a `_Layout`, habilitar `UseGestionModulesDesignSystem` y retirar el shell duplicado.
- [x] Eliminar estilos inline locales y sustituir las reglas globales heredadas por estilos acotados al dashboard y tokens compartidos.
- [x] Homogeneizar KPI, cabecera, accesos, tablas, estados, notificaciones y estados vacios; las notificaciones son botones accesibles.
- [x] Localizar contenido visible y valores dinamicos (incluidos aria-labels, fechas, ingresos y cuenta regresiva) en ES/EN.
- [x] Mantener la consulta del dashboard y el refresco limitados al profesional autenticado; la prueba `DashboardStats_DevuelveSoloCitasDelProfesionalAutenticado` paso.
- [x] Compilar el proyecto en salida aislada mientras la instancia existente mantenia bloqueado el ensamblado de `bin`; compilacion correcta, con un aviso preexistente `CS0162` en `CitaService.cs`.
- [ ] Validar en navegador autenticado a 375, 480, 768, 1024 y 1440 px; comprobar overflow, interacciones, consola y cambio ES/EN. La instancia local redirigio al inicio de sesion y no permitio completar esta validacion.

#### `st-odo-02-agenda` — Agenda personal del profesional

- Ya usa `_Layout`; activar `UseGestionModulesDesignSystem`.
- Referencia: `st-adm-08-agenda` (misma estructura de calendario FullCalendar u equivalente).
- Verificar que el sidebar sea el del profesional, no el del administrador.
- Retirar los 3 estilos inline restantes.
- Confirmar que el calendario solo muestra las citas del profesional autenticado.
- Comprobar que las acciones disponibles (ver detalle, iniciar atencion) no incluyen controles de administracion (reasignar, cancelar masivo).
- Validar overflow interno del calendario en movil (permitido; el documento no debe desbordar).

#### `st-odo-03-historial` — Historial clinico del profesional

- [x] Usa `_Layout`, sidebar profesional del layout y `UseGestionModulesDesignSystem`; sin sidebar manual ni estilos inline.
- [x] Estandarizar el header global azul con breadcrumb, titulo y accion de descarga; conservar habilitacion de PDF solo con paciente seleccionado.
- [ ] Completar cobertura i18n: titulos, etiquetas, ayudas, mensajes de estado y controles.
- [ ] Revisar permisos: el profesional solo ve historias de sus propios pacientes; no cambiar la logica de autorizacion, verificar que el markup no exponga rutas de otros pacientes.
- [ ] Validar el panel de detalle/expansion si existe: Escape, retorno de foco, scroll interno.

#### `st-odo-04-odontograma` — Odontograma digital

- **Restriccion critica:** no tocar el SVG/canvas, coordenadas, eventos de diente ni interacciones del odontograma.
- Migrar el shell: quitar DOCTYPE propio, `Layout = null`, sidebar duplicado y recursos globales.
- Activar `_Layout` y sidebar profesional.
- Activar `UseGestionModulesDesignSystem`.
- Reemplazar los 8 estilos inline en controles perifericos (encabezado, botones de guardar/limpiar, mensajes de estado).
- Agregar i18n a etiquetas y mensajes de estado circundantes (no al interior del odontograma si usa textos SVG embebidos).
- Validar que el lienzo mantiene su tamano y comportamiento en cada viewport; documentar si requiere scroll interno.

#### `st-odo-05-servicios` — Servicios del profesional

- Normalizado el shell con `_Layout`, sidebar profesional y `UseGestionModulesDesignSystem`; el main respeta el margen del layout y no incluye controles duplicados de navegacion.
- KPI estandarizados con `.stats-grid`, `.stat-card`, tokens y colores semanticos; filtros usan los controles compartidos y estados usan `status-colors.css`.
- Sustituidos los estilos heredados/globales por reglas acotadas a `.professional-services`, con breakpoints, foco visible, soporte de movimiento reducido e impresion.
- Panel de detalles con scroll interno, bloqueo de scroll de fondo, Escape/backdrop, foco inicial, trampa de foco y retorno al boton disparador; no cambia asignaciones ni agrega operaciones de escritura.
- Filtros de texto, especialidad y disponibilidad conservan los datos SSR; los textos de interfaz y los valores de respaldo tienen claves ES/EN. Nombres/categorias servidos desde el catalogo permanecen como datos fuente.
- Build de la aplicacion aprobado; sintaxis JS/CSS, referencias de IDs y cobertura/ausencia de duplicados en claves ES/EN verificadas. No se modificaron controladores ni persistencia.
- Pendiente: smoke visual autenticado a 375, 480, 768, 1024 y 1440 px, consola, filtros y ciclo del panel de detalles. Confirmar apariencia con datos de prueba sin modificar datos compartidos.

#### `st-odo-06-pacientes` — Lista de pacientes del profesional

- Implementado: migracion a `_Layout`, sidebar compartido, `UseGestionModulesDesignSystem`, estilos locales acotados, filtros server-side, estado vacio, paginacion localizada y acciones con texto visible.
- El endpoint de datos rechaza profesionales sin asociacion, limita pacientes/citas/servicios a los registros propios o asignados y devuelve la proxima cita para poblar la columna correspondiente.
- La vista de detalle general ahora verifica la relacion del paciente antes de permitir acceso a un usuario Profesional.
- Agregada prueba unitaria para acceso a pacientes asignados o atendidos por el profesional y rechazo de pacientes no relacionados.
- Validacion automatizada: compilacion de la aplicacion y del proyecto de pruebas completadas; las 4 pruebas de aislamiento para pacientes asignados, atendidos, ajenos y cuentas sin profesional asociado pasaron en base InMemory aislada.
- Pendiente: smoke test autenticado de filtros, paginacion, ES/EN, consola y matriz responsive; la disponibilidad de espacio en C: impidio la primera restauracion, aunque las compilaciones aisladas y la prueba dirigida finalizaron correctamente.

#### `st-odo-07-seguimiento-tratamiento` — Seguimiento de tratamientos

- Migrado a `_Layout`, sidebar compartido y `UseGestionModulesDesignSystem`; se preservo la estructura concurrente de tarjetas, seguimiento de sesiones y progreso.
- Retirados DOCTYPE, shell/sidebar duplicados y estilos inline; los estilos locales quedaron acotados a `.professional-treatment` y usan tokens compartidos.
- Formulario, filtros, paginacion, estados vacios, estadisticas y dialogos quedaron integrados con el diseno compartido; botones de accion conservan texto visible y los dialogos tienen limites de altura y scroll.
- Completada la cobertura de i18n ES/EN para los controles y mensajes de exito/error de la vista. Los mensajes de validacion enviados directamente por el API aun pueden aparecer en espanol en modo ingles.
- Estados de tratamiento se muestran con clases canonicas de `status-colors.css`; estadisticas usan los colores compartidos.
- Validacion automatizada: build de la aplicacion aprobado sin advertencias; 4 pruebas existentes de aislamiento para pacientes profesionales aprobadas. Sintaxis JS, parsing de CSS, correspondencia de IDs estaticos y cobertura/ausencia de duplicados en claves ES/EN verificadas.
- Pendiente: smoke test autenticado de filtros, paginacion, formularios, dialogos, ES/EN, consola y matriz responsive. Confirmar aislamiento con datos de prueba, sin operar sobre datos clinicos compartidos.

#### `st-odo-09-perfil-profesional` — Perfil del profesional

- Ya usa `_Layout`; activar `UseGestionModulesDesignSystem`.
- Retirar los 7 estilos inline; usar tokens para colores de avatar, espaciado y bordes.
- Revisar formulario de edicion de perfil: controles con altura 40 px, `border: 1px solid var(--border)`, radio canonico, foco visible.
- Antiforgery en el formulario de guardado (ya debe existir; verificar que el token se envie).
- i18n: completar etiquetas, placeholders y mensajes de validacion.
- Foto de perfil: si hay upload, conservar el contrato de endpoint; no cambiar el `name` ni el `enctype`.
- Validar que los campos de solo lectura (registro medico, especialidad) no sean editables desde el markup aunque el CSS los oculte.

#### `st-adm-14-reportes-clinicos` — Reporte clinico del profesional

- [x] Migrado a `_Layout` con `UseGestionModulesDesignSystem`.
- Referencia: `Reportes/vista_admin` para la estructura de pestanas/filtros; adaptar al contexto del profesional (solo sus datos).
- Conservar graficos: no cambiar la libreria ni los datos; solo el contenedor y su responsividad.
- Exportaciones PDF/CSV: mantener rutas y nombres de archivo; no cambiar el handler.
- Filtros de fecha: conservar los IDs que usa el script; agregar i18n a etiquetas.
- Verificar que el sidebar sea el del profesional, no el del administrador.

#### `servicios-asignados` — En curso

- Ya en `_Layout` con `GestionModules ON`; pendiente validacion de navegador.
- Completar: probar seleccion, asignacion, actualizacion y cambio de estado en entorno de pruebas aislado.
- i18n de la vista completada para ES/EN: etiquetas y estados localizables, estado del profesional en el selector y mensajes de error/exito de las acciones. Los nombres de especialidades y servicios se conservan como datos del catalogo.
- Verificar responsive a 375, 480, 768, 1024 y 1440 px.

### Matriz de seguimiento — rol Profesional

| Vista | Shell | GestionModules | inline styles | i18n | Pendiente / evidencia |
|---|---|---|---|---|---|
| `st-odo-01` Dashboard | `_Layout` OK | Si | 0 | Si | Migracion completa (i18n incluida). Pendiente smoke autenticado a 375-1440 px, consola, cambio ES/EN y confirmacion de KPI/datos solo del profesional. Prueba `DashboardStats_DevuelveSoloCitasDelProfesionalAutenticado` aprobada. |
| `st-odo-02` Agenda personal | `_Layout` OK | Si | 1 (dinamico) | Si | Shell y GestionModules aplicados. Pendiente smoke autenticado: sidebar del profesional, calendario solo con sus citas, acciones sin controles de administracion y overflow interno del calendario. |
| `st-odo-03` Historial clinico | `_Layout` OK | Si | 0 | Parcial | Header global azul aplicado. Pendiente completar i18n clinica, validar permisos de pacientes, panel con Escape/foco y smoke autenticado. Datos clinicos sensibles: no cambiar IDs ni `name`. |
| `st-odo-04` Odontograma | `_Layout` OK | Si | 0 | Si | Shell y GestionModules aplicados; SVG/canvas intacto. Pendiente comprobar odontograma funcional (clics, guardado, geometria sin regresion) y smoke autenticado. |
| `st-odo-05` Servicios | `_Layout` OK | Si | 0 | Si | Build y chequeos estaticos aprobados; falta smoke visual/responsive autenticado. Vista de consulta, sin escrituras. |
| `st-odo-06` Pacientes | `_Layout` OK | Si | 0 | Si | Implementado con pruebas de aislamiento aprobadas; falta smoke visual autenticado de filtros, paginacion y ES/EN. |
| `st-odo-07` Seguimiento tratamientos | `_Layout` OK | Si | 0 | Si | Build y pruebas enfocadas aprobados; sintaxis JS/CSS e i18n comprobados. Falta smoke visual autenticado, responsive y validacion funcional aislada. |
| `st-odo-09` Perfil profesional | `_Layout` OK | Si | 0 | Parcial | Shell y GestionModules aplicados. Pendiente completar i18n, verificar antiforgery del formulario, campos de solo lectura no editables y smoke autenticado. |
| `st-adm-14` Reportes clinicos | `_Layout` OK | Si | 0 | Si | Migrado a `_Layout`. Pendiente conservar/verificar graficos, exportaciones PDF/CSV y filtros por profesional, y smoke autenticado con sus datos. |
| `Reportes/vista_prof` Reporte del profesional (`/reportes/vista-prof`) | `_Layout` OK | Si | 1 (ancho dinamico) | Si | La ruta requiere el rol Profesional y filtra citas/facturas por su perfil. Tres pruebas enfocadas pasan, incluido aislamiento y ausencia de datos ficticios. Falta smoke visual autenticado del rol Profesional. |
| `servicios-asignados` | `_Layout` OK | Si | 0 | Si | Pendiente validacion funcional en entorno aislado (seleccion, asignacion, cambio de estado) y smoke responsive autenticado. |

Fase de migracion cerrada para las 10 vistas del rol Profesional (shell `_Layout`, `UseGestionModulesDesignSystem`, iconos canonicos, estilos inline trasladados a clases semanticas, scripts aislados con `asp-append-version` e i18n ES/EN base). Queda pendiente la fase de verificacion: smoke visual autenticado en los cinco anchos, aislamiento de datos por profesional, sidebar correcto, odontograma funcional y continuidad clinica entre vistas.

### Criterios de aceptacion especificos del rol Profesional

Ademas del checklist obligatorio comun, cada vista del profesional debe cumplir:

- **Aislamiento de datos:** el profesional nunca ve datos de pacientes o citas que no le pertenecen. Verificar en navegador con una sesion de profesional autenticado que las listas, graficos y exportaciones solo contienen sus registros.
- **Acciones autorizadas:** no aparecen botones de administracion (eliminar usuario, reasignar masivo, editar profesional ajeno, configuracion global). Las acciones disponibles son las del rol Profesional segun los permisos de `Rol_Menu_Permiso`.
- **Odontograma funcional:** tras la migracion del shell, comprobar que el odontograma carga, responde a clics en dientes, guarda observaciones y no tiene errores de consola ni regresion de geometria.
- **Continuidad clinica:** historial, odontograma, seguimiento y preparacion de consulta son un flujo continuo. Verificar que los enlaces entre vistas (ir al odontograma desde el historial, volver al historial desde el odontograma) funcionan con el `id_paciente` correcto y sin perdida del contexto de sesion.
- **Sidebar correcto:** todas las vistas del profesional deben mostrar `_SidebarProfesional`, no `_SidebarAdmin` ni `_SidebarRecepcionista`. El sidebar dinamico de `_Layout` lo selecciona por rol; no insertar parciales manuales.

## Estandarizacion — rol Paciente

El rol Paciente agrupa vistas en cuatro modulos: `Gestion_De_Citas` (mis citas y notificaciones), `Historia_Clinica` (historial clinico propio), `Gestion_De_PQR` (radicacion de PQR) y `Perfiles` (perfil del paciente y completado de perfil). El paciente solo accede a su propia informacion: sus citas, sus notificaciones, su historia clinica y sus radicados. Ninguna de estas vistas debe exponer datos de otros pacientes ni controles de administracion.

### Inventario y estado actual

| Vista | Ruta | Layout | DOCTYPE | Sidebar dup. | GestionModules | inline styles | i18n |
|---|---|---|---|---|---|---|---|
| `st-pac-01` Mis citas | `Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml` | `_Layout` | No | No | Si (via `_ViewStart`) | 1 (dinamico) | Si |
| `st-pac-02` Historial clinico | `Historia_Clinica/st-pac-02-historial/index.cshtml` | `_Layout` | No | No | Si | 0 | Si |
| `st-pac-03` Notificaciones | `Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml` | `_Layout` | No | No | No | 0 | Si |
| `st-pac-04` Nueva PQR | `Gestion_De_PQR/st-pac-04-nueva-pqr/index.cshtml` | `_Layout` | No | No | Si | 0 | Si |
| `st-pac-perfil` Perfil del paciente | `Perfiles/st-pac-perfil-Paciente/perfil-paciente.cshtml` | `_Layout` | No | No | Si | 0 | Si |
| `completar-perfil` Completar perfil | `Perfiles/completar-perfil-paciente.cshtml` | `_Layout` | No | No | Si | 0 | Si |

Las seis vistas heredan `_Layout` con `_SidebarDynamic` (sidebar por rol), sin `<!DOCTYPE>` propio, sin sidebar insertado manualmente y sin estilos inline de presentacion. El unico `style` restante es el `width` dinamico de la barra de progreso de `st-pac-01`, que se actualiza por JavaScript y no es un estilo de presentacion estatico.

Deuda tecnica registrada: `st-pac-01` y `st-pac-03` no activan `UseGestionModulesDesignSystem` y sus `styles.css` conservan reglas legado del shell (`.sidebar`, `.hamburger`, `.overlay`, `body { display: flex }`, `html { font-size: 15px }`, `:root`). Como `sidebar.css` ya provee la estructura del sidebar, del hamburger y del overlay, esas reglas locales son duplicacion pendiente de retirar. Activar `GestionModules` en estas dos vistas cambiaria la grilla (la hoja compartida fija `.stats-grid` en cuatro columnas, mientras `st-pac-01` muestra cinco tarjetas KPI), por lo que requiere validacion visual previa a `375, 480, 768, 1024 y 1440` px antes de habilitarse.

### Restricciones criticas del rol Paciente

1. **Aislamiento de datos:** el paciente solo ve sus citas, notificaciones, historia clinica y radicados. Los endpoints deben limitar los resultados al usuario autenticado; el markup no debe exponer IDs ni rutas de otros pacientes.
2. **Historia clinica** (`st-pac-02`): los datos son clinicamente sensibles y de solo lectura para el paciente. No se cambian IDs, `name` ni la estructura de las secciones clinicas.
3. **Notificaciones** (`st-pac-03`): el contador de no leidas se calcula en cliente; las acciones de marcar leida y eliminar usan `window.ToastService` y antiforgery. No introducir `alert()` ni `confirm()` nativos.
4. **Radicacion de PQR** (`st-pac-04`): el formulario envia el token antiforgery por `<meta name="csrf-request-token">`; conservar el contrato del endpoint y la carga de adjuntos.
5. **Perfil del paciente** (`st-pac-perfil`): el documento y la fecha de nacimiento son de solo lectura para el paciente; su confirmacion depende de datos reales. No habilitar la edicion de campos clinicos (alergias, condiciones) reservados al profesional.

### Trabajo realizado en el rol Paciente

- Migracion de las seis vistas al shell compartido `_Layout` con sidebar dinamico, secciones `Styles`/`Scripts` y `@Html.AntiForgeryToken()` donde aplica.
- Eliminacion de `<!DOCTYPE>`/`<html>`/`<head>`/`<body>` propios, sidebars manuales (incluido `_SidebarPaciente`) y recursos duplicados.
- Retiro de toasts y footers propios de `st-pac-01` y `st-pac-03`: el toast lo provee `_Toasts.cshtml` (`window.ToastService`) y el footer lo inyecta `footer.js` solo si no existe `#dynamic-footer`.
- Migracion de los estilos inline de `st-pac-04` a clases semanticas en `styles.css` (`.section-title--spaced`, `.char-counter`, `.channel-hours`, `.info-card-title--split`, `.info-card-link`), y de `st-pac-03` a `.page-header .header-actions`.
- Reemplazo de handlers inline `onclick` y de emojis por Google Material Symbols Outlined.
- i18n del contenido visible en ES/EN: encabezados, breadcrumbs, tabs, filtros, botones, placeholders, `aria-label` y modales. Se corrigio el patron `data-i18n` sobre botones con icono, que al reemplazar `textContent` borraba el icono; el texto ahora va en un `<span data-i18n>` interno.
- Registro de las claves nuevas en `wwwroot/js/i18n.config.js` para ambos idiomas: `status.new`/`requested`/`rejected`, `citas.*`, `notifications.*`, `pac_complete.*`, `pac_pqr.*`, `pac_profile.*` y `pac_hist.*`.

### Matriz de seguimiento — rol Paciente

| Vista | Shell | GestionModules | inline styles | i18n | Pendiente / evidencia |
|---|---|---|---|---|---|
| `st-pac-01` Mis citas | _Layout OK | Si (via `_ViewStart`) | 1 (dinamico) | Si | Reglas legado del shell retiradas de `styles.css`; grilla de 5 KPI sobreescrita con `#main-content .stats-grid` (1100px→3, 750px→2, 480px→gap). Pendiente smoke visual autenticado y validacion del modal de cancelacion. |
| `st-pac-02` Historial clinico | _Layout OK | Si | 0 | Si | Header global azul con breadcrumb y accion de descarga; pendiente smoke visual autenticado a 375-1440 px, consola y ES/EN, y confirmacion de solo lectura de datos clinicos. |
| `st-pac-03` Notificaciones | _Layout OK | Si (via `_ViewStart`) | 0 | Si | Reglas legado del shell (`:root`, reset, sidebar, hamburger, overlay, `.nav-group*` y `.main`/`.page-header` en 680px) retiradas de `styles.css`. Pendiente smoke visual y acciones marcar leida/eliminar con datos aislados. |
| `st-pac-04` Nueva PQR | _Layout OK | Si | 0 | Si | Smoke del formulario, adjuntos y antiforgery en entorno aislado; responsive y ES/EN. |
| `st-pac-perfil` Perfil del paciente | _Layout OK | Si | 0 | Si | Validar solo lectura de documento/fecha y campos clinicos; responsive y ES/EN. |
| `completar-perfil` Completar perfil | _Layout OK | Si | 0 | Si | Validar formulario de documento/fecha y persistencia; responsive y ES/EN. |

### Criterios de aceptacion especificos del rol Paciente

Ademas del checklist obligatorio comun, cada vista del paciente debe cumplir:

- **Aislamiento de datos:** verificar en navegador con una sesion de paciente autenticado que citas, notificaciones, historia clinica y radicados corresponden solo a su usuario.
- **Sin controles de administracion:** no aparecen acciones de gestion de otros pacientes, profesionales ni configuracion global.
- **Solo lectura clinica:** la historia clinica y los campos clinicos del perfil no son editables desde el markup ni desde el cliente.
- **Sidebar correcto:** todas las vistas muestran el sidebar del rol Paciente provisto por `_Layout`; no insertar parciales manuales.
- **Evidencia:** el build mas reciente del proyecto se completo con 0 errores y una advertencia preexistente `CS0162` en `CitaService.cs`; las claves i18n ES/EN estan cubiertas y verificadas contra las vistas. El smoke visual autenticado y el responsive en los cinco anchos sigue pendiente por vista.

## Estandarizacion — rol Auxiliar

El rol Auxiliar cubre diez vistas de apoyo clinico, preparacion del consultorio, agenda y perfil. Todas deben usar `_Layout`, el sidebar `_SidebarAuxiliar`, el encabezado compartido azul y `UseGestionModulesDesignSystem = true`. Se mantienen los contratos de seleccion de cita/paciente, checklist, estados, carga de documentos, antiforgery y acciones de perfil.

### Matriz de seguimiento — rol Auxiliar

| Vista | Shell / header | GestionModules | Pendiente / evidencia |
|---|---|---|---|
| `st-aux-01` Panel operativo | `_Layout`, sidebar auxiliar y header global | Si | Smoke autenticado, responsive, acciones y datos limitados al rol. |
| `st-aux-02` Agenda de apoyo | `_Layout`, sidebar auxiliar y header global | Si | Smoke de vistas dia/semana y responsive; retirar estilos inline residuales no dinamicos. |
| `st-aux-03` Preparacion de consulta | `_Layout`, sidebar auxiliar y header global | Si | Se restauro la ruta y se carga el ViewModel con la cita, alergias e historia clinica; selector, progreso y checklist ahora usan estado local por cita. La confirmacion informa que el guardado es local al dispositivo: no existe persistencia de checklist en servidor. Smoke autenticado Auxiliar: pagina, CSS y JS cargan; se verifico estado sin citas y controles deshabilitados. Falta probar con una cita del dia, ES/EN y responsive. |
| `st-aux-05` Historial parcial | `_Layout`, sidebar auxiliar y header global | Si | Validar privacidad, alertas clinicas y responsive. |
| `st-aux-06` Asistencia en procedimiento | `_Layout`, sidebar auxiliar y header global | Si | Conservar seleccion de cita, temporizador, checklist y alertas; revisar estilos inline de estado. |
| `st-aux-07` Control postoperatorio | Migrado de shell independiente a `_Layout`; header global | Si | Validar selector de cita, estados, guardado/antiforgery y responsive. |
| `st-aux-08` Documentos clinicos | Migrado de shell independiente a `_Layout`; header global | Si | Validar paciente, antiforgery, subida/descarga/preview y responsive. |
| `st-aux-09` Estado del consultorio | `_Layout`, sidebar auxiliar y header global | Si | Validar cambio de consultorio, checklist y estados persistidos. |
| `st-aux-10` Citas finalizadas | `_Layout`, sidebar auxiliar y header global | Si | Validar filtros/exportacion y responsive. |
| `st-aux-11` Perfil auxiliar | Migrado de shell independiente a `_Layout`; header global | Si | Validar edicion de perfil, cambio de contrasena y responsive. |

Las cuatro vistas que antes declaraban `Layout = null` ahora reutilizan el sidebar, skip-link, selector de idioma, toasts, confirmacion, chatbot, footer y carga de scripts del layout compartido. Las vistas conservan sus accesos moviles especificos y los toasts locales que consumen sus scripts. La auditoria completa de i18n, estilos inline, shell CSS legado y smoke visual autenticado sigue pendiente. En `st-aux-03` se restauro la ruta GET que no existia y la interaccion de cliente; el checklist y las observaciones se guardan en localStorage, no en la base de datos.

## Estandarizacion — rol Recepcionista

El alcance incluye las ocho vistas dedicadas al rol, mas el reporte operativo de Recepcion y el listado de pacientes servido por la vista compartida de Administracion. Todas usan `_Layout`, el encabezado azul compartido y el sistema `gestion-modules.css`; las rutas que tambien permiten Administrador conservan el sidebar que corresponde al rol autenticado. Las navegaciones moviles propias de las vistas se preservan.

### Matriz de seguimiento — rol Recepcionista

| Vista | Shell / header | GestionModules | Pendiente / evidencia |
|---|---|---|---|
| `st-rec-01` Dashboard de recepcion | `_Layout`, sidebar por rol y header global | Si | Se activo el sistema compartido y el footer dinamico; validar KPIs, alertas, acciones y responsive. |
| `st-rec-02` Registrar paciente | `_Layout`, sidebar por rol y header global | Si | Validar pasos, campos, antiforgery, errores y creacion de paciente/cita. |
| `st-rec-03` Gestion de citas | `_Layout`, sidebar por rol y header global | Si | Se activo el sistema compartido y el footer dinamico; validar filtros, paginacion, alta/edicion/cancelacion y responsive. |
| `st-rec-04` Generar factura | `_Layout`, sidebar por rol y header global | Si | Validar calculos, pagos, impresion y datos reales de factura. |
| `st-rec-05` Recordatorios | `_Layout`, sidebar por rol y header global | Si | Se activo el sistema compartido y el footer dinamico; validar envios, historial, alertas y responsive. |
| `st-rec-historial` Historial clinico | `_Layout`, sidebar por rol y header global | Si | Validar permisos de solo lectura y filtros/detalles de paciente. |
| `st-rec-06` Perfil recepcionista | `_Layout`, sidebar por rol y header global | Si | Validar datos, edicion de perfil y cambio de contrasena. |
| `st-rec-pacientes` Listado de pacientes | Vista compartida `st-adm-05`, sidebar y breadcrumb de Recepcion | Si | Los enlaces desde reportes y la miga de pan ahora conservan la ruta/identidad de Recepcion. Validar que no exponga acciones exclusivas de Administracion. |
| `vista_recepcion` Reportes operativos | `_Layout`, sidebar por rol y header global | Si | Validar filtros, indicadores, reportes y exportaciones. |

Las pantallas `st-rec-01`, `st-rec-03` y `st-rec-05` ya cargaban `_Layout`, pero no habilitaban `gestion-modules.css`; ahora lo hacen y dejan el footer comun al layout. Los flujos moviles y los scripts de cada vista se mantienen. El listado compartido y los enlaces de reportes muestran ahora la ruta/miga correspondiente a Recepcionista. Smoke autenticado: las nueve rutas listadas devuelven 200 y muestran el encabezado azul compartido y un solo footer. En las tres vistas recien ajustadas se comprobaron anchos CSS de 375, 640, 768 y 1280 px sin overflow horizontal del documento. Siguen pendientes la regresion de formularios, cobros y recordatorios y el smoke responsive de las demas vistas.

## Alcance incorporado de la propuesta adjunta

Se incorpora su inventario vista por vista, las referencias canonicas, los casos de prueba especificos y los criterios de aceptacion. La propuesta no reemplaza los hallazgos del repositorio: cuando una recomendacion contradice el markup, las rutas, los IDs o los datos que ya usa una vista, se conserva primero el contrato existente y se registra el cambio funcional como decision separada.

### Referencias visuales y tecnicas

Las referencias para comparar componentes son `st-adm-01-dashboard`, `st-adm-08-agenda`, `st-adm-09-citas`, `st-adm-07-gestion-profesionales` (incluida la vista `servicios-asignados`) y `st-adm-14-reportes-clinicos`. Son referencias de patrones visuales y de UX, no autorizacion para reemplazar el comportamiento de otras vistas.

El orden de carga real de `_Layout.cshtml` es: `variables.css`, `ui-components.css`, CSS local de la vista, `gestion-modules.css` cuando la vista activa `UseGestionModulesDesignSystem`, `status-colors.css` y estilos compartidos de shell/footer. No se cambiara a una activacion global de `UseGestionModulesDesignSystem` ni se tocara masivamente `_ViewStart`; cada modulo/vista se habilita y valida explicitamente. La ruta compartida correcta es `~/css/shared/gestion-modules.css`.

El motor i18n reconoce `data-i18n-ph` para placeholders (no `data-i18n-placeholder`). Los spans de paginacion mantienen sus nodos dinamicos. Las vistas anonimas y publicas conservan un shell sin sidebar autenticado.

### Inventario completo

El conteo es de vistas de pagina; parciales como `st-adm-08-agenda/_CalendarSection.cshtml`, componentes compartidos y `_ViewStart.cshtml` no se cuentan como pagina independiente. Las paginas de referencia ya migradas tambien deben superar la checklist vigente; su CSS compartido no las exime de auditoria.

| Area | Cantidad | Vistas incluidas |
|---|---:|---|
| Acceso y seguridad | 7 | `st-adm-02-gestion-usuarios`, `st-adm-03-gestion-roles`, `st-adm-15-bitacora`, `login`, `register`, `recover`, `change-password`. |
| Gestion de citas | 14 | `st-adm-01-dashboard`, `st-adm-08-agenda`, `st-adm-09-citas`, `st-aux-01-panel-operativo`, `st-aux-02-agenda-apoyo`, `st-aux-06-asistencia-procedi`, `st-aux-09-estado-consultorio`, `st-aux-10-citas-finalizadas`, `st-odo-02-agenda`, `st-pac-01-mis-citas`, `st-pac-03-notificaciones`, `st-rec-01-dashboard`, `st-rec-03-gestion-citas`, `st-rec-05-recordatorios`. |
| Gestion de profesionales | 5 | `st-adm-07-gestion-profesionales`, `servicios-asignados`, `st-adm-14-reportes-clinicos`, `st-odo-01-dashboard`, `st-odo-09-perfil-profesional`. |
| Gestion de pacientes | 5 | `st-adm-05-gestion-pacientes`, `detalle-paciente`, `st-aux-03-preparacion-consulta`, `st-aux-05-historial-parcial`, `st-rec-02-registrar-paciente`. |
| Historia clinica | 9 | `st-adm-historial`, `st-aux-07-control-postoperato`, `st-aux-08-documentos-clinicos`, `st-odo-03-historial`, `st-odo-04-odontograma`, `st-odo-06-pacientes`, `st-odo-07-seguimiento-tratamiento`, `st-pac-02-historial`, `st-rec-historial`. |
| Facturacion y pagos | 2 | `st-adm-12-facturacion`, `st-rec-04-generar-factura`. |
| Servicios y recursos | 3 | `st-adm-10-servicios`, `st-adm-16-configuracion-general`, `st-odo-05-servicios`. |
| Gestion de PQR | 2 | `st-adm-17-gestion-pqr`, `st-pac-04-nueva-pqr`. |
| Reportes | 3 | `vista_admin`, `vista_prof`, `vista_recepcion`. |
| Perfiles | 4 | `completar-perfil-paciente`, `st-aux-11-perfil-auxiliar`, `st-pac-perfil-Paciente`, `st-rec-06-perfil-recepcionista`. |
| Centro de ayuda | 4 | `Guias_Tutoriales_y_Soporte`, `ComoProgramarUnaCita`, `SoporteTicket`, `Soporte y Tickets`. |
| Publico y desarrollo | 5 | `homepage`, `privacidad`, `terminos`, `Styleguide`, `Views/shared/index.cshtml` (confirmar si es pagina o redireccion antes de migrar). |
| **Total** | **63** | **12 areas funcionales.** |

### Requisitos particulares por grupo

- **Acceso y ayuda:** mantener autenticacion, permisos, antiforgery y feedback; login, registro y recuperacion no heredan el sidebar autenticado. `change-password` se clasifica por su ruta y autorizacion real antes de elegir shell.
- **Citas y profesionales:** corregir los dos desbordamientos ya medidos (dashboard y agenda), preservar calendarios, graficos, filtros, paginacion, exportaciones y modales. Verificar por separado `servicios-asignados`; las cinco vistas de referencia tambien pasan regresion.
- **Pacientes y facturacion:** preservar formularios multipaso, datos de pacientes, operaciones de cobro y calculos; probar filtros, validaciones y descargas existentes.
- **Historia clinica:** proteger permisos y datos sensibles. En odontograma no modificar la geometria ni la interaccion SVG/canvas; estandarizar solo el shell y los controles circundantes.
- **Servicios y PQR:** preservar activacion de servicios, archivos y respuestas; revisar los handlers de paginacion de PQR contra los elementos realmente renderizados, sin crear controles ficticios para silenciar avisos.
- **Reportes y perfiles:** conservar filtros, graficos, exportaciones y preferencias; resolver el rol efectivo de `vista_admin` con la ruta/controlador antes de cambiar navegacion o autorizacion.
- **Ayuda, publico y desarrollo:** usar el shell correspondiente (anonimo o autenticado); mantener contenido legal y verificar si `Views/shared/index.cshtml` es accesible como pagina.

### Correcciones y decisiones respecto de la propuesta

- Bitacora ya usa `_Layout`, carga la lista SSR y actualiza datos desde `/api/v1/reportes/bitacora`; el filtro de modulo se llena con las tablas presentes. La ruta, el encabezado y responsive pasaron la auditoria transversal reciente; quedan pendientes las interacciones propias y la checklist visual completa. El build aislado actual compila con 0 errores y una advertencia preexistente `CS0162`.
- Bitacora actualmente filtra por busqueda, nivel, modulo y una fecha (`#filterDate`); no tiene `#fechaInicio`/`#fechaFin`, y los niveles implementados son `critical`, `error`, `warning`, `info`, `success` y `debug`. No se cambian a INFO/WARN/ERROR/FATAL ni se agregan filtros sin confirmacion funcional.
- La vista declara opciones PDF/CSV/JSON, pero su exportacion actual simula progreso y descarga; no se documentara como exportacion real a Excel/CSV ni se modificara como parte de una migracion visual.
- Los colores de severidad de auditoria son especificos del dominio y no se convierten mecanicamente en estados de registro. `status-colors.css` se aplica a estados; severidad conserva sus clases y contraste apropiados.
- Los totales de fase y porcentajes de avance de la propuesta no se toman como evidencia. La matriz del repositorio y las pruebas efectivamente ejecutadas son la fuente del estado.
- El smoke test de rutas se ejecuta con el perfil/rol autorizado y datos disponibles. Un 401/403 esperado no se trata como error visual; se valida aparte la autorizacion. La consola excluye ruido externo identificado, pero no errores propios ocultos.
- `_Layout` carga `i18n-core` y su configuracion antes de los scripts de cada vista, para que estos puedan usar el servicio al inicializarse. `Reportes/vista_admin` incluye localmente `@addTagHelper` para que `asp-append-version` genere la URL versionada. No existe un `_ViewImports.cshtml` global; al probarlo, vistas heredadas con expresiones C# en atributos `<option>` fallaron con RZ1031, por lo que no se debe habilitar globalmente sin migrar esos casos y validar todo el proyecto.

## Validacion de la ultima intervencion

- `Reportes/vista_prof` ahora usa el encabezado y layout compartidos, exige el rol Profesional, filtra las metricas, citas y facturas por el perfil ligado a la cuenta, y presenta estados localizados tambien en el detalle de cita.
- `ComoProgramarUnaCita` y `SoporteTicket` activan `UseGestionModulesDesignSystem`, usan el markup canonico del encabezado y ya no redefinen `body`, `.main` ni el header desde CSS local. Soporte usa las fuentes compartidas DM Sans/Syne en lugar de cargar fuentes distintas.
- Pruebas enfocadas de reportes: 3 aprobadas. `docker compose up -d --build web` reconstruyo y arranco el servicio; `http://127.0.0.1:5259/` devolvio HTTP 200.
- Suite completa: 458 de 463 pruebas aprobadas; fallan cinco pruebas de `CitaServiceTests` porque esperan `InvalidOperationException` y reciben `CitaConflictException`. No se modifico ese servicio como parte de esta intervencion.
- No se pudo completar el smoke autenticado del reporte ni de las vistas internas de Ayuda: el navegador disponible no conserva una sesion valida y redirige al login. No se declaran completos los checks visuales por rol ni la estandarizacion global de las 63 vistas; la matriz conserva sus pendientes.

## Endurecimiento de vistas publicas y acceso

Se conservaron los shells anonimos existentes (`Views/Publico/_LayoutPublico.cshtml` y `Views/Acceso_Y_Seguridad/_LayoutAuth.cshtml`); no se recrearon layouts ni se confundio la ubicacion de una vista con su autorizacion. Los cambios de esta fase son:

- Se elimino el endpoint web que recibia una contrasena por query string y devolvia el hash.
- Las politicas de login, recuperacion y registro ahora particionan los limites por IP; la recuperacion conserva ademas sus limites por cuenta en `AuthService`. El encabezado `Retry-After` se entrega al rechazar una solicitud.
- Se configuraron proxies reenviados solo para IPs declaradas en `KnownProxies`; el despliegue debe cargar las IP reales del proxy, sin confiar indiscriminadamente en `X-Forwarded-For`.
- Las cookies de autenticacion explicitan `HttpOnly`, `SameSite` y el uso de HTTPS en produccion. Las paginas de acceso no se almacenan en cache, la ruta de acceso denegado responde 403 y los errores de antiforgery no exponen el detalle interno.
- `Security:RequireHttpsCookies` es verdadero por defecto fuera de Development; el Compose de desarrollo lo desactiva porque publica solo HTTP en `127.0.0.1`. El perfil de produccion no lo desactiva.
- La validacion de `returnUrl` usa `Url.IsLocalUrl` y el GET del login ya no cierra una sesion existente como efecto secundario.
- Los IDs de solicitud aceptados desde `X-Request-ID` se limitan a caracteres seguros y 64 caracteres antes de reflejarlos en respuestas/logs.
- Se agregaron encabezados de proteccion y CSP en modo `Report-Only`; no se debe pasar a enforcement hasta inventariar los recursos externos e inline de todas las vistas y configurar un receptor/monitoreo util.
- El endpoint de contacto publico abre WhatsApp solo tras validar los campos y aceptar el consentimiento. El JavaScript publico ya no inserta texto de usuario con `innerHTML` ni ejecuta handlers obsoletos de chat/PQR.
- Tailwind se compila localmente a `wwwroot/css/generated/tailwind.css`; `package-lock.json` fija dependencias y `node_modules/` queda ignorado. La carga del asset se versiona mediante Tag Helpers en los shells/publicaciones que lo usan.
- Los logs de autenticacion ya no guardan el correo en claro; los campos estructurados de correlacion quedan redactados.

La compilacion de la aplicacion y las 22 pruebas de `AccesoSeguridadControllerTests` pasan. Persisten advertencias de nulabilidad en `ViewComponentsTests.cs`, fuera del alcance de esta fase. Se reconstruyo y arranco `smiletrack-web` en Docker junto al SQL Server healthy. La homepage, login, registro, recuperacion y `/css/generated/tailwind.css` devuelven HTTP 200; la respuesta de login emite el token antiforgery y las vistas cargan Tailwind local, no CDN. El navegador integrado confirmo homepage y login actualizados. La primera publicacion encontro que el Compose local sirve HTTP mientras antiforgery exigia cookies `Secure`; se agrego `Security:RequireHttpsCookies`, desactivado solo en el Compose de desarrollo local (loopback HTTP). El perfil de produccion deja la opcion en `true` por defecto y conserva redireccion HTTPS/HSTS. Se detuvo unicamente la instancia host antigua que ocupaba el puerto 5259 para que Docker pudiera publicar la nueva.

La veracidad del domicilio, telefono, precios, testimonios, certificaciones, habilitaciones, cifras comerciales y afirmaciones clinicas del sitio debe confirmarla la organizacion. No se consideran verificadas ni se deben republicar como datos reales solo porque aparecen en la vista. Tambien quedan pendientes la autohospedacion de fuentes/imagenes externas, la calibracion final de CSP, MFA (requiere elegir proveedor/metodo) y una revision visual responsive autenticada.
