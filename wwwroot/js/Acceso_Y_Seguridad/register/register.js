/**
 * ════════════════════════════════════════════════════════
 * SMILETRACK — LÓGICA DE REGISTRO DE USUARIOS
 * Archivo: views/auth/register/register.js
 * Propósito: Validar formulario, mostrar fortaleza de contraseña y manejar envío
 * ════════════════════════════════════════════════════════
    // El registro de service worker se elimina porque no hay `sw.js` implementado
    // y registrar uno sin archivo provoca errores en consola en entornos de producción.

/**
 * Clase principal que encapsula toda la funcionalidad del registro
 * Patrón: Clase con constructor para inicialización organizada y mantenible
 */
class DentalRegister {

    /**
     * Constructor: se ejecuta al crear la instancia de la clase
     * Aquí guardamos referencias a los elementos del DOM que usaremos frecuentemente
     */
    constructor() {
        // Referencia al formulario principal de registro
        this.form = document.getElementById('registerForm');
        // Campo de entrada para la contraseña
        this.passwordInput = document.getElementById('password');
        // Campo para confirmar la contraseña
        this.confirmPassword = document.getElementById('confirm-password');
        // Botón para mostrar/ocultar la contraseña
        this.passwordToggle = document.getElementById('togglePassword');
        // Barra visual que muestra la fortaleza de la contraseña
        this.passwordStrength = document.getElementById('passwordStrength');
        this.passwordCriterionLength = document.getElementById('passwordCriterionLength');
        this.passwordCriterionUpper = document.getElementById('passwordCriterionUpper');
        this.passwordCriterionNumber = document.getElementById('passwordCriterionNumber');
        this.passwordCriterionSymbol = document.getElementById('passwordCriterionSymbol');
        // Botón de envío del formulario
        this.registerBtn = document.getElementById('registerBtn');
        // Checkbox de aceptación de términos y condiciones
        this.termsCheckbox = document.getElementById('terms');
        // Select de rol elegido por el usuario
        this.roleSelect = document.getElementById('role');
        this.roleError = document.getElementById('roleError');
        // Contenedor de error global del formulario
        this.registerGlobalError = document.getElementById('registerGlobalError');

        // Iniciar la aplicación vinculando eventos y configurando estado inicial
        this.init();
    }

    /**
     * Método de inicialización: configura eventos y estado inicial de la interfaz
     */
    init() {
        // Vincular todos los eventos a sus funciones correspondientes
        this.bindEvents();
        // Ajustar estado inicial del botón de envío según aceptación de términos
        this.toggleSubmitButton();
        // Enfocar automáticamente el primer campo para mejor experiencia de usuario
        this.autoFocus();
    }

    /**
     * Vincula todos los eventos de la interfaz con sus funciones handler
     * Separar eventos en un método propio hace el código más organizado y fácil de mantener
     */
    bindEvents() {
        // Toggle para mostrar/ocultar contraseña al hacer clic en el icono del ojo
        this.passwordToggle.addEventListener('click', () => this.togglePassword());

        // Actualizar barra de fortaleza mientras el usuario escribe la contraseña
        this.passwordInput.addEventListener('input', () => this.updatePasswordStrength());

        // Validar que la confirmación de contraseña coincida con la original
        this.confirmPassword.addEventListener('input', () => this.validateConfirmPassword());

        // Manejar el envío del formulario cuando el usuario hace clic en "Crear Cuenta"
        this.form.addEventListener('submit', (e) => this.handleSubmit(e));

        // Validación en tiempo real para todos los campos requeridos del formulario
        this.form.querySelectorAll('input, select').forEach(field => {
            field.addEventListener('blur', () => this.validateField(field));
            field.addEventListener('input', () => this.validateField(field));
            if (field.name === 'email') {
                field.addEventListener('input', () => this.validateField(field));
            }
        });

        // Validación del selector de rol y limpieza de error asociado
        this.roleSelect?.addEventListener('change', () => this.validateRole());

        // Habilitar/deshabilitar botón de registro según si se aceptan los términos
        this.termsCheckbox.addEventListener('change', () => this.toggleSubmitButton());
        // Limpiar error global cuando el usuario interactúa nuevamente con el formulario
        this.form.querySelectorAll('input, select').forEach(field => {
            field.addEventListener('input', () => this.clearGlobalError());
            field.addEventListener('change', () => this.clearGlobalError());
        });
    }

    /**
     * Alterna la visibilidad de la contraseña entre texto plano y asteriscos
     * También cambia el icono del botón entre "ojo abierto" y "ojo tachado"
     */
    togglePassword() {
        // Cambiar el tipo de input entre password y text
        const type = this.passwordInput.type === 'password' ? 'text' : 'password';
        this.passwordInput.type = type;

        // Actualizar el icono de material-symbols según el estado
        const icon = this.passwordToggle.querySelector('.material-symbols-outlined');
        if (icon) {
            icon.textContent = type === 'password' ? 'visibility' : 'visibility_off';
        }

        const label = type === 'password' ? 'Mostrar contraseña' : 'Ocultar contraseña';
        this.passwordToggle.setAttribute('aria-label', label);
    }

