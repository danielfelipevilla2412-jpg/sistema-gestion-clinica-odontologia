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

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
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
  'eps', 'poliza', 'afiliacion',
  'passActual', 'passNueva', 'passConfirm'
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
  if (avatarUpload) avatarUpload.style.display = 'flex';

  // Botones
  const btnEditar = safeGetElement('btnEditar');
  const btnGuardar = safeGetElement('btnGuardar');
  const btnCancelar = safeGetElement('btnCancelar');
  if (btnEditar) btnEditar.style.display = 'none';
  if (btnGuardar) btnGuardar.style.display = 'inline-flex';
  if (btnCancelar) btnCancelar.style.display = 'inline-flex';

  // Enfocar primer campo
  const nombreInput = safeGetElement('nombre');
  if (nombreInput) nombreInput.focus();

  showToast('✏️ Modo edición activado');
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
  showToast('↩️ Cambios descartados');
}

function guardarCambios() {
  // Validar contraseña si se está cambiando
  const passActual = safeGetElement('passActual')?.value || '';
  const passNueva = safeGetElement('passNueva')?.value || '';
  const passConfirm = safeGetElement('passConfirm')?.value || '';

  if (passNueva || passConfirm) {
    if (!passActual) {
      showToast('❌ Ingresa tu contraseña actual', 'error');
      safeGetElement('passActual')?.focus();
      return;
    }
    if (passNueva.length < 8) {
      showToast('❌ La nueva contraseña debe tener al menos 8 caracteres', 'error');
      safeGetElement('passNueva')?.focus();
      return;
    }
    if (passNueva !== passConfirm) {
      showToast('❌ Las contraseñas no coinciden', 'error');
      safeGetElement('passConfirm')?.focus();
      return;
    }
  }

  // Validar email
  const email = safeGetElement('email')?.value?.trim() || '';
  if (email && !email.includes('@')) {
    showToast('❌ Ingresa un correo electrónico válido', 'error');
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
  showToast('✅ Perfil actualizado correctamente');
}

function desactivarEdicion() {
  modoEdicion = false;

  // Deshabilitar todos los campos
  [...camposEditables, ...camposBloqueados].forEach(id => {
    const el = safeGetElement(id);
    if (el) el.disabled = true;
  });

  // Limpiar contraseñas
  ['passActual', 'passNueva', 'passConfirm'].forEach(id => {
    const el = safeGetElement(id);
    if (el) el.value = '';
  });

  // Quitar highlight de cards
  document.querySelectorAll('.card').forEach(c => c.classList.remove('editing'));

  // Ocultar avatar upload
  const avatarUpload = safeGetElement('avatarUpload');
  if (avatarUpload) avatarUpload.style.display = 'none';

  // Botones
  const btnEditar = safeGetElement('btnEditar');
  const btnGuardar = safeGetElement('btnGuardar');
  const btnCancelar = safeGetElement('btnCancelar');
  if (btnEditar) btnEditar.style.display = 'inline-flex';
  if (btnGuardar) btnGuardar.style.display = 'none';
  if (btnCancelar) btnCancelar.style.display = 'none';
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
      showToast('❌ La imagen debe ser menor a 2MB', 'error');
      avatarInput.value = '';
      return;
    }

    // Validar tipo
    if (!file.type.startsWith('image/')) {
      showToast('❌ Solo se permiten imágenes (JPG, PNG)', 'error');
      avatarInput.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = (ev) => {
      const imgURL = ev.target.result;

      // Actualizar preview
      if (avatarPreview) {
        avatarPreview.innerHTML = `<img src="${imgURL}" alt="Foto de perfil" />`;
      }

      // Actualizar header
      if (headerAvatar) {
        headerAvatar.innerHTML = `<img src="${imgURL}" alt="Foto de perfil" />`;
      }

      showToast('📷 Foto actualizada');
    };
    reader.readAsDataURL(file);
  });
}

// ═══════════════════════════════════════════════════════════════════
//  TOGGLE CONTRASEÑA
// ═══════════════════════════════════════════════════════════════════
window.togglePass = (inputId, btn) => {
  const input = safeGetElement(inputId);
  if (!input || input.disabled) return;
  if (input.type === 'password') {
    input.type = 'text';
    btn.textContent = '🙈';
    btn.setAttribute('aria-label', 'Ocultar contraseña');
  } else {
    input.type = 'password';
    btn.textContent = '👁️';
    btn.setAttribute('aria-label', 'Mostrar contraseña');
  }
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
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
    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

const initNavGroups = () => {
  document.querySelectorAll('.nav-group-header').forEach(header => {
    const group = header.parentElement;
    const toggle = () => {
      const isOpen = group.classList.toggle('open');
      header.setAttribute('aria-expanded', isOpen);
    };
    header.addEventListener('click', toggle);
    header.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        toggle();
      }
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════
const init = () => {
  initSidebar();
  initNavGroups();
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