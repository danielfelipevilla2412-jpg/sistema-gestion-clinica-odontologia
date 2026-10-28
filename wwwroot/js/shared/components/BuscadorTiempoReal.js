(() => {
  const init = () => document.querySelectorAll('[data-realtime-search], .search-wrapper .search-input').forEach((input) => {
    if (input.dataset.searchReady === 'true') return;
    input.dataset.searchReady = 'true';
    let timer;
    input.addEventListener('input', () => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => {
        const term = input.value.trim().toLocaleLowerCase();
        const selector = input.dataset.targetSelector || input.dataset.target || 'tbody tr';
        document.querySelectorAll(selector).forEach((row) => {
          row.hidden = term !== '' && !row.textContent.toLocaleLowerCase().includes(term);
        });
        const count = document.getElementById(`${input.id}Count`);
        if (count) {
          const visible = Array.from(document.querySelectorAll(selector)).filter(row => !row.hidden).length;
          count.textContent = term ? `${visible} resultado(s)` : '';
        }
      }, 300);
    });
  });
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init, { once: true }); else init();
})();
