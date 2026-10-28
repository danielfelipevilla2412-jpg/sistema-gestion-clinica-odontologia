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
  const formattedAmount = new Intl.NumberFormat('es-CO', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2
  }).format(Number(num) || 0);
  return `$${formattedAmount} COP`;
};

// Extrae valor numérico de string con formato de moneda
const parseCurrency = (str) => {
  const normalized = String(str).trim().replace(/[^0-9,.-]/g, '');
  const amount = normalized.includes(',')
    ? Number(normalized.replace(/\./g, '').replace(',', '.'))
    : Number(normalized.replace(/\./g, ''));
  return Number.isFinite(amount) ? amount : 0;
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
  if (discountEl) discountEl.textContent = totalDiscount > 0 ? `- ${formatCurrency(totalDiscount)}` : '$0 COP';
  if (ivaEl) ivaEl.textContent = '$0 COP';
  if (totalEl) totalEl.textContent = formatCurrency(total);

  if (amountReceived && btnPay && !window.isPaid) {
    const pending = window.currentInvoiceId ? window.currentInvoicePending : total;
    amountReceived.value = formatCurrency(pending);
    btnPay.textContent = `✓ Registrar abono — ${formatCurrency(pending)}`;
    btnPay.setAttribute('aria-label', `Registrar abono de ${formatCurrency(pending)}`);
  }
};

