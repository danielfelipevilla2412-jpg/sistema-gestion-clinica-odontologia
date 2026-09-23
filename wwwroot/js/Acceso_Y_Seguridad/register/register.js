/* ============================================
   SmileTrack — Lógica de Registro de Pacientes (register.js)
   ============================================
   Autor: Johan Santamaria / SmileTrack Team
   Fecha: 29/07/2026 (Actualizado 2026-09-12)

   DESCRIPCIÓN Y POR QUÉ DE ARQUITECTURA:
   Encapsula la lógica client-side del registro de pacientes usando el patrón de clase (`DentalRegister`).
   Su propósito es garantizar la integridad de los datos ingresados antes de enviarlos al servidor C#,
   evaluar la complejidad de la clave y guiar al usuario mediante feedback visual claro.

   FUNCIONALIDADES PRINCIPALES:
   - Validación en tiempo real de campos obligatorios (nombres, documento, correo institucional/personal).
   - Medidor de Fortaleza de Contraseña (`evaluatePasswordStrength`): Analiza longitud, números, símbolos y mayúsculas.
   - Confirmación de Claves: Valida coincidencia exacta de contraseñas notificando discrepancias en pantalla.
   - Envío AJAX / Form Submit: Maneja el bloqueo del botón y estado de carga (`aria-busy="true"`).

   NOTAS DE MANTENIMIENTO:
   - Se removió el registro de Service Worker no existente para prevenir errores de consola en producción.
   - Toda validación local es re-evaluada por la capa de servicios C# (`PacienteService.cs`) en el backend.
============================================ */

/**
 * Clase principal que encapsula toda la funcionalidad del registro.
 * POR QUÉ Clase: Usar una clase organiza el código en métodos cohesivos, evita variables globales
 * y facilita la extensibilidad futura (p.ej. herencia para un registro de profesionales).
 */
class DentalRegister {

    /**
     * Constructor: se ejecuta al crear la instancia de la clase.
     * POR QUÉ: Centralizamos todas las referencias DOM aquí para:
     *   1. Obtenerlas una única vez (mejor rendimiento que buscar en el DOM en cada evento).
     *   2. Tener visibilidad clara de todos los elementos que maneja esta clase.
     *   3. Facilitar las pruebas unitarias al poder inyectar mocks si fuera necesario.
     */
    constructor() {
        // Formulario principal de registro (contiene todos los campos)
        this.form = document.getElementById('registerForm');

        // Campos de contraseña (original y confirmación)
        this.passwordInput = document.getElementById('password');
        this.confirmPassword = document.getElementById('confirm-password');

        // Botón para mostrar/ocultar la contraseña
        this.passwordToggle = document.getElementById('togglePassword');

        // Barra visual que muestra la fortaleza de la contraseña (roja/amarilla/verde)
        this.passwordStrength = document.getElementById('passwordStrength');

        // Indicadores individuales de criterios de contraseña segura
        this.passwordCriterionLength = document.getElementById('passwordCriterionLength'); // Mínimo 8 caracteres
        this.passwordCriterionUpper  = document.getElementById('passwordCriterionUpper');  // Al menos una mayúscula
        this.passwordCriterionNumber = document.getElementById('passwordCriterionNumber'); // Al menos un número
        this.passwordCriterionSymbol = document.getElementById('passwordCriterionSymbol'); // Al menos un símbolo

        // Botón de envío del formulario (se deshabilita hasta aceptar términos)
        this.registerBtn = document.getElementById('registerBtn');

        // Checkbox de aceptación de términos y condiciones
        // POR QUÉ: La aceptación explícita es un requisito legal (Ley 1581 de protección de datos Colombia)
        this.termsCheckbox = document.getElementById('terms');

        // Select de rol elegido por el usuario (Paciente, Profesional)
        this.roleSelect = document.getElementById('role');
        this.roleError  = document.getElementById('roleError'); // Span de error junto al select de rol

        // Contenedor de error global del formulario (visible cuando múltiples campos fallan)
        this.registerGlobalError = document.getElementById('registerGlobalError');

        // Iniciar la aplicación vinculando eventos y configurando estado inicial
        this.init();
    }

    /**
     * Método de inicialización.
     * POR QUÉ: Separar la inicialización del constructor permite que el constructor
     * sea un simple "punto de entrada" y facilita sobrescribir `init()` en clases derivadas.
     */
    init() {
        // Vincular todos los eventos a sus funciones correspondientes
        this.bindEvents();
        // Ajustar estado inicial del botón de envío según aceptación de términos
        // (al cargar la página el checkbox está sin marcar, entonces el botón estará deshabilitado)
        this.toggleSubmitButton();
        // Enfocar automáticamente el primer campo para mejor experiencia de usuario
        // (el usuario puede empezar a escribir sin hacer clic)
        this.autoFocus();
    }

    /**
     * Vincula todos los eventos de la interfaz con sus funciones handler.
     * POR QUÉ: Centralizar todos los addEventListener() en un único método:
     *   - Facilita auditar qué eventos están activos.
     *   - Evita dispersar los listeners por todo el código.
     *   - Si se necesita "limpiar" todos los listeners, solo hay que refactorizar aquí.
     */
    bindEvents() {
        // Toggle para mostrar/ocultar contraseña al hacer clic en el icono del ojo
        this.passwordToggle.addEventListener('click', () => this.togglePassword());

        // Actualizar barra de fortaleza mientras el usuario escribe la contraseña.
        // El evento 'input' se dispara en cada tecla (a diferencia de 'change' que solo se dispara al perder el foco).
        this.passwordInput.addEventListener('input', () => this.updatePasswordStrength());

        // Validar que la confirmación de contraseña coincida con la original
        this.confirmPassword.addEventListener('input', () => this.validateConfirmPassword());

        // Manejar el envío del formulario cuando el usuario hace clic en "Crear Cuenta"
        this.form.addEventListener('submit', (e) => this.handleSubmit(e));

        // ── RESTRICCIÓN DE TECLADO POR TIPO DE CAMPO ──
        // POR QUÉ: Previene que el usuario ingrese caracteres inválidos en campos como nombres
        // (solo letras) o teléfono (solo números), eliminando la necesidad de mostrar errores
        // por ese tipo de dato incorrecto después de que el usuario ya escribió mucho.
        ValidationUtils.restrictToLetters(document.getElementById('first-name'));  // Solo letras en nombres
        ValidationUtils.restrictToLetters(document.getElementById('last-name'));   // Solo letras en apellidos
        ValidationUtils.restrictToNumbers(document.getElementById('doc-num'));     // Solo números en documento
        ValidationUtils.restrictToPhone(document.getElementById('phone'));         // Solo números y + para teléfono

        // Validación en tiempo real al salir del foco (blur) y al escribir (input)
        // POR QUÉ: El evento 'blur' valida cuando el usuario sale del campo (después de terminar de escribir)
        // mientras que 'input' valida en tiempo real para dar feedback inmediato.
        this.form.querySelectorAll('input, select').forEach(field => {
            field.addEventListener('blur', () => this.validateField(field));
            field.addEventListener('input', () => this.validateField(field));
            if (field.name === 'email') {
                // Doble binding del email para asegurarnos de validar formato al escribir
                field.addEventListener('input', () => this.validateField(field));
            }
        });

        // Validación del selector de rol cuando el usuario lo cambia
        this.roleSelect?.addEventListener('change', () => this.validateRole());

        // Habilitar/deshabilitar botón de registro según si se aceptan los términos.
        // POR QUÉ: El botón deshabilitado previene envíos accidentales antes de aceptar términos.
        this.termsCheckbox.addEventListener('change', () => this.toggleSubmitButton());

        // Limpiar el error global cuando el usuario interactúa nuevamente con el formulario.
        // POR QUÉ: El error global muestra un mensaje general del servidor (p.ej. "correo ya registrado").
        // Debe desaparecer cuando el usuario empieza a corregir sus datos.
        this.form.querySelectorAll('input, select').forEach(field => {
            field.addEventListener('input', () => this.clearGlobalError());
            field.addEventListener('change', () => this.clearGlobalError());
        });
    }

