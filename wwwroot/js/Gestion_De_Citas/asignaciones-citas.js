/* SmileTrack: filtra profesionales/servicios según asignaciones activas. */
(() => {
  const pairs = [
    ['idServicioCita', 'idProfesionalCita'],
    ['newService', 'newDoctor'],
    ['editService', 'editDoctor']
  ];
  async function initialize() {
    const response = await window.SmileTrack.utils.fetchWithTimeout('/gestion-de-citas/api/asignaciones-profesionales', {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' }
    });
    if (!response.ok) throw new Error(`No se pudo consultar las asignaciones (${response.status}).`);
    const assignments = await response.json();
    for (const [serviceId, professionalId] of pairs) {
      const service = document.getElementById(serviceId);
      const professional = document.getElementById(professionalId);
      if (!service || !professional) continue;
      const originalServices = [...service.options].map(option => option.cloneNode(true));
      const originalProfessionals = [...professional.options].map(option => option.cloneNode(true));
      const hint = document.createElement('small');
      hint.style.display = 'block';
      hint.style.marginTop = '5px';
      hint.setAttribute('aria-live', 'polite');
      professional.insertAdjacentElement('afterend', hint);
      let updating = false;
      function refresh(changed) {
        if (updating) return;
        updating = true;
        const serviceValue = service.value;
        const professionalValue = professional.value;
        const allowed = (sid, pid) => assignments.some(a => String(a.idServicio) === sid && String(a.idProfesional) === pid);
        const availableServices = originalServices.filter(o => !o.value || !professionalValue || allowed(o.value, professionalValue));
        const availableProfessionals = originalProfessionals.filter(o => !o.value || !serviceValue || allowed(serviceValue, o.value));
        function replace(select, options, previous) {
          select.replaceChildren(...options.map(o => o.cloneNode(true)));
          select.value = options.some(o => o.value === previous) ? previous : '';
        }
        // Conservar la selección del campo que el usuario acaba de cambiar.
        if (changed === 'service') {
          replace(professional, availableProfessionals, professionalValue);
          replace(service, originalServices.filter(o => !o.value || !professional.value || allowed(o.value, professional.value)), serviceValue);
        } else {
          replace(service, availableServices, serviceValue);
          replace(professional, originalProfessionals.filter(o => !o.value || !service.value || allowed(service.value, o.value)), professionalValue);
        }
        const selected = assignments.find(a => String(a.idServicio) === service.value && String(a.idProfesional) === professional.value);
        hint.textContent = selected
          ? `Precio de referencia: ${Number(selected.precioAplicado).toLocaleString('es-CO', {style:'currency',currency:'COP',maximumFractionDigits:0})}${selected.precioPersonalizado ? ' (personalizado)' : ' (base)'}. No modifica facturación.`
          : 'Seleccione servicio y profesional. Solo se permiten asignaciones activas y compatibles.';
        updating = false;
      }
      service.addEventListener('change', () => refresh('service'));
      professional.addEventListener('change', () => refresh('professional'));
      refresh(service.value ? 'service' : 'professional');
    }
  }
  document.addEventListener('DOMContentLoaded', () => {
    initialize().catch(error => {
      console.error('[SmileTrack] Integración de asignaciones:', error);
      // En caso de error, no permitir agendar con un catálogo sin validar.
      for (const [sid, pid] of pairs) {
        for (const id of [sid, pid]) {
          const select = document.getElementById(id);
          if (select) { select.disabled = true; select.title = 'No se pudieron validar las asignaciones. Recargue la página.'; }
        }
      }
    });
  });
})();
