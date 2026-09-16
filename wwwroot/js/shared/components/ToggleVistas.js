(() => {
  const init = () => document.querySelectorAll('.toggle-vistas').forEach((group) => {
    group.querySelectorAll('.toggle-vista').forEach((button) => button.addEventListener('click', () => {
      group.querySelectorAll('.toggle-vista').forEach((item) => { item.classList.remove('active'); item.setAttribute('aria-selected', 'false'); });
      button.classList.add('active'); button.setAttribute('aria-selected', 'true');
      group.dispatchEvent(new CustomEvent('viewchange', { bubbles: true, detail: { view: button.dataset.view } }));
    }));
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
