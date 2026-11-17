(() => {
// ===== CHROME COMPARTIDO =====
const safeGetElement = (id) => document.getElementById(id);

const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, character => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;'
}[character]));

const showToast = (message, type = 'success') => {
  const toast = window.ToastService?.[type];
  if (typeof toast !== 'function') {
    console.error('[SmileTrack][PQR] El servicio compartido de notificaciones no está disponible.');
    return;
  }
  toast(window.I18N?.t('pqr.title') || 'PQR', escapeHtml(message));
};

const translate = (key, fallback) => window.I18N?.t(key) || fallback;

// ===== CLIENTE API LOCAL (autocontenido, sin depender de wwwroot/js/lib/apiRequest.js) =====
function getPqrAntiforgeryToken() {
  const meta = document.querySelector('meta[name="csrf-request-token"]');
  const metaToken = meta?.getAttribute('content');
  if (metaToken) return metaToken;

  const hidden = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  return hidden || null;
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
    if (!csrfToken) {
      throw new Error('No se pudo validar la solicitud. Recarga la página e inténtalo de nuevo.');
    }
    opts.headers = { ...opts.headers, 'X-CSRF-TOKEN': csrfToken };
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
  recibida: { key: 'pqr.received', label: 'Recibida', cls: 'badge-recibido' },
  en_proceso: { key: 'pqr.status_in_progress', label: 'En proceso', cls: 'badge-proceso' },
  resuelta: { key: 'pqr.resolved', label: 'Resuelta', cls: 'badge-resuelto' },
  cerrada: { key: 'pqr.status_closed', label: 'Cerrada', cls: 'badge-cerrado' },
  rechazada: { key: 'pqr.rejected', label: 'Rechazada', cls: 'badge-rechazado' }
};

const TIPO_BADGE = {
  queja: { key: 'pqr.type_complaint', label: 'Queja', cls: 'badge-queja' },
  reclamo: { key: 'pqr.type_claim', label: 'Reclamo', cls: 'badge-reclamo' },
  peticion: { key: 'pqr.type_petition', label: 'Petición', cls: 'badge-peticion' },
  sugerencia: { key: 'pqr.type_suggestion', label: 'Sugerencia', cls: 'badge-peticion' }
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
  rechazada: 'rechazado'
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
    body.innerHTML = `<tr class="pqr-empty-row"><td colspan="8"><div class="empty-state">
      <span class="material-symbols-outlined" aria-hidden="true">folder_off</span>
      <h3>${escapeHtml(window.I18N?.t('pqr.no_requests') || 'No hay PQR registradas.')}</h3>
    </div></td></tr>`;
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
      <tr data-id="${escapeHtml(id)}" data-tipo="${escapeHtml(p.tipo)}" data-estado="${ESTADO_FILTRO[p.estado] || 'recibido'}">
        <td class="radicado">${escapeHtml(p.radicado)}</td>
        <td class="paciente-info">
          <strong>${escapeHtml(p.paciente)}</strong>
          <span>${escapeHtml(p.documento)}</span>
        </td>
        <td><span class="badge ${tipoBadge.cls}">${escapeHtml(translate(tipoBadge.key, tipoBadge.label))}</span></td>
        <td>${escapeHtml(p.titulo)}</td>
        <td><span class="badge ${estadoBadge.cls}">${escapeHtml(translate(estadoBadge.key, estadoBadge.label))}</span></td>
        <td><span class="badge ${prioridadBadge.cls}">${prioridadBadge.label}</span></td>
        <td class="${vencida ? 'tiempo-vencido' : ''}">${escapeHtml(formatTiempoAbierto(p.fechaCreacionIso))}${vencida ? ' <span class="material-symbols-outlined" aria-hidden="true" style="font-size:16px;vertical-align:middle;color:var(--danger)">warning</span>' : ''}</td>
        <td class="actions-cell">
          <button class="action-btn" type="button" data-action="view" data-id="${escapeHtml(id)}"
                      title="${escapeHtml(translate('pqr.view_details', 'Ver detalle'))}"
                      aria-label="${escapeHtml(translate('pqr.view_details_aria', 'Ver detalle de la solicitud {id}').replace('{id}', String(p.radicado)))}">
            <span class="material-symbols-outlined" aria-hidden="true">visibility</span>
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

  safeGetElement('searchInput')?.addEventListener('input', filterTable);
  safeGetElement('filterTipo')?.addEventListener('change', filterTable);
  safeGetElement('filterEstado')?.addEventListener('change', filterTable);
  safeGetElement('closeDetailButton')?.addEventListener('click', closeDetail);
  safeGetElement('detailOverlay')?.addEventListener('click', closeDetail);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && currentId !== null) closeDetail();
  });
  safeGetElement('pqrsTableBody')?.addEventListener('click', event => {
    const button = event.target.closest('[data-action="view"]');
    if (button) showDetail(button.dataset.id);
  });
  document.querySelectorAll('.status-btn[data-state]').forEach(button => {
    button.addEventListener('click', () => changeStatus(button, button.dataset.state));
  });
});

