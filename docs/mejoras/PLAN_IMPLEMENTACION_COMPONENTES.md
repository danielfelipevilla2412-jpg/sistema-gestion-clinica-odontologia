# 🔧 PLAN DE IMPLEMENTACIÓN: BIBLIOTECA DE COMPONENTES COMPARTIDOS

## 📅 Fecha
**15 de septiembre de 2026**

---

## 🎯 Objetivo

Crear una biblioteca centralizada de componentes reutilizables que permita aplicar las mejoras de **st-odo-01-dashboard** y **st-odo-02-agenda** a las demás vistas del sistema sin duplicar código.

---

## 🏗️ ARQUITECTURA PROPUESTA

### **Estructura de Directorios**

```
sistema-gestion-clinica-odontologia/
│
├── Views/
│   └── Shared/
│       └── Components/
│           ├── ProximaCitaWidget/
│           │   ├── Default.cshtml
│           │   └── ProximaCitaWidgetViewComponent.cs
│           ├── PanelEstadisticas/
│           │   ├── Default.cshtml
│           │   └── PanelEstadisticasViewComponent.cs
│           ├── BuscadorTiempoReal/
│           │   ├── Default.cshtml
│           │   └── BuscadorTiempoRealViewComponent.cs
│           ├── NotificacionesInteligentes/
│           │   ├── Default.cshtml
│           │   └── NotificacionesInteligentesViewComponent.cs
│           ├── ToggleVistas/
│           │   ├── Default.cshtml
│           │   └── ToggleVistasViewComponent.cs
│           ├── PanelRendimiento/
│           │   ├── Default.cshtml
│           │   └── PanelRendimientoViewComponent.cs
│           ├── IndicadoresTiempo/
│           │   ├── Default.cshtml
│           │   └── IndicadoresTiempoViewComponent.cs
│           └── AccesosRapidos/
│               ├── Default.cshtml
│               └── AccesosRapidosViewComponent.cs
│
├── wwwroot/
│   ├── css/
│   │   └── shared/
│   │       └── components/
│   │           ├── proxima-cita.css
│   │           ├── panel-estadisticas.css
│   │           ├── buscador-tiempo-real.css
│   │           ├── notificaciones.css
│   │           ├── toggle-vistas.css
│   │           ├── panel-rendimiento.css
│   │           ├── indicadores-tiempo.css
│   │           ├── accesos-rapidos.css
│   │           └── animations.css
│   │
│   └── js/
│       └── shared/
│           └── components/
│               ├── ProximaCitaWidget.js
│               ├── PanelEstadisticas.js
│               ├── BuscadorTiempoReal.js
│               ├── NotificacionesInteligentes.js
│               ├── ToggleVistas.js
│               ├── PanelRendimiento.js
│               ├── IndicadoresTiempo.js
│               ├── AccesosRapidos.js
│               └── utils/
│                   ├── keyboard-shortcuts.js
│                   ├── auto-update.js
│                   └── accessibility.js
│
└── Models/
    └── ViewModels/
        └── Components/
            ├── ProximaCitaWidgetViewModel.cs
            ├── PanelEstadisticasViewModel.cs
            ├── NotificacionesViewModel.cs
            └── AccesosRapidosViewModel.cs
```

---

## 📦 COMPONENTES A CREAR

### **1️⃣ ProximaCitaWidget**

**Propósito:** Mostrar la próxima cita con countdown y contexto  
**Usado en:** st-odo-01-dashboard, st-pac-01-mis-citas, st-rec-01-dashboard (adaptado)

#### **ViewModel**

```csharp
// Models/ViewModels/Components/ProximaCitaWidgetViewModel.cs
public class ProximaCitaWidgetViewModel
{
    public int? IdCita { get; set; }
    public string NombrePaciente { get; set; }
    public string NombreServicio { get; set; }
    public DateTime FechaHora { get; set; }
    public string NombreConsultorio { get; set; }
    public string UltimaAtencion { get; set; }
    public bool EsUrgente => (FechaHora - DateTime.Now).TotalMinutes <= 15;
    public int MinutosHasta => (int)(FechaHora - DateTime.Now).TotalMinutes;
}
```

