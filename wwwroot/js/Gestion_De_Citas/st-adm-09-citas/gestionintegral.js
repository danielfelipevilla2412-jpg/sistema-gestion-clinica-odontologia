/**
 * ============================================
 * SmileTrack — Gestión Integral de Citas (st-adm-09-citas)
 * ============================================
 * Autor: Johan Santamaria
 * Fecha: 29/07/2026
 *
 * PROPÓSITO:
 * Gestiona la interactividad de la tabla de citas del administrador:
 * búsqueda debounced, filtrado local dinámico, control de modales,
 * y delega el guardado/cancelación al backend MVC en lugar de consumir
 * directamente un API REST PUT/DELETE en /api/citas.
 *
 * DECISIONES TÉCNICAS:
 * - Predomina SSR en la vista de citas del administrador
 * - Guardado / cancelación se realizan mediante formularios MVC existentes
 * - LocalStorage se usa solo como fallback de datos para el cliente
 * - Debounce en búsqueda para evitar recargas innecesarias
 * - Notificaciones no bloqueantes (toasts) para mejor UX
 *
 * NOTAS DE MANTENIMIENTO:
 * - Esta página no expone un endpoint genérico GET /api/citas para listado
 * - Los formularios deben publicar a /gestion-de-citas/guardar-cita y
 *   /gestion-de-citas/eliminar-cita según la implementación del backend
 * - El código cliente debe funcionar tanto con SSR como con datos locales
 * ============================================
 */

// ════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API Y AUTENTICACIÓN
// ════════════════════════════════════════════════════════════════════

/**
 * Base URL de la API REST.
 * Se mantiene como '/api' según la configuración del proyecto.
 */
const API_BASE = '/api';

/**
 * Tamaño de página para paginación de API.
 */
const API_PAGE_SIZE = 100;

/**
 * Centraliza los headers de autenticación.
 * Primero intenta JWT (sessionStorage st_jwt),
 * si no existe el backend acepta Cookie Authentication
 * (política ApiOrCookie).
 *
 * @returns {Object} Headers con Content-Type, Accept y opcionalmente Authorization
 */
const getAuthHeaders = () => {
    const headers = {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };

    try {
        const jwt = sessionStorage.getItem('st_jwt');
        if (jwt) {
            headers['Authorization'] = `Bearer ${jwt}`;
        }
    } catch (error) {
        // sessionStorage deshabilitado (modo privado), se ignora silenciosamente
    }

    return headers;
};

// ════════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ════════════════════════════════════════════════════════════════════

/**
 * Obtiene un elemento del DOM de forma segura.
 * Previene excepciones fatales en la inicialización si un elemento no existe.
 *
 * @param {string} elementId - ID del elemento a buscar
 * @returns {HTMLElement|null} Elemento encontrado o null
 */
const safeGetElement = (elementId) => {
    const element = document.getElementById(elementId);
    if (!element) {
        console.warn(`[SmileTrack] Elemento no encontrado: #${elementId}`);
    }
    return element;
};

/**
 * Debounce para evitar saturar la API con peticiones redundantes
 * ante cambios veloces del usuario (ej: typing en búsqueda).
 *
 * @param {Function} callback - Función a ejecutar
 * @param {number} delay - Milisegundos de espera
 * @returns {Function} Función debounced
 */
const debounce = (callback, delay) => {
    let timeoutId;

    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => callback.apply(this, args), delay);
    };
};

/**
 * Muestra una notificación toast no bloqueante.
 * Brinda retroalimentación al usuario sin entorpecer el flujo de trabajo.
 *
 * @param {string} message - Mensaje a mostrar
 * @param {string} type - Tipo de toast: 'success', 'error', 'warning'
 */

/**
 * Abre el modal de Crear/Editar Cita y gestiona el cierre por click fuera.
 * Se asigna a window directamente para que esté disponible ANTES de que el
 * inline script de la vista se registre en DOMContentLoaded (evita problema
 * de orden de carga: JS carga primero, luego inline script lo sobrescribe).
 */
window.openModalCita = () => {
    const modal = document.getElementById('modalCita');
    if (!modal) return;

    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    modal.addEventListener('click', function outsideHandler(e) {
        if (e.target === modal) {
            window.closeModalCita();
            modal.removeEventListener('click', outsideHandler);
        }
    });
};

/**
 * Cierra el modal de Crear/Editar Cita y restaura el scroll del body.
 */
window.closeModalCita = () => {
    const modal = document.getElementById('modalCita');
    if (!modal) return;

    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
};

// setFieldError removed in favor of ValidationUtils

