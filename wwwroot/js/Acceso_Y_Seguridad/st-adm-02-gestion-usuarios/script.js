const API_BASE = '/api/admin/usuarios';
const SAMPLE_USERS = [];

const safeGetElement = (id) => document.getElementById(id);

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn(...args), delay);
  };
};

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className =
    `toast ${
      type === 'error'
        ? 'error'
        : type === 'warning'
          ? 'warning'
          : ''
    } show`;

  clearTimeout(toast._timeoutId);

  toast._timeoutId = setTimeout(() => {
    toast.classList.remove('show');
  }, 3500);
};

let users = Array.isArray(window.RAZOR_USERS)
  ? window.RAZOR_USERS
  : [];

let searchQuery = '';
let selectedRole = '';
let selectedStatus = '';
let currentPage = 1;
let editingUserId = null;

const itemsPerPage = 5;

/* ================================================================
   CSRF
================================================================ */

const getCsrfToken = () =>
  document.querySelector(
    '#formAddUser input[name="__RequestVerificationToken"]'
  )?.value || '';

/* ================================================================
   API
================================================================ */

const apiFetch = async (url, options = {}) => {
  const headers = new Headers(options.headers || {});

  headers.set('Accept', 'application/json');

  if (
    options.body &&
    typeof options.body !== 'string'
  ) {
    headers.set('Content-Type', 'application/json');
    options.body = JSON.stringify(options.body);
  }

  const csrf = getCsrfToken();

  if (csrf) {
    headers.set('X-CSRF-TOKEN', csrf);
  }

  const response = await fetch(url, {
    credentials: 'same-origin',
    ...options,
    headers
  });

  const contentType =
    response.headers.get('content-type') || '';

  const payload = contentType.includes('application/json')
    ? await response.json()
    : {
        success: response.ok,
        message: await response.text()
      };

  if (!response.ok || payload.success === false) {
    throw new Error(
      payload.message ||
      `Error HTTP ${response.status}`
    );
  }

  return payload;
};

/* ================================================================
   ESTADOS
================================================================ */

const normalizeStatus = (status) => {
  const value = String(status || '').toLowerCase();

  if (value === 'bloqueado') {
    return 'Bloqueado';
  }

  return value === 'activo'
    ? 'Activo'
    : 'Inactivo';
};

/* ================================================================
   CONTRASEÑA
================================================================ */

const PASSWORD_RULES = [
  {
    id: 'passwordRuleLength',
    label: 'Mínimo 8 caracteres',
    test: (value) => value.length >= 8
  },
  {
    id: 'passwordRuleUppercase',
    label: 'Una letra mayúscula',
    test: (value) => /[A-Z]/.test(value)
  },
  {
    id: 'passwordRuleLowercase',
    label: 'Una letra minúscula',
    test: (value) => /[a-z]/.test(value)
  },
  {
    id: 'passwordRuleNumber',
    label: 'Un número',
    test: (value) => /\d/.test(value)
  },
  {
    id: 'passwordRuleSymbol',
    label: 'Un símbolo especial',
    test: (value) => /[^A-Za-z\d]/.test(value)
  }
];

const isPasswordValid = (password) =>
  PASSWORD_RULES.every((rule) => rule.test(password));

const createPasswordRequirements = () => {
  const passwordInput = safeGetElement('userPassword');

  if (!passwordInput) {
    return null;
  }

  let requirements =
    safeGetElement('passwordRequirements');

  if (requirements) {
    return requirements;
  }

  requirements = document.createElement('div');
  requirements.id = 'passwordRequirements';

  requirements.style.marginTop = '8px';
  requirements.style.fontSize = '12px';
  requirements.style.lineHeight = '1.6';

  const title = document.createElement('div');
  title.textContent = 'La contraseña debe cumplir:';
  title.style.fontWeight = '600';
  title.style.marginBottom = '4px';

  requirements.appendChild(title);

  PASSWORD_RULES.forEach((rule) => {
    const row = document.createElement('div');

    row.id = rule.id;

    row.dataset.valid = 'false';

    row.style.display = 'flex';
    row.style.alignItems = 'center';
    row.style.gap = '6px';
    row.style.color = '#6b7280';

    const icon = document.createElement('span');

    icon.className = 'password-rule-icon';
    icon.textContent = '✗';
    icon.style.fontWeight = '700';

    const text = document.createElement('span');

    text.textContent = rule.label;

    row.appendChild(icon);
    row.appendChild(text);

    requirements.appendChild(row);
  });

const passwordWrapper =
  passwordInput.parentElement?.dataset.passwordWrapper === 'true'
    ? passwordInput.parentElement
    : null;

if (passwordWrapper) {
  passwordWrapper.insertAdjacentElement(
    'afterend',
    requirements
  );
} else {
  passwordInput.insertAdjacentElement(
    'afterend',
    requirements
  );
}

  return requirements;
};

