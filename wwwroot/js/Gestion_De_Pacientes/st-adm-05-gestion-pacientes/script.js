(() => {
'use strict';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  return document.getElementById(id);
};

const translate = (key, values) => window.I18N.t(key, values);

const translateGender = (value) => {
  const key = {
    M: 'gestion_pacientes_gender_male',
    F: 'gestion_pacientes_gender_female',
    O: 'gestion_pacientes_gender_other'
  }[String(value || '').trim().toUpperCase()];

  return key ? translate(key) : value || translate('common.not_available');
};

const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;'
})[character]);

const debounce = (fn, delay) => {
  let timeoutId;

  return (...args) => {
    clearTimeout(timeoutId);

    timeoutId = setTimeout(
      () => fn(...args),
      delay
    );
  };
};

const showToast = (message, type = 'success') => {
  const service = window.ToastService;
  const title = translate(`common.${type}`);

  if (!service || typeof service[type] !== 'function') {
    console.error('[SmileTrack][Pacientes] ToastService no está disponible.');
    return;
  }

  service[type](title, message);
};

// ═══════════════════════════════════════════════════════════════════
//  VARIABLES GLOBALES
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025) - estadoFiltro: controla el filtro de estado del paciente
// (activo / inactivo / retirado / todos). Se envía al endpoint /buscar
// como parámetro estado= y se aplica siempre en el servidor, no en cliente.
// ANTES: el listado solo mostraba activos y no había selector en la UI.
let searchQuery    = '';
let estadoFiltro   = 'activo';   // default: solo activos (comportamiento anterior)
let filterAlergias = '';
let filterCita     = '';
let filterHistorial = '';
let currentPage    = 1;
let serverTotal    = 0;
let serverPages    = 1;

const itemsPerPage = 20;

// ═══════════════════════════════════════════════════════════════════
//  FORMATO DE FECHAS
// ═══════════════════════════════════════════════════════════════════

