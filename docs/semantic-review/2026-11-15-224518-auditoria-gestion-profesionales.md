# Auditoría de vistas — Módulo Gestión de Profesionales

Revisión de las cinco vistas, sus CSS individuales y `gestion-modules.css`. Las señales del design system (`Layout = "_Layout"`, `ViewData["UseGestionModulesDesignSystem"] = true`) están presentes en todas las vistas, pero los CSS por vista reimplementan ~600 líneas ya cubiertas por el archivo compartido, creando tres bases divergentes. Hay además huecos de accesibilidad concretos y un bug SSR en el dashboard.

**Watch for:**
- **CSS duplicado masivo (confirmado):** `st-adm-07` y `st-adm-14` copian íntegramente sidebar, hamburger, main, header, botones, stats, filtros, tabla y paginación — todo ya definido en `gestion-modules.css`. Cada vista añade ~600 líneas muertas.
- **Tabs de filtrado vs. form GET en st-adm-07 (confirmado):** Los botones `.tab-btn` cambian estado visual pero no disparan ningún filtro real; el formulario GET por encima ya filtra por `estado`. Dos mecanismos paralelos para la misma función crean inconsistencia UX.
- **Formulario de ausencias sin validación de fechas cliente (confirmado):** `absenceFechaInicio`/`absenceFechaFin` se leen, se envían, pero app.js no verifica que fin ≥ inicio antes del POST; la validación solo ocurre en el servidor tras el conflicto.
- **SVG de rendimiento sin título accesible (confirmado):** El `<svg>` del score-circle no tiene `<title>` ni `aria-label`; los lectores de pantalla leen el gradiente inline como texto sin contexto.
- **`onsubmit="return confirm()"` en eliminar credencial (confirmado):** Patrón bloqueante incompatible con lectores de pantalla y políticas CSP estrictas.

**Verdict**: NEEDS_CHANGES

---

## High-level view

El sistema de diseño está bien adoptado en todas las vistas a nivel de señales (`UseGestionModulesDesignSystem`, `Layout = "_Layout"`), pero los CSS por vista ignoran la cobertura real de `gestion-modules.css` y reimplementan prácticamente todo su contenido desde cero. Esto crea tres bases de código CSS divergentes que inevitablemente se desincronizarán.

`st-adm-07` es la vista más compleja y la mejor construida, pero mezcla dos estrategias de filtrado (tabs JavaScript + form GET servidor) que el usuario no puede reconciliar. Los modales usan `inert` correctamente; el modal de horarios carga datos de API y renderiza bien, pero el sub-formulario de ausencias omite validación de rango de fechas en cliente.

`st-adm-14` delega todo el filtrado al servidor y el JS es mínimo. La única pieza no estándar es el modal de alerta clínica, que implementa focus-trap con Tab/Shift+Tab — correcto.

`st-odo-01-dashboard` tiene 18 ViewData todos consumidos correctamente. El SVG del gráfico de rendimiento circular (`score-circle`) se genera en parte en Razor (stroke-dasharray inicial) y se anima en JS, pero el elemento `<svg>` carece de `<title>` accesible. Los cálculos de porcentajes de asistencia ocurren en el bloque `@{ }` Razor a partir de contadores ya calculados por el Controller: son derivados simples, pero deberían moverse al Controller para que la vista no tenga código de derivación.

`st-odo-09-perfil-profesional` incrusta footer y `<nav class="mobile-nav">` directamente en la vista en lugar de delegarlos al layout compartido, lo que rompe la separación de responsabilidades y duplica esos elementos si el layout ya los incluye. El formulario de cambio de contraseña intercepta el `submit` vía JS y envía a un endpoint `/api`, pero el `action` del form HTML apunta a `/acceso-y-seguridad/cambiar-contrasena` (el endpoint no-API) como fallback; si JS falla, el usuario llega a un endpoint diferente con un payload de form distinto al que espera la API.

`servicios-asignados.cshtml` es la vista más limpia del módulo: sin JS complejo, sin modales, Server-Side-Render puro. Los únicos problemas son accesibilidad menor en el formulario de selección de profesional y la ausencia de paginación en la tabla de asignaciones existentes.

---

<details>
<summary>Issues (19)</summary>

