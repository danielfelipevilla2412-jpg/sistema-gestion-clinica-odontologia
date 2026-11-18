# Auditoría de Vistas — Módulo de Gestión de Citas (SmileTrack MVC)

Revisión exhaustiva de las 15 vistas CSHTML, 14 CSS de módulo y sus JS asociados contra el design system base `gestion-modules.css`. El módulo abarca cuatro roles (Administración, Recepción, Odontólogo, Paciente/Auxiliar) y mantiene buena coherencia estructural, pero acumula una cantidad significativa de CSS redundante y varios problemas de accesibilidad y validación que se documentan a continuación.

**Watch for:**
- **CSS duplicado masivo** (confirmed): todos los CSS de módulo re-declaran el bloque completo de variables `:root` y reset de `body`/`html` que ya viven en `gestion-modules.css`, más el sidebar completo con `linear-gradient`. En algunas vistas esto suma 300–500 líneas de CSS sin efecto. Es el cambio con mayor retorno en mantenibilidad.
- **Fuga de foco en modales** (confirmed): `st-rec-01-dashboard` omite el atributo `inert` en sus modales (el JS maneja el foco, pero el HTML no tiene el atributo de respaldo), mientras que las demás vistas sí lo usan correctamente.
- **Textos sin `data-i18n`** (confirmed): `st-citas-finalizadas`, `st-aux-01-panel-operativo` y `st-rec-05-recordatorios` tienen varios textos estáticos en español sin atributo de internacionalización, en contraste con el resto del módulo.
- **`stat-card[data-color="other"]`** (confirmed): `st-adm-01-dashboard` usa `data-color="other"` para la tarjeta "Otros estados", pero ese valor no tiene definición de color en `gestion-modules.css` ni en el CSS del módulo, por lo que el número aparece sin coloración.

**Verdict**: NEEDS_CHANGES

---

## Vista de alto nivel

El módulo sigue `Layout="_Layout"` + `ViewData["UseGestionModulesDesignSystem"]=true` de forma uniforme en las 15 vistas. La consistencia estructural (page-header, stats-grid, table-wrap, paginación, modales con `inert`) es alta en las vistas de administración y recepción; baja en las auxiliares más antiguas.

La duplicación de CSS es el problema de mayor superficie. Cada CSS de módulo abre con un bloque `:root { ... }` que redefine las mismas 20+ variables ya declaradas en `gestion-modules.css`, seguido de un reset completo y, en al menos seis módulos, el sidebar entero. Cambiar `--primary` requiere editar 15 archivos. Los módulos `panel-operativo.css` y `styles.css` de adm-01 incluyen el comentario explícito "IDÉNTICO A st-odo-01", confirmando que la duplicación fue intencional como copia de base.

Las vistas de roles auxiliar y paciente tienen el mayor déficit de internacionalización: stat-subs, notas del sistema y cadenas construidas en Razor sin clave `data-i18n`. `st-odo-02` omite además las clases `form-label`/`form-input` del design system en su modal de nueva cita, lo que produce apariencia visual diferente a los formularios equivalentes en los otros tres roles.

`st-rec-01-dashboard` construye sus modales desde JS sin plantilla HTML inicial, por lo que el atributo `inert` depende de que el JS lo aplique. Si el script falla, Tab puede alcanzar elementos del modal oculto. Es la única vista del módulo sin cobertura de respaldo en foco de teclado.

`st-pac-01-mis-citas` tiene cinco tarjetas KPI en un `stats-grid` de cuatro columnas: en móvil (≤ 750 px) la quinta queda sola en su fila. Dos de las cinco usan `data-color="red"`, eliminando diferenciación visual entre "Canceladas" y "Solicitudes rechazadas".

---

<details>
<summary>Issues (19)</summary>

1. **`:root` duplicado en todos los CSS de módulo** — Las 14 hojas CSS de módulo re-declaran el bloque completo de variables CSS ya definido en `gestion-modules.css`. Eliminar el bloque `:root` y el reset de `body`/`html` de cada CSS de módulo; sólo mantener variables exclusivas del módulo.

