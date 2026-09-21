# DIAGNÓSTICO TAREA 2 — Migrar Selects Nativos a Dropdowns Custom

**Proyecto:** SmileTrack (ASP.NET Core MVC 9.0)  
**Fecha:** 16 de Septiembre de 2026  
**Alcance:** st-odo-02-agenda, st-aux-05-historial-parcial, st-aux-06-asistencia-procedi

---

## 📊 Resumen Ejecutivo

### Resultado: ✅ **TAREA YA COMPLETADA - NO REQUIERE CAMBIOS**

**Las 3 vistas auditadas YA TIENEN dropdowns custom implementados correctamente.**

---

## 🔍 Auditoría Detallada

### Vista 1: st-odo-02-agenda

**Archivo:** `Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml`

#### Elementos analizados:

**1. `#filterStatus` (Filtro por estado)**

✅ **DROPDOWN CUSTOM YA IMPLEMENTADO**

**Líneas 113-126:** Dropdown custom funcional
```cshtml
<div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterStatus" 
            aria-haspopup="true" aria-expanded="false" aria-controls="filterStatusMenu">
        <span>Todos los estados</span>
        <span class="dd-arrow" aria-hidden="true">▼</span>
    </button>
    <div class="dropdown-menu" id="filterStatusMenu" role="menu" aria-label="Filtrar por estado">
        <div class="dd-item active" role="menuitem" tabindex="0" data-value="">Todos los estados</div>
        <div class="dd-item" role="menuitem" tabindex="0" data-value="programada">Programadas (@(ViewData["CitasProgramadas"] ?? 0))</div>
        <div class="dd-item" role="menuitem" tabindex="0" data-value="confirmada">Confirmadas (@(ViewData["CitasConfirmadas"] ?? 0))</div>
        <div class="dd-item" role="menuitem" tabindex="0" data-value="atendida">Atendidas (@(ViewData["CitasAtendidas"] ?? 0))</div>
        <div class="dd-item" role="menuitem" tabindex="0" data-value="cancelada">Canceladas (@(ViewData["CitasCanceladas"] ?? 0))</div>
    </div>
</div>
```

**Líneas 143-149:** Select nativo como backing store (visually-hidden)
```cshtml
<select id="filterStatus" class="visually-hidden" aria-label="Filtrar por estado" tabindex="-1">
    <option value="">Todos los estados</option>
    <option value="programada">Programadas (@(ViewData["CitasProgramadas"] ?? 0))</option>
    <option value="confirmada">Confirmadas (@(ViewData["CitasConfirmadas"] ?? 0))</option>
    <option value="atendida">Atendidas (@(ViewData["CitasAtendidas"] ?? 0))</option>
    <option value="cancelada">Canceladas (@(ViewData["CitasCanceladas"] ?? 0))</option>
</select>
```

**Patrón implementado:**
- ✅ Dropdown custom visible con botón + menú
- ✅ Select nativo oculto con `class="visually-hidden"` y `tabindex="-1"`
- ✅ Sincronización bidireccional entre dropdown y select
- ✅ ARIA completo (haspopup, expanded, controls, role="menu/menuitem")

---

**2. `#filterOffice` (Filtro por consultorio)**

✅ **DROPDOWN CUSTOM YA IMPLEMENTADO**

**Líneas 128-141:** Dropdown custom funcional
```cshtml
<div class="dropdown-wrap">
    <button type="button" class="filter-btn" id="btnFilterOffice" 
            aria-haspopup="true" aria-expanded="false" aria-controls="filterOfficeMenu">
        <span>Todos los consultorios</span>
        <span class="dd-arrow" aria-hidden="true">▼</span>
    </button>
    <div class="dropdown-menu" id="filterOfficeMenu" role="menu" aria-label="Filtrar por consultorio">
        <div class="dd-item active" role="menuitem" tabindex="0" data-value="">Todos los consultorios</div>
        @foreach (var consultorio in Model.Consultorios)
        {
            <div class="dd-item" role="menuitem" tabindex="0" data-value="@consultorio.Id">@consultorio.Text</div>
        }
    </div>
</div>
```

