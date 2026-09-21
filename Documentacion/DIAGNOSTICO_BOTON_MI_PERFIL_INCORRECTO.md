# 🔍 DIAGNÓSTICO - Botón "Mi Perfil" Aparece Incorrectamente

**Proyecto:** SmileTrack (ASP.NET Core MVC 9.0)  
**Fecha:** 16 de Septiembre de 2026  
**Problema Reportado:** El botón "Mi Perfil" aparece en vistas de rol Paciente y módulos de Gestión de Citas y Profesionales donde no debería estar

---

## 📋 Contexto

**Reporte del usuario:**
> "¿Por qué aparece en las vistas del rol de pacientes y módulo gestión de citas y profesionales un botón de mi perfil? Debería quitarse."

---

## 🔍 Diagnóstico del Problema

### Arquitectura del Sistema

**Layout Master:** `Views/shared/_Layout.cshtml`
- Usado por **19 vistas** de los módulos Gestión de Citas y Gestión de Profesionales
- Carga dinámicamente el sidebar según el rol del usuario

**Sidebar Dinámico:** `Views/shared/_SidebarDynamic.cshtml` (línea 28)

```csharp
@{
    string sidebarPartial = User.IsInRole("Recepcionista") ? "_SidebarRecepcionista"
                          : User.IsInRole("Auxiliar")      ? "_SidebarAuxiliar"
                          : User.IsInRole("Profesional")   ? "_SidebarProfesional"
                          : User.IsInRole("Paciente")      ? "_SidebarPaciente"
                          : "_SidebarAdmin";
}
@{ await Html.RenderPartialAsync(sidebarPartial); }
```

**Decisión de diseño:**
- El sidebar NO depende de la vista que se esté mostrando
- El sidebar depende **exclusivamente del ROL del usuario autenticado**
- Un usuario con rol "Paciente" verá `_SidebarPaciente` en TODAS las vistas
- Un usuario con rol "Profesional" verá `_SidebarProfesional` en TODAS las vistas

---

## ✅ Estado Actual Verificado

### 1. Sidebar del Paciente (`_SidebarPaciente.cshtml`)

**Líneas 80-90 - Grupo "Mi perfil":**

```csharp
new
{
    Title = "Mi perfil",
    Items = new[]
    {
        new { Label = "Mi perfil", Href = "/perfiles/st-pac-perfil-paciente", Icon = "👤" },
        new { Label = "Cambiar Contraseña", Href = "/acceso-y-seguridad/cambiar-contrasena", Icon = "🔐" }
    }
}
```

**✅ Estado:** El botón "Mi perfil" existe y apunta a `/perfiles/st-pac-perfil-paciente`

---

### 2. Sidebar del Profesional (`_SidebarProfesional.cshtml`)

**Líneas 53-59 - Grupo "Perfil":**

```html
<div class="nav-group">
  <div class="nav-group-header" role="button" tabindex="0" aria-expanded="false" data-i18n="nav.group_perfil">
    Perfil <span class="nav-arrow" aria-hidden="true">▼</span>
  </div>
  <div class="nav-group-items">
    <a href="/gestion-de-profesionales/st-odo-09-perfil-profesional" 
       class="@linkClass(...)" data-icon="👤" data-i18n="nav.item_mi_perfil">Mi Perfil</a>
    <a href="/acceso-y-seguridad/cambiar-contrasena" 
       class="@linkClass(...)" data-icon="🔐" data-i18n="nav.item_cambiar_contrasena">Cambiar Contraseña</a>
  </div>
</div>
```

**✅ Estado:** El botón "Mi Perfil" existe y apunta a `/gestion-de-profesionales/st-odo-09-perfil-profesional`

---

### 3. Dashboard del Profesional (`st-odo-01-dashboard/index.cshtml`)

**Líneas 141-145 - Acceso rápido en contenido:**

```html
<a href="/gestion-de-profesionales/st-odo-09-perfil-profesional" class="quick-action-card">
    <span class="material-symbols-outlined">person</span>
    <strong>Mi Perfil</strong>
    <span>Datos personales</span>
</a>
```

**✅ Estado:** Existe un acceso rápido adicional "Mi Perfil" **dentro del contenido** del dashboard

---

## 🎯 Análisis del Problema

### ❌ Malentendido Identificado

El usuario reporta que "Mi Perfil" **aparece donde no debería**, pero en realidad:

**✅ Es comportamiento CORRECTO del sistema:**

1. **Rol Paciente** → Ve `_SidebarPaciente` con opción "Mi perfil" ✅
   - Ruta: `/perfiles/st-pac-perfil-paciente`
   - **DEBE existir:** Los pacientes necesitan ver/editar su perfil

2. **Rol Profesional** → Ve `_SidebarProfesional` con opción "Mi Perfil" ✅
   - Ruta: `/gestion-de-profesionales/st-odo-09-perfil-profesional`
   - **DEBE existir:** Los profesionales necesitan gestionar su perfil profesional

3. **Dashboard Profesional** → Tiene acceso rápido "Mi Perfil" ✅
   - Es un acceso directo en la página de inicio del profesional
   - **DEBE existir:** Facilita acceso rápido a información personal

---

## 🤔 Posibles Interpretaciones del Reporte

### Interpretación A: Confusión Usuario/Rol

**Posibilidad:** El usuario accedió con un rol (ej: Paciente) a rutas que NO corresponden a ese rol:

```
Usuario con rol "Paciente" intenta acceder a:
  /gestion-de-citas/st-odo-02-agenda        ← Vista de PROFESIONAL
  /gestion-de-profesionales/st-odo-01-dashboard  ← Vista de PROFESIONAL
```

