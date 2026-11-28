(() => {
  'use strict';

  const root = document.documentElement;
  const storageKey = 'smiletrack.theme';
  const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
  let preference;

  const isTheme = value => value === 'dark' || value === 'light';

  try {
    const saved = localStorage.getItem(storageKey);
    if (isTheme(saved)) preference = saved;
  } catch {
    // The switch still works when browser storage is blocked.
  }

  function updateControls() {
    const dark = root.dataset.stTheme === 'dark';
    const label = root.lang.startsWith('en') ? 'Dark mode' : 'Modo oscuro';
    document.querySelectorAll('[data-theme-toggle]').forEach(button => {
      const state = button.getAttribute('role') === 'switch' ? 'aria-checked' : 'aria-pressed';
      button.setAttribute(state, String(dark));
      button.setAttribute('aria-label', label);
      button.title = label;
      button.classList.toggle('active', dark);
      button.hidden = false;
      const icon = button.querySelector('[data-theme-icon]');
      if (icon) icon.textContent = dark ? '☀' : '☾';
    });
  }

  function applyTheme() {
    root.dataset.stTheme = preference ?? (systemTheme.matches ? 'dark' : 'light');
    updateControls();
  }

  document.addEventListener('click', event => {
    if (!event.target.closest?.('[data-theme-toggle]')) return;
    preference = root.dataset.stTheme === 'dark' ? 'light' : 'dark';
    try {
      localStorage.setItem(storageKey, preference);
    } catch {
      // Keep the selection for the current page.
    }
    applyTheme();
  });

  systemTheme.addEventListener('change', applyTheme);
  window.addEventListener('storage', event => {
    if (event.key !== storageKey && event.key !== null) return;
    preference = isTheme(event.newValue) ? event.newValue : undefined;
    applyTheme();
  });
  document.addEventListener('i18n:change', updateControls);
  document.addEventListener('DOMContentLoaded', updateControls, { once: true });
  applyTheme();
})();

