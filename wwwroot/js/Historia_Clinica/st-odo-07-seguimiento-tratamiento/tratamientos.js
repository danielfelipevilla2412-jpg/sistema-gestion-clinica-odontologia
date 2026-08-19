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

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y CONFIGURACIÓN GLOBAL
// ═══════════════════════════════════════════════════════════════════

const AVATAR_COLORS = ['#3b82f6','#22c55e','#f59e0b','#9333ea','#ef4444','#0ea5e9','#ec4899'];

const avatarColor = (n) => {
  let h = 0;
  for (let i = 0; i < n.length; i++) h = n.charCodeAt(i) + ((h << 5) - h);
  return AVATAR_COLORS[Math.abs(h) % AVATAR_COLORS.length];
};

const getInitials = (n) => {
  const p = n.trim().split(' ');
  return (p[0]?.[0] || '') + (p[1]?.[0] || '');
};

const fmtFecha = (iso) => {
  if (!iso) return null;
  const [y, m, d] = iso.split('-');
  const meses = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
  return `${parseInt(d)} ${meses[parseInt(m)-1]} ${y}`;
};

// Estado global de la aplicación
let allTratamientos = [];
let tratamientosFiltrados = [];
let paginaActual = 1;
let tratamientosPorPagina = 10;

// ═══════════════════════════════════════════════════════════════════
//  ACTUALIZAR ESTADÍSTICAS
// ═══════════════════════════════════════════════════════════════════

const updateStats = (data) => {
  const total = data.length;
  const enCurso = data.filter(t => t.estado === 'en-curso').length;
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

  requestAnimationFrame(() => {
    document.querySelectorAll('.prog-bar[data-width]').forEach(bar => {
      bar.style.width = bar.dataset.width + '%';
    });
  });
};

const buildCard = (t) => {
  const color = avatarColor(t.paciente);
  const inits = getInitials(t.paciente);

  const badgeMap = {
    'en-curso':   { cls: 'badge-en-curso', label: 'En curso' },
    'completado': { cls: 'badge-completado', label: 'Completado' },
    'pausado':    { cls: 'badge-pausado', label: 'Pausado' },
  };
  const badge = badgeMap[t.estado] || badgeMap['pausado'];

  let barClass = 'bar-orange', pctClass = 'pct-orange';
  if (t.estado === 'completado') { barClass = 'bar-green'; pctClass = 'pct-green'; }
  if (t.estado === 'pausado')    { barClass = 'bar-red'; pctClass = 'pct-red'; }

  let footer = '';
  if (t.estado === 'en-curso') {
    footer = `
      <span class="trat-meta">Inicio: <strong><time datetime="${t.inicio}">${fmtFecha(t.inicio) || '—'}</time></strong></span>
      <span class="trat-meta">Estimado: <strong><time datetime="${t.estimado}">${fmtFecha(t.estimado) || '—'}</time></strong></span>
      <span class="trat-meta">Sesiones: <strong>${t.sesiones} de ${t.totalSesiones}</strong></span>
    `;
  } else if (t.estado === 'completado') {
    footer = `
      <span class="trat-meta">Finalizado: <strong><time datetime="${t.finalizado}">${fmtFecha(t.finalizado) || '—'}</time></strong></span>
      <span class="trat-meta">Sesiones: <strong>${t.sesiones} de ${t.totalSesiones}</strong></span>
    `;
  } else if (t.estado === 'pausado') {
    footer = `
      <span class="trat-meta pausado-note">${t.nota || 'Pausado'}</span>
      <span class="trat-meta" style="margin-left:auto">Sesiones: <strong>${t.sesiones} de ${t.totalSesiones}</strong></span>
    `;
  }

  return `
    <div class="trat-card estado-${t.estado}" role="listitem" aria-labelledby="trat-title-${t.id}" tabindex="0">
      <div class="trat-header">
        <div>
          <span class="trat-nombre" id="trat-title-${t.id}">${t.nombre}</span>
          <span class="trat-tipo">${t.tipo}</span>
        </div>
        <span class="badge-estado ${badge.cls}" role="status" aria-label="Estado: ${badge.label}">${badge.label}</span>
      </div>
      <div class="trat-paciente">
        <div class="p-chip" style="background:${color}" aria-hidden="true">${inits}</div>
        <div class="p-info">
          <span class="p-chip-name">${t.paciente}</span>
          <span class="p-odontologo">${t.odontologo}</span>
        </div>
      </div>
      <div class="trat-progress-row">
        <span class="prog-pct ${pctClass}" aria-label="Progreso: ${t.progreso}%">${t.progreso}%</span>
        <div class="prog-bar-bg" role="progressbar" aria-valuenow="${t.progreso}" aria-valuemin="0" aria-valuemax="100" aria-label="Progreso del tratamiento ${t.nombre}: ${t.progreso}% completado">
          <div class="prog-bar ${barClass}" data-width="${t.progreso}" style="width:0"></div>
        </div>
      </div>
      <div class="trat-footer">${footer}</div>
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
  try {
    const resp = await fetch('/historia-clinica/st-odo-07-seguimiento-tratamiento/data', { headers: { 'Accept': 'application/json' } });
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    return await resp.json();
  } catch (error) {
    console.error('No se pudo cargar el seguimiento de tratamientos:', error);
    showToast('No se pudo cargar el seguimiento de tratamientos', 'error');
    return [];
  }
}

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initEventos();
  
  allTratamientos = await fetchTratamientos();
  tratamientosFiltrados = [...allTratamientos];
  
  populateFilters(allTratamientos);
  updateStats(allTratamientos);
  renderizarTodo();
};

document.addEventListener('DOMContentLoaded', init);