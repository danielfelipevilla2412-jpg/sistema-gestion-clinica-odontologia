/* ============================================
SmileTrack — Gestión de Citas Recepción (st-rec-03-gestion-citas)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026 (actualizado 2026-07-31)

DESCRIPCIÓN:
Módulo principal de recepcionista. La API REST es la fuente de verdad del listado y las operaciones.

FUNCIONALIDADES PRINCIPALES:
- Carga de citas desde la API, sin persistencia local alternativa
- Filtros combinados (búsqueda texto, profesional, fecha, estado)
- CRUD UI: ver detalle y edición mediante formularios HTML a /gestion-de-citas/guardar-cita;
           cancelación mediante POST a /gestion-de-citas/eliminar-cita

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController → GuardarCita/EliminarCita para escritura (listado no expuesto vía API genérica)
- Endpoints: POST /gestion-de-citas/guardar-cita y POST /gestion-de-citas/eliminar-cita para guardado
- CSS: ~/css/Gestion_De_Citas/st-rec-03-gestion-citas/styles.css
- JS: ~/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los formatos de estado servidor↔UI están centralizados en STATUS_MAP_SERVER / STATUS_MAP_CLIENTE.
  (Cambiar la etiqueta visible al usuario = solo tocar esos 2 objetos).
- appointmentStorage mantiene únicamente la respuesta API actual en memoria.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API + AUTH
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const API_PAGE_SIZE = 10;
let configuredDurationMinutes = 60;
let currentApiPage = 1;
let totalApiRecords = 0;

const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* sessionStorage deshabilitado (modo privado) */ }
  return headers;
};

// Mapeos de estado (estándar en TODOS módulos citas)
const normalizeEstadoKey = (value) => {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .replace(/[áàäâ]/g, 'a')
    .replace(/[éèëê]/g, 'e')
    .replace(/[íìïî]/g, 'i')
    .replace(/[óòöô]/g, 'o')
    .replace(/[úùüû]/g, 'u')
    .replace(/[_\-\s]+/g, '_')
    .replace(/[^a-z0-9_]/g, '');
};

const STATUS_MAP_SERVER = {
  programada:  { label: 'Agendada',    cls: 'status-agendada' },
  agendada:    { label: 'Agendada',    cls: 'status-agendada' },
  confirmada:  { label: 'Confirmada',  cls: 'status-agendada' },
  en_proceso:  { label: 'En consulta', cls: 'status-consulta' },
  'en_proceso_': { label: 'En consulta', cls: 'status-consulta' },
  'en_proceso_1': { label: 'En consulta', cls: 'status-consulta' },
  finalizada:  { label: 'Atendida',    cls: 'status-atendida' },
  atendida:    { label: 'Atendida',    cls: 'status-atendida' },
  cancelada:   { label: 'Cancelada',   cls: 'status-no-asistio' },
  no_asistida: { label: 'No asistió',  cls: 'status-no-asistio' },
  'no_asistio': { label: 'No asistió', cls: 'status-no-asistio' }
};
const STATUS_MAP_CLIENTE = {
  'Agendada':    'programada',
  'Confirmada':  'confirmada',
  'En consulta': 'en_proceso',
  'Atendida':    'finalizada',
  'Cancelada':   'cancelada',
  'No asistió':  'no_asistida',
  'No asistio':  'no_asistida'
};
const STATUS_OPTIONS = Object.keys(STATUS_MAP_CLIENTE);

function mostrarErrorUsuario(mensaje) {
  let div = document.getElementById('smiletrack-error-bar');
  if (!div) {
    div = document.createElement('div');
    div.id = 'smiletrack-error-bar';
    div.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:99999;background:#dc2626;color:white;padding:14px 20px;text-align:center;font-family:system-ui,-apple-system,sans-serif;font-size:15px;box-shadow:0 4px 12px rgba(0,0,0,.15);border-bottom:3px solid #991b1b;';
    div.setAttribute('role', 'alert');
    document.body.appendChild(div);
  }
  div.replaceChildren();
  const strong = document.createElement('strong');
  strong.textContent = '[SmileTrack]';
  const message = document.createTextNode(` ${mensaje} `);
  const close = document.createElement('button');
  close.type = 'button';
  close.textContent = '×';
  close.setAttribute('aria-label', 'Cerrar mensaje');
  close.style.cssText = 'margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;';
  close.addEventListener('click', () => { div.style.display = 'none'; });
  div.append(strong, message, close);
  div.style.display = 'block';
}

const showToast = (message, type = 'info') => {
  const toast = window.ToastService;
  if (toast?.show) toast.show(message, type);
  else if (type === 'error') toast?.error?.(message);
  else toast?.success?.(message);
};

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => { clearTimeout(timeoutId); timeoutId = setTimeout(() => fn(...args), delay); };
};