// ===== NOTIFICACIONES Y ACTUALIZACIÓN AUTOMÁTICA =====
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
      ? `${translate('pqr.notifications_aria', 'Nuevas solicitudes PQR')}: ${count}`
      : translate('pqr.no_notifications', 'No hay nuevas solicitudes PQR');
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

  // Si el detalle ya está abierto, actualizamos únicamente la información
  // visible de la PQR. No reconstruimos el panel ni tocamos el textarea.
  if (currentId && pqrsData[currentId]) {
    const data = pqrsData[currentId];
    const responseTextarea = safeGetElement('responseTextarea');

    safeGetElement('detailTitle').textContent = data.titulo;
    safeGetElement('detailRadicado').textContent = data.radicado;
    safeGetElement('infoRadicado').textContent = data.fecha;
    safeGetElement('infoTiempo').textContent = formatTiempoAbierto(data.fechaCreacionIso);
    safeGetElement('infoPaciente').textContent = data.paciente;
    safeGetElement('infoDocumento').textContent = data.documento;
    safeGetElement('infoEmail').textContent = data.email;
    safeGetElement('detailDescripcion').textContent = data.descripcion;

    renderResponseThread(data);
    updateBadgeColors(data);
    updateStatusButtons(data.estado);

    // Nunca modificamos el contenido que el administrador esté escribiendo.
    if (responseTextarea) responseTextarea.focus({ preventScroll: true });
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

  // La vista ya llega cargada con los datos de Razor.
  // No hacemos una consulta automática al entrar ni al abrir una PQR.
  // La sincronización con el servidor se realiza después de enviar una respuesta.
}

// Show Management (la vista actual ya es la bandeja de gestión)
function showManagement() {}

