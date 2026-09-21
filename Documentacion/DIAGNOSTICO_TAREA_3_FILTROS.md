# DIAGNÓSTICO TAREA 3 — Estandarizar Barra de Filtros (.filtros-bar)

**Proyecto:** SmileTrack (ASP.NET Core MVC 9.0)  
**Fecha:** 16 de Septiembre de 2026  
**Alcance:** 19 vistas de Gestión de Citas y Gestión de Profesionales

---

## 📊 Resumen Ejecutivo

### Resultado Preliminar: ⚠️ **ESTANDARIZACIÓN PARCIAL**

**De 19 vistas auditadas:**
- ✅ **7 vistas** ya tienen `.filtros-bar` implementada correctamente
- ⚠️ **12 vistas** tienen filtros con patrones diferentes o sin barra estandarizada
- ✅ Patrón estándar ya existe en `wwwroot/css/shared/filter-components.css`

---

## 🎯 Patrón Estándar Detectado

### Estructura HTML Correcta:

```html
<section class="filtros-bar" role="toolbar" aria-label="Filtros de...">
  <!-- Búsqueda -->
  <div class="search-wrap">
    <span class="material-symbols-outlined search-icon" aria-hidden="true">search</span>
    <input type="search" class="search-input" placeholder="Buscar..." />
  </div>

  <!-- Dropdowns custom -->
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilter..." 
            aria-haspopup="true" aria-expanded="false">
      <span>Texto del filtro</span>
      <span class="dd-arrow" aria-hidden="true">▼</span>
    </button>
    <div class="dropdown-menu" id="filterMenu..." role="menu">
      <div class="dd-item active" role="menuitem" tabindex="0" data-value="">Todos</div>
      <div class="dd-item" role="menuitem" tabindex="0" data-value="...">Opción 1</div>
    </div>
  </div>

  <!-- Backing stores ocultos -->
  <select id="filter..." class="visually-hidden" tabindex="-1">
    ...
  </select>
</section>
```

### CSS Estándar (filter-components.css):

```css
.filtros-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  background: var(--bg-secondary, #f8f9fa);
  border-radius: 8px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}
```

---

## 🔍 Auditoría Detallada por Vista

### Módulo: Gestión de Citas (15 vistas)

#### ✅ Vista 1: st-adm-09-citas
**Archivo:** `Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml`  
**Línea:** 123  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar` con patrón estandarizado

**Código detectado:**
```cshtml
<section class="filtros-bar" aria-label="Filtros de búsqueda" data-i18n-aria-label="adm09_filters_aria">
  <form method="get" class="filters-row">
    <div class="search-wrap">
      <span class="material-symbols-outlined search-icon" aria-hidden="true">search</span>
      <input type="search" id="searchAppointments" name="search" class="search-input"
             placeholder="Buscar paciente..." />
    </div>
    <div class="dropdown-wrap">
      <button type="button" class="filter-btn" id="btnFilterStatus">...</button>
    </div>
    <div class="dropdown-wrap">
      <button type="button" class="filter-btn" id="btnFilterProfessional">...</button>
    </div>
    <div class="filter-wrap">
      <input type="date" id="filterDate" name="fecha" class="status-select" />
    </div>
  </form>
</section>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ `.search-wrap` con icono Material Symbols
- ✅ `.dropdown-wrap` para filtros
- ✅ Input de fecha con `.status-select`
- ✅ Backing stores ocultos con `visually-hidden`
- ✅ Botones de acción (Aplicar/Limpiar)

---

#### ✅ Vista 2: st-adm-08-agenda
**Archivo:** `Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml`  
**Línea:** 53  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<div class="filtros-bar" role="toolbar" aria-label="Filtros de agenda">
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterProfessional">...</button>
  </div>
</div>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ Dropdown custom para profesional
- ✅ Patrón correcto

---

