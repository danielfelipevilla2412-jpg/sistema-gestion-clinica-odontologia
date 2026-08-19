console.log('historial-rec.js cargado');
// ── DATOS REALES (API) ──────────────────────────────────────────────────
let pacientes = [];
const MESES_REC = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

function fmtFechaRec(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return `${String(d.getDate()).padStart(2, '0')} ${MESES_REC[d.getMonth()]} ${d.getFullYear()}`;
}

function normalizarPacienteRec(p) {
  return {
    ...p,
    ultimaConsulta: fmtFechaRec(p.ultimaConsulta),
    consultas: (p.consultas || []).map(c => ({
      fecha: fmtFechaRec(c.fecha),
      descripcion: c.proc,
      observacion: c.nota
    })),
    tratamientos: p.tratamientos || [],
    documentos: p.documentos || []
  };
}

async function cargarPacientesRec() {
  try {
    const resp = await fetch('/historia-clinica/st-rec-historial/data', { headers: { 'Accept': 'application/json' } });
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const data = await resp.json();
    pacientes = data.map(normalizarPacienteRec);
  } catch (e) {
    console.error('No se pudo cargar el historial de pacientes:', e);
    pacientes = [];
  }
  poblarFiltroOdontologosRec();
  pacientesFiltrados = [...pacientes];
  updateStats();
  renderTable(pacientesFiltrados);
}

function poblarFiltroOdontologosRec() {
  const select = document.getElementById('filterOdontologo');
  if (!select) return;
  const vistos = new Map();
  pacientes.forEach(p => { if (p.odontologoKey) vistos.set(p.odontologoKey, p.odontologo); });
  select.innerHTML = '<option value="">Todos los odontólogos</option>' +
    [...vistos.entries()].map(([key, nombre]) => `<option value="${key}">${nombre}</option>`).join('');
}

let pacientesFiltrados = [...pacientes];
let currentPage = 1;
let pageSize = 10;
let totalPages = 1;

const safeGetElement = (id) => document.getElementById(id);

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');

  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

const getPageData = (list) => {
  const sizeValue = safeGetElement('pageSizeSelect')?.value;
  pageSize = sizeValue === 'all' ? list.length : Number(sizeValue) || 10;
  totalPages = pageSize === 0 ? 1 : Math.max(1, Math.ceil(list.length / pageSize));
  currentPage = Math.min(Math.max(1, currentPage), totalPages);
  if (pageSize === list.length) return list;
  const start = (currentPage - 1) * pageSize;
  return list.slice(start, start + pageSize);
};

const updatePagination = (totalRecords) => {
  const controls = safeGetElement('paginationControls');
  const pageShowing = safeGetElement('pageShowing');
  const pageTotal = safeGetElement('pageTotal');
  const pageInfo = safeGetElement('pageSummary');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');

  if (!controls || totalRecords <= pageSize) {
    if (controls) controls.style.display = 'none';
  } else {
    controls.style.display = 'flex';
    if (pageShowing) pageShowing.textContent = String(Math.min(pageSize, totalRecords));
    if (pageTotal) pageTotal.textContent = String(totalRecords);
    if (pageInfo) pageInfo.textContent = `Página ${currentPage} de ${totalPages}`;
    if (btnPrev) btnPrev.disabled = currentPage <= 1;
    if (btnNext) btnNext.disabled = currentPage >= totalPages;
  }
};

const updateStats = () => {
  const total = pacientes.length;
  const today = pacientes.filter((p) => {
    const fecha = new Date(p.ultimaConsulta);
    const now = new Date();
    return fecha.getDate() === now.getDate() && fecha.getMonth() === now.getMonth() && fecha.getFullYear() === now.getFullYear();
  }).length;
  const alerta = pacientes.filter((p) => p.alerta).length;
  const odontologos = new Set(pacientes.filter((p) => p.estado === 'activo').map((p) => p.odontologo)).size;

  const mapping = {
    statTotal: total,
    statHoy: today,
    statAlerta: alerta,
    statOdo: odontologos
  };

  Object.entries(mapping).forEach(([id, value]) => {
    const el = safeGetElement(id);
    if (el) el.textContent = String(value);
  });
};

