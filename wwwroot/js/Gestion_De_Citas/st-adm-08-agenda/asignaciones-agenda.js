/* SmileTrack — Filtrado de asignaciones activas en Agenda General. */
(() => {
  document.addEventListener('DOMContentLoaded', async () => {
    const form = document.getElementById('formNewAppointment');
    const service = document.getElementById('newApptService');
    const professional = document.getElementById('newApptProfessional');
    const save = document.getElementById('modalNewApptSave');
    if (!form || !service || !professional || !save) return;

    const originalProfessionals = [...professional.options].map(o => o.cloneNode(true));
    const hint = document.createElement('small');
    hint.setAttribute('aria-live', 'polite');
    hint.style.display = 'block';
    hint.style.marginTop = '5px';
    professional.insertAdjacentElement('afterend', hint);

    let assignments = null;
    let loading = false;
    const refresh = () => {
      const selectedService = service.value;
      const previous = professional.value;
      const valid = Array.isArray(assignments) && !!selectedService;
      const allowed = valid ? assignments.filter(a => String(a.idServicio) === selectedService) : [];
      const ids = new Set(allowed.map(a => String(a.idProfesional)));
      const options = originalProfessionals.filter(o => !o.value || ids.has(o.value));
      professional.replaceChildren(...options.map(o => o.cloneNode(true)));
      professional.value = ids.has(previous) ? previous : '';
      const hasEligibleProfessionals = valid && allowed.length > 0;
      professional.disabled = !hasEligibleProfessionals;
      const selected = allowed.find(a => String(a.idProfesional) === professional.value);
      save.disabled = !selected;
      if (!Array.isArray(assignments)) hint.textContent = 'No se pudieron validar las asignaciones. No es posible guardar la cita.';
      else if (!selectedService) hint.textContent = 'Selecciona un servicio para ver los profesionales habilitados.';
      else if (!allowed.length) hint.textContent = 'Este servicio no tiene profesionales habilitados. El administrador debe configurar la especialidad del servicio y asignarlo a un profesional compatible.';
      else if (selected) hint.textContent = `Precio de referencia: ${Number(selected.precioAplicado).toLocaleString('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })}${selected.precioPersonalizado ? ' (personalizado)' : ' (base)'}. No modifica facturación.`;
      else hint.textContent = `${allowed.length} profesional(es) habilitado(s) para este servicio. La disponibilidad horaria se valida al guardar.`;
    };
    const load = async () => {
      if (loading) return;
      loading = true;
      assignments = null;
      refresh();
      try {
        const response = await fetch('/gestion-de-citas/api/asignaciones-profesionales', {
          credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const data = await response.json();
        if (!Array.isArray(data)) throw new Error('Formato de respuesta inválido');
        assignments = data;
      } catch (error) {
        console.error('[SmileTrack][Agenda] No se pudieron cargar asignaciones:', error);
      } finally {
        loading = false;
        refresh();
      }
    };
    service.addEventListener('change', refresh);
    professional.addEventListener('change', refresh);
    // La consulta de disponibilidad rellena ambos campos mediante JavaScript,
    // por lo que el evento change no se dispara automáticamente.
    document.getElementById('agendaAvailabilityResults')?.addEventListener('click', event => {
      if (!event.target.closest('[data-assign-professional]')) return;
      // Ejecutar tras el manejador existente que rellena el formulario.
      queueMicrotask(refresh);
    });
    // Tras cerrar el modal, form.reset() puede restaurar opciones previas.
    form.addEventListener('reset', () => queueMicrotask(refresh));
    await load();
  });
})();
