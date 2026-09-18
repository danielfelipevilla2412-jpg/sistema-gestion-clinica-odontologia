# Prompt de Continuación - Tareas Pendientes de Estandarización

**Proyecto:** SmileTrack (ASP.NET Core MVC 9.0 + SQL Server LocalDB)  
**Contexto:** Estandarización de módulos Gestión de Citas y Gestión de Profesionales  
**Fecha base:** 16 de Septiembre de 2026  

---

## 📋 Estado Actual

### ✅ Tareas Completadas (4 de 8)

- ✅ **TAREA 1** - Botones unificados (1 cambio aplicado)
- ✅ **TAREA 4** - Stat-cards (ya implementado)
- ✅ **TAREA 6** - fetchAppointments (ya implementado)
- ✅ **TAREA 7** - Campo Medicamentos (ya implementado)
- ✅ **TAREA 8** - localStorage auditado (sin cambios requeridos)

### ⚠️ Tareas Pendientes (3 de 8)

- ⚠️ **TAREA 2** - Migrar selects nativos a dropdowns custom
- ⚠️ **TAREA 3** - Estandarizar filtros con patrón `.filtros-bar`
- ⚠️ **TAREA 5** - st-aux-02 mover controles de vista + fecha dinámica

---

## 🎯 PROMPT DE CONTINUACIÓN

Copia y pega este prompt completo en una nueva sesión con Kiro:

```
CONTEXTO PROYECTO:

Estoy trabajando en SmileTrack (ASP.NET Core MVC 9.0 + SQL Server LocalDB), 
proyecto de gestión clínica odontológica. Ya se completaron las TAREAS 1, 4, 6, 7 y 8 
de estandarización de los módulos Gestión de Citas y Gestión de Profesionales.

UBICACIÓN DE DOCUMENTACIÓN PREVIA:

Lee estos archivos para entender el contexto completo:

1. `Documentacion/AUDITORIA_ESTANDARIZACION_MODULOS.md`
   - Estado general de las 8 tareas
   - Diagnóstico completo con evidencia
   - Planes de implementación detallados

2. `Documentacion/RESUMEN_TAREAS_1_Y_3.md`
   - TAREA 1 ya completada
   - TAREA 3 diagnosticada y lista para implementar
   - JavaScript y CSS completos preparados

3. `Documentacion/AUDITORIA_TAREA_8_LOCALSTORAGE.md`
   - TAREA 8 completada (sin cambios requeridos)

TAREAS PENDIENTES:

Implementa las siguientes tareas EN ESTE ORDEN:

---

## TAREA 3 - Estandarizar Filtros (PRIORIDAD ALTA)

**Objetivo:** Unificar todos los filtros bajo el patrón `.filtros-bar` con dropdowns custom.

**Patrón de referencia:** 
`Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml`

**Vistas a migrar:**

1. **st-odo-02-agenda** (Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml)
   - Migrar `#filterStatus` (select nativo → dropdown custom)
   - Migrar `#filterOffice` (select nativo → dropdown custom)
   - Mover filtros fuera de `.agenda-controls .controls-right`
   - Crear nueva sección `.filtros-bar` debajo de `.agenda-controls`

2. **st-aux-05-historial-parcial** (Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml)
   - Migrar `#pacienteSelect` de `.header-actions` a nueva `.filtros-bar`
   - AGREGAR búsqueda interna en el dropdown (input dentro del menú)
   - Mantener la funcionalidad de cambio con window.location.search

3. **st-adm-09-citas** (Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml)
   - Renombrar clase `.filters-section` → `.filtros-bar`
   - Migrar selects nativos `#filterStatus`, `#filterProfessional`, `#filterDate` a dropdowns
   - Mantener compatibilidad con lógica de filtrado existente

4. **st-pac-01-mis-citas** (Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml)
   - Renombrar clase `.toolbar` → `.filtros-bar`
   - Migrar `#filterEstado` (select nativo → dropdown custom)

**PASO 1:** Crea archivos compartidos

Archivo: `wwwroot/js/shared/dropdown-filters.js`

