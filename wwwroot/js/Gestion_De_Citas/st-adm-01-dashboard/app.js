/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Dashboard de Citas Administrativo (st-adm-01-dashboard)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-adm-01-dashboard/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Maneja las interacciones del lado del cliente en el dashboard administrativo de citas.
 * Inicializa la barra visual de ocupación y la exportación de reportes PDF.
 * El layout compartido gestiona la navegación y animación de contadores.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Respeto a `prefers-reduced-motion` en la barra de ocupación.
 * - Despliegue de notificaciones vía ToastService global.
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

    const SELECTORS = {
        exportBtn:     '#btnExport',
        exportBtnText: '#btnExport .btn-export-text',
        occupancyBar:  '#dashboardOccupancyBar'
    };

    const API_ENDPOINTS = {
        exportPdf: '/gestion-de-citas/st-adm-01-dashboard/exportar-pdf'
    };

    // ═══════════════════════════════════════════════════════════════════
    // 2. ESTADO DE LA APLICACIÓN
    // ═══════════════════════════════════════════════════════════════════

    // Módulo sin estado persistente; la vista y el layout compartido proveen los datos.

    // ═══════════════════════════════════════════════════════════════════
    // 3. UTILIDADES
    // ═══════════════════════════════════════════════════════════════════

    /** Shorthand para document.querySelector. */
    const qs = sel => document.querySelector(sel);

    /** Detecta si el usuario prefiere movimiento reducido. */
    const prefersReducedMotion = () =>
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ═══════════════════════════════════════════════════════════════════
    // 4. SERVICIOS Y API
    // ═══════════════════════════════════════════════════════════════════

    /**
     * Descarga el reporte PDF del dashboard desde el servidor.
     * @returns {Promise<Response>}
     */
    const fetchDashboardPdf = () =>
        window.SmileTrack.utils.fetchWithTimeout(API_ENDPOINTS.exportPdf, {
            method:      'GET',
            credentials: 'same-origin',
            headers:     { Accept: 'application/pdf' },
            timeoutMs:   60000
        });

    // ═══════════════════════════════════════════════════════════════════
    // 5. RENDERIZADO Y DOM
    // ═══════════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════════
    // 6. EXPORTACIÓN DE REPORTES
    // ═══════════════════════════════════════════════════════════════════

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
            btn.setAttribute('aria-busy', 'true');
            const idleText = btnText?.textContent ?? 'Exportar PDF';
            if (btnText) btnText.textContent = 'Generando…';

            try {
                const res = await fetchDashboardPdf();
                if (!res.ok) throw new Error(`HTTP ${res.status}`);

                const contentType = res.headers.get('content-type')?.toLowerCase() ?? '';
                if (!contentType.includes('application/pdf')) {
                    throw new Error('El servidor no devolvió un PDF.');
                }

                const blob = await res.blob();
                if (blob.size === 0) throw new Error('El archivo PDF está vacío.');
                const url  = URL.createObjectURL(blob);
                const now  = new Date();
                const localDate = [
                    now.getFullYear(),
                    String(now.getMonth() + 1).padStart(2, '0'),
                    String(now.getDate()).padStart(2, '0')
                ].join('-');
                const link = Object.assign(document.createElement('a'), {
                    href:     url,
                    download: `reporte-dashboard-${localDate}.pdf`
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
                btn.removeAttribute('aria-busy');
                if (btnText) btnText.textContent = idleText;
            }
        });
    };

    // ═══════════════════════════════════════════════════════════════════
    // 7. INICIALIZACIÓN
    // ═══════════════════════════════════════════════════════════════════

    document.addEventListener('DOMContentLoaded', () => {
        initExportButton();
        initOccupancyBar();
    });

})();
