/* ============================================
   SmileTrack — Mi Agenda Odontólogo (st-odo-02-agenda)
   ============================================
   Autor: Johan Santamaria / Antigravity
   Fecha: 29/07/2026 (Actualizado 2026-09-12)
   DESCRIPCIÓN:
   Maneja la interactividad del grid semanal de la agenda del odontólogo,
   la navegación por semanas, filtrado por consultorio, modales de citas
   y transiciones de estado de atención en tiempo real conectadas a SQL Server.
============================================ */

// ===================================================================
// 1. CONSTANTES Y CONFIGURACIÓN
// ===================================================================

let selectedAppointmentId = null;
let selectedPatientId = null;

// ===================================================================
// 2. UTILIDADES Y FORMATEADORES DE DATOS
// ===================================================================

/**
 * Escapa caracteres HTML para prevenir vulnerabilidades XSS.
 * @param {string} rawText
 * @returns {string}
 */
const escapeHtml = (rawText) => {
  if (!rawText) return '';
  return String(rawText)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
};

/**
 * Obtiene el token CSRF antiforgery desde la cookie o el DOM.
 * @returns {string|null}
 */
const getCsrfTokenValue = () => {
  const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
  if (tokenInput) return tokenInput.value;
  const cookieMatch = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  return cookieMatch ? decodeURIComponent(cookieMatch[2]) : null;
};

/**
 * Muestra notificaciones tipo Toast usando el servicio global o fallback local.
 * @param {string} messageText
 * @param {string} [notificationType='info']
 */
const showToastNotification = (messageText, notificationType = 'info') => {
  if (window.ToastService) {
    window.ToastService[notificationType]?.(messageText);
    return;
  }

  const existingToast = document.getElementById('toast');
  if (existingToast) {
    existingToast.textContent = messageText;
    existingToast.className = `toast show ${notificationType}`;
    setTimeout(() => { existingToast.classList.remove('show'); }, 3500);
    return;
  }

  const dynamicToast = document.createElement('div');
  dynamicToast.className = 'toast';
  dynamicToast.textContent = messageText;
  dynamicToast.style.cssText = `
    position: fixed;
    top: 20px;
    right: 20px;
    background: ${notificationType === 'success' ? '#22c55e' : notificationType === 'error' ? '#ef4444' : '#3b82f6'};
    color: white;
    padding: 1rem 1.5rem;
    border-radius: 8px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
    z-index: 10000;
    animation: slideIn 0.3s ease;
  `;

  document.body.appendChild(dynamicToast);
  setTimeout(() => {
    dynamicToast.style.animation = 'slideOut 0.3s ease';
    setTimeout(() => dynamicToast.remove(), 300);
  }, 3000);
};

/**
 * Anuncia mensajes para lectores de pantalla mediante un elemento de aria-live.
 * @param {string} screenReaderMessage
 */
const announceScreenReaderMessage = (screenReaderMessage) => {
  let announcerElement = document.getElementById('live-announcer');
  if (!announcerElement) {
    announcerElement = document.createElement('div');
    announcerElement.id = 'live-announcer';
    announcerElement.setAttribute('role', 'status');
    announcerElement.setAttribute('aria-live', 'polite');
    announcerElement.setAttribute('aria-atomic', 'true');
    announcerElement.style.cssText = 'position:absolute;left:-10000px;width:1px;height:1px;overflow:hidden;';
    document.body.appendChild(announcerElement);
  }
  announcerElement.textContent = screenReaderMessage;
};

/**
 * Verifica si la fecha de la cita permite el cambio al estado solicitado.
 * @param {string} appointmentDate
 * @param {string} targetStatus
 * @returns {boolean}
 */
