/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Recordatorios Recepción (st-rec-05-recordatorios)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra el envío masivo o individual de recordatorios de citas vía correo electrónico/SMS/WhatsApp.
 * Permite a la recepción confirmar asistencias y re-notificar a los pacientes con citas agendadas para el día siguiente.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Envío asíncrono con retroalimentación visual inmediata.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Strec05Recordatorios
 * - HTML: Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const DEFAULT_PAGE_SIZE = 100;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

let selectedAppointmentIdsList = [];

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const buildApiAuthHeaders = () => {
  const headersMap = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  const requestToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (requestToken) headersMap['X-CSRF-TOKEN'] = requestToken;
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headersMap['Authorization'] = `Bearer ${jwt}`;
  } catch { /* modo privado */ }
  return headersMap;
};

const getTomorrowIsoDateString = () => {
  const dateObj = new Date();
  dateObj.setDate(dateObj.getDate() + 1);
  return dateObj.toISOString().split('T')[0];
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const sendRemindersToPatients = async (selectedIds, customMessageText) => {
  if (!selectedIds || selectedIds.length === 0) {
    if (window.ToastService) window.ToastService.error('Por favor, selecciona al menos un paciente para enviar el recordatorio.');
    return;
  }

  const triggerButton = safeGetElement('btnSendSelected') || safeGetElement('btnSendAllMobile');
  const originalButtonText = triggerButton?.textContent;
  if (triggerButton) { triggerButton.disabled = true; triggerButton.textContent = 'Enviando…'; }

  try {
    const requestPayload = { IdsCitas: selectedIds };
    if (customMessageText) requestPayload.MensajePersonalizado = customMessageText;

    const response = await fetch('/api/citas/recordatorios/enviar', {
      method: 'POST',
      credentials: 'same-origin',
      headers: buildApiAuthHeaders(),
      body: JSON.stringify(requestPayload)
    });

    let payload;
    try { payload = await response.json(); } catch { payload = { success: response.ok }; }

    if (response.ok && payload.success !== false) {
      const sentCount = payload.enviados ?? selectedIds.length;
      const failedCount = payload.fallidos ?? 0;
      if (failedCount > 0) {
        const notifyPartialHandler = sentCount > 0
          ? window.ToastService?.warning
          : window.ToastService?.error;
        if (notifyPartialHandler) {
          notifyPartialHandler(
            `Recordatorios enviados: ${sentCount} éxito(s), ${failedCount} fallo(s). Verifica que los pacientes tengan correo registrado.`
          );
        }
      } else {
        if (window.ToastService) {
          window.ToastService.success(
            payload.message || `Se enviaron ${sentCount} recordatorio(s) exitosamente por correo electrónico.`
          );
        }
      }
      
      const listContainer = safeGetElement('patientListContainer');
      listContainer?.querySelectorAll('.patient-row.selected .custom-checkbox').forEach(checkboxElement => {
        checkboxElement.checked = false;
        checkboxElement.closest('.patient-row')?.classList.remove('selected');
      });
    } else {
      if (window.ToastService) {
        window.ToastService.error(
          payload.message || 'No fue posible enviar los recordatorios. Verifica la configuración del servidor de correo.'
        );
      }
    }
  } catch (err) {
    console.error('[SmileTrack] Error al enviar recordatorios:', err);
    if (window.ToastService) window.ToastService.error('Error de conexión al enviar recordatorios. Verifica tu conexión a internet.');
  } finally {
    if (triggerButton) { triggerButton.disabled = false; triggerButton.textContent = originalButtonText; }
  }
};

const fetchUnconfirmedAppointmentsForTomorrow = async () => {
  const tomorrowIsoDate = getTomorrowIsoDateString();
  const response = await fetch(`/api/citas?estado=programada&fecha=${tomorrowIsoDate}&pageSize=${DEFAULT_PAGE_SIZE}`, {
    headers: { 'Accept': 'application/json' }
  });
  if (!response.ok) throw new Error(`status ${response.status}`);
  const payload = await response.json();
  return (payload.data ?? []).map(appointment => appointment.IdCita ?? appointment.idCita).filter(Boolean);
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const getSelectedPatientAppointmentIds = () => {
  const listContainer = safeGetElement('patientListContainer');
  if (!listContainer) return [];
  const selectedIds = [];
  listContainer.querySelectorAll('.patient-row').forEach(rowElement => {
    const checkboxElement = rowElement.querySelector('.custom-checkbox');
    if (!checkboxElement?.checked) return;
    
    const rawIdValue = checkboxElement.dataset.citaId
      ?? checkboxElement.dataset.id
      ?? rowElement.dataset.citaId
      ?? rowElement.dataset.id
      ?? rowElement.querySelector('input[name="citaId"]')?.value
      ?? rowElement.querySelector('input[name="id"]')?.value;
    const parsedId = parseInt(rawIdValue, 10);
    if (!isNaN(parsedId) && parsedId > 0) selectedIds.push(parsedId);
  });
  return selectedIds;
};

const updatePatientRowSelectionState = (checkboxElement) => {
  const rowElement = checkboxElement.closest('.patient-row');
  if (!rowElement) return;
  
  if (checkboxElement.checked) {
    rowElement.classList.add('selected');
  } else {
    rowElement.classList.remove('selected');
  }
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const setupSendSelectedButtonListener = () => {
  const sendSelectedBtn = safeGetElement('btnSendSelected');
  if (!sendSelectedBtn) return;

  sendSelectedBtn.addEventListener('click', () => {
    selectedAppointmentIdsList = getSelectedPatientAppointmentIds();
    sendRemindersToPatients(selectedAppointmentIdsList);
  });
};

const setupSendAllMobileButtonListener = () => {
  const sendAllMobileBtn = safeGetElement('btnSendAllMobile');
  if (!sendAllMobileBtn) return;

  sendAllMobileBtn.addEventListener('click', () => {
    const listContainer = safeGetElement('patientListContainer');
    if (!listContainer) return;
    
    listContainer.querySelectorAll('.patient-row[data-es-manana="true"]:not(.dimmed)').forEach(rowElement => {
      const checkboxElement = rowElement.querySelector('.custom-checkbox');
      if (checkboxElement && !checkboxElement.disabled) {
        checkboxElement.checked = true;
        rowElement.classList.add('selected');
      }
    });
    
    selectedAppointmentIdsList = getSelectedPatientAppointmentIds();
    sendRemindersToPatients(selectedAppointmentIdsList);
  });
};

const setupAlertButtonsListeners = () => {
  const unconfirmedAlertBtn = safeGetElement('btnAlertUnconfirmed');
  const overdueAlertBtn = safeGetElement('btnAlertOverdue');

  if (unconfirmedAlertBtn) {
    unconfirmedAlertBtn.addEventListener('click', async () => {
      unconfirmedAlertBtn.disabled = true;
      unconfirmedAlertBtn.textContent = 'Enviando…';
      try {
        const unconfirmedIds = await fetchUnconfirmedAppointmentsForTomorrow();
        if (unconfirmedIds.length === 0) {
          if (window.ToastService) window.ToastService.success('No hay citas sin confirmar para mañana.');
        } else {
          await sendRemindersToPatients(unconfirmedIds, 'Recordatorio: tienes una cita programada para mañana. Por favor confirma tu asistencia.');
        }
      } catch (err) {
        console.warn('[SmileTrack] Error al buscar citas no confirmadas:', err);
        if (window.ToastService) window.ToastService.error('No fue posible obtener las citas sin confirmar.');
      } finally {
        unconfirmedAlertBtn.disabled = false;
        unconfirmedAlertBtn.textContent = 'Enviar notificación';
      }
    });
  }

  if (overdueAlertBtn) {
    overdueAlertBtn.addEventListener('click', () => {
      if (window.ToastService) window.ToastService.warning('Los recordatorios de pago no están conectados a un contrato de facturación disponible.');
    });
  }
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupPatientCheckboxListeners = () => {
  const listContainer = safeGetElement('patientListContainer');
  if (!listContainer) return;

  listContainer.addEventListener('change', (event) => {
    if (event.target.classList.contains('custom-checkbox')) {
      updatePatientRowSelectionState(event.target);
    }
  });

  listContainer.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      const rowElement = event.target.closest('.patient-row');
      if (rowElement) {
        event.preventDefault();
        const checkboxElement = rowElement.querySelector('.custom-checkbox');
        if (checkboxElement) {
          checkboxElement.checked = !checkboxElement.checked;
          updatePatientRowSelectionState(checkboxElement);
        }
      }
    }
  });
};

const initializeRemindersModule = () => {
  setupPatientCheckboxListeners();
  setupSendSelectedButtonListener();
  setupSendAllMobileButtonListener();
  setupAlertButtonsListeners();

  window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeRemindersModule);