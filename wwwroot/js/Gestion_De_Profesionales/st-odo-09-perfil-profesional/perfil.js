/* ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: Perfil Profesional (st-odo-09-perfil-profesional)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Profesionales/st-odo-09-perfil-profesional/perfil.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra la vista de perfil de usuario para el profesional odontológico.
 * Permite actualizar datos personales, especialidades, horario laboral semanal y credenciales de acceso (contraseña)
 * consumiendo los endpoints de `ProfesionalesApiController`.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Envíos asíncronos vía fetch con tokens CSRF (Antiforgery) agregados en cabeceras.
 * - Validación estricta de política de contraseñas seguras y horarios válidos por día de la semana.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionProfesionalesController / ProfesionalesApiController
 * - HTML: Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml
 * ============================================ */

// ===================================================================
// 1. CONSTANTES Y CONFIGURACIÓN
// ===================================================================

const API_BASE_URL = '/api';

/** Estructura base de los días de la semana con metadatos de visualización */
const WEEK_DAYS_STRUCTURE = [
  { day: 'Lun', dayFull: 'Lunes'     },
  { day: 'Mar', dayFull: 'Martes'    },
  { day: 'Mié', dayFull: 'Miércoles' },
  { day: 'Jue', dayFull: 'Jueves'    },
  { day: 'Vie', dayFull: 'Viernes'   },
  { day: 'Sáb', dayFull: 'Sábado'    },
  { day: 'Dom', dayFull: 'Domingo'   },
];

/** Plantilla inicial de datos del perfil */
const DEFAULT_PROFILE_TEMPLATE = { horario: [] };

// ===================================================================
// 2. GESTIÓN DE ESTADO LOCAL
// ===================================================================

let profileState = { ...DEFAULT_PROFILE_TEMPLATE };
let editingDayIndex = null;
let lastFocusedElement = null;
let isScheduleLoadedFromApi = false;

// ===================================================================
// 3. UTILIDADES Y FORMATEADORES
// ===================================================================

/**
 * Obtiene un elemento DOM por ID de forma segura con advertencia en consola.
 * @param {string} elementId
 * @returns {HTMLElement|null}
 */
const getElementByIdSafe = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (callbackFn, delayMs) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(callbackFn, delayMs) : callbackFn;

/**
 * Ejecuta una petición fetch con timeout dinámico mediante AbortController.
 * @param {string} url
 * @param {Object} [options={}]
 * @param {number} [timeoutMs=15000]
 * @returns {Promise<Response>}
 */
const fetchWithTimeout = async (url, options = {}, timeoutMs = 15000) => {
  const abortController = new AbortController();
  const timeoutId = window.setTimeout(() => abortController.abort(), timeoutMs);

  try {
    return await fetch(url, { ...options, signal: abortController.signal });
  } catch (error) {
    if (error.name === 'AbortError') {
      throw new Error('La solicitud tardó demasiado. Inténtalo de nuevo.');
    }
    throw error;
  } finally {
    window.clearTimeout(timeoutId);
  }
};

/**
 * Calcula la cantidad de horas transcurridas entre hora inicio y fin (HH:MM).
 * @param {string} startTime
 * @param {string} endTime
 * @returns {number}
 */
const calculateTimeDifferenceInHours = (startTime, endTime) => {
  if (!startTime || !endTime) return 0;
  const [startHour, startMinute] = startTime.split(':').map(Number);
  const [endHour, endMinute] = endTime.split(':').map(Number);
  const totalHours = (endHour + endMinute / 60) - (startHour + startMinute / 60);
  return Math.max(0, totalHours);
};

/**
 * Obtiene la clase CSS (morning / afternoon) según la hora de inicio.
 * @param {string} startTime
 * @returns {string}
 */
const getSlotCssClassByStartHour = (startTime) => {
  const [startHour] = startTime.split(':').map(Number);
  return startHour < 12 ? 'morning' : 'afternoon';
};

/**
 * Obtiene el ícono representativo según la hora de inicio.
 * @param {string} startTime
 * @returns {string}
 */
const getSlotIconByStartHour = (startTime) => {
  const [startHour] = startTime.split(':').map(Number);
  return startHour < 12 ? '🌅' : '🌇';
};

/**
 * Obtiene el ID del profesional leyendo el atributo dataset del contenedor DOM.
 * @returns {number|null}
 */
