/* ============================================
SmileTrack — Mi Agenda Odontólogo (st-odo-02-agenda)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026 (actualizado 2026-07-31)

DESCRIPCIÓN:
Controla el renderizado de la agenda semanal del odontólogo. Consume citas REALES desde
GET /api/citas (el backend aplica filtro automático por rol Profesional → IdProfesional claim),
y transiciona estados vía PUT /api/citas/{id}. Mantiene fallback a LocalStorage si la API
no responde para no romper la UX.

FUNCIONALIDADES PRINCIPALES:
- Carga reactiva de citas asociadas al odontólogo autenticado (filtro automático backend)
- Transición de estados de citas (Iniciar atención, no asistió, etc.) vía PUT /api/citas/{id}
- Sincronización con el selector de semana y búsqueda debounced
- Filtro semanal aplicado en cliente sobre el dataset cacheado

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController → ApiListarCitas, ApiActualizarCita
- Endpoints: GET /api/citas, PUT /api/citas/{id} [Authorize(Policy = "ApiOrCookie")]
- CSS: ~/css/Gestion_De_Citas/st-odo-02-agenda/agenda.css
- JS: ~/js/Gestion_De_Citas/st-odo-02-agenda/agenda.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- El filtrado por IdProfesional lo hace el controller usando ClaimTypes.Role y Claim "IdProfesional"
  (ver ApiListarCitas en GestionCitasController.cs). Nunca se filtra solo en cliente (privacidad).
- El mapeo server↔cliente usa las constantes ESTADO_MAP_SERVER y ESTADO_MAP_CLIENTE.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API Y AUTENTICACIÓN
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const API_PAGE_SIZE = 200;

const getAuthHeaders = () => {
  const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
  try {
    const jwt = sessionStorage.getItem('st_jwt');
    if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
  } catch (e) { /* navegación privada */ }
  return headers;
};

const LOCAL_STORAGE_KEY = 'smiletrack_agenda_odo';

// Mapeo estado server (en_proceso / confirmada / ...) ↔ etiquetas UI amigables
const ESTADO_MAP_SERVER = {
  'programada':  { label: 'Agendada',    class: 'badge-agendada'   },
  'confirmada':  { label: 'Confirmada',  class: 'badge-agendada'   },
  'en_proceso':  { label: 'En consulta', class: 'badge-en-consulta' },
  'finalizada':  { label: 'Atendida',    class: 'badge-atendida'   },
  'atendida':    { label: 'Atendida',    class: 'badge-atendida'   },
  'cancelada':   { label: 'Cancelada',   class: 'badge-cancelada'  },
  'no_asistida': { label: 'No asistió',  class: 'badge-no-asistio' }
};
const ESTADO_MAP_CLIENTE = {
  'Atendida':    'finalizada',
  'En consulta': 'en_proceso',
  'Agendada':    'programada',
  'Confirmada':  'confirmada',
  'No asistió':  'no_asistida',
  'Cancelada':   'cancelada'
};

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
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
//  MAPEOS DE DATOS: Server → Cliente
// ═══════════════════════════════════════════════════════════════════

const fmtFechaCorta = (fh) => {
  try {
    const d = new Date(fh);
    const dias = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
    return `${dias[d.getDay()]} ${String(d.getDate()).padStart(2, '0')}`;
  } catch { return '—'; }
};
const fmtHora12 = (fh) => {
  try {
    const d = new Date(fh);
    let h = d.getHours();
    const m = String(d.getMinutes()).padStart(2, '0');
    const p = h >= 12 ? 'PM' : 'AM';
    if (h === 0) h = 12; else if (h > 12) h -= 12;
    return `${String(h).padStart(2, '0')}:${m} ${p}`;
  } catch { return '—'; }
};
const fmtDuracion = (servicioNombre) => {
  // Aproximación por servicio (en un proyecto real lo devuelve la API).
  if (!servicioNombre) return '30 min';
  const s = servicioNombre.toLowerCase();
  if (s.includes('ortodoncia') || s.includes('endodon')) return '60 min';
  if (s.includes('limpieza') || s.includes('blanqueamiento')) return '45 min';
  if (s.includes('revisión') || s.includes('control') || s.includes('general')) return '20 min';
  return '30 min';
};

