# 🚀 Mejoras Propuestas para Vistas del Profesional

## Documento de Mejoras Técnicas y UX
**Fecha:** 15 de septiembre de 2026  
**Autor:** Análisis Sistema SmileTrack  
**Vistas Analizadas:** st-odo-01-dashboard, st-odo-02-agenda

---

## 📊 ST-ODO-01: DASHBOARD DEL ODONTÓLOGO

### 🎯 **PRIORIDAD ALTA - Mejoras Críticas**

#### 1. **Eliminar Botón "Exportar Reporte" del Dashboard**
**Problema:** Funcionalidad duplicada con la vista de reportes profesional.

**Acción:**
```diff
- <button class="btn-primary" id="btnExport">📊 Exportar reporte</button>
+ <a href="/reportes/vista-profesional" class="btn-secondary">
+   <span class="material-symbols-outlined">analytics</span>
+   Ver Reportes Completos
+ </a>
```

**Beneficio:** Reduce confusión y dirige al usuario a la herramienta correcta.

---

#### 2. **Agregar Widget de "Próxima Cita Urgente"**
**Problema:** El profesional tiene que buscar manualmente su próxima cita.

**Nueva Sección a Agregar:**
```html
<!-- NUEVO: Banner de Próxima Cita (después del header) -->
<section class="next-appointment-banner" aria-live="polite">
    @{
        var proximaCita = ViewData["OdoProximaCitaUrgente"] as SmileTrack_MVC.Models.Entities.Cita;
        if (proximaCita != null)
        {
            var minutos = (proximaCita.FechaHora - DateTime.Now).TotalMinutes;
            var urgente = minutos <= 15 && minutos > 0;
            var claseUrgencia = urgente ? "urgent" : "upcoming";
            
            <div class="next-appt-card @claseUrgencia">
                <div class="next-appt-icon">
                    <span class="material-symbols-outlined">schedule</span>
                </div>
                <div class="next-appt-info">
                    <span class="next-appt-label">Próxima cita</span>
                    <strong class="next-appt-patient">@proximaCita.Paciente?.Nombres @proximaCita.Paciente?.Apellidos</strong>
                    <span class="next-appt-time">
                        @proximaCita.FechaHora.ToString("HH:mm") — @proximaCita.Servicio?.Nombre — @proximaCita.Consultorio?.Nombre
                    </span>
                    @if (urgente)
                    {
                        <span class="next-appt-countdown">⏰ En @((int)minutos) minutos</span>
                    }
                </div>
                <a href="/gestion-de-citas/st-odo-02-agenda" class="btn-primary btn-sm">
                    Ver en Agenda
                </a>
            </div>
        }
        else
        {
            <div class="next-appt-card empty">
                <span class="material-symbols-outlined">check_circle</span>
                <span>No tienes citas programadas para hoy. ¡Buen trabajo!</span>
            </div>
        }
    }
</section>
```

**CSS Requerido:**
```css
.next-appointment-banner {
    margin: 1.5rem 0;
}

.next-appt-card {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 1rem 1.5rem;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    border-radius: 12px;
    box-shadow: 0 4px 12px rgba(102, 126, 234, 0.2);
}

.next-appt-card.urgent {
    background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
    animation: pulse 2s ease-in-out infinite;
}

.next-appt-card.empty {
    background: #f0fdf4;
    color: #166534;
    border: 2px solid #86efac;
}

@keyframes pulse {
    0%, 100% { box-shadow: 0 4px 12px rgba(245, 87, 108, 0.3); }
    50% { box-shadow: 0 4px 20px rgba(245, 87, 108, 0.6); }
}

.next-appt-icon {
    font-size: 2.5rem;
    opacity: 0.9;
}

.next-appt-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.next-appt-label {
    font-size: 0.75rem;
    text-transform: uppercase;
    opacity: 0.8;
    font-weight: 600;
}

.next-appt-patient {
    font-size: 1.1rem;
    font-weight: 700;
}

.next-appt-time {
    font-size: 0.9rem;
    opacity: 0.9;
}

.next-appt-countdown {
    font-size: 0.85rem;
    font-weight: 600;
    background: rgba(255,255,255,0.2);
    padding: 0.25rem 0.5rem;
    border-radius: 6px;
    display: inline-block;
    margin-top: 0.25rem;
}
```

