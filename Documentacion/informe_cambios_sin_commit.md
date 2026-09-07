# Informe de Cambios Sin Commitear — SmileTrack MVC
**Fecha:** 06/08/2026 | **Total de archivos modificados:** ~112 | **Líneas afectadas:** +4.105 / -4.805

---

## Resumen Ejecutivo

Los cambios abarcan cuatro grandes áreas: **seguridad y autenticación**, **nuevas APIs REST**, **refactorización de servicios de backend**, y una **reescritura completa de la capa de vistas** (Sidebars, layouts, íconos y accesibilidad). También se incorporaron archivos completamente nuevos (Helpers, Tests, partials compartidos, y el directorio `wwwroot/css/shared/` y `wwwroot/js/shared/`).

---

## 1. BACKEND — Infraestructura y Configuración

### `Program.cs` (+177 líneas)
| Cambio | Impacto |
|---|---|
| **Rate Limiting nativo de ASP.NET 9** agregado con dos políticas: `LoginByIp` (10 req / 15 min) y `LoginByEmail` (5 req / 15 min) | Protección contra ataques de fuerza bruta y diccionario en el login |
| Respuesta HTTP 429 personalizada: JSON para APIs, HTML con redirect para navegadores | UX mejorada al ser bloqueado por límite de intentos |
| `ApiOrCookie` authorization policy registrada | Permite que las nuevas APIs REST acepten tanto JWT Bearer como cookies de sesión |
| Refactorización de variables: `var` → tipos explícitos en toda la configuración | Calidad de código y legibilidad |

### `SmileTrack_MVC.csproj` (+17 líneas)
- Nuevas referencias a paquetes NuGet para Rate Limiting y capacidades de email extendidas.

### `.gitignore` (+3 líneas)
- Exclusiones adicionales para archivos de entorno local.

---

## 2. BACKEND — Servicios

### `Services/Email/EmailService.cs` (+175 líneas)
| Cambio | Impacto |
|---|---|
| **Nuevo método `SendCitaNotificacionAsync`** — envía email HTML al paciente cuando su cita cambia a `Confirmada` o `Cancelada` | El paciente recibe notificación automática por correo al cambiar el estado de su cita |
| Email con diseño HTML responsivo: banner de color (verde = confirmada, rojo = cancelada), datos de la cita (fecha, profesional, servicio), estado visual destacado | Comunicación profesional y clara con el paciente |
| Envío fire-and-forget con `Task.Run` para no bloquear la respuesta al usuario | Performance: el usuario no espera al SMTP |
| Localización de fecha en español colombiano (`es-CO`) | Coherencia regional |

### `Services/Email/IEmailService.cs` (+20 líneas)
- Declaración de la interfaz `SendCitaNotificacionAsync` para inyección de dependencia.

### `Services/AuthService.cs` (+49 líneas)
- Mejoras internas de validación y tipado explícito.

---

## 3. BACKEND — Helpers (NUEVO — untracked)

### `Helpers/EstadoCitaHelper.cs` — **ARCHIVO NUEVO**
```
ResolveEstadoNombre(string? estado, string? fallback)
NormalizeEstado(string? estado)
```
- **Propósito:** Normalizar y estandarizar los nombres de estado de cita en todo el sistema.
- **Mapeos:** `"programada"/"agendada"` → `"Programada"`, `"confirmada"/"confirmado"` → `"Confirmada"`, `"atendida"/"realizada"/"completada"` → `"Atendida"`, `"cancelada"` → `"Cancelada"`.
- **Impacto:** Elimina inconsistencias de capitalización y variantes de texto que causaban bugs en filtros y reportes.

---

## 4. BACKEND — Modelos

### `Models/ViewModels/CentroDeAyudaViewModel.cs` (+121 líneas)
- Nuevos ViewModels: `SupportTicketViewModel`, `UsuarioViewModel`, `CentroAyudaGuidePanel`, `CentroAyudaSupportPanel`.
- Permiten pasar datos tipados a la vista de Centro de Ayuda en lugar de ViewBag dinámico.

