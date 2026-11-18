(() => {
  const apiBase = '/api/profesionales';
  const professionalSelect = document.getElementById('absenceProfessional');
  const form = document.getElementById('absenceForm');
  const rows = document.getElementById('absenceRows');
  const feedback = document.getElementById('absenceFeedback');
  const saveButton = document.getElementById('absenceSaveButton');
  const cancelEditButton = document.getElementById('absenceCancelEdit');

  const csrfToken = () =>
    document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

  const apiRequest = async (url, options = {}) => {
    const headers = new Headers(options.headers || {});
    headers.set('Accept', 'application/json');
    if (options.body) headers.set('Content-Type', 'application/json');
    if (options.method && options.method !== 'GET') {
      const token = csrfToken();
      if (!token) throw new Error('No se encontró el token de seguridad para guardar la ausencia.');
      headers.set('X-CSRF-TOKEN', token);
    }

    const response = await fetch(url, {
      ...options,
      headers,
      credentials: 'same-origin'
    });
    const payload = await response.json();
    if (!response.ok || payload?.success === false) {
      throw new Error(payload?.message || `La solicitud falló (${response.status}).`);
    }
    return payload;
  };

  const setFeedback = (message, isError = false) => {
    feedback.textContent = message;
    feedback.setAttribute('role', isError ? 'alert' : 'status');
  };

  const resetForm = () => {
    document.getElementById('absenceId').value = '';
    document.getElementById('absenceTipo').value = 'vacaciones';
    document.getElementById('absenceFechaInicio').value = '';
    document.getElementById('absenceFechaFin').value = '';
    document.getElementById('absenceObservaciones').value = '';
    saveButton.textContent = 'Registrar ausencia';
    cancelEditButton.hidden = true;
  };

  const createCell = (text) => {
    const cell = document.createElement('td');
    cell.textContent = text || '—';
    return cell;
  };

  const renderAbsences = (items) => {
    rows.replaceChildren();
    if (!items.length) {
      const row = document.createElement('tr');
      const cell = createCell('No hay ausencias registradas para este profesional.');
      cell.colSpan = 6;
      row.appendChild(cell);
      rows.appendChild(row);
      return;
    }

    for (const item of items) {
      const row = document.createElement('tr');
      row.append(
        createCell(professionalSelect.selectedOptions[0]?.textContent),
        createCell(item.tipo),
        createCell(item.fechaInicio),
        createCell(item.fechaFin),
        createCell(item.observaciones)
      );

      const actions = document.createElement('td');
      const edit = document.createElement('button');
      edit.type = 'button';
      edit.className = 'btn-secondary';
      edit.textContent = 'Editar';
      edit.dataset.action = 'edit';
      edit.dataset.id = String(item.idAusencia);
      const remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'btn-danger';
      remove.textContent = 'Eliminar';
      remove.dataset.action = 'delete';
      remove.dataset.id = String(item.idAusencia);
      actions.append(edit, remove);
      row.appendChild(actions);
      rows.appendChild(row);
    }
  };

  const loadAbsences = async () => {
    const professionalId = professionalSelect.value;
    resetForm();
    if (!professionalId) {
      renderAbsences([]);
      setFeedback('');
      return;
    }

    rows.replaceChildren();
    const loadingRow = document.createElement('tr');
    const loadingCell = createCell('Cargando ausencias…');
    loadingCell.colSpan = 6;
    loadingRow.appendChild(loadingCell);
    rows.appendChild(loadingRow);

    try {
      const payload = await apiRequest(`${apiBase}/${encodeURIComponent(professionalId)}/ausencias`);
      renderAbsences(Array.isArray(payload.data) ? payload.data : []);
      setFeedback('');
    } catch (error) {
      renderAbsences([]);
      setFeedback(`No se pudieron cargar las ausencias: ${error.message}`, true);
    }
  };

  const previewConflicts = async (professionalId, payload) => {
    const query = new URLSearchParams({
      fechaInicio: payload.fechaInicio,
      fechaFin: payload.fechaFin
    });
    return apiRequest(`${apiBase}/${encodeURIComponent(professionalId)}/ausencias/preview?${query}`);
  };

  const confirmAffectedAppointments = (appointments) => {
    const details = appointments.map((appointment) =>
      `${appointment.fechaHora} — ${appointment.paciente} (${appointment.servicio})`
    ).join('\n');
    return window.confirm(
      `Hay ${appointments.length} cita(s) dentro del período. Si continúas, el sistema intentará reasignarlas y cancelará las que no pueda reasignar.\n\n${details}\n\n¿Deseas continuar?`
    );
  };

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const professionalId = professionalSelect.value;
    if (!professionalId) {
      setFeedback('Selecciona un profesional.', true);
      return;
    }

    const absenceId = document.getElementById('absenceId').value;
    const payload = {
      tipo: document.getElementById('absenceTipo').value,
      fechaInicio: document.getElementById('absenceFechaInicio').value,
      fechaFin: document.getElementById('absenceFechaFin').value,
      observaciones: document.getElementById('absenceObservaciones').value || null
    };
    if (payload.fechaFin < payload.fechaInicio) {
      setFeedback('La fecha de fin debe ser igual o posterior a la fecha de inicio.', true);
      return;
    }

    saveButton.disabled = true;
    try {
      let result;
      if (absenceId) {
        result = await apiRequest(
          `${apiBase}/${encodeURIComponent(professionalId)}/ausencias/${encodeURIComponent(absenceId)}`,
          { method: 'PUT', body: JSON.stringify(payload) }
        );
      } else {
        const preview = await previewConflicts(professionalId, payload);
        if (preview.count > 0) {
          if (!confirmAffectedAppointments(preview.data || [])) return;
          result = await apiRequest(
            `${apiBase}/${encodeURIComponent(professionalId)}/ausencias-reasignar`,
            { method: 'POST', body: JSON.stringify(payload) }
          );
        } else {
          result = await apiRequest(
            `${apiBase}/${encodeURIComponent(professionalId)}/ausencias`,
            { method: 'POST', body: JSON.stringify(payload) }
          );
        }
      }

      const successMessage = result.message || 'Ausencia guardada correctamente.';
      resetForm();
      await loadAbsences();
      setFeedback(successMessage);
    } catch (error) {
      setFeedback(`No se pudo guardar la ausencia: ${error.message}`, true);
    } finally {
      saveButton.disabled = false;
    }
  });

  rows.addEventListener('click', async (event) => {
    const button = event.target.closest('button[data-action]');
    if (!button) return;

    const professionalId = professionalSelect.value;
    const absenceId = button.dataset.id;
    if (!professionalId || !absenceId) return;

    if (button.dataset.action === 'edit') {
      try {
        const payload = await apiRequest(`${apiBase}/${encodeURIComponent(professionalId)}/ausencias`);
        const item = (payload.data || []).find((absence) => String(absence.idAusencia) === absenceId);
        if (!item) throw new Error('La ausencia seleccionada ya no está disponible.');
        document.getElementById('absenceId').value = String(item.idAusencia);
        document.getElementById('absenceTipo').value = item.tipo;
        document.getElementById('absenceFechaInicio').value = item.fechaInicio;
        document.getElementById('absenceFechaFin').value = item.fechaFin;
        document.getElementById('absenceObservaciones').value = item.observaciones || '';
        saveButton.textContent = 'Guardar cambios';
        cancelEditButton.hidden = false;
        document.getElementById('absenceFormTitle').scrollIntoView({ behavior: 'smooth' });
        setFeedback('Editando ausencia seleccionada.');
      } catch (error) {
        setFeedback(`No se pudo cargar la ausencia: ${error.message}`, true);
      }
      return;
    }

    if (!window.confirm('¿Eliminar esta ausencia? Esta acción no se puede deshacer.')) return;
    button.disabled = true;
    try {
      const result = await apiRequest(
        `${apiBase}/${encodeURIComponent(professionalId)}/ausencias/${encodeURIComponent(absenceId)}`,
        { method: 'DELETE' }
      );
      const successMessage = result.message || 'Ausencia eliminada.';
      await loadAbsences();
      setFeedback(successMessage);
    } catch (error) {
      setFeedback(`No se pudo eliminar la ausencia: ${error.message}`, true);
    } finally {
      button.disabled = false;
    }
  });

  professionalSelect.addEventListener('change', loadAbsences);
  cancelEditButton.addEventListener('click', resetForm);
  document.getElementById('absenceReload').addEventListener('click', loadAbsences);
})();
