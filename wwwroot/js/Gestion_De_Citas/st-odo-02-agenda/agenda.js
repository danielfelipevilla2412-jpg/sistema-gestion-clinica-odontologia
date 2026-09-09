/* ============================================
SmileTrack — Mi Agenda Odontólogo (st-odo-02-agenda)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026 (actualizado 2026-07-31)

DESCRIPCIÓN:
Controla el renderizado de la agenda semanal del odontólogo. Consume citas REALES desde
GET /api/citas (el backend aplica filtro automático por rol Profesional → IdProfesional claim),
y transiciona estados vía PUT /api/citas/{id} y permite editar notas vía PUT /api/citas/{id}/notas. 
La agenda del profesional utiliza únicamente
  datos actuales del servidor.No se muestran datos almacenados localmente cuando la API no está disponible.
FUNCIONALIDADES PRINCIPALES:
- Carga reactiva de citas asociadas al odontólogo autenticado (filtro automático backend)
- Transición de estados de citas (Iniciar atención, no asistió, etc.) vía PUT /api/citas/{id}
- Sincronización con el selector de semana y búsqueda debounced
- Filtro semanal aplicado en cliente sobre el dataset cacheado

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController → ApiListarCitas, ApiActualizarCita
- Endpoints: GET /api/citas, PUT /api/citas/{id}, PUT /api/citas/{id}/notas [Authorize]
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

const getCsrfToken = () => {
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

const LOCAL_STORAGE_KEY = 'smiletrack_agenda_odo';

// Mapeo estado server (en_proceso / confirmada / ...) ↔ etiquetas UI amigables
const ESTADO_MAP_SERVER = {
  'programada': { label: 'Agendada', class: 'badge-agendada' },
  'confirmada': { label: 'Confirmada', class: 'badge-agendada' },
  'en_proceso': { label: 'En consulta', class: 'badge-en-consulta' },
  'finalizada': { label: 'Atendida', class: 'badge-atendida' },
  'atendida': { label: 'Atendida', class: 'badge-atendida' },
  'cancelada': { label: 'Cancelada', class: 'badge-cancelada' },
  'no_asistida': { label: 'No asistió', class: 'badge-no-asistio' }
};
const ESTADO_MAP_CLIENTE = {
  'Atendida': 'finalizada',
  'En consulta': 'en_proceso',
  'Agendada': 'programada',
  'Confirmada': 'confirmada',
  'No asistió': 'no_asistida',
  'Cancelada': 'cancelada'
};

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  if (window.CommonUtils?.getEl) return window.CommonUtils.getEl(id);
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  if (window.CommonUtils?.debounce) return window.CommonUtils.debounce(fn, delay);
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
//  MAPEOS DE DATOS: Server → Cliente
// ═══════════════════════════════════════════════════════════════════
const escapeHtml = (value) => {
  if (window.CommonUtils?.escapeHtml) return window.CommonUtils.escapeHtml(value);
  return String(value ?? '').replace(
    /[&<>'"]/g,
    (c) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      "'": '&#39;',
      '"': '&quot;'
    }[c])
  );
};

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
  if (!servicioNombre) return '60 min';
  return '60 min';
};

const calcularDuracion = (horaInicio, horaFin) => {
  if (!horaInicio || !horaFin) return 60;

  const inicio = new Date(`1970-01-01T${horaInicio}`);
  const fin = new Date(`1970-01-01T${horaFin}`);

  if (
    Number.isNaN(inicio.getTime()) ||
    Number.isNaN(fin.getTime())
  ) {
    return 60;
  }

  let minutos =
    Math.round((fin - inicio) / 60000);

  if (minutos < 0) {
    minutos += 24 * 60;
  }

  return minutos;
};
const mapServerToClient = (srv, duracionConfigurada) => {
  if (!srv) return null;

  const fechaValor =
    srv.FechaHora ??
    srv.fechaHora ??
    null;

  const fechaHora = fechaValor
    ? new Date(fechaValor)
    : null;

  const fechaHoraISO =
    fechaHora && !Number.isNaN(fechaHora.getTime())
      ? fechaHora.toISOString()
      : null;

  const estadoServer =
    String(
      srv.Estado ??
      srv.estado ??
      'programada'
    ).trim().toLowerCase();

  const estadoInfo =
    ESTADO_MAP_SERVER[estadoServer] ??
    ESTADO_MAP_SERVER.programada;

  const paciente =
    srv.Paciente?.NombreCompleto ??
    srv.paciente?.nombreCompleto ??
    '—';

  const servicio =
    srv.Servicio?.Nombre ??
    srv.servicio?.nombre ??
    'Sin servicio';

  const horaInicio =
    srv.HoraInicio ??
    srv.horaInicio ??
    null;

  const horaFin =
    srv.HoraFin ??
    srv.horaFin ??
    null;

  const duracionRespuesta = Number(
    srv.DuracionMinutos ??
    srv.duracionMinutos ??
    duracionConfigurada
  );

  const duracion =
    Number.isFinite(duracionRespuesta) && duracionRespuesta > 0
      ? duracionRespuesta
      : calcularDuracion(horaInicio, horaFin);

  return {
    id: srv.IdCita ?? srv.idCita,

    idPaciente:
      srv.IdPaciente ??
      srv.idPaciente,

    idProfesional:
      srv.IdProfesional ??
      srv.idProfesional,

    idServicio:
      srv.IdServicio ??
      srv.idServicio,

    idConsultorio:
      srv.IdConsultorio ??
      srv.idConsultorio,

    idEstado:
      srv.IdEstado ??
      srv.idEstado,

    fecha:
      fechaHoraISO
        ? fmtFechaCorta(fechaHoraISO)
        : '—',

    fechaISO:
      fechaHoraISO
        ? fechaHoraISO.split('T')[0]
        : '',

    hora:
      fechaHoraISO
        ? fmtHora12(fechaHoraISO)
        : '—',

    horaISO:
      fechaHoraISO
        ? `${String(fechaHora.getHours()).padStart(2, '0')}:${String(fechaHora.getMinutes()).padStart(2, '0')}`
        : '00:00',

    fechaHoraISO,

    horaInicio,
    horaFin,

    duracion,

    paciente,
    servicio,

    notas:
      srv.Notas ??
      srv.notas ??
      '',

    estado:
      estadoInfo.label,

    estadoServer,

    estadoClass:
      estadoInfo.class,

    active:
      estadoInfo.label === 'En consulta',

    _raw: srv
  };
};

// ═══════════════════════════════════════════════════════════════════
//  FALLBACK LOCAL
// ═══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
//  CACHÉ LOCAL (respaldo de la última respuesta real del servidor)
// ═══════════════════════════════════════════════════════════════════

const loadLocal = () => [];
const saveLocal = (arr) => {
  // Se conserva la función por compatibilidad, pero los datos del servidor
  // son siempre la fuente de verdad para la agenda del profesional.
  try { localStorage.removeItem(LOCAL_STORAGE_KEY); } catch { }
};

// Estado global: nunca iniciar con datos locales potencialmente obsoletos.
let appointments = [];
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
  return `<button class="btn-icon edit-icon${isRed ? ' red' : ''}" title="Editar notas" aria-label="Editar notas de cita" onclick="editAppointment(${id})"><span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">edit</span></button>`;
};
const formatTimeISO = (horaAMPM) => {
  const parts = horaAMPM.split(' ');
  if (parts.length < 2) return '09:00';
  const [time, period] = parts;
  let [h, m] = (time || '09:00').split(':').map(Number);
  if (period === 'PM' && h !== 12) h += 12;
  if (period === 'AM' && h === 12) h = 0;
  return `${String(h || 0).padStart(2, '0')}:${String(m || 0).padStart(2, '0')}`;
};
const getDateTimeISO = (fechaISO, horaAMPM) => `${fechaISO}T${formatTimeISO(horaAMPM)}:00`;
const TRANSICIONES_ESTADO = {
  'Agendada': [
    'Confirmada',
    'No asistió',
    'Cancelada'
  ],

  'Confirmada': [
    'En consulta',
    'No asistió',
    'Cancelada'
  ],

  'En consulta': [
    'Atendida'
  ],

  'Atendida': [],
  'Cancelada': [],
  'No asistió': []
};

const renderStatusSelector = (appointment) => {
  const opciones =
    TRANSICIONES_ESTADO[
    appointment.estado
    ] || [];

  if (!opciones.length) {
    return '';
  }

  return `
    <select
      class="appointment-status-select"
      data-cita-id="${appointment.id}"
      aria-label="Cambiar estado de la cita"
    >
      <option value="">
        Cambiar estado
      </option>

      ${opciones
      .map(
        (estado) =>
          `<option value="${escapeHtml(estado)}">${escapeHtml(estado)}</option>`
      )
      .join('')}
    </select>
  `;
};

const renderTable = (data) => {
  const tbody = safeGetElement('agendaTbody');

  if (!tbody) return;

  tbody.innerHTML = '';

  if (!data.length) {
    tbody.innerHTML = `
      <tr>
        <td
          colspan="7"
          style="text-align:center;padding:24px;color:var(--text-muted);"
        >
          No hay citas para esta semana.
        </td>
      </tr>
    `;
    return;
  }

  const weekStart =
    getWeekStart(
      new Date(),
      weekOffset
    );

  const weekEnd =
    new Date(weekStart);

  weekEnd.setDate(
    weekStart.getDate() + 6
  );

  const startDate = new Date(weekStart);
  startDate.setHours(0, 0, 0, 0);

  const endDate = new Date(weekStart);
  endDate.setDate(endDate.getDate() + 6);
  endDate.setHours(23, 59, 59, 999);

  const inWeek = data.filter((a) => {
    if (!a.fechaISO) return false;

    const d = new Date(`${a.fechaISO}T00:00:00`);

    return d >= startDate && d <= endDate;
  });

  const rows =
    inWeek.length
      ? inWeek
      : data.slice(0, 10);
  rows.forEach((item) => {
    const tr =
      document.createElement('tr');

    if (item.active) {
      tr.classList.add('row-active');
    }

    if (item.estado === 'Cancelada') {
      tr.classList.add('row-cancelada');
    }

    tr.setAttribute(
      'role',
      'row'
    );

    const dtiso =
      item.fechaHoraISO ||
      getDateTimeISO(
        item.fechaISO,
        item.hora
      );

    tr.innerHTML = `
      <td class="td-fecha">
        <time
          datetime="${escapeHtml(item.fechaISO)}"
        >
          ${escapeHtml(item.fecha)}
        </time>
      </td>

      <td>
        <span class="pill-hora">
          <time datetime="${escapeHtml(dtiso)}">
            ${escapeHtml(item.hora)}
          </time>
        </span>
      </td>

      <td class="td-paciente">
        ${escapeHtml(item.paciente)}
      </td>

      <td>
        ${escapeHtml(item.servicio)}
      </td>
 <td>
  ${Number.isFinite(Number(item.duracion))
        ? `${Number(item.duracion)} min`
        : '60 min'}
</td>

      <td>
        <div
          style="
            display:flex;
            align-items:center;
            gap:8px;
            flex-wrap:wrap;
          "
        >
          <span
            class="badge ${badgeClass(item.estado)}"
            role="status"
            aria-label="Estado: ${escapeHtml(item.estado)}"
          >
            ${escapeHtml(item.estado)}
          </span>

          ${renderStatusSelector(item)
      }
        </div>
      </td>

      <td>
        <div class="actions-cell">
          <button
            class="btn-icon"
            type="button"
            title="Ver detalle"
            aria-label="Ver detalle de cita de ${escapeHtml(item.paciente)}"
            onclick="openModal(${item.id})"
          >
            <span class="material-symbols-outlined" aria-hidden="true" style="font-size:1.1rem;">visibility</span>
          </button>

          ${editIcon(
        item.id,
        item.estado
      )}
        </div>
      </td>
    `;

    tr.style.cursor = 'pointer';

    tr.setAttribute(
      'tabindex',
      '0'
    );

    tr.setAttribute(
      'aria-label',
      `Ver detalle de cita de ${item.paciente} el ${item.fecha} a las ${item.hora}`
    );

    tr.addEventListener(
      'click',
      (e) => {
        if (
          !e.target.closest('.btn-icon') &&
          !e.target.closest('.appointment-status-select')
        ) {
          openModal(item.id);
        }
      }
    );

    tr.addEventListener(
      'keydown',
      (e) => {
        if (
          (e.key === 'Enter' ||
            e.key === ' ') &&
          !e.target.closest('.btn-icon') &&
          !e.target.closest('.appointment-status-select')
        ) {
          e.preventDefault();
          openModal(item.id);
        }
      }
    );

    tbody.appendChild(tr);

    const statusSelect =
      tr.querySelector(
        '.appointment-status-select'
      );

    if (statusSelect) {
      statusSelect.addEventListener(
        'change',
        async (event) => {
          const newStatus =
            event.target.value;

          if (!newStatus) return;

          const previousStatus =
            item.estado;

          ModalService.confirm({
            title: 'Cambiar estado de cita',
            message: `¿Cambiar esta cita de "${previousStatus}" a "${newStatus}"?`,
            confirmText: 'Sí, cambiar',
            cancelText: 'No',
            isDanger: true,
            onConfirm: async () => {
              statusSelect.disabled = true;

              const resultado =
                await changeAppointmentStatus(
                  item,
                  newStatus
                );

              if (!resultado) {
                statusSelect.disabled = false;
              }

              statusSelect.value = '';
            }
          });

          event.target.value = '';
        }
      );
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
// MODAL DETALLE + EDITAR NOTAS
// ═══════════════════════════════════════════════════════════════════

window.openModal = (id) => {
  const item = appointments.find(a => a.id === id);
  if (!item) return;
  const dtiso = getDateTimeISO(item.fechaISO, item.hora);
  const content = safeGetElement('modalContent');
  if (content) {
    content.innerHTML = `
      <div class="modal-row"><span class="modal-key">Fecha</span><span class="modal-val"><time datetime="${escapeHtml(item.fechaISO)}T00:00:00">${escapeHtml(item.fecha)}</time></span></div>
      <div class="modal-row"><span class="modal-key">Hora</span><span class="modal-val"><time datetime="${escapeHtml(dtiso)}">${escapeHtml(item.hora)}</time></span></div>
      <div class="modal-row"><span class="modal-key">Paciente</span><span class="modal-val">${escapeHtml(item.paciente)}</span></div>
      <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${escapeHtml(item.servicio)}</span></div>
      <div class="modal-row"><span class="modal-key">Duración</span><span class="modal-val">${escapeHtml(item.duracion || '60 min')}</span></div>
      <div class="modal-row"><span class="modal-key">Estado</span><span class="modal-val"><span class="badge ${escapeHtml(badgeClass(item.estado))}" role="status">${escapeHtml(item.estado)}</span></span></div>
      <div class="modal-row"><span class="modal-key">Notas</span><span class="modal-val">${escapeHtml(item.notas || 'Sin notas')}</span></div>
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

window.editAppointment = (id) => {
  const item = appointments.find(a => a.id === id);
  if (!item) return;

  const content = safeGetElement('modalContent');
  const modalOverlay = safeGetElement('modalOverlay');
  const modalTitle = safeGetElement('modalTitle');

  if (!content || !modalOverlay || !modalTitle) return;

  modalTitle.textContent = 'Editar notas de la cita';

  content.innerHTML = `
    <div class="modal-row"><span class="modal-key">Fecha</span><span class="modal-val">${item.fecha}</span></div>
    <div class="modal-row"><span class="modal-key">Hora</span><span class="modal-val">${item.hora}</span></div>
    <div class="modal-row"><span class="modal-key">Paciente</span><span class="modal-val">${escapeHtml(item.paciente)}</span></div>
    <div class="modal-row"><span class="modal-key">Servicio</span><span class="modal-val">${escapeHtml(item.servicio)}</span></div>
    <div class="modal-row"><span class="modal-key">Estado</span><span class="modal-val"><span class="badge ${badgeClass(item.estado)}">${escapeHtml(item.estado)}</span></span></div>

    <div style="margin-top:16px;">
      <label for="editAppointmentNotes" style="display:block;margin-bottom:6px;font-weight:600;">Notas de la cita</label>
      <textarea
        id="editAppointmentNotes"
        rows="5"
        maxlength="4000"
        style="width:100%;box-sizing:border-box;resize:vertical;padding:10px 12px;border:1px solid #d1d5db;border-radius:8px;font:inherit;"
        placeholder="Agrega observaciones o notas relevantes de la cita..."
      >${escapeHtml(item.notas || '')}</textarea>
      <div style="margin-top:5px;font-size:12px;color:#6b7280;">Desde tu agenda solo puedes modificar las notas de esta cita.</div>
    </div>

    <div class="modal-footer" style="margin-top:16px;display:flex;justify-content:flex-end;gap:8px;">
      <button type="button" class="btn-secondary" id="cancelEditAppointmentNotes">Cancelar</button>
      <button type="button" class="btn-primary" id="saveEditAppointmentNotes">Guardar cambios</button>
    </div>
  `;

  modalOverlay.classList.add('open');
  modalOverlay.setAttribute('aria-hidden', 'false');
  modalOverlay.removeAttribute('inert');
  document.body.style.overflow = 'hidden';

  const notesInput = safeGetElement('editAppointmentNotes');
  const cancelBtn = safeGetElement('cancelEditAppointmentNotes');
  const saveBtn = safeGetElement('saveEditAppointmentNotes');

  notesInput?.focus();

  const closeAndRestore = () => {
    modalTitle.textContent = 'Detalle de Cita';
    closeModal();
  };

  cancelBtn?.addEventListener('click', closeAndRestore);

  saveBtn?.addEventListener('click', async () => {
    if (!notesInput || !saveBtn) return;

    const notas = notesInput.value.trim();
    const notasOriginales = item.notas || '';

    saveBtn.disabled = true;
    saveBtn.textContent = 'Guardando...';

    try {
      const res = await fetch(`${API_BASE}/citas/${id}/notas`, {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: JSON.stringify({
          IdCita: id,
          Notas: notas
        })
      });

      let payload;
      try {
        payload = await res.json();
      } catch {
        payload = { success: res.ok };
      }

      if (!res.ok || !payload.success) {
        throw new Error(
          payload.message ||
          'No fue posible actualizar las notas.'
        );
      }

      item.notas = payload.notas ?? notas;

      if (item._raw) {
        item._raw.Notas = item.notas;
        item._raw.notas = item.notas;
      }

      renderTable(appointments);
      closeAndRestore();

      if (window.ToastService) {
        window.ToastService.success(
          'Notas de la cita actualizadas correctamente.'
        );
      }
    } catch (error) {
      item.notas = notasOriginales;

      if (window.ToastService) {
        window.ToastService.error(
          error.message ||
          'No se pudieron actualizar las notas.'
        );
      } else {
        showToast(
          error.message ||
          'No se pudieron actualizar las notas.',
          'error'
        );
      }

      saveBtn.disabled = false;
      saveBtn.textContent = 'Guardar cambios';
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  CONTADORES Y NAVEGACIÓN SEMANAL
// ═══════════════════════════════════════════════════════════════════

const animateCounter = (el, target) => {
  if (!el) return;
  if (el._counterInterval) clearInterval(el._counterInterval);
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  el._counterInterval = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) {
      clearInterval(el._counterInterval);
      el._counterInterval = null;
    }
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
      appointments = payload.data.map((item) =>
        mapServerToClient(item, payload.duracionMinutos)
      );
      saveLocal(appointments);
    } else {
      throw new Error('payload inválido');
    }
  } catch (err) {
    console.error('[SmileTrack] Agenda odontólogo: no se pudo cargar la agenda real desde el servidor:', err);
    appointments = [];
    try { localStorage.removeItem(LOCAL_STORAGE_KEY); } catch { }
    if (window.ToastService) {
      window.ToastService.error('No se pudo cargar tu agenda. Verifica tu sesión o intenta nuevamente.');
    }
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

const changeAppointmentStatus = async (
  appointment,
  newStatus
) => {
  if (
    !appointment ||
    !appointment.id ||
    !newStatus
  ) {
    return false;
  }

  const estadosPermitidos =
    TRANSICIONES_ESTADO[
    appointment.estado
    ] || [];

  if (
    !estadosPermitidos.includes(
      newStatus
    )
  ) {
    window.ToastService?.error?.(
      `No está permitido cambiar de "${appointment.estado}" a "${newStatus}".`
    );

    return false;
  }

  const estadoServer =
    ESTADO_MAP_CLIENTE[
    newStatus
    ];

  if (!estadoServer) {
    window.ToastService?.error?.(
      'Estado inválido.'
    );

    return false;
  }

  try {
    const response =
      await fetch(
        `${API_BASE}/citas/${appointment.id}/estado`,
        {
          method: 'PUT',
          credentials: 'same-origin',
          headers: getAuthHeaders(),
          body: JSON.stringify({
            estado: estadoServer
          })
        }
      );

    let payload = null;

    try {
      payload =
        await response.json();
    } catch {
      payload = {
        success: response.ok
      };
    }

    if (
      !response.ok ||
      payload.success === false
    ) {
      throw new Error(
        payload.message ||
        `Error HTTP ${response.status}`
      );
    }

    const estadoFinal =
      String(
        payload.estado ||
        estadoServer
      )
        .trim()
        .toLowerCase();

    const estadoInfo =
      ESTADO_MAP_SERVER[
      estadoFinal
      ];

    if (!estadoInfo) {
      throw new Error(
        'El servidor devolvió un estado desconocido.'
      );
    }

    appointment.estadoServer =
      estadoFinal;

    appointment.estado =
      estadoInfo.label;

    appointment.estadoClass =
      estadoInfo.class;

    appointment.active =
      estadoInfo.label ===
      'En consulta';

    if (appointment._raw) {
      appointment._raw.Estado =
        estadoFinal;

      appointment._raw.estado =
        estadoFinal;
    }

    renderTable(
      appointments
    );

    updateCounts();

    window.ToastService?.success?.(
      payload.message ||
      'Estado actualizado correctamente.'
    );

    return true;
  } catch (error) {
    console.error(
      '[SmileTrack] Error cambiando estado de cita:',
      error
    );

    window.ToastService?.error?.(
      error.message ||
      'No se pudo actualizar el estado.'
    );

    return false;
  }
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
  const days = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
  const months = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
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
    window.addEventListener('beforeunload', () => { });
  } catch (err) {
    console.error('[SmileTrack] Error init agenda.js:', err);
    mostrarErrorUsuario(err.message || 'Error cargando agenda odontólogo. Intente recargar.');
  }
};

document.addEventListener('DOMContentLoaded', init);