### `Models/ViewModels/AuthViewModels.cs` (+9 líneas)
- Campos adicionales para el flujo de autenticación mejorado.

### `Models/Shared/PagedResult.cs` (+2 líneas)
- Ajuste menor de tipado.

---

## 5. BACKEND — Controladores

### `Controllers/GestionCitasController.cs` (+461 líneas) ⭐ Mayor cambio

| Cambio | Detalle |
|---|---|
| **Inyección de `IEmailService`** en el constructor | Permite enviar emails desde el controlador |
| **DTO `CitaApiUpdateDto`** creado internamente | Serialización tipada para la API de actualización de citas |
| **`GET /api/citas`** — nueva API REST paginada | Devuelve citas filtradas por rol: Paciente solo ve sus citas, Profesional solo las suyas, Admin ve todas. Paginada (page, pageSize hasta 500) |
| **`PUT /api/citas/{id}`** — nueva API REST de actualización | Permite actualizar estado, profesional, servicio, consultorio, notas y fecha desde el frontend sin recargar la página |
| **`POST /api/citas/{id}/confirmar`** y **`/cancelar`** | Endpoints dedicados para confirmar/cancelar citas + disparo automático de email al paciente |
| **Auditoría automática** en creación/actualización de citas desde Agenda | Registra en bitácora cada cambio con datos anteriores y nuevos |

### `Controllers/AccesoYSeguridadController.cs` (+78 líneas)
| Cambio | Detalle |
|---|---|
| `[EnableRateLimiting("LoginByIp")]` en `LoginPost` | Activa el rate limit en el endpoint de login |
| Variables `var` → tipos explícitos | Refactorización de calidad |
| Mejoras en manejo de claims de `IdPaciente` e `IdProfesional` | Más robusto al extraer datos de sesión |

### `Controllers/CentroDeAyudaController.cs` (+249 líneas) ⭐ Reescritura total
| Cambio | Detalle |
|---|---|
| Renombrado de `CentroAyudaController` → `CentroDeAyudaController` | Consistencia de nomenclatura |
| **3 rutas activas documentadas** con `[Authorize]` | `/guias-tutoriales`, `/como-programar-cita`, `/soporte` |
| **5 rutas legacy** con redirección | Mantiene compatibilidad con URLs anteriores sin romper enlaces |
| Acción `Soporte()` con modelo tipado `SupportTicketViewModel` | Vista de soporte con datos reales del usuario autenticado (iniciales, nombre, email) |
| Documentación XML completa del controlador | |

### `Controllers/FacturacionPagosController.cs` (+20 líneas)
- Refactorización menor y mejoras de tipado.

### `Controllers/GestionPacientesController.cs`, `HistoriaClinicaController.cs`, `PerfilesController.cs`, `PqrController.cs`, `PublicoController.cs`, `ReportesController.cs`, `ServiciosRecursosController.cs`, `ViewProxyController.cs`, `GestionProfesionalesController.cs`
- Correcciones de `var` → tipos explícitos, ajustes de rutas y pequeñas mejoras de robustez en manejo de errores.

---

## 6. TESTS (NUEVO — untracked)

### `SmileTrack_MVC.Tests/Unit/EstadoCitaHelperTests.cs` — **ARCHIVO NUEVO**
- Tests unitarios para `EstadoCitaHelper`: cubre todos los casos de normalización de estado (programada, confirmada, atendida, cancelada, fallback, null, vacío).

---

## 7. VISTAS — Sidebars (Componentes Compartidos)

### `Views/shared/_SidebarAdmin.cshtml` (+250 líneas) ⭐ Reescritura total

