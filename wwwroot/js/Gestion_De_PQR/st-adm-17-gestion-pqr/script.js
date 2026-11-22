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
let paginaActual = 1;
let totalPaginas = 1;
let totalFiltrado = 0;
let busquedaTimer = null;
let solicitudBandeja = 0;

function renderPaginacion() {
  const panel = safeGetElement('pqrPaginacion');
  if (!panel) return;
  panel.hidden = false;
  safeGetElement('pqrPaginaInfo').textContent = `Página ${paginaActual} de ${totalPaginas} · ${totalFiltrado} resultados`;
  const anterior = safeGetElement('pqrAnterior');
  const siguiente = safeGetElement('pqrSiguiente');
  anterior.hidden = siguiente.hidden = totalPaginas <= 1;
  anterior.disabled = paginaActual <= 1;
  siguiente.disabled = paginaActual >= totalPaginas;
}


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
          ${p.documento && p.documento !== 'N/A' ? `<span>${escapeHtml(p.documento)}</span>` : ''}
        </td>
        <td><span class="badge ${tipoBadge.cls}">${escapeHtml(translate(tipoBadge.key, tipoBadge.label))}</span></td>
        <td class="pqr-table-subject">${escapeHtml(p.titulo)}</td>
        <td><span class="badge ${estadoBadge.cls}">${escapeHtml(translate(estadoBadge.key, estadoBadge.label))}</span></td>
        <td><span class="badge ${prioridadBadge.cls}">${prioridadBadge.label}</span></td>
        <td class="pqr-table-date ${vencida ? 'tiempo-vencido' : ''}">${escapeHtml(p.fecha?.split(" ")[0]?.split("-").reverse().join("/") || "—")}${vencida ? ' <span class="material-symbols-outlined" aria-hidden="true" style="font-size:16px;vertical-align:middle;color:var(--danger)">warning</span>' : ''}</td>
        <td class="pqr-table-actions">
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

function contextoPqr(pqr) {
  return [
    pqr.moduloSoftware && `Módulo o pantalla: ${pqr.moduloSoftware}`,
    pqr.profesional && `Profesional: ${pqr.profesional}`,
    pqr.fechaServicio && `Fecha del servicio: ${pqr.fechaServicio}`
  ].filter(Boolean).join(' · ');
}

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
      contexto: contextoPqr(p),
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


  safeGetElement('searchInput')?.addEventListener('input', () => { clearTimeout(busquedaTimer); busquedaTimer = setTimeout(() => cambiarFiltros(), 350); });
  safeGetElement('filterTipo')?.addEventListener('change', cambiarFiltros);
  safeGetElement('filterEstado')?.addEventListener('change', cambiarFiltros);
  safeGetElement('filterOrigen')?.addEventListener('change', cambiarFiltros);
  safeGetElement('pqrAnterior')?.addEventListener('click', () => { if (paginaActual > 1) { paginaActual--; refreshPqrsFromServer(); } });
  safeGetElement('pqrSiguiente')?.addEventListener('click', () => { if (paginaActual < totalPaginas) { paginaActual++; refreshPqrsFromServer(); } });
  const radicado = new URLSearchParams(location.search).get('pqr');
  const search = safeGetElement('searchInput');
  if (search && /^[1-9]\d*$/.test(radicado || '')) {
    search.value = `PQR-${radicado.padStart(4, '0')}`;
    refreshPqrsFromServer().then(() => {
      if (pqrsData[radicado] && search.value === `PQR-${radicado.padStart(4, '0')}`) showDetail(radicado);
    });
  } else {
    refreshPqrsFromServer();
  }
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
      contexto: contextoPqr(p),
      respuesta: p.respuesta,
      fechaRespuesta: p.fechaRespuesta,
      atendidaPor: p.atendidaPor,
      evidenciaAdjunto: p.evidenciaAdjunto
    };
  });

  window.RAZOR_PQR_STATS = payload.stats;
  renderTable();
  renderStats();
  if (payload.paginacion) {
    paginaActual = payload.paginacion.pagina;
    totalPaginas = payload.paginacion.totalPaginas;
    totalFiltrado = payload.paginacion.totalFiltrado;
    renderPaginacion();
  }

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
    safeGetElement('detailContexto').textContent = data.contexto;

    cargarHistorialPqr(currentId);
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
    const solicitud = ++solicitudBandeja;
    const params = new URLSearchParams({
      pagina: String(paginaActual),
      buscar: safeGetElement('searchInput')?.value || '',
      tipo: safeGetElement('filterTipo')?.value || 'todos',
      estado: safeGetElement('filterEstado')?.value || 'todos',
      origen: safeGetElement('filterOrigen')?.value || 'todos'
    });
    const response = await fetch(`/gestion-de-pqr/api/pqr?${params}`, {
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
    if (solicitud !== solicitudBandeja) return;
    applyPqrPayload(payload, { showNewToast: false });
    pqrFirstRefresh = false;
  } catch (error) {
    console.error('Error actualizando la bandeja de PQR:', error);
  }
}

// Show Management (la vista actual ya es la bandeja de gestión)
function showManagement() {}

// Show Detail Panel
let pendingPqrStatus = null;
let sendingPqrResponse = false;