const createPasswordToggle = () => {
  const passwordInput = safeGetElement('userPassword');

  if (!passwordInput) {
    return;
  }

  let button = safeGetElement('togglePasswordVisibility');

  /*
   * Crear un wrapper exclusivamente para el input
   * y el botón de visibilidad.
   */
  let wrapper = passwordInput.parentElement;

  if (
    !wrapper ||
    wrapper.dataset.passwordWrapper !== 'true'
  ) {
    wrapper = document.createElement('div');

    wrapper.dataset.passwordWrapper = 'true';

    wrapper.style.setProperty(
      'position',
      'relative',
      'important'
    );

    wrapper.style.setProperty(
      'width',
      '100%',
      'important'
    );

    wrapper.style.setProperty(
      'height',
      'auto',
      'important'
    );

    wrapper.style.setProperty(
      'display',
      'block',
      'important'
    );

    passwordInput.parentNode.insertBefore(
      wrapper,
      passwordInput
    );

    wrapper.appendChild(passwordInput);
  }

  /*
   * Configuración del input.
   */
  passwordInput.style.setProperty(
    'width',
    '100%',
    'important'
  );

  passwordInput.style.setProperty(
    'box-sizing',
    'border-box',
    'important'
  );

  passwordInput.style.setProperty(
    'padding-right',
    '46px',
    'important'
  );

  /*
   * Si el botón ya existe en el HTML,
   * lo reutilizamos.
   */
  if (!button) {
    button = document.createElement('button');

    button.type = 'button';
    button.id = 'togglePasswordVisibility';

    button.textContent = '👁';

    wrapper.appendChild(button);

    button.addEventListener('click', () => {
      const visible =
        passwordInput.type === 'text';

      passwordInput.type =
        visible ? 'password' : 'text';

      button.textContent =
        visible ? '👁' : '🙈';

      button.setAttribute(
        'aria-label',
        visible
          ? 'Mostrar contraseña'
          : 'Ocultar contraseña'
      );

      button.setAttribute(
        'aria-pressed',
        String(!visible)
      );
    });
  } else {
    /*
     * Mover el botón existente al wrapper.
     */
    wrapper.appendChild(button);
  }

  /*
   * POSICIÓN REAL DEL BOTÓN
   * Dentro del input, lado derecho.
   */
  button.style.setProperty(
    'position',
    'absolute',
    'important'
  );

  button.style.setProperty(
    'right',
    '6px',
    'important'
  );

  button.style.setProperty(
    'top',
    '50%',
    'important'
  );

  button.style.setProperty(
    'transform',
    'translateY(-50%)',
    'important'
  );

  button.style.setProperty(
    'height',
    '30px',
    'important'
  );

  button.style.setProperty(
    'width',
    '34px',
    'important'
  );

  button.style.setProperty(
    'display',
    'flex',
    'important'
  );

  button.style.setProperty(
    'align-items',
    'center',
    'important'
  );

  button.style.setProperty(
    'justify-content',
    'center',
    'important'
  );

  button.style.setProperty(
    'border',
    '0',
    'important'
  );

  button.style.setProperty(
    'background',
    'transparent',
    'important'
  );

  button.style.setProperty(
    'padding',
    '0',
    'important'
  );

  button.style.setProperty(
    'margin',
    '0',
    'important'
  );

  button.style.setProperty(
    'cursor',
    'pointer',
    'important'
  );

  button.style.setProperty(
    'font-size',
    '16px',
    'important'
  );

  button.style.setProperty(
    'line-height',
    '1',
    'important'
  );

  button.style.setProperty(
    'z-index',
    '100',
    'important'
  );

  button.setAttribute(
    'aria-label',
    passwordInput.type === 'text'
      ? 'Ocultar contraseña'
      : 'Mostrar contraseña'
  );

  button.setAttribute(
    'aria-pressed',
    String(
      passwordInput.type === 'text'
    )
  );
};

