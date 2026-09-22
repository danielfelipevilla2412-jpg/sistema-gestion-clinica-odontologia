(function () {
  'use strict';

  function initFilterDropdowns() {
    const dropdowns = document.querySelectorAll('.dropdown-wrap');

    dropdowns.forEach((wrapper) => {
      const btn = wrapper.querySelector('.filter-btn');
      const menu = wrapper.querySelector('.dropdown-menu');

      if (!btn || !menu) return;

      const searchInput = menu.querySelector('.dropdown-search');
      const items = menu.querySelectorAll('.dd-item');

      const closeMenu = () => {
        menu.classList.remove('open');
        btn.setAttribute('aria-expanded', 'false');
      };

      btn.addEventListener('click', function (e) {
        e.stopPropagation();
        const isOpen = menu.classList.contains('open');

        document.querySelectorAll('.dropdown-menu.open').forEach((m) => {
          if (m !== menu) {
            m.classList.remove('open');
            const otherBtn = m.previousElementSibling;
            if (otherBtn) otherBtn.setAttribute('aria-expanded', 'false');
          }
        });

        menu.classList.toggle('open', !isOpen);
        btn.setAttribute('aria-expanded', String(!isOpen));

        if (!isOpen && searchInput) {
          setTimeout(() => searchInput.focus(), 100);
        }
      });

      items.forEach((item) => {
        item.addEventListener('click', function () {
          const value = this.dataset.value || '';
          const text = this.textContent.trim();

          const btnText = btn.querySelector('span:first-child');
          if (btnText) btnText.textContent = text;

          items.forEach((i) => i.classList.remove('active'));
          this.classList.add('active');
          btn.dataset.value = value;

          const event = new CustomEvent('dropdown-change', {
            detail: { value, text },
            bubbles: true,
          });
          btn.dispatchEvent(event);

          closeMenu();
        });

        item.addEventListener('keydown', (e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            item.click();
          }
        });
      });

      if (searchInput) {
        searchInput.addEventListener('input', function (e) {
          const query = e.target.value.toLowerCase();

          items.forEach((item) => {
            const searchText = (item.dataset.search || item.textContent).toLowerCase();
            item.style.display = searchText.includes(query) ? '' : 'none';
          });
        });

        searchInput.addEventListener('click', (e) => e.stopPropagation());
      }

      btn.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          btn.click();
        }
      });
    });

    document.addEventListener('click', function () {
      document.querySelectorAll('.dropdown-menu.open').forEach((m) => {
        const prev = m.previousElementSibling;
        if (prev) prev.setAttribute('aria-expanded', 'false');
        m.classList.remove('open');
      });
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        document.querySelectorAll('.dropdown-menu.open').forEach((m) => {
          const prev = m.previousElementSibling;
          if (prev) prev.setAttribute('aria-expanded', 'false');
          m.classList.remove('open');
          prev?.focus();
        });
      }
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initFilterDropdowns);
  } else {
    initFilterDropdowns();
  }

  window.SmileTrack = window.SmileTrack || {};
  window.SmileTrack.initFilterDropdowns = initFilterDropdowns;
})();
