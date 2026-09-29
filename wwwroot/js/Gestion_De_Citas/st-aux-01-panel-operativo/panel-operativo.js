/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Panel Operativo de Auxiliares (st-aux-01-panel-operativo)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra el panel operativo en tiempo real para el personal de auxilio clínico.
 * Permite visualizar el flujo de pacientes del día, actualizar estados de consultorio y atención,
 * y consultar indicadores de rendimiento.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtra únicamente el flujo de la jornada actual.
 * - Animación fluida de contadores KPI con gestión de cola para notificaciones Toast.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux01PanelOperativo
 * - HTML: Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml
 * ============================================ */

// ════════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ════════════════════════════════════════════════════════════════════

const API_BASE_URL = (window.APP_CONFIG && window.APP_CONFIG.ApiBase) ? window.APP_CONFIG.ApiBase : '/api';
const activeAnimationsSet = new Set();
const cleanupHandlersList = [];

// ════════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ════════════════════════════════════════════════════════════════════

let isExportingPdfState = false;

// ════════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ════════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const trackedRequestAnimationFrame = (callback) => {
  let animationId;
  const wrapper = (timestamp) => {
    callback(timestamp);
    activeAnimationsSet.delete(animationId);
  };
  animationId = requestAnimationFrame(wrapper);
  activeAnimationsSet.add(animationId);
  return animationId;
};

// ════════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ════════════════════════════════════════════════════════════════════

async function exportReportPdf() {
  try {
    const response = await fetch('/gestion-de-citas/st-adm-01-dashboard/exportar-pdf', {
      method: 'GET',
      credentials: 'same-origin',
      headers: { Accept: 'application/pdf' }
    });
    if (!response.ok) throw new Error(`Error HTTP ${response.status}`);
    const pdfBlob = await response.blob();
    const blobUrl = window.URL.createObjectURL(pdfBlob);
    const downloadAnchor = document.createElement('a');
    downloadAnchor.href = blobUrl;
    downloadAnchor.download = `reporte-dashboard-${new Date().toISOString().slice(0, 10)}.pdf`;
    downloadAnchor.click();
    window.URL.revokeObjectURL(blobUrl);
    
    return true;
  } catch (error) {
    console.error('[SmileTrack][API] Error exportando reporte:', error);
    throw error;
  }
}

// ════════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ════════════════════════════════════════════════════════════════════

const initializeNativeAnimations = () => {
  document.querySelectorAll('.stat-number[data-target]:not([data-animated="1"])').forEach(element => {
    if (typeof window.animateCounter === 'function') {
      window.animateCounter(element, Number(element.dataset.target) || 0);
    }
  });

  trackedRequestAnimationFrame(() => {
    document.querySelectorAll('[data-width]').forEach(progressBar => {
      progressBar.style.width = progressBar.dataset.width + '%';
    });
  });
};

// ════════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ════════════════════════════════════════════════════════════════════

const setupExportButtonListener = () => {
  const exportBtn = safeGetElement('btnExport');
  const exportIcon = safeGetElement('btnExportIcon');
  const exportLabel = safeGetElement('btnExportLabel');

  if (!exportBtn || !exportIcon || !exportLabel) return;

  const handleExportClick = async () => {
    if (exportBtn.disabled || isExportingPdfState) return;

    isExportingPdfState = true;
    exportBtn.disabled = true;
    exportIcon.classList.add('spin');
    exportLabel.textContent = 'Generando...';

    try {
      await exportReportPdf();
      if (window.ToastService) window.ToastService.success('✅ Reporte PDF generado exitosamente');
    } catch (error) {
      if (window.ToastService) window.ToastService.error('❌ Error al generar el reporte. Intenta de nuevo.');
    } finally {
      exportIcon.classList.remove('spin');
      exportLabel.textContent = 'Exportar PDF';
      exportBtn.disabled = false;
      isExportingPdfState = false;
    }
  };

  exportBtn.addEventListener('click', handleExportClick);
  cleanupHandlersList.push(() => exportBtn.removeEventListener('click', handleExportClick));
};

// ════════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ════════════════════════════════════════════════════════════════════

const setupSidebarNavigation = () => {
  const hamburgerButton = safeGetElement('hamburger');
  const sidebarElement = safeGetElement('sidebar');
  const overlayElement = safeGetElement('overlay');

  if (!hamburgerButton || !sidebarElement || !overlayElement) return;

  const toggleMenuState = (showMenu) => {
    sidebarElement.classList.toggle('open', showMenu);
    overlayElement.classList.toggle('open', showMenu);
    hamburgerButton.setAttribute('aria-expanded', String(showMenu));
    overlayElement.setAttribute('aria-hidden', String(!showMenu));

    if (showMenu) {
      const firstLink = sidebarElement.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburgerButton.focus();
    }
  };

  const handleHamburgerClick = () => toggleMenuState(true);
  const handleOverlayClick = () => toggleMenuState(false);
  const handleKeyDown = (event) => {
    if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
      event.preventDefault();
      toggleMenuState(false);
    }
  };

  hamburgerButton.addEventListener('click', handleHamburgerClick);
  overlayElement.addEventListener('click', handleOverlayClick);
  document.addEventListener('keydown', handleKeyDown);

  cleanupHandlersList.push(() => {
    hamburgerButton.removeEventListener('click', handleHamburgerClick);
    overlayElement.removeEventListener('click', handleOverlayClick);
    document.removeEventListener('keydown', handleKeyDown);
  });

  const navItems = sidebarElement.querySelectorAll('.nav-item');
  const handleNavClick = () => {
    if (window.innerWidth <= 680) toggleMenuState(false);
  };
  navItems.forEach(item => item.addEventListener('click', handleNavClick));
  cleanupHandlersList.push(() => {
    navItems.forEach(item => item.removeEventListener('click', handleNavClick));
  });
};

const initializePanelOperativoModule = async () => {
  try {
    setupSidebarNavigation();
    setupExportButtonListener();
    initializeNativeAnimations();

    setTimeout(() => {
      if (window.ToastService) window.ToastService.success('✅ Panel administrativo cargado');
    }, 500);
  } catch (error) {
    console.error('[SmileTrack][Init] Falla crítica durante la inicialización:', error);
  }

  window.addEventListener('beforeunload', () => {
    activeAnimationsSet.forEach(animationId => cancelAnimationFrame(animationId));
    activeAnimationsSet.clear();
    cleanupHandlersList.forEach(cleanupFunction => cleanupFunction());
  });
};

document.addEventListener('DOMContentLoaded', initializePanelOperativoModule);