const canChangeStatusByDate = (appointmentDate, targetStatus) => {
  if (!appointmentDate) return false;

  const restrictedStatuses = ['Atendida', 'No asistió', 'No asistida'];
  const normalizedStatus = String(targetStatus || '').trim();
  
  if (normalizedStatus !== 'En consulta' && !restrictedStatuses.includes(normalizedStatus)) {
    return true;
  }

  const scheduledDate = new Date(`${appointmentDate}T00:00:00`);
  const todayDate = new Date();
  todayDate.setHours(0, 0, 0, 0);

  return scheduledDate <= todayDate;
};

// ===================================================================
// 3. COMUNICACIÓN CON LA API REST
// ===================================================================

/**
 * Envía la actualización de estado de una cita al servidor.
 * @param {string|number} appointmentId
 * @param {string} newStatus
 * @param {Function} [onSuccessCallback]
 */
const changeAppointmentStatusApi = async (appointmentId, newStatus, onSuccessCallback) => {
  if (!appointmentId) return;

  try {
    if (window.apiRequest) {
      const result = await window.apiRequest(`/api/citas/${encodeURIComponent(appointmentId)}/estado`, {
        method: 'PUT',
        body: JSON.stringify({ estado: newStatus })
      });
      if (!result?.success) {
        throw new Error(result?.message || 'No se pudo actualizar el estado de la cita.');
      }

      showToastNotification(result.message || `Cita actualizada a '${newStatus}' correctamente.`, 'success');
      if (onSuccessCallback) onSuccessCallback();
    } else {
      const formElement = document.createElement('form');
      formElement.method = 'POST';
      formElement.action = '/gestion-de-citas/cambiar-estado';

      const csrfToken = getCsrfTokenValue();
      if (csrfToken) {
        const inputCsrf = document.createElement('input');
        inputCsrf.type = 'hidden';
        inputCsrf.name = '__RequestVerificationToken';
        inputCsrf.value = csrfToken;
        formElement.appendChild(inputCsrf);
      }

      const inputId = document.createElement('input');
      inputId.type = 'hidden';
      inputId.name = 'IdCita';
      inputId.value = appointmentId;
      formElement.appendChild(inputId);

      const inputStatus = document.createElement('input');
      inputStatus.type = 'hidden';
      inputStatus.name = 'Estado';
      inputStatus.value = newStatus;
      formElement.appendChild(inputStatus);

      const inputReturnUrl = document.createElement('input');
      inputReturnUrl.type = 'hidden';
      inputReturnUrl.name = 'ReturnUrl';
      inputReturnUrl.value = window.location.pathname + window.location.search;
      formElement.appendChild(inputReturnUrl);

      document.body.appendChild(formElement);
      formElement.submit();
    }
  } catch (error) {
    console.error('Error al cambiar estado:', error);
    showToastNotification(error.message || 'Ocurrió un error al actualizar el estado de la cita.', 'error');
  }
};

// ===================================================================
// 4. RENDERIZADO Y MANIPULACIÓN DEL DOM
// ===================================================================

/**
 * Construye el bloque HTML informativo del profesional asignado a la cita.
 * @param {Object} doctorDetails
 * @returns {string}
 */
const buildProfessionalDetailBlock = (doctorDetails) => {
  const extraInfoLines = [];
  if (doctorDetails.email) extraInfoLines.push(`<div><strong>Correo:</strong> ${escapeHtml(doctorDetails.email)}</div>`);
  if (doctorDetails.phone) extraInfoLines.push(`<div><strong>Teléfono:</strong> ${escapeHtml(doctorDetails.phone)}</div>`);
  if (doctorDetails.registry) extraInfoLines.push(`<div><strong>Registro médico:</strong> ${escapeHtml(doctorDetails.registry)}</div>`);
  if (doctorDetails.userStatus) extraInfoLines.push(`<div><strong>Estado cuenta:</strong> ${escapeHtml(doctorDetails.userStatus)}</div>`);

  return (doctorDetails.name || extraInfoLines.length > 0)
    ? `<div class="appointment-professional-details">
           ${doctorDetails.name ? `<div><strong>Profesional:</strong> ${escapeHtml(doctorDetails.name)}</div>` : ''}
           ${extraInfoLines.join('')}
       </div>`
    : '';
};

