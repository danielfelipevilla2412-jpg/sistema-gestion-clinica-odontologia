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
- Los estados de cita se consumen desde CommonUtils.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API + AUTH
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const API_PAGE_SIZE = 200;

const getCsrfToken = () => {
  const requestToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (requestToken) return requestToken;
  const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  return match ? decodeURIComponent(match[2]) : null;
};

const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  const csrfToken = getCsrfToken();
  if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* navegación privada */ }
  return headers;
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
  div.replaceChildren();
  const strong = document.createElement('strong');
  strong.textContent = '[SmileTrack]';
  const close = document.createElement('button');
  close.type = 'button';
  close.textContent = '×';
  close.setAttribute('aria-label', 'Cerrar mensaje de error');
  close.style.cssText = 'margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;';
  close.addEventListener('click', () => { div.style.display = 'none'; });
  div.append(strong, document.createTextNode(` ${mensaje} `), close);
  div.style.display = 'block';
}

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  if (window.CommonUtils?.getEl) return window.CommonUtils.getEl(id);
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  if (window.CommonUtils?.debounce) return window.CommonUtils.debounce(fn, delay);
  let t;
  return (...a) => { clearTimeout(t); t = setTimeout(() => fn.apply(this, a), delay); };
};

const escapeHtml = (value) => {
  if (window.CommonUtils?.escapeHtml) return window.CommonUtils.escapeHtml(value);
  return String(value ?? '').replace(/[&<>'"]/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    "'": '&#39;',
    '"': '&quot;'
  }[character]));
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
//  MAPEO SERVER → CLIENTE
// ═══════════════════════════════════════════════════════════════════

