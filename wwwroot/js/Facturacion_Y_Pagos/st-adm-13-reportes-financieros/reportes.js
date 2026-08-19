/**
 * SMILETRACK — REPORTES FINANCIEROS (script.js)
 * API-ready + Accesibilidad + Persistencia fallback
 * Gráficos accesibles con ARIA + filtros funcionales + exportación simulada
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

const fmtCurrency = (amount) => {
  if (typeof amount === 'string') return amount;
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(amount);
};

const fmtDate = (iso) => {
  if (!iso) return '—';
  const [y, m, d] = iso.split('-');
  const meses = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
  return `${parseInt(d)} ${meses[parseInt(m)-1]} ${y}`;
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON LOCALSTORAGE
// ═══════════════════════════════════════════════════════════════════

const financialStorage = {
  key: 'smiletrack_reportes_financieros',
  
  load: () => {
    if (window.RAZOR_FINANCIAL_REPORT && Array.isArray(window.RAZOR_FINANCIAL_REPORT.transactions)) {
      return window.RAZOR_FINANCIAL_REPORT;
    }
    const stored = localStorage.getItem(financialStorage.key);
    if (stored) {
      try { return JSON.parse(stored); }
      catch (e) { console.warn('Error al cargar reportes, usando datos de ejemplo'); }
    }
    // Datos de ejemplo con fechas actualizadas a mayo 2026
    return {
      kpis: { income: 18400000, received: 14100000, pending: 4200000, margin: 78 },
      barChart: [
        { month: 'Dic', value: 12000000, label: '$12M' },
        { month: 'Ene', value: 14000000, label: '$14M' },
        { month: 'Feb', value: 11000000, label: '$11M' },
        { month: 'Mar', value: 15000000, label: '$15M' },
        { month: 'Abr', value: 16000000, label: '$16M' },
        { month: 'May', value: 18400000, label: '$18.4M', active: true }
      ],
      donutChart: [
        { label: 'Ortodoncia', value: 38, color: '#2563eb' },
        { label: 'Blanqueamiento', value: 26, color: '#16a34a' },
        { label: 'Cirugía', value: 18, color: '#d97706' },
        { label: 'Otros', value: 18, color: '#7c3aed' }
      ],
      transactions: [
        { id: 1, number: 'TXN-2026-891', patient: 'Julián Restrepo', service: 'Limpieza Dental', date: '2026-05-15', amount: 85000, status: 'pagado', avatar: 'JR', color: 'blue' },
        { id: 2, number: 'TXN-2026-892', patient: 'Lucía Torres', service: 'Ortodoncia Mes 6', date: '2026-05-20', amount: 1200000, status: 'pendiente', avatar: 'LT', color: 'green' },
        { id: 3, number: 'TXN-2026-893', patient: 'Mariana Esparza', service: 'Endodoncia', date: '2026-05-02', amount: 2100000, status: 'pagado', avatar: 'ME', color: 'purple' },
        { id: 4, number: 'TXN-2026-894', patient: 'Sebastián Correa', service: 'Extracción', date: '2026-05-10', amount: 180000, status: 'anulado', avatar: 'SC', color: 'red' },
        { id: 5, number: 'TXN-2026-895', patient: 'Laura Pineda', service: 'Blanqueamiento LED', date: '2026-05-22', amount: 560000, status: 'pagado', avatar: 'LP', color: 'orange' }
      ]
    };
  },
  
  save: (data) => {
    try { localStorage.setItem(financialStorage.key, JSON.stringify(data)); return true; }
    catch (e) { console.error('Error al guardar reportes:', e); return false; }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO
// ═══════════════════════════════════════════════════════════════════

let data = financialStorage.load();
let searchQuery = '';
let filterMonth = '';
let currentPage = 1;
const itemsPerPage = 5;

const avatarColors = {
  blue: 'bg-blue-100 text-blue-600', green: 'bg-green-100 text-green-600',
  purple: 'bg-purple-100 text-purple-600', orange: 'bg-orange-100 text-orange-600', red: 'bg-red-100 text-red-600'
};

const statusLabels = {
  pagado: { label: 'Pagado', class: 'pagado' },
  pendiente: { label: 'Pendiente', class: 'pendiente' },
  anulado: { label: 'Anulado', class: 'anulado' }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: KPIs CON ANIMACIÓN
// ═══════════════════════════════════════════════════════════════════

const animateCounter = (el, target, isCurrency = false, isPercent = false) => {
  if (!el) return;
  let current = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const timer = setInterval(() => {
    current = Math.min(current + step, target);
    if (isCurrency) el.textContent = fmtCurrency(current);
    else if (isPercent) el.textContent = `${current}%`;
    else el.textContent = current;
    if (current >= target) clearInterval(timer);
  }, 30);
};

const renderKPIs = () => {
  animateCounter(safeGetElement('statIncome'), data.kpis.income, true);
  animateCounter(safeGetElement('statReceived'), data.kpis.received, true);
  animateCounter(safeGetElement('statPending'), data.kpis.pending, true);
  animateCounter(safeGetElement('statMargin'), data.kpis.margin, false, true);
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: GRÁFICO DE BARRAS ACCESIBLE
// ═══════════════════════════════════════════════════════════════════

const renderBarChart = () => {
  const container = safeGetElement('barChart');
  const legend = safeGetElement('chart-legend');
  if (!container) return;
  
  const maxValue = Math.max(...data.barChart.map(d => d.value));
  
  container.innerHTML = data.barChart.map((d, i) => {
    const height = (d.value / maxValue) * 100;
    return `
      <div class="bar-item" role="listitem">
        <span class="bar-value" aria-hidden="true">${d.label}</span>
        <div class="bar ${d.active ? 'active' : ''}" 
             style="height:${height}%" 
             tabindex="0"
             role="graphics-symbol"
             aria-label="${d.month}: ${d.label} de ingresos"
             data-month="${d.month}"
             data-value="${d.value}">
        </div>
        <span class="bar-label">${d.month}</span>
      </div>
    `;
  }).join('');
  
  // Soporte keyboard para barras
  container.querySelectorAll('.bar').forEach(bar => {
    bar.addEventListener('keydown', (e) => {
      if (['Enter',' '].includes(e.key)) {
        e.preventDefault();
        const month = bar.dataset.month;
        const value = fmtCurrency(parseInt(bar.dataset.value));
        showToast(`📊 ${month}: ${value} de ingresos`);
      }
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: GRÁFICO DONUT ACCESIBLE
// ═══════════════════════════════════════════════════════════════════

const renderDonutChart = () => {
  const container = safeGetElement('donutChart');
  if (!container) return;
  
  // Generar conic-gradient dinámico
  let gradient = 'conic-gradient(';
  let start = 0;
  data.donutChart.forEach((item, i) => {
    const end = start + item.value;
    gradient += `${item.color} ${start}% ${end}%`;
    if (i < data.donutChart.length - 1) gradient += ', ';
    start = end;
  });
  gradient += ')';
  
  container.style.background = gradient;
  
  // Actualizar leyenda con valores dinámicos
  const legendItems = container.closest('.chart-donut')?.querySelectorAll('.legend-item');
  if (legendItems) {
    legendItems.forEach((item, i) => {
      const strong = item.querySelector('strong');
      if (strong && data.donutChart[i]) {
        strong.textContent = `${data.donutChart[i].value}%`;
      }
    });
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FILTRADO COMBINADO
// ═══════════════════════════════════════════════════════════════════

const getFilteredTransactions = () => {
  return data.transactions.filter(t => {
    // Filtro por búsqueda
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      if (!t.number.toLowerCase().includes(query) && !t.patient.toLowerCase().includes(query)) return false;
    }
    
    // Filtro por mes
    if (filterMonth && !t.date.startsWith(filterMonth)) return false;
    
    return true;
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: TABLA DE TRANSACCIONES
// ═══════════════════════════════════════════════════════════════════

const renderTransactions = () => {
  const body = safeGetElement('transactionsBody');
  if (!body) return;
  
  const filtered = getFilteredTransactions();
  
  if (!filtered.length) {
    body.innerHTML = '<div class="empty-state" role="status">No se encontraron transacciones con los criterios de búsqueda.</div>';
    return;
  }
  
  // Paginación
  const start = (currentPage - 1) * itemsPerPage;
  const pageData = filtered.slice(start, start + itemsPerPage);
  
  body.innerHTML = pageData.map(t => {
    const status = statusLabels[t.status] || statusLabels.pendiente;
    const amountClass = t.status === 'anulado' ? 'text-[var(--red)] line-through' : 
                       t.status === 'pendiente' ? 'text-[var(--orange)]' : 'text-[var(--green)]';
    
    return `
      <div class="table-row" role="row" tabindex="0" aria-label="Transacción ${t.number} de ${t.patient}">
        <div class="table-col col-numero" role="cell" data-label="N° Transacción"><strong class="text-[var(--primary)]">#${t.number}</strong></div>
        <div class="table-col col-paciente" role="cell" data-label="Paciente">
          <div class="patient-info">
            <div class="patient-avatar ${avatarColors[t.color] || avatarColors.blue}" aria-hidden="true">${t.avatar}</div>
            <span class="patient-name">${t.patient}</span>
          </div>
        </div>
        <div class="table-col col-servicio" role="cell" data-label="Servicio">${t.service}</div>
        <div class="table-col col-fecha" role="cell" data-label="Fecha"><time datetime="${t.date}">${fmtDate(t.date)}</time></div>
        <div class="table-col col-monto" role="cell" data-label="Monto"><strong class="${amountClass}">${fmtCurrency(t.amount)}</strong></div>
        <div class="table-col col-estado text-center" role="cell" data-label="Estado">
          <span class="status-badge ${status.class}" role="status" aria-label="Estado: ${status.label}">${status.label}</span>
        </div>
        <div class="table-col col-accion text-right" role="cell" data-label="Acción">
          <button class="action-btn btn-receipt" aria-label="Ver comprobante de ${t.number}" data-id="${t.id}" title="Ver comprobante">🧾</button>
        </div>
      </div>
    `;
  }).join('');
  
  // Event listeners para acciones
  body.querySelectorAll('.btn-receipt').forEach(btn => {
    btn.addEventListener('click', (e) => viewReceipt(parseInt(e.currentTarget.dataset.id)));
    btn.addEventListener('keydown', (e) => { if (['Enter',' '].includes(e.key)) { e.preventDefault(); viewReceipt(parseInt(e.currentTarget.dataset.id)); }});
  });
  
  updatePagination(filtered.length);
};

// Ver comprobante (simulado)
const viewReceipt = (id) => {
  const txn = data.transactions.find(t => t.id === id);
  if (!txn) return;
  showToast(`🧾 Comprobante de ${txn.number}: ${fmtCurrency(txn.amount)}`, 'success');
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN
// ═══════════════════════════════════════════════════════════════════

const updatePagination = (totalItems) => {
  const totalPages = Math.ceil(totalItems / itemsPerPage);
  const showing = Math.min(itemsPerPage, totalItems - (currentPage - 1) * itemsPerPage);
  
  const pageShowing = safeGetElement('pageShowing');
  const pageTotal = safeGetElement('pageTotal');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  
  if (pageShowing) pageShowing.textContent = showing;
  if (pageTotal) pageTotal.textContent = totalItems;
  if (btnPrev) btnPrev.disabled = currentPage === 1;
  if (btnNext) btnNext.disabled = currentPage >= totalPages;
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
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
    if (show) { const firstLink = sidebar.querySelector('.nav-item'); if (firstLink) firstLink.focus(); }
    else { hamburger.focus(); }
  };
  
  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));
  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => { if (window.innerWidth <= 680) toggleMenu(false); });
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && sidebar.classList.contains('open')) { e.preventDefault(); toggleMenu(false); }});
};

const initCharts = () => {
  renderBarChart();
  renderDonutChart();
  
  // Filtro de año para gráfico de barras
  const filterYear = safeGetElement('filterYear');
  filterYear?.addEventListener('change', (e) => {
    // En producción: fetch de datos del año seleccionado
    showToast(`📊 Cargando datos de ${e.target.value}...`, 'success');
  });
};

const initSearch = () => {
  const searchInput = safeGetElement('searchTransactions');
  searchInput?.addEventListener('input', debounce((e) => {
    searchQuery = e.target.value.toLowerCase();
    currentPage = 1;
    renderTransactions();
  }, 250));
};

const initFilters = () => {
  const filterMonthEl = safeGetElement('filterMonth');
  filterMonthEl?.addEventListener('change', (e) => {
    filterMonth = e.target.value;
    currentPage = 1;
    renderTransactions();
  });
};

const initPagination = () => {
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  
  btnPrev?.addEventListener('click', () => {
    if (currentPage > 1) { currentPage--; renderTransactions(); }
  });
  
  btnNext?.addEventListener('click', () => {
    const filtered = getFilteredTransactions();
    const totalPages = Math.ceil(filtered.length / itemsPerPage);
    if (currentPage < totalPages) { currentPage++; renderTransactions(); }
  });
};

const initExport = () => {
  const btn = safeGetElement('btnExport');
  btn?.addEventListener('click', () => {
    showToast('📥 Generando Excel... (simulado)', 'success');
    // En producción: window.open(`${API_BASE}/admin/reports/financial/export?format=xlsx`)
  });
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
// ═══════════════════════════════════════════════════════════════════

async function fetchFinancialReports() {
  try {
    // const res = await fetch(`${API_BASE}/admin/reports/financial`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    return financialStorage.load();
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return financialStorage.load();
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initCharts();
  initSearch();
  initFilters();
  initPagination();
  initExport();
  
  data = await fetchFinancialReports();
  renderKPIs();
  renderTransactions();
  
  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init);