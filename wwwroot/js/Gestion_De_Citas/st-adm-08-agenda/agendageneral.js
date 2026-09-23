/**
 * ============================================
 * SmileTrack — Agenda General (agendageneral.js)
 * ============================================
 * Autor: Johan Santamaria
 * Fecha: 29/07/2026
 *
 * PROPÓSITO:
 * Gestiona la interactividad de la agenda general:
 * - Navegación del sidebar responsive
 * - Sistema de notificaciones toast con cola
 * - Modales para crear y visualizar citas
 * - Filtros dinámicos por profesional y consultorio
 *
 * DECISIONES TÉCNICAS:
 * - Cola de toasts: evita solapamiento de notificaciones
 * - trackedRAF: cleanup de animaciones para prevenir memory leaks
 * - Debounce con maxWait: balance entre responsividad y performance
 * - Fallbacks progresivos: funcionalidad básica si JS falla parcialmente
 *
 * NOTAS DE MANTENIMIENTO:
 * - API_BASE se lee de window.APP_CONFIG para facilitar testing
 * - Comentarios explican el "por qué" de las decisiones de diseño
 * - cleanupHandlers previene memory leaks al navegar
 * ============================================
 */

// ════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN GLOBAL
// ════════════════════════════════════════════════════════════════════

// Leer configuración de API desde window.APP_CONFIG permite cambiar
// la base de API sin recompilar el JavaScript
const API_BASE = (window.APP_CONFIG?.ApiBase) || '/api';

// Set para rastrear animaciones activas y poder cancelarlas
const activeAnimations = new Set();

// Cola de notificaciones para evitar solapamiento visual

// Array de funciones de limpieza para remover event listeners
const cleanupHandlers = [];
let agendaEditRequestHandler = null;
let agendaNewAppointmentHandler = null;

// ════════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ════════════════════════════════════════════════════════════════════

/**
 * Obtiene un elemento del DOM de forma segura.
 * Retorna null y muestra advertencia si no existe.
 */
const safeGetElement = (elementId) => {
    const element = document.getElementById(elementId);
    if (!element) {
        console.warn(`[SmileTrack][UI] Elemento no encontrado: #${elementId}`);
    }
    return element;
};

/**
 * Debounce con soporte de tiempo máximo de espera.
 * Balance entre responsividad y rendimiento.
 */
const debounce = (callback, delay, maxWait = null) => {
    let timeoutId;
    let lastInvokeTime = 0;

    return (...args) => {
        const currentTime = Date.now();
        clearTimeout(timeoutId);

        // Si se excede el tiempo máximo, ejecutar inmediatamente
        if (maxWait && lastInvokeTime && (currentTime - lastInvokeTime >= maxWait)) {
            lastInvokeTime = currentTime;
            callback.apply(this, args);
        } else {
            if (!lastInvokeTime) {
                lastInvokeTime = currentTime;
            }
            timeoutId = setTimeout(() => {
                lastInvokeTime = 0;
                callback.apply(this, args);
            }, delay);
        }
    };
};

/**
 * Muestra una notificación toast con cola para evitar solapamiento.
 */

/**
 * Ejecuta una función en el próximo frame de animación y la rastrea.
 * Permite cleanup en beforeunload para prevenir memory leaks.
 */
const trackedRAF = (callback) => {
    let animationId;

    const wrapper = (timestamp) => {
        callback(timestamp);
        activeAnimations.delete(animationId);
    };

    animationId = requestAnimationFrame(wrapper);
    activeAnimations.add(animationId);

    return animationId;
};

// ════════════════════════════════════════════════════════════════════
//  FUNCIONES DE ANIMACIÓN  (ahora usa window.animateCounter global)
// ════════════════════════════════════════════════════════════════════

/**
 * Inicializa todas las animaciones nativas del dashboard.
 */
const initNativeAnimations = () => {
    // Animar contadores numéricos
    document.querySelectorAll('.stat-number[data-target]:not([data-animated="1"])').forEach(element => {
        window.animateCounter(element, Number(element.dataset.target) || 0);
    });

    // Animar barras de progreso usando requestAnimationFrame rastreado
    trackedRAF(() => {
        document.querySelectorAll('[data-width]').forEach(progressBar => {
            progressBar.style.width = `${progressBar.dataset.width}%`;
        });
    });
};