**Actualización en Controller:**
```csharp
// Agregar en ProfesionalesController.Stodo01Dashboard()
var proximaCita = citasDelMes
    .Where(c => 
        c.FechaHora >= DateTime.Now &&
        (c.Estado?.ToLower() == "agendada" || 
         c.Estado?.ToLower() == "programada" ||
         c.Estado?.ToLower() == "confirmada"))
    .OrderBy(c => c.FechaHora)
    .FirstOrDefault();

ViewData["OdoProximaCitaUrgente"] = proximaCita;
```

---

#### 3. **Mejorar Barra de Progreso "Sistema Operativo"**
**Problema:** El label "Sistema operativo" es genérico y no aporta valor al profesional.

**Reemplazo Propuesto:**
```diff
- <span class="progress-label">Sistema operativo</span>
+ <span class="progress-label" id="dailyProgressLabel">
+   @atendidasHoyCount de @citasHoyCount citas completadas hoy
+ </span>
```

**Actualizar JavaScript:**
```javascript
// En app.js, actualizar dinámicamente el progreso
function actualizarProgresoDelDia() {
    const totalHoy = parseInt(document.getElementById('statCitas')?.textContent || 0);
    const atendidas = parseInt(document.getElementById('statProfesionales')?.textContent || 0);
    const porcentaje = totalHoy > 0 ? Math.round((atendidas / totalHoy) * 100) : 0;
    
    const progressBar = document.getElementById('topProgressBar');
    const progressLabel = document.getElementById('dailyProgressLabel');
    
    if (progressBar) {
        progressBar.style.width = `${porcentaje}%`;
        progressBar.parentElement.setAttribute('aria-valuenow', porcentaje);
    }
    
    if (progressLabel) {
        progressLabel.textContent = `${atendidas} de ${totalHoy} citas completadas hoy (${porcentaje}%)`;
    }
}

// Llamar después de cargar stats
document.addEventListener('DOMContentLoaded', () => {
    setTimeout(actualizarProgresoDelDia, 1000);
});
```

---

#### 4. **Agregar Accesos Rápidos (Quick Actions)**
**Nueva Sección antes del content-grid:**

```html
<!-- Accesos Rápidos -->
<section class="quick-actions" aria-label="Accesos rápidos">
    <h2 class="section-subtitle">Accesos Rápidos</h2>
    <div class="quick-actions-grid">
        <a href="/gestion-de-citas/st-odo-02-agenda" class="quick-action-card">
            <span class="material-symbols-outlined">calendar_month</span>
            <strong>Mi Agenda</strong>
            <span>Ver y gestionar citas</span>
        </a>
        <a href="/historia-clinica/st-odo-06-pacientes" class="quick-action-card">
            <span class="material-symbols-outlined">folder_open</span>
            <strong>Mis Pacientes</strong>
            <span>Historial clínico</span>
        </a>
        <a href="/reportes/vista-profesional" class="quick-action-card">
            <span class="material-symbols-outlined">analytics</span>
            <strong>Reportes</strong>
            <span>Estadísticas detalladas</span>
        </a>
        <a href="/gestion-de-profesionales/st-odo-09-perfil-profesional" class="quick-action-card">
            <span class="material-symbols-outlined">person</span>
            <strong>Mi Perfil</strong>
            <span>Datos personales</span>
        </a>
    </div>
</section>
```

**CSS:**
```css
.section-subtitle {
    font-size: 1.1rem;
    font-weight: 600;
    color: var(--text-primary);
    margin: 1.5rem 0 1rem;
}

.quick-actions-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 1rem;
    margin-bottom: 2rem;
}

.quick-action-card {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    padding: 1.5rem 1rem;
    background: white;
    border: 2px solid #e5e7eb;
    border-radius: 12px;
    text-decoration: none;
    color: var(--text-primary);
    transition: all 0.2s ease;
    text-align: center;
}

.quick-action-card:hover {
    border-color: var(--primary);
    box-shadow: 0 4px 12px rgba(59, 130, 246, 0.1);
    transform: translateY(-2px);
}

.quick-action-card .material-symbols-outlined {
    font-size: 2rem;
    color: var(--primary);
}

.quick-action-card strong {
    font-size: 0.95rem;
    font-weight: 600;
}

.quick-action-card span:last-child {
    font-size: 0.8rem;
    color: var(--text-muted);
}
```

