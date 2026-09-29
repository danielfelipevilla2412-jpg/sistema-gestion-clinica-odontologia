/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Agenda General de Citas (st-adm-08-agenda)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-adm-08-agenda/agendageneral.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra la interactividad del cuadrante de agenda médica general (semanal y diaria).
 * Permite filtrar por profesional/consultorio, navegar entre semanas, visualizar detalles de citas
 * y desplegar el modal interactivo para agendamiento o reagendamiento rápido.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtros combinados de profesional y consultorio en tiempo real.
 * - Validación cliente de rangos de hora acordes con el horario de apertura/cierre.
 * - Manejo de cola de notificaciones Toast sin solapamiento visual.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Stadm08Agenda
 * - HTML: Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml
 * ============================================ */

// ════════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ════════════════════════════════════════════════════════════════════

const API_BASE_URL = (window.APP_CONFIG?.ApiBase) || '/api';
const activeAnimationsSet = new Set();
const cleanupHandlersList = [];

// ════════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ════════════════════════════════════════════════════════════════════

let agendaEditRequestHandlerRef = null;
let agendaNewAppointmentHandlerRef = null;

// ════════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ════════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
    window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (callback, delay) =>
    window.CommonUtils?.debounce ? window.CommonUtils.debounce(callback, delay) : callback;

const trackedRequestAnimationFrame = (callback) => {
    let animationId;

    const wrapper = (timestamp) => {
        callback(timestamp);
        activeAnimationsSet.delete(animationId);
    };

    animationId = requestAnimationFrame(wrapper);
    activeAnimationsSet.add(animationId);

    return animationId;
};

const parseIsoDate = (dateString) => {
    const dateParts = dateString.split('-').map(part => parseInt(part, 10));
    return new Date(dateParts[0], dateParts[1] - 1, dateParts[2]);
};

const formatIsoDate = (dateObject) => dateObject.toISOString().slice(0, 10);

const getMondayOfWeek = (targetDate) => {
    const dateObj = new Date(targetDate);
    const dayOfWeek = dateObj.getDay();
    const mondayOffset = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
    dateObj.setDate(dateObj.getDate() + mondayOffset);
    dateObj.setHours(0, 0, 0, 0);
    return dateObj;
};

const calculateAppointmentEndTime = (startTime, durationMinutes = 60) => {
    if (!startTime) return '';
    const [hours, minutes] = startTime.split(':').map(Number);
    const totalMinutes = hours * 60 + minutes + durationMinutes;
    return `${String(Math.floor((totalMinutes % 1440) / 60)).padStart(2, '0')}:${String(totalMinutes % 60).padStart(2, '0')}`;
};

const getStatusCssClass = (statusString) => {
    const normalizedStatus = String(statusString ?? '').trim().toLowerCase();
    if (normalizedStatus === 'disponible') return 'available';
    if (normalizedStatus === 'pendiente') return 'reserved';

    return {
        programada: 'reserved',
        confirmada: 'confirmed',
        en_proceso: 'confirmed',
        atendida: 'attended',
        cancelada: 'cancelled',
        no_asistida: 'cancelled'
    }[CommonUtils.normalizeAppointmentStatus(statusString)] || 'reserved';
};

const getSelectedSelectOptionText = (selectElement) => {
    return selectElement.options[selectElement.selectedIndex]?.text || '';
};

const findCalendarDayContainer = (dateString) => {
    const dateId = `day-${dateString.replace(/-/g, '')}`;
    return document.getElementById(dateId)?.closest('.calendar-day');
};

// ════════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ════════════════════════════════════════════════════════════════════

const loadWeekData = async (weekStartIso, pushHistory = true) => {
    const calendarSection = document.querySelector('.calendar-section');
    const filterProfessional = safeGetElement('filterProfessional');
    const filterOffice = safeGetElement('filterOffice');

    calendarSection?.classList.add('is-loading');
    try {
        const searchParams = new URLSearchParams(window.location.search);
        searchParams.set('weekStart', weekStartIso);
        if (filterProfessional?.value) searchParams.set('professionalId', filterProfessional.value);
        else searchParams.set('professionalId', '0');
        if (filterOffice?.value) searchParams.set('officeId', filterOffice.value);
        else searchParams.delete('officeId');

        const requestUrl = `/gestion-de-citas/st-adm-08-agenda?${searchParams.toString()}`;
        const response = await fetch(requestUrl, { credentials: 'same-origin' });
        if (!response.ok) throw new Error('No se pudo cargar la semana');
        const responseHtml = await response.text();
        replaceCalendarSectionHtml(responseHtml);

        if (pushHistory) {
            const newUrl = `${window.location.pathname}?${searchParams.toString()}`;
            history.pushState({ weekStart: weekStartIso }, '', newUrl);
        }
    } catch (err) {
        console.error('[SmileTrack][Agenda] Error cargando semana:', err);
        if (window.ToastService) window.ToastService.error('❌ No fue posible cargar la semana seleccionada');
    } finally {
        calendarSection?.classList.remove('is-loading');
    }
};

