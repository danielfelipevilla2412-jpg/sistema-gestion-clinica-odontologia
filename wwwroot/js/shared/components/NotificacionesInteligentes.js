(() => {
  const init = () => document.querySelectorAll('[data-notification-dismiss], .notification-dismiss').forEach((button) => {
    if (button.dataset.dismissReady === 'true') return;
    button.dataset.dismissReady = 'true';
    button.addEventListener('click', () => {
      const item = button.closest('[data-notification-item]') || button.closest('.notification-item') || button.closest('li') || button.parentElement;
      if (item) item.remove();
    });
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
