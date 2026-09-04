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
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

/**
 * Obtiene un elemento DOM de forma segura.
 * Previene errores cuando el elemento no existe en la página.
 *
 * @param {string} id - ID del elemento
 * @returns {HTMLElement|null}
 */
const safeGetElement = (id) => {
  const element = document.getElementById(id);
  if (!element) {
    console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  }
  return element;
};
window.safeGetElement = safeGetElement;

/**
 * Realiza una petición fetch centralizada a la API con manejo de CSRF.
 */
async function apiRequest(url, options = {}) {
    const headers = new Headers(options.headers || {});
    headers.set('Accept', 'application/json');

    if (options.body && !headers.has('Content-Type')) {
        headers.set('Content-Type', 'application/json');
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) {
        headers.set('X-CSRF-TOKEN', token);
    }

    const response = await fetch(url, {
        ...options,
        headers,
        credentials: 'same-origin'
    });

    let data = null;
    try {
        data = await response.json();
    } catch { }

    if (!response.ok) {
        const message = data?.message || `Error HTTP ${response.status}.`;
        throw new Error(message);
    }

    return data;
}
/**
 * Ejecuta una función después de que el usuario deja de escribir.
 * Evita envíos repetidos de formulario o recargas en cada tecla.
 *
 * @param {Function} callback
 * @param {number} delay
 * @returns {Function}
 */
const debounce = (callback, delay = 250) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => callback.apply(this, args), delay);
  };
};
window.debounce = debounce;
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

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO Y UTILIDADES DE UI
// ═══════════════════════════════════════════════════════════════════

/**
 * Anima contador numérico de 0 al valor objetivo.
 * WHY: Mejora visual al cargar estadísticas — indica que el número es dinámico.
 * @param {HTMLElement} el
 * @param {number} target
 */