2. **Sidebar CSS duplicado en seis módulos** — `panel-operativo.css`, `styles.css` (adm-01), y otros cuatro CSS incluyen el `.sidebar`, `.nav-item`, `.sidebar-logo`, etc. en 100–150 líneas. El sidebar se renderiza desde el Layout/partial; estos estilos deben vivir en el CSS del layout compartido, no en cada módulo.

3. **`data-color="other"` sin color definido** — `st-adm-01-dashboard/index.cshtml` línea ~100: la tarjeta "Otros estados" usa `data-color="other"`, selector no cubierto en `gestion-modules.css`. El `.stat-number` aparece en el color texto por defecto. Añadir `data-color="other"` al design system o cambiar a un color existente (ej. `data-color="purple"`).

4. **Foco de teclado en modales de `st-rec-01-dashboard`** — Los modales se construyen íntegramente en JS sin plantilla HTML inicial. No hay garantía de que `inert` se aplique antes de que el usuario pueda accionar Tab. Añadir plantillas HTML de modal con `aria-hidden="true" inert` como en el resto del módulo, y activarlas desde JS.

5. **`st-pac-01-mis-citas`: cinco `stat-card` en un grid de cuatro columnas** — Línea ~65: cinco tarjetas con `data-color="red"` duplicado ("Canceladas" y "Solicitudes rechazadas"). En pantallas ≤ 750 px la quinta tarjeta queda sola. Usar cuatro tarjetas o aplicar `grid-template-columns: repeat(5, ...)` con breakpoint específico.

6. **`data-color="red"` duplicado en `st-pac-01`** — Dos tarjetas tienen `data-color="red"` (Canceladas y Solicitudes rechazadas). Usar `data-color="cancelled"` / `data-color="purple"` para diferenciarlas visualmente.

7. **Textos sin `data-i18n` en `st-citas-finalizadas`** — Líneas ~60–65: `<h1>Citas Finalizadas del Día</h1>` y la mayoría de las cadenas de la sección de header no tienen atributo `data-i18n`, mientras que las columnas de tabla sí lo tienen.

8. **Textos sin `data-i18n` en `st-aux-01-panel-operativo`** — `stat-sub` (p.ej. "Total registrados", "Programadas", "En servicio", "Solo citas atendidas"), la sección "Notas del sistema" y texto del chart no tienen atributos i18n, inconsistente con los KPIs del mismo módulo.

9. **Textos sin `data-i18n` en `st-rec-05-recordatorios`** — Cadenas como "Mañana", "Pasado mañana", "Enviados recientemente", y el conteo `@historialEnviados.Count recientes` están hardcodeados. El nombre del canal "Correo electrónico no disponible" tampoco tiene clave i18n.

10. **`<style>` inline en `st-rec-05-recordatorios`** — Al final del archivo hay un bloque `<style>` que define `.visually-hidden`. Esta clase ya existe en todos los CSS de módulo. Eliminar el bloque inline.

11. **`st-adm-08-agenda`: sección de disponibilidad sin `data-i18n`** — Líneas ~85–105 de `index.cshtml`: "Consultar disponibilidad", "Elige un servicio y horario...", "Fecha", "Hora", "Servicio", "Consultar", "Selecciona fecha, hora y servicio para consultar profesionales." No tienen atributos `data-i18n`.

12. **Script JS de `st-rec-01-dashboard` fuera del bloque `@section Scripts`** — Línea ~115: `<script src="~/js/.../app.js" defer></script>` está escrito con `Url.Content` directamente en el body en lugar de dentro de `@section Scripts { ... }` como el resto del módulo. Esto puede causar orden de carga inconsistente.

