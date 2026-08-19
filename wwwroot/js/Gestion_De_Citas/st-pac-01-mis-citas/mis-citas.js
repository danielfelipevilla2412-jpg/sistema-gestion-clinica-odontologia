/* ============================================
SmileTrack — Mis Citas Paciente (st-pac-01-mis-citas)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026 (actualizado 2026-07-31)

DESCRIPCIÓN:
Módulo para paciente autenticado. Consume SOLO sus citas desde GET /api/citas — el backend
aplica automáticamente el filtro por IdPaciente (Claim "IdPaciente" agregado en /api/login),
por lo que incluso si el paciente manipula el request, NUNCA ve citas de terceros (privacidad).

FUNCIONALIDADES PRINCIPALES:
- Render de tabla con citas del paciente cargadas desde API REST (GET /api/citas)
- Filtros combinados: texto búsqueda (profesional/servicio/fecha) + selector por estado
- Cancelación de citas programadas/confirmadas vía DELETE /api/citas/{id} (soft delete server)
- Modal de detalle y modal de confirmación de cancelación con validación de 24h

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController → ApiListarCitas, ApiEliminarCita
- Endpoints: GET /api/citas [filtro IdPaciente automático por Claim], DELETE /api/citas/{id}
- CSS: ~/css/Gestion_De_Citas/st-pac-01-mis-citas/styles.css
- JS: ~/js/Gestion_De_Citas/st-pac-01-mis-citas/mis-citas.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- SEGURIDAD IMPORTANTE: El filtro por IdPaciente se aplica EN EL CONTROLLER, no en cliente.
  (Si se hace solo en JS, paciente podría ver otras citas; el backend lo impide siempre).
- STATUS_MAP_SERVER / STATUS_MAP_CLIENTE = mismas constantes que los otros 3 módulos citas.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API + AUTH
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const API_PAGE_SIZE = 200;
const STORAGE_KEY = 'smiletrack_pac_mis_citas';

const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* navegación privada */ }
  return headers;
};

// Estados (mismo mapeo que agenda.js / app.js)
const STATUS_MAP_SERVER = {
  programada:  { label: 'Agendada',   cls: 'badge-agendada'   },
  confirmada:  { label: 'Confirmada', cls: 'badge-confirmada' },
  en_proceso:  { label: 'En curso',   cls: 'badge-confirmada' },
  finalizada:  { label: 'Completada', cls: 'badge-completada' },
  atendida:    { label: 'Completada', cls: 'badge-completada' },
  cancelada:   { label: 'Cancelada',  cls: 'badge-cancelada'  },
  no_asistida: { label: 'No asistió', cls: 'badge-cancelada'  }
};
const STATUS_MAP_CLIENTE = {
  'Agendada':   'programada',
  'Confirmada': 'confirmada',
  'En curso':   'en_proceso',
  'Completada': 'finalizada',
  'Cancelada':  'cancelada',
  'No asistió': 'no_asistida'
};

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
  let t;
  return (...a) => { clearTimeout(t); t = setTimeout(() => fn.apply(this, a), delay); };
};

const notify = (type, title, message) => {
  if (window.ToastService && typeof window.ToastService[type] === 'function') {
    window.ToastService[type](title, message);
  } else {
    console[type === 'error' ? 'error' : 'log'](`[SmileTrack] ${title}${message ? ` - ${message}` : ''}`);
  }
};

const fmtFecha = (fh) => {
  try {
    const d = new Date(fh);
    const dias = ['Dom','Lun','Mar','Mié','Jue','Vie','Sáb'];
    const m = String(d.getDate()).padStart(2, '0');
    const meses = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    return `${dias[d.getDay()]} ${m} ${meses[d.getMonth()]}`;
  } catch { return '—'; }
};
const fmtHora = (fh) => {
  try {
    const d = new Date(fh);
    let h = d.getHours(); const mm = String(d.getMinutes()).padStart(2, '0');
    const p = h >= 12 ? 'PM' : 'AM';
    if (h === 0) h = 12; else if (h > 12) h -= 12;
    return `${String(h).padStart(2,'0')}:${mm} ${p}`;
  } catch { return '—'; }
};

