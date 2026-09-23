// ===== CHROME COMPARTIDO (sidebar / hamburger / toast) =====
const safeGetElement = (id) => document.getElementById(id);

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast${type === 'error' ? ' error' : type === 'warning' ? ' warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
  };

  hamburger.addEventListener('click', () => toggleMenu(!sidebar.classList.contains('open')));
  overlay.addEventListener('click', () => toggleMenu(false));
};

// ===== CLIENTE API LOCAL (autocontenido, sin depender de wwwroot/js/lib/apiRequest.js) =====
function getPqrAntiforgeryToken() {
  const hidden = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (hidden) return hidden;

  const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  return match ? decodeURIComponent(match[2]) : null;
}

async function pqrApiRequest(path, options = {}) {
  const opts = {
    method: options.method || 'GET',
    headers: options.headers || {},
    body: options.body,
    credentials: 'same-origin'
  };

  const method = opts.method.toUpperCase();
  if (!['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method)) {
    const csrfToken = getPqrAntiforgeryToken();
    if (csrfToken) opts.headers = { ...opts.headers, 'X-CSRF-TOKEN': csrfToken };
  }

  if (opts.body && typeof opts.body === 'object') {
    opts.headers['Content-Type'] = opts.headers['Content-Type'] || 'application/json';
    opts.body = JSON.stringify(opts.body);
  }
  opts.headers['Accept'] = opts.headers['Accept'] || 'application/json';

  try {
    const res = await fetch(path, opts);
    if (res.status === 401) {
      window.location.href = '/acceso-y-seguridad/login';
      return null;
    }
    const contentType = res.headers.get('content-type') || '';
    if (contentType.includes('application/json')) return await res.json();
    return await res.text();
  } catch (err) {
    console.error('[pqrApiRequest] Error:', err);
    throw err;
  }
}

// ===== DATOS REALES (SQL Server, vía PqrController) =====

const ESTADO_BADGE = {
  recibida: { label: 'Recibida', cls: 'badge-recibido' },
  en_proceso: { label: 'En proceso', cls: 'badge-proceso' },
  resuelta: { label: 'Resuelta', cls: 'badge-resuelto' },
  cerrada: { label: 'Cerrada', cls: 'badge-cerrado' },
  rechazada: { label: 'Rechazada', cls: 'badge-cerrado' }
};

const TIPO_BADGE = {
  queja: { label: 'Queja', cls: 'badge-queja' },
  reclamo: { label: 'Reclamo', cls: 'badge-reclamo' },
  peticion: { label: 'Petición', cls: 'badge-peticion' },
  sugerencia: { label: 'Sugerencia', cls: 'badge-peticion' }
};

const PRIORIDAD_BADGE = {
  baja: { label: 'Baja', cls: 'badge-prioridad-baja' },
  media: { label: 'Media', cls: 'badge-prioridad-media' },
  alta: { label: 'Alta', cls: 'badge-prioridad-alta' },
  urgente: { label: 'Alta', cls: 'badge-prioridad-alta' }
};

// data-estado usado por el filtro de la tabla (coincide 1:1 con los botones de estado)
const ESTADO_FILTRO = {
  recibida: 'recibido',
  en_proceso: 'proceso',
  resuelta: 'resuelto',
  cerrada: 'cerrado',
  rechazada: 'cerrado'
};

let pqrsData = {};
let currentId = null;

const formatTiempoAbierto = (fechaIso) => {
  if (!fechaIso) return '—';
  const ms = Date.now() - new Date(fechaIso).getTime();
  const dias = Math.floor(ms / (1000 * 60 * 60 * 24));
  const horas = Math.floor((ms / (1000 * 60 * 60)) % 24);
  return `${dias}d ${horas}h`;
};

const renderTable = () => {
  const body = safeGetElement('pqrsTableBody');
  if (!body) return;

  const ids = Object.keys(pqrsData);
  if (ids.length === 0) {
    body.innerHTML = '<tr><td colspan="8" style="text-align:center; padding: 24px;">No hay PQR registradas.</td></tr>';
    return;
  }

  body.innerHTML = ids.map(id => {
    const p = pqrsData[id];
    const tipoBadge = TIPO_BADGE[p.tipo] || TIPO_BADGE.peticion;
    const estadoBadge = ESTADO_BADGE[p.estado] || ESTADO_BADGE.recibida;
    const prioridadBadge = PRIORIDAD_BADGE[p.prioridad] || PRIORIDAD_BADGE.media;
    const vencida = !['resuelta', 'cerrada', 'rechazada'].includes(p.estado)
      && (Date.now() - new Date(p.fechaCreacionIso).getTime()) / (1000 * 60 * 60 * 24) > 15;

    return `
      <tr data-id="${id}" data-tipo="${p.tipo}" data-estado="${ESTADO_FILTRO[p.estado] || 'recibido'}">
        <td class="radicado">${p.radicado}</td>
        <td class="paciente-info">
          <strong>${p.paciente}</strong>
          <span>${p.documento}</span>
        </td>
        <td><span class="badge ${tipoBadge.cls}">${tipoBadge.label}</span></td>
        <td>${p.titulo}</td>
        <td><span class="badge ${estadoBadge.cls}">${estadoBadge.label}</span></td>
        <td><span class="badge ${prioridadBadge.cls}">${prioridadBadge.label}</span></td>
        <td class="${vencida ? 'tiempo-vencido' : ''}">${formatTiempoAbierto(p.fechaCreacionIso)}${vencida ? ' <i class="fas fa-exclamation-triangle"></i>' : ''}</td>
        <td>
          <button class="action-btn" onclick="showDetail(${id})" title="Ver detalle">
            <i class="fas fa-eye"></i>
          </button>
        </td>
      </tr>`;
  }).join('');
};

const renderStats = () => {
  const stats = window.RAZOR_PQR_STATS;
  if (!stats) return;
  const setText = (id, val) => { const el = safeGetElement(id); if (el) el.textContent = val; };
  setText('statTotal', stats.total);
  setText('statSinResponder', stats.sinResponder);
  setText('statEnGestion', stats.enGestion);
  setText('statVencidas', stats.vencidas);
};

document.addEventListener('DOMContentLoaded', () => {
  initSidebar();

  if (Array.isArray(window.RAZOR_PQRS)) {
    window.RAZOR_PQRS.forEach(p => {
      pqrsData[p.id] = {
        radicado: p.ticket,
        titulo: p.subject,
        tipo: p.type,
        estado: p.status,
        prioridad: p.priority,
        paciente: p.patient,
        documento: p.documento,
        email: p.email,
        fecha: p.date,
        fechaCreacionIso: p.fechaCreacionIso,
        descripcion: p.description,
        respuesta: p.respuesta,
        fechaRespuesta: p.fechaRespuesta,
        atendidaPor: p.atendidaPor,
        evidenciaAdjunto: p.evidenciaAdjunto
      };
    });
  }

  renderTable();
  renderStats();
  initResponseForm();
  initPqrNotifications();
});

// ===== NOTIFICACIONES Y ACTUALIZACIÓN AUTOMÁTICA =====
let pqrPollingTimer = null;
let pqrFirstRefresh = true;

function updateNotificationBadge(stats) {
  const badge = safeGetElement('pqrNotificationBadge');
  if (!badge) return;

  const count = Number(stats?.sinResponder || 0);
  badge.textContent = count > 99 ? '99+' : String(count);
  badge.classList.toggle('is-hidden', count === 0);

  const button = safeGetElement('pqrNotificationBtn');
  if (button) {
    const label = count > 0
      ? `Nuevas solicitudes PQR: ${count}`
      : 'No hay nuevas solicitudes PQR';
    button.title = label;
    button.setAttribute('aria-label', label);
  }
}

function applyPqrPayload(payload, { showNewToast = false } = {}) {
  if (!payload || payload.success !== true || !Array.isArray(payload.pqrs)) return;

  const previousIds = new Set(Object.keys(pqrsData));

  pqrsData = {};
  payload.pqrs.forEach(p => {
    pqrsData[p.id] = {
      radicado: p.ticket,
      titulo: p.subject,
      tipo: p.type,
      estado: p.status,
      prioridad: p.priority,
      paciente: p.patient,
      documento: p.documento,
      email: p.email,
      fecha: p.date,
      fechaCreacionIso: p.fechaCreacionIso,
      descripcion: p.description,
      respuesta: p.respuesta,
      fechaRespuesta: p.fechaRespuesta,
      atendidaPor: p.atendidaPor,
      evidenciaAdjunto: p.evidenciaAdjunto
    };
  });

  window.RAZOR_PQR_STATS = payload.stats;
  renderTable();
  renderStats();
  updateNotificationBadge(payload.stats);
  filterTable();

  if (currentId && pqrsData[currentId]) {
    showDetail(currentId);
  } else if (currentId) {
    closeDetail();
  }

  if (showNewToast) {
    const newPqrs = payload.pqrs.filter(p => !previousIds.has(String(p.id)));
    if (newPqrs.length > 0) {
      const newest = newPqrs[0];
      showToast(`Nueva PQR recibida: ${newest.ticket}`, 'warning');
    }
  }
}

async function refreshPqrsFromServer() {
  try {
    const response = await fetch('/gestion-de-pqr/api/pqr', {
      method: 'GET',
      credentials: 'same-origin',
      headers: { 'Accept': 'application/json' }
    });

    if (response.status === 401) {
      window.location.href = '/acceso-y-seguridad/login';
      return;
    }

    if (!response.ok) return;

    const payload = await response.json();
    applyPqrPayload(payload, { showNewToast: !pqrFirstRefresh });
    pqrFirstRefresh = false;
  } catch (error) {
    console.error('Error actualizando la bandeja de PQR:', error);
  }
}

function initPqrNotifications() {
  const button = safeGetElement('pqrNotificationBtn');
  button?.addEventListener('click', () => {
    const select = safeGetElement('filterEstado');
    if (select) {
      select.value = 'recibido';
      filterTable();
    }

    safeGetElement('pqrsTable')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  });

  updateNotificationBadge(window.RAZOR_PQR_STATS);

  // Consulta periódica para que las nuevas PQR aparezcan sin recargar la página.
  refreshPqrsFromServer();
  pqrPollingTimer = window.setInterval(refreshPqrsFromServer, 5000);
}

// Show Management (la vista actual ya es la bandeja de gestión)
function showManagement() {}

// Show Detail Panel
function showDetail(id) {
  currentId = id;
  const data = pqrsData[id];
  if (!data) return;

  document.querySelectorAll('tbody tr').forEach(row => row.classList.remove('selected'));
  const selectedRow = document.querySelector(`tr[data-id="${id}"]`);
  if (selectedRow) selectedRow.classList.add('selected');

  document.getElementById('emptyState').style.display = 'none';
  document.getElementById('detailContent').style.display = 'block';

  document.getElementById('detailTitle').textContent = data.titulo;
  document.getElementById('detailRadicado').textContent = data.radicado;
  document.getElementById('infoRadicado').textContent = data.fecha;
  document.getElementById('infoTiempo').textContent = formatTiempoAbierto(data.fechaCreacionIso);
  document.getElementById('infoPaciente').textContent = data.paciente;
  document.getElementById('infoDocumento').textContent = data.documento;
  document.getElementById('infoEmail').textContent = data.email;
  document.getElementById('detailDescripcion').textContent = data.descripcion;

  const evidenciaSection = document.getElementById('evidenciaSection');
  const evidenciaLink = document.getElementById('evidenciaLink');
  if (data.evidenciaAdjunto) {
    evidenciaLink.href = `/${data.evidenciaAdjunto}`;
    evidenciaSection.style.display = 'block';
  } else {
    evidenciaSection.style.display = 'none';
  }

  renderResponseThread(data);
  updateBadgeColors(data);

  const textarea = document.getElementById('responseTextarea');
  if (textarea) textarea.value = '';

  if (window.innerWidth <= 1200) {
    document.getElementById('detailPanel').classList.add('show');
    document.querySelector('.overlay').classList.add('show');
  }

  updateStatusButtons(data.estado);
}

function renderResponseThread(data) {
  const thread = document.getElementById('responsesThread');
  const title = document.getElementById('responsesTitle');
  if (!thread || !title) return;

  if (data.respuesta) {
    title.textContent = 'Hilo de Respuestas (1)';
    thread.innerHTML = `
      <div class="timeline-item">
        <div class="timeline-header">
          <span class="timeline-author">${data.atendidaPor || 'Administrador'}</span>
          <span class="timeline-date">${data.fechaRespuesta || ''}</span>
        </div>
        <div class="timeline-content">${data.respuesta}</div>
      </div>`;
  } else {
    title.textContent = 'Hilo de Respuestas (0)';
    thread.innerHTML = '<p style="color: var(--text-light, #888); font-size: 14px;">Aún no se ha respondido esta solicitud.</p>';
  }
}

function updateBadgeColors(data) {
  const tipoBadge = TIPO_BADGE[data.tipo] || TIPO_BADGE.peticion;
  const estadoBadge = ESTADO_BADGE[data.estado] || ESTADO_BADGE.recibida;
  const prioridadBadge = PRIORIDAD_BADGE[data.prioridad] || PRIORIDAD_BADGE.media;

  const tipoEl = document.getElementById('detailTipo');
  tipoEl.className = `badge ${tipoBadge.cls}`;
  tipoEl.textContent = tipoBadge.label;

  const estadoEl = document.getElementById('detailEstado');
  estadoEl.className = `badge ${estadoBadge.cls}`;
  estadoEl.textContent = estadoBadge.label;

  const prioridadEl = document.getElementById('detailPrioridad');
  prioridadEl.className = `badge ${prioridadBadge.cls}`;
  prioridadEl.textContent = prioridadBadge.label;
}

function updateStatusButtons(estado) {
  document.querySelectorAll('.status-btn').forEach(btn => btn.classList.remove('active'));
  const map = { recibida: 'recibido', en_proceso: 'proceso', resuelta: 'resuelto', cerrada: 'cerrado', rechazada: 'cerrado' };
  const btnClass = map[estado];
  if (btnClass) {
    const btn = document.querySelector(`.status-btn.${btnClass}`);
    if (btn) btn.classList.add('active');
  }
}

function closeDetail() {
  document.getElementById('detailContent').style.display = 'none';
  document.getElementById('emptyState').style.display = 'flex';
  document.getElementById('detailPanel').classList.remove('show');
  document.querySelector('.overlay').classList.remove('show');
  document.querySelectorAll('tbody tr').forEach(row => row.classList.remove('selected'));
  currentId = null;
}

// Change Status — persiste en SQL Server vía PUT /gestion-de-pqr/api/pqr/{id}/estado
async function changeStatus(btn, estado) {
  if (!currentId) return;

  try {
    const result = await pqrApiRequest(`/gestion-de-pqr/api/pqr/${currentId}/estado`, {
      method: 'PUT',
      body: { estado }
    });

    if (!result || result.success !== true) {
      showToast(result?.message || 'No fue posible actualizar el estado.', 'error');
      return;
    }

    pqrsData[currentId].estado = result.data?.estado || estado;
    document.querySelectorAll('.status-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    updateBadgeColors(pqrsData[currentId]);
    renderTable();
    renderStats();
    showToast('Estado actualizado correctamente.');
    await refreshPqrsFromServer();
  } catch (error) {
    console.error('Error cambiando estado:', error);
    showToast('Error de conexión al actualizar el estado.', 'error');
  }
}

// Enviar respuesta — persiste en SQL Server vía POST /gestion-de-pqr/api/pqr/{id}/responder
function initResponseForm() {
  const btn = safeGetElement('btnSendResponse');
  btn?.addEventListener('click', async () => {
    if (!currentId) return;

    const textarea = safeGetElement('responseTextarea');
    const respuesta = textarea?.value?.trim();

    if (!respuesta || respuesta.length < 3) {
      showToast('Escribe una respuesta antes de enviarla.', 'warning');
      return;
    }

    btn.disabled = true;
    try {
      const result = await pqrApiRequest(`/gestion-de-pqr/api/pqr/${currentId}/responder`, {
        method: 'POST',
        body: { respuesta }
      });

      if (!result || result.success !== true) {
        showToast(result?.message || 'No fue posible enviar la respuesta.', 'error');
        return;
      }

      const data = pqrsData[currentId];
      data.respuesta = result.data.respuesta;
      data.fechaRespuesta = result.data.fechaRespuesta;
      data.atendidaPor = result.data.atendidaPor;
      data.estado = result.data.estado;

      renderResponseThread(data);
      updateBadgeColors(data);
      updateStatusButtons(data.estado);
      renderTable();
      renderStats();
      textarea.value = '';
      showToast('Respuesta enviada correctamente.');
      await refreshPqrsFromServer();
    } catch (error) {
      console.error('Error enviando respuesta:', error);
      showToast('Error de conexión al enviar la respuesta.', 'error');
    } finally {
      btn.disabled = false;
    }
  });
}

// Filter Table
function filterTable() {
  const searchInput = document.getElementById('searchInput').value.toLowerCase();
  const filterTipo = document.getElementById('filterTipo').value;
  const filterEstado = document.getElementById('filterEstado').value;

  const rows = document.querySelectorAll('#pqrsTableBody tr[data-id]');

  rows.forEach(row => {
    const paciente = row.querySelector('.paciente-info strong')?.textContent.toLowerCase() || '';
    const radicado = row.querySelector('.radicado')?.textContent.toLowerCase() || '';
    const asunto = row.cells[3]?.textContent.toLowerCase() || '';
    const tipo = row.getAttribute('data-tipo');
    const id = row.getAttribute('data-id');
    const estadoReal = pqrsData[id]?.estado;

    const matchesSearch = paciente.includes(searchInput) || radicado.includes(searchInput) || asunto.includes(searchInput);
    const matchesTipo = filterTipo === 'todos' || tipo === filterTipo;
    const matchesEstado = filterEstado === 'todos' || estadoReal === filterEstado;

    row.style.display = (matchesSearch && matchesTipo && matchesEstado) ? '' : 'none';
  });
}

// Handle window resize
window.addEventListener('resize', function () {
  if (window.innerWidth > 1200) {
    document.getElementById('detailPanel').classList.remove('show');
    document.querySelector('.overlay').classList.remove('show');
  }
});
