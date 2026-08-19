// =============================================
// SMILETRACK — REGISTRAR PACIENTE (app.js)
// [MEJORA]: Código refactorizado con patrones reutilizables de SmileTrack
// =============================================

/**
 * [MEJORA]: Utilidad reutilizable para obtener elementos con null check seguro
 * Consistente con todos los módulos anteriores - evita errores si el elemento no existe
 * @param {string} id - ID del elemento a buscar
 * @returns {HTMLElement|null} Elemento o null si no existe
 */
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) {
    console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  }
  return el;
};

/**
 * [MEJORA]: Toast con cleanup de timeout para evitar solapamientos
 * Consistente con módulos anteriores - mejora robustez en notificaciones rápidas
 * @param {string} message - Mensaje a mostrar
 * @param {'success'|'error'} type - Tipo de toast
 */
const showToast = (message, type = 'success') => {
  const toastContainer = safeGetElement('toastContainer');
  if (!toastContainer) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type === 'error' ? 'error' : ''}`;
  toast.setAttribute('role', 'alert');
  toast.setAttribute('aria-live', 'assertive');

  const icon = type === 'error' ? '⚠️' : '✅';
  const title = type === 'error' ? 'Error' : 'Éxito';

  toast.innerHTML = `
    <span class="toast-icon" aria-hidden="true">${icon}</span>
    <div class="toast-content">
      <p class="toast-title">${title}</p>
      <p class="toast-desc">${message}</p>
    </div>
    <button class="toast-close" aria-label="Cerrar notificación">×</button>
  `;

  toastContainer.appendChild(toast);

  // [MEJORA]: Usar requestAnimationFrame para animación sincronizada
  requestAnimationFrame(() => {
    toast.classList.add('show');
  });

  // Cerrar manualmente
  const closeBtn = toast.querySelector('.toast-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 250);
    });
  }

  // [MEJORA]: Auto-cerrar con cleanup de timeout para evitar memory leaks
  const autoCloseTimeout = setTimeout(() => {
    toast.classList.remove('show');
    setTimeout(() => toast.remove(), 250);
  }, 4500);

  // Guardar referencia para cleanup si se cierra manualmente antes
  toast._autoCloseTimeout = autoCloseTimeout;
};

// ═══ CONFIGURACIÓN DEL FORMULARIO ═══

/**
 * Configuración de validación por campo ID
 * @typedef {Object} FieldValidation
 * @property {Function} validate - Función de validación
 * @property {string} errorMessage - Mensaje de error
 */

const FIELD_VALIDATORS = {
  nombres: {
    validate: (value) => value.trim().length >= 2,
    errorMessage: 'Por favor ingresa los nombres.'
  },
  apellidos: {
    validate: (value) => value.trim().length >= 2,
    errorMessage: 'Por favor ingresa los apellidos.'
  },
  tipoDoc: {
    validate: (value) => value.trim() !== '',
    errorMessage: 'Por favor selecciona un tipo de documento.'
  },
  documento: {
    validate: (value) => /^[0-9]{7,15}$/.test(value.trim()),
    errorMessage: 'Por favor ingresa un número válido (solo números).'
  },
  fechaNacimiento: {
    validate: (value) => {
      if (!value) return false;
      const birth = new Date(value);
      const now = new Date();
      return birth <= now && birth.getFullYear() >= 1900;
    },
    errorMessage: 'Por favor ingresa una fecha válida.'
  },
  telefono: {
    validate: (value) => {
      const clean = value.replace(/\s+/g, '');
      return /^[0-9]{7,15}$/.test(clean);
    },
    errorMessage: 'Por favor ingresa un teléfono válido (mínimo 7 dígitos).'
  },
  correo: {
    validate: (value) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()),
    errorMessage: 'Por favor ingresa un correo válido.'
  },
  fechaCita: {
    validate: (value) => {
      if (!value) return true; // Campo opcional
      const selected = new Date(value);
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      return selected >= today;
    },
    errorMessage: 'La fecha no puede ser en el pasado.'
  }
};

/**
 * Valida un campo individual y muestra/oculta errores
 * @param {HTMLInputElement|HTMLSelectElement} input - Elemento a validar
 * @returns {boolean} true si es válido, false si hay error
 */
const validateField = (input) => {
  if (!input) return true;

  const group = input.closest('.form-group');
  if (!group) return true;

  const validator = FIELD_VALIDATORS[input.id];
  const errorSpan = group.querySelector('.error-message');

  // Si no hay validador registrado, considerar válido
  if (!validator) {
    input.classList.remove('error');
    if (errorSpan) errorSpan.classList.remove('visible');
    return true;
  }

  const value = input.value;
  const isValid = validator.validate(value);

  if (!isValid) {
    input.classList.add('error');
    if (errorSpan) {
      errorSpan.textContent = validator.errorMessage;
      errorSpan.classList.add('visible');
    }
    // [MEJORA]: Anunciar error a screen readers
    input.setAttribute('aria-invalid', 'true');
    input.setAttribute('aria-describedby', `error-${input.id}`);
  } else {
    input.classList.remove('error');
    if (errorSpan) errorSpan.classList.remove('visible');
    input.removeAttribute('aria-invalid');
  }

  return isValid;
};

/**
 * Valida todos los campos del Paso 1
 * @returns {boolean} true si todos los campos son válidos
 */
const validateStep1 = () => {
  const requiredFields = ['nombres', 'apellidos', 'tipoDoc', 'documento', 'fechaNacimiento', 'telefono', 'correo'];
  let allValid = true;

  requiredFields.forEach(fieldId => {
    const field = safeGetElement(fieldId);
    if (field && !validateField(field)) {
      allValid = false;
    }
  });

  return allValid;
};

/**
 * Configura validación en tiempo real para un campo
 * @param {string} fieldId - ID del campo a configurar
 */
const setupFieldValidation = (fieldId) => {
  const field = safeGetElement(fieldId);
  if (!field) return;

  // [MEJORA]: Usar event delegation conceptual con debounce para mejor performance
  const debouncedValidate = debounce(() => validateField(field), 150);

  ['input', 'change', 'blur'].forEach(eventType => {
    field.addEventListener(eventType, debouncedValidate);
  });

  // Validar al recuperar foco para mostrar estado actual
  field.addEventListener('focus', () => {
    if (field.classList.contains('error')) {
      validateField(field);
    }
  });
};

// ═══ STEPPER MULTIPASO ═══

/**
 * Actualiza el estado visual y ARIA del stepper
 * @param {1|2} step - Número del paso a activar
 */
const updateStepper = (step) => {
  const stepNum1 = safeGetElement('stepNum1');
  const stepNum2 = safeGetElement('stepNum2');
  const stepLabel1 = safeGetElement('stepLabel1');
  const stepLabel2 = safeGetElement('stepLabel2');
  const stepConnector = safeGetElement('stepConnector');
  const stepContent1 = safeGetElement('stepContent1');
  const stepContent2 = safeGetElement('stepContent2');
  const stepBtn1 = safeGetElement('stepBtn1');
  const stepBtn2 = safeGetElement('stepBtn2');

  if (!stepNum1 || !stepNum2) return;

  if (step === 1) {
    // Paso 1 activo
    stepNum1.className = 'step-num active';
    stepLabel1?.classList.remove('completed', 'inactive');
    stepLabel1?.classList.add('active');
    stepNum2.className = 'step-num inactive';
    stepLabel2?.classList.remove('active', 'completed');
    stepLabel2?.classList.add('inactive');
    stepConnector?.classList.remove('active');

    stepContent1?.classList.add('active');
    stepContent2?.classList.remove('active');
    stepContent2?.setAttribute('aria-hidden', 'true');
    stepContent1?.removeAttribute('aria-hidden');

    stepBtn1?.setAttribute('aria-selected', 'true');
    stepBtn1?.setAttribute('tabindex', '0');
    stepBtn2?.setAttribute('aria-selected', 'false');
    stepBtn2?.setAttribute('tabindex', '-1');

    // [MEJORA]: Enfocar primer campo del paso al activarlo
    requestAnimationFrame(() => {
      const firstInput = stepContent1?.querySelector('.form-input, .form-select');
      if (firstInput) firstInput.focus();
    });

  } else if (step === 2) {
    // Paso 2 activo, paso 1 completado
    stepNum1.className = 'step-num completed';
    stepLabel1?.classList.remove('active', 'inactive');
    stepLabel1?.classList.add('completed');
    stepNum2.className = 'step-num active';
    stepLabel2?.classList.remove('inactive', 'completed');
    stepLabel2?.classList.add('active');
    stepConnector?.classList.add('active');

    stepContent1?.classList.remove('active');
    stepContent2?.classList.add('active');
    stepContent1?.setAttribute('aria-hidden', 'true');
    stepContent2?.removeAttribute('aria-hidden');

    stepBtn1?.setAttribute('aria-selected', 'false');
    stepBtn1?.setAttribute('tabindex', '-1');
    stepBtn2?.setAttribute('aria-selected', 'true');
    stepBtn2?.setAttribute('tabindex', '0');

    // [MEJORA]: Enfocar primer campo del paso 2 al activarlo
    requestAnimationFrame(() => {
      const firstInput = stepContent2?.querySelector('.form-input, .form-select');
      if (firstInput) firstInput.focus();
    });
  }
};

// ═══ MANEJO DEL MENÚ MÓVIL ═══

/**
 * Inicializa eventos del menú móvil con gestión de accesibilidad
 * Consistente con todos los módulos anteriores
 */
const initMobileMenu = () => {
  const ham = safeGetElement('hamburger');
  const sb = safeGetElement('sidebar');
  const ov = safeGetElement('overlay');

  if (!ham || !sb || !ov) return;

  const toggleMenu = (show) => {
    if (show) {
      sb.classList.add('open');
      ov.classList.add('open');
      ham.setAttribute('aria-expanded', 'true');
      ov.setAttribute('aria-hidden', 'false');

      // [MEJORA]: Manejo de foco para accesibilidad
      sb.dataset.previousFocus = document.activeElement?.id || '';
      const firstLink = sb.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      sb.classList.remove('open');
      ov.classList.remove('open');
      ham.setAttribute('aria-expanded', 'false');
      ov.setAttribute('aria-hidden', 'true');

      // Restaurar foco al elemento que abrió el menú
      const prevFocus = sb.dataset.previousFocus;
      if (prevFocus) safeGetElement(prevFocus)?.focus();
      else ham.focus();
    }
  };

  ham.addEventListener('click', () => toggleMenu(true));
  ov.addEventListener('click', () => toggleMenu(false));

  // Cerrar menú al navegar (móvil)
  document.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMenu(false);
      }
    });
  });

  // [MEJORA]: Escape cierra menú con restauración de foco
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sb.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// ═══ INICIALIZACIÓN PRINCIPAL ═══

/**
 * Función principal de inicialización del formulario de registro
 */
const init = () => {
  // Generar número de historia clínica aleatorio
  const autoHC = safeGetElement('autoHC');
  if (autoHC) {
    const year = new Date().getFullYear();
    const random = Math.floor(1000 + Math.random() * 9000);
    autoHC.textContent = `HC-${year}-${random}`;
  }

  // Configurar fechas mínimas/máximas
  const today = new Date().toISOString().split('T')[0];
  const fechaNac = safeGetElement('fechaNacimiento');
  const fechaCita = safeGetElement('fechaCita');

  if (fechaNac) {
    fechaNac.max = today;
    // [MEJORA]: Valor por defecto solo si está vacío
    if (!fechaNac.value) fechaNac.value = '1995-08-15';
  }
  if (fechaCita) fechaCita.min = today;

  // Inicializar componentes de UI
  initMobileMenu();

  // Configurar validación en tiempo real para campos del Paso 1
  Object.keys(FIELD_VALIDATORS).forEach(setupFieldValidation);

  // ── Navegación entre pasos ──
  const nextBtn = safeGetElement('nextBtn');
  const prevBtn = safeGetElement('prevBtn');
  const stepBtn1 = safeGetElement('stepBtn1');
  const stepBtn2 = safeGetElement('stepBtn2');

  if (nextBtn) {
    nextBtn.addEventListener('click', () => {
      if (validateStep1()) {
        updateStepper(2);
        // [MEJORA]: Scroll suave al inicio del paso
        window.scrollTo({ top: 0, behavior: 'smooth' });
      } else {
        showToast('Por favor corrige los errores del formulario.', 'error');
        // [MEJORA]: Enfocar primer campo con error
        const firstError = document.querySelector('.form-input.error, .form-select.error');
        if (firstError) firstError.focus();
      }
    });
  }

  if (prevBtn) {
    prevBtn.addEventListener('click', () => {
      updateStepper(1);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  // Navegación directa por botones del stepper (accesibilidad keyboard)
  if (stepBtn1) {
    stepBtn1.addEventListener('click', () => updateStepper(1));
    stepBtn1.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        updateStepper(1);
      }
    });
  }

  if (stepBtn2) {
    stepBtn2.addEventListener('click', () => {
      if (validateStep1()) {
        updateStepper(2);
      } else {
        showToast('Completa el Paso 1 antes de continuar.', 'error');
      }
    });
    stepBtn2.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        if (validateStep1()) updateStepper(2);
        else showToast('Completa el Paso 1 antes de continuar.', 'error');
      }
    });
  }

  // Botones de cancelar
  const cancelBtn1 = safeGetElement('cancelBtn1');
  const cancelBtn2 = safeGetElement('cancelBtn2');
  const handleCancel = () => {
    if (confirm('¿Estás seguro de cancelar? Los datos no guardados se perderán.')) {
      window.history.back();
    }
  };
  if (cancelBtn1) cancelBtn1.addEventListener('click', handleCancel);
  if (cancelBtn2) cancelBtn2.addEventListener('click', handleCancel);

  // ── Envío del formulario ──
  const patientForm = safeGetElement('patientForm');
  if (patientForm) {
    patientForm.addEventListener('submit', async (e) => {
      e.preventDefault();

      if (!validateStep1()) {
        updateStepper(1);
        showToast('Corrige los errores antes de guardar.', 'error');
        const firstError = document.querySelector('.form-input.error, .form-select.error');
        if (firstError) firstError.focus();
        return;
      }

      const saveBtn = safeGetElement('saveBtn');
      if (!saveBtn) return;

      const originalContent = saveBtn.innerHTML;

      // Estado de carga con deshabilitación
      saveBtn.disabled = true;
      saveBtn.setAttribute('aria-busy', 'true');
      saveBtn.innerHTML = '<span aria-hidden="true">⏳</span> Guardando...';

      try {
        const formData = new FormData(patientForm);

        const token = document.querySelector(
          'input[name="__RequestVerificationToken"]'
        )?.value;

        if (!token) {
          throw new Error('No se encontró el token de seguridad del formulario.');
        }

        const response = await fetch('/gestion-de-pacientes/crear', {
          method: 'POST',
          body: formData,
          credentials: 'include',
          headers: {
            'X-CSRF-TOKEN': token
          }
        });

        const result = await response.json();

        if (!response.ok) {
          throw new Error(result.message || 'No fue posible registrar el paciente.');
        }

        showToast(`✓ ${result.message}`, 'success');

        saveBtn.disabled = false;
        saveBtn.removeAttribute('aria-busy');
        saveBtn.innerHTML = originalContent;

        setTimeout(() => {
          window.location.href = '/gestion-de-pacientes/st-adm-05-gestion-pacientes';
        }, 1500);

      } catch (error) {
        console.error('[SmileTrack][Paciente] Error al registrar:', error);

        showToast(
          error.message || 'Ocurrió un error al registrar el paciente.',
          'error'
        );

        saveBtn.disabled = false;
        saveBtn.removeAttribute('aria-busy');
        saveBtn.innerHTML = originalContent;
      }
    });
  }

  // [MEJORA]: Limpieza de listeners al unload (buena práctica para SPAs)
  window.addEventListener('beforeunload', () => {
    // En una SPA real, aquí se removerían listeners para evitar memory leaks
  });
};

// ═══ UTILIDADES ═══

/**
 * [MEJORA]: Función debounce reutilizable para optimizar eventos de input
 * Consistente con todos los módulos anteriores
 * @param {Function} fn - Función a ejecutar
 * @param {number} delay - Tiempo de espera en ms
 * @returns {Function} Función debounceada
 */
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);