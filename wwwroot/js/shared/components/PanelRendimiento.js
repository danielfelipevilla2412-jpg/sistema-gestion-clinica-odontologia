(() => {
  const init = () => document.querySelectorAll('[data-performance]').forEach((panel) => {
    const ring = panel.querySelector('.performance-ring');
    if (ring) ring.style.setProperty('--performance', panel.dataset.performance || '0');
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
