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
    // En modo edición se muestra la lista completa de profesionales activos y se
    // conserva la preselección de la cita. El filtro por servicio solo se vuelve a
    // aplicar cuando el usuario cambia manualmente el servicio.
    let showAllProfessionals = false;

    const restoreFullList = (previous) => {
      professional.replaceChildren(...originalProfessionals.map(o => o.cloneNode(true)));
      professional.disabled = false;
      if (previous && [...professional.options].some(o => o.value === previous)) {
        professional.value = previous;
      }
      save.disabled = false;
      hint.textContent = 'Mostrando todos los profesionales activos. Selecciona un servicio para filtrar por especialidad.';
    };

    const refresh = () => {
      const selectedService = service.value;
      const previous = professional.value;
      const assignmentsLoaded = Array.isArray(assignments);

      if (showAllProfessionals) {
        restoreFullList(previous);
        return;
      }

      if (!selectedService || !assignmentsLoaded) {
        professional.replaceChildren(...originalProfessionals.map(o => o.cloneNode(true)));
        professional.value = previous;
        professional.disabled = false;
        save.disabled = true;
        hint.textContent = !assignmentsLoaded
          ? loading
            ? 'Cargando profesionales habilitados...'
            : 'No se pudieron cargar las asignaciones. Verifica que los profesionales tengan servicios asignados.'
          : 'Selecciona un servicio para filtrar los profesionales habilitados.';
        return;
      }

      const allowed = assignments.filter(a => String(a.idServicio) === selectedService);
      const ids = new Set(allowed.map(a => String(a.idProfesional)));
      const options = allowed.length
        ? originalProfessionals.filter(o => !o.value || ids.has(o.value))
        : originalProfessionals;
      professional.replaceChildren(...options.map(o => o.cloneNode(true)));
      professional.value = options.some(o => o.value === previous) ? previous : '';
      professional.disabled = false;
      const selected = allowed.find(a => String(a.idProfesional) === professional.value);
      save.disabled = !selected;
      if (!allowed.length) hint.textContent = 'No hay profesionales asignados activamente a este servicio. Puedes seleccionar uno, pero no guardar la cita hasta configurar la especialidad y la asignación del servicio.';
      else if (selected) hint.textContent = `Precio de referencia: ${Number(selected.precioAplicado).toLocaleString('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 })}${selected.precioPersonalizado ? ' (personalizado)' : ' (base)'}. No modifica facturación.`;
      else hint.textContent = `${allowed.length} profesional(es) habilitado(s) para este servicio. La disponibilidad horaria se valida al guardar.`;
    };
    const load = async () => {
      if (loading) return;
      loading = true;
      assignments = null;
      refresh();
      try {
        const response = await window.SmileTrack.utils.fetchWithTimeout('/gestion-de-citas/api/asignaciones-profesionales', {
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
    // El usuario cambia el servicio: dejamos de mostrar la lista completa y
    // volvemos a filtrar los profesionales por las asignaciones del servicio.
    service.addEventListener('change', () => {
      showAllProfessionals = false;
      if (Array.isArray(assignments)) refresh();
      else void load();
    });
    professional.addEventListener('change', refresh);
    // La consulta de disponibilidad rellena ambos campos mediante JavaScript,
    // por lo que el evento change no se dispara automáticamente.
    document.getElementById('agendaAvailabilityResults')?.addEventListener('click', event => {
      if (!event.target.closest('[data-assign-professional]')) return;
      // Ejecutar tras el manejador existente que rellena el formulario.
      showAllProfessionals = false;
      queueMicrotask(refresh);
    });
    // Cuando se edita una cita existente, los campos se rellenan por JS sin
    // disparar 'change'. Mostramos la lista completa de profesionales activos y
    // conservamos el profesional de la cita hasta que el usuario cambie el servicio.
    document.addEventListener('smiletrack:edit-appointment', () => {
      showAllProfessionals = true;
      queueMicrotask(refresh);
    });
    // Tras cerrar el modal, form.reset() puede restaurar opciones previas y
    // volvemos al comportamiento de alta (filtrar profesionales por servicio).
    form.addEventListener('reset', () => {
      showAllProfessionals = false;
      queueMicrotask(refresh);
    });
    await load();
  });
})();
