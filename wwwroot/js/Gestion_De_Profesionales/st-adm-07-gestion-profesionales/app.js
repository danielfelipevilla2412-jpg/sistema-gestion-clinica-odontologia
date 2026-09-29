/* ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: Gestión de Profesionales (st-adm-07-gestion-profesionales)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra la interfaz de gestión de odontólogos y especialistas para el Administrador.
 * Implementa arquitectura híbrida (SSR inicial + operaciones REST asíncronas) para el registro,
 * modificación de especialidades, cambio de estados operacionales y resúmenes estadísticos.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Paginación asíncrona y filtrado reactivo debounced por especialidad y estado.
 * - Validación en cliente de formatos de Registro Médico, Email y Teléfono antes del envío API.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionProfesionalesController / ProfesionalesApiController (/api/profesionales)
 * - HTML: Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml
 * ============================================ */

// ===================================================================
// 1. CONSTANTES Y CONFIGURACIÓN
// ===================================================================

/** Base URL para la API REST de profesionales */
const API_BASE_URL = '/api/profesionales';

/** Tamaño de página estándar para la paginación de la tabla */
const ITEMS_PER_PAGE = 10;

/** Mapeo de especialidades a sufijos de clases CSS de badges */
const SPECIALTY_COLOR_MAP = {
  'Odontología General': 'general',
  'Ortodoncia': 'ortodoncia',
  'Endodoncia': 'endodoncia',
  'Odontopediatría': 'pediatria',
  'Cirugía Oral': 'cirugia',
  'Periodoncia': 'periodoncia',
  'Implantología': 'implante',
  'Rehabilitación Oral': 'rehab',
};

/** Días de la semana para la configuración de horarios */
const WEEK_DAYS = [
  { key: 'Lunes', label: 'Lunes', short: 'Lun' },
  { key: 'Martes', label: 'Martes', short: 'Mar' },
  { key: 'Miercoles', label: 'Miércoles', short: 'Mié' },
  { key: 'Jueves', label: 'Jueves', short: 'Jue' },
  { key: 'Viernes', label: 'Viernes', short: 'Vie' },
  { key: 'Sabado', label: 'Sábado', short: 'Sáb' },
  { key: 'Domingo', label: 'Domingo', short: 'Dom' },
];

// ===================================================================
// 2. GESTIÓN DE ESTADO LOCAL
// ===================================================================

let currentPageIndex = 1;
let editingProfessionalId = null;
let detailProfessionalId = null;
let professionalAbsenceRecords = [];
let professionalScheduleRecords = [];
let lastFocusedElement = null;

// ===================================================================
// 3. UTILIDADES Y FORMATO DE UI
// ===================================================================

/**
 * Obtiene un elemento DOM por ID de forma segura con diagnóstico en consola.
 * @param {string} elementId - ID del elemento DOM.
 * @returns {HTMLElement|null}
 */
const getElementByIdSafe = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

/**
 * Retorna la clase CSS badge asociada a una especialidad.
 * @param {string} specialtyName
 * @returns {string}
 */
const getSpecialtyBadgeClass = (specialtyName) => SPECIALTY_COLOR_MAP[specialtyName] || 'general';

/**
 * Retorna la clase CSS de badge asociada al estado del profesional.
 * @param {string} statusValue
 * @returns {string}
 */
const getStatusBadgeClass = (statusValue) => {
  const statusClassMap = {
    'activo': 'activo', 'Activo': 'activo',
    'vacaciones': 'vacaciones', 'Vacaciones': 'vacaciones',
    'inactivo': 'inactivo', 'Inactivo': 'inactivo',
  };
  return statusClassMap[statusValue] || 'inactivo';
};

/**
 * Obtiene el color de fondo para el avatar del profesional según su especialidad.
 * @param {string} specialtyName
 * @returns {string}
 */
const getAvatarColorBySpecialty = (specialtyName) => {
  const avatarColors = {
    'general':    'var(--spec-general)',
    'ortodoncia': 'var(--spec-ortodoncia)',
    'endodoncia': 'var(--spec-endodoncia)',
    'pediatria':  'var(--spec-pediatria)',
    'cirugia':    'var(--spec-cirugia)',
    'periodoncia':'var(--spec-periodoncia)',
    'implante':   'var(--spec-implante)',
    'rehab':      'var(--spec-rehab)',
  };
  return avatarColors[getSpecialtyBadgeClass(specialtyName)] || 'var(--spec-general)';
};

// ===================================================================
// 4. COMUNICACIÓN CON LA API REST
// ===================================================================

/**
 * Realiza la petición para obtener la lista paginada y filtrada de profesionales.
 * @param {Object} queryParams
 * @returns {Promise<Object>}
 */
const fetchProfessionalsApi = async (queryParams = {}) => {
  const urlParams = new URLSearchParams();
  urlParams.set('page', queryParams.page ?? 1);
  urlParams.set('pageSize', queryParams.pageSize ?? ITEMS_PER_PAGE);

  if (queryParams.search) urlParams.set('search', queryParams.search);
  if (queryParams.especialidad) urlParams.set('especialidad', queryParams.especialidad);
  if (queryParams.estado) urlParams.set('estado', queryParams.estado);

  return await apiRequest(`${API_BASE_URL}?${urlParams.toString()}`);
};

/**
 * Carga los profesionales y actualiza la tabla y la paginación.
 */