#### **ViewComponent**

```csharp
// Views/Shared/Components/ProximaCitaWidget/ProximaCitaWidgetViewComponent.cs
public class ProximaCitaWidgetViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public ProximaCitaWidgetViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync(int? idProfesional = null, int? idPaciente = null)
    {
        var query = _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora > DateTime.Now)
            .Where(c => c.IdEstadoCita != 4 && c.IdEstadoCita != 5); // No canceladas ni completadas

        if (idProfesional.HasValue)
            query = query.Where(c => c.IdProfesional == idProfesional.Value);
        
        if (idPaciente.HasValue)
            query = query.Where(c => c.IdPaciente == idPaciente.Value);

        var proximaCita = await query
            .OrderBy(c => c.FechaHora)
            .FirstOrDefaultAsync();

        if (proximaCita == null)
            return View(new ProximaCitaWidgetViewModel());

        var ultimaAtencion = await _context.Citas
            .Where(c => c.IdPaciente == proximaCita.IdPaciente)
            .Where(c => c.IdEstadoCita == 3) // Atendida
            .OrderByDescending(c => c.FechaHora)
            .Select(c => c.FechaHora)
            .FirstOrDefaultAsync();

        var viewModel = new ProximaCitaWidgetViewModel
        {
            IdCita = proximaCita.IdCita,
            NombrePaciente = $"{proximaCita.Paciente.Nombre} {proximaCita.Paciente.Apellido}",
            NombreServicio = proximaCita.Servicio.Nombre,
            FechaHora = proximaCita.FechaHora,
            NombreConsultorio = proximaCita.Consultorio.Nombre,
            UltimaAtencion = ultimaAtencion != default 
                ? $"Última atención: {ultimaAtencion:dd/MM/yyyy}" 
                : "Primera visita"
        };

        return View(viewModel);
    }
}
```

#### **Vista**

```cshtml
@* Views/Shared/Components/ProximaCitaWidget/Default.cshtml *@
@model ProximaCitaWidgetViewModel

<link rel="stylesheet" href="~/css/shared/components/proxima-cita.css" asp-append-version="true" />

@if (Model.IdCita.HasValue)
{
    <section class="proxima-cita-banner @(Model.EsUrgente ? "urgent" : "upcoming")" 
             data-fecha="@Model.FechaHora.ToString("o")">
        <div class="banner-icon">
            <span class="material-symbols-outlined">schedule</span>
        </div>
        <div class="banner-content">
            <h3 class="banner-title">Próxima cita @(Model.EsUrgente ? "¡URGENTE!" : "")</h3>
            <p class="banner-paciente">@Model.NombrePaciente</p>
            <p class="banner-servicio">@Model.NombreServicio</p>
            <p class="banner-meta">
                <span>@Model.FechaHora.ToString("HH:mm")</span> • 
                <span>@Model.NombreConsultorio</span>
            </p>
            @if (!string.IsNullOrEmpty(Model.UltimaAtencion))
            {
                <p class="banner-ultima">@Model.UltimaAtencion</p>
            }
        </div>
        <div class="banner-countdown">
            <span class="countdown-value" id="countdownMinutes">@Model.MinutosHasta</span>
            <span class="countdown-label">minutos</span>
        </div>
    </section>
}
else
{
    <section class="proxima-cita-banner empty">
        <div class="banner-icon">
            <span class="material-symbols-outlined">event_available</span>
        </div>
        <div class="banner-content">
            <h3 class="banner-title">No tienes citas programadas</h3>
            <p class="banner-subtitle">Tu agenda está despejada</p>
        </div>
    </section>
}

<script src="~/js/shared/components/ProximaCitaWidget.js" asp-append-version="true"></script>
```

#### **JavaScript**

