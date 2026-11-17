(() => {
'use strict';

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  return el;
};

const t = (key, values) => {
  if (!window.I18N || typeof window.I18N.t !== 'function') {
    throw new Error('El servicio de traducción no está disponible.');
  }
  return window.I18N.t(key, values);
};

const debounce = (fn, delay) => {
  let timeoutId;
  return function (...args) {
    window.clearTimeout(timeoutId);
    timeoutId = window.setTimeout(() => fn(...args), delay);
  };
};

const showToast = (message, type = 'success') => {
  const service = window.ToastService;
  if (!service || typeof service[type] !== 'function') {
    console.error('[SmileTrack] El servicio de notificaciones no está disponible.');
    return;
  }
  const titleKey = type === 'warning' ? 'toast.warning_title'
    : type === 'error' ? 'toast.error_title' : 'toast.success_title';
  service[type](t(titleKey), message);
};

const escapeHtml = (value) => String(value ?? '')
  .replaceAll('&', '&amp;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;')
  .replaceAll('"', '&quot;')
  .replaceAll("'", '&#39;');

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y CONFIGURACIÓN GLOBAL
// ═══════════════════════════════════════════════════════════════════

const fmtFecha = (iso) => {
  if (!iso) return null;
  const date = new Date(`${String(iso).slice(0, 10)}T12:00:00`);
  return Number.isNaN(date.getTime())
    ? null
    : new Intl.DateTimeFormat(t('odo07.date_locale'), { day: 'numeric', month: 'short', year: 'numeric' }).format(date);
};

// Estado global de la aplicación
let allTratamientos = [];
let tratamientosFiltrados = [];
// Yeray: Mantiene sesiones y referencias cargadas para mostrar el conteo real y sus vínculos en cada plan.
let allSesiones = [];
let opcionesCitasSesion = [];
let opcionesNotasSesion = [];
let datosOpcionesPlan = {};
let paginaActual = 1;
let tratamientosPorPagina = 10;

// ═══════════════════════════════════════════════════════════════════
//  ACTUALIZAR ESTADÍSTICAS
// ═══════════════════════════════════════════════════════════════════

const updateStats = (data) => {
  const total = data.length;
  const enCurso = data.filter(t => ['aprobado', 'en-curso'].includes(t.estado)).length;
  const completado = data.filter(t => t.estado === 'completado').length;
  const pausado = data.filter(t => t.estado === 'pausado').length;
  
  const statTotal = safeGetElement('statTotal');
  const statEnCurso = safeGetElement('statEnCurso');
  const statCompletado = safeGetElement('statCompletado');
  const statPausado = safeGetElement('statPausado');
  
  if (statTotal) statTotal.textContent = total;
  if (statEnCurso) statEnCurso.textContent = enCurso;
  if (statCompletado) statCompletado.textContent = completado;
  if (statPausado) statPausado.textContent = pausado;
};

// ═══════════════════════════════════════════════════════════════════
//  POPULAR FILTROS DROPDOWN
// ═══════════════════════════════════════════════════════════════════

const populateFilters = (data) => {
  const pacientes = [...new Set(data.map(t => t.paciente))].sort();
  const tipos = [...new Set(data.map(t => t.tipo))].sort();
  const odontologos = [...new Set(data.map(t => t.odontologo))].sort();
  
  const filterPaciente = safeGetElement('filterPaciente');
  if (filterPaciente) {
    const currentValue = filterPaciente.value;
    filterPaciente.replaceChildren(new Option(t('odo07.all_patients'), ''));
    pacientes.forEach(p => {
      const option = document.createElement('option');
      option.value = p;
      option.textContent = p;
      filterPaciente.appendChild(option);
    });
    filterPaciente.value = currentValue;
  }
  
  const filterTipo = safeGetElement('filterTipo');
  if (filterTipo) {
    const currentValue = filterTipo.value;
    filterTipo.replaceChildren(new Option(t('odo07.all_types'), ''));
    tipos.forEach(t => {
      const option = document.createElement('option');
      option.value = t;
      option.textContent = t;
      filterTipo.appendChild(option);
    });
    filterTipo.value = currentValue;
  }
  
  const filterOdontologo = safeGetElement('filterOdontologo');
  if (filterOdontologo) {
    const currentValue = filterOdontologo.value;
    filterOdontologo.replaceChildren(new Option(t('odo07.all_professionals'), ''));
    odontologos.forEach(o => {
      const option = document.createElement('option');
      option.value = o;
      option.textContent = o;
      filterOdontologo.appendChild(option);
    });
    filterOdontologo.value = currentValue;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FILTRAR Y BUSCAR
// ═══════════════════════════════════════════════════════════════════

const aplicarFiltros = () => {
  const searchInput = safeGetElement('searchInput');
  const filterPaciente = safeGetElement('filterPaciente');
  const filterEstado = safeGetElement('filterEstado');
  const filterTipo = safeGetElement('filterTipo');
  const filterOdontologo = safeGetElement('filterOdontologo');
  
  const searchTerm = searchInput?.value.toLowerCase().trim() || '';
  const pacienteFilter = filterPaciente?.value || '';
  const estadoFilter = filterEstado?.value || '';
  const tipoFilter = filterTipo?.value || '';
  const odontologoFilter = filterOdontologo?.value || '';
  
  tratamientosFiltrados = allTratamientos.filter(t => {
    const matchSearch = !searchTerm || 
      t.paciente.toLowerCase().includes(searchTerm) ||
      t.nombre.toLowerCase().includes(searchTerm) ||
      t.cedula.includes(searchTerm);
    
    const matchPaciente = !pacienteFilter || t.paciente === pacienteFilter;
    const matchEstado = !estadoFilter || t.estado === estadoFilter;
    const matchTipo = !tipoFilter || t.tipo === tipoFilter;
    const matchOdontologo = !odontologoFilter || t.odontologo === odontologoFilter;
    
    return matchSearch && matchPaciente && matchEstado && matchTipo && matchOdontologo;
  });
  
  paginaActual = 1;
  renderizarTodo();
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

const obtenerPaginaActual = () => {
  const inicio = (paginaActual - 1) * tratamientosPorPagina;
  const fin = inicio + tratamientosPorPagina;
  return tratamientosFiltrados.slice(inicio, fin);
};

const getTotalPaginas = () => {
  return Math.ceil(tratamientosFiltrados.length / tratamientosPorPagina);
};

const renderizarPaginacion = () => {
  const pagination = safeGetElement('pagination');
  const paginationNumbers = safeGetElement('paginationNumbers');
  const btnFirst = safeGetElement('btnFirst');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  const btnLast = safeGetElement('btnLast');
  
  if (!pagination || !paginationNumbers) return;
  
  const totalPaginas = getTotalPaginas();
  
  if (totalPaginas <= 1) {
    pagination.hidden = true;
    return;
  }
  
  pagination.hidden = false;
  
  if (btnFirst) btnFirst.disabled = paginaActual === 1;
  if (btnPrev) btnPrev.disabled = paginaActual === 1;
  if (btnNext) btnNext.disabled = paginaActual === totalPaginas;
  if (btnLast) btnLast.disabled = paginaActual === totalPaginas;
  
  paginationNumbers.innerHTML = '';
  
  let startPage = Math.max(1, paginaActual - 2);
  let endPage = Math.min(totalPaginas, startPage + 4);
  
  if (endPage - startPage < 4) {
    startPage = Math.max(1, endPage - 4);
  }
  
  for (let i = startPage; i <= endPage; i++) {
    const btn = document.createElement('button');
    btn.className = `pagination-btn ${i === paginaActual ? 'active' : ''}`;
    btn.type = 'button';
    btn.textContent = i;
    btn.setAttribute('aria-label', t('odo07.page_number', { number: i }));
    if (i === paginaActual) btn.setAttribute('aria-current', 'page');
    btn.addEventListener('click', () => {
      paginaActual = i;
      renderizarTodo();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    paginationNumbers.appendChild(btn);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: LISTA DE TRATAMIENTOS
// ═══════════════════════════════════════════════════════════════════

const renderLista = () => {
  const list = safeGetElement('tratamientosList');
  const empty = safeGetElement('emptyState');
  if (!list) return;

  const tratamientosPagina = obtenerPaginaActual();

  if (!tratamientosPagina.length) {
    list.innerHTML = '';
    if (empty) {
      empty.hidden = false;
    }
    return;
  }
  
  if (empty) {
    empty.hidden = true;
  }

  list.innerHTML = tratamientosPagina.map(t => buildCard(t)).join('');

};

const buildCard = (treatment) => {
  const badgeMap = {
    propuesto: { cls: 'pendiente', label: 'odo07.status_proposed' },
    aprobado: { cls: 'en-curso', label: 'odo07.status_approved' },
    'en-curso': { cls: 'en-curso', label: 'odo07.status_in_progress' },
    completado: { cls: 'completada', label: 'odo07.status_completed' },
    pausado: { cls: 'pendiente', label: 'odo07.status_paused' },
    cancelado: { cls: 'cancelado', label: 'odo07.status_cancelled' }
  };
  const badge = badgeMap[treatment.estado] || badgeMap.propuesto;
  const estadoClase = badgeMap[treatment.estado] ? treatment.estado : 'propuesto';
  const id = Number(treatment.id);
  const estados = [
    ['propuesto', 'odo07.status_proposed'], ['aprobado', 'odo07.status_approved'],
    ['en-curso', 'odo07.status_in_progress'], ['pausado', 'odo07.status_paused'],
    ['completado', 'odo07.status_completed'], ['cancelado', 'odo07.status_cancelled']
  ];
  const options = estados.map(([value, label]) =>
    `<option value="${value}"${treatment.estado === value ? ' selected' : ''}>${escapeHtml(t(label))}</option>`
  ).join('');
  const fecha = treatment.inicio ? String(treatment.inicio).slice(0, 10) : '';
  const sesionesEstimadas = Number(treatment.sesionesEstimadas || 0);
  const sesionesRealizadas = Number(treatment.sesionesRealizadas || 0);
  const progreso = sesionesEstimadas > 0 ? Math.min(100, Math.round(sesionesRealizadas * 100 / sesionesEstimadas)) : 0;
  const superaEstimacion = sesionesRealizadas > sesionesEstimadas;
  const sesionesPlan = allSesiones.filter(s => Number(s.planId) === id);
  const sesionesHtml = sesionesPlan.length
    ? sesionesPlan.map(s => `
        <div class="session-row ${s.anulada ? 'is-annulled' : ''}">
          <span>${escapeHtml(fmtFecha(String(s.fecha).slice(0, 10)) || '—')} · ${escapeHtml(s.profesional || t('odo07.professional'))}</span>
          <span>${s.citaId ? `${escapeHtml(t('odo07.appointment_number', { number: Number(s.citaId) }))}` : ''}${s.notaId ? ` ${escapeHtml(t('odo07.note_number', { number: Number(s.notaId) }))}` : ''}</span>
          ${s.anulada
            ? `<span>${escapeHtml(t('odo07.annulled_by', { name: s.profesionalAnula || t('odo07.professional'), reason: s.motivoAnulacion || '' }))}</span>`
            : `<button type="button" class="btn-danger btn-anular-sesion" data-session-id="${Number(s.id)}">${escapeHtml(t('odo07.annul'))}</button>`}
        </div>`).join('')
    : `<p>${escapeHtml(t('odo07.no_sessions'))}</p>`;
  const badgeLabel = t(badge.label);
  return `
    <div class="trat-card estado-${estadoClase}" role="listitem" aria-labelledby="trat-title-${id}"
      data-plan-id="${id}" data-paciente-id="${Number(treatment.pacienteId)}"
      data-servicio-id="${Number(treatment.servicioId)}" data-historia-id="${Number(treatment.historiaId)}"
      data-sesiones-estimadas="${sesionesEstimadas}" data-numero-fdi="${escapeHtml(treatment.numeroFdi || '')}"
      data-observacion="${escapeHtml(treatment.nota || '')}">
      <div class="trat-header">
        <div>
          <span class="trat-nombre" id="trat-title-${id}">${escapeHtml(treatment.nombre)}</span>
          <span class="trat-tipo">${escapeHtml(treatment.tipo || t('odo07.service'))}</span>
        </div>
        <span class="badge-estado status-badge ${badge.cls}" role="status" aria-label="${escapeHtml(t('odo07.status_named', { status: badgeLabel }))}">${escapeHtml(badgeLabel)}</span>
      </div>
      <div class="trat-paciente">
        <div class="p-info">
          <span class="p-chip-name">${escapeHtml(treatment.paciente)}</span>
          <span class="p-odontologo">${escapeHtml(treatment.odontologo || t('odo07.unassigned'))} · ${escapeHtml(t('odo07.document_label', { document: treatment.cedula || t('odo07.document_unknown') }))}</span>
        </div>
      </div>
      <div class="trat-footer">
        <span class="trat-meta">${escapeHtml(t('odo07.registered_on'))}: <strong><time datetime="${escapeHtml(fecha)}">${escapeHtml(fmtFecha(fecha) || '—')}</time></strong></span>
        <span class="trat-meta">${escapeHtml(t('odo07.tooth_fdi'))}: <strong>${escapeHtml(treatment.numeroFdi || t('odo07.general'))}</strong></span>
        ${treatment.nota ? `<span class="trat-meta">${escapeHtml(treatment.nota)}</span>` : ''}
        <div class="trat-status-controls">
          <label for="planEstado-${id}">${escapeHtml(t('odo07.update_status'))}</label>
          <select class="plan-estado-select" id="planEstado-${id}">${options}</select>
          <label for="sesionesEstimadas-${id}">${escapeHtml(t('odo07.estimated'))}</label>
          <input class="plan-sesiones-estimadas" id="sesionesEstimadas-${id}" type="number" min="0" step="1" value="${sesionesEstimadas}" />
          <button type="button" class="btn-primary btn-actualizar-plan">${escapeHtml(t('odo07.save'))}</button>
        </div>
        <section class="session-summary" aria-label="${escapeHtml(t('odo07.session_followup_aria'))}">
          <p>${escapeHtml(t('odo07.sessions_completed_count', { completed: sesionesRealizadas, estimated: sesionesEstimadas }))}</p>
          <progress max="100" value="${progreso}" aria-label="${escapeHtml(t('odo07.session_progress_aria', { progress: progreso }))}">${progreso}%</progress>
          ${superaEstimacion ? `<p class="session-warning">${escapeHtml(t('odo07.over_estimate'))}</p>` : ''}
          <button type="button" class="btn-secondary btn-registrar-sesion" data-plan-id="${id}">${escapeHtml(t('odo07.register_session'))}</button>
          <details><summary>${escapeHtml(t('odo07.view_sessions', { count: sesionesPlan.length }))}</summary><div class="session-list">${sesionesHtml}</div></details>
        </section>
      </div>
    </div>
  `;
};

// ═══════════════════════════════════════════════════════════════════
//  ACTUALIZAR CONTADOR DE RESULTADOS
// ═══════════════════════════════════════════════════════════════════

const actualizarContador = () => {
  const resultsCount = safeGetElement('resultsCount');
  if (!resultsCount) return;
  
  const total = tratamientosFiltrados.length;
  if (total === 0) {
    resultsCount.textContent = t('odo07.showing_none');
  } else {
    const inicio = (paginaActual - 1) * tratamientosPorPagina + 1;
    const fin = Math.min(paginaActual * tratamientosPorPagina, total);
    resultsCount.textContent = t('odo07.showing_range', { start: inicio, end: fin, total });
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDERIZAR TODO (Orquestador)
// ═══════════════════════════════════════════════════════════════════

const renderizarTodo = () => {
  renderLista();
  renderizarPaginacion();
  actualizarContador();
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZAR EVENTOS
// ═══════════════════════════════════════════════════════════════════

const initEventos = () => {
  const searchInput = safeGetElement('searchInput');
  if (searchInput) {
    searchInput.addEventListener('input', debounce(aplicarFiltros, 300));
  }
  
  ['filterPaciente', 'filterEstado', 'filterTipo', 'filterOdontologo'].forEach(id => {
    const filter = safeGetElement(id);
    if (filter) {
      filter.addEventListener('change', aplicarFiltros);
    }
  });
  
  const filterPorPagina = safeGetElement('filterPorPagina');
  if (filterPorPagina) {
    filterPorPagina.addEventListener('change', (e) => {
      tratamientosPorPagina = parseInt(e.target.value);
      paginaActual = 1;
      renderizarTodo();
    });
  }

  const planForm = safeGetElement('planTratamientoForm');
  planForm?.addEventListener('submit', event => {
    event.preventDefault();
    const button = safeGetElement('guardarPlanTratamiento');
    const pacienteId = Number(safeGetElement('planPaciente')?.value);
    const servicioId = Number(safeGetElement('planServicio')?.value);
    if (!pacienteId || !servicioId || !button) {
      showToast(t('odo07.select_patient_service_error'), 'warning');
      return;
    }

    guardarPlan({
      pacienteId,
      servicioId,
      sesionesEstimadas: Number(safeGetElement('planSesionesEstimadas')?.value || 0),
      numeroFdi: safeGetElement('planNumeroFdi')?.value.trim() || null,
      estado: safeGetElement('planEstado')?.value || 'propuesto',
      observacion: safeGetElement('planObservacion')?.value.trim() || null
    }, button);
  });

  safeGetElement('tratamientosList')?.addEventListener('click', event => {
    const registrarSesion = event.target.closest('.btn-registrar-sesion');
    if (registrarSesion) {
      const card = registrarSesion.closest('[data-plan-id]');
      if (card) abrirDialogoSesion(card);
      return;
    }

    const anularSesion = event.target.closest('.btn-anular-sesion');
    if (anularSesion) {
      abrirDialogoAnulacion(Number(anularSesion.dataset.sessionId));
      return;
    }

    const button = event.target.closest('.btn-actualizar-plan');
    if (!button) return;
    const card = button.closest('[data-plan-id]');
    const estado = card?.querySelector('.plan-estado-select')?.value;
    if (!card || !estado) return;

    guardarPlan({
      idPlan: Number(card.dataset.planId),
      pacienteId: Number(card.dataset.pacienteId),
      servicioId: Number(card.dataset.servicioId),
      sesionesEstimadas: Number(card.querySelector('.plan-sesiones-estimadas')?.value || 0),
      numeroFdi: card.dataset.numeroFdi || null,
      observacion: card.dataset.observacion || null,
      estado
    }, button);
  });

  document.querySelectorAll('[data-close-dialog]').forEach(button => {
    button.addEventListener('click', () => document.getElementById(button.dataset.closeDialog)?.close());
  });

  safeGetElement('sessionForm')?.addEventListener('submit', registrarSesionPlan);
  safeGetElement('annulSessionForm')?.addEventListener('submit', anularSesionPlan);
  
  const btnFirst = safeGetElement('btnFirst');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  const btnLast = safeGetElement('btnLast');
  
  if (btnFirst) btnFirst.addEventListener('click', () => { paginaActual = 1; renderizarTodo(); window.scrollTo({ top: 0, behavior: 'smooth' }); });
  if (btnPrev) btnPrev.addEventListener('click', () => { if (paginaActual > 1) { paginaActual--; renderizarTodo(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
  if (btnNext) btnNext.addEventListener('click', () => { if (paginaActual < getTotalPaginas()) { paginaActual++; renderizarTodo(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
  if (btnLast) btnLast.addEventListener('click', () => { paginaActual = getTotalPaginas(); renderizarTodo(); window.scrollTo({ top: 0, behavior: 'smooth' }); });
  
  const btnClearFilters = safeGetElement('btnClearFilters');
  if (btnClearFilters) {
    btnClearFilters.addEventListener('click', () => {
      if (searchInput) searchInput.value = '';
      const fp = safeGetElement('filterPaciente'); if (fp) fp.value = '';
      const fe = safeGetElement('filterEstado'); if (fe) fe.value = '';
      const ft = safeGetElement('filterTipo'); if (ft) ft.value = '';
      const fo = safeGetElement('filterOdontologo'); if (fo) fo.value = '';
      aplicarFiltros();
    });
  }
};

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL
// ═══════════════════════════════════════════════════════════════════

async function fetchTratamientos() {
  const resp = await fetch('/historia-clinica/st-odo-07-seguimiento-tratamiento/data', { headers: { 'Accept': 'application/json' } });
  if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
  return await resp.json();
}

const poblarOpcionesPlan = (data) => {
  const pacienteSelect = safeGetElement('planPaciente');
  const servicioSelect = safeGetElement('planServicio');
  if (!pacienteSelect || !servicioSelect) return;

  pacienteSelect.replaceChildren(new Option(t('odo07.select_patient'), ''));
  (data.pacientes || []).forEach(paciente => {
    pacienteSelect.add(new Option(`${paciente.nombre} · ${paciente.documento}`, paciente.id));
  });

  servicioSelect.replaceChildren(new Option(t('odo07.select_service'), ''));
  (data.servicios || []).forEach(servicio => {
    servicioSelect.add(new Option(`${servicio.nombre} · ${servicio.categoria}`, servicio.id));
  });
};

const cargarPlanes = async () => {
  const data = await fetchTratamientos();
  datosOpcionesPlan = data;
  allTratamientos = Array.isArray(data.tratamientos) ? data.tratamientos : [];
  allSesiones = Array.isArray(data.sesiones) ? data.sesiones : [];
  opcionesCitasSesion = Array.isArray(data.citas) ? data.citas : [];
  opcionesNotasSesion = Array.isArray(data.notas) ? data.notas : [];
  tratamientosFiltrados = [...allTratamientos];
  poblarOpcionesPlan(data);
  populateFilters(allTratamientos);
  updateStats(allTratamientos);
  renderizarTodo();
};

const guardarPlan = async (payload, button) => {
  const token = document.querySelector('#planTratamientoForm input[name="__RequestVerificationToken"]')?.value;
  if (!token) {
    showToast(t('odo07.antiforgery_missing'), 'error');
    return;
  }
  button.disabled = true;
  try {
    const response = await fetch('/historia-clinica/st-odo-07-seguimiento-tratamiento/guardar', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      body: JSON.stringify(payload)
    });
    const result = await response.json();
    if (!response.ok || !result.success) throw new Error(result.message || t('odo07.save_plan_error'));
    showToast(result.advertencia ? t('odo07.over_estimate') : t('odo07.plan_saved'), result.advertencia ? 'warning' : 'success');
    await cargarPlanes();
  } catch (error) {
    console.error('No se pudo guardar el plan de tratamiento:', error);
    showToast(error.message || t('odo07.save_plan_error'), 'error');
  } finally {
    button.disabled = false;
  }
};

// Yeray: Filtra vínculos opcionales por paciente e historia para asociar cada sesión sin depender de que exista una cita.
const abrirDialogoSesion = (card) => {
  const dialog = safeGetElement('sessionDialog');
  const planId = Number(card.dataset.planId);
  const patientId = Number(card.dataset.pacienteId);
  const historyId = Number(card.dataset.historiaId);
  const citaSelect = safeGetElement('sessionCita');
  const notaSelect = safeGetElement('sessionNota');
  const fechaInput = safeGetElement('sessionDate');
  safeGetElement('sessionPlanId').value = String(planId);
  const hoy = new Date();
  fechaInput.value = new Date(hoy.getTime() - hoy.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
  citaSelect.replaceChildren(new Option(t('odo07.no_appointment'), ''));
  opcionesCitasSesion.filter(cita => Number(cita.pacienteId) === patientId).forEach(cita => {
    citaSelect.add(new Option(t('odo07.appointment_date', {
      number: cita.id,
      date: fmtFecha(String(cita.fecha).slice(0, 10))
    }), cita.id));
  });
  notaSelect.replaceChildren(new Option(t('odo07.no_note'), ''));
  opcionesNotasSesion.filter(nota => Number(nota.historiaId) === historyId).forEach(nota => {
    notaSelect.add(new Option(t('odo07.note_date', {
      number: nota.id,
      date: fmtFecha(String(nota.fecha).slice(0, 10))
    }), nota.id));
  });
  dialog.showModal();
};

const abrirDialogoAnulacion = (idSesion) => {
  safeGetElement('annulSessionId').value = String(idSesion);
  safeGetElement('annulSessionReason').value = '';
  safeGetElement('annulSessionDialog').showModal();
};

const enviarSesion = async (url, payload, dialog, button, successKey) => {
  const token = document.querySelector('#planTratamientoForm input[name="__RequestVerificationToken"]')?.value;
  if (!token) {
    showToast(t('odo07.antiforgery_missing'), 'error');
    return;
  }
  button.disabled = true;
  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': token
      },
      body: JSON.stringify(payload)
    });
    const result = await response.json();
    if (!response.ok || !result.success) throw new Error(result.message || t('odo07.session_update_error'));
    dialog.close();
    showToast(result.superaEstimacion ? t('odo07.over_estimate') : t(successKey),
      result.superaEstimacion ? 'warning' : 'success');
    await cargarPlanes();
  } catch (error) {
    showToast(error.message || t('odo07.session_update_error'), 'error');
  } finally {
    button.disabled = false;
  }
};

const registrarSesionPlan = (event) => {
  event.preventDefault();
  const dialog = safeGetElement('sessionDialog');
  const button = event.submitter || event.currentTarget.querySelector('button[type="submit"]');
  if (!button) {
    showToast(t('odo07.submit_button_missing'), 'error');
    return;
  }
  enviarSesion(`/historia-clinica/st-odo-07-seguimiento-tratamiento/${safeGetElement('sessionPlanId').value}/sesiones`, {
    fechaSesion: safeGetElement('sessionDate').value,
    citaId: Number(safeGetElement('sessionCita').value) || null,
    notaId: Number(safeGetElement('sessionNota').value) || null
  }, dialog, button, 'odo07.session_registered');
};

const anularSesionPlan = (event) => {
  event.preventDefault();
  const dialog = safeGetElement('annulSessionDialog');
  const button = event.submitter || event.currentTarget.querySelector('button[type="submit"]');
  if (!button) {
    showToast(t('odo07.submit_button_missing'), 'error');
    return;
  }
  enviarSesion(`/historia-clinica/st-odo-07-seguimiento-tratamiento/sesiones/${safeGetElement('annulSessionId').value}/anular`, {
    motivo: safeGetElement('annulSessionReason').value.trim()
  }, dialog, button, 'odo07.session_annulled');
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initEventos();
  try {
    await cargarPlanes();
  } catch (error) {
    console.error('No se pudo cargar el seguimiento de tratamientos:', error);
    showToast(t('odo07.load_error'), 'error');
  }
};

document.addEventListener('i18n:change', () => {
  poblarOpcionesPlan(datosOpcionesPlan);
  populateFilters(allTratamientos);
  renderizarTodo();
});

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init, { once: true });
} else {
  init();
}
})();