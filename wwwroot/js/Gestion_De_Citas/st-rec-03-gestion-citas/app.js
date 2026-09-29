/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Gestión de Citas Recepción (st-rec-03-gestion-citas)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Interfaz operativa de agendamiento para recepcionistas. Consume endpoints REST y formularios MVC
 * para la asignación rápida de turnos, confirmación telefónica de asistencias y modificación de datos de citas.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtros combinados de búsqueda en tiempo real por paciente, profesional, fecha y estado.
 * - Sincronización de estados utilizando etiquetas de la utilidad compartida CommonUtils.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Strec03GestionCitas
 * - HTML: Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml
 * ============================================ */

// ===================================================================
// 1. CONSTANTES Y CONFIGURACIÓN API
// ===================================================================

const API_BASE_URL = '/api';
const DEFAULT_PAGE_SIZE = 10;

let configuredDurationMinutes = 60;
let currentPageIndex = 1;
let totalRecordCount = 0;
let cancelTargetAppointmentId = null;
let originalDoctorSelectOptions = null;

/**
 * Retorna las cabeceras de autorización HTTP predeterminadas.
 * @returns {Object}
 */
const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  try {
    const jwtToken = sessionStorage.getItem('st_jwt');
    if (jwtToken) headers['Authorization'] = `Bearer ${jwtToken}`;
  } catch (error) {
    /* sessionStorage no disponible */
  }
  return headers;
};

/**
 * Retorna las cabeceras requeridas para solicitudes de cancelación / CSRF.
 * @returns {Object}
 */
const getCancelHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  const csrfToken = typeof CommonUtils !== 'undefined' && CommonUtils.getCsrfToken
    ? CommonUtils.getCsrfToken()
    : (document.querySelector('input[name="__RequestVerificationToken"]')?.value ||
      (document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/)?.[2] ? decodeURIComponent(document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/)[2]) : ''));
  if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
  try {
    const jwtToken = sessionStorage.getItem('st_jwt');
    if (jwtToken) headers['Authorization'] = `Bearer ${jwtToken}`;
  } catch (error) {
    /* Modo privado */
  }
  return headers;
};

// ===================================================================
// 2. GESTIÓN DE ESTADO LOCAL
// ===================================================================

let localAppointmentsState = [];

const appointmentState = {
  init: () => { localAppointmentsState = []; },
  getAll: () => [...localAppointmentsState],
  findById: (appointmentId) => localAppointmentsState.find(item => item.id === parseInt(appointmentId, 10)) || null,
  replaceAll: (newAppointments) => {
    localAppointmentsState = Array.isArray(newAppointments) ? newAppointments : [];
  }
};

// ===================================================================
// 3. UTILIDADES Y FORMATEADORES DE DATOS
// ===================================================================

/**
 * Obtiene un elemento DOM por ID de forma segura.
 * @param {string} elementId
 * @returns {HTMLElement|null}
 */
const getElementByIdSafe = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (callbackFn, delayMs) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(callbackFn, delayMs) : callbackFn;

const escapeHtml = (inputValue) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(inputValue) : String(inputValue ?? '');

/**
 * Muestra un banner de error visual persistente al usuario.
 * @param {string} errorMessage
 */
const renderUserErrorMessage = (errorMessage) => {
  let errorBarElement = document.getElementById('smiletrack-error-bar');
  if (!errorBarElement) {
    errorBarElement = document.createElement('div');
    errorBarElement.id = 'smiletrack-error-bar';
    errorBarElement.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:99999;background:#dc2626;color:white;padding:14px 20px;text-align:center;font-family:system-ui,-apple-system,sans-serif;font-size:15px;box-shadow:0 4px 12px rgba(0,0,0,.15);border-bottom:3px solid #991b1b;';
    errorBarElement.setAttribute('role', 'alert');
    document.body.appendChild(errorBarElement);
  }
  errorBarElement.replaceChildren();

  const titlePrefix = document.createElement('strong');
  titlePrefix.textContent = '[SmileTrack]';
  const textNode = document.createTextNode(` ${errorMessage} `);
  const closeButton = document.createElement('button');
  closeButton.type = 'button';
  closeButton.textContent = '×';
  closeButton.setAttribute('aria-label', 'Cerrar mensaje');
  closeButton.style.cssText = 'margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;';
  closeButton.addEventListener('click', () => { errorBarElement.style.display = 'none'; });

  errorBarElement.append(titlePrefix, textNode, closeButton);
  errorBarElement.style.display = 'block';
};

/**
 * Muestra notificaciones tipo Toast usando el servicio global.
 * @param {string} messageText
 * @param {string} [toastType='info']
 */
const showToastNotification = (messageText, toastType = 'info') => {
  const toastService = window.ToastService;
  if (toastService?.show) toastService.show(messageText, toastType);
  else if (toastType === 'error') toastService?.error?.(messageText);
  else toastService?.success?.(messageText);
};

/**
 * Formatea una fecha ISO a formato corto (ej. "20 mar").
 * @param {string} isoDateString
 * @returns {string}
 */
const formatShortDate = (isoDateString) => {
  try {
    const dateObj = new Date(isoDateString);
    const monthAbbreviations = ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'];
    return `${dateObj.getDate()} ${monthAbbreviations[dateObj.getMonth()]}`;
  } catch (e) {
    return '—';
  }
};

/**
 * Formatea una fecha ISO a formato 12 horas (ej. "09:30 AM").
 * @param {string} isoDateString
 * @returns {string}
 */
const formatTime12Hour = (isoDateString) => {
  try {
    const dateObj = new Date(isoDateString);
    let hours = dateObj.getHours();
    const minutes = String(dateObj.getMinutes()).padStart(2, '0');
    const period = hours >= 12 ? 'PM' : 'AM';
    if (hours === 0) hours = 12;
    else if (hours > 12) hours -= 12;
    return `${String(hours).padStart(2, '0')}:${minutes} ${period}`;
  } catch (e) {
    return '—';
  }
};

/**
 * Formatea una fecha ISO a formato 24 horas (ej. "14:30").
 * @param {string} isoDateString
 * @returns {string}
 */
const formatTime24Hour = (isoDateString) => {
  try {
    const dateObj = new Date(isoDateString);
    return `${String(dateObj.getHours()).padStart(2, '0')}:${String(dateObj.getMinutes()).padStart(2, '0')}`;
  } catch (e) {
    return '09:00';
  }
};

/**
 * Formatea la fecha ISO recortada a "YYYY-MM-DD".
 * @param {string} isoDateString
 * @returns {string}
 */
const formatIsoDate = (isoDateString) => {
  try {
    return (new Date(isoDateString)).toISOString().split('T')[0];
  } catch (e) {
    return new Date().toISOString().split('T')[0];
  }
};

/**
 * Mapea el objeto cita retornado por el servidor al esquema plano del cliente.
 * @param {Object} serverAppointment
 * @returns {Object}
 */
