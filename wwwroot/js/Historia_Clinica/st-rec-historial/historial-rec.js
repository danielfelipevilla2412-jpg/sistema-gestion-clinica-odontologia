console.log('historial-rec.js cargado');
// ── DATOS REALES (API) ──────────────────────────────────────────────────
let pacientes = [];
const MESES_REC = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
let pacientesFiltrados = [];
let currentPage = 1;
let pageSize = 10;
let totalPages = 1;
let totalRecords = 0;
let requestVersion = 0;
let odontologosDisponibles = [];
let filtrosRec = { search: '', odontologo: '', alerta: '', estado: '' };

function fmtFechaRec(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return `${String(d.getDate()).padStart(2, '0')} ${MESES_REC[d.getMonth()]} ${d.getFullYear()}`;
}

function normalizarPacienteRec(p) {
  return {
    ...p,
    nombre: p.nombre || 'Nombre no disponible',
    cedula: p.cedula || '',
    tipoDocumento: p.tipoDocumento || '',
    email: p.email || '',
    odontologo: p.odontologo || 'Sin asignar',
    estado: p.estado || 'sin estado',
    ultimaConsulta: fmtFechaRec(p.ultimaConsulta),
    consultas: (p.consultas || []).map(c => ({ ...c, fecha: fmtFechaRec(c.fecha), descripcion: c.proc || 'Consulta' })),
    tratamientos: p.tratamientos || [],
    documentos: p.documentos || []
  };
}

async function cargarPacientesRec() {
  const version = ++requestVersion;
  const pageSizeValue = safeGetElement('pageSizeSelect')?.value;
  pageSize = Number(pageSizeValue) || 10;
  const params = new URLSearchParams({ page: String(currentPage), pageSize: String(pageSize) });
  Object.entries(filtrosRec).forEach(([key, value]) => { if (value) params.set(key, value); });
  try {
    const resp = await fetch(`/historia-clinica/st-rec-historial/data?${params}`, { headers: { 'Accept': 'application/json' } });
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const data = await resp.json();
    if (version !== requestVersion) return;
    pacientes = (data.items || []).map(normalizarPacienteRec);
    totalRecords = data.total || 0;
    totalPages = data.totalPages || 1;
    odontologosDisponibles = data.odontologos || [];
    updateStats(data.stats);
  } catch (e) {
    if (version !== requestVersion) return;
    console.error('No se pudo cargar el historial de pacientes:', e);
    pacientes = [];
    totalRecords = 0;
    updateStats({});
  }
  poblarFiltroOdontologosRec();
  pacientesFiltrados = [...pacientes];
  renderTable(pacientesFiltrados);
}

function poblarFiltroOdontologosRec() {
  const select = document.getElementById('filterOdontologo');
  if (!select) return;
  const seleccionActual = select.value;
  select.innerHTML = '<option value="">Todos los odontólogos</option>' +
    odontologosDisponibles.map(({ key, nombre }) => `<option value="${key}">${nombre}</option>`).join('');
  select.value = seleccionActual;
}

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
    if (pageShowing) pageShowing.textContent = String(pacientes.length);
    if (pageTotal) pageTotal.textContent = String(totalRecords);
    if (pageInfo) pageInfo.textContent = `Página ${currentPage} de ${totalPages}`;
    if (btnPrev) btnPrev.disabled = currentPage <= 1;
    if (btnNext) btnNext.disabled = currentPage >= totalPages;
  }
};

