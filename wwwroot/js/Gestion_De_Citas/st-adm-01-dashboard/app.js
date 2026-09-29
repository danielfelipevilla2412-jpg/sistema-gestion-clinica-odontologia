/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Dashboard de Citas Administrativo (st-adm-01-dashboard)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-adm-01-dashboard/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Maneja las interacciones del lado del cliente en el dashboard administrativo de citas.
 * Implementa animaciones fluidas para indicadores KPI, barra visual de porcentaje de ocupación,
 * exportación de reportes a PDF y comportamiento responsive de la interfaz.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Respeto estricto a las preferencias del usuario (`prefers-reduced-motion`).
 * - Uso de `performance.now()` en animaciones numéricas para prevenir drift y garantizar 60 FPS.
 * - Despliegue de notificaciones amigables vía ToastService global.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Stadm01Dashboard
 * - HTML: Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml
 * ============================================ */

(() => {
    'use strict';

    // ═══════════════════════════════════════════════════════════════════
    // 1. CONSTANTES Y CONFIGURACIÓN
    // ═══════════════════════════════════════════════════════════════════

    const ANIMATION_DURATION_MS = 700;
    const KPI_POLL_INTERVAL_MS  = 60_000;

    const SELECTORS = {
        hamburger:     '#hamburger',
        sidebar:       '#sidebar',
        overlay:       '#overlay',
        exportBtn:     '#btnExport',
        exportBtnText: '#btnExport .btn-export-text',
        occupancyBar:  '#dashboardOccupancyBar',
        counters:      '.stat-number[data-target]'
    };

    const API_ENDPOINTS = {
        exportPdf: '/gestion-de-citas/st-adm-01-dashboard/exportar-pdf',
        kpis:      '/api/citas/kpis'
    };

    // ═══════════════════════════════════════════════════════════════════
    // 2. ESTADO DE LA APLICACIÓN
    // ═══════════════════════════════════════════════════════════════════

    // Módulo stateless: todo el estado reside en el DOM (data-target, class lists).

    // ═══════════════════════════════════════════════════════════════════
    // 3. UTILIDADES
    // ═══════════════════════════════════════════════════════════════════

    /** Shorthand para document.querySelector. */
    const qs = sel => document.querySelector(sel);

    /** Detecta si el usuario prefiere movimiento reducido. */
    const prefersReducedMotion = () =>
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    /** Formatea un número como moneda COP. */
    const formatCOP = amount =>
        new Intl.NumberFormat('es-CO', {
            style:                 'currency',
            currency:              'COP',
            maximumFractionDigits: 0
        }).format(amount);

    // ═══════════════════════════════════════════════════════════════════
    // 4. SERVICIOS Y API
    // ═══════════════════════════════════════════════════════════════════

    /**
     * Descarga el reporte PDF del dashboard desde el servidor.
     * @returns {Promise<Response>}
     */
    const fetchDashboardPdf = () =>
        fetch(API_ENDPOINTS.exportPdf, {
            method:      'GET',
            credentials: 'same-origin',
            headers:     { Accept: 'application/pdf' }
        });

    /**
     * Obtiene los KPIs actuales desde la API REST.
     * @returns {Promise<Response>}
     */
    const fetchKpis = () =>
        fetch(API_ENDPOINTS.kpis, {
            credentials: 'same-origin',
            headers:     { 'X-Requested-With': 'XMLHttpRequest' }
        });

    // ═══════════════════════════════════════════════════════════════════
    // 5. RENDERIZADO Y DOM
    // ═══════════════════════════════════════════════════════════════════

    /**
     * Anima numéricamente todos los contadores con `data-target` en la página.
     * Respeta `prefers-reduced-motion`.
     */
    const animateCounters = () => {
        document.querySelectorAll(SELECTORS.counters).forEach(el => {
            const raw        = el.dataset.target ?? '0';
            const isCurrency = el.classList.contains('currency');
            const target     = isCurrency ? parseFloat(raw) : parseInt(raw, 10);

            if (!Number.isFinite(target) || target <= 0) return;

            if (prefersReducedMotion()) {
                el.textContent = isCurrency ? formatCOP(target) : String(target);
                return;
            }

            if (window.CommonUtils?.animateCounter) {
                window.CommonUtils.animateCounter(el, target, {
                    duration: ANIMATION_DURATION_MS,
                    prefix: isCurrency ? '$' : ''
                });
            } else {
                el.textContent = isCurrency ? formatCOP(target) : target.toLocaleString('es-CO');
            }
        });
    };

    /**
     * Inicializa la barra de progreso de ocupación animándola hacia el ancho objetivo.
     * Respeta `prefers-reduced-motion`.
     */
    const initOccupancyBar = () => {
        const bar = qs(SELECTORS.occupancyBar);
        if (!bar) return;

        const raw   = Number(bar.dataset.width);
        const width = Number.isFinite(raw) ? Math.max(0, Math.min(100, raw)) : 0;

        if (prefersReducedMotion()) {
            bar.style.width = `${width}%`;
            return;
        }

        // Un frame de retraso para que la transición CSS se active.
        requestAnimationFrame(() => { bar.style.width = `${width}%`; });
    };

    /**
     * Actualiza los contadores KPI en el DOM con los valores recibidos del servidor.
     * @param {Object} data — Payload del DTO CitasKpiDto.
     */
    const renderKpiCounters = data => {
        const kpiMap = {
            'total':       data.Total       ?? data.total,
            'programadas': data.Programadas ?? data.programadas,
            'canceladas':  data.Canceladas  ?? data.canceladas,
            'atendidas':   data.Atendidas   ?? data.atendidas
        };

        document.querySelectorAll('.stat-number[data-target]').forEach(el => {
            const key = el.closest('[data-kpi]')?.dataset?.kpi;
            if (!key || kpiMap[key] === undefined) return;

            const newVal = Number(kpiMap[key]);
            if (!isNaN(newVal) && Number(el.dataset.target) !== newVal) {
                el.dataset.target = newVal;
                el.textContent    = newVal.toLocaleString('es-CO');
            }
        });
    };

    // ═══════════════════════════════════════════════════════════════════
    // 6. MANEJO DE MODALES Y SIDEBAR
    // ═══════════════════════════════════════════════════════════════════

    /**
     * Inicializa el sidebar responsive con soporte para hamburger, overlay
     * y navegación por teclado (Escape, foco accesible).
     */
    const initSidebar = () => {
        const hamburger = qs(SELECTORS.hamburger);
        const sidebar   = qs(SELECTORS.sidebar);
        const overlay   = qs(SELECTORS.overlay);
        if (!hamburger || !sidebar || !overlay) return;

        const setOpen = open => {
            sidebar.classList.toggle('open', open);
            overlay.classList.toggle('open', open);
            hamburger.setAttribute('aria-expanded', String(open));
            overlay.setAttribute('aria-hidden',     String(!open));
            if (open) {
                // Mover foco al primer ítem del menú (WCAG 2.4.3).
                sidebar.querySelector('.nav-item')?.focus();
            } else {
                hamburger.focus();
            }
        };

        hamburger.addEventListener('click', () => setOpen(true));
        overlay.addEventListener('click',   () => setOpen(false));
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && sidebar.classList.contains('open')) {
                e.preventDefault();
                setOpen(false);
            }
        });
    };

    /**
     * Inicializa el botón de exportación de PDF.
     * Gestiona el estado de carga y descarga el blob resultante.
     */
    const initExportButton = () => {
        const btn     = qs(SELECTORS.exportBtn);
        const btnText = qs(SELECTORS.exportBtnText);
        if (!btn) return;

        btn.addEventListener('click', async () => {
            if (btn.disabled) return;

            btn.disabled = true;
            if (btnText) btnText.textContent = 'Generando…';

            try {
                const res = await fetchDashboardPdf();
                if (!res.ok) throw new Error(`HTTP ${res.status}`);

                const blob = await res.blob();
                const url  = URL.createObjectURL(blob);
                const link = Object.assign(document.createElement('a'), {
                    href:     url,
                    download: `reporte-dashboard-${new Date().toISOString().slice(0, 10)}.pdf`
                });

                document.body.appendChild(link);
                link.click();
                link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 1000);

                window.ToastService?.success(
                    'Reporte generado',
                    'El PDF se descargó correctamente.'
                );
            } catch (err) {
                console.error('[SmileTrack][Dashboard] Error exportando PDF:', err);
                window.ToastService?.error(
                    'Error al exportar',
                    'No fue posible generar el reporte. Intente de nuevo.'
                );
            } finally {
                btn.disabled = false;
                if (btnText) btnText.textContent = 'Exportar PDF';
            }
        });
    };

    /**
     * Inicia el polling periódico de KPIs (cada 60 s).
     * Actualiza el DOM sin recargar la página.
     * El intervalo se limpia al salir de la página para evitar memory leaks.
     */
    const initKpiPolling = () => {
        const refreshKpis = async () => {
            try {
                const res = await fetchKpis();
                if (!res.ok) return;
                const payload = await res.json();
                if (!payload?.success || !payload?.data) return;
                renderKpiCounters(payload.data);
            } catch (err) {
                // Silencioso: el polling no debe interrumpir la UI.
                console.debug('[SmileTrack][Dashboard] Error en polling KPIs:', err);
            }
        };

        const timerId = setInterval(refreshKpis, KPI_POLL_INTERVAL_MS);
        window.addEventListener('beforeunload', () => clearInterval(timerId));
    };

    // ═══════════════════════════════════════════════════════════════════
    // 7. INICIALIZACIÓN
    // ═══════════════════════════════════════════════════════════════════

    document.addEventListener('DOMContentLoaded', () => {
        initSidebar();
        initExportButton();
        animateCounters();
        initOccupancyBar();
        initKpiPolling();
    });

})();
