(() => {
  const init = () => document.querySelectorAll('[data-quick-access], .acceso-card').forEach((element) => {
    if (element.dataset.quickAccessReady === 'true') return;
    element.dataset.quickAccessReady = 'true';
    element.addEventListener('click', (event) => {
      const ripple = document.createElement('span');
      ripple.className = 'quick-access-ripple';
      ripple.setAttribute('aria-hidden', 'true');
      const rect = element.getBoundingClientRect();
      ripple.style.left = `${event.clientX - rect.left}px`;
      ripple.style.top = `${event.clientY - rect.top}px`;
      element.appendChild(ripple);
      window.setTimeout(() => ripple.remove(), 500);
    });
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
