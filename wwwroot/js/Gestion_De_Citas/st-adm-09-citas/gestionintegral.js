/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Gestión Integral de Citas (st-adm-09-citas)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-adm-09-citas/gestionintegral.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra el panel de control integral de citas del Administrador/Recepcionista.
 * Maneja la tabla de citas paginada, filtrado reactivo debounced, modales de edición/detalle
 * y sincronización de datos con los endpoints del backend.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Búsqueda en tiempo real con debounce para optimizar peticiones al servidor.
 * - Confirmación mediante modal interactivo antes de ejecutar soft delete (cancelación).
 * - Despliegue de notificaciones Toast no bloqueantes con manejo de fallbacks.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Stadm09Citas
 * - HTML: Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml
 * ============================================ */

// ════════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ════════════════════════════════════════════════════════════════════

const API_BASE_URL = '/api';
const API_PAGE_SIZE_LIMIT = 100;
const DEFAULT_ITEMS_PER_PAGE = 5;

// ════════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ════════════════════════════════════════════════════════════════════

let configuredDurationMinutes = 60;
let modalCitaTriggerElRef = null;
let deleteCitaTriggerElRef = null;

let appointmentsList = [];
let searchQueryText = '';
let filterStatusValue = '';
let filterProfessionalValue = '';
let filterDateValue = '';
let currentTabFilter = 'all';
let currentPageNumber = 1;

const avatarColorCssMap = {
    blue:   '#2563eb',
    green:  '#059669',
    purple: '#7c3aed',
    red:    '#dc2626',
    slate:  '#64748b'
};

const statusLabelsProxy = new Proxy({}, {
    get: function (target, prop) {
        const info = window.CommonUtils.getStatusInfo(prop);
        return { label: info.label, class: info.cls };
    }
});

// ════════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ════════════════════════════════════════════════════════════════════

const safeGetElement = (id) =>
    window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(id) : document.getElementById(id);

const debounce = (fn, delay) =>
    window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const getAuthHeaders = () => {
    const headers = {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };
    try {
        const jwt = sessionStorage.getItem('st_jwt');
        if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
    } catch (error) { /* modo privado */ }
    return headers;
};

const getInvoiceHeaders = () => {
    const headers = getAuthHeaders();
    const csrf = window.CommonUtils?.getCsrfToken?.() || document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (csrf) headers['X-CSRF-TOKEN'] = csrf;
    return headers;
};

