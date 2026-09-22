(() => {
  const init = () => document.querySelectorAll('[data-stat-target], .stat-number[data-target]').forEach((element) => {
    if (element.dataset.statReady === 'true') return;
    element.dataset.statReady = 'true';
    const target = Number(element.dataset.statTarget || element.dataset.target || element.textContent || 0);
    element.textContent = String(Number.isFinite(target) ? target : 0);
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