const getProfessionalIdFromDom = () => {
  const scheduleGridElement = document.getElementById('scheduleGrid');
  const professionalId = parseInt(scheduleGridElement?.dataset?.profesionalId ?? '0', 10);
  return professionalId > 0 ? professionalId : null;
};

/**
 * Evalúa la fortaleza de una contraseña devolviendo 'weak', 'medium' o 'strong'.
 * @param {string} passwordText
 * @returns {string|null}
 */
const evaluatePasswordStrengthLevel = (passwordText) => {
  if (passwordText.length === 0) return null;

  let strengthScore = 0;
  if (passwordText.length >= 8) strengthScore++;
  if (passwordText.length >= 12) strengthScore++;
  if (/[a-z]/.test(passwordText) && /[A-Z]/.test(passwordText)) strengthScore++;
  if (/\d/.test(passwordText)) strengthScore++;
  if (/[^a-zA-Z0-9]/.test(passwordText)) strengthScore++;

  if (strengthScore <= 2) return 'weak';
  if (strengthScore <= 4) return 'medium';
  return 'strong';
};

// ===================================================================
// 4. COMUNICACIÓN CON LA API REST
// ===================================================================

/**
 * Transforma el listado de horarios de la API a la estructura interna de 7 días.
 * @param {Array} apiSchedulesList
 * @returns {Array}
 */
const mapApiSchedulesToProfileState = (apiSchedulesList) => {
  const normalizeDayName = (dayName) => (dayName || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();

  return WEEK_DAYS_STRUCTURE.map(({ day, dayFull }) => {
    const matchedSchedule = apiSchedulesList.find(
      (item) => normalizeDayName(item.diaSemana) === normalizeDayName(dayFull)
    );
    return matchedSchedule
      ? { day, dayFull, active: matchedSchedule.activo !== false, start: matchedSchedule.horaInicio || '', end: matchedSchedule.horaFin || '' }
      : { day, dayFull, active: false, start: '', end: '' };
  });
};

/**
 * Obtiene la configuración de horarios del profesional desde la API.
 * @returns {Promise<Object>}
 */
const fetchProfessionalScheduleApi = async () => {
  const professionalId = getProfessionalIdFromDom();
  if (!professionalId) {
    console.warn('[SmileTrack] No se encontró el ID del profesional en el DOM; se omite la carga del horario.');
    window.ToastService?.error(
      '❌ No se pudo cargar el horario',
      'No se identificó el profesional. Refresca la página e inténtalo de nuevo.'
    );
    return { horario: [], success: false };
  }

  try {
    const response = await fetchWithTimeout(`${API_BASE_URL}/profesionales/${professionalId}/horarios`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include'
    });

    if (!response.ok) {
      console.warn(`[SmileTrack] Error al cargar horarios: ${response.status}. Se deja el horario vacío.`);
      window.ToastService?.error(
        '❌ Error al cargar el horario',
        `El servidor respondió con código ${response.status}. No se podrá guardar el horario hasta que se carguen los datos reales.`
      );
      return { horario: [], success: false };
    }

    const payloadJson = await response.json();
    const mappedSchedule = mapApiSchedulesToProfileState(payloadJson.data ?? []);
    return { horario: mappedSchedule, success: true };
  } catch (error) {
    console.warn('[SmileTrack] Error de red al cargar horarios:', error);
    window.ToastService?.error(
      '❌ Error de conexión',
      'No se pudo contactar con el servidor para cargar el horario. Verifica la conexión y actualiza la página.'
    );
    return { horario: [], success: false };
  }
};

/**
 * Envía la actualización de contraseña a la API.
 * @param {Event} event
 */
