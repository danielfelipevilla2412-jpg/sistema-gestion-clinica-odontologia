/**
 * SMILETRACK — GESTIÓN DE FACTURACIÓN (script.js)
 * API-ready + Accesibilidad + Persistencia fallback
 * Filtros funcionales + Drawer accesible + Fechas actualizadas
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

const invoicesStorage = {
  key: 'smiletrack_facturas_admin',
  
  load: () => {
    if (Array.isArray(window.RAZOR_INVOICES) && window.RAZOR_INVOICES.length > 0) {
      return window.RAZOR_INVOICES;
    }
    const stored = localStorage.getItem(invoicesStorage.key);
    if (stored) {
      try { return JSON.parse(stored); }
      catch (e) { console.warn('Error al cargar facturas, usando datos de ejemplo'); }
    }
    // Datos de ejemplo con estados y fechas actualizadas
    return [
      { id: 1, number: 'FAC-2026-001', patient: 'Marco Antonio Solís', doc: '10293-A', date: '2026-05-12', total: 1200000, pending: 450000, status: 'parcial', avatar: 'MA', color: 'blue', history: [
        { date: '2026-05-10', type: 'abono', amount: 400000, note: 'Abono parcial recibido' },
        { date: '2026-05-11', type: 'recordatorio', note: 'Email automático enviado' }
      ]},
      { id: 2, number: 'FAC-2026-002', patient: 'Elena Rodríguez', doc: '10294-B', date: '2026-05-14', total: 850000, pending: 850000, status: 'pendiente', avatar: 'ER', color: 'green', history: [] },
      { id: 3, number: 'FAC-2026-003', patient: 'Juan Sebastian', doc: '10295-C', date: '2026-05-15', total: 2100000, pending: 1050000, status: 'parcial', avatar: 'JS', color: 'purple', history: [
        { date: '2026-05-13', type: 'abono', amount: 1050000, note: 'Primer pago recibido' }
      ]},
      { id: 4, number: 'FAC-2026-004', patient: 'Laura Pineda', doc: '10296-D', date: '2026-05-16', total: 560000, pending: 0, status: 'pagada', avatar: 'LP', color: 'orange', history: [
        { date: '2026-05-16', type: 'pago', amount: 560000, note: 'Pago completo recibido' }
      ]},
      { id: 5, number: 'FAC-2026-005', patient: 'Carlos Vega', doc: '10297-E', date: '2026-05-17', total: 320000, pending: 320000, status: 'anulada', avatar: 'CV', color: 'red', history: [
        { date: '2026-05-17', type: 'anulacion', note: 'Factura anulada por solicitud del paciente' }
      ]}
    ];
  },
  
  save: (data) => {
    try { localStorage.setItem(invoicesStorage.key, JSON.stringify(data)); return true; }
    catch (e) { console.error('Error al guardar facturas:', e); return false; }
  },
  
  addInvoice: (invoice) => {
    const data = invoicesStorage.load();
    invoice.id = data.length > 0 ? Math.max(...data.map(i => i.id)) + 1 : 1;
    invoice.history = invoice.history || [];
    data.unshift(invoice);
    invoicesStorage.save(data);
    return invoice;
  },
  
  getInvoice: (id) => invoicesStorage.load().find(i => i.id === id),
  
  updateInvoice: (id, updates) => {
    const data = invoicesStorage.load();
    const idx = data.findIndex(i => i.id === id);
    if (idx !== -1) {
      data[idx] = { ...data[idx], ...updates };
      invoicesStorage.save(data);
      return true;
    }
    return false;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO
// ═══════════════════════════════════════════════════════════════════

let invoices = invoicesStorage.load();
let searchQuery = '';
let filterStatus = '';
let filterMonth = '';
let currentPage = 1;
const itemsPerPage = 10;

const avatarColors = {
  blue: 'bg-blue-100 text-blue-600', green: 'bg-green-100 text-green-600',
  purple: 'bg-purple-100 text-purple-600', orange: 'bg-orange-100 text-orange-600', red: 'bg-red-100 text-red-600'
};

const statusLabels = {
  pagada: { label: 'Pagada', class: 'pagada' },
  pendiente: { label: 'Pendiente', class: 'pendiente' },
  parcial: { label: 'Parcial', class: 'parcial' },
  anulada: { label: 'Anulada', class: 'anulada' }
};

// ═══════════════════════════════════════════════════════════════════
//  FILTRADO COMBINADO
// ═══════════════════════════════════════════════════════════════════

const getFilteredInvoices = () => {
  return invoices.filter(i => {
    // Filtro por búsqueda (número o paciente)
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      if (!i.number.toLowerCase().includes(query) && !i.patient.toLowerCase().includes(query) && !i.doc.includes(query)) return false;
    }
    
    // Filtro por estado
    if (filterStatus && i.status !== filterStatus) return false;
    
    // Filtro por mes
    if (filterMonth && !i.date.startsWith(filterMonth)) return false;
    
    return true;
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: TABLA DE FACTURAS
// ═══════════════════════════════════════════════════════════════════

const renderInvoices = () => {
  const body = safeGetElement('invoicesBody');
  if (!body) return;
  
  const filtered = getFilteredInvoices();
  
  if (!filtered.length) {
    body.innerHTML = '<div class="empty-state" role="status">No se encontraron facturas con los criterios de búsqueda.</div>';
    return;
  }
  
  // Paginación
  const start = (currentPage - 1) * itemsPerPage;
  const pageData = filtered.slice(start, start + itemsPerPage);
  
  body.innerHTML = pageData.map(i => {
    const status = statusLabels[i.status] || statusLabels.pendiente;
    const pendingDisplay = i.pending > 0 ? fmtCurrency(i.pending) : '—';
    const pendingClass = i.pending > 0 ? 'text-[var(--red)]' : 'text-[var(--green)]';
    
    return `
      <div class="table-row" role="row" tabindex="0" aria-label="Factura ${i.number} de ${i.patient}" data-id="${i.id}">
        <div class="table-col col-numero" role="cell" data-label="N° Factura"><strong class="text-[var(--primary)]">${i.number}</strong></div>
        <div class="table-col col-paciente" role="cell" data-label="Paciente">
          <div class="patient-info">
            <div class="patient-avatar ${avatarColors[i.color] || avatarColors.blue}" aria-hidden="true">${i.avatar}</div>
            <div>
              <span class="patient-name">${i.patient}</span>
              <span class="patient-id">ID: ${i.doc}</span>
            </div>
          </div>
        </div>
        <div class="table-col col-fecha" role="cell" data-label="Fecha"><time datetime="${i.date}">${fmtDate(i.date)}</time></div>
        <div class="table-col col-total" role="cell" data-label="Total">${fmtCurrency(i.total)}</div>
        <div class="table-col col-pendiente" role="cell" data-label="Pendiente"><strong class="${pendingClass}">${pendingDisplay}</strong></div>
        <div class="table-col col-estado text-center" role="cell" data-label="Estado">
          <span class="status-badge ${status.class}" role="status" aria-label="Estado: ${status.label}">${status.label}</span>
        </div>
        <div class="table-col col-acciones text-right" role="cell" data-label="Acciones">
          <div class="actions-cell">
            <button class="action-btn btn-view" aria-label="Ver detalle de factura ${i.number}" data-id="${i.id}" title="Ver">👁️</button>
            <button class="action-btn btn-email" aria-label="Enviar recordatorio de factura ${i.number}" data-id="${i.id}" title="Enviar email">📧</button>
          </div>
        </div>
      </div>
    `;
  }).join('');
  
  // Event listeners para acciones
  body.querySelectorAll('.btn-view').forEach(btn => {
    btn.addEventListener('click', (e) => openDrawer(parseInt(e.currentTarget.dataset.id)));
    btn.addEventListener('keydown', (e) => { if (['Enter',' '].includes(e.key)) { e.preventDefault(); openDrawer(parseInt(e.currentTarget.dataset.id)); }});
  });
  body.querySelectorAll('.btn-email').forEach(btn => {
    btn.addEventListener('click', (e) => sendReminder(parseInt(e.currentTarget.dataset.id)));
    btn.addEventListener('keydown', (e) => { if (['Enter',' '].includes(e.key)) { e.preventDefault(); sendReminder(parseInt(e.currentTarget.dataset.id)); }});
  });
  
  // Click en fila abre drawer
  body.querySelectorAll('.table-row').forEach(row => {
    row.addEventListener('click', (e) => {
      if (!e.target.closest('.action-btn')) {
        const id = parseInt(row.dataset.id);
        openDrawer(id);
      }
    });
  });
  
  updatePagination(filtered.length);
};

// ═══════════════════════════════════════════════════════════════════
//  DRAWER: DETALLE DE FACTURA
// ═══════════════════════════════════════════════════════════════════

const openDrawer = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;
  
  const drawer = safeGetElement('drawerOverlay');
  const statusLabel = safeGetElement('drawerStatus');
  const subtotalEl = safeGetElement('drawerSubtotal');
  const taxEl = safeGetElement('drawerTax');
  const totalEl = safeGetElement('drawerTotal');
  const historyList = safeGetElement('drawerHistory');
  
  if (!drawer || !statusLabel) return;
  
  // Actualizar estado
  const status = statusLabels[invoice.status] || statusLabels.pendiente;
  statusLabel.textContent = status.label;
  statusLabel.className = `status-label ${status.class === 'pagada' ? 'text-[var(--green)]' : status.class === 'pendiente' ? 'text-[var(--red)]' : 'text-[var(--orange)]'}`;
  
  // Calcular subtotal e IVA
  const subtotal = invoice.total - (invoice.total * 0.19);
  const tax = invoice.total - subtotal;
  
  if (subtotalEl) subtotalEl.textContent = fmtCurrency(subtotal);
  if (taxEl) taxEl.textContent = fmtCurrency(tax);
  if (totalEl) totalEl.textContent = fmtCurrency(invoice.total);
  
  // Renderizar historial
  if (historyList) {
    if (invoice.history?.length) {
      historyList.innerHTML = invoice.history.map(h => `
        <li class="history-item" role="listitem">
          <span class="history-title">${h.note}</span>
          <span class="history-date"><time datetime="${h.date}">${fmtDate(h.date)}</time>${h.amount ? ` · ${fmtCurrency(h.amount)}` : ''}</span>
        </li>
      `).join('');
    } else {
      historyList.innerHTML = '<li class="history-item" style="border:none;padding:0"><span class="text-muted">Sin historial registrado</span></li>';
    }
  }
  
  // Mostrar drawer
  drawer.classList.add('open');
  drawer.setAttribute('aria-hidden', 'false');
  drawer.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
  
  // Enfocar botón de cerrar
  const closeBtn = safeGetElement('drawerClose');
  if (closeBtn) closeBtn.focus();
};

const closeDrawer = () => {
  const drawer = safeGetElement('drawerOverlay');
  if (drawer) {
    drawer.classList.remove('open');
    drawer.setAttribute('aria-hidden', 'true');
    drawer.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

// Enviar recordatorio de pago
const sendReminder = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;
  
  // Simular envío de email
  showToast(`📧 Recordatorio enviado a ${invoice.patient}`, 'success');
  
  // Agregar al historial
  const data = invoicesStorage.load();
  const idx = data.findIndex(i => i.id === id);
  if (idx !== -1) {
    data[idx].history = data[idx].history || [];
    data[idx].history.unshift({
      date: new Date().toISOString().split('T')[0],
      type: 'recordatorio',
      note: 'Recordatorio enviado por email'
    });
    invoicesStorage.save(data);
    invoices = data;
  }
};

// Registrar pago
const registerPayment = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;
  
  if (invoice.pending <= 0) {
    showToast('⚠️ Esta factura ya está pagada', 'warning');
    return;
  }
  
  // Simular registro de pago
  const paymentAmount = invoice.pending;
  invoicesStorage.updateInvoice(id, { pending: 0, status: 'pagada' });
  
  // Agregar al historial
  const data = invoicesStorage.load();
  const idx = data.findIndex(i => i.id === id);
  if (idx !== -1) {
    data[idx].history = data[idx].history || [];
    data[idx].history.unshift({
      date: new Date().toISOString().split('T')[0],
      type: 'pago',
      amount: paymentAmount,
      note: 'Pago completo registrado'
    });
    invoicesStorage.save(data);
    invoices = data;
  }
  
  renderInvoices();
  updateStats();
  closeDrawer();
  showToast(`✅ Pago de ${fmtCurrency(paymentAmount)} registrado para ${invoice.patient}`);
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN Y CONTADORES
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

const animateCounter = (el, target, isCurrency = false) => {
  if (!el) return;
  let current = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const timer = setInterval(() => {
    current = Math.min(current + step, target);
    el.textContent = isCurrency ? fmtCurrency(current) : current;
    if (current >= target) clearInterval(timer);
  }, 30);
};

const updateStats = () => {
  const total = invoices.length;
  const pending = invoices.reduce((sum, i) => sum + i.pending, 0);
  const paidToday = invoices.filter(i => {
    if (i.status !== 'pagada') return false;
    const today = new Date().toISOString().split('T')[0];
    return i.history?.some(h => h.date === today && h.type === 'pago');
  }).length;
  const cancelled = invoices.filter(i => i.status === 'anulada').length;
  
  animateCounter(safeGetElement('statTotal'), total);
  animateCounter(safeGetElement('statPending'), pending, true);
  animateCounter(safeGetElement('statPaidToday'), paidToday);
  animateCounter(safeGetElement('statCancelled'), cancelled);
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

const initSearch = () => {
  const searchInput = safeGetElement('searchInvoices');
  searchInput?.addEventListener('input', debounce((e) => {
    searchQuery = e.target.value.toLowerCase();
    currentPage = 1;
    renderInvoices();
  }, 250));
};

const initFilters = () => {
  const filterStatusEl = safeGetElement('filterStatus');
  const filterMonthEl = safeGetElement('filterMonth');
  
  filterStatusEl?.addEventListener('change', (e) => {
    filterStatus = e.target.value;
    currentPage = 1;
    renderInvoices();
  });
  
  filterMonthEl?.addEventListener('change', (e) => {
    filterMonth = e.target.value;
    currentPage = 1;
    renderInvoices();
  });
};

const initPagination = () => {
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  
  btnPrev?.addEventListener('click', () => {
    if (currentPage > 1) { currentPage--; renderInvoices(); }
  });
  
  btnNext?.addEventListener('click', () => {
    const filtered = getFilteredInvoices();
    const totalPages = Math.ceil(filtered.length / itemsPerPage);
    if (currentPage < totalPages) { currentPage++; renderInvoices(); }
  });
};

const initDrawer = () => {
  const drawer = safeGetElement('drawerOverlay');
  const drawerClose = safeGetElement('drawerClose');
  const btnRemind = safeGetElement('btnRemind');
  const btnRegister = safeGetElement('btnRegisterPayment');
  
  // Cerrar drawer
  drawerClose?.addEventListener('click', closeDrawer);
  drawer?.addEventListener('click', (e) => { if (e.target === drawer) closeDrawer(); });
  
  // Botones del drawer
  btnRemind?.addEventListener('click', () => {
    const id = parseInt(drawer.dataset.invoiceId);
    if (id) sendReminder(id);
  });
  
  btnRegister?.addEventListener('click', () => {
    const id = parseInt(drawer.dataset.invoiceId);
    if (id) registerPayment(id);
  });
  
  // Escape cierra drawer
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && drawer?.classList.contains('open')) {
      e.preventDefault();
      closeDrawer();
    }
  });
};

const initNewInvoice = () => {
  const btn = safeGetElement('btnNewInvoice');
  btn?.addEventListener('click', () => showToast('📝 Funcionalidad de nueva factura en desarrollo', 'warning'));
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
// ═══════════════════════════════════════════════════════════════════

async function fetchInvoices() {
  try {
    // const res = await fetch(`${API_BASE}/admin/invoices`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    return invoicesStorage.load();
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return invoicesStorage.load();
  }
}

async function addInvoiceAPI(invoice) {
  try {
    // const res = await fetch(`${API_BASE}/admin/invoices`, {
    //   method: 'POST', headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(invoice),
    // });
    // if (!res.ok) throw new Error('Add failed');
    // return await res.json();
    return invoicesStorage.addInvoice(invoice);
  } catch (error) {
    console.warn('Error al agregar factura en API:', error);
    return null;
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initSearch();
  initFilters();
  initPagination();
  initNewInvoice();
  initDrawer();
  
  invoices = await fetchInvoices();
  updateStats();
  renderInvoices();
  
  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init);