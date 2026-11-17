(() => {
  'use strict';

  const AUTO_REFRESH_INTERVAL_MS = 300_000;
  const INACTIVITY_TIMEOUT_MS = 600_000;
  const COUNTDOWN_INTERVAL_MS = 60_000;
  const dashboardCounters = {
    citasHoy: Number(document.getElementById('statCitas')?.dataset.target) || 0,
    citasAtendidas: Number(document.getElementById('statProfesionales')?.dataset.target) || 0
  };
  let lastUserActivityTimestamp = Date.now();

  const translate = (key, values) => {
    if (!window.I18N || typeof window.I18N.t !== 'function') {
      throw new Error('El servicio de traducción no está disponible.');
    }

    return window.I18N.t(key, values);
  };

  const getLocale = () => translate('odo01.date_locale');

  const formatHeaderDate = () => {
    const headerDate = document.getElementById('headerDate');
    document.title = `${translate('nav.dashboard_general')} — SmileTrack`;
    if (!headerDate) return;

    const now = new Date();
    headerDate.textContent = new Intl.DateTimeFormat(getLocale(), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric'
    }).format(now);
    headerDate.dateTime = now.toISOString().slice(0, 10);
  };

  const formatLocalizedDates = () => {
    document.querySelectorAll('.professional-dashboard time[data-date-format]').forEach(timeElement => {
      const value = new Date(timeElement.dateTime);
      if (Number.isNaN(value.getTime())) {
        console.error('[SmileTrack] Fecha no válida en el dashboard:', timeElement.dateTime);
        return;
      }

      const format = timeElement.dataset.dateFormat === 'long'
        ? { day: 'numeric', month: 'long', year: 'numeric' }
        : { day: 'numeric', month: 'short' };
      timeElement.textContent = new Intl.DateTimeFormat(getLocale(), format).format(value);
    });
  };

  const applyDynamicTranslations = () => {
    document.querySelectorAll('.professional-dashboard [data-i18n-count]').forEach(element => {
      element.textContent = translate(element.dataset.i18nCount, {
        count: element.dataset.count
      });
    });

    document.querySelectorAll('.professional-dashboard [data-status-key]').forEach(element => {
      element.setAttribute('aria-label', translate(element.dataset.statusKey, {
        count: element.dataset.count,
        percent: element.dataset.percent
      }));
    });

    const progressTrack = document.querySelector('.professional-dashboard [data-progress-aria]');
    const progressLabel = document.getElementById('dailyProgressLabel');
    if (progressTrack && progressLabel) {
      progressTrack.setAttribute('aria-label', translate('odo01.daily_progress_bar_aria', {
        percent: progressLabel.dataset.percent
      }));
      progressLabel.textContent = translate('odo01.daily_progress_label', {
        attended: progressLabel.dataset.attended,
        total: progressLabel.dataset.total,
        percent: progressLabel.dataset.percent
      });
    }
  };

  const readRevenueData = () => {
    const dataElement = document.getElementById('revenueChartData');
    if (!dataElement) {
      throw new Error('No se encontró el conjunto de datos de ingresos del dashboard.');
    }

    const data = JSON.parse(dataElement.textContent);
    if (!Array.isArray(data) || data.some(item =>
      !Number.isInteger(item.mesNumero) ||
      !Number.isInteger(item.anio) ||
      !Number.isFinite(Number(item.valor)))) {
      throw new Error('El conjunto de datos de ingresos del dashboard tiene un formato no válido.');
    }

    return data;
  };

  const renderRevenueChart = () => {
    const chart = document.getElementById('revenueChart');
    if (!chart) return;

    const dataset = readRevenueData();
    if (dataset.length === 0) {
      const emptyState = document.createElement('p');
      emptyState.className = 'chart-empty';
      emptyState.textContent = translate('odo01.revenue_empty');
      chart.replaceChildren(emptyState);
      return;
    }

    const maxRevenue = Math.max(...dataset.map(item => Number(item.valor)), 0);
    const currencyFormatter = new Intl.NumberFormat(getLocale(), {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    });
    const monthFormatter = new Intl.DateTimeFormat(getLocale(), { month: 'short' });
    const rows = dataset.map(item => {
      const revenue = Number(item.valor);
      const width = maxRevenue > 0 ? Math.round((revenue / maxRevenue) * 100) : 0;
      const monthDate = new Date(item.anio, item.mesNumero - 1, 1);
      const month = monthFormatter.format(monthDate);
      const row = document.createElement('div');
      row.className = 'chart-row';

      const monthLabel = document.createElement('span');
      monthLabel.className = 'chart-label';
      monthLabel.textContent = month;

      const barBackground = document.createElement('div');
      barBackground.className = 'chart-bar-bg';
      const bar = document.createElement('div');
      bar.className = `chart-bar-fill${revenue === maxRevenue && maxRevenue > 0 ? ' is-highest' : ''}`;
      bar.dataset.width = String(width);
      bar.setAttribute('role', 'progressbar');
      bar.setAttribute('aria-valuenow', String(width));
      bar.setAttribute('aria-valuemin', '0');
      bar.setAttribute('aria-valuemax', '100');
      bar.setAttribute('aria-label', translate('odo01.revenue_bar_aria', {
        month,
        amount: currencyFormatter.format(revenue)
      }));
      barBackground.appendChild(bar);

      const amountLabel = document.createElement('span');
      amountLabel.className = 'chart-val';
      amountLabel.textContent = currencyFormatter.format(revenue);

      row.append(monthLabel, barBackground, amountLabel);
      return row;
    });

    chart.replaceChildren(...rows);
    requestAnimationFrame(() => {
      chart.querySelectorAll('.chart-bar-fill').forEach(bar => {
        bar.style.width = `${Number(bar.dataset.width)}%`;
      });
    });
  };

  const animateBars = () => {
    document.querySelectorAll('.professional-dashboard .status-bar-fill').forEach(bar => {
      bar.style.width = `${Number(bar.dataset.width) || 0}%`;
    });
  };

  const updateNextAppointmentCountdown = () => {
    const appointmentCard = document.querySelector('.next-appt-card.urgent, .next-appt-card.upcoming');
    if (!appointmentCard) return;

    const countdown = appointmentCard.querySelector('.next-appt-countdown');
    const countdownIcon = countdown?.querySelector('.countdown-icon');
    const countdownText = countdown?.querySelector('.countdown-text');
    if (!countdown || !countdownIcon || !countdownText) return;

    const appointmentDate = new Date(appointmentCard.dataset.fechaHora);
    if (Number.isNaN(appointmentDate.getTime())) {
      console.error('[SmileTrack] Fecha de próxima cita no válida:', appointmentCard.dataset.fechaHora);
      countdown.hidden = true;
      return;
    }

    const now = new Date();
    const isToday = appointmentDate.toDateString() === now.toDateString();
    if (!isToday) {
      countdown.hidden = true;
      countdown.style.animation = '';
      appointmentCard.classList.remove('urgent');
      appointmentCard.classList.add('upcoming');
      return;
    }

    const minutes = Math.floor((appointmentDate.getTime() - now.getTime()) / 60_000);
    countdown.hidden = false;
    if (minutes <= 0) {
      countdownIcon.textContent = 'notifications_active';
      countdownText.textContent = translate('odo01.countdown_now');
      countdown.style.animation = 'pulse-urgent 1s ease-in-out infinite';
      appointmentCard.classList.add('urgent');
      appointmentCard.classList.remove('upcoming');
    } else {
      countdownIcon.textContent = 'schedule';
      countdownText.textContent = translate(
        minutes === 1 ? 'odo01.countdown_minutes_one' : 'odo01.countdown_minutes_other',
        { count: minutes }
      );
      countdown.style.animation = '';
      appointmentCard.classList.toggle('urgent', minutes <= 15);
      appointmentCard.classList.toggle('upcoming', minutes > 15);
    }
  };

  const updateDailyProgress = () => {
    const progressBar = document.getElementById('topProgressBar');
    const progressTrack = progressBar?.closest('[role="progressbar"]');
    const progressLabel = document.getElementById('dailyProgressLabel');
    if (!progressBar || !progressTrack || !progressLabel) return;

    const total = dashboardCounters.citasHoy;
    const attended = dashboardCounters.citasAtendidas;
    const percent = total > 0
      ? Math.max(0, Math.min(100, Math.round((attended / total) * 100)))
      : 0;
    progressBar.style.width = `${percent}%`;
    progressTrack.setAttribute('aria-valuenow', String(percent));
    progressLabel.dataset.attended = String(attended);
    progressLabel.dataset.total = String(total);
    progressLabel.dataset.percent = String(percent);
    applyDynamicTranslations();
  };

  const setupAutoRefreshStats = () => {
    ['mousedown', 'keydown', 'scroll', 'touchstart'].forEach(eventName => {
      document.addEventListener(eventName, () => {
        lastUserActivityTimestamp = Date.now();
      }, { passive: true });
    });

    window.setInterval(async () => {
      if (Date.now() - lastUserActivityTimestamp > INACTIVITY_TIMEOUT_MS) return;

      try {
        if (typeof window.SmileTrack?.utils?.fetchWithTimeout !== 'function') {
          throw new Error('No está disponible el cliente HTTP compartido.');
        }

        const response = await window.SmileTrack.utils.fetchWithTimeout('/api/profesionales/dashboard-stats', {
          method: 'GET',
          headers: { Accept: 'application/json' }
        });
        if (!response.ok) {
          throw new Error(`La actualización del dashboard respondió HTTP ${response.status}.`);
        }

        const data = await response.json();
        if (data.citasHoy !== undefined) {
          dashboardCounters.citasHoy = Number(data.citasHoy);
          if (!Number.isFinite(dashboardCounters.citasHoy)) {
            throw new Error('La API devolvió un total de citas no válido.');
          }
        }

        if (data.citasAtendidas !== undefined) {
          dashboardCounters.citasAtendidas = Number(data.citasAtendidas);
          if (!Number.isFinite(dashboardCounters.citasAtendidas)) {
            throw new Error('La API devolvió un total de citas atendidas no válido.');
          }

          const attendedCounter = document.getElementById('statProfesionales');
          if (attendedCounter && typeof window.animateCounter === 'function') {
            attendedCounter.removeAttribute('data-animated');
            window.animateCounter(attendedCounter, dashboardCounters.citasAtendidas);
          }
        }

        const todayCounter = document.getElementById('statCitas');
        if (data.citasHoy !== undefined && todayCounter && typeof window.animateCounter === 'function') {
          todayCounter.removeAttribute('data-animated');
          window.animateCounter(todayCounter, dashboardCounters.citasHoy);
        }
        updateDailyProgress();
      } catch (error) {
        console.error('[SmileTrack] Error actualizando dashboard:', error);
      }
    }, AUTO_REFRESH_INTERVAL_MS);
  };

  const setupDismissibleNotifications = () => {
    const notifications = document.querySelectorAll('.professional-dashboard .notif-item');
    const emptyState = document.querySelector('.professional-dashboard .notif-empty');

    notifications.forEach(button => {
      button.addEventListener('click', () => {
        button.hidden = true;
        const hasVisibleNotifications = Array.from(notifications).some(item => !item.hidden);
        if (emptyState) emptyState.hidden = hasVisibleNotifications;
      });
    });
  };

  const animatePerformanceCircle = () => {
    const scoreCircle = document.querySelector('.professional-dashboard .score-circle');
    const progressCircle = scoreCircle?.querySelector('.score-progress-circle');
    if (!scoreCircle || !progressCircle) return;

    const score = Math.max(0, Math.min(100, Number(scoreCircle.dataset.score) || 0));
    const circumference = 314;
    progressCircle.style.strokeDasharray = `0 ${circumference}`;
    requestAnimationFrame(() => {
      window.setTimeout(() => {
        progressCircle.style.strokeDasharray = `${circumference * score / 100} ${circumference}`;
      }, 300);
    });
  };

  const refreshLocalizedContent = () => {
    formatHeaderDate();
    formatLocalizedDates();
    applyDynamicTranslations();
    renderRevenueChart();
    updateNextAppointmentCountdown();
  };

  const initializeDashboard = () => {
    try {
      refreshLocalizedContent();
      animateBars();
      animatePerformanceCircle();
      updateDailyProgress();
      setupDismissibleNotifications();
      updateNextAppointmentCountdown();
      window.setInterval(updateNextAppointmentCountdown, COUNTDOWN_INTERVAL_MS);
      setupAutoRefreshStats();
      document.addEventListener('i18n:change', refreshLocalizedContent);
    } catch (error) {
      console.error('[SmileTrack] No fue posible inicializar el dashboard:', error);
    }
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeDashboard, { once: true });
  } else {
    initializeDashboard();
  }
})();
