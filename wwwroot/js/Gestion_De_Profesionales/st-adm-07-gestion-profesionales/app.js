/* ============================================
SmileTrack — Gestión de Profesionales (st-adm-07-gestion-profesionales)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona la interactividad del módulo de administración de profesionales. 
Este archivo consolida la lógica híbrida actual del módulo:

ARQUITECTURA (Fase 2 completada):
- Carga inicial (SSR): El Controller Razor entrega la vista inicial con los datos de BD 
  para garantizar una primera carga rápida y SEO-friendly.
- CRUD vía API REST: Las operaciones de Crear, Editar (GET/PUT), Cambiar estado (PATCH)
  y Desactivar (DELETE lógico) son asíncronas y consumen `/api/profesionales`.
- Renderizado Dinámico: Tras buscar, filtrar o realizar operaciones CRUD, la tabla 
  es actualizada en el cliente mediante JS sin recargar la página entera.

FUNCIONALIDADES PRINCIPALES:
- Modales de creación, edición y visualización de detalles, poblados vía API.
- Filtros asíncronos y búsqueda con `debounce`.
- Animación progresiva en los contadores de métricas del panel superior.
- Validación de formularios en cliente antes de enviar la petición API.

DEPENDENCIAS TÉCNICAS:
- Controller (SSR initial state): GestionProfesionalesController
- API Controller: ProfesionalesApiController
- CSS: ~/css/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/styles.css
- Partial / Otros: index.cshtml
============================================ */

// Base URL para futuras migraciones a API REST (actualmente no se usa en producción)
const API_BASE = '/api/profesionales';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES - CENTRALIZADAS EN utils.js
// ═══════════════════════════════════════════════════════════════════
// 
// NOTA: Las funciones siguientes están centralizadas en wwwroot/js/shared/utils.js
// Importadas bajo el namespace window.SmileTrack.utils
//
// Aliases globales disponibles para retrocompatibilidad:
// - safeGetElement()
// - debounce()
// - escapeHtml()
// - apiRequest()
// - animateCounter()
// - showToast()
// - openModal() / closeModal()
// - validateForm()
//
// Uso recomendado: window.SmileTrack.utils.safeGetElement(id)
// ═══════════════════════════════════════════════════════════════════
// ═══════════════════════════════════════════════════════════════════
//  MAPEO DE COLORES (solo UI, no afecta lógica de negocio)
// ═══════════════════════════════════════════════════════════════════
// WHY: Centralizar el mapeo aquí evita repetirlo en Razor y en JS.
const SPEC_COLORS = {
  'Odontología General': 'general',
  'Ortodoncia': 'ortodoncia',
  'Endodoncia': 'endodoncia',
  'Odontopediatría': 'pediatria',
  'Cirugía Oral': 'cirugia',
  'Periodoncia': 'periodoncia',
  'Implantología': 'implante',
  'Rehabilitación Oral': 'rehab',
};

// ═══════════════════════════════════════════════════════════════════
//  ESTADO DEL MÓDULO
// ═══════════════════════════════════════════════════════════════════

/** Página actual en la paginación de la tabla (usada por loadProfessionals y goToPage). */
let currentPage = 1;

/** Tamaño de página — debe coincidir con el pageSize enviado a la API. */
const itemsPerPage = 10;

/**
 * ID del profesional que está siendo editado actualmente.
 * null = ninguno (modo creación). Se limpia al cerrar el modal.
 */
let editingId = null;
let detailProfessionalId = null;
let professionalAbsences = [];

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO Y UTILIDADES DE UI
// ═══════════════════════════════════════════════════════════════════

/**
 * Anima contador numérico de 0 al valor objetivo.
 * DELEGADO a window.animateCounter (shared/utils.js) — guard data-animated,
 * formatea con toLocaleString es-CO y soporta data-format="currency-cop".
 * @param {HTMLElement} el
 * @param {number} target
 */
// (resolución de nombre global automática)

/**
 * Obtiene clase CSS para badge de especialidad.
 * @param {string} specialty
 * @returns {string}
 */
const getSpecBadgeClass = (specialty) => SPEC_COLORS[specialty] || 'general';

/**
 * Obtiene clase CSS para badge de estado.
 * @param {string} status
 * @returns {string}
 */
const getStatusBadgeClass = (status) => {
  const map = {
    'activo': 'activo', 'Activo': 'activo',
    'vacaciones': 'vacaciones', 'Vacaciones': 'vacaciones',
    'inactivo': 'inactivo', 'Inactivo': 'inactivo',
  };
  return map[status] || 'inactivo';
};

/**
 * Obtiene color de avatar por especialidad.
 * WHY: Colores deterministas (siempre el mismo por especialidad) mejoran
 *      el reconocimiento visual rápido al escanear la tabla.
 * @param {string} specialty
 * @returns {string}
 */
const getAvatarColor = (specialty) => {
  const colors = {
    'general':    'var(--spec-general)',
    'ortodoncia': 'var(--spec-ortodoncia)',
    'endodoncia': 'var(--spec-endodoncia)',
    'pediatria':  'var(--spec-pediatria)',
    'cirugia':    'var(--spec-cirugia)',
    'periodoncia':'var(--spec-periodoncia)',
    'implante':   'var(--spec-implante)',
    'rehab':      'var(--spec-rehab)',
  };
  return colors[getSpecBadgeClass(specialty)] || 'var(--spec-general)';
};



