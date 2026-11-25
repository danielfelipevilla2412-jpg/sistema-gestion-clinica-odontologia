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
 * [MEJORA]: Notificación delegada al sistema compartido (window.ToastService).
 * El contenedor de toasts lo provee el shell compartido (_Toasts.cshtml),
 * por lo que esta vista ya no administra un #toastContainer propio.
 * @param {string} message - Mensaje a mostrar
 * @param {'success'|'error'} type - Tipo de toast
 */
const showToast = (message, type = 'success') => {
  const service = window.ToastService;
  if (!service || typeof service[type] !== 'function') {
    console.warn('[SmileTrack][Paciente] ToastService no está disponible.');
    return;
  }

  const title = type === 'error' ? 'Error' : 'Éxito';
  service[type](title, message);
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
    validate: (value) => {
      const documentType = document.getElementById('tipoDoc')?.value;
      const normalized = value.trim();
      return documentType === 'PAS'
        ? /^[A-Za-z0-9]{5,20}$/.test(normalized)
        : /^[0-9]{7,15}$/.test(normalized);
    },
    errorMessage: 'Por favor ingresa un documento válido para el tipo seleccionado.'
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
      if (!value.trim()) return true;
      const clean = value.replace(/\s+/g, '');
      return /^[0-9]{7,15}$/.test(clean);
    },
    errorMessage: 'Por favor ingresa un teléfono válido (mínimo 7 dígitos).'
  },
  correo: {
    validate: (value) => !value.trim() || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()),
    errorMessage: 'Por favor ingresa un correo válido.'
  },
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
 * Valida los campos del formulario de registro
 * @returns {boolean} true si todos los campos son válidos
 */
const validateRegistrationForm = () => {
  const requiredFields = ['nombres', 'apellidos', 'tipoDoc', 'documento', 'fechaNacimiento'];
  let allValid = true;

  requiredFields.forEach(fieldId => {
    const field = safeGetElement(fieldId);
    if (field && !validateField(field)) {
      allValid = false;
    }
  });

  const birthDate = safeGetElement('fechaNacimiento')?.value;
  const isMinor = birthDate && new Date(`${birthDate}T00:00:00`) > new Date(new Date().setFullYear(new Date().getFullYear() - 18));
  const requiresResponsible = safeGetElement('tipoDoc')?.value === 'TI' || isMinor;
  if (requiresResponsible) {
    const responsibleName = safeGetElement('contactoEmergencia');
    const responsiblePhone = safeGetElement('telefonoEmergencia');
    const nameValid = Boolean(responsibleName?.value.trim());
    const phoneValid = /^[0-9]{7,15}$/.test((responsiblePhone?.value ?? '').replace(/\s+/g, ''));

    [[responsibleName, 'error-contactoEmergencia', nameValid],
      [responsiblePhone, 'error-telefonoEmergencia', phoneValid]].forEach(([field, errorId, valid]) => {
      if (!field) return;
      field.classList.toggle('error', !valid);
      field.setAttribute('aria-invalid', String(!valid));
      document.getElementById(errorId)?.classList.toggle('visible', !valid);
      if (!valid) allValid = false;
    });
  }

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
  const autoHC = safeGetElement('autoHC');

  // Configurar fechas mínimas/máximas
  const today = new Date().toISOString().split('T')[0];
  const fechaNac = safeGetElement('fechaNacimiento');
  if (fechaNac) {
    fechaNac.max = today;
    // [MEJORA]: Valor por defecto solo si está vacío
  }
  // Inicializar componentes de UI
  initMobileMenu();

  // Configurar validación en tiempo real para los campos del formulario
  Object.keys(FIELD_VALIDATORS).forEach(setupFieldValidation);

  // Botones de cancelar
  const cancelBtn1 = safeGetElement('cancelBtn1');
  cancelBtn1?.addEventListener('click', event => {
    if (!confirm('¿Volver al listado? Se perderán los datos sin guardar.')) event.preventDefault();
  });
  const syncGuardianRequirement = () => {
    const birth = fechaNac.value;
    const cutoff = today.replace(/^\d{4}/, year => String(Number(year) - 18));
    const required = safeGetElement('tipoDoc').value === 'TI' || !!birth && birth > cutoff;
    safeGetElement('guardianRequirement').hidden = !required;
    for (const id of ['contactoEmergencia', 'telefonoEmergencia']) safeGetElement(id).required = required;
  };
  fechaNac.addEventListener('change', syncGuardianRequirement);
  safeGetElement('tipoDoc').addEventListener('change', syncGuardianRequirement);
  syncGuardianRequirement();

  // ── Envío del formulario ──
  const patientForm = safeGetElement('patientForm');
  if (patientForm) {
    patientForm.addEventListener('submit', async (e) => {
      e.preventDefault();

      if (!validateRegistrationForm()) {
        showToast('Corrige los errores antes de guardar.', 'error');
        const firstError = document.querySelector('.form-input.error, .form-select.error');
        if (firstError) firstError.focus();
        return;
      }

      const saveBtn = safeGetElement('saveBtn');
      if (!saveBtn || saveBtn.disabled) return;

      const originalContent = saveBtn.innerHTML;

      // Estado de carga con deshabilitación
      saveBtn.disabled = true;
      saveBtn.setAttribute('aria-busy', 'true');
      saveBtn.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">progress_activity</span> Guardando...';

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

        if (response.status === 409 && Number.isSafeInteger(result.idPacienteExistente)) {
          const link = document.createElement('a');
          link.className = 'btn-secondary';
          link.href = `/gestion-de-pacientes/${result.idPacienteExistente}`;
          link.textContent = 'Abrir ficha existente';
          safeGetElement('duplicatePatientAction')?.remove();
          link.id = 'duplicatePatientAction';
          saveBtn.after(link);
          link.focus();
        }
        if (!response.ok || !result.ok) {
          throw new Error(result.message || 'No fue posible registrar el paciente.');
        }

        showToast(result.message, 'success');
        if (autoHC && result.codigoHistoria) autoHC.textContent = result.codigoHistoria;

        safeGetElement('registrationProfile').href = `/gestion-de-pacientes/${result.idPaciente}`;
        const appointmentLink = safeGetElement('registrationBook');
        if (appointmentLink) appointmentLink.href =
          `/gestion-de-citas/st-rec-03-gestion-citas?nueva=1&pacienteId=${result.idPaciente}`;
        const feedback = safeGetElement('registrationResult');
        patientForm.hidden = true;
        feedback.hidden = false;
        feedback.focus();

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