**Líneas 151-158:** Select nativo como backing store
```cshtml
<select id="filterOffice" class="visually-hidden" aria-label="Filtrar por consultorio" tabindex="-1">
    <option value="">Todos los consultorios</option>
    @foreach (var consultorio in Model.Consultorios)
    {
        <option value="@consultorio.Id" selected="@(Model.OfficeId == consultorio.Id ? "selected" : null)">@consultorio.Text</option>
    }
</select>
```

**Patrón implementado:**
- ✅ Dropdown custom visible con datos dinámicos desde Model
- ✅ Select nativo oculto con `visually-hidden`
- ✅ Sincronización automática
- ✅ ARIA completo

---

### Vista 2: st-aux-05-historial-parcial

**Archivo:** `Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml`

#### Elemento analizado:

**`#pacienteSelect` (Selector de paciente)**

✅ **DROPDOWN CUSTOM CON BÚSQUEDA YA IMPLEMENTADO**

**Líneas 56-69:** Dropdown custom con búsqueda integrada
```cshtml
<div class="dropdown-wrap">
  <button type="button" class="filter-btn" id="btnPacienteSelect" 
          aria-haspopup="true" aria-expanded="false" aria-controls="pacienteDropdownMenu">
    <span>@pacienteSeleccionadoNombre</span>
    <span class="dd-arrow" aria-hidden="true">▼</span>
  </button>
  <div class="dropdown-menu" id="pacienteDropdownMenu" role="menu" aria-label="Seleccionar paciente">
    <input type="text" class="dropdown-search" placeholder="Buscar paciente..." aria-label="Buscar paciente" />
    <div class="dd-items-scroll">
      <div class="dd-item @(pacienteSeleccionadoId == 0 ? "active" : "")" role="menuitem" tabindex="0" 
           data-value="" data-search="Seleccionar paciente">Seleccionar paciente</div>
      @foreach (var paciente in pacientes.Where(p => p.Estado == "activo"))
      {
        var isSelected = paciente.IdPaciente == pacienteSeleccionadoId;
        <div class="dd-item @(isSelected ? "active" : "")" role="menuitem" tabindex="0" 
             data-value="@paciente.IdPaciente" data-search="@paciente.NombresCompleto">@paciente.NombresCompleto</div>
      }
    </div>
  </div>
</div>
```

**Líneas 72-80:** Select nativo como backing store
```cshtml
<label for="pacienteSelect" class="visually-hidden">Seleccionar paciente</label>
<select id="pacienteSelect" class="visually-hidden" aria-label="Seleccionar paciente" tabindex="-1">
  <option value="">Seleccionar paciente</option>
  @foreach (var paciente in pacientes.Where(p => p.Estado == "activo"))
  {
    var isSelected = paciente.IdPaciente == pacienteSeleccionadoId;
    <option value="@paciente.IdPaciente" selected="@(isSelected ? "selected" : null)">@paciente.NombresCompleto</option>
  }
</select>
```

**Patrón implementado:**
- ✅ Dropdown custom con **búsqueda integrada** (línea 63)
- ✅ Input de búsqueda dentro del dropdown-menu
- ✅ Atributo `data-search` en cada item para filtrado
- ✅ Scroll interno con `dd-items-scroll`
- ✅ Select nativo oculto como backing store
- ✅ Sincronización bidireccional con evento `dropdown-change` (líneas 172-186)

**✅ CUMPLE REQUISITO:** La TAREA 2 especificaba "agregar filtro por texto dentro del menú" - **YA IMPLEMENTADO**

---

### Vista 3: st-aux-06-asistencia-procedi

**Archivo:** `Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml`

#### Elemento analizado:

**`#citaSelect` (Selector de cita)**

✅ **DROPDOWN CUSTOM YA IMPLEMENTADO**