---

### 🎨 **PRIORIDAD MEDIA - Mejoras de UX**

#### 5. **Hacer las Citas Próximas Clickeables**
**Actualizar la tabla de "Próximas citas de hoy":**

```diff
  <div class="invoice-item" role="listitem">
+   <a href="#" class="invoice-link" data-cita-id="@cita.IdCita">
      <div>
        <p class="invoice-code">@(cita.HoraInicio?.ToString(@"hh\:mm") ?? "00:00")</p>
        <p class="invoice-desc">Paciente: @(cita.Paciente?.Nombres ?? "N/A")</p>
      </div>
      <p class="invoice-amount">@(cita.Servicio?.Nombre ?? "Consulta")</p>
+   </a>
  </div>
```

**JavaScript:**
```javascript
// Modal rápido de detalle de cita
document.querySelectorAll('.invoice-link').forEach(link => {
    link.addEventListener('click', (e) => {
        e.preventDefault();
        const citaId = e.currentTarget.dataset.citaId;
        mostrarModalDetalleCita(citaId);
    });
});
```

---

#### 6. **Agregar Indicador de Rendimiento Diario**
**Nuevo widget en la columna derecha:**

```html
<!-- Rendimiento del Día -->
<section class="card" aria-labelledby="performanceTitle">
    <h2 class="card-title" id="performanceTitle">
        <span class="material-symbols-outlined">trending_up</span>
        Rendimiento del Día
    </h2>
    @{
        var tasaAsistencia = citasHoyCount > 0 
            ? (int)Math.Round((double)atendidasHoyCount / citasHoyCount * 100) 
            : 0;
        var emoji = tasaAsistencia >= 80 ? "🔥" : tasaAsistencia >= 50 ? "👍" : "⚠️";
    }
    <div class="performance-score">
        <div class="score-circle" data-score="@tasaAsistencia">
            <svg width="120" height="120">
                <circle cx="60" cy="60" r="50" fill="none" stroke="#e5e7eb" stroke-width="10"/>
                <circle cx="60" cy="60" r="50" fill="none" stroke="url(#gradient)" 
                        stroke-width="10" stroke-linecap="round"
                        stroke-dasharray="@(314 * tasaAsistencia / 100) 314"
                        transform="rotate(-90 60 60)"/>
                <defs>
                    <linearGradient id="gradient" x1="0%" y1="0%" x2="100%" y2="100%">
                        <stop offset="0%" style="stop-color:#667eea;stop-opacity:1" />
                        <stop offset="100%" style="stop-color:#764ba2;stop-opacity:1" />
                    </linearGradient>
                </defs>
            </svg>
            <div class="score-text">
                <span class="score-emoji">@emoji</span>
                <strong class="score-value">@tasaAsistencia%</strong>
                <span class="score-label">Atendidas</span>
            </div>
        </div>
        <p class="performance-message">
            @if (tasaAsistencia >= 80)
            {
                <strong style="color: #10b981;">¡Excelente día!</strong> Estás cumpliendo tus objetivos.
            }
            else if (tasaAsistencia >= 50)
            {
                <strong style="color: #f59e0b;">Buen ritmo</strong>, continúa así.
            }
            else
            {
                <strong style="color: #ef4444;">Varias citas pendientes</strong>, organiza tu tiempo.
            }
        </p>
    </div>
</section>
```

---

#### 7. **Notificaciones Inteligentes**
**Agregar panel de notificaciones contextuales:**