// ════════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ════════════════════════════════════════════════════════════════════

/**
 * Inicializa el menú hamburguesa y sidebar responsive.
 */
const initSidebar = () => {
    const hamburgerButton = safeGetElement('hamburger');
    const sidebarElement = safeGetElement('sidebar');
    const overlayElement = safeGetElement('overlay');

    if (!hamburgerButton || !sidebarElement || !overlayElement) return;

    const toggleMenu = (showMenu) => {
        sidebarElement.classList.toggle('open', showMenu);
        overlayElement.classList.toggle('open', showMenu);
        hamburgerButton.setAttribute('aria-expanded', showMenu);
        overlayElement.setAttribute('aria-hidden', !showMenu);

        // Gestionar foco para accesibilidad
        if (showMenu) {
            const firstNavigationLink = sidebarElement.querySelector('.nav-item');
            if (firstNavigationLink) firstNavigationLink.focus();
        } else {
            hamburgerButton.focus();
        }
    };

    const handleHamburgerClick = () => toggleMenu(true);
    const handleOverlayClick = () => toggleMenu(false);

    const handleKeyDown = (event) => {
        if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
            event.preventDefault();
            toggleMenu(false);
        }
    };

    // Registrar event listeners
    hamburgerButton.addEventListener('click', handleHamburgerClick);
    overlayElement.addEventListener('click', handleOverlayClick);
    document.addEventListener('keydown', handleKeyDown);

    // Funciones de limpieza para prevenir memory leaks
    cleanupHandlers.push(() => {
        hamburgerButton.removeEventListener('click', handleHamburgerClick);
        overlayElement.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleKeyDown);
    });

    // Cerrar menú automáticamente en móvil al hacer clic en un enlace
    const navigationItems = sidebarElement.querySelectorAll('.nav-item');
    const handleNavigationClick = () => {
        if (window.innerWidth <= 680) {
            toggleMenu(false);
        }
    };

    navigationItems.forEach(item => {
        item.addEventListener('click', handleNavigationClick);
    });

    cleanupHandlers.push(() => {
        navigationItems.forEach(item => {
            item.removeEventListener('click', handleNavigationClick);
        });
    });
};

// ════════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ════════════════════════════════════════════════════════════════════

/**
 * Inicializa la navegación entre semanas (prev/next/today).
 * Carga el contenido de la semana de forma asíncrona y actualiza el historial.
 */
