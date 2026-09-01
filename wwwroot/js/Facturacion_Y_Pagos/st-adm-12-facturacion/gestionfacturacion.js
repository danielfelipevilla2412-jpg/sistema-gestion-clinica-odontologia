/**
 * SMILETRACK — GESTIÓN DE FACTURACIÓN (script.js)
 * API-ready + Accesibilidad + Persistencia fallback
 * Filtros funcionales + Drawer accesible + Fechas actualizadas
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api/facturas';

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
//  FUENTE DE DATOS: SQL Server (renderizado por el servidor) + API REST
//  para las acciones de escritura (pago/anulación). Ya no se usa
//  localStorage como fuente de verdad de las facturas.
// ═══════════════════════════════════════════════════════════════════

const invoicesStorage = {

  // Los datos vienen siempre del servidor (FacturacionPagosController ->
  // AppDbContext.Facturas), serializados en ViewData["FacturasJson"].
  load: () => Array.isArray(window.RAZOR_INVOICES) ? window.RAZOR_INVOICES : [],
  
  getInvoice: (id) => invoicesStorage.load().find(i => i.id === id),
  

   // Actualiza solo el estado en memoria para reflejar de inmediato el
   // resultado de una llamada a la API; la próxima recarga de página
   // siempre traerá el estado real desde SQL Server.
  updateLocalCache: (id, updates) => {
    const idx = invoices.findIndex(i => i.id === id);
    if (idx !== -1) {
      invoices[idx] = { ...invoices[idx], ...updates };
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
  blue: 'avatar-blue', green: 'avatar-green',
  purple: 'avatar-purple', orange: 'avatar-orange', red: 'avatar-red'
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
    const patientInitials = String(i.patient || '')
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(namePart => namePart.charAt(0).toUpperCase())
      .join('') || 'P';
    const pendingDisplay = i.pending > 0 ? fmtCurrency(i.pending) : '—';
    const pendingClass = i.pending > 0 ? 'text-[var(--red)]' : 'text-[var(--green)]';
    
    return `
      <div class="table-row" role="row" tabindex="0" aria-label="Factura ${i.number} de ${i.patient}" data-id="${i.id}">
        <div class="table-col col-numero" role="cell" data-label="N° Factura"><strong class="text-[var(--primary)]">${i.number}</strong></div>
        <div class="table-col col-paciente" role="cell" data-label="Paciente">
          <div class="patient-info">
            <div class="patient-avatar ${avatarColors[i.color] || avatarColors.blue}" aria-hidden="true">${patientInitials}</div>
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
            <button class="action-btn btn-view" aria-label="Ver detalle de factura ${i.number}" data-id="${i.id}" title="Ver">👁️ <span class="btn-text">Ver</span></button>
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
  
  // Mostrar drawer
  drawer.dataset.invoiceId = String(id);
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
// Enviar recordatorio de pago (funcionalidad de notificación, no persiste
// estado de negocio; el envío real de correo se apoya en el EmailService
// del backend cuando esté disponible para este flujo).
   const sendReminder = (id) => {
   const invoice = invoicesStorage.getInvoice(id);
   if (!invoice) return;

const closePaymentSuccess = () => {
  const modal = safeGetElement('paymentSuccessOverlay');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

const closeInvoiceDetails = () => {
  const modal = safeGetElement('invoiceDetailsOverlay');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
};

const showInvoiceDetails = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  const modal = safeGetElement('invoiceDetailsOverlay');
  if (!invoice || !modal) return;

  const details = {
    invoiceDetailsNumber: invoice.number,
    invoiceDetailsPatient: invoice.patient,
    invoiceDetailsDocument: invoice.doc,
    invoiceDetailsDate: fmtDate(invoice.date),
    invoiceDetailsService: invoice.service || 'No especificado',
    invoiceDetailsStatus: (statusLabels[invoice.status] || statusLabels.pendiente).label,
    invoiceDetailsTotal: fmtCurrency(invoice.total),
    invoiceDetailsPending: invoice.pending > 0 ? fmtCurrency(invoice.pending) : 'Pagada'
  };
  Object.entries(details).forEach(([elementId, value]) => {
    const element = safeGetElement(elementId);
    if (element) element.textContent = value;
  });

  closeDrawer();
  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
  safeGetElement('invoiceDetailsConfirm')?.focus();
};

const showPaymentSuccess = (amount, patient) => {
  const modal = safeGetElement('paymentSuccessOverlay');
  const message = safeGetElement('paymentSuccessMessage');
  if (!modal) return;

  if (message) message.textContent = `El pago de ${fmtCurrency(amount)} de ${patient} se registró correctamente.`;
  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
  safeGetElement('paymentSuccessConfirm')?.focus();
};

// Enviar recordatorio de pago
const sendReminder = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;

  showToast(`📧 Recordatorio enviado a ${invoice.patient}`, 'success');
  
};

// Registrar pago — llama a la API real (PUT /api/facturas/{id}/pagos)
// que persiste el pago en SQL Server (tabla Factura: monto_pagado, estado, fecha_pago).
const registerPayment = async (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return; 
  
  if (invoice.pending <= 0) {
    showToast('⚠️ Esta factura ya está pagada', 'warning');
    return;
  }
  
  
  const paymentAmount = invoice.pending;
  
  
  
      try {
      const result = await window.apiRequest(`/api/facturas/${id}/pagos`, {
      method: 'PUT',
      body: { montoPagado: paymentAmount }
    });
    if (!result || result.success !== true) {
      showToast(result?.message || 'No fue posible registrar el pago.', 'error');
      return;
  }

  invoicesStorage.updateLocalCache(id, { pending: 0, status: result.data.estado });
  renderInvoices();
  updateStats();
  closeDrawer();
  showToast(`✅ Pago de ${fmtCurrency(paymentAmount)} registrado para ${invoice.patient}`);
} catch (error) {
    console.error('Error registrando pago:', error);
    showToast('Error de conexión al registrar el pago.', 'error');
  }
};

// Anular factura — llama a la API real (POST /api/facturas/{id}/anulacion).
  const cancelInvoice = async (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;

  if (invoice.status === 'anulada') {
    showToast('Esta factura ya está anulada.', 'warning');
    return;
  }

  if (!window.confirm(`¿Anular la factura ${invoice.number} de ${invoice.patient}? Esta acción no se puede deshacer.`)) {
    return;
  }

  try {
    const result = await window.apiRequest(`/api/facturas/${id}/anulacion`, {
      method: 'POST',
      body: { motivo: 'Anulada desde el panel administrativo' }
    });

    if (!result || result.success !== true) {
      showToast(result?.message || 'No fue posible anular la factura.', 'error');
      return;
    }

    invoicesStorage.updateLocalCache(id, { status: 'anulada' });
    renderInvoices();
    updateStats();
    closeDrawer();
    showToast(`Factura ${invoice.number} anulada correctamente.`);
  } catch (error) {
    console.error('Error anulando factura:', error);
    showToast('Error de conexión al anular la factura.', 'error');
  }

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
  const btnViewDetails = safeGetElement('btnViewDetails');
  const btnRegister = safeGetElement('btnRegisterPayment');
  const btnCancel = safeGetElement('btnCancelInvoice'); 
  const invoiceDetails = safeGetElement('invoiceDetailsOverlay');
  const invoiceDetailsClose = safeGetElement('invoiceDetailsClose');
  const invoiceDetailsConfirm = safeGetElement('invoiceDetailsConfirm');
  const paymentSuccess = safeGetElement('paymentSuccessOverlay');
  const paymentSuccessClose = safeGetElement('paymentSuccessClose');
  const paymentSuccessConfirm = safeGetElement('paymentSuccessConfirm');
  
  // Cerrar drawer
  drawerClose?.addEventListener('click', closeDrawer);
  drawer?.addEventListener('click', (e) => { if (e.target === drawer) closeDrawer(); });
  
  // Botones del drawer
  btnViewDetails?.addEventListener('click', () => {
    const id = parseInt(drawer.dataset.invoiceId);
    if (id) showInvoiceDetails(id);
  });
  
  btnRegister?.addEventListener('click', () => {
    const id = parseInt(drawer.dataset.invoiceId);
    if (id) registerPayment(id);
  });
    btnCancel?.addEventListener('click', () => {
    const id = parseInt(drawer.dataset.invoiceId);
    if (id) cancelInvoice(id);
  });
  

  paymentSuccessClose?.addEventListener('click', closePaymentSuccess);
  paymentSuccessConfirm?.addEventListener('click', closePaymentSuccess);
  paymentSuccess?.addEventListener('click', (e) => {
    if (e.target === paymentSuccess) closePaymentSuccess();
  });
  invoiceDetailsClose?.addEventListener('click', closeInvoiceDetails);
  invoiceDetailsConfirm?.addEventListener('click', closeInvoiceDetails);
  invoiceDetails?.addEventListener('click', (e) => {
    if (e.target === invoiceDetails) closeInvoiceDetails();
  });
  
  // Escape cierra drawer
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && drawer?.classList.contains('open')) {
      e.preventDefault();
      closeDrawer();
    }
    if (e.key === 'Escape' && paymentSuccess?.classList.contains('open')) {
      e.preventDefault();
      closePaymentSuccess();
    }
    if (e.key === 'Escape' && invoiceDetails?.classList.contains('open')) {
      e.preventDefault();
      closeInvoiceDetails();
    }
  });
};

const initNewInvoice = () => {
  const btn = safeGetElement('btnNewInvoice');
  const overlay = safeGetElement('newInvoiceOverlay');
  const form = safeGetElement('newInvoiceForm');
  const closeBtn = safeGetElement('newInvoiceClose');
  const cancelBtn = safeGetElement('newInvoiceCancel');
  const dateInput = safeGetElement('invoiceDate');

  // La creación real de facturas ocurre en el flujo de Recepción
  // (POST /api/facturas), que sí persiste en SQL Server.
  btn?.addEventListener('click', () => {
    window.location.href = '/facturacion-y-pagos/st-rec-04-generar-factura';
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && overlay?.classList.contains('open')) {
      e.preventDefault();
      closeModal();
    }
  });
};


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
  
  
  try {
    const response = await window.apiRequest('/api/facturas');
    if (response?.success && Array.isArray(response.data)) {
      invoices = response.data.map((f, idx) => ({
        id: f.id, number: f.numero, patient: f.paciente, doc: f.documento,
        date: String(f.fecha).slice(0, 10), total: f.total, pending: f.pendiente,
        status: f.estado, color: ['blue','green','purple','orange','red'][idx % 5],
        service: f.notas || 'Servicio Odontológico', history: []
      }));
    }
  } catch (error) {
    console.error('No fue posible cargar las facturas desde la API.', error);
    showToast('No fue posible cargar las facturas.', 'error');
  }
  updateStats();
  renderInvoices();
  
  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init)};}