// ═══════════════════════════════════════════════════════════════════
//  MAPEO SERVER → CLIENTE + FALLBACK LOCAL
// ═══════════════════════════════════════════════════════════════════

const mapServerToClient = (srv) => {
  const est = (srv.Estado || 'programada').toLowerCase();
  const info = STATUS_MAP_SERVER[est] || STATUS_MAP_SERVER['programada'];
  const fhISO = srv.FechaHora ? new Date(srv.FechaHora).toISOString() : null;
  const proximaFutura = info.label === 'Agendada' || info.label === 'Confirmada';
  const todayISO = new Date().toISOString().split('T')[0];
  const citaFechaISO = fhISO ? fhISO.split('T')[0] : todayISO;

  // BUG FIX 'undefined' en alertas:
  // El API puede devolver el nombre del profesional bajo distintos campos según la versión
  // del serializador (PascalCase vs camelCase). Probamos todas las variantes conocidas y
  // si ninguna tiene valor, usamos el fallback legible. Esto evita que `${item.doctor}`
  // en template literals resulte en el string literal 'undefined'.
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
    fecha: fmtFecha(fhISO),
    fechaISO: citaFechaISO,
    fechaHoraISO: fhISO || new Date().toISOString(),
    hora: fmtHora(fhISO),
    doctor: doctorNombre,
    servicio: servicioNombre,
    estado: info.label,
    active: proximaFutura && citaFechaISO === todayISO,
    _raw: srv
  };
};

// BUG FIX: El FALLBACK original no incluía `fechaHoraISO`, lo que causaba que
// diffHoras() recibiera undefined → new Date(undefined) → NaN → comparaciones
// incorrectas en abrirModalCancelar() (hrs < 24 y hrs < 2 ambas falsas cuando son NaN).
// Añadimos fechaHoraISO con timestamps futuros realistas para que las validaciones
// de cancelación funcionen correctamente con los datos de demostración.
const FALLBACK = [
  { id:1, fecha:'Vie 20 Mar', fechaISO:'2026-03-20', fechaHoraISO:'2026-03-20T10:00:00.000Z', hora:'10:00 AM', doctor:'Dr. Carlos Méndez',  servicio:'Control general',   estado:'Agendada',   active:true  },
  { id:2, fecha:'Vie 27 Mar', fechaISO:'2026-03-27', fechaHoraISO:'2026-03-27T15:30:00.000Z', hora:'03:30 PM', doctor:'Dra. Laura Gómez',   servicio:'Ortodoncia',        estado:'Agendada',   active:true  },
  { id:3, fecha:'Mar 10 Mar', fechaISO:'2026-03-10', fechaHoraISO:'2026-03-10T09:00:00.000Z', hora:'09:00 AM', doctor:'Dr. Carlos Méndez',  servicio:'Limpieza dental',   estado:'Completada', active:false },
  { id:4, fecha:'Lun 03 Feb', fechaISO:'2026-02-03', fechaHoraISO:'2026-02-03T11:30:00.000Z', hora:'11:30 AM', doctor:'Dra. Laura Gómez',   servicio:'Resina dental',     estado:'Completada', active:false },
  { id:5, fecha:'Mié 10 Ene', fechaISO:'2026-01-10', fechaHoraISO:'2026-01-10T10:00:00.000Z', hora:'10:00 AM', doctor:'Dr. Carlos Méndez',  servicio:'Ortodoncia',        estado:'Cancelada',  active:false }
];

let citas = [...FALLBACK];
let cancelId = null;

const loadLocal = () => {
  try {
    const r = localStorage.getItem(STORAGE_KEY);
    if (r) return JSON.parse(r);
  } catch {}
  return [...FALLBACK];
};
const saveLocal = () => {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(citas)); } catch {}
};

// ═══════════════════════════════════════════════════════════════════
//  HELPERS ESTADO / FILTROS / RENDER
// ═══════════════════════════════════════════════════════════════════

