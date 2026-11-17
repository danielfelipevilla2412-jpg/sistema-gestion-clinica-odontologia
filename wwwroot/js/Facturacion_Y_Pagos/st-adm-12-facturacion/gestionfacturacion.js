(function () {
'use strict';

/**
 * SMILETRACK — GESTIÓN DE FACTURACIÓN (script.js)
 * Facturación, filtros y diálogos de la vista administrativa.
 */

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

const translate = (key, vars) => window.I18N?.t(key, vars) || key;

const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, character => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;'
}[character]));

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
  const toastMethod = window.ToastService?.[type];
  if (typeof toastMethod !== 'function') {
    console.error('[SmileTrack] El servicio compartido de notificaciones no está disponible.');
    return;
  }
  const titleKey = type === 'error' ? 'billing.toast_error'
    : type === 'warning' ? 'billing.toast_warning'
      : type === 'success' ? 'billing.toast_success' : 'billing.toast_info';
  toastMethod(translate(titleKey), escapeHtml(message));
};

const fmtCurrency = (amount) => {
  if (typeof amount === 'string') return amount;
  const locale = window.I18N?.lang === 'en' ? 'en-US' : 'es-CO';
  return new Intl.NumberFormat(locale, { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(amount);
};

const fmtDate = (iso) => {
  if (!iso) return '—';
  const date = new Date(`${iso.slice(0, 10)}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return '—';
  const locale = window.I18N?.lang === 'en' ? 'en-US' : 'es-CO';
  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(date);
};

const getColombiaDate = () => {
  const dateParts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'America/Bogota',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  }).formatToParts(new Date()).reduce((parts, part) => {
    if (part.type !== 'literal') parts[part.type] = part.value;
    return parts;
  }, {});
  return `${dateParts.year}-${dateParts.month}-${dateParts.day}`;
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
  
  getInvoice: (id) => (Array.isArray(invoices) ? invoices.find(i => i.id === id) : null) || invoicesStorage.load().find(i => i.id === id),
  

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

const statusKeys = {
  pagada: 'billing.status_paid',
  pendiente: 'billing.status_pending',
  parcial: 'billing.status_partial',
  anulada: 'billing.status_voided'
};

const getStatus = (value) => {
  const status = String(value || '').toLowerCase();
  return {
    label: translate(statusKeys[status] || 'billing.status_pending'),
    class: statusKeys[status] ? status : 'pendiente'
  };
};

const dialogFocusTargets = new Map();
const counterTimers = new WeakMap();
let bodyOverflowBeforeDialog = '';

const openDialog = (dialog, initialFocus) => {
  if (!dialog) return;

  if (!dialog.classList.contains('open')) {
    if (dialogFocusTargets.size === 0) bodyOverflowBeforeDialog = document.body.style.overflow;
    dialogFocusTargets.set(dialog, document.activeElement instanceof HTMLElement ? document.activeElement : null);
  }
  dialog.classList.add('open');
  dialog.setAttribute('aria-hidden', 'false');
  dialog.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
  initialFocus?.focus();
};

const closeDialog = (dialog) => {
  if (!dialog?.classList.contains('open')) return;
  dialog.classList.remove('open');
  dialog.setAttribute('aria-hidden', 'true');
  dialog.setAttribute('inert', '');
  const returnTarget = dialogFocusTargets.get(dialog);
  dialogFocusTargets.delete(dialog);
  if (dialogFocusTargets.size === 0) document.body.style.overflow = bodyOverflowBeforeDialog;
  if (returnTarget?.isConnected) returnTarget.focus();
};

// ═══════════════════════════════════════════════════════════════════
//  FILTRADO COMBINADO
// ═══════════════════════════════════════════════════════════════════

const getFilteredInvoices = () => {
  return invoices.filter(i => {
    // Filtro por búsqueda (número o paciente)
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      const number = String(i.number || '').toLowerCase();
      const patient = String(i.patient || '').toLowerCase();
      const documentNumber = String(i.doc || '').toLowerCase();
      if (!number.includes(query) && !patient.includes(query) && !documentNumber.includes(query)) return false;
    }
    
    // Filtro por estado
    if (filterStatus && String(i.status || '').toLowerCase() !== filterStatus) return false;
    
    // Filtro por mes
    if (filterMonth && !String(i.date || '').startsWith(filterMonth)) return false;
    
    return true;
  });
};

const renderMonthOptions = () => {
  const select = document.getElementById('filterMonth');
  if (!select) return;

  const selectedMonth = filterMonth || select.value;
  const months = [...new Set(invoices
    .map(invoice => String(invoice.date || '').match(/^\d{4}-\d{2}/)?.[0])
    .filter(Boolean))].sort().reverse();
  const locale = window.I18N?.lang === 'en' ? 'en-US' : 'es-CO';

  select.replaceChildren();
  const allMonthsOption = document.createElement('option');
  allMonthsOption.value = '';
  allMonthsOption.textContent = translate('billing.all_months');
  select.appendChild(allMonthsOption);

  months.forEach(month => {
    const [year, monthNumber] = month.split('-').map(Number);
    const date = new Date(Date.UTC(year, monthNumber - 1, 1));
    const option = document.createElement('option');
    option.value = month;
    option.textContent = new Intl.DateTimeFormat(locale, {
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC'
    }).format(date);
    select.appendChild(option);
  });

  if (months.includes(selectedMonth)) {
    select.value = selectedMonth;
  } else {
    filterMonth = '';
    select.value = '';
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: TABLA DE FACTURAS
// ═══════════════════════════════════════════════════════════════════

const renderInvoices = () => {
  const body = safeGetElement('invoicesBody');
  if (!body) return;
  
  const filtered = getFilteredInvoices();
  currentPage = filtered.length
    ? Math.min(currentPage, Math.ceil(filtered.length / itemsPerPage))
    : 1;
  
  if (!filtered.length) {
    body.innerHTML = `
      <div class="empty-state" role="status">
        <span class="material-symbols-outlined empty-icon" aria-hidden="true">receipt_long</span>
        <h3 class="empty-state-title">${escapeHtml(translate('billing.empty_title'))}</h3>
        <p class="empty-state-text">${escapeHtml(translate(searchQuery || filterStatus || filterMonth ? 'billing.empty_filtered' : 'billing.empty_initial'))}</p>
      </div>
    `;
    updatePagination(0);
    return;
  }
  
  // Paginación
  const start = (currentPage - 1) * itemsPerPage;
  const pageData = filtered.slice(start, start + itemsPerPage);
  
  body.innerHTML = pageData.map(i => {
    const status = getStatus(i.status);
    const patientInitials = String(i.patient || '')
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(namePart => namePart.charAt(0).toUpperCase())
      .join('') || 'P';
    const pendingDisplay = i.pending > 0 ? fmtCurrency(i.pending) : '—';
    const pendingClass = i.pending > 0 ? 'amount-pending' : 'amount-paid';
    
    return `
      <div class="table-row" role="row" tabindex="0" aria-label="${escapeHtml(translate('billing.invoice_for', { number: i.number, patient: i.patient }))}" data-id="${escapeHtml(i.id)}">
        <div class="table-col col-numero" role="cell" data-label="${escapeHtml(translate('billing.invoice_number'))}"><strong>${escapeHtml(i.number)}</strong></div>
        <div class="table-col col-paciente" role="cell" data-label="${escapeHtml(translate('citas.patient'))}">
          <div class="patient-info">
            <div class="patient-avatar ${avatarColors[i.color] || avatarColors.blue}" aria-hidden="true">${escapeHtml(patientInitials)}</div>
            <div>
              <span class="patient-name">${escapeHtml(i.patient)}</span>
              <span class="patient-id">${escapeHtml(translate('billing.document_short'))}: ${escapeHtml(i.doc)}</span>
            </div>
          </div>
        </div>
        <div class="table-col col-fecha" role="cell" data-label="${escapeHtml(translate('facturacion.date'))}"><time datetime="${escapeHtml(i.date)}">${escapeHtml(fmtDate(i.date))}</time></div>
        <div class="table-col col-total" role="cell" data-label="${escapeHtml(translate('facturacion.total'))}">${escapeHtml(fmtCurrency(i.total))}</div>
        <div class="table-col col-pendiente" role="cell" data-label="${escapeHtml(translate('billing.pending_amount'))}"><strong class="${pendingClass}">${escapeHtml(pendingDisplay)}</strong></div>
        <div class="table-col col-estado text-center" role="cell" data-label="${escapeHtml(translate('facturacion.status'))}">
          <span class="status-badge ${escapeHtml(status.class)}" role="status" aria-label="${escapeHtml(translate('billing.status_aria', { status: status.label }))}">${escapeHtml(status.label)}</span>
        </div>
        <div class="table-col col-acciones text-right" role="cell" data-label="${escapeHtml(translate('table.actions'))}">
          <div class="actions-cell">
            <button type="button" class="action-btn btn-view" aria-label="${escapeHtml(translate('billing.view_invoice', { number: i.number }))}" data-id="${escapeHtml(i.id)}" title="${escapeHtml(translate('billing.view'))}">
              <span class="material-symbols-outlined action-icon" aria-hidden="true">visibility</span><span class="btn-text">${escapeHtml(translate('billing.view'))}</span>
            </button>
            <button type="button" class="action-btn btn-manage" aria-label="${escapeHtml(translate('billing.manage_invoice', { number: i.number }))}" data-id="${escapeHtml(i.id)}" title="${escapeHtml(translate('billing.manage'))}">
              <span class="material-symbols-outlined action-icon" aria-hidden="true">tune</span><span class="btn-text">${escapeHtml(translate('billing.manage'))}</span>
            </button>
          </div>
        </div>
      </div>
    `;
  }).join('');
  
  // Event listeners para acciones
  body.querySelectorAll('.btn-view').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      showInvoiceDetails(parseInt(e.currentTarget.dataset.id, 10));
    });
  });

  body.querySelectorAll('.btn-manage').forEach(btn => {
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      openDrawer(parseInt(e.currentTarget.dataset.id, 10));
    });
  });

  // Click en fila abre detalle
  body.querySelectorAll('.table-row').forEach(row => {
    row.addEventListener('click', (e) => {
      if (!e.target.closest('.action-btn')) {
        const id = parseInt(row.dataset.id, 10);
        row.focus();
        openDrawer(id);
      }
    });
    row.addEventListener('keydown', event => {
      if (event.target.closest('.action-btn') || !['Enter', ' '].includes(event.key)) return;
      event.preventDefault();
      openDrawer(parseInt(row.dataset.id, 10));
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
  const status = getStatus(invoice.status);
  statusLabel.textContent = status.label;
  statusLabel.className = `status-label ${status.class}`;
  const statusDot = drawer.querySelector('.status-dot');
  if (statusDot) statusDot.className = `status-dot ${status.class}`;
  
  // Calcular subtotal e IVA
  const subtotal = invoice.total - (invoice.total * 0.19);
  const tax = invoice.total - subtotal;
  
  if (subtotalEl) subtotalEl.textContent = fmtCurrency(subtotal);
  if (taxEl) taxEl.textContent = fmtCurrency(tax);
  if (totalEl) totalEl.textContent = fmtCurrency(invoice.total);
  
  // Mostrar drawer
  drawer.dataset.invoiceId = String(id);
  const closeBtn = safeGetElement('drawerClose');
  openDialog(drawer, closeBtn);
};

const closeDrawer = () => {
  closeDialog(safeGetElement('drawerOverlay'));
};
const closePaymentSuccess = () => {
  closeDialog(safeGetElement('paymentSuccessOverlay'));
};

const closeInvoiceDetails = () => {
  closeDialog(safeGetElement('invoiceDetailsOverlay'));
};

const showInvoiceDetails = async (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  const modal = safeGetElement('invoiceDetailsOverlay');
  if (!invoice || !modal) return;

  const details = {
    invoiceDetailsNumber: invoice.number,
    invoiceDetailsPatient: invoice.patient,
    invoiceDetailsDocument: invoice.doc,
    invoiceDetailsDate: fmtDate(invoice.date),
    invoiceDetailsService: invoice.service || translate('common.not_available'),
    invoiceDetailsStatus: getStatus(invoice.status).label,
    invoiceDetailsTotal: fmtCurrency(invoice.total),
    invoiceDetailsPaid: fmtCurrency(invoice.paid || 0),
    invoiceDetailsPending: invoice.pending > 0 ? fmtCurrency(invoice.pending) : translate('billing.status_paid')
  };
  Object.entries(details).forEach(([elementId, value]) => {
    const element = safeGetElement(elementId);
    if (element) element.textContent = value;
  });

  closeDrawer();
  openDialog(modal, safeGetElement('invoiceDetailsConfirm'));

  const historyList = safeGetElement('invoicePaymentHistory');
  if (!historyList) return;
  historyList.replaceChildren();
  const loading = document.createElement('li');
  loading.textContent = translate('billing.loading_payments');
  historyList.appendChild(loading);

  try {
    const response = await window.apiRequest(`/api/facturas/${id}`);
    if (!response?.success || !response.data) throw new Error(translate('billing.payment_history_error'));

    const data = response.data;
    const paid = safeGetElement('invoiceDetailsPaid');
    const pending = safeGetElement('invoiceDetailsPending');
    if (paid) paid.textContent = fmtCurrency(data.montoPagado);
    if (pending) pending.textContent = data.pendiente > 0 ? fmtCurrency(data.pendiente) : translate('billing.status_paid');

    historyList.replaceChildren();
    if (!data.pagos?.length) {
      const empty = document.createElement('li');
      empty.textContent = translate('billing.no_payments');
      historyList.appendChild(empty);
      return;
    }

    data.pagos.forEach((payment) => {
      const item = document.createElement('li');
      const locale = window.I18N?.lang === 'en' ? 'en-US' : 'es-CO';
      const date = new Date(payment.fechaUtc).toLocaleString(locale);
      item.textContent = `${date} · ${fmtCurrency(payment.monto)} · ${payment.metodoPago} · ${payment.estado} · ${payment.referencia}`;
      historyList.appendChild(item);
    });
  } catch {
    historyList.replaceChildren();
    const error = document.createElement('li');
    error.textContent = translate('billing.payment_history_error');
    historyList.appendChild(error);
  }
};

const showPaymentSuccess = (amount, patient) => {
  const modal = safeGetElement('paymentSuccessOverlay');
  const message = safeGetElement('paymentSuccessMessage');
  if (!modal) return;

  if (message) {
    message.textContent = translate('billing.payment_success_with_amount', {
      amount: fmtCurrency(amount),
      patient
    });
  }
  openDialog(modal, safeGetElement('paymentSuccessConfirm'));
};

// Enviar recordatorio de pago
const sendReminder = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;

  showToast(translate('billing.reminder_sent', { patient: invoice.patient }), 'success');
  
};

// Registrar pago — llama a la API real (POST /api/facturas/{id}/pagos)
// que persiste el pago en SQL Server (tabla Factura: monto_pagado, estado, fecha_pago).
const registerPayment = async (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return; 
  
  if (invoice.pending <= 0) {
    showToast(translate('billing.already_paid'), 'warning');
    return;
  }
  
  
  const paymentAmount = invoice.pending;
  
  
  
      try {
      const result = await window.apiRequest(`/api/facturas/${id}/pagos`, {
      method: 'POST',
      body: { montoPagado: paymentAmount, metodoPago: 'efectivo' }
    });
    if (!result || result.success !== true) {
      showToast(result?.message || translate('billing.payment_error'), 'error');
      return;
  }

  invoicesStorage.updateLocalCache(id, {
    paid: result.data.montoPagado,
    pending: result.data.pendiente,
    status: result.data.estado,
    history: (result.data.pagos || []).map(payment => ({
      date: String(payment.fechaUtc).slice(0, 10),
      type: 'pago'
    }))
  });
  renderInvoices();
  updateStats();
  closeDrawer();
  showPaymentSuccess(paymentAmount, invoice.patient);
} catch (error) {
    console.error('Error registrando pago:', error);
    showToast(translate('billing.payment_connection_error'), 'error');
  }
};

// Anular factura — llama a la API real (POST /api/facturas/{id}/anulacion).
const cancelInvoice = (id) => {
  const invoice = invoicesStorage.getInvoice(id);
  if (!invoice) return;

  if (String(invoice.status).toLowerCase() === 'anulada') {
    showToast(translate('billing.already_voided'), 'warning');
    return;
  }

  if (typeof window.ModalService?.confirm !== 'function') {
    console.error('[SmileTrack] El servicio compartido de confirmación no está disponible.');
    showToast(translate('billing.confirmation_unavailable'), 'error');
    return;
  }

  window.ModalService.confirm({
    title: translate('billing.void_confirmation_title'),
    message: escapeHtml(translate('billing.void_confirmation_message', {
      number: invoice.number,
      patient: invoice.patient
    })),
    confirmText: translate('billing.void_invoice'),
    cancelText: translate('common.cancel'),
    isDanger: true,
    onConfirm: async () => {
      try {
        const result = await window.apiRequest(`/api/facturas/${id}/anulacion`, {
          method: 'POST',
          body: { motivo: 'Anulada desde el panel administrativo' }
        });

        if (!result || result.success !== true) {
          showToast(result?.message || translate('billing.void_error'), 'error');
          return;
        }

        invoicesStorage.updateLocalCache(id, { status: 'anulada' });
        renderInvoices();
        updateStats();
        closeDrawer();
        showToast(translate('billing.void_success', { number: invoice.number }));
      } catch (error) {
        console.error('Error anulando la factura:', error);
        showToast(translate('billing.void_connection_error'), 'error');
      }
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  PAGINACIÓN Y CONTADORES
// ═══════════════════════════════════════════════════════════════════

const updatePagination = (totalItems) => {
  const totalPages = Math.ceil(totalItems / itemsPerPage);
  currentPage = totalPages ? Math.min(currentPage, totalPages) : 1;
  const showing = Math.max(0, Math.min(itemsPerPage, totalItems - (currentPage - 1) * itemsPerPage));

  const pageShowing = safeGetElement('pageShowing');
  const pageTotal = safeGetElement('pageTotal');
  const pagination = safeGetElement('invoicePagination');

  if (pageShowing) pageShowing.textContent = showing;
  if (pageTotal) pageTotal.textContent = totalItems;
  if (!pagination) return;

  const pageButtons = Array.from({ length: totalPages }, (_, index) => {
    const page = index + 1;
    return `<button type="button" class="pagination-btn${page === currentPage ? ' active' : ''}" data-page="${page}"${page === currentPage ? ' aria-current="page"' : ''} aria-label="${escapeHtml(translate('billing.page_number', { page }))}">${page}</button>`;
  }).join('');

  pagination.innerHTML = `
    <button type="button" class="pagination-btn" data-page="${Math.max(1, currentPage - 1)}" aria-label="${escapeHtml(translate('billing.previous_page'))}"${currentPage <= 1 ? ' disabled' : ''}>
      <span class="material-symbols-outlined" aria-hidden="true">chevron_left</span>
    </button>
    ${pageButtons}
    <button type="button" class="pagination-btn" data-page="${Math.min(totalPages || 1, currentPage + 1)}" aria-label="${escapeHtml(translate('billing.next_page'))}"${!totalPages || currentPage >= totalPages ? ' disabled' : ''}>
      <span class="material-symbols-outlined" aria-hidden="true">chevron_right</span>
    </button>
  `;
};

const animateCounter = (el, target, isCurrency = false) => {
  if (!el) return;
  clearInterval(counterTimers.get(el));
  let current = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const timer = setInterval(() => {
    current = Math.min(current + step, target);
    el.textContent = isCurrency ? fmtCurrency(current) : current;
    if (current >= target) {
      clearInterval(timer);
      counterTimers.delete(el);
    }
  }, 30);
  counterTimers.set(el, timer);
};

const updateStats = () => {
  const total = invoices.length;
  const pending = invoices.reduce((sum, i) => sum + (Number(i.pending) || 0), 0);
  const today = getColombiaDate();
  const paidToday = invoices.filter(i => {
    if (String(i.status).toLowerCase() !== 'pagada') return false;
    return i.history?.some(h => String(h.date || '').slice(0, 10) === today && h.type === 'pago');
  }).length;
  const cancelled = invoices.filter(i => String(i.status).toLowerCase() === 'anulada').length;
  
  animateCounter(safeGetElement('statTotal'), total);
  animateCounter(safeGetElement('statPending'), pending, true);
  animateCounter(safeGetElement('statPaidToday'), paidToday);
  animateCounter(safeGetElement('statCancelled'), cancelled);
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

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
  const pagination = safeGetElement('invoicePagination');
  pagination?.addEventListener('click', event => {
    const button = event.target.closest('button[data-page]');
    if (!button || button.disabled) return;
    currentPage = Number(button.dataset.page);
    renderInvoices();
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
  
};

const initNewInvoice = () => {
  const btn = safeGetElement('btnNewInvoice');
  const overlay = safeGetElement('newInvoiceOverlay');
  const form = safeGetElement('newInvoiceForm');
  const closeBtn = safeGetElement('newInvoiceClose');
  const cancelBtn = safeGetElement('newInvoiceCancel');
  const dateInput = safeGetElement('invoiceDate');
  const patientSelect = safeGetElement('invoicePatientSelect');
  const patientInput = safeGetElement('invoicePatient');
  const docInput = safeGetElement('invoiceDocument');
  const serviceSelect = safeGetElement('invoiceService');
  const totalInput = safeGetElement('invoiceTotal');

  const openModal = async () => {
    if (!overlay) return;

    if (dateInput && !dateInput.value) {
      dateInput.value = getColombiaDate();
    }

    if (patientSelect && patientSelect.options.length <= 1) {
      try {
        const resp = await window.apiRequest('/api/facturas/catalogos/pacientes');
        if (resp?.success && Array.isArray(resp.data)) {
          resp.data.forEach(p => {
            const opt = document.createElement('option');
            opt.value = p.id;
            opt.textContent = `${p.nombre} (${p.documento})`;
            opt.dataset.nombre = p.nombre;
            opt.dataset.doc = p.documento;
            patientSelect.appendChild(opt);
          });
        }
      } catch {
        showToast(translate('billing.patient_catalog_error'), 'warning');
      }
    }

    if (patientSelect && patientSelect.options.length > 1) {
      openDialog(overlay, patientSelect);
    } else if (patientInput) {
      openDialog(overlay, patientInput);
    } else {
      openDialog(overlay);
    }
  };

  const closeModal = () => {
    closeDialog(overlay);
  };

  btn?.addEventListener('click', (e) => {
    e.preventDefault();
    openModal();
  });

  closeBtn?.addEventListener('click', closeModal);
  cancelBtn?.addEventListener('click', closeModal);

  overlay?.addEventListener('click', (e) => {
    if (e.target === overlay) closeModal();
  });

  patientSelect?.addEventListener('change', () => {
    const opt = patientSelect.options[patientSelect.selectedIndex];
    if (opt && opt.value) {
      if (patientInput) patientInput.value = opt.dataset.nombre || opt.textContent.split(' (')[0];
      if (docInput) docInput.value = opt.dataset.doc || '';
    }
  });

  serviceSelect?.addEventListener('change', () => {
    const opt = serviceSelect.options[serviceSelect.selectedIndex];
    if (opt && opt.dataset.price && totalInput) {
      totalInput.value = opt.dataset.price;
    }
  });

  form?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const patientName = patientInput?.value.trim();
    const doc = docInput?.value.trim();
    const invoiceDate = dateInput?.value || getColombiaDate();
    const serviceName = serviceSelect?.value;
    const totalVal = parseFloat(totalInput?.value || '0');
    let selectedPatientId = parseInt(patientSelect?.value || '0', 10);
    const selectedServiceOpt = serviceSelect?.options[serviceSelect.selectedIndex];
    const selectedServiceId = selectedServiceOpt?.dataset.id ? parseInt(selectedServiceOpt.dataset.id, 10) : null;

    if (!patientName || !doc || !serviceName || totalVal <= 0) {
      showToast(translate('billing.required_fields_error'), 'error');
      return;
    }

    if (!Number.isInteger(selectedPatientId) || selectedPatientId <= 0) {
      showToast(translate('billing.patient_selection_required'), 'error');
      patientSelect?.focus();
      return;
    }

    const submitBtn = form.querySelector('button[type="submit"]');
    if (submitBtn) submitBtn.disabled = true;

    try {
      const payload = {
        idPaciente: selectedPatientId,
        notas: `Servicio: ${serviceName}`,
        items: [
          {
            idServicio: selectedServiceId,
            descripcion: serviceName,
            cantidad: 1,
            precioUnitario: totalVal
          }
        ]
      };
      const response = await window.apiRequest('/api/facturas', {
        method: 'POST',
        body: payload
      });
      if (response?.success !== true || !response.data?.id) {
        throw new Error(response?.message || translate('billing.invoice_create_error'));
      }

      const createdData = response.data;
      const newInvoice = {
        id: createdData.id,
        number: createdData.numero,
        patient: createdData?.paciente || patientName,
        doc: createdData?.documento || doc,
        date: createdData?.fecha ? String(createdData.fecha).slice(0, 10) : invoiceDate,
        total: Number(createdData.total ?? totalVal),
        pending: Number(createdData.pendiente ?? totalVal),
        status: createdData.estado || 'pendiente',
        avatar: patientName.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2),
        color: ['blue','green','purple','orange','red'][invoices.length % 5],
        service: serviceName,
        history: []
      };

      // Resetear filtros para garantizar que la nueva factura sea visible de inmediato en la tabla
      searchQuery = '';
      filterStatus = '';
      filterMonth = '';

      const searchInput = safeGetElement('searchInvoices');
      if (searchInput) searchInput.value = '';

      const filterStatusEl = safeGetElement('filterStatus');
      if (filterStatusEl) filterStatusEl.value = '';

      const filterMonthEl = safeGetElement('filterMonth');
      if (filterMonthEl) filterMonthEl.value = '';

      invoices.unshift(newInvoice);
      currentPage = 1;
      renderMonthOptions();
      updateStats();
      renderInvoices();

      showToast(translate('billing.invoice_created', { number: newInvoice.number }), 'success');
      form.reset();
      closeModal();
    } catch (err) {
      console.error('Error al generar la factura:', err);
      showToast(err.message || translate('billing.invoice_create_error'), 'error');
    } finally {
      if (submitBtn) submitBtn.disabled = false;
    }
  });

};

const initDialogKeyboard = () => {
  document.addEventListener('keydown', event => {
    const dialogs = Array.from(document.querySelectorAll(
      '.drawer-overlay.open, .invoice-details-overlay.open, .payment-success-overlay.open, .new-invoice-overlay.open'
    ));
    if (!dialogs.length) return;

    dialogs.sort((first, second) =>
      Number.parseInt(getComputedStyle(first).zIndex, 10) - Number.parseInt(getComputedStyle(second).zIndex, 10)
    );
    const dialog = dialogs[dialogs.length - 1];

    if (event.key === 'Escape') {
      event.preventDefault();
      closeDialog(dialog);
      return;
    }

    if (event.key !== 'Tab') return;
    const focusable = Array.from(dialog.querySelectorAll(
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(element => !element.hidden && element.getClientRects().length > 0);

    if (!focusable.length) {
      event.preventDefault();
      dialog.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });
};


// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSearch();
  initFilters();
  initPagination();
  initNewInvoice();
  initDrawer();
  initDialogKeyboard();
  
  
  try {
    const response = await window.apiRequest('/api/facturas');
    if (response?.success && Array.isArray(response.data)) {
      invoices = response.data.map((f, idx) => ({
        id: f.id, number: f.numero, patient: f.paciente, doc: f.documento,
        date: String(f.fecha).slice(0, 10), total: f.total, paid: f.montoPagado, pending: f.pendiente,
        status: f.estado, color: ['blue','green','purple','orange','red'][idx % 5],
        service: f.notas || 'Servicio Odontológico',
        history: f.fechaPago ? [{ date: String(f.fechaPago).slice(0, 10), type: 'pago' }] : []
      }));
    } else {
      showToast(translate('billing.invoice_load_error'), 'warning');
    }
  } catch (error) {
    console.error('No fue posible cargar las facturas desde la API.', error);
    showToast(translate('billing.invoice_load_error'), 'error');
  }
  renderMonthOptions();
  updateStats();
  renderInvoices();
  document.addEventListener('i18n:change', () => {
    renderMonthOptions();
    renderInvoices();
    updateStats();
  });
};

document.addEventListener('DOMContentLoaded', init);
})();