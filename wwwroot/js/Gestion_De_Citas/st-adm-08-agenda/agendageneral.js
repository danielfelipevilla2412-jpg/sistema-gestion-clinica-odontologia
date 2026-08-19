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
//  FUNCIONES DE ANIMACIÓN
// ════════════════════════════════════════════════════════════════════

/**
 * Anima un contador numérico desde 0 hasta el valor objetivo.
 */
const animateCounter = (element, targetString) => {
    if (!element) return;

    const targetValue = parseInt(targetString, 10);
    
    // Validar que el target sea un número válido y positivo
    if (isNaN(targetValue) || targetValue <= 0) return;

    let currentValue = 0;
    const step = Math.max(1, Math.ceil(targetValue / 30));

    const animationInterval = setInterval(() => {
        currentValue = Math.min(currentValue + step, targetValue);
        element.textContent = currentValue;

        if (currentValue >= targetValue) {
            clearInterval(animationInterval);
        }
    }, 30);
};

/**
 * Inicializa todas las animaciones nativas del dashboard.
 */
const initNativeAnimations = () => {
    // Animar contadores numéricos
    document.querySelectorAll('.stat-number[data-target]').forEach(element => {
        animateCounter(element, element.dataset.target);
    });

    // Animar barras de progreso usando requestAnimationFrame rastreado
    trackedRAF(() => {
        document.querySelectorAll('[data-width]').forEach(progressBar => {
            progressBar.style.width = `${progressBar.dataset.width}%`;
        });
    });
};

// ════════════════════════════════════════════════════════════════════
//  EXPORTACIÓN DE REPORTE PDF
// ════════════════════════════════════════════════════════════════════

/**
 * Genera y descarga un reporte PDF.
 * En producción, reemplazar con llamada real a la API.
 */
async function exportReport() {
    try {
        // Simulación de delay de red
        await new Promise(resolve => setTimeout(resolve, 1000));

        // Crear blob y generar descarga
        const reportContent = 'Reporte de Dashboard SmileTrack';
        const blob = new Blob([reportContent], { type: 'application/pdf' });
        const downloadUrl = window.URL.createObjectURL(blob);

        const downloadLink = document.createElement('a');
        downloadLink.href = downloadUrl;
        downloadLink.download = `reporte-dashboard-${new Date().toISOString().slice(0, 10)}.pdf`;
        downloadLink.click();

        // Liberar memoria del objeto URL
        window.URL.revokeObjectURL(downloadUrl);

        return true;
    } catch (error) {
        console.error('[SmileTrack][API] Error exportando reporte:', error);
        throw error;
    }
}

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

/**
 * Inicializa la funcionalidad de exportación de reportes.
 */