// Crea fila de tabla para un servicio con datos y atributos ARIA
const createInvoiceRow = (serviceData) => {
  const { title, desc, price, discount, serviceId } = serviceData;
  
  const row = document.createElement('tr');
  row.dataset.price = price;
  row.dataset.discount = discount;
  // id_servicio real (tabla Servicio en SQL Server); vacío si es un ítem
  // libre sin servicio de catálogo asociado (ej. la opción de ejemplo "consulta").
  row.dataset.serviceId = /^\d+$/.test(String(serviceId)) ? serviceId : '';
  row.dataset.title = title;
  row.dataset.desc = desc;
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
    const updatePatient = () => {
      const option = pacienteSelect.options[pacienteSelect.selectedIndex];
      if (!option.value) {
        patientNameEl.textContent = 'Seleccione un paciente';
        patientDocEl.textContent = '—';
        return;
      }
      patientNameEl.textContent = option.text;
      patientDocEl.textContent = option.dataset.doc || '—';
    };

    pacienteSelect.addEventListener('change', updatePatient);
    updatePatient();
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

// Normaliza el abono y valida que esté dentro del saldo pendiente.
const initAmountValidation = () => {
  const amountReceived = safeGetElement('amountReceived');
  if (!amountReceived) return;

  amountReceived.addEventListener('blur', (e) => {
    const val = parseCurrency(e.target.value);
    e.target.value = formatCurrency(val);

    if (!window.isPaid) {
      const totalEl = safeGetElement('invoiceTotal');
      const total = totalEl ? parseCurrency(totalEl.textContent) : 0;
      const pending = window.currentInvoiceId ? window.currentInvoicePending : total;

      if (val <= 0) {
        showToast('El monto del abono debe ser mayor que cero.', 'error');
      } else if (val > pending) {
        showToast(`El abono no puede superar el saldo pendiente de ${formatCurrency(pending)}.`, 'error');
      }
    }
  });
};

// Abre el diálogo nativo de impresión con la factura actual visible.
const initPrint = () => {
  const btnPrint = safeGetElement('btnPrint');
  if (btnPrint) {
    btnPrint.addEventListener('click', () => {
      window.print();
    });
  }
};

// Construye el payload de ítems (uno por fila de la tabla) para la API real.
// El descuento se incorpora al precio unitario efectivo de la línea, porque
// la tabla Detalle_Factura no modela descuentos por separado.
  const buildInvoiceItemsPayload = () => {
  const itemsBody = safeGetElement('invoiceItemsBody');
  if (!itemsBody) return [];

  return Array.from(itemsBody.querySelectorAll('tr')).map(row => {
    const price = parseFloat(row.dataset.price) || 0;
    const discount = parseFloat(row.dataset.discount) || 0;
    const idServicio = row.dataset.serviceId ? parseInt(row.dataset.serviceId, 10) : null;
    const descripcionBase = row.dataset.title || row.querySelector('.item-desc')?.textContent || 'Servicio';
    const descripcion = discount > 0 ? `${descripcionBase} (desc. ${formatCurrency(discount)})` : descripcionBase;

    return {
      idServicio,
      descripcion,
      cantidad: 1,
      precioUnitario: price,
      descuento: discount
    };
  });
};

let paymentConfirmationData = null;

const openPaymentConfirmation = (payment) => {
  const overlay = safeGetElement('paymentConfirmOverlay');
  const patientSelect = safeGetElement('pacienteSelect');
  const patientOption = patientSelect?.options[patientSelect.selectedIndex];
  const methodSelect = safeGetElement('paymentMethod');
  const total = payment.total;
  const serviceRows = Array.from(safeGetElement('invoiceItemsBody')?.querySelectorAll('tr') || []);
  const items = serviceRows.map((row) => ({
    title: row.dataset.title || 'Servicio',
    description: row.dataset.desc || '',
    price: parseFloat(row.dataset.price) || 0,
    discount: parseFloat(row.dataset.discount) || 0
  }));

  if (!overlay || !patientOption || !methodSelect) return;
  paymentConfirmationData = payment;

  safeGetElement('paymentConfirmPatient').textContent = patientOption.textContent.trim();
  safeGetElement('paymentConfirmDocument').textContent = patientOption.dataset.doc || 'No registrado';
  safeGetElement('paymentConfirmMethod').textContent = methodSelect.options[methodSelect.selectedIndex]?.textContent.trim() || 'Efectivo';
  safeGetElement('paymentConfirmSubtotal').textContent = formatCurrency(items.reduce((sum, item) => sum + item.price, 0));
  safeGetElement('paymentConfirmDiscount').textContent = formatCurrency(items.reduce((sum, item) => sum + item.discount, 0));
  safeGetElement('paymentConfirmTotal').textContent = formatCurrency(total);
  safeGetElement('paymentConfirmPreviousPaid').textContent = formatCurrency(payment.previousPaid);
  safeGetElement('paymentConfirmInstallment').textContent = formatCurrency(payment.amount);
  safeGetElement('paymentConfirmBalance').textContent = formatCurrency(payment.pendingBefore - payment.amount);

  const list = safeGetElement('paymentConfirmItems');
  list.replaceChildren();
  items.forEach((item) => {
    const row = document.createElement('li');
    const copy = document.createElement('div');
    copy.className = 'payment-confirm-item-copy';
    const title = document.createElement('strong');
    title.textContent = item.title;
    copy.appendChild(title);
    if (item.description) {
      const description = document.createElement('span');
      description.textContent = item.description;
      copy.appendChild(description);
    }
    if (item.discount > 0) {
      const discount = document.createElement('span');
      discount.textContent = `Costo ${formatCurrency(item.price)} · Descuento ${formatCurrency(item.discount)}`;
      copy.appendChild(discount);
    }
    const price = document.createElement('strong');
    price.className = 'payment-confirm-item-price';
    price.textContent = formatCurrency(item.price - item.discount);
    row.append(copy, price);
    list.appendChild(row);
  });

  overlay.hidden = false;
  overlay.setAttribute('aria-hidden', 'false');
  safeGetElement('paymentConfirmSubmit').focus();
};

const closePaymentConfirmation = () => {
  const overlay = safeGetElement('paymentConfirmOverlay');
  if (!overlay || !paymentConfirmationData) return;
  overlay.hidden = true;
  overlay.setAttribute('aria-hidden', 'true');
  paymentConfirmationData = null;
  safeGetElement('btnPay')?.focus();
};

const confirmPayment = async () => {
  const payment = paymentConfirmationData;
  const overlay = safeGetElement('paymentConfirmOverlay');
  const confirmButton = safeGetElement('paymentConfirmSubmit');
  const cancelButton = safeGetElement('paymentConfirmCancel');
  const btnPay = safeGetElement('btnPay');
  if (!payment || !overlay || !confirmButton || !btnPay) return;

  confirmButton.disabled = true;
  cancelButton.disabled = true;
  confirmButton.textContent = 'Registrando...';
  btnPay.disabled = true;

  try {
    const result = window.currentInvoiceId
      ? await window.apiRequest(`/api/facturas/${window.currentInvoiceId}/pagos`, {
        method: 'POST',
        body: { montoPagado: payment.amount, metodoPago: payment.metodoPago }
      })
      : await window.apiRequest('/api/facturas/registrar-con-pago', {
        method: 'POST',
        body: {
          idPaciente: payment.idPaciente,
          notas: null,
          detalles: payment.items,
          montoPagado: payment.amount,
          metodoPago: payment.metodoPago
        }
      });

    if (!result || result.success !== true) {
      throw new Error(result?.message || 'No fue posible guardar la factura y el abono.');
    }

    const invoice = result.data;
    window.currentInvoiceId = invoice.id;
    window.currentInvoicePaid = invoice.montoPagado;
    window.currentInvoicePending = invoice.pendiente;
    window.isPaid = invoice.estado === 'pagada';
    const invoiceNumberEl = document.querySelector('.invoice-number');
    if (invoiceNumberEl) invoiceNumberEl.textContent = invoice.numero;

    const statusBadge = safeGetElement('statusBadge');
    if (statusBadge) {
      const statusText = window.isPaid ? 'Factura Pagada' : 'Pago parcial';
      statusBadge.className = `status-badge status-${invoice.estado}`;
      statusBadge.innerHTML = `<span aria-hidden="true"></span> ${statusText}`;
      statusBadge.setAttribute('aria-label', `Estado: ${statusText}`);
    }

    ['pacienteSelect', 'profesionalSelect', 'servicioSelect', 'btnAddService'].forEach((id) => {
      const element = safeGetElement(id);
      if (element) element.disabled = true;
    });
    safeGetElement('invoiceItemsBody')?.querySelectorAll('.btn-delete').forEach((button) => {
      button.disabled = true;
      button.setAttribute('aria-disabled', 'true');
    });

    safeGetElement('invoicePaidSummary').hidden = false;
    safeGetElement('invoiceBalanceSummary').hidden = false;
    safeGetElement('invoicePaidAmount').textContent = formatCurrency(invoice.montoPagado);
    safeGetElement('invoiceBalanceAmount').textContent = formatCurrency(invoice.pendiente);
    const amountInput = safeGetElement('amountReceived');
    const methodSelect = safeGetElement('paymentMethod');
    if (amountInput) {
      amountInput.disabled = window.isPaid;
      if (!window.isPaid) amountInput.value = formatCurrency(invoice.pendiente);
    }
    if (methodSelect) methodSelect.disabled = window.isPaid;

    btnPay.className = window.isPaid ? 'btn-secondary' : 'btn-success';
    btnPay.textContent = window.isPaid ? 'Factura pagada' : `Registrar abono — ${formatCurrency(invoice.pendiente)}`;
    btnPay.disabled = window.isPaid;
    btnPay.setAttribute('aria-disabled', String(window.isPaid));
    overlay.hidden = true;
    overlay.setAttribute('aria-hidden', 'true');
    paymentConfirmationData = null;

    showToast(window.isPaid
      ? `Factura ${invoice.numero} pagada. Saldo pendiente: ${formatCurrency(invoice.pendiente)}.`
      : `Abono registrado en ${invoice.numero}. Saldo pendiente: ${formatCurrency(invoice.pendiente)}.`, 'success');
  } catch (error) {
    showToast(error.message || 'Error al registrar la factura.', 'error');
  } finally {
    confirmButton.disabled = false;
    cancelButton.disabled = false;
    confirmButton.textContent = 'Confirmar y registrar';
    if (!window.isPaid) btnPay.disabled = false;
  }
};

const initPayment = () => { 
  const btnPay = safeGetElement('btnPay');
  if (!btnPay) return;

  const overlay = safeGetElement('paymentConfirmOverlay');
  const confirmButton = safeGetElement('paymentConfirmSubmit');
  const cancelButton = safeGetElement('paymentConfirmCancel');
  const closeButton = safeGetElement('paymentConfirmClose');

  btnPay.addEventListener('click', () => {
    if (window.isPaid) {
      showToast('Esta factura ya ha sido pagada', 'info');
      return;
    }
    const pacienteSelect = safeGetElement('pacienteSelect');
    const profesionalSelect = safeGetElement('profesionalSelect');
    const totalEl = safeGetElement('invoiceTotal');
    const amountReceived = safeGetElement('amountReceived');
    
    
    if (!totalEl || !amountReceived || !pacienteSelect) return;
    const idPaciente = parseInt(pacienteSelect.value, 10);
    const idProfesional = profesionalSelect?.value ? parseInt(profesionalSelect.value, 10) : null;
    if (!idPaciente) {
      showToast('Seleccione un paciente antes de registrar el pago.', 'error');
      pacienteSelect.focus();
      return;
    }

    const items = buildInvoiceItemsPayload();
    if (items.length === 0) {
      showToast('Añada al menos un servicio a la factura.', 'error');
      return;
    }
    const total = parseCurrency(totalEl.textContent);
    const amount = parseCurrency(amountReceived.value);
    const pendingBefore = window.currentInvoiceId ? window.currentInvoicePending : total;

    if (amount <= 0) {
      showToast('El monto del abono debe ser mayor que cero.', 'error');
      amountReceived.focus();
      return;
    }
    if (amount > pendingBefore) {
      showToast(`El abono no puede superar el saldo pendiente de ${formatCurrency(pendingBefore)}.`, 'error');
      amountReceived.focus();
      return;
    }

    openPaymentConfirmation({
      idPaciente,
      items,
      total,
      amount,
      previousPaid: window.currentInvoicePaid || 0,
      pendingBefore,
      metodoPago: safeGetElement('paymentMethod')?.value || 'efectivo'
    });
  });

<<<<<<< HEAD
<<<<<<< HEAD
=======
>>>>>>> 83a1479ed269637a52ff5b05687963f999b92b23
    try {
      // 1) Crear la factura real en SQL Server.
      const creado = await window.apiRequest('/api/facturas', {
        method: 'POST',
        body: { idPaciente, idProfesional, notas: null, detalles: items }
      });
<<<<<<< HEAD
=======
  confirmButton?.addEventListener('click', confirmPayment);
  cancelButton?.addEventListener('click', closePaymentConfirmation);
  closeButton?.addEventListener('click', closePaymentConfirmation);
  overlay?.addEventListener('click', (event) => {
    if (event.target === overlay) closePaymentConfirmation();
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && overlay && !overlay.hidden) closePaymentConfirmation();
  });
};
>>>>>>> origin/facturacion_ayuda
=======
>>>>>>> 83a1479ed269637a52ff5b05687963f999b92b23

const formatInvoiceDate = (value, includeTime = false) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('es-CO', includeTime
    ? { dateStyle: 'medium', timeStyle: 'short' }
    : { dateStyle: 'medium' }).format(date);
};

