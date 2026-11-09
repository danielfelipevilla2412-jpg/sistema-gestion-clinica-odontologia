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

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

// Metadatos de los botones de acción de la tabla. Se usa el patrón canónico
// declarado en el propio styles.css de la vista (.btn-icon.action-btn, L262) con
// íconos Material Symbols y texto visible; se añaden marcadores semánticos
// (btn-view/btn-edit/btn-facturar) igual que st-rec-03/st-adm-07.
const getActionMeta = (actionType) => {
  const metaMap = {
    'pencil':       { icon: 'edit',         label: 'Editar',   cls: 'btn-icon action-btn btn-edit' },
    'file-invoice': { icon: 'receipt_long', label: 'Facturar', cls: 'btn-icon action-btn btn-facturar' },
    'eye':          { icon: 'visibility',   label: 'Ver',      cls: 'btn-icon action-btn btn-view' }
  };
  return metaMap[actionType] || { icon: 'visibility', label: 'Ver', cls: 'btn-icon action-btn btn-view' };
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
    const actionLabel = `${meta.label} cita de ${escapeHtml(appointmentItem.patient)}`;
    return `<button class="${meta.cls}" type="button"
              data-action="${action}"
              data-id="${escapeHtml(appointmentItem.id)}"
              title="${actionLabel}"
              aria-label="${actionLabel}">
              <span class="material-symbols-outlined" aria-hidden="true">${meta.icon}</span>
              <span class="btn-text">${meta.label}</span>
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

// El contador compartido ignora los elementos ya animados (guard data-animated);
// lo reiniciamos para permitir la animación desde 0 hasta el valor real servido.
const reanimateCounter = (element, value) => {
  if (!element || typeof window.animateCounter !== 'function') return;
  element.removeAttribute('data-animated');
  window.animateCounter(element, Number(value) || 0);
};

// Rellena los widgets de "Alertas pendientes" con los conteos reales del servidor
// y oculta los avisos sin incidencias (mismo criterio que st-rec-05-recordatorios).
const renderAlertWidgets = (dashboardData) => {
  const alertas = dashboardData.alertas || {};
  const sinConfirmar = Number(alertas.sinConfirmar) || 0;
  const facturasVencidas = Number(alertas.facturasVencidas) || 0;

  const sinConfirmarCard = safeGetElement('alertCardSinConfirmar');
  const sinConfirmarText = safeGetElement('alertSinConfirmarText');
  if (sinConfirmarCard && sinConfirmarText) {
    sinConfirmarText.textContent = `${sinConfirmar} paciente${sinConfirmar === 1 ? '' : 's'} sin confirmar`;
    sinConfirmarCard.style.display = sinConfirmar > 0 ? '' : 'none';
  }

  const facturasCard = safeGetElement('alertCardFacturasVencidas');
  const facturasText = safeGetElement('alertFacturasVencidasText');
  if (facturasCard && facturasText) {
    facturasText.textContent = `${facturasVencidas} factura${facturasVencidas === 1 ? '' : 's'} vencida${facturasVencidas === 1 ? '' : 's'}`;
    facturasCard.style.display = facturasVencidas > 0 ? '' : 'none';
  }

  const emptyState = safeGetElement('alertsEmptyState');
  if (emptyState) {
    emptyState.style.display = (sinConfirmar === 0 && facturasVencidas === 0) ? '' : 'none';
  }
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

  // Contadores numéricos: se animan con el patrón canónico (data-target + reanimación).
  const numericStats = {
    statCitasHoy: stats.citasHoy,
    statConfirmadas: stats.confirmadas,
    statPendientes: stats.pendientes
  };
  Object.entries(numericStats).forEach(([elementId, value]) => {
    const element = safeGetElement(elementId);
    if (!element) return;
    const numericValue = Number(value);
    if (Number.isFinite(numericValue)) {
      element.dataset.target = String(numericValue);
      reanimateCounter(element, numericValue);
    } else {
      element.textContent = '0';
    }
  });

  // "Facturas pendientes" llega ya formateado como moneda (p. ej. "$1.2k"): no es animable.
  const facturasPendientesElement = safeGetElement('statFacturasPendientes');
  if (facturasPendientesElement) {
    facturasPendientesElement.textContent = stats.facturasPendientes ?? '0';
  }

  const systemLastCheckElement = safeGetElement('systemLastCheck');
  if (systemLastCheckElement) {
    systemLastCheckElement.textContent = dashboardData.horaActualTexto || '';
  }

  renderAlertWidgets(dashboardData);

  const upcomingList = dashboardData.proximasCitas || [];
  renderUpcomingAppointmentsBanner(upcomingList);
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

// Navegación real de las acciones de la tabla:
//  - Editar / Ver → vista de gestión de citas (st-rec-03) con la cita preseleccionada.
//  - Facturar     → módulo de generación de factura (st-rec-04).
const handleTableAction = (event) => {
  const actionButton = event.target.closest('[data-action]');
  if (!actionButton) return;

  const appointmentId = actionButton.dataset.id;
  if (!appointmentId) return;

  if (actionButton.dataset.action === 'file-invoice') {
    window.location.href = '/facturacion-y-pagos/st-rec-04-generar-factura';
    return;
  }

  window.location.href = `/gestion-de-citas/st-rec-03-gestion-citas?editId=${encodeURIComponent(appointmentId)}`;
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

// Los botones "Notificar" se dirigen al módulo de recordatorios (st-rec-05),
// que concentra el envío real de notificaciones a pacientes.
const setupAlertButtons = () => {
  document.querySelectorAll('.btn-notify').forEach(buttonElement => {
    buttonElement.addEventListener('click', () => {
      window.location.href = '/gestion-de-citas/st-rec-05-recordatorios';
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
};

document.addEventListener('DOMContentLoaded', initializeReceptionDashboardModule);