/**
 * ============================================
 * SmileTrack — Dashboard Admin (app.js)
 * ============================================
 * Autor: Johan Santamaria
 * 
 * PROPÓSITO:
 * Maneja interacciones del dashboard: animaciones, 
 * exportación de reportes, navegación responsive y notificaciones.
 * 
 * DECISIONES TÉCNICAS:
 * - Cola de toasts: evita solapamiento de notificaciones rápidas
 * - trackedRAF: cleanup de animaciones para prevenir memory leaks
 * - Debounce con maxWait: balance entre responsividad y performance
 * - Fallbacks progresivos: funcionalidad básica si JS falla parcialmente
 * 
 * NOTAS DE MANTENIMIENTO:
 * - Comentarios explican el "por qué" de las decisiones, no el "qué" del código
 * - API_BASE se lee de window.APP_CONFIG para facilitar testing y despliegues multi-entorno
 * ============================================
 */

// ════════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN GLOBAL
// ════════════════════════════════════════════════════════════════════
// WHY: Leer de window.APP_CONFIG permite cambiar la base de API sin recompilar JS
const API_BASE = (window.APP_CONFIG && window.APP_CONFIG.ApiBase) ? window.APP_CONFIG.ApiBase : '/api';
const activeAnimations = new Set();

// ════════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ════════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  // WHY: console.warn en lugar de throw permite que la UI continúe funcionando parcialmente
  if (!el) console.warn(`[SmileTrack][UI] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay, maxWait = null) => {
  let timeoutId;
  let lastInvokeTime = 0;
  return (...args) => {
    const now = Date.now();
    clearTimeout(timeoutId);
    // WHY: maxWait previene que el usuario espere indefinidamente si sigue escribiendo
    if (maxWait && lastInvokeTime && (now - lastInvokeTime >= maxWait)) {
      lastInvokeTime = now;
      fn.apply(this, args);
    } else {
      if (!lastInvokeTime) lastInvokeTime = now;
      timeoutId = setTimeout(() => {
        lastInvokeTime = 0;
        fn.apply(this, args);
      }, delay);
    }
  };
};


const trackedRAF = (callback) => {
  let id;
  const wrapper = (timestamp) => {
    callback(timestamp);
    activeAnimations.delete(id);
  };
  id = requestAnimationFrame(wrapper);
  activeAnimations.add(id);
  return id;
};

// ════════════════════════════════════════════════════════════════════
//  FUNCIONES DE ANIMACIÓN
// ════════════════════════════════════════════════════════════════════

const animateCounter = (el, targetStr) => {
  if (!el) return;
  const target = parseInt(targetStr, 10);
  // WHY: Validar target previene animaciones infinitas o valores NaN en UI
  if (isNaN(target) || target <= 0) return;

  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

const initNativeAnimations = () => {
  document.querySelectorAll('.stat-number[data-target]').forEach(el => {
    animateCounter(el, el.dataset.target);
  });

  // WHY: trackedRAF permite cleanup de animaciones en beforeunload para prevenir memory leaks
  trackedRAF(() => {
    document.querySelectorAll('[data-width]').forEach(bar => {
      bar.style.width = bar.dataset.width + '%';
    });
  });
};

// ════════════════════════════════════════════════════════════════════
//  EXPORTAR REPORTE PDF
// ════════════════════════════════════════════════════════════════════

async function exportReport() {
  // TRY/CATCH EN FUNCIÓN ASÍNCRONA:
  // - Permite manejar errores de red o generación de blob sin romper la UI
  // - El catch re-lanza el error para que el caller (initExport) muestre toast de error
  try {
    // Simulación de delay de red: en producción reemplazar con fetch real a API
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    const blob = new Blob(['Reporte de Dashboard SmileTrack'], { type: 'application/pdf' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `reporte-dashboard-${new Date().toISOString().slice(0, 10)}.pdf`;
    a.click();
    window.URL.revokeObjectURL(url);
    
    return true;
  } catch (error) {
    // WHY: Loggear el error ayuda a debugging, pero no mostrar detalles sensibles al usuario
    console.error('[SmileTrack][API] Error exportando reporte:', error);
    throw error; // Re-lanzar para que el caller maneje la UI de error
  }
}

// ════════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ════════════════════════════════════════════════════════════════════

const cleanupHandlers = [];

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');

  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);

    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  const handleHamburgerClick = () => toggleMenu(true);
  const handleOverlayClick = () => toggleMenu(false);
  const handleKeyDown = (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  };

  hamburger.addEventListener('click', handleHamburgerClick);
  overlay.addEventListener('click', handleOverlayClick);
  document.addEventListener('keydown', handleKeyDown);

  // WHY: cleanupHandlers permite remover event listeners en beforeunload para prevenir memory leaks
  cleanupHandlers.push(() => {
    hamburger.removeEventListener('click', handleHamburgerClick);
    overlay.removeEventListener('click', handleOverlayClick);
    document.removeEventListener('keydown', handleKeyDown);
  });

  const navItems = sidebar.querySelectorAll('.nav-item');
  const handleNavClick = () => {
    // WHY: Cerrar menú en móvil al navegar mejora UX en pantallas pequeñas
    if (window.innerWidth <= 680) toggleMenu(false);
  };
  navItems.forEach(item => item.addEventListener('click', handleNavClick));
  cleanupHandlers.push(() => {
    navItems.forEach(item => item.removeEventListener('click', handleNavClick));
  });
};

const initExport = () => {
  const btn = safeGetElement('btnExport');
  const progressBar = safeGetElement('topProgressBar');

  if (!btn || !progressBar) return;

  const handleExport = async () => {
    if (btn.disabled) return;

    btn.disabled = true;
    btn.innerHTML = '⏳ Generando...';

    progressBar.style.transition = 'width 1s cubic-bezier(.4,0,.2,1)';
    progressBar.style.width = '100%';

    // TRY/CATCH EN EVENT HANDLER ASÍNCRONO:
    // - Permite manejar errores de exportReport sin romper la UI
    // - finally restaura el estado del botón incluso si hay error
    try {
      await exportReport();
      window.ToastService.error('✅ Reporte PDF generado exitosamente');
    } catch (error) {
      // WHY: Mostrar toast de error da feedback inmediato al usuario sin bloquear la interfaz
      window.ToastService.success('❌ Error al generar reporte');
    } finally {
      setTimeout(() => {
        btn.disabled = false;
        btn.innerHTML = '📄 Exportar PDF';
        const originalWidth = progressBar.closest('.progress-row').getAttribute('aria-valuenow');
        progressBar.style.width = (originalWidth || '75') + '%';
      }, 500);
    }
  };

  btn.addEventListener('click', handleExport);
  cleanupHandlers.push(() => btn.removeEventListener('click', handleExport));
};

// ════════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ════════════════════════════════════════════════════════════════════

const init = async () => {
  // TRY/CATCH EN FUNCIÓN PRINCIPAL:
  // - Captura errores críticos durante la inicialización que podrían romper toda la página
  // - Loggear el error ayuda a debugging sin exponer detalles al usuario final
  try {
    initSidebar();
    initExport();
    initNativeAnimations();

    setTimeout(() => {
      window.ToastService.success('✅ Panel administrativo cargado');
    }, 500);
  } catch (error) {
    // WHY: console.error con contexto ayuda a identificar la causa raíz en logs de producción
    console.error('[SmileTrack][Init] Falla crítica durante la inicialización:', error);
    // Opcional: mostrar toast de error genérico al usuario
    // window.ToastService.error('⚠️ Error cargando el panel. Recargue la página.');
  }

  // WHY: beforeunload cleanup previene memory leaks en SPA o navegación frecuente
  window.addEventListener('beforeunload', () => {
    activeAnimations.forEach(id => cancelAnimationFrame(id));
    activeAnimations.clear();
    cleanupHandlers.forEach(fn => fn());
  });
};

document.addEventListener('DOMContentLoaded', init);