13. **Formularios en `st-odo-02-agenda` sin clases del design system** — Dentro del modal `#formNewAppointment` (líneas ~185–240), los `<label>` y `<input>` no tienen las clases `form-label`/`form-input` que el design system espera. Genera apariencia visual diferente a los mismos formularios en `st-adm-08` y `st-rec-03`.

14. **`st-adm-09-citas`: `stat-card` usa `<div>` en vez de `<article>`** — En esta vista las tarjetas KPI usan `<div class="stat-card">` mientras que `st-adm-01-dashboard` usa `<article class="stat-card">`. La inconsistencia es menor pero afecta la semántica de outlines en screen readers.

15. **`st-adm-09-citas`: flecha de asistencia siempre muestra `↓`** — Línea ~115: `<span class="stat-sub">↓ @statKpiTasaAsis%` — la flecha es fija independientemente del valor. Comparar contra un umbral para mostrar `↑` o `↓`.

16. **`st-aux-02-agenda-apoyo`: tabla sin scroll horizontal** — `<div class="table-scroll">` existe pero la clase `table-scroll` no está en `gestion-modules.css`; el CSS del módulo debería garantizar `overflow-x: auto`. Si el CSS no la define, en móvil la tabla desborda el viewport. Verificar que `agenda-apoyo.css` define `overflow-x: auto` para esta clase.

17. **`st-aux-06-asistencia-procedi`: `data-row` sin clases del design system** — Los bloques `<div class="data-row"><strong>Paciente:</strong>...` son estructuras ad-hoc sin clases de `gestion-modules.css`. No hay label asociado a los `<span id="apPaciente">`, lo que hace esos campos inaccesibles para lectores de pantalla cuando se pueblan dinámicamente.

18. **`st-rec-03-gestion-citas`: `btnNuevaCita` texto con emoji** — Línea ~45: `<span data-i18n="citas.new">➕ Nueva cita</span>`. El emoji `➕` está hardcodeado y no es manejado por Material Symbols. Usar `<span class="material-symbols-outlined" aria-hidden="true">add</span>` como en el resto del módulo.

19. **`st-rec-05-recordatorios`: enlace de Login en nav móvil** — El último item de la nav móvil enlaza a `/acceso-y-seguridad/login`. Un usuario autenticado que llega a esta página ya tiene sesión activa; este enlace es residual de desarrollo y confunde. Reemplazar por el enlace de perfil del recepcionista.

</details>

---

<details>
<summary>Detalles</summary>

## CSS duplicado masivo contra `gestion-modules.css`

Todos los CSS de módulo abren con un bloque `:root` de 20–25 variables seguido de reset completo de `body`/`html`. Los confirmados con sidebar duplicado (`panel-operativo.css`, `styles.css` de adm-01, `styles.css` de rec-05) suman ~260–300 líneas eliminables cada uno. El problema es arquitectural: cada módulo se creó copiando el CSS anterior. `gestion-modules.css` ya se carga primero vía `ViewData["UseGestionModulesDesignSystem"]`; cada CSS de módulo sólo debería añadir componentes exclusivos del módulo (`.timer-circle`, `.procedure-pill`, `.legend-grid`, etc.).

## `data-color="other"` sin cobertura en el design system

En `st-adm-01-dashboard/index.cshtml` (línea ~100), la quinta tarjeta de la sección "Distribución por estado" usa `data-color="other"`. El archivo `gestion-modules.css` cubre: `blue`, `green`, `attended`, `orange`, `red`, `cancelled`, `confirmed`, `purple`. El valor `other` no produce ningún border-left ni coloración de `.stat-number`. El número de "Otros estados" se muestra en el color de texto por defecto, sin la diferenciación visual que tienen las demás tarjetas.

## Semántica de modales y foco de teclado

Las vistas `st-adm-08`, `st-adm-09`, `st-odo-02`, `st-rec-03`, `st-pac-01` y `st-pac-03` usan `role="dialog" aria-modal="true" aria-labelledby="..." aria-hidden="true" inert` como estado cerrado del modal, el patrón robusto de WCAG 2.1.