```javascript
// wwwroot/js/shared/components/ProximaCitaWidget.js
class ProximaCitaWidget {
    constructor(selector) {
        this.banner = document.querySelector(selector);
        if (!this.banner) return;
        
        this.fechaCita = new Date(this.banner.dataset.fecha);
        this.countdownElement = this.banner.querySelector('#countdownMinutes');
        
        this.iniciar();
    }

    iniciar() {
        this.actualizarCountdown();
        setInterval(() => this.actualizarCountdown(), 60000); // Cada minuto
    }

    actualizarCountdown() {
        const ahora = new Date();
        const diffMs = this.fechaCita - ahora;
        const minutos = Math.floor(diffMs / 60000);
        
        if (this.countdownElement) {
            this.countdownElement.textContent = minutos;
        }
        
        // Actualizar clase urgent/upcoming
        if (minutos <= 15) {
            this.banner.classList.add('urgent');
            this.banner.classList.remove('upcoming');
        } else {
            this.banner.classList.remove('urgent');
            this.banner.classList.add('upcoming');
        }
    }
}

// Auto-inicializar
document.addEventListener('DOMContentLoaded', () => {
    new ProximaCitaWidget('.proxima-cita-banner');
});
```

#### **Uso en una Vista**

```cshtml
@* En cualquier vista *@
@await Component.InvokeAsync("ProximaCitaWidget", new { idProfesional = User.FindFirst("IdProfesional")?.Value })

@* O para paciente *@
@await Component.InvokeAsync("ProximaCitaWidget", new { idPaciente = User.FindFirst("IdPaciente")?.Value })
```

---

### **2️⃣ BuscadorTiempoReal**

**Propósito:** Búsqueda con debounce y highlight de resultados  
**Usado en:** Todas las vistas con tablas o listas

#### **ViewComponent**

```csharp
// Views/Shared/Components/BuscadorTiempoReal/BuscadorTiempoRealViewComponent.cs
public class BuscadorTiempoRealViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string placeholder = "Buscar...", string targetSelector = ".searchable-item")
    {
        ViewBag.Placeholder = placeholder;
        ViewBag.TargetSelector = targetSelector;
        return View();
    }
}
```

#### **Vista**

```cshtml
@* Views/Shared/Components/BuscadorTiempoReal/Default.cshtml *@
<link rel="stylesheet" href="~/css/shared/components/buscador-tiempo-real.css" asp-append-version="true" />

<div class="search-wrapper">
    <span class="search-icon material-symbols-outlined">search</span>
    <input type="search" 
           id="searchRealTime" 
           class="search-input" 
           placeholder="@ViewBag.Placeholder"
           data-target="@ViewBag.TargetSelector"
           autocomplete="off">
    <span class="search-count" id="searchCount"></span>
</div>

<script src="~/js/shared/components/BuscadorTiempoReal.js" asp-append-version="true"></script>
```

#### **JavaScript**

```javascript
// wwwroot/js/shared/components/BuscadorTiempoReal.js
class BuscadorTiempoReal {
    constructor(inputSelector, targetSelector) {
        this.input = document.querySelector(inputSelector);
        this.targetSelector = targetSelector;
        this.countElement = document.getElementById('searchCount');
        this.debounceTimeout = null;
        
        if (!this.input) return;
        this.iniciar();
    }

    iniciar() {
        this.input.addEventListener('input', (e) => {
            clearTimeout(this.debounceTimeout);
            this.debounceTimeout = setTimeout(() => {
                this.buscar(e.target.value);
            }, 300);
        });

        // ESC para limpiar
        this.input.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.input.value = '';
                this.buscar('');
            }
        });
    }

    buscar(query) {
        const items = document.querySelectorAll(this.targetSelector);
        const lowerQuery = query.toLowerCase().trim();
        let matchCount = 0;

        items.forEach(item => {
            const text = item.textContent.toLowerCase();
            const matches = !lowerQuery || text.includes(lowerQuery);

            if (matches) {
                item.style.display = '';
                item.classList.remove('search-hidden');
                if (lowerQuery) {
                    item.classList.add('search-match');
                    this.highlight(item, lowerQuery);
                    matchCount++;
                } else {
                    item.classList.remove('search-match');
                    this.removeHighlight(item);
                }
            } else {
                item.style.display = 'none';
                item.classList.add('search-hidden');
            }
        });

        // Actualizar contador
        if (this.countElement) {
            if (lowerQuery) {
                this.countElement.textContent = `${matchCount} resultado${matchCount !== 1 ? 's' : ''}`;
                this.countElement.style.display = 'inline';
            } else {
                this.countElement.style.display = 'none';
            }
        }
    }

    highlight(element, query) {
        // Implementación simple, se puede mejorar con Mark.js
        element.setAttribute('data-original-html', element.innerHTML);
        const regex = new RegExp(`(${this.escapeRegex(query)})`, 'gi');
        element.innerHTML = element.textContent.replace(regex, '<mark>$1</mark>');
    }

    removeHighlight(element) {
        const original = element.getAttribute('data-original-html');
        if (original) {
            element.innerHTML = original;
            element.removeAttribute('data-original-html');
        }
    }

    escapeRegex(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }
}

// Auto-inicializar
document.addEventListener('DOMContentLoaded', () => {
    const input = document.getElementById('searchRealTime');
    if (input) {
        const target = input.dataset.target || '.searchable-item';
        new BuscadorTiempoReal('#searchRealTime', target);
    }
});
```

