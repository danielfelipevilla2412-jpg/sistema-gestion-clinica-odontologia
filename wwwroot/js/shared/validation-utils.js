/* ============================================
   SmileTrack — Utilidades de Validación Compartidas (shared/validation-utils.js)
   ============================================
   Helpers para validación común en formularios.
   ============================================ */

const ValidationUtils = (function () {
    
    /**
     * Valida si un correo electrónico tiene un formato correcto básico.
     * @param {string} email
     * @returns {boolean}
     */
    function isValidEmail(email) {
        if (!email) return false;
        // Regex básico para email
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(String(email).toLowerCase());
    }

    /**
     * Valida la fortaleza de una contraseña.
     * Criterios: min 8 chars, 1 mayúscula, 1 minúscula, 1 número.
     * @param {string} password 
     * @returns {Object} { isValid: boolean, message: string }
     */
    function validatePasswordStrength(password) {
        if (!password || password.length < 8) {
            return { isValid: false, message: "La contraseña debe tener al menos 8 caracteres." };
        }
        if (!/[A-Z]/.test(password)) {
            return { isValid: false, message: "La contraseña debe incluir al menos una letra mayúscula." };
        }
        if (!/[a-z]/.test(password)) {
            return { isValid: false, message: "La contraseña debe incluir al menos una letra minúscula." };
        }
        if (!/[0-9]/.test(password)) {
            return { isValid: false, message: "La contraseña debe incluir al menos un número." };
        }
        return { isValid: true, message: "Contraseña válida." };
    }

    /**
     * Valida un número de teléfono (solo dígitos, longitud específica opcional)
     * @param {string} phone
     * @param {number} minLength (default 7)
     * @param {number} maxLength (default 15)
     * @returns {boolean}
     */
    function isValidPhone(phone, minLength = 7, maxLength = 15) {
        if (!phone) return false;
        const cleanPhone = phone.replace(/\D/g, '');
        return cleanPhone.length >= minLength && cleanPhone.length <= maxLength;
    }

    /**
     * Verifica que dos campos sean iguales (ej. contraseña y confirmar)
     * @param {string} val1 
     * @param {string} val2 
     * @returns {boolean}
     */
    function areEqual(val1, val2) {
        return val1 === val2;
    }

    /**
 * Restringe un campo para que solo se puedan escribir letras (incluye tildes, ñ y espacios).
 * Bloquea números y símbolos en tiempo real, mientras el usuario escribe.
 * @param {HTMLElement} inputElement
 */
function restrictToLetters(inputElement) {
    if (!inputElement) return;
    inputElement.addEventListener('input', () => {
        const start = inputElement.selectionStart;
        const original = inputElement.value;
        const cleaned = original.replace(/[^A-Za-zÀ-ÖØ-öø-ÿñÑ\s]/g, '');
        if (cleaned !== original) {
            inputElement.value = cleaned;
            const diff = original.length - cleaned.length;
            inputElement.setSelectionRange(start - diff, start - diff);
        }
    });
}

/**
 * Restringe un campo para que solo se puedan escribir números.
 * @param {HTMLElement} inputElement
 */
function restrictToNumbers(inputElement) {
    if (!inputElement) return;
    inputElement.addEventListener('input', () => {
        const start = inputElement.selectionStart;
        const original = inputElement.value;
        const cleaned = original.replace(/[^0-9]/g, '');
        if (cleaned !== original) {
            inputElement.value = cleaned;
            const diff = original.length - cleaned.length;
            inputElement.setSelectionRange(start - diff, start - diff);
        }
    });
}

/**
 * Restringe un campo de teléfono: números y espacios, más un "+" solo al inicio.
 * @param {HTMLElement} inputElement
 */
function restrictToPhone(inputElement) {
    if (!inputElement) return;
    inputElement.addEventListener('input', () => {
        const start = inputElement.selectionStart;
        const original = inputElement.value;
        let cleaned = original.replace(/[^0-9+\s]/g, '');
        cleaned = cleaned.replace(/(?!^)\+/g, '');
        if (cleaned !== original) {
            inputElement.value = cleaned;
            const diff = original.length - cleaned.length;
            inputElement.setSelectionRange(start - diff, start - diff);
        }
    });
}

    const _DANGER_VAR = 'var(--danger, #ef4444)';
    const _SUCCESS_VAR = 'var(--success, #22c55e)';
    const _BORDER_VAR = 'var(--border, #e2e8f0)';
    const _PRIMARY_RING = 'var(--input-focus-ring, rgba(26, 86, 204, 0.15))';
    const _DANGER_RING = 'rgba(239, 68, 68, 0.15)';
    const _SUCCESS_RING = 'rgba(34, 197, 94, 0.15)';
    const _MSG_CLASS = 'validation-message-inline';

    function _ensureMessage(input, errorElement) {
        if (errorElement && errorElement instanceof HTMLElement) return errorElement;
        if (!(input instanceof HTMLElement)) return null;
        const existing = input.parentNode?.querySelector(':scope > .' + _MSG_CLASS);
        if (existing) return existing;
        const span = document.createElement('span');
        span.className = _MSG_CLASS;
        span.setAttribute('role', 'alert');
        span.style.cssText = [
            'display:block',
            'margin-top:4px',
            'font-size:.72rem',
            'line-height:1.2',
            'color:var(--danger,#ef4444)',
            'font-weight:500'
        ].join(';');
        input.parentNode?.insertBefore(span, input.nextSibling);
        return span;
    }

    function _setInputState(input, state) {
        if (!input || !(input instanceof HTMLElement)) return;
        input.removeAttribute('data-validation');
        input.style.borderColor = '';
        input.style.boxShadow = '';
        if (state === 'error') {
            input.setAttribute('data-validation', 'error');
            input.setAttribute('aria-invalid', 'true');
            input.style.borderColor = _DANGER_VAR;
            input.style.boxShadow = `0 0 0 3px ${_DANGER_RING}`;
        } else if (state === 'success') {
            input.setAttribute('data-validation', 'success');
            input.removeAttribute('aria-invalid');
            input.style.borderColor = _SUCCESS_VAR;
            input.style.boxShadow = `0 0 0 3px ${_SUCCESS_RING}`;
        } else {
            input.removeAttribute('aria-invalid');
            input.style.borderColor = _BORDER_VAR;
            input.style.boxShadow = '';
        }
    }

    /**
     * Añade estado de error visual a un input y muestra un mensaje
     * @param {HTMLElement} inputElement 
     * @param {HTMLElement} [errorElement] (opcional) elemento para mostrar el texto
     * @param {string} [message]
     */
    function showError(inputElement, errorElement, message) {
        if (!inputElement) return;
        _setInputState(inputElement, 'error');
        const msgEl = _ensureMessage(inputElement, errorElement);
        if (msgEl && message) {
            msgEl.textContent = message;
            msgEl.style.display = 'block';
            msgEl.style.color = _DANGER_VAR;
            if (inputElement.id && !msgEl.id) {
                msgEl.id = inputElement.id + '-error';
                const prev = inputElement.getAttribute('aria-describedby') || '';
                if (!prev.includes(msgEl.id)) {
                    inputElement.setAttribute('aria-describedby', prev ? prev + ' ' + msgEl.id : msgEl.id);
                }
            }
        } else if (msgEl && !message) {
            msgEl.textContent = '';
            msgEl.style.display = 'none';
        }
    }

    /**
     * Remueve estado de error visual de un input
     * @param {HTMLElement} inputElement 
     * @param {HTMLElement} [errorElement]
     */
    function clearError(inputElement, errorElement) {
        if (!inputElement) return;
        _setInputState(inputElement, null);
        const msgEl = _ensureMessage(inputElement, errorElement);
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.style.display = 'none';
            if (msgEl.id) {
                const prev = inputElement.getAttribute('aria-describedby') || '';
                const cleaned = prev.split(' ').filter(p => p && p !== msgEl.id).join(' ');
                if (cleaned) inputElement.setAttribute('aria-describedby', cleaned);
                else inputElement.removeAttribute('aria-describedby');
            }
        }
    }

    /**
     * Añade estado de éxito visual a un input
     * @param {HTMLElement} inputElement 
     * @param {HTMLElement} [errorElement]
     * @param {string} [message='']
     */
    function showSuccess(inputElement, errorElement, message = '') {
        if (!inputElement) return;
        _setInputState(inputElement, 'success');
        const msgEl = _ensureMessage(inputElement, errorElement);
        if (msgEl && message) {
            msgEl.textContent = message;
            msgEl.style.display = 'block';
            msgEl.style.color = _SUCCESS_VAR;
        } else if (msgEl) {
            msgEl.textContent = '';
            msgEl.style.display = 'none';
        }
    }

    return {
        isValidEmail,
        validatePasswordStrength,
        isValidPhone,
        areEqual,
        showError,
        clearError,
        showSuccess,
        restrictToLetters,
        restrictToNumbers,
        restrictToPhone
    };
})();

window.ValidationUtils = ValidationUtils;