const mapServerAppointmentToClient = (serverAppointment) => {
  const getProperty = (propName) => serverAppointment[propName] ?? serverAppointment[propName.charAt(0).toLowerCase() + propName.slice(1)];
  const rawStatus = getProperty('Estado') || 'programada';
  const rawDateTime = getProperty('FechaHora');
  const rawPatient = serverAppointment.Paciente || serverAppointment.paciente;
  const rawDoctor = serverAppointment.Profesional || serverAppointment.profesional;
  const rawService = serverAppointment.Servicio || serverAppointment.servicio;
  const rawOffice = serverAppointment.Consultorio || serverAppointment.consultorio;

  const clientStatus = CommonUtils.mapEstadoServerToClient(rawStatus);
  const statusInfo = CommonUtils.getStatusInfo(clientStatus);
  
  const doctorName = rawDoctor?.NombreCompleto || rawDoctor?.nombreCompleto || '—';
  const patientName = rawPatient?.NombreCompleto || rawPatient?.nombreCompleto || '—';
  const serviceName = rawService?.Nombre || rawService?.nombre || '—';
  
  const dateIsoString = formatIsoDate(rawDateTime);
  const todayIso = new Date().toISOString().split('T')[0];
  const tomorrowIso = (() => { const date = new Date(); date.setDate(date.getDate() + 1); return date.toISOString().split('T')[0]; })();

  return {
<<<<<<< Updated upstream
    id: get('IdCita'),
    patientId: get('IdPaciente'),
    professionalId: get('IdProfesional'),
    serviceId: get('IdServicio'),
    officeId: get('IdConsultorio'),
    date: fmtFechaCorta(fechaHora),
    dateISO,
    time: fmtHora12(fechaHora),
    timeISO: fmtHora24(fechaHora),
    patient,
    doctor,
    service,
    office: consultorioRaw?.Nombre || consultorioRaw?.nombre || '—',
    status: info.label,
    statusClass: info.cls,
    highlight: dateISO === hoy && info.label === 'En consulta',
    noShow: info.label === 'No asistió',
    notes: get('Notas') || '',
    // Helper para filtros predefinidos ('today' / 'tomorrow')
    _dateMatchPreset: { today: dateISO === hoy, tomorrow: dateISO === manana },
    _raw: srv,
    _durationMinutes: get('DuracionMinutos') || null
=======
    id: getProperty('IdCita'),
    patientId: getProperty('IdPaciente'),
    professionalId: getProperty('IdProfesional'),
    serviceId: getProperty('IdServicio'),
    officeId: getProperty('IdConsultorio'),
    statusId: getProperty('IdEstado') ?? serverAppointment.EstadoCita?.IdEstado ?? '',
    date: formatShortDate(rawDateTime),
    dateISO: dateIsoString,
    time: formatTime12Hour(rawDateTime),
    timeISO: formatTime24Hour(rawDateTime),
    patient: patientName,
    doctor: doctorName,
    service: serviceName,
    office: rawOffice?.Nombre || rawOffice?.nombre || '—',
    status: statusInfo.label,
    statusClass: statusInfo.cls,
    highlight: dateIsoString === todayIso && statusInfo.label === 'En consulta',
    noShow: statusInfo.label === 'No asistió',
    notes: getProperty('Notas') || '',
    _dateMatchPreset: { today: dateIsoString === todayIso, tomorrow: dateIsoString === tomorrowIso },
    _raw: serverAppointment,
    _durationMinutes: getProperty('DuracionMinutos') || null
>>>>>>> Stashed changes
  };
};

/**
 * Calcula la hora final a partir de la hora de inicio y la duración configurada.
 * @param {string} startTime
 * @returns {string}
 */
const calculateEndTimeFromDuration = (startTime) => {
  const [startHour, startMinute] = startTime.split(':').map(Number);
  const totalMinutes = (startHour * 60) + startMinute + configuredDurationMinutes;
  const endHour = Math.floor((totalMinutes % 1440) / 60);
  const endMinute = totalMinutes % 60;
  return `${String(endHour).padStart(2, '0')}:${String(endMinute).padStart(2, '0')}`;
};

// ===================================================================
// 4. COMUNICACIÓN CON LA API REST
// ===================================================================

/**
 * Consulta la lista paginada y filtrada de citas desde la API.
 * @param {number} [page=1]
 * @returns {Promise<Array>}
 */
const fetchAppointments = async (page = 1) => {
  try {
    const queryParams = new URLSearchParams({ page: String(page), pageSize: String(DEFAULT_PAGE_SIZE) });
    const searchKeyword = getElementByIdSafe('searchPatient')?.value.trim();
    const professionalId = getElementByIdSafe('filterProfessional')?.value;
    const selectedDate = getElementByIdSafe('filterDate')?.value;
    const selectedStatus = getElementByIdSafe('filterStatus')?.value;

    if (searchKeyword) queryParams.set('search', searchKeyword);
    if (professionalId) queryParams.set('profesional', professionalId);
    if (selectedDate) queryParams.set('fecha', selectedDate);
    if (selectedStatus) queryParams.set('estado', selectedStatus);

    const response = await fetch(`${API_BASE_URL}/citas?${queryParams.toString()}`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', 'Accept': 'application/json' }
    });

    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    
    configuredDurationMinutes = Number(payload.duracionMinutos) > 0 ? Number(payload.duracionMinutos) : 60;
    
    if (payload && payload.success && Array.isArray(payload.data)) {
      const mappedAppointments = payload.data.map(mapServerAppointmentToClient);
      currentPageIndex = Number(payload.page) || page;
      totalRecordCount = Number(payload.total) || mappedAppointments.length;
      appointmentState.replaceAll(mappedAppointments);
      return mappedAppointments;
    }
    throw new Error('payload inválido');
  } catch (error) {
    console.warn('[SmileTrack] No se pudo cargar citas desde /api/citas:', error);
    appointmentState.replaceAll([]);
    renderUserErrorMessage('No fue posible consultar las citas. La lista está vacía hasta recuperar la conexión con la API.');
    return [];
  }
};

/**
 * Consulta las solicitudes de citas pendientes por confirmar.
 * @returns {Promise<Array>}
 */
const fetchPendingAppointmentRequests = async () => {
  const listElement = getElementByIdSafe('solicitudesPendientesList');
  const badgeElement = getElementByIdSafe('solicitudesPendientesBadge');
  if (!listElement) return [];

  try {
    const response = await fetch(`${API_BASE_URL}/citas/solicitudes-pendientes`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'Accept': 'application/json' }
    });

    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    const pendingRequests = Array.isArray(payload?.data) ? payload.data : [];

    if (badgeElement) badgeElement.textContent = String(pendingRequests.length);

    if (!pendingRequests.length) {
      listElement.innerHTML = '<div class="empty-state" role="status" style="padding:1.25rem; color:#64748b;">No hay solicitudes pendientes por confirmar.</div>';
      return pendingRequests;
    }

    renderPendingRequestsList(listElement, pendingRequests);
    return pendingRequests;
  } catch (error) {
    console.warn('[SmileTrack] No se pudo cargar solicitudes pendientes:', error);
    if (listElement) listElement.innerHTML = '<div class="empty-state" role="status" style="padding:1.25rem; color:#64748b;">No fue posible cargar las solicitudes pendientes en este momento.</div>';
    return [];
  }
};

