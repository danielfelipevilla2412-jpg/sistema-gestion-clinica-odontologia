/* ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: Dashboard del Odontólogo (st-odo-01-dashboard)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Profesionales/st-odo-01-dashboard/app.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Panel de control principal para el profesional odontológico logueado.
 * Presenta el banner de próxima cita urgente, tarjetas KPI de citas atendidas/pendientes del día,
 * barra de avance de metas clínicas y accesos directos a la historia clínica.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Datos servidos por SSR y refrescados mediante solicitudes periódicas para mantener el widget activo.
 * - Formateo automático de moneda COP para ingresos acumulados.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionProfesionalesController -> Stodo01Dashboard
 * - HTML: Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const AUTO_REFRESH_INTERVAL_MS = 300_000; // 5 minutos
const INACTIVITY_TIMEOUT_MS = 600_000;    // 10 minutos
const COUNTDOWN_INTERVAL_MS = 60_000;      // 1 minuto

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

let lastUserActivityTimestamp = Date.now();

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

const displayCurrentHeaderDate = () => {
  const headerDateElement = safeGetElement('headerDate');
  if (!headerDateElement) return;

  const now = new Date();
  const formattedDate = now.toLocaleDateString('es-CO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  });

  headerDateElement.textContent = formattedDate;
  headerDateElement.setAttribute('datetime', now.toISOString().slice(0, 10));
};

const addKpiTooltips = () => {
  const statCards = document.querySelectorAll('.stat-card');
  
  statCards.forEach(cardElement => {
    const labelText = cardElement.querySelector('.stat-label')?.textContent;
    
    if (labelText) {
      let tooltipText = '';
      
      if (labelText.includes('Pacientes del mes')) {
        tooltipText = 'Número de pacientes únicos atendidos este mes';
      } else if (labelText.includes('Citas hoy')) {
        tooltipText = 'Total de citas programadas para hoy';
      } else if (labelText.includes('Atendidas')) {
        tooltipText = 'Citas completadas hoy';
      } else if (labelText.includes('Ingresos')) {
        tooltipText = 'Ingresos generados este mes por citas atendidas';
      }
      
      if (tooltipText) {
        cardElement.setAttribute('title', tooltipText);
        cardElement.style.cursor = 'help';
      }
    }
  });
};

const enhanceKeyboardNavigation = () => {
  const focusableElements = document.querySelectorAll(
    'a, button, input, select, textarea, [tabindex]:not([tabindex="-1"])'
  );

  focusableElements.forEach(element => {
    element.addEventListener('keydown', (event) => {
      if (event.key === 'Home') {
        event.preventDefault();
        focusableElements[0]?.focus();
      }
      
      if (event.key === 'End') {
        event.preventDefault();
        focusableElements[focusableElements.length - 1]?.focus();
      }
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const setupAutoRefreshStats = () => {
  ['mousedown', 'keydown', 'scroll', 'touchstart'].forEach(eventName => {
    document.addEventListener(eventName, () => {
      lastUserActivityTimestamp = Date.now();
    }, { passive: true });
  });

  const autoRefreshHandler = async () => {
    if (Date.now() - lastUserActivityTimestamp > INACTIVITY_TIMEOUT_MS) {
      return;
    }

    try {
      const response = await fetch('/api/profesionales/dashboard-stats', {
        method: 'GET',
        headers: { 'Accept': 'application/json' }
      });

      if (!response.ok) return;

      const responseData = await response.json();
      
      if (responseData.citasHoy !== undefined) {
        const appointmentCountEl = safeGetElement('statCitas');
        if (appointmentCountEl && typeof window.animateCounter === 'function') {
          window.animateCounter(appointmentCountEl, responseData.citasHoy);
        }
      }

      if (responseData.citasAtendidas !== undefined) {
        const attendedCountEl = safeGetElement('statProfesionales');
        if (attendedCountEl && typeof window.animateCounter === 'function') {
          window.animateCounter(attendedCountEl, responseData.citasAtendidas);
        }
      }

      setTimeout(updateDailyProgress, 1000);
      console.log('[SmileTrack] Dashboard actualizado:', new Date().toLocaleTimeString('es-CO'));
    } catch (error) {
      console.error('[SmileTrack] Error actualizando dashboard:', error);
    }
  };

  setInterval(autoRefreshHandler, AUTO_REFRESH_INTERVAL_MS);
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const renderRevenueChart = () => {
  const chartContainer = safeGetElement('revenueChart');
  if (!chartContainer) return;

  const dataset = Array.isArray(window.ODO_REVENUE) ? window.ODO_REVENUE : [];

  if (!dataset.length) {
    const emptyMessage = document.createElement('p');
    emptyMessage.style.cssText = 'padding:16px;color:var(--text-muted);';
    emptyMessage.textContent = 'No hay ingresos registrados en el período.';
    chartContainer.replaceChildren(emptyMessage);
    return;
  }

  const maxRevenueValue = Math.max(
    ...dataset.map(item => Number(item.valor) || 0),
    0
  );

  const currencyFormatter = new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0
  });

  chartContainer.innerHTML = dataset.map(item => {
    const revenueValue = Number(item.valor) || 0;
    const barWidth = maxRevenueValue > 0
      ? Math.round((revenueValue / maxRevenueValue) * 100)
      : 0;

    const colorClass = (revenueValue === maxRevenueValue && maxRevenueValue > 0)
      ? 'green'
      : 'blue';

    return `
      <div class="chart-row">
        <span class="chart-label">${escapeHtml(item.mes)}</span>
        <div class="chart-bar-bg">
          <div
            class="chart-bar-fill ${colorClass}"
            data-width="${barWidth}"
            style="width:0"
            role="progressbar"
            aria-valuenow="${barWidth}"
            aria-valuemin="0"
            aria-valuemax="100"
            aria-label="${escapeHtml(item.mes)}: ${escapeHtml(currencyFormatter.format(revenueValue))}"
          ></div>
        </div>
        <span class="chart-val">${currencyFormatter.format(revenueValue)}</span>
      </div>`;
  }).join('');

  requestAnimationFrame(() => {
    chartContainer.querySelectorAll('.chart-bar-fill').forEach(barElement => {
      barElement.style.width = `${Number(barElement.dataset.width) || 0}%`;
    });
  });
};

const animateStatusBars = () => {
  document.querySelectorAll('.status-bar-fill').forEach(barElement => {
    const targetWidth = Number(barElement.dataset.width) || 0;
    requestAnimationFrame(() => {
      barElement.style.width = `${targetWidth}%`;
    });
  });
};

const updateNextAppointmentCountdown = () => {
  const appointmentCard = document.querySelector('.next-appt-card.urgent, .next-appt-card.upcoming');
  if (!appointmentCard) return;

  const countdownLabel = appointmentCard.querySelector('.next-appt-countdown');
  if (!countdownLabel) return;

  const timeText = appointmentCard.querySelector('.next-appt-time')?.textContent;
  if (!timeText) return;

  const timeMatch = timeText.match(/(\d{2}):(\d{2})/);
  if (!timeMatch) return;

  const now = new Date();
  const appointmentToday = new Date(now);
  appointmentToday.setHours(parseInt(timeMatch[1], 10), parseInt(timeMatch[2], 10), 0, 0);

  const differenceMs = appointmentToday - now;
  const differenceMinutes = Math.floor(differenceMs / 60000);

  if (differenceMinutes <= 0) {
    countdownLabel.textContent = '⏰ ¡Es ahora!';
    countdownLabel.style.background = 'rgba(255,255,255,0.4)';
    countdownLabel.style.animation = 'pulse-urgent 1s ease-in-out infinite';
  } else if (differenceMinutes <= 15) {
    countdownLabel.textContent = `⏰ En ${differenceMinutes} minuto${differenceMinutes !== 1 ? 's' : ''}`;
    if (!appointmentCard.classList.contains('urgent')) {
      appointmentCard.classList.add('urgent');
      appointmentCard.classList.remove('upcoming');
    }
  } else {
    countdownLabel.textContent = `🕐 En ${differenceMinutes} minutos`;
  }
};

const updateDailyProgress = () => {
  const progressBar = safeGetElement('topProgressBar');
  const progressLabel = safeGetElement('dailyProgressLabel');
  
  if (!progressBar || !progressLabel) return;

  const statCitasEl = safeGetElement('statCitas');
  const statProfesionalesEl = safeGetElement('statProfesionales');

  if (!statCitasEl || !statProfesionalesEl) return;

  const totalAppointmentsToday = parseInt(statCitasEl.textContent, 10) || 0;
  const attendedAppointments = parseInt(statProfesionalesEl.textContent, 10) || 0;
  const progressPercentage = totalAppointmentsToday > 0 ? Math.round((attendedAppointments / totalAppointmentsToday) * 100) : 0;

  setTimeout(() => {
    progressBar.style.width = `${progressPercentage}%`;
    progressBar.parentElement.setAttribute('aria-valuenow', progressPercentage);
    progressLabel.textContent = `${attendedAppointments} de ${totalAppointmentsToday} citas completadas hoy (${progressPercentage}%)`;
  }, 500);
};

const animatePerformanceCircle = () => {
  const scoreCircleElement = document.querySelector('.score-circle');
  if (!scoreCircleElement) return;

  const scoreValue = parseInt(scoreCircleElement.dataset.score, 10) || 0;
  const circleElement = scoreCircleElement.querySelector('circle[stroke="url(#gradient)"]');
  
  if (!circleElement) return;

  const circumference = 314; // 2 * π * 50
  const targetDasharrayLength = (circumference * scoreValue) / 100;

  circleElement.style.strokeDasharray = `0 ${circumference}`;
  
  requestAnimationFrame(() => {
    setTimeout(() => {
      circleElement.style.strokeDasharray = `${targetDasharrayLength} ${circumference}`;
    }, 300);
  });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const setupExportButtonListener = () => {
  const exportBtn = safeGetElement('btnExport');
  if (!exportBtn) return;

  exportBtn.addEventListener('click', () => {
    if (window.ToastService) {
      window.ToastService.warning('La exportación de reportes se habilitará en una fase posterior.');
    }
  });
};

const setupQuickAccessCards = () => {
  const quickActionCards = document.querySelectorAll('.quick-action-card');
  
  quickActionCards.forEach(cardElement => {
    cardElement.addEventListener('click', function(event) {
      const rippleSpan = document.createElement('span');
      rippleSpan.style.cssText = `
        position: absolute;
        border-radius: 50%;
        background: rgba(26, 86, 204, 0.3);
        width: 100px;
        height: 100px;
        margin-top: -50px;
        margin-left: -50px;
        animation: ripple 0.6s;
        pointer-events: none;
      `;
      
      const boundingRectangle = this.getBoundingClientRect();
      rippleSpan.style.left = (event.clientX - boundingRectangle.left) + 'px';
      rippleSpan.style.top = (event.clientY - boundingRectangle.top) + 'px';
      
      this.appendChild(rippleSpan);
      setTimeout(() => rippleSpan.remove(), 600);
    });

    if (!document.getElementById('ripple-animation')) {
      const styleSheet = document.createElement('style');
      styleSheet.id = 'ripple-animation';
      styleSheet.textContent = `
        @keyframes ripple {
          from { opacity: 1; transform: scale(0); }
          to { opacity: 0; transform: scale(2); }
        }
      `;
      document.head.appendChild(styleSheet);
    }
  });
};

const setupDismissibleNotifications = () => {
  const notificationItems = document.querySelectorAll('.notif-item');
  
  notificationItems.forEach(itemElement => {
    itemElement.style.cursor = 'pointer';
    
    itemElement.addEventListener('click', function() {
      this.style.opacity = '0.5';
      this.style.pointerEvents = 'none';
      
      setTimeout(() => {
        this.style.display = 'none';
        
        const remainingItems = Array.from(document.querySelectorAll('.notif-item'))
          .filter(element => element.style.display !== 'none');
        
        if (remainingItems.length === 0) {
          const notificationsList = document.querySelector('.notifications-list');
          if (notificationsList) {
            notificationsList.innerHTML = `
              <div class="notif-empty">
                <span class="material-symbols-outlined">check_circle</span>
                <span>Todo al día</span>
              </div>
            `;
          }
        }
      }, 300);
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupSidebarNavigation = () => {
  const hamburgerButton = safeGetElement('hamburger');
  const sidebarElement = safeGetElement('sidebar');
  const overlayElement = safeGetElement('overlay');
  if (!hamburgerButton || !sidebarElement || !overlayElement) return;

  const toggleMenuState = (shouldShow) => {
    sidebarElement.classList.toggle('open', shouldShow);
    overlayElement.classList.toggle('open', shouldShow);
    hamburgerButton.setAttribute('aria-expanded', String(shouldShow));
    overlayElement.setAttribute('aria-hidden', String(!shouldShow));

    if (shouldShow) sidebarElement.querySelector('.nav-item')?.focus();
    else hamburgerButton.focus();
  };

  hamburgerButton.addEventListener('click', () => toggleMenuState(true));
  overlayElement.addEventListener('click', () => toggleMenuState(false));

  sidebarElement.querySelectorAll('.nav-item').forEach(navItem => {
    navItem.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenuState(false);
    });
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && sidebarElement.classList.contains('open')) {
      event.preventDefault();
      toggleMenuState(false);
    }
  });
};

const initDashboardEnhancements = () => {
  setTimeout(updateDailyProgress, 1500);
  setTimeout(animatePerformanceCircle, 800);

  setupQuickAccessCards();
  setupDismissibleNotifications();
  addKpiTooltips();
  enhanceKeyboardNavigation();

  updateNextAppointmentCountdown();
  setInterval(updateNextAppointmentCountdown, COUNTDOWN_INTERVAL_MS);
};

const initializeDashboardModule = () => {
  setupSidebarNavigation();
  displayCurrentHeaderDate();
  setupExportButtonListener();

  document.querySelectorAll('.stat-number[data-target]').forEach(numberElement => {
    if (typeof window.animateCounter === 'function') {
      window.animateCounter(numberElement, numberElement.dataset.target);
    }
  });

  renderRevenueChart();
  animateStatusBars();

  setTimeout(initDashboardEnhancements, 1000);
};

document.addEventListener('DOMContentLoaded', initializeDashboardModule);

window.addEventListener('beforeunload', () => {
  document.querySelectorAll('[style*="animation"]').forEach(animatedElement => {
    animatedElement.style.animation = 'none';
  });
});