**Resultado:**
- El sistema carga la vista (si no hay [Authorize] que lo bloquee)
- El sidebar muestra `_SidebarPaciente` (porque el rol es Paciente)
- Aparece "Mi perfil" del paciente en una vista de profesional

**¿Es un bug?** ❌ NO - Es falta de autorización en el controller

---

### Interpretación B: Enlaces Duplicados

**Posibilidad:** Hay múltiples lugares donde aparece "Mi Perfil":

1. **Sidebar** (siempre visible)
2. **Dashboard del profesional** (acceso rápido)
3. **¿Breadcrumbs/headers de otras vistas?** (a verificar)

**¿Es un bug?** ⚠️ DEPENDE - Si hay redundancia excesiva, sí

---

### Interpretación C: Contexto Incorrecto

**Posibilidad:** El usuario espera que el sidebar cambie según el módulo:

```
Usuario con rol "Profesional" espera:
  - En /gestion-de-citas/*        → Sidebar de Citas (sin Mi Perfil)
  - En /gestion-de-profesionales/* → Sidebar de Profesionales (con Mi Perfil)
```

**Realidad del sistema:**
```
Usuario con rol "Profesional" ve:
  - En TODAS las vistas → _SidebarProfesional (siempre con Mi Perfil)
```

**¿Es un bug?** ❌ NO - Es decisión de diseño (sidebar por rol, no por módulo)

---

## 📊 Evidencia de Grep

### Búsqueda 1: Todos los "Mi Perfil" en vistas

```bash
grep -ri "Mi perfil|mi perfil|Mi Perfil" Views/**/*.cshtml

Resultados:
- _SidebarProfesional.cshtml:56    → Sidebar (grupo Perfil)
- _SidebarPaciente.cshtml:20,32,83,86 → Sidebar (grupo Mi perfil)
- _SidebarAuxiliar.cshtml:118      → Sidebar (grupo Perfil)
- st-rec-06-perfil-recepcionista/perfilrecepcionista.cshtml → Título de página
- st-pac-perfil-Paciente/perfil-paciente.cshtml → Título de página
- st-aux-11-perfil-auxiliar/mi-perfil.cshtml → Título de página
- st-odo-09-perfil-profesional/index.cshtml → Título de página
- st-odo-01-dashboard/index.cshtml:142 → Acceso rápido en contenido
```

---

## ✅ Recomendaciones

### Opción 1: Mantener como está (✅ RECOMENDADO)

**Razón:** El comportamiento actual es correcto:
- Cada rol tiene su sidebar con opciones pertinentes
- "Mi Perfil" es una función estándar de cualquier aplicación
- Los pacientes, profesionales, auxiliares y recepcionistas NECESITAN acceder a su perfil

**Acción:** ❌ Ninguna - Explicar al usuario que es comportamiento esperado

---

### Opción 2: Agregar autorización estricta en controllers

**Si el problema es que usuarios con rol Paciente acceden a vistas de Profesional:**

```csharp
[Authorize(Roles = "Profesional")]  // ← Falta en algunos controllers
public IActionResult StOdo01Dashboard() { ... }
```

**Acción:**
1. Revisar todos los controllers de Gestión de Citas y Profesionales
2. Agregar `[Authorize(Roles = "...")]` a cada action
3. Verificar que solo los roles autorizados puedan acceder

---

### Opción 3: Remover acceso rápido "Mi Perfil" del dashboard

**Si se considera redundante con el sidebar:**

**Archivo:** `Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml` (línea 141-145)

**Cambio:** Eliminar la tarjeta de acceso rápido "Mi Perfil"

**Justificación:** Ya está disponible en el sidebar, no es necesario duplicarlo

---

### Opción 4: Sidebar contextual por módulo (❌ NO RECOMENDADO)

**Cambio arquitectónico grande:**
- Crear sidebars por módulo: `_SidebarCitas.cshtml`, `_SidebarProfesionales.cshtml`
- Modificar `_SidebarDynamic.cshtml` para elegir por ruta, no por rol
- Requiere refactorización de 19 vistas y lógica de navegación

**Justificación:** ❌ Demasiado invasivo para un cambio cosmético

---

## 🎯 Pregunta para el Usuario

**¿Cuál es el problema específico que observas?**

1. **¿Un usuario con rol Paciente ve el sidebar de Paciente en vistas de Profesional?**
   - Problema: Falta autorización en controllers
   - Solución: Agregar `[Authorize(Roles = "...")]`

2. **¿El botón "Mi Perfil" aparece demasiadas veces (sidebar + dashboard)?**
   - Problema: Redundancia de enlaces
   - Solución: Remover acceso rápido del dashboard (línea 141-145)

3. **¿El sidebar debería cambiar según el módulo, no según el rol?**
   - Problema: Decisión de diseño del sistema
   - Solución: Refactorización arquitectónica (no recomendado)

4. **¿El usuario espera que "Mi Perfil" NO exista en absoluto?**
   - Problema: Expectativa incorrecta
   - Solución: Explicar que es función estándar

---

## 📝 Siguiente Paso

**Antes de hacer cambios, necesito confirmación del usuario:**

1. ¿En qué vista específica ves "Mi Perfil" donde no debería estar?
2. ¿Con qué rol de usuario estás accediendo cuando lo ves?
3. ¿Cuál es el comportamiento esperado?

**Sin esta información, NO puedo determinar si es un bug o comportamiento correcto.**

---

**Diagnóstico completado por:** Kiro AI Assistant  
**Fecha:** 16 de Septiembre de 2026  
**Estado:** ⏸️ PENDIENTE DE ACLARACIÓN DEL USUARIO