const updatePasswordRequirements = () => {
  const passwordInput =
    safeGetElement('userPassword');

  const saveButton =
    safeGetElement('btnSaveUser');

  if (!passwordInput) {
    return;
  }

  const value = passwordInput.value || '';

  const requirements =
    createPasswordRequirements();

  if (!requirements) {
    return;
  }

  PASSWORD_RULES.forEach((rule) => {
    const row = safeGetElement(rule.id);

    if (!row) {
      return;
    }

    const icon =
      row.querySelector('.password-rule-icon');

    const valid =
      rule.test(value);

    row.dataset.valid =
      String(valid);

    row.style.color =
      valid
        ? '#15803d'
        : '#b91c1c';

    if (icon) {
      icon.textContent =
        valid ? '✓' : '✗';
    }
  });

  const valid =
    isPasswordValid(value);

  const editing =
    Boolean(editingUserId);

  /*
   * Al editar:
   * - contraseña vacía = mantener la actual
   * - contraseña escrita = debe cumplir las reglas
   */
  const allowed =
    editing
      ? value.length === 0 || valid
      : valid;

  if (saveButton) {
    saveButton.disabled = !allowed;

    saveButton.style.opacity =
      allowed ? '1' : '0.6';

    saveButton.style.cursor =
      allowed ? 'pointer' : 'not-allowed';
  }

  const help =
    safeGetElement('userPasswordHelp');

  if (help) {
    if (!editing) {
      help.textContent =
        valid
          ? 'Contraseña válida.'
          : 'Completa todos los requisitos para continuar.';

      help.style.color =
        valid ? '#15803d' : '#b91c1c';
    } else {
      if (value.length === 0) {
        help.textContent =
          'Déjala vacía para conservar la contraseña actual.';
        help.style.color = '#6b7280';
      } else {
        help.textContent =
          valid
            ? 'La nueva contraseña cumple todos los requisitos.'
            : 'Completa todos los requisitos para cambiar la contraseña.';
        help.style.color =
          valid ? '#15803d' : '#b91c1c';
      }
    }
  }
};

const initPasswordControls = () => {
  createPasswordToggle();
  createPasswordRequirements();

  const passwordInput =
    safeGetElement('userPassword');

  if (!passwordInput) {
    return;
  }

  passwordInput.addEventListener(
    'input',
    updatePasswordRequirements
  );

  updatePasswordRequirements();
};

/* ================================================================
   ESTADÍSTICAS
================================================================ */

const updateStats = () => {
  const total = users.length;

  const active = users.filter(
    (u) =>
      normalizeStatus(u.status) === 'Activo'
  ).length;

  const blocked = users.filter(
    (u) =>
      normalizeStatus(u.status) === 'Bloqueado'
  ).length;

  const inactive =
    total - active - blocked;

  const set = (id, value) => {
    const element = safeGetElement(id);

    if (element) {
      element.textContent = value;
    }
  };

  set('statTotal', total);
  set('statActive', active);
  set('statBlocked', blocked);
  set('statInactive', inactive);
};

/* ================================================================
   BADGES
================================================================ */

const getRoleBadgeClass = (role) => ({
  Administrador: 'admin',
  Recepcionista: 'recep',
  Profesional: 'prof',
  Auxiliar: 'aux',
  Paciente: 'paciente'
}[role] || 'paciente');

const getStatusBadgeClass = (status) => {
  const normalized =
    normalizeStatus(status);

  if (normalized === 'Bloqueado') {
    return 'bloqueado';
  }

  return normalized === 'Activo'
    ? 'activo'
    : 'inactivo';
};

/* ================================================================
   ÚLTIMO ACCESO
================================================================ */