---

### **3️⃣ PanelEstadisticas**

**Propósito:** Panel de KPIs con animación de conteo  
**Usado en:** Todos los dashboards

#### **ViewModel**

```csharp
// Models/ViewModels/Components/PanelEstadisticasViewModel.cs
public class EstadisticaItem
{
    public string Color { get; set; } // blue, green, orange, purple, red
    public int Valor { get; set; }
    public string Etiqueta { get; set; }
    public string SubEtiqueta { get; set; }
    public string Tooltip { get; set; }
}

public class PanelEstadisticasViewModel
{
    public List<EstadisticaItem> Estadisticas { get; set; } = new();
}
```

#### **ViewComponent**

```csharp
// Views/Shared/Components/PanelEstadisticas/PanelEstadisticasViewComponent.cs
public class PanelEstadisticasViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(List<EstadisticaItem> estadisticas)
    {
        var viewModel = new PanelEstadisticasViewModel
        {
            Estadisticas = estadisticas
        };
        return View(viewModel);
    }
}
```

#### **Vista**

```cshtml
@* Views/Shared/Components/PanelEstadisticas/Default.cshtml *@
@model PanelEstadisticasViewModel

<link rel="stylesheet" href="~/css/shared/components/panel-estadisticas.css" asp-append-version="true" />

<section class="stats-grid" aria-label="Panel de estadísticas">
    @foreach (var stat in Model.Estadisticas)
    {
        <article class="stat-card" data-color="@stat.Color" title="@stat.Tooltip">
            <span class="stat-number" data-target="@stat.Valor" aria-live="polite">0</span>
            <span class="stat-label">@stat.Etiqueta</span>
            @if (!string.IsNullOrEmpty(stat.SubEtiqueta))
            {
                <span class="stat-sub">@stat.SubEtiqueta</span>
            }
        </article>
    }
</section>

<script src="~/js/shared/components/PanelEstadisticas.js" asp-append-version="true"></script>
```

#### **Uso**

```csharp
// En el controller
var stats = new List<EstadisticaItem>
{
    new() { Color = "blue", Valor = 150, Etiqueta = "Pacientes activos", Tooltip = "Total de pacientes registrados" },
    new() { Color = "green", Valor = 45, Etiqueta = "Citas hoy", SubEtiqueta = "No canceladas" },
    new() { Color = "orange", Valor = 8, Etiqueta = "Profesionales", SubEtiqueta = "En servicio" }
};

// En la vista
@await Component.InvokeAsync("PanelEstadisticas", new { estadisticas = stats })
```

---

### **4️⃣ NotificacionesInteligentes**

**Propósito:** Panel de notificaciones contextuales  
**Usado en:** st-odo-01-dashboard, st-rec-01-dashboard, st-aux-01-panel-operativo

#### **ViewModel**