**Líneas 52-64:** Dropdown custom funcional
```cshtml
<div class="dropdown-wrap">
  <button type="button" class="filter-btn" id="btnCitaSelect" 
          aria-haspopup="true" aria-expanded="false" aria-controls="citaSelectMenu">
    <span>@(citaSeleccionadaId > 0 ? citasSelector.FirstOrDefault(c => c.IdCita == citaSeleccionadaId)?.FechaHora.ToString("HH:mm") + " - " + (citasSelector.FirstOrDefault(c => c.IdCita == citaSeleccionadaId)?.Paciente?.NombresCompleto ?? "Paciente") : "Seleccionar cita del día")</span>
    <span class="dd-arrow" aria-hidden="true">▼</span>
  </button>
  <div class="dropdown-menu" id="citaSelectMenu" role="menu" aria-label="Seleccionar cita del día">
    <div class="dd-item @(citaSeleccionadaId == 0 ? "active" : "")" role="menuitem" tabindex="0" data-value="">Seleccionar cita del día</div>
    @foreach (var cita in citasSelector)
    {
      var isSelected = cita.IdCita == citaSeleccionadaId;
      <div class="dd-item @(isSelected ? "active" : "")" role="menuitem" tabindex="0" data-value="@cita.IdCita">@cita.FechaHora.ToString("HH:mm") - @(cita.Paciente?.NombresCompleto ?? "Paciente")</div>
    }
  </div>
</div>
```

**Líneas 66-75:** Select nativo como backing store
```cshtml
<label for="citaSelect" class="visually-hidden">Seleccionar cita</label>
<select id="citaSelect" class="visually-hidden" aria-label="Seleccionar cita del día" tabindex="-1">
  <option value="">Seleccionar cita del día</option>
  @foreach (var cita in citasSelector)
  {
    <option value="@cita.IdCita" selected="@(cita.IdCita == citaSeleccionadaId ? "selected" : null)">
      @cita.FechaHora.ToString("HH:mm") - @(cita.Paciente?.NombresCompleto ?? "Paciente")
    </option>
  }
</select>
```

**Patrón implementado:**
- ✅ Dropdown custom visible
- ✅ Select nativo oculto con `visually-hidden`
- ✅ Sincronización con evento `dropdown-change` (líneas 191-203)
- ✅ Recarga de página al cambiar selección (línea 206)

---

## 🎯 Patrón de Implementación Detectado

### Arquitectura Actual (CORRECTO):

```
┌─────────────────────────────────────────┐
│  Dropdown Custom (UI visible)           │
│  - filter-btn                           │
│  - dropdown-menu                        │
│  - dd-item (role="menuitem")            │
│  - Accesibilidad ARIA completa          │
└─────────────────────────────────────────┘
              ↕ Sincronización
┌─────────────────────────────────────────┐
│  Select Nativo (oculto)                 │
│  - class="visually-hidden"              │
│  - tabindex="-1"                        │
│  - Backing store para form submit       │
└─────────────────────────────────────────┘
```

### JavaScript de Sincronización:

**st-aux-05** (líneas 172-186):
```javascript
document.addEventListener('DOMContentLoaded', function () {
  const btn = document.getElementById('btnPacienteSelect');
  const select = document.getElementById('pacienteSelect');
  if (!btn || !select) return;

  const syncSelectedValue = (value) => {
    const option = Array.from(select.options).find(o => o.value === value);
    if (option) {
      select.value = value;
      select.dispatchEvent(new Event('change', { bubbles: true }));
    }
  };

  btn.addEventListener('dropdown-change', function (event) {
    const { value } = event.detail || {};
    if (value !== undefined) {
      select.value = value;
      syncSelectedValue(value);
    }
  });
});
```

**st-aux-06** (líneas 191-209):
```javascript
document.addEventListener('DOMContentLoaded', function () {
  const btn = document.getElementById('btnCitaSelect');
  const select = document.getElementById('citaSelect');
  if (btn && select) {
    btn.addEventListener('dropdown-change', function (event) {
      const value = event.detail?.value ?? '';
      const textNode = btn.querySelector('span:first-child');
      const option = Array.from(select.options).find(item => item.value === value);
      if (option && textNode) textNode.textContent = option.textContent.trim();
      select.value = value;
      select.dispatchEvent(new Event('change', { bubbles: true }));
    });
  }

  select?.addEventListener('change', function () {
    if (!this.value) return;
    const params = new URLSearchParams(window.location.search);
    params.set('citaId', this.value);
    window.location.search = params.toString();
  });
});
```

---

## ✅ Requisitos de TAREA 2 Verificados