const validateCitaForm = (form) => {
    let valid = true;

    const requiredFields = [
        {
            field: form.querySelector('#idPacienteCita'),
            message: 'Selecciona un paciente.'
        },
        {
            field: form.querySelector('#idProfesionalCita'),
            message: 'Selecciona un profesional.'
        },
        {
            field: form.querySelector('#idConsultorioCita'),
            message: 'Selecciona un consultorio.'
        },
        {
            field: form.querySelector('#idServicioCita'),
            message: 'Selecciona un servicio.'
        },
        {
            field: form.querySelector('#fechaCita'),
            message: 'Selecciona una fecha válida.'
        },
        {
            field: form.querySelector('#horaInicioCita'),
            message: 'Selecciona una hora de inicio.'
        },
        {
            field: form.querySelector('#idEstadoCita'),
            message: 'Selecciona un estado.'
        }
    ];

    if (window.ValidationUtils) {
        requiredFields.forEach(({ field }) => {
            if (field) {
                window.ValidationUtils.clearError(field);
                field.setAttribute('aria-invalid', 'false');
            }
        });
    }

    requiredFields.forEach(({ field, message }) => {
        const value = field?.value?.trim() || '';
        const isValid = Boolean(value);

        if (field) {
            field.setAttribute('aria-invalid', String(!isValid));
        }

        if (!isValid) {
            valid = false;

            if (field && window.ValidationUtils) {
                window.ValidationUtils.showError(
                    field,
                    null,
                    message
                );
            }
        }
    });

    const fecha = form.querySelector('#fechaCita')?.value || '';
    const horaInicio = form.querySelector('#horaInicioCita')?.value || '';
    const horaFin = form.querySelector('#horaFinCita')?.value || '';

    // Validar rango de horario cuando existe hora fin
    if (window.AppointmentUtils && fecha && horaInicio && horaFin) {
        const errors = window.AppointmentUtils.validateAppointmentTime(
            fecha,
            horaInicio,
            horaFin
        );

        if (errors.length > 0) {
            errors.forEach(err => {
                if (err.field === 'general') {
                    if (window.ToastService) {
                        window.ToastService.warning('Horario inválido', err.message);
                    }
                    return;
                }

                let inputId = '';

                if (err.field === 'fecha') {
                    inputId = 'fechaCita';
                } else if (err.field === 'horaInicio') {
                    inputId = 'horaInicioCita';
                } else if (err.field === 'horaFin') {
                    inputId = 'horaFinCita';
                }

                const inputEl = form.querySelector(`#${inputId}`);

                if (inputEl && window.ValidationUtils) {
                    window.ValidationUtils.showError(
                        inputEl,
                        null,
                        err.message
                    );
                }
            });

            valid = false;
        }
    }

    // Evitar citas en el pasado
    if (fecha && horaInicio) {
        const selectedDate = new Date(`${fecha}T${horaInicio}`);

        if (selectedDate < new Date()) {
            const fechaField = form.querySelector('#fechaCita');

            if (fechaField && window.ValidationUtils) {
                window.ValidationUtils.showError(
                    fechaField,
                    null,
                    'No puedes agendar una cita en un horario pasado.'
                );
            }

            valid = false;
        }
    }

    return valid;
};

const submitCitaForm = (event) => {
    const form = event.currentTarget;

    if (!validateCitaForm(form)) {
        event.preventDefault();

        if (window.ToastService) {
            window.ToastService.warning('Validación',
                'Completa correctamente los campos obligatorios de la cita.'
            );
        }

        const firstInvalid = form.querySelector(
            '[aria-invalid="true"]'
        );

        if (firstInvalid) {
            firstInvalid.focus();
        }

        return;
    }

    const submitButton = form.querySelector('#btnGuardarCita');

    if (submitButton) {
        submitButton.disabled = true;
        submitButton.textContent = 'Guardando...';
    }

    /*
     * No usamos fetch.
     * No usamos localStorage para crear la cita.
     *
     * El formulario se enviará normalmente mediante POST a:
     *
     * /gestion-de-citas/guardar-cita
     *
     * El controlador recibe CitaViewModel y guarda en SQL Server.
     */
};
/**
 * Mapea el formato del servidor al formato del cliente.
 *
 * Formato servidor: { IdCita, Paciente.NombreCompleto, Profesional.NombreCompleto, FechaHora ISO, Estado }
 * Formato cliente: { id, date, time, patient, doc, professional (slug), professionalName, service, status, avatar, color }
 *
 * @param {Object} serverData - Datos del servidor
 * @returns {Object} Datos formateados para el cliente
 */
const mapServerToClient = (serverData) => {
    const fechaHora = serverData.FechaHora ? new Date(serverData.FechaHora) : null;

    // Extraer componentes de fecha
    const year = fechaHora ? fechaHora.getFullYear() : 0;
    const month = fechaHora ? String(fechaHora.getMonth() + 1).padStart(2, '0') : '01';
    const day = fechaHora ? String(fechaHora.getDate()).padStart(2, '0') : '01';
    const hours = fechaHora ? String(fechaHora.getHours()).padStart(2, '0') : '09';
    const minutes = fechaHora ? String(fechaHora.getMinutes()).padStart(2, '0') : '00';

    // Datos del paciente
    const patientFullName = serverData.Paciente?.NombreCompleto || '—';
    const patientInitials = window.AppointmentUtils ? window.AppointmentUtils.getInitials(patientFullName) : 'XX';
    const patientColor = window.AppointmentUtils ? window.AppointmentUtils.pickColorByInitials(patientInitials) : 'blue';

    // El servidor no expone Documento/Paciente en listado;
    // usamos "Id" como identificación temporal.
    // Si se requiere cédula, ampliar ApiListarCitas con Paciente.Documento.
    const documentId = serverData.IdPaciente ? `#${serverData.IdPaciente}` : '—';

    return {
        id: serverData.IdCita,
        date: `${year}-${month}-${day}`,
        time: `${hours}:${minutes}`,
        patient: patientFullName,
        doc: documentId,
        professional: slugFromName(serverData.Profesional?.NombreCompleto),
        professionalName: serverData.Profesional?.NombreCompleto || 'Sin asignar',
        service: serverData.Servicio?.Nombre || 'Sin servicio',
        status: mapEstadoServerToClient(serverData.Estado),
        avatar: patientInitials,
        color: patientColor,
        _raw: serverData
    };
};

