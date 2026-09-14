/* ============================================
   SmileTrack — Lógica de Autenticación de Usuarios (login.js)
   ============================================
   Autor: Johan Santamaria / SmileTrack Team
   Fecha: 29/07/2026 (Actualizado 2026-09-12)

   DESCRIPCIÓN Y POR QUÉ DE ARQUITECTURA Y SEGURIDAD:
   Controla la experiencia cliente del inicio de sesión. Garantiza que el usuario seleccione un rol válido
   antes de enviar el formulario, valida el formato del correo electrónico en tiempo real y ofrece feedback
   inmediato ante errores de autenticación devueltos por el servidor C#.

   FUNCIONALIDADES PRINCIPALES:
   - Selección de Rol: Maneja el estado activo de los botones de rol (`data-role`) e inyecta el valor en un input hidden.
   - Visibilidad de Contraseña: Toggle seguro del tipo de input (`password` ↔ `text`) con actualización de icono ARIA.
   - Validación Formato Email: Verifica sintaxis mediante utilidades de validación compartidas (`ValidationUtils`).
   - Gestión de Modal de Error: Captura respuestas de fallo del servidor y despliega el modal interactivo `#authErrorModal`.

   NOTAS DE SEGURIDAD Y MANTENIMIENTO:
   - El backend siempre valida que el rol seleccionado en cliente coincida con los Claims asignados al usuario en SQL Server.
   - Se utiliza `sessionStorage` para almacenar temporalmente tokens de sesión cuando se requiera fallback JWT.
============================================ */