```html
<!-- Panel de Notificaciones (columna derecha, después de estado) -->
<section class="card notifications-card" aria-labelledby="notifTitle">
    <h2 class="card-title" id="notifTitle">
        <span class="material-symbols-outlined">notifications</span>
        Notificaciones
    </h2>
    <div class="notifications-list">
        @{
            var citasPendientesConfirmar = ViewData["CitasPendientesConfirmar"] as int? ?? 0;
            var pacientesEspera = ViewData["PacientesEnEspera"] as int? ?? 0;
            var historiasPendientes = ViewData["HistoriasPendientes"] as int? ?? 0;
        }
        
        @if (citasPendientesConfirmar > 0)
        {
            <div class="notif-item warning">
                <span class="material-symbols-outlined">schedule</span>
                <div>
                    <strong>@citasPendientesConfirmar citas sin confirmar</strong>
                    <span>Revisa tu agenda para confirmar</span>
                </div>
            </div>
        }
        
        @if (pacientesEspera > 0)
        {
            <div class="notif-item urgent">
                <span class="material-symbols-outlined">groups</span>
                <div>
                    <strong>@pacientesEspera pacientes en espera</strong>
                    <span>Esperando en sala de atención</span>
                </div>
            </div>
        }
        
        @if (historiasPendientes > 0)
        {
            <div class="notif-item info">
                <span class="material-symbols-outlined">assignment</span>
                <div>
                    <strong>@historiasPendientes historias por completar</strong>
                    <span>Cierra las historias clínicas pendientes</span>
                </div>
            </div>
        }
        
        @if (citasPendientesConfirmar == 0 && pacientesEspera == 0 && historiasPendientes == 0)
        {
            <div class="notif-empty">
                <span class="material-symbols-outlined">check_circle</span>
                <span>Todo al día</span>
            </div>
        }
    </div>
</section>
```

---

### 🔧 **PRIORIDAD BAJA - Optimizaciones**

#### 8. **Actualización Automática de Datos**
```javascript
// Auto-refresh cada 5 minutos
setInterval(async () => {
    try {
        const response = await fetch('/api/profesionales/dashboard-stats');
        const data = await response.json();
        actualizarKPIs(data);
    } catch (error) {
        console.error('Error actualizando stats:', error);
    }
}, 300000); // 5 minutos
```

#### 9. **Modo Compacto para Pantallas Pequeñas**
```css
@media (max-width: 768px) {
    .content-grid {
        grid-template-columns: 1fr;
    }
    
    .quick-actions-grid {
        grid-template-columns: repeat(2, 1fr);
    }
}
```

---

## 📅 ST-ODO-02: MI AGENDA (CALENDARIO SEMANAL)

### 🎯 **PRIORIDAD ALTA - Mejoras Críticas**

#### 1. **Agregar Vista de "Día Actual" (Alternativa al Semanal)**
**Problema:** En pantallas pequeñas, la vista semanal es difícil de navegar.

**Solución: Toggle de Vista:**
```html
<!-- Agregar en agenda-controls, antes de week-nav -->
<div class="view-toggle" role="radiogroup" aria-label="Tipo de vista">
    <button class="view-btn active" data-view="week" aria-pressed="true">
        <span class="material-symbols-outlined">view_week</span>
        Semana
    </button>
    <button class="view-btn" data-view="day" aria-pressed="false">
        <span class="material-symbols-outlined">view_day</span>
        Día
    </button>
    <button class="view-btn" data-view="list" aria-pressed="false">
        <span class="material-symbols-outlined">list</span>
        Lista
    </button>
</div>
```

**JavaScript para cambiar vistas:**
```javascript
const viewButtons = document.querySelectorAll('.view-btn');
const calendarBody = document.querySelector('.calendar-body');

viewButtons.forEach(btn => {
    btn.addEventListener('click', () => {
        const view = btn.dataset.view;
        
        // Update active state
        viewButtons.forEach(b => {
            b.classList.remove('active');
            b.setAttribute('aria-pressed', 'false');
        });
        btn.classList.add('active');
        btn.setAttribute('aria-pressed', 'true');
        
        // Apply view
        calendarBody.dataset.view = view;
        
        if (view === 'day') {
            mostrarVistaDia();
        } else if (view === 'list') {
            mostrarVistaLista();
        } else {
            mostrarVistaSemana();
        }
    });
});
```

