// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);

  if (!el) {
    console.warn(`Elemento no encontrado: #${id}`);
  }

  return el;
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
      () => fn.apply(this, args),
      delay
    );
  };
};

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');

  if (!toast) return;

  toast.textContent = message;

  toast.className =
    `toast${type === 'error'
      ? ' error'
      : type === 'warning'
        ? ' warning'
        : ''
    } show`;

  if (toast._timeoutId) {
    clearTimeout(toast._timeoutId);
  }

  toast._timeoutId = setTimeout(() => {
    toast.classList.remove('show');
  }, 3000);
};

// ═══════════════════════════════════════════════════════════════════
//  CONTADORES
// ═══════════════════════════════════════════════════════════════════

const animateCounters = () => {
  document
    .querySelectorAll('.stat-number[data-target]')
    .forEach(el => {

      const target =
        parseInt(
          el.getAttribute('data-target'),
          10
        ) || 0;

      let current = 0;

      const increment =
        Math.max(
          1,
          Math.ceil(target / 30)
        );

      const timer = setInterval(() => {

        current += increment;

        if (current >= target) {
          current = target;
          clearInterval(timer);
        }

        el.textContent = current;

      }, 30);
    });
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

  if (!iso) {
    return 'N/A';
  }

  try {

    const dObj = new Date(iso);

    if (isNaN(dObj.getTime())) {
      return 'N/A';
    }

    const meses = [
      'Ene',
      'Feb',
      'Mar',
      'Abr',
      'May',
      'Jun',
      'Jul',
      'Ago',
      'Sep',
      'Oct',
      'Nov',
      'Dic'
    ];

    return `${dObj.getDate()} ${meses[dObj.getMonth()]} ${dObj.getFullYear()}`;

  } catch {

    return 'N/A';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS
// ═══════════════════════════════════════════════════════════════════

// The array contains only the currently requested server page.
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
    Name: nombre || 'Sin nombre',
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
    History: value('History', 'history', []).map(item => ({
      Date: item.Date ?? item.date,
      Procedure: item.Procedure ?? item.procedure,
      Doctor: item.Doctor ?? item.doctor
    }))
  };
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

// Yeray (2025) - renderPaginationButtons usa serverPages cuando hay búsqueda
// activa (el servidor conoce el total real). Para la carga inicial usa el
// total del array local filtrado.
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
    btn.setAttribute('aria-label', `Página ${i}`);
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

// Yeray (2025) - fetchPacientes: llama a /gestion-de-pacientes/buscar
// y actualiza window.RAZOR_PATIENTS con la página recibida.
// Solo se activa cuando hay texto de búsqueda (searchQuery != '').
// Para la carga inicial y los filtros locales se sigue usando RAZOR_PATIENTS.
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

    window.RAZOR_PATIENTS = (json.items ?? []).map(normalizarPacienteApi);
    serverTotal = json.total ?? 0;
    serverPages = json.totalPages ?? 1;
    return true;

  } catch (err) {
    if (err.name === 'AbortError') return false;
    console.error('[SmileTrack][Pacientes] Error búsqueda server-side:', err);
    showToast('No se pudo cargar la lista de pacientes.', 'error');
    const fallbackPatients = getPatients();
    serverTotal = fallbackPatients.length;
    serverPages = Math.max(1, Math.ceil(serverTotal / itemsPerPage));
    return fallbackPatients.length > 0;
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

          const color =
            patient.Color === 'green'
              ? '#22c55e'
              : patient.Color === 'purple'
                ? '#9333ea'
                : patient.Color === 'yellow'
                  ? '#f59e0b'
                  : '#2563eb';

          const allergies =
            Array.isArray(patient.Allergies) &&
            patient.Allergies.length

              ? patient.Allergies
                  .map(
                    item =>
                          `<span class="allergy-badge">${escapeHtml(item)}</span>`
                  )
                  .join('')

              : '<span class="patient-id">Sin alergias</span>';

          return `
            <div
              class="table-row patient-row"
              data-name="${escapeHtml((patient.Name || '').toLowerCase())}"
              data-doc="${escapeHtml((patient.Doc || '').toLowerCase())}"
            >

              <div
                class="table-col col-paciente"
                data-label="Paciente"
              >
                <div class="patient-info">

                  <div
                    class="patient-avatar"
                    style="background:${color}"
                  >
                    ${initials}
                  </div>

                  <div>

                    <span class="patient-name">
                      ${escapeHtml(patient.Name || 'Sin nombre')}
                    </span>

                    <span class="patient-id">
                      ${escapeHtml(patient.Doc || 'Sin documento')}
                    </span>

                  </div>

                </div>
              </div>

              <div
                class="table-col col-fecha"
                data-label="Última consulta"
              >
                ${fmtDate(patient.LastVisit)}
              </div>

              <div
                class="table-col col-diagnostico"
                data-label="Diagnóstico"
              >
                ${escapeHtml(patient.Diagnosis || 'N/A')}
              </div>

              <div
                class="table-col col-cita"
                data-label="Próxima cita"
              >
                ${fmtDate(patient.NextVisit)}
              </div>

              <div
                class="table-col col-alergias"
                data-label="Alergias"
              >
                ${allergies}
              </div>

              <div
                class="table-col col-acciones"
                data-label="Acciones"
              >
                <div class="actions-cell">

                  <button
                    class="action-btn btn-view"
                    data-id="${patient.Id}"
                    aria-label="Ver perfil de ${escapeHtml(patient.Name)}"
                    title="Ver perfil completo"
                  >
                    <span class="action-icon" aria-hidden="true">👁️</span>
                    <span class="action-label">Perfil</span>
                  </button>

                  <button
                    class="action-btn btn-edit"
                    data-id="${patient.Id}"
                    aria-label="Editar ${escapeHtml(patient.Name)}"
                    title="Editar paciente"
                  >
                    <span class="action-icon" aria-hidden="true">✏️</span>
                    <span class="action-label">Editar</span>
                  </button>

                  <button
                    class="action-btn btn-history"
                    data-id="${patient.Id}"
                    aria-label="Historia clínica de ${escapeHtml(patient.Name)}"
                    title="Historia clínica"
                  >
                    <span class="action-icon" aria-hidden="true">📋</span>
                    <span class="action-label">Historial</span>
                  </button>

                </div>
              </div>

            </div>
          `;

        })
        .join('');
  }

  if (empty) {
    empty.style.display      = displayPatients.length === 0 ? 'block' : 'none';
    empty.setAttribute('aria-hidden', displayPatients.length === 0 ? 'false' : 'true');
  }

  if (pageShowing) {
    pageShowing.textContent = totalForPagination === 0 ? '0' : String(displayPatients.length);
  }

  if (pageTotal) {
    pageTotal.textContent = String(totalForPagination);
  }

  if (btnPrev) btnPrev.disabled = currentPage <= 1;
  if (btnNext) btnNext.disabled = currentPage >= totalPagesForPagination;

  renderPaginationButtons(totalPagesForPagination);

  // Contador de resultados
  const filterResults   = safeGetElement('filterResults');
  const hasActiveFilters = searchQuery || filterAlergias || filterCita || filterHistorial;
  if (filterResults) {
    filterResults.textContent = hasActiveFilters
      ? `${totalForPagination} resultado${totalForPagination !== 1 ? 's' : ''} encontrado${totalForPagination !== 1 ? 's' : ''}`
      : '';
  }

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