// Esperamos a que el DOM esté listo antes de manipular elementos.
// Sin este listener, los getElementById fallarían porque el HTML aún no ha sido parseado.
document.addEventListener('DOMContentLoaded', function () {

  // ── REFERENCIAS DOM ──
  // Obtenemos referencias una sola vez y las reutilizamos en todo el script.
  // Esto es más eficiente que llamar a getElementById() múltiples veces en cada evento.
  const emailInput = document.getElementById('email');               // Campo de correo electrónico
  const passInput = document.getElementById('password');             // Campo de contraseña
  const loginForm = document.getElementById('loginForm');            // Formulario completo de login
  const loginBtn = document.getElementById('loginBtn');              // Botón de envío del formulario
  const toggleBtn = document.getElementById('togglePassword');       // Botón ojo para ver/ocultar contraseña
  const toggleIcon = document.getElementById('toggleIcon');          // Ícono de Material Symbols dentro del botón
  const roleButtons = document.querySelectorAll('.role-btn');        // Todos los botones de selección de rol
  const selectedRoleInput = document.getElementById('selectedRole'); // Input hidden que almacena el rol elegido
  const authModal = document.getElementById('authErrorModal');       // Modal de error de autenticación (cuenta bloqueada)
  const authErrorClose = document.getElementById('authErrorClose');  // Botón X para cerrar el modal
  const authErrorCloseBtn = document.getElementById('authErrorCloseBtn'); // Botón secundario de cierre

  // ── FALLBACK DE TOAST ──
  // POR QUÉ: Si el script de ToastService no cargó (error de red, cache, etc.),
  // usamos alert() como último recurso para que el usuario siempre vea los mensajes de error.
  // Esto previene que los fallos de autenticación queden silenciosos.
  const Toast = window.ToastService || {
      error: (title, msg) => alert(title + ": " + msg),
      success: (title, msg) => alert(title + ": " + msg)
  };

  // Utilidades de validación compartidas (correo, teléfono, formato general)
  // Se definen en ValidationUtils.js cargado globalmente desde _Layout.cshtml
  const ValUtils = window.ValidationUtils;

  // ═══════════════════════════════════════════════════════════════════
  // SECCIÓN 1: MODAL DE ERROR DE AUTENTICACIÓN (WCAG 2.1 AA)
  // ═══════════════════════════════════════════════════════════════════
  // POR QUÉ: Usamos un modal en lugar de un mensaje inline para los errores graves
  // (cuenta bloqueada, credenciales inválidas múltiples veces). El modal fuerza al usuario
  // a leer el mensaje antes de continuar, evitando que ignore advertencias críticas.
  // Implementa trampa de foco (focus trap) para cumplir WCAG 2.1 Criterio 2.1.2.
  const openAuthModal = () => {
    if (!authModal) return;
    authModal.classList.remove('hidden');           // Hace visible el modal en el DOM
    authModal.setAttribute('aria-hidden', 'false'); // Anuncia el modal a lectores de pantalla
    authModal.removeAttribute('inert');             // Restablece foco interactivo en elementos internos
    // Enfocar el botón de cierre en lugar del primer elemento del formulario,
    // para que el usuario entienda que hay que interactuar con este modal primero
    setTimeout(() => authErrorClose?.focus(), 60);
  };

  const closeAuthModal = () => {
    if (!authModal) return;
    authModal.classList.add('hidden');             // Oculta el modal visualmente
    authModal.setAttribute('aria-hidden', 'true'); // Oculta el modal de lectores de pantalla
    authModal.setAttribute('inert', '');           // `inert` bloquea Tab y clicks en el modal oculto
    // Devolvemos el foco al campo de correo para que el usuario pueda corregir sus datos
    // sin necesidad de hacer clic manualmente en el campo
    emailInput?.focus();
  };

  // POR QUÉ: El backend C# puede renderizar la vista con el modal ya visible
  // cuando detecta un error crítico en el POST. En ese caso, debemos activarlo
  // de inmediato sin esperar ningún evento de clic del usuario.
  if (authModal && !authModal.classList.contains('hidden')) {
    openAuthModal();
  }

  // Cerrar modal al hacer clic en los botones de cierre
  if (authErrorClose) authErrorClose.addEventListener('click', closeAuthModal);
  if (authErrorCloseBtn) authErrorCloseBtn.addEventListener('click', closeAuthModal);

  // Cerrar al hacer clic en el backdrop oscuro (fuera de la caja del modal).
  // `e.target === authModal` distingue el fondo del contenido interior del modal.
  if (authModal) {
    authModal.addEventListener('click', (e) => {
      if (e.target === authModal) closeAuthModal();
    });
  }

  // ═══════════════════════════════════════════════════════════════════
  // SECCIÓN 2: MOSTRAR / OCULTAR CONTRASEÑA (TOGGLE ACCESIBLE)
  // ═══════════════════════════════════════════════════════════════════
  // POR QUÉ: En dispositivos móviles con teclado virtual, los usuarios cometen
  // errores de tipeo frecuentes. Poder ver la contraseña reduce frustraciones
  // y abandonos del formulario de login.
  if (toggleBtn && passInput && toggleIcon) {
    toggleBtn.addEventListener('click', () => {
      // Determinamos el estado actual: si type=password, estamos ocultando (isHidden=true)
      const isHidden = passInput.type === 'password';
      // Alternamos el tipo: password (oculta con ●●●) o text (muestra caracteres reales)
      passInput.type = isHidden ? 'text' : 'password';
      // Actualizamos el ícono de Material Symbols: ojo abierto = visible, ojo tachado = oculto
      toggleIcon.textContent = isHidden ? 'visibility' : 'visibility_off';
      // aria-pressed comunica el estado del toggle a tecnologías asistivas
      toggleBtn.setAttribute('aria-pressed', String(isHidden));
      // aria-label describe la acción que realizará el botón AL SIGUIENTE clic (no la actual)
      toggleBtn.setAttribute('aria-label', isHidden ? 'Ocultar contraseña' : 'Mostrar contraseña');
      // Devolvemos el foco al campo de contraseña para no interrumpir el flujo de teclado
      passInput.focus();
    });
  }

  // ═══════════════════════════════════════════════════════════════════
  // SECCIÓN 3: SELECTOR INTERACTIVO DE ROLES (RADIO GROUP SEMÁNTICO)
  // ═══════════════════════════════════════════════════════════════════
  // POR QUÉ: El sistema tiene múltiples roles (Paciente, Profesional, Administrador) con
  // diferentes pantallas de destino tras el login. El rol seleccionado en cliente se inyecta
  // en el input hidden `#selectedRole` para que el backend C# lo valide contra los Claims
  // del usuario en la base de datos SQL Server.
  // Nota: aunque el cliente envíe un rol equivocado, el backend siempre verifica la consistencia.
  const activateRole = (btn) => {
    // Primero desactivamos TODOS los botones de rol (limpieza de estado previo)
    roleButtons.forEach((b) => {
      b.classList.remove('active');          // Quita estilos visuales de activo
      b.setAttribute('aria-checked', 'false'); // Anuncia a lectores de pantalla que está inactivo
    });
    // Luego activamos únicamente el botón clickeado
    btn.classList.add('active');
    btn.setAttribute('aria-checked', 'true');

    // Sincronizamos el valor del input hidden con el rol elegido.
    // Preferimos `data-role-label` (texto legible) sobre `data-role` (clave técnica) para el POST.
    if (selectedRoleInput) {
      selectedRoleInput.value = btn.dataset.roleLabel || btn.dataset.role || 'Profesional';
    }
  };

  // Soporte dual: clic del ratón Y navegación por teclado (Enter / Espacio).
  // POR QUÉ: Los botones con role="radio" deben activarse también con teclado según WCAG 2.1.
  roleButtons.forEach((btn) => {
    btn.addEventListener('click', () => activateRole(btn));
    btn.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault(); // Previene scroll de página al presionar Espacio
        activateRole(btn);
      }
    });
  });

  // Activación del rol por defecto al cargar la página.
  // POR QUÉ: El formulario no debe quedar sin un rol seleccionado, ya que el backend
  // rechazaría el POST si selectedRole está vacío. Se prioriza 'profesional' como rol más común.
  const defaultBtn = Array.from(roleButtons).find((b) => b.dataset.role === 'profesional') || roleButtons[0];
  if (defaultBtn) activateRole(defaultBtn);

  // ═══════════════════════════════════════════════════════════════════
  // SECCIÓN 4: VALIDACIÓN DE CAMPOS EN TIEMPO REAL (INLINE FEEDBACK)
  // ═══════════════════════════════════════════════════════════════════
  // POR QUÉ: La validación en tiempo real (en cada tecla) permite al usuario corregir
  // errores mientras escribe, en lugar de descubrirlos solo al hacer Submit.
  // Esto reduce la tasa de abandono en formularios de autenticación.

  // Función auxiliar para mostrar u ocultar mensajes de error junto a cada campo.
  // Delega en ValidationUtils si está disponible (centraliza los estilos de error),
  // o usa una clase CSS simple como fallback.
  const setFieldError = (input, message) => {
    if (ValUtils) {
      // ValidationUtils.showError/clearError aplica estilos, iconos y ARIA de forma consistente
      const errEl = document.getElementById(`${input.id}Error`); // Elemento <span> de error asociado al campo
      if (message) {
        ValUtils.showError(input, errEl, message);  // Muestra el error con estilos rojos
      } else {
        ValUtils.clearError(input, errEl);          // Limpia el error (campo válido → estilo verde)
      }
    } else {
      // Fallback: simple toggle de clase CSS de error si ValidationUtils no está disponible
      input.classList.toggle('input-error', Boolean(message));
    }
  };

  // Adjuntamos el listener de validación a ambos campos (email y contraseña).
  // El operador ?. previene errores si alguno de los inputs no existe en el DOM.
  [emailInput, passInput].forEach((input) => {
    input?.addEventListener('input', () => {
      if (!input.value.trim()) {
        // Campo vacío: marcamos como obligatorio
        setFieldError(input, 'Este campo es obligatorio');
      } else if (input.id === 'email' && ValUtils && !ValUtils.isValidEmail(input.value.trim())) {
        // Solo validamos formato de email cuando el campo es el de correo,
        // para no aplicar la regex de email al campo de contraseña
        setFieldError(input, 'Ingresa un correo válido');
      } else {
        // Campo correcto: limpiamos cualquier error previo
        setFieldError(input, '');
      }
    });
  });

  // ═══════════════════════════════════════════════════════════════════
  // SECCIÓN 5: ENVÍO ASÍNCRONO DEL FORMULARIO (FETCH / AJAX)
  // ═══════════════════════════════════════════════════════════════════
  // POR QUÉ: Usamos fetch() en lugar de un submit() nativo para:
  //   1. Mostrar feedback visual (spinner) sin recargar la página.
  //   2. Manejar la respuesta JSON del servidor para redirigir dinámicamente.
  //   3. Diferenciar tipos de error (red vs. credenciales vs. cuenta bloqueada).
  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      // Prevenimos el envío tradicional del formulario (que recargaría la página entera)
      e.preventDefault();

      // Capturamos los valores actuales de los campos (sin espacios en los extremos)
      const email = emailInput?.value.trim();
      const pass = passInput?.value.trim();

      // ── VALIDACIÓN CLIENT-SIDE PREVIA AL SERVIDOR ──
      // POR QUÉ: No queremos gastar un round-trip HTTP si hay errores obvios en el cliente.
      // El backend también valida, pero esta capa previene llamadas innecesarias al servidor.
      if (!email || !pass) {
        if (!email) setFieldError(emailInput, 'Completa tu correo electrónico');
        if (!pass) setFieldError(passInput, 'Completa tu contraseña');
        Toast.error('Datos incompletos', 'Completa tu correo y contraseña para ingresar.');
        return; // Detenemos la ejecución sin llamar al servidor
      }

      // Validamos el formato del email antes de enviarlo (regex de RFC 5322 simplificada)
      if (ValUtils && !ValUtils.isValidEmail(email)) {
        setFieldError(emailInput, 'Ingresa un correo válido');
        Toast.warning('Formato inválido', 'Por favor ingresa un correo electrónico válido.');
        return;
      }

      // ── SPINNER DE CARGA EN BOTÓN SUBMIT ──
      // POR QUÉ: Deshabilitar el botón previene que el usuario haga doble clic y genere
      // dos peticiones simultáneas al servidor (condición de carrera / double-submit).
      // `aria-busy='true'` anuncia el estado de carga a lectores de pantalla.
      if (loginBtn) {
        loginBtn.disabled = true;
        loginBtn.setAttribute('aria-busy', 'true');
        // Reemplazamos el texto del botón con un SVG animado de spinner
        loginBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true" style="width:18px;height:18px;animation:st-spin .7s linear infinite;vertical-align:middle;flex-shrink:0;"><path d="M12 2a10 10 0 0 1 10 10"/></svg> <span>Iniciando sesión...</span>';
        // Inyectamos la animación CSS del spinner solo si no existe ya en el head
        // (evitamos duplicar la regla @keyframes en cada submit)
        if (!document.getElementById('st-spinner-style')) {
          const s = document.createElement('style');
          s.id = 'st-spinner-style';
          s.textContent = '@keyframes st-spin { to { transform: rotate(360deg); } }';
          document.head.appendChild(s);
        }
      }

      try {
        // Convertimos el FormData a URLSearchParams para enviar como application/x-www-form-urlencoded.
        // POR QUÉ: El backend C# con [ValidateAntiForgeryToken] espera este content-type, no multipart/form-data.
        // El token antiforgery ya está incluido en los campos del formulario (campo __RequestVerificationToken).
        const formData = new FormData(loginForm);
        const urlParams = new URLSearchParams(formData);

        const response = await fetch(loginForm.action, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-www-form-urlencoded', // Requerido por ASP.NET ValidateAntiForgeryToken
            'Accept': 'application/json'                          // Pedimos respuesta JSON al controller C#
          },
          body: urlParams
        });

        // `.catch(() => null)` previene un error de parseo si el server devuelve HTML en vez de JSON
        const data = await response.json().catch(() => null);

        if (response.ok && data?.success) {
          // Login exitoso: mostramos toast de bienvenida y redirigimos
          Toast.success('¡Bienvenido!', 'Iniciando sesión de forma segura...');

          // POR QUÉ: El Administrador siempre va al dashboard central, independientemente del
          // `ReturnUrl` del server. Esto evita que un admin quede atrapado en una ruta de paciente.
          const rolActivo = selectedRoleInput?.value?.trim().toLowerCase();
          const destino = rolActivo === 'administrador'
            ? '/gestion-de-citas/st-adm-01-dashboard'
            : (data.redirectUrl || '/');
          window.location.href = destino;

        } else {
          // ── LOGIN FALLIDO: Restaurar estado del botón ──
          // POR QUÉ: Si el login falla, el usuario debe poder intentarlo de nuevo.
          // Restauramos el botón al estado original para permitir otro intento.
          if (loginBtn) {
            loginBtn.disabled = false;
            loginBtn.setAttribute('aria-busy', 'false');
            loginBtn.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">login</span> <span id="btnText">Iniciar Sesión</span>';
          }

          const errorMsg = data?.message || 'Credenciales incorrectas. Verifica tus datos e intenta de nuevo.';

          // Diferenciamos entre cuenta bloqueada (403) y credenciales incorrectas.
          // POR QUÉ: Una cuenta bloqueada merece un modal con instrucciones de desbloqueo,
          // no un simple mensaje de "credenciales incorrectas".
          if (response.status === 403 || errorMsg.toLowerCase().includes('bloqueada')) {
             openAuthModal(); // Muestra el modal de cuenta bloqueada
          } else {
             Toast.error('Acceso denegado', errorMsg);
             // Marcamos ambos campos como incorrectos (sin revelar cuál es el error específico)
             // POR QUÉ: No indicar qué campo falló es una práctica de seguridad estándar
             // para prevenir la enumeración de usuarios existentes.
             setFieldError(emailInput, 'Verifica tu correo');
             setFieldError(passInput, 'Verifica tu contraseña');
          }
        }
      } catch (error) {
        // Error de red (sin conexión, timeout del servidor, etc.)
        // A diferencia de un error 401, este no es culpa del usuario.
        console.error('Login error:', error);
        Toast.error('Error de conexión', 'No pudimos conectar con el servidor. Intenta nuevamente.');
        // Restauramos el botón para que el usuario pueda reintentar
        if (loginBtn) {
          loginBtn.disabled = false;
          loginBtn.setAttribute('aria-busy', 'false');
          loginBtn.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">login</span> <span id="btnText">Iniciar Sesión</span>';
        }
      }
    });
  }

  // ── CIERRE DE MODAL CON TECLA ESCAPE (WCAG 2.1 AA) ──
  // POR QUÉ: El estándar WCAG 2.1 Criterio de Éxito 3.2.5 establece que los
  // diálogos modales deben poder cerrarse con la tecla Escape.
  // También mejora la experiencia en teclados sin ratón (usuarios de accesibilidad).
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      // Solo cerramos si el modal está actualmente visible (evitamos cerrar modales ya cerrados)
      if (authModal && !authModal.classList.contains('hidden')) {
        closeAuthModal();
      }
    }
  });
});