function showDetail(id) {
  pendingPqrStatus = null;
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
  safeGetElement('detailContexto').textContent = data.contexto;

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
  cargarHistorialPqr(data.id);
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

async function cargarHistorialPqr(id) {
  const thread = safeGetElement('responsesThread');
  const title = safeGetElement('responsesTitle');
  if (!thread || !title) return;
  try {
    const response = await fetch(`/gestion-de-pqr/api/pqr/${encodeURIComponent(id)}/historial`, {
      credentials: 'same-origin', headers: { Accept: 'application/json' }
    });
    if (!response.ok) return;
    const result = await response.json();
    if (String(currentId) !== String(id) || !result.success) return;
    const eventos = result.eventos || [];
    if (!eventos.length) return; // Los registros anteriores conservan la vista de respuesta existente.
    title.textContent = `Historial de seguimiento (${eventos.length})`;
    thread.innerHTML = eventos.map(e => `
      <div class="timeline-item">
        <div class="timeline-header">
          <span class="timeline-author">${escapeHtml(e.tipoEvento === 'respuesta' ? 'Respuesta administrativa' : 'Cambio de estado')}</span>
          <span class="timeline-date">${escapeHtml(new Date(e.fechaEvento).toLocaleString('es-CO'))}</span>
        </div>
        <div class="timeline-content">
          <div>${escapeHtml(e.estadoAnterior || '—')} → ${escapeHtml(e.estadoNuevo)}</div>
          ${e.respuesta ? `<p>${escapeHtml(e.respuesta)}</p>` : ''}
        </div>
      </div>`).join('');
  } catch (error) {
    console.error('[PQR] No fue posible consultar el historial:', error);
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
  const bloqueada = sendingPqrResponse || ['resuelta', 'cerrada', 'rechazada'].includes(estado);
  document.querySelectorAll('.status-btn').forEach(btn => {
    btn.classList.remove('active');
    btn.disabled = bloqueada;
  });
  const respuesta = document.getElementById('responseTextarea');
  const btnRespuesta = document.getElementById('btnSendResponse');
  if (respuesta) respuesta.disabled = bloqueada;
  if (btnRespuesta) btnRespuesta.disabled = bloqueada;
  const map = { recibida: 'recibido', en_proceso: 'proceso', resuelta: 'resuelto', cerrada: 'cerrado', rechazada: 'rechazado' };
  const btnClass = map[pendingPqrStatus || estado];
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
  pendingPqrStatus = null;
  Array.from(document.querySelectorAll('button[data-action="view"]'))
    .find(button => button.dataset.id === String(previousId))?.focus();
}

// Selección provisional: no guardar estado ni bloquear la respuesta hasta enviar.
function changeStatus(btn, estado) {
  if (!currentId || sendingPqrResponse) return;
  const data = pqrsData[currentId];
  if (!data || ['resuelta', 'cerrada', 'rechazada'].includes(data.estado)) {
    showToast('Esta PQR está finalizada y no admite cambios.', 'warning');
    return;
  }
  const permitted = data.estado === 'recibida'
    ? ['en_proceso', 'resuelta', 'cerrada', 'rechazada']
    : data.estado === 'en_proceso'
      ? ['resuelta', 'cerrada', 'rechazada'] : [];
  if (estado !== data.estado && !permitted.includes(estado)) {
    showToast('La transición seleccionada no está permitida.', 'warning');
    return;
  }
  pendingPqrStatus = estado === data.estado ? null : estado;
  updateStatusButtons(data.estado);
  showToast('Estado seleccionado. Se guardará al enviar la respuesta.');
}

// Enviar respuesta — persiste en SQL Server vía POST /gestion-de-pqr/api/pqr/{id}/responder
function initResponseForm() {
  const btn = safeGetElement('btnSendResponse');
  btn?.addEventListener('click', async () => {
    if (!currentId || sendingPqrResponse) return;
    const id = currentId;
    if (['resuelta', 'cerrada', 'rechazada'].includes(pqrsData[id]?.estado)) {
      showToast('Esta PQR no admite nuevas respuestas.', 'warning');
      return;
    }

    const textarea = safeGetElement('responseTextarea');
    const respuesta = textarea?.value?.trim();

    if (!respuesta || respuesta.length < 3) {
      showToast('Escribe una respuesta antes de enviarla.', 'warning');
      return;
    }

    sendingPqrResponse = true;
    btn.disabled = true;
    try {
      const result = await pqrApiRequest(`/gestion-de-pqr/api/pqr/${id}/responder`, {
        method: 'POST',
        body: { respuesta, nuevoEstado: pendingPqrStatus }
      });

      if (!result || result.success !== true) {
        showToast(result?.message || 'No fue posible enviar la respuesta.', 'error');
        return;
      }

      if (currentId !== id || !pqrsData[id]) {
        showToast('Respuesta enviada correctamente.');
        await refreshPqrsFromServer();
        return;
      }

      const data = pqrsData[id];
      data.respuesta = result.data.respuesta;
      data.fechaRespuesta = result.data.fechaRespuesta;
      data.atendidaPor = result.data.atendidaPor;
      data.estado = result.data.estado;
      pendingPqrStatus = null;

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
      showToast(error instanceof Error ? error.message : 'Error de conexión al enviar la respuesta.', 'error');
    } finally {
      sendingPqrResponse = false;
      if (currentId && pqrsData[currentId]) updateStatusButtons(pqrsData[currentId].estado);
    }
  });
}

// Los filtros se aplican en SQL Server; nunca filtrar únicamente la página visible.
function cambiarFiltros() {
  paginaActual = 1;
  refreshPqrsFromServer();
}
function filterTable() { cambiarFiltros(); }

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