const fmtLastAccess = (iso) => {
  if (!iso) {
    return 'Nunca';
  }

  const date = new Date(iso);

  if (Number.isNaN(date.getTime())) {
    return 'Nunca';
  }

  const now = new Date();

  const diffMins = Math.max(
    0,
    Math.floor(
      (now - date) / 60000
    )
  );

  if (diffMins < 60) {
    return `Hace ${diffMins} min`;
  }

  const diffHours =
    Math.floor(diffMins / 60);

  if (diffHours < 24) {
    return `Hace ${diffHours} h`;
  }

  const diffDays =
    Math.floor(diffHours / 24);

  if (diffDays < 7) {
    return `Hace ${diffDays} d`;
  }

  return date.toLocaleDateString(
    'es-CO',
    {
      day: 'numeric',
      month: 'short'
    }
  );
};

/* ================================================================
   PAGINACIÓN
================================================================ */

const updatePagination = (total) => {
  const info =
    safeGetElement('paginationInfo');

  const buttons =
    safeGetElement('paginationButtons');

  if (!info || !buttons) {
    return;
  }

  const totalPages =
    Math.max(
      1,
      Math.ceil(
        total / itemsPerPage
      )
    );

  currentPage =
    Math.min(
      currentPage,
      totalPages
    );

  const start =
    total === 0
      ? 0
      : (currentPage - 1) *
          itemsPerPage +
        1;

  const end =
    Math.min(
      currentPage * itemsPerPage,
      total
    );

  info.textContent =
    `Mostrando ${start}-${end} de ${total} usuarios`;

  buttons.innerHTML = '';

  const addButton = (
    text,
    label,
    disabled,
    active,
    handler
  ) => {
    const btn =
      document.createElement('button');

    btn.type = 'button';
    btn.textContent = text;
    btn.setAttribute(
      'aria-label',
      label
    );

    btn.disabled =
      disabled;

    if (active) {
      btn.classList.add('active');
    }

    btn.addEventListener(
      'click',
      handler
    );

    buttons.appendChild(btn);
  };

  addButton(
    '«',
    'Página anterior',
    currentPage === 1,
    false,
    () => {
      currentPage--;
      renderTable(users);
    }
  );

  for (
    let page = 1;
    page <= totalPages;
    page++
  ) {
    addButton(
      String(page),
      `Ir a página ${page}`,
      false,
      page === currentPage,
      () => {
        currentPage = page;
        renderTable(users);
      }
    );
  }

  addButton(
    '»',
    'Página siguiente',
    currentPage === totalPages,
    false,
    () => {
      currentPage++;
      renderTable(users);
    }
  );
};

/* ================================================================
   ESCAPE HTML
================================================================ */

const escapeHtml = (value) =>
  String(value ?? '').replace(
    /[&<>'"]/g,
    (c) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      "'": '&#39;',
      '"': '&quot;'
    }[c])
  );

/* ================================================================
   TABLA
================================================================ */

const renderTable = (data) => {
  const tbody =
    safeGetElement('usersTbody');

  if (!tbody) {
    return;
  }

  const filtered =
    data.filter((u) => {
      const text =
        `${u.name || ''} ${u.email || ''}`
          .toLowerCase();

      return (
        (!searchQuery ||
          text.includes(
            searchQuery.toLowerCase()
          )) &&
        (!selectedRole ||
          u.role === selectedRole) &&
        (!selectedStatus ||
          normalizeStatus(u.status) ===
            selectedStatus)
      );
    });

  const totalPages =
    Math.max(
      1,
      Math.ceil(
        filtered.length /
          itemsPerPage
      )
    );

  currentPage =
    Math.min(
      currentPage,
      totalPages
    );

  const start =
    (currentPage - 1) *
    itemsPerPage;

  const paginated =
    filtered.slice(
      start,
      start + itemsPerPage
    );

  tbody.innerHTML = '';

  if (!paginated.length) {
    tbody.innerHTML =
      '<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No se encontraron usuarios.</td></tr>';

    updatePagination(0);
    return;
  }

  for (const u of paginated) {
    const tr =
      document.createElement('tr');

    const status =
      normalizeStatus(u.status);

    const blocked =
      status === 'Bloqueado';

    tr.innerHTML = `
      <td class="td-usuario">
        <div
          class="u-avatar"
          style="background:var(--${u.color || 'blue'}-500)"
          aria-hidden="true"
        >
          ${u.initials || '?'}
        </div>

        <span class="u-name">
          ${escapeHtml(u.name || '')}
        </span>
      </td>

      <td>
        ${escapeHtml(u.email || '')}
      </td>

      <td>
        <span
          class="badge-role ${getRoleBadgeClass(u.role)}"
        >
          ${escapeHtml(u.role || 'Sin Rol')}
        </span>
      </td>

      <td>
        <span
          class="badge-status ${getStatusBadgeClass(status)}"
        >
          ${status}
        </span>
      </td>

      <td>
        <time>
          ${fmtLastAccess(u.lastAccess)}
        </time>
      </td>

      <td>
        <div class="actions-cell">

          <button
            class="btn-icon view"
            title="Ver detalles"
            onclick="viewUser(${u.id})"
          >
            👁️
          </button>

          <button
            class="btn-icon edit"
            title="Editar usuario"
            onclick="editUser(${u.id})"
          >
            ✏️
          </button>

          <button
            class="btn-icon lock"
            title="${blocked ? 'Activar' : 'Desactivar'}"
            onclick="toggleStatus(${u.id})"
          >
            ${blocked ? '🔓' : '🔒'}
          </button>

        </div>
      </td>
    `;

    tbody.appendChild(tr);
  }

  updatePagination(filtered.length);
};

