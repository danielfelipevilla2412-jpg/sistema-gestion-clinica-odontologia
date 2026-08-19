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

let searchQuery = '';
let filterAlergias = '';
let filterCita = '';
let filterHistorial = '';
let currentPage = 1;

const itemsPerPage = 5;

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

const getPatients = () => {

  return Array.isArray(window.RAZOR_PATIENTS)
    ? window.RAZOR_PATIENTS
    : [];

};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS
// ═══════════════════════════════════════════════════════════════════

const getFilteredPatients = () => {

  const query =
    searchQuery
      .trim()
      .toLowerCase();

  const patients = getPatients();

  return patients.filter((patient) => {

    // Búsqueda por texto
    if (query) {

      const haystack = [
        patient.Name,
        patient.Doc,
        patient.Diagnosis,
        patient.Allergies?.join(' ')
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      if (!haystack.includes(query)) {
        return false;
      }
    }

    // Filtro por alergias
    if (
      filterAlergias === 'con' &&
      !(
        Array.isArray(patient.Allergies) &&
        patient.Allergies.length > 0
      )
    ) {
      return false;
    }

    if (
      filterAlergias === 'sin' &&
      Array.isArray(patient.Allergies) &&
      patient.Allergies.length > 0
    ) {
      return false;
    }

    // Filtro por próxima cita
    if (
      filterCita === 'con' &&
      !patient.NextVisit
    ) {
      return false;
    }

    if (
      filterCita === 'sin' &&
      patient.NextVisit
    ) {
      return false;
    }

    // Filtro por historial
    if (
      filterHistorial === 'con' &&
      !(
        Array.isArray(patient.History) &&
        patient.History.length > 0
      )
    ) {
      return false;
    }

    if (
      filterHistorial === 'sin' &&
      Array.isArray(patient.History) &&
      patient.History.length > 0
    ) {
      return false;
    }

    return true;
  });
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

const renderPaginationButtons = (total) => {

  const paginationNav =
    document.querySelector('.pagination');

  const btnPrev =
    safeGetElement('btnPrev');

  const btnNext =
    safeGetElement('btnNext');

  if (!paginationNav) {
    return;
  }

  const maxPage =
    Math.max(
      1,
      Math.ceil(total / itemsPerPage)
    );

  const existingPages =
    paginationNav.querySelectorAll(
      '.page-num-btn'
    );

  existingPages.forEach(btn => {
    btn.remove();
  });

  for (
    let i = 1;
    i <= maxPage;
    i++
  ) {

    const btn =
      document.createElement('button');

    btn.className =
      `pagination-btn page-num-btn${i === currentPage ? ' active' : ''}`;

    btn.setAttribute(
      'aria-label',
      `Página ${i}`
    );

    if (i === currentPage) {
      btn.setAttribute(
        'aria-current',
        'page'
      );
    }

    btn.textContent =
      String(i);

    btn.addEventListener(
      'click',
      () => {

        currentPage = i;

        renderPatients();
      }
    );

    paginationNav.insertBefore(
      btn,
      btnNext
    );
  }

  if (btnPrev) {
    btnPrev.disabled =
      currentPage <= 1;
  }

  if (btnNext) {
    btnNext.disabled =
      currentPage >= maxPage;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDERIZADO DE PACIENTES
// ═══════════════════════════════════════════════════════════════════

const renderPatients = () => {

  const container =
    safeGetElement('patientsBody');

  const empty =
    safeGetElement('emptyState');

  const pageShowing =
    safeGetElement('pageShowing');

  const pageTotal =
    safeGetElement('pageTotal');

  const btnPrev =
    safeGetElement('btnPrev');

  const btnNext =
    safeGetElement('btnNext');

  const filtered =
    getFilteredPatients();

  const total =
    filtered.length;

  const start =
    (currentPage - 1) *
    itemsPerPage;

  const pagePatients =
    filtered.slice(
      start,
      start + itemsPerPage
    );

  if (container) {

    container.innerHTML =
      pagePatients
        .map((patient) => {

          const initials =
            patient.Initials ||
            '??';

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
                      `<span class="allergy-badge">${item}</span>`
                  )
                  .join('')

              : '<span class="patient-id">Sin alergias</span>';

          return `
            <div
              class="table-row patient-row"
              data-name="${(patient.Name || '').toLowerCase()}"
              data-doc="${(patient.Doc || '').toLowerCase()}"
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
                      ${patient.Name || 'Sin nombre'}
                    </span>

                    <span class="patient-id">
                      ${patient.Doc || 'Sin documento'}
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
                ${patient.Diagnosis || 'N/A'}
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
                    aria-label="Ver detalle de ${patient.Name}"
                    title="Ver detalle del paciente"
                  >
                    <span
                      class="action-icon"
                      aria-hidden="true"
                    >
                      👁️
                    </span>

                    <span class="action-label">
                      Detalle
                    </span>
                  </button>

                  <button
                    class="action-btn btn-edit"
                    data-id="${patient.Id}"
                    aria-label="Editar ${patient.Name}"
                    title="Editar paciente"
                  >
                    <span
                      class="action-icon"
                      aria-hidden="true"
                    >
                      ✏️
                    </span>

                    <span class="action-label">
                      Editar
                    </span>
                  </button>

                  <button
                    class="action-btn btn-disable"
                    data-id="${patient.Id}"
                    aria-label="Desactivar ${patient.Name}"
                    title="Desactivar paciente"
                  >
                    <span
                      class="action-icon"
                      aria-hidden="true"
                    >
                      🗑️
                    </span>

                    <span class="action-label">
                      Desactivar
                    </span>
                  </button>

                  <button
                    class="action-btn btn-history"
                    data-id="${patient.Id}"
                    aria-label="Ver historial clínico de ${patient.Name}"
                    title="Historial clínico"
                  >
                    <span
                      class="action-icon"
                      aria-hidden="true"
                    >
                      📋
                    </span>

                    <span class="action-label">
                      Historial
                    </span>
                  </button>

                </div>
              </div>

            </div>
          `;

        })
        .join('');
  }

  if (empty) {

    empty.style.display =
      pagePatients.length === 0
        ? 'block'
        : 'none';

    empty.setAttribute(
      'aria-hidden',
      pagePatients.length === 0
        ? 'false'
        : 'true'
    );
  }

  if (pageShowing) {

    pageShowing.textContent =
      total === 0
        ? '0'
        : `${Math.min(
          start + pagePatients.length,
          total
        )}`;
  }

  if (pageTotal) {
    pageTotal.textContent =
      String(total);
  }

  if (btnPrev) {

    btnPrev.disabled =
      currentPage <= 1;
  }

  if (btnNext) {

    btnNext.disabled =
      start + pagePatients.length >= total;
  }

  renderPaginationButtons(total);

  // Contador de resultados
  const filterResults =
    safeGetElement('filterResults');

  const hasActiveFilters =
    searchQuery ||
    filterAlergias ||
    filterCita ||
    filterHistorial;

  if (filterResults) {

    filterResults.textContent =
      hasActiveFilters

        ? `${total} resultado${total !== 1 ? 's' : ''} encontrado${total !== 1 ? 's' : ''}`

        : '';
  }

  // Detalle
  document
    .querySelectorAll('.btn-view')
    .forEach((btn) => {

      btn.addEventListener(
        'click',
        (event) => {

          openPatientModal(
            Number(
              event.currentTarget.dataset.id
            ),
            'detail'
          );
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

  // Desactivar
  document
    .querySelectorAll('.btn-disable')
    .forEach((btn) => {

      btn.addEventListener(
        'click',
        async (event) => {

          const id =
            Number(
              event.currentTarget.dataset.id
            );

          await desactivarPaciente(id);
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
          ${patient.Doc || 'N/A'}
        </p>

        <p>
          <strong>Fecha de nacimiento:</strong>
          ${fmtDate(patient.FechaNacimiento)}
        </p>

        <p>
          <strong>Género:</strong>
          ${patient.Genero || 'N/A'}
        </p>

        <p>
          <strong>Teléfono:</strong>
          ${patient.Telefono || 'N/A'}
        </p>

        <p>
          <strong>Correo:</strong>
          ${patient.Correo || 'N/A'}
        </p>

        <p>
          <strong>Ciudad:</strong>
          ${patient.Ciudad || 'N/A'}
        </p>

        <p>
          <strong>Grupo sanguíneo:</strong>
          ${patient.GrupoSanguineo || 'N/A'}
        </p>

        <p>
          <strong>Última consulta:</strong>
          <time>
            ${fmtDate(patient.LastVisit)}
          </time>
        </p>

        <p>
          <strong>Diagnóstico:</strong>
          ${patient.Diagnosis || 'N/A'}
        </p>

        <p>
          <strong>Próxima cita:</strong>
          <time>
            ${fmtDate(patient.NextVisit)}
          </time>
        </p>

        <p>
          <strong>Alergias:</strong>
          ${patient.AlergiasTexto || 'Ninguna'}
        </p>

        <p>
          <strong>Estado:</strong>
          ${patient.Estado || 'N/A'}
        </p>

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
                    ${entry.Procedure}
                  </strong>
                </div>

                <div
                  style="
                    font-size:.85rem;
                    color:var(--text-muted)
                  "
                >
                  Dr. ${entry.Doctor}
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

    content.innerHTML = `
      <form id="editPatientForm">

        <div class="form-group">

          <label
            class="form-label"
            for="editNombres"
          >
            Nombres
          </label>

          <input
            type="text"
            id="editNombres"
            class="form-input"
            value="${nombresActuales}"
            required
          />

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editApellidos"
          >
            Apellidos
          </label>

          <input
            type="text"
            id="editApellidos"
            class="form-input"
            value="${apellidosActuales}"
            required
          />

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editTelefono"
          >
            Teléfono
          </label>

          <input
            type="text"
            id="editTelefono"
            class="form-input"
            value="${patient.Telefono || ''}"
          />

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editCorreo"
          >
            Correo
          </label>

          <input
            type="email"
            id="editCorreo"
            class="form-input"
            value="${patient.Correo || ''}"
          />

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editCiudad"
          >
            Ciudad
          </label>

          <input
            type="text"
            id="editCiudad"
            class="form-input"
            value="${patient.Ciudad || ''}"
          />

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editGenero"
          >
            Género
          </label>

          <select
            id="editGenero"
            class="form-select"
          >

            <option
              value="M"
              ${patient.Genero === 'M' ? 'selected' : ''}
            >
              Masculino
            </option>

            <option
              value="F"
              ${patient.Genero === 'F' ? 'selected' : ''}
            >
              Femenino
            </option>

            <option
              value="O"
              ${patient.Genero === 'O' ? 'selected' : ''}
            >
              Otro
            </option>

          </select>

        </div>

        <div class="form-group">

          <label
            class="form-label"
            for="editAlergias"
          >
            Alergias
          </label>

          <input
            type="text"
            id="editAlergias"
            class="form-input"
            value="${patient.AlergiasTexto || ''}"
          />

        </div>

        <div
          style="
            display:flex;
            gap:10px;
            justify-content:flex-end;
            margin-top:20px;
          "
        >

          <button
            type="button"
            class="btn-secondary"
            id="editCancelBtn"
          >
            Cancelar
          </button>

          <button
            type="submit"
            class="btn-primary"
            id="editSaveBtn"
          >
            Guardar cambios
          </button>

        </div>

      </form>
    `;

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

        data.append(
          'estado',
          patient.Estado || 'activo'
        );

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

const initSearch = () => {

  const searchInput =
    safeGetElement(
      'searchPatients'
    );

  searchInput?.addEventListener(
    'input',
    debounce(
      (event) => {

        searchQuery =
          event.target.value
            .toLowerCase();

        currentPage = 1;

        renderPatients();
      },
      250
    )
  );
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

const initPagination = () => {

  const btnPrev =
    safeGetElement(
      'btnPrev'
    );

  const btnNext =
    safeGetElement(
      'btnNext'
    );

  btnPrev?.addEventListener(
    'click',
    () => {

      if (currentPage > 1) {

        currentPage--;

        renderPatients();
      }
    }
  );

  btnNext?.addEventListener(
    'click',
    () => {

      const filtered =
        getFilteredPatients();

      const maxPage =
        Math.max(
          1,
          Math.ceil(
            filtered.length /
            itemsPerPage
          )
        );

      if (
        currentPage <
        maxPage
      ) {

        currentPage++;

        renderPatients();
      }
    }
  );
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

const initFilters = () => {

  const applyFilters =
    () => {

      currentPage = 1;

      renderPatients();

      const hasActive =
        filterAlergias ||
        filterCita ||
        filterHistorial;

      const clearBtn =
        safeGetElement(
          'btnClearFilters'
        );

      if (clearBtn) {

        clearBtn.classList.toggle(
          'active',
          !!hasActive
        );
      }
    };

  safeGetElement(
    'filterAlergias'
  )?.addEventListener(
    'change',
    (e) => {

      filterAlergias =
        e.target.value;

      applyFilters();
    }
  );

  safeGetElement(
    'filterCita'
  )?.addEventListener(
    'change',
    (e) => {

      filterCita =
        e.target.value;

      applyFilters();
    }
  );

  safeGetElement(
    'filterHistorial'
  )?.addEventListener(
    'change',
    (e) => {

      filterHistorial =
        e.target.value;

      applyFilters();
    }
  );

  safeGetElement(
    'btnClearFilters'
  )?.addEventListener(
    'click',
    () => {

      filterAlergias = '';
      filterCita = '';
      filterHistorial = '';

      const sel1 =
        safeGetElement(
          'filterAlergias'
        );

      const sel2 =
        safeGetElement(
          'filterCita'
        );

      const sel3 =
        safeGetElement(
          'filterHistorial'
        );

      if (sel1) {
        sel1.value = '';
      }

      if (sel2) {
        sel2.value = '';
      }

      if (sel3) {
        sel3.value = '';
      }

      applyFilters();
    }
  );
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = () => {

  initSidebar();

  initNavGroups();

  initSearch();

  initPagination();

  initModal();

  initFilters();

  animateCounters();

  renderPatients();
};

document.addEventListener(
  'DOMContentLoaded',
  init
);