const addInvoiceCell = (row, value) => {
  const cell = document.createElement('td');
  cell.textContent = value;
  row.appendChild(cell);
};

const renderSavedInvoiceDetail = (invoice) => {
  const detail = safeGetElement('savedInvoiceDetail');
  const fields = {
    savedInvoicePatient: invoice.paciente,
    savedInvoiceDocument: invoice.documento,
    savedInvoiceDate: formatInvoiceDate(invoice.fecha),
    savedInvoiceSubtotal: formatCurrency(invoice.subtotal),
    savedInvoiceTotal: formatCurrency(invoice.total),
    savedInvoicePaid: formatCurrency(invoice.montoPagado),
    savedInvoicePending: formatCurrency(invoice.pendiente),
    savedInvoiceStatus: invoice.estado
  };
  Object.entries(fields).forEach(([id, value]) => {
    safeGetElement(id).textContent = value || '—';
  });

  const services = safeGetElement('savedInvoiceServices');
  services.replaceChildren();
  (invoice.detalles || []).forEach((service) => {
    const item = document.createElement('li');
    item.textContent = `${service.descripcion} · ${service.cantidad} × ${formatCurrency(service.precioUnitario)} · Descuento ${formatCurrency(service.descuento)} · Subtotal neto ${formatCurrency(service.subtotal)}`;
    services.appendChild(item);
  });
  if (!invoice.detalles?.length) {
    const item = document.createElement('li');
    item.textContent = 'La factura no tiene servicios detallados.';
    services.appendChild(item);
  }

  const payments = safeGetElement('savedInvoicePayments');
  payments.replaceChildren();
  (invoice.pagos || []).forEach((payment) => {
    const item = document.createElement('li');
    item.textContent = `${formatInvoiceDate(payment.fechaUtc, true)} · ${formatCurrency(payment.monto)} · ${payment.metodoPago} · ${payment.estado} · Ref. ${payment.referencia}`;
    payments.appendChild(item);
  });
  if (!invoice.pagos?.length) {
    const item = document.createElement('li');
    item.textContent = 'No hay abonos registrados.';
    payments.appendChild(item);
  }
  detail.hidden = false;
};