const submitPasswordChangeApi = async (event) => {
  event.preventDefault();

  const currentPasswordElement = getElementByIdSafe('currentPassword');
  const newPasswordElement = getElementByIdSafe('newPassword');
  const confirmPasswordElement = getElementByIdSafe('confirmPassword');

  const currentPasswordValue = currentPasswordElement?.value;
  const newPasswordValue = newPasswordElement?.value;
  const confirmPasswordValue = confirmPasswordElement?.value;

  if (window.ValidationUtils) {
    if (currentPasswordElement) window.ValidationUtils.clearError(currentPasswordElement);
    if (newPasswordElement) window.ValidationUtils.clearError(newPasswordElement);
    if (confirmPasswordElement) window.ValidationUtils.clearError(confirmPasswordElement);
  }

  let isFormValid = true;

  if (currentPasswordValue?.length < 6) {
    if (window.ValidationUtils && currentPasswordElement) {
      window.ValidationUtils.showError(currentPasswordElement, null, 'La contraseña actual debe tener al menos 6 caracteres');
    }
    isFormValid = false;
  }

  if (newPasswordValue?.length < 8) {
    if (window.ValidationUtils && newPasswordElement) {
      window.ValidationUtils.showError(newPasswordElement, null, 'La nueva contraseña debe tener al menos 8 caracteres');
    }
    isFormValid = false;
  }

  if (newPasswordValue !== confirmPasswordValue) {
    if (window.ValidationUtils && confirmPasswordElement) {
      window.ValidationUtils.showError(confirmPasswordElement, null, 'Las nuevas contraseñas no coinciden');
    }
    isFormValid = false;
  }

  if (currentPasswordValue === newPasswordValue) {
    if (window.ValidationUtils && newPasswordElement) {
      window.ValidationUtils.showError(newPasswordElement, null, 'La nueva contraseña debe ser diferente a la actual');
    }
    isFormValid = false;
  }

  if (!isFormValid) {
    window.ToastService?.warning('⚠️ Verifique los campos resaltados en rojo');
    return;
  }

  const updateSubmitButton = event.target.querySelector('.btn-update');
  if (updateSubmitButton) {
    updateSubmitButton.disabled = true;
    updateSubmitButton.textContent = '⏳ Actualizando...';
  }

  try {
    const csrfToken = event.target.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const response = await fetchWithTimeout('/acceso-y-seguridad/cambiar-contrasena/api', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {})
      },
      body: JSON.stringify({
        ContrasenaActual: currentPasswordValue,
        NuevaContrasena: newPasswordValue,
        ConfirmarContrasena: confirmPasswordValue
      })
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok || payload.success === false) {
      throw new Error(payload.message || 'No fue posible cambiar la contraseña.');
    }

    window.ToastService?.success(payload.message || 'Contraseña actualizada. Inicia sesión nuevamente.');
    window.setTimeout(() => { window.location.href = '/acceso-y-seguridad/login'; }, 900);
  } catch (error) {
    window.ToastService?.error(error.message || 'No fue posible cambiar la contraseña.');
    if (updateSubmitButton) {
      updateSubmitButton.disabled = false;
      updateSubmitButton.textContent = 'Actualizar contraseña';
    }
  }
};

/**
 * Guarda los cambios de horario en la API.
 */