const submitAppointmentData = async (formData, formElement) => {
    const abortController = new AbortController();
    const timeoutId = window.setTimeout(() => abortController.abort(), 15000);
    try {
        const tokenInput = formElement.querySelector('input[name="__RequestVerificationToken"]');
        const csrfToken = tokenInput?.value || '';

        const response = await fetch(`${API_BASE_URL}/citas/agenda`, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-CSRF-TOKEN': csrfToken
            },
            body: JSON.stringify(formData),
            signal: abortController.signal
        });

        if (!response.ok) {
            const errorBody = await response.json().catch(() => null);
            throw new Error(errorBody?.message || `El servidor respondió con ${response.status}.`);
        }

        return await response.json();
    } catch (error) {
        console.error('[SmileTrack][API] Error al crear cita:', error);
        if (error.name === 'AbortError') {
            throw new Error('La solicitud tardó demasiado. Verifica tu conexión e inténtalo de nuevo.');
        }
        throw error;
    } finally {
        window.clearTimeout(timeoutId);
    }
};

// ════════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ════════════════════════════════════════════════════════════════════

const replaceCalendarSectionHtml = (htmlText) => {
    try {
        const parser = new DOMParser();
        const parsedDoc = parser.parseFromString(htmlText, 'text/html');
        const newCalendarSection = parsedDoc.querySelector('.calendar-section');
        const newWeekLabel = parsedDoc.querySelector('#weekLabel');
        const currentWeekLabelEl = safeGetElement('weekLabel');

        if (newCalendarSection) {
            const existingSection = document.querySelector('.calendar-section');
            if (existingSection) existingSection.replaceWith(newCalendarSection.cloneNode(true));
            initializeNewAppointmentModal();
            initializeNativeAnimations();
        }
        if (newWeekLabel && currentWeekLabelEl) {
            currentWeekLabelEl.textContent = newWeekLabel.textContent || currentWeekLabelEl.textContent;
            const weekStartData = newWeekLabel.getAttribute('data-week-start');
            if (weekStartData) currentWeekLabelEl.dataset.weekStart = weekStartData;
            const durationData = newWeekLabel.getAttribute('data-duration-minutes');
            if (durationData) currentWeekLabelEl.dataset.durationMinutes = durationData;
        }
    } catch (err) {
        console.error('[SmileTrack][Agenda] Error reemplazando sección de calendario:', err);
    }
};

const initializeNativeAnimations = () => {
    document.querySelectorAll('.stat-number[data-target]:not([data-animated="1"])').forEach(element => {
        if (typeof window.animateCounter === 'function') {
            window.animateCounter(element, Number(element.dataset.target) || 0);
        }
    });

    trackedRequestAnimationFrame(() => {
        document.querySelectorAll('[data-width]').forEach(progressBar => {
            progressBar.style.width = `${progressBar.dataset.width}%`;
        });
    });
};