**Qué se agregó / cambió:**
| Elemento | Antes | Después |
|---|---|---|
| Estructura HTML | Solo `<nav>` suelto | `<aside class="sidebar" id="sidebar">` completo con `aria-label` |
| **Logo del sistema** | ❌ No existía | ✅ `<img src="~/images/Imagenes/Logos/logo.jpg">` en header del sidebar |
| **Bloque de usuario** | ❌ No existía | ✅ Muestra nombre real, email y avatar con iniciales calculadas dinámicamente desde los Claims del usuario autenticado |
| `aria-expanded` en grupos | ❌ No tenía | ✅ Agregado para accesibilidad |
| **Centro de Ayuda** | ❌ No estaba en el menú | ✅ Nuevo grupo con enlace a `/centro-de-ayuda/guias-tutoriales` |
| Indentación y estructura | Items directos sin jerarquía clara | Grupos `nav-group` correctamente anidados e indentados |
| Gestión de Citas | Enlace simple | Dashboard, Agenda General, Gestión Integral — 3 ítems |
| Configuración General | ❌ No estaba | ✅ Enlace a `/servicios-y-recursos/st-adm-16-configuracion-general` |

### `Views/shared/_SidebarRecepcionista.cshtml` (+179 líneas) ⭐ Reescritura total

**Qué se agregó / cambió:**
| Elemento | Detalle |
|---|---|
| `<aside>` completo con `aria-label` | Estructura semántica correcta |
| **Logo del sistema** | Imagen del logo en el header del sidebar |
| **Bloque de usuario con datos reales** | Nombre, email e iniciales del recepcionista autenticado via Claims |
| `aria-expanded` en todos los `nav-group-header` | Accesibilidad keyboard-first |
| **Grupo "Gestión de Citas"** ampliado | Dashboard Recepción, Gestión de Citas, Recordatorios |
| **Centro de Ayuda** | Nuevo ítem en el menú |
| Botón de **Cerrar Sesión** | Visible en el sidebar (era solo accesible desde otras vistas) |

### `Views/shared/_SidebarAuxiliar.cshtml` (+148 líneas) ⭐ Reescritura total

**Qué se agregó / cambió:**
| Elemento | Detalle |
|---|---|
| `<aside>` + logo + bloque de usuario | Misma estructura unificada que Admin y Recepcionista |
| Datos reales del auxiliar autenticado | Claims → nombre, email, iniciales |
| Gestión de Citas expandida | Panel Operativo, Agenda de Apoyo, Estado del Consultorio, Citas Finalizadas |
| Historia Clínica | Control Post-operatorio, Documentos Clínicos |
| Centro de Ayuda | Nuevo ítem |

---

## 8. VISTAS — Capa de Interfaz de Usuario

### Archivos Nuevos (untracked)

#### `Views/shared/_Toasts.cshtml` — **NUEVO**
- Sistema de notificaciones flotantes (toasts) globales.
- Lee `TempData["SuccessMessage"]`, `["ErrorMessage"]`, `["InfoMessage"]` y los muestra automáticamente con `window.ToastService`.
- Incluye el CSS y JS de toasts por sí solo.

#### `Views/shared/_ConfirmModal.cshtml` — **NUEVO**
- Modal de confirmación reutilizable en todo el sistema.

#### `Views/Publico/privacidad.cshtml` — **NUEVO**
- Vista de política de privacidad.

#### `Views/Publico/terminos.cshtml` — **NUEVO**
- Vista de términos y condiciones.

---

### Módulo Acceso y Seguridad

#### `login/index.cshtml` (+24 líneas)
- Mensaje de error visual cuando el usuario es bloqueado por rate limit (`?rateLimited=1`).
- Mejoras de UX en validación del formulario.

#### `recover/index.cshtml` (+51 líneas)
- Flujo de recuperación de contraseña mejorado.
- Mensajes de éxito/error más descriptivos.

#### `register/index.cshtml` (+73 líneas)
- Validaciones en tiempo real más robustas.
- Mejoras de accesibilidad (ARIA labels).

---

