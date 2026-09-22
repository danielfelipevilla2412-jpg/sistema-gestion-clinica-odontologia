/*
 SmileTrack — Dashboard del Odontólogo (st-odo-01-dashboard)
 Fuente de datos: Controller + Razor. No usa datos de demostración.
*/

const safeGetElement = (id) => document.getElementById(id);

const escapeHtml = (value) => String(value ?? '')
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#039;');

const animateCounter = (el, target) => {
  if (!el) return;
  const numericTarget = Number(target) || 0;
  if (numericTarget === 0) {
    el.textContent = '0';
    return;
  }

  let current = 0;
  const step = Math.max(1, Math.ceil(numericTarget / 30));
  const timer = setInterval(() => {
    current = Math.min(current + step, numericTarget);
    el.textContent = current;
    if (current >= numericTarget) clearInterval(timer);
  }, 30);
};

const initHeaderDate = () => {
  const headerDate = safeGetElement('headerDate');
  if (!headerDate) return;

  const now = new Date();
  const formatted = now.toLocaleDateString('es-CO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  });

  headerDate.textContent = formatted;
  headerDate.setAttribute('datetime', now.toISOString().slice(0, 10));
};

const renderRevenueChart = () => {
  const container = safeGetElement('revenueChart');
  if (!container) return;

  const data = Array.isArray(window.ODO_REVENUE)
    ? window.ODO_REVENUE
    : [];

  if (!data.length) {
    const empty = document.createElement('p');
    empty.style.cssText = 'padding:16px;color:var(--text-muted);';
    empty.textContent = 'No hay ingresos registrados en el período.';
    container.replaceChildren(empty);
    return;
  }

  const maxValue = Math.max(
    ...data.map((item) => Number(item.valor) || 0),
    0
  );

  const currency = new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0
  });

  container.innerHTML = data.map((item) => {
    const value = Number(item.valor) || 0;
    const width = maxValue > 0
      ? Math.round((value / maxValue) * 100)
      : 0;

    const colorClass = value === maxValue && maxValue > 0
      ? 'green'
      : 'blue';

    return `
      <div class="chart-row">
        <span class="chart-label">${escapeHtml(item.mes)}</span>
        <div class="chart-bar-bg">
          <div
            class="chart-bar-fill ${colorClass}"
            data-width="${width}"
            style="width:0"
            role="progressbar"
            aria-valuenow="${width}"
            aria-valuemin="0"
            aria-valuemax="100"
            aria-label="${escapeHtml(item.mes)}: ${escapeHtml(currency.format(value))}"
          ></div>
        </div>
        <span class="chart-val">${currency.format(value)}</span>
      </div>`;
  }).join('');

  requestAnimationFrame(() => {
    container.querySelectorAll('.chart-bar-fill').forEach((bar) => {
      bar.style.width = `${Number(bar.dataset.width) || 0}%`;
    });
  });
};

const animateExistingBars = () => {
  document.querySelectorAll('.status-bar-fill').forEach((bar) => {
    const width = Number(bar.dataset.width) || 0;
    requestAnimationFrame(() => {
      bar.style.width = `${width}%`;
    });
  });
};

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

    if (show) sidebar.querySelector('.nav-item')?.focus();
    else hamburger.focus();
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  sidebar.querySelectorAll('.nav-item').forEach((item) => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && sidebar.classList.contains('open')) {
      event.preventDefault();
      toggleMenu(false);
    }
  });
};

const initExport = () => {
  const btn = safeGetElement('btnExport');
  if (!btn) return;

  btn.addEventListener('click', () => {
    if (window.ToastService) {
      window.ToastService.warning(
        'La exportación de reportes se habilitará en una fase posterior.'
      );
    }
  });
};

const init = () => {
  initSidebar();
  initHeaderDate();
  initExport();

  document.querySelectorAll('.stat-number[data-target]').forEach((el) => {
    animateCounter(el, el.dataset.target);
  });

  renderRevenueChart();
  animateExistingBars();
};

document.addEventListener('DOMContentLoaded', init);


/* ═══════════════════════════════════════════════════════════════
   MEJORAS 2026-09-15: FUNCIONALIDADES DINÁMICAS DASHBOARD
   ═══════════════════════════════════════════════════════════════ */

/**
 * Actualiza el countdown de la próxima cita urgente
 */