**CSS:**
```css
.view-toggle {
    display: flex;
    gap: 0.5rem;
    background: white;
    padding: 0.25rem;
    border-radius: 8px;
    border: 1px solid #e5e7eb;
}

.view-btn {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.5rem 1rem;
    border: none;
    background: transparent;
    border-radius: 6px;
    cursor: pointer;
    transition: all 0.2s;
    color: var(--text-muted);
}

.view-btn.active {
    background: var(--primary);
    color: white;
}

.view-btn:hover:not(.active) {
    background: #f3f4f6;
}

/* Vista de día - mostrar solo columna actual */
.calendar-body[data-view="day"] .calendar-day:not(.current) {
    display: none;
}

.calendar-body[data-view="day"] .calendar-day.current {
    min-width: 100%;
}

/* Vista de lista */
.calendar-body[data-view="list"] {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
}

.calendar-body[data-view="list"] .calendar-day {
    display: flex;
    flex-direction: row;
    align-items: flex-start;
    border-bottom: 1px solid #e5e7eb;
    padding: 1rem 0;
}

.calendar-body[data-view="list"] .appointment {
    margin: 0.25rem 0;
}
```

---

#### 2. **Agregar Filtro por Estado de Cita**
**Agregar en controls-right:**

```html
<div class="filter-group">
    <label for="filterStatus" class="filter-label">Estado</label>
    <select id="filterStatus" class="status-select" aria-label="Filtrar por estado">
        <option value="">Todos los estados</option>
        <option value="programada">Programadas</option>
        <option value="confirmada">Confirmadas</option>
        <option value="atendida">Atendidas</option>
        <option value="cancelada">Canceladas</option>
    </select>
</div>
```

**JavaScript:**
```javascript
document.getElementById('filterStatus').addEventListener('change', (e) => {
    const estado = e.target.value.toLowerCase();
    const appointments = document.querySelectorAll('.appointment');
    
    appointments.forEach(appt => {
        if (!estado || appt.dataset.status.toLowerCase().includes(estado)) {
            appt.style.display = '';
        } else {
            appt.style.display = 'none';
        }
    });
});
```

---

#### 3. **Mejorar Modal de Detalle de Cita**
**Agregar más información y acciones:**

```javascript
function mostrarDetalleCompletoCita(citaData) {
    const content = `
        <div class="appt-detail-grid">
            <div class="appt-detail-section">
                <h3><span class="material-symbols-outlined">person</span> Paciente</h3>
                <p><strong>${citaData.pacienteNombre}</strong></p>
                <p>Documento: ${citaData.pacienteDoc || 'N/A'}</p>
                <p>Teléfono: ${citaData.pacienteTelefono || 'N/A'}</p>
                <a href="/historia-clinica/paciente/${citaData.pacienteId}" class="btn-link">
                    Ver Historia Clínica →
                </a>
            </div>
            
            <div class="appt-detail-section">
                <h3><span class="material-symbols-outlined">schedule</span> Horario</h3>
                <p><strong>${citaData.fecha}</strong></p>
                <p>${citaData.horaInicio} - ${citaData.horaFin}</p>
                <p>Duración: ${citaData.duracion} minutos</p>
            </div>
            
            <div class="appt-detail-section">
                <h3><span class="material-symbols-outlined">medical_services</span> Servicio</h3>
                <p><strong>${citaData.servicio}</strong></p>
                <p>Consultorio: ${citaData.consultorio}</p>
            </div>
            
            <div class="appt-detail-section full-width">
                <h3><span class="material-symbols-outlined">notes</span> Observaciones</h3>
                <p>${citaData.notas || 'Sin observaciones'}</p>
            </div>
            
            <div class="appt-detail-section full-width">
                <h3><span class="material-symbols-outlined">info</span> Estado</h3>
                <span class="badge-status ${citaData.estadoClase}">${citaData.estado}</span>
            </div>
            
            ${citaData.ultimaAtencion ? `
            <div class="appt-detail-section full-width alert-info">
                <span class="material-symbols-outlined">history</span>
                <div>
                    <strong>Última atención:</strong> ${citaData.ultimaAtencion}
                    <br><small>Procedimiento: ${citaData.ultimoProcedimiento}</small>
                </div>
            </div>
            ` : ''}
        </div>
    `;
    
    document.getElementById('modalApptContent').innerHTML = content;
    document.getElementById('modalAppointment').classList.add('active');
    document.getElementById('modalAppointment').setAttribute('aria-hidden', 'false');
    document.getElementById('modalAppointment').removeAttribute('inert');
}
```