1. **CSS duplicado masivo en st-adm-07 y st-adm-14** — Ambas vistas copian ~600 líneas (sidebar, hamburger, main, header, botones, stats, filtros, tabla, paginación) ya cubiertas por `gestion-modules.css`. Eliminar esas secciones de los CSS individuales.
2. **Tabs de estado vs. form GET en st-adm-07** — Los botones `.tab-btn` tienen `aria-pressed` pero no actualizan el filtro real (el GET filtra por `estado` en query string). Conectar los tabs al submit del form o eliminarlos a favor de los selects existentes.
3. **Ausencias sin validación de fechas cliente** — `absenceFechaFin` puede ser menor que `absenceFechaInicio` sin feedback hasta recibir error del servidor. Añadir validación `fechaFin >= fechaInicio` antes del POST en `renderAbsences`/`initAbsenceForm` en app.js.
4. **SVG score-circle sin título ARIA** — El `<svg>` del dashboard no tiene `<title>` ni `aria-label`; lectores de pantalla no comunican el porcentaje de rendimiento. Añadir `<title id="scoreTitle">Rendimiento del día: @tasaAsistencia%</title>` y `aria-labelledby="scoreTitle"` al elemento `<svg>`.
5. **`onsubmit="return confirm()"` en credencial delete (perfil)** — Patrón bloqueante incompatible con lectores de pantalla y potencialmente bloqueado por CSP. Reemplazar con modal de confirmación o un manejador JS.
6. **Footer y mobile-nav hardcodeados en st-odo-09** — La vista incluye `<footer>` y `<nav class="mobile-nav">` directamente, duplicando elementos que debería proveer el layout. Mover al layout o a un partial compartido.
7. **Fallback form-action en cambio de contraseña (perfil)** — El `<form action="/acceso-y-seguridad/cambiar-contrasena">` apunta al endpoint MVC tradicional, pero perfil.js intercepta el submit y envía a `/acceso-y-seguridad/cambiar-contrasena/api` con JSON. Si JS falla, el formulario hace POST al endpoint incorrecto con `Content-Type: application/x-www-form-urlencoded`. Alinear el `action` al endpoint real o usar `action=""` con el handler JS como única vía.
8. **`stat-icon` invisible en gestion-modules.css** — `gestion-modules.css` define `.stat-icon` con `position: absolute; opacity: .35` pero los CSS individuales de `st-adm-07` y `st-adm-14` no definen `.stat-icon`, por lo que dependen de este estilo compartido sin declararlo localmente. Si se elimina el CSS duplicado, esta clase queda cubierta; si se mantiene el CSS local, falta la regla.
9. **Tabla de asignaciones sin paginación (servicios-asignados)** — `asignaciones` se renderiza completa sin paginación. Un profesional con muchos servicios generará una tabla larga sin control de tamaño.
10. **`badge-inactivo` con colores inconsistentes entre vistas** — En `gestion-modules.css`: fondo `#fee2e2` / texto `#991b1b` (rojo). En `st-odo-09/styles.css`: fondo `#f1f5f9` / texto `#64748b` (gris). La misma clase muestra significados visuales distintos según la vista.
11. **Tabs del modal de detalle sin atributo `tabindex="0"` en paneles** — Los `role="tabpanel"` (`tabPaneGeneral`, `tabPaneHorarios`, `tabPaneAusencias`) no tienen `tabindex="0"`, lo que impide que sean enfocables por teclado al ser activados, violando WCAG 2.1 criterio 4.1.2.
12. **`progress-row` en dashboard no tiene el estilo de `gestion-modules.css`** — `gestion-modules.css` define `.main .progress-row` con `background`, `border`, `border-radius` y `box-shadow`. El CSS del dashboard (`st-odo-01/styles.css`) redefine `progress-row` sin esos estilos. Las reglas colisionan; la versión del dashboard gana por especificidad de clase.
13. **Texto hardcoded en tabla de credenciales (st-adm-07)** — Encabezados `PROFESIONAL`, `DOCUMENTO`, `ARCHIVO`, `ESTADO`, `REVISIÓN` no tienen `data-i18n`. El resto de la vista usa `data-i18n` sistemáticamente.
14. **`formCategoria` no conectado al backend** — El select de Categoría en el modal de creación (`formCategoria`) usa `especialidad.Nombre` como value. El endpoint `/api/profesionales` espera `Categoria` como texto libre o enum; no hay evidencia de que este campo se procese. Si el Controller lo ignora, el campo es un engaño para el usuario.
15. **`renderScheduleDays` en app.js usa `style` inline para inputs** — Las filas de horario del modal de detalle generan `style="padding:4px 8px;font-size:0.85rem;"` directamente en el HTML generado por JS, en lugar de usar clases CSS. Esto hace imposible tematizar o sobreescribir esos estilos desde los CSS.
16. **Ausencias: `.modal-absence-form` sin `aria-label`** — El `<form id="absenceForm">` no tiene `aria-label` ni `aria-labelledby`, por lo que un lector de pantalla no contextualiza el formulario dentro del tab.
17. **`notif-empty` con `hidden="@(!hayNotificaciones)"` incorrecto** — En Razor, `hidden="false"` sigue siendo el atributo `hidden` presente (HTML booleano), lo que oculta el elemento incluso cuando hay notificaciones. El valor correcto es no emitir el atributo cuando `hayNotificaciones` es verdadero. Usar `@(hayNotificaciones ? "true" : null)` como valor, o mover la lógica a JS.
18. **`<progress-row>` en dashboard duplica clase con `gestion-modules.css`** — `gestion-modules.css` incluye `.main .progress-row` con padding y box-shadow. El CSS del dashboard lo redefine sin esas propiedades. La barra de progreso puede perder su fondo blanco/borde en algunas resoluciones dependiendo del orden de carga de los stylesheets.
19. **`btn-danger` en tabla de credenciales (st-adm-07) usa color texto blanco** — `gestion-modules.css` define `.actions-cell .action-btn.btn-danger` con `color: var(--danger)` (rojo sobre fondo claro). Los botones de la tabla de verificación de credenciales usan `.btn-danger` sin `.action-btn`, obteniendo el estilo de `.main .btn-danger` (texto blanco sobre fondo rojo), lo que es correcto visualmente pero inconsistente con la intención del componente de tabla.

