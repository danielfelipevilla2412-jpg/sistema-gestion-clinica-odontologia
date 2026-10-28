(() => {
  const init = () => {
    document.querySelectorAll('.proxima-cita-banner[data-fecha]').forEach((banner) => {
      if (banner.dataset.countdownReady === 'true') return;
      banner.dataset.countdownReady = 'true';
      const fecha = new Date(banner.dataset.fecha);
      const value = banner.querySelector('.countdown-value');
      const update = () => {
        const minutes = Math.max(0, Math.floor((fecha - new Date()) / 60000));
        if (value) value.textContent = String(minutes);
        banner.classList.toggle('urgent', minutes <= 15);
        banner.classList.toggle('upcoming', minutes > 15);
      };
      update();
      const timer = window.setInterval(update, 60000);
      window.addEventListener('pagehide', () => window.clearInterval(timer), { once: true });
    });
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