/**
 * Renderiza el contenido del modal con la información de la cita seleccionada.
 * @param {HTMLElement} modalContentElement
 * @param {Object} appointmentData
 */
const renderAppointmentModalContent = (modalContentElement, appointmentData) => {
  const doctorBlockHtml = buildProfessionalDetailBlock(appointmentData.doctorInfo);

  modalContentElement.innerHTML = `
    <div class="appointment-detail-layout">
        <div class="appointment-detail-summary">
            <h3>${escapeHtml(appointmentData.patientName)}</h3>
            <p>
                ${appointmentData.patientId ? `<strong>ID:</strong> ${escapeHtml(appointmentData.patientId)} · ` : ''}📅 ${escapeHtml(appointmentData.dateStr)} | ⏰ ${escapeHtml(appointmentData.startTime)} - ${escapeHtml(appointmentData.endTime)}
            </p>
        </div>
        ${doctorBlockHtml}
        <div class="appointment-detail-facts">
            <div><strong>Servicio:</strong> ${escapeHtml(appointmentData.serviceName)}</div>
            <div><strong>Consultorio:</strong> ${escapeHtml(appointmentData.officeName)}</div>
            <div><strong>Estado actual:</strong> <span class="badge">${escapeHtml(appointmentData.status)}</span></div>
        </div>
        <div class="appointment-detail-notes">
            <strong>Observaciones previas:</strong>
            <p>
                ${escapeHtml(appointmentData.notes)}
            </p>
        </div>
    </div>
  `;
};

/**
 * Actualiza los indicadores visuales de tiempo real (en curso, próxima, retrasada).
 */
const updateRealtimeAppointmentIndicators = () => {
  const currentDate = new Date();
  const appointmentCards = document.querySelectorAll('.appointment[data-start-time]');
  let updatedCount = 0;

  appointmentCards.forEach(card => {
    const cardDate = card.dataset.date;
    const startTimeStr = card.dataset.startTime;
    const endTimeStr = card.dataset.endTime;
    const currentStatus = card.dataset.status;

    if (!cardDate || !startTimeStr || !endTimeStr) return;

    const startDateTime = new Date(`${cardDate}T${startTimeStr}`);
    const endDateTime = new Date(`${cardDate}T${endTimeStr}`);

    card.classList.remove('in-progress', 'upcoming', 'overdue');
    const indicatorSpan = card.querySelector('.appt-status-indicator');
    if (!indicatorSpan) return;

    if (currentDate >= startDateTime && currentDate <= endDateTime) {
      card.classList.add('in-progress');
      indicatorSpan.textContent = 'EN CURSO';
      indicatorSpan.setAttribute('aria-label', 'Cita en curso');
      updatedCount++;
    } else if (currentDate < startDateTime) {
      const minutesRemaining = Math.floor((startDateTime - currentDate) / 60000);
      if (minutesRemaining <= 15) {
        card.classList.add('upcoming');
        indicatorSpan.textContent = `En ${minutesRemaining} min`;
        indicatorSpan.setAttribute('aria-label', `Cita en ${minutesRemaining} minutos`);
        updatedCount++;
      }
    } else if (currentDate > endDateTime && !['atendida', 'cancelada', 'completada'].includes((currentStatus || '').toLowerCase())) {
      card.classList.add('overdue');
      indicatorSpan.textContent = 'RETRASADA';
      indicatorSpan.setAttribute('aria-label', 'Cita retrasada');
      updatedCount++;
    }
  });

  if (updatedCount > 0) {
    console.log(`⏰ ${updatedCount} indicadores de tiempo actualizados`);
  }
};

/**
 * Recalcula y actualiza los contadores del panel de estadísticas.
 */
