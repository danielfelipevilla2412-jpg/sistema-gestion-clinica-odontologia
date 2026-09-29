/* ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: Reportes Clínicos (st-adm-14-reportes-clinicos)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/reportes.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Visualización e interacción con los informes de producción clínica por odontólogo y especialidad.
 * Permite filtrar por profesional, periodo de tiempo y exportar resúmenes operacionales.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtrado reactivo en cliente integrado con la paginación servida por el controlador MVC.
 * - Los contadores de métricas se animan desde `data-target` al cargar la página.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionProfesionalesController -> Stadm14ReportesClinicos
 * - HTML: Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml
 * - window.animateCounter — definido en shared/utils.js
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const DEBOUNCE_DELAY_MS = 300;

const METRIC_ELEMENT_IDS = ['metricTotal', 'metricActivos', 'metricAttendanceRate'];

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

// Módulo stateless: el estado visible reside en el DOM y en el filtro del servidor MVC.

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES
// ═══════════════════════════════════════════════════════════════════

/**
 * Obtiene un elemento del DOM por ID con advertencia si no existe.
 * @param {string} id
 * @returns {HTMLElement|null}
 */
const safeGetElement = (id) =>
    window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(id) : document.getElementById(id);

const debounce = (fn, delay) =>
    window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

// Sin llamadas AJAX propias: la paginación y el filtrado son server-side (MVC form submit).

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

/**
 * Anima los contadores de métricas del reporte usando `window.animateCounter`
 * (definido en shared/utils.js). Leemos `data-target` de cada elemento.
 */
const initMetricCounters = () => {
    METRIC_ELEMENT_IDS.forEach(id => {
        const el = safeGetElement(id);
        if (!el) return;

        const target = parseInt(el.getAttribute('data-target') ?? '0', 10);
        if (!isNaN(target) && target > 0) {
            // `window.animateCounter` conserva su nombre original por backward-compat.
            animateCounter(el, target);
        } else {
            el.textContent = '0';
        }
    });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y SIDEBAR
// ═══════════════════════════════════════════════════════════════════

/**
 * Inicializa el sidebar responsive (hamburger + overlay + cierre por Escape).
 */
const initSidebar = () => {
    const hamburger = safeGetElement('hamburger');
    const sidebar   = safeGetElement('sidebar');
    const overlay   = safeGetElement('overlay');
    if (!hamburger || !sidebar || !overlay) return;

    const toggleMenu = show => {
        sidebar.classList.toggle('open', show);
        overlay.classList.toggle('open', show);
        hamburger.setAttribute('aria-expanded', String(show));
        overlay.setAttribute('aria-hidden',     String(!show));
    };

    hamburger.addEventListener('click', () => toggleMenu(true));
    overlay.addEventListener('click',   () => toggleMenu(false));
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape' && sidebar.classList.contains('open')) {
            e.preventDefault();
            toggleMenu(false);
        }
    });
};

/**
 * Inicializa el modal de alertas de reporte.
 * Abre el modal con el texto del `data-alert` del botón disparador.
 * Cierra por botón, clic en backdrop o tecla Escape.
 */
const initAlertModal = () => {
    const modal     = safeGetElement('reportAlertModal');
    const modalBody = safeGetElement('reportAlertBody');
    const closeBtn  = document.querySelector('.report-alert-close');
    if (!modal || !modalBody) return;

    const openModal = text => {
        modalBody.textContent = text || 'Sin observación registrada.';
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        // Mover foco al botón de cierre (accesibilidad).
        document.querySelector('.report-alert-close')?.focus();
    };

    const closeModal = () => {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    };

    // Botones disparadores
    document.querySelectorAll('.alert-trigger').forEach(button => {
        button.addEventListener('click', () =>
            openModal(button.dataset.alert || 'Sin observación registrada.')
        );
    });

    // Botón de cierre explícito
    closeBtn?.addEventListener('click', closeModal);

    // Cierre por clic en backdrop o en elementos marcados con data-close-modal
    modal.addEventListener('click', event => {
        const isCloseTarget = event.target instanceof HTMLElement &&
                              event.target.dataset.closeModal === 'true';
        if (isCloseTarget || event.target === modal) closeModal();
    });

    // Cierre por Escape
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && modal.classList.contains('is-open')) closeModal();
    });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const initReportesModule = () => {
    initSidebar();
    initMetricCounters();
    initAlertModal();
};

document.addEventListener('DOMContentLoaded', initReportesModule);