const saveScheduleChangesApi = async () => {
  if (editingDayIndex === null) return;

  if (!isScheduleLoadedFromApi) {
    window.ToastService?.error(
      '❌ No se puede guardar',
      'El horario no se cargó correctamente desde el servidor. Refresca la página e inténtalo de nuevo.'
    );
    return;
  }

  const currentDayState = profileState.horario[editingDayIndex];
  const isDayActive = getElementByIdSafe('modalDayActive')?.checked;
  const startTimeInput = getElementByIdSafe('modalStartTime');
  const endTimeInput = getElementByIdSafe('modalEndTime');
  const startTimeValue = startTimeInput?.value;
  const endTimeValue = endTimeInput?.value;

  if (window.ValidationUtils) {
    if (startTimeInput) window.ValidationUtils.clearError(startTimeInput);
    if (endTimeInput) window.ValidationUtils.clearError(endTimeInput);
  }

  if (isDayActive) {
    if (!startTimeValue || !endTimeValue) {
      if (window.ValidationUtils && !startTimeValue && startTimeInput) window.ValidationUtils.showError(startTimeInput, null, 'Por favor completa el horario');
      if (window.ValidationUtils && !endTimeValue && endTimeInput) window.ValidationUtils.showError(endTimeInput, null, 'Por favor completa el horario');
      window.ToastService?.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }

    const calculatedHours = calculateTimeDifferenceInHours(startTimeValue, endTimeValue);
    if (calculatedHours <= 0) {
      if (window.ValidationUtils && endTimeInput) {
        window.ValidationUtils.showError(endTimeInput, null, 'La hora de fin debe ser mayor a la hora de inicio');
      }
      window.ToastService?.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }

    if (calculatedHours > 12) {
      if (window.ValidationUtils && endTimeInput) {
        window.ValidationUtils.showError(endTimeInput, null, 'El horario máximo es de 12 horas por día');
      }
      window.ToastService?.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }
  }

  const updatedDayState = isDayActive
    ? { ...currentDayState, active: true, start: startTimeValue, end: endTimeValue }
    : { ...currentDayState, active: false, start: '', end: '' };

  const fullUpdatedSchedule = profileState.horario.map((dayItem, idx) =>
    idx === editingDayIndex ? updatedDayState : dayItem
  );

  const professionalId = getProfessionalIdFromDom();
  if (!professionalId) {
    profileState.horario[editingDayIndex] = updatedDayState;
    renderWeeklyScheduleGrid();
    closeScheduleEditorModal();
    window.ToastService?.success(`✅ Horario de ${updatedDayState.dayFull} actualizado`);
    return;
  }

  try {
    const saveButton = getElementByIdSafe('modalSave');
    if (saveButton) {
      saveButton.disabled = true;
      saveButton.setAttribute('aria-busy', 'true');
      saveButton.textContent = 'Guardando...';
    }

    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const response = await fetchWithTimeout(`${API_BASE_URL}/profesionales/${professionalId}/horarios`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        ...(csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {})
      },
      credentials: 'include',
      body: JSON.stringify(fullUpdatedSchedule)
    });

    if (!response.ok) {
      const errorJson = await response.json().catch(() => ({}));
      const errorMessage = errorJson.message || `Error ${response.status}`;
      window.ToastService?.error('❌ Error al guardar el horario', errorMessage);
      return;
    }

    profileState.horario = fullUpdatedSchedule;
    renderWeeklyScheduleGrid();
    closeScheduleEditorModal();
    window.ToastService?.success(`✅ Horario de ${updatedDayState.dayFull} actualizado`);
  } catch (error) {
    console.error('[SmileTrack] Error de red al guardar horario:', error);
    window.ToastService?.error('❌ Error de conexión', 'No se pudo guardar el horario. Inténtalo de nuevo.');
  } finally {
    const saveButton = getElementByIdSafe('modalSave');
    if (saveButton) {
      saveButton.disabled = false;
      saveButton.removeAttribute('aria-busy');
      saveButton.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">save</span> Guardar horario';
    }
  }
};

// ===================================================================
// 5. RENDERIZADO Y MANIPULACIÓN DEL DOM
// ===================================================================

/**
 * Renderiza una tarjeta individual de día para el horario.
 * @param {Object} dayData
 * @param {number} index
 * @returns {string}
 */
const renderScheduleDayCard = (dayData, index) => {
  const hours = dayData.active ? calculateTimeDifferenceInHours(dayData.start, dayData.end) : 0;
  const statusClass = dayData.active ? 'active' : 'inactive';

  let timeSlotsHtml = '';
  if (dayData.active && dayData.start && dayData.end) {
    const slotCssClass = getSlotCssClassByStartHour(dayData.start);
    const slotIcon = getSlotIconByStartHour(dayData.start);
    timeSlotsHtml = `
      <span class="time-slot ${slotCssClass}">
        <span class="time-slot-icon" aria-hidden="true">${slotIcon}</span>
        <time datetime="${dayData.start}">${dayData.start}</time> - <time datetime="${dayData.end}">${dayData.end}</time>
      </span>
    `;
  } else {
    timeSlotsHtml = '<div class="no-schedule">— Descanso —</div>';
  }

  const hoursBadgeText = dayData.active ? `${hours}h` : '—';

  return {
    html: `
      <div class="schedule-day ${statusClass}" 
           role="listitem"
           tabindex="0"
           aria-label="${dayData.dayFull}: ${dayData.active ? `${hours} horas, ${dayData.start} a ${dayData.end}` : 'No disponible'}"
           data-index="${index}">
        <div class="day-name">${dayData.day}</div>
        <div class="time-blocks">${timeSlotsHtml}</div>
        <div class="hours-badge" aria-label="${hours} horas">${hoursBadgeText}</div>
      </div>
    `,
    hours,
    isActive: dayData.active
  };
};

/**
 * Renderiza la grilla de horarios semanales.
 */