const updateAgendaStatisticsCounters = () => {
  const appointmentCards = document.querySelectorAll('.appointment:not(.available)');
  const statistics = {
    total: 0,
    programada: 0,
    confirmada: 0,
    atendida: 0,
    cancelada: 0
  };

  appointmentCards.forEach(card => {
    if (card.style.display === 'none') return;

    statistics.total++;
    const statusLower = (card.dataset.status || '').toLowerCase();

    if (statusLower.includes('programada') || statusLower.includes('agendada')) {
      statistics.programada++;
    } else if (statusLower.includes('confirmada')) {
      statistics.confirmada++;
    } else if (statusLower.includes('atendida')) {
      statistics.atendida++;
    } else if (statusLower.includes('cancelada')) {
      statistics.cancelada++;
    }
  });

  const updateStatValue = (selector, value) => {
    const targetElement = document.querySelector(selector);
    if (targetElement) targetElement.textContent = value;
  };

  updateStatValue('.quick-stat-item[data-status="total"] strong', statistics.total);
  updateStatValue('.quick-stat-item[data-status="programada"] strong', statistics.programada);
  updateStatValue('.quick-stat-item[data-status="confirmada"] strong', statistics.confirmada);
  updateStatValue('.quick-stat-item[data-status="atendida"] strong', statistics.atendida);

  console.log('📊 Estadísticas actualizadas:', statistics);
};

// ===================================================================
// 5. GESTIÓN DE MODALES
// ===================================================================

/**
 * Cierra el modal especificado por elemento DOM.
 * @param {HTMLElement} modalElement
 */
const closeModalElement = (modalElement) => {
  if (!modalElement) return;
  modalElement.classList.remove('open');
  modalElement.setAttribute('aria-hidden', 'true');
  modalElement.removeAttribute('hidden');
  modalElement.setAttribute('inert', '');
};

/**
 * Inicializa el modal de detalle de cita y asigna manejadores de botones clínicos.
 */