const actualizarCountdownProximaCita = () => {
  const card = document.querySelector('.next-appt-card.urgent, .next-appt-card.upcoming');
  if (!card) return;

  const countdown = card.querySelector('.next-appt-countdown');
  if (!countdown) return;

  // Extraer la hora de la cita del texto
  const timeText = card.querySelector('.next-appt-time')?.textContent;
  if (!timeText) return;

  const match = timeText.match(/(\d{2}):(\d{2})/);
  if (!match) return;

  const now = new Date();
  const citaHoy = new Date(now);
  citaHoy.setHours(parseInt(match[1]), parseInt(match[2]), 0, 0);

  const diffMs = citaHoy - now;
  const diffMins = Math.floor(diffMs / 60000);

  if (diffMins <= 0) {
    countdown.textContent = '⏰ ¡Es ahora!';
    countdown.style.background = 'rgba(255,255,255,0.4)';
    countdown.style.animation = 'pulse-urgent 1s ease-in-out infinite';
  } else if (diffMins <= 15) {
    countdown.textContent = `⏰ En ${diffMins} minuto${diffMins !== 1 ? 's' : ''}`;
    if (!card.classList.contains('urgent')) {
      card.classList.add('urgent');
      card.classList.remove('upcoming');
    }
  } else {
    countdown.textContent = `🕐 En ${diffMins} minutos`;
  }
};

/**
 * Actualiza la barra de progreso del día dinámicamente
 */
const actualizarProgresoDelDia = () => {
  const progressBar = safeGetElement('topProgressBar');
  const progressLabel = safeGetElement('dailyProgressLabel');
  
  if (!progressBar || !progressLabel) return;

  const statCitas = safeGetElement('statCitas');
  const statProfesionales = safeGetElement('statProfesionales'); // Atendidas

  if (!statCitas || !statProfesionales) return;

  const totalHoy = parseInt(statCitas.textContent) || 0;
  const atendidas = parseInt(statProfesionales.textContent) || 0;
  const porcentaje = totalHoy > 0 ? Math.round((atendidas / totalHoy) * 100) : 0;

  // Animar el cambio
  setTimeout(() => {
    progressBar.style.width = `${porcentaje}%`;
    progressBar.parentElement.setAttribute('aria-valuenow', porcentaje);
    progressLabel.textContent = `${atendidas} de ${totalHoy} citas completadas hoy (${porcentaje}%)`;
  }, 500);
};

/**
 * Anima el círculo de rendimiento SVG
 */
const animarCirculoRendimiento = () => {
  const scoreCircle = document.querySelector('.score-circle');
  if (!scoreCircle) return;

  const score = parseInt(scoreCircle.dataset.score) || 0;
  const circle = scoreCircle.querySelector('circle[stroke="url(#gradient)"]');
  
  if (!circle) return;

  const circumference = 314; // 2 * π * 50 (radio)
  const targetLength = (circumference * score) / 100;

  // Animar desde 0 hasta el valor actual
  circle.style.strokeDasharray = `0 ${circumference}`;
  
  requestAnimationFrame(() => {
    setTimeout(() => {
      circle.style.strokeDasharray = `${targetLength} ${circumference}`;
    }, 300);
  });
};

/**
 * Agrega interactividad a las tarjetas de acceso rápido
 */