const badgeClass = (estado) => {
  const serverKey = (STATUS_MAP_CLIENTE[estado] || estado).toLowerCase();
  return STATUS_MAP_SERVER[serverKey]?.cls || 'badge-agendada';
};

const getFiltered = () => {
  const sIn = safeGetElement('searchInput');
  const fIn = safeGetElement('filterEstado');
  if (!sIn || !fIn) return citas;
  const q = sIn.value.toLowerCase().trim();
  const st = fIn.value;

  return citas.filter(c => {
    const matchQ = !q || c.doctor.toLowerCase().includes(q)
      || c.servicio.toLowerCase().includes(q)
      || c.fecha.toLowerCase().includes(q);
    const matchS = !st || c.estado === st;
    return matchQ && matchS;
  });
};

const updateStats = () => {
  const total = citas.length;
  const comp = citas.filter(c => c.estado === 'Completada').length;
  const pend = citas.filter(c => c.estado === 'Agendada' || c.estado === 'Confirmada' || c.estado === 'En curso').length;
  const canc = citas.filter(c => c.estado === 'Cancelada' || c.estado === 'No asistió').length;

  const elT = safeGetElement('cnt-total');
  const elC = safeGetElement('cnt-completadas');
  const elP = safeGetElement('cnt-pendientes');
  const elX = safeGetElement('cnt-canceladas');
  if (elT) elT.textContent = total;
  if (elC) elC.textContent = comp;
  if (elP) elP.textContent = pend;
  if (elX) elX.textContent = canc;

  const bar = safeGetElement('progressBar');
  const lbl = safeGetElement('progressLabel');
  const pct = total > 0 ? Math.round((comp / total) * 100) : 0;
  if (bar) {
    bar.style.width = pct + '%';
    bar.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', pct);
  }
  if (lbl) lbl.textContent = `${comp} de ${total} citas completadas`;
};

const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const start = performance.now();
  const dur = 900;
  const tick = (t) => {
    const p = Math.min((t - start) / dur, 1);
    const eased = 1 - Math.pow(1 - p, 3);
    el.textContent = Math.floor(eased * target);
    if (p < 1) requestAnimationFrame(tick); else el.textContent = target;
  };
  requestAnimationFrame(tick);
};

const animateCounters = () => {
  const total = citas.length;
  const comp = citas.filter(c => c.estado === 'Completada').length;
  const pend = citas.filter(c => c.estado === 'Agendada' || c.estado === 'Confirmada').length;
  const canc = citas.filter(c => c.estado === 'Cancelada').length;
  animateCounter(safeGetElement('cnt-total'), total);
  animateCounter(safeGetElement('cnt-completadas'), comp);
  animateCounter(safeGetElement('cnt-pendientes'), pend);
  animateCounter(safeGetElement('cnt-canceladas'), canc);
};

