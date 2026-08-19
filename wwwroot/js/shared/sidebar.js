/* ============================================
   SmileTrack — Sidebar Compartido (shared/sidebar.js)
   ============================================
   Autor: Johan Santamaria
   Fecha: 05/08/2026

   PROPÓSITO:
   Módulo JS centralizado para el sidebar de navegación de SmileTrack.
   Reemplaza ~40 bloques <script> inline idénticos que existían en cada
   vista del sistema (uno por módulo/rol), concentrando toda la lógica
   del sidebar en un solo archivo cacheado por el navegador.

   RESPONSABILIDADES:
   1. Accordion     — colapsar/expandir grupos de navegación con animación
                      maxHeight. Usa scrollHeight real para que la transición
                      CSS funcione sin valores hardcodeados.
   2. Auto-expand   — al cargar la página, el grupo que contiene el enlace
                      activo se expande automáticamente para orientar al usuario.
   3. Hamburger     — abrir/cerrar el sidebar en móvil con overlay semitransparente,
                      gestión de overflow del body y devolución de foco accesible.
   4. Teclado       — soporte de Enter/Space en headers de grupo (WCAG 2.1 SC 2.1.1).
   5. Cierre auto   — cerrar el sidebar al navegar en móvil y al presionar Escape.

   DECISIONES TÉCNICAS:
   - IIFE: encapsula las funciones en un scope privado para no contaminar el
     objeto global window con funciones de utilidad interna.
   - document.readyState: el script puede colocarse al final del <body> (lo más
     habitual) o en el <head> con defer — en ambos casos la inicialización se
     ejecuta cuando el DOM esté disponible.
   - scrollHeight: se recalcula en cada expansión para que los grupos con
     contenido dinámico (ej: listas generadas por Razor) tengan la altura correcta.

   USO:
   Incluir DESPUÉS del partial del sidebar (al final del <body>):
     <script src="@Url.Content("~/js/shared/sidebar.js")"></script>

   DEPENDENCIAS:
   - HTML: requiere #hamburger, #sidebar, #overlay en el DOM.
   - CSS: sidebar.css (~/css/shared/sidebar.css) debe estar cargado.

   NOTAS DE MANTENIMIENTO:
   - No depende de jQuery ni de ninguna librería externa.
   - Compatible con todos los roles del sistema (Admin, Profesional,
     Recepcionista, Auxiliar, Paciente).
   - Este archivo tiene cobertura en las 49 vistas con sidebar del sistema.
   ============================================ */

