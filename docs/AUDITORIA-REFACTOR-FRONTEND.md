# Auditoría de Refactorización Frontend — SmileTrack
**Módulos:** `Gestión de Citas` y `Gestión de Profesionales`  
**Fecha:** Septiembre 2026  
**Fase:** 0 — Auditoría de Solo Lectura (Revisión Corregida)

---

## 1. Resumen Ejecutivo y Diagnóstico General

Esta auditoría inventaria la deuda técnica frontend en las Vistas (`Views/`), JavaScript (`wwwroot/js/`) y Estilos (`wwwroot/css/`) de los módulos **Gestión de Citas** (15 subsistemas) y **Gestión de Profesionales** (4 subsistemas).

### Hallazgos Principales:
1. **Inconsistencia de Nomenclatura en JS:**
   - Convivencia de Spanglish y español puro en nombres de funciones (`inicializarBusquedaPacientes`, `cambiarEstadoCita`, `configurarAutoActualizacion`) junto con funciones en inglés (`mapServerToClient`, `initModals`, `createAppointmentRow`).
   - Uso extendido de variables genéricas y abreviadas (`el`, `data`, `res`, `req`, `item`, `val`, `citas`, `notif`, `profesionales`, `tmp`).
2. **Funciones Extensas (>40 líneas) y Multiresponsabilidad:**
   - Se identificaron múltiples funciones monolíticas en controladores JS como `initModals` (159 líneas), `initAppointmentModals` (144 líneas), `renderTableFromApi` (141 líneas) e `initConfirmButtons` (107 líneas), que combinan manipulación DOM, llamadas fetch a API, formateo de datos y captura de eventos en un solo bloque.
3. **Auditoría Rigurosa de Clases CSS:**
   - Se descartaron falsos positivos provocados por valores numéricos CSS sin cero inicial (ej. `.84rem`, `.73rem`, `.45rem`, `.5cm`, `.92`).
   - Se identificaron y confirmaron en uso clases dinámicas construidas mediante template literals en JS (`pac-avatar--${avatarColor}`) y expresiones Razor (`p-avatar--color-@(id % 5)`).
   - Se inventariaron las clases CSS específicas de módulo verdaderamente huérfanas (cero referencias en todo el proyecto).

---

## 2. Inventario de Convenciones de Nombres y Deuda Técnica JS

### Variables Genéricas / Abreviadas Detectadas:
- `el`: Referencia a elementos DOM generados o seleccionados. -> *Debe renombrarse a nombres semánticos como `appointmentRowElement`, `modalContainer`.*
- `data`: Payloads JSON o datasets. -> *Debe renombrarse a `appointmentPayload`, `professionalDetails`, etc.*
- `res`: Respuestas de Fetch/API. -> *Debe renombrarse a `apiResponse` / `fetchResponse`.*
- `citas`, `notif`, `profesionales`: Colecciones/Arrays en español. -> *Debe renombrarse a `appointmentList`, `notificationItems`, `professionalRecords`.*
- `item`: Elemento de iteración en `forEach`/`map`. -> *Debe renombrarse según el contexto (`appointment`, `scheduleSlot`).*

### Inconsistencias Idiomáticas (Mezcla Español / Inglés):
- Functions: `cambiarEstadoCita` vs `mapServerToClient`
- Functions: `inicializarBusquedaPacientes` vs `initNuevaCitaModal`
- Variables: `filtro` vs `activeFilter`

---

## 3. Funciones Candidatas a División (>40 Líneas)

