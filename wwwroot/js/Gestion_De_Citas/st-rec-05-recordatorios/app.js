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

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

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

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const sendRemindersToPatients = async (selectedIds, customMessageText, buttonOverride = null) => {
  const appointmentIds = [...new Set((selectedIds ?? []).filter(id => Number.isInteger(id) && id > 0))];
  if (appointmentIds.length === 0) {
    if (window.ToastService) window.ToastService.error('Por favor, selecciona al menos un paciente para enviar el recordatorio.');
    return;
  }

  const triggerButton = buttonOverride || safeGetElement('btnSendSelected') || safeGetElement('btnSendAllMobile');
  const originalButtonMarkup = triggerButton?.innerHTML;
  if (triggerButton) { triggerButton.disabled = true; triggerButton.textContent = 'Enviando…'; }

  try {
    const requestPayload = { IdsCitas: appointmentIds };
    if (customMessageText) requestPayload.MensajePersonalizado = customMessageText;

    const response = await window.SmileTrack.utils.fetchWithTimeout('/api/citas/recordatorios/enviar', {
      method: 'POST',
      credentials: 'same-origin',
      headers: buildApiAuthHeaders(),
      body: JSON.stringify(requestPayload),
      timeoutMs: 60000
    });

    const responseText = await response.text();
    let payload;
    try {
      payload = responseText ? JSON.parse(responseText) : null;
    } catch {
      throw new Error('El servidor devolvió una respuesta inválida al enviar los recordatorios.');
    }

    if (!response.ok || payload?.success !== true) {
      throw new Error(payload?.message || `No fue posible enviar los recordatorios (HTTP ${response.status}).`);
    }

    {
      const sentCount = payload.enviados ?? 0;
      const failedCount = payload.fallidos ?? 0;
      if (failedCount > 0) {
        const notifyPartialHandler = sentCount > 0
          ? (window.ToastService?.warning || window.ToastService?.error)
          : window.ToastService?.error;
        if (notifyPartialHandler) {
          notifyPartialHandler(
            `Recordatorios enviados: ${sentCount} éxito(s), ${failedCount} fallo(s). Verifica que los pacientes tengan correo registrado.`
          );
        }
      } else if (sentCount > 0) {
        if (window.ToastService) {
          window.ToastService.success(
            payload.message || `Se enviaron ${sentCount} recordatorio(s) exitosamente por correo electrónico.`
          );
        }
      } else if (window.ToastService) {
        window.ToastService.error(payload.message || 'No se pudo enviar ningún recordatorio.');
      }
      
      const listContainer = safeGetElement('patientListContainer');
      const sentIds = new Set(Array.isArray(payload.idsEnviados) ? payload.idsEnviados.map(Number) : []);
      listContainer?.querySelectorAll('.patient-row').forEach(rowElement => {
        if (!sentIds.has(Number(rowElement.dataset.id))) return;
        const checkboxElement = rowElement.querySelector('.custom-checkbox');
        if (checkboxElement) checkboxElement.checked = false;
        rowElement.classList.remove('selected');
      });

      if (sentIds.size > 0) {
        const sentCounter = safeGetElement('statsSentCount');
        if (sentCounter) {
          const currentCount = Number.parseInt(sentCounter.dataset.target ?? sentCounter.textContent ?? '0', 10) || 0;
          sentCounter.dataset.target = String(currentCount + sentIds.size);
          sentCounter.textContent = sentCounter.dataset.target;
        }

        const historyScroller = safeGetElement('historyScroller');
        const historyCount = document.querySelector('.history-count');
        if (historyScroller) {
          historyScroller.querySelector('[data-i18n="rec05.no_history"]')?.remove();
          for (const id of sentIds) {
            const row = listContainer?.querySelector(`.patient-row[data-id="${id}"]`);
            if (!row) continue;

            const card = document.createElement('article');
            card.className = 'history-card';
            card.setAttribute('role', 'listitem');
            const icon = document.createElement('span');
            icon.className = 'material-symbols-outlined avatar-circle avatar-green';
            icon.setAttribute('aria-hidden', 'true');
            icon.textContent = 'mark_email_read';

            const details = document.createElement('div');
            const name = document.createElement('p');
            name.className = 'patient-name';
            name.textContent = row.querySelector('.patient-name')?.textContent?.trim() || 'Paciente';
            const sentAt = document.createElement('p');
            sentAt.className = 'history-time';
            sentAt.textContent = `Enviado ${new Intl.DateTimeFormat('es-CO', { dateStyle: 'short', timeStyle: 'short' }).format(new Date())}`;
            const appointmentTime = row.querySelector('time')?.dateTime;
            const appointmentDate = document.createElement('p');
            appointmentDate.className = 'history-time';
            appointmentDate.textContent = appointmentTime
              ? `Cita ${new Intl.DateTimeFormat('es-CO').format(new Date(appointmentTime))}`
              : 'Cita programada';
            details.append(name, sentAt, appointmentDate);
            card.append(icon, details);
            historyScroller.prepend(card);
          }

          historyScroller.querySelectorAll('.history-card').forEach((card, index) => {
            if (index >= 5) card.remove();
          });
          if (historyCount) historyCount.textContent = `${historyScroller.querySelectorAll('.history-card').length} recientes`;
        }
      }
    }
  } catch (err) {
    console.error('[SmileTrack] Error al enviar recordatorios:', err);
    if (window.ToastService) window.ToastService.error(err instanceof Error ? err.message : 'Error de conexión al enviar recordatorios.');
  } finally {
    if (triggerButton) { triggerButton.disabled = false; triggerButton.innerHTML = originalButtonMarkup; }
  }
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
    sendRemindersToPatients(getSelectedPatientAppointmentIds(), null, sendSelectedBtn);
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
    
    sendRemindersToPatients(getSelectedPatientAppointmentIds(), null, sendAllMobileBtn);
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
        const listContainer = safeGetElement('patientListContainer');
        const unconfirmedIds = [...(listContainer?.querySelectorAll('.patient-row[data-es-manana="true"]:not(.dimmed)') ?? [])]
          .map(rowElement => Number(rowElement.dataset.id))
          .filter(id => Number.isInteger(id) && id > 0);
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
    overdueAlertBtn.addEventListener('click', async () => {
      overdueAlertBtn.disabled = true;
      overdueAlertBtn.textContent = 'Enviando…';
      try {
        const response = await window.SmileTrack.utils.fetchWithTimeout('/api/facturas/recordatorios-vencidas', {
          method: 'POST',
          credentials: 'same-origin',
          headers: buildApiAuthHeaders(),
          timeoutMs: 60000
        });

        let payload;
        try {
          payload = await response.json();
        } catch {
          throw new Error('El servidor devolvió una respuesta inválida al procesar los recordatorios de pago.');
        }

        if (!response.ok || payload?.success !== true) {
          throw new Error(payload?.message || `No fue posible enviar los recordatorios de pago (HTTP ${response.status}).`);
        }

        const enviados = Number(payload.enviados) || 0;
        const fallidos = Number(payload.fallidos) || 0;
        const mensaje = payload.message || 'Recordatorios de pago procesados.';
        if (fallidos > 0) {
          const notifyHandler = enviados > 0
            ? window.ToastService?.warning
            : window.ToastService?.error;
          notifyHandler?.(mensaje);
        } else if (enviados > 0) {
          window.ToastService?.success(mensaje);
        } else {
          window.ToastService?.warning(mensaje);
        }
      } catch (err) {
        console.warn('[SmileTrack] Error al enviar recordatorios de pago:', err);
        if (window.ToastService) window.ToastService.error('Error de conexión al enviar los recordatorios de pago.');
      } finally {
        overdueAlertBtn.disabled = false;
        overdueAlertBtn.textContent = 'Notificar';
      }
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

};

const initializeRemindersModule = () => {
  setupPatientCheckboxListeners();
  setupSendSelectedButtonListener();
  setupSendAllMobileButtonListener();
  setupAlertButtonsListeners();

  window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeRemindersModule);