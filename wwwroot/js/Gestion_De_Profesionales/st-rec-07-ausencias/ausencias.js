(() => {
  const apiBase = '/api/profesionales';
  const professionalSelect = document.getElementById('absenceProfessional');
  const form = document.getElementById('absenceForm');
  const rows = document.getElementById('absenceRows');
  const feedback = document.getElementById('absenceFeedback');
  const startDateInput = document.getElementById('absenceFechaInicio');
  const endDateInput = document.getElementById('absenceFechaFin');
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
    const payload = await response.json().catch(() => null);
    if (!response.ok || !payload || payload.success === false) {
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
    startDateInput.value = '';
    endDateInput.value = '';
    startDateInput.removeAttribute('aria-invalid');
    endDateInput.removeAttribute('aria-invalid');
    endDateInput.removeAttribute('aria-describedby');
    document.getElementById('absenceObservaciones').value = '';
    saveButton.textContent = 'Registrar ausencia';
    cancelEditButton.hidden = true;
  };

  const byId = id => document.getElementById(id);
  const dialog = byId('absenceDialog');
  const start = byId('absenceFechaInicio');
  const end = byId('absenceFechaFin');
  const editingId = byId('absenceId');
  const previewButton = byId('absencePreview');
  const impact = byId('absenceImpact');
  const confirmImpact = byId('absenceConfirmInput');
  const formFeedback = byId('absenceFormFeedback');
  const states = { curso: 'En curso', proximas: 'Próxima', historial: 'Finalizada' };
  let absences = [];
  let today = '';
  let busy = false;
  let reviewedCount = null;

  function invalidatePreview() {
    end.min = start.value;
    reviewedCount = null;
    impact.hidden = true;
    confirmImpact.checked = false;
    formFeedback.textContent = '';
  }

  function openForm(item) {
    form.reset();
    editingId.value = item?.idAusencia || '';
    professionalSelect.disabled = !!item;
    professionalSelect.value = item?.idProfesional || byId('absenceFilterProfessional').value;
    byId('absenceTipo').value = item?.tipo || 'vacaciones';
    start.value = item?.fechaInicio || '';
    end.value = item?.fechaFin || '';
    byId('absenceObservaciones').value = item?.observaciones || '';
    byId('absenceFormTitle').textContent = item ? 'Editar ausencia' : 'Nueva ausencia';
    saveButton.textContent = item ? 'Guardar cambios' : 'Registrar ausencia';
    cancelEditButton.textContent = item ? 'Cancelar edición' : 'Cerrar';
    invalidatePreview();
    dialog.showModal();
  }

  function cell(text) {
    const td = document.createElement('td');
    td.textContent = text || '—';
    return td;
  }

  function render() {
    const professional = byId('absenceFilterProfessional').value;
    const type = byId('absenceFilterType').value;
    const period = byId('absenceFilterPeriod').value;
    rows.replaceChildren();
    for (const item of absences) {
      const state = item.fechaFin < today ? 'historial' : item.fechaInicio > today ? 'proximas' : 'curso';
      if ((professional && String(item.idProfesional) !== professional) || (type && item.tipo !== type)) continue;
      if (period !== 'todas' && !(period === 'vigentes' ? state !== 'historial' : state === period)) continue;

      const row = rows.insertRow();
      const status = cell('');
      const badge = document.createElement('span');
      badge.className = 'absence-badge';
      badge.dataset.state = state;
      badge.textContent = states[state];
      status.replaceChildren(badge);
      const actions = cell('');
      actions.className = 'absence-actions';
      actions.replaceChildren();
      for (const [action, label] of [['edit', 'Editar'], ['delete', 'Eliminar']]) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = action === 'edit' ? 'btn-secondary' : 'btn-danger';
        button.dataset.action = action;
        button.dataset.id = item.idAusencia;
        button.textContent = label;
        actions.append(button);
      }
      const typeLabel = byId('absenceFilterType').querySelector(`option[value="${['vacaciones','incapacidad','permiso','otro'].includes(item.tipo) ? item.tipo : 'otro'}"]`)?.textContent;
      const formatDate = date => date.split('-').reverse().join('/');
      row.append(cell(item.profesional), cell(typeLabel), cell(`${formatDate(item.fechaInicio)} – ${formatDate(item.fechaFin)}`),
        status, cell(item.observaciones), actions);
    }
    if (!rows.children.length) {
      const empty = cell('No hay ausencias para estos filtros.');
      empty.colSpan = 6;
      rows.insertRow().append(empty);
    }
  }

  async function loadAbsences() {
    const reload = byId('absenceReload');
    reload.disabled = true;
    try {
      const result = await apiRequest('/gestion-de-profesionales/ausencias/datos');
      absences = result.data;
      today = result.hoy;
      render();
    } catch (error) {
      setFeedback(`No se pudieron cargar las ausencias: ${error.message}`, true);
    } finally {
      reload.disabled = false;
    }
  }

  function setBusy(value) {
    busy = value;
    for (const control of [previewButton, saveButton, cancelEditButton, start, end, byId('absenceTipo'), byId('absenceObservaciones'), confirmImpact]) {
      control.disabled = value;
    }
    professionalSelect.disabled = value || !!editingId.value;
  }

  function validate() {
    formFeedback.textContent = '';
    if (!form.reportValidity()) return false;
    if (end.value < start.value) {
      formFeedback.textContent = 'La fecha final debe ser igual o posterior a la inicial.';
      return false;
    }
    if (absences.some(item => String(item.idProfesional) === professionalSelect.value
      && String(item.idAusencia) !== editingId.value && item.fechaInicio <= end.value && item.fechaFin >= start.value)) {
      formFeedback.textContent = 'El periodo se cruza con otra ausencia del profesional.';
      return false;
    }
    return true;
  }

  async function preview() {
    const query = new URLSearchParams({ fechaInicio: start.value, fechaFin: end.value });
    const result = await apiRequest(`${apiBase}/${professionalSelect.value}/ausencias/preview?${query}`);
    const list = byId('absenceImpactList');
    list.replaceChildren();
    for (const appointment of result.data) {
      const item = document.createElement('li');
      const link = document.createElement('a');
      link.href = `/gestion-de-citas/st-rec-03-gestion-citas?verId=${encodeURIComponent(appointment.idCita)}`;
      link.target = '_blank';
      link.rel = 'noopener';
      const [date, time] = String(appointment.fechaHora).split('T');
      link.textContent = `${date.split('-').reverse().join('/')} ${(time || '').slice(0, 5)} · ${appointment.paciente} · ${appointment.servicio}`;
      item.append(link);
      list.append(item);
    }
    byId('absenceImpactText').textContent = result.count === 0 ? 'No hay citas afectadas.'
      : editingId.value
        ? `${result.count} cita(s) requieren gestión manual después de guardar los cambios.`
        : `${result.count} cita(s) afectadas. Al confirmar, el sistema intentará reasignarlas y cancelará las que no pueda reasignar.`;
    const agenda = byId('absenceAgendaLink');
    const filters = new URLSearchParams({ vista: 'todas', profesional: professionalSelect.value, desde: start.value, hasta: end.value });
    agenda.href = `/gestion-de-citas/st-rec-03-gestion-citas?${filters}`;
    agenda.hidden = result.count === 0;
    byId('absenceConfirmImpact').hidden = result.count === 0;
    reviewedCount = result.count;
    impact.hidden = false;
  }

  previewButton.addEventListener('click', async () => {
    if (busy || !validate()) return;
    invalidatePreview();
    setBusy(true);
    try { await preview(); }
    catch (error) { formFeedback.textContent = error.message; }
    finally { setBusy(false); }
  });

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (busy || !validate()) return;
    const payload = {
      tipo: byId('absenceTipo').value,
      fechaInicio: start.value,
      fechaFin: end.value,
      observaciones: byId('absenceObservaciones').value.trim() || null
    };
    const id = editingId.value;
    const base = `${apiBase}/${professionalSelect.value}/ausencias`;
    setBusy(true);
    try {
      if (reviewedCount === null) {
        await preview();
        formFeedback.textContent = 'Revisa el resultado antes de confirmar el guardado.';
        return;
      }
      if (reviewedCount > 0 && !confirmImpact.checked) {
        formFeedback.textContent = 'Confirma que revisaste las citas afectadas.';
        return;
      }
      const result = await apiRequest(id ? `${base}/${id}` : reviewedCount > 0 ? `${base}-reasignar` : base, {
        method: id ? 'PUT' : 'POST',
        body: JSON.stringify(payload)
      });
      dialog.close();
      setFeedback(result.message || 'Ausencia guardada.');
      await loadAbsences();
    } catch (error) {
      invalidatePreview();
      formFeedback.textContent = error.message;
    } finally {
      setBusy(false);
    }
  });

  [startDateInput, endDateInput].forEach((input) => {
    input.addEventListener('change', () => {
      startDateInput.removeAttribute('aria-invalid');
      endDateInput.removeAttribute('aria-invalid');
      endDateInput.removeAttribute('aria-describedby');
    });
  });

  rows.addEventListener('click', async event => {
    const button = event.target.closest('button[data-action]');
    const item = button && absences.find(a => String(a.idAusencia) === button.dataset.id);
    if (!item) return;
    if (button.dataset.action === 'edit') {
      openForm(item);
      return;
    }
    if (!window.confirm('¿Eliminar esta ausencia?')) return;
    button.disabled = true;
    try {
      const result = await apiRequest(`${apiBase}/${item.idProfesional}/ausencias/${item.idAusencia}`, { method: 'DELETE' });
      setFeedback(result.message || 'Ausencia eliminada.');
      await loadAbsences();
    } catch (error) {
      setFeedback(error.message, true);
    } finally {
      button.disabled = false;
    }
  });

  for (const id of ['absenceFilterProfessional', 'absenceFilterType', 'absenceFilterPeriod']) byId(id).addEventListener('change', render);
  for (const control of [professionalSelect, start, end]) control.addEventListener('input', invalidatePreview);
  dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); });
  cancelEditButton.addEventListener('click', () => dialog.close());
  byId('absenceNew').addEventListener('click', () => openForm());
  byId('absenceReload').addEventListener('click', loadAbsences);
  loadAbsences();
})();