const createAppointmentCardElement = (appointmentData) => {
    const appointmentDiv = document.createElement('div');
    appointmentDiv.className = `appointment ${appointmentData.statusClass}`;
    appointmentDiv.setAttribute('tabindex', '0');
    appointmentDiv.setAttribute('role', 'button');

    Object.entries({
        id: appointmentData.id,
        date: appointmentData.date,
        startTime: appointmentData.startTime,
        endTime: appointmentData.endTime,
        status: appointmentData.status,
        patientId: appointmentData.patientId,
        professionalId: appointmentData.professionalId,
        officeId: appointmentData.officeId,
        serviceId: appointmentData.serviceId,
        notes: appointmentData.notes,
        patientName: appointmentData.patientName,
        professionalName: appointmentData.professionalName,
        officeName: appointmentData.officeName,
        serviceName: appointmentData.serviceName
    }).forEach(([key, value]) => {
        if (value !== undefined && value !== null) {
            appointmentDiv.dataset[key] = String(value);
        }
    });

    const ariaLabelText = `Cita ${appointmentData.status}: ${appointmentData.patientName}, ${appointmentData.startTime}, ${appointmentData.serviceName}, ${appointmentData.officeName}`;
    appointmentDiv.setAttribute('aria-label', ariaLabelText);

    const timeElement = document.createElement('time');
    timeElement.className = 'appt-time';
    timeElement.setAttribute('datetime', `${appointmentData.date}T${appointmentData.startTime}:00`);
    timeElement.textContent = appointmentData.startTime;

    const patientElement = document.createElement('span');
    patientElement.className = 'appt-patient';
    patientElement.textContent = appointmentData.patientName;

    const detailElement = document.createElement('span');
    detailElement.className = 'appt-detail';
    detailElement.textContent = `${appointmentData.serviceName} · ${appointmentData.officeName}`;

    appointmentDiv.appendChild(timeElement);
    appointmentDiv.appendChild(patientElement);
    appointmentDiv.appendChild(detailElement);

    return appointmentDiv;
};

const addAppointmentToCalendarView = (appointmentData) => {
    const dayContainer = findCalendarDayContainer(appointmentData.date);

    if (!dayContainer) {
        console.warn(`[SmileTrack][Agenda] No se encontró el contenedor del día: ${appointmentData.date}`);
        if (window.ToastService) window.ToastService.warning('⚠️ La fecha seleccionada no está en la vista actual del calendario');
        return false;
    }

    const noAppointmentsPlaceholder = dayContainer.querySelector('.appointment.available');
    if (noAppointmentsPlaceholder && noAppointmentsPlaceholder.querySelector('.appt-patient')?.textContent === 'Sin citas') {
        noAppointmentsPlaceholder.remove();
    }

    const appointmentElement = createAppointmentCardElement(appointmentData);
    const existingAppointments = Array.from(dayContainer.querySelectorAll('.appointment:not(.available)'));
    const insertPosition = existingAppointments.findIndex(existing => {
        const existingTime = existing.querySelector('.appt-time')?.textContent;
        return existingTime && existingTime > appointmentData.startTime;
    });

    if (insertPosition === -1) {
        dayContainer.appendChild(appointmentElement);
    } else {
        dayContainer.insertBefore(appointmentElement, existingAppointments[insertPosition]);
    }

    appointmentElement.style.opacity = '0';
    appointmentElement.style.transform = 'translateY(-10px)';
    setTimeout(() => {
        appointmentElement.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
        appointmentElement.style.opacity = '1';
        appointmentElement.style.transform = 'translateY(0)';
    }, 10);

    return true;
};

// ════════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ════════════════════════════════════════════════════════════════════