const renderTable = (list) => {
  const tbody = safeGetElement('tablaBody');
  const count = safeGetElement('resultsCount');
  const noResults = safeGetElement('noResults');
  if (!tbody || !count || !noResults) return;

  tbody.innerHTML = '';
  const pageData = getPageData(list);
  count.textContent = `Mostrando ${pageData.length} de ${list.length} paciente${list.length !== 1 ? 's' : ''}`;

  if (list.length === 0) {
    noResults.style.display = 'grid';
    updatePagination(0);
    return;
  }

  noResults.style.display = 'none';
  pageData.forEach((p) => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td>
        <div class="td-paciente">
          <div class="pac-avatar">${(p.nombre || '?').split(' ').filter(Boolean).map((part) => part[0] || '').join('').slice(0, 2).toUpperCase() || '?'}</div>
          <div>
            <div class="pac-name">${p.nombre}</div>
            <div class="pac-email">${p.email}</div>
          </div>
        </div>
      </td>
      <td>${p.cedula}</td>
      <td>${p.odontologo}</td>
      <td>${p.ultimaConsulta}</td>
      <td><span class="badge-alerta ${p.alerta ? 'si' : 'no'}">${p.alerta ? '⚠️ Sí' : '— No'}</span></td>
      <td><span class="badge-estado ${p.estado}">${p.estado.charAt(0).toUpperCase() + p.estado.slice(1)}</span></td>
      <td><button class="btn-view">📋 Historial</button></td>
    `;
    tbody.appendChild(tr);

    const actionButton = tr.querySelector('.btn-view');
    if (actionButton) {
      actionButton.addEventListener('click', () => renderHistoryDetails(p));
    }
  });
  updatePagination(list.length);
};

const renderHistoryList = (elementId, items, renderItem) => {
  const element = safeGetElement(elementId);
  if (!element) return;
  element.innerHTML = items.length
    ? items.map(renderItem).join('')
    : '<li>No hay registros disponibles.</li>';
};

const renderHistoryDetails = (paciente) => {
  const elName = safeGetElement('historyPatientName');
  if (elName) elName.textContent = paciente.nombre;
  const elCedula = safeGetElement('historyCedula');
  if (elCedula) elCedula.textContent = paciente.cedula;
  const elOdontologo = safeGetElement('historyOdontologo');
  if (elOdontologo) elOdontologo.textContent = paciente.odontologo;
  const elUltima = safeGetElement('historyUltimaConsulta');
  if (elUltima) elUltima.textContent = paciente.ultimaConsulta;
  const elAlerta = safeGetElement('historyAlerta');
  if (elAlerta) elAlerta.textContent = paciente.alerta ? '⚠️ Sí' : '— No';

  renderHistoryList('historyConsultations', paciente.consultas || [], (item) => `
      <li>
        <strong>${item.fecha}</strong> · ${item.descripcion}
        <span>${item.observacion}</span>
      </li>
    `);

  renderHistoryList('historyTreatments', paciente.tratamientos || [], (item) => `
      <li>
        <strong>${item.nombre}</strong>
        <span>Progreso: ${item.progreso}%</span>
      </li>
    `);

  renderHistoryList('historyDocuments', paciente.documentos || [], (item) => `
      <li>
        <strong>${item.nombre}</strong>
        <span>${item.tipo === 'pdf' ? 'PDF' : item.tipo.toUpperCase()}</span>
      </li>
    `);

  const panel = safeGetElement('historyPanel');
  if (panel) {
    panel.style.display = 'block';
    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
};

const hideHistoryPanel = () => {
  const panel = safeGetElement('historyPanel');
  if (panel) panel.style.display = 'none';
};

const filterPatients = () => {
  currentPage = 1;
  const query = safeGetElement('searchInput')?.value.toLowerCase().trim() || '';
  const odontologo = safeGetElement('filterOdontologo')?.value || '';
  const alerta = safeGetElement('filterAlerta')?.value || '';
  const estado = safeGetElement('filterEstado')?.value || '';

  pacientesFiltrados = pacientes.filter((p) => {
    const matchesQuery = !query || p.nombre.toLowerCase().includes(query) || p.cedula.includes(query) || p.email.toLowerCase().includes(query);
    const matchesOdo = !odontologo || p.odontologoKey === odontologo;
    const matchesAlerta = !alerta || (alerta === 'si' ? p.alerta : !p.alerta);
    const matchesEstado = !estado || p.estado === estado;
    return matchesQuery && matchesOdo && matchesAlerta && matchesEstado;
  });

  renderTable(pacientesFiltrados);
};

const initFilters = () => {
  safeGetElement('searchInput')?.addEventListener('input', filterPatients);
  safeGetElement('filterOdontologo')?.addEventListener('change', filterPatients);
  safeGetElement('filterAlerta')?.addEventListener('change', filterPatients);
  safeGetElement('filterEstado')?.addEventListener('change', filterPatients);
  safeGetElement('pageSizeSelect')?.addEventListener('change', () => {
    currentPage = 1;
    renderTable(pacientesFiltrados);
  });
  safeGetElement('btnPrev')?.addEventListener('click', () => {
    if (currentPage > 1) {
      currentPage -= 1;
      renderTable(pacientesFiltrados);
    }
  });
  safeGetElement('btnNext')?.addEventListener('click', () => {
    if (currentPage < totalPages) {
      currentPage += 1;
      renderTable(pacientesFiltrados);
    }
  });
  safeGetElement('closeHistoryPanel')?.addEventListener('click', hideHistoryPanel);
};

const initNavGroups = () => {
  document.querySelectorAll('.nav-group-header').forEach((header) => {
    const group = header.parentElement;
    const toggle = () => {
      const isOpen = group.classList.toggle('open');
      header.setAttribute('aria-expanded', String(isOpen));
    };
    header.addEventListener('click', toggle);
    header.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        toggle();
      }
    });
  });
};

const init = () => {
  initSidebar();
  initNavGroups();
  initFilters();
  cargarPacientesRec();
};

document.addEventListener('DOMContentLoaded', init);