/**
 * Mapea el estado del servidor al estado del cliente.
 *
 * @param {string} estado - Estado del servidor
 * @returns {string} Estado normalizado para el cliente
 */
const mapEstadoServerToClient = (estado) => {
    if (window.AppointmentUtils) {
        return window.AppointmentUtils.mapEstadoServerToClient(estado);
    }
    if (!estado) return 'programada';
    return estado.toLowerCase().trim();
};

/**
 * Mapea el estado del cliente al estado del servidor.
 *
 * @param {string} estado - Estado del cliente
 * @returns {string} Estado normalizado para el servidor
 */
const mapEstadoClientToServer = (estado) => {
    if (estado === 'atendida') return 'finalizada';
    if (estado === 'no-show') return 'no_asistida';
    return estado || 'programada';
};

// ════════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON LOCALSTORAGE (FALLBACK OFFLINE)
// ════════════════════════════════════════════════════════════════════

/**
 * Módulo de almacenamiento local para citas.
 * Permite simular persistencia de datos en modo offline.
 */
const appointmentsStorage = {
    key: 'smiletrack_citas_admin',

    /**
     * Carga las citas desde LocalStorage (caché de la última respuesta real de la API).
     * Esta ruta de código solo se activa cuando NO hay filas renderizadas por el servidor
     * (ver shouldUseServerRenderedList) y fetchAppointments() no pudo contactar la API.
     *
     * @returns {Array} Array de citas (vacío si no hay caché ni conexión)
     */
    load: () => {
        const stored = localStorage.getItem(appointmentsStorage.key);

        if (stored) {
            try {
                return JSON.parse(stored);
            } catch (error) {
                console.warn('Error al cargar citas locales');
            }
        }

        // Sin caché ni conexión: estado vacío real (antes había 5 citas ficticias)
        return [];
    },

    /**
     * Guarda las citas en LocalStorage.
     *
     * @param {Array} data - Array de citas a guardar
     * @returns {boolean} True si se guardó exitosamente
     */
    save: (data) => {
        try {
            localStorage.setItem(appointmentsStorage.key, JSON.stringify(data));
            return true;
        } catch (error) {
            console.error('Error al guardar citas locales:', error);
            return false;
        }
    },

    /**
     * Agrega una nueva cita al almacenamiento local.
     *
     * @param {Object} appointment - Cita a agregar
     * @returns {Object} Cita agregada con ID asignado
     */
    addAppointment: (appointment) => {
        const data = appointmentsStorage.load();
        appointment.id = data.length > 0 ? Math.max(...data.map(a => a.id)) + 1 : 1;
        data.unshift(appointment);
        appointmentsStorage.save(data);
        return appointment;
    },

    /**
     * Obtiene una cita por su ID desde el almacenamiento local.
     *
     * @param {number} id - ID de la cita
     * @returns {Object|undefined} Cita encontrada o undefined
     */
    getAppointment: (id) => {
        return appointmentsStorage.load().find(appointment => appointment.id === id);
    }
};

// ════════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO DE LA APLICACIÓN
// ════════════════════════════════════════════════════════════════════

/**
 * Estado global de la aplicación.
 * Se inicializa con datos del almacenamiento local.
 */
let appointments = appointmentsStorage.load();
let searchQuery = '';
let filterStatus = '';
let filterProfessional = '';
let filterDate = '';
let currentTab = 'all';
let currentPage = 1;
const itemsPerPage = 5;

/**
 * Mapeo de colores para avatares de pacientes.
 */
const avatarColors = {
    blue: 'bg-blue-100 text-blue-600',
    green: 'bg-green-100 text-green-600',
    purple: 'bg-purple-100 text-purple-600',
    red: 'bg-red-100 text-red-600',
    slate: 'bg-slate-100 text-slate-600'
};

/**
 * Determina si se debe usar la lista renderizada en servidor (Razor).
 * Actualmente retorna false para activar renderizado cliente con API.
 *
 * @returns {boolean} True para usar renderizado servidor
 */
const shouldUseServerRenderedList = () => {
    const tbody = document.getElementById('citasBody');
    return !!(tbody && tbody.children.length > 0 && tbody.querySelector('.table-row'));
};

/**
 * Configuración de labels y clases CSS para cada estado de cita.
 * Usa AppointmentUtils si está disponible.
 */
const statusLabels = new Proxy({}, {
    get: function (target, prop) {
        if (window.AppointmentUtils) {
            return window.AppointmentUtils.getStatusLabelAndClass(prop);
        }
        return { label: prop, class: 'programada' };
    }
});

// ════════════════════════════════════════════════════════════════════
//  UTILIDADES DE FORMATO
// ════════════════════════════════════════════════════════════════════