function renderTableFromApi(result) {
    const tbody = safeGetElement('professionalsTbody');
    if (!tbody) return;

    tbody.innerHTML = '';
    const items = result?.data || []; // API devuelve 'data' como array de items

    if (items.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No se encontraron profesionales con los filtros aplicados.</td></tr>`;
        return;
    }

    // Usar escapeHtml centralizado desde utils.js
    const escapeHtmlLocal = window.SmileTrack?.utils?.escapeHtml || window.escapeHtml || ((s) => s);

    for (const p of items) {
        const tr = document.createElement('tr');
        tr.setAttribute('role', 'row');

        const name = `${escapeHtmlLocal(p.nombres)} ${escapeHtmlLocal(p.apellidos)}`.trim();

        // Especialidad principal: primera de la lista
        const especialidad = p.especialidades && p.especialidades.length > 0
            ? escapeHtmlLocal(p.especialidades[0].nombre)
            : '';

        const specClass = getSpecBadgeClass(especialidad);
        const statusClass = getStatusBadgeClass(p.estado);
        const avatarColor = getAvatarColor(especialidad);

        const initialN = p.nombres ? p.nombres.charAt(0).toUpperCase() : '';
        const initialA = p.apellidos ? p.apellidos.charAt(0).toUpperCase() : '';
        const initials = `${initialN}${initialA}`;

        const telefono = escapeHtmlLocal(p.telefono);
        const estadoText = escapeHtmlLocal(p.estado);
        const registroMedico = escapeHtmlLocal(p.registroMedico);

        tr.innerHTML = `
          <td class="td-profesional">
            <div class="p-avatar" style="background:${avatarColor}" aria-hidden="true">${initials}</div>
            <span class="p-name">${name}</span>
          </td>
          <td><span class="badge-spec ${specClass}">${especialidad || '—'}</span></td>
          <td>${registroMedico}</td>
          <td>${telefono || '—'}</td>
          <td><span class="badge-status ${statusClass}" role="status" aria-label="Estado: ${estadoText}">${estadoText}</span></td>
          <td>
            <div class="actions-cell">
              <button class="btn-secondary btn-view view"
                      type="button"
                      data-id="${p.idProfesional}"
                      data-name="${name}"
                      data-initials="${initials}"
                      data-specialty="${especialidad}"
                      data-registry="${registroMedico}"
                      data-phone="${telefono}"
                      data-status="${estadoText}"
                      data-avatar-color="${avatarColor}"
                      data-status-class="${statusClass}"
                      aria-label="Ver detalles del profesional ${name}"
                      title="Ver detalles del profesional ${name}">
                <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span> <span class="btn-text">Ver</span>
              </button>
              <button class="btn-secondary edit"
                      type="button"
                      aria-label="Editar el profesional ${name}"
                      title="Editar profesional ${name}"
                      onclick="editProfessional(${p.idProfesional})">
                <span class="material-symbols-outlined action-icon" aria-hidden="true">edit</span> <span class="btn-text">Editar</span>
              </button>
              ${(estadoText || '').toLowerCase() === 'activo'
                ? `<button class="btn-danger btn-delete toggle"
                        type="button"
                        data-id="${p.idProfesional}"
                        data-name="${name}"
                        data-estado="${estadoText}"
                        aria-label="Desactivar el profesional ${name}"
                        title="Desactivar profesional ${name}">
                    <span class="material-symbols-outlined action-icon" aria-hidden="true">block</span> <span class="btn-text">Desactivar</span>
                  </button>`
                : `<button class="btn-danger btn-delete toggle"
                        type="button"
                        data-id="${p.idProfesional}"
                        data-name="${name}"
                        data-estado="${estadoText || 'inactivo'}"
                        aria-label="Reactivar el profesional ${name}"
                        title="Reactivar profesional ${name}">
                    <span class="material-symbols-outlined action-icon" aria-hidden="true">check_circle</span> <span class="btn-text">Reactivar</span>
                  </button>`
              }
            </div>
          </td>
        `;

        // Enlazar el botón Ver al modal de detalle
        const viewBtn = tr.querySelector('.btn-view');
        viewBtn?.addEventListener('click', () => {
            const avatar = safeGetElement('detailAvatar');
            const nameEl = safeGetElement('detailName');
            const specialtyEl = safeGetElement('detailSpecialty');
            const registryEl = safeGetElement('detailRegistry');
            const phoneEl = safeGetElement('detailPhone');
            const statusEl = safeGetElement('detailStatus');

            // Leer color y clase del propio dataset del botón — evita el bug
            // de closure donde avatarColor pertenecía a la última iteración del loop.
            const btnAvatarColor = viewBtn.dataset.avatarColor || avatarColor;
            const btnStatusClass = viewBtn.dataset.statusClass || statusClass;

            if (avatar) { avatar.textContent = viewBtn.dataset.initials || '--'; avatar.style.background = btnAvatarColor; }
            if (nameEl) nameEl.textContent = viewBtn.dataset.name || '--';
            if (specialtyEl) specialtyEl.textContent = viewBtn.dataset.specialty || '--';
            if (registryEl) registryEl.textContent = viewBtn.dataset.registry || '--';
            if (phoneEl) phoneEl.textContent = viewBtn.dataset.phone || '--';
            if (statusEl) {
                statusEl.textContent = viewBtn.dataset.status || '--';
                statusEl.className = `badge-status ${btnStatusClass}`;
            }

            const modal = safeGetElement('modalDetail');
            if (modal) {
                modal.classList.add('open');
                modal.setAttribute('aria-hidden', 'false');
                modal.removeAttribute('inert');
                document.body.style.overflow = 'hidden';
                detailProfessionalId = Number(viewBtn.dataset.id);
                resetAbsenceForm();
                loadProfessionalAbsences(detailProfessionalId);
                safeGetElement('modalDetailClose')?.focus();
            }
        });

        // Enlazar el botón Desactivar/Reactivar al modal de confirmación
        const deleteBtn = tr.querySelector('.btn-delete');
        deleteBtn?.addEventListener('click', () => {
            openConfirmToggleEstadoModal(p.idProfesional, name, deleteBtn.dataset.estado || 'activo');
        });

        tbody.appendChild(tr);
    }
}

const setProfessionalsLoading = (loading) => {
  const table = safeGetElement('professionalsTable');
  if (table) table.setAttribute('aria-busy', String(loading));
  const buttons = safeGetElement('paginationButtons');
  if (buttons) buttons.querySelectorAll('button').forEach(button => { button.disabled = loading; });
};

const resetAbsenceForm = () => {
  safeGetElement('absenceForm')?.reset();
  safeGetElement('absenceId').value = '';
  safeGetElement('absenceSaveButton').textContent = 'Registrar ausencia';
  safeGetElement('absenceCancelEdit').hidden = true;
};

const renderAbsences = () => {
  const list = safeGetElement('absenceList');
  if (!list) return;
  list.replaceChildren();
  if (professionalAbsences.length === 0) {
    list.textContent = 'No hay ausencias registradas.';
    return;
  }

  professionalAbsences.forEach((absence) => {
    const item = document.createElement('div');
    item.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;border:1px solid var(--border-color,#e5e7eb);padding:10px;border-radius:6px;';
    const details = document.createElement('div');
    details.innerHTML = `<strong>${escapeHtml(absence.tipo || 'Ausencia')}</strong><br><small>${escapeHtml(absence.fechaInicio)} a ${escapeHtml(absence.fechaFin)}</small>${absence.observaciones ? `<br><small>${escapeHtml(absence.observaciones)}</small>` : ''}`;
    const actions = document.createElement('div');
    const edit = document.createElement('button');
    edit.type = 'button';
    edit.className = 'btn-secondary';
    edit.textContent = 'Editar';
    edit.addEventListener('click', () => {
      safeGetElement('absenceId').value = absence.idAusencia;
      safeGetElement('absenceTipo').value = absence.tipo || 'otro';
      safeGetElement('absenceFechaInicio').value = absence.fechaInicio;
      safeGetElement('absenceFechaFin').value = absence.fechaFin;
      safeGetElement('absenceObservaciones').value = absence.observaciones || '';
      safeGetElement('absenceSaveButton').textContent = 'Guardar ausencia';
      safeGetElement('absenceCancelEdit').hidden = false;
    });
    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'btn-secondary';
    remove.textContent = 'Eliminar';
    remove.addEventListener('click', async () => {
      window.ModalService?.confirm({
        title: '¿Eliminar ausencia?',
        message: 'Esta acción eliminará permanentemente el registro de ausencia del profesional y no podrá deshacerse.',
        confirmText: 'Sí, eliminar',
        cancelText: 'Cancelar',
        isDanger: true,
        onConfirm: async () => {
          try {
            const result = await apiRequest(`${API_BASE}/${detailProfessionalId}/ausencias/${absence.idAusencia}`, { method: 'DELETE' });
            window.ToastService?.success(result.message || 'Ausencia eliminada.');
            await loadProfessionalAbsences(detailProfessionalId);
          } catch (error) {
            window.ToastService?.error(`No se pudo eliminar la ausencia: ${error.message}`);
          }
        }
      });
    });
    actions.append(edit, remove);
    item.append(details, actions);
    list.appendChild(item);
  });
};

// ═══════════════════════════════════════════════════════════════════
//  PESTAÑAS DEL MODAL DETALLE
// ═══════════════════════════════════════════════════════════════════

const initModalTabs = () => {
  const tabs = [
    { btn: 'tabBtnGeneral', pane: 'tabPaneGeneral' },
    { btn: 'tabBtnHorarios', pane: 'tabPaneHorarios' },
    { btn: 'tabBtnAusencias', pane: 'tabPaneAusencias' },
  ];

  tabs.forEach(t => {
    const btnEl = safeGetElement(t.btn);
    btnEl?.addEventListener('click', () => {
      tabs.forEach(other => {
        safeGetElement(other.btn)?.classList.remove('active');
        safeGetElement(other.btn)?.setAttribute('aria-selected', 'false');
        safeGetElement(other.pane)?.classList.remove('active');
      });
      btnEl.classList.add('active');
      btnEl.setAttribute('aria-selected', 'true');
      safeGetElement(t.pane)?.classList.add('active');
    });
  });
};

const resetModalTabs = () => {
  safeGetElement('tabBtnGeneral')?.click();
};

// ═══════════════════════════════════════════════════════════════════
//  GESTIÓN DE HORARIOS DEL PROFESIONAL
// ═══════════════════════════════════════════════════════════════════

const DAYS_OF_WEEK = [
  { key: 'Lunes', label: 'Lunes', short: 'Lun' },
  { key: 'Martes', label: 'Martes', short: 'Mar' },
  { key: 'Miercoles', label: 'Miércoles', short: 'Mié' },
  { key: 'Jueves', label: 'Jueves', short: 'Jue' },
  { key: 'Viernes', label: 'Viernes', short: 'Vie' },
  { key: 'Sabado', label: 'Sábado', short: 'Sáb' },
  { key: 'Domingo', label: 'Domingo', short: 'Dom' },
];

let professionalSchedules = [];

const renderScheduleDays = () => {
  const container = safeGetElement('scheduleDaysContainer');
  if (!container) return;
  container.innerHTML = '';

  DAYS_OF_WEEK.forEach((d) => {
    const existing = professionalSchedules.find(
      (s) => (s.diaSemana || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') ===
             d.key.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    );

    const isActive = existing ? existing.activo : (d.key !== 'Sabado' && d.key !== 'Domingo');
    const startTime = existing?.horaInicio ? existing.horaInicio.substring(0, 5) : '08:00';
    const endTime = existing?.horaFin ? existing.horaFin.substring(0, 5) : '17:00';

    const row = document.createElement('div');
    row.className = `schedule-row ${isActive ? '' : 'inactive'}`;
    row.dataset.day = d.key;

    row.innerHTML = `
      <div class="schedule-row-day">${d.label}</div>
      <label class="schedule-toggle">
        <input type="checkbox" class="schedule-day-active" ${isActive ? 'checked' : ''} data-day="${d.key}">
        <span>${isActive ? 'Atiende' : 'No atiende'}</span>
      </label>
      <div class="schedule-time-group">
        <span class="schedule-time-label">Inicio:</span>
        <input type="time" class="form-input schedule-time-start" value="${startTime}" ${isActive ? '' : 'disabled'} style="padding:4px 8px;font-size:0.85rem;" required>
      </div>
      <div class="schedule-time-group">
        <span class="schedule-time-label">Fin:</span>
        <input type="time" class="form-input schedule-time-end" value="${endTime}" ${isActive ? '' : 'disabled'} style="padding:4px 8px;font-size:0.85rem;" required>
      </div>
    `;

    const checkbox = row.querySelector('.schedule-day-active');
    const labelSpan = row.querySelector('.schedule-toggle span');
    const startInput = row.querySelector('.schedule-time-start');
    const endInput = row.querySelector('.schedule-time-end');

    checkbox.addEventListener('change', (e) => {
      const checked = e.target.checked;
      row.classList.toggle('inactive', !checked);
      labelSpan.textContent = checked ? 'Atiende' : 'No atiende';
      startInput.disabled = !checked;
      endInput.disabled = !checked;
    });

    container.appendChild(row);
  });
};

const applyDefaultSchedule = () => {
  const rows = document.querySelectorAll('#scheduleDaysContainer .schedule-row');
  rows.forEach(row => {
    const day = row.dataset.day;
    const isWeekday = day !== 'Sabado' && day !== 'Domingo';
    const checkbox = row.querySelector('.schedule-day-active');
    const labelSpan = row.querySelector('.schedule-toggle span');
    const startInput = row.querySelector('.schedule-time-start');
    const endInput = row.querySelector('.schedule-time-end');

    if (checkbox) checkbox.checked = isWeekday;
    if (labelSpan) labelSpan.textContent = isWeekday ? 'Atiende' : 'No atiende';
    row.classList.toggle('inactive', !isWeekday);
    if (startInput) {
      startInput.value = '08:00';
      startInput.disabled = !isWeekday;
    }
    if (endInput) {
      endInput.value = '17:00';
      endInput.disabled = !isWeekday;
    }
  });
  window.ToastService?.info?.('Horario estándar (Lun-Vie 08:00 - 17:00) cargado en el formulario. Recuerda guardar cambios.');
};

const loadProfessionalSchedule = async (id) => {
  detailProfessionalId = Number(id);
  const loading = safeGetElement('scheduleLoading');
  const statusMsg = safeGetElement('scheduleStatusMsg');
  if (statusMsg) statusMsg.textContent = '';
  if (loading) loading.textContent = 'Cargando horarios de atención...';

  try {
    const result = await apiRequest(`${API_BASE}/${detailProfessionalId}/horarios`);
    professionalSchedules = result?.data || [];
    renderScheduleDays();
  } catch (error) {
    professionalSchedules = [];
    renderScheduleDays();
    if (statusMsg) {
      statusMsg.style.color = 'var(--text-muted)';
      statusMsg.textContent = 'ℹ️ Sin horario personalizado (aplica horario general de la clínica).';
    }
  } finally {
    if (loading) loading.textContent = '';
  }
};

const saveSchedule = async (event) => {
  event.preventDefault();
  if (!detailProfessionalId) return;

  const rows = document.querySelectorAll('#scheduleDaysContainer .schedule-row');
  const payload = [];

  for (const row of rows) {
    const dayKey = row.dataset.day;
    const active = row.querySelector('.schedule-day-active').checked;
    const start = row.querySelector('.schedule-time-start').value;
    const end = row.querySelector('.schedule-time-end').value;

    if (active) {
      if (!start || !end) {
        window.ToastService?.warning?.(`Debes especificar hora de inicio y fin para el ${dayKey}.`);
        return;
      }
      if (end <= start) {
        window.ToastService?.warning?.(`La hora de fin debe ser posterior a la hora de inicio para el ${dayKey}.`);
        return;
      }
    }

    payload.push({
      day: dayKey.substring(0, 3),
      diaSemana: dayKey,
      dayFull: dayKey,
      active: active,
      start: start,
      end: end
    });
  }

  const saveBtn = safeGetElement('scheduleSaveButton');
  const statusMsg = safeGetElement('scheduleStatusMsg');
  if (saveBtn) saveBtn.disabled = true;
  if (statusMsg) {
    statusMsg.style.color = 'var(--primary)';
    statusMsg.textContent = '⏳ Guardando horarios en base de datos...';
  }

  try {
    const result = await apiRequest(`${API_BASE}/${detailProfessionalId}/horarios`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    window.ToastService?.success?.(result?.message || 'Horarios actualizados correctamente.');
    if (statusMsg) {
      statusMsg.style.color = 'var(--green, #166534)';
      statusMsg.textContent = '✅ Horarios guardados en base de datos.';
    }
    await loadProfessionalSchedule(detailProfessionalId);
  } catch (error) {
    window.ToastService?.error?.(`No se pudieron guardar los horarios: ${error.message}`);
    if (statusMsg) {
      statusMsg.style.color = 'var(--red, #b91c1c)';
      statusMsg.textContent = `❌ Error: ${error.message}`;
    }
  } finally {
    if (saveBtn) saveBtn.disabled = false;
  }
};

const loadProfessionalAbsences = async (id) => {
  detailProfessionalId = Number(id);
  const loading = safeGetElement('absenceLoading');
  if (loading) loading.textContent = 'Cargando ausencias...';
  try {
    const result = await apiRequest(`${API_BASE}/${detailProfessionalId}/ausencias`);
    professionalAbsences = result?.data || [];
    renderAbsences();
  } catch (error) {
    professionalAbsences = [];
    const list = safeGetElement('absenceList');
    if (list) list.textContent = `No se pudieron cargar las ausencias: ${error.message}`;
  } finally {
    if (loading) loading.textContent = '';
  }
};

const saveAbsence = async (event) => {
  event.preventDefault();
  const id = safeGetElement('absenceId').value;
  const body = {
    tipo: safeGetElement('absenceTipo').value,
    fechaInicio: safeGetElement('absenceFechaInicio').value,
    fechaFin: safeGetElement('absenceFechaFin').value,
    observaciones: safeGetElement('absenceObservaciones').value || null
  };
  if (body.fechaFin < body.fechaInicio) {
    window.ToastService?.warning('La fecha de fin debe ser igual o posterior a la fecha de inicio.');
    return;
  }
  const button = safeGetElement('absenceSaveButton');
  button.disabled = true;
  try {
    const endpoint = id
      ? `${API_BASE}/${detailProfessionalId}/ausencias/${id}`
      : `${API_BASE}/${detailProfessionalId}/ausencias`;
    const result = await apiRequest(endpoint, { method: id ? 'PUT' : 'POST', body });
    window.ToastService?.success(result.message || 'Ausencia guardada.');
    resetAbsenceForm();
    await loadProfessionalAbsences(detailProfessionalId);
  } catch (error) {
    window.ToastService?.error(`No se pudo guardar la ausencia: ${error.message}`);
  } finally {
    button.disabled = false;
  }
};



/**
 * Edita profesional: carga los datos desde la API y llena el modal.
 * - Carga todos los campos editables, incluyendo Categoria (H-02).
 * - Guarda el estado original en data-originalEstado para que saveProfessional
 *   solo dispare el PATCH cuando el estado realmente cambia (H-05).
 */
window.editProfessional = async (id) => {
  try {
    const result = await apiRequest(`${API_BASE}/${id}`);
    const p = result.data;
    if (!p) return;

    editingId = id;

    // Llenar campos del formulario con los datos de la API
    const set = (fieldId, value) => { const el = safeGetElement(fieldId); if (el) el.value = value ?? ''; };

    set('formIdProfesional', p.idProfesional);
    set('formNombres', p.nombres);
    set('formApellidos', p.apellidos);
    set('formRegistroMedico', p.registroMedico);
    set('formCategoria', p.categoria);   // H-02: cargar Categoria al abrir el modal
    set('formTelefono', p.telefono);
    set('formCorreoAcceso', p.correoAcceso);

    // Sincronizar el select de Estado.
    // H-05: también almacenamos el estado original para comparar al guardar y
    //       no ejecutar un PATCH innecesario cuando no cambió.
    const estadoNorm = (p.estado || 'activo').toLowerCase();
    const statusSelect = safeGetElement('formStatus');
    if (statusSelect) {
      statusSelect.value = estadoNorm;
      statusSelect.dataset.originalEstado = estadoNorm;  // H-05: referencia original
    }
    set('formEstado', estadoNorm);

    // Contraseña: vacía siempre en edición (se conserva si no se cambia)
    set('formContrasenaAcceso', '');
    updateProfessionalPasswordRules();

    // Especialidad: asignar por idEspecialidad numérico
    const formSelect = document.querySelector('select[name="IdEspecialidad"]') || safeGetElement('formIdEspecialidad');
    if (formSelect && p.especialidades && p.especialidades.length > 0) {
      formSelect.value = p.especialidades[0].idEspecialidad;
    } else if (formSelect) {
      formSelect.value = '';
    }

    const modalTitle = safeGetElement('modalFormTitle');
    if (modalTitle) modalTitle.textContent = 'Editar Profesional';

    // Pasar isEditing=true para que openFormModal no resetee el formulario
    openFormModal(true);
  } catch (err) {
    window.ToastService?.error(`❌ No se pudo cargar el profesional: ${err.message}`);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODALES
// ═══════════════════════════════════════════════════════════════════

/**
 * Abre modal de formulario y registra quién lo abrió.
 * WHY: WCAG 2.4.3 — al cerrar un modal el foco debe regresar al elemento
 *      que lo disparó. Sin esto el foco queda al principio del documento.
 *
 * @param {boolean} [isEditing=false] - true cuando se llama desde editProfessional;
 *   en ese caso NO se resetea el formulario porque los datos ya fueron llenados.
 */
const openFormModal = (isEditing = false) => {
  // Registrar el botón que abre el modal para devolverle el foco al cerrar
  lastModalOpener = document.activeElement;

  // Solo limpiar el formulario al crear un profesional nuevo.
  // Al editar, editProfessional() ya llenó los campos — no los borramos.
  if (!isEditing) {
    editingId = null;
    const form = safeGetElement('formProfessional');
    if (form) form.reset();

    const modalTitle = safeGetElement('modalFormTitle');
    if (modalTitle) modalTitle.textContent = 'Nuevo Profesional';
  }

  const modal = safeGetElement('modalForm');
  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    const firstInput = modal.querySelector('input:not([type="hidden"])');
    if (firstInput) firstInput.focus();
    document.body.style.overflow = 'hidden';
  }
};

/**
 * Cierra modal de formulario y devuelve el foco al elemento que lo abrió.
 * WHY: WCAG 2.4.3 — el foco debe regresar al botón que disparó el modal
 *      ("+ Nuevo Profesional" o el botón editar ✏️ de la fila correspondiente).
 */
const closeFormModal = () => {
  const modal = safeGetElement('modalForm');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  editingId = null;
  // Devolver el foco al elemento que abrió el modal (si sigue en el DOM)
  if (lastModalOpener && typeof lastModalOpener.focus === 'function') {
    lastModalOpener.focus();
  }
  lastModalOpener = null;
};

/**
 * Cierra modal de detalle y devuelve el foco al elemento que lo abrió.
 * WHY: Mismo principio WCAG 2.4.3 que closeFormModal.
 */
const closeDetailModal = () => {
  const modal = safeGetElement('modalDetail');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  if (lastModalOpener && typeof lastModalOpener.focus === 'function') {
    lastModalOpener.focus();
  }
  lastModalOpener = null;
};

// Crea o actualiza profesional — submit nativo para que el antiforgery token viaje correctamente
// setFieldValidity removed in favor of ValidationUtils

const validateProfessionalForm = (form) => {
  let valid = true;

  const requiredFields = [
    { id: 'formNombres', message: 'Ingresa los nombres.' },
    { id: 'formApellidos', message: 'Ingresa los apellidos.' },
    { id: 'formRegistroMedico', message: 'Ingresa el registro médico.' },
    { id: 'formCorreoAcceso', message: 'Ingresa un correo de acceso válido.' }
  ];

  if (window.ValidationUtils) {
    requiredFields.forEach(({ id }) => {
      const field = safeGetElement(id);
      if (field) window.ValidationUtils.clearError(field);
    });
  }

  requiredFields.forEach(({ id, message }) => {
    const field = safeGetElement(id);
    const value = field?.value.trim() || '';
    const fieldValid = Boolean(value);

    if (!fieldValid) {
      valid = false;
      if (window.ValidationUtils && field) {
        window.ValidationUtils.showError(field, null, message);
      }
    }
  });

  const correo = safeGetElement('formCorreoAcceso');
  if (correo && correo.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo.value.trim())) {
    valid = false;
    if (window.ValidationUtils) {
      window.ValidationUtils.showError(correo, null, 'Ingresa un correo de acceso válido.');
    }
  }

  // Seguridad: la contraseña no se valida ni se envía desde el cliente.
  // La generación segura se realiza en backend al crear el profesional.
  return valid;
};

const updateProfessionalPasswordRules = () => {
  const input = safeGetElement('formContrasenaAcceso');
  if (!input) return;

  const value = input.value || '';
  const rules = {
    length: value.length >= 8,
    upper: /[A-Z]/.test(value),
    lower: /[a-z]/.test(value),
    number: /\d/.test(value),
    symbol: /[^A-Za-z\d]/.test(value)
  };

  Object.entries(rules).forEach(([key, ok]) => {
    const row = document.querySelector(`#professionalPasswordRules [data-rule="${key}"]`);
    if (!row) return;
    const icon = row.querySelector('.rule-icon');
    if (icon) icon.textContent = ok ? '✓' : '✗';
    row.style.color = ok ? '#15803d' : '#b91c1c';
  });

  const formId = safeGetElement('formIdProfesional');
  const editing = Number(formId?.value || 0) > 0;
  const help = safeGetElement('formContrasenaHelp');

  if (help) {
    if (editing && value.length === 0) {
      help.textContent = 'La contraseña no se modifica desde esta pantalla.';
      help.style.color = '#6b7280';
    } else {
      help.textContent = 'La contraseña se genera automáticamente en el sistema.';
      help.style.color = '#6b7280';
    }
  }
};

const initProfessionalPassword = () => {
  const input = safeGetElement('formContrasenaAcceso');
  const toggle = safeGetElement('toggleProfesionalPassword');
  if (!input) return;

  input.addEventListener('input', updateProfessionalPasswordRules);
  updateProfessionalPasswordRules();

  toggle?.addEventListener('click', () => {
    const visible = input.type === 'text';
    input.type = visible ? 'password' : 'text';
    toggle.textContent = visible ? '👁' : '🙈';
    toggle.setAttribute('aria-label', visible ? 'Mostrar contraseña' : 'Ocultar contraseña');
  });
};


/**
 * Intercepta el submit del formulario y lo envía a la API (POST o PUT).
 * H-02: el payload incluye ahora el campo 'categoria'.
 * H-05: el PATCH de estado solo se ejecuta cuando el estado cambió respecto
 *        al valor original cargado al abrir el modal (data-originalEstado).
 */
const saveProfessional = async (e) => {
  e.preventDefault();

  const form = e.currentTarget;
  const valid = validateProfessionalForm(form);
  if (!valid) {
    window.ToastService?.warning('⚠️ Completa los campos obligatorios marcados en rojo.');
    const firstInvalid = form.querySelector('[aria-invalid="true"]');
    if (firstInvalid) firstInvalid.focus();
    return;
  }

  const submitBtn = form.querySelector('[type="submit"]');
  if (submitBtn) { submitBtn.disabled = true; submitBtn.textContent = '⏳ Guardando...'; }

  // Construir el payload que espera ProfesionalApiRequest.
  // NOTA: 'estado' NO se incluye aquí porque ProfesionalApiRequest no lo expone;
  //       el cambio de estado se maneja por separado vía PATCH /{id}/estado.
  const getData = (id) => safeGetElement(id)?.value?.trim() ?? '';
  const idProfesional = Number(getData('formIdProfesional'));
  const isEditing = idProfesional > 0;

  // IdEspecialidad viene del select con name="IdEspecialidad"
  const especialidadEl = form.querySelector('select[name="IdEspecialidad"]') || safeGetElement('formIdEspecialidad');
  const idEspecialidad = especialidadEl ? Number(especialidadEl.value) || null : null;

  const payload = {
    nombres:        getData('formNombres'),
    apellidos:      getData('formApellidos'),
    registroMedico: getData('formRegistroMedico'),
    categoria:      getData('formCategoria') || null,  // H-02: conservar Categoria en BD
    telefono:       getData('formTelefono')  || null,
    correoAcceso:   getData('formCorreoAcceso'),
    idEspecialidad: idEspecialidad,
    estado:         isEditing ? (safeGetElement('formStatus')?.value || safeGetElement('formEstado')?.value || '').trim().toLowerCase() || null : null,
  };

  // Seguridad: la contraseña nunca se envía desde el cliente. El backend genera
  // una contraseña temporal segura al crear el profesional y la notificación se
  // gestiona en el servidor, evitando exponer credenciales en la solicitud.
  if (!isEditing) {
    delete payload.contrasenaAcceso;
  }

  try {
    let result;
    if (isEditing) {
      result = await apiRequest(`${API_BASE}/${idProfesional}`, {
        method: 'PUT',
        body: JSON.stringify(payload)
      });

    } else {
      result = await apiRequest(API_BASE, {
        method: 'POST',
        body: JSON.stringify(payload)
      });
    }

    if (!result || result.success === false) {
      throw new Error(result?.message || 'No fue posible guardar el profesional.');
    }

    window.ToastService?.success(`✅ ${result.message || 'Profesional guardado correctamente.'}`);
    closeFormModal();
    currentPage = 1;
    await loadProfessionals();
  } catch (err) {
    window.ToastService?.error(`❌ ${err.message}`);
  } finally {
    if (submitBtn) { submitBtn.disabled = false; submitBtn.textContent = '💾 Guardar'; }
  }
};