const loadSavedInvoiceDetail = async (invoiceId, button) => {
  const message = safeGetElement('savedInvoicesMessage');
  button.disabled = true;
  message.textContent = 'Cargando detalle de la factura...';
  try {
    const response = await window.apiRequest(`/api/facturas/${invoiceId}`);
    if (!response?.success || !response.data) throw new Error(response?.message || 'No fue posible consultar el detalle.');
    renderSavedInvoiceDetail(response.data);
    message.textContent = `Detalle de la factura ${response.data.numero}.`;
  } catch (error) {
    message.textContent = error.message || 'No fue posible consultar el detalle de la factura.';
  } finally {
    button.disabled = false;
  }
};

const loadSavedInvoices = async () => {
  const rows = safeGetElement('savedInvoicesRows');
  const message = safeGetElement('savedInvoicesMessage');
  const detail = safeGetElement('savedInvoiceDetail');
  rows.replaceChildren();
  detail.hidden = true;
  message.textContent = 'Consultando facturas guardadas...';

  try {
    const response = await window.apiRequest('/api/facturas');
    if (!response?.success || !Array.isArray(response.data)) throw new Error(response?.message || 'No fue posible consultar las facturas.');

    if (response.data.length === 0) {
      message.textContent = 'No hay facturas guardadas.';
      return;
    }

    response.data.forEach((invoice) => {
      const row = document.createElement('tr');
      addInvoiceCell(row, invoice.numero);
      addInvoiceCell(row, formatInvoiceDate(invoice.fecha));
      addInvoiceCell(row, `${invoice.paciente} · ${invoice.documento}`);
      addInvoiceCell(row, invoice.servicios || '—');
      addInvoiceCell(row, formatCurrency(invoice.subtotal));
      addInvoiceCell(row, formatCurrency(invoice.total));
      addInvoiceCell(row, formatCurrency(invoice.montoPagado));
      addInvoiceCell(row, formatCurrency(invoice.pendiente));
      addInvoiceCell(row, invoice.estado);

      const actionCell = document.createElement('td');
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'saved-invoice-select';
      button.textContent = 'Consultar';
      button.setAttribute('aria-label', `Consultar factura ${invoice.numero}`);
      button.addEventListener('click', () => loadSavedInvoiceDetail(invoice.id, button));
      actionCell.appendChild(button);
      row.appendChild(actionCell);
      rows.appendChild(row);
    });
    message.textContent = `${response.data.length} factura(s) consultada(s) desde la base de datos.`;
  } catch (error) {
    message.textContent = error.message || 'No fue posible consultar las facturas guardadas.';
  }
};

