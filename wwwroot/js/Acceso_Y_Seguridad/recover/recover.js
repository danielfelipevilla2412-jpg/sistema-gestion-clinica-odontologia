/**
 * ════════════════════════════════════════════════════════
 * SMILETRACK — RECUPERAR CONTRASEÑA (3 PASOS)
 * recover.js
 * ════════════════════════════════════════════════════════
 */

class PasswordRecovery {
    constructor() {
        // Referencias DOM
        this.steps = {
            1: document.getElementById('step1'),
            2: document.getElementById('step2'),
            3: document.getElementById('step3'),
            success: document.getElementById('successStep')
        };
        this.indicators = document.querySelectorAll('.step-indicator');
        this.stepDesc = document.getElementById('stepDescription');
        
        // Formularios
        this.form1 = document.getElementById('step1');
        this.form2 = document.getElementById('step2');
        this.form3 = document.getElementById('step3');
        
        // Inputs
        this.emailInput = document.getElementById('email');
        this.codeInput = document.getElementById('code');
        this.newPassInput = document.getElementById('newPassword');
        this.confirmPassInput = document.getElementById('confirmPassword');
        
        // Feedback
        this.emailError = document.getElementById('emailError');
        this.codeError = document.getElementById('codeError');
        this.passMatch = document.getElementById('passMatch');
        this.maskedEmail = document.getElementById('maskedEmail');
        
        // Botones
        this.resendBtn = document.getElementById('resendCode');
        this.back1 = document.getElementById('backToStep1');
        this.back2 = document.getElementById('backToStep2');
        this.resetBtn = document.getElementById('resetBtn');
        
        // Estado
        this.currentStep = 1;
        this.verificationCode = null;
        this.userEmail = null;
        this.resendCooldown = 0;
        
        this.init();
    }

    init() {
        this.bindEvents();
        this.setupPasswordValidation();
        setTimeout(() => this.emailInput?.focus(), 300);
    }

    bindEvents() {
        // Paso 1: Email
        this.form1?.addEventListener('submit', (e) => this.handleStep1(e));
        this.emailInput?.addEventListener('input', () => this.clearError(this.emailError));
        
        // Paso 2: Código — inicializar recuadros OTP
        this.initOtpInputs();
        this.form2?.addEventListener('submit', (e) => this.handleStep2(e));
        this.back1?.addEventListener('click', () => this.goToStep(1));
        this.resendBtn?.addEventListener('click', () => this.resendCode());
        
        // Paso 3: Nueva contraseña
        this.form3?.addEventListener('submit', (e) => this.handleStep3(e));
        this.newPassInput?.addEventListener('input', () => { this.validatePasswordRequirements(); this.validatePasswordMatch(); });
        this.confirmPassInput?.addEventListener('input', () => this.validatePasswordMatch());
        this.back2?.addEventListener('click', () => this.goToStep(2));
        
        // Toggle password visibility
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

    /** Limpia y resetea los recuadros OTP */
    clearOtpInputs() {
        document.querySelectorAll('.otp-digit').forEach(d => {
            d.value = '';
            d.classList.remove('filled', 'error');
        });
        if (this.codeInput) this.codeInput.value = '';
    }

    /** Muestra estado de error animado en todos los recuadros */
    shakeOtpInputs() {
        document.querySelectorAll('.otp-digit').forEach(d => {
            d.classList.add('error');
            setTimeout(() => d.classList.remove('error'), 400);
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

    // ── SIMULACIONES DE API (Reemplazar en producción) ─
    async sendRecoveryCode(email) {
        const response = await fetch('/acceso-y-seguridad/recover/send-code', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': this.getCsrfToken()
            },
            body: JSON.stringify({ correo: email })
        });

        if (!response.ok) {
            return { success: false, message: 'No se pudo enviar el código. Intenta más tarde.' };
        }

        return response.json();
    }

    async verifyCode(code) {
        const response = await fetch('/acceso-y-seguridad/recover/verify-code', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': this.getCsrfToken()
            },
            body: JSON.stringify({ correo: this.userEmail, codigo: code })
        });

        if (!response.ok) {
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
