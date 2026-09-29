/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Mis Citas Paciente (st-pac-01-mis-citas)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-pac-01-mis-citas/mis-citas.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Interfaz interactiva del portal del paciente para la consulta de sus citas históricas y programadas.
 * Permite solicitar reagendamientos o solicitar cancelación de citas con validación anticipada.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Privacidad y Ownership estricto: El backend valida el claim de identidad del paciente en cada petición.
 * - Validación cliente de tiempo mínimo para cancelar citas (24 horas de anticipación requeridas).
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Stpac01MisCitas / CitasApiController
 * - HTML: Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const API_BASE_URL = '/api';
const API_PAGE_SIZE = 200;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

let currentFilterTab = 'all';
let appointmentsList = [];
let pendingCancelAppointmentId = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const fetchCsrfToken = () => {
  const requestToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (requestToken) return requestToken;
  const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  return match ? decodeURIComponent(match[2]) : null;
};

const buildAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  const csrfToken = fetchCsrfToken();
  if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* navegación privada */ }
  return headers;
};

function displayUserErrorMessage(errorMessage) {
  let errorBanner = document.getElementById('smiletrack-error-bar');
  if (!errorBanner) {
    errorBanner = document.createElement('div');
    errorBanner.id = 'smiletrack-error-bar';
    errorBanner.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:99999;background:#dc2626;color:white;padding:14px 20px;text-align:center;font-family:system-ui,-apple-system,sans-serif;font-size:15px;box-shadow:0 4px 12px rgba(0,0,0,.15);border-bottom:3px solid #991b1b;';
    errorBanner.setAttribute('role', 'alert');
    document.body.appendChild(errorBanner);
  }
  errorBanner.replaceChildren();
  const strongTag = document.createElement('strong');
  strongTag.textContent = '[SmileTrack]';
  const closeBtn = document.createElement('button');
  closeBtn.type = 'button';
  closeBtn.textContent = '×';
  closeBtn.setAttribute('aria-label', 'Cerrar mensaje de error');
  closeBtn.style.cssText = 'margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;';
  closeBtn.addEventListener('click', () => { errorBanner.style.display = 'none'; });
  errorBanner.append(strongTag, document.createTextNode(` ${errorMessage} `), closeBtn);
  errorBanner.style.display = 'block';
}