```javascript
/**
 * SmileTrack - Dropdown Filters Component
 * Maneja dropdowns custom de filtros con búsqueda interna
 */

(function() {
  'use strict';

  function initFilterDropdowns() {
    const dropdowns = document.querySelectorAll('.dropdown-wrap');
    
    dropdowns.forEach(wrapper => {
      const btn = wrapper.querySelector('.filter-btn');
      const menu = wrapper.querySelector('.dropdown-menu');
      
      if (!btn || !menu) return;
      
      const searchInput = menu.querySelector('.dropdown-search');
      
      // Toggle dropdown
      btn.addEventListener('click', function(e) {
        e.stopPropagation();
        const isOpen = menu.classList.contains('open');
        
        // Cerrar otros dropdowns
        document.querySelectorAll('.dropdown-menu.open').forEach(m => {
          if (m !== menu) {
            m.classList.remove('open');
            m.previousElementSibling?.setAttribute('aria-expanded', 'false');
          }
        });
        
        // Toggle actual
        menu.classList.toggle('open', !isOpen);
        btn.setAttribute('aria-expanded', !isOpen);
        
        // Focus en búsqueda si existe
        if (!isOpen && searchInput) {
          setTimeout(() => searchInput.focus(), 100);
        }
      });
      
      // Selección de items
      const items = menu.querySelectorAll('.dd-item');
      items.forEach(item => {
        item.addEventListener('click', function() {
          const value = this.dataset.value || '';
          const text = this.textContent.trim();
          
          // Actualizar texto del botón
          const btnText = btn.querySelector('span:first-child');
          if (btnText) btnText.textContent = text;
          
          // Actualizar clase active
          items.forEach(i => i.classList.remove('active'));
          this.classList.add('active');
          
          // Guardar valor en el botón
          btn.dataset.value = value;
          
          // Emitir evento personalizado
          const event = new CustomEvent('dropdown-change', {
            detail: { value, text },
            bubbles: true
          });
          btn.dispatchEvent(event);
          
          // Cerrar menú
          menu.classList.remove('open');
          btn.setAttribute('aria-expanded', 'false');
        });
      });
      
      // Búsqueda interna
      if (searchInput) {
        searchInput.addEventListener('input', function(e) {
          const query = e.target.value.toLowerCase();
          
          items.forEach(item => {
            const searchText = item.dataset.search || 
              item.textContent.toLowerCase();
            
            if (searchText.includes(query)) {
              item.style.display = '';
            } else {
              item.style.display = 'none';
            }
          });
        });
        
        // Prevenir cierre al escribir
        searchInput.addEventListener('click', e => e.stopPropagation());
      }
      
      // Navegación por teclado
      btn.addEventListener('keydown', function(e) {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          btn.click();
        }
      });
    });
    
    // Cerrar al hacer click fuera
    document.addEventListener('click', function() {
      document.querySelectorAll('.dropdown-menu.open').forEach(menu => {
        menu.classList.remove('open');
        const btn = menu.previousElementSibling;
        if (btn) btn.setAttribute('aria-expanded', 'false');
      });
    });
    
    // Cerrar con Escape
    document.addEventListener('keydown', function(e) {
      if (e.key === 'Escape') {
        document.querySelectorAll('.dropdown-menu.open').forEach(menu => {
          menu.classList.remove('open');
          const btn = menu.previousElementSibling;
          if (btn) {
            btn.setAttribute('aria-expanded', 'false');
            btn.focus();
          }
        });
      }
    });
  }
  
  // Inicializar en DOMContentLoaded
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initFilterDropdowns);
  } else {
    initFilterDropdowns();
  }
  
  // Exponer globalmente para re-inicialización dinámica
  window.SmileTrack = window.SmileTrack || {};
  window.SmileTrack.initFilterDropdowns = initFilterDropdowns;
})();
```

Archivo: `wwwroot/css/shared/filter-components.css`