const initializeNewAppointmentModal = () => {
    const fabButton = safeGetElement('btnNewAppointment');
    const modalOverlay = safeGetElement('modalNewAppointment');
    const closeButton = safeGetElement('modalNewApptClose');
    const cancelButton = safeGetElement('modalNewApptCancel');
    const saveButton = safeGetElement('modalNewApptSave');
    const formElement = safeGetElement('formNewAppointment');

    if (!fabButton || !modalOverlay || !formElement) {
        console.warn('[SmileTrack][Agenda] No se pudieron encontrar elementos del modal de nueva cita');
        return;
    }

    if (formElement.dataset.initialized === 'true') return;
    formElement.dataset.initialized = 'true';
    let lastFocusedElement = null;

    const getConfiguredDuration = () => {
        const duration = Number(safeGetElement('weekLabel')?.dataset.durationMinutes);
        return Number.isFinite(duration) && duration > 0 ? duration : 60;
    };

    const syncEndTimeInputs = () => {
        const endInput = formElement.querySelector('#newApptEndTime');
        const startInput = formElement.querySelector('#newApptStartTime');
        if (endInput) endInput.value = calculateAppointmentEndTime(startInput?.value, getConfiguredDuration());
    };

    const openNewAppointmentModal = () => {
        const detailModal = safeGetElement('modalAppointment');
        if (detailModal?.classList.contains('open')) {
            detailModal.classList.remove('open');
            detailModal.setAttribute('aria-hidden', 'true');
            detailModal.setAttribute('inert', '');
        }
        lastFocusedElement = document.activeElement;
        modalOverlay.classList.add('open');
        modalOverlay.setAttribute('aria-hidden', 'false');
        modalOverlay.removeAttribute('inert');
        document.body.classList.add('modal-open');

        const todayIsoString = new Date().toISOString().split('T')[0];
        const dateInput = formElement.querySelector('#newApptDate');
        if (dateInput) {
            dateInput.setAttribute('min', todayIsoString);
            if (!dateInput.value) {
                dateInput.value = todayIsoString;
            }
        }
        syncEndTimeInputs();

        setTimeout(() => {
            const firstInput = formElement.querySelector('input, select');
            if (firstInput) firstInput.focus();
        }, 100);
    };

    const closeNewAppointmentModal = () => {
        modalOverlay.classList.remove('open');
        modalOverlay.setAttribute('aria-hidden', 'true');
        modalOverlay.setAttribute('inert', '');
        document.body.classList.remove('modal-open');
        formElement.reset();
        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        } else {
            fabButton.focus();
        }
        lastFocusedElement = null;
    };

    const handleEditAppointmentEvent = (event) => {
        const data = event.detail;
        if (!data) return;

        const valuesMap = {
            appointmentId: data.id,
            newApptDate: data.date,
            newApptStatus: data.status,
            newApptStartTime: data.startTime,
            newApptEndTime: data.endTime,
            newApptPatient: data.patientId,
            newApptProfessional: data.professionalId,
            newApptOffice: data.officeId,
            newApptService: data.serviceId,
            newApptNotes: data.notes
        };

        Object.entries(valuesMap).forEach(([id, value]) => {
            const inputField = formElement.querySelector(`#${id}`);
            if (inputField && value !== undefined && value !== null) inputField.value = value;
        });
        openNewAppointmentModal();
    };

    if (agendaEditRequestHandlerRef) {
        document.removeEventListener('smiletrack:edit-appointment', agendaEditRequestHandlerRef);
    }
    agendaEditRequestHandlerRef = handleEditAppointmentEvent;
    document.addEventListener('smiletrack:edit-appointment', agendaEditRequestHandlerRef);

    const handleNewAppointmentFromCalendarEvent = (event) => {
        const date = event.detail?.date;
        openNewAppointmentModal();
        const dateInput = formElement.querySelector('#newApptDate');
        if (dateInput && date) dateInput.value = date;
    };

    if (agendaNewAppointmentHandlerRef) {
        document.removeEventListener('smiletrack:new-appointment', agendaNewAppointmentHandlerRef);
    }
    agendaNewAppointmentHandlerRef = handleNewAppointmentFromCalendarEvent;
    document.addEventListener('smiletrack:new-appointment', agendaNewAppointmentHandlerRef);

    const handleNewAppointmentFormSubmit = async (event) => {
        event.preventDefault();

        if (saveButton.disabled) return;

        saveButton.disabled = true;
        const originalButtonText = saveButton.textContent;
        saveButton.textContent = 'Guardando...';

        try {
            const parseIntOrNull = (val) => {
                const parsed = parseInt(val, 10);
                return Number.isFinite(parsed) ? parsed : null;
            };

            const formDataPayload = {
                IdCita: parseIntOrNull(formElement.querySelector('#appointmentId')?.value || ''),
                Fecha: formElement.querySelector('#newApptDate').value,
                Estado: formElement.querySelector('#newApptStatus').value,
                HoraInicio: formElement.querySelector('#newApptStartTime').value,
                IdPaciente: parseIntOrNull(formElement.querySelector('#newApptPatient').value),
                IdProfesional: parseIntOrNull(formElement.querySelector('#newApptProfessional').value),
                IdConsultorio: parseIntOrNull(formElement.querySelector('#newApptOffice').value),
                IdServicio: parseIntOrNull(formElement.querySelector('#newApptService').value),
                Notas: formElement.querySelector('#newApptNotes').value
            };

            const derivedEndTime = calculateAppointmentEndTime(formDataPayload.HoraInicio, getConfiguredDuration());

            if (!formDataPayload.Fecha || !formDataPayload.HoraInicio || !formDataPayload.IdPaciente || !formDataPayload.IdProfesional || !formDataPayload.IdConsultorio || !formDataPayload.IdServicio) {
                if (window.ToastService) window.ToastService.warning('⚠️ Completa todos los campos obligatorios antes de guardar la cita.');
                saveButton.disabled = false;
                saveButton.textContent = originalButtonText;
                return;
            }

            if (window.AppointmentUtils && window.ValidationUtils) {
                formElement.querySelectorAll('input, select').forEach(input => window.ValidationUtils.clearError(input));

                const validationErrors = window.AppointmentUtils.validateAppointmentTime(formDataPayload.Fecha, formDataPayload.HoraInicio, derivedEndTime);
                if (validationErrors.length > 0) {
                    validationErrors.forEach(err => {
                        if (err.field === 'general') {
                            if (window.ToastService) window.ToastService.warning(`⚠️ ${err.message}`);
                        } else {
                            let inputId = '';
                            if (err.field === 'fecha') inputId = 'newApptDate';
                            if (err.field === 'horaInicio') inputId = 'newApptStartTime';
                            if (err.field === 'horaFin') inputId = 'newApptEndTime';
                            
                            const inputEl = formElement.querySelector(`#${inputId}`);
                            if (inputEl) {
                                window.ValidationUtils.showError(inputEl, null, err.message);
                            } else {
                                if (window.ToastService) window.ToastService.warning(`⚠️ ${err.message}`);
                            }
                        }
                    });
                    
                    if (!validationErrors.some(e => e.field === 'general')) {
                        if (window.ToastService) window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
                    }
                    saveButton.disabled = false;
                    saveButton.textContent = originalButtonText;
                    return;
                }
            }

            const appointmentCardData = {
                date: formDataPayload.Fecha,
                startTime: formDataPayload.HoraInicio,
                endTime: derivedEndTime,
                status: formDataPayload.Estado,
                statusClass: getStatusCssClass(formDataPayload.Estado),
                patientName: getSelectedSelectOptionText(formElement.querySelector('#newApptPatient')),
                professionalName: getSelectedSelectOptionText(formElement.querySelector('#newApptProfessional')),
                officeName: getSelectedSelectOptionText(formElement.querySelector('#newApptOffice')),
                serviceName: getSelectedSelectOptionText(formElement.querySelector('#newApptService')),
                notes: formDataPayload.Notas,
                patientId: formDataPayload.IdPaciente,
                professionalId: formDataPayload.IdProfesional,
                officeId: formDataPayload.IdConsultorio,
                serviceId: formDataPayload.IdServicio
            };

            const result = await submitAppointmentData(formDataPayload, formElement);

            if (!result.success) {
                throw new Error(result.message || 'El servidor no confirmó el guardado de la cita.');
            }

            const isUpdateOperation = Boolean(formDataPayload.IdCita);
            const addedToCalendar = !isUpdateOperation && addAppointmentToCalendarView(appointmentCardData);

            if (window.ToastService) {
                window.ToastService.success(
                    isUpdateOperation
                        ? '✅ Cita actualizada exitosamente'
                        : addedToCalendar
                            ? '✅ Cita creada exitosamente'
                            : '✅ Cita guardada. Cambia a la semana seleccionada para verla.'
                );
            }
            closeNewAppointmentModal();
            if (isUpdateOperation) {
                window.setTimeout(() => window.location.reload(), 250);
            }
        } catch (error) {
            console.error('[SmileTrack][Agenda] Error al guardar cita:', error);
            if (window.ToastService) window.ToastService.error(`❌ ${error.message || 'No fue posible guardar la cita. Intente nuevamente.'}`);
        } finally {
            saveButton.disabled = false;
            saveButton.textContent = originalButtonText;
        }
    };

    fabButton.addEventListener('click', openNewAppointmentModal);
    closeButton?.addEventListener('click', closeNewAppointmentModal);
    cancelButton?.addEventListener('click', closeNewAppointmentModal);
    formElement.addEventListener('submit', handleNewAppointmentFormSubmit);
    formElement.querySelector('#newApptStartTime')?.addEventListener('input', syncEndTimeInputs);
    formElement.querySelector('#newApptStartTime')?.addEventListener('change', syncEndTimeInputs);

    const handleOverlayClick = (event) => {
        if (event.target === modalOverlay) {
            closeNewAppointmentModal();
        }
    };
    modalOverlay.addEventListener('click', handleOverlayClick);

    const handleEscapeKey = (event) => {
        if (event.key === 'Escape' && modalOverlay.classList.contains('open')) {
            event.preventDefault();
            closeNewAppointmentModal();
            return;
        }
        if (event.key === 'Tab' && modalOverlay.classList.contains('open')) {
            const focusableElements = [...modalOverlay.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])')]
                .filter(element => !element.disabled && element.offsetParent !== null);
            if (focusableElements.length === 0) return;
            const firstFocusable = focusableElements[0];
            const lastFocusable = focusableElements[focusableElements.length - 1];
            if (event.shiftKey && document.activeElement === firstFocusable) {
                event.preventDefault();
                lastFocusable.focus();
            } else if (!event.shiftKey && document.activeElement === lastFocusable) {
                event.preventDefault();
                firstFocusable.focus();
            }
        }
    };
    document.addEventListener('keydown', handleEscapeKey);

    cleanupHandlersList.push(() => {
        fabButton.removeEventListener('click', openNewAppointmentModal);
        closeButton?.removeEventListener('click', closeNewAppointmentModal);
        cancelButton?.removeEventListener('click', closeNewAppointmentModal);
        formElement.removeEventListener('submit', handleNewAppointmentFormSubmit);
        formElement.querySelector('#newApptStartTime')?.removeEventListener('input', syncEndTimeInputs);
        formElement.querySelector('#newApptStartTime')?.removeEventListener('change', syncEndTimeInputs);
        modalOverlay.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleEscapeKey);
        document.removeEventListener('smiletrack:edit-appointment', handleEditAppointmentEvent);
        document.removeEventListener('smiletrack:new-appointment', handleNewAppointmentFromCalendarEvent);
    });
};

