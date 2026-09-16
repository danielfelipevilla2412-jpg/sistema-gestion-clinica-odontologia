/* ============================================
SmileTrack — Dashboard Recepción (st-rec-01-dashboard)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Controla la carga de pacientes en sala de espera, el cálculo de sus tiempos acumulados y la canalización de pacientes hacia consultorios.

FUNCIONALIDADES PRINCIPALES:
- Carga y actualización en tiempo real de pacientes en sala de espera
- Manejo de botones de acción para llamar pacientes o cambiar su estado de recepción

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y RecepcionDashboard
- CSS: ~/css/Gestion_De_Citas/st-rec-01-dashboard/styles.css
- JS: ~/js/Gestion_De_Citas/st-rec-01-dashboard/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
============================================ */

// WHY: safeGetElement evita excepciones fatales en tiempo de ejecución si un id no se encuentra en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) {
    console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  }
  return el;
};

// WHY: Debounce evita la sobrecarga del hilo principal ante eventos repetitivos como tecleos de búsqueda o redimensiones
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

const escapeHtml = (value) => String(value ?? '').replace(/[&<>'"]/g, (character) => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  "'": '&#39;',
  '"': '&quot;'
}[character]));

// WHY: Muestra retroalimentación temporal autolimpiable para no interrumpir el flujo visual de la recepción

// ═══ DATOS REALES DE CITAS (ver ConstruirDashboardRecepcionAsync en GestionCitasController.cs) ═══
const appointments = window.smiletrackDashboardRecData?.appointments || [];

// ═══ UTILIDADES DE RENDERIZADO ═══

/**
 * Mapeo de iconos para acciones de tabla.
 * Cada entrada incluye icono, texto visible (btn-text) y si es acción de riesgo.
 * WHY: el patrón canónico del sistema (st-adm-07) requiere emoji + <span class="btn-text">
 * junto al ícono para que la acción sea legible sin depender de tooltip.
 */
const getActionMeta = (action) => {
  const map = {
    'pencil':       { icon: '✏️', label: 'Editar',   cls: 'btn-icon action-btn edit',     danger: false },
    'file-invoice': { icon: '🧾', label: 'Facturar', cls: 'btn-icon action-btn btn-facturar', danger: false },
    'eye':          { icon: '👁️', label: 'Ver',      cls: 'btn-icon action-btn btn-view',  danger: false }
  };
  return map[action] || { icon: '👁️', label: 'Ver', cls: 'btn-icon action-btn btn-view', danger: false };
};

/**
 * Crea el elemento de fila para una cita individual.
 * BUG FIX patrón canónico: los botones de acción ahora siguen la misma estructura
 * que st-adm-07 — clase "btn-icon action-btn", emoji + <span class="btn-text">texto</span>,
 * data-action para event delegation, y aria-label descriptivo con nombre del paciente.
 * Antes solo tenían emoji sin texto visible, lo que rompía consistencia visual y
 * dejaba sin contexto a usuarios con modo alto contraste o sin soporte de emoji.
 */
const createAppointmentRow = (appt) => {
  const tr = document.createElement('tr');
  if (appt.highlight) tr.classList.add('row-highlight');
  tr.setAttribute('role', 'row');

  const actionButtons = appt.actions.map(action => {
    const meta = getActionMeta(action);
    return `<button class="${meta.cls}" type="button"
              data-action="${action}"
              title="${escapeHtml(meta.label)} cita de ${escapeHtml(appt.patient)}"
              aria-label="${escapeHtml(meta.label)} cita de ${escapeHtml(appt.patient)}">
              ${meta.icon} <span class="btn-text">${meta.label}</span>
            </button>`;
  }).join('');

  tr.innerHTML = `
    <td class="col-hora">${escapeHtml(appt.time)}</td>
    <td class="col-paciente">${escapeHtml(appt.patient)}</td>
    <td class="col-profesional">${escapeHtml(appt.doctor)}</td>
    <td class="col-servicio">${escapeHtml(appt.service)}</td>
    <td><span class="status-badge ${escapeHtml(appt.statusClass)}" role="status" aria-label="Estado: ${escapeHtml(appt.status)}">${escapeHtml(appt.status)}</span></td>
    <td>
      <div class="actions-cell" role="group" aria-label="Acciones para ${escapeHtml(appt.patient)}">
        ${actionButtons}
      </div>
    </td>
  `;

  return tr;
};