(function () {
  'use strict';

  // ════════════════════════════════════════════════════════════════════
  //  ACCORDION — GRUPOS DE NAVEGACIÓN COLAPSABLES
  // ════════════════════════════════════════════════════════════════════

  /**
   * Inicializa el accordion de grupos de navegación lateral.
   *
   * Cada .nav-group-header actúa como botón para colapsar/expandir
   * el .nav-group-items inmediatamente siguiente.
   *
   * El estado inicial de cada grupo se determina automáticamente:
   * si el grupo contiene el enlace activo (clase .active o aria-current="page"),
   * se expande; de lo contrario, se colapsa.
   *
   * WHY scroll-height dinámico: usar un valor fijo de maxHeight causaría que
   * grupos con muchos ítems se corten visualmente. scrollHeight devuelve la
   * altura real del contenido, garantizando que la animación siempre muestre
   * el grupo completo sin overflow oculto.
   */
  function initAccordion() {
    /** @type {NodeListOf<HTMLElement>} */
    var headers = document.querySelectorAll('.nav-group-header');

    headers.forEach(function (header) {
      /** @type {HTMLElement|null} */
      var items = header.nextElementSibling;
      /** @type {HTMLElement|null} */
      var arrow = header.querySelector('.nav-arrow');

      // Saltar grupos sin estructura válida (robustez ante HTML incompleto)
      if (!items || !arrow) return;

      /**
       * Expande o colapsa el grupo modificando maxHeight para la transición CSS.
       *
       * @param {boolean} expand - true para expandir, false para colapsar
       */
      function setExpanded(expand) {
        if (expand) {
          // WHY scrollHeight: permite que la transición CSS muestre el
          // contenido completo sin hardcodear una altura máxima
          items.style.maxHeight = items.scrollHeight + 'px';
          arrow.style.transform = 'rotate(0deg)';
        } else {
          items.style.maxHeight = '0px';
          arrow.style.transform = 'rotate(-90deg)';
        }
        // Actualiza aria-expanded para lectores de pantalla (WCAG 4.1.2)
        header.setAttribute('aria-expanded', String(expand));
      }

      // ── Auto-expand ──────────────────────────────────────────────
      // WHY: el usuario debe ver inmediatamente dónde se encuentra en la
      // navegación. Colapsar el grupo activo obligaría a un clic extra.
      // La detección combina clase CSS (.active) y atributo ARIA para
      // compatibilidad con ambos sistemas de marcado del proyecto.
      var hasActive = !!items.querySelector('.nav-item.active, [aria-current="page"]');
      setExpanded(hasActive);

      /**
       * Alterna el estado expandido/colapsado del grupo.
       * Se vincula a clic y a las teclas Enter/Space.
       */
      function toggle() {
        var isCurrentlyExpanded = header.getAttribute('aria-expanded') === 'true';
        setExpanded(!isCurrentlyExpanded);
      }

      // Clic con ratón o touch
      header.addEventListener('click', toggle);

      // Teclado: WCAG 2.1 SC 2.1.1 — los controles de UI deben ser
      // operables con teclado. Enter activa el elemento (como clic),
      // Space activa botones de toggle (estándar ARIA button pattern).
      header.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault(); // Prevenir scroll de página con Space
          toggle();
        }
      });
    });
  }

  // ════════════════════════════════════════════════════════════════════
  //  HAMBURGER — MENÚ MÓVIL CON OVERLAY
  // ════════════════════════════════════════════════════════════════════

  /**
   * Inicializa el botón hamburger para la navegación en dispositivos móviles.
   *
   * El hamburger muestra/oculta el sidebar lateral mediante clases CSS.
   * Se acompaña de un overlay semitransparente que permite cerrar el menú
   * haciendo clic fuera de él (patrón drawer estándar en aplicaciones móviles).
   *
   * WHY overflow hidden en body: sin esto el contenido de la página sigue siendo
   * desplazable mientras el menú está abierto, produciendo una UX confusa donde
   * el usuario no sabe si está interactuando con el menú o con la página.
   *
   * WHY foco al hamburger al cerrar: WCAG 2.4.3 — al cerrar un componente
   * emergente, el foco debe regresar al elemento que lo activó para que el
   * usuario de teclado/lector de pantalla no quede desorientado.
   */
  function initHamburger() {
    /** @type {HTMLElement|null} */
    var hamburger = document.getElementById('hamburger');
    /** @type {HTMLElement|null} */
    var sidebar   = document.getElementById('sidebar');
    /** @type {HTMLElement|null} */
    var overlay   = document.getElementById('overlay');

    // Salir silenciosamente si los elementos no existen en esta vista
    if (!hamburger || !sidebar) return;

    /**
     * Abre o cierra el menú lateral en móvil.
     *
     * @param {boolean} open - true para abrir, false para cerrar
     */
    function setOpen(open) {
      sidebar.classList.toggle('open', open);
      hamburger.setAttribute('aria-expanded', String(open));

      if (overlay) {
        overlay.classList.toggle('open', open);
        // aria-hidden controla si el overlay es visible para lectores de pantalla
        overlay.setAttribute('aria-hidden', String(!open));
      }

      // Bloquear scroll del body mientras el drawer está abierto
      document.body.style.overflow = open ? 'hidden' : '';
    }

    // Abrir/cerrar al pulsar el botón hamburger
    hamburger.addEventListener('click', function () {
      var isCurrentlyOpen = sidebar.classList.contains('open');
      setOpen(!isCurrentlyOpen);
    });

    // Cerrar al hacer clic en el overlay (área fuera del sidebar)
    if (overlay) {
      overlay.addEventListener('click', function () {
        setOpen(false);
      });
    }

    // Cerrar con tecla Escape — patrón de accesibilidad estándar para drawers
    // (WCAG 2.1 SC 2.1.1 y ARIA Authoring Practices Guide — Dialog pattern)
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && sidebar.classList.contains('open')) {
        setOpen(false);
        // WHY foco al hamburger: el usuario de teclado debe recuperar la
        // posición de foco anterior para seguir navegando normalmente
        hamburger.focus();
      }
    });

    // Cerrar automáticamente al navegar a un enlace en pantallas pequeñas.
    // WHY: en móvil, al hacer clic en un enlace el sidebar permanecería visible
    // tapando el contenido de la nueva página durante la transición.
    var navLinks = sidebar.querySelectorAll('.nav-item:not(.nav-item--logout)');
    navLinks.forEach(function (link) {
      link.addEventListener('click', function () {
        if (window.innerWidth <= 680) {
          setOpen(false);
        }
      });
    });
  }

  // ════════════════════════════════════════════════════════════════════
  //  INICIALIZACIÓN
  // ════════════════════════════════════════════════════════════════════

  /**
   * Punto de entrada único del módulo.
   * Se ejecuta cuando el DOM esté completamente disponible,
   * independientemente de dónde se coloque la etiqueta <script>.
   *
   * Orden de inicialización:
   *   1. initAccordion() — binds de grupos de nav y estado inicial
   *   2. initHamburger()  — binds del menú móvil
   *   3. initMiniSidebar() — botón de colapso desktop con localStorage
   *
   * WHY IIFE + readyState: no se usa DOMContentLoaded directamente
   * para cubrir el caso en que el script se cargue después de que el
   * DOM ya esté listo (ej: scripts dinámicos, deferred scripts).
   */
  function init() {
    initAccordion();
    initHamburger();
    initMiniSidebar();
  }

  // ════════════════════════════════════════════════════════════════════
  //  MINI-SIDEBAR — COLAPSO DE DESKTOP
  // ════════════════════════════════════════════════════════════════════

  /**
   * Inicializa el botón de colapso del sidebar en escritorio.
   *
   * Comportamiento:
   * - Agrega un botón circular (‹) al sidebar que colapsa/expande.
   * - El estado se persiste en localStorage bajo la clave 'sidebar-mini'.
   * - Cuando está colapsado (.mini), muestra solo íconos emoji con tooltips.
   * - El main content ajusta su margin-left para aprovechar el espacio ganado.
   *
   * WHY localStorage: el estado debe sobrevivir a recargas de página para
   * no obligar al usuario a reconfigurar en cada navegación.
   *
   * WHY solo desktop (> 680px): en móvil el sidebar es un drawer; mezclarlo
   * con el modo mini crearía conflictos de estado (open vs mini).
   */
  function initMiniSidebar() {
    var sidebar = document.getElementById('sidebar');
    if (!sidebar) return;

    // Solo habilitar en desktop
    if (window.innerWidth <= 680) return;

    // ── Crear botón de colapso ────────────────────────────────────────
    var btn = document.createElement('button');
    btn.className = 'sidebar-collapse-btn';
    btn.setAttribute('aria-label', 'Contraer menú lateral');
    btn.setAttribute('title', 'Contraer menú lateral');
    btn.innerHTML = '&#8249;'; // ‹
    sidebar.appendChild(btn);

    // ── Agregar titles a los nav-items para tooltips en modo mini ─────
    var navItems = sidebar.querySelectorAll('.nav-item');
    navItems.forEach(function (item) {
      // Extraer el texto del ítem (sin el ícono del ::before que viene de data-icon)
      var text = (item.textContent || '').trim();
      if (text && !item.getAttribute('title')) {
        item.setAttribute('title', text);
        // Envolver el texto en un span para poder ocultarlo en modo mini
        var childNodes = Array.from(item.childNodes);
        childNodes.forEach(function (node) {
          if (node.nodeType === Node.TEXT_NODE && node.textContent.trim()) {
            var span = document.createElement('span');
            span.className = 'nav-text';
            span.textContent = node.textContent;
            item.replaceChild(span, node);
          }
        });
      }
    });

    // Envolver texto de nav-group-header en .nav-group-label
    var groupHeaders = sidebar.querySelectorAll('.nav-group-header');
    groupHeaders.forEach(function (header) {
      var childNodes = Array.from(header.childNodes);
      childNodes.forEach(function (node) {
        if (node.nodeType === Node.TEXT_NODE && node.textContent.trim()) {
          var span = document.createElement('span');
          span.className = 'nav-group-label';
          span.textContent = node.textContent;
          header.replaceChild(span, node);
        }
      });
    });

    // ── Aplicar estado guardado en localStorage ───────────────────────
    var isMini = localStorage.getItem('sidebar-mini') === 'true';
    applyMiniState(sidebar, btn, isMini);

    // ── Toggle al hacer clic en el botón ─────────────────────────────
    btn.addEventListener('click', function () {
      var nowMini = !sidebar.classList.contains('mini');
      applyMiniState(sidebar, btn, nowMini);
      localStorage.setItem('sidebar-mini', String(nowMini));
    });

    // Reaccionar a cambios de tamaño de ventana: desactivar mini en móvil
    window.addEventListener('resize', function () {
      if (window.innerWidth <= 680) {
        sidebar.classList.remove('mini');
        adjustMainOffset(false);
      }
    });
  }

  /**
   * Aplica o quita la clase .mini al sidebar y ajusta el offset del main.
   *
   * @param {HTMLElement} sidebar
   * @param {HTMLButtonElement} btn
   * @param {boolean} mini
   */
  function applyMiniState(sidebar, btn, mini) {
    sidebar.classList.toggle('mini', mini);
    btn.setAttribute('aria-label', mini ? 'Expandir menú lateral' : 'Contraer menú lateral');
    btn.setAttribute('title', mini ? 'Expandir menú lateral' : 'Contraer menú lateral');
    adjustMainOffset(mini);
  }

  /**
   * Ajusta el margin-left del contenido principal según el estado del sidebar.
   * WHY: sin este ajuste, el main content quedaría debajo del sidebar en modo mini.
   *
   * @param {boolean} mini
   */
  function adjustMainOffset(mini) {
    var main = document.querySelector('.main');
    if (!main) return;
    if (mini) {
      main.classList.add('sidebar-mini-offset');
    } else {
      main.classList.remove('sidebar-mini-offset');
    }
  }

  if (document.readyState === 'loading') {
    // DOM aún cargando — esperar al evento
    document.addEventListener('DOMContentLoaded', init);
  } else {
    // DOM ya disponible (script al final del <body> o cargado tarde)
    init();
  }

})();