/**
 * Formatea una fecha ISO a formato legible en español.
 *
 * @param {string} isoDate - Fecha en formato ISO (YYYY-MM-DD)
 * @returns {string} Fecha formateada (ej: "24 May 2026")
 */
const fmtDate = (isoDate) => {
    if (!isoDate) return '—';

    const [year, month, day] = isoDate.split('-');
    const meses = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

    return `${parseInt(day)} ${meses[parseInt(month) - 1]} ${year}`;
};

/**
 * Formatea una hora de 24h a formato 12h con AM/PM.
 *
 * @param {string} time - Hora en formato HH:MM
 * @returns {string} Hora formateada (ej: "2:30 PM")
 */
const fmtTime = (time) => {
    if (!time) return '—';

    const [hours, minutes] = time.split(':');
    const hour = parseInt(hours);
    const period = hour >= 12 ? 'PM' : 'AM';
    const displayHour = hour > 12 ? hour - 12 : (hour === 0 ? 12 : hour);

    return `${displayHour}:${minutes} ${period}`;
};

// ════════════════════════════════════════════════════════════════════
//  FILTRADO COMBINADO
// ════════════════════════════════════════════════════════════════════

/**
 * Filtra las citas según todos los criterios activos.
 * Combina búsqueda, filtros de estado, profesional, fecha y tab actual.
 *
 * @returns {Array} Array de citas filtradas
 */
const getFilteredAppointments = () => {
    return appointments.filter(appointment => {
        // Filtrar por tab actual
        if (currentTab === 'cancelled' && appointment.status !== 'cancelada') return false;
        if (currentTab === 'no-show' && appointment.status !== 'no-show') return false;

        // Filtrar por estado
        if (filterStatus && appointment.status !== filterStatus) return false;

        // Filtrar por profesional
        if (filterProfessional && appointment.professional !== filterProfessional) return false;

        // Filtrar por fecha
        if (filterDate && appointment.date !== filterDate) return false;

        // Filtrar por búsqueda de texto
        if (searchQuery) {
            const query = searchQuery.toLowerCase();
            const patientMatch = appointment.patient.toLowerCase().includes(query);
            const docMatch = String(appointment.doc).toLowerCase().includes(query);

            if (!patientMatch && !docMatch) return false;
        }

        return true;
    });
};

// ════════════════════════════════════════════════════════════════════
//  RENDER: TABLA DE CITAS
// ════════════════════════════════════════════════════════════════════

/**
 * Renderiza la tabla de citas con los datos filtrados y paginados.
 * Incluye event listeners para botones de acción.
 */
const renderAppointments = () => {
    const tableBody = safeGetElement('citasBody');
    if (!tableBody || shouldUseServerRenderedList()) return;

    const filteredAppointments = getFilteredAppointments();

    // Mostrar estado vacío si no hay resultados
    if (!filteredAppointments.length) {
        tableBody.innerHTML = '<div class="empty-state" role="status">No se encontraron citas con los criterios de búsqueda.</div>';
        updatePagination(0);
        return;
    }

    // Calcular paginación
    const startIndex = (currentPage - 1) * itemsPerPage;
    const pageData = filteredAppointments.slice(startIndex, startIndex + itemsPerPage);

    // Renderizar filas de la tabla
    tableBody.innerHTML = pageData.map(appointment => {
        const status = statusLabels[appointment.status] || statusLabels.programada;

        return `
            <div class="table-row" role="row" tabindex="0" aria-label="Cita de ${appointment.patient} el ${fmtDate(appointment.date)}">
                <div class="table-col col-fecha" role="cell" data-label="Fecha">
                    <time datetime="${appointment.date}">${fmtDate(appointment.date)}</time>
                </div>
                <div class="table-col col-hora" role="cell" data-label="Hora">
                    <time datetime="${appointment.date}T${appointment.time}:00">${fmtTime(appointment.time)}</time>
                </div>
                <div class="table-col col-paciente" role="cell" data-label="Paciente">
                    <div class="patient-info">
                        <div class="patient-avatar ${avatarColors[appointment.color] || avatarColors.blue}" aria-hidden="true">
                            ${appointment.avatar}
                        </div>
                        <div>
                            <span class="patient-name">${appointment.patient}</span>
                            <span class="patient-id">ID: ${appointment.doc}</span>
                        </div>
                    </div>
                </div>
                <div class="table-col col-profesional" role="cell" data-label="Profesional">
                    ${appointment.professionalName}
                </div>
                <div class="table-col col-servicio" role="cell" data-label="Servicio">
                    ${appointment.service}
                </div>
                <div class="table-col col-estado text-center" role="cell" data-label="Estado">
                    <span class="status-badge ${status.class}" role="status" aria-label="Estado: ${status.label}">
                        ${status.label}
                    </span>
                </div>
                <div class="table-col col-acciones text-right" role="cell" data-label="Acciones">
                    <div class="actions-cell">
                        <button class="action-btn btn-view" aria-label="Ver detalle de cita de ${appointment.patient}" data-id="${appointment.id}" title="Ver detalle">
                          👁️ <span class="btn-text">Ver</span>
                        </button>
                        <button class="action-btn btn-edit" aria-label="Editar cita de ${appointment.patient}" data-id="${appointment.id}" title="Editar cita">
                          ✏️ <span class="btn-text">Editar</span>
                        </button>
                        <button class="action-btn btn-delete" aria-label="Cancelar cita de ${appointment.patient}" data-id="${appointment.id}" title="Cancelar cita">
                          ❌ <span class="btn-text">Cancelar</span>
                        </button>
                    </div>
                </div>
            </div>
        `;
    }).join('');

    // Agregar event listeners a botones de ver
    tableBody.querySelectorAll('.btn-view').forEach(button => {
        button.addEventListener('click', (event) => {
            openAppointmentModal(parseInt(event.currentTarget.dataset.id), 'view');
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                openAppointmentModal(parseInt(event.currentTarget.dataset.id), 'view');
            }
        });
    });

    // Agregar event listeners a botones de editar
    tableBody.querySelectorAll('.btn-edit').forEach(button => {
        button.addEventListener('click', (event) => {
            openAppointmentModal(parseInt(event.currentTarget.dataset.id), 'edit');
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                openAppointmentModal(parseInt(event.currentTarget.dataset.id), 'edit');
            }
        });
    });

    // Agregar event listeners a botones de eliminar
    tableBody.querySelectorAll('.btn-delete').forEach(button => {
        button.addEventListener('click', (event) => {
            cancelAppointment(parseInt(event.currentTarget.dataset.id));
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                cancelAppointment(parseInt(event.currentTarget.dataset.id));
            }
        });
    });

    updatePagination(filteredAppointments.length);
};

// ════════════════════════════════════════════════════════════════════
//  MODAL DE CITA
// ════════════════════════════════════════════════════════════════════

/**
 * Obtiene una cita por su ID, buscando primero en memoria y luego en storage.
 *
 * @param {number} id - ID de la cita
 * @returns {Object|undefined} Cita encontrada o undefined
 */
const getAppointmentById = (id) => {
    const inMemory = appointments.find(appointment => appointment.id === id);
    if (inMemory) return inMemory;
    return appointmentsStorage.getAppointment(id);
};

/**
 * Abre el modal de cita en modo vista o edición.
 *
 * @param {number} id - ID de la cita
 * @param {string} mode - Modo: 'view' o 'edit'
 */
const openAppointmentModal = (id, mode) => {
    const appointment = getAppointmentById(id);

    if (!appointment) {
        window.ToastService.warning('Cita no encontrada');
        return;
    }

    const modalElement = safeGetElement('modalAppointment');
    const contentElement = safeGetElement('modalAppointmentContent');
    const titleElement = safeGetElement('modalAppointmentTitle');
    const editButton = safeGetElement('modalAppointmentEdit');

    if (!modalElement || !contentElement || !titleElement) return;

    if (mode === 'view') {
        // Modo visualización
        titleElement.textContent = `Detalle: ${appointment.patient}`;
        editButton.style.display = 'none';

        contentElement.innerHTML = `
            <p><strong>Fecha:</strong> <time datetime="${appointment.date}">${fmtDate(appointment.date)}</time></p>
            <p><strong>Hora:</strong> <time datetime="${appointment.date}T${appointment.time}:00">${fmtTime(appointment.time)}</time></p>
            <p><strong>Documento / ID:</strong> ${appointment.doc}</p>
            <p><strong>Profesional:</strong> ${appointment.professionalName}</p>
            <p><strong>Servicio:</strong> ${appointment.service}</p>
            <p><strong>Estado:</strong> 
                <span class="status-badge ${statusLabels[appointment.status]?.class || 'programada'}">
                    ${statusLabels[appointment.status]?.label || appointment.status}
                </span>
            </p>
        `;
    } else {
        // Modo edición
        titleElement.textContent = `Editar: ${appointment.patient}`;
        editButton.style.display = 'inline-flex';
        editButton.textContent = 'Guardar';

        contentElement.innerHTML = `
            <p>
                <strong>Fecha:</strong> 
                <input type="date" value="${appointment.date}" id="editDate" class="filter-date" style="margin-left:8px">
            </p>
            <p>
                <strong>Hora:</strong> 
                <input type="time" value="${appointment.time}" id="editTime" class="filter-select" style="margin-left:8px">
            </p>
            <p>
                <strong>Servicio:</strong> 
                <input type="text" value="${appointment.service}" id="editService" class="search-input" style="margin-left:8px;width:200px" placeholder="Nombre servicio">
            </p>
            <p>
                <strong>Estado:</strong> 
                <select id="editStatus" class="filter-select" style="margin-left:8px">
                    <option value="programada" ${appointment.status === 'programada' ? 'selected' : ''}>Programada</option>
                    <option value="confirmada" ${appointment.status === 'confirmada' ? 'selected' : ''}>Confirmada</option>
                    <option value="atendida" ${appointment.status === 'atendida' ? 'selected' : ''}>Atendida</option>
                    <option value="cancelada" ${appointment.status === 'cancelada' ? 'selected' : ''}>Cancelada</option>
                </select>
            </p>
        `;

        editButton.onclick = () => saveAppointmentEdit(id);
    }

    // Mostrar modal
    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    // Focus en botón de cerrar para accesibilidad
    const closeButton = safeGetElement('modalAppointmentClose');
    if (closeButton) closeButton.focus();
};

/**
 * Cierra el modal de cita y restaura el scroll del body.
 */
