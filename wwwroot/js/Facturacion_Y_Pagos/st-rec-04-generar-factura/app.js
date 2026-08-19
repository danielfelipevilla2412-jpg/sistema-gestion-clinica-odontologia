// =============================================
// SMILETRACK — GESTIÓN DE FACTURAS (app.js)
// =============================================

// Obtiene elemento del DOM con manejo seguro de null
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Formatea número como moneda colombiana (COP)
const formatCurrency = (num) => {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    minimumFractionDigits: 0
  }).format(num).replace('COP', '$').trim();
};

// Extrae valor numérico de string con formato de moneda
const parseCurrency = (str) => {
  return parseInt(str.replace(/[^0-9]/g, ''), 10) || 0;
};

// Muestra notificación temporal con auto-cierre y cleanup de timeout
const showToast = (message, type = 'success') => {
  const toastContainer = safeGetElement('toastContainer');
  if (!toastContainer) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type === 'error' ? 'error' : type === 'info' ? 'info' : ''}`;
  toast.setAttribute('role', 'alert');

  const icon = type === 'error' ? '⚠️' : type === 'info' ? 'ℹ️' : '✅';
  const title = type === 'error' ? 'Error' : type === 'info' ? 'Información' : 'Éxito';

  toast.innerHTML = `
    <span class="toast-icon" aria-hidden="true">${icon}</span>
    <div class="toast-content">
      <p class="toast-title">${title}</p>
      <p class="toast-desc">${message}</p>
    </div>
    <button class="toast-close" aria-label="Cerrar notificación">×</button>
  `;

  toastContainer.appendChild(toast);

  requestAnimationFrame(() => toast.classList.add('show'));

  const closeBtn = toast.querySelector('.toast-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 250);
    });
  }

  const autoCloseTimeout = setTimeout(() => {
    toast.classList.remove('show');
    setTimeout(() => toast.remove(), 250);
  }, 4500);
  
  toast._autoCloseTimeout = autoCloseTimeout;
};

// Recalcula subtotal, descuentos y total de la factura
const recalculateInvoice = () => {
  let subtotal = 0;
  let totalDiscount = 0;

  const itemsBody = safeGetElement('invoiceItemsBody');
  if (!itemsBody) return;

  itemsBody.querySelectorAll('tr').forEach(row => {
    const price = parseFloat(row.dataset.price) || 0;
    const discount = parseFloat(row.dataset.discount) || 0;
    subtotal += price;
    totalDiscount += discount;
  });

  const total = subtotal - totalDiscount;

  const subtotalEl = safeGetElement('invoiceSubtotal');
  const discountEl = safeGetElement('invoiceDiscount');
  const ivaEl = safeGetElement('invoiceIva');
  const totalEl = safeGetElement('invoiceTotal');
  const amountReceived = safeGetElement('amountReceived');
  const btnPay = safeGetElement('btnPay');

  if (subtotalEl) subtotalEl.textContent = formatCurrency(subtotal);
  if (discountEl) discountEl.textContent = totalDiscount > 0 ? `- ${formatCurrency(totalDiscount)}` : '$0';
  if (ivaEl) ivaEl.textContent = '$0';
  if (totalEl) totalEl.textContent = formatCurrency(total);

  if (amountReceived && btnPay && !window.isPaid) {
    amountReceived.value = formatCurrency(total);
    btnPay.innerHTML = `✓ Registrar pago — ${formatCurrency(total)}`;
    btnPay.setAttribute('aria-label', `Registrar pago de ${formatCurrency(total)}`);
  }
};

// Crea fila de tabla para un servicio con datos y atributos ARIA
const createInvoiceRow = (serviceData) => {
  const { title, desc, price, discount, serviceId } = serviceData;
  
  const row = document.createElement('tr');
  row.dataset.price = price;
  row.dataset.discount = discount;
  row.setAttribute('role', 'row');

  row.innerHTML = `
    <td>
      <span class="item-desc">${title}</span>
      <div class="item-sub">${desc}</div>
    </td>
    <td class="text-center">1</td>
    <td class="text-right">${formatCurrency(price)}</td>
    <td class="text-right ${discount > 0 ? 'item-discount' : ''}">
      ${discount > 0 ? formatCurrency(discount) : '—'}
    </td>
    <td class="text-right item-total">${formatCurrency(price - discount)}</td>
    <td class="text-center no-print">
      <button class="btn-delete" title="Eliminar" aria-label="Eliminar ${title} de la factura">✕</button>
    </td>
  `;
  
  return row;
};

// Añade servicio seleccionado a la tabla de items
const addService = () => {
  if (window.isPaid) {
    showToast('No se pueden añadir servicios a una factura pagada', 'error');
    return;
  }

  const servicioSelect = safeGetElement('servicioSelect');
  if (!servicioSelect) return;

  const option = servicioSelect.options[servicioSelect.selectedIndex];
  const serviceId = option.value;
  const price = parseFloat(option.dataset.price);
  const desc = option.dataset.desc;
  const title = option.text.split(' - ')[0];

  // Aplica descuento automático para consultas de ejemplo
  const discount = serviceId === 'consulta' ? 5000 : 0;

  const row = createInvoiceRow({ title, desc, price, discount, serviceId });
  
  const itemsBody = safeGetElement('invoiceItemsBody');
  if (itemsBody) {
    itemsBody.appendChild(row);
    recalculateInvoice();
    showToast(`Añadido: ${title}`);
  }
};

// Maneja eliminación de items con event delegation
const initDeleteHandlers = () => {
  const itemsBody = safeGetElement('invoiceItemsBody');
  if (!itemsBody) return;

  itemsBody.addEventListener('click', (e) => {
    const btn = e.target.closest('.btn-delete');
    if (!btn) return;

    if (window.isPaid) {
      showToast('No se pueden modificar facturas pagadas', 'error');
      return;
    }

    const row = btn.closest('tr');
    const title = row?.querySelector('.item-desc')?.textContent || 'item';
    
    row.remove();
    recalculateInvoice();
    showToast(`Eliminado: ${title}`, 'info');
  });

  // Soporte para teclado en botones de eliminar
  itemsBody.addEventListener('keydown', (e) => {
    if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('.btn-delete')) {
      e.preventDefault();
      e.target.click();
    }
  });
};

// Actualiza el paciente mostrado en el header de la factura al cambiar selección
const initPatientSelect = () => {
  const pacienteSelect = safeGetElement('pacienteSelect');
  const patientNameEl = safeGetElement('invoicePatientName');
  const patientDocEl = safeGetElement('invoicePatientDoc');

  if (pacienteSelect && patientNameEl && patientDocEl) {
    pacienteSelect.addEventListener('change', (e) => {
      const option = e.target.options[e.target.selectedIndex];
      if (!option.value) {
        patientNameEl.textContent = 'Seleccione un paciente';
        patientDocEl.textContent = '—';
        return;
      }
      patientNameEl.textContent = option.text;
      patientDocEl.textContent = option.dataset.doc || '—';
    });
  }
};

// Actualiza profesional mostrado en header al cambiar selección
const initProfessionalSelect = () => {
  const profesionalSelect = safeGetElement('profesionalSelect');
  const invoiceProfessional = safeGetElement('invoiceProfessional');

  if (profesionalSelect && invoiceProfessional) {
    profesionalSelect.addEventListener('change', (e) => {
      invoiceProfessional.textContent = e.target.value;
      showToast(`Profesional: ${e.target.value}`, 'info');
    });
  }
};

// Valida que monto recibido cubra el total al perder foco
const initAmountValidation = () => {
  const amountReceived = safeGetElement('amountReceived');
  if (!amountReceived) return;

  amountReceived.addEventListener('blur', (e) => {
    const val = parseCurrency(e.target.value);
    e.target.value = formatCurrency(val);

    if (!window.isPaid) {
      const totalEl = safeGetElement('invoiceTotal');
      const total = totalEl ? parseCurrency(totalEl.textContent) : 0;
      
      if (val < total) {
        showToast('Monto insuficiente para cubrir el total', 'error');
      }
    }
  });
};

// Inicializa botón de impresión que usa window.print()
const initPrint = () => {
  const btnPrint = safeGetElement('btnPrint');
  if (btnPrint) {
    btnPrint.addEventListener('click', () => {
      window.print();
    });
  }
};

// Registra pago y actualiza estado de factura a pagada
const initPayment = () => {
  const btnPay = safeGetElement('btnPay');
  if (!btnPay) return;

  btnPay.addEventListener('click', () => {
    if (window.isPaid) {
      showToast('Esta factura ya ha sido pagada', 'info');
      return;
    }

    const totalEl = safeGetElement('invoiceTotal');
    const amountReceived = safeGetElement('amountReceived');
    
    if (!totalEl || !amountReceived) return;

    const total = parseCurrency(totalEl.textContent);
    const received = parseCurrency(amountReceived.value);

    if (received < total) {
      showToast(`Pago insuficiente. Total: ${formatCurrency(total)}`, 'error');
      return;
    }

    // Marca factura como pagada
    window.isPaid = true;

    // Actualiza badge de estado visual y ARIA
    const statusBadge = safeGetElement('statusBadge');
    if (statusBadge) {
      statusBadge.className = 'status-badge status-pagada';
      statusBadge.innerHTML = '<span aria-hidden="true"></span> Factura Pagada';
      statusBadge.setAttribute('aria-label', 'Estado: Factura pagada');
    }

    // Deshabilita controles de edición
    const profesionalSelect = safeGetElement('profesionalSelect');
    const servicioSelect = safeGetElement('servicioSelect');
    const btnAddService = safeGetElement('btnAddService');
    const paymentMethod = safeGetElement('paymentMethod');

    if (profesionalSelect) profesionalSelect.disabled = true;
    if (servicioSelect) servicioSelect.disabled = true;
    if (btnAddService) {
      btnAddService.disabled = true;
      btnAddService.style.opacity = '.6';
      btnAddService.setAttribute('aria-disabled', 'true');
    }
    if (amountReceived) amountReceived.disabled = true;
    if (paymentMethod) paymentMethod.disabled = true;

    // Deshabilita botones de eliminar en items
    const itemsBody = safeGetElement('invoiceItemsBody');
    if (itemsBody) {
      itemsBody.querySelectorAll('.btn-delete').forEach(btn => {
        btn.disabled = true;
        btn.style.opacity = '.4';
        btn.setAttribute('aria-disabled', 'true');
      });
    }

    // Actualiza botón de pago
    btnPay.className = 'btn-secondary';
    btnPay.innerHTML = '✓ Factura Pagada';
    btnPay.disabled = true;
    btnPay.setAttribute('aria-disabled', 'true');

    // Calcula y muestra cambio si aplica
    const change = received - total;
    if (change > 0) {
      showToast(`✓ Pago registrado. Cambio: ${formatCurrency(change)}`, 'success');
    } else {
      showToast('✓ Pago registrado exitosamente', 'success');
    }
  });
};

// Inicializa menú móvil con gestión de foco y atributos ARIA
const initMobileMenu = () => {
  const ham = safeGetElement('hamburger');
  const sb = safeGetElement('sidebar');
  const ov = safeGetElement('overlay');

  if (!ham || !sb || !ov) return;

  const toggleMenu = (show) => {
    if (show) {
      sb.classList.add('open');
      ov.classList.add('open');
      ham.setAttribute('aria-expanded', 'true');
      ov.setAttribute('aria-hidden', 'false');
      sb.dataset.previousFocus = document.activeElement?.id || '';
      const firstLink = sb.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      sb.classList.remove('open');
      ov.classList.remove('open');
      ham.setAttribute('aria-expanded', 'false');
      ov.setAttribute('aria-hidden', 'true');
      const prevFocus = sb.dataset.previousFocus;
      if (prevFocus) safeGetElement(prevFocus)?.focus();
      else ham.focus();
    }
  };

  ham.addEventListener('click', () => toggleMenu(true));
  ov.addEventListener('click', () => toggleMenu(false));

  document.querySelectorAll('.nav-item').forEach(item => {
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

// Inicializa datos de ejemplo en tabla de items
const initSampleData = () => {
  const itemsBody = safeGetElement('invoiceItemsBody');
  if (!itemsBody) return;

  itemsBody.innerHTML = `
    <tr data-price="30000" data-discount="0">
      <td><span class="item-desc">Control de tratamiento</span><div class="item-sub">Pieza 23 — Seguimiento endodoncia</div></td>
      <td class="text-center">1</td>
      <td class="text-right">$30.000</td>
      <td class="text-right">—</td>
      <td class="text-right item-total">$30.000</td>
      <td class="text-center no-print"><button class="btn-delete" title="Eliminar" aria-label="Eliminar Control de tratamiento">✕</button></td>
    </tr>
    <tr data-price="50000" data-discount="5000">
      <td><span class="item-desc">Consulta General</span><div class="item-sub">Valoración y diagnóstico</div></td>
      <td class="text-center">1</td>
      <td class="text-right">$50.000</td>
      <td class="text-right item-discount">$5.000</td>
      <td class="text-right item-total">$45.000</td>
      <td class="text-center no-print"><button class="btn-delete" title="Eliminar" aria-label="Eliminar Consulta General">✕</button></td>
    </tr>
  `;
};

// Función principal de inicialización
const init = () => {
  window.isPaid = false;

  initMobileMenu();
  initSampleData();
  recalculateInvoice();
  initDeleteHandlers();
  initPatientSelect();
  initProfessionalSelect();
  initAmountValidation();
  initPrint();
  initPayment();

  const btnAddService = safeGetElement('btnAddService');
  if (btnAddService) {
    btnAddService.addEventListener('click', addService);
  }

  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

document.addEventListener('DOMContentLoaded', init);