const initAppointmentModals = () => {
  const modalElement = document.getElementById('modalAppointment');
  const modalCloseButton = document.getElementById('modalApptClose');
  const modalContentElement = document.getElementById('modalApptContent');
  const startConsultationButton = document.getElementById('btnIniciarAtencion');
  const markAttendedButton = document.getElementById('btnMarcarAtendida');
  const cancelButton = document.getElementById('modalApptCancelar');

  if (!modalElement || !modalContentElement) return;

  document.querySelectorAll('.appointment:not(.available)').forEach(card => {
    card.addEventListener('click', () => {
      selectedAppointmentId = card.getAttribute('data-id');
      selectedPatientId = card.getAttribute('data-patient-id');

      const appointmentData = {
        patientId: selectedPatientId,
        patientName: card.getAttribute('data-patient-name') || 'Paciente sin nombre',
        serviceName: card.getAttribute('data-service-name') || 'Servicio no especificado',
        officeName: card.getAttribute('data-office-name') || 'Sin consultorio',
        dateStr: card.getAttribute('data-date') || '',
        startTime: card.getAttribute('data-start-time') || '',
        endTime: card.getAttribute('data-end-time') || '',
        status: card.getAttribute('data-status') || 'Agendada',
        notes: card.getAttribute('data-notes') || 'Sin observaciones.',
        doctorInfo: {
          name: card.getAttribute('data-professional-name') || '',
          email: card.getAttribute('data-professional-email') || '',
          phone: card.getAttribute('data-professional-phone') || '',
          registry: card.getAttribute('data-professional-registry') || '',
          userStatus: card.getAttribute('data-professional-user-status') || ''
        }
      };

      const currentStatus = window.CommonUtils?.normalizeAppointmentStatus(appointmentData.status)
        || appointmentData.status.trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[-\s]+/g, '_');
      const canStartConsultation = ['programada', 'confirmada', 'en_sala_de_espera'].includes(currentStatus);
      const canCloseAppointment = ['programada', 'confirmada', 'en_sala_de_espera', 'en_proceso'].includes(currentStatus);
      if (startConsultationButton) startConsultationButton.hidden = !canStartConsultation;
      if (markAttendedButton) markAttendedButton.hidden = !canCloseAppointment;
      if (cancelButton) cancelButton.hidden = !canCloseAppointment;

      const rescheduleButton = modalElement.querySelector('#modalApptReagendar');
      if (rescheduleButton) {
        const isReschedulable = card.dataset.reschedulable === 'true';
        rescheduleButton.hidden = !isReschedulable;
        Object.assign(rescheduleButton.dataset, {
          id: selectedAppointmentId || '',
          date: appointmentData.dateStr,
          startTime: appointmentData.startTime,
          patientName: appointmentData.patientName,
          serviceName: appointmentData.serviceName,
          reschedulable: String(isReschedulable)
        });
      }

      renderAppointmentModalContent(modalContentElement, appointmentData);

      const historyUrl = selectedPatientId
        ? `/historia-clinica/st-odo-03-historial?pacienteId=${encodeURIComponent(selectedPatientId)}&citaId=${encodeURIComponent(selectedAppointmentId || '')}`
        : '/historia-clinica/st-odo-06-pacientes';

      const viewHistoryButton = document.createElement('button');
      viewHistoryButton.type = 'button';
      viewHistoryButton.className = 'btn-secondary';
      viewHistoryButton.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">history</span> Ver historial';
      viewHistoryButton.addEventListener('click', () => {
        window.location.href = historyUrl;
      });

      const modalActionsGrid = modalElement.querySelector('.modal-actions-grid');
      if (modalActionsGrid) {
        const existingHistoryButton = modalActionsGrid.querySelector('[data-history-patient="true"]');
        if (existingHistoryButton) existingHistoryButton.remove();
        viewHistoryButton.setAttribute('data-history-patient', 'true');
        modalActionsGrid.insertBefore(viewHistoryButton, modalActionsGrid.firstChild);
      }

      if (startConsultationButton) {
        startConsultationButton.onclick = (e) => {
          e.preventDefault();
          if (!canChangeStatusByDate(appointmentData.dateStr, 'En consulta')) {
            showToastNotification('⚠️ No puedes iniciar atención antes del día programado de la cita.', 'error');
            return;
          }
          changeAppointmentStatusApi(selectedAppointmentId, 'En consulta', () => {
            window.location.href = historyUrl;
          });
        };
      }

      modalElement.classList.add('open');
      modalElement.setAttribute('aria-hidden', 'false');
      modalElement.removeAttribute('hidden');
      modalElement.removeAttribute('inert');
    });
  });

  modalCloseButton?.addEventListener('click', () => closeModalElement(modalElement));
  modalElement.addEventListener('click', (e) => {
    if (e.target === modalElement) closeModalElement(modalElement);
  });

  markAttendedButton?.addEventListener('click', () => {
    if (!selectedAppointmentId) return;
    const targetCard = document.querySelector(`.appointment[data-id="${selectedAppointmentId}"]`) || document.querySelector(`[data-id="${selectedAppointmentId}"]`);
    const cardDate = targetCard?.getAttribute('data-date');
    if (!canChangeStatusByDate(cardDate, 'Atendida')) {
      showToastNotification('⚠️ Solo puedes marcar la cita como atendida desde el día programado o después.', 'error');
      return;
    }
    changeAppointmentStatusApi(selectedAppointmentId, 'Atendida', () => {
      closeModalElement(modalElement);
      window.location.reload();
    });
  });

  cancelButton?.addEventListener('click', () => {
    if (!selectedAppointmentId) return;
    const targetCard = document.querySelector(`.appointment[data-id="${selectedAppointmentId}"]`) || document.querySelector(`[data-id="${selectedAppointmentId}"]`);
    const cardDate = targetCard?.getAttribute('data-date');
    if (!canChangeStatusByDate(cardDate, 'No asistió')) {
      showToastNotification('⚠️ Solo puedes registrar inasistencia desde el día de la cita o después.', 'error');
      return;
    }
    changeAppointmentStatusApi(selectedAppointmentId, 'No asistió', () => {
      closeModalElement(modalElement);
      window.location.reload();
    });
  });
};