function displayUserErrorMessage(message) {
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

const formatDateLabel = (isoDate) => {
    if (!isoDate) return '—';
    const [year, month, day] = isoDate.split('-');
    const months = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
    return `${parseInt(day, 10)} ${months[parseInt(month, 10) - 1]} ${year}`;
};

const formatTime12hLabel = (timeString) => {
    if (!timeString) return '—';
    const [hours, minutes] = timeString.split(':');
    const hourNum = parseInt(hours, 10);
    const period = hourNum >= 12 ? 'PM' : 'AM';
    const displayHour = hourNum > 12 ? hourNum - 12 : (hourNum === 0 ? 12 : hourNum);
    return `${displayHour}:${minutes} ${period}`;
};

const shouldUseServerRenderedList = () => {
    const tbody = document.getElementById('citasBody');
    return !!(tbody && tbody.children.length > 0 && tbody.querySelector('tr'));
};

const animateCounter = (element, target) => {
  if (typeof window.animateCounter === 'function') {
    if (element && element.dataset.animated === '1') {
      delete element.dataset.animated;
    }
    window.animateCounter(element, target);
  }
};

// Global Modals Functions for HTML onclick compatibility
window.openModalCita = (triggerEl) => {
    modalCitaTriggerElRef = triggerEl || document.activeElement;
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

window.closeModalCita = () => {
    const modal = document.getElementById('modalCita');
    if (!modal) return;

    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    if (modalCitaTriggerElRef && typeof modalCitaTriggerElRef.focus === 'function' && document.contains(modalCitaTriggerElRef)) {
        modalCitaTriggerElRef.focus();
    }
    modalCitaTriggerElRef = null;
};

window.openConfirmDeleteCita = (id, patientName, triggerEl) => {
    deleteCitaTriggerElRef = triggerEl || document.activeElement;
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

    setTimeout(() => {
        const cancelBtn = document.getElementById('modalConfirmDeleteCitaCancel');
        if (cancelBtn) cancelBtn.focus();
    }, 50);
};

window.closeConfirmDeleteCita = () => {
    const modal = document.getElementById('modalConfirmDeleteCita');
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    if (deleteCitaTriggerElRef && typeof deleteCitaTriggerElRef.focus === 'function' && document.contains(deleteCitaTriggerElRef)) {
        deleteCitaTriggerElRef.focus();
    }
    deleteCitaTriggerElRef = null;
};

// ════════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ════════════════════════════════════════════════════════════════════

async function fetchAppointmentsFromApi() {
    try {
        const params = new URLSearchParams({ page: '1', pageSize: String(API_PAGE_SIZE_LIMIT) });

        if (searchQueryText) params.set('search', searchQueryText);
        if (filterStatusValue) params.set('estado', filterStatusValue);
        if (filterProfessionalValue) params.set('profesional', filterProfessionalValue);
        if (filterDateValue) params.set('fecha', filterDateValue);

        const response = await fetch(`${API_BASE_URL}/citas?${params.toString()}`, {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                ...getAuthHeaders(),
                'Accept': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(`status ${response.status}`);
        }

        const payload = await response.json();
        const data = Array.isArray(payload?.data) ? payload.data.map(mapServerToClient) : [];
        appointmentsList = data;
        return data;
    } catch (error) {
        console.warn('[SmileTrack] No se pudo cargar citas desde /api/citas:', error);
        appointmentsList = [];
        const tableBody = safeGetElement('citasBody');
        if (tableBody) {
            tableBody.innerHTML = '<div class="empty-state" role="status">No hay citas para los filtros actuales</div>';
        }
        return [];
    }
}

const loadConfiguredDurationMinutes = async () => {
    try {
        const response = await fetch(`${API_BASE_URL}/citas?page=1&pageSize=1`, {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });
        if (!response.ok) return;
        const payload = await response.json();
        if (Number(payload.duracionMinutos) > 0) {
            configuredDurationMinutes = Number(payload.duracionMinutos);
        }
    } catch (error) {
        console.warn('[SmileTrack] No se pudo cargar la duración configurada:', error);
    }
};

const createInvoiceFromAppointment = async (id, buttonElement) => {
    buttonElement.disabled = true;
    try {
        const response = await fetch(`${API_BASE_URL}/facturas/desde-cita/${id}`, {
            method: 'POST',
            credentials: 'same-origin',
            headers: getInvoiceHeaders()
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(payload.message || 'No fue posible generar la factura.');
        if (window.ToastService) window.ToastService.success(payload.message || 'Factura generada correctamente.');
        appointmentsList = await fetchAppointmentsFromApi();
        renderAppointmentsTable();
    } catch (error) {
        if (window.ToastService) window.ToastService.error(error.message || 'No fue posible generar la factura.');
        buttonElement.disabled = false;
    }
};

// ════════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ════════════════════════════════════════════════════════════════════

const mapServerToClient = (serverData) => {
    const fechaHora = serverData.FechaHora ? new Date(serverData.FechaHora) : null;

    const year = fechaHora ? fechaHora.getFullYear() : 0;
    const month = fechaHora ? String(fechaHora.getMonth() + 1).padStart(2, '0') : '01';
    const day = fechaHora ? String(fechaHora.getDate()).padStart(2, '0') : '01';
    const hours = fechaHora ? String(fechaHora.getHours()).padStart(2, '0') : '09';
    const minutes = fechaHora ? String(fechaHora.getMinutes()).padStart(2, '0') : '00';

    const patientFullName = serverData.Paciente?.NombreCompleto || '—';
    const patientInitials = window.AppointmentUtils ? window.AppointmentUtils.getInitials(patientFullName) : 'XX';
    const patientColor = window.AppointmentUtils ? window.AppointmentUtils.pickColorByInitials(patientInitials) : 'blue';
    const documentId = serverData.IdPaciente ? `#${serverData.IdPaciente}` : '—';

    return {
        id: serverData.IdCita,
        date: `${year}-${month}-${day}`,
        time: `${hours}:${minutes}`,
        patient: patientFullName,
        doc: documentId,
        professional: (serverData.Profesional?.NombreCompleto || '')
            .toLowerCase().trim().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, ''),
        professionalName: serverData.Profesional?.NombreCompleto || 'Sin asignar',
        service: serverData.Servicio?.Nombre || 'Sin servicio',
        status: mapEstadoServerToClient(serverData.Estado),
        avatar: patientInitials,
        color: patientColor,
        _raw: serverData
    };
};

const mapEstadoServerToClient = (estado) => window.CommonUtils.mapEstadoServerToClient(estado);
const mapEstadoClientToServer = (estado) => window.CommonUtils.mapEstadoClienteToServer(estado);

const getFilteredAppointmentsList = () => {
    return appointmentsList.filter(appointment => {
        if (currentTabFilter === 'cancelled' && appointment.status !== 'cancelada') return false;
        if (currentTabFilter === 'no-show' && appointment.status !== 'no-show') return false;

        if (filterStatusValue && appointment.status !== filterStatusValue) return false;
        if (filterProfessionalValue && appointment.professional !== filterProfessionalValue) return false;
        if (filterDateValue && appointment.date !== filterDateValue) return false;

        if (searchQueryText) {
            const query = searchQueryText.toLowerCase();
            const patientMatch = appointment.patient.toLowerCase().includes(query);
            const docMatch = String(appointment.doc).toLowerCase().includes(query);

            if (!patientMatch && !docMatch) return false;
        }

        return true;
    });
};

const renderAppointmentsTable = () => {
    const tableBody = safeGetElement('citasBody');
    if (!tableBody || shouldUseServerRenderedList()) return;

    const filteredData = getFilteredAppointmentsList();

    if (!filteredData.length) {
        tableBody.innerHTML = '<div class="empty-state" role="status">No hay citas para los filtros actuales</div>';
        updatePaginationDisplay(0);
        return;
    }

    const startIndex = (currentPageNumber - 1) * DEFAULT_ITEMS_PER_PAGE;
    const pageData = filteredData.slice(startIndex, startIndex + DEFAULT_ITEMS_PER_PAGE);

    tableBody.innerHTML = pageData.map(appointment => {
        const status = statusLabelsProxy[appointment.status] || statusLabelsProxy.programada;
        const invoiceButton = ['atendida', 'completada', 'finalizada'].includes(String(status.label).toLowerCase())
            ? `<button class="action-btn btn-secondary btn-invoice" aria-label="Generar factura de ${escapeHtml(appointment.patient)}" data-id="${appointment.id}" title="Generar factura">
                  <span class="material-symbols-outlined action-icon" aria-hidden="true">receipt_long</span><span class="btn-text">Facturar</span>
               </button>`
            : '';

        return `
            <tr class="table-row" role="row" tabindex="0" aria-label="Cita de ${escapeHtml(appointment.patient)} el ${escapeHtml(formatDateLabel(appointment.date))}">
                <td class="col-fecha" role="cell" data-label="Fecha">
                    <time datetime="${escapeHtml(appointment.date)}">${escapeHtml(formatDateLabel(appointment.date))}</time>
                </td>
                <td class="col-hora" role="cell" data-label="Hora">
                    <time datetime="${escapeHtml(appointment.date)}T${escapeHtml(appointment.time)}:00">${escapeHtml(formatTime12hLabel(appointment.time))}</time>
                </td>
                <td class="col-paciente" role="cell" data-label="Paciente">
                    <div class="patient-info">
                        <div class="patient-avatar" style="background:${avatarColorCssMap[appointment.color] || avatarColorCssMap.blue}; color:#fff;" aria-hidden="true">
                            ${escapeHtml(appointment.avatar)}
                        </div>
                        <div>
                            <span class="patient-name">${escapeHtml(appointment.patient)}</span>
                            <span class="patient-id">ID: ${escapeHtml(appointment.doc)}</span>
                        </div>
                    </div>
                </td>
                <td class="col-profesional" role="cell" data-label="Profesional">
                    ${escapeHtml(appointment.professionalName)}
                </td>
                <td class="col-servicio" role="cell" data-label="Servicio">
                    ${escapeHtml(appointment.service)}
                </td>
                <td class="col-estado text-center" role="cell" data-label="Estado">
                    <span class="status-badge ${status.class}" role="status" aria-label="Estado: ${escapeHtml(status.label)}">
                        ${escapeHtml(status.label)}
                    </span>
                </td>
                <td class="col-acciones text-right" role="cell" data-label="Acciones">
                    <div class="actions-cell">
                        <button class="action-btn btn-secondary btn-view" aria-label="Ver detalle de cita de ${escapeHtml(appointment.patient)}" data-id="${appointment.id}" title="Ver detalle">
                          <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span> <span class="btn-text">Ver</span>
                        </button>
                        <a href="/gestion-de-citas/st-adm-09-citas?editId=${appointment.id}" class="action-btn btn-secondary btn-edit" aria-label="Editar cita de ${escapeHtml(appointment.patient)}" data-id="${appointment.id}" title="Editar cita">
                          <span class="material-symbols-outlined action-icon" aria-hidden="true">edit</span> <span class="btn-text">Editar</span>
                        </a>
                        ${invoiceButton}
                        <button class="action-btn btn-danger btn-delete" aria-label="Cancelar cita de ${escapeHtml(appointment.patient)}" data-id="${appointment.id}" title="Cancelar cita" onclick="openConfirmDeleteCita(${appointment.id}, '${escapeHtml(appointment.patient)}')">
                          <span class="material-symbols-outlined action-icon" aria-hidden="true">delete</span> <span class="btn-text">Eliminar</span>
                        </button>
                    </div>
                </td>
            </tr>
        `;
    }).join('');

    tableBody.querySelectorAll('.btn-view').forEach(button => {
        button.addEventListener('click', (event) => {
            openAppointmentModal(parseInt(event.currentTarget.dataset.id, 10), 'view');
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                openAppointmentModal(parseInt(event.currentTarget.dataset.id, 10), 'view');
            }
        });
    });

    tableBody.querySelectorAll('.btn-edit').forEach(button => {
        button.addEventListener('click', (event) => {
            openAppointmentModal(parseInt(event.currentTarget.dataset.id, 10), 'edit');
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                openAppointmentModal(parseInt(event.currentTarget.dataset.id, 10), 'edit');
            }
        });
    });

    tableBody.querySelectorAll('.btn-invoice').forEach(button => {
        button.addEventListener('click', () => createInvoiceFromAppointment(parseInt(button.dataset.id, 10), button));
    });

    tableBody.querySelectorAll('.btn-delete').forEach(button => {
        button.addEventListener('click', (event) => {
            cancelAppointment(parseInt(event.currentTarget.dataset.id, 10));
        });
        button.addEventListener('keydown', (event) => {
            if (['Enter', ' '].includes(event.key)) {
                event.preventDefault();
                cancelAppointment(parseInt(event.currentTarget.dataset.id, 10));
            }
        });
    });

    updatePaginationDisplay(filteredData.length);
};

const updatePaginationDisplay = (totalItems) => {
    const showing = Math.min(DEFAULT_ITEMS_PER_PAGE, Math.max(0, totalItems - (currentPageNumber - 1) * DEFAULT_ITEMS_PER_PAGE));
    const pageShowingElement = safeGetElement('pageShowing');
    const pageTotalElement = safeGetElement('pageTotal');

    if (pageShowingElement) pageShowingElement.textContent = showing;
    if (pageTotalElement) pageTotalElement.textContent = totalItems;
};

const updateSummaryStats = () => {
    if (shouldUseServerRenderedList()) return;

    const total = appointmentsList.length;
    const scheduled = appointmentsList.filter(appointment =>
        appointment.status === 'programada' || appointment.status === 'confirmada'
    ).length;
    const cancelled = appointmentsList.filter(appointment => appointment.status === 'cancelada').length;
    const attended = appointmentsList.filter(appointment => appointment.status === 'atendida').length;

    animateCounter(safeGetElement('citas-stat-total'), total);
    animateCounter(safeGetElement('citas-stat-programadas'), scheduled);
    animateCounter(safeGetElement('citas-stat-canceladas'), cancelled);
    animateCounter(safeGetElement('citas-stat-atendidas'), attended);
};

// ════════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ════════════════════════════════════════════════════════════════════

const validateCitaForm = (form) => {
    let valid = true;

    const requiredFields = [
        { field: form.querySelector('#idPacienteCita'), message: 'Selecciona un paciente.' },
        { field: form.querySelector('#idProfesionalCita'), message: 'Selecciona un profesional.' },
        { field: form.querySelector('#idConsultorioCita'), message: 'Selecciona un consultorio.' },
        { field: form.querySelector('#idServicioCita'), message: 'Selecciona un servicio.' },
        { field: form.querySelector('#fechaCita'), message: 'Selecciona una fecha válida.' },
        { field: form.querySelector('#horaInicioCita'), message: 'Selecciona una hora de inicio.' },
        { field: form.querySelector('#idEstadoCita'), message: 'Selecciona un estado.' }
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
                window.ValidationUtils.showError(field, null, message);
            }
        }
    });

    const fecha = form.querySelector('#fechaCita')?.value || '';
    const horaInicio = form.querySelector('#horaInicioCita')?.value || '';
    const horaFin = form.querySelector('#horaFinCita')?.value || '';

    if (window.AppointmentUtils && fecha && horaInicio && horaFin) {
        const errors = window.AppointmentUtils.validateAppointmentTime(fecha, horaInicio, horaFin);

        if (errors.length > 0) {
            errors.forEach(err => {
                if (err.field === 'general') {
                    if (window.ToastService) window.ToastService.warning('Horario inválido', err.message);
                    return;
                }

                let inputId = '';
                if (err.field === 'fecha') inputId = 'fechaCita';
                else if (err.field === 'horaInicio') inputId = 'horaInicioCita';
                else if (err.field === 'horaFin') inputId = 'horaFinCita';

                const inputEl = form.querySelector(`#${inputId}`);
                if (inputEl && window.ValidationUtils) {
                    window.ValidationUtils.showError(inputEl, null, err.message);
                }
            });

            valid = false;
        }
    }

    if (fecha && horaInicio) {
        const selectedDate = new Date(`${fecha}T${horaInicio}`);
        if (selectedDate < new Date()) {
            const fechaField = form.querySelector('#fechaCita');
            if (fechaField && window.ValidationUtils) {
                window.ValidationUtils.showError(fechaField, null, 'No puedes agendar una cita en un horario pasado.');
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
            window.ToastService.warning('Validación', 'Completa correctamente los campos obligatorios de la cita.');
        }

        const firstInvalid = form.querySelector('[aria-invalid="true"]');
        if (firstInvalid) firstInvalid.focus();
        return;
    }

    const submitButton = form.querySelector('#btnGuardarCita');
    if (submitButton) {
        submitButton.disabled = true;
        submitButton.textContent = 'Guardando...';
    }
};

