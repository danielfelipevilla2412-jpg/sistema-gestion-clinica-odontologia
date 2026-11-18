(() => {
  const modal = document.getElementById('modalReagendarCita');
  const form = document.getElementById('formReagendarCita');
  const dateInput = document.getElementById('reagendarFecha');
  const timeInput = document.getElementById('reagendarHora');
  const info = document.getElementById('modalReagendarCitaInfo');
  const status = document.getElementById('reagendarEstado');
  const saveButton = document.getElementById('modalReagendarCitaSave');
  if (!modal || !form || !dateInput || !timeInput || !info || !status || !saveButton) return;

  let draggedAppointment = null;
  let lastFocusedElement = null;
  let bodyWasModalOpen = false;

  const openModal = (appointment, date, time) => {
    if (!appointment?.dataset?.id || appointment.dataset.reschedulable !== 'true') return;
    lastFocusedElement = document.activeElement;
    bodyWasModalOpen = document.body.classList.contains('modal-open');
    form.dataset.appointmentId = appointment.dataset.id;
    dateInput.value = date || appointment.dataset.date || '';
    timeInput.value = (time || appointment.dataset.startTime || '').slice(0, 5);
    info.textContent = `Cita de ${appointment.dataset.patientName || 'paciente'} · ${appointment.dataset.serviceName || 'consulta'}`;
    status.textContent = '';
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    document.body.classList.add('modal-open');
    dateInput.focus();
  };

  const closeModal = () => {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    if (!bodyWasModalOpen) document.body.classList.remove('modal-open');
    status.textContent = '';
    if (lastFocusedElement instanceof HTMLElement) lastFocusedElement.focus();
    lastFocusedElement = null;
    bodyWasModalOpen = false;
  };

  document.addEventListener('click', event => {
    const button = event.target.closest('#modalApptReagendar');
    if (!button) return;
    event.preventDefault();
    event.stopPropagation();
    openModal(button, button.dataset.date, button.dataset.startTime);
  }, true);

  document.addEventListener('dragstart', event => {
    const appointment = event.target.closest('.appointment[draggable="true"][data-reschedulable="true"]');
    if (!appointment || !event.dataTransfer) return;
    draggedAppointment = appointment;
    appointment.classList.add('dragging');
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', appointment.dataset.id);
  });

  document.addEventListener('dragend', () => {
    draggedAppointment?.classList.remove('dragging');
    draggedAppointment = null;
    document.querySelectorAll('.calendar-day.drag-over').forEach(day => day.classList.remove('drag-over'));
  });

  document.addEventListener('dragover', event => {
    const day = event.target.closest('.calendar-day[data-date]:not(.closed)');
    if (!day || !draggedAppointment) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
    day.classList.add('drag-over');
  });

  document.addEventListener('dragleave', event => {
    const day = event.target.closest('.calendar-day.drag-over');
    if (day && !day.contains(event.relatedTarget)) day.classList.remove('drag-over');
  });

  document.addEventListener('drop', event => {
    const day = event.target.closest('.calendar-day[data-date]:not(.closed)');
    if (!day || !draggedAppointment) return;
    event.preventDefault();
    day.classList.remove('drag-over');
    openModal(draggedAppointment, day.dataset.date, draggedAppointment.dataset.startTime);
  });

  document.getElementById('modalReagendarCitaClose')?.addEventListener('click', closeModal);
  document.getElementById('modalReagendarCitaCancel')?.addEventListener('click', closeModal);
  modal.addEventListener('click', event => {
    if (event.target === modal) closeModal();
  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && modal.classList.contains('open')) {
      event.preventDefault();
      event.stopImmediatePropagation();
      closeModal();
    }
  });

  form.addEventListener('submit', async event => {
    event.preventDefault();
    const appointmentId = form.dataset.appointmentId;
    if (!appointmentId || !dateInput.value || !timeInput.value) return;

    saveButton.disabled = true;
    status.textContent = 'Validando disponibilidad…';
    try {
      const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
      const response = await fetch(`/api/citas/${encodeURIComponent(appointmentId)}/reagendar`, {
        method: 'PUT',
        credentials: 'same-origin',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
          'X-CSRF-TOKEN': token
        },
        body: JSON.stringify({ fechaHora: `${dateInput.value}T${timeInput.value}:00` })
      });

      let result = null;
      try {
        result = await response.json();
      } catch {
        result = null;
      }

      if (!response.ok || result?.success !== true) {
        throw new Error(result?.message || `No se pudo reagendar la cita (${response.status}).`);
      }

      status.textContent = result.message || 'Cita reagendada correctamente.';
      window.ToastService?.success(status.textContent);
      window.location.reload();
    } catch (error) {
      status.textContent = error.message || 'No se pudo validar la nueva fecha y hora.';
      window.ToastService?.error(status.textContent);
    } finally {
      saveButton.disabled = false;
    }
  });
})();