```csharp
// Models/ViewModels/Components/NotificacionesViewModel.cs
public enum TipoNotificacion
{
    Info,
    Warning,
    Urgent
}

public class NotificacionItem
{
    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; }
    public string Mensaje { get; set; }
    public string Accion { get; set; } // Texto del botón
    public string UrlAccion { get; set; }
}

public class NotificacionesViewModel
{
    public List<NotificacionItem> Notificaciones { get; set; } = new();
}
```

#### **ViewComponent**

```csharp
// Views/Shared/Components/NotificacionesInteligentes/NotificacionesInteligentesViewComponent.cs
public class NotificacionesInteligentesViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public NotificacionesInteligentesViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync(int idProfesional)
    {
        var notificaciones = new List<NotificacionItem>();

        // Citas pendientes de confirmar (próximas 24h)
        var citasSinConfirmar = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional)
            .Where(c => c.IdEstadoCita == 1) // Agendada
            .Where(c => c.FechaHora >= DateTime.Now && c.FechaHora <= DateTime.Now.AddHours(24))
            .CountAsync();

        if (citasSinConfirmar > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Warning,
                Titulo = $"{citasSinConfirmar} cita{(citasSinConfirmar > 1 ? "s" : "")} sin confirmar",
                Mensaje = "Recuerda confirmar con los pacientes",
                Accion = "Ver citas",
                UrlAccion = "/gestion-citas/st-odo-02-agenda"
            });
        }

        // Pacientes en sala de espera
        var pacientesEspera = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional)
            .Where(c => c.IdEstadoCita == 2) // Confirmada (en espera)
            .Where(c => c.FechaHora.Date == DateTime.Today)
            .Where(c => c.FechaHora <= DateTime.Now)
            .CountAsync();

        if (pacientesEspera > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Urgent,
                Titulo = $"{pacientesEspera} paciente{(pacientesEspera > 1 ? "s" : "")} en espera",
                Mensaje = "Listos para atención",
                Accion = "Atender",
                UrlAccion = "/gestion-citas/st-odo-02-agenda"
            });
        }

        // Historias clínicas pendientes
        var historiasPendientes = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional)
            .Where(c => c.IdEstadoCita == 3) // Atendida
            .Where(c => c.FechaHora >= DateTime.Today.AddDays(-7))
            .Where(c => c.IdHistoriaClinica == null)
            .CountAsync();

        if (historiasPendientes > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Info,
                Titulo = $"{historiasPendientes} historia{(historiasPendientes > 1 ? "s" : "")} clínica{(historiasPendientes > 1 ? "s" : "")} pendiente{(historiasPendientes > 1 ? "s" : "")}",
                Mensaje = "Completa los registros de la semana",
                Accion = "Completar",
                UrlAccion = "/historia-clinica/pendientes"
            });
        }

        var viewModel = new NotificacionesViewModel
        {
            Notificaciones = notificaciones
        };

        return View(viewModel);
    }
}
```

#### **Vista**

```cshtml
@* Views/Shared/Components/NotificacionesInteligentes/Default.cshtml *@
@model NotificacionesViewModel

<link rel="stylesheet" href="~/css/shared/components/notificaciones.css" asp-append-version="true" />

<section class="notifications-panel" aria-label="Notificaciones">
    <h3 class="panel-title">Notificaciones</h3>
    
    @if (Model.Notificaciones.Any())
    {
        <div class="notifications-list">
            @foreach (var notif in Model.Notificaciones)
            {
                var tipoClass = notif.Tipo.ToString().ToLower();
                <article class="notification-item notification-@tipoClass" data-dismissible="true">
                    <div class="notification-icon">
                        <span class="material-symbols-outlined">
                            @switch (notif.Tipo)
                            {
                                case TipoNotificacion.Urgent:
                                    @:priority_high
                                    break;
                                case TipoNotificacion.Warning:
                                    @:warning
                                    break;
                                default:
                                    @:info
                                    break;
                            }
                        </span>
                    </div>
                    <div class="notification-content">
                        <h4 class="notification-title">@notif.Titulo</h4>
                        <p class="notification-message">@notif.Mensaje</p>
                        @if (!string.IsNullOrEmpty(notif.UrlAccion))
                        {
                            <a href="@notif.UrlAccion" class="notification-action">@notif.Accion</a>
                        }
                    </div>
                    <button class="notification-dismiss" aria-label="Descartar notificación">
                        <span class="material-symbols-outlined">close</span>
                    </button>
                </article>
            }
        </div>
    }
    else
    {
        <div class="notifications-empty">
            <span class="material-symbols-outlined">check_circle</span>
            <p>Todo al día</p>
        </div>
    }
</section>

<script src="~/js/shared/components/NotificacionesInteligentes.js" asp-append-version="true"></script>
```