### Módulo Gestión de Citas

#### `st-adm-01-dashboard/index.cshtml` (-532 → refactorización)
- Simplificación de la vista; datos dinámicos desde API en lugar de HTML estático.
- Tabla de citas ahora se puebla via JavaScript con la API `/api/citas`.

#### `st-rec-03-gestion-citas/index.cshtml` (+101 líneas netas)
- Integración con los nuevos endpoints de confirmación y cancelación.
- Botones de acción que invocan `/api/citas/{id}/confirmar` y `/api/citas/{id}/cancelar`.
- Paginación mejorada.

#### `st-rec-05-recordatorios/index.cshtml` (en proceso de corrección de íconos)
- **`??` SMS/Correo** → `<span class="material-symbols-outlined">sms</span>` / `mail`
- **`??` Enviar** → `<span class="material-symbols-outlined">send</span>`
- **Alertas section** → íconos `warning`, `error`, `invoice`
- **Mobile nav** → íconos `dashboard`, `event`, `notifications`, `login`
- Footer → `<span class="material-symbols-outlined">copyright</span>`

#### `st-pac-03-notificaciones/index.cshtml` (+83 líneas)
- Filtros "Recordatorios", "Confirmaciones", "Cancelaciones", "Mensajes" conectados a la nueva API de citas.
- Íconos `??` reemplazados por `material-symbols-outlined`.

#### `st-odo-02-agenda`, `st-aux-*`, `st-pac-01-mis-citas`, `st-rec-01-dashboard`
- Corrección de íconos `??` en todos los archivos.
- Integración con nuevos endpoints API.

---

### Módulo Historia Clínica

#### `historial-adm.cshtml`, `historial-rec.cshtml` (+23 / +41 líneas)
- Material Symbols font añadido donde faltaba.
- Íconos corregidos.

---

### Módulo Perfiles

#### `perfilrecepcionista.cshtml` (+159 → simplificación)
- Íconos `??` reemplazados con `material-symbols-outlined`: edit, lock, tune, history, warning.
- Mobile nav corregido.

#### `mi-perfil.cshtml` (Auxiliar) (+144 → simplificación)
- Mismo patrón de corrección de íconos.

---

### Módulo Centro de Ayuda

#### `Guias_Tutoriales_y_Soporte/index.cshtml` (+148 → refactorización)
- Vista reescrita para usar el nuevo `CentroDeAyudaViewModel`.
- Contenido real de guías y paneles de soporte.

#### `SoporteTicket.cshtml` (+10 líneas)
- Pequeño ajuste de layout.

---

### Módulo Servicios y Recursos

#### `catalogoservicios.cshtml` (+156 → simplificación)
- Íconos `??` en buscador y nav móvil reemplazados.

#### `st-adm-16-configuracion-general/index.cshtml` (+171 → simplificación)
- Botones de edición con íconos `??` → `edit` (Material Symbols).
- Nav móvil completo corregido.

---

## 9. CSS — Nuevos Archivos y Estilos

### `wwwroot/css/shared/` — **DIRECTORIO COMPLETAMENTE NUEVO (untracked)**

| Archivo | Descripción |
|---|---|
| `icons.css` | Importación centralizada de Material Symbols Outlined + clase `.material-symbols-outlined` con variantes (`icon-sm`, `icon-md`, `icon-lg`, `icon-xl`, `icon-filled`) |
| `toasts.css` | Sistema de toasts: animaciones, variantes success/error/warning/info, responsive móvil |
| `sidebar.css` | Estilos unificados del sidebar para todos los roles (18.9 KB) |
| `modals.css` | Estilos del modal de confirmación |
| `print.css` | Estilos de impresión para vistas con tablas |

