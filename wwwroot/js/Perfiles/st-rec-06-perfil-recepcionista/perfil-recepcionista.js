/**
 * SMILETRACK — MI PERFIL (recepcionista)
 * Lógica con formularios, persistencia localStorage y validaciones.
 * Las notificaciones usan window.ToastService (shared/toasts.js) que el
 * layout _Layout carga mediante _Toasts.cshtml.
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

// Muestra notificación temporal mediante el servicio compartido de toasts
const showToast = (message, type = 'success') => {
  const titulos = {
    success: 'Éxito',
    error: 'Error',
    warning: 'Advertencia',
    info: 'Información'
  };
  const servicio = window.ToastService && window.ToastService[type];
  if (typeof servicio === 'function') {
    servicio.call(window.ToastService, titulos[type] || 'Información', message);
  }
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
      if (confirm('Esta acción es irreversible. ¿Realmente deseas eliminar tu cuenta?')) {
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
  // El menú móvil (hamburger/overlay/sidebar) lo inicializa shared/sidebar.js del layout.
  initPhotoUpload();
  initProfileForm();
  initPasswordForm();
  initToggles();
  initDangerActions();
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);