const initWeekNavigation = () => {
    const weekLabelEl = safeGetElement('weekLabel');
    const btnPrev = safeGetElement('btnPrev');
    const btnNext = safeGetElement('btnNext');
    const btnToday = safeGetElement('btnToday');
    const filterProfessional = safeGetElement('filterProfessional');
    const filterOffice = safeGetElement('filterOffice');

    if (!weekLabelEl || !btnPrev || !btnNext || !btnToday) return;

    const parseIso = (s) => {
        const parts = s.split('-').map(p => parseInt(p, 10));
        return new Date(parts[0], parts[1] - 1, parts[2]);
    };

    const toIso = (d) => d.toISOString().slice(0, 10);

    const getMonday = (date) => {
        const d = new Date(date);
        const day = d.getDay();
        const diff = (day === 0 ? -6 : 1) - day; // Monday offset
        d.setDate(d.getDate() + diff);
        d.setHours(0,0,0,0);
        return d;
    };

    const currentWeekStart = () => {
        const ds = weekLabelEl.dataset.weekStart;
        if (!ds) return getMonday(new Date());
        try { return getMonday(parseIso(ds)); } catch { return getMonday(new Date()); }
    };

    const replaceCalendarSection = (htmlText) => {
        try {
            const parser = new DOMParser();
            const doc = parser.parseFromString(htmlText, 'text/html');
            const newSection = doc.querySelector('.calendar-section');
            const newWeekLabel = doc.querySelector('#weekLabel');
            if (newSection) {
                const existing = document.querySelector('.calendar-section');
                if (existing) existing.replaceWith(newSection.cloneNode(true));
                // rebind modal handlers: reinitialize the new appointment modal listeners
                initNewAppointmentModal();
                initNativeAnimations();
            }
            if (newWeekLabel && weekLabelEl) {
                weekLabelEl.textContent = newWeekLabel.textContent || weekLabelEl.textContent;
                const ds = newWeekLabel.getAttribute('data-week-start');
                if (ds) weekLabelEl.dataset.weekStart = ds;
                const duration = newWeekLabel.getAttribute('data-duration-minutes');
                if (duration) weekLabelEl.dataset.durationMinutes = duration;
            }
        } catch (err) {
            console.error('[SmileTrack][Agenda] Error reemplazando sección de calendario:', err);
        }
    };

    const loadWeek = async (weekStartIso, push = true) => {
        const calendarSection = document.querySelector('.calendar-section');
        calendarSection?.classList.add('is-loading');
        try {
            const search = new URLSearchParams(window.location.search);
            search.set('weekStart', weekStartIso);
            if (filterProfessional?.value) search.set('professionalId', filterProfessional.value);
            else search.set('professionalId', '0');
            if (filterOffice?.value) search.set('officeId', filterOffice.value);
            else search.delete('officeId');
            const url = `/gestion-de-citas/st-adm-08-agenda?${search.toString()}`;
            const res = await fetch(url, { credentials: 'same-origin' });
            if (!res.ok) throw new Error('No se pudo cargar la semana');
            const txt = await res.text();
            replaceCalendarSection(txt);

            if (push) {
                const newUrl = `${window.location.pathname}?${search.toString()}`;
                history.pushState({ weekStart: weekStartIso }, '', newUrl);
            }
        } catch (err) {
            console.error('[SmileTrack][Agenda] Error cargando semana:', err);
            window.ToastService.error('❌ No fue posible cargar la semana seleccionada');
        } finally {
            calendarSection?.classList.remove('is-loading');
        }
    };

    // FASE-0 E-MEM-01: Los handlers se declaran con nombre para que removeEventListener
    // reciba LA MISMA referencia de función. Antes se usaban arrow functions nuevas
    // en removeEventListener que nunca hacían match → memory leak real.
    const handlePrevWeek = () => {
        const monday = currentWeekStart();
        monday.setDate(monday.getDate() - 7);
        loadWeek(toIso(monday));
    };

    const handleNextWeek = () => {
        const monday = currentWeekStart();
        monday.setDate(monday.getDate() + 7);
        loadWeek(toIso(monday));
    };

    const handleToday = () => {
        const monday = getMonday(new Date());
        loadWeek(toIso(monday));
    };

    const handlePopstate = (e) => {
        const stateWeek = (e.state && e.state.weekStart) || (new URLSearchParams(window.location.search)).get('weekStart');
        const search = new URLSearchParams(window.location.search);
        if (filterProfessional) filterProfessional.value = search.get('professionalId') || '';
        if (filterOffice) filterOffice.value = search.get('officeId') || '';
        if (stateWeek) loadWeek(stateWeek, false);
    };

    const handleProfessionalFilter = () => loadWeek(toIso(currentWeekStart()));
    const handleOfficeFilter = () => loadWeek(toIso(currentWeekStart()));

    btnPrev.addEventListener('click', handlePrevWeek);
    btnNext.addEventListener('click', handleNextWeek);
    btnToday.addEventListener('click', handleToday);
    filterProfessional?.addEventListener('change', handleProfessionalFilter);
    filterOffice?.addEventListener('change', handleOfficeFilter);
    window.addEventListener('popstate', handlePopstate);

    cleanupHandlers.push(() => {
        btnPrev.removeEventListener('click', handlePrevWeek);
        btnNext.removeEventListener('click', handleNextWeek);
        btnToday.removeEventListener('click', handleToday);
        filterProfessional?.removeEventListener('change', handleProfessionalFilter);
        filterOffice?.removeEventListener('change', handleOfficeFilter);
        window.removeEventListener('popstate', handlePopstate);
    });
};

/**
 * Punto de entrada principal de la aplicación.
 * Inicializa todos los componentes y configura limpieza al cerrar.
 */