#### ✅ Vista 3: st-odo-02-agenda
**Archivo:** `Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml`  
**Línea:** 111  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<div class="filtros-bar" role="toolbar" aria-label="Filtros de agenda">
  <div class="search-patient-wrap">
    <span class="material-symbols-outlined">search</span>
    <input type="search" id="searchPatient" placeholder="Buscar paciente..." />
  </div>
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterStatus">...</button>
  </div>
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterOffice">...</button>
  </div>
</div>
```

**Nota:** Usa `.search-patient-wrap` en lugar de `.search-wrap` (variante menor)

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ⚠️ `.search-patient-wrap` (debería ser `.search-wrap`)
- ✅ Dropdowns custom (estado y consultorio)
- ✅ Backing stores ocultos

---

#### ✅ Vista 4: st-aux-02-agenda-apoyo
**Archivo:** `Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml`  
**Línea:** 74  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<div class="filtros-bar" role="toolbar" aria-label="Filtros de agenda" data-i18n-aria-label="table.filter">
  <!-- Dropdown profesional accesible -->
  <div class="dropdown-wrap">...</div>
</div>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ Dropdown custom

---

#### ✅ Vista 5: st-pac-01-mis-citas
**Archivo:** `Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml`  
**Línea:** 79  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<div class="filtros-bar" role="toolbar" aria-label="Filtros de citas" data-i18n-aria-label="table.filter">
  <div class="search-wrap">
    <span class="search-icon material-symbols-outlined" aria-hidden="true">search</span>
    <input type="search" class="search-input" id="citasSearchInput" />
  </div>
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterStatus">...</button>
  </div>
</div>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ `.search-wrap` con icono
- ✅ Dropdown custom de estado
- ✅ Patrón correcto

---

#### ✅ Vista 6: st-rec-03-gestion-citas
**Archivo:** `Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml`  
**Línea:** 107  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<form method="get" class="filtros-bar" role="toolbar" aria-label="Filtros de citas">
  <div class="search-wrap">
    <span class="search-icon" aria-hidden="true">🔍</span>
    <input type="search" class="search-input" id="searchPatient" name="search" />
  </div>
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterProfessional">...</button>
  </div>
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterStatus">...</button>
  </div>
  <div class="filter-wrap">
    <input type="date" class="status-select" id="filterDate" name="fecha" />
  </div>
</form>
```

**Nota:** Usa emoji 🔍 en lugar de Material Symbols (inconsistencia menor)

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ⚠️ Emoji en lugar de icono Material Symbols
- ✅ Tres dropdowns custom
- ✅ Input de fecha
- ✅ Backing stores ocultos

---

#### ⚠️ Vista 7: st-adm-01-dashboard
**Archivo:** `Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN** - No se encontró `.filtros-bar`

**Motivo:** Dashboard sin filtros (solo métricas y gráficos)  
**Acción requerida:** ✅ **No aplicable** - Dashboard no requiere filtros

---

#### ⚠️ Vista 8: st-aux-01-panel-operativo
**Archivo:** `Views/Gestion_De_Citas/st-aux-01-panel-operativo/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN** - No se encontró `.filtros-bar`

**Motivo:** Panel operativo sin filtros (métricas en tiempo real)  
**Acción requerida:** ✅ **No aplicable** - Panel no requiere filtros

---

#### ⚠️ Vista 9: st-aux-05-historial-parcial
**Archivo:** `Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml`  
**Estado:** ⚠️ **REQUIERE ESTANDARIZACIÓN**

**Código actual (líneas 56-81):**
```cshtml
<div class="header-actions">
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnPacienteSelect">...</button>
  </div>
  <label for="pacienteSelect" class="visually-hidden">Seleccionar paciente</label>
  <select id="pacienteSelect" class="visually-hidden">...</select>
</div>
```

**Problema:** El dropdown está en `.header-actions` del `<header class="page-header">`, NO en `.filtros-bar`

**Acción requerida:** ⚠️ **MOVER** dropdown a una `.filtros-bar` independiente fuera del header

---

#### ⚠️ Vista 10: st-aux-06-asistencia-procedi
**Archivo:** `Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml`  
**Estado:** ⚠️ **REQUIERE ESTANDARIZACIÓN**