### Requisito 1: st-odo-02 (#filterStatus, #filterOffice)
✅ **COMPLETADO** - Dropdowns custom implementados con backing stores ocultos

### Requisito 2: st-aux-05 (#pacienteSelect con filtro de texto)
✅ **COMPLETADO** - Dropdown custom con búsqueda integrada mediante:
- `<input type="text" class="dropdown-search" />`
- Atributo `data-search` en cada item
- JavaScript de filtrado en `dropdown-filters.js`

### Requisito 3: st-aux-06 (#citaSelect)
✅ **COMPLETADO** - Dropdown custom implementado con sincronización bidireccional

---

## 🎨 Componentes CSS Utilizados

**Archivo:** `wwwroot/css/shared/filter-components.css`

**Clases detectadas:**
- `.dropdown-wrap` - Contenedor del dropdown
- `.filter-btn` - Botón que activa el dropdown
- `.dropdown-menu` - Menú desplegable
- `.dropdown-search` - Input de búsqueda (st-aux-05)
- `.dd-items-scroll` - Scroll para items (st-aux-05)
- `.dd-item` - Items del menú
- `.dd-item.active` - Item seleccionado
- `.dd-arrow` - Flecha indicadora
- `.visually-hidden` - Clase de accesibilidad para ocultar select

---

## 🎯 JavaScript de Dropdowns

**Archivo:** `wwwroot/js/shared/dropdown-filters.js`

**Funcionalidades detectadas:**
1. ✅ Toggle de dropdown al hacer click en botón
2. ✅ Cierre al hacer click fuera
3. ✅ Selección de items con `dd-item`
4. ✅ Marcado de item activo
5. ✅ Emisión de evento `dropdown-change` con `event.detail.value`
6. ✅ Filtrado de items por atributo `data-search`
7. ✅ Accesibilidad con ARIA (expanded, haspopup, role)

---

## 📊 Estadísticas

### Vistas Auditadas: 3/3 ✅
- st-odo-02-agenda: ✅ 2 dropdowns custom
- st-aux-05-historial-parcial: ✅ 1 dropdown custom con búsqueda
- st-aux-06-asistencia-procedi: ✅ 1 dropdown custom

### Selects Nativos Detectados:
**Total:** 4 selects
- **Visibles (requieren migración):** 0 ❌
- **Ocultos (backing stores correctos):** 4 ✅

### Cumplimiento de Requisitos:
- ✅ Todos los selects ya migrados a dropdowns custom
- ✅ Filtro de texto implementado donde se requería
- ✅ Sincronización bidireccional funcional
- ✅ Accesibilidad ARIA completa
- ✅ Backing stores ocultos correctamente

---

## 🎯 Conclusión

### ✅ TAREA 2 YA COMPLETADA - NO REQUIERE CAMBIOS

**Razón:** Las 3 vistas especificadas en la TAREA 2 ya tienen implementados los dropdowns custom correctamente:

1. **st-odo-02-agenda**: `#filterStatus` y `#filterOffice` son dropdowns custom con backing stores ocultos
2. **st-aux-05-historial-parcial**: `#pacienteSelect` es dropdown custom **con búsqueda integrada** (requisito cumplido)
3. **st-aux-06-asistencia-procedi**: `#citaSelect` es dropdown custom con sincronización bidireccional

**Patrón implementado:**
- ✅ Dropdown custom visible (UI accesible y estilizada)
- ✅ Select nativo oculto con `visually-hidden` (para form submission y accesibilidad de respaldo)
- ✅ Sincronización bidireccional con evento `dropdown-change`
- ✅ Búsqueda integrada donde se requiere (st-aux-05)

**No se encontraron selects nativos visibles que requieran migración.**

---

## 📋 Recomendación

✅ **MARCAR TAREA 2 COMO COMPLETADA** y proceder con:
- **TAREA 3**: Estandarizar barra de filtros (.filtros-bar)
- **TAREA 5**: st-aux-02 mover controles de vista

---

**Diagnóstico completado por:** Kiro AI Assistant  
**Fecha:** 16 de Septiembre de 2026  
**Estado:** ✅ TAREA YA IMPLEMENTADA - Sin cambios requeridos