/**
 * Inicializa el modal para registrar una nueva cita o sobrecupo.
 */
const initNewAppointmentModal = () => {
  const fabButton = document.getElementById('btnNewAppointment');
  const modalNewElement = document.getElementById('modalNewAppointment');
  const modalCloseButton = document.getElementById('modalNewApptClose');
  const modalCancelButton = document.getElementById('modalNewApptCancel');

  if (!modalNewElement) return;

  const openNewAppointmentModal = (dateString = '') => {
    if (dateString) {
      const dateInputField = document.getElementById('newApptDate');
      if (dateInputField) dateInputField.value = dateString;
    }
    modalNewElement.classList.add('open');
    modalNewElement.setAttribute('aria-hidden', 'false');
    modalNewElement.removeAttribute('hidden');
    modalNewElement.removeAttribute('inert');
  };

  fabButton?.addEventListener('click', () => openNewAppointmentModal());

  document.querySelectorAll('.appointment.available').forEach(availableSlot => {
    availableSlot.addEventListener('click', () => {
      const slotDate = availableSlot.getAttribute('data-date');
      openNewAppointmentModal(slotDate);
    });
  });

  modalCloseButton?.addEventListener('click', () => closeModalElement(modalNewElement));
  modalCancelButton?.addEventListener('click', () => closeModalElement(modalNewElement));
  modalNewElement.addEventListener('click', (e) => {
    if (e.target === modalNewElement) closeModalElement(modalNewElement);
  });
};

// ===================================================================
// 6. INTERACCIONES AVANZADAS DE LA AGENDA
// ===================================================================
// NOTA: el drag & drop y el botón "Reagendar" los gestiona
// shared/reagendamiento-agenda.js (cargado por la vista). No duplicar esa
// lógica aquí: al registrar el mismo 'drop' en dos sitios, un solo soltar
// disparaba a la vez el modal compartido y un confirm() + fetch con header
// CSRF incorrecto ('RequestVerificationToken' en lugar de 'X-CSRF-TOKEN').

/**
 * Inicializa los controles del toggle entre vistas de calendario.
 */
const initViewModeToggle = () => {
  const viewButtons = document.querySelectorAll('.view-btn');
  const calendarWrapper = document.querySelector('.calendar-scroll-wrapper');
  if (!viewButtons.length || !calendarWrapper) return;

  viewButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const selectedView = btn.dataset.view;

      viewButtons.forEach(b => {
        b.classList.remove('active');
        b.setAttribute('aria-pressed', 'false');
      });
      btn.classList.add('active');
      btn.setAttribute('aria-pressed', 'true');

      calendarWrapper.dataset.view = selectedView;
      localStorage.setItem('agendaView', selectedView);

      announceScreenReaderMessage(`Vista cambiada a ${btn.textContent.trim()}`);
      console.log(`📅 Vista cambiada a: ${selectedView}`);
    });
  });

  const savedViewPreference = localStorage.getItem('agendaView');
  if (savedViewPreference) {
    const savedButton = document.querySelector(`.view-btn[data-view="${savedViewPreference}"]`);
    if (savedButton) savedButton.click();
  }
};

/**
 * Configura la búsqueda reactiva de pacientes en la grilla.
 */