---

#### 4. **Indicador Visual de Tiempo en Citas**
**Agregar indicador de tiempo transcurrido/restante:**

```javascript
// Actualizar cada minuto
function actualizarIndicadoresDetiempo() {
    const ahora = new Date();
    const appointments = document.querySelectorAll('.appointment[data-start-time]');
    
    appointments.forEach(appt => {
        const fecha = appt.dataset.date;
        const hora = appt.dataset.startTime;
        const horaFin = appt.dataset.endTime;
        
        const inicio = new Date(`${fecha}T${hora}`);
        const fin = new Date(`${fecha}T${horaFin}`);
        
        // Quitar indicadores previos
        appt.classList.remove('in-progress', 'upcoming', 'overdue');
        
        if (ahora >= inicio && ahora <= fin) {
            appt.classList.add('in-progress');
            appt.setAttribute('data-status-time', 'EN CURSO');
        } else if (ahora < inicio) {
            const minutos = Math.floor((inicio - ahora) / 60000);
            if (minutos <= 15) {
                appt.classList.add('upcoming');
                appt.setAttribute('data-status-time', `En ${minutos} min`);
            }
        } else if (ahora > fin && appt.dataset.status !== 'atendida') {
            appt.classList.add('overdue');
            appt.setAttribute('data-status-time', 'RETRASADA');
        }
    });
}

// Ejecutar cada minuto
setInterval(actualizarIndicadoresDeTiempo, 60000);
document.addEventListener('DOMContentLoaded', actualizarIndicadoresDetiempo);
```

**CSS:**
```css
.appointment::before {
    content: attr(data-status-time);
    position: absolute;
    top: -8px;
    right: 8px;
    font-size: 0.7rem;
    font-weight: 700;
    padding: 2px 8px;
    border-radius: 4px;
    display: none;
}

.appointment.in-progress {
    border-left: 4px solid #10b981;
    animation: pulse-green 2s infinite;
}

.appointment.in-progress::before {
    display: block;
    background: #10b981;
    color: white;
}

.appointment.upcoming {
    border-left: 4px solid #f59e0b;
}

.appointment.upcoming::before {
    display: block;
    background: #f59e0b;
    color: white;
}

.appointment.overdue {
    border-left: 4px solid #ef4444;
}

.appointment.overdue::before {
    display: block;
    background: #ef4444;
    color: white;
}

@keyframes pulse-green {
    0%, 100% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.4); }
    50% { box-shadow: 0 0 0 8px rgba(16, 185, 129, 0); }
}
```

---

### 🎨 **PRIORIDAD MEDIA - Mejoras de UX**

#### 5. **Drag & Drop para Reagendar Citas**
**Permitir arrastrar citas a otro día:**

```javascript
let draggedAppointment = null;

// Hacer citas arrastrables
document.querySelectorAll('.appointment').forEach(appt => {
    appt.setAttribute('draggable', 'true');
    
    appt.addEventListener('dragstart', (e) => {
        draggedAppointment = e.target;
        e.target.classList.add('dragging');
        e.dataTransfer.effectAllowed = 'move';
    });
    
    appt.addEventListener('dragend', (e) => {
        e.target.classList.remove('dragging');
    });
});

// Hacer días como drop targets
document.querySelectorAll('.calendar-day').forEach(day => {
    day.addEventListener('dragover', (e) => {
        if (day.classList.contains('closed')) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        day.classList.add('drag-over');
    });
    
    day.addEventListener('dragleave', (e) => {
        day.classList.remove('drag-over');
    });
    
    day.addEventListener('drop', async (e) => {
        e.preventDefault();
        day.classList.remove('drag-over');
        
        if (!draggedAppointment) return;
        
        const nuevaFecha = day.querySelector('[data-date]')?.dataset.date;
        const citaId = draggedAppointment.dataset.id;
        
        if (confirm(`¿Reagendar cita para el ${nuevaFecha}?`)) {
            await reagendarCita(citaId, nuevaFecha);
        }
        
        draggedAppointment = null;
    });
});

async function reagendarCita(citaId, nuevaFecha) {
    try {
        const response = await fetch(`/api/citas/${citaId}/reagendar`, {
            method: 'PATCH',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ nuevaFecha })
        });
        
        if (response.ok) {
            mostrarToast('Cita reagendada exitosamente', 'success');
            setTimeout(() => location.reload(), 1500);
        } else {
            mostrarToast('Error al reagendar', 'error');
        }
    } catch (error) {
        mostrarToast('Error de conexión', 'error');
    }
}
```