const safeGetElement = (elementId) => {
const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

const notifyUser = (type, title, message) => {
  if (window.ToastService && typeof window.ToastService[type] === 'function') {
    window.ToastService[type](title, message);
  } else {
    console[type === 'error' ? 'error' : 'log'](`[SmileTrack] ${title}${message ? ` - ${message}` : ''}`);
  }
};

const formatDateLabel = (isoDateString) => {
  try {
    const dateObj = new Date(isoDateString);
    const dayNames = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    const monthDay = String(dateObj.getDate()).padStart(2, '0');
    const monthNames = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
    return `${dayNames[dateObj.getDay()]} ${monthDay} ${monthNames[dateObj.getMonth()]}`;
  } catch { return '—'; }
};

const formatTimeLabel = (isoDateString) => {
  try {
    const dateObj = new Date(isoDateString);
    let hours = dateObj.getHours();
    const minutes = String(dateObj.getMinutes()).padStart(2, '0');
    const period = hours >= 12 ? 'PM' : 'AM';
    if (hours === 0) hours = 12; else if (hours > 12) hours -= 12;
    return `${String(hours).padStart(2, '0')}:${minutes} ${period}`;
  } catch { return '—'; }
};

const calculateHoursDifference = (fechaHoraISO) => {
  if (!fechaHoraISO) return -Infinity;
  const appointmentDate = new Date(fechaHoraISO);
  if (isNaN(appointmentDate.getTime())) return -Infinity;
  return (appointmentDate.getTime() - Date.now()) / 3_600_000;
};

const getBadgeClassForStatus = (statusLabel) => {
  const serverKey = CommonUtils.mapEstadoClienteToServer(statusLabel);
  return CommonUtils.getStatusInfo(serverKey).cls || 'badge-agendada';
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

async function fetchPatientAppointments() {
  try {
    const response = await fetch(`${API_BASE_URL}/citas?page=1&pageSize=${API_PAGE_SIZE}`, {
      method: 'GET',
      headers: buildAuthHeaders()
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    if (payload && payload.success && Array.isArray(payload.data)) {
      appointmentsList = payload.data.map(mapServerToClient);
      return;
    }
    throw new Error('payload inválido');
  } catch (err) {
    console.warn('[SmileTrack] Mis citas paciente: no se pudo cargar la API:', err);
    appointmentsList = [];
    displayUserErrorMessage('No fue posible consultar tus citas. La lista está vacía hasta recuperar la conexión con la API.');
  }
}

const loadAvailableServicesModalOptions = async () => {
  const serviceSelect = safeGetElement('citaServicio');
  if (!serviceSelect) return;
  try {
    const response = await fetch('/api/servicios', {
      headers: { 'Accept': 'application/json' }
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const payload = await response.json();
    const servicesList = payload.data ?? payload ?? [];
    if (!Array.isArray(servicesList) || servicesList.length === 0) throw new Error('catálogo vacío');

    serviceSelect.innerHTML = '<option value="">Selecciona un servicio</option>';
    servicesList.forEach(serviceItem => {
      const optionElement = document.createElement('option');
      optionElement.value = serviceItem.idServicio ?? serviceItem.IdServicio ?? '';
      optionElement.textContent = serviceItem.nombre ?? serviceItem.Nombre ?? '';
      serviceSelect.appendChild(optionElement);
    });
  } catch (err) {
    console.warn('[SmileTrack] No se pudieron cargar servicios desde API:', err);
    serviceSelect.innerHTML = '<option value="">No hay servicios disponibles</option>';
  }
};

const confirmAppointmentCancellation = async () => {
  if (!pendingCancelAppointmentId) return;
  const appointmentItem = appointmentsList.find(item => item.id === pendingCancelAppointmentId);
  if (!appointmentItem) return;

  const previousStatus = appointmentItem.estado;
  appointmentItem.estado = 'Cancelada';
  appointmentItem.active = false;
  updateAppointmentStats();
  renderAppointmentsTable();
  notifyUser('success', 'Éxito', 'Cita cancelada correctamente');

  let operationSuccess = false;
  let statusMessage = null;
  try {
    const response = await fetch(`${API_BASE_URL}/citas/${pendingCancelAppointmentId}`, {
      method: 'DELETE',
      headers: buildAuthHeaders()
    });
    let payload;
    try { payload = await response.json(); } catch { payload = { success: response.ok }; }
    operationSuccess = response.ok && payload.success;
    statusMessage = payload.message;
  } catch (err) {
    console.warn('[SmileTrack] Cancel cita offline paciente:', err);
    operationSuccess = false;
    statusMessage = 'No fue posible conectar con la API para cancelar la cita.';
  }

  if (operationSuccess) {
    closeCancelAppointmentModal();
  } else {
    appointmentItem.estado = previousStatus;
    appointmentItem.active = previousStatus === 'Agendada' || previousStatus === 'Confirmada';
    updateAppointmentStats();
    renderAppointmentsTable();
    const errorBox = safeGetElement('cancelarError');
    if (errorBox) errorBox.textContent = statusMessage || 'No fue posible cancelar la cita. Intente más tarde o contacte recepción.';
  }
};

const handleNewAppointmentSubmit = async () => {
  const dateInput = safeGetElement('citaFecha');
  const serviceSelect = safeGetElement('citaServicio');
  const noteTextarea = safeGetElement('citaNota');
  const confirmButton = safeGetElement('confirmarNuevaCita');

  if (!dateInput?.value) {
    dateInput?.focus();
    if (dateInput) { dateInput.style.borderColor = 'var(--orange)'; setTimeout(() => dateInput.style.borderColor = '', 2000); }
    notifyUser('warning', 'Falta la fecha', 'Por favor selecciona una fecha para la cita.');
    return;
  }

  const todayIsoString = new Date().toISOString().split('T')[0];
  if (dateInput.value < todayIsoString) {
    dateInput.value = todayIsoString;
    notifyUser('warning', 'Fecha inválida', 'No puedes solicitar citas en fechas pasadas.');
    return;
  }

  const requestBody = {
    Fecha: dateInput.value,
    IdServicio: serviceSelect?.value ? Number(serviceSelect.value) : 0,
    Notas: noteTextarea?.value?.trim() || null
  };

  if (confirmButton) { confirmButton.disabled = true; confirmButton.textContent = 'Enviando…'; }

  try {
    const headers = buildAuthHeaders();
    const response = await fetch('/api/citas/solicitar', {
      method: 'POST',
      credentials: 'same-origin',
      headers,
      body: JSON.stringify(requestBody)
    });

    let payload;
    try { payload = await response.json(); } catch { payload = { success: response.ok }; }

    if (response.ok && payload.success !== false) {
      const modalElement = safeGetElement('modalNuevaCita');
      if (modalElement) {
        modalElement.classList.remove('open');
        modalElement.setAttribute('aria-hidden', 'true');
        modalElement.setAttribute('inert', '');
      }
      notifyUser('success', '¡Solicitud enviada!',
        'Tu solicitud de cita fue recibida. El equipo de recepción la revisará y te confirmará los detalles en breve.');

      await fetchPatientAppointments();
      animateAppointmentCounters();
      updateAppointmentStats();
      renderAppointmentsTable();
    } else {
      const failureMsg = payload.message || 'No fue posible enviar tu solicitud. Inténtalo de nuevo.';
      notifyUser('error', 'Error al solicitar', failureMsg);
    }
  } catch (err) {
    console.warn('[SmileTrack] Error al solicitar cita:', err);
    notifyUser('error', 'Sin conexión', 'No fue posible conectar con el servidor. Verifica tu conexión y vuelve a intentarlo.');
  } finally {
    if (confirmButton) { confirmButton.disabled = false; confirmButton.textContent = 'Solicitar'; }
  }
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const mapServerToClient = (srv) => {
  const estadoServidor = srv.Estado ?? srv.estado ?? srv.EstadoCatalogo ?? srv.estadoCatalogo ?? '';
  const clientStatus = CommonUtils.mapEstadoServerToClient(estadoServidor);
  const statusInfo = CommonUtils.getStatusInfo(clientStatus);
  const fechaHora = srv.FechaHora ?? srv.fechaHora;
  const fechaHoraIso = fechaHora ? new Date(fechaHora).toISOString() : null;
  const isUpcoming = statusInfo.label === 'Agendada' || statusInfo.label === 'Confirmada';
  const todayIsoDate = new Date().toISOString().split('T')[0];
  const appointmentIsoDate = fechaHoraIso ? fechaHoraIso.split('T')[0] : todayIsoDate;

  const prof = srv.Profesional ?? srv.profesional ?? null;
  const doctorNombre = (
    prof?.NombreCompleto ||
    prof?.nombreCompleto ||
    (prof?.Nombres && prof?.Apellidos ? `${prof.Nombres} ${prof.Apellidos}`.trim() : null) ||
    (prof?.nombres && prof?.apellidos ? `${prof.nombres} ${prof.apellidos}`.trim() : null) ||
    prof?.Nombre ||
    prof?.nombre ||
    null
  ) || 'Profesional sin asignar';

  const svc = srv.Servicio ?? srv.servicio ?? null;
  const servicioNombre = (
    svc?.Nombre || svc?.nombre || null
  ) || 'Sin servicio';

  return {
    id: srv.IdCita ?? srv.idCita,
    fecha: formatDateLabel(fechaHoraIso),
    fechaISO: appointmentIsoDate,
    fechaHoraISO: fechaHoraIso || new Date().toISOString(),
    hora: formatTimeLabel(fechaHoraIso),
    doctor: doctorNombre,
    servicio: servicioNombre,
    estado: statusInfo.label,
    active: isUpcoming && appointmentIsoDate === todayIsoDate,
    _raw: srv
  };
};

const getFilteredAppointments = () => {
  const searchInput = safeGetElement('searchInput');
  const statusFilterSelect = safeGetElement('filterEstado');
  if (!searchInput || !statusFilterSelect) return appointmentsList;
  const queryText = searchInput.value.toLowerCase().trim();
  const selectedStatus = statusFilterSelect.value;

  return appointmentsList.filter(item => {
    const matchQuery = !queryText || item.doctor.toLowerCase().includes(queryText)
      || item.servicio.toLowerCase().includes(queryText)
      || item.fecha.toLowerCase().includes(queryText);
    const matchStatus = !selectedStatus || item.estado === selectedStatus;
    const matchTab = currentFilterTab === 'all'
      || (currentFilterTab === 'pending' && ['Agendada', 'Confirmada', 'En curso'].includes(item.estado))
      || (currentFilterTab === 'completed' && item.estado === 'Completada')
      || (currentFilterTab === 'cancelled' && ['Cancelada', 'No asistió'].includes(item.estado));
    return matchQuery && matchStatus && matchTab;
  });
};

const updateAppointmentStats = () => {
  const totalCount = appointmentsList.length;
  const completedCount = appointmentsList.filter(c => c.estado === 'Completada').length;
  const pendingCount = appointmentsList.filter(c => c.estado === 'Agendada' || c.estado === 'Confirmada' || c.estado === 'En curso').length;
  const cancelledCount = appointmentsList.filter(c => c.estado === 'Cancelada' || c.estado === 'No asistió').length;

  const totalEl = safeGetElement('cnt-total');
  const completedEl = safeGetElement('cnt-completadas');
  const pendingEl = safeGetElement('cnt-pendientes');
  const cancelledEl = safeGetElement('cnt-canceladas');
  if (totalEl) totalEl.textContent = totalCount;
  if (completedEl) completedEl.textContent = completedCount;
  if (pendingEl) pendingEl.textContent = pendingCount;
  if (cancelledEl) cancelledEl.textContent = cancelledCount;

  const progressBar = safeGetElement('progressBar');
  const progressLabel = safeGetElement('progressLabel');
  const percentage = totalCount > 0 ? Math.round((completedCount / totalCount) * 100) : 0;
  if (progressBar) {
    progressBar.style.width = percentage + '%';
    progressBar.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', percentage);
  }
  if (progressLabel) progressLabel.textContent = `${completedCount} de ${totalCount} citas completadas`;
};

const animateAppointmentCounters = () => {
  const totalCount = appointmentsList.length;
  const completedCount = appointmentsList.filter(c => c.estado === 'Completada').length;
  const pendingCount = appointmentsList.filter(c => c.estado === 'Agendada' || c.estado === 'Confirmada').length;
  const cancelledCount = appointmentsList.filter(c => c.estado === 'Cancelada').length;

  const nodes = [safeGetElement('cnt-total'), safeGetElement('cnt-completadas'), safeGetElement('cnt-pendientes'), safeGetElement('cnt-canceladas')];
  nodes.forEach(n => { if (n) delete n.dataset.animated; });
  if (typeof window.animateCounter === 'function') {
    window.animateCounter(safeGetElement('cnt-total'), totalCount);
    window.animateCounter(safeGetElement('cnt-completadas'), completedCount);
    window.animateCounter(safeGetElement('cnt-pendientes'), pendingCount);
    window.animateCounter(safeGetElement('cnt-canceladas'), cancelledCount);
  }
};

const renderAppointmentsTable = () => {
  const filteredData = getFilteredAppointments();
  const tableBody = safeGetElement('citasTbody');
  if (!tableBody) return;
  tableBody.innerHTML = '';

  const countLabel = safeGetElement('countLabel');
  if (countLabel) countLabel.textContent = `${filteredData.length} resultado${filteredData.length !== 1 ? 's' : ''}`;

  if (!filteredData.length) {
    const emptyRow = document.createElement('tr');
    const emptyCell = document.createElement('td');
    emptyCell.colSpan = 6;
    emptyCell.innerHTML = '<div class="empty-state"><span class="material-symbols-outlined" aria-hidden="true" style="font-size:2rem;color:var(--text-muted);display:block;margin-bottom:8px;">inbox</span><p>No hay citas que coincidan con los filtros.</p></div>';
    emptyRow.appendChild(emptyCell);
    tableBody.replaceChildren(emptyRow);
    return;
  }

  filteredData.forEach(item => {
    const tr = document.createElement('tr');
    if (item.active) tr.classList.add('row-active');
    if (item.estado === 'Cancelada') tr.classList.add('row-cancelada');
    const canCancel = item.estado === 'Agendada' || item.estado === 'Confirmada';
    tr.innerHTML = `
      <td class="td-fecha">${escapeHtml(item.fecha)}</td>
      <td><span class="pill-hora">${escapeHtml(item.hora)}</span></td>
      <td class="td-doctor">${escapeHtml(item.doctor)}</td>
      <td>${escapeHtml(item.servicio)}</td>
      <td><span class="badge ${escapeHtml(getBadgeClassForStatus(item.estado))}">${escapeHtml(item.estado)}</span></td>
      <td>
        <div class="actions-cell">
          <button class="btn-secondary btn-view" type="button" id="btn-ver-${item.id}"
                  title="Ver detalle" data-action="ver" data-id="${item.id}" aria-label="Ver detalle de cita">
            <span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">visibility</span> <span class="btn-text">Ver</span>
          </button>
          ${canCancel ? `<button class="btn-danger btn-delete" type="button" id="btn-cancelar-${item.id}"
                  title="Cancelar cita" data-action="cancelar" data-id="${item.id}" aria-label="Cancelar cita">
            <span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">cancel</span> <span class="btn-text">Cancelar</span>
          </button>` : ''}
        </div>
      </td>`;
    tableBody.appendChild(tr);
  });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const openAppointmentDetailModal = (appointmentId) => {
  const item = appointmentsList.find(c => c.id === appointmentId);
  if (!item) return;
  const modalContent = safeGetElement('modalContent');
  if (modalContent) {
    modalContent.innerHTML = `
      <div class="modal-row"><span class="modal-key">Fecha</span>   <span class="modal-val">${escapeHtml(item.fecha)}</span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>    <span class="modal-val">${escapeHtml(item.hora)}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${escapeHtml(item.doctor)}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${escapeHtml(item.servicio)}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>
        <span class="modal-val"><span class="badge ${escapeHtml(getBadgeClassForStatus(item.estado))}">${escapeHtml(item.estado)}</span></span>
      </div>`;
  }
  const modalOverlay = safeGetElement('modalOverlay');
  if (modalOverlay) {
    modalOverlay.dataset.opener = `btn-ver-${appointmentId}`;
    modalOverlay.classList.add('open');
    modalOverlay.setAttribute('aria-hidden', 'false');
    modalOverlay.removeAttribute('inert');
    safeGetElement('modalClose')?.focus();
  }
};

const closeAppointmentDetailModal = () => {
  const modalOverlay = safeGetElement('modalOverlay');
  if (!modalOverlay) return;
  modalOverlay.classList.remove('open');
  modalOverlay.setAttribute('aria-hidden', 'true');
  modalOverlay.setAttribute('inert', '');
  const openerId = modalOverlay.dataset.opener;
  if (openerId) safeGetElement(openerId)?.focus();
};

const openCancelAppointmentModal = (appointmentId) => {
  const item = appointmentsList.find(c => c.id === appointmentId);
  if (!item) return;

  const hoursRemaining = calculateHoursDifference(item.fechaHoraISO);
  const confirmBtn = safeGetElement('cancelarSi');
  const descriptionText = safeGetElement('cancelarDesc');
  const warningBox = safeGetElement('cancelarAdvertencia');
  const errorBox = safeGetElement('cancelarError');

  if (errorBox) errorBox.textContent = '';

  if (hoursRemaining < 24 && item.estado !== 'Cancelada' && item.estado !== 'Completada') {
    if (warningBox) warningBox.textContent = `⚠️ Esta cita es en ${Math.max(1, Math.round(hoursRemaining))} horas. La clínica recomienda cancelar con al menos 24h de anticipación.`;
    warningBox?.classList.remove('hidden');
  } else {
    warningBox?.classList.add('hidden');
  }

  if (item.estado === 'Completada' || item.estado === 'Cancelada' || item.estado === 'No asistió') {
    if (confirmBtn) confirmBtn.disabled = true;
    if (descriptionText) descriptionText.textContent = `No se puede cancelar: la cita está en estado "${item.estado}".`;
  } else if (hoursRemaining < 2) {
    if (confirmBtn) confirmBtn.disabled = true;
    if (descriptionText) descriptionText.textContent = `Cita en menos de 2 horas. Comuníquese con la clínica para cancelar.`;
  } else {
    if (confirmBtn) confirmBtn.disabled = false;
    if (descriptionText) descriptionText.textContent = `Cita del ${item.fecha} a las ${item.hora} — ${item.servicio} con ${item.doctor}`;
  }

  pendingCancelAppointmentId = appointmentId;
  const cancelModal = safeGetElement('modalCancelar');
  if (cancelModal) {
    cancelModal.dataset.opener = `btn-cancelar-${appointmentId}`;
    cancelModal.classList.add('open');
    cancelModal.setAttribute('aria-hidden', 'false');
    cancelModal.removeAttribute('inert');
    safeGetElement('cancelarSi')?.focus();
  }
};

const closeCancelAppointmentModal = () => {
  pendingCancelAppointmentId = null;
  const cancelModal = safeGetElement('modalCancelar');
  if (!cancelModal) return;
  cancelModal.classList.remove('open');
  cancelModal.setAttribute('aria-hidden', 'true');
  cancelModal.setAttribute('inert', '');
  const openerId = cancelModal.dataset.opener;
  if (openerId) safeGetElement(openerId)?.focus();
};

const setupNewAppointmentModal = () => {
  const newAppointmentBtn = safeGetElement('btnNuevaCita');
  const newAppointmentModal = safeGetElement('modalNuevaCita');
  if (!newAppointmentModal || !newAppointmentBtn) return;

  const dateInput = safeGetElement('citaFecha');
  if (dateInput) {
    dateInput.min = new Date().toISOString().split('T')[0];
  }

  const closeModalHandler = () => {
    newAppointmentModal.classList.remove('open');
    newAppointmentModal.setAttribute('aria-hidden', 'true');
    newAppointmentModal.setAttribute('inert', '');
    const openerId = newAppointmentModal.dataset.opener;
    if (openerId) safeGetElement(openerId)?.focus();
  };

  newAppointmentBtn.addEventListener('click', () => {
    newAppointmentModal.dataset.opener = 'btnNuevaCita';
    if (dateInput) dateInput.min = new Date().toISOString().split('T')[0];
    newAppointmentModal.classList.add('open');
    newAppointmentModal.setAttribute('aria-hidden', 'false');
    newAppointmentModal.removeAttribute('inert');
    dateInput?.focus();
    loadAvailableServicesModalOptions();
  });

  safeGetElement('closeNuevaCita')?.addEventListener('click', closeModalHandler);
  safeGetElement('cancelarNuevaCita')?.addEventListener('click', closeModalHandler);
  newAppointmentModal.addEventListener('click', event => { if (event.target === newAppointmentModal) closeModalHandler(); });

  dateInput?.addEventListener('change', () => {
    const todayIsoDate = new Date().toISOString().split('T')[0];
    if (dateInput.value < todayIsoDate) {
      dateInput.value = todayIsoDate;
      notifyUser('warning', 'Fecha inválida', 'No puedes seleccionar fechas pasadas. Se ha ajustado al día de hoy.');
    }
  });

  safeGetElement('confirmarNuevaCita')?.addEventListener('click', handleNewAppointmentSubmit);
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupTableActionListeners = () => {
  const tableBody = safeGetElement('citasTbody');
  if (!tableBody) return;
  tableBody.addEventListener('click', event => {
    const actionBtn = event.target.closest('[data-action]');
    if (!actionBtn) return;
    const actionType = actionBtn.dataset.action;
    const appointmentId = parseInt(actionBtn.dataset.id, 10);
    if (isNaN(appointmentId)) return;
    if (actionType === 'ver') openAppointmentDetailModal(appointmentId);
    else if (actionType === 'cancelar') openCancelAppointmentModal(appointmentId);
  });
};

const setupStatusTabListeners = () => {
  document.querySelectorAll('[data-tab]').forEach(tabElement => {
    tabElement.addEventListener('click', () => {
      currentFilterTab = tabElement.dataset.tab || 'all';
      document.querySelectorAll('[data-tab]').forEach(itemElement => {
        const isSelected = itemElement === tabElement;
        itemElement.classList.toggle('active', isSelected);
        itemElement.setAttribute('aria-selected', String(isSelected));
      });
      renderAppointmentsTable();
    });
  });
};

const initializeMisCitasModule = async () => {
  try {
    appointmentsList = [];
    animateAppointmentCounters();
    updateAppointmentStats();
    renderAppointmentsTable();

    setupNewAppointmentModal();
    setupTableActionListeners();
    setupStatusTabListeners();

    safeGetElement('filterEstado')?.addEventListener('change', renderAppointmentsTable);
    const searchInput = safeGetElement('searchInput');
    if (searchInput) searchInput.addEventListener('input', debounce(renderAppointmentsTable, 180));

    safeGetElement('modalClose')?.addEventListener('click', closeAppointmentDetailModal);
    safeGetElement('modalOverlay')?.addEventListener('click', event => {
      if (event.target.closest('#modalOverlay') === event.target) closeAppointmentDetailModal();
    });
    safeGetElement('cancelarNo')?.addEventListener('click', closeCancelAppointmentModal);
    safeGetElement('cancelarSi')?.addEventListener('click', confirmAppointmentCancellation);
    safeGetElement('modalCancelar')?.addEventListener('click', event => {
      if (event.target.closest('#modalCancelar') === event.target) closeCancelAppointmentModal();
    });

    document.addEventListener('keydown', event => {
      if (event.key !== 'Escape') return;
      const newAppointmentModal = safeGetElement('modalNuevaCita');
      const cancelModal = safeGetElement('modalCancelar');
      const detailModal = safeGetElement('modalOverlay');

      if (newAppointmentModal?.classList.contains('open')) {
        newAppointmentModal.classList.remove('open');
        newAppointmentModal.setAttribute('aria-hidden', 'true');
        newAppointmentModal.setAttribute('inert', '');
      } else if (cancelModal?.classList.contains('open')) {
        closeCancelAppointmentModal();
      } else if (detailModal?.classList.contains('open')) {
        closeAppointmentDetailModal();
      }
    });

    await fetchPatientAppointments();
    animateAppointmentCounters();
    updateAppointmentStats();
    renderAppointmentsTable();

    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
  } catch (err) {
    console.error('[SmileTrack] Error init mis-citas.js (paciente):', err);
    displayUserErrorMessage(err.message || 'Error cargando módulo "Mis Citas". Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', initializeMisCitasModule);