/* ================================================================
   GET USERS
================================================================ */

const fetchUsers = async () => {
  const response =
    await apiFetch(
      API_BASE,
      {
        method: 'GET'
      }
    );

  return Array.isArray(response.data)
    ? response.data
    : [];
};

/* ================================================================
   MODAL
================================================================ */

const openModal = (user = null) => {
  const modal =
    safeGetElement('modalAddUser');

  const form =
    safeGetElement('formAddUser');

  if (!modal || !form) {
    return;
  }

  editingUserId =
    user?.id ?? null;

  safeGetElement('userId').value =
    editingUserId ?? '';

  safeGetElement('modalTitle').textContent =
    editingUserId
      ? 'Editar Usuario'
      : 'Crear Nuevo Usuario';

  safeGetElement('btnSaveUser').textContent =
    editingUserId
      ? 'Guardar cambios'
      : 'Crear usuario';

  safeGetElement('userName').value =
    user
      ? (user.name || '').split(' ')[0]
      : '';

  safeGetElement('userLastName').value =
    user
      ? (user.name || '')
          .split(' ')
          .slice(1)
          .join(' ')
      : '';

  safeGetElement('userEmail').value =
    user?.email || '';

  const passwordInput =
    safeGetElement('userPassword');

  passwordInput.value = '';

  safeGetElement('userRole').value =
    user?.role || '';

  safeGetElement('userStatus').value =
    normalizeStatus(
      user?.status || 'Activo'
    );

  const role =
    safeGetElement('userRole');

  [...role.options].forEach(
    (o) => {
      o.disabled = false;
    }
  );

  if (!editingUserId) {
    const adminOption =
      [...role.options].find(
        (o) =>
          o.value === 'Administrador'
      );

    if (adminOption) {
      adminOption.disabled = true;
    }
  }

  if (
    editingUserId &&
    [
      'Administrador',
      'Profesional',
      'Paciente'
    ].includes(user?.role)
  ) {
    role.disabled = true;
  }

  passwordInput.required =
    !editingUserId;

  safeGetElement('userPasswordHelp').textContent =
    editingUserId
      ? 'Déjala vacía para conservar la contraseña actual.'
      : 'Obligatoria al crear.';

  modal.classList.add('open');

  modal.setAttribute(
    'aria-hidden',
    'false'
  );

  modal.removeAttribute('inert');

  document.body.style.overflow =
    'hidden';

  safeGetElement('userName')?.focus();

  updatePasswordRequirements();
};

const closeModal = () => {
  const modal =
    safeGetElement('modalAddUser');

  if (!modal) {
    return;
  }

  modal.classList.remove('open');

  modal.setAttribute(
    'aria-hidden',
    'true'
  );

  modal.setAttribute(
    'inert',
    ''
  );

  document.body.style.overflow = '';

  safeGetElement('formAddUser')?.reset();

  editingUserId = null;

  /*
   * Recalcular el estado del botón después del reset.
   */
  updatePasswordRequirements();
};

/* ================================================================
   VER USUARIO
================================================================ */