const bindProfessionalFieldValidation = () => {
  const form = safeGetElement('formProfessional');
  const absenceForm = safeGetElement('absenceForm');
  if (!form) return;

  form.querySelectorAll('input, select').forEach((field) => {
    field.addEventListener('input', () => {
      if (field.id === 'formTelefono' && field.value.trim()) {
        const validPhone = (field.value.match(/\d/g) || []).length >= 7
          && (field.value.match(/\d/g) || []).length <= 15;
        if (window.ValidationUtils) {
          if (!validPhone) window.ValidationUtils.showError(field, null, 'Ingresa un teléfono válido.');
          else window.ValidationUtils.clearError(field);
        }
      } else if (field.id === 'formRegistroMedico' && field.value.trim()) {
        const validRegistry = /^[A-Za-z0-9\-\. ]{3,30}$/.test(field.value.trim());
        if (window.ValidationUtils) {
          if (!validRegistry) window.ValidationUtils.showError(field, null, 'Use solo letras, números y guiones.');
          else window.ValidationUtils.clearError(field);
        }
      } else {
         if (window.ValidationUtils && field.value.trim()) {
            window.ValidationUtils.clearError(field);
         }
      }
    });
  });
};


// ═══════════════════════════════════════════════════════════════════
//  FILTROS Y BÚSQUEDA
// ═══════════════════════════════════════════════════════════════════

