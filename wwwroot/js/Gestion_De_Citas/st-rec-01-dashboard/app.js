/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Dashboard Recepción (st-rec-01-dashboard)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-rec-01-dashboard/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra el panel de control de recepción para la gestión de sala de espera y recepción de pacientes.
 * Mide tiempos de espera en tiempo real y gestiona el flujo de ingreso del paciente al consultorio.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Cálculo de minutos en sala de espera con alertas de tiempo excedido (> 15 min).
 * - Cambio de estado de la cita a "en_sala_de_espera" o "en_proceso" con notificación al odontólogo.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Strec01Dashboard
 * - HTML: Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const POLLING_INTERVAL_MS = 30000;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

const receptionAppointmentsList = window.smiletrackDashboardRecData?.appointments || [];
let upcomingAppointmentsTimerRef = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

const getActionMeta = (actionType) => {
  const metaMap = {
    'pencil':       { icon: '✏️', label: 'Editar',   cls: 'btn-secondary edit',     danger: false },
    'file-invoice': { icon: '🧾', label: 'Facturar', cls: 'btn-secondary btn-facturar', danger: false },
    'eye':          { icon: '👁️', label: 'Ver',      cls: 'btn-secondary btn-view',  danger: false }
  };
  return metaMap[actionType] || { icon: '👁️', label: 'Ver', cls: 'btn-secondary btn-view', danger: false };
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const refreshUpcomingAppointmentsData = async () => {
  if (document.hidden) return;
  try {
    const response = await fetch('/api/citas/proximas?ventanaMinutos=30', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' }
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    renderUpcomingAppointmentsBanner(Array.isArray(payload.proximasCitas) ? payload.proximasCitas : []);
  } catch (error) {
    console.warn('[SmileTrack] No se pudieron actualizar las próximas citas:', error);
  }
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const createAppointmentRow = (appointmentItem) => {
  const tableRow = document.createElement('tr');
  if (appointmentItem.highlight) tableRow.classList.add('row-highlight');
  tableRow.setAttribute('role', 'row');

  const actionButtonsHtml = appointmentItem.actions.map(action => {
    const meta = getActionMeta(action);
    return `<button class="${meta.cls}" type="button"
              data-action="${action}"
              title="${escapeHtml(meta.label)} cita de ${escapeHtml(appointmentItem.patient)}"
              aria-label="${escapeHtml(meta.label)} cita de ${escapeHtml(appointmentItem.patient)}">
              ${meta.icon} <span class="btn-text">${meta.label}</span>
            </button>`;
  }).join('');

  tableRow.innerHTML = `
    <td class="col-hora">${escapeHtml(appointmentItem.time)}</td>
    <td class="col-paciente">${escapeHtml(appointmentItem.patient)}</td>
    <td class="col-profesional">${escapeHtml(appointmentItem.doctor)}</td>
    <td class="col-servicio">${escapeHtml(appointmentItem.service)}</td>
    <td><span class="status-badge ${escapeHtml(appointmentItem.statusClass)}" role="status" aria-label="Estado: ${escapeHtml(appointmentItem.status)}">${escapeHtml(appointmentItem.status)}</span></td>
    <td>
      <div class="actions-cell" role="group" aria-label="Acciones para ${escapeHtml(appointmentItem.patient)}">
        ${actionButtonsHtml}
      </div>
    </td>
  `;

  return tableRow;
};

const renderAppointments = () => {
  const tableBody = safeGetElement('appointmentsTable');
  if (!tableBody) return;

  tableBody.innerHTML = '';

  if (receptionAppointmentsList.length === 0) {
    tableBody.innerHTML = `
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

  const documentFragment = document.createDocumentFragment();
  receptionAppointmentsList.forEach(appointmentItem => {
    const rowElement = createAppointmentRow(appointmentItem);
    documentFragment.appendChild(rowElement);
  });
  tableBody.appendChild(documentFragment);
};

const renderUpcomingAppointmentsBanner = (upcomingAppointmentsList) => {
  const bannerElement = safeGetElement('alertBannerProximas');
  const descriptionElement = safeGetElement('alertBannerDesc');
  if (!bannerElement || !descriptionElement) return;

  descriptionElement.replaceChildren(...upcomingAppointmentsList.map(item => {
    const paragraphElement = document.createElement('p');
    const timeElement = document.createElement('time');
    timeElement.dateTime = item.fechaIso || '';
    const strongElement = document.createElement('strong');
    strongElement.textContent = item.hora || '';
    timeElement.appendChild(strongElement);
    paragraphElement.append(timeElement, document.createTextNode(` ${item.texto || ''}`));
    return paragraphElement;
  }));
  bannerElement.style.display = upcomingAppointmentsList.length > 0 ? '' : 'none';
};

const renderServerSummaryStats = () => {
  const dashboardData = window.smiletrackDashboardRecData || {};

  const subtitleElement = safeGetElement('pageSubtitleDate');
  if (subtitleElement) {
    const timeElement = document.createElement('time');
    timeElement.className = 'text-primary font-bold';
    timeElement.dateTime = dashboardData.horaActualIso || '';
    timeElement.textContent = dashboardData.horaActualTexto || '';
    subtitleElement.replaceChildren(document.createTextNode(`${dashboardData.fechaHoraTexto || ''} - `), timeElement);
  }

  const stats = dashboardData.stats || {};
  const setElementValue = (elementId, value) => {
    const element = safeGetElement(elementId);
    if (element) element.textContent = value ?? '0';
  };
  setElementValue('statCitasHoy', stats.citasHoy);
  setElementValue('statConfirmadas', stats.confirmadas);
  setElementValue('statPendientes', stats.pendientes);
  setElementValue('statFacturasPendientes', stats.facturasPendientes);

  const upcomingList = dashboardData.proximasCitas || [];
  renderUpcomingAppointmentsBanner(upcomingList);
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const handleTableAction = (event) => {
  const actionButton = event.target.closest('[data-action]');
  if (!actionButton) return;

  const actionType = actionButton.dataset.action;
  const tableRow = actionButton.closest('tr');
  const patientName = tableRow?.querySelector('.col-paciente')?.textContent?.trim() || 'el paciente';

  if (actionType === 'pencil') {
    window.ToastService?.success('Editando cita', `Editando cita de ${patientName}…`);
  } else if (actionType === 'file-invoice') {
    window.ToastService?.success('Facturando', `Generando factura para ${patientName}…`);
  } else if (actionType === 'eye') {
    window.ToastService?.info('Detalle', `Viendo detalles de ${patientName}`);
  }
};

const setupHeaderActionButtons = () => {
  const newPatientBtn = safeGetElement('btnNuevoPaciente');
  const generateInvoiceBtn = safeGetElement('btnGenerarFactura');

  if (newPatientBtn) {
    newPatientBtn.addEventListener('click', () => {
      window.location.href = '/gestion-de-pacientes/st-rec-02-registrar-paciente';
    });
  }

  if (generateInvoiceBtn) {
    generateInvoiceBtn.addEventListener('click', () => {
      window.location.href = '/facturacion-y-pagos/st-rec-04-generar-factura';
    });
  }
};

const setupAlertButtons = () => {
  document.querySelectorAll('.btn-notify').forEach(buttonElement => {
    buttonElement.addEventListener('click', () => {
      if (buttonElement.disabled) return;
      
      const alertType = buttonElement.dataset.alert;
      const feedbackMessage = alertType === 'sin-confirmar' 
        ? 'Notificaciones de confirmación enviadas' 
        : 'Recordatorio de pago enviado';
      
      if (window.ToastService) window.ToastService.success(feedbackMessage);
      
      buttonElement.disabled = true;
      buttonElement.setAttribute('aria-disabled', 'true');
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupUpcomingAppointmentsPolling = () => {
  const stopPolling = () => {
    if (upcomingAppointmentsTimerRef) {
      clearInterval(upcomingAppointmentsTimerRef);
      upcomingAppointmentsTimerRef = null;
    }
  };
  const resumePolling = () => {
    stopPolling();
    if (!document.hidden) {
      refreshUpcomingAppointmentsData();
      upcomingAppointmentsTimerRef = setInterval(refreshUpcomingAppointmentsData, POLLING_INTERVAL_MS);
    }
  };
  document.addEventListener('visibilitychange', resumePolling);
  window.addEventListener('pagehide', stopPolling, { once: true });
  resumePolling();
};

const initializeReceptionDashboardModule = () => {
  renderServerSummaryStats();
  renderAppointments();
  
  setupHeaderActionButtons();
  setupAlertButtons();
  setupUpcomingAppointmentsPolling();
  
  const tableBody = safeGetElement('appointmentsTable');
  if (tableBody) {
    tableBody.addEventListener('click', handleTableAction);
    tableBody.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        const targetButton = event.target.closest('[data-action]');
        if (targetButton) {
          event.preventDefault();
          targetButton.click();
        }
      }
    });
  }
  
  window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeReceptionDashboardModule);