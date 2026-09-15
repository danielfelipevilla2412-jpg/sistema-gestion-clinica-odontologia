/* ============================================
   SmileTrack — Lógica de Recuperación de Contraseña en 3 Pasos (recover.js)
   ============================================
   Autor: Johan Santamaria / SmileTrack Team
   Fecha: 29/07/2026 (Actualizado 2026-09-12)

   DESCRIPCIÓN Y POR QUÉ DE ARQUITECTURA:
   Implementa una máquina de estados cliente en JavaScript (`PasswordRecovery`) que coordina el flujo
   de recuperación de credenciales mediante peticiones asíncronas a las APIs REST del sistema.
   Garantiza que el usuario reciba retroalimentación instantánea en cada paso sin perder el contexto visual.

   MÁQUINA DE ESTADOS Y PASOS:
   - Paso 1: Solicitud de código enviando correo a `/acceso-y-seguridad/api/recuperacion/solicitar-codigo`.
   - Paso 2: Validación OTP de 6 dígitos enviando a `/acceso-y-seguridad/api/recuperacion/verificar-codigo`.
     Maneja contador regresivo dinámico de reenvió de correo y limita intentos.
   - Paso 3: Actualización de clave enviando a `/acceso-y-seguridad/api/recuperacion/restablecer-password`.
   - Paso Exitoso: Muestra confirmación y redirige a la pantalla de inicio de sesión.

   SEGURIDAD Y HEADER CSRF:
   - Extrae el token Antiforgery desde la cookie `XSRF-TOKEN` o la etiqueta `<meta name="csrf-request-token">`
     e inyecta el header `X-CSRF-TOKEN` en cada llamada `fetch`.
============================================ */

class PasswordRecovery {
    constructor() {
        // ── REFERENCIAS A LOS PASOS DEL FLUJO MULTI-PASO ──
        // POR QUÉ: El flujo de recuperación tiene 4 estados distintos (pasos 1-3 + éxito).
        // Guardamos todos como un objeto para navegar entre ellos de forma más expresiva
        // que con variables sueltas (this.steps[1] vs this.step1).
        this.steps = {
            1: document.getElementById('step1'),        // Formulario de email
            2: document.getElementById('step2'),        // Formulario de código OTP
            3: document.getElementById('step3'),        // Formulario de nueva contraseña
            success: document.getElementById('successStep') // Pantalla de éxito final
        };

        // Indicadores del progreso en la parte superior (círculos 1-2-3)
        this.indicators = document.querySelectorAll('.step-indicator');
        // Texto descriptivo del paso actual (cambia según el paso)
        this.stepDesc = document.getElementById('stepDescription');

        // ── FORMULARIOS DE CADA PASO ──
        // Referenciamos por ID para que el submit handler de cada uno sea independiente
        this.form1 = document.getElementById('step1'); // Step 1 actúa como form también
        this.form2 = document.getElementById('step2');
        this.form3 = document.getElementById('step3');

        // ── INPUTS DE CADA PASO ──
        this.emailInput       = document.getElementById('email');           // Correo para enviar código
        this.codeInput        = document.getElementById('code');            // Input hidden sincronizado con OTP boxes
        this.newPassInput     = document.getElementById('newPassword');     // Nueva contraseña
        this.confirmPassInput = document.getElementById('confirmPassword'); // Confirmación de nueva contraseña

        // ── ELEMENTOS DE FEEDBACK ──
        this.emailError  = document.getElementById('emailError');  // Mensaje de error bajo el campo de email
        this.codeError   = document.getElementById('codeError');   // Mensaje de error bajo el OTP
        this.passMatch   = document.getElementById('passMatch');    // Texto de coincidencia de contraseñas
        this.maskedEmail = document.getElementById('maskedEmail'); // Muestra el email enmascarado (ab***@x.com)

        // ── BOTONES DE ACCIÓN ──
        this.resendBtn = document.getElementById('resendCode');   // Reenviar código OTP al correo
        this.back1     = document.getElementById('backToStep1'); // Volver al paso 1 desde el paso 2
        this.back2     = document.getElementById('backToStep2'); // Volver al paso 2 desde el paso 3
        this.resetBtn  = document.getElementById('resetBtn');   // Botón de confirmación de nueva contraseña

        // ── ESTADO INTERNO DE LA MÁQUINA ──
        this.currentStep      = 1;    // Paso actual (1, 2 o 3)
        this.verificationCode = null; // Código OTP generado (no se usa en cliente, solo en servidor)
        this.userEmail        = null; // Email guardado en Paso 1 para reutilizar en Paso 2 y 3
        this.resendCooldown   = 0;    // Contador de espera para reenviar (en segundos)

        this.init();
    }

    init() {
        // Vinculamos todos los eventos a sus handlers correspondientes
        this.bindEvents();
        // Configuramos la validación visual de criterios de contraseña del Paso 3
        this.setupPasswordValidation();
        // Enfocamos el campo de email automáticamente al cargar.
        // setTimeout de 300ms permite que la animación de entrada del form termine antes de enfocar.
        setTimeout(() => this.emailInput?.focus(), 300);
    }

    bindEvents() {
        // ── PASO 1: EMAIL ──
        // submit interceptado para enviar el código OTP sin recargar la página
        this.form1?.addEventListener('submit', (e) => this.handleStep1(e));
        // Limpiamos el error de email mientras el usuario corrige el campo
        this.emailInput?.addEventListener('input', () => this.clearError(this.emailError));

        // ── PASO 2: CÓDIGO OTP ──
        // Inicializamos los 6 recuadros individuales del código OTP (auto-avance, pegado, etc.)
        this.initOtpInputs();
        this.form2?.addEventListener('submit', (e) => this.handleStep2(e));
        // Botón "Volver": el usuario puede corregir su email si se equivocó
        this.back1?.addEventListener('click', () => this.goToStep(1));
        // Botón de reenvío: permite solicitar un nuevo código si no llegó o expiró
        this.resendBtn?.addEventListener('click', () => this.resendCode());

        // ── PASO 3: NUEVA CONTRASEÑA ──
        this.form3?.addEventListener('submit', (e) => this.handleStep3(e));
        // Validamos requisitos Y coincidencia en tiempo real mientras escribe la nueva contraseña
        this.newPassInput?.addEventListener('input', () => { this.validatePasswordRequirements(); this.validatePasswordMatch(); });
        // Validamos coincidencia también cuando escribe en el campo de confirmación
        this.confirmPassInput?.addEventListener('input', () => this.validatePasswordMatch());
        this.back2?.addEventListener('click', () => this.goToStep(2));

        // Toggle de visibilidad de contraseña (hay uno en nueva y otro en confirmar)
        document.querySelectorAll('.toggle-password').forEach(btn => {
            btn.addEventListener('click', (e) => this.togglePassword(e));
        });
    }

    // ── RECUADROS OTP (auto-avance, retroceso, pegado) ──────────────
    initOtpInputs() {
        const digits = document.querySelectorAll('.otp-digit');
        if (!digits.length) return;

        const syncHiddenInput = () => {
            const val = Array.from(digits).map(d => d.value).join('');
            if (this.codeInput) this.codeInput.value = val;
        };

        digits.forEach((digit, idx) => {
            // Solo permitir dígitos
            digit.addEventListener('keydown', (e) => {
                // Borrar: retroceder al recuadro anterior y limpiarlo
                if (e.key === 'Backspace') {
                    e.preventDefault();
                    digit.value = '';
                    digit.classList.remove('filled');
                    syncHiddenInput();
                    if (idx > 0) digits[idx - 1].focus();
                    return;
                }
                // Flechas: navegación manual
                if (e.key === 'ArrowLeft' && idx > 0) { e.preventDefault(); digits[idx - 1].focus(); return; }
                if (e.key === 'ArrowRight' && idx < digits.length - 1) { e.preventDefault(); digits[idx + 1].focus(); return; }
                // Bloquear no-dígitos (excepto Tab, Ctrl+V, etc.)
                if (!/^\d$/.test(e.key) && !['Tab', 'Enter'].includes(e.key) && !e.ctrlKey && !e.metaKey) {
                    e.preventDefault();
                }
            });

            digit.addEventListener('input', (e) => {
                // Tomar solo el último carácter ingresado y asegurarse de que sea dígito
                const raw = digit.value.replace(/\D/g, '');
                digit.value = raw ? raw[raw.length - 1] : '';
                digit.classList.toggle('filled', !!digit.value);
                syncHiddenInput();
                this.clearError(this.codeError);
                // Avanzar automáticamente al siguiente recuadro
                if (digit.value && idx < digits.length - 1) {
                    digits[idx + 1].focus();
                }
            });

            // Seleccionar el contenido al hacer focus para facilitar reescritura
            digit.addEventListener('focus', () => digit.select());
        });

        // Soporte de pegado: pegar "123456" distribuye un dígito por recuadro
        digits[0].addEventListener('paste', (e) => {
            e.preventDefault();
            const pasted = (e.clipboardData?.getData('text') ?? '').replace(/\D/g, '').slice(0, 6);
            pasted.split('').forEach((char, i) => {
                if (digits[i]) {
                    digits[i].value = char;
                    digits[i].classList.add('filled');
                }
            });
            syncHiddenInput();
            this.clearError(this.codeError);
            // Mover foco al último recuadro rellenado
            const lastFilled = Math.min(pasted.length, digits.length - 1);
            digits[lastFilled].focus();
        });
    }

    /** Limpia y resetea los recuadros OTP al volver al Paso 2 o al reenviar el código */
    clearOtpInputs() {
        document.querySelectorAll('.otp-digit').forEach(d => {
            d.value = '';
            d.classList.remove('filled', 'error'); // Quitamos clases de estado
        });
        if (this.codeInput) this.codeInput.value = ''; // Limpiamos también el input hidden
    }

    /** Muestra animación de "sacudida" en los recuadros cuando el código es incorrecto.
     * POR QUÉ: La animación de error da feedback táctil sin necesitar texto adicional,
     * indicando visualmente que el código ingresado fue rechazado por el servidor. */
    shakeOtpInputs() {
        document.querySelectorAll('.otp-digit').forEach(d => {
            d.classList.add('error'); // CSS aplica una animación de shake en esta clase
            setTimeout(() => d.classList.remove('error'), 400); // Removemos tras 400ms
        });
    }

    // ── NAVEGACIÓN ENTRE PASOS ──────────────────────────
    goToStep(step) {
        // Ocultar todos los pasos
        Object.values(this.steps).forEach(s => s?.classList.add('hidden'));
        
        // Mostrar paso actual
        this.steps[step]?.classList.remove('hidden');
        this.currentStep = step;
        
        // Actualizar indicadores
        this.indicators.forEach((ind, i) => {
            ind.classList.toggle('active', i + 1 === step);
            ind.classList.toggle('completed', i + 1 < step);
            ind.setAttribute('aria-current', i + 1 === step ? 'step' : 'false');
        });

        // Marcar pasos ocultos para accesibilidad
        Object.entries(this.steps).forEach(([key, stepEl]) => {
            if (!stepEl) return;
            const hidden = key !== String(step);
            stepEl.setAttribute('aria-hidden', hidden ? 'true' : 'false');
        });
        
        // Actualizar descripción
        const descriptions = {
            1: 'Ingresa tu correo electrónico y te enviaremos un código de verificación.',
            2: 'Revisa tu correo e ingresa el código de 6 dígitos que recibiste.',
            3: 'Crea una nueva contraseña segura para tu cuenta.'
        };
        this.stepDesc.textContent = descriptions[step];
        
        // Enfocar primer input del paso
        const firstInput = this.steps[step]?.querySelector('input');
        setTimeout(() => firstInput?.focus(), 100);
    }

    // ── PASO 1: EMAIL ───────────────────────────────────
    async handleStep1(e) {
        e.preventDefault();
        const email = this.emailInput.value.trim();
        
        if (!this.isValidEmail(email)) {
            this.showError(this.emailError, 'Ingresa un correo electrónico válido');
            return;
        }
        
        this.setLoading(this.form1.querySelector('button'), true);
        
        try {
            const response = await this.sendRecoveryCode(email);
            if (!response.success) {
                this.showError(this.emailError, response.message || 'Error al enviar el código. Intenta de nuevo.');
                return;
            }

            this.userEmail = email;
            this.maskedEmail.textContent = this.maskEmail(email);
            this.showToast(response.message || 'Código enviado a tu correo', 'success');
            this.goToStep(2);
            this.codeInput.focus();
        } catch (error) {
            this.showError(this.emailError, 'Error al enviar código. Intenta de nuevo.');
        } finally {
            this.setLoading(this.form1.querySelector('button'), false);
        }
    }

    // ── PASO 2: CÓDIGO ──────────────────────────────────
    async handleStep2(e) {
        e.preventDefault();
        // Leer del input oculto sincronizado por initOtpInputs
        const code = (this.codeInput?.value ?? '').trim();
        if (code.length !== 6 || !/^\d{6}$/.test(code)) {
            this.showError(this.codeError, 'Ingresa los 6 dígitos del código');
            this.shakeOtpInputs?.();
            return;
        }
        this.setLoading(this.form2.querySelector('button[type="submit"]'), true);
        try {
            const response = await this.verifyCode(code);
            if (!response.success) {
                this.showError(this.codeError, response.message || 'Código incorrecto o expirado.');
                this.shakeOtpInputs?.();
                return;
            }
            this.showToast(response.message || 'Código verificado', 'success');
            this.goToStep(3);
            this.newPassInput.focus();
        } catch (error) {
            this.showError(this.codeError, 'Error de verificación. Intenta de nuevo.');
        } finally {
            this.setLoading(this.form2.querySelector('button[type="submit"]'), false);
        }
    }
    async resendCode() {
        if (!this.userEmail) return;
        if (this.resendCooldown > 0) {
            this.showToast(`Espera ${this.resendCooldown} segundos antes de reenviar.`, 'info');
            return;
        }

        this.resendBtn.disabled = true;
        this.resendBtn.textContent = 'Enviando...';

        try {
            const response = await this.sendRecoveryCode(this.userEmail);
            if (!response.success) {
                this.showToast(response.message || 'No se pudo reenviar el código.', 'error');
            } else {
                this.showToast(response.message || 'Nuevo código enviado', 'info');
            }
        } catch (error) {
            this.showToast('No se pudo reenviar el código.', 'error');
        } finally {
            this.resendCooldown = 30;
            const countdown = () => {
                if (this.resendCooldown <= 0) {
                    this.resendBtn.disabled = false;
                    this.resendBtn.textContent = '¿No recibiste el código? Reenviar';
                    return;
                }
                this.resendBtn.textContent = `Reenviar en ${this.resendCooldown}s`;
                this.resendCooldown -= 1;
                setTimeout(countdown, 1000);
            };
            countdown();
        }
    }

    // ── PASO 3: NUEVA CONTRASEÑA ────────────────────────
    setupPasswordValidation() {
        const checks = {
            length: { el: document.getElementById('passLength'), test: v => v.length >= 8 },
            upper: { el: document.getElementById('passUpper'), test: v => /[A-Z]/.test(v) },
            number: { el: document.getElementById('passNumber'), test: v => /[0-9]/.test(v) },
            symbol: { el: document.getElementById('passSymbol'), test: v => /[^A-Za-z0-9]/.test(v) }
        };
        
        this.newPassInput?.addEventListener('input', () => {
            const val = this.newPassInput.value;
            Object.entries(checks).forEach(([key, check]) => {
                if (check.el) check.el.classList.toggle('valid', check.test(val));
            });
            this.validatePasswordMatch();
        });
    }

    validatePasswordMatch() {
        const newPass = this.newPassInput.value;
        const confirm = this.confirmPassInput.value;
        
        if (confirm && newPass !== confirm) {
            this.passMatch.textContent = 'Las contraseñas no coinciden';
            this.passMatch.className = 'text-xs mt-2 text-error';
            this.passMatch.classList.remove('hidden');
            return false;
        } else if (confirm) {
            this.passMatch.textContent = '✓ Las contraseñas coinciden';
            this.passMatch.className = 'text-xs mt-2 text-success';
            this.passMatch.classList.remove('hidden');
            return true;
        }
        this.passMatch.classList.add('hidden');
        return false;
    }

    validatePassword() {
        this.validatePasswordRequirements();
        return this.validatePasswordMatch();
    }

    async handleStep3(e) {
        e.preventDefault();
        
        if (!this.validatePasswordRequirements()) {
            this.showToast('La contraseña no cumple los requisitos', 'error');
            return;
        }
        
        if (!this.validatePasswordMatch()) {
            this.showToast('Las contraseñas no coinciden', 'error');
            return;
        }
        
        if (!this.userEmail) {
            this.showToast('Ocurrió un error. Regresa al paso anterior.', 'error');
            return;
        }

        const codigo = (this.codeInput?.value ?? '').trim();
        if (codigo.length !== 6 || !/^[0-9]{6}$/.test(codigo)) {
            this.showError(this.codeError, 'Ingresa un código válido de 6 dígitos');
            this.goToStep(2);
            return;
        }
        
        this.setLoading(this.resetBtn, true);
        
        try {
            const response = await this.resetPassword(this.userEmail, codigo, this.newPassInput.value);
            if (!response.success) {
                this.showToast(response.message || 'Error al restablecer. Intenta de nuevo.', 'error');
                this.goToStep(2);
                return;
            }

            Object.values(this.steps).forEach(s => s?.classList.add('hidden'));
            this.steps.success?.classList.remove('hidden');
            this.stepDesc.textContent = 'Tu contraseña ha sido actualizada exitosamente.';
            this.showToast(response.message || '¡Contraseña restablecida!', 'success');
        } catch (error) {
            this.showToast('Error al restablecer. Intenta de nuevo.', 'error');
            this.goToStep(2);
        } finally {
            this.setLoading(this.resetBtn, false);
        }
    }

    // ── VALIDACIONES ────────────────────────────────────
    isValidEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) && email.length <= 254;
    }

    validatePasswordRequirements() {
        const val = this.newPassInput.value;
        return val.length >= 8 && /[A-Z]/.test(val) && /[0-9]/.test(val) && /[^A-Za-z0-9]/.test(val);
    }

    togglePassword(e) {
        const btn = e.currentTarget;
        const input = btn.closest('.relative').querySelector('input');
        const icon = btn.querySelector('.visibility-icon');
        
        if (input.type === 'password') {
            input.type = 'text';
            icon.textContent = 'visibility';
        } else {
            input.type = 'password';
            icon.textContent = 'visibility_off';
        }
    }

    // ── UTILIDADES ──────────────────────────────────────
    showError(el, msg) {
        el.textContent = msg;
        el.classList.remove('hidden');
    }

    clearError(el) {
        el?.classList.add('hidden');
    }

    setLoading(btn, loading) {
        if (!btn) return;
        btn.disabled = loading;
        btn.classList.toggle('opacity-70', loading);
        btn.classList.toggle('cursor-wait', loading);
    }

    maskEmail(email) {
        const [user, domain] = email.split('@');
        return `${user.slice(0, 3)}***@${domain}`;
    }

    generateCode() {
        // Deprecated: generation of codes is handled server-side using a cryptographically secure RNG.
        return Math.floor(100000 + Math.random() * 900000).toString();
    }

    getCsrfToken() {
        // Prefer the request token exposed by the server in a meta tag (safer for SPA),
        // fall back to the cookie for backwards compatibility.
        const meta = document.querySelector('meta[name="csrf-request-token"]');
        if (meta && meta.content) return meta.content;
        const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
        return match ? decodeURIComponent(match[2]) : null;
    }

    showToast(message, type = 'info') {
        if (window.ToastService) {
            if (type === 'success') window.ToastService.success('Éxito', message);
            else if (type === 'error') window.ToastService.error('Error', message);
            else if (type === 'warning') window.ToastService.warning('Advertencia', message);
            else window.ToastService.info('Información', message);
        } else {
            alert(message);
        }
    }

    // ── MÉTODOS DE LLAMADAS A LA API ──
    // POR QUÉ: Separamos las llamadas HTTP en métodos propios para:
    //   1. Poder reemplazarlos fácilmente si cambian los endpoints.
    //   2. Mantener la lógica de negocio separada del manejo de HTTP.
    //   3. Facilitar pruebas unitarias mediante mocking de estos métodos.
    // Todos incluyen el token CSRF en el header para proteger contra ataques CSRF.

    async sendRecoveryCode(email) {
        const response = await fetch('/acceso-y-seguridad/recover/send-code', {
            method: 'POST',
            credentials: 'same-origin', // Incluye cookies de sesión en la petición
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': this.getCsrfToken() // Token anti-CSRF obligatorio
            },
            body: JSON.stringify({ correo: email }) // El servidor espera el campo 'correo'
        });

        if (!response.ok) {
            // En error de red (4xx, 5xx), devolvemos un objeto de fallo uniforme
            return { success: false, message: 'No se pudo enviar el código. Intenta más tarde.' };
        }

        return response.json(); // Esperamos { success: true, message: '...' } del servidor
    }

    async verifyCode(code) {
        const response = await fetch('/acceso-y-seguridad/recover/verify-code', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': this.getCsrfToken()
            },
            // Enviamos tanto el email como el código; el servidor los valida juntos
            body: JSON.stringify({ correo: this.userEmail, codigo: code })
        });

        if (!response.ok) {
            // Intentamos parsear el mensaje de error del servidor, si existe
            const body = await response.json().catch(() => null);
            return { success: false, message: body?.message ?? 'No se pudo verificar el código.' };
        }

        return response.json();
    }

    async resetPassword(email, code, newPassword) {
        const response = await fetch('/acceso-y-seguridad/recover/reset-password', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': this.getCsrfToken()
            },
            body: JSON.stringify({
                correo: email,
                codigo: code,
                nuevaContrasena: newPassword,
                // Enviamos también la confirmación para que el servidor pueda verificar coincidencia
                confirmarContrasena: this.confirmPassInput.value
            })
        });

        if (!response.ok) {
            const body = await response.json().catch(() => null);
            return { success: false, message: body?.message ?? 'No se pudo restablecer la contraseña.' };
        }

        return response.json();
    }
}


// ════════════════════════════════════════════════════════
// INICIALIZACIÓN
// ════════════════════════════════════════════════════════
document.addEventListener('DOMContentLoaded', () => {
    new PasswordRecovery();
});
