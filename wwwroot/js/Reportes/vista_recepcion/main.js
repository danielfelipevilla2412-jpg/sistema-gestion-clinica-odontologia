/* =============================================
   SmileTrack — Reportes Operativos (Recepción) main.js
   (Misma estructura de interacción que vista_admin)
   El sidebar, el hamburger y el overlay son gestionados por shared/sidebar.js
   desde el layout compartido; aquí solo queda la interacción propia de la vista.
   ============================================= */

(function () {
  'use strict';

  const translate = key => window.I18N?.t(key) || key;

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

      window.ToastService?.success(
        translate('common.success'),
        translate('reportes.toast_conciliado')
      );

      document.querySelectorAll('.js-conciliar').forEach(btn => {
        btn.disabled = true;
        btn.textContent = translate('reportes.btn_turno_conciliado');
      });
    });
  }
})();
