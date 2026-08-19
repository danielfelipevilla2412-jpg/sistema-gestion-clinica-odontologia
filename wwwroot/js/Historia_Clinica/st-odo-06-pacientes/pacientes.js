/**
 * SMILETRACK — MIS PACIENTES ODONTÓLOGO (pacientes.js)
 * Paginación + Filtros múltiples + Modal detalle + Modal historial
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
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
//  ESTADO INICIAL (se reemplaza con datos reales vía fetchPacientes())
// ═══════════════════════════════════════════════════════════════════
const SAMPLE_PACIENTES = [];

let pacientes = [...SAMPLE_PACIENTES];
let filteredPacientes = [...SAMPLE_PACIENTES];
let currentPage = 1;
let pageSize = 10;

// Estado de filtros
let fEstado = '';
let fAlergias = '';
let fCita = '';
let fServicio = '';
let fSearch = '';

// ═══════════════════════════════════════════════════════════════════
//  HELPERS DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════
const badgeClass = (estado) => ({ activo: 'badge-activo', seguimiento: 'badge-seguimiento', alta: 'badge-alta' }[estado] || 'badge-activo');
const estadoLabel = (estado) => ({ activo: 'Activo', seguimiento: 'En seguimiento', alta: 'Alta médica' }[estado] || estado);
const getAvatarColor = (nombre) => {
  const colors = ['var(--avatar-bg-1)', 'var(--avatar-bg-2)', 'var(--avatar-bg-3)', 'var(--avatar-bg-4)', 'var(--avatar-bg-5)'];
  return colors[nombre.charCodeAt(0) % colors.length];
};
const getInitials = (nombre) => nombre.split(' ').map(n => n[0] || '').join('').toUpperCase().slice(0, 2);

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════
const updatePagination = (total) => {
  const controls = safeGetElement('paginationControls');
  const pageShowing = safeGetElement('pageShowing');
  const pageTotal = safeGetElement('pageTotal');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  const pageNumBtns = safeGetElement('pageNumBtns');

  const totalPages = pageSize === 0 ? 1 : Math.max(1, Math.ceil(total / pageSize));
  const showing = pageSize === 0 ? total : Math.min(pageSize, total - (currentPage - 1) * pageSize);

  if (pageShowing) pageShowing.textContent = String(showing);
  if (pageTotal) pageTotal.textContent = String(total);

  if (controls) controls.style.display = total === 0 ? 'none' : 'flex';

  if (btnPrev) btnPrev.disabled = currentPage <= 1;
  if (btnNext) btnNext.disabled = currentPage >= totalPages;

  if (pageNumBtns) {
    pageNumBtns.innerHTML = '';
    for (let i = 1; i <= totalPages; i++) {
      const btn = document.createElement('button');
      btn.className = `pagination-btn${i === currentPage ? ' active' : ''}`;
      btn.setAttribute('aria-label', `Página ${i}`);
      if (i === currentPage) btn.setAttribute('aria-current', 'page');
      btn.textContent = String(i);
      btn.addEventListener('click', () => { currentPage = i; renderTable(filteredPacientes); });
      pageNumBtns.appendChild(btn);
    }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDERIZADO DE TABLA
// ═══════════════════════════════════════════════════════════════════
const renderTable = (data) => {
  const tbody = safeGetElement('pacientesTbody');
  if (!tbody) return;

  filteredPacientes = data;
  const total = data.length;

  const start = pageSize === 0 ? 0 : (currentPage - 1) * pageSize;
  const pageData = pageSize === 0 ? data : data.slice(start, start + pageSize);

  tbody.innerHTML = '';

  if (!pageData.length) {
    tbody.innerHTML = `<tr><td colspan="6" class="table-empty">No se encontraron pacientes con los filtros aplicados.</td></tr>`;
    updatePagination(0);
    return;
  }

  pageData.forEach(p => {
    const tr = document.createElement('tr');
    tr.setAttribute('role', 'row');
    tr.setAttribute('tabindex', '0');
    tr.setAttribute('aria-label', `Ver detalle de ${p.nombre}`);
    tr.style.cursor = 'pointer';

    const proximaClase = p.proxima === '—' ? 'sin-cita' : '';
    const avatarColor = getAvatarColor(p.nombre);
    const initials = getInitials(p.nombre);

    tr.innerHTML = `
      <td class="td-paciente">
        <div class="p-avatar" style="background:${avatarColor}" aria-hidden="true">${initials}</div>
        <div>
          <div class="p-name">${p.nombre}</div>
          <div class="p-cedula">${p.cedula}</div>
        </div>
      </td>
      <td class="td-fecha"><time datetime="${p.ultimaISO}">${p.ultima}</time></td>
      <td>${p.servicio}</td>
      <td class="td-cita ${proximaClase}">
        ${p.proxima === '—' ? 'Sin cita' : `<time datetime="${p.proximaISO}">${p.proxima}</time>`}
      </td>
      <td><span class="badge ${badgeClass(p.estado)}" role="status" aria-label="Estado: ${estadoLabel(p.estado)}">${estadoLabel(p.estado)}</span></td>
      <td>
        <div class="actions-cell">
          <button class="btn-action btn-detalle" data-id="${p.id}" aria-label="Ver detalle de ${p.nombre}" title="Ver detalle del paciente">
            <span aria-hidden="true">👁️</span> Detalle
          </button>
          <button class="btn-action btn-historial" data-id="${p.id}" aria-label="Ver historial clínico de ${p.nombre}" title="Historial clínico">
            <span aria-hidden="true">📋</span> Historial
          </button>
        </div>
      </td>
    `;

    tr.addEventListener('click', (e) => {
      if (!e.target.closest('.btn-action')) openModal(p.id);
    });
    tr.addEventListener('keydown', (e) => {
      if ((e.key === 'Enter' || e.key === ' ') && !e.target.closest('.btn-action')) {
        e.preventDefault();
        openModal(p.id);
      }
    });

    tbody.appendChild(tr);
  });

  // Eventos de botones
  tbody.querySelectorAll('.btn-detalle').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      openModal(Number(btn.dataset.id));
    });
  });
  tbody.querySelectorAll('.btn-historial').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      openHistorialModal(Number(btn.dataset.id));
    });
  });

  updatePagination(total);

  // Actualizar contador de resultados
  const filterResults = safeGetElement('filterResults');
  const hasFilters = fSearch || fEstado || fAlergias || fCita || fServicio;
  if (filterResults) {
    filterResults.textContent = hasFilters
      ? `${total} resultado${total !== 1 ? 's' : ''} encontrado${total !== 1 ? 's' : ''}`
      : '';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS
// ═══════════════════════════════════════════════════════════════════
const applyFilters = () => {
  currentPage = 1;
  const q = fSearch.trim().toLowerCase();

  const filtered = pacientes.filter(p => {
    if (q && !p.nombre.toLowerCase().includes(q) && !p.cedula.includes(q) && !p.servicio.toLowerCase().includes(q)) return false;
    if (fEstado && p.estado !== fEstado) return false;
    if (fAlergias === 'con' && !(p.alergias && p.alergias.length > 0)) return false;
    if (fAlergias === 'sin' && (p.alergias && p.alergias.length > 0)) return false;
    if (fCita === 'con' && (!p.proximaISO)) return false;
    if (fCita === 'sin' && p.proximaISO) return false;
    if (fServicio && p.servicio !== fServicio) return false;
    return true;
  });

  // Indicar estado activo del botón limpiar
  const hasFilters = fSearch || fEstado || fAlergias || fCita || fServicio;
  const clearBtn = safeGetElement('btnClearFilters');
  if (clearBtn) clearBtn.classList.toggle('active', !!hasFilters);

  renderTable(filtered);
};

// Poblar select de servicios dinámicamente
const populateServicioSelect = () => {
  const select = safeGetElement('filterServicio');
  if (!select) return;
  const servicios = [...new Set(pacientes.map(p => p.servicio))].sort();
  servicios.forEach(s => {
    const opt = document.createElement('option');
    opt.value = s;
    opt.textContent = s;
    select.appendChild(opt);
  });
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL DETALLE
// ═══════════════════════════════════════════════════════════════════
const openModal = (id) => {
  const p = pacientes.find(a => a.id === id);
  if (!p) return;

  const content = safeGetElement('modalContent');
  if (content) {
    const avatarColor = getAvatarColor(p.nombre);
    const initials = getInitials(p.nombre);
    const alergiasHtml = p.alergias && p.alergias.length
      ? `<span class="modal-val danger">${p.alergias.join(', ')}</span>`
      : '<span class="modal-val">Ninguna</span>';

    content.innerHTML = `
      <div class="modal-avatar-wrap">
        <div class="modal-avatar" style="background:${avatarColor}" aria-hidden="true">${initials}</div>
      </div>
      <h3 class="modal-name">${p.nombre}</h3>
      <p class="modal-cedula">${p.cedula}</p>
      <div class="modal-grid">
        <div class="modal-item">
          <div class="modal-key">Última consulta</div>
          <div class="modal-val"><time datetime="${p.ultimaISO}">${p.ultima}</time></div>
        </div>
        <div class="modal-item">
          <div class="modal-key">Próxima cita</div>
          <div class="modal-val ${p.proxima === '—' ? 'danger' : ''}">
            ${p.proxima === '—' ? 'Sin cita' : `<time datetime="${p.proximaISO}">${p.proxima}</time>`}
          </div>
        </div>
        <div class="modal-item">
          <div class="modal-key">Servicio</div>
          <div class="modal-val">${p.servicio}</div>
        </div>
        <div class="modal-item">
          <div class="modal-key">Estado</div>
          <div class="modal-val"><span class="badge ${badgeClass(p.estado)}">${estadoLabel(p.estado)}</span></div>
        </div>
        <div class="modal-item">
          <div class="modal-key">Teléfono</div>
          <div class="modal-val">${p.telefono}</div>
        </div>
        <div class="modal-item">
          <div class="modal-key">Email</div>
          <div class="modal-val">${p.email}</div>
        </div>
        <div class="modal-item" style="grid-column: span 2;">
          <div class="modal-key">Alergias</div>
          ${alergiasHtml}
        </div>
      </div>
      <div class="modal-actions">
        <button class="btn-modal-historial" onclick="openHistorialModal(${p.id}); closeModal();">📋 Ver historial clínico</button>
      </div>
    `;
  }

  const overlay = safeGetElement('modalOverlay');
  if (overlay) {
    overlay.classList.add('open');
    overlay.setAttribute('aria-hidden', 'false');
    overlay.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    safeGetElement('modalClose')?.focus();
  }
};

const closeModal = () => {
  const overlay = safeGetElement('modalOverlay');
  if (overlay) {
    overlay.classList.remove('open');
    overlay.setAttribute('aria-hidden', 'true');
    overlay.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL HISTORIAL CLÍNICO
// ═══════════════════════════════════════════════════════════════════
const openHistorialModal = (id) => {
  const p = pacientes.find(a => a.id === id);
  if (!p) return;

  const title = safeGetElement('modalHistorialTitle');
  if (title) title.textContent = `Historial Clínico — ${p.nombre}`;

  const content = safeGetElement('modalHistorialContent');
  if (content) {
    const avatarColor = getAvatarColor(p.nombre);
    const initials = getInitials(p.nombre);
    const alergiasHtml = p.alergias && p.alergias.length
      ? p.alergias.map(a => `<span class="alerg-badge">⚠️ ${a}</span>`).join('')
      : '<span class="text-muted">Sin alergias registradas</span>';

    const historialHtml = p.historial && p.historial.length
      ? p.historial.map((h, i) => `
          <div class="hist-entry${i === 0 ? ' hist-entry--latest' : ''}">
            <div class="hist-entry-header">
              <div class="hist-date-wrap">
                <span class="hist-dot"></span>
                <time class="hist-date" datetime="${h.fechaISO}">${h.fecha}</time>
                ${i === 0 ? '<span class="hist-badge-reciente">Más reciente</span>' : ''}
              </div>
              <span class="hist-doctor">👨‍⚕️ ${h.doctor}</span>
            </div>
            <div class="hist-procedimiento">${h.procedimiento}</div>
            <div class="hist-notas">${h.notas}</div>
          </div>
        `).join('')
      : '<p class="text-muted" style="text-align:center;padding:20px;">Sin historial registrado.</p>';

    content.innerHTML = `
      <div class="hist-patient-header">
        <div class="modal-avatar" style="background:${avatarColor};width:48px;height:48px;font-size:1rem;" aria-hidden="true">${initials}</div>
        <div>
          <div class="hist-patient-name">${p.nombre}</div>
          <div class="hist-patient-cedula">${p.cedula} · ${p.telefono}</div>
        </div>
        <span class="badge ${badgeClass(p.estado)} hist-estado">${estadoLabel(p.estado)}</span>
      </div>

      <div class="hist-info-grid">
        <div class="hist-info-item">
          <div class="hist-info-label">Última consulta</div>
          <div class="hist-info-val">${p.ultima}</div>
        </div>
        <div class="hist-info-item">
          <div class="hist-info-label">Próxima cita</div>
          <div class="hist-info-val ${p.proxima === '—' ? 'danger' : ''}">${p.proxima === '—' ? 'Sin cita programada' : p.proxima}</div>
        </div>
        <div class="hist-info-item">
          <div class="hist-info-label">Servicio actual</div>
          <div class="hist-info-val">${p.servicio}</div>
        </div>
        <div class="hist-info-item">
          <div class="hist-info-label">Alergias</div>
          <div class="hist-alergias-wrap">${alergiasHtml}</div>
        </div>
      </div>

      <div class="hist-timeline-title">
        <span>📋</span> Registro de consultas
        <span class="hist-count">${p.historial ? p.historial.length : 0} registro${p.historial && p.historial.length !== 1 ? 's' : ''}</span>
      </div>
      <div class="hist-timeline">
        ${historialHtml}
      </div>
    `;
  }

  const overlay = safeGetElement('modalHistorialOverlay');
  if (overlay) {
    overlay.classList.add('open');
    overlay.setAttribute('aria-hidden', 'false');
    overlay.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    safeGetElement('modalHistorialClose')?.focus();
  }
};

const closeHistorialModal = () => {
  const overlay = safeGetElement('modalHistorialOverlay');
  if (overlay) {
    overlay.classList.remove('open');
    overlay.setAttribute('aria-hidden', 'true');
    overlay.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  CONTADORES ANIMADOS
// ═══════════════════════════════════════════════════════════════════
const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

const updateCounts = () => {
  animateCounter(safeGetElement('statTotal'), pacientes.length);
  animateCounter(safeGetElement('statActivos'), pacientes.filter(p => p.estado === 'activo').length);
  animateCounter(safeGetElement('statSeguimiento'), pacientes.filter(p => p.estado === 'seguimiento').length);
  animateCounter(safeGetElement('statAltas'), pacientes.filter(p => p.estado === 'alta').length);
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════
const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);
    if (show) { sidebar.querySelector('.nav-item')?.focus(); } else { hamburger.focus(); }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));
  sidebar.querySelectorAll('.nav-item').forEach(item =>
    item.addEventListener('click', () => { if (window.innerWidth <= 680) toggleMenu(false); })
  );
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) { e.preventDefault(); toggleMenu(false); }
  });
};

const initModals = () => {
  // Modal detalle
  safeGetElement('modalClose')?.addEventListener('click', closeModal);
  safeGetElement('modalOverlay')?.addEventListener('click', (e) => { if (e.target === e.currentTarget) closeModal(); });

  // Modal historial
  safeGetElement('modalHistorialClose')?.addEventListener('click', closeHistorialModal);
  safeGetElement('modalHistorialOverlay')?.addEventListener('click', (e) => { if (e.target === e.currentTarget) closeHistorialModal(); });

  // Escape cierra cualquier modal abierto
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (safeGetElement('modalHistorialOverlay')?.classList.contains('open')) { e.preventDefault(); closeHistorialModal(); }
      else if (safeGetElement('modalOverlay')?.classList.contains('open')) { e.preventDefault(); closeModal(); }
    }
  });
};

const initFilters = () => {
  safeGetElement('searchInput')?.addEventListener('input', debounce((e) => {
    fSearch = e.target.value;
    applyFilters();
  }, 250));

  safeGetElement('filterEstado')?.addEventListener('change', (e) => { fEstado = e.target.value; applyFilters(); });
  safeGetElement('filterAlergias')?.addEventListener('change', (e) => { fAlergias = e.target.value; applyFilters(); });
  safeGetElement('filterCita')?.addEventListener('change', (e) => { fCita = e.target.value; applyFilters(); });
  safeGetElement('filterServicio')?.addEventListener('change', (e) => { fServicio = e.target.value; applyFilters(); });

  safeGetElement('pageSizeSelect')?.addEventListener('change', (e) => {
    pageSize = e.target.value === 'all' ? 0 : Number(e.target.value);
    currentPage = 1;
    renderTable(filteredPacientes);
  });

  safeGetElement('btnClearFilters')?.addEventListener('click', () => {
    fSearch = ''; fEstado = ''; fAlergias = ''; fCita = ''; fServicio = '';
    const ids = ['searchInput', 'filterEstado', 'filterAlergias', 'filterCita', 'filterServicio'];
    ids.forEach(id => { const el = safeGetElement(id); if (el) el.value = ''; });
    safeGetElement('btnClearFilters')?.classList.remove('active');
    applyFilters();
  });

  safeGetElement('btnPrev')?.addEventListener('click', () => {
    if (currentPage > 1) { currentPage -= 1; renderTable(filteredPacientes); }
  });
  safeGetElement('btnNext')?.addEventListener('click', () => {
    const totalPages = pageSize === 0 ? 1 : Math.ceil(filteredPacientes.length / pageSize);
    if (currentPage < totalPages) { currentPage += 1; renderTable(filteredPacientes); }
  });
};

const MESES_PAC = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
const fmtFechaCorta = (iso) => {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
};
const fmtFechaLarga = (iso) => {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return `${String(d.getDate()).padStart(2, '0')} ${MESES_PAC[d.getMonth()]} ${d.getFullYear()}`;
};
const toISODate = (iso) => (iso ? String(iso).slice(0, 10) : null);

// Convierte el DTO de /historia-clinica/st-odo-06-pacientes/data (mismo formato que
// st-adm-historial) al modelo que usa esta vista: separa la última cita ya pasada
// de la próxima cita futura a partir del listado real de citas del paciente.
function normalizarPacienteOdo(p) {
  const ahora = new Date();
  const consultas = [...(p.consultas || [])].sort((a, b) => new Date(b.fecha) - new Date(a.fecha));
  const pasadas = consultas.filter(c => new Date(c.fecha) <= ahora);
  const futuras = consultas.filter(c => new Date(c.fecha) > ahora).sort((a, b) => new Date(a.fecha) - new Date(b.fecha));
  const ultimaConsulta = pasadas[0] || consultas[0] || null;
  const proximaConsulta = futuras[0] || null;

  return {
    id: p.id,
    nombre: p.nombre,
    cedula: p.cedula,
    ultima: fmtFechaCorta(ultimaConsulta?.fecha),
    ultimaISO: toISODate(ultimaConsulta?.fecha),
    servicio: ultimaConsulta?.proc || 'Sin servicio registrado',
    proxima: proximaConsulta ? fmtFechaCorta(proximaConsulta.fecha) : '—',
    proximaISO: toISODate(proximaConsulta?.fecha),
    estado: p.estado === 'inactivo' ? 'alta' : 'activo',
    alergias: p.alerta && p.alertaTexto ? p.alertaTexto.split(',').map(a => a.trim()).filter(Boolean) : [],
    telefono: p.telefono || '',
    email: p.email || '',
    historial: consultas.map(c => ({
      fecha: fmtFechaLarga(c.fecha),
      fechaISO: toISODate(c.fecha),
      procedimiento: c.proc,
      doctor: c.odo,
      notas: c.nota || ''
    }))
  };
}

async function fetchPacientes() {
  try {
    const resp = await fetch('/historia-clinica/st-odo-06-pacientes/data', { headers: { 'Accept': 'application/json' } });
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
    const data = await resp.json();
    return data.map(normalizarPacienteOdo);
  } catch (error) {
    console.error('No se pudieron cargar los pacientes desde el servidor:', error);
    showToast('No se pudieron cargar los pacientes', 'error');
    return [];
  }
}

const init = async () => {
  initSidebar();
  initModals();

  pacientes = await fetchPacientes();
  filteredPacientes = [...pacientes];

  populateServicioSelect();
  initFilters();
  renderTable(pacientes);
  updateCounts();
};

document.addEventListener('DOMContentLoaded', init);