(() => {
  const init = () => document.querySelectorAll('[data-time-status]').forEach((item) => item.setAttribute('aria-label', item.textContent.trim()));
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