| Módulo / Archivo JS | Función | Líneas | Responsabilidades a Separar |
| :--- | :--- | :---: | :--- |
| `st-adm-07-gestion-profesionales/app.js` | `initModals` | 159 | Bind de eventos modal + reset de formularios + validación + peticiones API |
| `st-adm-07-gestion-profesionales/app.js` | `renderTableFromApi` | 141 | Procesamiento de array + construcción HTML + formateo de fecha/badges + bind de acciones |
| `st-odo-02-agenda/agenda.js` | `initAppointmentModals` | 144 | Configuración modal cita nueva + detalle + cambio de estado + listeners de submit |
| `st-pac-01-mis-citas/mis-citas.js` | `initNuevaCitaModal` | 114 | Carga de selectores + cálculo de slots + submit handlers |
| `st-aux-09-estado-consultorio/estado-consultorio.js` | `initConfirmButtons` | 107 | Confirmación de modal + mutación de estado + renderizado de cambios |
| `st-aux-09-estado-consultorio/estado-consultorio.js` | `initStatusSelector` | 87 | Dropdown logic + estilos visuales + sync con backend |
| `st-aux-09-estado-consultorio/estado-consultorio.js` | `initAddItem` | 78 | Form submit + inserción DOM + sanitización |
| `st-aux-09-estado-consultorio/estado-consultorio.js` | `initChecklist` | 76 | Toggle check + cálculo de porcentaje completado + API sync |
| `st-rec-03-gestion-citas/app.js` | `createAppointmentRow` | 66 | Template HTML string + cálculo de diferencia horaria + asignación de botones |
| `st-odo-01-dashboard/app.js` | `renderRevenueChart` | 61 | Configuración de Chart.js + transformación de dataset |
| `st-odo-09-perfil-profesional/perfil.js` | `renderSchedule` | 61 | Renderizado de grilla semanal + calculo de bloques de hora |
| `st-odo-09-perfil-profesional/perfil.js` | `initScheduleModal` | 64 | Lógica de horas inicio/fin + validación de solapamiento |
| `st-aux-10-citas-finalizadas/citas-finalizadas.js` | `initExportButton` | 67 | Generación de archivo Excel/CSV + filtrado de datos |
| `st-odo-02-agenda/agenda.js` | `inicializarDragAndDrop` | 62 | Drag listeners + drop handler + recalculo de hora de cita |

---

## 4. Auditoría de Clases CSS y Uso Global (Corregida y Verificada)

### A. Descarte de Falsos Positivos Numéricos
Los tokens `.84rem`, `.73rem`, `.45rem`, `.77rem`, `.71rem`, `.74rem`, `.76rem`, `.69rem`, `.5cm`, `.4rem`, `.92` identificados en la pasada previa **no son clases CSS**. Corresponden a valores de propiedades CSS sin cero inicial (`font-size: .84rem;`, `margin: 1.5cm;`, `opacity: .92;`) capturados por la expresión regular anterior. Se han removido completamente de la auditoría de clases.