    /**
     * Alterna la visibilidad de la contraseña entre texto plano y asteriscos.
     * POR QUÉ: Cambiar el `type` del input es el método estándar del navegador para esto.
     * No usamos CSS visibility porque los gestores de contraseñas necesitan que el type cambie.
     */
    togglePassword() {
        // Determinamos el nuevo tipo: si era password (oculta), pasa a text (visible)
        const type = this.passwordInput.type === 'password' ? 'text' : 'password';
        this.passwordInput.type = type;

        // Actualizamos el icono de material-symbols según el estado
        const icon = this.passwordToggle.querySelector('.material-symbols-outlined');
        if (icon) {
            // 'visibility' = ojo abierto (contraseña ahora visible), 'visibility_off' = ojo tachado (oculta)
            icon.textContent = type === 'password' ? 'visibility' : 'visibility_off';
        }

        // Actualizamos la etiqueta ARIA del botón para accesibilidad.
        // El label describe la acción del SIGUIENTE clic (no el estado actual).
        const label = type === 'password' ? 'Mostrar contraseña' : 'Ocultar contraseña';
        this.passwordToggle.setAttribute('aria-label', label);
    }

    /**
     * Calcula y muestra visualmente la fortaleza de la contraseña ingresada.
     * Se ejecuta en cada tecla pulsada en el campo de contraseña.
     * POR QUÉ: El feedback visual inmediato (barra de color) ayuda al usuario a crear
     * contraseñas más seguras sin necesidad de leer reglas complejas.
     */
    updatePasswordStrength() {
        const password = this.passwordInput.value;
        const strength = this.calculateStrength(password);

        // Aplicamos la clase de color y el ancho de la barra según el puntaje calculado
        this.passwordStrength.className = `mt-3 h-2 w-full overflow-hidden rounded-full ${strength.class}`;
        this.passwordStrength.style.width = `${strength.width}%`;
        this.passwordStrength.classList.remove('hidden');

        // Actualizamos los indicadores de criterios individuales con verde cuando se cumplen.
        // Usamos `classList.toggle` con el resultado booleáno de cada test (true = agrega la clase).
        this.passwordCriterionLength.classList.toggle('text-green-400', password.length >= 8);
        this.passwordCriterionUpper.classList.toggle('text-green-400', /[A-Z]/.test(password));
        this.passwordCriterionNumber.classList.toggle('text-green-400', /[0-9]/.test(password));
        this.passwordCriterionSymbol.classList.toggle('text-green-400', /[^A-Za-z0-9]/.test(password));

        // Si el campo está vacío, ocultamos la barra y reseteamos todos los indicadores
        if (!password) {
            this.passwordStrength.classList.add('hidden');
            [
                this.passwordCriterionLength,
                this.passwordCriterionUpper,
                this.passwordCriterionNumber,
                this.passwordCriterionSymbol
            ].forEach(el => el.classList.remove('text-green-400'));
        }

        // Re-validamos el campo de contraseña para actualizar el borde rojo/verde
        this.validateField(this.passwordInput);
    }

    /**
     * Calcula un puntaje de fortaleza de la contraseña basado en 4 criterios estándar.
     * POR QUÉ: Los mismos 4 criterios que el usuario ve en pantalla son los que evaluamos,
     * garantizando consistencia entre lo que se muestra y lo que se valida.
     * @param {string} password - La contraseña a evaluar
     * @returns {object} Objeto con ancho porcentual y clase de color para la barra visual
     */
    calculateStrength(password) {
        let score = 0;
        // Criterios de evaluación: exactamente los 4 que se muestran al usuario
        if (password.length >= 8) score++; // +1 por longitud mínima aceptable
        if (/[A-Z]/.test(password)) score++; // +1 por contener al menos una mayúscula
        if (/[0-9]/.test(password)) score++; // +1 por contener al menos un número
        if (/[^A-Za-z0-9]/.test(password)) score++; // +1 por contener al menos un carácter especial

        // Retornar configuración visual según el puntaje obtenido (máximo 4 puntos)
        if (score <= 1) return { width: 33,  class: 'strength-weak' };   // Débil: 1 de 4 criterios
        if (score <= 3) return { width: 66,  class: 'strength-medium' }; // Media: 2-3 criterios
        return             { width: 100, class: 'strength-strong' };     // Fuerte: los 4 criterios
    }