const closeAppointmentModal = () => {
    const modalElement = safeGetElement('modalAppointment');

    if (modalElement) {
        modalElement.classList.remove('open');
        modalElement.setAttribute('aria-hidden', 'true');
        modalElement.setAttribute('inert', '');
        document.body.style.overflow = '';
    }
};

/**
 * Guarda los cambios de una cita mediante el formulario MVC existente.
 * No depende de un endpoint PUT /api/citas/{id} directo.
 *
 * @param {number} id - ID de la cita a actualizar
 */
const saveAppointmentEdit = (id) => {
    const appointment = getAppointmentById(id);
    if (!appointment) return;

    const rawData = appointment._raw || {};
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) {
        window.ToastService.error(
            'Error',
            'No se pudo guardar la cita: token antiforgery no encontrado.'
        );
        return;
    }

    const form = document.createElement('form');
    form.method = 'post';
    form.action = '/gestion-de-citas/guardar-cita';
    form.style.display = 'none';

    const fields = {
        __RequestVerificationToken: tokenInput.value,
        ReturnUrl: window.location.pathname + window.location.search,
        IdCita: id,
        IdPaciente: rawData.IdPaciente || appointment.id || 0,
        IdProfesional: rawData.IdProfesional || 0,
        IdConsultorio: rawData.IdConsultorio || 0,
        IdServicio: rawData.IdServicio || 0,
        IdEstado: rawData.IdEstado || 0,
        Fecha: safeGetElement('editDate')?.value || appointment.date,
        HoraInicio: safeGetElement('editTime')?.value || appointment.time,
        Estado: safeGetElement('editStatus')?.value || rawData.Estado || appointment.status,
        Notas: rawData.Notas || ''
    };

    Object.entries(fields).forEach(([name, value]) => {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = name;
        input.value = String(value ?? '');
        form.appendChild(input);
    });

    document.body.appendChild(form);
    form.submit();
};

/**
 * Abre el modal de confirmación de eliminación de cita (SSR o client-side).
 * Reemplaza window.confirm() para mejorar UX y respetar el diseño del sistema.
 *
 * @param {number} id - ID de la cita a cancelar
 * @param {string} [patientName] - Nombre del paciente para el mensaje de confirmación
 */
window.openConfirmDeleteCita = (id, patientName) => {
    const modal = document.getElementById('modalConfirmDeleteCita');
    const msgEl = document.getElementById('modalConfirmDeleteCitaMessage');
    const idInput = document.getElementById('deleteCitaId');
    const returnUrlInput = document.getElementById('deleteCitaReturnUrl');

    if (!modal) return;

    if (msgEl) {
        const nombre = patientName ? ` de ${patientName}` : '';
        msgEl.textContent = `¿Está seguro de eliminar esta cita${nombre}? Esta acción no se puede deshacer.`;
    }
    if (idInput) idInput.value = id;
    if (returnUrlInput) returnUrlInput.value = window.location.pathname + window.location.search;

    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    // Focus en botón cancelar para prevenir confirmación accidental
    setTimeout(() => {
        const cancelBtn = document.getElementById('modalConfirmDeleteCitaCancel');
        if (cancelBtn) cancelBtn.focus();
    }, 50);
};

/**
 * Cierra el modal de confirmación de eliminación de cita.
 */
window.closeConfirmDeleteCita = () => {
    const modal = document.getElementById('modalConfirmDeleteCita');
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
};

/**
 * Cancela una cita usando modal de confirmación personalizado (no window.confirm).
 * Se activa desde botones de la tabla renderizada en cliente (modo fallback).
 *
 * @param {number} id - ID de la cita a cancelar
 * @param {string} [patientName] - Nombre del paciente (opcional)
 */
const cancelAppointment = (id, patientName) => {
    window.openConfirmDeleteCita(id, patientName || '');
};

// ════════════════════════════════════════════════════════════════════
//  PAGINACIÓN Y CONTADORES
// ════════════════════════════════════════════════════════════════════

/**
 * Actualiza la UI de paginación según el total de items filtrados.
 *
 * @param {number} totalItems - Total de items después del filtrado
 */
const updatePagination = (totalItems) => {
    const totalPages = Math.ceil(totalItems / itemsPerPage) || 1;
    const showing = Math.min(itemsPerPage, Math.max(0, totalItems - (currentPage - 1) * itemsPerPage));

    const pageShowingElement = safeGetElement('pageShowing');
    const pageTotalElement = safeGetElement('pageTotal');
    const previousButton = safeGetElement('btnPrev');
    const nextButton = safeGetElement('btnNext');

    if (pageShowingElement) pageShowingElement.textContent = showing;
    if (pageTotalElement) pageTotalElement.textContent = totalItems;
    if (previousButton) previousButton.disabled = currentPage === 1;
    if (nextButton) nextButton.disabled = currentPage >= totalPages;
};

/**
 * Anima un contador numérico desde 0 hasta el valor objetivo.
 *
 * @param {HTMLElement} element - Elemento DOM a animar
 * @param {number} target - Valor objetivo
 */