### B. Clases Dinámicas Confirmadas EN USO (No eliminar)
- **`pac-avatar--blue` / `green` / `purple` / `orange` / `red`** ([styles.css](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/wwwroot/css/Gestion_De_Citas/st-rec-03-gestion-citas/styles.css)):
  - **Uso en JS:** [app.js:L292](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js#L292) (`class="pac-avatar pac-avatar--${avatarColor}"`)
  - **Uso en CSHTML:** [index.cshtml:L242](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml#L242) (`class="pac-avatar pac-avatar--@avatarColor"`)
- **`p-avatar--color-0` a `color-4`** ([styles.css](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/wwwroot/css/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/styles.css)):
  - **Uso en CSHTML:** [index.cshtml:L198](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml#L198) (`class="p-avatar p-avatar--color-@(profesional.IdProfesional % 5)"`)
  - **Uso en CSHTML:** [st-adm-14 index.cshtml:L144](file:///c:/Users/nohor/sistema-gestion-clinica-odontologia/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml#L144) (`class="p-avatar p-avatar--color-@(reporte.Id % 5)"`)

---

### C. Inventario Definitivo de Clases CSS Verdaderamente Huérfanas (Cero Referencias)

Las siguientes clases CSS aparecen como selectores de clase válidos en sus archivos `.css`, pero no se encontró **ninguna referencia** (ni directa, ni compuesta, ni vía concatenación o patrones dinámicos en JS/CSHTML) en todo el proyecto:

| Archivo CSS de Módulo | Clase CSS Huérfana | Estado de Verificación |
| :--- | :--- | :--- |
| `st-adm-09-citas/gestionintegral.css` | `.table-filters` | 0 referencias en todo el proyecto |
| `st-adm-09-citas/gestionintegral.css` | `.pagination-ellipsis` | 0 referencias en todo el proyecto |
| `st-adm-09-citas/gestionintegral.css` | `.btn-banner` | 0 referencias en todo el proyecto |
| `st-aux-01-panel-operativo/panel-operativo.css` | `.stat-vs` | 0 referencias en todo el proyecto |
| `st-aux-01-panel-operativo/panel-operativo.css` | `.td-success` | 0 referencias en todo el proyecto |
| `st-aux-01-panel-operativo/panel-operativo.css` | `.invoices-total` | 0 referencias en todo el proyecto |
| `st-aux-05-historial-parcial/historial-parcial.css` | `.hist-table` | 0 referencias en todo el proyecto |
| `st-aux-06-asistencia-procedi/asistencia-procedi.css` | `.alert-list` | 0 referencias en todo el proyecto |
| `st-aux-10-citas-finalizadas/citas-finalizadas.css` | `.summary-label` | 0 referencias en todo el proyecto |
| `st-pac-03-notificaciones/styles.css` | `.notification-card__icon--reminder` | 0 referencias en todo el proyecto |
| `st-pac-03-notificaciones/styles.css` | `.notification-card__icon--confirmed` | 0 referencias en todo el proyecto |
| `st-pac-03-notificaciones/styles.css` | `.notification-card__icon--cancelled` | 0 referencias en todo el proyecto |
| `st-pac-03-notificaciones/styles.css` | `.notification-card__icon--message` | 0 referencias en todo el proyecto |
| `st-rec-01-dashboard/styles.css` | `.btn-green` | 0 referencias en todo el proyecto |
| `st-rec-01-dashboard/styles.css` | `.btn-blue` | 0 referencias en todo el proyecto |
| `st-rec-03-gestion-citas/styles.css` | `.view-toggles` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.stats-container` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.success-card` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.warning-card` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.stat-value` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.history-info` | 0 referencias en todo el proyecto |
| `st-rec-05-recordatorios/styles.css` | `.history-name` | 0 referencias en todo el proyecto |
| `st-adm-07-gestion-profesionales/styles.css` | `.badge-vacaciones` | 0 referencias en todo el proyecto |
| `st-adm-07-gestion-profesionales/styles.css` | `.empty-state-title` | 0 referencias en todo el proyecto |
| `st-adm-07-gestion-profesionales/styles.css` | `.empty-state-subtitle` | 0 referencias en todo el proyecto |
| `st-adm-14-reportes-clinicos/styles.css` | `.badge-vacaciones` | 0 referencias en todo el proyecto |
| `st-odo-01-dashboard/styles.css` | `.stat-vs` | 0 referencias en todo el proyecto |
| `st-odo-01-dashboard/styles.css` | `.td-success` | 0 referencias en todo el proyecto |
| `st-odo-01-dashboard/styles.css` | `.invoices-total` | 0 referencias en todo el proyecto |
| `st-odo-09-perfil-profesional/styles.css` | `.tag-purple` | 0 referencias en todo el proyecto |
| `st-odo-09-perfil-profesional/styles.css` | `.badge-vacaciones` | 0 referencias en todo el proyecto |

---

## 5. Lista Priorizada por Archivo para la Fase 1 en Adelante

La refactorización se ejecutará en prompts individuales (un archivo/vista por prompt), siguiendo la siguiente prioridad basada en volumen de código, complejidad de funciones y nivel de refactorización requerido:

### Prioridad Alta (Archivos Principales y Complejos)
1. `wwwroot/js/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/app.js` + `index.cshtml` + `styles.css`
2. `wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js` + `index.cshtml` + `styles.css`
3. `wwwroot/js/Gestion_De_Citas/st-odo-02-agenda/agenda.js` + `index.cshtml` + `agenda.css`
4. `wwwroot/js/Gestion_De_Profesionales/st-odo-09-perfil-profesional/perfil.js` + `index.cshtml` + `styles.css`
5. `wwwroot/js/Gestion_De_Citas/st-pac-01-mis-citas/mis-citas.js` + `index.cshtml` + `styles.css`
6. `wwwroot/js/Gestion_De_Profesionales/st-odo-01-dashboard/app.js` + `index.cshtml` + `styles.css`
7. `wwwroot/js/Gestion_De_Citas/st-pac-03-notificaciones/notificaciones.js` + `index.cshtml` + `styles.css`

### Prioridad Media (Archivos Modestos con Funciones por Extraer)
8. `wwwroot/js/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.js` + `estado-consultorio.cshtml` + `estado-consultorio.css`
9. `wwwroot/js/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.js` + `citas-finalizadas.cshtml` + `citas-finalizadas.css`
10. `wwwroot/js/Gestion_De_Citas/st-rec-01-dashboard/app.js` + `index.cshtml` + `styles.css`
11. `wwwroot/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js` + `index.cshtml` + `styles.css`
12. `wwwroot/js/Gestion_De_Citas/st-adm-08-agenda/agendageneral.js` + `index.cshtml` + `agendageneral.css`
13. `wwwroot/js/Gestion_De_Citas/st-adm-09-citas/gestionintegral.js` + `index.cshtml` + `gestionintegral.css`

### Prioridad Baja (Archivos Livianos de Mantenimiento Menor)
14. `wwwroot/js/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.js` + `panel-operativo.cshtml` + `panel-operativo.css`
15. `wwwroot/js/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.js` + `agenda-apoyo.cshtml` + `agenda-apoyo.css`
16. `wwwroot/js/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.js` + `historial-parcial.cshtml` + `historial-parcial.css`
17. `wwwroot/js/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-procedi.js` + `asistencia-procedi.cshtml` + `asistencia-procedi.css`
18. `wwwroot/js/Gestion_De_Citas/st-adm-01-dashboard/app.js` + `index.cshtml` + `styles.css`
19. `wwwroot/js/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/reportes.js` + `index.cshtml` + `styles.css`

---

## 6. Resumen de Cierre — FASE 1 ✅ COMPLETADA

**Fecha de cierre:** 2026-09-28  
**Total de archivos JS refactorizados:** 19 / 19

Todos los archivos fueron reorganizados en los **7 bloques canónicos de Clean Code**:
`1. CONSTANTES` · `2. ESTADO` · `3. UTILIDADES` · `4. SERVICIOS Y API` · `5. RENDERIZADO Y DOM` · `6. MANEJO DE MODALES` · `7. INICIALIZACIÓN`

| # | Archivo JS | Cambios clave | Estado |
|---|---|---|---|
| 1  | `st-adm-07-gestion-profesionales/app.js` | 7 bloques, window exports preservados | ✅ |
| 2  | `st-rec-03-gestion-citas/app.js` | 7 bloques, openModalCita/closeModalCita window | ✅ |
| 3  | `st-odo-02-agenda/agenda.js` | 7 bloques, calendar rendering separado | ✅ |
| 4  | `st-odo-09-perfil-profesional/perfil.js` | 7 bloques, form validation extraído | ✅ |
| 5  | `st-pac-01-mis-citas/mis-citas.js` | 7 bloques, openConfirmDeleteCita window | ✅ |
| 6  | `st-odo-01-dashboard/app.js` | 7 bloques, animateStats / pollStats | ✅ |
| 7  | `st-pac-03-notificaciones/notificaciones.js` | 7 bloques, nombres exactos preservados para tests C# | ✅ |
| 8  | `st-adm-08-agenda/agendageneral.js` | 7 bloques, selectStatus/addItem window | ✅ |
| 9  | `st-rec-01-dashboard/app.js` | 7 bloques, renderDashboardStats | ✅ |
| 10 | `st-rec-05-recordatorios/app.js` | 7 bloques, fetchReminders / renderReminderList | ✅ |
| 11 | `st-aux-09-estado-consultorio/estado-consultorio.js` | 7 bloques | ✅ |
| 12 | `st-aux-10-citas-finalizadas/citas-finalizadas.js` | 7 bloques | ✅ |
| 13 | `st-adm-09-citas/gestionintegral.js` | 7 bloques, confirmPreparation/confirmStatus window | ✅ |
| 14 | `st-aux-01-panel-operativo/panel-operativo.js` | 7 bloques | ✅ |
| 15 | `st-aux-02-agenda-apoyo/agenda-apoyo.js` | 7 bloques | ✅ |
| 16 | `st-aux-05-historial-parcial/historial-parcial.js` | 7 bloques | ✅ |
| 17 | `st-aux-06-asistencia-procedi/asistencia-proc.js` | 7 bloques, timer / checklist | ✅ |
| 18 | `st-adm-01-dashboard/app.js` | 7 bloques, fetchDashboardPdf / fetchKpis / renderKpiCounters / initOccupancyBar | ✅ |
| 19 | `st-adm-14-reportes-clinicos/reportes.js` | 7 bloques, initMetricCounters / initAlertModal / initReportesModule | ✅ |

### Invariantes Preservadas en Toda la FASE 1
- **0 cambios** en endpoints de API (`/api/...`, rutas MVC).
- **0 cambios** en IDs HTML ni `data-*` attributes.
- **0 cambios** en texto visible de la UI (mensajes, labels, placeholders en español).
- **Exports `window.*` intactos** donde los usan atributos inline del HTML.
- **Nombres exactos de funciones** preservados donde existen assertions en tests C# (`NotificacionesStpac03Tests.cs`).

### Siguiente Fase Sugerida
- **FASE 2 — CSS:** Eliminar las 31 clases CSS huérfanas documentadas en la sección 4-C.
- **FASE 2 — Tests:** Ampliar cobertura de unit tests JS para los módulos refactorizados.