const initRealtimePatientSearch = () => {
  const searchInput = document.getElementById('searchPatient');
  if (!searchInput) return;

  let searchTimeout;

  searchInput.addEventListener('input', (e) => {
    clearTimeout(searchTimeout);

    searchTimeout = setTimeout(() => {
      const queryText = e.target.value.toLowerCase().trim();
      const appointmentCards = document.querySelectorAll('.appointment:not(.available)');
      let matchCount = 0;

      appointmentCards.forEach(card => {
        const patientName = (card.dataset.patientName || '').toLowerCase();
        const serviceName = (card.dataset.serviceName || '').toLowerCase();
        const notesText = (card.dataset.notes || '').toLowerCase();

        const isMatch = !queryText ||
          patientName.includes(queryText) ||
          serviceName.includes(queryText) ||
          notesText.includes(queryText);

        if (isMatch) {
          card.classList.remove('search-hidden');
          if (queryText) {
            card.classList.add('search-match');
            matchCount++;
          } else {
            card.classList.remove('search-match');
          }
        } else {
          card.classList.add('search-hidden');
          card.classList.remove('search-match');
        }
      });

      if (queryText && matchCount === 0) {
        announceScreenReaderMessage('No se encontraron citas con ese criterio');
      } else if (queryText) {
        announceScreenReaderMessage(`${matchCount} cita${matchCount !== 1 ? 's' : ''} encontrada${matchCount !== 1 ? 's' : ''}`);
      }

      console.log(`🔍 Búsqueda: "${queryText}" - ${matchCount} resultados`);
    }, 300);
  });

  searchInput.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      searchInput.value = '';
      searchInput.dispatchEvent(new Event('input'));
      searchInput.blur();
    }
  });
};

/**
 * Inicializa el filtro select por estado de cita.
 */
const initStatusFilter = () => {
  const filterStatusSelect = document.getElementById('filterStatus');
  if (!filterStatusSelect) return;

  // Normaliza el estado a un token canónico para que "programada" (opción del
  // select) coincida también con "Agendada"/"agendada" (valor que llega del server).
  const normalizeStatus = (value) =>
    window.CommonUtils?.normalizeAppointmentStatus(value)
    || String(value || '').toLowerCase().trim();

  filterStatusSelect.addEventListener('change', (e) => {
    const selectedStatus = e.target.value;
    const targetStatus = normalizeStatus(selectedStatus);
    const appointmentCards = document.querySelectorAll('.appointment:not(.available)');
    let visibleCount = 0;

    appointmentCards.forEach(card => {
      const cardStatus = normalizeStatus(card.dataset.status || '');

      if (!selectedStatus || cardStatus === targetStatus) {
        card.style.display = '';
        visibleCount++;
      } else {
        card.style.display = 'none';
      }
    });

    const statusLabelText = selectedStatus || 'todos los estados';
    announceScreenReaderMessage(`Mostrando ${visibleCount} citas con estado: ${statusLabelText}`);
    console.log(`🎯 Filtro de estado: ${statusLabelText} - ${visibleCount} citas`);
  });
};

/**
 * Configura los atajos de teclado globales para navegación rápida.
 */
const initKeyboardShortcuts = () => {
  document.addEventListener('keydown', (e) => {
    if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) return;

    if (e.key === 'v' || e.key === 'V') {
      const viewButtons = document.querySelectorAll('.view-btn');
      const activeBtn = document.querySelector('.view-btn.active');
      if (activeBtn && viewButtons.length > 0) {
        const currentIndex = Array.from(viewButtons).indexOf(activeBtn);
        const nextIndex = (currentIndex + 1) % viewButtons.length;
        viewButtons[nextIndex].click();
      }
    }

    if (e.key === 'h' || e.key === 'H') {
      const todayButton = document.getElementById('btnToday');
      if (todayButton) todayButton.click();
    }

    if (e.key === 'ArrowLeft' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      document.getElementById('btnPrev')?.click();
    }

    if (e.key === 'ArrowRight' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      document.getElementById('btnNext')?.click();
    }
  });

  console.log('⌨️ Atajos de teclado configurados: V (cambiar vista), H (hoy), Ctrl+← → (navegar semanas)');
};

// ===================================================================
// 7. LISTENERS DE EVENTOS E INICIALIZACIÓN
// ===================================================================

/**
 * Configura los botones de navegación por semana (Anterior, Siguiente, Hoy).
 */