</details>

---

<details>
<summary>Detalles</summary>

## CSS duplicado entre hojas de estilo individuales y gestion-modules.css

Tanto `st-adm-07/styles.css` como `st-adm-14/styles.css` abren con un bloque `:root` que redeclara todas las variables base (`--sidebar-w`, `--bg`, `--white`, `--primary`, `--border`, `--shadow`, `--radius`, `--font`, `--font-display`, `--tr`) ya presentes en `gestion-modules.css`. A continuación, cada una copia íntegramente los bloques de sidebar, hamburger, overlay, `.main`, `.page-header`, `.breadcrumb`, `.btn-primary`, `.btn-secondary`, `.stats-grid`, `.stat-card`, `.filters-row`, `.search-wrap`, `.status-select`, `.table-card`, `.table-base`, `.pagination`, `.mobile-nav`, y todos sus breakpoints responsive.

La superposición es casi total. Las únicas reglas genuinamente específicas de cada vista son:

- **st-adm-07**: `.modal-overlay`, `.modal`, `.modal-lg`, `.form-grid`, `.specialty-chip`, `.schedule-row`, `.modal-tabs`, `.p-avatar--color-*`, `.badge-spec`, `.badge-status`, `.nav-group`, ausencias form grid.
- **st-adm-14**: `.report-error`, `.alert-trigger`, `.report-alert-modal` y sus componentes internos, `.p-doc`.

El resto — aproximadamente 600 líneas en cada archivo — es eliminable.

`st-odo-09/styles.css` sigue el mismo patrón para sidebar, hamburger, main, header, body, skip-link, visually-hidden, focus-visible. Las reglas específicas del perfil (`.profile-header-card`, `.content-layout`, `.info-grid`, `.schedule-day`, `.modal-backdrop`, `.toggle-slider`, credenciales) son legítimas.

`st-odo-01/styles.css` **no duplica** los componentes globales: solo define `.professional-dashboard` como prefijo y extiende los tokens `var(--space-*)`, `var(--type-*)`, `var(--weight-*)` de un sistema de tokens que no está en `gestion-modules.css`. Esos tokens adicionales deben migrarse a `gestion-modules.css` o documentarse explícitamente como extensión del sistema de diseño.