const fmtDate = (iso) => {
  if (!iso) return translate('common.not_available');

  try {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return translate('common.not_available');
    return new Intl.DateTimeFormat(window.I18N.lang === 'en' ? 'en-US' : 'es-CO', {
      day: 'numeric',
      month: 'short',
      year: 'numeric'
    }).format(date);
  } catch (error) {
    console.error('[SmileTrack][Pacientes] Error al formatear una fecha:', error);
    return translate('common.not_available');
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS
// ═══════════════════════════════════════════════════════════════════

// The array contains the server page currently displayed.
const getPatients = () => {
  return Array.isArray(window.RAZOR_PATIENTS)
    ? window.RAZOR_PATIENTS
    : [];
};

const normalizarPacienteApi = (patient, index) => {
  const value = (pascal, camel, fallback = '') => patient[pascal] ?? patient[camel] ?? fallback;
  const nombres = value('Nombres', 'nombres');
  const apellidos = value('Apellidos', 'apellidos');
  const nombre = value('Name', 'name', `${nombres} ${apellidos}`.trim());
  const tipoDocumento = value('TipoDocumento', 'tipoDocumento');
  const documento = value('Documento', 'documento');
  const color = value('Color', 'color', ['blue', 'green', 'yellow', 'purple', 'slate'][index % 5]);

  return {
    ...patient,
    Id: value('Id', 'id', 0),
    Nombres: nombres,
    Apellidos: apellidos,
    Name: nombre || translate('gestion_pacientes_unnamed'),
    Initials: value('Initials', 'initials', `${nombres?.[0] || ''}${apellidos?.[0] || ''}`.toUpperCase() || '??'),
    TipoDocumento: tipoDocumento,
    Documento: documento,
    Doc: value('Doc', 'doc', [tipoDocumento, documento].filter(Boolean).join(' ')),
    FechaNacimiento: value('FechaNacimiento', 'fechaNacimiento'),
    Genero: value('Genero', 'genero'),
    Telefono: value('Telefono', 'telefono'),
    Correo: value('Correo', 'correo'),
    Ciudad: value('Ciudad', 'ciudad'),
    Direccion: value('Direccion', 'direccion'),
    GrupoSanguineo: value('GrupoSanguineo', 'grupoSanguineo'),
    AlergiasTexto: value('AlergiasTexto', 'alergiasTexto'),
    AntecedentesMedicos: value('AntecedentesMedicos', 'antecedentesMedicos'),
    ContactoEmergencia: value('ContactoEmergencia', 'contactoEmergencia'),
    TelefonoEmergencia: value('TelefonoEmergencia', 'telefonoEmergencia'),
    MedicamentosTexto: value('MedicamentosTexto', 'medicamentosTexto'),
    Estado: value('Estado', 'estado', 'activo'),
    LastVisit: value('LastVisit', 'lastVisit'),
    Diagnosis: value('Diagnosis', 'diagnosis'),
    NextVisit: value('NextVisit', 'nextVisit'),
    Allergies: value('Allergies', 'allergies', []),
    Color: color,
    History: (Array.isArray(value('History', 'history', [])) ? value('History', 'history', []) : []).map(item => ({
      Date: item.Date ?? item.date,
      Procedure: item.Procedure ?? item.procedure,
      Doctor: item.Doctor ?? item.doctor
    }))
  };
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

// Pagination totals come from the server-side result.
const renderPaginationButtons = (totalPages) => {

  const paginationNav = document.querySelector('.pagination');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');

  if (!paginationNav) return;

  const maxPage = Math.max(1, totalPages);

  paginationNav.querySelectorAll('.page-num-btn').forEach(btn => btn.remove());

  for (let i = 1; i <= maxPage; i++) {
    const btn = document.createElement('button');
    btn.className = `pagination-btn page-num-btn${i === currentPage ? ' active' : ''}`;
    btn.setAttribute('aria-label', translate('gestion_pacientes_page_number_aria', { page: i }));
    if (i === currentPage) btn.setAttribute('aria-current', 'page');
    btn.textContent = String(i);
    btn.addEventListener('click', () => {
      currentPage = i;
      renderPatients();
    });
    paginationNav.insertBefore(btn, btnNext);
  }

  if (btnPrev) btnPrev.disabled = currentPage <= 1;
  if (btnNext) btnNext.disabled = currentPage >= maxPage;
};

// ═══════════════════════════════════════════════════════════════════
//  FETCH SERVER-SIDE
// ═══════════════════════════════════════════════════════════════════

// Load the current page and all active filters from the server.
let _fetchController = null;

const fetchPacientes = async () => {

  // Cancelar fetch previo si aún está en curso
  if (_fetchController) _fetchController.abort();
  _fetchController = new AbortController();

  // Yeray (2025) - se agrega estado= para soportar inactivos/retirados/todos
  const params = new URLSearchParams({
    page:     String(currentPage),
    pageSize: String(itemsPerPage),
    estado:   estadoFiltro        // siempre se envía; default 'activo'
  });
  if (searchQuery) params.set('search', searchQuery);
  if (filterAlergias) params.set('alergias', filterAlergias);
  if (filterCita) params.set('cita', filterCita);
  if (filterHistorial) params.set('historial', filterHistorial);
  const requestedEditId = new URLSearchParams(window.location.search).get('editar');
  if (requestedEditId) {
    params.set('editar', requestedEditId);
    params.set('estado', 'todos');
  }

  try {
    const resp = await fetch(
      `/gestion-de-pacientes/buscar?${params}`,
      { credentials: 'same-origin', signal: _fetchController.signal }
    );
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const json = await resp.json();

    if (!Array.isArray(json.items) || !Number.isFinite(json.total) || !Number.isFinite(json.totalPages)) {
      throw new Error('La respuesta del servidor tiene un formato inválido.');
    }

    window.RAZOR_PATIENTS = json.items.map(normalizarPacienteApi);
    serverTotal = json.total;
    serverPages = Math.max(1, json.totalPages);
    return true;

  } catch (err) {
    if (err instanceof Error && err.name === 'AbortError') return false;
    console.error('[SmileTrack][Pacientes] Error búsqueda server-side:', err);
    showToast(translate('gestion_pacientes_error_loading'), 'error');
    return false;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDERIZADO DE PACIENTES
// ═══════════════════════════════════════════════════════════════════

const renderPatients = async () => {

  const container   = safeGetElement('patientsBody');
  const empty       = safeGetElement('emptyState');
  const pageShowing = safeGetElement('pageShowing');
  const pageTotal   = safeGetElement('pageTotal');
  const btnPrev     = safeGetElement('btnPrev');
  const btnNext     = safeGetElement('btnNext');

  if (!await fetchPacientes()) return;
  const displayPatients = getPatients();
  const totalForPagination = serverTotal;
  const totalPagesForPagination = serverPages;

  if (container) {

    container.innerHTML =
      displayPatients
        .map((patient) => {

          const initials =
            escapeHtml(patient.Initials || '??');

          const color = ['blue', 'green', 'yellow', 'purple', 'slate'].includes(patient.Color)
            ? patient.Color
            : 'blue';

          const allergies =
            Array.isArray(patient.Allergies) &&
            patient.Allergies.length

              ? patient.Allergies
                  .map(
                    item =>
                          `<span class="allergy-badge">${escapeHtml(item)}</span>`
                  )
                  .join('')

              : `<span class="patient-id">${escapeHtml(translate('gestion_pacientes_without_allergies'))}</span>`;

          return `
            <div
              class="table-row patient-row"
              role="row"
              data-name="${escapeHtml((patient.Name || '').toLowerCase())}"
              data-doc="${escapeHtml((patient.Doc || '').toLowerCase())}"
            >

              <div
                class="table-col col-paciente"
                role="gridcell"
                data-label="${escapeHtml(translate('gestion_pacientes_column_patient'))}"
              >
                <div class="patient-info">

                  <div
                    class="patient-avatar"
                    data-color="${color}"
                    aria-hidden="true"
                  >
                    ${initials}
                  </div>

                  <div>

                    <span class="patient-name">
                      ${escapeHtml(patient.Name || translate('gestion_pacientes_unnamed'))}
                    </span>

                    <span class="patient-id">
                      ${escapeHtml(patient.Doc || translate('gestion_pacientes_no_document'))}
                    </span>

                  </div>

                </div>
              </div>

              <div
                class="table-col col-fecha"
                role="gridcell"
                data-label="${escapeHtml(translate('gestion_pacientes_column_last_visit'))}"
              >
                ${fmtDate(patient.LastVisit)}
              </div>

              <div
                class="table-col col-diagnostico"
                role="gridcell"
                data-label="${escapeHtml(translate('gestion_pacientes_column_diagnosis'))}"
              >
                ${escapeHtml(patient.Diagnosis || translate('common.not_available'))}
              </div>

              <div
                class="table-col col-cita"
                role="gridcell"
                data-label="${escapeHtml(translate('gestion_pacientes_column_next_visit'))}"
              >
                ${fmtDate(patient.NextVisit)}
              </div>

              <div
                class="table-col col-alergias"
                role="gridcell"
                data-label="${escapeHtml(translate('gestion_pacientes_column_allergies'))}"
              >
                ${allergies}
              </div>

              <div
                class="table-col col-acciones"
                role="gridcell"
                data-label="${escapeHtml(translate('table.actions'))}"
              >
                <div class="actions-cell">

                  <button
                    type="button"
                    class="action-btn btn-view"
                    data-id="${patient.Id}"
                    aria-label="${escapeHtml(translate('gestion_pacientes_view_profile_aria', { name: patient.Name }))}"
                    title="${escapeHtml(translate('gestion_pacientes_view_profile'))}"
                  >
                    <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span>
                    <span class="action-label">${escapeHtml(translate('gestion_pacientes_action_view'))}</span>
                  </button>

                  <button
                    type="button"
                    class="action-btn btn-edit"
                    data-id="${patient.Id}"
                    aria-label="${escapeHtml(translate('gestion_pacientes_edit_patient_aria', { name: patient.Name }))}"
                    title="${escapeHtml(translate('gestion_pacientes_edit_patient'))}"
                  >
                    <span class="material-symbols-outlined action-icon" aria-hidden="true">edit</span>
                    <span class="action-label">${escapeHtml(translate('common.edit'))}</span>
                  </button>

                  <button
                    type="button"
                    class="action-btn btn-history"
                    data-id="${patient.Id}"
                    aria-label="${escapeHtml(translate('gestion_pacientes_history_aria', { name: patient.Name }))}"
                    title="${escapeHtml(translate('gestion_pacientes_history'))}"
                  >
                    <span class="material-symbols-outlined action-icon" aria-hidden="true">history</span>
                    <span class="action-label">${escapeHtml(translate('gestion_pacientes_history'))}</span>
                  </button>

                </div>
              </div>

            </div>
          `;

        })
        .join('');
  }

  if (empty) {
    empty.hidden = displayPatients.length > 0;
  }

  if (pageShowing) {
    pageShowing.textContent = String(
      totalForPagination === 0 ? 0 : (currentPage - 1) * itemsPerPage + displayPatients.length
    );
  }

  if (pageTotal) {
    pageTotal.textContent = String(totalForPagination);
  }

  if (btnPrev) btnPrev.disabled = currentPage <= 1;
  if (btnNext) btnNext.disabled = currentPage >= totalPagesForPagination;

  renderPaginationButtons(totalPagesForPagination);

  // Contador de resultados
  const filterResults   = safeGetElement('filterResults');
  const hasActiveFilters = searchQuery || filterAlergias || filterCita || filterHistorial || estadoFiltro !== 'activo';
  if (filterResults) {
    filterResults.textContent = hasActiveFilters
      ? translate('gestion_pacientes_filter_results', { count: totalForPagination })
      : '';
  }

  if (window.I18N) window.I18N.apply(container || document);

  // Yeray (2025) - Detalle: navega a la vista de perfil completo
  // ANTES: abría un modal con datos limitados del ViewModel del listado.
  // AHORA: redirige a GET /gestion-de-pacientes/{id}, que carga todos los datos
  //        del paciente incluyendo antecedentes, contacto de emergencia y
  //        resumen de actividad.
  document
    .querySelectorAll('.btn-view')
    .forEach((btn) => {

      btn.addEventListener(
        'click',
        (event) => {
          const id = Number(event.currentTarget.dataset.id);
          window.location.href = `/gestion-de-pacientes/${id}`;
        }
      );
    });

  // Historial
  document
    .querySelectorAll('.btn-history')
    .forEach((btn) => {

      btn.addEventListener(
        'click',
        (event) => {

          openPatientModal(
            Number(
              event.currentTarget.dataset.id
            ),
            'history'
          );
        }
      );
    });

  // Editar
  document
    .querySelectorAll('.btn-edit')
    .forEach((btn) => {

      btn.addEventListener(
        'click',
        (event) => {

          openPatientModal(
            Number(
              event.currentTarget.dataset.id
            ),
            'edit'
          );
        }
      );
    });
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL
// ═══════════════════════════════════════════════════════════════════

let modalReturnFocus = null;
let activeModal = null;

const openPatientModal = (id, type, focusCloseButton = true) => {

  const patients =
    getPatients();

  const patient =
    patients.find(
      item =>
        Number(item.Id) ===
        Number(id)
    );

  if (!patient) {
    return;
  }

  activeModal = { id, type };

  const modal =
    safeGetElement('modalPatient');

  const content =
    safeGetElement('modalPatientContent');

  const title =
    safeGetElement('modalPatientTitle');

  if (
    !modal ||
    !content ||
    !title
  ) {
    return;
  }

  if (!modal.classList.contains('open')) {
    modalReturnFocus = document.activeElement;
  }

  // ═══════════════════════════════════════════════════════════════
  // DETALLE
  // ═══════════════════════════════════════════════════════════════

  if (type === 'detail') {
    const patientStatus = String(patient.Estado || '').trim().toLowerCase();
    const patientStatusClass = {
      activo: 'activo',
      inactivo: 'inactivo',
      retirado: 'retirado'
    }[patientStatus] || 'neutral';
    const patientStatusTranslation = {
      activo: 'status.active',
      inactivo: 'status.inactive',
      retirado: 'gestion_pacientes_status_retired'
    }[patientStatus] || 'common.not_available';

    title.textContent = translate('gestion_pacientes_detail_title', { name: patient.Name });

    content.innerHTML = `
      <div class="modal-detail">
        <div class="patient-detail-grid">
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('common.document'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.Doc || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_birth_date'))}</span>
            <span class="patient-detail-value">${fmtDate(patient.FechaNacimiento)}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_gender'))}</span>
            <span class="patient-detail-value">${escapeHtml(translateGender(patient.Genero))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('common.phone'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.Telefono || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('common.email'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.Correo || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_city'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.Ciudad || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_blood_type'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.GrupoSanguineo || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_column_last_visit'))}</span>
            <span class="patient-detail-value"><time>${fmtDate(patient.LastVisit)}</time></span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_column_diagnosis'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.Diagnosis || translate('common.not_available'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_column_next_visit'))}</span>
            <span class="patient-detail-value"><time>${fmtDate(patient.NextVisit)}</time></span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_column_allergies'))}</span>
            <span class="patient-detail-value">${escapeHtml(patient.AlergiasTexto || translate('gestion_pacientes_without_allergies'))}</span>
          </div>
          <div class="patient-detail-field">
            <span class="patient-detail-label">${escapeHtml(translate('gestion_pacientes_filter_status'))}</span>
            <span class="patient-detail-value">
              <span class="status-badge ${patientStatusClass}" role="status">${escapeHtml(translate(patientStatusTranslation))}</span>
            </span>
          </div>
        </div>

        ${window.canEditPatientIdentity && patient.Estado === 'activo' ? `
        <div class="patient-deactivate-actions">
          <button
            type="button"
            class="action-btn btn-danger btn-disable"
            data-id="${patient.Id}"
            aria-label="${escapeHtml(translate('gestion_pacientes_deactivate_aria', { name: patient.Name }))}"
          >
            <span class="material-symbols-outlined action-icon" aria-hidden="true">person_off</span>
            <span>${escapeHtml(translate('gestion_pacientes_deactivate'))}</span>
          </button>
        </div>` : ''}
      </div>
    `;

  // ═══════════════════════════════════════════════════════════════
  // HISTORIAL
  // ═══════════════════════════════════════════════════════════════

  } else if (type === 'history') {

    title.textContent = translate('gestion_pacientes_history_title', { name: patient.Name });

    if (
      patient.History &&
      patient.History.length
    ) {

      content.innerHTML = `
        <ul class="patient-history-list">

          ${patient.History
            .map(entry => `

              <li class="patient-history-item">
                <time class="patient-history-date">
                  ${fmtDate(entry.Date)}
                </time>

                <div class="patient-history-procedure">
                  <strong>${escapeHtml(entry.Procedure)}</strong>
                </div>

                <div class="patient-history-doctor">${escapeHtml(translate('gestion_pacientes_doctor_prefix'))} ${escapeHtml(entry.Doctor)}</div>
              </li>

            `)
            .join('')}

        </ul>
      `;

    } else {

      content.innerHTML = `<p class="patient-history-empty">${escapeHtml(translate('gestion_pacientes_no_history'))}</p>`;
    }

  // ═══════════════════════════════════════════════════════════════
  // EDITAR
  // ═══════════════════════════════════════════════════════════════

  } else if (type === 'edit') {

    title.textContent = translate('gestion_pacientes_edit_title', { name: patient.Name });

    const modalFooter = modal.querySelector('.modal-footer');
    if (modalFooter) {
      modalFooter.hidden = true;
    }

    /*
     * Nombres y apellidos separados:
     * utilizamos Nombres/Apellidos si están disponibles
     * en el ViewModel.
     */
    const nombresActuales =
      patient.Nombres ||
      patient.Name?.split(' ').slice(0, -1).join(' ') ||
      '';

    const apellidosActuales =
      patient.Apellidos ||
      patient.Name?.split(' ').slice(-1).join(' ') ||
      '';

    content.outerHTML = `
      <form id="editPatientForm" class="patient-modal-body modal-body modal-edit-form" role="document">

        <!-- SECCIÓN: INFORMACIÓN PERSONAL -->
        <div class="form-section-title" data-i18n="gestion_pacientes_form_personal">Información Personal</div>
        ${window.canEditPatientIdentity ? `
          <div class="form-group">
            <label class="form-label" for="editEstado" data-i18n="gestion_pacientes_filter_status">Estado</label>
            <select id="editEstado" class="form-select" data-state-select>
              <option value="activo" data-i18n="status.active" ${patient.Estado === 'activo' ? 'selected' : ''}>Activo</option>
              <option value="inactivo" data-i18n="status.inactive" ${patient.Estado === 'inactivo' ? 'selected' : ''}>Inactivo</option>
              <option value="retirado" data-i18n="gestion_pacientes_status_retired" ${patient.Estado === 'retirado' ? 'selected' : ''}>Retirado</option>
            </select>
          </div>
          <div class="form-group">
            <label class="form-label" for="editTipoDocumento" data-i18n="gestion_pacientes_document_type">Tipo de documento</label>
            <select id="editTipoDocumento" class="form-select">
              ${[['CC', 'gestion_pacientes_id_cc'], ['TI', 'gestion_pacientes_id_ti'], ['CE', 'gestion_pacientes_id_ce'], ['PAS', 'gestion_pacientes_id_passport'], ['NIT', 'gestion_pacientes_id_nit']]
                .map(([value, key]) => `<option value="${value}" data-i18n="${key}" ${patient.TipoDocumento === value ? 'selected' : ''}>${escapeHtml(translate(key))}</option>`)
                .join('')}
            </select>
          </div>
          <div class="form-group">
            <label class="form-label" for="editDocumento" data-i18n="common.document">Documento</label>
            <input type="text" id="editDocumento" class="form-input" value="${escapeHtml(patient.Documento)}" maxlength="20" required />
          </div>
          <div class="form-group">
            <label class="form-label" for="editFechaNacimiento" data-i18n="gestion_pacientes_birth_date">Fecha de nacimiento</label>
            <input type="date" id="editFechaNacimiento" class="form-input" value="${escapeHtml(String(patient.FechaNacimiento || '').slice(0, 10))}" required />
          </div>
          <div class="form-group">
            <label class="form-label" for="editGrupoSanguineo" data-i18n="gestion_pacientes_blood_type">Grupo sanguíneo</label>
            <select id="editGrupoSanguineo" class="form-select">
              <option value="" data-i18n="gestion_pacientes_unspecified">Sin especificar</option>
              ${['O+', 'O-', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-']
                .map((group) => `<option value="${group}" ${patient.GrupoSanguineo === group ? 'selected' : ''}>${group}</option>`)
                .join('')}
            </select>
          </div>
          <div class="form-group form-group--full">
            <label class="form-label" for="editDireccion" data-i18n="common.address">Dirección</label>
            <input type="text" id="editDireccion" class="form-input" value="${escapeHtml(patient.Direccion)}" maxlength="255" />
          </div>
        ` : ''}

        <div class="form-group">
          <label class="form-label" for="editNombres" data-i18n="common.name">Nombres</label>
          <input
            type="text"
            id="editNombres"
            class="form-input"
            value="${escapeHtml(nombresActuales)}"
            required
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editApellidos" data-i18n="common.lastname">Apellidos</label>
          <input
            type="text"
            id="editApellidos"
            class="form-input"
            value="${escapeHtml(apellidosActuales)}"
            required
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editGenero" data-i18n="gestion_pacientes_gender">Género</label>
          <select id="editGenero" class="form-select">
            <option value="M" data-i18n="gestion_pacientes_gender_male" ${patient.Genero === 'M' ? 'selected' : ''}>Masculino</option>
            <option value="F" data-i18n="gestion_pacientes_gender_female" ${patient.Genero === 'F' ? 'selected' : ''}>Femenino</option>
            <option value="O" data-i18n="gestion_pacientes_gender_other" ${patient.Genero === 'O' ? 'selected' : ''}>Otro</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label" for="editCiudad" data-i18n="gestion_pacientes_city">Ciudad</label>
          <input type="text" id="editCiudad" class="form-input" value="${escapeHtml(patient.Ciudad)}" />
        </div>

        <!-- SECCIÓN: CONTACTO -->
        <div class="form-section-title" data-i18n="gestion_pacientes_form_contact">Información de Contacto</div>

        <div class="form-group">
          <label class="form-label" for="editTelefono" data-i18n="common.phone">Teléfono</label>
          <input type="tel" id="editTelefono" class="form-input" value="${escapeHtml(patient.Telefono)}" />
        </div>

        <div class="form-group">
          <label class="form-label" for="editCorreo" data-i18n="common.email">Correo Electrónico</label>
          <input type="email" id="editCorreo" class="form-input" value="${escapeHtml(patient.Correo)}" />
        </div>

        <!-- SECCIÓN: EMERGENCIA -->
        <div class="form-section-title" data-i18n="gestion_pacientes_emergency_contact">Contacto de Emergencia</div>

        <div class="form-group">
          <label class="form-label" for="editContactoEmergencia" data-i18n="gestion_pacientes_emergency_name">Nombre del Contacto</label>
          <input
            type="text"
            id="editContactoEmergencia"
            class="form-input"
            data-i18n-ph="gestion_pacientes_full_name_placeholder"
            placeholder="Nombre completo"
            value="${escapeHtml(patient.ContactoEmergencia)}"
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editTelefonoEmergencia" data-i18n="gestion_pacientes_emergency_phone">Teléfono de Emergencia</label>
          <input
            type="tel"
            id="editTelefonoEmergencia"
            class="form-input"
            data-i18n-ph="gestion_pacientes_phone_example"
            placeholder="Ej. 300 123 4567"
            value="${escapeHtml(patient.TelefonoEmergencia)}"
          />
        </div>

        <!-- SECCIÓN: SALUD -->
        <div class="form-section-title" data-i18n="gestion_pacientes_form_health">Información de Salud</div>

        <div class="form-group">
          <label class="form-label" for="editAllergyInput" data-i18n="gestion_pacientes_known_allergies">Alergias conocidas</label>
          <div class="allergy-editor">
            <div class="allergy-chip-list" id="editAllergyChips" aria-live="polite"></div>
            <div class="allergy-entry">
              <input type="text" id="editAllergyInput" class="form-input" maxlength="150" data-i18n-ph="gestion_pacientes_allergy_example" placeholder="Ej. Penicilina" />
              <button type="button" class="btn-secondary" id="addPatientAllergy" data-i18n="common.add">Agregar</button>
            </div>
            <input type="hidden" id="editAlergias" value="${escapeHtml(patient.AlergiasTexto)}" />
            <span class="error-message" id="editAllergyError" role="alert" hidden></span>
          </div>
        </div>

        <div class="form-group form-group--full">
          <label class="form-label" for="editAntecedentesMedicos" data-i18n="gestion_pacientes_medical_history">Antecedentes Médicos</label>
          <textarea
            id="editAntecedentesMedicos"
            class="form-input"
            data-i18n-ph="gestion_pacientes_medical_history_placeholder"
            placeholder="Enfermedades previas, cirugías, condiciones crónicas, tratamientos actuales..."
          >${escapeHtml(patient.AntecedentesMedicos)}</textarea>
        </div>

        <!-- BOTONES DE ACCIÓN -->
        <div class="form-actions">
          <button type="button" class="btn-secondary" id="editCancelBtn" data-i18n="common.cancel">Cancelar</button>
          <button type="submit" class="btn-primary" id="editSaveBtn" data-i18n="gestion_pacientes_save_changes">Guardar cambios</button>
        </div>

      </form>
    `;

    const allergyValues = [...new Set((Array.isArray(patient.Allergies)
      ? patient.Allergies
      : (patient.AlergiasTexto || '').split(','))
      .map(value => String(value).trim())
      .filter(Boolean))];
    const allergyInput = safeGetElement('editAllergyInput');
    const allergyChips = safeGetElement('editAllergyChips');
    const allergyHidden = safeGetElement('editAlergias');
    const allergyError = safeGetElement('editAllergyError');

    const renderAllergies = () => {
      if (!allergyChips || !allergyHidden) return;
      allergyChips.replaceChildren();
      allergyValues.forEach((value, index) => {
        const chip = document.createElement('span');
        chip.className = 'allergy-chip';
        const label = document.createElement('span');
        label.textContent = value;
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'allergy-chip-remove';
        remove.setAttribute('aria-label', translate('gestion_pacientes_remove_allergy_aria', { name: value }));
        remove.textContent = '×';
        remove.addEventListener('click', () => {
          allergyValues.splice(index, 1);
          renderAllergies();
        });
        chip.append(label, remove);
        allergyChips.append(chip);
      });
      allergyHidden.value = allergyValues.join(', ');
    };

    const addAllergy = () => {
      const value = allergyInput?.value.trim() || '';
      if (allergyError) {
        allergyError.hidden = true;
        allergyError.textContent = '';
      }
      if (!value) return;
      if (value.length > 150) {
        if (allergyError) {
          allergyError.textContent = translate('gestion_pacientes_allergy_max_length');
          allergyError.hidden = false;
        }
        return;
      }
      if (!allergyValues.some(existing => existing.toLocaleLowerCase() === value.toLocaleLowerCase())) {
        allergyValues.push(value);
        renderAllergies();
      }
      if (allergyInput) allergyInput.value = '';
      allergyInput?.focus();
    };

    safeGetElement('addPatientAllergy')?.addEventListener('click', addAllergy);
    allergyInput?.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        addAllergy();
      }
    });
    renderAllergies();

    const editForm =
      safeGetElement('editPatientForm');

    if (window.I18N) window.I18N.apply(editForm || content);

    editForm?.addEventListener(
      'submit',
      async (event) => {

        event.preventDefault();

        const saveBtn =
          safeGetElement('editSaveBtn');

        if (saveBtn) {

          saveBtn.disabled = true;
          saveBtn.textContent = translate('common.saving');
        }

        const token =
          document.querySelector(
            'input[name="__RequestVerificationToken"]'
          )?.value;

        if (!token) {

          showToast(
            translate('gestion_pacientes_csrf_missing'),
            'error'
          );

          if (saveBtn) {

            saveBtn.disabled = false;
            saveBtn.textContent = translate('gestion_pacientes_save_changes');
          }

          return;
        }

        const data =
          new FormData();

        const nombres =
          safeGetElement('editNombres')
            ?.value
            .trim() || '';

        const apellidos =
          safeGetElement('editApellidos')
            ?.value
            .trim() || '';

        const telefono =
          safeGetElement('editTelefono')
            ?.value
            .trim() || '';

        const correo =
          safeGetElement('editCorreo')
            ?.value
            .trim() || '';

        const ciudad =
          safeGetElement('editCiudad')
            ?.value
            .trim() || '';

        const alergias =
          safeGetElement('editAlergias')
            ?.value
            .trim() || '';

        const genero =
          safeGetElement('editGenero')
            ?.value || '';

        // Yeray (2025) - campos nuevos enviados al endpoint actualizar
        const contactoEmergencia =
          safeGetElement('editContactoEmergencia')
            ?.value
            .trim() ?? '';

        const telefonoEmergencia =
          safeGetElement('editTelefonoEmergencia')
            ?.value
            .trim() ?? '';

        const antecedentesMedicos =
          safeGetElement('editAntecedentesMedicos')
            ?.value
            .trim() ?? '';

        data.append(
          'idPaciente',
          String(patient.Id)
        );

        data.append(
          'nombres',
          nombres
        );

        data.append(
          'apellidos',
          apellidos
        );

        data.append(
          'telefono',
          telefono
        );

        data.append(
          'correo',
          correo
        );

        data.append(
          'ciudad',
          ciudad
        );

        data.append(
          'alergias',
          alergias
        );

        data.append(
          'genero',
          genero
        );

        // Yeray (2025) - append de los 3 campos nuevos
        data.append('contactoEmergencia',  contactoEmergencia);
        data.append('telefonoEmergencia',  telefonoEmergencia);
        data.append('antecedentesMedicos', antecedentesMedicos);

        data.append(
          'estado',
          safeGetElement('editEstado')?.value || patient.Estado || 'activo'
        );

        for (const [fieldId, fieldName] of [
          ['editTipoDocumento', 'tipoDoc'],
          ['editDocumento', 'documento'],
          ['editFechaNacimiento', 'fechaNacimiento'],
          ['editGrupoSanguineo', 'grupoSanguineo'],
          ['editDireccion', 'direccion']
        ]) {
          const field = safeGetElement(fieldId);
          if (field) data.append(fieldName, field.value.trim());
        }

        try {

          const response =
            await fetch(
              '/gestion-de-pacientes/actualizar',
              {
                method: 'POST',
                body: data,
                credentials: 'same-origin',
                headers: {
                  'X-CSRF-TOKEN': token
                }
              }
            );

          const result =
            await response.json();

          if (!response.ok) {

            throw new Error(
              result.message ||
              translate('gestion_pacientes_update_error')
            );
          }

          // Actualizar objeto en memoria
          patient.Name =
            `${nombres} ${apellidos}`.trim();

          patient.Nombres =
            nombres;

          patient.Apellidos =
            apellidos;

          patient.Telefono =
            telefono || null;

          patient.Correo =
            correo || null;

          patient.Ciudad =
            ciudad || null;

          patient.Genero =
            genero || null;

          patient.AlergiasTexto =
            alergias || null;

          patient.Allergies =
            alergias

              ? alergias
                  .split(',')
                  .map(
                    item => item.trim()
                  )
                  .filter(Boolean)

              : [];

          // Yeray (2025) - sincronizar los 3 campos nuevos en memoria
          patient.ContactoEmergencia  = contactoEmergencia  || null;
          patient.TelefonoEmergencia  = telefonoEmergencia  || null;
          patient.AntecedentesMedicos = antecedentesMedicos || null;

          showToast(
            translate('gestion_pacientes_update_success'),
            'success'
          );

          closePatientModal();

          renderPatients();

        } catch (error) {

          console.error(
            '[SmileTrack][Pacientes] Error UPDATE:',
            error
          );

          showToast(
            error.message ||
            translate('gestion_pacientes_update_error'),
            'error'
          );

          if (saveBtn) {

            saveBtn.disabled = false;
            saveBtn.textContent = translate('gestion_pacientes_save_changes');
          }
        }
      }
    );

    safeGetElement(
      'editCancelBtn'
    )?.addEventListener(
      'click',
      closePatientModal
    );
  }

  modal.classList.add('open');

  modal.setAttribute(
    'aria-hidden',
    'false'
  );

  modal.removeAttribute(
    'inert'
  );

  // Agregar listener para el botón de desactivar en el modal
  const btnDisableInModal = modal.querySelector('.btn-disable');
  if (btnDisableInModal) {
    btnDisableInModal.addEventListener(
      'click',
      (event) => {
        event.preventDefault();
        desactivarPaciente(patient.Id);
      }
    );
  }

  document.body.style.overflow = 'hidden';
  if (focusCloseButton) modal.querySelector('.modal-close-btn')?.focus();
};

// ═══════════════════════════════════════════════════════════════════
//  CERRAR MODAL
// ═══════════════════════════════════════════════════════════════════

const closePatientModal = () => {

  const modal =
    safeGetElement(
      'modalPatient'
    );

  if (!modal) {
    return;
  }

  activeModal = null;

  const editForm = modal.querySelector('#editPatientForm');
  if (editForm) {
    const content = document.createElement('div');
    content.id = 'modalPatientContent';
    content.className = 'patient-modal-body modal-body';
    content.setAttribute('role', 'document');
    editForm.replaceWith(content);
  }
  const modalFooter = modal.querySelector('.modal-footer');
  if (modalFooter) {
    modalFooter.hidden = false;
  }

  modal.classList.remove(
    'open'
  );

  modal.setAttribute(
    'aria-hidden',
    'true'
  );

  modal.setAttribute(
    'inert',
    ''
  );

  document.body.style.overflow = '';

  if (modalReturnFocus instanceof HTMLElement && modalReturnFocus.isConnected) {
    modalReturnFocus.focus();
  } else {
    safeGetElement('patientsTableWrap')?.focus();
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DESACTIVAR PACIENTE
// ═══════════════════════════════════════════════════════════════════

const desactivarPaciente = (id) => {
  const patient = getPatients().find(item => Number(item.Id) === Number(id));
  const service = window.ModalService;

  if (!patient) return;
  if (!service || typeof service.confirm !== 'function') {
    console.error('[SmileTrack][Pacientes] ModalService no está disponible.');
    showToast(translate('gestion_pacientes_error_deactivate'), 'error');
    return;
  }

  service.confirm({
    title: translate('gestion_pacientes_confirm_deactivate_title'),
    message: translate('gestion_pacientes_confirm_deactivate_message', {
      name: escapeHtml(patient.Name)
    }),
    confirmText: translate('gestion_pacientes_deactivate'),
    cancelText: translate('common.cancel'),
    isDanger: true,
    onConfirm: () => confirmarDesactivacion(patient)
  });
};

const confirmarDesactivacion = async (patient) => {
  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  if (!token) {
    const error = new Error(translate('gestion_pacientes_csrf_missing'));
    showToast(error.message, 'error');
    throw error;
  }

  const data = new FormData();
  data.append('idPaciente', String(patient.Id));

  try {
    const response = await fetch('/gestion-de-pacientes/desactivar', {
      method: 'POST',
      body: data,
      credentials: 'same-origin',
      headers: { 'X-CSRF-TOKEN': token }
    });
    const result = await response.json();

    if (!response.ok) {
      throw new Error(result.message || translate('gestion_pacientes_error_deactivate'));
    }

    window.RAZOR_PATIENTS = getPatients().filter(item => Number(item.Id) !== Number(patient.Id));
    showToast(translate('gestion_pacientes_deactivate_success'), 'success');
    closePatientModal();
    currentPage = 1;
    await renderPatients();
  } catch (error) {
    console.error('[SmileTrack][Pacientes] Error DELETE:', error);
    showToast(error.message || translate('gestion_pacientes_error_deactivate'), 'error');
    throw error;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  BÚSQUEDA
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025) - initSearch: con debounce de 350ms llama al servidor
// si hay texto; sin texto vuelve al array local (RAZOR_PATIENTS inicial).
const initSearch = () => {

  const searchInput = safeGetElement('searchPatients');

  searchInput?.addEventListener(
    'input',
    debounce(async (event) => {
      searchQuery = event.target.value.toLowerCase().trim();
      currentPage = 1;
      await renderPatients();
    }, 350)
  );
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025): initPagination es ahora async para coordinar con el servidor.
const initPagination = () => {

  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');

  btnPrev?.addEventListener('click', async () => {
    if (currentPage > 1) { currentPage--; await renderPatients(); }
  });

  btnNext?.addEventListener('click', async () => {
    currentPage++;
    await renderPatients();
  });
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL
// ═══════════════════════════════════════════════════════════════════

const initModal = () => {
  const modal = safeGetElement('modalPatient');
  if (!modal) return;

  safeGetElement('modalPatientClose')?.addEventListener('click', closePatientModal);
  safeGetElement('modalPatientCancel')?.addEventListener('click', closePatientModal);

  modal.addEventListener('click', event => {
    if (event.target === modal) closePatientModal();
  });

  document.addEventListener('keydown', event => {
    if (!modal.classList.contains('open')) return;

    if (event.key === 'Escape') {
      closePatientModal();
      return;
    }

    if (event.key !== 'Tab') return;

    const focusable = Array.from(modal.querySelectorAll(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(element => !element.hidden);
    if (!focusable.length) {
      event.preventDefault();
      modal.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025): applyFilters es async para soportar renderPatients async.
const initFilters = () => {

  const applyFilters = async () => {
    currentPage = 1;
    await renderPatients();
    const clearBtn = safeGetElement('btnClearFilters');
    if (clearBtn) {
      clearBtn.classList.toggle('active', !!(searchQuery || filterAlergias || filterCita || filterHistorial || estadoFiltro !== 'activo'));
    }
  };

  // Yeray (2025) - selector de estado: conecta filterEstado a estadoFiltro
  // y dispara fetchPacientes (server-side) porque el estado se filtra en BD.
  safeGetElement('filterEstado')?.addEventListener('change', (e) => {
    estadoFiltro = e.target.value;
    applyFilters();
  });

  safeGetElement('filterAlergias')?.addEventListener('change', (e) => {
    filterAlergias = e.target.value; applyFilters();
  });
  safeGetElement('filterCita')?.addEventListener('change', (e) => {
    filterCita = e.target.value; applyFilters();
  });
  safeGetElement('filterHistorial')?.addEventListener('change', (e) => {
    filterHistorial = e.target.value; applyFilters();
  });

  safeGetElement('btnClearFilters')?.addEventListener('click', () => {
    estadoFiltro    = 'activo';   // vuelve al default
    filterAlergias  = '';
    filterCita      = '';
    filterHistorial = '';
    searchQuery = '';

    const searchInput = safeGetElement('searchPatients');
    const selEstado = safeGetElement('filterEstado');
    const sel1      = safeGetElement('filterAlergias');
    const sel2      = safeGetElement('filterCita');
    const sel3      = safeGetElement('filterHistorial');

    if (searchInput) searchInput.value = '';
    if (selEstado) selEstado.value = 'activo';
    if (sel1) sel1.value = '';
    if (sel2) sel2.value = '';
    if (sel3) sel3.value = '';

    applyFilters();
  });
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025) - init es async porque renderPatients es async.
const init = async () => {
  if (!window.I18N) throw new Error('El servicio de internacionalización no está disponible.');

  initSearch();
  initPagination();
  initModal();
  initFilters();
  await renderPatients();
  window.I18N.on('change', async () => {
    await renderPatients();
    if (!activeModal) return;

    if (activeModal.type === 'edit') {
      window.I18N.apply(safeGetElement('modalPatient'));
      const patient = getPatients().find(item => Number(item.Id) === Number(activeModal.id));
      if (patient) safeGetElement('modalPatientTitle').textContent = translate('gestion_pacientes_edit_title', { name: patient.Name });
      return;
    }

    openPatientModal(activeModal.id, activeModal.type, false);
  });
  const editHash = window.location.hash.match(/^#editar-(\d+)$/);
  if (editHash) {
    openPatientModal(Number(editHash[1]), 'edit');
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.hash}`);
  }
};

document.addEventListener('DOMContentLoaded', init);
})();