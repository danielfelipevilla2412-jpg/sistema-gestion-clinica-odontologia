/*
 SmileTrack — Dashboard del Odontólogo (st-odo-01-dashboard)
 Fuente de datos: Controller + Razor. No usa datos de demostración.
*/

const safeGetElement = (id) => document.getElementById(id);

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
    container.innerHTML = `
      <p style="padding:16px;color:var(--text-muted);">
        No hay ingresos registrados en el período.
      </p>`;
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
        <span class="chart-label">${String(item.mes || '').replace(/</g, '&lt;')}</span>
        <div class="chart-bar-bg">
          <div
            class="chart-bar-fill ${colorClass}"
            data-width="${width}"
            style="width:0"
            role="progressbar"
            aria-valuenow="${width}"
            aria-valuemin="0"
            aria-valuemax="100"
            aria-label="${String(item.mes || '')}: ${currency.format(value)}"
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
