/* =============================================
   SmileTrack — Reportes app.js
   (Misma estructura de interacción que otros módulos)
   ============================================= */

(function () {
  'use strict';

  /* ---------- Sidebar toggle (mobile) ---------- */
  const sidebar   = document.getElementById('sidebar');
  const hamburger = document.getElementById('hamburger');
  const overlay   = document.getElementById('overlay');

  function openSidebar() {
    sidebar.classList.add('open');
    overlay.classList.add('open');
    hamburger.setAttribute('aria-expanded', 'true');
    document.body.style.overflow = 'hidden';
  }

  function closeSidebar() {
    sidebar.classList.remove('open');
    overlay.classList.remove('open');
    hamburger.setAttribute('aria-expanded', 'false');
    document.body.style.overflow = '';
  }

  if (hamburger) {
    hamburger.addEventListener('click', () => {
      sidebar.classList.contains('open') ? closeSidebar() : openSidebar();
    });
  }
  if (overlay) overlay.addEventListener('click', closeSidebar);

// Collapse nav groups
  document.querySelectorAll('.nav-group').forEach(group => {
    const header = group.querySelector('.nav-group-header');
    const items  = group.querySelector('.nav-group-items');
    const arrow  = header?.querySelector('.nav-arrow');
    if (!header || !items) return;

    // Estado inicial: solo el grupo que contiene el link activo queda abierto
    const hasActiveLink = items.querySelector('.nav-item.active');
    if (!hasActiveLink) {
      items.style.display = 'none';
      if (arrow) arrow.style.transform = 'rotate(-90deg)';
    }

    header.addEventListener('click', () => {
      const isHidden = items.style.display === 'none';
      items.style.display = isHidden ? '' : 'none';
      if (arrow) arrow.style.transform = isHidden ? '' : 'rotate(-90deg)';
    });
  });

  /* ---------- Bar chart hover tooltip ---------- */
  const tooltip = document.createElement('div');
  tooltip.style.cssText = 'position:fixed;background:#1e293b;color:#fff;padding:6px 12px;border-radius:6px;font-size:12px;pointer-events:none;opacity:0;transition:opacity .15s;z-index:999;white-space:nowrap;box-shadow:0 4px 12px rgba(0,0,0,.2)';
  document.body.appendChild(tooltip);

  document.querySelectorAll('.bar-group').forEach(group => {
    const curr = group.dataset.curr;
    const prev = group.dataset.prev;
    const label = group.querySelector('.bar-group__label')?.textContent || '';

    group.addEventListener('mouseenter', () => {
      tooltip.innerHTML = `<strong>${label}</strong><br>Este año: ${curr}% · Anterior: ${prev}%`;
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
  const pagination  = document.getElementById('reportsPagination');
  const reportsBody = document.getElementById('reportsTableBody');

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
    if (pagination) pagination.style.display = isEmpty ? 'none' : '';
    if (emptyState) emptyState.style.display = isEmpty ? '' : 'none';
  }

  filterChips.forEach(chip => {
    chip.addEventListener('click', function () {
      filterChips.forEach(c => c.classList.remove('chip-btn--active'));
      this.classList.add('chip-btn--active');
      applyFilter(this.dataset.category);
    });
  });

  /* ---------- New report button ---------- */
  function wireNewReportBtn(btn) {
    if (!btn) return;
    const originalText = btn.textContent;
    btn.addEventListener('click', () => {
      btn.textContent = '⏳ Generando…';
      btn.disabled = true;
      setTimeout(() => {
        btn.textContent = originalText;
        btn.disabled = false;
      }, 1800);
    });
  }
  wireNewReportBtn(document.getElementById('newReportBtn'));
  wireNewReportBtn(document.getElementById('emptyStateNewReportBtn'));

  /* ---------- Pagination ---------- */
  document.querySelectorAll('.pg-btn:not(:disabled)').forEach(btn => {
    btn.addEventListener('click', function () {
      document.querySelectorAll('.pg-btn').forEach(b => b.classList.remove('pg-btn--active'));
      this.classList.add('pg-btn--active');
    });
  });

  /* ---------- Download button feedback ---------- */
  if (reportsBody) {
    reportsBody.addEventListener('click', event => {
      const btn = event.target.closest('button.btn-icon');
      if (!btn) return;
      const original = btn.textContent;
      btn.textContent = '✅';
      setTimeout(() => { btn.textContent = original; }, 1500);
    });
  }

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

  function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modal.removeAttribute('hidden');
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    document.body.style.overflow = 'hidden';
    const focusable = modal.querySelector('button, [href], input, select, textarea');
    if (focusable) focusable.focus();
  }

  function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('hidden', '');
    document.body.style.overflow = '';
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

  function createRecentReportRow(report) {
    const row = document.createElement('tr');
    row.dataset.category = report.category.toLowerCase();
    row.innerHTML = `
      <td>
        <div class="cell-icon-name">
          <span class="cell-icon" data-color="${report.color}">${report.icon}</span>
          ${report.name}
        </div>
      </td>
      <td>${report.date}</td>
      <td><span class="badge">${report.category}</span></td>
      <td>
        <span class="status status--ready">
          <span class="status__dot"></span> ${report.status}
        </span>
      </td>
      <td>
        <form action="/reportes/descargar/${report.id}" method="post" style="display:inline">
          <button type="submit" class="btn-icon" title="Descargar">📥</button>
        </form>
      </td>
    `;
    return row;
  }

  document.getElementById('formNewReport')?.addEventListener('submit', event => {
    event.preventDefault();
    const form = event.target;
    const reportName = form.reportName.value.trim() || 'Reporte sin título';
    const reportCategory = form.reportCategory.value || 'Financiero';
    const reportPeriod = form.reportPeriod.value;
    const submitBtn = form.querySelector('button[type="submit"]');
    const generatedDate = new Date().toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' });
    const iconLetter = reportCategory.charAt(0).toUpperCase();
    const categoryLower = reportCategory.toLowerCase();
    const color = categoryLower === 'financiero' ? 'blue' :
                  categoryLower === 'pacientes' ? 'green' :
                  (categoryLower === 'logística' || categoryLower === 'logistica') ? 'red' : 'purple';
    const newReport = {
      id: Date.now(),
      name: reportName,
      category: reportCategory,
      date: generatedDate,
      status: 'Listo',
      icon: iconLetter,
      color: color
    };

    if (submitBtn) {
      submitBtn.textContent = 'Generando…';
      submitBtn.disabled = true;
    }

    setTimeout(() => {
      if (reportsBody) {
        reportsBody.appendChild(createRecentReportRow(newReport));
      }
      closeModal('modalNewReport');
      applyFilter(document.querySelector('.chip-btn.chip-btn--active')?.dataset.category || 'todos');
      if (submitBtn) {
        submitBtn.textContent = 'Crear Reporte';
        submitBtn.disabled = false;
      }
      form.reset();
      form.reportCategory.value = 'Financiero';
    }, 1200);
  });

  document.getElementById('emptyStateNewReportBtn')?.addEventListener('click', () => openModal('modalNewReport'));

  document.getElementById('btnViewAllProcedures')?.addEventListener('click', () => openModal('modalProcedimientos'));

  document.getElementById('confirmPdfDownload')?.addEventListener('click', () => {
    closeModal('modalDownloadPDF');
    alert('PDF generado correctamente.');
  });

  document.getElementById('confirmCsvExport')?.addEventListener('click', () => {
    closeModal('modalExportCSV');
    alert('CSV exportado correctamente.');
  });

})();
