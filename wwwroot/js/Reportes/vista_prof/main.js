(function () {
  'use strict';

  const activeTriggers = new WeakMap();

  function openDialog(dialogId, trigger) {
    const dialog = document.getElementById(dialogId);
    if (!(dialog instanceof HTMLDialogElement) || dialog.open) return;

    activeTriggers.set(dialog, trigger);
    dialog.showModal();
    const closeButton = dialog.querySelector('[data-dialog-close]');
    if (closeButton instanceof HTMLElement) closeButton.focus();
  }

  document.querySelectorAll('[data-dialog-open]').forEach(trigger => {
    trigger.addEventListener('click', () => {
      const dialogId = trigger.getAttribute('data-dialog-open');
      if (dialogId) openDialog(dialogId, trigger);
    });
  });

  document.querySelectorAll('[data-dialog-close]').forEach(button => {
    button.addEventListener('click', () => {
      const dialog = button.closest('dialog');
      if (dialog instanceof HTMLDialogElement && dialog.open) dialog.close();
    });
  });

  document.querySelectorAll('dialog').forEach(dialog => {
    dialog.addEventListener('close', () => {
      const trigger = activeTriggers.get(dialog);
      if (trigger instanceof HTMLElement && trigger.isConnected) trigger.focus();
      activeTriggers.delete(dialog);
    });
  });

  document.querySelectorAll('[data-appointment-name]').forEach(trigger => {
    trigger.addEventListener('click', () => {
      const estadoVisible = trigger.closest('tr')?.querySelector('.status')?.textContent?.trim();
      const values = {
        detalleCitaPaciente: trigger.getAttribute('data-appointment-name'),
        detalleCitaServicio: trigger.getAttribute('data-appointment-service'),
        detalleCitaHora: trigger.getAttribute('data-appointment-time'),
        detalleCitaEstado: estadoVisible || trigger.getAttribute('data-appointment-status')
      };

      Object.entries(values).forEach(([id, value]) => {
        const element = document.getElementById(id);
        if (element) element.textContent = value || '';
      });

      openDialog('modalDetalleCita', trigger);
    });
  });

  document.querySelectorAll('[data-treatment-name]').forEach(trigger => {
    trigger.addEventListener('click', () => {
      const nombre = document.getElementById('detalleTratNombre');
      const cantidad = document.getElementById('detalleTratCantidad');
      const porcentaje = document.getElementById('detalleTratPorcentaje');

      if (nombre) nombre.textContent = trigger.getAttribute('data-treatment-name') || '';
      if (cantidad) cantidad.textContent = trigger.getAttribute('data-treatment-count') || '0';
      if (porcentaje) porcentaje.textContent = `${trigger.getAttribute('data-treatment-percent') || '0'}%`;

      openDialog('modalDetalleTratamiento', trigger);
    });
  });

  const printButton = document.getElementById('btnDescargarPdf');
  if (printButton) printButton.addEventListener('click', () => window.print());
})();