const loadProfessionalsList = async () => {
  try {
    const searchKeyword = document.querySelector('#searchInput')?.value?.trim() || '';
    const selectedSpecialty = document.querySelector('#filterSpecialty')?.value || '';
    const selectedStatus = document.querySelector('#filterStatus')?.value || '';

    setTableLoadingState(true);
    const apiResponse = await fetchProfessionalsApi({
      page: currentPageIndex,
      pageSize: ITEMS_PER_PAGE,
      search: searchKeyword,
      especialidad: selectedSpecialty,
      estado: selectedStatus
    });

    renderTableFromApi(apiResponse);
    renderPaginationFromApi(apiResponse);
  } catch (error) {
    const tableBody = getElementByIdSafe('professionalsTbody');
    if (tableBody) {
      tableBody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No fue posible cargar los profesionales. Intenta nuevamente.</td></tr>`;
    }
    window.ToastService?.error?.(`No fue posible cargar los profesionales: ${error.message}`);
  } finally {
    setTableLoadingState(false);
  }
};

/**
 * Carga el catálogo de especialidades desde la API para llenar los selectores.
 */
const loadSpecialtiesCatalog = async () => {
  try {
    const apiResult = await apiRequest(`${API_BASE_URL}/especialidades`);
    const specialtyList = apiResult.data || [];

    const filterSelect = getElementByIdSafe('filterSpecialty');
    const formSelect = document.querySelector('select[name="IdEspecialidad"]') || getElementByIdSafe('formIdEspecialidad');

    if (filterSelect) {
      const currentSelectedValue = filterSelect.value;
      filterSelect.innerHTML = '<option value="">Todas las especialidades</option>' +
        specialtyList.map(item => `<option value="${item.nombre}">${item.nombre}</option>`).join('');
      filterSelect.value = currentSelectedValue;
    }

    if (formSelect) {
      const currentSelectedValue = formSelect.value;
      formSelect.innerHTML = '<option value="" disabled selected>Selecciona una especialidad</option>' +
        specialtyList.map(item => `<option value="${item.idEspecialidad}">${item.nombre}</option>`).join('');
      if (currentSelectedValue) formSelect.value = currentSelectedValue;
    }
  } catch (error) {
    console.error('Error al cargar especialidades', error);
  }
};

/**
 * Carga los horarios de atención de un profesional específico.
 * @param {number} professionalId
 */
const loadProfessionalSchedule = async (professionalId) => {
  detailProfessionalId = Number(professionalId);
  const loadingIndicator = getElementByIdSafe('scheduleLoading');
  const statusMessageLabel = getElementByIdSafe('scheduleStatusMsg');
  if (statusMessageLabel) statusMessageLabel.textContent = '';
  if (loadingIndicator) loadingIndicator.textContent = 'Cargando horarios de atención...';

  try {
    const apiResult = await apiRequest(`${API_BASE_URL}/${detailProfessionalId}/horarios`);
    professionalScheduleRecords = apiResult?.data || [];
    renderScheduleDays();
  } catch (error) {
    professionalScheduleRecords = [];
    renderScheduleDays();
    if (statusMessageLabel) {
      statusMessageLabel.style.color = 'var(--text-muted)';
      statusMessageLabel.textContent = 'ℹ️ Sin horario personalizado (aplica horario general de la clínica).';
    }
  } finally {
    if (loadingIndicator) loadingIndicator.textContent = '';
  }
};

/**
 * Carga el historial de ausencias de un profesional.
 * @param {number} professionalId
 */
const loadProfessionalAbsences = async (professionalId) => {
  detailProfessionalId = Number(professionalId);
  const loadingIndicator = getElementByIdSafe('absenceLoading');
  if (loadingIndicator) loadingIndicator.textContent = 'Cargando ausencias...';
  try {
    const apiResult = await apiRequest(`${API_BASE_URL}/${detailProfessionalId}/ausencias`);
    professionalAbsenceRecords = apiResult?.data || [];
    renderAbsences();
  } catch (error) {
    professionalAbsenceRecords = [];
    const absenceListContainer = getElementByIdSafe('absenceList');
    if (absenceListContainer) absenceListContainer.textContent = `No se pudieron cargar las ausencias: ${error.message}`;
  } finally {
    if (loadingIndicator) loadingIndicator.textContent = '';
  }
};

// ===================================================================
// 5. MANIPULACIÓN DEL DOM Y RENDERIZADO DE COMPONENTES
// ===================================================================

/**
 * Crea el elemento tr para la tabla de profesionales.
 * @param {Object} professionalRecord
 * @returns {HTMLTableRowElement}
 */
const createProfessionalTableRow = (professionalRecord) => {
  const escapeHtmlUtil = window.SmileTrack?.utils?.escapeHtml || window.escapeHtml || ((str) => str);
  const tableRow = document.createElement('tr');
  tableRow.setAttribute('role', 'row');

  const fullName = `${escapeHtmlUtil(professionalRecord.nombres)} ${escapeHtmlUtil(professionalRecord.apellidos)}`.trim();
  const primarySpecialty = professionalRecord.especialidades && professionalRecord.especialidades.length > 0
    ? escapeHtmlUtil(professionalRecord.especialidades[0].nombre)
    : '';

  const specialtyBadgeClass = getSpecialtyBadgeClass(primarySpecialty);
  const statusBadgeClass = getStatusBadgeClass(professionalRecord.estado);
  const avatarBgColor = getAvatarColorBySpecialty(primarySpecialty);

  const initialFirst = professionalRecord.nombres ? professionalRecord.nombres.charAt(0).toUpperCase() : '';
  const initialLast = professionalRecord.apellidos ? professionalRecord.apellidos.charAt(0).toUpperCase() : '';
  const initialsText = `${initialFirst}${initialLast}`;

  const phoneText = escapeHtmlUtil(professionalRecord.telefono);
  const statusText = escapeHtmlUtil(professionalRecord.estado);
  const medicalLicense = escapeHtmlUtil(professionalRecord.registroMedico);

  const isProfessionalActive = (statusText || '').toLowerCase() === 'activo';

  tableRow.innerHTML = `
    <td class="td-profesional">
      <div class="p-avatar" style="background:${avatarBgColor}" aria-hidden="true">${initialsText}</div>
      <span class="p-name">${fullName}</span>
    </td>
    <td><span class="badge-spec ${specialtyBadgeClass}">${primarySpecialty || '—'}</span></td>
    <td>${medicalLicense}</td>
    <td>${phoneText || '—'}</td>
    <td><span class="badge-status ${statusBadgeClass}" role="status" aria-label="Estado: ${statusText}">${statusText}</span></td>
    <td>
      <div class="actions-cell">
        <button class="btn-secondary btn-view view"
                type="button"
                data-id="${professionalRecord.idProfesional}"
                data-name="${fullName}"
                data-initials="${initialsText}"
                data-specialty="${primarySpecialty}"
                data-registry="${medicalLicense}"
                data-phone="${phoneText}"
                data-status="${statusText}"
                data-avatar-color="${avatarBgColor}"
                data-status-class="${statusBadgeClass}"
                aria-label="Ver detalles del profesional ${fullName}"
                title="Ver detalles del profesional ${fullName}">
          <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span> <span class="btn-text">Ver</span>
        </button>
        <button class="btn-secondary edit"
                type="button"
                aria-label="Editar el profesional ${fullName}"
                title="Editar profesional ${fullName}"
                onclick="editProfessional(${professionalRecord.idProfesional})">
          <span class="material-symbols-outlined action-icon" aria-hidden="true">edit</span> <span class="btn-text">Editar</span>
        </button>
        <button class="btn-danger btn-delete toggle"
                type="button"
                data-id="${professionalRecord.idProfesional}"
                data-name="${fullName}"
                data-estado="${statusText || 'inactivo'}"
                aria-label="${isProfessionalActive ? 'Desactivar' : 'Reactivar'} el profesional ${fullName}"
                title="${isProfessionalActive ? 'Desactivar' : 'Reactivar'} profesional ${fullName}">
          <span class="material-symbols-outlined action-icon" aria-hidden="true">${isProfessionalActive ? 'block' : 'check_circle'}</span> <span class="btn-text">${isProfessionalActive ? 'Desactivar' : 'Reactivar'}</span>
        </button>
      </div>
    </td>
  `;

  // Listener para el botón Ver Detalle
  const viewButton = tableRow.querySelector('.btn-view');
  viewButton?.addEventListener('click', () => {
    openProfessionalDetailView(viewButton, avatarBgColor, statusBadgeClass);
  });

  // Listener para el botón Desactivar / Reactivar
  const deleteButton = tableRow.querySelector('.btn-delete');
  deleteButton?.addEventListener('click', () => {
    openConfirmToggleEstadoModal(professionalRecord.idProfesional, fullName, deleteButton.dataset.estado || 'activo');
  });

  return tableRow;
};

/**
 * Despliega la información del profesional en el modal de detalle.
 * @param {HTMLButtonElement} viewButton
 * @param {string} defaultAvatarBg
 * @param {string} defaultStatusClass
 */
const openProfessionalDetailView = (viewButton, defaultAvatarBg, defaultStatusClass) => {
  const avatarEl = getElementByIdSafe('detailAvatar');
  const nameEl = getElementByIdSafe('detailName');
  const specialtyEl = getElementByIdSafe('detailSpecialty');
  const registryEl = getElementByIdSafe('detailRegistry');
  const phoneEl = getElementByIdSafe('detailPhone');
  const statusEl = getElementByIdSafe('detailStatus');

  const avatarBgColor = viewButton.dataset.avatarColor || defaultAvatarBg;
  const statusBadgeClass = viewButton.dataset.statusClass || defaultStatusClass;

  if (avatarEl) { avatarEl.textContent = viewButton.dataset.initials || '--'; avatarEl.style.background = avatarBgColor; }
  if (nameEl) nameEl.textContent = viewButton.dataset.name || '--';
  if (specialtyEl) specialtyEl.textContent = viewButton.dataset.specialty || '--';
  if (registryEl) registryEl.textContent = viewButton.dataset.registry || '--';
  if (phoneEl) phoneEl.textContent = viewButton.dataset.phone || '--';
  if (statusEl) {
    statusEl.textContent = viewButton.dataset.status || '--';
    statusEl.className = `badge-status ${statusBadgeClass}`;
  }

  const modalElement = getElementByIdSafe('modalDetail');
  if (modalElement) {
    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    detailProfessionalId = Number(viewButton.dataset.id);
    resetAbsenceForm();
    loadProfessionalAbsences(detailProfessionalId);
    getElementByIdSafe('modalDetailClose')?.focus();
  }
};

/**
 * Renderiza el listado de profesionales recibido de la API en el tbody.
 * @param {Object} apiResult
 */
function renderTableFromApi(apiResult) {
  const tableBody = getElementByIdSafe('professionalsTbody');
  if (!tableBody) return;

  tableBody.innerHTML = '';
  const professionalList = apiResult?.data || [];

  if (professionalList.length === 0) {
    tableBody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No se encontraron profesionales con los filtros aplicados.</td></tr>`;
    return;
  }

  const fragment = document.createDocumentFragment();
  for (const professionalItem of professionalList) {
    const tableRowElement = createProfessionalTableRow(professionalItem);
    fragment.appendChild(tableRowElement);
  }
  tableBody.appendChild(fragment);
}

/**
 * Configura la indicación visual de carga en la tabla de profesionales.
 * @param {boolean} isLoading
 */
const setTableLoadingState = (isLoading) => {
  const tableElement = getElementByIdSafe('professionalsTable');
  if (tableElement) tableElement.setAttribute('aria-busy', String(isLoading));
  const paginationButtonsContainer = getElementByIdSafe('paginationButtons');
  if (paginationButtonsContainer) {
    paginationButtonsContainer.querySelectorAll('button').forEach(button => { button.disabled = isLoading; });
  }
};

/**
 * Renderiza los controles de la grilla de paginación desde la respuesta API.
 * @param {Object} apiResult
 */
const renderPaginationFromApi = (apiResult) => {
  const infoLabel = getElementByIdSafe('paginationInfo');
  const buttonsContainer = getElementByIdSafe('paginationButtons');
  if (!infoLabel || !buttonsContainer || !apiResult.pagination) return;

  const { page, pageSize, totalCount, totalPages } = apiResult.pagination;
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const rangeEnd = Math.min(page * pageSize, totalCount);

  infoLabel.textContent = `Mostrando ${rangeStart}-${rangeEnd} de ${totalCount} profesionales`;
  buttonsContainer.innerHTML = '';

  // Botón página anterior
  const previousButton = document.createElement('button');
  previousButton.textContent = '«';
  previousButton.setAttribute('aria-label', 'Página anterior');
  previousButton.disabled = page === 1 || totalCount === 0;
  previousButton.addEventListener('click', () => { if (page > 1) window.goToPage(page - 1); });
  buttonsContainer.appendChild(previousButton);

  // Botones numéricos
  for (let pageNumber = 1; pageNumber <= totalPages; pageNumber++) {
    const numberButton = document.createElement('button');
    numberButton.textContent = pageNumber;
    numberButton.setAttribute('aria-label', `Ir a página ${pageNumber}`);
    numberButton.setAttribute('aria-current', pageNumber === page ? 'page' : 'false');
    if (pageNumber === page) numberButton.classList.add('active');
    numberButton.addEventListener('click', () => window.goToPage(pageNumber));
    buttonsContainer.appendChild(numberButton);
  }

  // Botón página siguiente
  const nextButton = document.createElement('button');
  nextButton.textContent = '»';
  nextButton.setAttribute('aria-label', 'Página siguiente');
  nextButton.disabled = page >= totalPages || totalCount === 0;
  nextButton.addEventListener('click', () => { if (page < totalPages) window.goToPage(page + 1); });
  buttonsContainer.appendChild(nextButton);
};

/**
 * Renderiza las filas para la configuración de horarios semanales.
 */
const renderScheduleDays = () => {
  const container = getElementByIdSafe('scheduleDaysContainer');
  if (!container) return;
  container.innerHTML = '';

  WEEK_DAYS.forEach((dayConfig) => {
    const existingSchedule = professionalScheduleRecords.find(
      (s) => (s.diaSemana || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '') ===
             dayConfig.key.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    );

    const isDayActive = existingSchedule ? existingSchedule.activo : (dayConfig.key !== 'Sabado' && dayConfig.key !== 'Domingo');
    const startTimeValue = existingSchedule?.horaInicio ? existingSchedule.horaInicio.substring(0, 5) : '08:00';
    const endTimeValue = existingSchedule?.horaFin ? existingSchedule.horaFin.substring(0, 5) : '17:00';

    const dayRowElement = document.createElement('div');
    dayRowElement.className = `schedule-row ${isDayActive ? '' : 'inactive'}`;
    dayRowElement.dataset.day = dayConfig.key;

    dayRowElement.innerHTML = `
      <div class="schedule-row-day">${dayConfig.label}</div>
      <label class="schedule-toggle">
        <input type="checkbox" class="schedule-day-active" ${isDayActive ? 'checked' : ''} data-day="${dayConfig.key}">
        <span>${isDayActive ? 'Atiende' : 'No atiende'}</span>
      </label>
      <div class="schedule-time-group">
        <span class="schedule-time-label">Inicio:</span>
        <input type="time" class="form-input schedule-time-start" value="${startTimeValue}" ${isDayActive ? '' : 'disabled'} style="padding:4px 8px;font-size:0.85rem;" required>
      </div>
      <div class="schedule-time-group">
        <span class="schedule-time-label">Fin:</span>
        <input type="time" class="form-input schedule-time-end" value="${endTimeValue}" ${isDayActive ? '' : 'disabled'} style="padding:4px 8px;font-size:0.85rem;" required>
      </div>
    `;

    const activeCheckbox = dayRowElement.querySelector('.schedule-day-active');
    const labelSpanText = dayRowElement.querySelector('.schedule-toggle span');
    const startInput = dayRowElement.querySelector('.schedule-time-start');
    const endInput = dayRowElement.querySelector('.schedule-time-end');

    activeCheckbox.addEventListener('change', (e) => {
      const isChecked = e.target.checked;
      dayRowElement.classList.toggle('inactive', !isChecked);
      labelSpanText.textContent = isChecked ? 'Atiende' : 'No atiende';
      startInput.disabled = !isChecked;
      endInput.disabled = !isChecked;
    });

    container.appendChild(dayRowElement);
  });
};

/**
 * Renderiza el listado de ausencias en el tab del modal.
 */
const renderAbsences = () => {
  const absenceListContainer = getElementByIdSafe('absenceList');
  if (!absenceListContainer) return;
  absenceListContainer.replaceChildren();

  if (professionalAbsenceRecords.length === 0) {
    absenceListContainer.textContent = 'No hay ausencias registradas.';
    return;
  }

  const escapeHtmlUtil = window.SmileTrack?.utils?.escapeHtml || window.escapeHtml || ((str) => str);

  professionalAbsenceRecords.forEach((absenceItem) => {
    const absenceItemCard = document.createElement('div');
    absenceItemCard.style.cssText = 'display:flex;justify-content:space-between;align-items:center;gap:8px;border:1px solid var(--border-color,#e5e7eb);padding:10px;border-radius:6px;';
    
    const detailsContainer = document.createElement('div');
    detailsContainer.innerHTML = `<strong>${escapeHtmlUtil(absenceItem.tipo || 'Ausencia')}</strong><br><small>${escapeHtmlUtil(absenceItem.fechaInicio)} a ${escapeHtmlUtil(absenceItem.fechaFin)}</small>${absenceItem.observaciones ? `<br><small>${escapeHtmlUtil(absenceItem.observaciones)}</small>` : ''}`;
    
    const actionButtonsContainer = document.createElement('div');
    const editAbsenceButton = document.createElement('button');
    editAbsenceButton.type = 'button';
    editAbsenceButton.className = 'btn-secondary';
    editAbsenceButton.textContent = 'Editar';
    editAbsenceButton.addEventListener('click', () => {
      getElementByIdSafe('absenceId').value = absenceItem.idAusencia;
      getElementByIdSafe('absenceTipo').value = absenceItem.tipo || 'otro';
      getElementByIdSafe('absenceFechaInicio').value = absenceItem.fechaInicio;
      getElementByIdSafe('absenceFechaFin').value = absenceItem.fechaFin;
      getElementByIdSafe('absenceObservaciones').value = absenceItem.observaciones || '';
      getElementByIdSafe('absenceSaveButton').textContent = 'Guardar ausencia';
      getElementByIdSafe('absenceCancelEdit').hidden = false;
    });

    const deleteAbsenceButton = document.createElement('button');
    deleteAbsenceButton.type = 'button';
    deleteAbsenceButton.className = 'btn-secondary';
    deleteAbsenceButton.textContent = 'Eliminar';
    deleteAbsenceButton.addEventListener('click', async () => {
      window.ModalService?.confirm({
        title: '¿Eliminar ausencia?',
        message: 'Esta acción eliminará permanentemente el registro de ausencia del profesional y no podrá deshacerse.',
        confirmText: 'Sí, eliminar',
        cancelText: 'Cancelar',
        isDanger: true,
        onConfirm: async () => {
          try {
            const apiResult = await apiRequest(`${API_BASE_URL}/${detailProfessionalId}/ausencias/${absenceItem.idAusencia}`, { method: 'DELETE' });
            window.ToastService?.success(apiResult.message || 'Ausencia eliminada.');
            await loadProfessionalAbsences(detailProfessionalId);
          } catch (error) {
            window.ToastService?.error(`No se pudo eliminar la ausencia: ${error.message}`);
          }
        }
      });
    });

    actionButtonsContainer.append(editAbsenceButton, deleteAbsenceButton);
    absenceItemCard.append(detailsContainer, actionButtonsContainer);
    absenceListContainer.appendChild(absenceItemCard);
  });
};

/**
 * Anima los contadores numéricos cuando la vista viene renderizada desde el servidor (SSR).
 */
const initServerStats = () => {
  const statElements = [
    getElementByIdSafe('profesionales-stat-total') || getElementByIdSafe('metricTotal'),
    getElementByIdSafe('profesionales-stat-activos') || getElementByIdSafe('metricActives'),
    getElementByIdSafe('profesionales-stat-vacaciones') || getElementByIdSafe('metricVacations'),
    getElementByIdSafe('profesionales-stat-inactivos') || getElementByIdSafe('metricInactives'),
  ];

  statElements.forEach(targetEl => {
    if (!targetEl) return;
    const targetValue = parseInt(targetEl.getAttribute('data-target') ?? '0', 10);
    if (!isNaN(targetValue) && targetValue > 0) {
      animateCounter(targetEl, targetValue);
    } else {
      targetEl.textContent = '0';
    }
  });
};

// ===================================================================
// 6. GESTIÓN DE MODALES Y FORMULARIOS
// ===================================================================

/**
 * Resetea el formulario de ausencias a su estado por defecto.
 */
const resetAbsenceForm = () => {
  getElementByIdSafe('absenceForm')?.reset();
  const absenceIdInput = getElementByIdSafe('absenceId');
  if (absenceIdInput) absenceIdInput.value = '';
  const saveBtn = getElementByIdSafe('absenceSaveButton');
  if (saveBtn) saveBtn.textContent = 'Registrar ausencia';
  const cancelBtn = getElementByIdSafe('absenceCancelEdit');
  if (cancelBtn) cancelBtn.hidden = true;
};

/**
 * Inicializa las pestañas internas del modal de detalle.
 */
const initModalTabs = () => {
  const tabConfigs = [
    { btn: 'tabBtnGeneral', pane: 'tabPaneGeneral' },
    { btn: 'tabBtnHorarios', pane: 'tabPaneHorarios' },
    { btn: 'tabBtnAusencias', pane: 'tabPaneAusencias' },
  ];

  tabConfigs.forEach(tabItem => {
    const buttonElement = getElementByIdSafe(tabItem.btn);
    buttonElement?.addEventListener('click', () => {
      tabConfigs.forEach(otherTab => {
        getElementByIdSafe(otherTab.btn)?.classList.remove('active');
        getElementByIdSafe(otherTab.btn)?.setAttribute('aria-selected', 'false');
        getElementByIdSafe(otherTab.pane)?.classList.remove('active');
      });
      buttonElement.classList.add('active');
      buttonElement.setAttribute('aria-selected', 'true');
      getElementByIdSafe(tabItem.pane)?.classList.add('active');
    });
  });
};

/**
 * Restablece la pestaña activa del modal de detalle a 'General'.
 */
const resetModalTabs = () => {
  getElementByIdSafe('tabBtnGeneral')?.click();
};

/**
 * Abre el modal de formulario para crear o editar profesional.
 * @param {boolean} [isEditing=false]
 */
const openFormModal = (isEditing = false) => {
  lastFocusedElement = document.activeElement;

  if (!isEditing) {
    editingProfessionalId = null;
    const formElement = getElementByIdSafe('formProfessional');
    if (formElement) formElement.reset();

    const modalTitle = getElementByIdSafe('modalFormTitle');
    if (modalTitle) modalTitle.textContent = 'Nuevo Profesional';
  }

  const modalElement = getElementByIdSafe('modalForm');
  if (modalElement) {
    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    const firstInput = modalElement.querySelector('input:not([type="hidden"])');
    if (firstInput) firstInput.focus();
    document.body.style.overflow = 'hidden';
  }
};

/**
 * Cierra el modal de formulario y retorna el foco.
 */
const closeFormModal = () => {
  const modalElement = getElementByIdSafe('modalForm');
  if (modalElement) {
    modalElement.classList.remove('open');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  editingProfessionalId = null;
  if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
    lastFocusedElement.focus();
  }
  lastFocusedElement = null;
};

/**
 * Cierra el modal de detalle y retorna el foco.
 */
const closeDetailModal = () => {
  const modalElement = getElementByIdSafe('modalDetail');
  if (modalElement) {
    modalElement.classList.remove('open');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
    lastFocusedElement.focus();
  }
  lastFocusedElement = null;
};

/**
 * Despliega el modal de confirmación para cambiar el estado (desactivar/reactivar).
 * @param {number} professionalId
 * @param {string} fullName
 * @param {string} currentStatus
 */
const openConfirmToggleEstadoModal = (professionalId, fullName, currentStatus) => {
  lastFocusedElement = document.activeElement;
  const modalElement = getElementByIdSafe('modalConfirmDelete');
  const titleLabel = getElementByIdSafe('modalConfirmDeleteTitle');
  const messageLabel = getElementByIdSafe('modalConfirmDeleteMessage');
  const warningContainer = document.getElementById('modalConfirmDeleteWarning') || null;
  const hiddenIdInput = getElementByIdSafe('deleteProfesionalId');
  const hiddenEstadoInput = getElementByIdSafe('deleteProfesionalEstado');
  const confirmButton = getElementByIdSafe('modalConfirmDeleteConfirm');
  const cancelButton = getElementByIdSafe('modalConfirmDeleteCancel');

  const isActiveStatus = (currentStatus || 'activo').toLowerCase() === 'activo';

  if (titleLabel) {
    titleLabel.textContent = isActiveStatus ? 'Desactivar profesional' : 'Reactivar profesional';
  }
  if (messageLabel) {
    messageLabel.textContent = isActiveStatus
      ? `¿Estás seguro de desactivar a ${fullName}? El profesional quedará inactivo y no podrá recibir nuevas citas.`
      : `¿Estás seguro de reactivar a ${fullName}? El profesional pasará a estado activo y volverá a recibir citas.`;
  }
  if (warningContainer) {
    warningContainer.innerHTML = isActiveStatus
      ? '⚠️ El profesional pasará a estado <strong>inactivo</strong> y no podrá recibir nuevas citas. Esta operación es reversible: puedes reactivarlo desde la misma columna acciones. No es posible desactivar profesionales con citas activas pendientes.'
      : '✅ El profesional volverá a estado <strong>activo</strong> y estará disponible para agendar nuevas citas. Esta operación es reversible: puedes desactivarlo desde la misma columna acciones.';
  }
  if (cancelButton) {
    cancelButton.textContent = isActiveStatus ? 'Cancelar — no desactivar' : 'Cancelar — no reactivar';
  }
  if (confirmButton) {
    confirmButton.textContent = isActiveStatus ? 'Confirmar desactivación' : 'Confirmar reactivación';
    confirmButton.style.backgroundColor = isActiveStatus ? 'var(--red)' : 'var(--primary)';
    confirmButton.style.borderColor = isActiveStatus ? 'var(--red)' : 'var(--primary)';
    confirmButton.dataset.loadingText = isActiveStatus ? '⏳ Desactivando...' : '⏳ Reactivando...';
  }
  if (hiddenIdInput) hiddenIdInput.value = professionalId;
  if (hiddenEstadoInput) hiddenEstadoInput.value = currentStatus || 'activo';

  if (modalElement) {
    modalElement.classList.add('open');
    modalElement.setAttribute('aria-hidden', 'false');
    modalElement.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    setTimeout(() => {
      getElementByIdSafe('modalConfirmDeleteCancel')?.focus();
    }, 50);
  }
};

/**
 * Cierra el modal de confirmación de cambio de estado.
 */
const closeConfirmDeleteModal = () => {
  const modalElement = getElementByIdSafe('modalConfirmDelete');
  if (modalElement) {
    modalElement.classList.remove('open');
    modalElement.setAttribute('aria-hidden', 'true');
    modalElement.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
    lastFocusedElement.focus();
  }
  lastFocusedElement = null;
};

/**
 * Carga los datos de un profesional desde la API y llena el formulario de edición.
 * @param {number} professionalId
 */
window.editProfessional = async (professionalId) => {
  try {
    const apiResult = await apiRequest(`${API_BASE_URL}/${professionalId}`);
    const professionalData = apiResult.data;
    if (!professionalData) return;

    editingProfessionalId = professionalId;

    const setFieldValue = (fieldId, val) => {
      const fieldElement = getElementByIdSafe(fieldId);
      if (fieldElement) fieldElement.value = val ?? '';
    };

    setFieldValue('formIdProfesional', professionalData.idProfesional);
    setFieldValue('formNombres', professionalData.nombres);
    setFieldValue('formApellidos', professionalData.apellidos);
    setFieldValue('formRegistroMedico', professionalData.registroMedico);
    setFieldValue('formCategoria', professionalData.categoria);
    setFieldValue('formTelefono', professionalData.telefono);
    setFieldValue('formCorreoAcceso', professionalData.correoAcceso);

    const normalizedStatus = (professionalData.estado || 'activo').toLowerCase();
    const statusSelectElement = getElementByIdSafe('formStatus');
    if (statusSelectElement) {
      statusSelectElement.value = normalizedStatus;
      statusSelectElement.dataset.originalEstado = normalizedStatus;
    }
    setFieldValue('formEstado', normalizedStatus);
    setFieldValue('formContrasenaAcceso', '');
    updateProfessionalPasswordRules();

    const formSelect = document.querySelector('select[name="IdEspecialidad"]') || getElementByIdSafe('formIdEspecialidad');
    if (formSelect && professionalData.especialidades && professionalData.especialidades.length > 0) {
      formSelect.value = professionalData.especialidades[0].idEspecialidad;
    } else if (formSelect) {
      formSelect.value = '';
    }

    const modalTitle = getElementByIdSafe('modalFormTitle');
    if (modalTitle) modalTitle.textContent = 'Editar Profesional';

    openFormModal(true);
  } catch (error) {
    window.ToastService?.error(`❌ No se pudo cargar el profesional: ${error.message}`);
  }
};

/**
 * Valida los campos del formulario de profesional.
 * @param {HTMLFormElement} formElement
 * @returns {boolean}
 */
const validateProfessionalForm = (formElement) => {
  let isFormValid = true;

  const requiredFields = [
    { id: 'formNombres', message: 'Ingresa los nombres.' },
    { id: 'formApellidos', message: 'Ingresa los apellidos.' },
    { id: 'formRegistroMedico', message: 'Ingresa el registro médico.' },
    { id: 'formCorreoAcceso', message: 'Ingresa un correo de acceso válido.' }
  ];

  if (window.ValidationUtils) {
    requiredFields.forEach(({ id }) => {
      const fieldElement = getElementByIdSafe(id);
      if (fieldElement) window.ValidationUtils.clearError(fieldElement);
    });
  }

  requiredFields.forEach(({ id, message }) => {
    const fieldElement = getElementByIdSafe(id);
    const fieldValue = fieldElement?.value.trim() || '';
    const isFieldValid = Boolean(fieldValue);

    if (!isFieldValid) {
      isFormValid = false;
      if (window.ValidationUtils && fieldElement) {
        window.ValidationUtils.showError(fieldElement, null, message);
      }
    }
  });

  const emailField = getElementByIdSafe('formCorreoAcceso');
  if (emailField && emailField.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailField.value.trim())) {
    isFormValid = false;
    if (window.ValidationUtils) {
      window.ValidationUtils.showError(emailField, null, 'Ingresa un correo de acceso válido.');
    }
  }

  return isFormValid;
};