const animateCounter = (element, target) => {
    if (!element) return;

    let currentValue = 0;
    const step = Math.max(1, Math.ceil(target / 30));

    const animationTimer = setInterval(() => {
        currentValue = Math.min(currentValue + step, target);
        element.textContent = currentValue;

        if (currentValue >= target) {
            clearInterval(animationTimer);
        }
    }, 30);
};

/**
 * Actualiza las estadísticas (KPIs) en la parte superior de la página.
 */
const updateStats = () => {
    if (shouldUseServerRenderedList()) return;

    const total = appointments.length;
    const scheduled = appointments.filter(appointment =>
        appointment.status === 'programada' || appointment.status === 'confirmada'
    ).length;
    const cancelled = appointments.filter(appointment => appointment.status === 'cancelada').length;
    const attended = appointments.filter(appointment => appointment.status === 'atendida').length;

    animateCounter(safeGetElement('statTotal'), total);
    animateCounter(safeGetElement('statScheduled'), scheduled);
    animateCounter(safeGetElement('statCancelled'), cancelled);
    animateCounter(safeGetElement('statAttended'), attended);
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

    // Event listeners
    hamburgerButton.addEventListener('click', () => toggleMenu(true));
    overlayElement.addEventListener('click', () => toggleMenu(false));

    // Cerrar menú en móvil al hacer clic en un enlace
    sidebarElement.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            if (window.innerWidth <= 680) toggleMenu(false);
        });
    });

    // Cerrar con tecla Escape
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
            event.preventDefault();
            toggleMenu(false);
        }
    });
};

/**
 * Inicializa las tabs de filtrado rápido por estado.
 */
const initTabs = () => {
    if (shouldUseServerRenderedList()) return;

    const tabButtons = document.querySelectorAll('.tab-btn');

    tabButtons.forEach(tabButton => {
        tabButton.addEventListener('click', () => {
            // Actualizar estado visual
            tabButtons.forEach(button => {
                button.classList.remove('active');
                button.setAttribute('aria-selected', 'false');
            });

            tabButton.classList.add('active');
            tabButton.setAttribute('aria-selected', 'true');

            // Actualizar filtro y renderizar
            currentTab = tabButton.dataset.tab;
            currentPage = 1;
            renderAppointments();
        });
    });
};

/**
 * Inicializa la búsqueda con debounce.
 */
const initSearch = () => {
    if (shouldUseServerRenderedList()) return;

    const searchInput = safeGetElement('searchAppointments');

    searchInput?.addEventListener('input', debounce((event) => {
        searchQuery = (event.target.value || '').toLowerCase();
        currentPage = 1;
        renderAppointments();
    }, 250));
};

/**
 * Inicializa los filtros de estado, profesional y fecha.
 */
const initFilters = () => {
    if (shouldUseServerRenderedList()) return;

    const filterStatusElement = safeGetElement('filterStatus');
    const filterProfessionalElement = safeGetElement('filterProfessional');
    const filterDateElement = safeGetElement('filterDate');

    filterStatusElement?.addEventListener('change', (event) => {
        filterStatus = event.target.value;
        currentPage = 1;
        renderAppointments();
    });

    filterProfessionalElement?.addEventListener('change', (event) => {
        filterProfessional = event.target.value;
        currentPage = 1;
        renderAppointments();
    });

    filterDateElement?.addEventListener('change', (event) => {
        filterDate = event.target.value;
        currentPage = 1;
        renderAppointments();
    });
};

/**
 * Inicializa la paginación de la tabla.
 */
const initPagination = () => {
    if (shouldUseServerRenderedList()) return;

    const previousButton = safeGetElement('btnPrev');
    const nextButton = safeGetElement('btnNext');

    previousButton?.addEventListener('click', () => {
        if (currentPage > 1) {
            currentPage--;
            renderAppointments();
        }
    });

    nextButton?.addEventListener('click', () => {
        const filteredAppointments = getFilteredAppointments();
        const totalPages = Math.ceil(filteredAppointments.length / itemsPerPage);

        if (currentPage < totalPages) {
            currentPage++;
            renderAppointments();
        }
    });
};

/**
 * Inicializa el botón de nueva cita.
 * openModalCita y closeModalCita están en window al principio de este archivo,
 * garantizando disponibilidad antes de cualquier DOMContentLoaded (incluido el inline script).
 */
const initNewAppointment = () => {
    const newAppointmentButton = safeGetElement('btnNewCita') || safeGetElement('btnNewAppointment');
    if (!newAppointmentButton) return;
    if (newAppointmentButton.hasAttribute('onclick')) return;
    newAppointmentButton.addEventListener('click', () => {
        if (typeof window.openModalCita === 'function') window.openModalCita();
    });
};

/**
 * Inicializa el botón de optimización del banner.
 */
const initBanner = () => {
    const optimizeButton = safeGetElement('btnOptimize');

    optimizeButton?.addEventListener('click', () => {
        window.ToastService.success('⚙️ Optimizando agenda... (simulado)');
    });
};

// ════════════════════════════════════════════════════════════════════
//  LLAMADAS A LA API
// ════════════════════════════════════════════════════════════════════

/**
 * Petición principal de carga de citas.
 * Si la API responde usa esos datos; si falla cae al cache LocalStorage.
 *
 * @returns {Promise<Array>} Array de citas mapeadas
 */