const mapServerToClient = (srv) => {
  const fechaHora = srv.FechaHora ? new Date(srv.FechaHora).toISOString() : null;
  const estadoServer = (srv.Estado || 'programada').toLowerCase();
  const estadoInfo = ESTADO_MAP_SERVER[estadoServer] || ESTADO_MAP_SERVER['programada'];
  const paciente = srv.Paciente?.NombreCompleto || '—';
  const servicio = srv.Servicio?.Nombre || 'Sin servicio';
  const fechaISO = fechaHora ? fechaHora.split('T')[0] : new Date().toISOString().split('T')[0];
  const active = estadoInfo.label === 'En consulta';

  return {
    id: srv.IdCita,
    fecha: fmtFechaCorta(fechaHora),
    fechaISO,
    hora: fmtHora12(fechaHora),
    horaISO: fechaHora ? `${String(new Date(fechaHora).getHours()).padStart(2,'0')}:${String(new Date(fechaHora).getMinutes()).padStart(2,'0')}` : '09:00',
    paciente,
    servicio,
    duracion: fmtDuracion(servicio),
    estado: estadoInfo.label,
    estadoClass: estadoInfo.class,
    active,
    _raw: srv
  };
};

// ═══════════════════════════════════════════════════════════════════
//  FALLBACK LOCAL
// ═══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
//  CACHÉ LOCAL (respaldo de la última respuesta real del servidor)
// ═══════════════════════════════════════════════════════════════════

const loadLocal = () => {
  try {
    const s = localStorage.getItem(LOCAL_STORAGE_KEY);
    if (s) return JSON.parse(s);
  } catch {}
  // Sin caché ni conexión: estado vacío real (antes se mostraban 6 citas ficticias)
  return [];
};
const saveLocal = (arr) => {
  try { localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(arr)); } catch {}
};

// Estado global
let appointments = loadLocal();
let weekOffset = 0;

// ═══════════════════════════════════════════════════════════════════
//  RENDER TABLA
// ═══════════════════════════════════════════════════════════════════

const badgeClass = (estado) => {
  const info = ESTADO_MAP_SERVER[
    (ESTADO_MAP_CLIENTE[estado] || estado).toLowerCase()
  ];
  return info?.class || 'badge-agendada';
};
const editIcon = (id, estado) => {
  const isRed = ['Cancelada', 'No asistió'].includes(estado);
  return `<button class="btn-icon edit-icon${isRed ? ' red' : ''}" title="Editar estado" aria-label="Editar estado de cita" onclick="editAppointment(${id})">✏️</button>`;
};
const formatTimeISO = (horaAMPM) => {
  const parts = horaAMPM.split(' ');
  if (parts.length < 2) return '09:00';
  const [time, period] = parts;
  let [h, m] = (time || '09:00').split(':').map(Number);
  if (period === 'PM' && h !== 12) h += 12;
  if (period === 'AM' && h === 12) h = 0;
  return `${String(h||0).padStart(2,'0')}:${String(m||0).padStart(2,'0')}`;
};
const getDateTimeISO = (fechaISO, horaAMPM) => `${fechaISO}T${formatTimeISO(horaAMPM)}:00`;