/**
 * Actualiza los indicadores visuales de reglas de contraseña.
 */
const updateProfessionalPasswordRules = () => {
  const passwordInput = getElementByIdSafe('formContrasenaAcceso');
  if (!passwordInput) return;

  const inputValue = passwordInput.value || '';
  const passwordRules = {
    length: inputValue.length >= 8,
    upper: /[A-Z]/.test(inputValue),
    lower: /[a-z]/.test(inputValue),
    number: /\d/.test(inputValue),
    symbol: /[^A-Za-z\d]/.test(inputValue)
  };

  Object.entries(passwordRules).forEach(([ruleKey, isRuleSatisfied]) => {
    const ruleRow = document.querySelector(`#professionalPasswordRules [data-rule="${ruleKey}"]`);
    if (!ruleRow) return;
    const iconSpan = ruleRow.querySelector('.rule-icon');
    if (iconSpan) iconSpan.textContent = isRuleSatisfied ? '✓' : '✗';
    ruleRow.style.color = isRuleSatisfied ? '#15803d' : '#b91c1c';
  });

  const hiddenFormId = getElementByIdSafe('formIdProfesional');
  const isEditingMode = Number(hiddenFormId?.value || 0) > 0;
  const helpTextLabel = getElementByIdSafe('formContrasenaHelp');

  if (helpTextLabel) {
    if (isEditingMode && inputValue.length === 0) {
      helpTextLabel.textContent = 'La contraseña no se modifica desde esta pantalla.';
      helpTextLabel.style.color = '#6b7280';
    } else {
      helpTextLabel.textContent = 'La contraseña se genera automáticamente en el sistema.';
      helpTextLabel.style.color = '#6b7280';
    }
  }
};

