# Auditoría: Inconsistencia de Clases en Botones de Tablas (SSR vs CSR)

**Fecha:** 16 de septiembre de 2026  
**Problema inicial:** Aplicar filtros en `/gestion-de-profesionales/st-adm-07-gestion-profesionales` cambiaba el diseño de los botones de acción en la tabla.

---

## 🎯 Problema Identificado

Cuando se aplicaban filtros en las tablas, el JavaScript re-renderizaba las filas usando clases CSS diferentes a las que usaba el HTML renderizado por Razor (SSR). Esto causaba cambios visuales inconsistentes en los botones de acción.

### Patrón incorrecto (JavaScript antes del fix):
```html
<button class="btn-icon action-btn btn-view">👁️ <span class="btn-text">Ver</span></button>
<button class="btn-icon action-btn edit">✏️ <span class="btn-text">Editar</span></button>
<button class="btn-icon action-btn btn-delete">✕ <span class="btn-text">Cancelar</span></button>
```

### Patrón correcto (HTML Razor SSR):
```html
<button class="btn-secondary btn-view">👁️ <span class="btn-text">Ver</span></button>
<button class="btn-secondary edit">✏️ <span class="btn-text">Editar</span></button>
<button class="btn-danger btn-delete">✕ <span class="btn-text">Cancelar</span></button>
```

---

## 📋 Vistas Auditadas y Corregidas

### ✅ 1. st-adm-07-gestion-profesionales
**Ubicación:** `wwwroot/js/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/app.js`  
**Líneas modificadas:** 198-240  
**Estado:** ✅ CORREGIDO

**Cambios:**
- `btn-icon action-btn btn-view` → `btn-secondary btn-view`
- `btn-icon action-btn edit` → `btn-secondary edit`
- `btn-icon action-btn btn-delete` → `btn-danger btn-delete`

**Función afectada:** `renderTableFromApi()`

---

### ✅ 2. st-rec-03-gestion-citas
**Ubicación:** `wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js`  
**Líneas modificadas:** 283-318  
**Estado:** ✅ CORREGIDO

**Cambios:**
- `btn-icon action-btn btn-view` → `btn-secondary btn-view`
- `btn-icon action-btn edit` → `btn-secondary edit`
- `btn-icon action-btn btn-delete` → `btn-danger btn-delete`

**Función afectada:** `createAppointmentRow()`

---

### ✅ 3. st-rec-01-dashboard
**Ubicación:** `wwwroot/js/Gestion_De_Citas/st-rec-01-dashboard/app.js`  
**Estado:** ✅ CORREGIDO

**Nota:** Esta vista usa renderizado 100% JavaScript (no hay SSR de filas), pero se corrigió para mantener consistencia.

**Cambios en `getActionMeta()`:**
- `'pencil'`: `btn-icon action-btn edit` → `btn-secondary edit`
- `'file-invoice'`: `btn-icon action-btn btn-facturar` → `btn-secondary btn-facturar`
- `'eye'`: `btn-icon action-btn btn-view` → `btn-secondary btn-view`

---

### ✅ 4. st-pac-01-mis-citas
**Ubicación:** `wwwroot/js/Gestion_De_Citas/st-pac-01-mis-citas/mis-citas.js`  
**Estado:** ✅ CORREGIDO

**Nota:** Vista 100% JavaScript del módulo Paciente.

**Cambios:**
- `btn-icon action-btn btn-view` → `btn-secondary btn-view`
- `btn-icon action-btn btn-delete danger` → `btn-danger btn-delete`

**Función afectada:** `renderTable()`

---

### ✅ 5. st-adm-09-citas (Gestión Integral)
**Ubicación:** `wwwroot/js/Gestion_De_Citas/st-adm-09-citas/gestionintegral.js`  
**Estado:** ✅ SIN CAMBIOS NECESARIOS

**Razón:** Esta vista **NO renderiza dinámicamente nuevas filas** desde JavaScript. Solo trabaja con el HTML generado por Razor en el servidor. Los botones ya usan el patrón correcto.

---

## 📊 Resumen de Cambios

| Vista                          | Archivo                                | Estado              | Tipo de Renderizado |
|-------------------------------|----------------------------------------|---------------------|---------------------|
| st-adm-07-gestion-profesionales | app.js (líneas 198-240)               | ✅ Corregido        | SSR + CSR (API)     |
| st-rec-03-gestion-citas        | app.js (líneas 283-318)               | ✅ Corregido        | SSR + CSR (filtros) |
| st-rec-01-dashboard            | app.js (getActionMeta)                | ✅ Corregido        | 100% CSR            |
| st-pac-01-mis-citas            | mis-citas.js (renderTable)            | ✅ Corregido        | 100% CSR            |
| st-adm-09-citas                | gestionintegral.js                    | ✅ Sin cambios      | 100% SSR            |

---

## 🎨 Patrón de Diseño Estandarizado

### Botones de Acción Secundaria:
```html
<button class="btn-secondary btn-view">
  <span class="material-symbols-outlined">visibility</span>
  <span class="btn-text">Ver</span>
</button>
```

### Botones de Acción Peligrosa:
```html
<button class="btn-danger btn-delete">
  <span class="material-symbols-outlined">delete</span>
  <span class="btn-text">Eliminar</span>
</button>
```

### Clases a EVITAR (obsoletas):
- ❌ `btn-icon action-btn` (genera inconsistencia visual)
- ❌ Usar `danger` como clase adicional en vez de `btn-danger`

### Clases a USAR:
- ✅ `btn-secondary` para acciones de vista/edición
- ✅ `btn-danger` para acciones destructivas
- ✅ Específicas: `btn-view`, `edit`, `btn-delete`, etc.

---

## ✅ Resultado

**Problema resuelto:** Ahora, cuando se aplican filtros en cualquier tabla, los botones mantienen el mismo diseño que tenían en el renderizado inicial del servidor.

**Impacto:** 
- ✅ Consistencia visual entre SSR y CSR
- ✅ Mejor experiencia de usuario (no hay cambios visuales inesperados)
- ✅ Código más mantenible siguiendo un patrón único
- ✅ Mejora en accesibilidad (clases semánticas consistentes)