// Show Detail Panel
function showDetail(id) {
  currentId = id;
  const data = pqrsData[id];
  if (!data) return;

  document.querySelectorAll('tbody tr').forEach(row => row.classList.remove('selected'));
  const selectedRow = Array.from(document.querySelectorAll('tr[data-id]'))
    .find(row => row.dataset.id === String(id));
  if (selectedRow) selectedRow.classList.add('selected');

  safeGetElement('emptyState').hidden = true;
  safeGetElement('detailContent').hidden = false;

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
    const attachmentPath = String(data.evidenciaAdjunto);
    if (/^(?:https?:)?\/\//i.test(attachmentPath) || attachmentPath.includes('..')) {
      showToast('El archivo adjunto no tiene una ruta válida.', 'error');
      evidenciaSection.hidden = true;
    } else {
      evidenciaLink.href = `/${attachmentPath.replace(/^\/+/, '')}`;
      evidenciaSection.hidden = false;
    }
  } else {
    evidenciaSection.hidden = true;
  }

  renderResponseThread(data);
  updateBadgeColors(data);

  const textarea = document.getElementById('responseTextarea');
  if (textarea) textarea.value = '';

  if (window.innerWidth <= 1200) {
    document.getElementById('detailPanel').classList.add('show');
    safeGetElement('detailOverlay').classList.add('show');
    safeGetElement('detailOverlay').setAttribute('aria-hidden', 'false');
    safeGetElement('closeDetailButton').focus();
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
          <span class="timeline-author">${escapeHtml(data.atendidaPor || 'Administrador')}</span>
          <span class="timeline-date">${escapeHtml(data.fechaRespuesta || '')}</span>
        </div>
        <div class="timeline-content">${escapeHtml(data.respuesta)}</div>
      </div>`;
  } else {
    title.textContent = 'Hilo de Respuestas (0)';
    thread.innerHTML = '<p class="pqr-no-response">Aún no se ha respondido esta solicitud.</p>';
  }
}

function updateBadgeColors(data) {
  const tipoBadge = TIPO_BADGE[data.tipo] || TIPO_BADGE.peticion;
  const estadoBadge = ESTADO_BADGE[data.estado] || ESTADO_BADGE.recibida;
  const prioridadBadge = PRIORIDAD_BADGE[data.prioridad] || PRIORIDAD_BADGE.media;

  const tipoEl = document.getElementById('detailTipo');
  tipoEl.className = `badge ${tipoBadge.cls}`;
  tipoEl.textContent = translate(tipoBadge.key, tipoBadge.label);

  const estadoEl = document.getElementById('detailEstado');
  estadoEl.className = `badge ${estadoBadge.cls}`;
  estadoEl.textContent = translate(estadoBadge.key, estadoBadge.label);

  const prioridadEl = document.getElementById('detailPrioridad');
  prioridadEl.className = `badge ${prioridadBadge.cls}`;
  prioridadEl.textContent = prioridadBadge.label;
}

function updateStatusButtons(estado) {
  document.querySelectorAll('.status-btn').forEach(btn => btn.classList.remove('active'));
  const map = { recibida: 'recibido', en_proceso: 'proceso', resuelta: 'resuelto', cerrada: 'cerrado', rechazada: 'rechazado' };
  const btnClass = map[estado];
  if (btnClass) {
    const btn = document.querySelector(`.status-btn.${btnClass}`);
    if (btn) btn.classList.add('active');
  }
}

function closeDetail() {
  const previousId = currentId;
  safeGetElement('detailContent').hidden = true;
  safeGetElement('emptyState').hidden = false;
  document.getElementById('detailPanel').classList.remove('show');
  safeGetElement('detailOverlay').classList.remove('show');
  safeGetElement('detailOverlay').setAttribute('aria-hidden', 'true');
  document.querySelectorAll('tbody tr').forEach(row => row.classList.remove('selected'));
  currentId = null;
  Array.from(document.querySelectorAll('button[data-action="view"]'))
    .find(button => button.dataset.id === String(previousId))?.focus();
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
    filterTable();
    renderStats();
    showToast('Estado actualizado correctamente.');
  } catch (error) {
    console.error('Error cambiando estado:', error);
    showToast(error instanceof Error ? error.message : 'Error de conexión al actualizar el estado.', 'error');
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
      filterTable();
      renderStats();
      textarea.value = '';
      showToast('Respuesta enviada correctamente.');
      await refreshPqrsFromServer();
    } catch (error) {
      console.error('Error enviando respuesta:', error);
      showToast(error instanceof Error ? error.message : 'Error de conexión al enviar la respuesta.', 'error');
    } finally {
      btn.disabled = false;
    }
  });
}

// Filter Table
function filterTable() {
  const searchInput = safeGetElement('searchInput')?.value.toLowerCase() || '';
  const filterTipo = safeGetElement('filterTipo')?.value || 'todos';
  const filterEstado = safeGetElement('filterEstado')?.value || 'todos';

  const rows = document.querySelectorAll('#pqrsTableBody tr[data-id]');

  rows.forEach(row => {
    const paciente = row.querySelector('.paciente-info strong')?.textContent.toLowerCase() || '';
    const radicado = row.querySelector('.radicado')?.textContent.toLowerCase() || '';
    const asunto = row.cells[3]?.textContent.toLowerCase() || '';
    const tipo = row.getAttribute('data-tipo');
    const estadoReal = row.getAttribute('data-estado');

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
    safeGetElement('detailOverlay').classList.remove('show');
    safeGetElement('detailOverlay').setAttribute('aria-hidden', 'true');
  } else if (currentId !== null) {
    document.getElementById('detailPanel').classList.add('show');
    safeGetElement('detailOverlay').classList.add('show');
    safeGetElement('detailOverlay').setAttribute('aria-hidden', 'false');
  }
});
})();