const initWeekNavigationControls = () => {
  const previousWeekButton = document.getElementById('btnPrev');
  const nextWeekButton = document.getElementById('btnNext');
  const todayButton = document.getElementById('btnToday');
  const weekLabelElement = document.getElementById('weekLabel');

  if (!weekLabelElement) return;

  const currentWeekStartStr = weekLabelElement.getAttribute('data-week-start');
  // Se añade 'T00:00:00' para forzar el parseo en hora LOCAL. Sin esto, "yyyy-MM-dd"
  // se interpreta como UTC y en zonas con offset negativo (p. ej. America/Bogota)
  // se desplaza un día, provocando que "semana anterior/siguiente" salte de semana.
  const currentWeekStartDate = currentWeekStartStr ? new Date(`${currentWeekStartStr}T00:00:00`) : new Date();

  previousWeekButton?.addEventListener('click', () => {
    const previousWeek = new Date(currentWeekStartDate);
    previousWeek.setDate(previousWeek.getDate() - 7);
    navigateToWeekDate(previousWeek);
  });

  nextWeekButton?.addEventListener('click', () => {
    const nextWeek = new Date(currentWeekStartDate);
    nextWeek.setDate(nextWeek.getDate() + 7);
    navigateToWeekDate(nextWeek);
  });

  todayButton?.addEventListener('click', () => {
    navigateToWeekDate(new Date());
  });
};

/**
 * Navega a una semana específica actualizando el parámetro weekStart en la URL.
 * @param {Date} dateObj
 */
const navigateToWeekDate = (dateObj) => {
  const yearStr = dateObj.getFullYear();
  const monthStr = String(dateObj.getMonth() + 1).padStart(2, '0');
  const dayStr = String(dateObj.getDate()).padStart(2, '0');
  const dateFormatted = `${yearStr}-${monthStr}-${dayStr}`;

  const urlParams = new URLSearchParams(window.location.search);
  urlParams.set('weekStart', dateFormatted);
  window.location.search = urlParams.toString();
};

/**
 * Inicializa el filtro de consultorios.
 */
const initOfficeFilterControl = () => {
  const officeFilterSelect = document.getElementById('filterOffice');
  if (!officeFilterSelect) return;

  officeFilterSelect.addEventListener('change', (e) => {
    const urlParams = new URLSearchParams(window.location.search);
    if (e.target.value) {
      urlParams.set('officeId', e.target.value);
    } else {
      urlParams.delete('officeId');
    }
    window.location.search = urlParams.toString();
  });
};

/**
 * Inicializa todas las funcionalidades avanzadas de la agenda.
 */
const initAdvancedAgendaFeatures = () => {
  console.log('🚀 Inicializando mejoras de la agenda...');
  initViewModeToggle();
  initRealtimePatientSearch();
  initStatusFilter();
  initKeyboardShortcuts();

  updateRealtimeAppointmentIndicators();
  const realtimeIndicatorInterval = window.setInterval(() => {
    if (!document.hidden) updateRealtimeAppointmentIndicators();
  }, 60000);
  window.addEventListener('pagehide', () => window.clearInterval(realtimeIndicatorInterval), { once: true });
  updateAgendaStatisticsCounters();

  if (!document.getElementById('agenda-animations')) {
    const styleElement = document.createElement('style');
    styleElement.id = 'agenda-animations';
    styleElement.textContent = `
      @keyframes slideIn {
        from { transform: translateX(100%); opacity: 0; }
        to { transform: translateX(0); opacity: 1; }
      }
      @keyframes slideOut {
        from { transform: translateX(0); opacity: 1; }
        to { transform: translateX(100%); opacity: 0; }
      }
    `;
    document.head.appendChild(styleElement);
  }

  console.log('✅ Agenda mejorada iniciada correctamente');
};

/**
 * Inicializa el dashboard completo de la agenda del odontólogo.
 */
const initAgendaDashboard = () => {
  initWeekNavigationControls();
  initOfficeFilterControl();
  initAppointmentModals();
  initNewAppointmentModal();
};

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    initAgendaDashboard();
    setTimeout(initAdvancedAgendaFeatures, 1000);
  });
} else {
  initAgendaDashboard();
  setTimeout(initAdvancedAgendaFeatures, 1000);
}