/**
 * Confirma la asignación de una solicitud pendiente.
 * @param {number} appointmentId
 * @param {Object} pendingRequestItem
 */
const confirmPendingRequestAction = async (appointmentId, pendingRequestItem) => {
  try {
    const requestDate = pendingRequestItem.fecha ? String(pendingRequestItem.fecha).slice(0, 10) : new Date().toISOString().slice(0, 10);
    const requestTime = pendingRequestItem.horaInicio ? String(pendingRequestItem.horaInicio).slice(0, 5) : '09:00';
    
    const professionalsResponse = await fetch(`${API_BASE_URL}/citas/profesionales-disponibles?fecha=${encodeURIComponent(requestDate)}&horaInicio=${encodeURIComponent(requestTime)}&idServicio=${encodeURIComponent(pendingRequestItem.servicio?.idServicio ?? 0)}`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'Accept': 'application/json' }
    });
    
    const professionalsPayload = await professionalsResponse.json().catch(() => ({ data: [] }));
    const availableProfessionals = Array.isArray(professionalsPayload.data) ? professionalsPayload.data : [];
    
    if (!availableProfessionals.length) {
      showToastNotification('No hay profesionales disponibles para confirmar esta solicitud. Intente otra fecha u horario.', 'error');
      return;
    }

    const officeId = document.querySelector('#newConsultorio')?.value || document.querySelector('#modalNewAppointment [name="IdConsultorio"]')?.value || 1;
    const confirmPayload = {
      idCita: appointmentId,
      idProfesional: Number(availableProfessionals[0].idProfesional ?? availableProfessionals[0].IdProfesional),
      idConsultorio: Number(officeId),
      fecha: requestDate,
      horaInicio: `${requestTime}:00`,
      notas: pendingRequestItem.notas || pendingRequestItem.motivoConsulta || ''
    };

    const response = await fetch(`${API_BASE_URL}/citas/${appointmentId}/confirmar-asignacion`, {
      method: 'PUT',
      credentials: 'same-origin',
      headers: {
        ...getAuthHeaders(),
        'X-CSRF-TOKEN': document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/)?.[2] ? decodeURIComponent(document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/)[2]) : '',
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify(confirmPayload)
    });

    const result = await response.json().catch(() => ({}));
    if (!response.ok || result.success === false) {
      throw new Error(result.message || 'No fue posible confirmar la solicitud.');
    }

    showToastNotification('Solicitud confirmada y cita asignada correctamente.', 'success');
    await fetchPendingAppointmentRequests();
    await fetchAppointments(1);
  } catch (error) {
    console.warn('[SmileTrack] Error confirmando solicitud pendiente:', error);
    showToastNotification(error.message || 'No fue posible confirmar la solicitud pendiente.', 'error');
  }
};

/**
 * Envía la creación o actualización de una cita a la API.
 * @param {HTMLFormElement} formElement
 * @param {boolean} isUpdating
 * @param {HTMLButtonElement} submitButton
 */
const saveAppointmentApi = async (formElement, isUpdating, submitButton) => {
  const getInputValue = (selector) => formElement.querySelector(selector)?.value || '';
  const statusName = formElement.querySelector('[name="IdEstado"] option:checked')?.textContent?.trim()
    || formElement.querySelector('[name="Estado"]')?.value
    || 'Programada';

  const appointmentDate = getInputValue('[name="Fecha"]');
  const startTime = getInputValue('[name="HoraInicio"]');
  const csrfToken = getInputValue('input[name="__RequestVerificationToken"]');

  const requestPayload = {
    IdCita: isUpdating ? Number(getInputValue('[name="IdCita"]')) : null,
    IdPaciente: Number(getInputValue('[name="IdPaciente"]')),
    IdProfesional: Number(getInputValue('[name="IdProfesional"]')),
    IdServicio: Number(getInputValue('[name="IdServicio"]')),
    IdConsultorio: Number(getInputValue('[name="IdConsultorio"]')),
    Fecha: appointmentDate,
    HoraInicio: startTime,
    HoraFin: calculateEndTimeFromDuration(startTime),
    Estado: statusName,
    Notas: getInputValue('[name="MotivoConsulta"]') || getInputValue('[name="Notas"]')
  };

  try {
    const response = await fetch(`${API_BASE_URL}/citas/agenda`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'X-CSRF-TOKEN': csrfToken },
      body: JSON.stringify(requestPayload)
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok || payload.success === false) {
      throw new Error(payload.message || 'No fue posible guardar la cita.');
    }

    showToastNotification(isUpdating ? 'Cita actualizada correctamente.' : 'Cita creada correctamente.', 'success');
    window.setTimeout(() => window.location.reload(), 300);
  } catch (error) {
    showToastNotification(error.message || 'No fue posible guardar la cita.', 'error');
    if (submitButton) {
      submitButton.disabled = false;
      submitButton.textContent = isUpdating ? 'Actualizar cita' : 'Guardar cita';
    }
  }
};

/**
 * Genera una factura para una cita finalizada/completada.
 * @param {number} appointmentId
 */