`st-rec-01-dashboard` es la excepción: construye modales íntegramente desde JS sin plantilla HTML. El atributo `inert` no existe en el HTML inicial y sólo se aplica si el JS lo gestiona. Si el script falla o se carga fuera de orden, Tab alcanza los elementos del modal antes de que estén activos.

`st-pac-03-notificaciones` usa el carácter `×` como texto visible del botón de cierre del modal (`<button class="modal-close" ...>×</button>`). El `aria-label="Cerrar detalles"` está presente, pero NVDA y JAWS en modo de formulario pueden anunciar el carácter literal antes del aria-label. Cambiar al patrón `<span class="material-symbols-outlined" aria-hidden="true">close</span>` usado en el resto del módulo.

## Formularios sin clases del design system en `st-odo-02-agenda`

El modal de nueva cita en `st-odo-02-agenda/index.cshtml` (líneas ~185–240) usa `<label for="newApptDate">Fecha</label>` sin `class="form-label"`, e `<input type="date" ...>` sin `class="form-input"`. El resto de las vistas que tienen formularios similares (`st-adm-08`, `st-adm-09`, `st-rec-03`) sí aplican estas clases. El resultado es que los campos del formulario en `st-odo-02` pueden verse diferentes al depender del reset del browser en lugar del estilo del design system para los inputs.

## Internacionalización incompleta en vistas auxiliar

`st-aux-01-panel-operativo` tiene `data-i18n` en las `.stat-label` pero no en los `.stat-sub` ("Total registrados", "Programadas", "En servicio", "Solo citas atendidas"), la sección "Notas del sistema", ni el texto del chart de distribución de estados. Las mismas etiquetas en `st-adm-01-dashboard` sí tienen claves i18n equivalentes, creando una inconsistencia entre roles donde el mismo texto es traducible para administración pero no para auxiliar.

`st-rec-05-recordatorios` construye `diaTexto` en Razor: `string diaTexto = p.EsManana ? "Mañana" : "Pasado mañana"`. Esta cadena no pasa por el sistema i18n. Para internacionalizarla habría que exponer ambas cadenas como claves o calcular el offset de fecha en JS usando las claves `rec05.tomorrow` / `rec05.day_after_tomorrow`.

## Tabla de `st-aux-02-agenda-apoyo` sin scroll horizontal garantizado

La tabla `.agenda-table` está envuelta en `<div class="table-scroll">`. La clase `table-scroll` no existe en `gestion-modules.css`; debe estar definida en `agenda-apoyo.css`. Sin ver el CSS completo del módulo, la preocupación (possible) es que si esa clase no tiene `overflow-x: auto`, la tabla puede desbordar el viewport en móvil. Las demás vistas usan `.table-wrap` de `gestion-modules.css` que sí tiene `overflow-x: auto`. Homogeneizar al uso de `.table-wrap`.

## Flecha de asistencia estática en `st-adm-09-citas`

Línea ~115 de `st-adm-09-citas/index.cshtml`:
```html
<span class="stat-sub">↓ @statKpiTasaAsis% <span data-i18n="adm09_stat_attendance">asistencia</span></span>
```
El carácter `↓` está hardcodeado. `statKpiTasaAsis` es una tasa que puede aumentar o disminuir. En `st-adm-01-dashboard` (mismo módulo) este patrón se implementa correctamente con un bloque condicional que elige `↑`, `↓` o `→` según el valor de `variacion`. Aplicar el mismo patrón aquí.

## `st-rec-03-gestion-citas`: emoji y script JS en body

La apertura del botón "Nueva cita" (línea ~45) usa `➕ Nueva cita` como texto del span, mezclando emoji con el sistema de iconos. El emoji `➕` no sigue las guías de Material Symbols usadas en el resto del módulo y no es controlable via CSS.

