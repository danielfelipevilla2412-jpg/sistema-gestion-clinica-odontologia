/**
SMILETRACK — PERFIL DEL PACIENTE (st-pac-perfil.js)
API-ready + Accesibilidad + Persistencia fallback
*/
// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Usa el servicio de toasts compartido del layout si está disponible;
// si no, cae al contenedor local (#toast) para mantener compatibilidad.
const showToast = (message, type = 'success') => {
  const service = window.ToastService;
  if (service && typeof service[type] === 'function') {
    service[type](message);
    return;
  }
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// Traduce una clave con el motor i18n compartido cuando esté disponible.
const translate = (key, fallback) => {
  try {
    return (window.I18N && typeof window.I18N.t === 'function') ? window.I18N.t(key) : fallback;
  } catch (_) {
    return fallback;
  }
};

// Traduce una clave si el diccionario la conoce; si no, devuelve el texto de respaldo.
const t = (key, fallback) => (window.I18N && window.I18N.has(key)) ? window.I18N.t(key) : fallback;

// Muestra/oculta un elemento mediante la clase utilitaria .is-hidden
// (reemplaza el manejo de style.display inline).
const setHidden = (el, hidden) => {
  if (el) el.classList.toggle('is-hidden', hidden);
};

// ═══════════════════════════════════════════════════════════════════
//  ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════
let modoEdicion = false;
let snapshot = {};

// Campos editables por el paciente
const camposEditables = [
  'nombre', 'genero', 'estadoCivil',
  'email', 'telefono', 'direccion', 'ciudad', 'departamento',
  'emergNombre', 'emergParentesco', 'emergTelefono',
  'eps', 'poliza', 'afiliacion'
];

// Campos bloqueados siempre (solo lectura absoluta)
const camposBloqueados = ['cedula', 'fechaNac', 'tipoSangre'];

// ═══════════════════════════════════════════════════════════════════
//  MODO EDICIÓN
// ═══════════════════════════════════════════════════════════════════
function activarEdicion() {
  modoEdicion = true;

  // Guardar snapshot para cancelar
  snapshot = {};
  camposEditables.forEach(id => {
    const el = safeGetElement(id);
    if (el) snapshot[id] = el.value;
  });

  // Habilitar campos editables
  camposEditables.forEach(id => {
    const el = safeGetElement(id);
    if (el) el.disabled = false;
  });

  // Resaltar cards editables
  document.querySelectorAll('.card').forEach(c => c.classList.add('editing'));

  // Mostrar avatar upload
  const avatarUpload = safeGetElement('avatarUpload');
  setHidden(avatarUpload, false);

  // Botones
  const btnEditar = safeGetElement('btnEditar');
  const btnGuardar = safeGetElement('btnGuardar');
  const btnCancelar = safeGetElement('btnCancelar');
  setHidden(btnEditar, true);
  setHidden(btnGuardar, false);
  setHidden(btnCancelar, false);

  // Enfocar primer campo
  const nombreInput = safeGetElement('nombre');
  if (nombreInput) nombreInput.focus();

  showToast(t('pac_profile.toast_edit_mode', 'Modo edición activado'));
}

function cancelarEdicion() {
  modoEdicion = false;

  // Restaurar snapshot
  camposEditables.forEach(id => {
    const el = safeGetElement(id);
    if (el && snapshot[id] !== undefined) {
      el.value = snapshot[id];
    }
  });

  desactivarEdicion();
  showToast(t('pac_profile.toast_changes_discarded', 'Cambios descartados'));
}

function guardarCambios() {
  // Validar email
  const email = safeGetElement('email')?.value?.trim() || '';
  if (email && !email.includes('@')) {
    showToast(t('pac_profile.toast_invalid_email', 'Ingresa un correo electrónico válido'), 'error');
    safeGetElement('email')?.focus();
    return;
  }

  // Actualizar header con nombre nuevo
  const nombre = safeGetElement('nombre')?.value?.trim() || '';
  if (nombre) {
    const headerNombre = safeGetElement('headerNombre');
    if (headerNombre) headerNombre.textContent = nombre;

    const iniciales = nombre.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase();
    const headerAvatar = safeGetElement('headerAvatar');
    if (headerAvatar && !headerAvatar.querySelector('img')) {
      headerAvatar.textContent = iniciales;
    }
    const sidebarAvatar = safeGetElement('sidebarAvatar');
    if (sidebarAvatar) sidebarAvatar.textContent = iniciales;
    const avatarPreview = safeGetElement('avatarPreview');
    if (avatarPreview && !avatarPreview.querySelector('img')) {
      avatarPreview.textContent = iniciales;
    }
  }

  // Actualizar email en sidebar
  if (email) {
    const sidebarEmail = safeGetElement('sidebarEmail');
    if (sidebarEmail) sidebarEmail.textContent = email;
  }

  // Llamada al API (descomentar cuando el backend esté listo):
  /*
  const payload = {};
  camposEditables.forEach(id => {
    const el = safeGetElement(id);
    if (el) payload[id] = el.value;
  });
  fetch(`${API_BASE}/paciente/perfil`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  }).catch(console.error);
  */

  desactivarEdicion();
  window.PerfilPacienteService?.invalidateCache();
  showToast(t('pac_profile.toast_profile_updated', 'Perfil actualizado correctamente'));
}

function desactivarEdicion() {
  modoEdicion = false;

  // Deshabilitar todos los campos
  [...camposEditables, ...camposBloqueados].forEach(id => {
    const el = safeGetElement(id);
    if (el) el.disabled = true;
  });

  // Quitar highlight de cards
  document.querySelectorAll('.card').forEach(c => c.classList.remove('editing'));

  // Ocultar avatar upload
  const avatarUpload = safeGetElement('avatarUpload');
  setHidden(avatarUpload, true);

  // Botones
  const btnEditar = safeGetElement('btnEditar');
  const btnGuardar = safeGetElement('btnGuardar');
  const btnCancelar = safeGetElement('btnCancelar');
  setHidden(btnEditar, false);
  setHidden(btnGuardar, true);
  setHidden(btnCancelar, true);
}

// ═══════════════════════════════════════════════════════════════════
//  AVATAR UPLOAD
// ═══════════════════════════════════════════════════════════════════
function initAvatarUpload() {
  const avatarInput = safeGetElement('avatarInput');
  const avatarPreview = safeGetElement('avatarPreview');
  const headerAvatar = safeGetElement('headerAvatar');

  if (!avatarInput) return;

  avatarInput.addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file) return;

    // Validar tamaño (2MB)
    if (file.size > 2 * 1024 * 1024) {
      showToast(t('pac_profile.toast_image_too_large', 'La imagen debe ser menor a 2MB'), 'error');
      avatarInput.value = '';
      return;
    }

    // Validar tipo
    if (!file.type.startsWith('image/')) {
      showToast(t('pac_profile.toast_invalid_image_type', 'Solo se permiten imágenes (JPG, PNG)'), 'error');
      avatarInput.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = (ev) => {
      const imgURL = ev.target.result;

      // Actualizar preview
      if (avatarPreview) {
        avatarPreview.innerHTML = `<img src="${imgURL}" alt="${t('pac_profile.photo_alt', 'Foto de perfil')}" />`;
      }

      // Actualizar header
      if (headerAvatar) {
        headerAvatar.innerHTML = `<img src="${imgURL}" alt="${t('pac_profile.photo_alt', 'Foto de perfil')}" />`;
      }

      showToast(t('pac_profile.toast_photo_updated', 'Foto actualizada'));
    };
    reader.readAsDataURL(file);
  });
}

// ═══════════════════════════════════════════════════════════════════
//  TOGGLE CONTRASEÑA
// ═══════════════════════════════════════════════════════════════════


// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════
const init = () => {
  // El sidebar, hamburger, overlay y grupos de navegación los gestiona
  // el sidebar.js compartido del layout (_Layout.cshtml).
  initAvatarUpload();

  // Botones del header
  const btnEditar = safeGetElement('btnEditar');
  const btnGuardar = safeGetElement('btnGuardar');
  const btnCancelar = safeGetElement('btnCancelar');

  btnEditar?.addEventListener('click', activarEdicion);
  btnGuardar?.addEventListener('click', guardarCambios);
  btnCancelar?.addEventListener('click', cancelarEdicion);

  // Atajos de teclado
  document.addEventListener('keydown', (e) => {
    // Ctrl/Cmd + S para guardar
    if ((e.ctrlKey || e.metaKey) && e.key === 's') {
      e.preventDefault();
      if (modoEdicion) guardarCambios();
    }
    // Escape para cancelar
    if (e.key === 'Escape' && modoEdicion) {
      cancelarEdicion();
    }
  });
};

document.addEventListener('DOMContentLoaded', init);