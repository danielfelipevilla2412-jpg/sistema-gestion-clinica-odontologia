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
     * Añade estado de error visual a un input y muestra un mensaje
     * @param {HTMLElement} inputElement 
     * @param {HTMLElement} errorElement (opcional, para mostrar el texto)
     * @param {string} message 
     */
    function showError(inputElement, errorElement, message) {
        if (!inputElement) return;
        inputElement.classList.add('border-red-500', 'focus:ring-red-500');
        inputElement.classList.remove('border-[#c0c7d4]', 'focus:border-[#005ea4]', 'focus:ring-[#005ea4]/30', 'border-green-500');
        
        if (errorElement) {
            errorElement.textContent = message;
            errorElement.classList.remove('hidden');
            errorElement.classList.add('text-red-500');
            errorElement.classList.remove('text-green-500');
        }
    }

    /**
     * Remueve estado de error visual de un input
     * @param {HTMLElement} inputElement 
     * @param {HTMLElement} errorElement 
     */
    function clearError(inputElement, errorElement) {
        if (!inputElement) return;
        inputElement.classList.remove('border-red-500', 'focus:ring-red-500');
        inputElement.classList.add('border-[#c0c7d4]', 'focus:border-[#005ea4]', 'focus:ring-[#005ea4]/30');
        
        if (errorElement) {
            errorElement.textContent = '';
            errorElement.classList.add('hidden');
        }
    }

    /**
     * Añade estado de éxito visual a un input
     * @param {HTMLElement} inputElement 
     */
    function showSuccess(inputElement, errorElement, message = '') {
        if (!inputElement) return;
        inputElement.classList.remove('border-red-500', 'focus:ring-red-500', 'border-[#c0c7d4]');
        inputElement.classList.add('border-green-500', 'focus:ring-green-500');
        
        if (errorElement && message) {
            errorElement.textContent = message;
            errorElement.classList.remove('hidden', 'text-red-500');
            errorElement.classList.add('text-green-500');
        } else if (errorElement) {
            errorElement.classList.add('hidden');
        }
    }

    return {
        isValidEmail,
        validatePasswordStrength,
        isValidPhone,
        areEqual,
        showError,
        clearError,
        showSuccess
    };
})();

window.ValidationUtils = ValidationUtils;
