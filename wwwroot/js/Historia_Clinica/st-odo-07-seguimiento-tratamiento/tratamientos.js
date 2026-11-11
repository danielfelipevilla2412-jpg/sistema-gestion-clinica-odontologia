/**
 * SMILETRACK — SEGUIMIENTO TRATAMIENTOS (seguimiento.js)
 * Versión con filtros avanzados, búsqueda y paginación
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;

  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
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
  const [y, m, d] = iso.split('-');
  const meses = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
  return `${parseInt(d)} ${meses[parseInt(m)-1]} ${y}`;
};

// Estado global de la aplicación
let allTratamientos = [];
let tratamientosFiltrados = [];
// Yeray: Mantiene sesiones y referencias cargadas para mostrar el conteo real y sus vínculos en cada plan.
let allSesiones = [];
let opcionesCitasSesion = [];
let opcionesNotasSesion = [];
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
    filterPaciente.innerHTML = '<option value="">Todos los pacientes</option>';
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
    filterTipo.innerHTML = '<option value="">Todos los tipos</option>';
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
    filterOdontologo.innerHTML = '<option value="">Todos los odontólogos</option>';
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
    pagination.style.display = 'none';
    return;
  }
  
  pagination.style.display = 'flex';
  
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
    btn.textContent = i;
    btn.setAttribute('aria-label', `Página ${i}`);
    btn.setAttribute('aria-current', i === paginaActual ? 'page' : 'false');
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
      empty.style.display = 'block';
      empty.setAttribute('aria-hidden', 'false');
    }
    return;
  }
  
  if (empty) {
    empty.style.display = 'none';
    empty.setAttribute('aria-hidden', 'true');
  }

  list.innerHTML = tratamientosPagina.map(t => buildCard(t)).join('');

};

const buildCard = (t) => {
  const badgeMap = {
    'propuesto':  { cls: 'badge-pausado', label: 'Propuesto' },
    'aprobado':   { cls: 'badge-en-curso', label: 'Aprobado' },
    'en-curso':   { cls: 'badge-en-curso', label: 'En curso' },
    'completado': { cls: 'badge-completado', label: 'Completado' },
    'pausado':    { cls: 'badge-pausado', label: 'Pausado' },
    'cancelado':  { cls: 'badge-pausado', label: 'Cancelado' },
  };
  const badge = badgeMap[t.estado] || badgeMap.propuesto;
  const estadoClase = badgeMap[t.estado] ? t.estado : 'propuesto';
  const id = Number(t.id);
  const estados = [
    ['propuesto', 'Propuesto'], ['aprobado', 'Aprobado'], ['en-curso', 'En curso'],
    ['pausado', 'Pausado'], ['completado', 'Completado'], ['cancelado', 'Cancelado']
  ];
  const options = estados.map(([value, label]) =>
    `<option value="${value}"${t.estado === value ? ' selected' : ''}>${label}</option>`
  ).join('');
  const fecha = t.inicio ? String(t.inicio).slice(0, 10) : '';
  const sesionesEstimadas = Number(t.sesionesEstimadas || 0);
  const sesionesRealizadas = Number(t.sesionesRealizadas || 0);
  const progreso = sesionesEstimadas > 0 ? Math.min(100, Math.round(sesionesRealizadas * 100 / sesionesEstimadas)) : 0;
  const superaEstimacion = sesionesRealizadas > sesionesEstimadas;
  const sesionesPlan = allSesiones.filter(s => Number(s.planId) === id);
  const sesionesHtml = sesionesPlan.length
    ? sesionesPlan.map(s => `
        <div class="session-row ${s.anulada ? 'is-annulled' : ''}">
          <span>${fmtFecha(String(s.fecha).slice(0, 10)) || '—'} · ${escapeHtml(s.profesional || 'Profesional')}</span>
          <span>${s.citaId ? `Cita #${Number(s.citaId)}` : ''}${s.notaId ? ` Nota #${Number(s.notaId)}` : ''}</span>
          ${s.anulada
            ? `<span>Anulada por ${escapeHtml(s.profesionalAnula || 'profesional')}: ${escapeHtml(s.motivoAnulacion || '')}</span>`
            : `<button type="button" class="btn-anular-sesion" data-session-id="${Number(s.id)}">Anular</button>`}
        </div>`).join('')
    : '<p>No hay sesiones registradas.</p>';

  return `
    <div class="trat-card estado-${estadoClase}" role="listitem" aria-labelledby="trat-title-${id}"
      data-plan-id="${id}" data-paciente-id="${Number(t.pacienteId)}"
      data-servicio-id="${Number(t.servicioId)}" data-historia-id="${Number(t.historiaId)}"
      data-sesiones-estimadas="${sesionesEstimadas}" data-numero-fdi="${escapeHtml(t.numeroFdi || '')}"
      data-observacion="${escapeHtml(t.nota || '')}">
      <div class="trat-header">
        <div>
          <span class="trat-nombre" id="trat-title-${id}">${escapeHtml(t.nombre)}</span>
          <span class="trat-tipo">${escapeHtml(t.tipo || 'Servicio')}</span>
        </div>
        <span class="badge-estado ${badge.cls}" role="status" aria-label="Estado: ${badge.label}">${badge.label}</span>
      </div>
      <div class="trat-paciente">
        <div class="p-info">
          <span class="p-chip-name">${escapeHtml(t.paciente)}</span>
          <span class="p-odontologo">${escapeHtml(t.odontologo || 'Sin asignar')} · Documento: ${escapeHtml(t.cedula || 'N/D')}</span>
        </div>
      </div>
      <div class="trat-footer">
        <span class="trat-meta">Registrado: <strong><time datetime="${escapeHtml(fecha)}">${fmtFecha(fecha) || '—'}</time></strong></span>
        <span class="trat-meta">Pieza FDI: <strong>${escapeHtml(t.numeroFdi || 'General')}</strong></span>
        ${t.nota ? `<span class="trat-meta">${escapeHtml(t.nota)}</span>` : ''}
        <div class="trat-status-controls">
          <label for="planEstado-${id}">Actualizar estado</label>
          <select class="plan-estado-select" id="planEstado-${id}">${options}</select>
          <label for="sesionesEstimadas-${id}">Estimadas</label>
          <input class="plan-sesiones-estimadas" id="sesionesEstimadas-${id}" type="number" min="0" step="1" value="${sesionesEstimadas}" />
          <button type="button" class="btn-actualizar-plan">Guardar</button>
        </div>
        <section class="session-summary" aria-label="Seguimiento de sesiones">
          <p>Sesiones realizadas: <strong>${sesionesRealizadas}</strong> de <strong>${sesionesEstimadas}</strong> estimadas</p>
          <progress max="100" value="${progreso}" aria-label="Progreso de sesiones: ${progreso}%">${progreso}%</progress>
          ${superaEstimacion ? '<p class="session-warning">Las sesiones realizadas superan la estimación. Ajusta el plan.</p>' : ''}
          <button type="button" class="btn-registrar-sesion" data-plan-id="${id}">Registrar sesión realizada</button>
          <details><summary>Ver registro de sesiones (${sesionesPlan.length})</summary><div class="session-list">${sesionesHtml}</div></details>
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
    resultsCount.textContent = 'Mostrando 0 tratamientos';
  } else {
    const inicio = (paginaActual - 1) * tratamientosPorPagina + 1;
    const fin = Math.min(paginaActual * tratamientosPorPagina, total);
    resultsCount.textContent = `Mostrando ${inicio}-${fin} de ${total} tratamientos`;
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
      showToast('Selecciona el paciente y el servicio.', 'warning');
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

const initSidebar = () => {
  const ham = safeGetElement('hamburger');
  const sb = safeGetElement('sidebar');
  const ov = safeGetElement('overlay');
  
  if (!ham || !sb || !ov) return;

  const toggleMenu = (show) => {
    sb.classList.toggle('open', show);
    ov.classList.toggle('open', show);
    ham.setAttribute('aria-expanded', show);
    ov.setAttribute('aria-hidden', !show);
    
    if (show) {
      const firstLink = sb.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      ham.focus();
    }
  };

  ham.addEventListener('click', () => toggleMenu(true));
  ov.addEventListener('click', () => toggleMenu(false));

  sb.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sb.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
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

  pacienteSelect.replaceChildren(new Option('Selecciona un paciente', ''));
  (data.pacientes || []).forEach(paciente => {
    pacienteSelect.add(new Option(`${paciente.nombre} · ${paciente.documento}`, paciente.id));
  });

  servicioSelect.replaceChildren(new Option('Selecciona un servicio', ''));
  (data.servicios || []).forEach(servicio => {
    servicioSelect.add(new Option(`${servicio.nombre} · ${servicio.categoria}`, servicio.id));
  });
};

const cargarPlanes = async () => {
  const data = await fetchTratamientos();
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
  button.disabled = true;
  try {
    const response = await fetch('/historia-clinica/st-odo-07-seguimiento-tratamiento/guardar', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { RequestVerificationToken: token } : {})
      },
      body: JSON.stringify(payload)
    });
    const result = await response.json();
    if (!response.ok || !result.success) throw new Error(result.message || 'No se pudo guardar el plan.');
    showToast(result.advertencia || result.message || 'Plan de tratamiento guardado.', result.advertencia ? 'warning' : 'success');
    await cargarPlanes();
  } catch (error) {
    console.error('No se pudo guardar el plan de tratamiento:', error);
    showToast(error.message || 'No se pudo guardar el plan de tratamiento.', 'error');
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
  citaSelect.replaceChildren(new Option('Sin cita', ''));
  opcionesCitasSesion.filter(cita => Number(cita.pacienteId) === patientId).forEach(cita => {
    citaSelect.add(new Option(`Cita #${cita.id} · ${fmtFecha(String(cita.fecha).slice(0, 10))}`, cita.id));
  });
  notaSelect.replaceChildren(new Option('Sin nota', ''));
  opcionesNotasSesion.filter(nota => Number(nota.historiaId) === historyId).forEach(nota => {
    notaSelect.add(new Option(`Nota #${nota.id} · ${fmtFecha(String(nota.fecha).slice(0, 10))}`, nota.id));
  });
  dialog.showModal();
};

const abrirDialogoAnulacion = (idSesion) => {
  safeGetElement('annulSessionId').value = String(idSesion);
  safeGetElement('annulSessionReason').value = '';
  safeGetElement('annulSessionDialog').showModal();
};

const enviarSesion = async (url, payload, dialog, button) => {
  const token = document.querySelector('#planTratamientoForm input[name="__RequestVerificationToken"]')?.value;
  button.disabled = true;
  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { RequestVerificationToken: token } : {})
      },
      body: JSON.stringify(payload)
    });
    const result = await response.json();
    if (!response.ok || !result.success) throw new Error(result.message || 'No se pudo actualizar la sesión.');
    dialog.close();
    showToast(result.message, result.superaEstimacion ? 'warning' : 'success');
    await cargarPlanes();
  } catch (error) {
    showToast(error.message || 'No se pudo actualizar la sesión.', 'error');
  } finally {
    button.disabled = false;
  }
};

const registrarSesionPlan = (event) => {
  event.preventDefault();
  const dialog = safeGetElement('sessionDialog');
  const button = event.submitter;
  enviarSesion(`/historia-clinica/st-odo-07-seguimiento-tratamiento/${safeGetElement('sessionPlanId').value}/sesiones`, {
    fechaSesion: safeGetElement('sessionDate').value,
    citaId: Number(safeGetElement('sessionCita').value) || null,
    notaId: Number(safeGetElement('sessionNota').value) || null
  }, dialog, button);
};

const anularSesionPlan = (event) => {
  event.preventDefault();
  const dialog = safeGetElement('annulSessionDialog');
  const button = event.submitter;
  enviarSesion(`/historia-clinica/st-odo-07-seguimiento-tratamiento/sesiones/${safeGetElement('annulSessionId').value}/anular`, {
    motivo: safeGetElement('annulSessionReason').value.trim()
  }, dialog, button);
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initEventos();
  try {
    await cargarPlanes();
  } catch (error) {
    console.error('No se pudo cargar el seguimiento de tratamientos:', error);
    showToast('No se pudo cargar el seguimiento de tratamientos.', 'error');
  }
};

document.addEventListener('DOMContentLoaded', init);