const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

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

    const escapeHtml = (unsafe) => (unsafe || '').toString()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");

    for (const p of items) {
        const tr = document.createElement('tr');
        tr.setAttribute('role', 'row');

        const name = `${escapeHtml(p.nombres)} ${escapeHtml(p.apellidos)}`.trim();

        // Especialidad principal: primera de la lista
        const especialidad = p.especialidades && p.especialidades.length > 0
            ? escapeHtml(p.especialidades[0].nombre)
            : '';

        const specClass = getSpecBadgeClass(especialidad);
        const statusClass = getStatusBadgeClass(p.estado);
        const avatarColor = getAvatarColor(especialidad);

        const initialN = p.nombres ? p.nombres.charAt(0).toUpperCase() : '';
        const initialA = p.apellidos ? p.apellidos.charAt(0).toUpperCase() : '';
        const initials = `${initialN}${initialA}`;

        const telefono = escapeHtml(p.telefono);
        const estadoText = escapeHtml(p.estado);
        const registroMedico = escapeHtml(p.registroMedico);

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
              <button class="btn-icon action-btn btn-view"
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
                👁️ <span class="btn-text">Ver</span>
              </button>
              <button class="btn-icon edit action-btn"
                      type="button"
                      aria-label="Editar el profesional ${name}"
                      title="Editar profesional ${name}"
                      onclick="editProfessional(${p.idProfesional})">
                ✏️ <span class="btn-text">Editar</span>
              </button>
              <button class="btn-icon toggle action-btn btn-delete"
                      type="button"
                      data-id="${p.idProfesional}"
                      data-name="${name}"
                      aria-label="Desactivar el profesional ${name}"
                      title="Desactivar profesional ${name}">
                ❌ <span class="btn-text">Eliminar</span>
              </button>
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
                safeGetElement('modalDetailClose')?.focus();
            }
        });

        // Enlazar el botón Eliminar al modal de confirmación
        const deleteBtn = tr.querySelector('.btn-delete');
        deleteBtn?.addEventListener('click', () => {
            openConfirmDeleteModal(p.idProfesional, name);
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

  const password = safeGetElement('formContrasenaAcceso');
  const formId = safeGetElement('formIdProfesional');
  const editing = Number(formId?.value || 0) > 0;

  if (password) {
    const value = password.value || '';
    const passwordValid = /^(?=.{8,100}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$/.test(value);
    if ((!editing && !passwordValid) || (editing && value && !passwordValid)) {
      valid = false;
      updateProfessionalPasswordRules();
    }
  }

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
    row.textContent = `${ok ? '✓' : '✗'} ${row.textContent.slice(2)}`;
    row.style.color = ok ? '#15803d' : '#b91c1c';
  });

  const formId = safeGetElement('formIdProfesional');
  const editing = Number(formId?.value || 0) > 0;
  const help = safeGetElement('formContrasenaHelp');
  const allValid = Object.values(rules).every(Boolean);

  if (help) {
    if (editing && value.length === 0) {
      help.textContent = 'Déjala vacía para conservar la contraseña actual.';
      help.style.color = '#6b7280';
    } else {
      help.textContent = allValid
        ? 'Contraseña válida.'
        : 'Completa todos los requisitos de la contraseña.';
      help.style.color = allValid ? '#15803d' : '#b91c1c';
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
  };

  // Solo incluir contraseña si el campo tiene valor (en edición es opcional)
  const passwordVal = getData('formContrasenaAcceso');
  if (passwordVal) payload.contrasenaAcceso = passwordVal;

  // H-05: capturar estado actual y original para decidir si hace falta el PATCH.
  // originalEstado se guarda en data-originalEstado por editProfessional() al abrir el modal.
  const statusSelect = safeGetElement('formStatus');
  const nuevoEstado = isEditing
    ? (statusSelect?.value || safeGetElement('formEstado')?.value || '').trim().toLowerCase()
    : null;
  const originalEstado = isEditing
    ? (statusSelect?.dataset.originalEstado || '').toLowerCase()
    : null;

  try {
    let result;
    if (isEditing) {
      result = await apiRequest(`${API_BASE}/${idProfesional}`, {
        method: 'PUT',
        body: JSON.stringify(payload)
      });

      // H-05: PATCH solo cuando el estado cambió realmente.
      // Si originalEstado === nuevoEstado no hay escritura ni auditoría innecesaria.
      if (nuevoEstado && nuevoEstado !== originalEstado) {
        try {
          await apiRequest(`${API_BASE}/${idProfesional}/estado`, {
            method: 'PATCH',
            body: JSON.stringify({ estado: nuevoEstado })
          });
        } catch (estadoErr) {
          // El PATCH falla independientemente del PUT ya completado;
          // avisamos al usuario pero no revertimos los datos guardados.
          window.ToastService?.warning(`⚠️ Datos guardados pero no se pudo actualizar el estado: ${estadoErr.message}`);
        }
      }
    } else {
      result = await apiRequest(API_BASE, {
        method: 'POST',
        body: JSON.stringify(payload)
      });
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
  if (!form) return;

  form.querySelectorAll('input, select').forEach((field) => {
    field.addEventListener('input', () => {
      if (field.id === 'formTelefono' && field.value.trim()) {
        const validPhone = /^[0-9+\s()-]{7,15}$/.test(field.value.trim());
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

const openConfirmDeleteModal = (id, name) => {
  lastModalOpener = document.activeElement;
  const modal = safeGetElement('modalConfirmDelete');
  const message = safeGetElement('modalConfirmDeleteMessage');
  const deleteIdInput = safeGetElement('deleteProfesionalId');
  
  if (message) {
    message.textContent = `¿Estás seguro de desactivar a ${name}? El profesional quedará inactivo y no podrá recibir nuevas citas.`;
  }
  if (deleteIdInput) {
    deleteIdInput.value = id;
  }
  if (modal) {
    modal.classList.add('open');
    // CORRECCIÓN: usar setAttribute con string 'false' en lugar de removeAttribute
    // para que lectores de pantalla detecten correctamente el cambio de estado ARIA
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    // Focus en botón Cancelar: previene confirmación accidental (WCAG 2.4.3)
    setTimeout(() => {
      const cancelBtn = safeGetElement('modalConfirmDeleteCancel');
      if (cancelBtn) cancelBtn.focus();
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
  const btnNew = safeGetElement('btnNewProfessional');
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

  // Delegación de eventos para botones de la tabla SSR y renderTableFromApi.
  // WHY: los botones de la tabla SSR existen al cargar la página; los de renderTableFromApi
  //      se crean dinámicamente. La delegación en tbody captura ambos casos sin re-enlazar.
  const tbody = safeGetElement('professionalsTbody');
  tbody?.addEventListener('click', (e) => {
    const btn = e.target.closest('.btn-delete[data-id]');
    if (btn) {
      const id = btn.getAttribute('data-id');
      const name = btn.getAttribute('data-name') || 'este profesional';
      if (id) openConfirmDeleteModal(id, name);
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
        safeGetElement('modalDetailClose')?.focus();
      }
    }
  });

  // Botón de confirmación del DELETE lógico
  // Fase 2D — 2D.2: DELETE /api/profesionales/{id} en lugar de form.submit() al MVC.
  const btnConfirmDelete = safeGetElement('modalConfirmDeleteConfirm');
  btnConfirmDelete?.addEventListener('click', async () => {
    const deleteIdInput = safeGetElement('deleteProfesionalId');
    const id = Number(deleteIdInput?.value);
    if (!id) return;

    const confirmBtn = btnConfirmDelete;
    confirmBtn.disabled = true;
    confirmBtn.textContent = '⏳ Desactivando...';

    try {
      const result = await apiRequest(`${API_BASE}/${id}`, { method: 'DELETE' });
      window.ToastService?.success(`✅ ${result.message || 'Profesional desactivado correctamente.'}`);
      closeConfirmDeleteModal();
      await loadProfessionals();
    } catch (err) {
      window.ToastService?.error(`❌ ${err.message}`);
    } finally {
      confirmBtn.disabled = false;
      confirmBtn.textContent = '🗑️ Confirmar desactivación';
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
    safeGetElement('metricTotal'),
    safeGetElement('metricActives'),
    safeGetElement('metricVacations'),
    safeGetElement('metricInactives'),
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