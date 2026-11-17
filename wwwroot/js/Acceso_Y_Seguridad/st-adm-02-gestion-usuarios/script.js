const API_BASE = '/api/v1/admin/usuarios';
const SAMPLE_USERS = [];

const safeGetElement = (id) => document.getElementById(id);
const translate = (key, variables) =>
  window.I18N.t(key, variables);

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn(...args), delay);
  };
};

const showToast = (message, type = 'success') => {
  const titles = {
    success: 'Éxito',
    error: 'Error',
    warning: 'Validación'
  };
  const title = titles[type];
  const toast = window.ToastService?.[type];

  if (!title || typeof toast !== 'function') {
    throw new Error(`No hay un servicio de notificaciones disponible para "${type}".`);
  }

  toast(title, message);
};

let users = Array.isArray(window.RAZOR_USERS)
  ? window.RAZOR_USERS
  : [];

let searchQuery = '';
let selectedRole = '';
let selectedStatus = '';
let currentPage = 1;
let editingUserId = null;
let userModalReturnFocus = null;
let userModalPreviousBodyOverflow = '';

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
   MAPEO API -> FORMATO LOCAL
   La API devuelve los campos en español (idUsuario, nombre, estado...).
   Esta función los traduce al formato que ya usa el resto del script
   ({id, name, initials, email, role, status, lastAccess, color}).
================================================================ */

const ROLE_COLORS = {
  Administrador: 'purple',
  Recepcionista: 'orange',
  Profesional: 'green',
  Auxiliar: 'pink',
  Paciente: 'blue'
};