const renderWeeklyScheduleGrid = () => {
  const scheduleGridElement = getElementByIdSafe('scheduleGrid');
  if (!scheduleGridElement) return;

  let totalAccumulatedHours = 0;
  let activeDaysCount = 0;

  const htmlCardsList = profileState.horario.map((dayData, index) => {
    const renderedCard = renderScheduleDayCard(dayData, index);
    totalAccumulatedHours += renderedCard.hours;
    if (renderedCard.isActive) activeDaysCount++;
    return renderedCard.html;
  });

  scheduleGridElement.innerHTML = htmlCardsList.join('');

  scheduleGridElement.querySelectorAll('.schedule-day').forEach(dayCardElement => {
    const dayIndex = parseInt(dayCardElement.dataset.index, 10);
    
    dayCardElement.addEventListener('click', () => openScheduleEditorModal(dayIndex));
    dayCardElement.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        openScheduleEditorModal(dayIndex);
      }
    });
  });

  const totalHoursLabel = getElementByIdSafe('totalHours');
  const totalDaysLabel = getElementByIdSafe('totalDays');
  if (totalHoursLabel) totalHoursLabel.textContent = `${totalAccumulatedHours} horas`;
  if (totalDaysLabel) totalDaysLabel.textContent = `${activeDaysCount} días laborales`;
};

/**
 * Actualiza los campos visuales del modal de edición de horario.
 * @param {boolean} isDayActive
 */
const updateScheduleEditorModalUI = (isDayActive) => {
  const timeSectionContainer = getElementByIdSafe('modalTimeSection');
  const toggleTextSpan = getElementByIdSafe('toggleText');

  if (timeSectionContainer) {
    timeSectionContainer.classList.toggle('disabled', !isDayActive);
  }

  const startTimeInput = getElementByIdSafe('modalStartTime');
  const endTimeInput = getElementByIdSafe('modalEndTime');
  if (startTimeInput) startTimeInput.disabled = !isDayActive;
  if (endTimeInput) endTimeInput.disabled = !isDayActive;

  if (toggleTextSpan) {
    toggleTextSpan.textContent = isDayActive ? 'Día laboral' : 'No disponible';
    toggleTextSpan.classList.toggle('active', isDayActive);
    toggleTextSpan.classList.toggle('inactive', !isDayActive);
  }
};

/**
 * Actualiza el bloque de vista previa del horario en el modal.
 */
const updateSchedulePreviewDisplay = () => {
  const isDayActive = getElementByIdSafe('modalDayActive')?.checked;
  const startTimeValue = getElementByIdSafe('modalStartTime')?.value;
  const endTimeValue = getElementByIdSafe('modalEndTime')?.value;
  const previewContainer = getElementByIdSafe('modalPreview');

  if (!previewContainer) return;

  const previewTimeLabel = getElementByIdSafe('previewTime');
  const previewHoursLabel = getElementByIdSafe('previewHours');

  if (isDayActive && startTimeValue && endTimeValue) {
    const calculatedHours = calculateTimeDifferenceInHours(startTimeValue, endTimeValue);
    if (previewTimeLabel) previewTimeLabel.textContent = `${startTimeValue} - ${endTimeValue}`;
    if (previewHoursLabel) previewHoursLabel.textContent = `${calculatedHours} hora${calculatedHours !== 1 ? 's' : ''}`;
    previewContainer.style.opacity = '1';
  } else {
    previewContainer.style.opacity = '0.5';
    if (previewTimeLabel) previewTimeLabel.textContent = isDayActive ? 'Selecciona horario' : 'Día no laboral';
    if (previewHoursLabel) previewHoursLabel.textContent = isDayActive ? '' : 'Sin horario';
  }
};

/**
 * Modifica la habilitación del botón de guardar horario.
 * @param {boolean} isEnabled
 */
const setScheduleSaveButtonEnabledState = (isEnabled) => {
  const saveButton = getElementByIdSafe('modalSave');
  if (!saveButton) return;
  saveButton.disabled = !isEnabled;
  if (isEnabled) {
    saveButton.removeAttribute('aria-disabled');
    saveButton.removeAttribute('title');
  } else {
    saveButton.setAttribute('aria-disabled', 'true');
    saveButton.setAttribute('title', 'Debes cargar el horario real del servidor para poder guardar cambios.');
  }
};