window.viewUser = (id) => {
  const user = users.find(
    (u) => u.id === id
  );

  if (!user) {
    return;
  }

  const existingModal =
    document.getElementById('userDetailsModal');

  if (existingModal) {
    existingModal.remove();
  }

  const modal =
    document.createElement('div');

  modal.id = 'userDetailsModal';
  modal.className = 'modal-overlay open';

  modal.setAttribute('role', 'dialog');
  modal.setAttribute('aria-modal', 'true');
  modal.setAttribute('aria-labelledby', 'userDetailsTitle');

  modal.innerHTML = `
    <div class="modal">
      <button
        type="button"
        class="modal-close"
        id="closeUserDetails"
        aria-label="Cerrar detalles"
      >
        ×
      </button>

      <h2
        class="modal-title"
        id="userDetailsTitle"
      >
        Detalles del usuario
      </h2>

      <div style="display:grid;gap:12px;margin-top:20px;">

        <div>
          <strong>Nombre</strong>
          <div>${escapeHtml(user.name || '')}</div>
        </div>

        <div>
          <strong>Correo</strong>
          <div>${escapeHtml(user.email || '')}</div>
        </div>

        <div>
          <strong>Rol</strong>
          <div>
            ${escapeHtml(user.role || 'Sin rol')}
          </div>
        </div>

        <div>
          <strong>Estado</strong>
          <div>
            ${escapeHtml(
              normalizeStatus(user.status)
            )}
          </div>
        </div>

        <div>
          <strong>Último acceso</strong>
          <div>
            ${escapeHtml(
              fmtLastAccess(user.lastAccess)
            )}
          </div>
        </div>

        <div>
          <strong>ID de usuario</strong>
          <div>${user.id}</div>
        </div>

      </div>

      <div class="modal-footer">
        <button
          type="button"
          class="btn-secondary"
          id="closeUserDetailsFooter"
        >
          Cerrar
        </button>
      </div>
    </div>
  `;

  document.body.appendChild(modal);

  document.body.style.overflow = 'hidden';

  const close = () => {
    modal.remove();
    document.body.style.overflow = '';
  };

  document
    .getElementById('closeUserDetails')
    ?.addEventListener('click', close);

  document
    .getElementById('closeUserDetailsFooter')
    ?.addEventListener('click', close);

  modal.addEventListener('click', (event) => {
    if (event.target === modal) {
      close();
    }
  });
};


/* ================================================================
   EDITAR
================================================================ */

window.editUser = (id) => {
  const user =
    users.find(
      (u) => u.id === id
    );

  if (user) {
    openModal(user);
  }
};

/* ================================================================
   CAMBIAR ESTADO
================================================================ */

window.toggleStatus = async (id) => {
  const user =
    users.find(
      (u) => u.id === id
    );

  if (!user) {
    return;
  }

  const currentStatus =
    normalizeStatus(
      user.status
    );

  const next =
    currentStatus === 'Activo' ||
    currentStatus === 'Bloqueado'
      ? 'Inactivo'
      : 'Activo';

  try {
    const response =
      await apiFetch(
        `${API_BASE}/${id}/estado`,
        {
          method: 'POST',
          body: {
            estado: next
          }
        }
      );

    user.status =
      response.data.status;

    updateStats();
    renderTable(users);

    showToast(
      response.message ||
      'Estado actualizado.'
    );
  } catch (error) {
    showToast(
      error.message,
      'error'
    );
  }
};

/* ================================================================
   SUBMIT
================================================================ */

