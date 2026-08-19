/**
 * SMILETRACK — LOGIN
 * El usuario puede elegir el rol desde la interfaz y el backend valida que coincida con el rol del usuario.
 */

document.addEventListener('DOMContentLoaded', function () {
  const emailInput = document.getElementById('email');
  const passInput = document.getElementById('password');
  const loginForm = document.getElementById('loginForm');
  const loginBtn = document.getElementById('loginBtn');
  const toggleBtn = document.getElementById('togglePassword');
  const toggleIcon = document.getElementById('toggleIcon');
  const roleButtons = document.querySelectorAll('.role-btn');
  const selectedRoleInput = document.getElementById('selectedRole');
  const authModal = document.getElementById('authErrorModal');
  const authErrorClose = document.getElementById('authErrorClose');
  const authErrorCloseBtn = document.getElementById('authErrorCloseBtn');

  // Si no existen las utilidades globales, las emulamos de forma básica por seguridad
  const Toast = window.ToastService || {
      error: (title, msg) => alert(title + ": " + msg),
      success: (title, msg) => alert(title + ": " + msg)
  };
  const ValUtils = window.ValidationUtils;

  // ── Modales de error por cuenta bloqueada
  const openAuthModal = () => {
    if (!authModal) return;
    authModal.classList.remove('hidden');
    authModal.setAttribute('aria-hidden', 'false');
    authModal.removeAttribute('inert');
    setTimeout(() => authErrorClose?.focus(), 60);
  };

  const closeAuthModal = () => {
    if (!authModal) return;
    authModal.classList.add('hidden');
    authModal.setAttribute('aria-hidden', 'true');
    authModal.setAttribute('inert', '');
    emailInput?.focus();
  };

  if (authModal && !authModal.classList.contains('hidden')) {
    openAuthModal();
  }

  if (authErrorClose) authErrorClose.addEventListener('click', closeAuthModal);
  if (authErrorCloseBtn) authErrorCloseBtn.addEventListener('click', closeAuthModal);

  if (authModal) {
    authModal.addEventListener('click', (e) => {
      if (e.target === authModal) closeAuthModal();
    });
  }

  // ── Mostrar/Ocultar Contraseña
  if (toggleBtn && passInput && toggleIcon) {
    toggleBtn.addEventListener('click', () => {
      const isHidden = passInput.type === 'password';
      passInput.type = isHidden ? 'text' : 'password';
      toggleIcon.textContent = isHidden ? 'visibility' : 'visibility_off';
      toggleBtn.setAttribute('aria-pressed', String(isHidden));
      toggleBtn.setAttribute('aria-label', isHidden ? 'Ocultar contraseña' : 'Mostrar contraseña');
      passInput.focus();
    });
  }

  // ── Selector de Roles
  const activateRole = (btn) => {
    roleButtons.forEach((b) => {
      b.classList.remove('active');
      b.setAttribute('aria-checked', 'false');
    });
    btn.classList.add('active');
    btn.setAttribute('aria-checked', 'true');

    if (selectedRoleInput) {
      selectedRoleInput.value = btn.dataset.roleLabel || btn.dataset.role || 'Profesional';
    }
  };

  roleButtons.forEach((btn) => {
    btn.addEventListener('click', () => activateRole(btn));
    btn.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        activateRole(btn);
      }
    });
  });

  const defaultBtn = Array.from(roleButtons).find((b) => b.dataset.role === 'profesional') || roleButtons[0];
  if (defaultBtn) activateRole(defaultBtn);

  // ── Validación Inline en tiempo real
  const setFieldError = (input, message) => {
    if (ValUtils) {
      const errEl = document.getElementById(`${input.id}Error`);
      if (message) {
        ValUtils.showError(input, errEl, message);
      } else {
        ValUtils.clearError(input, errEl);
      }
    } else {
      input.classList.toggle('input-error', Boolean(message));
    }
  };

  [emailInput, passInput].forEach((input) => {
    input?.addEventListener('input', () => {
      if (!input.value.trim()) {
        setFieldError(input, 'Este campo es obligatorio');
      } else if (input.id === 'email' && ValUtils && !ValUtils.isValidEmail(input.value.trim())) {
        setFieldError(input, 'Ingresa un correo válido');
      } else {
        setFieldError(input, '');
      }
    });
  });

  // ── Intercepción del Formulario (AJAX/Fetch)
  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();

      const email = emailInput?.value.trim();
      const pass = passInput?.value.trim();

      if (!email || !pass) {
        if (!email) setFieldError(emailInput, 'Completa tu correo electrónico');
        if (!pass) setFieldError(passInput, 'Completa tu contraseña');
        Toast.error('Datos incompletos', 'Completa tu correo y contraseña para ingresar.');
        return;
      }

      if (ValUtils && !ValUtils.isValidEmail(email)) {
        setFieldError(emailInput, 'Ingresa un correo válido');
        Toast.warning('Formato inválido', 'Por favor ingresa un correo electrónico válido.');
        return;
      }

      // ── Spinner en botón de submit del login ─────────────────────────────
      // WHY: evita dobles clics que crearían sesiones paralelas o registros duplicados.
      // El spinner se mantiene activo durante la petición fetch; se restaura en caso de error.
      if (loginBtn) {
        loginBtn.disabled = true;
        loginBtn.setAttribute('aria-busy', 'true');
        loginBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true" style="width:18px;height:18px;animation:st-spin .7s linear infinite;vertical-align:middle;flex-shrink:0;"><path d="M12 2a10 10 0 0 1 10 10"/></svg> <span>Iniciando sesión...</span>';
        if (!document.getElementById('st-spinner-style')) {
          const s = document.createElement('style');
          s.id = 'st-spinner-style';
          s.textContent = '@keyframes st-spin { to { transform: rotate(360deg); } }';
          document.head.appendChild(s);
        }
      }

      try {
        const formData = new FormData(loginForm);
        const urlParams = new URLSearchParams(formData);

        const response = await fetch(loginForm.action, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'Accept': 'application/json'
          },
          body: urlParams
        });

        const data = await response.json().catch(() => null);

        if (response.ok && data?.success) {
          Toast.success('¡Bienvenido!', 'Iniciando sesión de forma segura...');
          // Redirigir al destino
          window.location.href = data.redirectUrl || '/';
        } else {
          // Restaurar botón
          if (loginBtn) {
            loginBtn.disabled = false;
            loginBtn.setAttribute('aria-busy', 'false');
            loginBtn.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">login</span> <span id="btnText">Iniciar Sesión</span>';
          }
          
          const errorMsg = data?.message || 'Credenciales incorrectas. Verifica tus datos e intenta de nuevo.';
          if (response.status === 403 || errorMsg.toLowerCase().includes('bloqueada')) {
             openAuthModal();
          } else {
             Toast.error('Acceso denegado', errorMsg);
             setFieldError(emailInput, 'Verifica tu correo');
             setFieldError(passInput, 'Verifica tu contraseña');
          }
        }
      } catch (error) {
        console.error('Login error:', error);
        Toast.error('Error de conexión', 'No pudimos conectar con el servidor. Intenta nuevamente.');
        if (loginBtn) {
          loginBtn.disabled = false;
          loginBtn.setAttribute('aria-busy', 'false');
          loginBtn.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">login</span> <span id="btnText">Iniciar Sesión</span>';
        }
      }
    });
  }

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (authModal && !authModal.classList.contains('hidden')) {
        closeAuthModal();
      }
    }
  });
});