**CSS:**
```css
.appointment.dragging {
    opacity: 0.5;
    cursor: move;
}

.calendar-day.drag-over {
    background: #f0f9ff;
    border: 2px dashed var(--primary);
}
```

---

#### 6. **Búsqueda Rápida de Pacientes**
**Agregar barra de búsqueda en la agenda:**

```html
<!-- Agregar en agenda-controls -->
<div class="search-patient-wrap">
    <span class="material-symbols-outlined">search</span>
    <input type="search" 
           id="searchPatient" 
           placeholder="Buscar paciente en la agenda..."
           aria-label="Buscar paciente">
</div>
```

**JavaScript:**
```javascript
const searchInput = document.getElementById('searchPatient');
let searchTimeout;

searchInput.addEventListener('input', (e) => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
        const query = e.target.value.toLowerCase().trim();
        const appointments = document.querySelectorAll('.appointment');
        
        appointments.forEach(appt => {
            const paciente = appt.dataset.patientName?.toLowerCase() || '';
            const servicio = appt.dataset.serviceName?.toLowerCase() || '';
            
            if (!query || paciente.includes(query) || servicio.includes(query)) {
                appt.style.display = '';
                appt.classList.remove('search-hidden');
            } else {
                appt.style.display = 'none';
                appt.classList.add('search-hidden');
            }
        });
        
        // Highlight matches
        if (query) {
            appointments.forEach(appt => {
                if (!appt.classList.contains('search-hidden')) {
                    appt.classList.add('search-match');
                }
            });
        } else {
            appointments.forEach(appt => appt.classList.remove('search-match'));
        }
    }, 300);
});
```

---

#### 7. **Exportar Agenda a PDF/Excel**
**Agregar botones de exportación:**

```html
<!-- En header-right -->
<div class="header-actions-group">
    <button type="button" class="btn-secondary" id="btnExportPDF">
        <span class="material-symbols-outlined">picture_as_pdf</span>
        PDF
    </button>
    <button type="button" class="btn-secondary" id="btnExportExcel">
        <span class="material-symbols-outlined">table_chart</span>
        Excel
    </button>
    <button type="button" class="btn-secondary" onclick="window.print()">
        <span class="material-symbols-outlined">print</span>
        Imprimir
    </button>
</div>
```

**JavaScript:**
```javascript
document.getElementById('btnExportPDF').addEventListener('click', async () => {
    const weekStart = document.getElementById('weekLabel').dataset.weekStart;
    window.open(`/api/agenda/exportar-pdf?weekStart=${weekStart}`, '_blank');
});

document.getElementById('btnExportExcel').addEventListener('click', async () => {
    const weekStart = document.getElementById('weekLabel').dataset.weekStart;
    window.location.href = `/api/agenda/exportar-excel?weekStart=${weekStart}`;
});
```

---

#### 8. **Notificaciones de Recordatorio**
**Web Push Notifications para citas próximas:**