const initializeAppointmentDetailModal = () => {
    const modalElement = safeGetElement('modalAppointment');
    const contentElement = safeGetElement('modalApptContent');
    const closeButton = safeGetElement('modalApptClose');
    const cancelButton = safeGetElement('modalApptCancel');
    const editButton = safeGetElement('modalApptEdit');
    if (!modalElement || !contentElement || !closeButton || !cancelButton || !editButton) return;
    if (modalElement.dataset.initialized === 'true') return;
    modalElement.dataset.initialized = 'true';

    let selectedAppointmentData = null;
    let lastFocusedElement = null;

    const closeDetailModal = () => {
        modalElement.classList.remove('open');
        modalElement.setAttribute('aria-hidden', 'true');
        modalElement.setAttribute('inert', '');
        document.body.classList.remove('modal-open');
        if (lastFocusedElement instanceof HTMLElement) lastFocusedElement.focus();
        lastFocusedElement = null;
        selectedAppointmentData = null;
    };

    const openDetailModal = (targetElement) => {
        selectedAppointmentData = {
            id: targetElement.dataset.id,
            date: targetElement.dataset.date,
            startTime: targetElement.dataset.startTime,
            endTime: targetElement.dataset.endTime,
            status: targetElement.dataset.status,
            patientId: targetElement.dataset.patientId,
            professionalId: targetElement.dataset.professionalId,
            officeId: targetElement.dataset.officeId,
            serviceId: targetElement.dataset.serviceId,
            notes: targetElement.dataset.notes || '',
            patientName: targetElement.dataset.patientName || 'Sin paciente',
            professionalName: targetElement.dataset.professionalName || 'Sin profesional',
            professionalEmail: targetElement.dataset.professionalEmail || '',
            professionalPhone: targetElement.dataset.professionalPhone || '',
            professionalRegistry: targetElement.dataset.professionalRegistry || '',
            professionalUserStatus: targetElement.dataset.professionalUserStatus || '',
            officeName: targetElement.dataset.officeName || 'Sin consultorio',
            serviceName: targetElement.dataset.serviceName || 'Sin servicio'
        };
        contentElement.innerHTML = '';
        const detailRows = [
            ['Paciente', selectedAppointmentData.patientName],
            ['Profesional', selectedAppointmentData.professionalName]
        ];
        if (selectedAppointmentData.professionalEmail)
            detailRows.push(['Correo profesional', selectedAppointmentData.professionalEmail]);
        if (selectedAppointmentData.professionalPhone)
            detailRows.push(['Teléfono profesional', selectedAppointmentData.professionalPhone]);
        if (selectedAppointmentData.professionalRegistry)
            detailRows.push(['Registro médico', selectedAppointmentData.professionalRegistry]);
        if (selectedAppointmentData.professionalUserStatus)
            detailRows.push(['Estado cuenta', selectedAppointmentData.professionalUserStatus]);
        detailRows.push(
            ['Fecha', selectedAppointmentData.date],
            ['Horario', `${selectedAppointmentData.startTime} - ${selectedAppointmentData.endTime}`],
            ['Servicio', selectedAppointmentData.serviceName],
            ['Consultorio', selectedAppointmentData.officeName],
            ['Estado', selectedAppointmentData.status],
            ['Observaciones', selectedAppointmentData.notes || 'Sin observaciones']
        );
        detailRows.forEach(([label, value]) => {
            const rowParagraph = document.createElement('p');
            const strongTag = document.createElement('strong');
            strongTag.textContent = `${label}: `;
            rowParagraph.append(strongTag, document.createTextNode(value));
            contentElement.appendChild(rowParagraph);
        });
        lastFocusedElement = document.activeElement;
        modalElement.classList.add('open');
        modalElement.setAttribute('aria-hidden', 'false');
        modalElement.removeAttribute('inert');
        document.body.classList.add('modal-open');
        closeButton.focus();
    };

    const handleCalendarClick = (event) => {
        const appointmentCard = event.target.closest('.appointment:not(.available)');
        if (appointmentCard) {
            openDetailModal(appointmentCard);
            return;
        }
        const availableSlot = event.target.closest('.appointment.available');
        if (availableSlot) {
            document.dispatchEvent(new CustomEvent('smiletrack:new-appointment', {
                detail: { date: availableSlot.dataset.date }
            }));
        }
    };

    const handleCalendarKeydown = (event) => {
        const appointmentCard = event.target.closest('.appointment:not(.available)');
        const availableSlot = event.target.closest('.appointment.available');
        if ((event.key === 'Enter' || event.key === ' ') && appointmentCard) {
            event.preventDefault();
            openDetailModal(appointmentCard);
        } else if ((event.key === 'Enter' || event.key === ' ') && availableSlot) {
            event.preventDefault();
            document.dispatchEvent(new CustomEvent('smiletrack:new-appointment', {
                detail: { date: availableSlot.dataset.date }
            }));
        }
    };

    const handleEditButtonClick = () => {
        if (!selectedAppointmentData) return;
        const appointmentToEdit = selectedAppointmentData;
        closeDetailModal();
        document.dispatchEvent(new CustomEvent('smiletrack:edit-appointment', { detail: appointmentToEdit }));
    };

    const handleEscapeKey = (event) => {
        if (!modalElement.classList.contains('open')) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            closeDetailModal();
            return;
        }
        if (event.key === 'Tab') {
            const focusableElements = [...modalElement.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])')]
                .filter(element => !element.disabled && element.offsetParent !== null);
            if (focusableElements.length === 0) return;
            const firstFocusable = focusableElements[0];
            const lastFocusable = focusableElements[focusableElements.length - 1];
            if (event.shiftKey && document.activeElement === firstFocusable) {
                event.preventDefault();
                lastFocusable.focus();
            } else if (!event.shiftKey && document.activeElement === lastFocusable) {
                event.preventDefault();
                firstFocusable.focus();
            }
        }
    };

    document.addEventListener('click', handleCalendarClick);
    document.addEventListener('keydown', handleCalendarKeydown);
    closeButton.addEventListener('click', closeDetailModal);
    cancelButton.addEventListener('click', closeDetailModal);
    editButton.addEventListener('click', handleEditButtonClick);
    const handleOverlayClick = (event) => {
        if (event.target === modalElement) closeDetailModal();
    };
    modalElement.addEventListener('click', handleOverlayClick);
    document.addEventListener('keydown', handleEscapeKey);

    cleanupHandlersList.push(() => {
        document.removeEventListener('click', handleCalendarClick);
        document.removeEventListener('keydown', handleCalendarKeydown);
        closeButton.removeEventListener('click', closeDetailModal);
        cancelButton.removeEventListener('click', closeDetailModal);
        editButton.removeEventListener('click', handleEditButtonClick);
        modalElement.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleEscapeKey);
        document.body.classList.remove('modal-open');
    });
};