const getAppointmentById = (id) => {
    return appointmentsList.find(appointment => Number(appointment.id) === Number(id));
};

const openAppointmentModal = (id, mode) => {
    const appointment = getAppointmentById(id);

    if (!appointment) {
        if (window.ToastService) window.ToastService.warning('Cita no encontrada');
        return;
    }

    const modalElement = safeGetElement('modalAppointment');
    const contentElement = safeGetElement('modalAppointmentContent');
    const titleElement = safeGetElement('modalAppointmentTitle');
    const editButton = safeGetElement('modalAppointmentEdit');

    if (!modalElement || !contentElement || !titleElement) return;

    if (mode === 'view') {
        titleElement.textContent = `Detalle: ${appointment.patient}`;
        if (editButton) editButton.style.display = 'none';

        contentElement.innerHTML = `
            <p><strong>Fecha:</strong> <time datetime="${escapeHtml(appointment.date)}">${escapeHtml(formatDateLabel(appointment.date))}</time></p>
            <p><strong>Hora:</strong> <time datetime="${escapeHtml(`${appointment.date}T${appointment.time}:00`)}">${escapeHtml(formatTime12hLabel(appointment.time))}</time></p>
            <p><strong>Documento / ID:</strong> ${escapeHtml(appointment.doc)}</p>
            <p><strong>Profesional:</strong> ${escapeHtml(appointment.professionalName)}</p>
            <p><strong>Servicio:</strong> ${escapeHtml(appointment.service)}</p>
            <p><strong>Estado:</strong> 
                <span class="status-badge ${statusLabelsProxy[appointment.status]?.class || 'programada'}">
                    ${escapeHtml(statusLabelsProxy[appointment.status]?.label || appointment.status)}
                </span>
            </p>
        `;
    } else {
        titleElement.textContent = `Editar: ${appointment.patient}`;
        if (editButton) {
            editButton.style.display = 'inline-flex';
            editButton.textContent = 'Guardar';
            editButton.onclick = () => saveAppointmentEdit(id);
        }

        contentElement.innerHTML = `
            <p>
                <strong>Fecha:</strong> 
                <input type="date" value="${escapeHtml(appointment.date)}" id="editDate" class="filter-date" style="margin-left:8px">
            </p>
            <p>
                <strong>Hora:</strong> 
                <input type="time" value="${escapeHtml(appointment.time)}" id="editTime" class="filter-select" style="margin-left:8px">
            </p>
            <p>
                <strong>Servicio:</strong> 
                <input type="text" value="${escapeHtml(appointment.service)}" id="editService" class="search-input" style="margin-left:8px;width:200px" placeholder="Nombre servicio">
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
    }

    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    const closeButton = safeGetElement('modalAppointmentClose');
    if (closeButton) closeButton.focus();
};

const closeAppointmentModal = () => {
    const modalElement = safeGetElement('modalAppointment');
    if (modalElement) {
        modalElement.classList.remove('open');
        modalElement.setAttribute('aria-hidden', 'true');
        modalElement.setAttribute('inert', '');
        document.body.style.overflow = '';
    }
};

const saveAppointmentEdit = (id) => {
    const appointment = getAppointmentById(id);
    if (!appointment) return;

    const rawData = appointment._raw || {};
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) {
        if (window.ToastService) window.ToastService.error('Error', 'No se pudo guardar la cita: token antiforgery no encontrado.');
        return;
    }

    const formElement = document.createElement('form');
    formElement.method = 'post';
    formElement.action = '/gestion-de-citas/guardar-cita';
    formElement.style.display = 'none';

    const fieldsMap = {
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

    Object.entries(fieldsMap).forEach(([name, value]) => {
        const inputHidden = document.createElement('input');
        inputHidden.type = 'hidden';
        inputHidden.name = name;
        inputHidden.value = String(value ?? '');
        formElement.appendChild(inputHidden);
    });

    document.body.appendChild(formElement);
    formElement.submit();
};

const cancelAppointment = (id, patientName) => {
    window.openConfirmDeleteCita(id, patientName || '');
};

const setupNewAppointmentModalTrigger = () => {
    const newAppointmentButton = safeGetElement('citas-btn-nueva') || safeGetElement('btnNewCita') || safeGetElement('btnNewAppointment');
    if (!newAppointmentButton) return;
    if (newAppointmentButton.hasAttribute('onclick')) return;
    newAppointmentButton.addEventListener('click', () => {
        if (typeof window.openModalCita === 'function') window.openModalCita();
    });
};

const setupEstadoConfirmationModal = () => {
    const statusSelects = document.querySelectorAll('.form-estado-inline select[name="Estado"]');

    statusSelects.forEach(select => {
        select.addEventListener('focus', function () {
            this.dataset.previousValue = this.value;
        });

        select.addEventListener('change', function () {
            const sel = this;
            const previousValue = sel.dataset.previousValue ?? sel.value;
            const newValue = sel.value;

            if (previousValue === newValue) return;

            const capitalize = s => s ? s.charAt(0).toUpperCase() + s.slice(1).toLowerCase() : s;

            if (window.ModalService) {
                window.ModalService.confirm({
                    title: 'Cambiar estado de cita',
                    message: `¿Confirmas cambiar el estado de "<strong>${capitalize(previousValue)}</strong>" a "<strong>${capitalize(newValue)}</strong>"?`,
                    confirmText: 'Sí, cambiar',
                    cancelText: 'Cancelar',
                    isDanger: false,
                    onConfirm: () => {
                        sel.value = newValue;
                        sel.form.submit();
                    }
                });
            }
            sel.value = previousValue;
        });
    });
};

const setupHoraCitaCalculator = () => {
    const horaInicio = document.getElementById('horaInicioCita');
    const horaFin = document.getElementById('horaFinCita');

    if (!horaInicio || !horaFin) return;

    const calcularHoraFin = () => {
        const valor = horaInicio.value;
        if (!valor) { horaFin.value = ''; return; }

        const [hora, minuto] = valor.split(':').map(Number);
        if (Number.isNaN(hora) || Number.isNaN(minuto)) { horaFin.value = ''; return; }

        const totalMinutos = (hora * 60) + minuto + configuredDurationMinutes;
        const resultado = totalMinutos % (24 * 60);

        const horaFinal = Math.floor(resultado / 60);
        const minutoFinal = resultado % 60;
        horaFin.value = `${String(horaFinal).padStart(2, '0')}:${String(minutoFinal).padStart(2, '0')}`;
    };

    const validarHorarioInline = () => {
        const fecha = document.getElementById('fechaCita')?.value || '';
        const errorEl = document.getElementById('error-horaInicioCita');
        if (!fecha || !horaInicio.value || !horaFin.value || !window.AppointmentUtils) return;

        const error = window.AppointmentUtils.validateAppointmentTime(fecha, horaInicio.value, horaFin.value)
            .find(item => item.field === 'horaInicio' || item.field === 'horaFin' || item.field === 'general');

        if (error) {
            horaInicio.setAttribute('aria-invalid', 'true');
            if (errorEl) { errorEl.textContent = error.message; errorEl.hidden = false; }
        } else {
            horaInicio.setAttribute('aria-invalid', 'false');
            if (errorEl) { errorEl.textContent = ''; errorEl.hidden = true; }
        }
    };

    horaInicio.addEventListener('input', () => { calcularHoraFin(); validarHorarioInline(); });
    horaInicio.addEventListener('change', () => { calcularHoraFin(); validarHorarioInline(); });
    horaFin.addEventListener('change', validarHorarioInline);
    document.getElementById('fechaCita')?.addEventListener('change', validarHorarioInline);

    calcularHoraFin();
};

const setupModalCitaEventListeners = () => {
    const modalElement = safeGetElement('modalCita');
    if (!modalElement) return;

    const citaForm = modalElement.querySelector('form');
    modalElement.addEventListener('click', (event) => {
        if (event.target === modalElement) window.closeModalCita();
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && modalElement.classList.contains('open')) {
            event.preventDefault();
            window.closeModalCita();
        }
    });

    citaForm?.addEventListener('submit', submitCitaForm);
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
        hamburgerButton.setAttribute('aria-expanded', String(showMenu));
        overlayElement.setAttribute('aria-hidden', String(!showMenu));

        if (showMenu) sidebarElement.querySelector('.nav-item')?.focus();
        else hamburgerButton.focus();
    };

    hamburgerButton.addEventListener('click', () => toggleMenuState(true));
    overlayElement.addEventListener('click', () => toggleMenuState(false));

    sidebarElement.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            if (window.innerWidth <= 680) toggleMenuState(false);
        });
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
            event.preventDefault();
            toggleMenuState(false);
        }
    });
};

const setupTabListeners = () => {
    if (shouldUseServerRenderedList()) return;

    const tabButtons = document.querySelectorAll('.tab-btn');
    tabButtons.forEach(tabButton => {
        tabButton.addEventListener('click', () => {
            tabButtons.forEach(button => {
                button.classList.remove('active');
                button.setAttribute('aria-selected', 'false');
            });

            tabButton.classList.add('active');
            tabButton.setAttribute('aria-selected', 'true');

            currentTabFilter = tabButton.dataset.tab;
            currentPageNumber = 1;
            renderAppointmentsTable();
        });
    });
};

const setupSearchInputListener = () => {
    if (shouldUseServerRenderedList()) return;
    const searchInput = safeGetElement('searchAppointments');

    searchInput?.addEventListener('input', debounce((event) => {
        searchQueryText = (event.target.value || '').toLowerCase();
        currentPageNumber = 1;
        renderAppointmentsTable();
    }, 250));
};

const setupFilterListeners = () => {
    if (shouldUseServerRenderedList()) return;

    safeGetElement('filterStatus')?.addEventListener('change', (event) => {
        filterStatusValue = event.target.value;
        currentPageNumber = 1;
        renderAppointmentsTable();
    });

    safeGetElement('filterProfessional')?.addEventListener('change', (event) => {
        filterProfessionalValue = event.target.value;
        currentPageNumber = 1;
        renderAppointmentsTable();
    });

    safeGetElement('filterDate')?.addEventListener('change', (event) => {
        filterDateValue = event.target.value;
        currentPageNumber = 1;
        renderAppointmentsTable();
    });
};

const initializeGestionIntegralModule = async () => {
    try {
        const useSSR = shouldUseServerRenderedList();

        setupSidebarNavigation();
        setupNewAppointmentModalTrigger();

        await loadConfiguredDurationMinutes();
        setupHoraCitaCalculator();
        setupModalCitaEventListeners();
        setupEstadoConfirmationModal();

        if (!useSSR) {
            setupTabListeners();
            setupSearchInputListener();
            setupFilterListeners();

            appointmentsList = await fetchAppointmentsFromApi();
            updateSummaryStats();
            renderAppointmentsTable();
        } else {
            document.querySelectorAll('[data-target]').forEach((element) => {
                const targetVal = parseInt(element.getAttribute('data-target'), 10);
                if (!Number.isNaN(targetVal) && targetVal >= 0) {
                    animateCounter(element, targetVal);
                }
            });
        }

    } catch (error) {
        console.error('[SmileTrack] Error inicializando módulo citas:', error);
        displayUserErrorMessage(error?.message || 'Error cargando el módulo de citas. Intente recargar.');
    }
};

document.addEventListener('DOMContentLoaded', initializeGestionIntegralModule);