const openPatientModal = (id, type) => {

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

  // ═══════════════════════════════════════════════════════════════
  // DETALLE
  // ═══════════════════════════════════════════════════════════════

  if (type === 'detail') {

    title.textContent =
      `Detalle de ${patient.Name}`;

    content.innerHTML = `
      <div class="modal-detail">

        <p>
          <strong>Documento:</strong>
          ${escapeHtml(patient.Doc || 'N/A')}
        </p>

        <p>
          <strong>Fecha de nacimiento:</strong>
          ${fmtDate(patient.FechaNacimiento)}
        </p>

        <p>
          <strong>Género:</strong>
          ${escapeHtml(patient.Genero || 'N/A')}
        </p>

        <p>
          <strong>Teléfono:</strong>
          ${escapeHtml(patient.Telefono || 'N/A')}
        </p>

        <p>
          <strong>Correo:</strong>
          ${escapeHtml(patient.Correo || 'N/A')}
        </p>

        <p>
          <strong>Ciudad:</strong>
          ${escapeHtml(patient.Ciudad || 'N/A')}
        </p>

        <p>
          <strong>Grupo sanguíneo:</strong>
          ${escapeHtml(patient.GrupoSanguineo || 'N/A')}
        </p>

        <p>
          <strong>Última consulta:</strong>
          <time>
            ${fmtDate(patient.LastVisit)}
          </time>
        </p>

        <p>
          <strong>Diagnóstico:</strong>
          ${escapeHtml(patient.Diagnosis || 'N/A')}
        </p>

        <p>
          <strong>Próxima cita:</strong>
          <time>
            ${fmtDate(patient.NextVisit)}
          </time>
        </p>

        <p>
          <strong>Alergias:</strong>
          ${escapeHtml(patient.AlergiasTexto || 'Ninguna')}
        </p>

        <p>
          <strong>Estado:</strong>
          ${escapeHtml(patient.Estado || 'N/A')}
        </p>

        ${window.canEditPatientIdentity && patient.Estado === 'activo' ? `
        <div style="margin-top:16px;padding-top:14px;border-top:1px solid var(--border);">
          <button
            class="action-btn btn-disable"
            data-id="${patient.Id}"
            style="color:var(--red);border-color:var(--red);width:100%;justify-content:center;"
            aria-label="Desactivar a ${escapeHtml(patient.Name)}"
          >
            🗑️ Desactivar paciente
          </button>
        </div>` : ''}

      </div>
    `;

  // ═══════════════════════════════════════════════════════════════
  // HISTORIAL
  // ═══════════════════════════════════════════════════════════════

  } else if (type === 'history') {

    title.textContent =
      `Historial Clínico de ${patient.Name}`;

    if (
      patient.History &&
      patient.History.length
    ) {

      content.innerHTML = `
        <ul
          style="
            list-style:none;
            padding:0;
            margin:0;
          "
        >

          ${patient.History
            .map(entry => `

              <li
                style="
                  padding:12px 0;
                  border-bottom:1px solid var(--border)
                "
              >

                <time
                  style="
                    color:var(--text-muted);
                    font-size:.85rem
                  "
                >
                  ${fmtDate(entry.Date)}
                </time>

                <div
                  style="margin-top:4px;"
                >
                  <strong>
                    ${escapeHtml(entry.Procedure)}
                  </strong>
                </div>

                <div
                  style="
                    font-size:.85rem;
                    color:var(--text-muted)
                  "
                >
                  Dr. ${escapeHtml(entry.Doctor)}
                </div>

              </li>

            `)
            .join('')}

        </ul>
      `;

    } else {

      content.innerHTML = `
        <p
          style="
            color:var(--text-muted);
            font-style:italic;
            text-align:center;
            padding:20px
          "
        >
          Sin historial registrado
        </p>
      `;
    }

  // ═══════════════════════════════════════════════════════════════
  // EDITAR
  // ═══════════════════════════════════════════════════════════════

  } else if (type === 'edit') {

    title.textContent =
      `Editar paciente: ${patient.Name}`;

    const modalFooter = modal.querySelector('.modal-footer');
    if (modalFooter) {
      modalFooter.hidden = true;
      modalFooter.style.display = 'none';
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
      <form id="editPatientForm" class="patient-modal-body modal-edit-form" role="document">

        <!-- SECCIÓN: INFORMACIÓN PERSONAL -->
        <div class="form-section-title">Información Personal</div>

        ${window.canEditPatientIdentity ? `
          <div class="form-group">
            <label class="form-label" for="editEstado">Estado</label>
            <select id="editEstado" class="form-select">
              <option value="activo" ${patient.Estado === 'activo' ? 'selected' : ''}>Activo</option>
              <option value="inactivo" ${patient.Estado === 'inactivo' ? 'selected' : ''}>Inactivo</option>
              <option value="retirado" ${patient.Estado === 'retirado' ? 'selected' : ''}>Retirado</option>
            </select>
          </div>
          <div class="form-group">
            <label class="form-label" for="editTipoDocumento">Tipo de documento</label>
            <select id="editTipoDocumento" class="form-select">
              ${[['CC', 'Cédula de ciudadanía'], ['TI', 'Tarjeta de identidad'], ['CE', 'Cédula de extranjería'], ['PAS', 'Pasaporte'], ['NIT', 'NIT']]
                .map(([value, label]) => `<option value="${value}" ${patient.TipoDocumento === value ? 'selected' : ''}>${label}</option>`)
                .join('')}
            </select>
          </div>
          <div class="form-group">
            <label class="form-label" for="editDocumento">Documento</label>
            <input type="text" id="editDocumento" class="form-input" value="${escapeHtml(patient.Documento)}" maxlength="20" required />
          </div>
          <div class="form-group">
            <label class="form-label" for="editFechaNacimiento">Fecha de nacimiento</label>
            <input type="date" id="editFechaNacimiento" class="form-input" value="${escapeHtml(String(patient.FechaNacimiento || '').slice(0, 10))}" required />
          </div>
          <div class="form-group">
            <label class="form-label" for="editGrupoSanguineo">Grupo sanguíneo</label>
            <select id="editGrupoSanguineo" class="form-select">
              <option value="">Sin especificar</option>
              ${['O+', 'O-', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-']
                .map((group) => `<option value="${group}" ${patient.GrupoSanguineo === group ? 'selected' : ''}>${group}</option>`)
                .join('')}
            </select>
          </div>
          <div class="form-group form-group--full">
            <label class="form-label" for="editDireccion">Dirección</label>
            <input type="text" id="editDireccion" class="form-input" value="${escapeHtml(patient.Direccion)}" maxlength="255" />
          </div>
        ` : ''}

        <div class="form-group">
          <label class="form-label" for="editNombres">Nombres</label>
          <input
            type="text"
            id="editNombres"
            class="form-input"
            value="${escapeHtml(nombresActuales)}"
            required
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editApellidos">Apellidos</label>
          <input
            type="text"
            id="editApellidos"
            class="form-input"
            value="${escapeHtml(apellidosActuales)}"
            required
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editGenero">Género</label>
          <select id="editGenero" class="form-select">
            <option value="M" ${patient.Genero === 'M' ? 'selected' : ''}>Masculino</option>
            <option value="F" ${patient.Genero === 'F' ? 'selected' : ''}>Femenino</option>
            <option value="O" ${patient.Genero === 'O' ? 'selected' : ''}>Otro</option>
          </select>
        </div>

        <div class="form-group">
          <label class="form-label" for="editCiudad">Ciudad</label>
          <input type="text" id="editCiudad" class="form-input" value="${escapeHtml(patient.Ciudad)}" />
        </div>

        <!-- SECCIÓN: CONTACTO -->
        <div class="form-section-title">Información de Contacto</div>

        <div class="form-group">
          <label class="form-label" for="editTelefono">Teléfono</label>
          <input type="tel" id="editTelefono" class="form-input" value="${escapeHtml(patient.Telefono)}" />
        </div>

        <div class="form-group">
          <label class="form-label" for="editCorreo">Correo Electrónico</label>
          <input type="email" id="editCorreo" class="form-input" value="${escapeHtml(patient.Correo)}" />
        </div>

        <!-- SECCIÓN: EMERGENCIA -->
        <div class="form-section-title">Contacto de Emergencia</div>

        <div class="form-group">
          <label class="form-label" for="editContactoEmergencia">Nombre del Contacto</label>
          <input
            type="text"
            id="editContactoEmergencia"
            class="form-input"
            placeholder="Nombre completo"
            value="${escapeHtml(patient.ContactoEmergencia)}"
          />
        </div>

        <div class="form-group">
          <label class="form-label" for="editTelefonoEmergencia">Teléfono de Emergencia</label>
          <input
            type="tel"
            id="editTelefonoEmergencia"
            class="form-input"
            placeholder="Ej. 300 123 4567"
            value="${escapeHtml(patient.TelefonoEmergencia)}"
          />
        </div>

        <!-- SECCIÓN: SALUD -->
        <div class="form-section-title">Información de Salud</div>

        <div class="form-group">
          <label class="form-label" for="editAllergyInput">Alergias conocidas</label>
          <div class="allergy-editor">
            <div class="allergy-chip-list" id="editAllergyChips" aria-live="polite"></div>
            <div class="allergy-entry">
              <input type="text" id="editAllergyInput" class="form-input" maxlength="150" placeholder="Ej. Penicilina" />
              <button type="button" class="btn-secondary" id="addPatientAllergy">Agregar</button>
            </div>
            <input type="hidden" id="editAlergias" value="${escapeHtml(patient.AlergiasTexto)}" />
            <span class="error-message" id="editAllergyError" role="alert" hidden></span>
          </div>
        </div>

        <div class="form-group form-group--full">
          <label class="form-label" for="editAntecedentesMedicos">Antecedentes Médicos</label>
          <textarea
            id="editAntecedentesMedicos"
            class="form-input"
            placeholder="Enfermedades previas, cirugías, condiciones crónicas, tratamientos actuales..."
          >${escapeHtml(patient.AntecedentesMedicos)}</textarea>
        </div>

        <!-- BOTONES DE ACCIÓN -->
        <div class="form-actions">
          <button type="button" class="btn-secondary" id="editCancelBtn">Cancelar</button>
          <button type="submit" class="btn-primary" id="editSaveBtn">Guardar cambios</button>
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
        remove.setAttribute('aria-label', `Quitar alergia ${value}`);
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
          allergyError.textContent = 'Cada alergia debe tener como máximo 150 caracteres.';
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

    editForm?.addEventListener(
      'submit',
      async (event) => {

        event.preventDefault();

        const saveBtn =
          safeGetElement('editSaveBtn');

        if (saveBtn) {

          saveBtn.disabled = true;
          saveBtn.textContent =
            'Guardando...';
        }

        const token =
          document.querySelector(
            'input[name="__RequestVerificationToken"]'
          )?.value;

        if (!token) {

          showToast(
            'No se encontró el token de seguridad.',
            'error'
          );

          if (saveBtn) {

            saveBtn.disabled = false;
            saveBtn.textContent =
              'Guardar cambios';
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
                credentials: 'include',
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
              'No fue posible actualizar el paciente.'
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
            'Paciente actualizado correctamente.',
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
            'No fue posible actualizar el paciente.',
            'error'
          );

          if (saveBtn) {

            saveBtn.disabled = false;
            saveBtn.textContent =
              'Guardar cambios';
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
      async (event) => {
        event.preventDefault();
        await desactivarPaciente(patient.Id);
        closePatientModal();
      }
    );
  }

  document.body.style.overflow =
    'hidden';
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

  const editForm = modal.querySelector('#editPatientForm');
  if (editForm) {
    const content = document.createElement('div');
    content.id = 'modalPatientContent';
    content.className = 'patient-modal-body';
    content.setAttribute('role', 'document');
    editForm.replaceWith(content);
  }
  const modalFooter = modal.querySelector('.modal-footer');
  if (modalFooter) {
    modalFooter.hidden = false;
    modalFooter.style.display = '';
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

  document.body.style.overflow =
    '';
};

// ═══════════════════════════════════════════════════════════════════
//  DESACTIVAR PACIENTE
// ═══════════════════════════════════════════════════════════════════

const desactivarPaciente = async (id) => {

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

  const confirmado =
    confirm(
      `¿Seguro que deseas desactivar a ${patient.Name}?`
    );

  if (!confirmado) {
    return;
  }

  const token =
    document.querySelector(
      'input[name="__RequestVerificationToken"]'
    )?.value;

  if (!token) {

    showToast(
      'No se encontró el token de seguridad.',
      'error'
    );

    return;
  }

  const data =
    new FormData();

  data.append(
    'idPaciente',
    String(id)
  );

  try {

    const response =
      await fetch(
        '/gestion-de-pacientes/desactivar',
        {
          method: 'POST',
          body: data,
          credentials: 'include',
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
        'No fue posible desactivar el paciente.'
      );
    }

    patient.Estado =
      'inactivo';

    /*
     * El GET del controlador solo muestra pacientes activos,
     * así que retiramos el paciente inmediatamente de la colección
     * local para mantener la interfaz consistente.
     */
    window.RAZOR_PATIENTS =
      patients.filter(
        item =>
          Number(item.Id) !==
          Number(id)
      );

    showToast(
      'Paciente desactivado correctamente.',
      'success'
    );

    currentPage = 1;

    renderPatients();

  } catch (error) {

    console.error(
      '[SmileTrack][Pacientes] Error DELETE:',
      error
    );

    showToast(
      error.message ||
      'No fue posible desactivar el paciente.',
      'error'
    );
  }
};

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR
// ═══════════════════════════════════════════════════════════════════

const initSidebar = () => {

  const hamburger =
    safeGetElement(
      'hamburger'
    );

  const sidebar =
    safeGetElement(
      'sidebar'
    );

  const overlay =
    safeGetElement(
      'overlay'
    );

  if (
    !hamburger ||
    !sidebar ||
    !overlay
  ) {
    return;
  }

  const toggleMenu =
    (show) => {

      sidebar.classList.toggle(
        'open',
        show
      );

      overlay.classList.toggle(
        'open',
        show
      );

      hamburger.setAttribute(
        'aria-expanded',
        String(show)
      );

      overlay.setAttribute(
        'aria-hidden',
        String(!show)
      );
    };

  hamburger.addEventListener(
    'click',
    () =>
      toggleMenu(
        !sidebar.classList.contains(
          'open'
        )
      )
  );

  overlay.addEventListener(
    'click',
    () =>
      toggleMenu(false)
  );
};

// ═══════════════════════════════════════════════════════════════════
//  NAVEGACIÓN
// ═══════════════════════════════════════════════════════════════════

const initNavGroups = () => {

  const groupHeaders =
    document.querySelectorAll(
      '.nav-group-header'
    );

  if (!groupHeaders.length) {
    return;
  }

  groupHeaders.forEach(
    header => {

      header.addEventListener(
        'click',
        function () {

          const group =
            this.closest(
              '.nav-group'
            );

          if (!group) {
            return;
          }

          group.classList.toggle(
            'open'
          );

          const isOpen =
            group.classList.contains(
              'open'
            );

          this.setAttribute(
            'aria-expanded',
            isOpen
          );
        }
      );

      header.addEventListener(
        'keydown',
        function (e) {

          if (
            e.key === 'Enter' ||
            e.key === ' '
          ) {

            e.preventDefault();

            this.click();
          }
        }
      );
    }
  );
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

  const modalClose =
    safeGetElement(
      'modalPatientClose'
    );

  const modalCancel =
    safeGetElement(
      'modalPatientCancel'
    );

  const modal =
    safeGetElement(
      'modalPatient'
    );

  modalClose?.addEventListener(
    'click',
    closePatientModal
  );

  modalCancel?.addEventListener(
    'click',
    closePatientModal
  );

  modal?.addEventListener(
    'click',
    (event) => {

      if (
        event.target ===
        modal
      ) {

        closePatientModal();
      }
    }
  );

  document.addEventListener(
    'keydown',
    (event) => {

      if (
        event.key === 'Escape'
      ) {

        closePatientModal();
      }
    }
  );
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
      clearBtn.classList.toggle('active', !!(filterAlergias || filterCita || filterHistorial || estadoFiltro !== 'activo'));
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

    const selEstado = safeGetElement('filterEstado');
    const sel1      = safeGetElement('filterAlergias');
    const sel2      = safeGetElement('filterCita');
    const sel3      = safeGetElement('filterHistorial');

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

  initSidebar();
  initNavGroups();
  initSearch();
  initPagination();
  initModal();
  initFilters();
  animateCounters();
  await renderPatients();
  const editHash = window.location.hash.match(/^#editar-(\d+)$/);
  if (editHash) {
    openPatientModal(Number(editHash[1]), 'edit');
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.hash}`);
  }
};

document.addEventListener('DOMContentLoaded', init);