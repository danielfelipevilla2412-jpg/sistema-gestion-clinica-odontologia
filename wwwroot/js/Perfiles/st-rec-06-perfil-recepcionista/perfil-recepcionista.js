/**
 * SMILETRACK — MI PERFIL (app.js)
 * Lógica con formularios, persistencia localStorage y validaciones
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

// Gestiona persistencia de perfil con localStorage
const profileStorage = {
  key: 'smiletrack_profile',
  
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
    return {
      nombre: 'Recepcionista',
      cargo: 'Recepción',
      email: 'recepcion@smiletrack.co',
      telefono: '+57 300 123 4567',
      bio: 'Encargada de recepción en SmileTrack. Apasionada por la atención al paciente.',
      preferences: {
        email: true,
        sms: false,
        dark: false
      }
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
  }
};

// Valida campo de formulario y muestra errores
const validateField = (input) => {
  const group = input.closest('.form-group');
  if (!group) return true;
  
  const errorSpan = group.querySelector('.error-message');
  let valid = true;
  
  if (input.required && !input.value.trim()) {
    valid = false;
  } else if (input.type === 'email' && input.value.trim()) {
    valid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(input.value.trim());
  } else if (input.minLength && input.value.length < input.minLength) {
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
  const inputs = form.querySelectorAll('input[required], select[required], textarea[required]');
  let allValid = true;
  
  inputs.forEach(input => {
    if (!validateField(input)) allValid = false;
  });
  
  return allValid;
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

// Inicializa carga y preview de foto de perfil
const initPhotoUpload = () => {
  const btnUpload = safeGetElement('btnUploadPhoto');
  const fileInput = safeGetElement('fileInput');
  const profileAvatar = safeGetElement('profileAvatar');
  
  if (!btnUpload || !fileInput || !profileAvatar) return;
  
  btnUpload.addEventListener('click', () => fileInput.click());
  
  fileInput.addEventListener('change', (e) => {
    const file = e.target.files[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      showToast('Por favor selecciona una imagen válida', 'error');
      return;
    }

    const reader = new FileReader();
    reader.onload = (event) => {
      // Muestra preview de la imagen seleccionada
      profileAvatar.textContent = '';
      profileAvatar.style.backgroundImage = `url(${event.target.result})`;
      profileAvatar.style.backgroundSize = 'cover';
      profileAvatar.style.backgroundPosition = 'center';
      showToast('Foto de perfil actualizada', 'success');
    };
    reader.readAsDataURL(file);
  });
};

// Inicializa formulario de datos personales con persistencia
const initProfileForm = () => {
  const form = safeGetElement('profileForm');
  const btnSave = safeGetElement('btnSaveProfile');
  const btnCancel = safeGetElement('btnCancel');
  
  if (!form) return;
  
  // Carga datos guardados en el formulario
  const profile = profileStorage.load();
  const fields = ['nombre', 'cargo', 'email', 'telefono', 'bio'];
  fields.forEach(field => {
    const input = safeGetElement(field);
    if (input && profile[field]) input.value = profile[field];
  });
  
  // Validación en tiempo real
  form.querySelectorAll('input[required], textarea[required]').forEach(input => {
    input.addEventListener('blur', () => validateField(input));
    input.addEventListener('input', () => {
      if (input.classList.contains('error')) validateField(input);
    });
  });
  
  // Manejo de envío del formulario
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    
    if (!validateForm(form)) {
      showToast('Por favor completa los campos requeridos', 'error');
      return;
    }
    
    // Recoge y guarda datos del perfil
    const updatedProfile = { ...profile };
    fields.forEach(field => {
      const input = safeGetElement(field);
      if (input) updatedProfile[field] = input.value;
    });
    
    profileStorage.save(updatedProfile);
    
    // Actualiza UI con nuevos valores
    const profileName = safeGetElement('profileName');
    const profileEmail = safeGetElement('profileEmail');
    if (profileName) profileName.textContent = updatedProfile.nombre;
    if (profileEmail) profileEmail.textContent = updatedProfile.email;
    
    showToast('Perfil actualizado correctamente', 'success');
  });
  
  // Manejo de cancelación
  if (btnCancel) {
    btnCancel.addEventListener('click', () => {
      // Recarga valores originales desde localStorage
      fields.forEach(field => {
        const input = safeGetElement(field);
        if (input && profile[field]) input.value = profile[field];
      });
      showToast('Cambios descartados', 'warning');
    });
  }
};

// Inicializa formulario de cambio de contraseña
const initPasswordForm = () => {
  const form = safeGetElement('passwordForm');
  if (!form) return;
  
  // Validación en tiempo real
  form.querySelectorAll('input').forEach(input => {
    input.addEventListener('blur', () => validateField(input));
    input.addEventListener('input', () => {
      if (input.classList.contains('error')) validateField(input);
    });
  });
  
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    
    const current = safeGetElement('currentPassword');
    const newPass = safeGetElement('newPassword');
    const confirm = safeGetElement('confirmPassword');
    
    if (!current?.value || !newPass?.value || !confirm?.value) {
      showToast('Completa todos los campos', 'warning');
      return;
    }

    if (newPass.value.length < 8) {
      newPass.classList.add('error');
      safeGetElement('error-newPassword')?.classList.add('visible');
      showToast('La contraseña debe tener al menos 8 caracteres', 'error');
      return;
    }

    if (newPass.value !== confirm.value) {
      confirm.classList.add('error');
      safeGetElement('error-confirmPassword')?.classList.add('visible');
      showToast('Las contraseñas no coinciden', 'error');
      return;
    }
    
    // Limpia campos después de éxito
    [current, newPass, confirm].forEach(input => {
      if (input) input.value = '';
    });
    
    showToast('Contraseña actualizada correctamente', 'success');
  });
};

// Inicializa toggle switches de preferencias con persistencia
const initToggles = () => {
  const toggles = {
    toggleEmail: 'email',
    toggleSMS: 'sms',
    toggleDark: 'dark'
  };
  
  const profile = profileStorage.load();
  
  Object.entries(toggles).forEach(([toggleId, prefKey]) => {
    const toggle = safeGetElement(toggleId);
    if (!toggle) return;
    
    // Establece estado inicial desde localStorage
    if (profile.preferences?.[prefKey]) {
      toggle.classList.add('active');
      toggle.setAttribute('aria-checked', 'true');
    }
    
    // Maneja cambio de estado
    toggle.addEventListener('click', () => {
      const isActive = !toggle.classList.contains('active');
      toggle.classList.toggle('active');
      toggle.setAttribute('aria-checked', isActive);
      
      // Actualiza y guarda preferencia
      profile.preferences[prefKey] = isActive;
      profileStorage.save(profile);
      
      const label = toggleId.replace('toggle', '');
      showToast(`${label} ${isActive ? 'activada' : 'desactivada'}`, 'info');
    });
    
    // Soporte para teclado en toggle
    toggle.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        toggle.click();
      }
    });
  });
};

// Inicializa acciones de zona de peligro con confirmaciones
const initDangerActions = () => {
  const btnLogoutAll = safeGetElement('btnLogoutAll');
  const btnDeleteAccount = safeGetElement('btnDeleteAccount');
  
  if (btnLogoutAll) {
    btnLogoutAll.addEventListener('click', () => {
      if (confirm('¿Estás seguro de cerrar sesión en todos tus dispositivos?')) {
        showToast('Sesión cerrada en todos los dispositivos', 'success');
        // En producción: invalidar tokens de sesión
        setTimeout(() => window.location.href = '../../auth/login/index.html', 1500);
      }
    });
  }
  
  if (btnDeleteAccount) {
    btnDeleteAccount.addEventListener('click', () => {
      if (confirm('⚠️ Esta acción es irreversible. ¿Realmente deseas eliminar tu cuenta?')) {
        const confirmText = prompt('Escribe "ELIMINAR" para confirmar:');
        if (confirmText === 'ELIMINAR') {
          // En producción: llamar a API para eliminar cuenta
          localStorage.removeItem(profileStorage.key);
          showToast('Cuenta eliminada. Redirigiendo...', 'warning');
          setTimeout(() => window.location.href = '../../auth/login/index.html', 2000);
        } else {
          showToast('Operación cancelada', 'info');
        }
      }
    });
  }
};

// Función principal de inicialización
const init = () => {
  // Inicializar componentes de UI
  initMobileMenu();
  initPhotoUpload();
  initProfileForm();
  initPasswordForm();
  initToggles();
  initDangerActions();
  
  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);