**Código actual (líneas 52-75):**
```cshtml
<div class="header-actions">
  <div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnCitaSelect">...</button>
  </div>
  <label for="citaSelect" class="visually-hidden">Seleccionar cita</label>
  <select id="citaSelect" class="visually-hidden">...</select>
</div>
```

**Problema:** El dropdown está en `.header-actions` del header, NO en `.filtros-bar`

**Acción requerida:** ⚠️ **MOVER** dropdown a una `.filtros-bar` independiente

---

#### ⚠️ Vista 11: st-aux-09-estado-consultorio
**Archivo:** `Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Acción requerida:** 🔍 **Revisar** si necesita filtros o selector

---

#### ⚠️ Vista 12: st-aux-10-citas-finalizadas
**Archivo:** `Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Acción requerida:** 🔍 **Revisar** si necesita filtros

---

#### ⚠️ Vista 13: st-pac-03-notificaciones
**Archivo:** `Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Acción requerida:** 🔍 **Revisar** si necesita filtros de notificaciones

---

#### ⚠️ Vista 14: st-rec-01-dashboard
**Archivo:** `Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Motivo:** Dashboard de recepcionista (métricas)  
**Acción requerida:** ✅ **No aplicable** - Dashboard no requiere filtros

---

#### ⚠️ Vista 15: st-rec-05-recordatorios
**Archivo:** `Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Acción requerida:** 🔍 **Revisar** si necesita filtros de recordatorios

---

### Módulo: Gestión de Profesionales (4 vistas)

#### ✅ Vista 1: st-adm-07-gestion-profesionales
**Archivo:** `Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml`  
**Línea:** 97  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar` con patrón estandarizado

**Código detectado:**
```cshtml
<section class="filtros-bar" aria-label="Filtros de búsqueda" data-i18n-aria-label="gestionprofesionales_filters_aria">
  <form method="get" class="filters-row">
    <div class="search-wrap">
      <span class="material-symbols-outlined search-icon" aria-hidden="true">search</span>
      <input type="search" id="searchInput" name="search" class="search-input"
             placeholder="Buscar por nombre, especialidad o registro..." />
    </div>
    <div class="dropdown-wrap">
      <button type="button" class="filter-btn" id="btnFilterSpecialty">...</button>
    </div>
    <div class="dropdown-wrap">
      <button type="button" class="filter-btn" id="btnFilterStatus">...</button>
    </div>
  </form>
</section>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ `.search-wrap` con icono Material Symbols
- ✅ Dos dropdowns custom (especialidad y estado)
- ✅ Backing stores ocultos
- ✅ Formulario GET para filtros

---

#### ✅ Vista 2: st-adm-14-reportes-clinicos
**Archivo:** `Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml`  
**Línea:** 76  
**Estado:** ✅ **CORRECTO** - Ya usa `.filtros-bar`

**Código detectado:**
```cshtml
<section class="filtros-bar" aria-label="Filtros de búsqueda" data-i18n-aria-label="reportesclinicos_filters_aria">
  <form method="get" class="filters-row">
    <div class="search-wrap">...</div>
  </form>
</section>
```

**Elementos:**
- ✅ `.filtros-bar` como contenedor
- ✅ Formulario GET con búsqueda

---

#### ⚠️ Vista 3: st-odo-01-dashboard
**Archivo:** `Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Motivo:** Dashboard del profesional (métricas personales)  
**Acción requerida:** ✅ **No aplicable** - Dashboard no requiere filtros

---

#### ⚠️ Vista 4: st-odo-09-perfil-profesional
**Archivo:** `Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml`  
**Estado:** ⚠️ **REQUIERE REVISIÓN**

**Motivo:** Perfil del profesional (formulario de edición)  
**Acción requerida:** ✅ **No aplicable** - Perfil no requiere filtros

---

## 📊 Estadísticas de Auditoría

### Gestión de Citas (15 vistas):
- ✅ **Con `.filtros-bar` correcta:** 6 vistas
- ⚠️ **Requieren estandarización:** 2 vistas (st-aux-05, st-aux-06)
- 🔍 **Requieren revisión:** 4 vistas (st-aux-09, st-aux-10, st-pac-03, st-rec-05)
- ✅ **No aplicable (dashboards):** 3 vistas (st-adm-01, st-aux-01, st-rec-01)