const initSavedInvoiceLookup = () => {
  const overlay = safeGetElement('savedInvoicesOverlay');
  const openButton = safeGetElement('btnSavedInvoices');
  const closeButtons = [safeGetElement('savedInvoicesClose'), safeGetElement('savedInvoicesDone')];
  if (!overlay || !openButton) return;

  const close = () => {
    overlay.hidden = true;
    overlay.setAttribute('aria-hidden', 'true');
    openButton.focus();
  };
  openButton.addEventListener('click', () => {
    overlay.hidden = false;
    overlay.setAttribute('aria-hidden', 'false');
    safeGetElement('savedInvoicesClose').focus();
    loadSavedInvoices();
  });
  closeButtons.forEach((button) => button.addEventListener('click', close));
  overlay.addEventListener('click', (event) => {
    if (event.target === overlay) close();
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && !overlay.hidden) close();
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


// Función principal de inicialización
const init = () => {
  window.isPaid = false;
  window.currentInvoiceId = null;
  window.currentInvoicePaid = 0;
  window.currentInvoicePending = 0;

  initMobileMenu();
  recalculateInvoice();
  initDeleteHandlers();
  initPatientSelect();
  initProfessionalSelect();
  initAmountValidation();
  initPrint();
  initPayment();
  initSavedInvoiceLookup();

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
