/**
 * ============================================
 * SmileTrack - Shared Utilities Library
 * ============================================
 * 
 * Funciones compartidas entre módulos para evitar duplicación.
 * Uso: <script src="~/js/shared/utils.js" asp-append-version="true"></script>
 * 
 * Namespace: window.SmileTrack.utils
 * 
 * Autor: Johan Santamaria
 * Fecha: 2026-09-10
 * ============================================
 */

// Crear namespace global para evitar conflictos
window.SmileTrack = window.SmileTrack || {};

/**
 * Objeto utils con todas las funciones compartidas
 */
window.SmileTrack.utils = {
  /**
   * Obtiene un elemento DOM de forma segura sin lanzar excepciones
   * @param {string} id - ID del elemento a buscar
   * @returns {HTMLElement|null} Elemento encontrado o null
   */
  safeGetElement: function(id) {
    const element = document.getElementById(id);
    if (!element) {
      console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
    }
    return element;
  },

  /**
   * Debounce para evitar múltiples ejecuciones rápidas
   * Útil para búsqueda, filtrados, y validación en tiempo real
   * @param {Function} callback - Función a ejecutar
   * @param {number} delay - Milisegundos de espera (default: 250)
   * @returns {Function} Función debounced
   */
  debounce: function(callback, delay = 250) {
    let timeoutId;
    return (...args) => {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => callback.apply(this, args), delay);
    };
  },

  /**
   * Escapa caracteres HTML para prevenir XSS
   * @param {*} value - Valor a escapar (convierte a string)
   * @returns {string} HTML escapado
   */
  escapeHtml: function(value) {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  },

  /**
   * Petición API centralizada con soporte para CSRF (MVC) y JWT (Legacy API)
   * Maneja automáticamente headers, autenticación y parsing de respuesta
   * @param {string} url - URL del endpoint
   * @param {Object} options - Opciones de fetch (method, body, headers, etc.)
   * @returns {Promise} Datos parseados o null si falla
   * @throws {Error} Si la respuesta no es ok (status >= 400)
   */
  apiRequest: async function(url, options = {}) {
    const headers = new Headers(options.headers || {});

    // Headers por defecto
    headers.set('Accept', 'application/json');
    if (options.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    // CSRF Token (MVC/Razor Pages)
    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (csrfToken) {
      headers.set('X-CSRF-TOKEN', csrfToken);
    }

    // JWT Token (Legacy API - si está disponible en sessionStorage)
    try {
      const jwt = sessionStorage.getItem('st_jwt');
      if (jwt) {
        headers.set('Authorization', `Bearer ${jwt}`);
      }
    } catch (e) {
      // sessionStorage puede estar deshabilitado en modo privado
    }

    try {
      const response = await fetch(url, {
        ...options,
        headers,
        credentials: 'same-origin'
      });

      let data;
      try {
        data = await response.json();
      } catch {
        data = null;
      }

      if (!response.ok) {
        const message = data?.message || data?.error || `HTTP ${response.status}`;
        const error = new Error(message);
        error.status = response.status;
        error.response = data;
        throw error;
      }

      return data;
    } catch (error) {
      console.error('[SmileTrack API] Error en', url, ':', error);
      throw error;
    }
  },

  /**
   * Anima un contador desde 0 hasta un valor objetivo
   * Mejora visual que indica cambios dinámicos
   * @param {HTMLElement} el - Elemento a animar
   * @param {number} target - Valor final del contador
   * @param {number} duration - Duración en milisegundos (default: 800)
   */
  _counterIntervals: new WeakMap(),
  animateCounter: function(el, target, duration) {
    if (!el || typeof target !== 'number' || target < 0) {
      return;
    }

    if (el.dataset.animated === '1') {
      return;
    }
    el.dataset.animated = '1';

    if (this._counterIntervals.has(el)) {
      clearInterval(this._counterIntervals.get(el));
    }

    const dur = duration ?? Number(el?.dataset?.duration) ?? 800;
    let current = 0;
    const step = Math.max(1, Math.ceil(target / 30));
    const stepDuration = target > 0 ? dur / (target / step) : 40;

    const fmtAttr = (el.getAttribute('data-format') || '').toLowerCase();
    const isCurrencyCop = fmtAttr === 'currency-cop';
    const fmt = isCurrencyCop
      ? new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })
      : null;

    const interval = setInterval(() => {
      current += step;
      if (current >= target) {
        current = target;
        clearInterval(interval);
        window.SmileTrack.utils._counterIntervals.delete(el);
      }
      if (fmt) {
        el.textContent = fmt.format(current);
      } else {
        el.textContent = current.toLocaleString('es-CO');
      }
    }, stepDuration);

    this._counterIntervals.set(el, interval);
  },

  /**
   * Muestra una notificación tipo toast (esquina inferior)
   * No bloqueante, desaparece automáticamente
   * @param {string} message - Mensaje a mostrar
   * @param {string} type - Tipo: 'info', 'success', 'error', 'warning' (default: 'info')
   * @param {number} duration - Duración en ms antes de cerrar (0 = no cierra, default: 3000)
   * @returns {HTMLElement} Elemento del toast
   */
  showToast: function(message, type = 'info', duration = 3000) {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'polite');
    toast.setAttribute('aria-atomic', 'true');
    
    const messageEl = document.createElement('span');
    messageEl.textContent = message;
    toast.appendChild(messageEl);

    const closeBtn = document.createElement('button');
    closeBtn.textContent = '×';
    closeBtn.className = 'toast-close';
    closeBtn.setAttribute('aria-label', 'Cerrar notificación');
    closeBtn.onclick = () => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 300);
    };
    toast.appendChild(closeBtn);

    document.body.appendChild(toast);

    // Trigger animation
    setTimeout(() => {
      toast.classList.add('show');
    }, 10);

    // Auto close después del delay
    if (duration > 0) {
      setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
      }, duration);
    }

    return toast;
  },

  /**
   * Abre un modal de forma accesible
   * @param {string} modalId - ID del elemento modal
   * @param {string} focusId - ID del elemento que recibe focus al cerrar (opcional)
   */
  openModal: function(modalId, focusId = null) {
    const modal = document.getElementById(modalId);
    if (!modal) {
      console.warn(`[SmileTrack] Modal no encontrado: #${modalId}`);
      return;
    }

    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.style.overflow = 'hidden';

    // Focus en el primer input del modal
    const firstInput = modal.querySelector('input, select, textarea, button');
    if (firstInput) {
      firstInput.focus();
    }

    // Almacenar ID del elemento que abre modal (para focus al cerrar)
    modal._triggerElement = focusId ? document.getElementById(focusId) : document.activeElement;

    // Cerrar al hacer click fuera
    const handleClickOutside = (e) => {
      if (e.target === modal) {
        this.closeModal(modalId);
        modal.removeEventListener('click', handleClickOutside);
      }
    };
    modal.addEventListener('click', handleClickOutside);
  },

  /**
   * Cierra un modal de forma accesible
   * @param {string} modalId - ID del elemento modal
   */
  closeModal: function(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;

    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = 'auto';

    // Restaurar focus al elemento que abrió el modal
    if (modal._triggerElement && modal._triggerElement.focus) {
      modal._triggerElement.focus();
    }
  },

  /**
   * Valida un formulario y muestra errores accesibles
   * @param {HTMLFormElement} form - Formulario a validar
   * @returns {boolean} True si es válido, false si tiene errores
   */
  validateForm: function(form) {
    if (!form) return false;

    let isValid = true;
    const errors = [];

    // Limpiar errores previos
    form.querySelectorAll('.error-message').forEach(el => {
      el.style.display = 'none';
    });

    // Validar campos requeridos
    form.querySelectorAll('[required]').forEach(field => {
      if (!field.value.trim()) {
        isValid = false;
        const errorId = field.getAttribute('aria-describedby');
        if (errorId) {
          const errorEl = document.getElementById(errorId);
          if (errorEl) {
            errorEl.style.display = 'block';
            field.setAttribute('aria-invalid', 'true');
          }
        }
        errors.push(`El campo ${field.name} es requerido.`);
      } else {
        field.setAttribute('aria-invalid', 'false');
      }
    });

    // Validar emails
    form.querySelectorAll('input[type="email"]').forEach(field => {
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
      if (field.value && !emailRegex.test(field.value)) {
        isValid = false;
        const errorId = field.getAttribute('aria-describedby');
        if (errorId) {
          const errorEl = document.getElementById(errorId);
          if (errorEl) {
            errorEl.textContent = 'Email inválido';
            errorEl.style.display = 'block';
          }
        }
        field.setAttribute('aria-invalid', 'true');
      }
    });

    return isValid;
  },

  /**
   * Formatea un número como moneda (COP)
   * @param {number} value - Valor a formatear
   * @returns {string} Valor formateado
   */
  formatCurrency: function(value) {
    return new Intl.NumberFormat('es-CO', {
      style: 'currency',
      currency: 'COP',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0
    }).format(value);
  },

  /**
   * Formatea una fecha según locale
   * @param {Date|string} date - Fecha a formatear
   * @param {string} format - Formato 'short', 'long', 'datetime' (default: 'short')
   * @returns {string} Fecha formateada
   */
  formatDate: function(date, format = 'short') {
    if (typeof date === 'string') {
      date = new Date(date);
    }

    const options = {
      'short': { year: 'numeric', month: '2-digit', day: '2-digit' },
      'long': { year: 'numeric', month: 'long', day: 'numeric' },
      'datetime': { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }
    };

    return new Intl.DateTimeFormat('es-CO', options[format] || options['short']).format(date);
  }
};

// Crear aliases globales para retrocompatibilidad (acceso directo)
window.safeGetElement = window.SmileTrack.utils.safeGetElement;
window.debounce = window.SmileTrack.utils.debounce;
window.escapeHtml = window.SmileTrack.utils.escapeHtml;
window.apiRequest = window.SmileTrack.utils.apiRequest;
window.animateCounter = window.SmileTrack.utils.animateCounter;
window.showToast = window.SmileTrack.utils.showToast;
window.openModal = window.SmileTrack.utils.openModal;
window.closeModal = window.SmileTrack.utils.closeModal;
window.validateForm = window.SmileTrack.utils.validateForm;

console.log('[SmileTrack] Utils library loaded successfully');
