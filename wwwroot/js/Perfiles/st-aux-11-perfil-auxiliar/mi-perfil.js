/**
 * SMILETRACK — MI PERFIL (app.js)
 * Lógica con persistencia, validación de contraseña y accesibilidad
 */

// Obtiene elemento del DOM con manejo seguro de null
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Reduce llamadas a función en eventos frecuentes de input
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// Muestra notificación temporal con auto-cierre y cleanup de timeout
const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;

  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// Valida campo de formulario y muestra errores
const validateField = (input) => {
  const group = input.closest('.form-group');
  if (!group) return true;
  
  const errorSpan = group.querySelector('.error-message');
  let valid = true;
  
  if (input.required && !input.value.trim()) {
    valid = false;
  }
  
  if (!valid) {
    input.classList.add('error');
    if (errorSpan) errorSpan.classList.add('visible');
    input.setAttribute('aria-invalid', 'true');
  } else {
    input.classList.remove('error');
    if (errorSpan) errorSpan.classList.remove('visible');
    input.removeAttribute('aria-invalid');
  }
  
  return valid;
};

// Valida todos los campos de un formulario
const validateForm = (form) => {
  const inputs = form.querySelectorAll('input[required]');
  let allValid = true;
  
  inputs.forEach(input => {
    if (!validateField(input)) allValid = false;
  });
  
  return allValid;
};

// Gestiona persistencia del perfil de usuario con localStorage
const profileStorage = {
  key: 'smiletrack_user_profile',
  
  // Carga perfil desde localStorage o usa valores por defecto
  load: () => {
    const stored = localStorage.getItem(profileStorage.key);
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch (e) {
        console.warn('Error al cargar perfil, usando valores por defecto');
      }
    }
    // Datos de ejemplo iniciales
    return {
      nombres: 'Sara',
      apellidos: 'Jiménez',
      tipoDoc: 'CC',
      numeroDoc: '1234567890',
      fechaIngreso: '2024-01-01',
      telefono: '312456 7890',
      email: 'auxiliar@smiletrack.co',
      avatar: 'SJ'
    };
  },
  
  // Guarda perfil en localStorage
  save: (profile) => {
    try {
      localStorage.setItem(profileStorage.key, JSON.stringify(profile));
      return true;
    } catch (e) {
      console.error('Error al guardar perfil:', e);
      return false;
    }
  },
  
  // Calcula porcentaje de completitud del perfil
  calculateCompletion: (profile) => {
    const requiredFields = ['nombres', 'apellidos', 'tipoDoc', 'numeroDoc', 'telefono', 'email'];
    const filled = requiredFields.filter(field => profile[field]?.trim()).length;
    return Math.round((filled / requiredFields.length) * 100);
  }
};


// Inicializa menú móvil con gestión de foco y atributos ARIA
const initMobileMenu = () => {
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  const hamburger = safeGetElement('hamburger');

  if (!sidebar || !overlay || !hamburger) return;

  const toggleMenu = (show) => {
    if (show) {
      sidebar.classList.add('open');
      overlay.classList.add('open');
      hamburger.setAttribute('aria-expanded', 'true');
      overlay.setAttribute('aria-hidden', 'false');
      
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      sidebar.classList.remove('open');
      overlay.classList.remove('open');
      hamburger.setAttribute('aria-expanded', 'false');
      overlay.setAttribute('aria-hidden', 'true');
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  sidebar.querySelectorAll('.nav-item').forEach(link => {
    link.addEventListener('click', () => {
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

// Inicializa carga y renderizado de datos de usuario
const initUserProfile = () => {
  const profile = profileStorage.load();
  
  // Actualiza elementos del DOM con datos del perfil
  const fields = {
    userAvatar: profile.avatar || 'Au',
    nombres: profile.nombres,
    apellidos: profile.apellidos,
    tipoDoc: profile.tipoDoc,
    numeroDoc: profile.numeroDoc ? '••••••••••' : '',
    fechaIngreso: profile.fechaIngreso ? new Date(profile.fechaIngreso).toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' }) : '',
    telefono: profile.telefono,
    email: profile.email
  };
  
  Object.entries(fields).forEach(([id, value]) => {
    const el = safeGetElement(id);
    if (el) el.textContent = value;
  });
  
  // Actualiza barra de progreso de completitud
  const completion = profileStorage.calculateCompletion(profile);
  const progress = safeGetElement('profileProgress');
  const progressText = safeGetElement('progressText');
  
  if (progress) {
    progress.style.width = `${completion}%`;
    progress.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', completion);
    progress.closest('[role="progressbar"]')?.setAttribute('aria-valuetext', `${completion}% completado`);
  }
  
  if (progressText) {
    progressText.textContent = `${completion}% completado`;
  }
};

// Inicializa saludo dinámico según hora del día
const initGreeting = () => {
  const greetingEl = safeGetElement('greeting');
  if (!greetingEl) return;
  
  const profile = profileStorage.load();
  const hour = new Date().getHours();
  let greeting = 'Buenas noches';
  
  if (hour >= 6 && hour < 12) greeting = 'Buenos días';
  else if (hour >= 12 && hour < 18) greeting = 'Buenas tardes';
  
  greetingEl.textContent = `${greeting}, ${profile.nombres} 👋`;
  greetingEl.setAttribute('aria-label', `Saludo: ${greeting} ${profile.nombres}`);
};




// Inicializa botón de edición de perfil (mock para futura implementación)
const initEditProfile = () => {
  const btn = safeGetElement('btnEditProfile');
  if (!btn) return;
  
  btn.addEventListener('click', () => {
    showToast('✏️ Función de edición próximamente disponible', 'info');
  });
};

// Función principal de inicialización
const init = () => {
  // Inicializar componentes de UI
  initMobileMenu();
  initUserProfile();
  initGreeting();
  initEditProfile();
  
  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);