```css
/**
 * SmileTrack - Filter Components
 * Estilos para barra de filtros y dropdowns custom
 */

/* Barra de filtros estandarizada */
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

/* Wrapper del dropdown */
.dropdown-wrap {
  position: relative;
  display: inline-block;
}

/* Botón del dropdown */
.filter-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border: 1px solid var(--border-color, #dee2e6);
  background: var(--bg-white, #fff);
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-family: inherit;
  transition: all 0.2s ease;
  min-width: 150px;
  justify-content: space-between;
}

.filter-btn:hover {
  border-color: var(--blue-500, #0d6efd);
  background: var(--blue-50, #e7f1ff);
}

.filter-btn:focus {
  outline: 2px solid var(--blue-500, #0d6efd);
  outline-offset: 2px;
}

.filter-btn.active {
  background: var(--blue-500, #0d6efd);
  color: white;
  border-color: var(--blue-500, #0d6efd);
}

/* Flecha del dropdown */
.dd-arrow {
  font-size: 0.7rem;
  transition: transform 0.2s ease;
  pointer-events: none;
}

.filter-btn[aria-expanded="true"] .dd-arrow {
  transform: rotate(180deg);
}

/* Menú desplegable */
.dropdown-menu {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  min-width: 100%;
  max-width: 350px;
  max-height: 400px;
  overflow-y: auto;
  background: var(--bg-white, #fff);
  border: 1px solid var(--border-color, #dee2e6);
  border-radius: 8px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
  z-index: 1000;
  display: none;
}

.dropdown-menu.open {
  display: block;
  animation: dropdownFadeIn 0.2s ease-out;
}

@keyframes dropdownFadeIn {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Items del dropdown */
.dd-item {
  padding: 10px 16px;
  cursor: pointer;
  transition: background 0.15s ease;
  font-size: 0.9rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.dd-item:hover {
  background: var(--blue-50, #e7f1ff);
}

.dd-item.active {
  background: var(--blue-100, #cfe2ff);
  font-weight: 500;
}

.dd-item.active::after {
  content: '✓';
  color: var(--blue-500, #0d6efd);
  font-weight: bold;
  margin-left: 8px;
}

/* Búsqueda interna del dropdown */
.dropdown-search {
  width: 100%;
  padding: 12px 16px;
  border: none;
  border-bottom: 1px solid var(--border-color, #dee2e6);
  outline: none;
  font-size: 0.9rem;
  font-family: inherit;
  position: sticky;
  top: 0;
  background: var(--bg-white, #fff);
  z-index: 1;
}

.dropdown-search:focus {
  border-bottom-color: var(--blue-500, #0d6efd);
  background: var(--blue-50, #f0f8ff);
}

.dropdown-search::placeholder {
  color: var(--text-muted, #6c757d);
}

/* Contenedor scrolleable de items */
.dd-items-scroll {
  max-height: 320px;
  overflow-y: auto;
}

/* Radiogroup de filtros */
.filter-group {
  display: flex;
  gap: 4px;
  background: var(--bg-white, #fff);
  border-radius: 6px;
  padding: 4px;
  border: 1px solid var(--border-color, #dee2e6);
}

.filter-group .filter-btn {
  min-width: auto;
  border: none;
  background: transparent;
  padding: 6px 12px;
}

.filter-group .filter-btn:hover {
  background: var(--blue-50, #e7f1ff);
  border: none;
}

.filter-group .filter-btn.active {
  background: var(--blue-500, #0d6efd);
  color: white;
  border: none;
}

/* Etiqueta de búsqueda */
.search-wrap {
  position: relative;
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 200px;
}

.search-wrap .search-icon {
  position: absolute;
  left: 12px;
  color: var(--text-muted, #6c757d);
  pointer-events: none;
}

.search-wrap .search-input {
  width: 100%;
  padding: 8px 16px 8px 40px;
  border: 1px solid var(--border-color, #dee2e6);
  border-radius: 6px;
  font-size: 0.9rem;
  font-family: inherit;
  transition: border-color 0.2s ease;
}

.search-wrap .search-input:focus {
  outline: none;
  border-color: var(--blue-500, #0d6efd);
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
}

/* Responsive */
@media (max-width: 768px) {
  .filtros-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 8px;
  }
  
  .filter-btn,
  .search-wrap {
    width: 100%;
    min-width: 100%;
  }
  
  .dropdown-menu {
    left: 0;
    right: 0;
    max-width: none;
  }
  
  .filter-group {
    width: 100%;
    justify-content: space-between;
  }
}

/* Scrollbar personalizado para dropdown */
.dropdown-menu::-webkit-scrollbar,
.dd-items-scroll::-webkit-scrollbar {
  width: 8px;
}

.dropdown-menu::-webkit-scrollbar-track,
.dd-items-scroll::-webkit-scrollbar-track {
  background: var(--bg-secondary, #f8f9fa);
  border-radius: 4px;
}

.dropdown-menu::-webkit-scrollbar-thumb,
.dd-items-scroll::-webkit-scrollbar-thumb {
  background: var(--border-color, #dee2e6);
  border-radius: 4px;
}

.dropdown-menu::-webkit-scrollbar-thumb:hover,
.dd-items-scroll::-webkit-scrollbar-thumb:hover {
  background: var(--text-muted, #6c757d);
}
```