const updateStats = (stats = {}) => {
  const mapping = {
    statTotal: stats.total ?? 0,
    statHoy: stats.hoy ?? 0,
    statAlerta: stats.alertas ?? 0,
    statOdo: stats.odontologos ?? 0
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
  count.textContent = `Mostrando ${list.length} de ${totalRecords} paciente${totalRecords !== 1 ? 's' : ''}`;

  if (list.length === 0) {
    noResults.style.display = 'grid';
    const message = noResults.querySelector('p');
    if (message) message.textContent = totalRecords === 0 && !pacientes.length
      ? 'No se pudieron cargar pacientes o no hay registros disponibles.'
      : 'No se encontraron pacientes con ese criterio.';
    updatePagination(0);
    return;
  }

  noResults.style.display = 'none';
  list.forEach((p) => {
    const tr = document.createElement('tr');
    const patientCell = document.createElement('td');
    const patientInfo = document.createElement('div');
    patientInfo.className = 'td-paciente';
    const avatar = document.createElement('div');
    avatar.className = 'pac-avatar';
    avatar.textContent = p.nombre.split(' ').filter(Boolean).map(part => part[0] || '').join('').slice(0, 2).toUpperCase() || '?';
    const patientText = document.createElement('div');
    const name = document.createElement('div');
    name.className = 'pac-name';
    name.textContent = p.nombre;
    const email = document.createElement('div');
    email.className = 'pac-email';
    email.textContent = p.email;
    patientText.append(name, email);
    patientInfo.append(avatar, patientText);
    patientCell.appendChild(patientInfo);

    const documentCell = document.createElement('td');
    documentCell.textContent = [p.tipoDocumento, p.cedula].filter(Boolean).join(' ') || 'No registrado';
    const dentistCell = document.createElement('td');
    dentistCell.textContent = p.odontologo;
    const lastVisitCell = document.createElement('td');
    lastVisitCell.textContent = p.ultimaConsulta;
    const alertCell = document.createElement('td');
    const alertBadge = document.createElement('span');
    alertBadge.className = `badge-alerta ${p.alerta ? 'si' : 'no'}`;
    alertBadge.textContent = p.alerta ? 'Sí' : 'No';
    alertCell.appendChild(alertBadge);
    const stateCell = document.createElement('td');
    const stateBadge = document.createElement('span');
    stateBadge.className = `badge-estado ${p.estado.toLowerCase()}`;
    stateBadge.textContent = p.estado.charAt(0).toUpperCase() + p.estado.slice(1);
    stateCell.appendChild(stateBadge);
    const actionCell = document.createElement('td');
    const actionButton = document.createElement('button');
    actionButton.type = 'button';
    actionButton.className = 'btn-view';
    actionButton.textContent = 'Historial';
    actionCell.appendChild(actionButton);
    tr.append(patientCell, documentCell, dentistCell, lastVisitCell, alertCell, stateCell, actionCell);
    tbody.appendChild(tr);

    actionButton.addEventListener('click', () => renderHistoryDetails(p));
  });
  updatePagination(totalRecords);
};

const renderHistoryList = (elementId, items, renderItem) => {
  const element = safeGetElement(elementId);
  if (!element) return;
  element.replaceChildren();
  if (!items.length) {
    const emptyItem = document.createElement('li');
    emptyItem.textContent = 'No hay registros disponibles.';
    element.appendChild(emptyItem);
    return;
  }
  items.forEach((item) => {
    const listItem = document.createElement('li');
    renderItem(listItem, item);
    element.appendChild(listItem);
  });
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

  renderHistoryList('historyConsultations', paciente.consultas || [], (itemElement, item) => {
    const title = document.createElement('strong');
    title.textContent = `${item.fecha} · ${item.descripcion}`;
    itemElement.appendChild(title);
    const provider = document.createElement('span');
    provider.textContent = item.odo || 'Profesional no registrado';
    itemElement.appendChild(provider);

    const fields = [
      ['Motivo', item.motivoConsulta],
      ['Enfermedad actual', item.enfermedadActual],
      ['Antecedentes', item.antecedentesMedicos],
      ['Síntomas', Array.isArray(item.sintomas) ? item.sintomas.join(', ') : ''],
      ['Alergias', item.alergias],
      ['Diagnóstico', item.diagnostico],
      ['Hallazgos', item.hallazgos],
      ['Observaciones odontológicas', item.odontogramaObservaciones],
      ['Exámenes', item.examenesComplementarios],
      ['Evolución', item.evolucionClinica || item.nota],
      ['Procedimiento', item.proc],
      ['Prescripción', item.prescripcion]
    ].filter(([, value]) => typeof value === 'string' && value.trim());

    if (fields.length) {
      const details = document.createElement('dl');
      details.className = 'history-record-details';
      fields.forEach(([label, value]) => {
        const row = document.createElement('div');
        const term = document.createElement('dt');
        const description = document.createElement('dd');
        term.textContent = label;
        description.textContent = value;
        row.append(term, description);
        details.appendChild(row);
      });
      itemElement.appendChild(details);
    }
  });

  renderHistoryList('historyTreatments', paciente.tratamientos || [], (itemElement, item) => {
    const title = document.createElement('strong');
    title.textContent = item.nombre || 'Tratamiento';
    const progress = document.createElement('span');
    progress.textContent = `Progreso: ${Number(item.progreso) || 0}%`;
    itemElement.append(title, progress);
  });

  renderHistoryList('historyDocuments', paciente.documentos || [], (itemElement, item) => {
    const title = document.createElement('strong');
    title.textContent = item.nombre || 'Documento clínico';
    const type = document.createElement('span');
    type.textContent = (item.tipo || 'documento').toUpperCase();
    itemElement.append(title, type);
  });

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
  filtrosRec = {
    search: safeGetElement('searchInput')?.value.trim() || '',
    odontologo: safeGetElement('filterOdontologo')?.value || '',
    alerta: safeGetElement('filterAlerta')?.value || '',
    estado: safeGetElement('filterEstado')?.value || ''
  };
  cargarPacientesRec();
};

const initFilters = () => {
  let searchTimer;
  safeGetElement('searchInput')?.addEventListener('input', () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(filterPatients, 250);
  });
  safeGetElement('filterOdontologo')?.addEventListener('change', filterPatients);
  safeGetElement('filterAlerta')?.addEventListener('change', filterPatients);
  safeGetElement('filterEstado')?.addEventListener('change', filterPatients);
  safeGetElement('pageSizeSelect')?.addEventListener('change', () => {
    currentPage = 1;
    cargarPacientesRec();
  });
  safeGetElement('btnPrev')?.addEventListener('click', () => {
    if (currentPage > 1) {
      currentPage -= 1;
      cargarPacientesRec();
    }
  });
  safeGetElement('btnNext')?.addEventListener('click', () => {
    if (currentPage < totalPages) {
      currentPage += 1;
      cargarPacientesRec();
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