    /**
     * Valida que la confirmación de contraseña coincida exactamente con la contraseña original.
     * POR QUÉ: La discrepancia entre contraseñas es uno de los errores más frustrantes en registro.
     * Dar feedback visual inmediato (rojo/verde) reduce abandonos del formulario.
     */
    validateConfirmPassword() {
        const password = this.passwordInput.value;
        const confirm  = this.confirmPassword.value;

        if (confirm && confirm !== password) {
            // Las contraseñas no coinciden: borde rojo + ring rojo
            this.confirmPassword.classList.add('border-red-300', 'ring-2', 'ring-red-200');
            this.confirmPassword.classList.remove('border-green-300', 'ring-green-200');
        } else if (confirm && confirm === password) {
            // Las contraseñas coinciden: borde verde + ring verde (confirmación positiva)
            this.confirmPassword.classList.remove('border-red-300', 'ring-2', 'ring-red-200');
            this.confirmPassword.classList.add('border-green-300', 'ring-2', 'ring-green-200');
        } else {
            // El campo de confirmación está vacío: estado neutro (sin indicadores de color)
            this.confirmPassword.classList.remove('border-red-300', 'ring-2', 'ring-red-200', 'border-green-300', 'ring-green-200');
        }
    }

    /**
     * Valida un campo individual según reglas específicas por tipo de dato.
     * POR QUÉ: Cada campo tiene validaciones distintas (email vs. teléfono vs. contraseña).
     * Usar un switch centralizado evita duplicar validaciones en cada listener de evento.
     * @param {HTMLElement} field - El elemento input o select a validar
     */
    validateField(field) {
        const value = field.value.trim();
        // Limpiamos clases de validación previas antes de aplicar nuevas (estado neutro)
        field.classList.remove('border-red-300', 'ring-red-200', 'border-green-300', 'ring-green-200');

        let isValid = true;

        // Reglas de validación específicas según el nombre del campo (atributo `name` del input)
        switch (field.name) {
            case 'Correo':
                // Regex básica de email: requiere @, dominio y extensión
                isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
                break;
            case 'Telefono':
                // Acepta formatos internacionales: +57 300 123 4567, (300) 123-4567, etc.
                // Se eliminan espacios, paréntesis y guiones antes de validar
                isValid = /^\+?[1-9]\d{1,14}$/.test(value.replace(/[\s()-]/g, ''));
                break;
            case 'Contrasena':
                // Mínimo 8 caracteres (la validación completa de complejidad se hace en handleSubmit)
                isValid = value.length >= 8;
                break;
            case 'ConfirmarContrasena':
                // Comparamos con el valor actual del campo de contraseña original
                isValid = value === this.passwordInput.value;
                break;
            case 'Rol':
                // El select de rol no debe estar en blanco (valor vacío = opción por defecto no seleccionada)
                isValid = value !== '';
                break;
            default:
                // Para todos los demás campos: mínimo 2 caracteres (nombres, apellidos, documento)
                isValid = value.length >= 2;
        }

        // Aplicamos estilos visuales de feedback según resultado:
        if (isValid && value) {
            // Válido y con contenido: borde verde
            field.classList.add('border-green-300', 'ring-2', 'ring-green-200/50');
        } else if (field.hasAttribute('required') && !value) {
            // Requerido y vacío: borde rojo (solo si el campo es obligatorio)
            field.classList.add('border-red-300', 'ring-2', 'ring-red-200/50');
        }
        // Nota: campos opcionales vacíos no se marcan en rojo
    }

    /**
     * Habilita o deshabilita el botón de registro según el estado del checkbox de términos.
     * POR QUÉ: El botón deshabilitado con opacidad reducida da una señal visual clara
     * de que hay un paso pendiente (aceptar términos) antes de poder continuar.
     * Esto cumple con el Criterio WCAG 1.4.3 de contraste de interfaz.
     */
    toggleSubmitButton() {
        const accepted = this.termsCheckbox.checked;
        this.registerBtn.disabled = !accepted;                              // Deshabilitar si no aceptó
        this.registerBtn.classList.toggle('opacity-50', !accepted);         // Apariencia semi-transparente
        this.registerBtn.classList.toggle('cursor-not-allowed', !accepted); // Cursor de "no permitido"
    }