const mapApiUser = (u) => {
  const nombre = u.nombre || '';
  const apellidos = u.apellidos || '';
  const estadoCapitalizado =
    u.estado
      ? u.estado.charAt(0).toUpperCase() + u.estado.slice(1)
      : 'Activo';

  return {
    id: u.idUsuario,
    name: `${nombre} ${apellidos}`.trim(),
    initials: `${nombre.charAt(0)}${apellidos.charAt(0)}`.toUpperCase(),
    email: u.correo,
    role: u.rol || 'Sin Rol',
    status: u.estaBloqueado ? 'Bloqueado' : estadoCapitalizado,
    lastAccess: u.ultimoLogin,
    color: ROLE_COLORS[u.rol] || 'blue'
  };
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
    wrapper.appendChild(button);

    button.addEventListener('click', () => {
      const visible =
        passwordInput.type === 'text';

      passwordInput.type =
        visible ? 'password' : 'text';

      updateToggleButtonState();
    });
  } else {
    /*
     * Mover el botón existente al wrapper.
     */
    wrapper.appendChild(button);
  }

  button.classList.add(
    'btn-icon',
    'password-visibility-toggle'
  );

  let icon =
    button.querySelector(
      '.material-symbols-outlined'
    );

  if (!icon) {
    icon = document.createElement('span');
    icon.className = 'material-symbols-outlined';
    icon.setAttribute('aria-hidden', 'true');
    button.replaceChildren(icon);
  }

  const updateToggleButtonState = () => {
    const visible =
      passwordInput.type === 'text';
    const label = translate(
      visible
        ? 'gestionusuarios_password_hide'
        : 'gestionusuarios_password_show'
    );

    icon.textContent =
      visible ? 'visibility_off' : 'visibility';
    button.setAttribute('aria-label', label);
    button.setAttribute('title', label);
    button.setAttribute(
      'aria-pressed',
      String(visible)
    );
  };

  updateToggleButtonState();
  document.addEventListener(
    'i18n:change',
    updateToggleButtonState
  );

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
    'z-index',
    '100',
    'important'
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
    return 'badge-bloqueado';
  }

  return normalized === 'Activo'
    ? 'badge-activo'
    : 'badge-inactivo';
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

  info.textContent = translate(
    'gestionusuarios_pagination_info',
    { start, end, total }
  );

  buttons.innerHTML = '';

  const addButton = (
    content,
    label,
    disabled,
    active,
    handler,
    icon = false
  ) => {
    const btn =
      document.createElement('button');

    btn.type = 'button';
    btn.className = 'pagination-btn';

    if (icon) {
      const iconElement =
        document.createElement('span');

      iconElement.className =
        'material-symbols-outlined';
      iconElement.setAttribute(
        'aria-hidden',
        'true'
      );
      iconElement.textContent = content;
      btn.appendChild(iconElement);
    } else {
      btn.textContent = content;
    }

    btn.setAttribute(
      'aria-label',
      label
    );

    btn.disabled =
      disabled;

    if (active) {
      btn.classList.add('active');
      btn.setAttribute('aria-current', 'page');
    }

    btn.addEventListener(
      'click',
      handler
    );

    buttons.appendChild(btn);
  };

  addButton(
    'chevron_left',
    translate('gestionusuarios_pagination_prev_aria'),
    currentPage === 1,
    false,
    () => {
      currentPage--;
      renderTable(users);
    },
    true
  );

  for (
    let page = 1;
    page <= totalPages;
    page++
  ) {
    addButton(
      String(page),
      translate(
        'gestionusuarios_pagination_page_aria',
        { page }
      ),
      false,
      page === currentPage,
      () => {
        currentPage = page;
        renderTable(users);
      }
    );
  }

  addButton(
    'chevron_right',
    translate('gestionusuarios_pagination_next_aria'),
    currentPage === totalPages,
    false,
    () => {
      currentPage++;
      renderTable(users);
    },
    true
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
      `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">${escapeHtml(translate('gestionusuarios_empty'))}</td></tr>`;

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
          class="u-avatar u-avatar--${u.color || 'blue'}"
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
            type="button"
            class="btn-secondary action-btn btn-view"
            title="${escapeHtml(translate('gestionusuarios_action_view_aria'))}"
            aria-label="${escapeHtml(translate('gestionusuarios_action_view_aria'))}"
            data-action="view"
            data-user-id="${u.id}"
          >
            <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span>
            <span class="btn-text">${escapeHtml(translate('gestionusuarios_action_view'))}</span>
          </button>

          <button
            type="button"
            class="btn-secondary action-btn btn-edit"
            title="${escapeHtml(translate('gestionusuarios_action_edit_aria'))}"
            aria-label="${escapeHtml(translate('gestionusuarios_action_edit_aria'))}"
            data-action="edit"
            data-user-id="${u.id}"
          >
            <span class="material-symbols-outlined action-icon" aria-hidden="true">edit</span>
            <span class="btn-text">${escapeHtml(translate('gestionusuarios_action_edit'))}</span>
          </button>

          <button type="button"
            class="${blocked ? 'btn-secondary' : 'btn-danger'} action-btn btn-delete"
            title="${escapeHtml(translate(blocked ? 'gestionusuarios_action_unblock_aria' : 'gestionusuarios_action_block_aria'))}"
            aria-label="${escapeHtml(translate(blocked ? 'gestionusuarios_action_unblock_aria' : 'gestionusuarios_action_block_aria'))}"
            data-action="toggle-block"
            data-user-id="${u.id}">
            <span class="material-symbols-outlined action-icon" aria-hidden="true">${blocked ? 'lock_open' : 'block'}</span>
            <span class="btn-text">${escapeHtml(translate(blocked ? 'gestionusuarios_action_unblock' : 'gestionusuarios_action_block'))}</span>
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
  const response = await apiFetch(API_BASE, { method: 'GET' });
  return Array.isArray(response.data)
    ? response.data.map(mapApiUser)
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

  userModalReturnFocus =
    document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
  userModalPreviousBodyOverflow =
    document.body.style.overflow;

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

  document.body.style.overflow =
    userModalPreviousBodyOverflow;

  safeGetElement('formAddUser')?.reset();

  editingUserId = null;

  /*
   * Recalcular el estado del botón después del reset.
   */
  updatePasswordRequirements();

  if (userModalReturnFocus?.isConnected) {
    userModalReturnFocus.focus();
  }
  userModalReturnFocus = null;
};

/* ================================================================
   VER USUARIO
================================================================ */