// ════════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ════════════════════════════════════════════════════════════════════

const setupSidebarNavigation = () => {
    const hamburgerButton = safeGetElement('hamburger');
    const sidebarElement = safeGetElement('sidebar');
    const overlayElement = safeGetElement('overlay');

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

    const handleHamburgerClick = () => toggleMenuState(true);
    const handleOverlayClick = () => toggleMenuState(false);

    const handleKeyDown = (event) => {
        if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
            event.preventDefault();
            toggleMenuState(false);
        }
    };

    hamburgerButton.addEventListener('click', handleHamburgerClick);
    overlayElement.addEventListener('click', handleOverlayClick);
    document.addEventListener('keydown', handleKeyDown);

    cleanupHandlersList.push(() => {
        hamburgerButton.removeEventListener('click', handleHamburgerClick);
        overlayElement.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleKeyDown);
    });

    const navigationItems = sidebarElement.querySelectorAll('.nav-item');
    const handleNavigationClick = () => {
        if (window.innerWidth <= 680) {
            toggleMenuState(false);
        }
    };

    navigationItems.forEach(item => {
        item.addEventListener('click', handleNavigationClick);
    });

    cleanupHandlersList.push(() => {
        navigationItems.forEach(item => {
            item.removeEventListener('click', handleNavigationClick);
        });
    });
};

