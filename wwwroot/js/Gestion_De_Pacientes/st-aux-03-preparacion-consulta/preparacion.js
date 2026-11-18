(() => {
  const config = window.smiletrackPreparacionConfig;
  const checklist = Array.from(document.querySelectorAll('#checklist input[type="checkbox"]'));
  const progressBar = document.querySelector('.progress-bar-bg');
  const progressFill = document.getElementById('progressFill');
  const progressText = document.getElementById('progressText');
  const observations = document.getElementById('observations');
  const confirmButton = document.getElementById('btnConfirmPreparation');
  const storageKey = config?.citaId ? `smiletrack:preparacion:v1:${config.citaId}` : null;

  if (!config || checklist.length === 0) {
    console.error('[SmileTrack] Faltan los datos necesarios para iniciar la preparación.');
    return;
  }

  if (!config.citaId) {
    checklist.forEach((checkbox) => {
      checkbox.disabled = true;
    });
    if (observations) observations.disabled = true;
  }

  const notify = (type, message) => {
    const service = window.ToastService;
    if (service && typeof service[type] === 'function') {
      service[type](message);
    } else {
      console.error(`[SmileTrack] ${message}`);
    }
  };

  const updateStatus = (badgeId, textId, checked) => {
    const badge = document.getElementById(badgeId);
    const label = document.getElementById(textId);
    if (!badge || !label) return;

    label.textContent = checked ? 'Listo' : 'Pendiente';
    badge.classList.toggle('status-complete', checked);
  };

  const updateProgress = () => {
    const completed = checklist.filter((checkbox) => checkbox.checked).length;
    const percentage = Math.round((completed / checklist.length) * 100);

    if (progressFill) progressFill.style.width = `${percentage}%`;
    if (progressBar) progressBar.setAttribute('aria-valuenow', String(percentage));
    if (progressText) progressText.textContent = `${completed} de ${checklist.length} ítems completados`;

    updateStatus('statusLimpieza', 'statusLimpiezaTxt', checklist[0]?.checked === true);
    updateStatus('statusEsterilizacion', 'statusEsterilizacionTxt', checklist[1]?.checked === true);
    updateStatus('statusMateriales', 'statusMaterialesTxt', checklist[5]?.checked === true);
  };

  const saveState = () => {
    if (!storageKey) return;

    try {
      localStorage.setItem(storageKey, JSON.stringify({
        checklist: checklist.map((checkbox) => checkbox.checked),
        observations: observations?.value ?? ''
      }));
    } catch (error) {
      console.error('[SmileTrack] No se pudo guardar el estado de preparación localmente.', error);
      notify('error', 'No se pudo guardar el estado de preparación en este dispositivo.');
    }
  };

  if (storageKey) {
    try {
      const savedState = localStorage.getItem(storageKey);
      if (savedState) {
        const state = JSON.parse(savedState);
        if (Array.isArray(state.checklist)) {
          checklist.forEach((checkbox, index) => {
            checkbox.checked = state.checklist[index] === true;
          });
        }
        if (observations && typeof state.observations === 'string') {
          observations.value = state.observations;
        }
      }
    } catch (error) {
      console.error('[SmileTrack] No se pudo leer el estado de preparación local.', error);
      notify('warning', 'No se pudo recuperar el progreso guardado en este dispositivo.');
    }
  }

  checklist.forEach((checkbox) => {
    checkbox.addEventListener('change', () => {
      updateProgress();
      saveState();
    });
  });

  observations?.addEventListener('input', saveState);
  updateProgress();

  document.getElementById('selectorCita')?.addEventListener('change', (event) => {
    const selectedCitaId = event.currentTarget.value;
    const url = new URL(window.location.href);
    url.searchParams.set('citaId', selectedCitaId);
    window.location.assign(url);
  });

  confirmButton?.addEventListener('click', () => {
    const pending = checklist.length - checklist.filter((checkbox) => checkbox.checked).length;
    if (pending > 0) {
      notify('warning', `Completa los ${pending} ítems pendientes antes de confirmar.`);
      return;
    }

    saveState();
    notify('success', 'Preparación completada y guardada en este dispositivo para esta cita.');
  });
})();