/**
 * Intercepta el submit del formulario y lo envía a la API (POST o PUT).
 * @param {Event} event
 */
const saveProfessional = async (event) => {
  event.preventDefault();

  const formElement = event.currentTarget;
  const isValid = validateProfessionalForm(formElement);
  if (!isValid) {
    window.ToastService?.warning('⚠️ Completa los campos obligatorios marcados en rojo.');
    const firstInvalidField = formElement.querySelector('[aria-invalid="true"]');
    if (firstInvalidField) firstInvalidField.focus();
    return;
  }

  const submitButton = formElement.querySelector('[type="submit"]');
  if (submitButton) { submitButton.disabled = true; submitButton.textContent = '⏳ Guardando...'; }

  const getFieldValue = (fieldId) => getElementByIdSafe(fieldId)?.value?.trim() ?? '';
  const professionalId = Number(getFieldValue('formIdProfesional'));
  const isEditingMode = professionalId > 0;

  const specialtySelectElement = formElement.querySelector('select[name="IdEspecialidad"]') || getElementByIdSafe('formIdEspecialidad');
  const selectedSpecialtyId = specialtySelectElement ? Number(specialtySelectElement.value) || null : null;

  const requestPayload = {
    nombres:        getFieldValue('formNombres'),
    apellidos:      getFieldValue('formApellidos'),
    registroMedico: getFieldValue('formRegistroMedico'),
    categoria:      getFieldValue('formCategoria') || null,
    telefono:       getFieldValue('formTelefono')  || null,
    correoAcceso:   getFieldValue('formCorreoAcceso'),
    idEspecialidad: selectedSpecialtyId,
    estado:         isEditingMode ? (getElementByIdSafe('formStatus')?.value || getElementByIdSafe('formEstado')?.value || '').trim().toLowerCase() || null : null,
  };

  if (!isEditingMode) {
    delete requestPayload.contrasenaAcceso;
  }

  try {
    let apiResult;
    if (isEditingMode) {
      apiResult = await apiRequest(`${API_BASE_URL}/${professionalId}`, {
        method: 'PUT',
        body: JSON.stringify(requestPayload)
      });
    } else {
      apiResult = await apiRequest(API_BASE_URL, {
        method: 'POST',
        body: JSON.stringify(requestPayload)
      });
    }

    if (!apiResult || apiResult.success === false) {
      throw new Error(apiResult?.message || 'No fue posible guardar el profesional.');
    }

    window.ToastService?.success(`✅ ${apiResult.message || 'Profesional guardado correctamente.'}`);
    closeFormModal();
    currentPageIndex = 1;
    await loadProfessionalsList();
  } catch (error) {
    window.ToastService?.error(`❌ ${error.message}`);
  } finally {
    if (submitButton) { submitButton.disabled = false; submitButton.textContent = '💾 Guardar'; }
  }
};