// ===================================================================
// 6. GESTIÓN DE FORMULARIOS Y MODALES
// ===================================================================

/**
 * Abre el modal de edición de horario para un día de la semana.
 * @param {number} dayIndex
 */
const openScheduleEditorModal = (dayIndex) => {
  editingDayIndex = dayIndex;
  const dayData = profileState.horario[dayIndex];

  const modalDayIconSpan = getElementByIdSafe('modalDayIcon');
  const modalDayNameSpan = getElementByIdSafe('modalDayName');
  const modalDayActiveCheckbox = getElementByIdSafe('modalDayActive');
  const modalStartTimeInput = getElementByIdSafe('modalStartTime');
  const modalEndTimeInput = getElementByIdSafe('modalEndTime');

  if (modalDayIconSpan) modalDayIconSpan.textContent = dayData.day;
  if (modalDayNameSpan) modalDayNameSpan.textContent = dayData.dayFull;
  if (modalDayActiveCheckbox) {
    modalDayActiveCheckbox.checked = dayData.active;
    modalDayActiveCheckbox.setAttribute('aria-checked', String(dayData.active));
  }
  if (modalStartTimeInput) modalStartTimeInput.value = dayData.start || '08:00';
  if (modalEndTimeInput) modalEndTimeInput.value = dayData.end || '12:00';

  updateScheduleEditorModalUI(dayData.active);
  updateSchedulePreviewDisplay();

  const modalElement = getElementByIdSafe('scheduleModal');
  if (modalElement) {
    lastFocusedElement = document.activeElement;
    modalElement.classList.add('active');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');

    const firstInput = modalElement.querySelector('input, button');
    if (firstInput) firstInput.focus();

    document.body.style.overflow = 'hidden';
  }
};

/**
 * Cierra el modal de edición de horario y restaura el foco.
 */
const closeScheduleEditorModal = () => {
  const modalElement = getElementByIdSafe('scheduleModal');
  if (modalElement) {
    modalElement.classList.remove('active');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  editingDayIndex = null;
  if (lastFocusedElement instanceof HTMLElement) {
    lastFocusedElement.focus();
  }
  lastFocusedElement = null;
};

/**
 * Muestra u oculta el texto de una contraseña.
 * @param {string} inputId
 * @param {HTMLButtonElement} toggleButton
 */
const togglePasswordVisibilityState = (inputId, toggleButton) => {
  const passwordInput = getElementByIdSafe(inputId);
  if (!passwordInput) return;

  const isPasswordHidden = passwordInput.type === 'password';
  passwordInput.type = isPasswordHidden ? 'text' : 'password';
  toggleButton.textContent = isPasswordHidden ? '🙈' : '👁';
  toggleButton.setAttribute('aria-pressed', String(isPasswordHidden));
};

// ===================================================================
// 7. LISTENERS DE EVENTOS E INICIALIZACIÓN
// ===================================================================

/**
 * Inicializa el comportamiento responsive del sidebar en móviles.
 */
const initMobileSidebarMenu = () => {
  const hamburgerButton = getElementByIdSafe('hamburger');
  const sidebarElement = getElementByIdSafe('sidebar');
  const overlayElement = getElementByIdSafe('overlay');

  if (!hamburgerButton || !sidebarElement || !overlayElement) return;

  const toggleMenuState = (showMenu) => {
    sidebarElement.classList.toggle('open', showMenu);
    overlayElement.classList.toggle('open', showMenu);
    hamburgerButton.setAttribute('aria-expanded', showMenu);
    overlayElement.setAttribute('aria-hidden', !showMenu);

    if (showMenu) {
      const firstNavigationLink = sidebarElement.querySelector('.nav-item');
      if (firstNavigationLink) firstNavigationLink.focus();
    } else {
      hamburgerButton.focus();
    }
  };

  hamburgerButton.addEventListener('click', () => toggleMenuState(true));
  overlayElement.addEventListener('click', () => toggleMenuState(false));

  sidebarElement.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMenuState(false);
      }
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebarElement.classList.contains('open')) {
      e.preventDefault();
      toggleMenuState(false);
    }
  });
};

/**
 * Configura los event listeners del formulario de cambio de contraseña.
 */
