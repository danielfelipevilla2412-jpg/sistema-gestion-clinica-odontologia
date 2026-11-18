/* =============================================
/* ============================================
   SmileTrack — Reportes app.js
   (Misma estructura de interacción que otros módulos)
   ============================================= */

(function () {
  'use strict';

  const translate = key => window.I18N?.t(key) || key;

  function updateLocalizedContent(lang) {
    const locale = lang === 'en' ? 'en-US' : 'es-CO';
    document.querySelectorAll('[data-report-date]').forEach(dateElement => {
      const timestamp = Date.parse(dateElement.dateTime);
      if (Number.isNaN(timestamp)) return;

      dateElement.textContent = new Intl.DateTimeFormat(locale, {
        day: 'numeric',
        month: 'short',
        year: 'numeric'
      }).format(timestamp);
    });

    document.title = `${translate('nav.group_reportes_analitica')} — SmileTrack`;
  }

  function initializeLocalizedContent() {
    updateLocalizedContent(window.I18N.lang);
    window.I18N.on('change', ({ lang }) => updateLocalizedContent(lang));
  }
  if (window.I18N) initializeLocalizedContent();
  else window.addEventListener('load', initializeLocalizedContent, { once: true });
  /* ---------- Bar chart hover tooltip ---------- */
  const tooltip = document.createElement('div');
  tooltip.style.cssText = 'position:fixed;background:#1e293b;color:#fff;padding:6px 12px;border-radius:6px;font-size:12px;pointer-events:none;opacity:0;transition:opacity .15s;z-index:999;white-space:nowrap;box-shadow:0 4px 12px rgba(0,0,0,.2)';
  document.body.appendChild(tooltip);

  document.querySelectorAll('.bar-group').forEach(group => {
    const curr = group.dataset.curr;
    const prev = group.dataset.prev;

    group.addEventListener('mouseenter', () => {
      const label = group.querySelector('.bar-group__label')?.textContent || '';
      tooltip.textContent = `${label} · ${translate('reportesadmin_this_year')}: ${curr} ${translate('reportesadmin_appointments')} · ${translate('reportesadmin_last_year')}: ${prev} ${translate('reportesadmin_appointments')}`;
      tooltip.style.opacity = '1';
    });
    group.addEventListener('mousemove', e => {
      tooltip.style.left = `${e.clientX + 14}px`;
      tooltip.style.top  = `${e.clientY - 40}px`;
    });
    group.addEventListener('mouseleave', () => {
      tooltip.style.opacity = '0';
    });
  });

  /* ---------- Reports table: category filter ---------- */
  const filterChips = document.querySelectorAll('.chip-btn');
  const tableWrap   = document.getElementById('tableWrap');
  const emptyState  = document.getElementById('tableEmptyState');

  function getReportRows() {
    return document.querySelectorAll('#reportsTableBody tr');
  }

  function applyFilter(category) {
    let visible = 0;
    getReportRows().forEach(row => {
      const matches = category === 'todos' || row.dataset.category === category;
      row.style.display = matches ? '' : 'none';
      if (matches) visible++;
    });

    const isEmpty = visible === 0;
    if (tableWrap) tableWrap.style.display = isEmpty ? 'none' : '';
    if (emptyState) emptyState.hidden = !isEmpty;
  }

  filterChips.forEach(chip => {
    chip.addEventListener('click', function () {
      filterChips.forEach(c => c.classList.remove('chip-btn--active'));
      this.classList.add('chip-btn--active');
      applyFilter(this.dataset.category);
    });
  });
  applyFilter(document.querySelector('.chip-btn--active')?.dataset.category || 'todos');

  /* ---------- Button press micro-interaction ---------- */
  document.querySelectorAll('.btn-primary, .btn-secondary').forEach(btn => {
    btn.addEventListener('mousedown', () => btn.style.transform = 'scale(0.96)');
    btn.addEventListener('mouseup',   () => btn.style.transform = '');
    btn.addEventListener('mouseleave',() => btn.style.transform = '');
  });

  /* ---------- Modal helpers ---------- */
  const modalMap = {
    newReportBtn: 'modalNewReport',
    btnViewAllReports: 'modalViewAllReports',
    btnPDF: 'modalDownloadPDF',
    btnCSV: 'modalExportCSV'
  };
  const modalTriggers = new Map();
  let previousBodyOverflow = '';

  function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modalTriggers.set(modalId, document.activeElement);
    if (!document.querySelector('.modal-overlay.open')) {
      previousBodyOverflow = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
    }
    modal.removeAttribute('hidden');
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    const focusable = modal.querySelector('button, [href], input, select, textarea');
    if (focusable) focusable.focus();
  }

  function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('hidden', '');
    if (!document.querySelector('.modal-overlay.open')) {
      document.body.style.overflow = previousBodyOverflow;
    }
    const trigger = modalTriggers.get(modalId);
    modalTriggers.delete(modalId);
    if (trigger instanceof HTMLElement && trigger.isConnected) trigger.focus();
  }

  Object.entries(modalMap).forEach(([buttonId, modalId]) => {
    const button = document.getElementById(buttonId);
    if (!button) return;
    button.addEventListener('click', () => openModal(modalId));
  });

  document.querySelectorAll('[data-modal-close]').forEach(closeBtn => {
    closeBtn.addEventListener('click', () => {
      const target = closeBtn.getAttribute('data-modal-close');
      if (target) closeModal(target);
    });
  });

  document.querySelectorAll('.modal-overlay').forEach(modal => {
    modal.addEventListener('click', event => {
      if (event.target === modal) {
        closeModal(modal.id);
      }
    });
  });

  document.getElementById('formNewReport')?.addEventListener('submit', async event => {
    event.preventDefault();
    const form = event.currentTarget;
    if (!(form instanceof HTMLFormElement) || !form.reportValidity()) return;

    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const headers = { Accept: 'application/json' };
    if (token) headers['X-CSRF-TOKEN'] = token;

    try {
      const response = await fetch(form.action, {
        method: 'POST',
        headers,
        body: new FormData(form),
        credentials: 'same-origin'
      });
      const payload = await response.json();
      if (!response.ok) {
        const detail = payload.detail || Object.values(payload.errors || {}).flat().join(' ') || 'No se pudo generar el reporte.';
        throw new Error(detail);
      }

      closeModal('modalNewReport');
      window.ToastService?.success(
        translate('reportesadmin_toast_success_title'),
        translate('reportesadmin_toast_success')
      );
      window.location.reload();
    } catch (error) {
      window.ToastService?.error(
        translate('reportesadmin_toast_error_title'),
        error instanceof Error ? error.message : translate('reportesadmin_toast_error')
      );
    }
  });

  document.getElementById('emptyStateNewReportBtn')?.addEventListener('click', () => openModal('modalNewReport'));

  document.getElementById('btnViewAllProcedures')?.addEventListener('click', () => openModal('modalProcedimientos'));

  document.getElementById('confirmPdfDownload')?.addEventListener('click', () => {
    const query = new URLSearchParams({
      formato: 'pdf',
      incluirGraficos: String(document.getElementById('includeCharts')?.checked ?? true),
      incluirResumen: String(document.getElementById('includeSummary')?.checked ?? true)
    });
    closeModal('modalDownloadPDF');
    window.location.href = `/reportes/exportar?${query.toString()}`;
  });

  document.getElementById('confirmCsvExport')?.addEventListener('click', () => {
    const query = new URLSearchParams({
      formato: 'csv',
      todos: String(document.getElementById('csvIncludeAll')?.checked ?? true),
      encabezados: String(document.getElementById('csvIncludeHeaders')?.checked ?? true)
    });
    closeModal('modalExportCSV');
    window.location.href = `/reportes/exportar?${query.toString()}`;
  });

  document.addEventListener('keydown', event => {
    const openModal = document.querySelector('.modal-overlay.open');
    if (!openModal) return;

    if (event.key === 'Escape') {
      closeModal(openModal.id);
      return;
    }

    if (event.key === 'Tab') {
      const focusable = Array.from(openModal.querySelectorAll(
        'button:not(:disabled), [href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'
      )).filter(element => element.getAttribute('aria-hidden') !== 'true');
      if (focusable.length === 0) return;

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }
  });

})();