const escapeHtml = (value) => String(value ?? '')
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#039;');


const shouldUseServerRenderedList = () => {
  return false;
};

const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

// Formato fecha: "20 mar"
const fmtFechaCorta = (fhIso) => {
  try {
    const d = new Date(fhIso);
    const meses = ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'];
    return `${d.getDate()} ${meses[d.getMonth()]}`;
  } catch { return '—'; }
};
const fmtHora12 = (fhIso) => {
  try {
    const d = new Date(fhIso);
    let h = d.getHours(); const m = String(d.getMinutes()).padStart(2, '0');
    const p = h >= 12 ? 'PM' : 'AM';
    if (h === 0) h = 12; else if (h > 12) h -= 12;
    return `${String(h).padStart(2,'0')}:${m} ${p}`;
  } catch { return '—'; }
};
const fmtHora24 = (fhIso) => {
  try {
    const d = new Date(fhIso);
    return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
  } catch { return '09:00'; }
};
const fmtFechaISO = (fhIso) => {
  try { return (new Date(fhIso)).toISOString().split('T')[0]; }
  catch { return new Date().toISOString().split('T')[0]; }
};

// ═══════════════════════════════════════════════════════════════════
//  MAPEOS SERVER → CLIENTE
// ═══════════════════════════════════════════════════════════════════

const mapServerToClient = (srv) => {
  const get = (name) => srv[name] ?? srv[name.charAt(0).toLowerCase() + name.slice(1)];
  const estadoRaw = get('Estado') || 'programada';
  const fechaHora = get('FechaHora');
  const pacienteRaw = srv.Paciente || srv.paciente;
  const profesionalRaw = srv.Profesional || srv.profesional;
  const servicioRaw = srv.Servicio || srv.servicio;
  const consultorioRaw = srv.Consultorio || srv.consultorio;
  const srvEstado = normalizeEstadoKey(estadoRaw);
  const info = STATUS_MAP_SERVER[srvEstado] || STATUS_MAP_SERVER.programada;
  const doctor = profesionalRaw?.NombreCompleto || profesionalRaw?.nombreCompleto || '—';
  const patient = pacienteRaw?.NombreCompleto || pacienteRaw?.nombreCompleto || '—';
  const service = servicioRaw?.Nombre || servicioRaw?.nombre || '—';
  const dateISO = fmtFechaISO(fechaHora);
  const hoy = new Date().toISOString().split('T')[0];
  const manana = (() => { const t = new Date(); t.setDate(t.getDate()+1); return t.toISOString().split('T')[0]; })();

  return {
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
  };
};

// ═══════════════════════════════════════════════════════════════════
//  ESTADO EFÍMERO DE LA RESPUESTA API ACTUAL
// ═══════════════════════════════════════════════════════════════════

let _appointments = [];