/**
 * Carga el horario por defecto en la interfaz.
 */
const applyDefaultSchedule = () => {
  const dayRows = document.querySelectorAll('#scheduleDaysContainer .schedule-row');
  dayRows.forEach(row => {
    const dayKey = row.dataset.day;
    const isWeekday = dayKey !== 'Sabado' && dayKey !== 'Domingo';
    const activeCheckbox = row.querySelector('.schedule-day-active');
    const labelSpanText = row.querySelector('.schedule-toggle span');
    const startInput = row.querySelector('.schedule-time-start');
    const endInput = row.querySelector('.schedule-time-end');

    if (activeCheckbox) activeCheckbox.checked = isWeekday;
    if (labelSpanText) labelSpanText.textContent = isWeekday ? 'Atiende' : 'No atiende';
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

/**
 * Guarda la configuración de horarios de atención del profesional.
 * @param {Event} event
 */
const saveSchedule = async (event) => {
  event.preventDefault();
  if (!detailProfessionalId) return;

  const dayRows = document.querySelectorAll('#scheduleDaysContainer .schedule-row');
  const schedulePayload = [];

  for (const row of dayRows) {
    const dayKey = row.dataset.day;
    const isActive = row.querySelector('.schedule-day-active').checked;
    const startTime = row.querySelector('.schedule-time-start').value;
    const endTime = row.querySelector('.schedule-time-end').value;

    if (isActive) {
      if (!startTime || !endTime) {
        window.ToastService?.warning?.(`Debes especificar hora de inicio y fin para el ${dayKey}.`);
        return;
      }
      if (endTime <= startTime) {
        window.ToastService?.warning?.(`La hora de fin debe ser posterior a la hora de inicio para el ${dayKey}.`);
        return;
      }
    }

    schedulePayload.push({
      day: dayKey.substring(0, 3),
      diaSemana: dayKey,
      dayFull: dayKey,
      active: isActive,
      start: startTime,
      end: endTime
    });
  }

  const saveButton = getElementByIdSafe('scheduleSaveButton');
  const statusMessageLabel = getElementByIdSafe('scheduleStatusMsg');
  if (saveButton) saveButton.disabled = true;
  if (statusMessageLabel) {
    statusMessageLabel.style.color = 'var(--primary)';
    statusMessageLabel.textContent = '⏳ Guardando horarios en base de datos...';
  }

  try {
    const apiResult = await apiRequest(`${API_BASE_URL}/${detailProfessionalId}/horarios`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(schedulePayload)
    });

    window.ToastService?.success?.(apiResult?.message || 'Horarios actualizados correctamente.');
    if (statusMessageLabel) {
      statusMessageLabel.style.color = 'var(--green, #166534)';
      statusMessageLabel.textContent = '✅ Horarios guardados en base de datos.';
    }
    await loadProfessionalSchedule(detailProfessionalId);
  } catch (error) {
    window.ToastService?.error?.(`No se pudieron guardar los horarios: ${error.message}`);
    if (statusMessageLabel) {
      statusMessageLabel.style.color = 'var(--red, #b91c1c)';
      statusMessageLabel.textContent = `❌ Error: ${error.message}`;
    }
  } finally {
    if (saveButton) saveButton.disabled = false;
  }
};

/**
 * Registra o edita una ausencia del profesional.
 * @param {Event} event
 */
const saveAbsence = async (event) => {
  event.preventDefault();
  const absenceId = getElementByIdSafe('absenceId').value;
  const absencePayload = {
    tipo: getElementByIdSafe('absenceTipo').value,
    fechaInicio: getElementByIdSafe('absenceFechaInicio').value,
    fechaFin: getElementByIdSafe('absenceFechaFin').value,
    observaciones: getElementByIdSafe('absenceObservaciones').value || null
  };

  if (absencePayload.fechaFin < absencePayload.fechaInicio) {
    window.ToastService?.warning('La fecha de fin debe ser igual o posterior a la fecha de inicio.');
    return;
  }

  const submitButton = getElementByIdSafe('absenceSaveButton');
  submitButton.disabled = true;

  try {
    const targetEndpoint = absenceId
      ? `${API_BASE_URL}/${detailProfessionalId}/ausencias/${absenceId}`
      : `${API_BASE_URL}/${detailProfessionalId}/ausencias`;
    const apiResult = await apiRequest(targetEndpoint, { method: absenceId ? 'PUT' : 'POST', body: absencePayload });
    window.ToastService?.success(apiResult.message || 'Ausencia guardada.');
    resetAbsenceForm();
    await loadProfessionalAbsences(detailProfessionalId);
  } catch (error) {
    window.ToastService?.error(`No se pudo guardar la ausencia: ${error.message}`);
  } finally {
    submitButton.disabled = false;
  }
};

// ===================================================================
// 7. EVENT LISTENERS E INICIALIZACIÓN PRINCIPAL
// ===================================================================

/**
 * Navega a una página específica de la tabla de profesionales.
 * @param {number} pageNumber
 */
window.goToPage = async (pageNumber) => {
  currentPageIndex = pageNumber;
  await loadProfessionalsList();
};

/**
 * Inicializa el comportamiento del sidebar responsive en dispositivos móviles.
 */
const initSidebar = () => {
  const hamburgerButton = getElementByIdSafe('hamburger');
  const sidebarElement = getElementByIdSafe('sidebar');
  const overlayElement = getElementByIdSafe('overlay');

  if (!hamburgerButton || !sidebarElement || !overlayElement) return;

  const toggleMobileMenu = (showMenu) => {
    sidebarElement.classList.toggle('open', showMenu);
    overlayElement.classList.toggle('open', showMenu);
    hamburgerButton.setAttribute('aria-expanded', showMenu);
    overlayElement.setAttribute('aria-hidden', !showMenu);

    if (showMenu) {
      const firstNavigationLink = sidebarElement.querySelector('.nav-item');
      if (firstNavigationLink) firstNavigationLink.focus();
    } else {
      hamburgerButton.focus();
    }
  };

  hamburgerButton.addEventListener('click', () => toggleMobileMenu(true));
  overlayElement.addEventListener('click', () => toggleMobileMenu(false));

  sidebarElement.querySelectorAll('.nav-item').forEach(navItem => {
    navItem.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMobileMenu(false);
      }
    });
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
      event.preventDefault();
      toggleMobileMenu(false);
    }
  });
};