El script `app.js` de `st-rec-01-dashboard` se carga con `<script src="@Url.Content(...)" defer></script>` directamente en el body, fuera del bloque `@section Scripts`. El resto del módulo usa `asp-append-version="true"` para cache-busting. Este script perderá el cache-busting y puede cargarse antes de que las dependencias compartidas estén disponibles dependiendo de cómo el Layout concatene los scripts.

## Accesibilidad en `st-aux-06-asistencia-procedi`

Los campos de datos de cita (`<div class="data-row"><strong>Paciente:</strong> <span id="apPaciente">—</span></div>`) no tienen asociación semántica entre la etiqueta y el valor dinámico. Cuando JS rellena el `<span>`, el contenedor padre `#procedureContent` no tiene `aria-live="polite"`, por lo que un lector de pantalla no anuncia el cambio. Usar `<dl>/<dt>/<dd>` o añadir `aria-live="polite"` al contenedor.

## Cobertura de test

No existen archivos de test visibles en el workspace para estas vistas. Los comportamientos sin cobertura identificada:
- Lógica de color dinámico de avatares (hash de nombre → índice de color array) — puede producir colores inesperados si el nombre tiene caracteres Unicode con valores de hash altos negativos tras `Math.Abs`.
- Transiciones de estado permitidas vía `EstadoCitaHelper.IsTransitionAllowed` — no hay test del mapeo en contexto de UI.
- Animación counter en `stat-number[data-target]` — sin test de que el valor final coincide con `data-target` tras la animación.

</details>

---

## 10 cambios más impactantes

Priorizados por retorno en mantenibilidad, accesibilidad y coherencia visual:

**1. Eliminar bloques `:root`, reset y sidebar duplicados de los 14 CSS de módulo** *(Alta — confirmado)* — Reduce el bundle CSS del módulo en estimados 1.500–2.500 líneas en total. Cambio mecánico, bajo riesgo de regresión.

**2. Corregir `data-color="other"` en `st-adm-01-dashboard`** *(Alta — confirmado)* — La tarjeta "Otros estados" no tiene coloración. Añadir a `gestion-modules.css`: `.main .stat-card[data-color="other"] { border-left-color: #94a3b8; }` y `.main .stat-card[data-color="other"] .stat-number { color: #94a3b8; }`.

**3. Mover el `<script>` de `st-rec-01-dashboard` a `@section Scripts`** *(Alta — confirmado)* — Cambiar de `Url.Content` sin cache-busting a `asp-append-version="true"` dentro del bloque section para garantizar orden de carga y actualización de cache.

**4. Aplicar clases `form-label`/`form-input` al modal de nueva cita en `st-odo-02-agenda`** *(Media — confirmado)* — Homogeniza la apariencia visual de los formularios entre los cuatro roles.

**5. Añadir `data-i18n` a los textos faltantes en `st-aux-01-panel-operativo`** *(Media — confirmado)* — Stat-subs, notas del sistema y chart labels. Patrón ya establecido en la misma vista para las stat-labels.

**6. Eliminar el bloque `<style>` inline de `st-rec-05-recordatorios`** *(Media — confirmado)* — `.visually-hidden` ya existe en el CSS del módulo. Este bloque es redundante y fragmenta los estilos.

**7. Corregir la quinta `stat-card` en `st-pac-01-mis-citas`** *(Media — confirmado)* — Reemplazar el segundo `data-color="red"` por `data-color="purple"`, y añadir un breakpoint `grid-template-columns: repeat(5, ...)` o combinar las dos tarjetas rojas en una sola "No completadas".

**8. Homogeneizar `.table-scroll` → `.table-wrap` en `st-aux-02-agenda-apoyo`** *(Media — possible)* — Garantiza el scroll horizontal en móvil vía la clase ya definida en `gestion-modules.css`.

**9. Corregir la flecha hardcodeada en `st-adm-09-citas`** *(Baja — confirmado)* — Aplicar el mismo patrón condicional Razor que ya usa `st-adm-01-dashboard` para `↑`/`↓`/`→`.

