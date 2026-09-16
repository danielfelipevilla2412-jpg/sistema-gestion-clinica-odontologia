/**
 * ============================================
 * SmileTrack — Dashboard de Citas (app.js)
 * ============================================
 * Autor: Johan Santamaria
 *
 * PROPÓSITO:
 * Maneja animaciones de contadores, barra de ocupación,
 * exportación PDF y navegación responsive del sidebar.
 *
 * DECISIONES DE DISEÑO:
 * - IIFE para no contaminar el scope global.
 * - prefers-reduced-motion respetado en todas las animaciones.
 * - performance.now() en lugar de setInterval para animaciones
 *   de contadores: evita drift y se cancela solo cuando llega al 100%.
 * - ToastService ya inyectado por _Toasts.cshtml — no se reinventa aquí.
 * - No se importan API_BASE, debounce ni trackedRAF: no se necesitan
 *   en una página de solo-lectura sin llamadas API del cliente.
 ============================================ */
(() => {
    'use strict';

    // ── Selectores ──────────────────────────────────────────────
    const SEL = {
        hamburger:       '#hamburger',
        sidebar:         '#sidebar',
        overlay:         '#overlay',
        exportBtn:       '#btnExport',
        exportBtnText:   '#btnExport .btn-export-text',
        occupancyBar:    '#dashboardOccupancyBar',
        counters:        '.stat-number[data-target]'
    };

    const q = sel => document.querySelector(sel);

    // ── Utilidad: respeta prefers-reduced-motion ─────────────────
    const prefersReducedMotion = () =>
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ── Formato de moneda (es-CO) ────────────────────────────────
    const fmtCOP = amount =>
        new Intl.NumberFormat('es-CO', {
            style:                 'currency',
            currency:              'COP',
            maximumFractionDigits: 0
        }).format(amount);

    // ─────────────────────────────────────────────────────────────
    //  ANIMACIÓN DE CONTADORES
    // ─────────────────────────────────────────────────────────────
    const animateCounters = () => {
        document.querySelectorAll(SEL.counters).forEach(el => {
            const raw    = el.dataset.target ?? '0';
            const target = el.classList.contains('currency')
                ? parseFloat(raw)   // preservar decimales para ingresos
                : parseInt(raw, 10);

            if (!Number.isFinite(target) || target <= 0) return;

            // Sin animación si el usuario prefiere movimiento reducido.
            if (prefersReducedMotion()) {
                el.textContent = el.classList.contains('currency')
                    ? fmtCOP(target)
                    : String(target);
                return;
            }

            const DURATION = 700; // ms
            const start    = performance.now();

            const tick = now => {
                const progress = Math.min((now - start) / DURATION, 1);
                const current  = target * progress;

                el.textContent = el.classList.contains('currency')
                    ? fmtCOP(current)
                    : String(Math.round(current));

                if (progress < 1) requestAnimationFrame(tick);
            };

            requestAnimationFrame(tick);
        });
    };

    // ─────────────────────────────────────────────────────────────
    //  BARRA DE OCUPACIÓN
    // ─────────────────────────────────────────────────────────────
    const initProgressBar = () => {
        const bar = q(SEL.occupancyBar);
        if (!bar) return;

        const raw   = Number(bar.dataset.width);
        const width = Number.isFinite(raw) ? Math.max(0, Math.min(100, raw)) : 0;

        if (prefersReducedMotion()) {
            bar.style.width = `${width}%`;
            return;
        }

        // Un frame de retraso para que la transición CSS se active.
        requestAnimationFrame(() => {
            bar.style.width = `${width}%`;
        });
    };

    // ─────────────────────────────────────────────────────────────
    //  SIDEBAR RESPONSIVE
    // ─────────────────────────────────────────────────────────────
    const initSidebar = () => {
        const hamburger = q(SEL.hamburger);
        const sidebar   = q(SEL.sidebar);
        const overlay   = q(SEL.overlay);

        if (!hamburger || !sidebar || !overlay) return;

        const setOpen = open => {
            sidebar.classList.toggle('open', open);
            overlay.classList.toggle('open', open);
            hamburger.setAttribute('aria-expanded', String(open));
            overlay.setAttribute('aria-hidden', String(!open));
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

    // ─────────────────────────────────────────────────────────────
    //  EXPORTAR PDF
    // ─────────────────────────────────────────────────────────────
    const initExport = () => {
        const btn     = q(SEL.exportBtn);
        const btnText = q(SEL.exportBtnText);
        if (!btn) return;

        btn.addEventListener('click', async () => {
            if (btn.disabled) return;

            btn.disabled = true;
            if (btnText) btnText.textContent = 'Generando…';

            try {
                const res = await fetch(
                    '/gestion-de-citas/st-adm-01-dashboard/exportar-pdf',
                    { method: 'GET', credentials: 'same-origin', headers: { Accept: 'application/pdf' } }
                );

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

    // ─────────────────────────────────────────────────────────────
    //  INICIALIZACIÓN
    // ─────────────────────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', () => {
        initSidebar();
        initExport();
        animateCounters();
        initProgressBar();
    });
})();