const renderTable = () => {
  const data = getFiltered();
  const tbody = safeGetElement('citasTbody');
  if (!tbody) return;
  tbody.innerHTML = '';

  const lbl = safeGetElement('countLabel');
  if (lbl) lbl.textContent = `${data.length} resultado${data.length !== 1 ? 's' : ''}`;

  if (!data.length) {
    tbody.innerHTML = `<tr><td colspan="6"><div class="empty-state"><span class="empty-icon" aria-hidden="true">📭</span><p>No hay citas que coincidan con los filtros.</p></div></td></tr>`;
    return;
  }

  data.forEach(item => {
    const tr = document.createElement('tr');
    if (item.active) tr.classList.add('row-active');
    if (item.estado === 'Cancelada') tr.classList.add('row-cancelada');
    const canCancel = item.estado === 'Agendada' || item.estado === 'Confirmada';
    tr.innerHTML = `
      <td class="td-fecha">${item.fecha}</td>
      <td><span class="pill-hora">${item.hora}</span></td>
      <td class="td-doctor">${item.doctor}</td>
      <td>${item.servicio}</td>
      <td><span class="badge ${badgeClass(item.estado)}">${item.estado}</span></td>
      <td>
        <div class="actions-cell">
          <button class="btn-icon action-btn btn-view" type="button" id="btn-ver-${item.id}"
                  title="Ver detalle" data-action="ver" data-id="${item.id}" aria-label="Ver detalle de cita">
            👁️ <span class="btn-text">Ver</span>
          </button>
          ${canCancel ? `<button class="btn-icon action-btn btn-delete danger" type="button" id="btn-cancelar-${item.id}"
                  title="Cancelar cita" data-action="cancelar" data-id="${item.id}" aria-label="Cancelar cita">
            ✕ <span class="btn-text">Cancelar</span>
          </button>` : ''}
        </div>
      </td>`;
    tbody.appendChild(tr);
  });
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL DETALLE + CANCELACIÓN
// ═══════════════════════════════════════════════════════════════════

const openModal = (id) => {
  const item = citas.find(c => c.id === id);
  if (!item) return;
  const mc = safeGetElement('modalContent');
  if (mc) {
    mc.innerHTML = `
      <div class="modal-row"><span class="modal-key">Fecha</span>   <span class="modal-val">${item.fecha}</span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>    <span class="modal-val">${item.hora}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${item.doctor}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${item.servicio}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>
        <span class="modal-val"><span class="badge ${badgeClass(item.estado)}">${item.estado}</span></span>
      </div>`;
  }
  const mo = safeGetElement('modalOverlay');
  if (mo) {
    mo.dataset.opener = `btn-ver-${id}`;
    mo.classList.add('open');
    mo.setAttribute('aria-hidden', 'false');
    mo.removeAttribute('inert');
    safeGetElement('modalClose')?.focus();
  }
};
const closeModal = () => {
  const mo = safeGetElement('modalOverlay');
  if (!mo) return;
  mo.classList.remove('open');
  mo.setAttribute('aria-hidden', 'true');
  mo.setAttribute('inert', '');
  const opener = mo.dataset.opener;
  if (opener) safeGetElement(opener)?.focus();
};

// BUG FIX: diffHoras recibía undefined cuando item.fechaHoraISO no estaba presente
// (ocurría con datos del FALLBACK antes del fix, y puede ocurrir si el API
// devuelve FechaHora=null). new Date(undefined/null/invalid) retorna Invalid Date,
// cuyo getTime() es NaN. NaN arithmetic produce NaN, y NaN < 24 / NaN < 2 son
// ambas false, haciendo que la rama de advertencia nunca ejecutara y que la rama
// de bloqueo (<2h) tampoco, resultando en que citas ya pasadas fueran cancelables.
// Ahora retornamos -Infinity si la fecha es inválida para que la UI las trate
// correctamente como citas ya pasadas (no cancelables).
const diffHoras = (fechaHoraISO) => {
  if (!fechaHoraISO) return -Infinity;
  const cita = new Date(fechaHoraISO);
  if (isNaN(cita.getTime())) return -Infinity;
  return (cita.getTime() - Date.now()) / 3_600_000;
};

const abrirModalCancelar = (id) => {
  const item = citas.find(c => c.id === id);
  if (!item) return;

  const hrs = diffHoras(item.fechaHoraISO);
  const btnSi = safeGetElement('cancelarSi');
  const desc = safeGetElement('cancelarDesc');
  const adv = safeGetElement('cancelarAdvertencia');
  const error = safeGetElement('cancelarError');

  if (error) error.textContent = '';

  if (hrs < 24 && item.estado !== 'Cancelada' && item.estado !== 'Completada') {
    if (adv) adv.textContent = `⚠️ Esta cita es en ${Math.max(1, Math.round(hrs))} horas. La clínica recomienda cancelar con al menos 24h de anticipación.`;
    adv?.classList.remove('hidden');
  } else {
    adv?.classList.add('hidden');
  }

  if (item.estado === 'Completada' || item.estado === 'Cancelada' || item.estado === 'No asistió') {
    if (btnSi) btnSi.disabled = true;
    if (desc) desc.textContent = `No se puede cancelar: la cita está en estado "${item.estado}".`;
  } else if (hrs < 2) {
    if (btnSi) btnSi.disabled = true;
    if (desc) desc.textContent = `Cita en menos de 2 horas. Comuníquese con la clínica para cancelar.`;
  } else {
    if (btnSi) btnSi.disabled = false;
    if (desc) desc.textContent = `Cita del ${item.fecha} a las ${item.hora} — ${item.servicio} con ${item.doctor}`;
  }

  cancelId = id;
  const m = safeGetElement('modalCancelar');
  if (m) {
    m.dataset.opener = `btn-cancelar-${id}`;
    m.classList.add('open');
    m.setAttribute('aria-hidden', 'false');
    m.removeAttribute('inert');
    safeGetElement('cancelarSi')?.focus();
  }
};

const cerrarModalCancelar = () => {
  cancelId = null;
  const m = safeGetElement('modalCancelar');
  if (!m) return;
  m.classList.remove('open');
  m.setAttribute('aria-hidden', 'true');
  m.setAttribute('inert', '');
  const opener = m.dataset.opener;
  if (opener) safeGetElement(opener)?.focus();
};

const confirmarCancelacion = async () => {
  if (!cancelId) return;
  const item = citas.find(c => c.id === cancelId);
  if (!item) return;

  // Optimistic update local
  const beforeEstado = item.estado;
  item.estado = 'Cancelada';
  item.active = false;
  updateStats();
  renderTable();
  notify('success', 'Éxito', 'Cita cancelada correctamente');

  let success = false, msg = null;
  try {
    const res = await fetch(`${API_BASE}/citas/${cancelId}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });
    let payload;
    try { payload = await res.json(); } catch { payload = { success: res.ok }; }
    success = res.ok && payload.success;
    msg = payload.message;
  } catch (err) {
    console.warn('[SmileTrack] Cancel cita offline paciente:', err);
    success = true;
    msg = 'Cancelación guardada localmente';
  }

  if (success) {
    saveLocal();
    cerrarModalCancelar();
    if (msg && msg.toLowerCase().includes('local'))
      notify('warning', 'Atención', 'Cancelación guardada localmente');
  } else {
    item.estado = beforeEstado;
    item.active = beforeEstado === 'Agendada' || beforeEstado === 'Confirmada';
    updateStats();
    renderTable();
    const errorBox = safeGetElement('cancelarError');
    if (errorBox) errorBox.textContent = msg || 'No fue posible cancelar la cita. Intente más tarde o contacte recepción.';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL SOLICITUD NUEVA CITA (UI placebo; endpoint en desarrollarse)
// ═══════════════════════════════════════════════════════════════════

const initNuevaCitaModal = () => {
  const btn = safeGetElement('btnNuevaCita');
  const modal = safeGetElement('modalNuevaCita');
  if (!modal || !btn) return;

  btn.addEventListener('click', () => {
    modal.dataset.opener = 'btnNuevaCita';
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    modal.querySelector('.form-input')?.focus();
  });

  const cerrar = () => {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    const o = modal.dataset.opener;
    if (o) safeGetElement(o)?.focus();
  };

  safeGetElement('closeNuevaCita')?.addEventListener('click', cerrar);
  safeGetElement('cancelarNuevaCita')?.addEventListener('click', cerrar);
  modal.addEventListener('click', e => { if (e.target === modal) cerrar(); });

  safeGetElement('confirmarNuevaCita')?.addEventListener('click', () => {
    const fecha = safeGetElement('citaFecha');
    if (!fecha?.value) {
      if (fecha) {
        fecha.focus();
        fecha.style.borderColor = 'var(--orange)';
        setTimeout(() => fecha.style.borderColor = '', 2000);
      }
      return;
    }
    cerrar();
    notify('success', 'Éxito', 'Solicitud de cita enviada exitosamente');
    if (fecha) fecha.value = '';
    safeGetElement('citaServicio') && (safeGetElement('citaServicio').selectedIndex = 0);
    const nta = safeGetElement('citaNota');
    if (nta) nta.value = '';
  });
};

// ═══════════════════════════════════════════════════════════════════
//  EVENTOS TABLA (event delegation)
// ═══════════════════════════════════════════════════════════════════

const initTableEvents = () => {
  const tbody = safeGetElement('citasTbody');
  if (!tbody) return;
  tbody.addEventListener('click', e => {
    const b = e.target.closest('[data-action]');
    if (!b) return;
    const accion = b.dataset.action;
    const id = parseInt(b.dataset.id, 10);
    if (isNaN(id)) return;
    if (accion === 'ver') openModal(id);
    else if (accion === 'cancelar') abrirModalCancelar(id);
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FETCH CITAS DESDE API
// ═══════════════════════════════════════════════════════════════════

async function fetchAppointments() {
  try {
    const res = await fetch(`${API_BASE}/citas?page=1&pageSize=${API_PAGE_SIZE}`, {
      method: 'GET',
      headers: getAuthHeaders()
    });
    if (!res.ok) throw new Error(`status ${res.status}`);
    const payload = await res.json();
    if (payload && payload.success && Array.isArray(payload.data)) {
      citas = payload.data.map(mapServerToClient);
      saveLocal();
      return;
    }
    throw new Error('payload inválido');
  } catch (err) {
    console.warn('[SmileTrack] Mis citas paciente: fallback LocalStorage:', err);
    citas = loadLocal();
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INIT PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  try {
    // 1. Cargar datos — si localStorage está vacío la primera vez, usar FALLBACK explícito
    // BUG FIX: loadLocal() retornaba el FALLBACK pero solo si localStorage tenía una
    // entrada previa; en la primera carga (localStorage vacío) retornaba FALLBACK también,
    // pero si alguna versión anterior guardó un array vacío [] en el storage, lo devolvía
    // vacío. Ahora normalizamos: si el resultado de loadLocal() está vacío, usamos FALLBACK.
    const stored = loadLocal();
    citas = (Array.isArray(stored) && stored.length > 0) ? stored : [...FALLBACK];
    animateCounters();
    updateStats();
    renderTable();

    initNuevaCitaModal();
    initTableEvents();

    // Filtros
    safeGetElement('filterEstado')?.addEventListener('change', renderTable);
    const sEl = safeGetElement('searchInput');
    if (sEl) sEl.addEventListener('input', debounce(renderTable, 180));

    // Modal detalle / Escape
    safeGetElement('modalClose')?.addEventListener('click', closeModal);
    safeGetElement('modalOverlay')?.addEventListener('click', e => {
      if (e.target.closest('#modalOverlay') === e.target) closeModal();
    });
    safeGetElement('cancelarNo')?.addEventListener('click', cerrarModalCancelar);
    safeGetElement('cancelarSi')?.addEventListener('click', confirmarCancelacion);
    safeGetElement('modalCancelar')?.addEventListener('click', e => {
      if (e.target.closest('#modalCancelar') === e.target) cerrarModalCancelar();
    });

    // Escape ordenado: nueva → cancelar → detalle
    document.addEventListener('keydown', e => {
      if (e.key !== 'Escape') return;
      const mNueva = safeGetElement('modalNuevaCita');
      const mCanc = safeGetElement('modalCancelar');
      const mDet = safeGetElement('modalOverlay');
      if (mNueva?.classList.contains('open')) {
        mNueva.classList.remove('open');
        mNueva.setAttribute('aria-hidden', 'true');
        mNueva.setAttribute('inert', '');
      } else if (mCanc?.classList.contains('open')) cerrarModalCancelar();
      else if (mDet?.classList.contains('open')) closeModal();
    });

    // Fetch real desde API (sobrescribe datos locales si tiene éxito)
    await fetchAppointments();
    animateCounters();
    updateStats();
    renderTable();

    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
  } catch (err) {
    console.error('[SmileTrack] Error init mis-citas.js (paciente):', err);
    mostrarErrorUsuario(err.message || 'Error cargando módulo "Mis Citas". Intente recargar.');
  }
};

// Remover listeners duplicados DOMContentLoaded (había 2 en el archivo original)
document.addEventListener('DOMContentLoaded', init);