## Tabs de estado sin acción real en st-adm-07

La vista tiene dos mecanismos paralelos para filtrar por estado: los botones `.tab-btn` con `data-tab="activo|vacaciones|inactivo"` y el `<select id="filterStatus" name="estado">` dentro del form GET. `loadProfessionalsList()` en app.js lee `document.querySelector('#filterStatus')?.value` para el filtro — no los `.tab-btn`. Hacer clic en "Activos" cambia `aria-pressed="true"` sin filtrar nada. El estado ARIA comunica una acción que no ocurrió.

## Modal de ausencias: validación de fechas ausente en cliente

El submit de `#absenceForm` en app.js no incluye ninguna comparación `fechaFin >= fechaInicio`. El modal de conflictos (`absenceConflictModal`) se activa cuando el servidor detecta citas dentro del período, pero si las fechas son inválidas, el servidor devuelve un error genérico como toast sin señalar el campo — violando WCAG 3.3.1 (el error debe identificar el campo). Los inputs de fecha tampoco tienen `min` attribute; `absenceFechaInicio` debería tener `min="@DateTime.Today.ToString("yyyy-MM-dd")"` para prevenir ausencias retroactivas.

## SVG de rendimiento sin semántica accesible

En `st-odo-01-dashboard/index.cshtml`, el `<svg>` del score-circle no tiene `role="img"` ni `<title>`. El valor `@tasaAsistencia%` está en el `<div class="score-text">` adyacente como texto visible, así que la corrección mínima es añadir `aria-hidden="true" focusable="false"` al SVG para que los lectores de pantalla no expongan el `<linearGradient id="gradient">` como nodo de texto:

```html
<svg width="120" height="120" viewBox="0 0 120 120"
     aria-hidden="true" focusable="false">
```

El atributo `focusable="false"` es necesario para IE11/Edge antiguo, donde los SVG son focusables por defecto.

La variable `--cita-cancelada-bg` y otras `--cita-*` usadas en `st-odo-01/styles.css` para las notificaciones no están definidas en `gestion-modules.css`. Si el CSS del módulo de citas no se carga, los colores de las notificaciones colapsan a `transparent`.

## Fallback de formulario de contraseña en st-odo-09

```html
<form id="passwordForm" method="post"
      action="/acceso-y-seguridad/cambiar-contrasena" novalidate>
```

perfil.js escucha el `submit` y envía `fetch` a `/acceso-y-seguridad/cambiar-contrasena/api` con `Content-Type: application/json`. Si el JS no carga, el form hace POST al endpoint MVC con `application/x-www-form-urlencoded`, que espera un modelo de formulario diferente al JSON que maneja el endpoint `/api`. El riesgo práctico es bajo, pero la inconsistencia complica el mantenimiento y puede causar errores silenciosos si el endpoint MVC devuelve un redirect en lugar de un error claro.

## Modal de detalle: tabpanels no enfocables

```html
<div class="modal-tab-pane active" id="tabPaneGeneral"
     role="tabpanel" aria-labelledby="tabBtnGeneral">
```

WCAG 2.1 criterio 4.1.2 requiere que los elementos con `role="tabpanel"` sean enfocables (`tabindex="0"`) para que el usuario de teclado pueda navegar al panel tras activar el tab. Ninguno de los tres paneles tiene `tabindex`. Cuando el usuario activa "Horarios" con teclado, el foco permanece en el botón tab sin llegar al contenido del panel.

## `hidden` booleano mal usado en notif-empty (dashboard)

```razor
<div class="notif-empty" hidden="@(!hayNotificaciones)">
```

`hidden="False"` (lo que Razor emite cuando `hayNotificaciones` es `true`) sigue activando el atributo booleano — el navegador lo interpreta como presente, ocultando el elemento. El estado inicial del SSR es el inverso del esperado: el div "Todo al día" aparece oculto cuando hay notificaciones y debería ser visible, y viceversa. app.js corrige esto en runtime mediante `setupDismissibleNotifications()`, pero cualquier usuario sin JS o con JS lento verá el estado incorrecto.