    /**
     * Maneja el envío del formulario: valida todos los campos, muestra estado de carga
     * y envía los datos al servidor mediante fetch.
     * POR QUÉ: Al interceptar el submit podemos validar antes de llamar al servidor,
     * dar feedback visual de carga y manejar errores del backend sin recargar la página.
     * @param {Event} e - Evento de submit del formulario
     */
    async handleSubmit(e) {
        // Prevenir el envío nativo del formulario (que recargaría la página)
        e.preventDefault();

        // ── VALIDACIÓN FINAL PRE-ENVÍO ──
        // POR QUÉ: Aunque ya validamos en tiempo real, realizamos una validación sincrónica
        // final para capturar cualquier campo que el usuario haya dejado sin tocar.
        let isValid = true;
        this.form.querySelectorAll('[required]').forEach(field => {
            if (!field.value.trim()) {
                this.validateField(field); // Marca el campo visualmente en rojo
                isValid = false;           // Marcamos el formulario como inválido
            }
        });

        // Si hay campos vacíos, mostramos error global y detenemos la ejecución
        if (!isValid) {
            this.setGlobalError('Completa todos los campos requeridos antes de continuar');
            return;
        }

        // Verificamos que las contraseñas coincidan (segunda verificación explícita)
        if (this.passwordInput.value !== this.confirmPassword.value) {
            this.setGlobalError('Las contraseñas no coinciden. Revisa el campo de confirmación.');
            return;
        }

        // Verificación de complejidad mínima de contraseña con regex compleja.
        // POR QUÉ: Esta regex combina todos los criterios en una sola expresión:
        //   (?=.*[A-Z])       al menos una mayúscula
        //   (?=.*\d)          al menos un dígito
        //   (?=.*[^A-Za-z0-9]) al menos un carácter especial
        //   .{8,}             al menos 8 caracteres totales
        if (!/^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(this.passwordInput.value)) {
            this.setGlobalError('La contraseña debe tener mínimo 8 caracteres, una mayúscula, un número y un símbolo.');
            return;
        }

        // Validar rol seleccionado (no puede estar en blanco)
        if (!this.validateRole()) {
            this.setGlobalError('Selecciona un rol válido antes de continuar.');
            return;
        }

        // Activar estado de carga: spinner en botón y deshabilitar para evitar doble envío
        this.setLoading(true);

        try {
            // Convertimos FormData a URLSearchParams para compatibilidad con [ValidateAntiForgeryToken] de ASP.NET
            const formData   = new FormData(this.form);
            const urlParams  = new URLSearchParams(formData);

            const response = await fetch(this.form.action, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded', // Requerido por ASP.NET
                    'Accept': 'application/json'                          // Esperamos JSON del controller
                },
                body: urlParams
            });

            // .catch(() => null) previene error si el servidor devuelve HTML en lugar de JSON
            const data = await response.json().catch(() => null);