    /**
     * Calcula y muestra visualmente la fortaleza de la contraseña ingresada
     * Se ejecuta en cada tecla pulsada en el campo de contraseña
     */
    updatePasswordStrength() {
        const password = this.passwordInput.value;
        const strength = this.calculateStrength(password);

        this.passwordStrength.className = `mt-3 h-2 w-full overflow-hidden rounded-full ${strength.class}`;
        this.passwordStrength.style.width = `${strength.width}%`;
        this.passwordStrength.classList.remove('hidden');

        this.passwordCriterionLength.classList.toggle('text-green-400', password.length >= 8);
        this.passwordCriterionUpper.classList.toggle('text-green-400', /[A-Z]/.test(password));
        this.passwordCriterionNumber.classList.toggle('text-green-400', /[0-9]/.test(password));
        this.passwordCriterionSymbol.classList.toggle('text-green-400', /[^A-Za-z0-9]/.test(password));

        if (!password) {
            this.passwordStrength.classList.add('hidden');
            [
                this.passwordCriterionLength,
                this.passwordCriterionUpper,
                this.passwordCriterionNumber,
                this.passwordCriterionSymbol
            ].forEach(el => el.classList.remove('text-green-400'));
        }

        this.validateField(this.passwordInput);
    }

    /**
     * Calcula un puntaje de fortaleza basado en criterios de seguridad estándar
     * @param {string} password - La contraseña a evaluar
     * @returns {object} Objeto con ancho porcentual y clase de color para la barra visual
     */
    calculateStrength(password) {
        let score = 0;
        // Criterios de evaluación: longitud, minúsculas, mayúsculas, números, caracteres especiales
        if (password.length >= 8) score++;
        if (/[a-z]/.test(password)) score++;
        if (/[A-Z]/.test(password)) score++;
        if (/[0-9]/.test(password)) score++;
        if (/[^A-Za-z0-9]/.test(password)) score++;

        // Retornar configuración visual según el puntaje obtenido
        if (score <= 2) return { width: 33, class: 'strength-weak' };   /* Débil */
        if (score <= 4) return { width: 66, class: 'strength-medium' }; /* Media */
        return { width: 100, class: 'strength-strong' };                /* Fuerte */
    }

    /**
     * Valida que la confirmación de contraseña coincida exactamente con la contraseña original
     * Aplica estilos visuales de éxito o error según el resultado
     */
    validateConfirmPassword() {
        const password = this.passwordInput.value;
        const confirm = this.confirmPassword.value;

        if (confirm && confirm !== password) {
            this.confirmPassword.classList.add('border-red-300', 'ring-2', 'ring-red-200');
            this.confirmPassword.classList.remove('border-green-300', 'ring-green-200');
        } else if (confirm && confirm === password) {
            this.confirmPassword.classList.remove('border-red-300', 'ring-2', 'ring-red-200');
            this.confirmPassword.classList.add('border-green-300', 'ring-2', 'ring-green-200');
        } else {
            this.confirmPassword.classList.remove('border-red-300', 'ring-2', 'ring-red-200', 'border-green-300', 'ring-green-200');
        }
    }

    /**
     * Valida un campo individual según reglas específicas para cada tipo de dato
     * @param {HTMLElement} field - El elemento input o select a validar
     */
    validateField(field) {
        const value = field.value.trim();
        // Limpiar clases de validación previas antes de aplicar nuevas
        field.classList.remove('border-red-300', 'ring-red-200', 'border-green-300', 'ring-green-200');

        let isValid = true;

        // Reglas de validación específicas según el nombre del campo
        switch (field.name) {
            case 'Correo':
                isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
                break;
            case 'Telefono':
                isValid = /^\+?[1-9]\d{1,14}$/.test(value.replace(/[\s()-]/g, ''));
                break;
            case 'Contrasena':
                isValid = value.length >= 8;
                break;
                case 'ConfirmarContrasena':
                isValid = value === this.passwordInput.value;
                break;
            case 'Rol':
                isValid = value !== '';
                break;
            default:
                isValid = value.length >= 2;
        }

        if (isValid && value) {
            field.classList.add('border-green-300', 'ring-2', 'ring-green-200/50');
        } else if (field.hasAttribute('required') && !value) {
            field.classList.add('border-red-300', 'ring-2', 'ring-red-200/50');
        }
    }

    /**
     * Habilita o deshabilita el botón de registro según el estado del checkbox de términos
     * También aplica estilos visuales de estado deshabilitado cuando corresponde
     */
    toggleSubmitButton() {
        this.registerBtn.disabled = !this.termsCheckbox.checked;
        this.registerBtn.classList.toggle('opacity-50', !this.termsCheckbox.checked);
        this.registerBtn.classList.toggle('cursor-not-allowed', !this.termsCheckbox.checked);
    }