const init = async () => {
    try {
        initSidebar();
        // Navigation between weeks: improves UX by loading week content via fetch
        initWeekNavigation();
        initNativeAnimations();
        initNewAppointmentModal();
        initAppointmentDetailModal();

        setTimeout(() => {
            window.ToastService.success('✅ Panel administrativo cargado');
        }, 500);

    } catch (error) {
        console.error('[SmileTrack][Init] Falla crítica durante la inicialización:', error);
    }

    window.addEventListener('beforeunload', () => {
        activeAnimations.forEach(animationId => {
            cancelAnimationFrame(animationId);
        });
        activeAnimations.clear();

        cleanupHandlers.forEach(cleanupFunction => {
            cleanupFunction();
        });
    });
};

// Inicializar cuando el DOM esté completamente cargado
document.addEventListener('DOMContentLoaded', init);
// ════════════════════════════════════════════════════════════════════
//  MODAL DE NUEVA CITA
// ════════════════════════════════════════════════════════════════════

/**
 * Inicializa el modal de crear nueva cita y su lógica de guardado.
 */
const initNewAppointmentModal = () => {
    const fabButton = safeGetElement('btnNewAppointment');
    const modalOverlay = safeGetElement('modalNewAppointment');
    const closeButton = safeGetElement('modalNewApptClose');
    const cancelButton = safeGetElement('modalNewApptCancel');
    const saveButton = safeGetElement('modalNewApptSave');
    const form = safeGetElement('formNewAppointment');

    if (!fabButton || !modalOverlay || !form) {
        console.warn('[SmileTrack][Agenda] No se pudieron encontrar elementos del modal de nueva cita');
        return;
    }

    if (form.dataset.initialized === 'true') return;
    form.dataset.initialized = 'true';
    let lastFocusedElement = null;

    const getConfiguredDuration = () => {
        const duration = Number(safeGetElement('weekLabel')?.dataset.durationMinutes);
        return Number.isFinite(duration) && duration > 0 ? duration : 60;
    };

    const calculateEndTime = (startTime) => {
        if (!startTime) return '';
        const [hours, minutes] = startTime.split(':').map(Number);
        const totalMinutes = hours * 60 + minutes + getConfiguredDuration();
        return `${String(Math.floor((totalMinutes % 1440) / 60)).padStart(2, '0')}:${String(totalMinutes % 60).padStart(2, '0')}`;
    };

    const syncEndTime = () => {
        const endInput = form.querySelector('#newApptEndTime');
        const startInput = form.querySelector('#newApptStartTime');
        if (endInput) endInput.value = calculateEndTime(startInput?.value);
    };

    /**
     * Abre el modal de nueva cita
     */
    const openModal = () => {
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
        
        // Establecer fecha mínima como hoy
        const today = new Date().toISOString().split('T')[0];
        const dateInput = form.querySelector('#newApptDate');
        if (dateInput) {
            dateInput.setAttribute('min', today);
            if (!dateInput.value) {
                dateInput.value = today;
            }
        }
        syncEndTime();

        // Focus en el primer campo
        setTimeout(() => {
            const firstInput = form.querySelector('input, select');
            if (firstInput) firstInput.focus();
        }, 100);
    };

    /**
     * Cierra el modal de nueva cita
     */
    const closeModal = () => {
        modalOverlay.classList.remove('open');
        modalOverlay.setAttribute('aria-hidden', 'true');
        modalOverlay.setAttribute('inert', '');
        document.body.classList.remove('modal-open');
        form.reset();
        if (lastFocusedElement instanceof HTMLElement) {
            lastFocusedElement.focus();
        } else {
            fabButton.focus();
        }
        lastFocusedElement = null;
    };

    const editAppointment = (event) => {
        const data = event.detail;
        if (!data) return;

        const values = {
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

        Object.entries(values).forEach(([id, value]) => {
            const input = form.querySelector(`#${id}`);
            if (input && value !== undefined && value !== null) input.value = value;
        });
        openModal();
    };

    if (agendaEditRequestHandler) {
        document.removeEventListener('smiletrack:edit-appointment', agendaEditRequestHandler);
    }
    agendaEditRequestHandler = editAppointment;
    document.addEventListener('smiletrack:edit-appointment', agendaEditRequestHandler);

    const newAppointmentFromCalendar = (event) => {
        const date = event.detail?.date;
        openModal();
        const dateInput = form.querySelector('#newApptDate');
        if (dateInput && date) dateInput.value = date;
    };
    if (agendaNewAppointmentHandler) {
        document.removeEventListener('smiletrack:new-appointment', agendaNewAppointmentHandler);
    }
    agendaNewAppointmentHandler = newAppointmentFromCalendar;
    document.addEventListener('smiletrack:new-appointment', agendaNewAppointmentHandler);

    /**
     * Mapea el estado de la cita a la clase CSS correspondiente
     */
    const getStatusClass = (status) => {
        const normalized = String(status ?? '').trim().toLowerCase();
        if (normalized === 'disponible') return 'available';
        if (normalized === 'pendiente') return 'reserved';

        return {
            programada: 'reserved',
            confirmada: 'confirmed',
            en_proceso: 'confirmed',
            atendida: 'attended',
            cancelada: 'cancelled',
            no_asistida: 'cancelled'
        }[CommonUtils.normalizeAppointmentStatus(status)] || 'reserved';
    };

    /**
     * Encuentra el contenedor del día en el calendario basado en la fecha
     */
    const findDayContainer = (dateString) => {
        // Convertir formato YYYY-MM-DD a YYYYMMDD para el ID
        const dateId = `day-${dateString.replace(/-/g, '')}`;
        return document.getElementById(dateId)?.closest('.calendar-day');
    };

    /**
     * Crea el elemento DOM para la nueva cita
     */
    const createAppointmentElement = (appointmentData) => {
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
        
        const ariaLabel = `Cita ${appointmentData.status}: ${appointmentData.patientName}, ${appointmentData.startTime}, ${appointmentData.serviceName}, ${appointmentData.officeName}`;
        appointmentDiv.setAttribute('aria-label', ariaLabel);

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

    /**
     * Agrega la cita al calendario en el día correspondiente
     */
    const addAppointmentToCalendar = (appointmentData) => {
        const dayContainer = findDayContainer(appointmentData.date);
        
        if (!dayContainer) {
            console.warn(`[SmileTrack][Agenda] No se encontró el contenedor del día: ${appointmentData.date}`);
            window.ToastService.warning('⚠️ La fecha seleccionada no está en la vista actual del calendario');
            return false;
        }

        // Verificar si hay un mensaje de "Sin citas" y removerlo
        const noAppointmentsMessage = dayContainer.querySelector('.appointment.available');
        if (noAppointmentsMessage && noAppointmentsMessage.querySelector('.appt-patient')?.textContent === 'Sin citas') {
            noAppointmentsMessage.remove();
        }

        // Crear y agregar el elemento de la cita
        const appointmentElement = createAppointmentElement(appointmentData);
        
        // Insertar en orden cronológico
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

        // Animación de entrada
        appointmentElement.style.opacity = '0';
        appointmentElement.style.transform = 'translateY(-10px)';
        setTimeout(() => {
            appointmentElement.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            appointmentElement.style.opacity = '1';
            appointmentElement.style.transform = 'translateY(0)';
        }, 10);

        return true;
    };

    /**
     * Obtiene el texto seleccionado de un elemento select
     */
    const getSelectedText = (selectElement) => {
        return selectElement.options[selectElement.selectedIndex]?.text || '';
    };

    /**
     * Envía los datos del formulario al servidor
     */
    const submitAppointment = async (formData) => {
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), 15000);
        try {
            const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenInput?.value || '';

            const response = await fetch(`${API_BASE}/citas/agenda`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-CSRF-TOKEN': token
                },
                body: JSON.stringify(formData),
                signal: controller.signal
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

    /**
     * Maneja el envío del formulario de nueva cita
     */
    const handleFormSubmit = async (event) => {
        event.preventDefault();

        if (saveButton.disabled) return;

        // Deshabilitar botón durante el envío
        saveButton.disabled = true;
        const originalText = saveButton.textContent;
        saveButton.textContent = 'Guardando...';

        try {
            // Recopilar datos del formulario
            const parseIntOrNull = (value) => {
                const parsed = parseInt(value, 10);
                return Number.isFinite(parsed) ? parsed : null;
            };

            const formData = {
                IdCita: parseIntOrNull(form.querySelector('#appointmentId')?.value || ''),
                Fecha: form.querySelector('#newApptDate').value,
                Estado: form.querySelector('#newApptStatus').value,
                HoraInicio: form.querySelector('#newApptStartTime').value,
                IdPaciente: parseIntOrNull(form.querySelector('#newApptPatient').value),
                IdProfesional: parseIntOrNull(form.querySelector('#newApptProfessional').value),
                IdConsultorio: parseIntOrNull(form.querySelector('#newApptOffice').value),
                IdServicio: parseIntOrNull(form.querySelector('#newApptService').value),
                Notas: form.querySelector('#newApptNotes').value
            };

            // Validación básica de campos obligatorios antes de enviar
            const horaFinDerivada = calculateEndTime(formData.HoraInicio);

            if (!formData.Fecha || !formData.HoraInicio || !formData.IdPaciente || !formData.IdProfesional || !formData.IdConsultorio || !formData.IdServicio) {
                window.ToastService.warning('⚠️ Completa todos los campos obligatorios antes de guardar la cita.');
                saveButton.disabled = false;
                saveButton.textContent = originalText;
                return;
            }

            // Validation with AppointmentUtils and ValidationUtils
            if (window.AppointmentUtils && window.ValidationUtils) {
                // Clear previous errors
                form.querySelectorAll('input, select').forEach(input => window.ValidationUtils.clearError(input));

                const errors = window.AppointmentUtils.validateAppointmentTime(formData.Fecha, formData.HoraInicio, horaFinDerivada);
                if (errors.length > 0) {
                    errors.forEach(err => {
                        if (err.field === 'general') {
                            window.ToastService.warning(`⚠️ ${err.message}`);
                        } else {
                            let inputId = '';
                            if (err.field === 'fecha') inputId = 'newApptDate';
                            if (err.field === 'horaInicio') inputId = 'newApptStartTime';
                            if (err.field === 'horaFin') inputId = 'newApptEndTime';
                            
                            const inputEl = form.querySelector(`#${inputId}`);
                            if (inputEl) {
                                window.ValidationUtils.showError(inputEl, null, err.message);
                            } else {
                                window.ToastService.warning(`⚠️ ${err.message}`);
                            }
                        }
                    });
                    
                    if (!errors.some(e => e.field === 'general')) {
                        window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
                    }
                    saveButton.disabled = false;
                    saveButton.textContent = originalText;
                    return;
                }
            } else {
                // Fallback validación básica
                if (!formData.Fecha || !formData.HoraInicio) {
                    window.ToastService.warning('⚠️ Por favor complete todos los campos obligatorios');
                    saveButton.disabled = false;
                    saveButton.textContent = originalText;
                    return;
                }

            }

            // Preparar datos para el calendario
            const appointmentData = {
                date: formData.Fecha,
                startTime: formData.HoraInicio,
                endTime: horaFinDerivada,
                status: formData.Estado,
                statusClass: getStatusClass(formData.Estado),
                patientName: getSelectedText(form.querySelector('#newApptPatient')),
                professionalName: getSelectedText(form.querySelector('#newApptProfessional')),
                officeName: getSelectedText(form.querySelector('#newApptOffice')),
                serviceName: getSelectedText(form.querySelector('#newApptService')),
                notes: formData.Notas,
                patientId: formData.IdPaciente,
                professionalId: formData.IdProfesional,
                officeId: formData.IdConsultorio,
                serviceId: formData.IdServicio
            };

            // Enviar al servidor
            const result = await submitAppointment(formData);

            if (!result.success) {
                throw new Error(result.message || 'El servidor no confirmó el guardado de la cita.');
            }

            const isUpdate = Boolean(formData.IdCita);
            const added = !isUpdate && addAppointmentToCalendar(appointmentData);

            window.ToastService.success(
                isUpdate
                    ? '✅ Cita actualizada exitosamente'
                    : added
                        ? '✅ Cita creada exitosamente'
                        : '✅ Cita guardada. Cambia a la semana seleccionada para verla.'
            );
            closeModal();
            if (isUpdate) {
                window.setTimeout(() => window.location.reload(), 250);
            }
        } catch (error) {
            console.error('[SmileTrack][Agenda] Error al guardar cita:', error);
            window.ToastService.error(`❌ ${error.message || 'No fue posible guardar la cita. Intente nuevamente.'}`);
        } finally {
            // Restaurar estado del botón
            saveButton.disabled = false;
            saveButton.textContent = originalText;
        }
    };

    // Registrar event listeners
    fabButton.addEventListener('click', openModal);
    closeButton?.addEventListener('click', closeModal);
    cancelButton?.addEventListener('click', closeModal);
    form.addEventListener('submit', handleFormSubmit);
    form.querySelector('#newApptStartTime')?.addEventListener('input', syncEndTime);
    form.querySelector('#newApptStartTime')?.addEventListener('change', syncEndTime);

    // Cerrar modal al hacer clic fuera del contenido
    const handleOverlayClick = (event) => {
        if (event.target === modalOverlay) {
            closeModal();
        }
    };
    modalOverlay.addEventListener('click', handleOverlayClick);

    // Cerrar modal con tecla Escape
    const handleEscapeKey = (event) => {
        if (event.key === 'Escape' && modalOverlay.classList.contains('open')) {
            event.preventDefault();
            closeModal();
            return;
        }
        if (event.key === 'Tab' && modalOverlay.classList.contains('open')) {
            const focusable = [...modalOverlay.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])')]
                .filter(element => !element.disabled && element.offsetParent !== null);
            if (focusable.length === 0) return;
            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    };
    document.addEventListener('keydown', handleEscapeKey);

    // Funciones de limpieza
    cleanupHandlers.push(() => {
        fabButton.removeEventListener('click', openModal);
        closeButton?.removeEventListener('click', closeModal);
        cancelButton?.removeEventListener('click', closeModal);
        form.removeEventListener('submit', handleFormSubmit);
        form.querySelector('#newApptStartTime')?.removeEventListener('input', syncEndTime);
        form.querySelector('#newApptStartTime')?.removeEventListener('change', syncEndTime);
        modalOverlay.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleEscapeKey);
        document.removeEventListener('smiletrack:edit-appointment', editAppointment);
        document.removeEventListener('smiletrack:new-appointment', newAppointmentFromCalendar);
    });
};

const initAppointmentDetailModal = () => {
    const modal = safeGetElement('modalAppointment');
    const content = safeGetElement('modalApptContent');
    const closeButton = safeGetElement('modalApptClose');
    const cancelButton = safeGetElement('modalApptCancel');
    const editButton = safeGetElement('modalApptEdit');
    if (!modal || !content || !closeButton || !cancelButton || !editButton) return;
    if (modal.dataset.initialized === 'true') return;
    modal.dataset.initialized = 'true';

    let selectedAppointment = null;
    let lastFocusedElement = null;

    const close = () => {
        modal.classList.remove('open');
        modal.setAttribute('aria-hidden', 'true');
        modal.setAttribute('inert', '');
        document.body.classList.remove('modal-open');
        if (lastFocusedElement instanceof HTMLElement) lastFocusedElement.focus();
        lastFocusedElement = null;
        selectedAppointment = null;
    };

    const open = (element) => {
        selectedAppointment = {
            id: element.dataset.id,
            date: element.dataset.date,
            startTime: element.dataset.startTime,
            endTime: element.dataset.endTime,
            status: element.dataset.status,
            patientId: element.dataset.patientId,
            professionalId: element.dataset.professionalId,
            officeId: element.dataset.officeId,
            serviceId: element.dataset.serviceId,
            notes: element.dataset.notes || '',
            patientName: element.dataset.patientName || 'Sin paciente',
            professionalName: element.dataset.professionalName || 'Sin profesional',
            professionalEmail: element.dataset.professionalEmail || '',
            professionalPhone: element.dataset.professionalPhone || '',
            professionalRegistry: element.dataset.professionalRegistry || '',
            professionalUserStatus: element.dataset.professionalUserStatus || '',
            officeName: element.dataset.officeName || 'Sin consultorio',
            serviceName: element.dataset.serviceName || 'Sin servicio'
        };
        content.innerHTML = '';
        const rows = [
            ['Paciente', selectedAppointment.patientName],
            ['Profesional', selectedAppointment.professionalName]
        ];
        if (selectedAppointment.professionalEmail)
            rows.push(['Correo profesional', selectedAppointment.professionalEmail]);
        if (selectedAppointment.professionalPhone)
            rows.push(['Teléfono profesional', selectedAppointment.professionalPhone]);
        if (selectedAppointment.professionalRegistry)
            rows.push(['Registro médico', selectedAppointment.professionalRegistry]);
        if (selectedAppointment.professionalUserStatus)
            rows.push(['Estado cuenta', selectedAppointment.professionalUserStatus]);
        rows.push(
            ['Fecha', selectedAppointment.date],
            ['Horario', `${selectedAppointment.startTime} - ${selectedAppointment.endTime}`],
            ['Servicio', selectedAppointment.serviceName],
            ['Consultorio', selectedAppointment.officeName],
            ['Estado', selectedAppointment.status],
            ['Observaciones', selectedAppointment.notes || 'Sin observaciones']
        );
        rows.forEach(([label, value]) => {
            const row = document.createElement('p');
            const strong = document.createElement('strong');
            strong.textContent = `${label}: `;
            row.append(strong, document.createTextNode(value));
            content.appendChild(row);
        });
        lastFocusedElement = document.activeElement;
        modal.classList.add('open');
        modal.setAttribute('aria-hidden', 'false');
        modal.removeAttribute('inert');
        document.body.classList.add('modal-open');
        closeButton.focus();
    };

    const handleCalendarClick = (event) => {
        const appointment = event.target.closest('.appointment:not(.available)');
        if (appointment) {
            open(appointment);
            return;
        }
        const available = event.target.closest('.appointment.available');
        if (available) {
            document.dispatchEvent(new CustomEvent('smiletrack:new-appointment', {
                detail: { date: available.dataset.date }
            }));
        }
    };
    const handleCalendarKeydown = (event) => {
        const appointment = event.target.closest('.appointment:not(.available)');
        const available = event.target.closest('.appointment.available');
        if ((event.key === 'Enter' || event.key === ' ') && appointment) {
            event.preventDefault();
            open(appointment);
        } else if ((event.key === 'Enter' || event.key === ' ') && available) {
            event.preventDefault();
            document.dispatchEvent(new CustomEvent('smiletrack:new-appointment', {
                detail: { date: available.dataset.date }
            }));
        }
    };
    const handleEdit = () => {
        if (!selectedAppointment) return;
        const appointmentToEdit = selectedAppointment;
        close();
        document.dispatchEvent(new CustomEvent('smiletrack:edit-appointment', { detail: appointmentToEdit }));
    };
    const handleEscape = (event) => {
        if (!modal.classList.contains('open')) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            close();
            return;
        }
        if (event.key === 'Tab') {
            const focusable = [...modal.querySelectorAll('button, input, select, textarea, [tabindex]:not([tabindex="-1"])')]
                .filter(element => !element.disabled && element.offsetParent !== null);
            if (focusable.length === 0) return;
            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }
    };

    document.addEventListener('click', handleCalendarClick);
    document.addEventListener('keydown', handleCalendarKeydown);
    closeButton.addEventListener('click', close);
    cancelButton.addEventListener('click', close);
    editButton.addEventListener('click', handleEdit);
    const handleOverlayClick = (event) => {
        if (event.target === modal) close();
    };
    modal.addEventListener('click', handleOverlayClick);
    document.addEventListener('keydown', handleEscape);

    cleanupHandlers.push(() => {
        document.removeEventListener('click', handleCalendarClick);
        document.removeEventListener('keydown', handleCalendarKeydown);
        closeButton.removeEventListener('click', close);
        cancelButton.removeEventListener('click', close);
        editButton.removeEventListener('click', handleEdit);
        modal.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleEscape);
        document.body.classList.remove('modal-open');
    });
};