            if (response.ok && data?.success) {
                // Registro exitoso: notificamos y redirigimos al login tras 2 segundos
                this.showNotification('¡Cuenta creada exitosamente!', 'success');
                setTimeout(() => {
                    // Redirigimos al login usando la URL del servidor o la ruta por defecto
                    window.location.href = data.redirectUrl || "/acceso-y-seguridad/login";
                }, 2000); // 2 segundos para que el usuario lea el mensaje de éxito
            } else {
                // El servidor rechazó el registro (correo duplicado, documento ya registrado, etc.)
                this.showNotification(data?.message || 'Error al crear cuenta. Revisa tus datos.', 'error');
            }
        } catch (error) {
            // Error de red o fallo inesperado del servidor
            console.error(error);
            this.setGlobalError('Error de conexión con el servidor. Inténtalo nuevamente.');
        } finally {
            // `finally` garantiza que el botón siempre se restaura,
            // incluso si hubo un error inesperado en el bloque try o catch
            this.setLoading(false);
        }
    }

    /**
     * Alterna el estado visual de carga del botón de registro.
     * POR QUÉ: Mostrar un spinner en el botón (en lugar de deshabilitar sin feedback)
     * indica al usuario que la solicitud está siendo procesada y previene confusión.
     * @param {boolean} loading - true para mostrar spinner, false para restaurar texto original
     */
    setLoading(loading) {
        const btn = this.registerBtn;
        if (loading) {
            // Guardamos el HTML original del botón para poder restaurarlo después
            // POR QUÉ: En lugar de hardcodear el HTML del botón aquí, lo guardamos dinámicamente
            // para que sea resistente a cambios en el template HTML.
            const original = btn.innerHTML;
            btn.dataset.original = original;
            // Reemplazamos contenido con spinner animado y texto de carga
            btn.innerHTML = `
                <div class="loading">
                    <div class="spinner"></div>
                    Creando cuenta...
                </div>
            `;
            btn.disabled = true; // Bloqueamos el botón para evitar doble envío
        } else {
            // Restauramos el contenido original del botón guardado en dataset
            btn.innerHTML = btn.dataset.original || 'Crear Cuenta';
            // El botón solo queda habilitado si el usuario ya aceptó los términos
            btn.disabled = !this.termsCheckbox.checked;
        }
    }

    // Removido: simulateApiCall (se reemplazó por la llamada real al endpoint POST)

    /**
     * Muestra una notificación al usuario usando ToastService si está disponible.
     * POR QUÉ: ToastService es un servicio global compartido entre módulos.
     * Si no está cargado (fallo de red, orden de carga), usamos alert() como fallback
     * para garantizar que el usuario siempre reciba el mensaje.
     */
    showNotification(message, type = 'info') {
        if (window.ToastService) {
            if (type === 'success') window.ToastService.success('Éxito', message);
            else if (type === 'error') window.ToastService.error('Error', message);
            else if (type === 'warning') window.ToastService.warning('Advertencia', message);
            else window.ToastService.info('Información', message);
        } else {
            // Fallback de último recurso si ToastService no está disponible
            alert(message);
        }
    }

    /**
     * Valida que se haya seleccionado un rol antes de enviar el formulario.
     * POR QUÉ: El rol determina los permisos del usuario en el sistema.
     * Un registro sin rol asignado crearía una cuenta sin acceso a ninguna sección.
     * @returns {boolean} true si el rol es válido, false si está vacío
     */
    validateRole() {
        if (!this.roleSelect) return true; // Si no hay select de rol, no hay nada que validar

        const selectedValue = this.roleSelect.value?.trim();
        const isValid = selectedValue !== ''; // El select por defecto tiene value="" (opción placeholder)

        if (!isValid) {
            // Marcamos el select con borde rojo y mostramos el mensaje de error
            this.roleSelect.classList.add('border-red-300', 'ring-2', 'ring-red-200/50');
            this.roleError?.classList.remove('hidden'); // El span de error se hace visible
        } else {
            // Rol válido: borde verde y ocultamos el error
            this.roleSelect.classList.remove('border-red-300', 'ring-2', 'ring-red-200/50');
            this.roleSelect.classList.add('border-green-300', 'ring-2', 'ring-green-200/50');
            this.roleError?.classList.add('hidden');
        }

        return isValid;
    }

    /**
     * Muestra un mensaje de error global en el contenedor de errores del formulario.
     * POR QUÉ: Algunos errores del servidor (como "correo ya registrado") afectan al
     * formulario completo, no a un campo específico. Este método los muestra en un
     * área dedicada visible al usuario sin reemplazar el contenido del formulario.
     */
    setGlobalError(message) {
        if (this.registerGlobalError) {
            this.registerGlobalError.textContent = message; // Texto del error
            this.registerGlobalError.classList.remove('hidden'); // Hacemos visible el contenedor
        }
        // Adicionalmente, mostramos el toast para mayor visibilidad
        this.showNotification(message, 'error');
    }

    /**
     * Limpia el mensaje de error global del formulario.
     * POR QUÉ: El error global debe desaparecer cuando el usuario empieza a corregir
     * sus datos para no crear confusión sobre si el error aún aplica.
     */
    clearGlobalError() {
        if (this.registerGlobalError) {
            this.registerGlobalError.textContent = ''; // Limpiamos el texto
            this.registerGlobalError.classList.add('hidden'); // Ocultamos el contenedor
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