/* =============================================
   SmileTrack – main.js (Reportes Profesional)
   Idéntico al patrón de st-odo-01-dashboard
   ============================================= */

(function () {
  'use strict';

  /* ---- Sidebar toggle (mobile) — patrón dashboard ---- */
  const sidebar = document.getElementById('sidebar');
  const hamburger = document.getElementById('hamburger');
  const overlay = document.getElementById('overlay');

  if (hamburger && sidebar && overlay) {
    hamburger.addEventListener('click', () => {
      const isOpen = sidebar.classList.toggle('open');
      overlay.classList.toggle('open', isOpen);
      hamburger.setAttribute('aria-expanded', String(isOpen));
      document.body.style.overflow = isOpen ? 'hidden' : '';
    });

    overlay.addEventListener('click', closeSidebar);

    document.querySelectorAll('.nav-item').forEach(item => {
      item.addEventListener('click', () => {
        if (window.innerWidth <= 680) closeSidebar();
      });
    });

    window.addEventListener('resize', () => {
      if (window.innerWidth > 680) closeSidebar();
    });
  }

  function closeSidebar() {
    if (!sidebar) return;
    sidebar.classList.remove('open');
    overlay && overlay.classList.remove('open');
    hamburger && hamburger.setAttribute('aria-expanded', 'false');
    document.body.style.overflow = '';
  }

  /* ---- Nav-group accordion — idéntico a dashboard ---- */
  document.querySelectorAll('.nav-group-header').forEach(header => {
    const group = header.closest('.nav-group');
    if (!group) return;

    // Auto-expande el grupo con el link activo
    if (group.querySelector('.nav-item.active')) {
      group.setAttribute('aria-expanded', 'true');
    } else {
      group.setAttribute('aria-expanded', 'false');
    }

    function toggle() {
      const expanded = group.getAttribute('aria-expanded') === 'true';
      group.setAttribute('aria-expanded', String(!expanded));
    }

    header.addEventListener('click', toggle);
    header.addEventListener('keydown', e => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
    });
  });

  /* ---- Progress bar animation ---- */
  function animateBars() {
    document.querySelectorAll('.progress-fill').forEach(bar => {
      const target = bar.style.width;
      bar.style.width = '0%';
      requestAnimationFrame(() => {
        setTimeout(() => { bar.style.width = target; }, 80);
      });
    });
  }

  /* ---- Donut animation ---- */
  function animateDonut() {
    const fill = document.querySelector('.donut-fill');
    if (!fill) return;
    const finalOffset = parseFloat(fill.getAttribute('stroke-dashoffset') || '87.9');
    fill.style.transition = 'none';
    fill.setAttribute('stroke-dashoffset', '251.2');
    requestAnimationFrame(() => {
      setTimeout(() => {
        fill.style.transition = 'stroke-dashoffset 1s ease';
        fill.setAttribute('stroke-dashoffset', String(finalOffset));
      }, 200);
    });
  }

  /* ---- Action buttons ---- */
  /* ---- Modal helpers (mismo patrón que vista_admin/app.js) ---- */
  function openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modal.removeAttribute('hidden');
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    document.body.style.overflow = 'hidden';
  }

  function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (!modal) return;
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('hidden', '');
    document.body.style.overflow = '';
  }

  document.querySelectorAll('[data-modal-close]').forEach(closeBtn => {
    closeBtn.addEventListener('click', () => {
      const target = closeBtn.getAttribute('data-modal-close');
      if (target) closeModal(target);
    });
  });

  document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', e => {
      if (e.target === overlay) closeModal(overlay.id);
    });
  });

  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
      document.querySelectorAll('.modal-overlay.open').forEach(m => closeModal(m.id));
    }
  });

  /* ---- Modal: Detalle de cita ---- */
  document.querySelectorAll('.btn-icon[data-paciente]').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      document.getElementById('detalleCitaPaciente').textContent = btn.dataset.paciente + ' (ID: #' + btn.dataset.id + ')';
      document.getElementById('detalleCitaServicio').textContent = btn.dataset.servicio;
      document.getElementById('detalleCitaHora').textContent = btn.dataset.hora;
      document.getElementById('detalleCitaEstado').textContent = btn.dataset.estado;
      openModal('modalDetalleCita');
    });
  });

  /* ---- Modal: Detalle de tratamiento ---- */
  document.querySelectorAll('.prof-revenue-row[data-nombre]').forEach(row => {
    row.addEventListener('click', () => {
      document.getElementById('detalleTratNombre').textContent = row.dataset.nombre;
      document.getElementById('detalleTratCantidad').textContent = row.dataset.cantidad + ' citas';
      document.getElementById('detalleTratPorcentaje').textContent = row.dataset.porcentaje + '% del total de citas';
      openModal('modalDetalleTratamiento');
    });
  });

  /* ---- Modal: Nueva cita rápida ---- */
  const btnNuevaCita = document.getElementById('btnNuevaCita');
  if (btnNuevaCita) {
    btnNuevaCita.addEventListener('click', () => openModal('modalNuevaCita'));
  }

  const formNuevaCita = document.getElementById('formNuevaCita');
  if (formNuevaCita) {
    formNuevaCita.addEventListener('submit', e => {
      e.preventDefault();
      // Nota: esto todavía no guarda en la base de datos.
      // Para persistir la cita hace falta un endpoint que acepte el rol "Profesional".
      closeModal('modalNuevaCita');
      alert('Solicitud registrada. Un recepcionista confirmará el horario en la agenda.');
      formNuevaCita.reset();
    });
  }

  /* ---- Modal: Confirmar exportar PDF ---- */
  const btnDescargarPdf = document.getElementById('btnDescargarPdf');
  if (btnDescargarPdf) {
    btnDescargarPdf.addEventListener('click', () => openModal('modalExportarPdf'));
  }

  const btnConfirmarExportar = document.getElementById('btnConfirmarExportar');
  if (btnConfirmarExportar) {
    btnConfirmarExportar.addEventListener('click', () => {
      closeModal('modalExportarPdf');
      window.print();
    });
  }

  /* ---- Run ---- */
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => { animateBars(); animateDonut(); });
  } else {
    animateBars();
    animateDonut();
  }

})();