/**
 * Inicializa la lógica de filtrado reactivo debounced y el formulario de búsqueda.
 */
const initFiltersAPI = () => {
  const searchInputField = getElementByIdSafe('searchInput');
  const specialtyFilterSelect = getElementByIdSafe('filterSpecialty');
  const statusFilterSelect = getElementByIdSafe('filterStatus');

  const filterFormElement = searchInputField?.closest('form') || document.querySelector('.filters-section form');
  if (filterFormElement) {
    filterFormElement.addEventListener('submit', (e) => {
      e.preventDefault();
      currentPageIndex = 1;
      loadProfessionalsList();
    });
  }

  searchInputField?.addEventListener('input', debounce(() => {
    currentPageIndex = 1;
    loadProfessionalsList();
  }, 400));

  specialtyFilterSelect?.addEventListener('change', () => {
    currentPageIndex = 1;
    loadProfessionalsList();
  });

  statusFilterSelect?.addEventListener('change', () => {
    currentPageIndex = 1;
    loadProfessionalsList();
  });
};

/**
 * Asigna la validación visual de campos de entrada.
 */
const bindProfessionalFieldValidation = () => {
  const formElement = getElementByIdSafe('formProfessional');
  if (!formElement) return;

  formElement.querySelectorAll('input, select').forEach((inputField) => {
    inputField.addEventListener('input', () => {
      if (inputField.id === 'formTelefono' && inputField.value.trim()) {
        const isValidPhone = (inputField.value.match(/\d/g) || []).length >= 7
          && (inputField.value.match(/\d/g) || []).length <= 15;
        if (window.ValidationUtils) {
          if (!isValidPhone) window.ValidationUtils.showError(inputField, null, 'Ingresa un teléfono válido.');
          else window.ValidationUtils.clearError(inputField);
        }
      } else if (inputField.id === 'formRegistroMedico' && inputField.value.trim()) {
        const isValidMedicalRegistry = /^[A-Za-z0-9\-\. ]{3,30}$/.test(inputField.value.trim());
        if (window.ValidationUtils) {
          if (!isValidMedicalRegistry) window.ValidationUtils.showError(inputField, null, 'Use solo letras, números y guiones.');
          else window.ValidationUtils.clearError(inputField);
        }
      } else {
        if (window.ValidationUtils && inputField.value.trim()) {
          window.ValidationUtils.clearError(inputField);
        }
      }
    });
  });
};