**10. Reemplazar el emoji `➕` en `st-rec-03-gestion-citas`** *(Baja — confirmado)* — Sustituir por `<span class="material-symbols-outlined" aria-hidden="true">add</span>` y envolver el texto en `<span data-i18n="citas.new">Nueva cita</span>`.

---

## CSS eliminable por redundancia con `gestion-modules.css`

Los bloques siguientes pueden eliminarse de cada CSS de módulo sin efecto visual, porque `gestion-modules.css` ya los define con valores idénticos o superiores:

```
En TODOS los CSS de módulo:
  :root { --sidebar-w, --bg, --white, --primary, --primary-light, --primary-dark,
          --green, --orange, --purple, --text, --border, --shadow, --shadow-md,
          --radius, --radius-sm, --radius-full, --font, --font-display, --tr,
          --danger, --input-focus-ring }

  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  html { font-size: 15px; scroll-behavior: smooth; }
  body { font-family: var(--font); background: var(--bg); color: var(--text); ... }
  a { text-decoration: none; color: inherit; }
  button { font-family: inherit; cursor: pointer; }
  img { max-width: 100%; height: auto; display: block; }

  .skip-link { ... }          ← en el Layout
  .visually-hidden { ... }    ← en gestion-modules.css (y en el Layout)
  :focus-visible { ... }      ← en gestion-modules.css
  @media (prefers-reduced-motion) { ... }  ← en gestion-modules.css

En panel-operativo.css, styles.css (adm-01), styles.css (rec-05) y otros con sidebar:
  .sidebar { ... }
  .sidebar-logo { ... }
  .sidebar-nav { ... }
  .nav-item { ... }
  .nav-separator { ... }
  .sidebar-user { ... }
  .user-avatar { ... }
  .user-info, .user-role, .user-email { ... }
```

Variables exclusivas de cada módulo que **sí deben mantenerse** (no están en `gestion-modules.css`):
- `--alert-red-bg`, `--alert-yellow-bg`, y variantes de alerta en `st-rec-05/styles.css`
- `--avatar-blue/green/amber/gray` en `st-rec-05/styles.css`
- `--invoice-warning-bg/text` en `st-adm-01/styles.css`
- Cualquier variable específica de componente que no sea una renombración de las variables del design system.

</details>

---

<details>
<summary>Mapa de archivos</summary>

| Archivo | Qué cambió en esta revisión |
|---|---|
| `Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml` | Uso de `data-color="other"` sin cobertura CSS |
| `Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml` | Sección de disponibilidad sin `data-i18n`; formulario correcto |
| `Views/Gestion_De_Citas/st-adm-08-agenda/_CalendarSection.cshtml` | Revisada; sin issues nuevos |
| `Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml` | Flecha de asistencia hardcodeada; `<div>` vs `<article>` en stat-cards |
| `Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml` | Textos sin `data-i18n` en stat-subs y notas |
| `Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml` | Clase `table-scroll` no estándar; posible falta de overflow-x |
| `Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml` | Campos de datos sin asociación semántica accesible |
| `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml` | Revisada; sin issues nuevos |
| `Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml` | Textos de header sin `data-i18n` |
| `Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml` | Formulario modal sin clases del design system |
| `Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml` | Quinta stat-card rompiendo grid; `data-color="red"` duplicado |
| `Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml` | Botón cierre modal usa `×` en vez de icono SVG |
| `Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml` | Script fuera de `@section Scripts`; modales construidos en JS sin plantilla HTML |
| `Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml` | Emoji `➕` hardcodeado en botón |
| `Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml` | Textos sin `data-i18n`; `<style>` inline redundante; link de Login en nav móvil |
| `wwwroot/css/Gestion_De_Citas/*/` (14 archivos) | `:root`, reset y sidebar duplicados en todos |
| `wwwroot/css/shared/gestion-modules.css` | Falta cobertura para `data-color="other"` |

Diff base: comparación manual contra rama actual (no hay PR número asociado).

</details>