const initExport = () => {
    const exportButton = safeGetElement('btnExport');
    const progressBar = safeGetElement('topProgressBar');

    if (!exportButton || !progressBar) return;

    const handleExportClick = async () => {
        if (exportButton.disabled) return;

        // Actualizar UI durante la exportación
        exportButton.disabled = true;
        exportButton.innerHTML = '⏳ Generando...';

        progressBar.style.transition = 'width 1s cubic-bezier(.4,0,.2,1)';
        progressBar.style.width = '100%';

        try {
            await exportReport();
            window.ToastService.error('✅ Reporte PDF generado exitosamente');
        } catch (error) {
            window.ToastService.success('❌ Error al generar reporte');
        } finally {
            // Restaurar estado del botón después de un breve delay
            setTimeout(() => {
                exportButton.disabled = false;
                exportButton.innerHTML = '📄 Exportar PDF';

                const progressRow = progressBar.closest('.progress-row');
                const originalWidth = progressRow?.getAttribute('aria-valuenow') || '75';
                progressBar.style.width = `${originalWidth}%`;
            }, 500);
        }
    };

    exportButton.addEventListener('click', handleExportClick);
    cleanupHandlers.push(() => {
        exportButton.removeEventListener('click', handleExportClick);
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
            }
        } catch (err) {
            console.error('[SmileTrack][Agenda] Error reemplazando sección de calendario:', err);
        }
    };

    const loadWeek = async (weekStartIso, push = true) => {
        try {
            const search = new URLSearchParams(window.location.search);
            search.set('weekStart', weekStartIso);
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
        }
    };

    btnPrev.addEventListener('click', () => {
        const monday = currentWeekStart();
        monday.setDate(monday.getDate() - 7);
        loadWeek(toIso(monday));
    });

    btnNext.addEventListener('click', () => {
        const monday = currentWeekStart();
        monday.setDate(monday.getDate() + 7);
        loadWeek(toIso(monday));
    });

    btnToday.addEventListener('click', () => {
        const monday = getMonday(new Date());
        loadWeek(toIso(monday));
    });

    window.addEventListener('popstate', (e) => {
        const stateWeek = (e.state && e.state.weekStart) || (new URLSearchParams(window.location.search)).get('weekStart');
        if (stateWeek) loadWeek(stateWeek, false);
    });

    cleanupHandlers.push(() => {
        btnPrev.removeEventListener('click', () => {});
        btnNext.removeEventListener('click', () => {});
        btnToday.removeEventListener('click', () => {});
        window.removeEventListener('popstate', () => {});
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
        initNewAppointmentModal(); // ← AGREGAR ESTA LÍNEA

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

    /**
     * Abre el modal de nueva cita
     */
    const openModal = () => {
        modalOverlay.classList.add('open');
        modalOverlay.setAttribute('aria-hidden', 'false');
        modalOverlay.removeAttribute('inert');
        
        // Establecer fecha mínima como hoy
        const today = new Date().toISOString().split('T')[0];
        const dateInput = form.querySelector('#newApptDate');
        if (dateInput) {
            dateInput.setAttribute('min', today);
            if (!dateInput.value) {
                dateInput.value = today;
            }
        }

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
        form.reset();
        fabButton.focus();
    };

    /**
     * Mapea el estado de la cita a la clase CSS correspondiente
     */
    const getStatusClass = (status) => {
        if (window.AppointmentUtils) {
            return window.AppointmentUtils.getStatusLabelAndClass(status?.toLowerCase()).class;
        }
        const statusMap = {
            'Agendada': 'reserved',
            'Confirmada': 'confirmed',
            'Asistida': 'attended',
            'Cancelada': 'cancelled',
            'Disponible': 'available'
        };
        return statusMap[status] || 'reserved';
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

        // Agregar evento de clic para abrir detalle (si existe el modal de detalle)
        appointmentDiv.addEventListener('click', () => {
            console.log('[SmileTrack][Agenda] Abrir detalle de cita:', appointmentData);
            // Aquí puedes integrar con el modal de detalle existente si lo necesitas
        });

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
        try {
            const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenInput?.value || '';

            const response = await fetch(`${API_BASE}/appointments`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(formData)
            });

            if (!response.ok) {
                const errorBody = await response.json().catch(() => null);
                throw new Error(errorBody?.message || 'Error al crear la cita');
            }

            return await response.json();
        } catch (error) {
            console.error('[SmileTrack][API] Error al crear cita:', error);
            throw error;
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
                HoraFin: form.querySelector('#newApptEndTime').value,
                IdPaciente: parseIntOrNull(form.querySelector('#newApptPatient').value),
                IdProfesional: parseIntOrNull(form.querySelector('#newApptProfessional').value),
                IdConsultorio: parseIntOrNull(form.querySelector('#newApptOffice').value),
                IdServicio: parseIntOrNull(form.querySelector('#newApptService').value),
                Notas: form.querySelector('#newApptNotes').value
            };

            // Validación básica de campos obligatorios antes de enviar
            if (!formData.Fecha || !formData.HoraInicio || !formData.HoraFin || !formData.IdPaciente || !formData.IdProfesional || !formData.IdConsultorio || !formData.IdServicio) {
                window.ToastService.warning('⚠️ Completa todos los campos obligatorios antes de guardar la cita.');
                saveButton.disabled = false;
                saveButton.textContent = originalText;
                return;
            }

            // Validation with AppointmentUtils and ValidationUtils
            if (window.AppointmentUtils && window.ValidationUtils) {
                // Clear previous errors
                form.querySelectorAll('input, select').forEach(input => window.ValidationUtils.clearError(input));

                const errors = window.AppointmentUtils.validateAppointmentTime(formData.fecha, formData.horaInicio, formData.horaFin);
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
                if (!formData.fecha || !formData.horaInicio || !formData.horaFin) {
                    window.ToastService.warning('⚠️ Por favor complete todos los campos obligatorios');
                    saveButton.disabled = false;
                    saveButton.textContent = originalText;
                    return;
                }

                if (formData.horaInicio >= formData.horaFin) {
                    window.ToastService.warning('⚠️ La hora de inicio debe ser anterior a la hora de fin');
                    saveButton.disabled = false;
                    saveButton.textContent = originalText;
                    return;
                }
            }

            // Preparar datos para el calendario
            const appointmentData = {
                date: formData.fecha,
                startTime: formData.horaInicio,
                endTime: formData.horaFin,
                status: formData.estado,
                statusClass: getStatusClass(formData.estado),
                patientName: getSelectedText(form.querySelector('#newApptPatient')),
                professionalName: getSelectedText(form.querySelector('#newApptProfessional')),
                officeName: getSelectedText(form.querySelector('#newApptOffice')),
                serviceName: getSelectedText(form.querySelector('#newApptService')),
                notes: formData.notas
            };

            // Enviar al servidor
            const result = await submitAppointment(formData);

            if (result.success) {
                // Agregar al calendario
                const added = addAppointmentToCalendar(appointmentData);
                
                if (added) {
                    window.ToastService.success('✅ Cita creada exitosamente');
                    closeModal();
                }
            }
        } catch (error) {
            console.error('[SmileTrack][Agenda] Error al guardar cita:', error);
            window.ToastService.error('❌ Error al crear la cita. Intente nuevamente.');
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

    // Cerrar modal al hacer clic fuera del contenido
    modalOverlay.addEventListener('click', (event) => {
        if (event.target === modalOverlay) {
            closeModal();
        }
    });

    // Cerrar modal con tecla Escape
    const handleEscapeKey = (event) => {
        if (event.key === 'Escape' && modalOverlay.classList.contains('open')) {
            event.preventDefault();
            closeModal();
        }
    };
    document.addEventListener('keydown', handleEscapeKey);

    // Funciones de limpieza
    cleanupHandlers.push(() => {
        fabButton.removeEventListener('click', openModal);
        closeButton?.removeEventListener('click', closeModal);
        cancelButton?.removeEventListener('click', closeModal);
        form.removeEventListener('submit', handleFormSubmit);
        modalOverlay.removeEventListener('click', closeModal);
        document.removeEventListener('keydown', handleEscapeKey);
    });
};