/* ============================================
SmileTrack — Recordatorios (st-rec-05-recordatorios)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Controla la interfaz de recordatorios de recepción: envío manual, confirmación visual de estados de entrega y configuración de envíos automáticos.

FUNCIONALIDADES PRINCIPALES:
- Acciones de envío para recordatorios por medio de fetch API
- Confirmación visual temporal (Toast) de los envíos realizados
- Toggle de activación de canales de envío automáticos

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Recordatorios
- CSS: ~/css/Gestion_De_Citas/st-rec-05-recordatorios/styles.css
- JS: ~/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
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

// WHY: Las notificaciones no bloqueantes brindan retroalimentación al recepcionista sin interrumpir la gestión de la tabla

// ═══════════════════════════════════════════════════════════════════
//  CSRF / AUTH HEADERS
// ═══════════════════════════════════════════════════════════════════

const getApiHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  const requestToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (requestToken) headers['X-CSRF-TOKEN'] = requestToken;
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch { /* modo privado */ }
  return headers;
};

// ═══════════════════════════════════════════════════════════════════
//  RECOPILACIÓN DE CITAS SELECCIONADAS
// ═══════════════════════════════════════════════════════════════════

/**
 * Recopila los IDs de citas de los checkboxes marcados en #patientListContainer.
 * Cada fila .patient-row debe tener un atributo data-cita-id en la fila o en el checkbox.
 * @returns {number[]} Array de IDs de citas seleccionadas.
 */
const getSelectedPatients = () => {
  const container = safeGetElement('patientListContainer');
  if (!container) return [];
  const ids = [];
  container.querySelectorAll('.patient-row').forEach(row => {
    const checkbox = row.querySelector('.custom-checkbox');
    if (!checkbox?.checked) return;
    // Buscar el ID en data-cita-id del checkbox, o de la fila, o del input[name="citaId"]
    const rawId = checkbox.dataset.citaId
      ?? checkbox.dataset.id
      ?? row.dataset.citaId
      ?? row.dataset.id
      ?? row.querySelector('input[name="citaId"]')?.value
      ?? row.querySelector('input[name="id"]')?.value;
    const id = parseInt(rawId, 10);
    if (!isNaN(id) && id > 0) ids.push(id);
  });
  return ids;
};

// ═══════════════════════════════════════════════════════════════════
//  ENVÍO REAL DE RECORDATORIOS POR CORREO
// ═══════════════════════════════════════════════════════════════════

/**
 * Envía recordatorios de cita por correo a los pacientes de las citas seleccionadas.
 * Conecta con el endpoint real POST /api/citas/recordatorios/enviar.
 * @param {number[]} selectedIds - Array de IDs de citas.
 * @param {string} [mensajePersonalizado] - Mensaje adicional opcional.
 */
const sendReminders = async (selectedIds, mensajePersonalizado) => {
  if (!selectedIds || selectedIds.length === 0) {
    window.ToastService?.error?.('Por favor, selecciona al menos un paciente para enviar el recordatorio.');
    return;
  }

  // Deshabilitar botón para evitar doble envío
  const btn = safeGetElement('btnSendSelected') || safeGetElement('btnSendAllMobile');
  const originalText = btn?.textContent;
  if (btn) { btn.disabled = true; btn.textContent = 'Enviando…'; }

  try {
    const body = { IdsCitas: selectedIds };
    if (mensajePersonalizado) body.MensajePersonalizado = mensajePersonalizado;

    const res = await fetch('/api/citas/recordatorios/enviar', {
      method: 'POST',
      credentials: 'same-origin',
      headers: getApiHeaders(),
      body: JSON.stringify(body)
    });

    let payload;
    try { payload = await res.json(); } catch { payload = { success: res.ok }; }

    if (res.ok && payload.success !== false) {
      const enviados = payload.enviados ?? selectedIds.length;
      const fallidos = payload.fallidos ?? 0;
      if (fallidos > 0) {
        const notifyPartial = enviados > 0
          ? window.ToastService?.warning
          : window.ToastService?.error;
        notifyPartial?.(
          `Recordatorios enviados: ${enviados} éxito(s), ${fallidos} fallo(s). Verifica que los pacientes tengan correo registrado.`
        );
      } else {
        window.ToastService?.success?.(
          payload.message || `Se enviaron ${enviados} recordatorio(s) exitosamente por correo electrónico.`
        );
      }
      // Deseleccionar todos los checkboxes después del envío exitoso
      const container = safeGetElement('patientListContainer');
      container?.querySelectorAll('.patient-row.selected .custom-checkbox').forEach(cb => {
        cb.checked = false;
        cb.closest('.patient-row')?.classList.remove('selected');
      });
    } else {
      window.ToastService?.error?.(
        payload.message || 'No fue posible enviar los recordatorios. Verifica la configuración del servidor de correo.'
      );
    }
  } catch (err) {
    console.error('[SmileTrack] Error al enviar recordatorios:', err);
    window.ToastService?.error?.('Error de conexión al enviar recordatorios. Verifica tu conexión a internet.');
  } finally {
    if (btn) { btn.disabled = false; btn.textContent = originalText; }
  }
};