### Archivos CSS Modificados
| Archivo | Cambio |
|---|---|
| `login/login.css` (+13) | Estilos para el mensaje de rate limit |
| `st-adm-09-citas/gestionintegral.css` (+65) | Estilos para los nuevos botones de acción en la tabla de citas |
| `st-rec-03-gestion-citas/styles.css` (+32) | Estilos adicionales para paginación |
| `st-adm-14-reportes-clinicos/styles.css` (+54) | Nuevos estilos para los reportes |
| `Reportes/vista_admin/styles.css` (-223) | Simplificación — estilos movidos al shared |
| `Reportes/vista_prof/styles.css` (-86) | Simplificación |

---

## 10. JavaScript — Nuevos Archivos y Scripts

### `wwwroot/js/shared/` — **DIRECTORIO COMPLETAMENTE NUEVO (untracked)**

| Archivo | Descripción |
|---|---|
| `toasts.js` | `window.ToastService` con métodos `success()`, `error()`, `warning()`, `info()` — sistema global de notificaciones |
| `sidebar.js` | Lógica del sidebar: hamburger menú, overlay, grupos colapsables, estado activo automático por URL (16 KB) |
| `modals.js` | Gestión del modal de confirmación reutilizable |
| `validation-utils.js` | Utilidades de validación de formularios compartidas |
| `appointment-utils.js` | Utilidades para gestión de citas compartidas (15 KB) |

### Scripts JS Modificados (selección)

| Archivo | Cambio principal |
|---|---|
| `login/login.js` (+142) | Integración con rate limit: detecta 429, muestra countdown, deshabilita botón |
| `recover/recover.js` (+172) | Flujo renovado: validación en tiempo real, feedback visual |
| `register/register.js` (+232) | Validación robusta, checks en tiempo real |
| `st-adm-01-dashboard/app.js` (+39) | Carga dinámica de citas desde `/api/citas` |
| `st-adm-08-agenda/agendageneral.js` (+114) | Integración con API de confirmación/cancelación |
| `st-adm-09-citas/gestionintegral.js` (+248) | CRUD completo via API REST |
| `st-rec-03-gestion-citas/app.js` (+51) | Acciones de confirmación/cancelación con feedback toast |
| `st-rec-05-recordatorios/app.js` (-98) | Simplificado: lógica de envío de recordatorios depurada |

---

## Resumen de Impacto por Área

```
┌─────────────────────────────────────┬───────────┬──────────┐
│ Área                                │ Archivos  │ Impacto  │
├─────────────────────────────────────┼───────────┼──────────┤
│ Seguridad (Rate Limiting, Auth)     │ 3         │ 🔴 Alto  │
│ Nuevas APIs REST de Citas           │ 1         │ 🔴 Alto  │
│ Notificaciones Email Citas          │ 2         │ 🔴 Alto  │
│ Sidebars (reescritura total)        │ 3         │ 🔴 Alto  │
│ Sistema de Toasts (global)          │ 3         │ 🟡 Medio │
│ Sistema de Íconos (icons.css)       │ 1 (nuevo) │ 🟡 Medio │
│ Corrección de íconos ?? en vistas   │ ~40       │ 🟡 Medio │
│ Helper EstadoCita + Tests           │ 2 (nuevo) │ 🟡 Medio │
│ CSS/JS compartido                   │ 10 (nuevo)│ 🟡 Medio │
│ Centro de Ayuda (reescritura)       │ 2         │ 🟢 Bajo  │
│ Refactorización `var` → explícito   │ 12+       │ 🟢 Bajo  │
└─────────────────────────────────────┴───────────┴──────────┘
```

> [!WARNING]
> Los archivos en `wwwroot/css/shared/` y `wwwroot/js/shared/` son **untracked** — nunca han sido commiteados. Un `git checkout .` o `git clean -fd` los eliminaría permanentemente. Se recomienda hacer commit pronto.

> [!IMPORTANT]
> El módulo de Rate Limiting en `Program.cs` requiere que `builder.Services.AddRateLimiter()` esté registrado **antes** de `builder.Services.AddAuthentication()` en el pipeline para funcionar correctamente.