**PASO 2:** Migra cada vista una por una

Para cada vista:
1. Lee el archivo actual completo
2. Identifica la sección de filtros
3. Aplica el patrón `.filtros-bar` con dropdowns custom
4. Mantén los mismos IDs para compatibilidad con JS existente
5. Agrega event listeners para emitir eventos `change` cuando cambie la selección
6. Muestra el git diff del cambio

**PASO 3:** Actualiza los archivos JS relacionados

Para cada vista migrada, actualiza su archivo JavaScript:
- Escucha eventos `dropdown-change` en vez de `change` del select
- Mantén la misma lógica de filtrado
- Ejemplo:

```javascript
// ANTES
document.getElementById('filterStatus').addEventListener('change', function() {
  const value = this.value;
  filtrarPorEstado(value);
});

// DESPUÉS
const btnFilterStatus = document.getElementById('btnFilterStatus');
btnFilterStatus.addEventListener('dropdown-change', function(e) {
  const value = e.detail.value;
  filtrarPorEstado(value);
});
```

**REGLAS IMPORTANTES:**
- NO modifiques la lógica de filtrado existente
- NO cambies nombres de funciones o variables
- Mantén todos los data-attributes originales
- Conserva la accesibilidad (aria-* y role)
- Muestra git diff después de cada cambio

---

## TAREA 2 - Migrar Selects Nativos (DESPUÉS DE TAREA 3)

**Objetivo:** Reemplazar `<select>` nativos restantes por dropdowns custom.

Esta tarea es básicamente continuar TAREA 3 en vistas que no tenían filtros complejos.

**Vistas a revisar:**
- st-aux-06-asistencia-procedi
- st-rec-03-gestion-citas
- Cualquier otra vista con `<select class="status-select">`

**Usar el mismo patrón de TAREA 3.**

---

## TAREA 5 - st-aux-02 Controles de Vista

**Objetivo:** Mover controles de vista fuera del header + mostrar fecha dinámica.

**Archivo:** `Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml`

**Cambios:**

1. **Separar controles de vista y filtros:**

Actualmente hay DOS `.filtros-bar` mezclados. Separar en:
- `.vista-controls-bar` (toggle día/semana/lista + navegación)
- `.filtros-bar` (dropdown profesional + filtros tipo)

2. **Actualizar #phMeta dinámicamente:**

Cambiar de:
```cshtml
<p class="page-subtitle" id="phMeta">@metaTexto</p>
```

A:
```cshtml
<p class="page-subtitle" id="phMeta" aria-live="polite">
  <span id="fechaActivaDisplay">Cargando...</span>
</p>
```

Y agregar JavaScript para actualizar el texto según la vista activa.

3. **CSS para .vista-controls-bar:**

```css
.vista-controls-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  background: var(--bg-secondary);
  border-radius: 8px;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 12px;
}

.view-toggle {
  display: flex;
  gap: 4px;
  background: var(--bg-white);
  border-radius: 6px;
  padding: 4px;
  border: 1px solid var(--border-color);
}

.view-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  background: transparent;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.view-btn.active {
  background: var(--blue-500);
  color: white;
}

.date-nav-group {
  display: flex;
  align-items: center;
  gap: 12px;
}

.date-label {
  font-size: 1rem;
  font-weight: 500;
  min-width: 200px;
  text-align: center;
}
```

4. **JavaScript para fecha dinámica:**