/**
 * Inicializa los eventos del campo de contraseña.
 */
const initProfessionalPassword = () => {
  const passwordInput = getElementByIdSafe('formContrasenaAcceso');
  const toggleVisibilityButton = getElementByIdSafe('toggleProfesionalPassword');
  if (!passwordInput) return;

  passwordInput.addEventListener('input', updateProfessionalPasswordRules);
  updateProfessionalPasswordRules();

  toggleVisibilityButton?.addEventListener('click', () => {
    const isPasswordVisible = passwordInput.type === 'text';
    passwordInput.type = isPasswordVisible ? 'password' : 'text';
    toggleVisibilityButton.textContent = isPasswordVisible ? '👁' : '🙈';
    toggleVisibilityButton.setAttribute('aria-label', isPasswordVisible ? 'Mostrar contraseña' : 'Ocultar contraseña');
  });
};

/**
 * Configura los event listeners para abrir, cerrar y procesar modales.
 */
const initModals = () => {
  const newProfessionalButton = getElementByIdSafe('profesionales-btn-nuevo') || getElementByIdSafe('btnNewProfessional');
  const formModalCloseButton = getElementByIdSafe('modalFormClose');
  const formModalCancelButton = getElementByIdSafe('modalFormCancel');
  const detailModalCloseButton = getElementByIdSafe('modalDetailClose');
  const detailModalCancelButton = getElementByIdSafe('modalDetailCloseBtn');
  const confirmDeleteCloseButton = getElementByIdSafe('modalConfirmDeleteClose');
  const confirmDeleteCancelButton = getElementByIdSafe('modalConfirmDeleteCancel');
  
  const formModalElement = getElementByIdSafe('modalForm');
  const detailModalElement = getElementByIdSafe('modalDetail');
  const confirmDeleteModalElement = getElementByIdSafe('modalConfirmDelete');
  
  const formElement = getElementByIdSafe('formProfessional');
  const scheduleFormElement = getElementByIdSafe('scheduleForm');
  const absenceFormElement = getElementByIdSafe('absenceForm');

  newProfessionalButton?.addEventListener('click', () => openFormModal(false));

  formModalCloseButton?.addEventListener('click', closeFormModal);
  formModalCancelButton?.addEventListener('click', closeFormModal);
  detailModalCloseButton?.addEventListener('click', closeDetailModal);
  detailModalCancelButton?.addEventListener('click', closeDetailModal);
  confirmDeleteCloseButton?.addEventListener('click', closeConfirmDeleteModal);
  confirmDeleteCancelButton?.addEventListener('click', closeConfirmDeleteModal);

  formModalElement?.addEventListener('click', (e) => { if (e.target === e.currentTarget) closeFormModal(); });
  detailModalElement?.addEventListener('click', (e) => { if (e.target === e.currentTarget) closeDetailModal(); });
  confirmDeleteModalElement?.addEventListener('click', (e) => { if (e.target === e.currentTarget) closeConfirmDeleteModal(); });

  formElement?.addEventListener('submit', saveProfessional);
  scheduleFormElement?.addEventListener('submit', saveSchedule);
  getElementByIdSafe('btnApplyDefaultSchedule')?.addEventListener('click', applyDefaultSchedule);
  absenceFormElement?.addEventListener('submit', saveAbsence);
  getElementByIdSafe('absenceCancelEdit')?.addEventListener('click', resetAbsenceForm);

  const tableBody = getElementByIdSafe('professionalsTbody');
  tableBody?.addEventListener('click', (e) => {
    const deleteButton = e.target.closest('.btn-delete[data-id]');
    if (deleteButton) {
      const id = deleteButton.getAttribute('data-id');
      const name = deleteButton.getAttribute('data-name') || 'este profesional';
      const estado = deleteButton.getAttribute('data-estado') || 'activo';
      if (id) openConfirmToggleEstadoModal(id, name, estado);
    }
  });

  tableBody?.addEventListener('click', (e) => {
    const viewButton = e.target.closest('.btn-view[data-id]');
    if (viewButton) {
      openProfessionalDetailView(viewButton, getAvatarColorBySpecialty(viewButton.dataset.specialty || ''), getStatusBadgeClass(viewButton.dataset.status || ''));
      resetModalTabs();
      resetAbsenceForm();
      loadProfessionalSchedule(Number(viewButton.dataset.id));
      loadProfessionalAbsences(Number(viewButton.dataset.id));
    }
  });

  const confirmToggleStatusButton = getElementByIdSafe('modalConfirmDeleteConfirm');
  confirmToggleStatusButton?.addEventListener('click', async () => {
    const hiddenIdInput = getElementByIdSafe('deleteProfesionalId');
    const hiddenEstadoInput = getElementByIdSafe('deleteProfesionalEstado');
    const professionalId = Number(hiddenIdInput?.value);
    if (!professionalId) return;

    const currentStatus = (hiddenEstadoInput?.value || 'activo').toLowerCase();
    const isCurrentlyActive = currentStatus === 'activo';

    confirmToggleStatusButton.disabled = true;
    confirmToggleStatusButton.textContent = confirmToggleStatusButton.dataset.loadingText || (isCurrentlyActive ? '⏳ Desactivando...' : '⏳ Reactivando...');

    try {
      let apiResult;
      if (isCurrentlyActive) {
        apiResult = await apiRequest(`${API_BASE_URL}/${professionalId}`, { method: 'DELETE' });
        window.ToastService?.success(`✅ ${apiResult.message || 'Profesional desactivado correctamente.'}`);
      } else {
        apiResult = await apiRequest(`${API_BASE_URL}/${professionalId}/estado`, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ estado: 'activo' })
        });
        window.ToastService?.success(`✅ ${apiResult.message || 'Profesional reactivado correctamente.'}`);
      }
      closeConfirmDeleteModal();
      window.location.reload();
    } catch (error) {
      window.ToastService?.error(`❌ ${error.message}`);
    } finally {
      confirmToggleStatusButton.disabled = false;
      confirmToggleStatusButton.textContent = isCurrentlyActive ? 'Confirmar desactivación' : 'Confirmar reactivación';
    }
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (formModalElement?.classList.contains('open')) { e.preventDefault(); closeFormModal(); }
      if (detailModalElement?.classList.contains('open')) { e.preventDefault(); closeDetailModal(); }
      if (confirmDeleteModalElement?.classList.contains('open')) { e.preventDefault(); closeConfirmDeleteModal(); }
    }
  });
};

/**
 * Función de inicialización principal.
 */
const init = async () => {
  initSidebar();
  initModals();
  initModalTabs();
  bindProfessionalFieldValidation();
  initProfessionalPassword();
  initFiltersAPI();

  await loadSpecialtiesCatalog();
  initServerStats();

  window.addEventListener('beforeunload', () => {});
};

document.addEventListener('DOMContentLoaded', init);
