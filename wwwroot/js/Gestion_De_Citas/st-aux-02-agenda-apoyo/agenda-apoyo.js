(() => {
  const filters = document.getElementById('apoyoFiltros');
  if (!filters) return;

  const professional = document.getElementById('apoyoProfesional');
  const type = document.getElementById('apoyoTipo');
  const clear = document.getElementById('apoyoLimpiar');
  const count = document.getElementById('apoyoCantidad');
  const table = document.getElementById('apoyoTabla');
  const empty = document.getElementById('apoyoSinResultados');
  const next = document.getElementById('apoyoSiguiente');
  const nextText = document.getElementById('apoyoSiguienteTexto');
  const nextAction = document.getElementById('apoyoSiguienteAccion');
  const rows = [...document.querySelectorAll('#agendaBody tr')];

  function update() {
    const visible = rows.filter(row => {
      row.hidden = (professional.value !== '' && professional.value !== row.dataset.profesional)
        || (type.value !== '' && type.value !== row.dataset.tipo);
      return !row.hidden;
    });

    clear.hidden = !professional.value && !type.value;
    table.hidden = visible.length === 0;
    empty.hidden = visible.length > 0;
    count.textContent = visible.length === 1 ? '1 cita' : visible.length + ' citas';

    const upcoming = visible.find(row => row.dataset.proxima);
    const action = upcoming?.querySelector('[data-preparacion]');
    next.hidden = !upcoming;
    nextText.textContent = upcoming?.dataset.proxima ?? '';
    nextAction.hidden = !action;
    if (action) nextAction.href = action.href;
    else nextAction.removeAttribute('href');
  }

  filters.addEventListener('change', update);
  clear.addEventListener('click', () => {
    professional.value = '';
    type.value = '';
    update();
    professional.focus();
  });
  filters.hidden = false;
  update();
})();
