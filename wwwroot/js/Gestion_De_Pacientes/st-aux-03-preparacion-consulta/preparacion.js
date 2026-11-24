(() => {
  document.getElementById('selectorCita')?.addEventListener('change', ({ currentTarget }) => {
    const url = new URL(window.location.href);
    url.searchParams.set('citaId', currentTarget.value);
    window.location.assign(url);
  });

  const checklist = document.getElementById('checklist');
  const citaId = window.smiletrackPreparacionConfig?.citaId;
  if (!checklist || !citaId) return;
  const tasks = [...checklist.querySelectorAll('[data-tarea]')].map(row => ({
    index: Number(row.dataset.tarea),
    done: row.querySelector('[data-realizada]'),
    skip: row.querySelector('[data-no-aplica]')
  }));
  const observations = document.getElementById('observations');
  const button = document.getElementById('btnConfirmPreparation');
  const status = document.getElementById('prepStatus');
  const progress = document.getElementById('prepProgress');
  const summary = document.getElementById('progressText');
  const key = 'smiletrack:preparacion:v1:' + citaId;
  let confirmedAt = null;

  try {
    const saved = JSON.parse(localStorage.getItem(key) || '{}') ?? {};
    for (const task of tasks) {
      if (task.skip) task.skip.checked = saved.noAplica?.[task.index] === true;
      task.done.checked = !task.skip?.checked && saved.checklist?.[task.index] === true;
    }
    if (typeof saved.observations === 'string') observations.value = saved.observations;
    if (typeof saved.confirmedAt === 'string' && Number.isFinite(Date.parse(saved.confirmedAt))) {
      confirmedAt = saved.confirmedAt;
    }
  } catch {
    status.textContent = 'No se pudo recuperar la preparación local.';
  }

  const resolved = task => task.done.checked || task.skip?.checked;
  if (!tasks.every(resolved)) confirmedAt = null;

  function update() {
    const total = tasks.filter(resolved).length;
    progress.value = total;
    summary.textContent = total + ' de ' + tasks.length + ' tareas resueltas · ' + (tasks.length - total) + ' pendientes';
    button.disabled = total !== tasks.length || Boolean(confirmedAt);
    if (confirmedAt) {
      const date = new Intl.DateTimeFormat('es-CO', {
        dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Bogota'
      }).format(new Date(confirmedAt));
      status.textContent = 'Preparación completa · ' + date;
    }
  }

  function save(timestamp) {
    const state = { checklist: [], noAplica: [], observations: observations.value, confirmedAt: timestamp };
    for (const task of tasks) {
      state.checklist[task.index] = task.done.checked;
      state.noAplica[task.index] = task.skip?.checked ?? false;
    }
    try {
      localStorage.setItem(key, JSON.stringify(state));
      return true;
    } catch {
      status.textContent = 'No se pudo guardar en este navegador.';
      return false;
    }
  }

  function saveDraft() {
    confirmedAt = null;
    status.textContent = '';
    save(null);
    update();
  }

  for (const task of tasks) {
    task.done.addEventListener('change', () => {
      if (task.done.checked && task.skip) task.skip.checked = false;
      saveDraft();
    });
    task.skip?.addEventListener('change', () => {
      if (task.skip.checked) task.done.checked = false;
      saveDraft();
    });
  }
  observations.addEventListener('input', saveDraft);
  button.addEventListener('click', () => {
    if (!tasks.every(resolved)) return;
    const timestamp = new Date().toISOString();
    if (save(timestamp)) confirmedAt = timestamp;
    update();
  });
  update();
})();