const mapServerToClient = (srv) => {
  const est = CommonUtils.mapEstadoServerToClient(srv.Estado);
  const info = CommonUtils.getStatusInfo(est);
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
let citas = [];
let cancelId = null;

// ═══════════════════════════════════════════════════════════════════
//  HELPERS ESTADO / FILTROS / RENDER
// ═══════════════════════════════════════════════════════════════════

const badgeClass = (estado) => {
  const serverKey = CommonUtils.mapEstadoClienteToServer(estado);
  return CommonUtils.getStatusInfo(serverKey).cls || 'badge-agendada';
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
    const emptyRow = document.createElement('tr');
    const emptyCell = document.createElement('td');
    emptyCell.colSpan = 6;
    emptyCell.innerHTML = '<div class="empty-state"><span class="material-symbols-outlined" aria-hidden="true" style="font-size:2rem;color:var(--text-muted);display:block;margin-bottom:8px;">inbox</span><p>No hay citas que coincidan con los filtros.</p></div>';
    emptyRow.appendChild(emptyCell);
    tbody.replaceChildren(emptyRow);
    return;
  }

  data.forEach(item => {
    const tr = document.createElement('tr');
    if (item.active) tr.classList.add('row-active');
    if (item.estado === 'Cancelada') tr.classList.add('row-cancelada');
    const canCancel = item.estado === 'Agendada' || item.estado === 'Confirmada';
    tr.innerHTML = `
      <td class="td-fecha">${escapeHtml(item.fecha)}</td>
      <td><span class="pill-hora">${escapeHtml(item.hora)}</span></td>
      <td class="td-doctor">${escapeHtml(item.doctor)}</td>
      <td>${escapeHtml(item.servicio)}</td>
      <td><span class="badge ${escapeHtml(badgeClass(item.estado))}">${escapeHtml(item.estado)}</span></td>
      <td>
        <div class="actions-cell">
          <button class="btn-icon action-btn btn-view" type="button" id="btn-ver-${item.id}"
                  title="Ver detalle" data-action="ver" data-id="${item.id}" aria-label="Ver detalle de cita">
            <span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">visibility</span> <span class="btn-text">Ver</span>
          </button>
          ${canCancel ? `<button class="btn-icon action-btn btn-delete danger" type="button" id="btn-cancelar-${item.id}"
                  title="Cancelar cita" data-action="cancelar" data-id="${item.id}" aria-label="Cancelar cita">
            <span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">cancel</span> <span class="btn-text">Cancelar</span>
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
      <div class="modal-row"><span class="modal-key">Fecha</span>   <span class="modal-val">${escapeHtml(item.fecha)}</span></div>
      <div class="modal-row"><span class="modal-key">Hora</span>    <span class="modal-val">${escapeHtml(item.hora)}</span></div>
      <div class="modal-row"><span class="modal-key">Profesional</span>  <span class="modal-val">${escapeHtml(item.doctor)}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${escapeHtml(item.servicio)}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span>
        <span class="modal-val"><span class="badge ${escapeHtml(badgeClass(item.estado))}">${escapeHtml(item.estado)}</span></span>
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

  // Mantener el estado local solo mientras se confirma la operación remota.
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
    success = false;
    msg = 'No fue posible conectar con la API para cancelar la cita.';
  }

  if (success) {
    cerrarModalCancelar();
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
//  MODAL SOLICITUD NUEVA CITA — conectado a POST /api/citas/solicitar
// ═══════════════════════════════════════════════════════════════════

/**
 * Carga los servicios disponibles desde la API y los inyecta en #citaServicio.
 * Si la API falla, deja el selector vacío para no enviar IDs inventados.
 */
const cargarServiciosEnModal = async () => {
  const select = safeGetElement('citaServicio');
  if (!select) return;
  try {
    const res = await fetch('/api/servicios', {
      headers: { 'Accept': 'application/json' }
    });
    if (!res.ok) throw new Error(`status ${res.status}`);
    const payload = await res.json();
    const servicios = payload.data ?? payload ?? [];
    if (!Array.isArray(servicios) || servicios.length === 0) throw new Error('catálogo vacío');
    // Reemplazar opciones con las de la BD
    select.innerHTML = '<option value="">Selecciona un servicio</option>';
    servicios.forEach(s => {
      const opt = document.createElement('option');
      opt.value = s.idServicio ?? s.IdServicio ?? '';
      opt.textContent = s.nombre ?? s.Nombre ?? '';
      select.appendChild(opt);
    });
  } catch (err) {
    console.warn('[SmileTrack] No se pudieron cargar servicios desde API:', err);
    select.innerHTML = '<option value="">No hay servicios disponibles</option>';
  }
};

const initNuevaCitaModal = () => {
  const btn = safeGetElement('btnNuevaCita');
  const modal = safeGetElement('modalNuevaCita');
  if (!modal || !btn) return;

  // Bloquear fechas pasadas: min = hoy
  const fechaInput = safeGetElement('citaFecha');
  if (fechaInput) {
    fechaInput.min = new Date().toISOString().split('T')[0];
  }

  const cerrar = () => {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    const o = modal.dataset.opener;
    if (o) safeGetElement(o)?.focus();
  };

  btn.addEventListener('click', () => {
    modal.dataset.opener = 'btnNuevaCita';
    // Actualizar min en caso de que cambie el día
    if (fechaInput) fechaInput.min = new Date().toISOString().split('T')[0];
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    fechaInput?.focus();
    // Cargar servicios desde BD en cada apertura (por si cambia la lista)
    cargarServiciosEnModal();
  });

  safeGetElement('closeNuevaCita')?.addEventListener('click', cerrar);
  safeGetElement('cancelarNuevaCita')?.addEventListener('click', cerrar);
  modal.addEventListener('click', e => { if (e.target === modal) cerrar(); });

  // Validación adicional en blur para fecha
  fechaInput?.addEventListener('change', () => {
    const hoy = new Date().toISOString().split('T')[0];
    if (fechaInput.value < hoy) {
      fechaInput.value = hoy;
      notify('warning', 'Fecha inválida', 'No puedes seleccionar fechas pasadas. Se ha ajustado al día de hoy.');
    }
  });

  safeGetElement('confirmarNuevaCita')?.addEventListener('click', async () => {
    const fecha = safeGetElement('citaFecha');
    const servicio = safeGetElement('citaServicio');
    const nota = safeGetElement('citaNota');
    const btnConfirmar = safeGetElement('confirmarNuevaCita');

    // Validar que la fecha esté seleccionada
    if (!fecha?.value) {
      fecha?.focus();
      if (fecha) { fecha.style.borderColor = 'var(--orange)'; setTimeout(() => fecha.style.borderColor = '', 2000); }
      notify('warning', 'Falta la fecha', 'Por favor selecciona una fecha para la cita.');
      return;
    }
    // Validar que la fecha no sea pasada
    const hoy = new Date().toISOString().split('T')[0];
    if (fecha.value < hoy) {
      fecha.value = hoy;
      notify('warning', 'Fecha inválida', 'No puedes solicitar citas en fechas pasadas.');
      return;
    }

    // Preparar body — los nombres deben coincidir exactamente con CitaSolicitudPacienteDto del backend
    const body = {
      Fecha: fecha.value,
      IdServicio: servicio?.value ? Number(servicio.value) : 0,
      Notas: nota?.value?.trim() || null
    };

    // Deshabilitar botón y mostrar spinner
    if (btnConfirmar) { btnConfirmar.disabled = true; btnConfirmar.textContent = 'Enviando…'; }

    try {
      const csrfToken = getCsrfToken();
      const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
      if (csrfToken) headers['X-CSRF-TOKEN'] = csrfToken;
      try {
        const jwt = sessionStorage.getItem('st_jwt');
        if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
      } catch { /* modo privado */ }

      const res = await fetch('/api/citas/solicitar', {
        method: 'POST',
        credentials: 'same-origin',
        headers,
        body: JSON.stringify(body)
      });

      let payload;
      try { payload = await res.json(); } catch { payload = { success: res.ok }; }

      if (res.ok && payload.success !== false) {
        cerrar();
        notify('success', '¡Solicitud enviada!',
          'Tu solicitud de cita fue recibida. El equipo de recepción la revisará y te confirmará los detalles en breve.');
        // Recargar la tabla para que aparezca la nueva cita en estado "Solicitada"
        await fetchAppointments();
        animateCounters();
        updateStats();
        renderTable();
      } else {
        const msg = payload.message || 'No fue posible enviar tu solicitud. Inténtalo de nuevo.';
        notify('error', 'Error al solicitar', msg);
      }
    } catch (err) {
      console.warn('[SmileTrack] Error al solicitar cita:', err);
      notify('error', 'Sin conexión', 'No fue posible conectar con el servidor. Verifica tu conexión y vuelve a intentarlo.');
    } finally {
      if (btnConfirmar) { btnConfirmar.disabled = false; btnConfirmar.textContent = 'Solicitar'; }
    }
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
      return;
    }
    throw new Error('payload inválido');
  } catch (err) {
    console.warn('[SmileTrack] Mis citas paciente: no se pudo cargar la API:', err);
    citas = [];
    mostrarErrorUsuario('No fue posible consultar tus citas. La lista está vacía hasta recuperar la conexión con la API.');
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INIT PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  try {
    // La API es la única fuente de verdad; no se usan datos locales como respaldo.
    citas = [];
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