/**
 * Carga especialidades desde la API y pobla los selects.
 */
async function loadSpecialties() {
    try {
        const result = await apiRequest(`${API_BASE}/especialidades`);
        const specialties = result.data || [];
        
        const filterSelect = safeGetElement('filterSpecialty');
        // El id exacto en el formulario dependerá de Razor, comúnmente 'IdEspecialidad' o 'formIdEspecialidad'
        const formSelect = document.querySelector('select[name="IdEspecialidad"]') || safeGetElement('formIdEspecialidad');

        if (filterSelect) {
            const currentVal = filterSelect.value;
            filterSelect.innerHTML = '<option value="">Todas las especialidades</option>' +
                specialties.map(s => `<option value="${s.nombre}">${s.nombre}</option>`).join('');
            filterSelect.value = currentVal;
        }

        if (formSelect) {
            const currentVal = formSelect.value;
            formSelect.innerHTML = '<option value="" disabled selected>Selecciona una especialidad</option>' +
                specialties.map(s => `<option value="${s.idEspecialidad}">${s.nombre}</option>`).join('');
            if (currentVal) formSelect.value = currentVal;
        }
    } catch (e) {
        console.error("Error al cargar especialidades", e);
    }
}

/**
 * Navega a una página específica y recarga la tabla.
 */