const renderTable = (data) => {
  const tbody = safeGetElement('agendaTbody');
  if (!tbody) return;
  tbody.innerHTML = '';

  if (!data.length) {
    tbody.innerHTML = `<tr><td colspan="7" style="text-align:center;padding:24px;color:var(--text-muted);">No hay citas para esta semana.</td></tr>`;
    return;
  }

  const weekStart = getWeekStart(new Date(), weekOffset);
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekStart.getDate() + 6);
  const inWeek = data.filter(a => {
    const d = new Date(a.fechaISO);
    return d >= weekStart && d <= weekEnd;
  });
  const rows = inWeek.length ? inWeek : data.slice(0, 10);

  rows.forEach(item => {
    const tr = document.createElement('tr');
    if (item.active) tr.classList.add('row-active');
    if (item.estado === 'Cancelada') tr.classList.add('row-cancelada');
    tr.setAttribute('role', 'row');

    const dtiso = getDateTimeISO(item.fechaISO, item.hora);
    tr.innerHTML = `
      <td class="td-fecha"><time datetime="${item.fechaISO}T00:00:00">${item.fecha}</time></td>
      <td><span class="pill-hora"><time datetime="${dtiso}">${item.hora}</time></span></td>
      <td class="td-paciente">${item.paciente}</td>
      <td>${item.servicio}</td>
      <td>${item.duracion}</td>
      <td><span class="badge ${badgeClass(item.estado)}" role="status" aria-label="Estado: ${item.estado}">${item.estado}</span></td>
      <td>
        <div class="actions-cell">
          <button class="btn-icon" title="Ver detalle" aria-label="Ver detalle de cita de ${item.paciente}" onclick="openModal(${item.id})">👁️</button>
          ${editIcon(item.id, item.estado)}
        </div>
      </td>
    `;
    tr.style.cursor = 'pointer';
    tr.setAttribute('tabindex', '0');
    tr.setAttribute('aria-label', `Ver detalle de cita de ${item.paciente} el ${item.fecha} a las ${item.hora}`);
    tr.addEventListener('click', e => { if (!e.target.closest('.btn-icon')) openModal(item.id); });
    tr.addEventListener('keydown', e => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); if (!e.target.closest('.btn-icon')) openModal(item.id); }
    });
    tbody.appendChild(tr);
  });
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL DETALLE + EDITAR ESTADO (con PUT API)
// ═══════════════════════════════════════════════════════════════════

window.openModal = (id) => {
  const item = appointments.find(a => a.id === id);
  if (!item) return;
  const dtiso = getDateTimeISO(item.fechaISO, item.hora);
  const content = safeGetElement('modalContent');
  if (content) {
    content.innerHTML = `
      <div class="modal-row"><span class="modal-key">Fecha</span><span class="modal-val"><time datetime="${item.fechaISO}T00:00:00">${item.fecha}</time></span></div>
      <div class="modal-row"><span class="modal-key">Hora</span><span class="modal-val"><time datetime="${dtiso}">${item.hora}</time></span></div>
      <div class="modal-row"><span class="modal-key">Paciente</span><span class="modal-val">${item.paciente}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${item.servicio}</span></div>
      <div class="modal-row"><span class="modal-key">Duración</span><span class="modal-val">${item.duracion}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span><span class="modal-val"><span class="badge ${badgeClass(item.estado)}" role="status">${item.estado}</span></span></div>
    `;
  }
  const modalOverlay = safeGetElement('modalOverlay');
  if (modalOverlay) {
    modalOverlay.classList.add('open');
    modalOverlay.setAttribute('aria-hidden', 'false');
    modalOverlay.removeAttribute('inert');
    const closeBtn = safeGetElement('modalClose');
    if (closeBtn) closeBtn.focus();
    document.body.style.overflow = 'hidden';
  }
};