// Maneja cambio de estado en checkboxes de pacientes
const handleCheckboxChange = (checkbox) => {
  const row = checkbox.closest('.patient-row');
  if (!row) return;
  
  if (checkbox.checked) {
    row.classList.add('selected');
  } else {
    row.classList.remove('selected');
  }
};

// Inicializa eventos de checkboxes en lista de pacientes
const initPatientCheckboxes = () => {
  const container = safeGetElement('patientListContainer');
  if (!container) return;

  container.addEventListener('change', (e) => {
    if (e.target.classList.contains('custom-checkbox')) {
      handleCheckboxChange(e.target);
    }
  });

  // Soporte para teclado en filas de paciente
  container.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      const row = e.target.closest('.patient-row');
      if (row) {
        e.preventDefault();
        const checkbox = row.querySelector('.custom-checkbox');
        if (checkbox) {
          checkbox.checked = !checkbox.checked;
          handleCheckboxChange(checkbox);
        }
      }
    }
  });
};

// Inicializa botón de enviar seleccionados
const initSendSelected = () => {
  const btn = safeGetElement('btnSendSelected');
  if (!btn) return;

  btn.addEventListener('click', () => {
    const selected = getSelectedPatients();
    sendReminders(selected);
  });
};

// Inicializa botón móvil de enviar a todos
const initSendAllMobile = () => {
  const btn = safeGetElement('btnSendAllMobile');
  if (!btn) return;

  btn.addEventListener('click', () => {
    // Selecciona todos los pacientes programados para mañana
    const container = safeGetElement('patientListContainer');
    if (!container) return;
    
    container.querySelectorAll('.patient-row[data-es-manana="true"]:not(.dimmed)').forEach(row => {
      const checkbox = row.querySelector('.custom-checkbox');
      if (checkbox && !checkbox.disabled) {
        checkbox.checked = true;
        row.classList.add('selected');
      }
    });
    
    const selected = getSelectedPatients();
    sendReminders(selected);
  });
};

// Inicializa botones de alertas laterales
const initAlertButtons = () => {
  const btnUnconfirmed = safeGetElement('btnAlertUnconfirmed');
  const btnOverdue = safeGetElement('btnAlertOverdue');

  if (btnUnconfirmed) {
    btnUnconfirmed.addEventListener('click', async () => {
      btnUnconfirmed.disabled = true;
      btnUnconfirmed.textContent = 'Enviando…';
      try {
        // Obtener IDs de citas no confirmadas con cita para mañana
        const mañana = (() => {
          const d = new Date(); d.setDate(d.getDate() + 1);
          return d.toISOString().split('T')[0];
        })();
        const res = await fetch(`/api/citas?estado=programada&fecha=${mañana}&pageSize=100`, {
          headers: { 'Accept': 'application/json' }
        });
        if (!res.ok) throw new Error(`status ${res.status}`);
        const payload = await res.json();
        const ids = (payload.data ?? []).map(c => c.IdCita ?? c.idCita).filter(Boolean);
        if (ids.length === 0) {
          window.ToastService?.success?.('No hay citas sin confirmar para mañana.');
        } else {
          await sendReminders(ids, 'Recordatorio: tienes una cita programada para mañana. Por favor confirma tu asistencia.');
        }
      } catch (err) {
        console.warn('[SmileTrack] Error al buscar citas no confirmadas:', err);
        window.ToastService?.error?.('No fue posible obtener las citas sin confirmar.');
      } finally {
        btnUnconfirmed.disabled = false;
        btnUnconfirmed.textContent = 'Enviar notificación';
      }
    });
  }

  if (btnOverdue) {
    btnOverdue.addEventListener('click', () => {
      window.ToastService?.warning?.('Los recordatorios de pago no están conectados a un contrato de facturación disponible.');
    });
  }
};

// Inicializa menú móvil (delegado al módulo centralizado)
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// Función principal de inicialización
const init = () => {
  initMobileMenu();
  initPatientCheckboxes();
  initSendSelected();
  initSendAllMobile();
  initAlertButtons();

  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

document.addEventListener('DOMContentLoaded', init);