/* ============================================
SmileTrack — Gestión de Citas Recepción (st-rec-03-gestion-citas)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026 (actualizado 2026-07-31)

DESCRIPCIÓN:
Módulo principal de recepcionista. Carga la UI de citas con datos renderizados por SSR o fallback
a LocalStorage cuando no hay un endpoint de listado disponible, y escribe cambios de alta/edición/cancelación
mediante los controladores MVC existentes, no mediante API REST PUT/DELETE remotos directos.

FUNCIONALIDADES PRINCIPALES:
- Carga de citas renderizadas por servidor o fallback LocalStorage cuando la API de listado no está disponible
- Filtros combinados (búsqueda texto, profesional, fecha, estado)
- CRUD UI: ver detalle y edición mediante formularios HTML a /gestion-de-citas/guardar-cita;
           cancelación mediante POST a /gestion-de-citas/eliminar-cita
- Persistencia LocalStorage transparente como fallback offline

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController → GuardarCita/EliminarCita para escritura (listado no expuesto vía API genérica)
- Endpoints: POST /gestion-de-citas/guardar-cita y POST /gestion-de-citas/eliminar-cita para guardado
- CSS: ~/css/Gestion_De_Citas/st-rec-03-gestion-citas/styles.css
- JS: ~/js/Gestion_De_Citas/st-rec-03-gestion-citas/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los formatos de estado servidor↔UI están centralizados en STATUS_MAP_SERVER / STATUS_MAP_CLIENTE.
  (Cambiar la etiqueta visible al usuario = solo tocar esos 2 objetos).
- appointmentStorage usa fallback LocalStorage SIEMPRE. El fetch a la API sobrescribe el cache.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API + AUTH
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const API_PAGE_SIZE = 200;
const STORAGE_KEY = 'smiletrack_rec_appointments';

const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* sessionStorage deshabilitado (modo privado) */ }
  return headers;
};

// Mapeos de estado (estándar en TODOS módulos citas)
const STATUS_MAP_SERVER = {
  programada:  { label: 'Agendada',    cls: 'status-agendada'  },
  confirmada:  { label: 'Confirmada',  cls: 'status-agendada'  },
  en_proceso:  { label: 'En consulta', cls: 'status-consulta'  },
  finalizada:  { label: 'Atendida',    cls: 'status-atendida'  },
  atendida:    { label: 'Atendida',    cls: 'status-atendida'  },
  cancelada:   { label: 'Cancelada',   cls: 'status-no-asistio'},
  no_asistida: { label: 'No asistió',  cls: 'status-no-asistio'}
};
const STATUS_MAP_CLIENTE = {
  'Agendada':    'programada',
  'Confirmada':  'confirmada',
  'En consulta': 'en_proceso',
  'Atendida':    'finalizada',
  'Cancelada':   'cancelada',
  'No asistió':  'no_asistida'
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
  div.innerHTML = '<strong>[SmileTrack]</strong> ' + mensaje + ' <button onclick="document.getElementById(\'smiletrack-error-bar\').style.display=\'none\'" style="margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;">×</button>';
  div.style.display = 'block';
}

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


const shouldUseServerRenderedList = () => {
  const tbody = safeGetElement('appointmentsTable');
  return !!(tbody && tbody.children.length > 0 && tbody.querySelector('tr'));
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
  const srvEstado = (srv.Estado || 'programada').toLowerCase();
  const info = STATUS_MAP_SERVER[srvEstado] || STATUS_MAP_SERVER['programada'];
  const doctor = srv.Profesional?.NombreCompleto || '—';
  const patient = srv.Paciente?.NombreCompleto || '—';
  const service = srv.Servicio?.Nombre || '—';
  const dateISO = fmtFechaISO(srv.FechaHora);
  const hoy = new Date().toISOString().split('T')[0];
  const manana = (() => { const t = new Date(); t.setDate(t.getDate()+1); return t.toISOString().split('T')[0]; })();

  return {
    id: srv.IdCita,
    date: fmtFechaCorta(srv.FechaHora),
    dateISO,
    time: fmtHora12(srv.FechaHora),
    timeISO: fmtHora24(srv.FechaHora),
    patient,
    doctor,
    service,
    office: 'C1', // En un proyecto real vendría del campo Consultorio en la tabla Citas
    status: info.label,
    statusClass: info.cls,
    highlight: dateISO === hoy && info.label === 'En consulta',
    noShow: info.label === 'No asistió',
    notes: srv.Notas || '',
    // Helper para filtros predefinidos ('today' / 'tomorrow')
    _dateMatchPreset: { today: dateISO === hoy, tomorrow: dateISO === manana },
    _raw: srv
  };
};

// ═══════════════════════════════════════════════════════════════════
//  CACHÉ LOCAL (respaldo de la última respuesta real, solo si no hay SSR)
// ═══════════════════════════════════════════════════════════════════

// Estado vacío real: esta ruta de código solo se usa si la tabla no viene
// renderizada por el servidor (ver shouldUseServerRenderedList) y la API
// tampoco responde. Antes había 5 citas ficticias aquí.
const FALLBACK_DATA = [];