window.goToPage = async (page) => {
    currentPage = page;
    await loadProfessionals();
};

/**
 * Renderiza los controles de paginación desde la metadata de la API.
 */
const renderPaginationFromApi = (result) => {
    const info = safeGetElement('paginationInfo');
    const buttons = safeGetElement('paginationButtons');
    if (!info || !buttons || !result.pagination) return;

    const { page, pageSize, totalCount, totalPages } = result.pagination;

    const start = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
    const end = Math.min(page * pageSize, totalCount);

    info.textContent = `Mostrando ${start}-${end} de ${totalCount} profesionales`;
    buttons.innerHTML = '';

    // Botón anterior
    const btnPrev = document.createElement('button');
    btnPrev.textContent = '«';
    btnPrev.setAttribute('aria-label', 'Página anterior');
    btnPrev.disabled = page === 1 || totalCount === 0;
    btnPrev.addEventListener('click', () => { if (page > 1) goToPage(page - 1); });
    buttons.appendChild(btnPrev);

    // Botones numéricos
    for (let i = 1; i <= totalPages; i++) {
        const btn = document.createElement('button');
        btn.textContent = i;
        btn.setAttribute('aria-label', `Ir a página ${i}`);
        btn.setAttribute('aria-current', i === page ? 'page' : 'false');
        if (i === page) btn.classList.add('active');
        btn.addEventListener('click', () => goToPage(i));
        buttons.appendChild(btn);
    }

    // Botón siguiente
    const btnNext = document.createElement('button');
    btnNext.textContent = '»';
    btnNext.setAttribute('aria-label', 'Página siguiente');
    btnNext.disabled = page >= totalPages || totalCount === 0;
    btnNext.addEventListener('click', () => { if (page < totalPages) goToPage(page + 1); });
    buttons.appendChild(btnNext);
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS (Listas para conectar al backend C#)
// ═══════════════════════════════════════════════════════════════════

/**
 * Obtiene lista de profesionales desde el servidor.
 */
async function fetchProfessionals(params = {}) {
    const query = new URLSearchParams();

    query.set('page', params.page ?? 1);
    query.set('pageSize', params.pageSize ?? 10);

    if (params.search) {
        query.set('search', params.search);
    }

    if (params.especialidad) {
        query.set('especialidad', params.especialidad);
    }

    if (params.estado) {
        query.set('estado', params.estado);
    }

    return await apiRequest(`${API_BASE}?${query.toString()}`);
}

async function loadProfessionals() {
  try {
    const search = document.querySelector('#searchInput')?.value?.trim() || '';
    const especialidad = document.querySelector('#filterSpecialty')?.value || '';
    const estado = document.querySelector('#filterStatus')?.value || '';

    setProfessionalsLoading(true);
    const result = await fetchProfessionals({
        page: currentPage,
        pageSize: itemsPerPage,
        search,
        especialidad,
        estado
    });

    renderTableFromApi(result);
    renderPaginationFromApi(result);
  } catch (error) {
    const tbody = safeGetElement('professionalsTbody');
    if (tbody) {
      tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No fue posible cargar los profesionales. Intenta nuevamente.</td></tr>`;
    }
    window.ToastService?.error?.(`No fue posible cargar los profesionales: ${error.message}`);
  } finally {
    setProfessionalsLoading(false);
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

/**
 * Inicializa sidebar móvil con gestión de foco y ARIA.
 * WHY: Mejora accesibilidad — sin esto, el sidebar abierto en móvil no tiene gestión de foco
 *      y un usuario de lector de pantalla no sabe que el menú está abierto.
 */
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
    
    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  // ✅ Navegación: cerrar menú en móvil, SIN bloquear enlaces
  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMenu(false);
      }
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

/**
 * Inicializa los filtros para usar la API en lugar de submit de MVC.
 */
const initFiltersAPI = () => {
  const searchInput = safeGetElement('searchInput');
  const filterSpecialty = safeGetElement('filterSpecialty');
  const filterStatus = safeGetElement('filterStatus');

  // Prevenir que el formulario recargue la página si el usuario presiona Enter
  const form = searchInput?.closest('form') || document.querySelector('.filters-section form');
  if (form) {
      form.addEventListener('submit', (e) => {
          e.preventDefault();
          currentPage = 1;
          loadProfessionals();
      });
  }

  searchInput?.addEventListener('input', debounce(() => {
      currentPage = 1;
      loadProfessionals();
  }, 400));

  filterSpecialty?.addEventListener('change', () => {
      currentPage = 1;
      loadProfessionals();
  });

  filterStatus?.addEventListener('change', () => {
      currentPage = 1;
      loadProfessionals();
  });
};

/**
 * Inicializa modales: eventos de apertura, cierre y teclado.
 * WHY: Centraliza toda la configuración de modales para que initSidebar/initFilters
 *      no necesiten conocer los detalles del modal (separación de responsabilidades).
 */
// Variables for delete modal
let lastModalOpener = null;

const openConfirmToggleEstadoModal = (id, name, estadoActual) => {
  lastModalOpener = document.activeElement;
  const modal = safeGetElement('modalConfirmDelete');
  const titleEl = safeGetElement('modalConfirmDeleteTitle');
  const message = safeGetElement('modalConfirmDeleteMessage');
  const warning = document.getElementById('modalConfirmDeleteWarning') || null;
  const deleteIdInput = safeGetElement('deleteProfesionalId');
  const estadoInput = safeGetElement('deleteProfesionalEstado');
  const confirmBtn = safeGetElement('modalConfirmDeleteConfirm');
  const cancelBtn = safeGetElement('modalConfirmDeleteCancel');

  const esActivo = (estadoActual || 'activo').toLowerCase() === 'activo';

  if (titleEl) {
    titleEl.textContent = esActivo
      ? 'Desactivar profesional'
      : 'Reactivar profesional';
  }
  if (message) {
    message.textContent = esActivo
      ? `¿Estás seguro de desactivar a ${name}? El profesional quedará inactivo y no podrá recibir nuevas citas.`
      : `¿Estás seguro de reactivar a ${name}? El profesional pasará a estado activo y volverá a recibir citas.`;
  }
  if (warning) {
    warning.innerHTML = esActivo
      ? '⚠️ El profesional pasará a estado <strong>inactivo</strong> y no podrá recibir nuevas citas. Esta operación es reversible: puedes reactivarlo desde la misma columna acciones. No es posible desactivar profesionales con citas activas pendientes.'
      : '✅ El profesional volverá a estado <strong>activo</strong> y estará disponible para agendar nuevas citas. Esta operación es reversible: puedes desactivarlo desde la misma columna acciones.';
  }
  if (cancelBtn) {
    cancelBtn.textContent = esActivo ? 'Cancelar — no desactivar' : 'Cancelar — no reactivar';
  }
  if (confirmBtn) {
    confirmBtn.textContent = esActivo ? 'Confirmar desactivación' : 'Confirmar reactivación';
    confirmBtn.style.backgroundColor = esActivo ? 'var(--red)' : 'var(--primary)';
    confirmBtn.style.borderColor = esActivo ? 'var(--red)' : 'var(--primary)';
    confirmBtn.dataset.loadingText = esActivo ? '⏳ Desactivando...' : '⏳ Reactivando...';
  }
  if (deleteIdInput) deleteIdInput.value = id;
  if (estadoInput) estadoInput.value = estadoActual || 'activo';

  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    setTimeout(() => {
      const cancelBtnEl = safeGetElement('modalConfirmDeleteCancel');
      if (cancelBtnEl) cancelBtnEl.focus();
    }, 50);
  }
};

const closeConfirmDeleteModal = () => {
  const modal = safeGetElement('modalConfirmDelete');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  if (lastModalOpener && typeof lastModalOpener.focus === 'function') {
    lastModalOpener.focus();
  }
  lastModalOpener = null;
};

const initModals = () => {
  const btnNew = safeGetElement('profesionales-btn-nuevo') || safeGetElement('btnNewProfessional');
  const modalFormClose = safeGetElement('modalFormClose');
  const modalFormCancel = safeGetElement('modalFormCancel');
  const modalDetailClose = safeGetElement('modalDetailClose');
  const modalDetailCloseBtn = safeGetElement('modalDetailCloseBtn');
  const modalConfirmDeleteClose = safeGetElement('modalConfirmDeleteClose');
  const modalConfirmDeleteCancel = safeGetElement('modalConfirmDeleteCancel');
  const modalForm = safeGetElement('modalForm');
  const modalDetail = safeGetElement('modalDetail');
  const modalConfirmDelete = safeGetElement('modalConfirmDelete');
  const form = safeGetElement('formProfessional');
  
  // Abrir modal crear
  btnNew?.addEventListener('click', openFormModal);
  
  // Cerrar modales
  modalFormClose?.addEventListener('click', closeFormModal);
  modalFormCancel?.addEventListener('click', closeFormModal);
  modalDetailClose?.addEventListener('click', closeDetailModal);
  modalDetailCloseBtn?.addEventListener('click', closeDetailModal);
  modalConfirmDeleteClose?.addEventListener('click', closeConfirmDeleteModal);
  modalConfirmDeleteCancel?.addEventListener('click', closeConfirmDeleteModal);
  
  modalForm?.addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeFormModal();
  });
  modalDetail?.addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeDetailModal();
  });
  modalConfirmDelete?.addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeConfirmDeleteModal();
  });
  
  // Submit del formulario (POST / PUT via API)
  form?.addEventListener('submit', saveProfessional);
  scheduleForm?.addEventListener('submit', saveSchedule);
  safeGetElement('btnApplyDefaultSchedule')?.addEventListener('click', applyDefaultSchedule);
  absenceForm?.addEventListener('submit', saveAbsence);
  safeGetElement('absenceCancelEdit')?.addEventListener('click', resetAbsenceForm);

  // Delegación de eventos para botones de la tabla SSR y renderTableFromApi.
  // WHY: los botones de la tabla SSR existen al cargar la página; los de renderTableFromApi
  //      se crean dinámicamente. La delegación en tbody captura ambos casos sin re-enlazar.
  const tbody = safeGetElement('professionalsTbody');
  tbody?.addEventListener('click', (e) => {
    const btn = e.target.closest('.btn-delete[data-id]');
    if (btn) {
      const id = btn.getAttribute('data-id');
      const name = btn.getAttribute('data-name') || 'este profesional';
      const estado = btn.getAttribute('data-estado') || 'activo';
      if (id) openConfirmToggleEstadoModal(id, name, estado);
    }
  });

  // Delegación para botones Ver de la tabla SSR
  tbody?.addEventListener('click', (e) => {
    const btn = e.target.closest('.btn-view[data-id]');
    if (btn) {
      const avatar = safeGetElement('detailAvatar');
      const nameEl = safeGetElement('detailName');
      const specialtyEl = safeGetElement('detailSpecialty');
      const registryEl = safeGetElement('detailRegistry');
      const phoneEl = safeGetElement('detailPhone');
      const statusEl = safeGetElement('detailStatus');

            if (avatar) {
              avatar.textContent = btn.dataset.initials || '--';
              // Restaurar el color del avatar desde data-avatar-color si fue generado por la API.
              // Para filas SSR el dataset puede no tener el atributo; en ese caso usar
              // el estilo inline que ya trae el avatar del HTML renderizado por Razor.
              if (btn.dataset.avatarColor) {
                avatar.style.background = btn.dataset.avatarColor;
              }
            }
      if (nameEl) nameEl.textContent = btn.dataset.name || '--';
      if (specialtyEl) specialtyEl.textContent = btn.dataset.specialty || '--';
      if (registryEl) registryEl.textContent = btn.dataset.registry || '--';
      if (phoneEl) phoneEl.textContent = btn.dataset.phone || '--';
      if (statusEl) {
        statusEl.textContent = btn.dataset.status || '--';
        statusEl.className = `badge-status badge-${(btn.dataset.status || '').toLowerCase()}`;
      }

      const modal = safeGetElement('modalDetail');
      if (modal) {
        modal.classList.add('open');
        modal.setAttribute('aria-hidden', 'false');
        modal.removeAttribute('inert');
        document.body.style.overflow = 'hidden';
        detailProfessionalId = Number(btn.dataset.id);
        resetModalTabs();
        resetAbsenceForm();
        loadProfessionalSchedule(detailProfessionalId);
        loadProfessionalAbsences(detailProfessionalId);
        safeGetElement('modalDetailClose')?.focus();
      }
    }
  });

  // Botón de confirmación del toggle de estado (Desactivar/Reactivar)
  // Activo -> DELETE /api/profesionales/{id} (baja lógica)
  // Inactivo/Vacaciones -> PATCH /api/profesionales/{id}/estado {estado:"activo"}
  const btnConfirmDelete = safeGetElement('modalConfirmDeleteConfirm');
  btnConfirmDelete?.addEventListener('click', async () => {
    const deleteIdInput = safeGetElement('deleteProfesionalId');
    const estadoInput = safeGetElement('deleteProfesionalEstado');
    const id = Number(deleteIdInput?.value);
    if (!id) return;

    const estadoActual = (estadoInput?.value || 'activo').toLowerCase();
    const esActivo = estadoActual === 'activo';

    const confirmBtn = btnConfirmDelete;
    confirmBtn.disabled = true;
    confirmBtn.textContent = confirmBtn.dataset.loadingText || (esActivo ? '⏳ Desactivando...' : '⏳ Reactivando...');

    try {
      let result;
      if (esActivo) {
        result = await apiRequest(`${API_BASE}/${id}`, { method: 'DELETE' });
        window.ToastService?.success(`✅ ${result.message || 'Profesional desactivado correctamente.'}`);
      } else {
        result = await apiRequest(`${API_BASE}/${id}/estado`, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ estado: 'activo' })
        });
        window.ToastService?.success(`✅ ${result.message || 'Profesional reactivado correctamente.'}`);
      }
      closeConfirmDeleteModal();
      // La tabla inicial se renderiza con Razor y el render API tiene una
      // estructura visual distinta; recargar conserva la paridad SSR.
      window.location.reload();
    } catch (err) {
      window.ToastService?.error(`❌ ${err.message}`);
    } finally {
      confirmBtn.disabled = false;
      confirmBtn.textContent = esActivo ? 'Confirmar desactivación' : 'Confirmar reactivación';
    }
  });
  
  // Soporte para teclado en modales
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (modalForm?.classList.contains('open')) {
        e.preventDefault();
        closeFormModal();
      }
      if (modalDetail?.classList.contains('open')) {
        e.preventDefault();
        closeDetailModal();
      }
      if (modalConfirmDelete?.classList.contains('open')) {
        e.preventDefault();
        closeConfirmDeleteModal();
      }
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

/**
 * Anima los contadores de la sección Stats cuando la tabla viene de SSR.
 * WHY: updateStats() se saltea con SSR (correctamente), pero los data-target
 *      del HTML de Razor contienen los valores reales del servidor. Esta función
 *      los lee y activa la animación 0 → N para que los cards no queden en 0.
 *
 * Flujo:
 *   Razor escribe  <span data-target="42">0</span>
 *   Esta fn lee    data-target = 42
 *   animateCounter anima el span de 0 a 42
 */
const initServerStats = () => {
  const statEls = [
    safeGetElement('profesionales-stat-total') || safeGetElement('metricTotal'),
    safeGetElement('profesionales-stat-activos') || safeGetElement('metricActives'),
    safeGetElement('profesionales-stat-vacaciones') || safeGetElement('metricVacations'),
    safeGetElement('profesionales-stat-inactivos') || safeGetElement('metricInactives'),
  ];

  statEls.forEach(el => {
    if (!el) return;
    const target = parseInt(el.getAttribute('data-target') ?? '0', 10);
    // Solo animar si el target es válido y mayor que 0
    if (!isNaN(target) && target > 0) {
      animateCounter(el, target);
    } else {
      // Si el target es 0, mostrar 0 directamente sin animar
      el.textContent = '0';
    }
  });
};

/**
 * Inicializa todos los componentes al cargar la página.
 */
const init = async () => {
  initSidebar();
  initModals();
  initModalTabs();
  bindProfessionalFieldValidation();
  initProfessionalPassword();

  // Inicializar filtros via API e interceptar el formulario de búsqueda
  initFiltersAPI();

  // Cargar especialidades desde la API para poblar los selects del formulario
  await loadSpecialties();

  // Animar contadores del Stats Grid con los valores que Razor ya escribió en data-target.
  // WHY: la tabla viene renderizada por el servidor (SSR); los contadores ya tienen valores
  //      reales en data-target — solo necesitamos activar la animación visual.
  initServerStats();

  // NO se llama a loadProfessionals() aquí: la tabla SSR que Razor generó es
  // la fuente de verdad inicial. loadProfessionals() se invoca únicamente cuando
  // el usuario usa los filtros de búsqueda o después de operaciones CRUD.
  // WHY: evita doble renderizado (parpadeo SSR→API) y mejora el tiempo de carga.

  // Limpieza al unload para evitar memory leaks en implementaciones SPA
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);