const initPasswordChangeForm = () => {
  const passwordFormElement = getElementByIdSafe('passwordForm');
  const newPasswordInput = getElementByIdSafe('newPassword');

  document.querySelectorAll('.toggle-password').forEach(button => {
    button.addEventListener('click', () => {
      const targetInputId = button.closest('.input-with-icon')?.querySelector('.form-input')?.id;
      if (targetInputId) togglePasswordVisibilityState(targetInputId, button);
    });
  });

  if (newPasswordInput) {
    newPasswordInput.addEventListener('input', debounce(() => {
      const passwordText = newPasswordInput.value;
      const strengthIndicatorElement = getElementByIdSafe('passwordStrength');

      if (!strengthIndicatorElement) return;

      if (passwordText.length === 0) {
        strengthIndicatorElement.className = 'password-strength';
        strengthIndicatorElement.textContent = '';
        return;
      }

      const strengthLevel = evaluatePasswordStrengthLevel(passwordText);
      strengthIndicatorElement.className = `password-strength ${strengthLevel}`;

      const strengthLabelsMap = {
        weak: '🔴 Débil',
        medium: '🟡 Media',
        strong: '🟢 Fuerte'
      };
      strengthIndicatorElement.textContent = strengthLabelsMap[strengthLevel] || '';
    }, 150));
  }

  if (passwordFormElement) {
    passwordFormElement.addEventListener('submit', submitPasswordChangeApi);
  }
};

/**
 * Inicializa los eventos del modal de edición de horario.
 */
const initScheduleModalEvents = () => {
  const modalElement = getElementByIdSafe('scheduleModal');
  const closeIconButton = getElementByIdSafe('modalClose');
  const cancelButton = getElementByIdSafe('modalCancel');
  const saveButton = getElementByIdSafe('modalSave');
  const activeCheckbox = getElementByIdSafe('modalDayActive');
  const startTimeInput = getElementByIdSafe('modalStartTime');
  const endTimeInput = getElementByIdSafe('modalEndTime');

  if (closeIconButton) closeIconButton.addEventListener('click', closeScheduleEditorModal);
  if (cancelButton) cancelButton.addEventListener('click', closeScheduleEditorModal);
  if (modalElement) {
    modalElement.addEventListener('click', (e) => {
      if (e.target === e.currentTarget) closeScheduleEditorModal();
    });
  }

  if (saveButton) saveButton.addEventListener('click', saveScheduleChangesApi);

  if (activeCheckbox) {
    activeCheckbox.addEventListener('change', () => {
      activeCheckbox.setAttribute('aria-checked', String(activeCheckbox.checked));
      updateScheduleEditorModalUI(activeCheckbox.checked);
      updateSchedulePreviewDisplay();
    });
  }

  if (startTimeInput) {
    startTimeInput.addEventListener('change', updateSchedulePreviewDisplay);
    startTimeInput.addEventListener('input', updateSchedulePreviewDisplay);
  }
  if (endTimeInput) {
    endTimeInput.addEventListener('change', updateSchedulePreviewDisplay);
    endTimeInput.addEventListener('input', updateSchedulePreviewDisplay);
  }

  document.addEventListener('keydown', (e) => {
    if (!modalElement?.classList.contains('active')) return;
    if (e.key === 'Escape') {
      e.preventDefault();
      closeScheduleEditorModal();
      return;
    }
    if (e.key === 'Tab') {
      const focusableElements = [...modalElement.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])')]
        .filter(el => !el.disabled && el.offsetParent !== null);
      if (focusableElements.length === 0) return;
      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];

      if (e.shiftKey && document.activeElement === firstElement) {
        e.preventDefault();
        lastElement.focus();
      } else if (!e.shiftKey && document.activeElement === lastElement) {
        e.preventDefault();
        firstElement.focus();
      }
    }
  });
};

/**
 * Función principal de inicialización del módulo de perfil profesional.
 */
const init = async () => {
  initMobileSidebarMenu();
  initPasswordChangeForm();
  initScheduleModalEvents();

  setScheduleSaveButtonEnabledState(false);

  const fetchResult = await fetchProfessionalScheduleApi();
  profileState = { horario: fetchResult.horario };
  isScheduleLoadedFromApi = fetchResult.success === true;

  setScheduleSaveButtonEnabledState(isScheduleLoadedFromApi);
  renderWeeklyScheduleGrid();

  window.addEventListener('beforeunload', () => {});
};

document.addEventListener('DOMContentLoaded', init);