    /**
     * Maneja el envío del formulario: valida, muestra carga y procesa el registro
     * @param {Event} e - Evento de submit del formulario
     */
    async handleSubmit(e) {
        // Prevenir comportamiento por defecto (recarga de página)
        e.preventDefault();

        // Validación final: verificar que todos los campos requeridos tengan valor
        let isValid = true;
        this.form.querySelectorAll('[required]').forEach(field => {
            if (!field.value.trim()) {
                this.validateField(field);
                isValid = false;
            }
        });

        // Si hay campos vacíos, mostrar mensaje de error y detener ejecución
        if (!isValid) {
            this.setGlobalError('Completa todos los campos requeridos antes de continuar');
            return;
        }

        // Verificar que las contraseñas coincidan antes de enviar
        if (this.passwordInput.value !== this.confirmPassword.value) {
            this.setGlobalError('Las contraseñas no coinciden. Revisa el campo de confirmación.');
            return;
        }

        if (!/^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(this.passwordInput.value)) {
            this.setGlobalError('La contraseña debe tener mínimo 8 caracteres, una mayúscula, un número y un símbolo.');
            return;
        }

        // Validar rol seleccionado antes de enviar
        if (!this.validateRole()) {
            this.setGlobalError('Selecciona un rol válido antes de continuar.');
            return;
        }

        // Activar estado de carga: mostrar spinner y deshabilitar botón
        this.setLoading(true);

        try {
            const formData = new FormData(this.form);
            const urlParams = new URLSearchParams(formData);
            
            const response = await fetch(this.form.action, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'Accept': 'application/json'
                },
                body: urlParams
            });
            
            const data = await response.json().catch(() => null);

            if (response.ok && data?.success) {
                this.showNotification('¡Cuenta creada exitosamente!', 'success');
                setTimeout(() => {
                    window.location.href = data.redirectUrl || "/acceso-y-seguridad/login";
                }, 2000);
            } else {
                this.showNotification(data?.message || 'Error al crear cuenta. Revisa tus datos.', 'error');
            }
        } catch (error) {
            console.error(error);
            this.setGlobalError('Error de conexión con el servidor. Inténtalo nuevamente.');
        } finally {
            this.setLoading(false);
        }
    }

    /**
     * Alterna el estado visual de carga del botón de registro
     * @param {boolean} loading - true para mostrar spinner, false para restaurar texto original
     */
    setLoading(loading) {
        const btn = this.registerBtn;
        if (loading) {
            // Guardar HTML original para poder restaurarlo después
            const original = btn.innerHTML;
            btn.dataset.original = original;
            // Reemplazar contenido con spinner animado y texto de carga
            btn.innerHTML = `
                <div class="loading">
                    <div class="spinner"></div>
                    Creando cuenta...
                </div>
            `;
            btn.disabled = true;
        } else {
            // Restaurar contenido y estado original del botón
            btn.innerHTML = btn.dataset.original || 'Crear Cuenta';
            btn.disabled = !this.termsCheckbox.checked;
        }
    }

    // Removido: simulateApiCall

    showNotification(message, type = 'info') {
        if (window.ToastService) {
            if (type === 'success') window.ToastService.success('Éxito', message);
            else if (type === 'error') window.ToastService.error('Error', message);
            else if (type === 'warning') window.ToastService.warning('Advertencia', message);
            else window.ToastService.info('Información', message);
        } else {
            alert(message);
        }
    }

    validateRole() {
        if (!this.roleSelect) return true;

        const selectedValue = this.roleSelect.value?.trim();
        const isValid = selectedValue !== '';

        if (!isValid) {
            this.roleSelect.classList.add('border-red-300', 'ring-2', 'ring-red-200/50');
            this.roleError?.classList.remove('hidden');
        } else {
            this.roleSelect.classList.remove('border-red-300', 'ring-2', 'ring-red-200/50');
            this.roleSelect.classList.add('border-green-300', 'ring-2', 'ring-green-200/50');
            this.roleError?.classList.add('hidden');
        }

        return isValid;
    }

    setGlobalError(message) {
        if (this.registerGlobalError) {
            this.registerGlobalError.textContent = message;
            this.registerGlobalError.classList.remove('hidden');
        }
        this.showNotification(message, 'error');
    }

    clearGlobalError() {
        if (this.registerGlobalError) {
            this.registerGlobalError.textContent = '';
            this.registerGlobalError.classList.add('hidden');
        }
    }

    /**
     * Enfoca automáticamente el primer campo del formulario al cargar la página
     * Mejora la experiencia de usuario al permitir escribir inmediatamente sin hacer clic
     */
    autoFocus() {
        document.getElementById('first-name').focus();
    }
}

// ════════════════════════════════════════════════════════
// INICIALIZACIÓN DE LA APLICACIÓN
// Se ejecuta cuando el DOM está completamente cargado y listo para manipular
// ════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', () => {
    new DentalRegister();
});

// ════════════════════════════════════════════════════════
// SERVICE WORKER PARA PWA (OPCIONAL)
// Service worker registration removed: no `sw.js` present in repository.