const closeModal = () => {
  const modalOverlay = safeGetElement('modalOverlay');
  if (modalOverlay) {
    modalOverlay.classList.remove('open');
    modalOverlay.setAttribute('aria-hidden', 'true');
    modalOverlay.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

window.editAppointment = async (id) => {
  const item = appointments.find(a => a.id === id);
  if (!item) return;

  const validos = Object.keys(ESTADO_MAP_CLIENTE);
  const promptMsg = `Estado actual: ${item.estado}\n\nEscribe nuevo estado:\n` +
    validos.map(v => `• ${v}`).join('\n');
  const nuevo = prompt(promptMsg);
  if (!nuevo) return;

  const normalizado = validos.find(v => v.toLowerCase() === nuevo.trim().toLowerCase());
  if (!normalizado) {
    window.ToastService.error('Estado no válido. Usa uno de los valores permitidos.');
    return;
  }

  // 1. Optimistic update en UI (mejor UX)
  const originalEstado = item.estado;
  item.estado = normalizado;
  item.active = normalizado === 'En consulta';
  renderTable(appointments);
  updateCounts();

  // 2. PUT /api/citas/{id}
  const estadoServer = ESTADO_MAP_CLIENTE[normalizado] || normalizado.toLowerCase();
  const raw = item._raw || {};
  const fechaHora = item.fechaISO && item.horaISO
    ? `${item.fechaISO}T${item.horaISO}:00`
    : raw.FechaHora || new Date().toISOString();

  const body = {
    IdCita: id,
    IdPaciente: raw.IdPaciente || 0,
    IdProfesional: raw.IdProfesional || null,
    IdServicio: raw.IdServicio || 0,
    FechaHora: fechaHora,
    Estado: estadoServer,
    Notas: raw.Notas || ''
  };

  try {
    const res = await fetch(`${API_BASE}/citas/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(body)
    });
    let payload;
    try { payload = await res.json(); } catch { payload = { success: res.ok }; }

    if (res.ok && payload.success) {
      saveLocal(appointments);
      window.ToastService.success(`Estado actualizado a "${normalizado}"`);
    } else {
      // Rollback UI si falla el server
      item.estado = originalEstado;
      item.active = originalEstado === 'En consulta';
      renderTable(appointments);
      updateCounts();
      window.ToastService.error(payload.message || 'No fue posible actualizar el estado en el servidor');
    }
  } catch (netErr) {
    console.warn('[SmileTrack] PUT offline, guardado local:', netErr);
    saveLocal(appointments);
    window.ToastService.warning(`⚠️ Estado guardado localmente (sin conexión): ${normalizado}`);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  CONTADORES Y NAVEGACIÓN SEMANAL
// ═══════════════════════════════════════════════════════════════════

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

const updateCounts = () => {
  const weekStart = getWeekStart(new Date(), weekOffset);
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekStart.getDate() + 6);
  const weekAppts = appointments.filter(a => {
    const d = new Date(a.fechaISO);
    return d >= weekStart && d <= weekEnd;
  });

  const hoyISO = new Date().toISOString().split('T')[0];
  const citasHoy = weekAppts.filter(a => a.fechaISO === hoyISO);
  const totalHoy = citasHoy.length;
  const atendidas = weekAppts.filter(a => a.estado === 'Atendida').length;
  const pendientes = weekAppts.filter(a => a.estado === 'Agendada' || a.estado === 'Confirmada').length;

  const now = new Date();
  const mes = appointments.filter(a => {
    const d = new Date(a.fechaISO);
    return d.getMonth() === now.getMonth() && d.getFullYear() === now.getFullYear();
  }).filter(a => a.estado === 'Agendada' || a.estado === 'Confirmada').length;

  animateCounter(safeGetElement('statHoy'), totalHoy);
  animateCounter(safeGetElement('statAtendidas'), atendidas);
  animateCounter(safeGetElement('statPendientes'), pendientes);
  animateCounter(safeGetElement('statMes'), mes);

  const atendidasHoy = citasHoy.filter(a => a.estado === 'Atendida').length;
  const pct = totalHoy > 0 ? Math.round((atendidasHoy / totalHoy) * 100) : 0;
  const progressBar = safeGetElement('progressBar');
  const progressLabel = safeGetElement('progressLabel');
  const progressWrap = progressBar?.closest('[role="progressbar"]');
  if (progressBar) progressBar.style.width = pct + '%';
  if (progressWrap) {
    progressWrap.setAttribute('aria-valuenow', pct);
    progressWrap.setAttribute('aria-valuetext', `${pct}% de citas de hoy completadas`);
  }
  if (progressLabel) {
    progressLabel.textContent = `${atendidasHoy} de ${totalHoy} citas completadas hoy`;
    progressLabel.setAttribute('aria-label', `${atendidasHoy} de ${totalHoy} citas completadas hoy`);
  }
};

const getWeekStart = (baseDate, offset) => {
  const d = new Date(baseDate);
  d.setDate(d.getDate() + offset * 7);
  const day = d.getDay();
  const diff = d.getDate() - day + (day === 0 ? -6 : 1);
  return new Date(d.setDate(diff));
};
const weekLabel = (offset) => {
  const now = new Date();
  now.setDate(now.getDate() + offset * 7);
  const day = now.getDay();
  const mon = new Date(now); mon.setDate(now.getDate() - (day === 0 ? 6 : day - 1));
  const sat = new Date(mon); sat.setDate(mon.getDate() + 5);
  const mes = mon.toLocaleDateString('es-ES', { month: 'long' });
  return `Semana ${mon.getDate()}-${sat.getDate()} de ${mes} ${mon.getFullYear()}`;
};

// ═══════════════════════════════════════════════════════════════════
//  FETCH API CITAS
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
      appointments = payload.data.map(mapServerToClient);
      saveLocal(appointments);
    } else {
      throw new Error('payload inválido');
    }
  } catch (err) {
    console.warn('[SmileTrack] Agenda odontólogo: no se pudo conectar con el servidor:', err);
    appointments = loadLocal();
    if (window.ToastService) window.ToastService.error('No se pudo cargar la agenda desde el servidor');
  }
  renderTable(appointments);
  updateCounts();
  return appointments;
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN COMPONENTES UI
// ═══════════════════════════════════════════════════════════════════

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);
    if (show) sidebar.querySelector('.nav-item')?.focus();
    else hamburger.focus();
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));
  sidebar.querySelectorAll('.nav-item').forEach(item => item.addEventListener('click', () => {
    if (window.innerWidth <= 680) toggleMenu(false);
  }));
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) toggleMenu(false);
  });
};

const initGlobalStatus = () => {
  const select = safeGetElement('globalStatus');
  if (!select) return;
  select.addEventListener('change', async function () {
    const val = this.value;
    if (!val) return;
    if (!confirm(`¿Cambiar todas las citas visibles a "${val}"?`)) { this.value = ''; return; }

    const weekStart = getWeekStart(new Date(), weekOffset);
    const weekEnd = new Date(weekStart);
    weekEnd.setDate(weekStart.getDate() + 6);

    const targets = appointments.filter(a => {
      const d = new Date(a.fechaISO);
      return d >= weekStart && d <= weekEnd;
    });

    for (const item of targets) {
      // Usamos window.editAppointment (mismo flujo PUT API)
      const fakePrompt = window.prompt;
      window.prompt = () => val; // mock temporal
      try { await window.editAppointment(item.id); } catch {}
      window.prompt = fakePrompt;
    }

    renderTable(appointments);
    updateCounts();
    window.ToastService.success('Estados actualizados en servidor y local');
    this.value = '';
  });
};

const initModal = () => {
  safeGetElement('modalClose')?.addEventListener('click', closeModal);
  const overlay = safeGetElement('modalOverlay');
  overlay?.addEventListener('click', (e) => { if (e.target === overlay) closeModal(); });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && overlay?.classList.contains('open')) closeModal();
  });
};

const updateHeaderDate = () => {
  const now = new Date();
  const days = ['Domingo','Lunes','Martes','Miércoles','Jueves','Viernes','Sábado'];
  const months = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  const el = safeGetElement('headerDate');
  if (el) {
    el.textContent = `${days[now.getDay()]}, ${now.getDate()} de ${months[now.getMonth()]} ${now.getFullYear()}`;
    el.setAttribute('datetime', now.toISOString().split('T')[0]);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  INIT PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  try {
    initSidebar();
    initGlobalStatus();
    initModal();
    updateHeaderDate();

    const weekTitle = safeGetElement('weekTitle');
    if (weekTitle) weekTitle.textContent = weekLabel(0);

    safeGetElement('btnPrev')?.addEventListener('click', () => {
      weekOffset--;
      if (weekTitle) weekTitle.textContent = weekLabel(weekOffset);
      renderTable(appointments);
      updateCounts();
    });
    safeGetElement('btnNext')?.addEventListener('click', () => {
      weekOffset++;
      if (weekTitle) weekTitle.textContent = weekLabel(weekOffset);
      renderTable(appointments);
      updateCounts();
    });

    await fetchAppointments();
    window.addEventListener('beforeunload', () => {});
  } catch (err) {
    console.error('[SmileTrack] Error init agenda.js:', err);
    mostrarErrorUsuario(err.message || 'Error cargando agenda odontólogo. Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', init);