const setupWeekNavigationListeners = () => {
    const weekLabelEl = safeGetElement('weekLabel');
    const btnPrev = safeGetElement('btnPrev');
    const btnNext = safeGetElement('btnNext');
    const btnToday = safeGetElement('btnToday');
    const filterProfessional = safeGetElement('filterProfessional');
    const filterOffice = safeGetElement('filterOffice');

    if (!weekLabelEl || !btnPrev || !btnNext || !btnToday) return;

    const getCurrentWeekMonday = () => {
        const datasetStart = weekLabelEl.dataset.weekStart;
        if (!datasetStart) return getMondayOfWeek(new Date());
        try { return getMondayOfWeek(parseIsoDate(datasetStart)); } catch { return getMondayOfWeek(new Date()); }
    };

    const handlePrevWeekClick = () => {
        const mondayDate = getCurrentWeekMonday();
        mondayDate.setDate(mondayDate.getDate() - 7);
        loadWeekData(formatIsoDate(mondayDate));
    };

    const handleNextWeekClick = () => {
        const mondayDate = getCurrentWeekMonday();
        mondayDate.setDate(mondayDate.getDate() + 7);
        loadWeekData(formatIsoDate(mondayDate));
    };

    const handleTodayClick = () => {
        const mondayDate = getMondayOfWeek(new Date());
        loadWeekData(formatIsoDate(mondayDate));
    };

    const handlePopstateEvent = (event) => {
        const stateWeek = (event.state && event.state.weekStart) || (new URLSearchParams(window.location.search)).get('weekStart');
        const searchParams = new URLSearchParams(window.location.search);
        if (filterProfessional) filterProfessional.value = searchParams.get('professionalId') || '';
        if (filterOffice) filterOffice.value = searchParams.get('officeId') || '';
        if (stateWeek) loadWeekData(stateWeek, false);
    };

    const handleProfessionalFilterChange = () => loadWeekData(formatIsoDate(getCurrentWeekMonday()));
    const handleOfficeFilterChange = () => loadWeekData(formatIsoDate(getCurrentWeekMonday()));

    btnPrev.addEventListener('click', handlePrevWeekClick);
    btnNext.addEventListener('click', handleNextWeekClick);
    btnToday.addEventListener('click', handleTodayClick);
    filterProfessional?.addEventListener('change', handleProfessionalFilterChange);
    filterOffice?.addEventListener('change', handleOfficeFilterChange);
    window.addEventListener('popstate', handlePopstateEvent);

    cleanupHandlersList.push(() => {
        btnPrev.removeEventListener('click', handlePrevWeekClick);
        btnNext.removeEventListener('click', handleNextWeekClick);
        btnToday.removeEventListener('click', handleTodayClick);
        filterProfessional?.removeEventListener('change', handleProfessionalFilterChange);
        filterOffice?.removeEventListener('change', handleOfficeFilterChange);
        window.removeEventListener('popstate', handlePopstateEvent);
    });
};

const initializeAgendaGeneralModule = async () => {
    try {
        setupSidebarNavigation();
        setupWeekNavigationListeners();
        initializeNativeAnimations();
        initializeNewAppointmentModal();
        initializeAppointmentDetailModal();

        setTimeout(() => {
            if (window.ToastService) window.ToastService.success('✅ Panel administrativo cargado');
        }, 500);

    } catch (error) {
        console.error('[SmileTrack][Init] Falla crítica durante la inicialización:', error);
    }

    window.addEventListener('beforeunload', () => {
        activeAnimationsSet.forEach(animationId => {
            cancelAnimationFrame(animationId);
        });
        activeAnimationsSet.clear();

        cleanupHandlersList.forEach(cleanupFunction => {
            cleanupFunction();
        });
    });
};

document.addEventListener('DOMContentLoaded', initializeAgendaGeneralModule);