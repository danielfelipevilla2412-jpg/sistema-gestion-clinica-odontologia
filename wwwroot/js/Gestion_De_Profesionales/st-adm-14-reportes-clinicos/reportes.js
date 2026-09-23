/* ============================================
SmileTrack — Reportes Clínicos (st-adm-14-reportes-clinicos)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona la interactividad del módulo de reportes clínicos: sidebar móvil, filtros de búsqueda y el comportamiento de los controles de la tabla paginada de reportes.

FUNCIONALIDADES PRINCIPALES:
- Sidebar móvil con gestión de foco y atributos ARIA heredado del patrón base de st-adm-07
- Filtro de búsqueda y selección de profesional con actualización reactiva de la tabla vía formulario
- Cierre de notificaciones de éxito o error generadas tras acciones del Controller

DEPENDENCIAS TÉCNICAS:
- Controller: GestionProfesionalesController (ViewData: ProfesionalesReportes, ReportesClinicos, ReportesClinicosPage)
- CSS: ~/css/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/styles.css
- JS: ~/js/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/reportes.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- Este archivo es más liviano que app.js de st-adm-07 porque la paginación y el filtrado son server-side (formulario GET).
============================================ */

// ═══════════════════════════════════════════════════════════════════
// UTILIDADES GLOBALES (iguales a st-adm-07)
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
    const el = document.getElementById(id);
    if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
    return el;
};

const debounce = (fn, delay) => {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => fn.apply(this, args), delay);
    };
};


// ═══════════════════════════════════════════════════════════════════
// ANIMACIÓN DE CONTADORES  →  window.animateCounter (shared/utils.js)
// ═══════════════════════════════════════════════════════════════════

// animateCounter conserva nombre de llamada por backward-compat;
// el binding dinámico resuelve window.animateCounter automáticamente.

// ═══════════════════════════════════════════════════════════════════
// INICIALIZACIÓN DE COMPONENTES (iguales a st-adm-07)
// ═══════════════════════════════════════════════════════════════════

const initSidebar = () => {
    const hamburger = safeGetElement('hamburger');
    const sidebar = safeGetElement('sidebar');
    const overlay = safeGetElement('overlay');
    if (!hamburger || !sidebar || !overlay) return;

    const toggleMenu = (show) => {
        sidebar.classList.toggle('open', show);
        overlay.classList.toggle('open', show);
        hamburger.setAttribute('aria-expanded', String(show));
        overlay.setAttribute('aria-hidden', String(!show));
    };

    hamburger.addEventListener('click', () => toggleMenu(true));
    overlay.addEventListener('click', () => toggleMenu(false));
};

const initServerStats = () => {
    const statEls = [
        safeGetElement('metricTotal'),
        safeGetElement('metricActivos'),
            safeGetElement('metricAttendanceRate'),
    ];

    statEls.forEach(el => {
        if (!el) return;
        const target = parseInt(el.getAttribute('data-target') ?? '0', 10);
        if (!isNaN(target) && target > 0) {
            animateCounter(el, target);
        } else {
            el.textContent = '0';
        }
    });
};

// ═══════════════════════════════════════════════════════════════════
// FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const initAlertButtons = () => {
    const modal = safeGetElement('reportAlertModal');
    const modalBody = safeGetElement('reportAlertBody');
    const closeBtn = document.querySelector('.report-alert-close');
    if (!modal || !modalBody) return;

    const openModal = (text) => {
        modalBody.textContent = text || 'Sin observación registrada.';
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        const closeFocusTarget = document.querySelector('.report-alert-close');
        if (closeFocusTarget) closeFocusTarget.focus();
    };

    const closeModal = () => {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    };

    document.querySelectorAll('.alert-trigger').forEach((button) => {
        button.addEventListener('click', () => {
            openModal(button.dataset.alert || 'Sin observación registrada.');
        });
    });

    if (closeBtn) {
        closeBtn.addEventListener('click', closeModal);
    }

    modal.addEventListener('click', (event) => {
        const closeTarget = event.target instanceof HTMLElement && event.target.dataset.closeModal === 'true';
        if (closeTarget || event.target === modal) {
            closeModal();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && modal.classList.contains('is-open')) {
            closeModal();
        }
    });
};

const init = async () => {
    initSidebar();
    initServerStats(); // Anima contadores desde data-target
    initAlertButtons();
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);