const generateInvoiceFromAppointment = async (appointmentId) => {
  const invoiceButton = document.querySelector(`[data-action="invoice"][data-id="${appointmentId}"]`);
  if (invoiceButton) invoiceButton.disabled = true;

  try {
    const headers = getCancelHeaders();
    if (!headers['X-CSRF-TOKEN']) {
      throw new Error('No se pudo validar la sesión. Recarga la página e inténtalo nuevamente.');
    }

    const response = await fetch(`${API_BASE_URL}/facturas/desde-cita/${appointmentId}`, {
      method: 'POST',
      credentials: 'same-origin',
      headers
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      const errorDetail = payload.message || (response.status === 403
        ? 'No tienes permisos para generar facturas.'
        : response.status === 400
          ? 'La cita no cumple las condiciones para facturación.'
          : `No fue posible generar la factura (HTTP ${response.status}).`);
      throw new Error(errorDetail);
    }

    showToastNotification(payload.message || 'Factura generada correctamente.', 'success');
    const updatedAppointments = await fetchAppointments(currentPageIndex);
    renderAppointmentsTable(updatedAppointments);
    updateMetricsCounters();

    window.setTimeout(() => {
      const generatedInvoiceId = payload.data?.id;
      const destinationUrl = generatedInvoiceId
        ? `/facturacion-y-pagos/st-adm-12-facturacion?facturaId=${encodeURIComponent(generatedInvoiceId)}`
        : '/facturacion-y-pagos/st-adm-12-facturacion';
      if (window.confirm('Factura creada. ¿Deseas abrir el módulo de facturación?')) {
        window.location.assign(destinationUrl);
      }
    }, 50);
  } catch (error) {
    showToastNotification(error.message || 'No fue posible generar la factura.', 'error');
    if (invoiceButton) invoiceButton.disabled = false;
  }
};

/**
 * Ejecuta la cancelación de la cita seleccionada.
 */
const executeAppointmentCancellation = async () => {
  if (!cancelTargetAppointmentId) return;
  const targetId = cancelTargetAppointmentId;
  closeCancelAppointmentModal();

  try {
    const response = await fetch(`${API_BASE_URL}/citas/${targetId}`, {
      method: 'DELETE',
      credentials: 'same-origin',
      headers: getCancelHeaders(),
      body: JSON.stringify({ motivo: 'Cancelada por recepción' })
    });

    let payload;
    try { payload = await response.json(); } catch { payload = { success: response.ok }; }

    if (response.ok && payload.success !== false) {
      showToastNotification('Cita cancelada exitosamente.', 'success');
      const updatedAppointments = await fetchAppointments(currentPageIndex);
      renderAppointmentsTable(updatedAppointments);
      updateMetricsCounters();
    } else {
      showToastNotification(payload.message || 'No fue posible cancelar la cita.', 'error');
    }
  } catch (error) {
    console.warn('[SmileTrack] Error al cancelar cita:', error);
    showToastNotification('Error de conexión al cancelar la cita.', 'error');
  }
};

// ===================================================================
// 5. MANIPULACIÓN DEL DOM Y RENDERIZADO DE LA TABLA
// ===================================================================

/**
 * Construye los botones de acción para cada fila de la tabla de citas.
 * @param {Object} appointmentItem
 * @returns {string}
 */
const renderRowActionButtons = (appointmentItem) => {
  const isCancelled = (appointmentItem.status || '').toLowerCase() === 'cancelada' || (appointmentItem.status || '').toLowerCase() === 'cancelado';
  const isBillable = ['atendida', 'completada', 'finalizada'].includes((appointmentItem.status || '').toLowerCase());

  const invoiceButtonHtml = isBillable ? `
    <button class="btn-secondary btn-invoice" type="button"
        data-action="invoice" data-id="${appointmentItem.id}"
        aria-label="Generar factura de ${escapeHtml(appointmentItem.patient)}"
        title="Generar factura">
      <span class="material-symbols-outlined" aria-hidden="true">receipt_long</span>
      <span class="btn-text">Facturar</span>
    </button>` : '';

  const cancelButtonHtml = isCancelled ? '' : `
    <button class="btn-danger btn-delete" type="button"
            data-action="cancel" data-id="${appointmentItem.id}"
            data-paciente="${escapeHtml(appointmentItem.patient)}"
            aria-label="Cancelar cita de ${escapeHtml(appointmentItem.patient)}"
            title="Cancelar cita de ${escapeHtml(appointmentItem.patient)}">
      ✕ <span class="btn-text">Cancelar</span>
    </button>`;

  return `
    <div class="actions-cell" role="group" aria-label="Acciones para ${escapeHtml(appointmentItem.patient)}">
      <button class="btn-secondary btn-view" type="button"
              data-action="view" data-id="${appointmentItem.id}"
              aria-label="Ver detalles de ${escapeHtml(appointmentItem.patient)}"
              title="Ver detalles de ${escapeHtml(appointmentItem.patient)}">
        👁️ <span class="btn-text">Ver</span>
      </button>
      <button class="btn-secondary edit" type="button"
              data-action="edit" data-id="${appointmentItem.id}"
              aria-label="Editar cita de ${escapeHtml(appointmentItem.patient)}"
              title="Editar cita de ${escapeHtml(appointmentItem.patient)}">
        ✏️ <span class="btn-text">Editar</span>
      </button>${invoiceButtonHtml}${cancelButtonHtml}
    </div>
  `;
};

/**
 * Crea la fila de la tabla HTML para una cita.
 * @param {Object} appointmentItem
 * @returns {HTMLTableRowElement}
 */
const createAppointmentTableRow = (appointmentItem) => {
  const tableRowElement = document.createElement('tr');
  tableRowElement.dataset.id = appointmentItem.id;
  if (appointmentItem.highlight) tableRowElement.classList.add('row-highlight');
  if (appointmentItem.noShow) tableRowElement.classList.add('row-no-show');

  const nameParts = (appointmentItem.patient || '').trim().split(/\s+/).filter(Boolean);
  const initials = nameParts.length > 1
    ? (nameParts[0][0] + nameParts[nameParts.length - 1][0]).toUpperCase()
    : (nameParts[0] || '?').slice(0, 2).toUpperCase();

  const colorPalette = ['blue', 'green', 'purple', 'orange', 'red'];
  let stringHash = 0;
  for (let i = 0; i < initials.length; i++) stringHash = ((stringHash << 5) - stringHash) + initials.charCodeAt(i);
  const avatarColor = colorPalette[Math.abs(stringHash) % colorPalette.length];

  tableRowElement.innerHTML = `
    <td class="col-fecha">${escapeHtml(appointmentItem.date)}</td>
    <td class="col-hora"><span class="pill-hora" aria-label="Hora: ${escapeHtml(appointmentItem.time)}">${escapeHtml(appointmentItem.time)}</span></td>
    <td class="col-paciente">
      <div class="td-paciente">
        <div class="pac-avatar pac-avatar--${avatarColor}" aria-hidden="true">${escapeHtml(initials)}</div>
        <span class="pac-name">${escapeHtml(appointmentItem.patient)}</span>
      </div>
    </td>
    <td class="col-profesional">${escapeHtml(appointmentItem.doctor)}</td>
    <td class="col-servicio">${escapeHtml(appointmentItem.service)}</td>
    <td class="col-consultorio">${escapeHtml(appointmentItem.office)}</td>
    <td><span class="status-badge ${appointmentItem.statusClass}" role="status" aria-label="Estado: ${escapeHtml(appointmentItem.status)}">${escapeHtml(appointmentItem.status)}</span></td>
    <td>${renderRowActionButtons(appointmentItem)}</td>
  `;

  return tableRowElement;
};

/**
 * Renderiza la lista de citas en la tabla.
 * @param {Array} appointmentList
 */
const renderAppointmentsTable = (appointmentList) => {
  const tableBodyElement = getElementByIdSafe('appointmentsTable');
  if (!tableBodyElement) return;

  if (!appointmentList.length) {
    tableBodyElement.innerHTML = `<tr><td colspan="8" style="text-align:center;padding:24px;color:var(--text-muted);"><span aria-hidden="true">📅</span><br>No hay citas que coincidan con los filtros.</td></tr>`;
    updatePaginationInfo(0, 0, 0);
    return;
  }

  const documentFragment = document.createDocumentFragment();
  appointmentList.forEach(item => documentFragment.appendChild(createAppointmentTableRow(item)));
  tableBodyElement.innerHTML = '';
  tableBodyElement.appendChild(documentFragment);

  const rangeStart = appointmentList.length ? ((currentPageIndex - 1) * DEFAULT_PAGE_SIZE) + 1 : 0;
  const rangeEnd = Math.min(rangeStart + appointmentList.length - 1, totalRecordCount);
  updatePaginationInfo(rangeStart, rangeEnd, totalRecordCount);
};

/**
 * Renderiza los elementos de solicitudes pendientes.
 * @param {HTMLElement} listElement
 * @param {Array} pendingRequests
 */
const renderPendingRequestsList = (listElement, pendingRequests) => {
  const requestItemsHtml = pendingRequests.map((solicitudItem) => {
    const formattedDate = solicitudItem?.fecha ? new Date(solicitudItem.fecha).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' }) : 'Sin fecha';
    const formattedTime = solicitudItem?.horaInicio ? String(solicitudItem.horaInicio).slice(0, 5) : '08:00';
    const patientName = solicitudItem?.paciente?.nombreCompleto || 'Paciente';
    const serviceName = solicitudItem?.servicio?.nombre || 'Servicio por confirmar';
    const notesText = solicitudItem?.notas ? escapeHtml(solicitudItem.notas) : 'Sin observaciones adicionales.';

    return `
      <article class="pending-request-item" style="display:flex; flex-wrap:wrap; gap:1rem; justify-content:space-between; align-items:center; padding:1rem 1.1rem; border:1px solid #e2e8f0; border-radius:12px; background:#f8fafc; margin-bottom:0.75rem;">
        <div>
          <strong style="display:block; font-size:0.98rem; margin-bottom:0.35rem;">${escapeHtml(patientName)}</strong>
          <small style="display:block; color:#475569;">${escapeHtml(formattedDate)} · ${escapeHtml(formattedTime)} · ${escapeHtml(serviceName)}</small>
          <small style="display:block; color:#64748b; margin-top:0.3rem;">${notesText}</small>
        </div>
        <button type="button" class="btn-primary" data-confirm-pending="${solicitudItem.idCita}" style="white-space:nowrap;">Confirmar</button>
      </article>
    `;
  }).join('');

  listElement.innerHTML = requestItemsHtml;

  listElement.querySelectorAll('[data-confirm-pending]').forEach((confirmButton) => {
    confirmButton.addEventListener('click', async () => {
      const appointmentId = Number(confirmButton.getAttribute('data-confirm-pending'));
      const pendingRequestItem = pendingRequests.find(item => Number(item.idCita) === appointmentId);
      if (!pendingRequestItem) return;
      await confirmPendingRequestAction(appointmentId, pendingRequestItem);
    });
  });
};

/**
 * Anima las métricas KPI renderizadas desde SSR.
 */
const animateServerMetrics = () => {
  [['metricToday'], ['metricConfirmed'], ['metricPending'], ['metricCancelled']].forEach(([metricId]) => {
    const metricElement = getElementByIdSafe(metricId);
    if (!metricElement) return;
    const targetValue = parseInt(metricElement.getAttribute('data-target') ?? '0', 10);
    if (!isNaN(targetValue) && targetValue > 0) animateCounter(metricElement, targetValue);
    else metricElement.textContent = String(targetValue ?? 0);
  });
};

/**
 * Recalcula y actualiza las métricas KPI de las citas.
 */
const updateMetricsCounters = () => {
  const allAppointments = appointmentState.getAll();
  const todayIso = new Date().toISOString().split('T')[0];
  
  const todayCount = allAppointments.filter(a => a.dateISO === todayIso).length;
  const confirmedCount = allAppointments.filter(a => a.status === 'Confirmada' || a.status === 'En consulta').length;
  const pendingCount = allAppointments.filter(a => a.status === 'Agendada').length;
  const cancelledCount = allAppointments.filter(a => a.status === 'Cancelada' || a.status === 'No asistió').length;

  [['metricToday', todayCount], ['metricConfirmed', confirmedCount],
   ['metricPending', pendingCount], ['metricCancelled', cancelledCount]].forEach(([elementId, countValue]) => {
    const targetElement = getElementByIdSafe(elementId);
    if (targetElement) animateCounter(targetElement, countValue);
  });
};

/**
 * Actualiza la información de la paginación.
 * @param {number} rangeStart
 * @param {number} rangeEnd
 * @param {number} totalCount
 */
const updatePaginationInfo = (rangeStart, rangeEnd, totalCount) => {
  const infoElement = getElementByIdSafe('paginationInfo');
  if (infoElement) infoElement.textContent = totalCount > 0 ? `Mostrando ${rangeStart}-${rangeEnd} de ${totalCount} citas` : 'Sin resultados';
};

// ===================================================================
// 6. GESTIÓN DE MODALES Y FORMULARIOS
// ===================================================================

const modalManager = {
  open: (modalId) => {
    const modalElement = getElementByIdSafe(modalId);
    if (!modalElement) return;
    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    const firstInput = modalElement.querySelector('input:not([type=hidden]), select, textarea, button:not(.modal-close)');
    if (firstInput) firstInput.focus();
    document.body.style.overflow = 'hidden';
  },
  close: (modalId) => {
    const modalElement = getElementByIdSafe(modalId);
    if (!modalElement) return;
    modalElement.classList.remove('open');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

/**
 * Valida un campo de entrada individual.
 * @param {HTMLInputElement|HTMLSelectElement} inputElement
 * @returns {boolean}
 */
const validateField = (inputElement) => {
  const formGroup = inputElement.closest('.form-group');
  if (!formGroup) return true;
  const errorLabel = formGroup.querySelector('.error-message');
  let isValid = true;
  const inputValue = String(inputElement.value || '').trim();

  if (inputElement.required && !inputValue) isValid = false;
  else if (inputElement.type === 'email' && inputValue)
    isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(inputValue);
  else if (inputElement.type === 'date' && inputValue) {
    const selectedDate = new Date(inputValue);
    const today = new Date();
    today.setHours(0,0,0,0);
    isValid = selectedDate.getTime() >= today.getTime();
  }

  inputElement.classList.toggle('error', !isValid);
  if (errorLabel) errorLabel.classList.toggle('visible', !isValid);
  inputElement.toggleAttribute('aria-invalid', !isValid);
  return isValid;
};

/**
 * Valida un formulario completo.
 * @param {HTMLFormElement} formElement
 * @returns {boolean}
 */
const validateForm = (formElement) => {
  let isFormValid = true;
  formElement.querySelectorAll('input[required], select[required], textarea[required]').forEach(input => {
    if (!validateField(input)) isFormValid = false;
  });
  return isFormValid;
};

/**
 * Abre el modal para ver los detalles de una cita.
 * @param {number} appointmentId
 */
const openViewAppointmentModal = (appointmentId) => {
  const appointmentItem = appointmentState.findById(appointmentId);
  if (!appointmentItem) return;

  const contentContainer = getElementByIdSafe('modalViewContent');
  if (contentContainer) {
    contentContainer.innerHTML = `
      <div class="modal-row"><span class="modal-key">Paciente</span>     <span class="modal-val">${escapeHtml(appointmentItem.patient)}</span></div>
      <div class="modal-row"><span class="modal-key">Fecha</span>        <span class="modal-val"><time datetime="${escapeHtml(appointmentItem.dateISO)}">${escapeHtml(appointmentItem.date)}</time></span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>         <span class="modal-val">${escapeHtml(appointmentItem.time)}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${escapeHtml(appointmentItem.doctor)}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span>     <span class="modal-val">${escapeHtml(appointmentItem.service)}</span></div>
      <div class="modal-row"><span class="modal-key">Consultorio</span>  <span class="modal-val">${escapeHtml(appointmentItem.office)}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>       <span class="modal-val"><span class="status-badge ${appointmentItem.statusClass}">${escapeHtml(appointmentItem.status)}</span></span></div>
      ${appointmentItem.notes ? `<div class="modal-row"><span class="modal-key">Notas</span><span class="modal-val">${escapeHtml(appointmentItem.notes)}</span></div>` : ''}
    `;
  }

  const editButton = getElementByIdSafe('modalViewEdit');
  if (editButton) {
    const newEditButton = editButton.cloneNode(true);
    editButton.replaceWith(newEditButton);
    newEditButton.addEventListener('click', () => {
      modalManager.close('modalViewAppointment');
      openEditAppointmentModal(appointmentId);
    });
  }

  modalManager.open('modalViewAppointment');
};

/**
 * Abre el modal para editar una cita.
 * @param {number} appointmentId
 */
const openEditAppointmentModal = (appointmentId) => {
  const appointmentItem = appointmentState.findById(appointmentId);
  const rawData = appointmentItem?._raw || {};

  const formFieldsMap = {
    editAppointmentId: appointmentItem?.id ?? rawData.IdCita ?? '',
    editPatient: appointmentItem?.patientId ?? rawData.IdPaciente ?? '',
    editDate: appointmentItem?.dateISO || rawData.Fecha || '',
    editTime: appointmentItem?.timeISO || rawData.HoraInicio || '',
    editDoctor: appointmentItem?.professionalId ?? rawData.IdProfesional ?? '',
    editService: appointmentItem?.serviceId ?? rawData.IdServicio ?? '',
    editOffice: appointmentItem?.officeId ?? rawData.IdConsultorio ?? '',
    editStatus: appointmentItem?.statusId ?? rawData.IdEstado ?? '',
    editNotes: appointmentItem?.notes ?? rawData.Notas ?? ''
  };

  Object.entries(formFieldsMap).forEach(([fieldId, fieldValue]) => {
    const inputElement = getElementByIdSafe(fieldId);
    if (!inputElement) return;
    inputElement.value = fieldValue ?? '';
  });

  document.querySelectorAll('#modalEditAppointment .error').forEach(element => element.classList.remove('error'));
  document.querySelectorAll('#modalEditAppointment .error-message.visible').forEach(element => element.classList.remove('visible'));
  modalManager.open('modalEditAppointment');
};

/**
 * Abre el modal para cancelar una cita.
 * @param {number} appointmentId
 * @param {Object} dataset
 */
const openCancelAppointmentModal = (appointmentId, dataset) => {
  cancelTargetAppointmentId = parseInt(appointmentId, 10);
  const appointmentItem = appointmentState.findById(appointmentId);
  const patientName = dataset?.paciente || appointmentItem?.patient || `ID ${appointmentId}`;

  const confirmModalElement = document.getElementById('confirmModal');
  if (confirmModalElement) {
    const messageElement = confirmModalElement.querySelector('#confirmModalMessage, .confirm-modal-message, p');
    if (messageElement) messageElement.textContent = `¿Estás seguro de que deseas cancelar la cita de ${patientName}? Esta acción no se puede deshacer.`;
    const titleElement = confirmModalElement.querySelector('#confirmModalTitle, .confirm-modal-title, h2');
    if (titleElement) titleElement.textContent = 'Cancelar cita';

    confirmModalElement.classList.add('open');
    confirmModalElement.setAttribute('aria-hidden', 'false');
    confirmModalElement.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    const confirmButton = confirmModalElement.querySelector('#confirmModalConfirm, .confirm-modal-confirm, [data-action="confirm"]');
    if (confirmButton) {
      const newConfirmButton = confirmButton.cloneNode(true);
      confirmButton.replaceWith(newConfirmButton);
      newConfirmButton.addEventListener('click', executeAppointmentCancellation);
    }

    const cancelButton = confirmModalElement.querySelector('#confirmModalCancel, .confirm-modal-cancel, [data-action="cancel"]');
    if (cancelButton) {
      const newCancelButton = cancelButton.cloneNode(true);
      cancelButton.replaceWith(newCancelButton);
      newCancelButton.addEventListener('click', closeCancelAppointmentModal);
    }
  } else {
    openInlineCancelModal(patientName);
  }
};

/**
 * Cierra el modal de confirmación de cancelación.
 */
const closeCancelAppointmentModal = () => {
  const modalElement = document.getElementById('confirmModal') || document.getElementById('inlineCancelModal');
  if (modalElement) {
    modalElement.classList.remove('open');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  cancelTargetAppointmentId = null;
};

/**
 * Despliega un modal inline para cancelar en caso de no existir el parcial _ConfirmModal.
 * @param {string} patientName
 */
const openInlineCancelModal = (patientName) => {
  let inlineModalElement = document.getElementById('inlineCancelModal');
  if (!inlineModalElement) {
    inlineModalElement = document.createElement('div');
    inlineModalElement.id = 'inlineCancelModal';
    inlineModalElement.className = 'modal-overlay';
    inlineModalElement.setAttribute('role', 'dialog');
    inlineModalElement.setAttribute('aria-modal', 'true');
    inlineModalElement.setAttribute('aria-labelledby', 'inlineCancelTitle');
    inlineModalElement.innerHTML = `
      <div class="modal modal--sm">
        <h2 class="modal-title" id="inlineCancelTitle">Cancelar cita</h2>
        <p class="modal-desc" id="inlineCancelMsg"></p>
        <div class="modal-footer">
          <button type="button" class="btn-secondary" id="inlineCancelNo">Volver</button>
          <button type="button" class="btn-danger" id="inlineCancelSi">Sí, cancelar</button>
        </div>
      </div>`;
    document.body.appendChild(inlineModalElement);
    inlineModalElement.addEventListener('click', e => { if (e.target === inlineModalElement) closeCancelAppointmentModal(); });
    document.getElementById('inlineCancelNo')?.addEventListener('click', closeCancelAppointmentModal);
    document.getElementById('inlineCancelSi')?.addEventListener('click', executeAppointmentCancellation);
  }

  const messageElement = document.getElementById('inlineCancelMsg');
  if (messageElement) messageElement.textContent = `¿Estás seguro de que deseas cancelar la cita de ${patientName}? Esta acción no se puede deshacer.`;
  
  inlineModalElement.classList.add('open');
  inlineModalElement.setAttribute('aria-hidden', 'false');
  inlineModalElement.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
};

// ===================================================================
// 7. LISTENERS DE EVENTOS E INICIALIZACIÓN
// ===================================================================

/**
 * Manejador de eventos delegado para la tabla de citas.
 * @param {Event} event
 */
const handleTableAction = async (event) => {
  const actionButton = event.target.closest('[data-action]');
  if (!actionButton) return;
  event.preventDefault();
  event.stopPropagation();

  const { action, id } = actionButton.dataset;

  if (action === 'view')    return openViewAppointmentModal(id);
  if (action === 'edit')    return openEditAppointmentModal(id, actionButton.dataset);
  if (action === 'cancel')  return openCancelAppointmentModal(id, actionButton.dataset);
  if (action === 'invoice') return generateInvoiceFromAppointment(id);
};

<<<<<<< Updated upstream
const handleTableAction = async (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;
  e.preventDefault();
  e.stopPropagation();
  const { action, id } = btn.dataset;

  if (action === 'view')   return openViewModal(id);
  if (action === 'edit')   return openEditModal(id, btn.dataset);
  if (action === 'cancel') return openCancelModal(id, btn.dataset);
  if (action === 'invoice') return createInvoiceFromAppointment(id);
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL VER DETALLE
// ═══════════════════════════════════════════════════════════════════

const openViewModal = (id) => {
  const a = appointmentStorage.findById(id);
  if (!a) return;
  const content = safeGetElement('modalViewContent');
  if (content) {
    content.innerHTML = `
      <div class="modal-row"><span class="modal-key">Paciente</span>     <span class="modal-val">${escapeHtml(a.patient)}</span></div>
      <div class="modal-row"><span class="modal-key">Fecha</span>        <span class="modal-val"><time datetime="${escapeHtml(a.dateISO)}">${escapeHtml(a.date)}</time></span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>         <span class="modal-val">${escapeHtml(a.time)}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${escapeHtml(a.doctor)}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span>     <span class="modal-val">${escapeHtml(a.service)}</span></div>
      <div class="modal-row"><span class="modal-key">Consultorio</span>  <span class="modal-val">${escapeHtml(a.office)}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>       <span class="modal-val"><span class="status-badge ${a.statusClass}">${escapeHtml(a.status)}</span></span></div>
      ${a.notes ? `<div class="modal-row"><span class="modal-key">Notas</span><span class="modal-val">${escapeHtml(a.notes)}</span></div>` : ''}
    `;
  }
  const editBtn = safeGetElement('modalViewEdit');
  if (editBtn) {
    const fresh = editBtn.cloneNode(true);
    editBtn.replaceWith(fresh);
    fresh.addEventListener('click', () => { modalManager.close('modalViewAppointment'); openEditModal(id); });
  }
  modalManager.open('modalViewAppointment');
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL EDITAR CITA (con PUT API)
// ═══════════════════════════════════════════════════════════════════

const openEditModal = (id, dataset) => {
  const a = appointmentStorage.findById(id);

  const pacienteVal = dataset?.pacienteId || a?.patientId || a?.patient || '';
  const profesionalVal = dataset?.profesionalId || a?.professionalId || a?.doctor || '';
  const servicioVal = dataset?.servicioId || a?.serviceId || a?.service || '';
  const consultorioVal = dataset?.consultorioId || a?.officeId || a?.office || '';
  const estadoVal = dataset?.estadoId || a?.status || '';
  const fechaVal = dataset?.fecha || a?.dateISO || '';
  const horaVal = dataset?.hora || a?.timeISO || '';
  const notasVal = dataset?.notas ?? a?.notes ?? dataset?.motivo ?? '';

  const idEl = safeGetElement('editAppointmentId');
  if (idEl) idEl.value = id;

  const selects = {
    editPatient: pacienteVal,
    editDoctor: profesionalVal,
    editService: servicioVal,
    editOffice: consultorioVal,
    editStatus: estadoVal
  };
  Object.entries(selects).forEach(([k, v]) => {
    const el = safeGetElement(k);
    if (el && v !== undefined && v !== null && v !== '') {
      el.value = String(v);
    }
  });

  const dateEl = safeGetElement('editDate');
  if (dateEl && fechaVal) dateEl.value = fechaVal;
  const timeEl = safeGetElement('editTime');
  if (timeEl && horaVal) timeEl.value = horaVal;
  const notesEl = safeGetElement('editNotes');
  if (notesEl) notesEl.value = notasVal;

  document.querySelectorAll('#modalEditAppointment .error').forEach(x => x.classList.remove('error'));
  document.querySelectorAll('#modalEditAppointment .error-message.visible').forEach(x => x.classList.remove('visible'));
  modalManager.open('modalEditAppointment');
};

const submitEditAppointment = (e) => {
  e.preventDefault();
  const form = e.currentTarget;
  if (!validateForm(form)) {
    window.ToastService.error('Por favor completa los campos requeridos.');
=======
/**
 * Maneja el submit de edición de cita.
 * @param {Event} event
 */
const submitEditAppointment = (event) => {
  event.preventDefault();
  const formElement = event.currentTarget;
  if (!validateForm(formElement)) {
    showToastNotification('Por favor completa los campos requeridos.', 'error');
>>>>>>> Stashed changes
    return;
  }
  const submitButton = formElement.querySelector('[type="submit"]');
  if (submitButton) {
    submitButton.disabled = true;
    submitButton.textContent = 'Guardando...';
  }
  saveAppointmentApi(formElement, true, submitButton);
};

/**
 * Maneja el submit de nueva cita.
 * @param {Event} event
 */
const submitNewAppointment = (event) => {
  event.preventDefault();
  const formElement = event.currentTarget;
  if (!validateForm(formElement)) {
    showToastNotification('Por favor completa los campos requeridos.', 'error');
    return;
  }
  const submitButton = formElement.querySelector('[type="submit"]');
  if (submitButton) {
    submitButton.disabled = true;
    submitButton.textContent = 'Guardando...';
  }
  saveAppointmentApi(formElement, false, submitButton);
};

/**
 * Actualiza las opciones del SELECT #newDoctor en tiempo real según disponibilidad.
 */
const updateAvailableProfessionalsOptions = debounce(async () => {
  const dateInput = getElementByIdSafe('newDate');
  const timeInput = getElementByIdSafe('newTime');
  const doctorSelect = getElementByIdSafe('newDoctor');
  if (!dateInput || !timeInput || !doctorSelect) return;

  const dateValue = dateInput.value;
  const timeValue = timeInput.value;
  if (!dateValue || !timeValue) return;

  if (!originalDoctorSelectOptions) {
    originalDoctorSelectOptions = doctorSelect.innerHTML;
  }

  try {
    const urlParams = new URLSearchParams({ fecha: dateValue, horaInicio: timeValue });
    const response = await fetch(`${API_BASE_URL}/citas/profesionales-disponibles?${urlParams}`, {
      headers: { 'Accept': 'application/json' }
    });

    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    const availableList = payload.data ?? [];

    const previousValue = doctorSelect.value;
    doctorSelect.innerHTML = `<option value="">Seleccionar profesional${availableList.length === 0 ? ' (sin disponibilidad)' : ''}</option>`;
    
    availableList.forEach(professional => {
      const optionElement = document.createElement('option');
      optionElement.value = professional.idProfesional ?? professional.IdProfesional ?? '';
      optionElement.textContent = professional.nombreCompleto ?? professional.NombreCompleto ?? professional.nombre ?? '';
      doctorSelect.appendChild(optionElement);
    });

    if (previousValue) doctorSelect.value = previousValue;

    const alertElement = getElementByIdSafe('availabilityAlert');
    if (alertElement) {
      const alertTextSpan = alertElement.querySelector('.availability-alert-text');
      if (alertTextSpan) {
        alertTextSpan.textContent = availableList.length > 0
          ? `${availableList.length} profesional(es) disponible(s) para el horario seleccionado`
          : 'Sin profesionales disponibles para el horario seleccionado';
      }
      alertElement.style.display = '';
    }
  } catch (error) {
    console.warn('[SmileTrack] No se pudo consultar disponibilidad de profesionales:', error);
    showToastNotification('No se pudo verificar la disponibilidad de profesionales. Intenta de nuevo.', 'error');
    if (originalDoctorSelectOptions) doctorSelect.innerHTML = originalDoctorSelectOptions;
  }
}, 400);

/**
 * Inicializa los manejadores para los modales de la interfaz.
 */
const initModalHandlers = () => {
  const modalButtonsMap = {
    modalNewClose: 'modalNewAppointment', modalViewClose: 'modalViewAppointment',
    modalEditClose: 'modalEditAppointment',
    modalNewCancel: 'modalNewAppointment', modalViewCancel: 'modalViewAppointment',
    modalEditCancel: 'modalEditAppointment'
  };

  Object.entries(modalButtonsMap).forEach(([buttonId, modalId]) => {
    getElementByIdSafe(buttonId)?.addEventListener('click', () => modalManager.close(modalId));
  });

  document.querySelectorAll('.modal-overlay').forEach(overlay =>
    overlay.addEventListener('click', (e) => {
      if (e.target === overlay) modalManager.close(overlay.id);
    })
  );

  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    document.querySelectorAll('.modal-overlay.open').forEach(modal => modalManager.close(modal.id));
  });
};

/**
 * Inicializa los botones de creación de nueva cita.
 */
const initNewAppointmentButtons = () => {
  const openNewAppointmentModal = () => {
    const formElement = getElementByIdSafe('formNewAppointment');
    if (formElement) {
      formElement.reset();
      formElement.querySelectorAll('input, select, textarea').forEach(input => input.classList.remove('error'));
      formElement.querySelectorAll('.error-message.visible').forEach(element => element.classList.remove('visible'));
      formElement.querySelectorAll('input, select, textarea').forEach(input => input.removeAttribute('aria-invalid'));
    }
    const dateInput = getElementByIdSafe('newDate');
    if (dateInput) dateInput.min = new Date().toISOString().split('T')[0];

    const alertElement = getElementByIdSafe('availabilityAlert');
    if (alertElement) alertElement.style.display = 'none';

    originalDoctorSelectOptions = null;
    modalManager.open('modalNewAppointment');
  };

  getElementByIdSafe('btnNuevaCita')?.addEventListener('click', openNewAppointmentModal);
  getElementByIdSafe('fabNuevaCita')?.addEventListener('click', openNewAppointmentModal);

  getElementByIdSafe('newDate')?.addEventListener('change', updateAvailableProfessionalsOptions);
  getElementByIdSafe('newTime')?.addEventListener('change', updateAvailableProfessionalsOptions);
};

/**
 * Función principal de inicialización.
 */
const init = async () => {
  try {
    appointmentState.init();
    initModalHandlers();
    initNewAppointmentButtons();

    const newFormElement = getElementByIdSafe('formNewAppointment');
    if (newFormElement) newFormElement.addEventListener('submit', submitNewAppointment);
    newFormElement?.querySelectorAll('input[required], select[required]').forEach(input => {
      input.addEventListener('blur', () => validateField(input));
      input.addEventListener('input', () => { if (input.classList.contains('error')) validateField(input); });
    });

    const editFormElement = getElementByIdSafe('formEditAppointment');
    if (editFormElement) editFormElement.addEventListener('submit', submitEditAppointment);
    editFormElement?.querySelectorAll('input[required], select[required]').forEach(input => {
      input.addEventListener('blur', () => validateField(input));
      input.addEventListener('input', () => { if (input.classList.contains('error')) validateField(input); });
    });

    const appointmentsList = await fetchAppointments();
    await fetchPendingAppointmentRequests();
    updateMetricsCounters();
    renderAppointmentsTable(appointmentState.getAll());

    const refreshFromApi = async () => {
      const updatedList = await fetchAppointments(1);
      await fetchPendingAppointmentRequests();
      renderAppointmentsTable(updatedList);
      updateMetricsCounters();
    };

    getElementByIdSafe('searchPatient')?.addEventListener('input', debounce(refreshFromApi, 250));
    ['filterProfessional', 'filterDate', 'filterStatus'].forEach(elementId =>
      getElementByIdSafe(elementId)?.addEventListener('change', refreshFromApi)
    );

    document.querySelector('.filter-bar')?.addEventListener('submit', (event) => {
      event.preventDefault();
      refreshFromApi();
    });

    const tableBodyElement = getElementByIdSafe('appointmentsTable');
    if (tableBodyElement) {
      tableBodyElement.addEventListener('click', handleTableAction);
      tableBodyElement.addEventListener('keydown', (e) => {
        if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('[data-action]')) {
          e.preventDefault(); e.target.click();
        }
      });
    }
  } catch (error) {
    console.error('[SmileTrack] Error en la inicialización de st-rec-03 app.js:', error);
    renderUserErrorMessage(error.message || 'Error cargando módulo de gestión de citas. Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', init);