// Almacén en memoria (cargado de LocalStorage / API al init)
let _appointments = [];

const appointmentStorage = {
  init: () => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) { _appointments = JSON.parse(raw); return; }
    } catch {}
    _appointments = [...FALLBACK_DATA];
    appointmentStorage._persist();
  },
  _persist: () => {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(_appointments)); }
    catch (e) { console.error('[SmileTrack] Save LocalStorage:', e); }
  },
  getAll: () => [..._appointments],
  findById: (id) => _appointments.find(a => a.id === parseInt(id, 10)) || null,

  replaceAll: (nuevos) => {
    _appointments = Array.isArray(nuevos) ? nuevos : [];
    appointmentStorage._persist();
  },
  add: (appt) => {
    const newId = _appointments.length
      ? Math.max(..._appointments.map(a => a.id)) + 1
      : 1;
    const nuevo = { ...appt, id: newId };
    _appointments.unshift(nuevo);
    appointmentStorage._persist();
    return nuevo;
  },
  update: (id, updates) => {
    const i = _appointments.findIndex(a => a.id === parseInt(id, 10));
    if (i === -1) return null;
    _appointments[i] = { ..._appointments[i], ...updates };
    appointmentStorage._persist();
    return _appointments[i];
  },
  // "delete" = cancelar cita (coincide con soft-delete server)
  delete: (id) => {
    const i = _appointments.findIndex(a => a.id === parseInt(id, 10));
    if (i !== -1) {
      _appointments[i].status = 'Cancelada';
      _appointments[i].statusClass = 'status-no-asistio';
      appointmentStorage._persist();
    }
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
    <td class="col-fecha">${appt.date}</td>
    <td class="col-hora"><span class="pill-hora" aria-label="Hora: ${appt.time}">${appt.time}</span></td>
    <td class="col-paciente">
      <div class="td-paciente">
        <div class="pac-avatar pac-avatar--${avatarColor}" aria-hidden="true">${initials}</div>
        <span class="pac-name">${appt.patient}</span>
      </div>
    </td>
    <td class="col-profesional">${appt.doctor}</td>
    <td class="col-servicio">${appt.service}</td>
    <td class="col-consultorio">${appt.office}</td>
    <td><span class="status-badge ${appt.statusClass}" role="status" aria-label="Estado: ${appt.status}">${appt.status}</span></td>
    <td>
      <div class="actions-cell" role="group" aria-label="Acciones para ${appt.patient}">
        <button class="btn-icon action-btn btn-view" type="button"
                data-action="view" data-id="${appt.id}"
                aria-label="Ver detalles de ${appt.patient}"
                title="Ver detalles de ${appt.patient}">
          👁️ <span class="btn-text">Ver</span>
        </button>
        <button class="btn-icon action-btn edit" type="button"
                data-action="edit" data-id="${appt.id}"
                aria-label="Editar cita de ${appt.patient}"
                title="Editar cita de ${appt.patient}">
          ✏️ <span class="btn-text">Editar</span>
        </button>
        <button class="btn-icon action-btn" type="button"
                data-action="sync" data-id="${appt.id}"
                aria-label="Sincronizar cita de ${appt.patient}"
                title="Sincronizar cita de ${appt.patient}">
          🔄 <span class="btn-text">Sincronizar</span>
        </button>
        <button class="btn-icon action-btn btn-delete" type="button"
                data-action="cancel" data-id="${appt.id}"
                aria-label="Cancelar cita de ${appt.patient}"
                title="Cancelar cita de ${appt.patient}">
          ✕ <span class="btn-text">Cancelar</span>
        </button>
      </div>
    </td>
  `;
  return tr;
};

const renderAppointments = (data) => {
  if (shouldUseServerRenderedList()) return;
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
  updatePaginationInfo(1, Math.min(5, data.length), data.length);
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
    const matchProf = !prof || a.doctor === prof;
    const matchDate = !datePreset || (datePreset === 'today' && a._dateMatchPreset?.today)
                                  || (datePreset === 'tomorrow' && a._dateMatchPreset?.tomorrow);
    const matchSt = !st || a.status === st;
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
  return {
    IdCita: appt.id,
    IdPaciente: raw.IdPaciente || 0,
    IdProfesional: raw.IdProfesional || null,
    IdServicio: raw.IdServicio || 0,
    FechaHora: fh,
    Estado: STATUS_MAP_CLIENTE[estadoUI] || (estadoUI || 'programada').toLowerCase(),
    Notas: overrides.notes !== undefined ? overrides.notes : (appt.notes || '')
  };
};

const handleTableAction = async (e) => {
  const btn = e.target.closest('[data-action]');
  if (!btn) return;
  const { action, id } = btn.dataset;

  if (action === 'view')   return openViewModal(id);
  if (action === 'edit')   return openEditModal(id);
  if (action === 'sync')   return openSyncModal(id);
  if (action === 'cancel') {
    // Abrir modal de confirmación personalizado en lugar de window.confirm() nativo
    if (typeof window.openConfirmDeleteCita === 'function') {
      window.openConfirmDeleteCita(id, '');
    }
    return;
  }
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
      <div class="modal-row"><span class="modal-key">Paciente</span>     <span class="modal-val">${a.patient}</span></div>
      <div class="modal-row"><span class="modal-key">Fecha</span>        <span class="modal-val"><time datetime="${a.dateISO}">${a.date}</time></span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>         <span class="modal-val">${a.time}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${a.doctor}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span>     <span class="modal-val">${a.service}</span></div>
      <div class="modal-row"><span class="modal-key">Consultorio</span>  <span class="modal-val">${a.office}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>       <span class="modal-val"><span class="status-badge ${a.statusClass}">${a.status}</span></span></div>
      ${a.notes ? `<div class="modal-row"><span class="modal-key">Notas</span><span class="modal-val">${a.notes}</span></div>` : ''}
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
  const form = e.currentTarget;
  if (!validateForm(form)) {
    e.preventDefault();
    window.ToastService.error('Por favor completa los campos requeridos.');
    return;
  }
  const submitBtn = form.querySelector('[type="submit"]');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Guardando...';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL SINCRONIZAR (Google/Outlook/Apple) — UI placebo
// ═══════════════════════════════════════════════════════════════════

const openSyncModal = (id) => {
  ['syncGoogle','syncOutlook','syncApple'].forEach(bid => {
    const b = safeGetElement(bid);
    if (!b) return;
    const fresh = b.cloneNode(true);
    b.replaceWith(fresh);
    fresh.addEventListener('click', () => {
      const platform = fresh.dataset.platform || 'calendario';
      showToast(`Sincronizando con ${platform}…`, 'info');
      setTimeout(() => { window.ToastService.success(`Cita sincronizada con ${platform}`); modalManager.close('modalSyncCalendar'); }, 1500);
    });
  });
  modalManager.open('modalSyncCalendar');
};

// ═══════════════════════════════════════════════════════════════════
//  HANDLERS MODALES + MÓVIL + FORM NUEVA CITA
// ═══════════════════════════════════════════════════════════════════

const initModalHandlers = () => {
  const map = {
    modalNewClose: 'modalNewAppointment', modalViewClose: 'modalViewAppointment',
    modalEditClose: 'modalEditAppointment', modalSyncClose: 'modalSyncCalendar',
    modalNewCancel: 'modalNewAppointment', modalViewCancel: 'modalViewAppointment',
    modalEditCancel: 'modalEditAppointment', modalSyncCancel: 'modalSyncCalendar'
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
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  const hamb = safeGetElement('hamburger');
  if (!sidebar || !overlay || !hamb) return;
  const toggle = (s) => {
    sidebar.classList.toggle('open', s);
    overlay.classList.toggle('open', s);
    hamb.setAttribute('aria-expanded', String(s));
    overlay.setAttribute('aria-hidden', String(!s));
    if (s) sidebar.querySelector('.nav-item')?.focus(); else hamb.focus();
  };
  hamb.addEventListener('click', () => toggle(true));
  overlay.addEventListener('click', () => toggle(false));
  sidebar.querySelectorAll('.nav-item').forEach(l =>
    l.addEventListener('click', () => { if (window.innerWidth <= 680) toggle(false); })
  );
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) { e.preventDefault(); toggle(false); }
  });
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
    modalManager.open('modalNewAppointment');
  };
  safeGetElement('btnNuevaCita')?.addEventListener('click', open);
  safeGetElement('fabNuevaCita')?.addEventListener('click', open);
};

const submitNewAppointment = (e) => {
  const form = e.currentTarget;
  if (!validateForm(form)) {
    e.preventDefault();
    window.ToastService.error('Por favor completa los campos requeridos.');
    return;
  }
  const submitBtn = form.querySelector('[type="submit"]');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Guardando...';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FETCH INICIAL
// ═══════════════════════════════════════════════════════════════════

async function fetchAppointments() {
  try {
    const res = await fetch('/api/citas?page=1&pageSize=100', {
      method: 'GET',
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }
    });
    if (!res.ok) throw new Error(`status ${res.status}`);
    const payload = await res.json();
    if (payload && payload.success && Array.isArray(payload.data)) {
      const citas = payload.data.map(mapServerToClient);
      appointmentStorage.replaceAll(citas);
      return citas;
    }
    throw new Error('payload inválido');
  } catch (err) {
    console.warn('[SmileTrack] No se pudo cargar citas desde /api/citas:', err);
    return appointmentStorage.getAll();
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
      // Carga inicial de datos (API → LocalStorage fallback)
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
      safeGetElement('searchPatient')?.addEventListener('input', debounce(filterAppointments, 180));
      ['filterProfessional','filterDate','filterStatus'].forEach(id =>
        safeGetElement(id)?.addEventListener('change', filterAppointments)
      );
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
      // Modo SSR: animar los KPI renderizados por Razor con data-target
      initServerMetrics();
    }
  } catch (err) {
    console.error('[SmileTrack] Error init app.js (recepcionista):', err);
    mostrarErrorUsuario(err.message || 'Error cargando módulo de gestión de citas. Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', init);