Corrección:
```razor
<div class="notif-empty" @(hayNotificaciones ? "hidden" : "")>
```

## Accesibilidad en servicios-asignados

La `<section>` que contiene la tabla de asignaciones no tiene un nombre accesible propio; el `aria-label` está en el `div.table-wrap` interior, no en la sección. El `tabindex="0"` en el div de scroll es correcto. El problema más concreto: si `asignaciones` supera 20–30 ítems, la tabla crece sin control de tamaño ni paginación.

## Lógica de negocio en vistas

La vista `st-odo-01-dashboard/index.cshtml` calcula cinco porcentajes de distribución de citas (`pctAtendidas`, `pctProgramadas`, `pctCanceladas`, `pctNoAsistio`) en el bloque `@{ }` Razor. Aunque son derivados simples, pertenecen al Controller: la vista no debería tener código de derivación. El Controller debería exponer `ViewData["PctAtendidas"]` directamente.

## Cobertura de tests

No hay tests de UI automatizados identificados para ninguna de estas vistas. Sin cobertura de:
- Paginación bajo filtros combinados
- Apertura y cierre de modales con teclado
- Render del score-circle con `tasaAsistencia = 0`
- Comportamiento de `notif-empty` cuando todas las notificaciones son descartadas
- Validación de fechas en formulario de ausencias

</details>

---

## 10 cambios más impactantes

### 1. Eliminar CSS redundante de st-adm-07/styles.css y st-adm-14/styles.css
**Impacto: Alto.** Eliminar los bloques de sidebar, hamburger, main, header, botones, stats, filtros, tabla y paginación de ambos archivos — aproximadamente 600 líneas en cada uno. Dejar solo las reglas genuinamente específicas de cada vista (modales, badges de especialidad, alert modal, chips). Esto elimina la divergencia CSS que inevitablemente producirá bugs de apariencia.

**Reglas a eliminar de st-adm-07/styles.css:**
```css
/* Eliminar: .sidebar, .sidebar-*, .hamburger, .overlay,
   .main { margin-left... }, .page-header, .breadcrumb,
   .page-title, .page-subtitle, .btn-primary, .btn-secondary,
   .stats-grid, .stat-card, .stat-number, .stat-label,
   .filters-section, .filters-row, .search-wrap, .search-input,
   .filter-wrap, .status-select, .table-card, .table-wrap,
   .table-base y sus thead/tbody, .pagination, .pagination-buttons,
   .mobile-nav, @media (max-width: 1100px/680px/480px) para esos,
   .nav-group, .nav-group-header — todo ya cubre gestion-modules.css */
```

### 2. Corregir `hidden` booleano en `notif-empty` del dashboard
**Impacto: Alto (corrección de bug SSR).** Cambiar:
```razor
<!-- Actual (buggy): -->
<div class="notif-empty" hidden="@(!hayNotificaciones)">

<!-- Correcto: -->
<div class="notif-empty" @(hayNotificaciones ? "hidden" : "")>
```

### 3. Conectar tabs de estado a filtro real en st-adm-07
**Impacto: Alto (UX + accesibilidad).** Los tabs con `aria-pressed` que no ejecutan ninguna acción son un engaño accesible. Dos opciones:
- Conectar cada tab click al `filterStatus` select y disparar el form GET.
- Eliminar los tabs y mejorar la visibilidad del select de estado existente.

### 4. Añadir `aria-hidden="true"` al SVG del score-circle en dashboard
**Impacto: Alto (accesibilidad).** El valor ya está visible como texto `@tasaAsistencia%`. El SVG solo añade ruido para lectores de pantalla:
```html
<svg width="120" height="120" viewBox="0 0 120 120" aria-hidden="true" focusable="false">
```

### 5. Añadir validación `fechaFin >= fechaInicio` en cliente para ausencias
**Impacto: Alto (UX + accesibilidad WCAG 3.3.1).** En app.js, en la función que procesa el submit de `#absenceForm`, antes del POST:
```js
const inicio = new Date(document.getElementById('absenceFechaInicio').value);
const fin = new Date(document.getElementById('absenceFechaFin').value);
if (fin < inicio) {
  // showError en absenceFechaFin
  return;
}
```