---

## 📝 PASOS DE IMPLEMENTACIÓN

### **Fase 1: Preparación (1 semana)**

1. ✅ Crear estructura de carpetas
2. ✅ Configurar namespace `SmileTrack_MVC.ViewComponents`
3. ✅ Crear ViewModels base
4. ✅ Configurar bundling para CSS/JS compartido

### **Fase 2: Componentes Core (2 semanas)**

1. ✅ ProximaCitaWidget
2. ✅ BuscadorTiempoReal
3. ✅ PanelEstadisticas
4. ✅ NotificacionesInteligentes

### **Fase 3: Componentes Avanzados (2 semanas)**

5. ✅ ToggleVistas
6. ✅ PanelRendimiento
7. ✅ IndicadoresTiempo
8. ✅ AccesosRapidos

### **Fase 4: Utilidades JS (1 semana)**

9. ✅ keyboard-shortcuts.js
10. ✅ auto-update.js
11. ✅ accessibility.js
12. ✅ drag-drop.js

### **Fase 5: Testing (1 semana)**

13. ✅ Unit tests para ViewComponents
14. ✅ Integration tests
15. ✅ Testing manual en navegadores
16. ✅ Accessibility testing

---

## 🎯 BENEFICIOS DE ESTA ARQUITECTURA

### **1. DRY (Don't Repeat Yourself)**
- Código escrito 1 vez, usado en N vistas
- Mantenimiento centralizado
- Bugs corregidos globalmente

### **2. Consistencia de UX**
- Comportamiento idéntico en todas las vistas
- Estilos unificados
- Experiencia predecible

### **3. Facilidad de Actualización**
- Cambiar componente → Afecta todas las vistas automáticamente
- Versioning con asp-append-version
- Cache busting automático

### **4. Testing Simplificado**
- Test unitario del componente aislado
- Mock del DbContext
- CI/CD más sencillo

### **5. Onboarding de Desarrolladores**
- Documentación centralizada
- Ejemplos de uso claros
- Componentes autodocumentados

---

## 📊 COMPARACIÓN: ANTES vs. DESPUÉS

### **Antes (Sin Componentes)**

```
Implementar búsqueda en 10 vistas:
- 10 × HTML = 10 archivos modificados
- 10 × CSS = 10 archivos CSS
- 10 × JS = 10 archivos JS
- Testing = 10 × tiempo
- Bugs = 10 × probabilidad
- Tiempo total = 20 horas
```

### **Después (Con Componentes)**

```
Implementar búsqueda en 10 vistas:
- 1 × ViewComponent = 1 archivo
- 1 × CSS = 1 archivo
- 1 × JS = 1 archivo
- 10 × línea de invocación en vistas
- Testing = 1 × tiempo
- Bugs = 1 × probabilidad
- Tiempo total = 4 horas

AHORRO: 80% de tiempo
```

---

## ✅ CONCLUSIÓN

La implementación de componentes compartidos reduce el esfuerzo de aplicar mejoras a las 17 vistas restantes de **18-24 semanas a 10-12 semanas**, un ahorro del **50%**.

Además, garantiza consistencia, facilita mantenimiento y mejora la calidad general del código.

---

**Preparado por:** Johan Santamaria / Antigravity  
**Fecha:** 15 de septiembre de 2026  
**Versión:** 1.0