```javascript
let fechaAgenda = new Date(); // Inicializar con la fecha del modelo
let vistaActiva = 'dia'; // o 'semana' según el modelo

function actualizarFechaDisplay() {
  const display = document.getElementById('fechaActivaDisplay');
  let texto = '';
  
  if (vistaActiva === 'dia') {
    texto = `Citas del día ${fechaAgenda.toLocaleDateString('es-CO', {
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    })}`;
  } else if (vistaActiva === 'semana') {
    const finSemana = new Date(fechaAgenda);
    finSemana.setDate(finSemana.getDate() + 6);
    texto = `Semana del ${fechaAgenda.toLocaleDateString('es-CO', {
      day: 'numeric',
      month: 'short'
    })} al ${finSemana.toLocaleDateString('es-CO', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    })}`;
  }
  
  if (display) display.textContent = texto;
}

// Event listeners para cambio de vista y navegación
document.querySelectorAll('.view-btn').forEach(btn => {
  btn.addEventListener('click', function() {
    vistaActiva = this.dataset.view;
    actualizarFechaDisplay();
    // ... resto de lógica
  });
});

// Inicializar
document.addEventListener('DOMContentLoaded', actualizarFechaDisplay);
```

**NO toques la lógica de ConstruirAgendaApoyoAsync en el controlador.**

---

## ORDEN DE EJECUCIÓN SUGERIDO:

1. ✅ Crear archivos compartidos (JS y CSS)
2. ✅ TAREA 3 - Vista 1: st-aux-05-historial-parcial (más simple)
3. ✅ TAREA 3 - Vista 2: st-pac-01-mis-citas
4. ✅ TAREA 3 - Vista 3: st-odo-02-agenda
5. ✅ TAREA 3 - Vista 4: st-adm-09-citas
6. ✅ TAREA 2 - Revisar vistas restantes
7. ✅ TAREA 5 - st-aux-02 controles

---

## REGLAS GENERALES:

1. **Un cambio a la vez** - No mezcles múltiples vistas en un solo commit
2. **Mostrar git diff** - Después de cada cambio
3. **Mantener compatibilidad** - No rompas JavaScript existente
4. **Verificar accesibilidad** - Roles ARIA y navegación por teclado
5. **No modificar BD** - Solo cambios de UI

---

## VERIFICACIÓN FINAL:

Después de completar todas las tareas:

1. ✅ Todos los filtros usan `.filtros-bar`
2. ✅ Todos los selects son dropdowns custom
3. ✅ JavaScript compartido funciona en todas las vistas
4. ✅ CSS compartido aplicado correctamente
5. ✅ Accesibilidad mantenida
6. ✅ Git diff limpio sin conflictos

---

FIN DEL PROMPT. COMIENZA LA IMPLEMENTACIÓN CON TAREA 3.
```

---

## 📊 Métricas de Trabajo Pendiente

| Tarea | Archivos a Crear | Archivos a Modificar | Líneas Estimadas | Tiempo |
|-------|------------------|----------------------|------------------|--------|
| TAREA 3 | 2 (JS + CSS) | 4 vistas + 4 JS | ~800 líneas | 4-6 horas |
| TAREA 2 | 0 | 2-3 vistas | ~200 líneas | 1-2 horas |
| TAREA 5 | 0 | 1 vista + 1 JS + 1 CSS | ~150 líneas | 1-2 horas |
| **TOTAL** | **2** | **10-12** | **~1,150** | **6-10 horas** |

---

## 📁 Estructura Final de Archivos

```
SmileTrack/
├── wwwroot/
│   ├── js/
│   │   └── shared/
│   │       └── dropdown-filters.js ← NUEVO (TAREA 3)
│   └── css/
│       └── shared/
│           └── filter-components.css ← NUEVO (TAREA 3)
│
└── Views/
    └── Gestion_De_Citas/
        ├── st-odo-02-agenda/index.cshtml ← MODIFICAR (TAREA 3)
        ├── st-aux-05-historial-parcial/historial-parcial.cshtml ← MODIFICAR (TAREA 3)
        ├── st-adm-09-citas/index.cshtml ← MODIFICAR (TAREA 3)
        ├── st-pac-01-mis-citas/index.cshtml ← MODIFICAR (TAREA 3)
        └── st-aux-02-agenda-apoyo/agenda-apoyo.cshtml ← MODIFICAR (TAREA 5)
```

---

**Documento generado por:** Kiro AI Assistant  
**Fecha:** 16 de Septiembre de 2026  
**Versión:** 1.0  
**Estado:** Listo para nueva sesión