/**
 * Renderiza la tabla de citas con los datos actuales
 * [MEJORA]: Separación de lógica de creación de elementos para mejor mantenibilidad
 */
const renderAppointments = () => {
  const tbody = safeGetElement('appointmentsTable');
  if (!tbody) return;

  tbody.innerHTML = '';

  if (appointments.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="6" style="text-align:center;padding:20px;">
          <div class="empty-state" role="status">
            <span class="empty-icon" aria-hidden="true">📅</span>
            <p>No hay citas programadas.</p>
          </div>
        </td>
      </tr>`;
    return;
  }

  // [MEJORA]: Usar DocumentFragment para mejor performance en inserciones múltiples
  const fragment = document.createDocumentFragment();
  appointments.forEach(appt => {
    const row = createAppointmentRow(appt);
    fragment.appendChild(row);
  });
  tbody.appendChild(fragment);
};

// ═══ MANEJADORES DE EVENTOS ═══

/**
 * Event delegation para acciones en la tabla de citas.
 * BUG FIX: el selector antes era '.action-icon' — clase que ya no se usa tras el cambio
 * al patrón canónico. Ahora busca '[data-action]' directamente, que es más robusto
 * y no depende de ninguna clase CSS específica.
 */
const handleTableAction = (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;

  const action = btn.dataset.action;
  const row = btn.closest('tr');
  const patientName = row?.querySelector('.col-paciente')?.textContent?.trim() || 'el paciente';

  if (action === 'pencil') {
    window.ToastService?.success('Editando cita', `Editando cita de ${patientName}…`);
  } else if (action === 'file-invoice') {
    window.ToastService?.success('Facturando', `Generando factura para ${patientName}…`);
  } else if (action === 'eye') {
    window.ToastService?.info('Detalle', `Viendo detalles de ${patientName}`);
  }
};

/**
 * Maneja el toggle del menú móvil con gestión de accesibilidad
 * Consistente con módulos anteriores
 */
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

/**
 * Inicializa botones de acción principal del header.
 * BUG FIX: los handlers existían pero la navegación estaba comentada como placeholder.
 * Ahora navegan a las rutas reales del módulo:
 *   - "Nuevo paciente"    → /gestion-de-pacientes/st-rec-02-registrar-paciente
 *   - "Generar factura"   → /facturacion-y-pagos/st-rec-04-generar-factura
 * Estas rutas coinciden con las entradas del _SidebarRecepcionista, garantizando
 * consistencia con la navegación lateral.
 */
const initActionButtons = () => {
  const btnNuevo   = safeGetElement('btnNuevoPaciente');
  const btnFactura = safeGetElement('btnGenerarFactura');

  if (btnNuevo) {
    btnNuevo.addEventListener('click', () => {
      window.location.href = '/gestion-de-pacientes/st-rec-02-registrar-paciente';
    });
  }

  if (btnFactura) {
    btnFactura.addEventListener('click', () => {
      window.location.href = '/facturacion-y-pagos/st-rec-04-generar-factura';
    });
  }
};

/**
 * Inicializa botones de notificación de alertas
 * [MEJORA]: Validación de estado para evitar múltiples clicks
 */
const initAlertButtons = () => {
  document.querySelectorAll('.btn-notify').forEach(btn => {
    btn.addEventListener('click', () => {
      // [MEJORA]: Prevenir ejecución si ya está deshabilitado
      if (btn.disabled) return;
      
      const alertType = btn.dataset.alert;
      const msg = alertType === 'sin-confirmar' 
        ? 'Notificaciones de confirmación enviadas' 
        : 'Recordatorio de pago enviado';
      
      window.ToastService.success(msg);
      
      // [MEJORA]: Deshabilitar botón con feedback visual
      btn.disabled = true;
      btn.setAttribute('aria-disabled', 'true');
    });
  });
};

// ═══ INIT PRINCIPAL ═══

/**
 * Función principal de inicialización del dashboard
 */
// Renderiza los datos reales inyectados por el servidor: fecha/hora del encabezado,
// estadísticas del día y banner de próximas citas (ver ConstruirDashboardRecepcionAsync
// en GestionCitasController.cs).
const renderProximasCitas = (proximas) => {
  const banner = safeGetElement('alertBannerProximas');
  const desc = safeGetElement('alertBannerDesc');
  if (!banner || !desc) return;

  desc.replaceChildren(...proximas.map(p => {
    const paragraph = document.createElement('p');
    const time = document.createElement('time');
    time.dateTime = p.fechaIso || '';
    const strong = document.createElement('strong');
    strong.textContent = p.hora || '';
    time.appendChild(strong);
    paragraph.append(time, document.createTextNode(` ${p.texto || ''}`));
    return paragraph;
  }));
  banner.style.display = proximas.length > 0 ? '' : 'none';
};

const renderResumenServidor = () => {
  const d = window.smiletrackDashboardRecData || {};

  const subtitle = safeGetElement('pageSubtitleDate');
  if (subtitle) {
    const time = document.createElement('time');
    time.className = 'text-primary font-bold';
    time.dateTime = d.horaActualIso || '';
    time.textContent = d.horaActualTexto || '';
    subtitle.replaceChildren(document.createTextNode(`${d.fechaHoraTexto || ''} - `), time);
  }

  const stats = d.stats || {};
  const set = (id, val) => { const el = safeGetElement(id); if (el) el.textContent = val ?? '0'; };
  set('statCitasHoy', stats.citasHoy);
  set('statConfirmadas', stats.confirmadas);
  set('statPendientes', stats.pendientes);
  set('statFacturasPendientes', stats.facturasPendientes);

  const proximas = d.proximasCitas || [];
  renderProximasCitas(proximas);
};

let proximasCitasTimer = null;

const refreshProximasCitas = async () => {
  if (document.hidden) return;
  try {
    const response = await fetch('/api/citas/proximas?ventanaMinutos=30', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' }
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    renderProximasCitas(Array.isArray(payload.proximasCitas) ? payload.proximasCitas : []);
  } catch (error) {
    console.warn('[SmileTrack] No se pudieron actualizar las próximas citas:', error);
  }
};

const initProximasPolling = () => {
  const stop = () => {
    if (proximasCitasTimer) {
      clearInterval(proximasCitasTimer);
      proximasCitasTimer = null;
    }
  };
  const resume = () => {
    stop();
    if (!document.hidden) {
      refreshProximasCitas();
      proximasCitasTimer = setInterval(refreshProximasCitas, 30000);
    }
  };
  document.addEventListener('visibilitychange', resume);
  window.addEventListener('pagehide', stop, { once: true });
  resume();
};

const init = () => {
  // Inicializar componentes de UI
  initMobileMenu();
  
  // Renderizado inicial de datos
  renderResumenServidor();
  renderAppointments();
  
  // Inicializar interacciones
  initActionButtons();
  initAlertButtons();
  initProximasPolling();
  
  // [MEJORA]: Event delegation para tabla de citas (performance)
  const tableBody = safeGetElement('appointmentsTable');
  if (tableBody) {
    tableBody.addEventListener('click', handleTableAction);
    // [MEJORA]: Soporte para activación con teclado (Enter/Space)
    tableBody.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        const target = e.target.closest('.action-icon');
        if (target) {
          e.preventDefault();
          target.click();
        }
      }
    });
  }
  
  // [MEJORA]: Limpieza de listeners al unload (buena práctica para SPAs)
  window.addEventListener('beforeunload', () => {
    // En una SPA real, aquí se removerían listeners para evitar memory leaks
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);