const viewUser = (id) => {
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

  const returnFocus =
    document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
  const previousBodyOverflow =
    document.body.style.overflow;

  modal.id = 'userDetailsModal';
  modal.className = 'modal-overlay open';

  modal.setAttribute('role', 'dialog');
  modal.setAttribute('aria-modal', 'true');
  modal.setAttribute('aria-labelledby', 'userDetailsTitle');
  modal.setAttribute('aria-hidden', 'false');

  modal.innerHTML = `
    <div class="modal">
      <button
        type="button"
        class="btn-icon modal-close"
        id="closeUserDetails"
        aria-label="${escapeHtml(translate('common.close'))}"
      >
        <span class="material-symbols-outlined" aria-hidden="true">close</span>
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
    document.body.style.overflow =
      previousBodyOverflow;
    if (returnFocus?.isConnected) {
      returnFocus.focus();
    }
  };

  document
    .getElementById('closeUserDetails')
    ?.addEventListener('click', close);

  document
    .getElementById('closeUserDetailsFooter')
    ?.addEventListener('click', close);

  document
    .getElementById('closeUserDetails')
    ?.focus();

  modal.addEventListener('click', (event) => {
    if (event.target === modal) {
      close();
    }
  });
};


/* ================================================================
   EDITAR
================================================================ */

const editUser = (id) => {
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
      ? 'inactivo'
      : 'activo';

    try {
      const response = await apiFetch(
      `${API_BASE}/${id}/estado`,
      { method: 'PATCH', body: { estado: next.toLowerCase() } }
      );

    user.status = mapApiUser(response.data).status;

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
   BLOQUEAR / DESBLOQUEAR USUARIO
   Alterna el estado de un usuario entre 'activo' y 'bloqueado'.
   Expuesto en window para ser llamado desde los botones inline del HTML
   generado dinámicamente en renderTable().
================================================================ */

const toggleBlock = async (id) => {
  const user = users.find((u) => u.id === id);
  if (!user) return;

  const currentStatus = normalizeStatus(user.status);
  const isBlocked = currentStatus === 'Bloqueado';
  const nextEstado = isBlocked ? 'activo' : 'bloqueado';

  try {
    const response = await apiFetch(
      `${API_BASE}/${id}/estado`,
      { method: 'PATCH', body: { estado: nextEstado } }
    );

    // Actualizar el estado local desde la respuesta mapeada
    user.status = mapApiUser(response.data).status;

    updateStats();
    renderTable(users);

    showToast(
      response.message ||
      (isBlocked ? 'Usuario desbloqueado.' : 'Usuario bloqueado.')
    );
  } catch (error) {
    showToast(error.message || 'No se pudo cambiar el estado del usuario.', 'error');
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
  let response;

  if (editingUserId) {
    // Actualizar datos básicos (nombre, apellidos, correo, rol) — sin contraseña ni estado
    response = await apiFetch(
      `${API_BASE}/${editingUserId}`,
      {
        method: 'PATCH',
        body: {
          nombre: name,
          apellidos: lastName,
          correo: email,
          idRol: roleToId(role)
        }
      }
    );

    // El estado tiene su propio endpoint
    await apiFetch(
      `${API_BASE}/${editingUserId}/estado`,
      { method: 'PATCH', body: { estado: status.toLowerCase() } }
    );

    // La contraseña también tiene su propio endpoint (solo si se escribió una nueva)
    if (password) {
      await apiFetch(
        `${API_BASE}/${editingUserId}/restablecer-contrasena`,
        { method: 'POST', body: { contrasenaTemporal: password } }
      );
    }

    const index = users.findIndex((u) => u.id === editingUserId);
    if (index >= 0) { users[index] = mapApiUser(response.data); }
  } else {
    // Crear usuario nuevo: nombre, apellidos, correo, contraseña, rol y estado sí van juntos
    response = await apiFetch(
      API_BASE,
      {
        method: 'POST',
        body: {
          nombre: name,
          apellidos: lastName,
          correo: email,
          contrasena: password,
          idRol: roleToId(role),
          estado: status.toLowerCase()
        }
      }
    );

    users.unshift(mapApiUser(response.data));
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
   ROLES (cargados desde la API, no hardcodeados)
================================================================ */

let ROLE_ID_MAP = {};

const loadRoleIds = async () => {
  try {
    const response = await apiFetch(`${API_BASE}/roles`, { method: 'GET' });

    ROLE_ID_MAP = {};

    (response.data || []).forEach((r) => {
      ROLE_ID_MAP[r.nombre] = r.idRol;
    });
  } catch (error) {
    console.error(
      '[SmileTrack] No se pudieron cargar los roles:',
      error
    );

    showToast(
      'No se pudieron cargar los roles disponibles.',
      'error'
    );
  }
};

const roleToId = (role) => ROLE_ID_MAP[role] || 0;

/* ================================================================
   FILTROS
================================================================ */

const initFilters = () => {
  const searchInput =
    safeGetElement('searchUser');
  const roleFilter =
    safeGetElement('filterRole');
  const statusFilter =
    safeGetElement('filterStatus');

  searchInput?.addEventListener(
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

  roleFilter?.addEventListener(
    'change',
    (e) => {
      selectedRole =
        e.target.value;

      currentPage = 1;

      renderTable(users);
    }
  );

  statusFilter?.addEventListener(
    'change',
    (e) => {
      selectedStatus =
        e.target.value;

      currentPage = 1;

      renderTable(users);
    }
  );

  safeGetElement(
    'userFiltersForm'
  )?.addEventListener(
    'submit',
    (event) => {
      event.preventDefault();
      searchQuery =
        searchInput?.value.trim() || '';
      selectedRole =
        roleFilter?.value || '';
      selectedStatus =
        statusFilter?.value || '';
      currentPage = 1;
      renderTable(users);
    }
  );

  safeGetElement(
    'btnClearFilters'
  )?.addEventListener(
    'click',
    () => {
      if (searchInput) {
        searchInput.value = '';
      }

      if (roleFilter) {
        roleFilter.value = '';
      }

      if (statusFilter) {
        statusFilter.value = '';
      }

      searchQuery = '';
      selectedRole = '';
      selectedStatus = '';
      currentPage = 1;
      renderTable(users);
      searchInput?.focus();
    }
  );

  document.addEventListener(
    'i18n:change',
    () => renderTable(users)
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

  document.addEventListener('keydown', (event) => {
    const modal = document.querySelector(
      '.modal-overlay.open[role="dialog"]'
    );
    if (!modal) return;

    if (event.key === 'Escape') {
      event.preventDefault();
      if (modal.id === 'userDetailsModal') {
        modal.querySelector('#closeUserDetails')?.click();
      } else {
        closeModal();
      }
      return;
    }

    if (event.key !== 'Tab') return;

    const focusable = Array.from(modal.querySelectorAll(
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(element =>
      !element.hidden && element.getClientRects().length > 0
    );
    if (!focusable.length) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!modal.contains(document.activeElement)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });

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

const initUserActions = () => {
  const tbody = safeGetElement('usersTbody');
  if (!tbody) {
    console.error('[SmileTrack][Usuarios] No se encontró la tabla de usuarios.');
    return;
  }

  tbody.addEventListener('click', (event) => {
    const target = event.target instanceof Element
      ? event.target.closest('button[data-action][data-user-id]')
      : null;

    if (!target || !tbody.contains(target)) return;

    const id = Number(target.dataset.userId);
    if (!Number.isSafeInteger(id) || id <= 0) return;

    switch (target.dataset.action) {
      case 'view':
        viewUser(id);
        break;
      case 'edit':
        editUser(id);
        break;
      case 'toggle-block':
        toggleBlock(id);
        break;
      default:
        console.error('[SmileTrack][Usuarios] Acción de tabla desconocida:', target.dataset.action);
    }
  });
};

/* ================================================================
   INIT
================================================================ */

const init = async () => {
  initSidebar();
  initFilters();
  initModal();
  initPasswordControls();
  initUserActions();
  
  await loadRoleIds()

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