async function fetchAppointments() {
    // No hay un endpoint GET /api/citas disponible en el backend actual para esta vista.
    // Usamos los datos locales / cache como fallback estable.
    console.warn('[SmileTrack] No existe GET /api/citas para esta vista. Usando datos locales/cache.');
    return appointmentsStorage.load();
}

// ════════════════════════════════════════════════════════════════════
//  MANEJO DE ERRORES
// ════════════════════════════════════════════════════════════════════

/**
 * Muestra un banner de error en la parte superior de la página.
 * Útil para errores críticos que el usuario debe conocer.
 *
 * @param {string} message - Mensaje de error a mostrar
 */
function mostrarErrorUsuario(message) {
    let errorBar = document.getElementById('smiletrack-error-bar');

    if (!errorBar) {
        errorBar = document.createElement('div');
        errorBar.id = 'smiletrack-error-bar';
        errorBar.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:99999;background:#dc2626;color:white;padding:14px 20px;text-align:center;font-family:system-ui,-apple-system,sans-serif;font-size:15px;box-shadow:0 4px 12px rgba(0,0,0,.15);border-bottom:3px solid #991b1b;';
        errorBar.setAttribute('role', 'alert');
        document.body.appendChild(errorBar);
    }

    errorBar.innerHTML = `<strong>[SmileTrack]</strong> ${message} <button onclick="document.getElementById('smiletrack-error-bar').style.display='none'" style="margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;">×</button>`;
    errorBar.style.display = 'block';
}

// ════════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ════════════════════════════════════════════════════════════════════

/**
 * Inicializa el modal real de Crear/Editar Cita.
 *
 * La vista st-adm-09-citas utiliza:
 *   #modalCita
 *
 * El formulario dentro del modal hace POST normal hacia:
 *   /gestion-de-citas/guardar-cita
 */
const initModal = () => {
    const modalElement = safeGetElement('modalCita');

    if (!modalElement) {
        console.warn('[SmileTrack] No se encontró el modal #modalCita.');
        return;
    }

    const citaForm = modalElement.querySelector('form');

    // Cerrar al hacer clic en el fondo del modal
    modalElement.addEventListener('click', (event) => {
        if (event.target === modalElement) {
            window.closeModalCita();
        }
    });

    // Cerrar con Escape
    document.addEventListener('keydown', (event) => {
        if (
            event.key === 'Escape' &&
            modalElement.classList.contains('open')
        ) {
            event.preventDefault();
            window.closeModalCita();
        }
    });

    // Validar antes del POST normal
    citaForm?.addEventListener('submit', submitCitaForm);
};

/**
 * Inicialización principal del módulo.
 */
const init = async () => {
    try {
        const useSSR = shouldUseServerRenderedList();

        // Componentes generales
        initSidebar();
        initNewAppointment();
        /**
 * Calcula automáticamente la hora de fin de la cita.
 * Todas las citas tienen una duración fija de 60 minutos.
 */
        /**
         * Calcula automáticamente la hora de fin.
         * Duración fija: 60 minutos.
         */
        const initHoraCita = () => {
            const horaInicio = document.getElementById('horaInicioCita');
            const horaFin = document.getElementById('horaFinCita');

            if (!horaInicio || !horaFin) {
                console.warn('[SmileTrack] No se encontraron los campos de hora.');
                return;
            }

            const calcularHoraFin = () => {
                const valor = horaInicio.value;

                if (!valor) {
                    horaFin.value = '';
                    return;
                }

                const [hora, minuto] = valor.split(':').map(Number);

                if (Number.isNaN(hora) || Number.isNaN(minuto)) {
                    horaFin.value = '';
                    return;
                }

                const totalMinutos = (hora * 60) + minuto + 60;
                const minutosDia = 24 * 60;

                const resultado = totalMinutos % minutosDia;

                const horaFinal = Math.floor(resultado / 60);
                const minutoFinal = resultado % 60;

                horaFin.value =
                    `${String(horaFinal).padStart(2, '0')}:${String(minutoFinal).padStart(2, '0')}`;
            };

            horaInicio.addEventListener('input', calcularHoraFin);
            horaInicio.addEventListener('change', calcularHoraFin);

            // Calcular al abrir/cargar
            calcularHoraFin();
        };

        initHoraCita();
        initModal();
        initBanner();

        // Componentes que solo se utilizan cuando
        // la tabla no viene renderizada por Razor.
        if (!useSSR) {
            initTabs();
            initSearch();
            initFilters();
            initPagination();

            appointments = await fetchAppointments();

            updateStats();
            renderAppointments();
        } else {
            // La tabla viene desde Razor/Servidor.
            // No sobrescribimos la información con LocalStorage.

            document.querySelectorAll('[data-target]').forEach((element) => {
                const target = parseInt(
                    element.getAttribute('data-target'),
                    10
                );

                if (!Number.isNaN(target) && target >= 0) {
                    animateCounter(element, target);
                }
            });
        }

    } catch (error) {
        console.error(
            '[SmileTrack] Error inicializando módulo citas:',
            error
        );

        mostrarErrorUsuario(
            error?.message ||
            'Error cargando el módulo de citas. Intente recargar.'
        );
    }
};

// Inicializar cuando el DOM esté completamente cargado
document.addEventListener('DOMContentLoaded', init);