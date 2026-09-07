/* =============================================
   SmileTrack — Reportes Operativos (Recepción) main.js
   (Misma estructura de interacción que vista_admin)
   ============================================= */

(function () {
  'use strict';

  /* ---------- Sidebar toggle (mobile) ---------- */
  const sidebar = document.getElementById('sidebar');
  const hamburger = document.getElementById('hamburger');
  const overlay = document.getElementById('overlay');

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

  /* ---------- Collapse nav groups ---------- */
  document.querySelectorAll('.nav-group').forEach(group => {
    const header = group.querySelector('.nav-group-header');
    const items = group.querySelector('.nav-group-items');
    const arrow = header?.querySelector('.nav-arrow');
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

  /* ---------- Table row click highlight ---------- */
  const rows = document.querySelectorAll('.data-table tbody tr');
  rows.forEach(row => {
    row.addEventListener('click', () => {
      rows.forEach(r => r.style.background = '');
      row.style.background = 'var(--primary-light)';
    });
  });

  /* ---------- Button press micro-interaction ---------- */
  document.querySelectorAll('.btn-primary, .btn-secondary, .btn-sm').forEach(btn => {
    btn.addEventListener('mousedown', () => btn.style.transform = 'scale(0.96)');
    btn.addEventListener('mouseup', () => btn.style.transform = '');
    btn.addEventListener('mouseleave', () => btn.style.transform = '');
  });
  /* ---- Modal helpers ---- */
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

  document.querySelectorAll('[data-modal-close]').forEach(btn => {
    btn.addEventListener('click', () => closeModal(btn.getAttribute('data-modal-close')));
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

  /* ---- Crear Cita ---- */
  document.querySelectorAll('.js-crear-cita').forEach(btn => {
    btn.addEventListener('click', () => openModal('modalCrearCita'));
  });

  /* ---- Reprogramar ---- */
  document.querySelectorAll('.js-reprogramar').forEach(btn => {
    btn.addEventListener('click', () => {
      document.getElementById('rpIdCita').value = btn.dataset.id;
      document.getElementById('rpIdPaciente').value = btn.dataset.paciente;
      document.getElementById('rpIdProfesional').value = btn.dataset.profesional || '';
      document.getElementById('rpIdServicio').value = btn.dataset.servicio || '';
      document.getElementById('reprogramarDesc').textContent =
        `${btn.dataset.profesionalNombre} · ${btn.dataset.servicioNombre}`;
      openModal('modalReprogramar');
    });
  });

  /* ---- Cancelar ---- */
  document.querySelectorAll('.js-cancelar').forEach(btn => {
    btn.addEventListener('click', () => {
      document.getElementById('cnIdCita').value = btn.dataset.id;
      document.getElementById('cancelarDesc').textContent =
        `¿Cancelar la cita de las ${btn.dataset.hora} con ${btn.dataset.profesionalNombre} (${btn.dataset.servicioNombre})? Esta acción no se puede deshacer.`;
      openModal('modalCancelar');
    });
  });

  /* ---- Editar paciente ---- */
  document.querySelectorAll('.js-editar-paciente').forEach(btn => {
    btn.addEventListener('click', () => {
      document.getElementById('epNombre').textContent = btn.dataset.nombre;
      document.getElementById('epContacto').textContent = btn.dataset.contacto;
      openModal('modalEditarPaciente');
    });
  });
  
  /* ---- Conciliar turno (solo confirmación visual, no persiste en BD) ---- */
document.querySelectorAll('.js-conciliar').forEach(btn => {
  btn.addEventListener('click', () => openModal('modalConciliar'));
});

const btnConfirmarConciliar = document.getElementById('btnConfirmarConciliar');
if (btnConfirmarConciliar) {
  btnConfirmarConciliar.addEventListener('click', () => {
    closeModal('modalConciliar');

    const toast = document.getElementById('toast');
    if (toast) {
      toast.textContent = '✅ Turno conciliado correctamente.';
      toast.className = 'toast show';
      setTimeout(() => toast.classList.remove('show'), 4000);
    }

    document.querySelectorAll('.js-conciliar').forEach(btn => {
      btn.disabled = true;
      btn.textContent = 'Turno ya conciliado';
    });
  });
}
})();