### 6. Mover footer y mobile-nav de st-odo-09 al layout compartido
**Impacto: Alto (consistencia de arquitectura).** El footer con copyright y la `<nav class="mobile-nav">` están hardcodeados en la vista de perfil. Si el layout ya los incluye, se duplican. Si no los incluye, todas las demás vistas del odontólogo los pierden. Crear un partial `_MobileNav.cshtml` y `_Footer.cshtml` incluidos desde `_Layout`.

### 7. Añadir `tabindex="0"` a los tabpanels del modal de detalle en st-adm-07
**Impacto: Medio (accesibilidad WCAG 4.1.2).** Los tres `role="tabpanel"` necesitan ser enfocables:
```html
<div class="modal-tab-pane active" id="tabPaneGeneral"
     role="tabpanel" aria-labelledby="tabBtnGeneral" tabindex="0">
```

### 8. Reemplazar `onsubmit="return confirm()"` en credencial delete
**Impacto: Medio (accesibilidad + preparación CSP).** En `st-odo-09/index.cshtml`, el form de eliminación de credencial:
```html
<!-- Actual: -->
<form method="post" ... onsubmit="return confirm('¿Eliminar...');">

<!-- Mejor: añadir un data-attribute y manejar desde perfil.js -->
<form method="post" ... data-confirm="¿Eliminar esta credencial?">
```
Con un listener global en perfil.js que intercepte `submit` en forms con `data-confirm`.

### 9. Alinear colores de `badge-inactivo` entre gestion-modules.css y st-odo-09/styles.css
**Impacto: Medio (consistencia visual).** `gestion-modules.css` define `badge-inactivo` como rojo (`#fee2e2` / `#991b1b`). `st-odo-09/styles.css` lo define como gris neutro (`#f1f5f9` / `#64748b`). Decidir la semántica correcta (¿inactivo es error/peligro o simplemente neutral?) y unificar en `gestion-modules.css`.

### 10. Mover cálculos de porcentajes de st-odo-01 vista al Controller
**Impacto: Medio (separación de responsabilidades).** Los cinco `var pct* = totalCitas > 0 ? (int)Math.Round(...)` en la vista deben calcularse en el Controller y exponerse como `ViewData["PctAtendidas"]`, etc. La vista solo asigna el valor al `data-width`.

---

## CSS que puede eliminarse por redundancia con gestion-modules.css

### De `st-adm-07/styles.css` (~580 líneas eliminables)

Bloques que replican `gestion-modules.css` **byte a byte** (mismos selectores, mismos valores):

| Bloque | Líneas aprox. |
|---|---|
| `:root` variables base | 25 |
| `*, *::before, *::after`, `html`, `body`, `a`, `button`, `img` | 12 |
| `.skip-link`, `.visually-hidden` | 15 |
| `:focus-visible` | 4 |
| `.sidebar`, `.sidebar-logo`, `.logo-img`, `.sidebar-nav`, `.nav-item`, variantes | 55 |
| `.hamburger`, `.overlay` | 15 |
| `.main { margin-left... }`, `@keyframes fadeIn` | 8 |
| `.page-header`, `.breadcrumb`, `.page-title`, `.page-subtitle` | 20 |
| `.btn-primary`, `.btn-secondary` (versión simplificada) | 25 |
| `.stats-grid`, `.stat-card`, `.stat-number`, `.stat-label`, variantes data-color | 50 |
| `.filters-section`, `.filters-row`, `.search-wrap`, `.search-icon`, `.search-input`, `.filter-wrap`, `.status-select` | 60 |
| `.table-card`, `.table-wrap`, `.table-base` y thead/tbody | 45 |
| `.pagination`, `.pagination-buttons` | 25 |
| `.mobile-nav`, `.mobile-nav-item`, `.mobile-nav-icon` | 25 |
| `@media (max-width: 1100px)`, `(680px)`, `(480px)` para los anteriores | 30 |
| `@media print` | 10 |
| `@media (prefers-reduced-motion)` global | 8 |
| `.nav-group`, `.nav-group-header`, `.nav-arrow`, `.nav-group-items` | 20 |
| **Total** | **~457 líneas** |