const inicializarAccesosRapidos = () => {
  const cards = document.querySelectorAll('.quick-action-card');
  
  cards.forEach(card => {
    // Agregar efecto de ripple al hacer click
    card.addEventListener('click', function(e) {
      const ripple = document.createElement('span');
      ripple.style.cssText = `
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
      
      const rect = this.getBoundingClientRect();
      ripple.style.left = (e.clientX - rect.left) + 'px';
      ripple.style.top = (e.clientY - rect.top) + 'px';
      
      this.appendChild(ripple);
      
      setTimeout(() => ripple.remove(), 600);
    });

    // Agregar animación CSS si no existe
    if (!document.getElementById('ripple-animation')) {
      const style = document.createElement('style');
      style.id = 'ripple-animation';
      style.textContent = `
        @keyframes ripple {
          from {
            opacity: 1;
            transform: scale(0);
          }
          to {
            opacity: 0;
            transform: scale(2);
          }
        }
      `;
      document.head.appendChild(style);
    }
  });
};

/**
 * Hace las notificaciones dismissibles (opcional)
 */
const inicializarNotificaciones = () => {
  const notifItems = document.querySelectorAll('.notif-item');
  
  notifItems.forEach(item => {
    // Agregar cursor pointer
    item.style.cursor = 'pointer';
    
    // Click para marcar como leída (solo visual)
    item.addEventListener('click', function() {
      this.style.opacity = '0.5';
      this.style.pointerEvents = 'none';
      
      setTimeout(() => {
        this.style.display = 'none';
        
        // Si no quedan notificaciones, mostrar mensaje "todo al día"
        const remaining = Array.from(document.querySelectorAll('.notif-item'))
          .filter(n => n.style.display !== 'none');
        
        if (remaining.length === 0) {
          const notifList = document.querySelector('.notifications-list');
          if (notifList) {
            notifList.innerHTML = `
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

/**
 * Auto-actualización de métricas cada 5 minutos
 */
const configurarAutoActualizacion = () => {
  // Solo en producción y si el usuario está activo
  let lastActivity = Date.now();
  
  ['mousedown', 'keydown', 'scroll', 'touchstart'].forEach(event => {
    document.addEventListener(event, () => {
      lastActivity = Date.now();
    }, { passive: true });
  });

  const autoRefresh = async () => {
    // No actualizar si el usuario ha estado inactivo más de 10 minutos
    if (Date.now() - lastActivity > 600000) {
      return;
    }

    try {
      const response = await fetch('/api/profesionales/dashboard-stats', {
        method: 'GET',
        headers: {
          'Accept': 'application/json'
        }
      });

      if (!response.ok) return;

      const data = await response.json();
      
      // Actualizar KPIs sin recargar la página
      if (data.citasHoy !== undefined) {
        const el = safeGetElement('statCitas');
        if (el) {
          animateCounter(el, data.citasHoy);
        }
      }

      if (data.citasAtendidas !== undefined) {
        const el = safeGetElement('statProfesionales');
        if (el) {
          animateCounter(el, data.citasAtendidas);
        }
      }

      // Actualizar barra de progreso
      setTimeout(actualizarProgresoDelDia, 1000);

      console.log('Dashboard actualizado:', new Date().toLocaleTimeString('es-CO'));
    } catch (error) {
      console.error('Error actualizando dashboard:', error);
    }
  };

  // Actualizar cada 5 minutos
  setInterval(autoRefresh, 300000);
};

/**
 * Añade tooltips informativos a los KPIs
 */
const agregarTooltipsKPIs = () => {
  const statCards = document.querySelectorAll('.stat-card');
  
  statCards.forEach(card => {
    const label = card.querySelector('.stat-label')?.textContent;
    
    if (label) {
      let tooltip = '';
      
      if (label.includes('Pacientes del mes')) {
        tooltip = 'Número de pacientes únicos atendidos este mes';
      } else if (label.includes('Citas hoy')) {
        tooltip = 'Total de citas programadas para hoy';
      } else if (label.includes('Atendidas')) {
        tooltip = 'Citas completadas hoy';
      } else if (label.includes('Ingresos')) {
        tooltip = 'Ingresos generados este mes por citas atendidas';
      }
      
      if (tooltip) {
        card.setAttribute('title', tooltip);
        card.style.cursor = 'help';
      }
    }
  });
};

/**
 * Manejo de teclado mejorado para accesibilidad
 */
const mejorarAccesibilidadTeclado = () => {
  // Navegación con Tab mejorada en tarjetas
  const focusableElements = document.querySelectorAll(
    'a, button, input, select, textarea, [tabindex]:not([tabindex="-1"])'
  );

  focusableElements.forEach((el, index) => {
    el.addEventListener('keydown', (e) => {
      // Home: ir al primer elemento
      if (e.key === 'Home') {
        e.preventDefault();
        focusableElements[0]?.focus();
      }
      
      // End: ir al último elemento
      if (e.key === 'End') {
        e.preventDefault();
        focusableElements[focusableElements.length - 1]?.focus();
      }
    });
  });
};

/**
 * Inicialización de todas las mejoras
 */
const inicializarMejoras = () => {
  console.log('🚀 Inicializando mejoras del dashboard...');

  // Actualizar progreso después de la animación inicial
  setTimeout(actualizarProgresoDelDia, 1500);

  // Animar círculo de rendimiento
  setTimeout(animarCirculoRendimiento, 800);

  // Configurar accesos rápidos interactivos
  inicializarAccesosRapidos();

  // Configurar notificaciones
  inicializarNotificaciones();

  // Agregar tooltips
  agregarTooltipsKPIs();

  // Mejorar accesibilidad
  mejorarAccesibilidadTeclado();

  // Actualizar countdown cada minuto
  actualizarCountdownProximaCita();
  setInterval(actualizarCountdownProximaCita, 60000);

  // Configurar auto-actualización (opcional, comentar si no se desea)
  // configurarAutoActualizacion();

  console.log('✅ Dashboard mejorado iniciado correctamente');
};

// Ejecutar mejoras después de la inicialización principal
document.addEventListener('DOMContentLoaded', () => {
  // Esperar a que termine la inicialización original
  setTimeout(inicializarMejoras, 1000);
});

// Limpiar animaciones al salir (para mejor rendimiento)
window.addEventListener('beforeunload', () => {
  document.querySelectorAll('[style*="animation"]').forEach(el => {
    el.style.animation = 'none';
  });
});