```javascript
// Solicitar permisos de notificación
async function solicitarPermisoNotificaciones() {
    if ('Notification' in window && Notification.permission === 'default') {
        const permission = await Notification.requestPermission();
        if (permission === 'granted') {
            localStorage.setItem('notificaciones_habilitadas', 'true');
        }
    }
}

// Programar notificaciones para citas del día
function programarNotificacionesCitas() {
    const citasHoy = obtenerCitasDelDia();
    
    citasHoy.forEach(cita => {
        const ahora = new Date();
        const horaCita = new Date(cita.fechaHora);
        const tiempoHasta = horaCita - ahora;
        
        // Notificar 15 minutos antes
        if (tiempoHasta > 0 && tiempoHasta <= 900000) { // 15 min en ms
            setTimeout(() => {
                new Notification('Cita Próxima', {
                    body: `${cita.paciente} - ${cita.servicio}\nEn 15 minutos`,
                    icon: '/images/logo.png',
                    tag: `cita-${cita.id}`,
                    requireInteraction: true
                });
            }, tiempoHasta - 900000);
        }
    });
}
```

---

### 🔧 **PRIORIDAD BAJA - Optimizaciones**

#### 9. **Carga Lazy de Semanas**
**Cargar semanas bajo demanda:**

```javascript
async function cargarSemanaDinamica(fecha) {
    try {
        showLoader();
        const response = await fetch(`/api/agenda/semana?fecha=${fecha}`);
        const data = await response.json();
        renderizarSemana(data);
    } catch (error) {
        mostrarToast('Error cargando agenda', 'error');
    } finally {
        hideLoader();
    }
}
```

#### 10. **Modo Offline**
**Cache de datos con Service Worker:**

```javascript
// Registrar service worker para funcionalidad offline
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').then(() => {
        console.log('Service Worker registrado');
    });
}
```

---

## 📋 Resumen de Prioridades

### **ST-ODO-01 Dashboard:**
| Mejora | Prioridad | Impacto | Esfuerzo |
|--------|-----------|---------|----------|
| Widget Próxima Cita Urgente | 🔴 Alta | Alto | Medio |
| Eliminar "Exportar Reporte" | 🔴 Alta | Medio | Bajo |
| Accesos Rápidos | 🔴 Alta | Alto | Bajo |
| Mejorar Barra Progreso | 🟡 Media | Medio | Bajo |
| Notificaciones Inteligentes | 🟡 Media | Alto | Medio |
| Rendimiento del Día | 🟡 Media | Medio | Medio |
| Auto-refresh | 🟢 Baja | Bajo | Bajo |

### **ST-ODO-02 Agenda:**
| Mejora | Prioridad | Impacto | Esfuerzo |
|--------|-----------|---------|----------|
| Toggle Vista (Semana/Día/Lista) | 🔴 Alta | Alto | Medio |
| Indicador de Tiempo Real | 🔴 Alta | Alto | Medio |
| Filtro por Estado | 🔴 Alta | Medio | Bajo |
| Búsqueda de Pacientes | 🟡 Media | Alto | Bajo |
| Drag & Drop | 🟡 Media | Alto | Alto |
| Exportar PDF/Excel | 🟡 Media | Medio | Medio |
| Notificaciones Push | 🟢 Baja | Medio | Alto |
| Modo Offline | 🟢 Baja | Bajo | Alto |

---

## 🚀 Plan de Implementación Sugerido

### **Sprint 1 (Semana 1-2):**
- ST-ODO-01: Widget Próxima Cita + Eliminar botón exportar
- ST-ODO-02: Filtro por Estado + Indicadores de tiempo

### **Sprint 2 (Semana 3-4):**
- ST-ODO-01: Accesos Rápidos + Mejorar progreso
- ST-ODO-02: Toggle de vistas + Búsqueda

### **Sprint 3 (Semana 5-6):**
- ST-ODO-01: Notificaciones + Rendimiento
- ST-ODO-02: Drag & Drop + Exportar

### **Sprint 4 (Backlog):**
- Optimizaciones de rendimiento
- Funcionalidades offline
- Notificaciones push

---

## 📊 Métricas de Éxito

### **KPIs a Medir:**
1. **Tiempo de carga inicial:** < 2 segundos
2. **Clicks para acceder a próxima cita:** 1 click desde dashboard
3. **Tasa de uso de vista móvil:** > 30%
4. **Satisfacción del usuario:** > 4.5/5
5. **Reducción de errores de agenda:** > 40%

---

**Fin del documento**