Reglas a **conservar** en `st-adm-07/styles.css`: `.modal-overlay`, `.modal`, `.modal-close`, `.modal-title`, `.modal-footer`, `.modal-detail`, `.detail-*`, `.specialty-chip*`, `.specialty-options`, `.modal-tabs`, `.modal-tab-btn`, `.modal-tab-pane`, `.schedule-grid` (modal), `.schedule-row`, `.form-grid`, `.modal-lg`, `.modal-absence-*`, `.modal-confirm-delete-*`, `.badge-spec.*`, `.badge-status.*`, `.badge-vacaciones`, `.p-avatar`, `.p-avatar--color-*`, `.p-name`, `.td-profesional`, `.table-tabs` (si se mantiene la funcionalidad), `.action-btn`, `.btn-text`, `.credentials-review-card`, `@keyframes fadeInRow`.

### De `st-adm-14/styles.css` (~520 líneas eliminables)

Idéntico análisis. Las mismas secciones de sidebar, hamburger, main, header, botones, stats, filtros, tabla y paginación. Reglas a conservar: `.report-error`, `.alert-trigger`, `.report-alert-modal`, `.report-alert-backdrop`, `.report-alert-panel`, `.report-alert-header`, `.report-alert-kicker`, `.report-alert-close`, `.report-alert-body`, `.p-doc`, `.btn-success` (si no está en gestion-modules.css), `.stats-grid { grid-template-columns: repeat(3, 1fr) }` (3 columnas en lugar de 4).

### De `st-odo-09/styles.css` (~250 líneas eliminables)

Bloques a eliminar: `.sidebar` completo, `.hamburger`, `.overlay`, `.main { margin-left }`, `.page-header`, `.breadcrumb`, `.page-title`, `.page-subtitle`, `.skip-link`, `.visually-hidden`, `body`, `a`, `button`, `img`, `:root` variables base, `.nav-group*`. Reglas de perfil específicas a conservar: todo el resto.

---

## Mapa de archivos

<details>
<summary>Archivos revisados</summary>

| Archivo | Cambios / hallazgos |
|---|---|
| `Views/.../st-adm-07.../index.cshtml` | Tabs sin acción real, cálculos inline en tabla, hardcoded en credenciales-thead, TempData toast correcto |
| `Views/.../st-adm-14.../index.cshtml` | `hidden` boolean en atributo `selected` de options (patrón Razor correcto), modal alerta bien implementado |
| `Views/.../st-odo-01.../index.cshtml` | SVG sin aria, `hidden` booleano en notif-empty incorrecto, 18 ViewData bien usados, cálculos de % a mover al Controller |
| `Views/.../st-odo-09.../index.cshtml` | Footer/mobile-nav hardcodeados, form action incorrecto, `onsubmit confirm` bloqueante |
| `Views/.../st-adm-07.../servicios-asignados.cshtml` | Tabla sin paginación, región accesible mejorable |
| `wwwroot/css/shared/gestion-modules.css` | Fuente de verdad del design system; falta definir `--cita-*` variables usadas por dashboard |
| `wwwroot/css/.../st-adm-07.../styles.css` | ~580 líneas duplicadas de gestion-modules.css |
| `wwwroot/css/.../st-adm-14.../styles.css` | ~520 líneas duplicadas de gestion-modules.css |
| `wwwroot/css/.../st-odo-01.../styles.css` | Usa tokens `--space-*`, `--type-*` no definidos en gestion-modules.css |
| `wwwroot/css/.../st-odo-09.../styles.css` | ~250 líneas duplicadas; `badge-inactivo` con color distinto al de gestion-modules |
| `wwwroot/js/.../st-adm-07.../app.js` | Tabs no conectados, ausencias sin validación fecha, renderScheduleDays usa estilos inline |
| `wwwroot/js/.../st-adm-14.../reportes.js` | Correcto; delega filtrado al servidor; modal de alerta con focus-trap bien implementado |
| `wwwroot/js/.../st-odo-01.../app.js` | Auto-refresh, countdown, animaciones — correcto; renderRevenueChart sin fallback cuando `readRevenueData` lanza |
| `wwwroot/js/.../st-odo-09.../perfil.js` | Endpoints inconsistentes en form de contraseña; lógica de horario robusta |

> Diff de referencia: cambios locales vs. rama principal. Ejecutar `git diff main --stat` para ver el detalle completo.

</details>