const submitUser = async (event) => {
  event.preventDefault();

  const name =
    safeGetElement(
      'userName'
    ).value.trim();

  const lastName =
    safeGetElement(
      'userLastName'
    ).value.trim();

  const email =
    safeGetElement(
      'userEmail'
    ).value.trim();

  const password =
    safeGetElement(
      'userPassword'
    ).value;

  const role =
    safeGetElement(
      'userRole'
    ).value;

  const status =
    safeGetElement(
      'userStatus'
    ).value;

  /*
   * Validaciones básicas.
   */
  if (
    !name ||
    !lastName ||
    !email ||
    !role ||
    (!editingUserId && !password)
  ) {
    showToast(
      'Completa los campos obligatorios.',
      'warning'
    );

    return;
  }

  /*
   * Validación fuerte de contraseña.
   */
  if (
    password &&
    !isPasswordValid(password)
  ) {
    updatePasswordRequirements();

    showToast(
      'La contraseña no cumple todos los requisitos.',
      'warning'
    );

    return;
  }

  const payload = {
    nombre: name,
    apellidos: lastName,
    correo: email,
    idRol: roleToId(role),
    estado: status
  };

  if (password) {
    payload.contrasena =
      password;
  }

  const saveButton =
    safeGetElement(
      'btnSaveUser'
    );

  if (saveButton) {
    saveButton.disabled = true;
    saveButton.textContent =
      editingUserId
        ? 'Guardando...'
        : 'Creando...';
  }

  try {
    const response =
      editingUserId
        ? await apiFetch(
            `${API_BASE}/${editingUserId}`,
            {
              method: 'PUT',
              body: payload
            }
          )
        : await apiFetch(
            API_BASE,
            {
              method: 'POST',
              body: payload
            }
          );

    if (editingUserId) {
      const index =
        users.findIndex(
          (u) =>
            u.id ===
            editingUserId
        );

      if (index >= 0) {
        users[index] =
          response.data;
      }
    } else {
      users.unshift(
        response.data
      );
    }

    closeModal();

    currentPage = 1;

    updateStats();
    renderTable(users);

    showToast(
      response.message ||
      'Operación realizada correctamente.'
    );
  } catch (error) {
    showToast(
      error.message ||
      'No se pudo completar la operación.',
      'error'
    );

    /*
     * El modal permanece abierto para que
     * el usuario pueda corregir los datos.
     */
  } finally {
    if (saveButton) {
      updatePasswordRequirements();

      if (!editingUserId) {
        saveButton.textContent =
          'Crear usuario';
      } else {
        saveButton.textContent =
          'Guardar cambios';
      }
    }
  }
};

/* ================================================================
   ROLES
================================================================ */

const roleToId = (role) => ({
  Administrador: 1,
  Profesional: 2,
  Auxiliar: 3,
  Recepcionista: 4,
  Paciente: 5
}[role] || 0);

/* ================================================================
   FILTROS
================================================================ */

const initFilters = () => {
  safeGetElement(
    'searchUser'
  )?.addEventListener(
    'input',
    debounce(
      (e) => {
        searchQuery =
          e.target.value.trim();

        currentPage = 1;

        renderTable(users);
      },
      250
    )
  );

  safeGetElement(
    'filterRole'
  )?.addEventListener(
    'change',
    (e) => {
      selectedRole =
        e.target.value;

      currentPage = 1;

      renderTable(users);
    }
  );

  safeGetElement(
    'filterStatus'
  )?.addEventListener(
    'change',
    (e) => {
      selectedStatus =
        e.target.value;

      currentPage = 1;

      renderTable(users);
    }
  );
};

/* ================================================================
   MODAL INIT
================================================================ */

const initModal = () => {
  safeGetElement(
    'btnAddUser'
  )?.addEventListener(
    'click',
    () => openModal()
  );

  safeGetElement(
    'modalClose'
  )?.addEventListener(
    'click',
    closeModal
  );

  safeGetElement(
    'modalCancel'
  )?.addEventListener(
    'click',
    closeModal
  );

  safeGetElement(
    'modalAddUser'
  )?.addEventListener(
    'click',
    (e) => {
      if (
        e.target ===
        e.currentTarget
      ) {
        closeModal();
      }
    }
  );

  safeGetElement(
    'formAddUser'
  )?.addEventListener(
    'submit',
    submitUser
  );
};

/* ================================================================
   SIDEBAR
================================================================ */

const initSidebar = () => {
  const hamburger =
    safeGetElement('hamburger');

  const sidebar =
    safeGetElement('sidebar');

  const overlay =
    safeGetElement('overlay');

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
    () => toggleMenu(true)
  );

  overlay.addEventListener(
    'click',
    () => toggleMenu(false)
  );
};

/* ================================================================
   INIT
================================================================ */

const init = async () => {
  initSidebar();
  initFilters();
  initModal();
  initPasswordControls();

  try {
    users =
      await fetchUsers();
  } catch (error) {
    console.error(
      '[SmileTrack] No se pudieron cargar usuarios:',
      error
    );

    showToast(
      'No se pudieron cargar los usuarios desde el servidor.',
      'error'
    );

    users = [];
  }

  updateStats();
  renderTable(users);
};

document.addEventListener(
  'DOMContentLoaded',
  init
);