### Gestión de Profesionales (4 vistas):
- ✅ **Con `.filtros-bar` correcta:** 2 vistas
- ✅ **No aplicable (dashboard/perfil):** 2 vistas (st-odo-01, st-odo-09)

### Total General:
- ✅ **Correctas:** 8/19 vistas (42%)
- ⚠️ **Requieren cambios:** 2/19 vistas (11%)
- 🔍 **Requieren revisión:** 4/19 vistas (21%)
- ✅ **No aplicable:** 5/19 vistas (26%)

---

## ⚠️ Inconsistencias Detectadas

### 1. Variantes de `.search-wrap`
**Vista afectada:** st-odo-02-agenda  
**Código actual:** `.search-patient-wrap`  
**Debe ser:** `.search-wrap`

### 2. Iconos inconsistentes
**Vistas afectadas:**
- st-rec-03-gestion-citas usa emoji 🔍
- Otras vistas usan `<span class="material-symbols-outlined">search</span>`

**Estándar:** Material Symbols

### 3. Dropdowns en header
**Vistas afectadas:**
- st-aux-05-historial-parcial
- st-aux-06-asistencia-procedi

**Problema:** Dropdown en `.header-actions` dentro de `<header class="page-header">`  
**Debe estar:** En `.filtros-bar` fuera del header

---

## 🎯 Plan de Estandarización

### Cambios Mínimos Requeridos:

#### 1. st-odo-02-agenda
- Renombrar `.search-patient-wrap` → `.search-wrap`

#### 2. st-rec-03-gestion-citas
- Cambiar emoji 🔍 → `<span class="material-symbols-outlined">search</span>`

#### 3. st-aux-05-historial-parcial
- Mover dropdown de `.header-actions` → nueva `.filtros-bar` debajo del header

#### 4. st-aux-06-asistencia-procedi
- Mover dropdown de `.header-actions` → nueva `.filtros-bar` debajo del header

#### 5-8. Vistas por revisar:
- st-aux-09-estado-consultorio
- st-aux-10-citas-finalizadas
- st-pac-03-notificaciones
- st-rec-05-recordatorios

**Acción:** Leer archivos para determinar si necesitan filtros

---

## ✅ Componentes Compartidos Disponibles

### CSS: `wwwroot/css/shared/filter-components.css`
- ✅ `.filtros-bar` - Contenedor principal
- ✅ `.search-wrap` - Wrapper de búsqueda
- ✅ `.search-icon` - Icono de búsqueda
- ✅ `.search-input` - Input de búsqueda
- ✅ `.dropdown-wrap` - Wrapper de dropdown
- ✅ `.filter-btn` - Botón de dropdown
- ✅ `.dropdown-menu` - Menú desplegable
- ✅ `.dd-item` - Item del menú
- ✅ `.dd-arrow` - Flecha indicadora
- ✅ `.dropdown-search` - Búsqueda dentro del dropdown
- ✅ `.dd-items-scroll` - Scroll de items

### JavaScript: `wwwroot/js/shared/dropdown-filters.js`
- ✅ Toggle de dropdowns
- ✅ Sincronización con backing stores
- ✅ Filtrado de items
- ✅ Eventos custom (`dropdown-change`)
- ✅ Accesibilidad ARIA

---

## 🎯 Siguiente Paso

**Prioridad ALTA:**
1. ✅ Leer las 4 vistas pendientes de revisión
2. ⚠️ Implementar cambios en 2 vistas (st-aux-05, st-aux-06)
3. ⚠️ Corregir inconsistencias menores (2 vistas)

**Total de cambios estimados:** 4-6 vistas

---

**Diagnóstico completado por:** Kiro AI Assistant  
**Fecha:** 16 de Septiembre de 2026  
**Estado:** ⚠️ 8/19 vistas correctas - Requiere estandarización de 2-6 vistas