const appointmentStorage = {
  init: () => { _appointments = []; },
  getAll: () => [..._appointments],
  findById: (id) => _appointments.find(a => a.id === parseInt(id, 10)) || null,

  replaceAll: (nuevos) => {
    _appointments = Array.isArray(nuevos) ? nuevos : [];
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL MANAGER
// ═══════════════════════════════════════════════════════════════════

const modalManager = {
  open: (id) => {
    const m = safeGetElement(id);
    if (!m) return;
    m.classList.add('open');
    m.setAttribute('aria-hidden', 'false');
    m.removeAttribute('inert');
    const f = m.querySelector('input:not([type=hidden]), select, textarea, button:not(.modal-close)');
    if (f) f.focus();
    document.body.style.overflow = 'hidden';
  },
  close: (id) => {
    const m = safeGetElement(id);
    if (!m) return;
    m.classList.remove('open');
    m.setAttribute('aria-hidden', 'true');
    m.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  VALIDACIÓN CAMPOS
// ═══════════════════════════════════════════════════════════════════

const validateField = (input) => {
  const group = input.closest('.form-group');
  if (!group) return true;
  const err = group.querySelector('.error-message');
  let ok = true;
  const value = String(input.value || '').trim();
  if (input.required && !value) ok = false;
  else if (input.type === 'email' && value)
    ok = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
  else if (input.type === 'date' && value) {
    const s = new Date(value);
    const today = new Date();
    today.setHours(0,0,0,0);
    ok = s.getTime() >= today.getTime();
  }
  input.classList.toggle('error', !ok);
  if (err) err.classList.toggle('visible', !ok);
  input.toggleAttribute('aria-invalid', !ok);
  return ok;
};
const validateForm = (form) => {
  let ok = true;
  form.querySelectorAll('input[required], select[required], textarea[required]').forEach(i => {
    if (!validateField(i)) ok = false;
  });
  return ok;
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER TABLA
// ═══════════════════════════════════════════════════════════════════

const createAppointmentRow = (appt) => {
  const tr = document.createElement('tr');
  tr.dataset.id = appt.id;
  if (appt.highlight) tr.classList.add('row-highlight');
  if (appt.noShow) tr.classList.add('row-no-show');

  // Avatar de paciente: iniciales del nombre (máx. 2 letras)
  const parts = (appt.patient || '').trim().split(/\s+/).filter(Boolean);
  const initials = parts.length > 1
    ? (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    : (parts[0] || '?').slice(0, 2).toUpperCase();
  const PALETTE = ['blue', 'green', 'purple', 'orange', 'red'];
  let hash = 0;
  for (let i = 0; i < initials.length; i++) hash = ((hash << 5) - hash) + initials.charCodeAt(i);
  const avatarColor = PALETTE[Math.abs(hash) % PALETTE.length];

  tr.innerHTML = `
    <td class="col-fecha">${escapeHtml(appt.date)}</td>
    <td class="col-hora"><span class="pill-hora" aria-label="Hora: ${escapeHtml(appt.time)}">${escapeHtml(appt.time)}</span></td>
    <td class="col-paciente">
      <div class="td-paciente">
        <div class="pac-avatar pac-avatar--${avatarColor}" aria-hidden="true">${escapeHtml(initials)}</div>
        <span class="pac-name">${escapeHtml(appt.patient)}</span>
      </div>
    </td>
    <td class="col-profesional">${escapeHtml(appt.doctor)}</td>
    <td class="col-servicio">${escapeHtml(appt.service)}</td>
    <td class="col-consultorio">${escapeHtml(appt.office)}</td>
    <td><span class="status-badge ${appt.statusClass}" role="status" aria-label="Estado: ${escapeHtml(appt.status)}">${escapeHtml(appt.status)}</span></td>
    <td>
      <div class="actions-cell" role="group" aria-label="Acciones para ${escapeHtml(appt.patient)}">
        <button class="btn-icon action-btn btn-view" type="button"
                data-action="view" data-id="${appt.id}"
                aria-label="Ver detalles de ${escapeHtml(appt.patient)}"
                title="Ver detalles de ${escapeHtml(appt.patient)}">
          👁️ <span class="btn-text">Ver</span>
        </button>
        <button class="btn-icon action-btn edit" type="button"
                data-action="edit" data-id="${appt.id}"
                aria-label="Editar cita de ${escapeHtml(appt.patient)}"
                title="Editar cita de ${escapeHtml(appt.patient)}">
          ✏️ <span class="btn-text">Editar</span>
        </button>
        <button class="btn-icon action-btn btn-delete" type="button"
                data-action="cancel" data-id="${appt.id}"
                aria-label="Cancelar cita de ${escapeHtml(appt.patient)}"
                title="Cancelar cita de ${escapeHtml(appt.patient)}">
          ✕ <span class="btn-text">Cancelar</span>
        </button>
      </div>
    </td>
  `;
  return tr;
};

const renderAppointments = (data) => {
  const tbody = safeGetElement('appointmentsTable');
  if (!tbody) return;

  if (!data.length) {
    tbody.innerHTML = `<tr><td colspan="8" style="text-align:center;padding:24px;color:var(--text-muted);"><span aria-hidden="true">📅</span><br>No hay citas que coincidan con los filtros.</td></tr>`;
    updatePaginationInfo(0, 0, 0);
    return;
  }
  const frag = document.createDocumentFragment();
  data.forEach(a => frag.appendChild(createAppointmentRow(a)));
  tbody.innerHTML = '';
  tbody.appendChild(frag);
  const start = data.length ? ((currentApiPage - 1) * API_PAGE_SIZE) + 1 : 0;
  const end = Math.min(start + data.length - 1, totalApiRecords);
  updatePaginationInfo(start, end, totalApiRecords);
};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS
// ═══════════════════════════════════════════════════════════════════

const filterAppointments = () => {
  if (shouldUseServerRenderedList()) return;
  const q = safeGetElement('searchPatient')?.value.toLowerCase().trim() || '';
  const prof = safeGetElement('filterProfessional')?.value || '';
  const datePreset = safeGetElement('filterDate')?.value || '';
  const st = safeGetElement('filterStatus')?.value || '';

  const filtered = appointmentStorage.getAll().filter(a => {
    const matchQ = !q || a.patient.toLowerCase().includes(q)
      || a.doctor.toLowerCase().includes(q)
      || a.service.toLowerCase().includes(q);
    const matchProf = !prof || String(a.professionalId) === String(prof);
    const matchDate = !datePreset || a.dateISO === datePreset;
    const matchSt = !st || String(a._raw?.Estado || '').toLowerCase() === st.toLowerCase();
    return matchQ && matchProf && matchDate && matchSt;
  });

  renderAppointments(filtered);
};

// ═══════════════════════════════════════════════════════════════════
//  MÉTRICAS + PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

const initServerMetrics = () => {
  [['metricToday'], ['metricConfirmed'], ['metricPending'], ['metricCancelled']].forEach(([id]) => {
    const el = safeGetElement(id);
    if (!el) return;
    const target = parseInt(el.getAttribute('data-target') ?? '0', 10);
    if (!isNaN(target) && target > 0) animateCounter(el, target);
    else el.textContent = String(target ?? 0);
  });
};

const updateMetrics = () => {
  if (shouldUseServerRenderedList()) {
    initServerMetrics();
    return;
  }
  const all = appointmentStorage.getAll();
  const hoy = new Date().toISOString().split('T')[0];
  const todayCount = all.filter(a => a.dateISO === hoy).length;
  const confirmed = all.filter(a => a.status === 'Confirmada' || a.status === 'En consulta').length;
  const pending = all.filter(a => a.status === 'Agendada').length;
  const cancelled = all.filter(a => a.status === 'Cancelada' || a.status === 'No asistió').length;

  [['metricToday', todayCount], ['metricConfirmed', confirmed],
   ['metricPending', pending], ['metricCancelled', cancelled]].forEach(([id, v]) => {
    const el = safeGetElement(id);
    if (el) animateCounter(el, v);
  });
};

const updatePaginationInfo = (start, end, total) => {
  const el = safeGetElement('paginationInfo');
  if (el) el.textContent = total > 0 ? `Mostrando ${start}-${end} de ${total} citas` : 'Sin resultados';
};

// ═══════════════════════════════════════════════════════════════════
//  ACCIONES TABLA (VER / EDITAR / SINCRONIZAR / CANCELAR)
// ═══════════════════════════════════════════════════════════════════

const buildServerBody = (appt, overrides = {}) => {
  const raw = appt._raw || {};
  const fh = (overrides.dateISO || appt.dateISO) && (overrides.timeISO || appt.timeISO)
    ? `${overrides.dateISO || appt.dateISO}T${overrides.timeISO || appt.timeISO}:00`
    : raw.FechaHora || new Date().toISOString();
  const estadoUI = overrides.status || appt.status;
  const estadoServidor = STATUS_MAP_CLIENTE[estadoUI] ||
    (normalizeEstadoKey(estadoUI) === 'en_consulta' ? 'en_proceso' : normalizeEstadoKey(estadoUI) || 'programada');
  return {
    IdCita: appt.id,
    IdPaciente: raw.IdPaciente ?? raw.idPaciente ?? appt.patientId ?? 0,
    IdProfesional: raw.IdProfesional ?? raw.idProfesional ?? appt.professionalId ?? null,
    IdServicio: raw.IdServicio ?? raw.idServicio ?? appt.serviceId ?? 0,
    FechaHora: fh,
    Estado: estadoServidor,
    Notas: overrides.notes !== undefined ? overrides.notes : (appt.notes || '')
  };
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL CONFIRMACIÓN CANCELAR CITA (recepción)
// ═══════════════════════════════════════════════════════════════════

let _cancelTargetId = null;

const getCancelHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  // CSRF token desde cookie XSRF-TOKEN
  const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  if (match) headers['X-CSRF-TOKEN'] = decodeURIComponent(match[2]);
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch { /* modo privado */ }
  return headers;
};

const openCancelModal = (id) => {
  _cancelTargetId = parseInt(id, 10);
  const appt = appointmentStorage.findById(id);
  const patient = appt?.patient || `ID ${id}`;
  // Reutilizar el modal de confirmación genérico _ConfirmModal.cshtml si existe
  const modal = document.getElementById('confirmModal');
  if (modal) {
    const msgEl = modal.querySelector('#confirmModalMessage, .confirm-modal-message, p');
    if (msgEl) msgEl.textContent = `¿Estás seguro de que deseas cancelar la cita de ${patient}? Esta acción no se puede deshacer.`;
    const title = modal.querySelector('#confirmModalTitle, .confirm-modal-title, h2');
    if (title) title.textContent = 'Cancelar cita';
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    // Asociar botón de confirmación
    const confirmBtn = modal.querySelector('#confirmModalConfirm, .confirm-modal-confirm, [data-action="confirm"]');
    if (confirmBtn) {
      const fresh = confirmBtn.cloneNode(true);
      confirmBtn.replaceWith(fresh);
      fresh.addEventListener('click', executeCancelCita);
    }
    const cancelBtn = modal.querySelector('#confirmModalCancel, .confirm-modal-cancel, [data-action="cancel"]');
    if (cancelBtn) {
      const fresh = cancelBtn.cloneNode(true);
      cancelBtn.replaceWith(fresh);
      fresh.addEventListener('click', closeCancelModal);
    }
  } else {
    // Fallback: modal inline creado dinámicamente
    _openInlineCancelModal(patient);
  }
};

const closeCancelModal = () => {
  const modal = document.getElementById('confirmModal') || document.getElementById('inlineCancelModal');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  _cancelTargetId = null;
};

const _openInlineCancelModal = (patient) => {
  let modal = document.getElementById('inlineCancelModal');
  if (!modal) {
    modal = document.createElement('div');
    modal.id = 'inlineCancelModal';
    modal.className = 'modal-overlay';
    modal.setAttribute('role', 'dialog');
    modal.setAttribute('aria-modal', 'true');
    modal.setAttribute('aria-labelledby', 'inlineCancelTitle');
    modal.innerHTML = `
      <div class="modal modal--sm">
        <h2 class="modal-title" id="inlineCancelTitle">Cancelar cita</h2>
        <p class="modal-desc" id="inlineCancelMsg"></p>
        <div class="modal-footer">
          <button type="button" class="btn-secondary" id="inlineCancelNo">Volver</button>
          <button type="button" class="btn-danger" id="inlineCancelSi">Sí, cancelar</button>
        </div>
      </div>`;
    document.body.appendChild(modal);
    modal.addEventListener('click', e => { if (e.target === modal) closeCancelModal(); });
    document.getElementById('inlineCancelNo')?.addEventListener('click', closeCancelModal);
    document.getElementById('inlineCancelSi')?.addEventListener('click', executeCancelCita);
  }
  const msgEl = document.getElementById('inlineCancelMsg');
  if (msgEl) msgEl.textContent = `¿Estás seguro de que deseas cancelar la cita de ${patient}? Esta acción no se puede deshacer.`;
  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
};

const executeCancelCita = async () => {
  if (!_cancelTargetId) return;
  const id = _cancelTargetId;
  closeCancelModal();
  try {
    const res = await fetch(`${API_BASE}/citas/${id}`, {
      method: 'DELETE',
      credentials: 'same-origin',
      headers: getCancelHeaders()
    });
    let payload;
    try { payload = await res.json(); } catch { payload = { success: res.ok }; }
    if (res.ok && payload.success !== false) {
      showToast('Cita cancelada exitosamente.', 'success');
      // Recargar la tabla para reflejar el cambio
      const citas = await fetchAppointments(currentApiPage);
      renderAppointments(citas);
      updateMetrics();
    } else {
      showToast(payload.message || 'No fue posible cancelar la cita.', 'error');
    }
  } catch (err) {
    console.warn('[SmileTrack] Error al cancelar cita:', err);
    showToast('Error de conexión al cancelar la cita.', 'error');
  }
};

const handleTableAction = async (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;
  e.preventDefault();
  e.stopPropagation();
  const { action, id } = btn.dataset;

  if (action === 'view')   return openViewModal(id);
  if (action === 'edit')   return openEditModal(id, btn.dataset);
  if (action === 'cancel') return openCancelModal(id);
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

const openEditModal = (id) => {
  const a = appointmentStorage.findById(id);
  if (!a) return;

  const fields = {
    editAppointmentId: a.id,
    editPatient: a.patient,
    editDate: a.dateISO,
    editTime: a.timeISO,
    editDoctor: a.doctor,
    editService: a.service,
    editOffice: a.office,
    editStatus: a.status,
    editNotes: a.notes || ''
  };
  Object.entries(fields).forEach(([k, v]) => { const el = safeGetElement(k); if (el) el.value = v; });
  document.querySelectorAll('#modalEditAppointment .error').forEach(x => x.classList.remove('error'));
  document.querySelectorAll('#modalEditAppointment .error-message.visible').forEach(x => x.classList.remove('visible'));
  modalManager.open('modalEditAppointment');
};

const submitEditAppointment = (e) => {
  e.preventDefault();
  const form = e.currentTarget;
  if (!validateForm(form)) {
    window.ToastService.error('Por favor completa los campos requeridos.');
    return;
  }
  const submitBtn = form.querySelector('[type="submit"]');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Guardando...';
  }
  guardarCitaPorApi(form, true, submitBtn);
};


// ═══════════════════════════════════════════════════════════════════
//  HANDLERS MODALES + MÓVIL + FORM NUEVA CITA
// ═══════════════════════════════════════════════════════════════════

const initModalHandlers = () => {
  const map = {
    modalNewClose: 'modalNewAppointment', modalViewClose: 'modalViewAppointment',
    modalEditClose: 'modalEditAppointment',
    modalNewCancel: 'modalNewAppointment', modalViewCancel: 'modalViewAppointment',
    modalEditCancel: 'modalEditAppointment'
  };
  Object.entries(map).forEach(([btnId, mid]) => {
    safeGetElement(btnId)?.addEventListener('click', () => modalManager.close(mid));
  });
  document.querySelectorAll('.modal-overlay').forEach(o =>
    o.addEventListener('click', (e) => {
      if (e.target === o) modalManager.close(o.id);
    })
  );
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    document.querySelectorAll('.modal-overlay.open').forEach(m => modalManager.close(m.id));
  });
};

const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

const handleViewToggle = (btn) => {
  document.querySelectorAll('.view-toggle').forEach(t => {
    t.classList.remove('active');
    t.setAttribute('aria-selected', 'false');
    t.setAttribute('tabindex', '-1');
  });
  btn.classList.add('active');
  btn.setAttribute('aria-selected', 'true');
  btn.setAttribute('tabindex', '0');
  const view = btn.dataset.view;
  if (view === 'calendar') showToast('Vista de calendario próximamente disponible', 'info');
  if (view === 'paused')   showToast('Mostrando citas pausadas', 'info');
};

const initPagination = () => {
  safeGetElement('prevPage')?.addEventListener('click', (e) => {
    if (!e.currentTarget.disabled) showToast('Página anterior', 'info');
  });
  safeGetElement('nextPage')?.addEventListener('click', () => showToast('Página siguiente', 'info'));
  document.querySelectorAll('.pagination-number').forEach(b => {
    b.addEventListener('click', () => {
      document.querySelectorAll('.pagination-number').forEach(x => {
        x.classList.remove('active'); x.removeAttribute('aria-current');
      });
      b.classList.add('active'); b.setAttribute('aria-current', 'page');
      showToast(`Mostrando página ${b.textContent}`, 'info');
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FILTRADO DINÁMICO PROFESIONALES DISPONIBLES
// ═══════════════════════════════════════════════════════════════════

/**
 * Consulta /api/citas/profesionales-disponibles con la fecha y hora indicadas
 * y actualiza las opciones del SELECT #newDoctor en tiempo real.
 * Si la API falla o devuelve lista vacía, restaura la lista completa desde el HTML original.
 */
let _originalDoctorOptions = null;

const actualizarProfesionalesDisponibles = debounce(async () => {
  const dateInp = safeGetElement('newDate');
  const timeInp = safeGetElement('newTime');
  const doctorSel = safeGetElement('newDoctor');
  if (!dateInp || !timeInp || !doctorSel) return;

  const fecha = dateInp.value;
  const hora  = timeInp.value;
  if (!fecha || !hora) return;

  // Guardar opciones originales la primera vez
  if (!_originalDoctorOptions) {
    _originalDoctorOptions = doctorSel.innerHTML;
  }

  try {
    const params = new URLSearchParams({ fecha, horaInicio: hora });
    const res = await fetch(`${API_BASE}/citas/profesionales-disponibles?${params}`, {
      headers: { 'Accept': 'application/json' }
    });
    if (!res.ok) throw new Error(`status ${res.status}`);
    const payload = await res.json();
    const lista = payload.data ?? [];

    const prevVal = doctorSel.value;
    doctorSel.innerHTML = `<option value="">Seleccionar profesional${lista.length === 0 ? ' (sin disponibilidad)' : ''}</option>`;
    lista.forEach(p => {
      const opt = document.createElement('option');
      opt.value = p.idProfesional ?? p.IdProfesional ?? '';
      opt.textContent = p.nombreCompleto ?? p.NombreCompleto ?? p.nombre ?? '';
      doctorSel.appendChild(opt);
    });
    // Intentar mantener la selección previa si el profesional sigue disponible
    if (prevVal) doctorSel.value = prevVal;

    // Mostrar alerta de disponibilidad
    const alert = safeGetElement('availabilityAlert');
    if (alert) {
      const txt = alert.querySelector('.availability-alert-text');
      if (txt) txt.textContent = lista.length > 0
        ? `${lista.length} profesional(es) disponible(s) para el horario seleccionado`
        : 'Sin profesionales disponibles para el horario seleccionado';
      alert.style.display = '';
    }
  } catch (err) {
    console.warn('[SmileTrack] No se pudo consultar disponibilidad de profesionales:', err);
    window.ToastService?.error?.('No se pudo verificar la disponibilidad de profesionales. Intenta de nuevo.');
    // Restaurar lista original en caso de error
    if (_originalDoctorOptions) doctorSel.innerHTML = _originalDoctorOptions;
  }
}, 400);

const initNewAppointmentButtons = () => {
  const open = () => {
    const form = safeGetElement('formNewAppointment');
    if (form) {
      form.reset();
      form.querySelectorAll('input, select, textarea').forEach(input => input.classList.remove('error'));
      form.querySelectorAll('.error-message.visible').forEach(x => x.classList.remove('visible'));
      form.querySelectorAll('input, select, textarea').forEach(input => input.removeAttribute('aria-invalid'));
    }
    const dtInp = safeGetElement('newDate');
    if (dtInp) dtInp.min = new Date().toISOString().split('T')[0];
    // Ocultar alerta de disponibilidad al abrir
    const alert = safeGetElement('availabilityAlert');
    if (alert) alert.style.display = 'none';
    // Restaurar lista de profesionales completa
    _originalDoctorOptions = null;
    modalManager.open('modalNewAppointment');
  };
  safeGetElement('btnNuevaCita')?.addEventListener('click', open);
  safeGetElement('fabNuevaCita')?.addEventListener('click', open);

  // Filtrado dinámico: actualizar profesionales disponibles cuando cambian fecha u hora
  safeGetElement('newDate')?.addEventListener('change', actualizarProfesionalesDisponibles);
  safeGetElement('newTime')?.addEventListener('change', actualizarProfesionalesDisponibles);
};

const submitNewAppointment = (e) => {
  e.preventDefault();
  const form = e.currentTarget;
  if (!validateForm(form)) {
    window.ToastService.error('Por favor completa los campos requeridos.');
    return;
  }
  const submitBtn = form.querySelector('[type="submit"]');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Guardando...';
  }
  guardarCitaPorApi(form, false, submitBtn);
};

const horaFinDesdeConfiguracion = (horaInicio) => {
  const [hora, minuto] = horaInicio.split(':').map(Number);
  const total = (hora * 60) + minuto + configuredDurationMinutes;
  return `${String(Math.floor((total % 1440) / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
};

const guardarCitaPorApi = async (form, actualizar, submitBtn) => {
  const getValue = (selector) => form.querySelector(selector)?.value || '';
  const estado = form.querySelector('[name="IdEstado"] option:checked')?.textContent?.trim()
    || form.querySelector('[name="Estado"]')?.value
    || 'Programada';
  const fecha = getValue('[name="Fecha"]');
  const horaInicio = getValue('[name="HoraInicio"]');
  const token = getValue('input[name="__RequestVerificationToken"]');
  const body = {
    IdCita: actualizar ? Number(getValue('[name="IdCita"]')) : null,
    IdPaciente: Number(getValue('[name="IdPaciente"]')),
    IdProfesional: Number(getValue('[name="IdProfesional"]')),
    IdServicio: Number(getValue('[name="IdServicio"]')),
    IdConsultorio: Number(getValue('[name="IdConsultorio"]')),
    Fecha: fecha,
    HoraInicio: horaInicio,
    HoraFin: horaFinDesdeConfiguracion(horaInicio),
    Estado: estado,
    Notas: getValue('[name="MotivoConsulta"]') || getValue('[name="Notas"]')
  };

  try {
    const response = await fetch(`${API_BASE}/citas/agenda`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'X-CSRF-TOKEN': token },
      body: JSON.stringify(body)
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok || payload.success === false)
      throw new Error(payload.message || 'No fue posible guardar la cita.');
    window.ToastService?.success?.(actualizar ? 'Cita actualizada correctamente.' : 'Cita creada correctamente.');
    window.setTimeout(() => window.location.reload(), 300);
  } catch (error) {
    window.ToastService?.error?.(error.message || 'No fue posible guardar la cita.');
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = actualizar ? 'Actualizar cita' : 'Guardar cita';
    }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FETCH INICIAL
// ═══════════════════════════════════════════════════════════════════

async function fetchAppointments(page = 1) {
  try {
    const params = new URLSearchParams({ page: String(page), pageSize: String(API_PAGE_SIZE) });
    const search = safeGetElement('searchPatient')?.value.trim();
    const professional = safeGetElement('filterProfessional')?.value;
    const date = safeGetElement('filterDate')?.value;
    const status = safeGetElement('filterStatus')?.value;
    if (search) params.set('search', search);
    if (professional) params.set('profesional', professional);
    if (date) params.set('fecha', date);
    if (status) params.set('estado', status);

    const res = await fetch(`${API_BASE}/citas?${params.toString()}`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json', 'Accept': 'application/json' }
    });
    if (!res.ok) throw new Error(`status ${res.status}`);
    const payload = await res.json();
    configuredDurationMinutes = Number(payload.duracionMinutos) > 0 ? Number(payload.duracionMinutos) : 60;
    if (payload && payload.success && Array.isArray(payload.data)) {
      const citas = payload.data.map(mapServerToClient);
      currentApiPage = Number(payload.page) || page;
      totalApiRecords = Number(payload.total) || citas.length;
      appointmentStorage.replaceAll(citas);
      return citas;
    }
    throw new Error('payload inválido');
  } catch (err) {
    console.warn('[SmileTrack] No se pudo cargar citas desde /api/citas:', err);
    appointmentStorage.replaceAll([]);
    mostrarErrorUsuario('No fue posible consultar las citas. La lista está vacía hasta recuperar la conexión con la API.');
    return [];
  }
}

async function fetchConfiguredDuration() {
  try {
    const res = await fetch(`${API_BASE}/citas?page=1&pageSize=1`, {
      method: 'GET',
      credentials: 'same-origin',
      headers: { ...getAuthHeaders(), Accept: 'application/json' }
    });
    if (!res.ok) return;
    const payload = await res.json();
    if (Number(payload.duracionMinutos) > 0)
      configuredDurationMinutes = Number(payload.duracionMinutos);
  } catch (err) {
    console.warn('[SmileTrack] No se pudo cargar la duración configurada:', err);
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INIT PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  try {
    const useSSR = shouldUseServerRenderedList();
    appointmentStorage.init();
    initMobileMenu();
    initModalHandlers();
    initPagination();
    initNewAppointmentButtons();

    // Asignar submit handlers
    const newForm = safeGetElement('formNewAppointment');
    if (newForm) newForm.addEventListener('submit', submitNewAppointment);
    newForm?.querySelectorAll('input[required], select[required]').forEach(inp => {
      inp.addEventListener('blur', () => validateField(inp));
      inp.addEventListener('input', () => { if (inp.classList.contains('error')) validateField(inp); });
    });

    const editForm = safeGetElement('formEditAppointment');
    if (editForm) editForm.addEventListener('submit', submitEditAppointment);
    editForm?.querySelectorAll('input[required], select[required]').forEach(inp => {
      inp.addEventListener('blur', () => validateField(inp));
      inp.addEventListener('input', () => { if (inp.classList.contains('error')) validateField(inp); });
    });

    if (!useSSR) {
      // Carga inicial desde la API; no se usa persistencia local como respaldo.
      await fetchAppointments();
      updateMetrics();
      renderAppointments(appointmentStorage.getAll());

      // Filtros y tabs (solo en modo client-side; en SSR los filtros son GET al servidor)
      document.querySelectorAll('.view-toggle').forEach(btn => {
        btn.addEventListener('click', () => handleViewToggle(btn));
        btn.addEventListener('keydown', (e) => {
          if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleViewToggle(btn); }
        });
      });
      const refreshFromApi = async () => {
        const citas = await fetchAppointments(1);
        renderAppointments(citas);
        updateMetrics();
      };
      safeGetElement('searchPatient')?.addEventListener('input', debounce(refreshFromApi, 250));
      ['filterProfessional','filterDate','filterStatus'].forEach(id =>
        safeGetElement(id)?.addEventListener('change', refreshFromApi)
      );
      document.querySelector('.filter-bar')?.addEventListener('submit', (event) => {
        event.preventDefault();
        refreshFromApi();
      });
      document.querySelectorAll('.pagination-number, #prevPage, #nextPage').forEach(control => {
        control.addEventListener('click', async (event) => {
          event.preventDefault();
          const target = new URL(control.href, window.location.origin).searchParams.get('page');
          const requestedPage = Number(target);
          if (!Number.isInteger(requestedPage) || requestedPage < 1) return;
          const citas = await fetchAppointments(requestedPage);
          renderAppointments(citas);
          updateMetrics();
        });
      });
      const tbody = safeGetElement('appointmentsTable');
      if (tbody) {
        tbody.addEventListener('click', handleTableAction);
        tbody.addEventListener('keydown', (e) => {
          if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('[data-action]')) {
            e.preventDefault(); e.target.click();
          }
        });
      }
    } else {
      await fetchConfiguredDuration();
      // Modo SSR: animar los KPI renderizados por Razor con data-target
      initServerMetrics();
    }
  } catch (err) {
    console.error('[SmileTrack] Error init app.js (recepcionista):', err);
    mostrarErrorUsuario(err.message || 'Error cargando módulo de gestión de citas. Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', init);
