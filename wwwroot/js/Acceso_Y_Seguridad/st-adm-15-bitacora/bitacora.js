/**
 * SMILETRACK — BITÁCORA DEL SISTEMA (bitacora.js)
 * API-ready + Accesibilidad + Renderizado inmediato (sin loading)
 * 
 * ✅ SIMPLIFICADO: Sin estado de carga, eventos visibles al instante
 * ✅ MANTENIDO: Filtros, paginación, modales, accesibilidad, live mode
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

const announceToScreenReader = (message) => {
  const announcer = safeGetElement('announcer');
  if (announcer) {
    announcer.textContent = message;
    setTimeout(() => { announcer.textContent = ''; }, 1000);
  }
};

const fmtTimestamp = (iso) => {
  if (!iso) return '—';
  const date = new Date(iso);
  return date.toLocaleString('es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false
  }).replace(',', '');
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS DE EJEMPLO
// ═══════════════════════════════════════════════════════════════════
const LOG_LEVELS = {
  critical: { label: 'Crítico', class: 'critical', icon: '🔴' },
  error: { label: 'Error', class: 'error', icon: '🟠' },
  warning: { label: 'Warning', class: 'warning', icon: '🟡' },
  info: { label: 'Info', class: 'info', icon: '🔵' },
  success: { label: 'Success', class: 'success', icon: '🟢' },
  debug: { label: 'Debug', class: 'debug', icon: '⚪' },
};

const MODULES = {
  auth: 'Autenticación',
  citas: 'Citas',
  facturacion: 'Facturación',
  database: 'Base de Datos',
  servicios: 'Servicios',
  system: 'Sistema',
};

const SAMPLE_LOGS = [
  { id: 1, timestamp: '2026-05-23T14:02:11', level: 'critical', module: 'database', description: 'Fallo de conexión al servidor primario. Redireccionando a réplica.', user: 'sistema', ip: '127.0.0.1', metadata: { server: 'primary-db-01', retry: 3 }, stack: 'ConnectionError: ECONNREFUSED\n    at TCP.connectWrap' },
  { id: 2, timestamp: '2026-05-23T13:58:44', level: 'warning', module: 'auth', description: '5 intentos fallidos de login detectados para el usuario r.mendez@smiletrack.co', user: 'r.mendez', ip: '192.168.1.45', metadata: { attempts: 5, locked: false } },
  { id: 3, timestamp: '2026-05-23T13:45:01', level: 'info', module: 'citas', description: 'Nueva cita #4821 creada para paciente Julián Restrepo — Dr. Méndez 10:00 AM', user: 'e.sotelo', ip: '192.168.1.32', metadata: { citaId: 4821, paciente: 'Julián Restrepo' } },
  { id: 4, timestamp: '2026-05-23T13:30:22', level: 'success', module: 'facturacion', description: 'Factura #FAC-2026-004 marcada como pagada — $560,000 COP recibidos', user: 'admin', ip: '192.168.1.10', metadata: { factura: 'FAC-2026-004', monto: 560000 } },
  { id: 5, timestamp: '2026-05-23T13:15:00', level: 'info', module: 'system', description: 'Respaldo automático completado exitosamente — 182 MB transferidos a nube', user: 'sistema', ip: '127.0.0.1', metadata: { size: '182MB', destination: 'cloud-backup' } },
  { id: 6, timestamp: '2026-05-23T12:55:37', level: 'warning', module: 'servicios', description: 'Servicio "Extracción Muela Juicio" desactivado — Sin confirmación del supervisor', user: 'c.ruiz', ip: '192.168.1.55', metadata: { servicio: 'extraccion-muela', requiresApproval: true } },
  { id: 7, timestamp: '2026-05-23T12:00:00', level: 'info', module: 'auth', description: 'Inicio de sesión exitoso — Odontólogo desde 192.168.1.45', user: 'odontologo', ip: '192.168.1.45', metadata: { role: 'odontologo', session: 'abc123' } },
];

let rawLogs = Array.isArray(window.RAZOR_BITACORA) && window.RAZOR_BITACORA.length > 0 ? window.RAZOR_BITACORA.map(b => ({
  id: b.id,
  timestamp: b.fecha,
  level: b.accion === 'DELETE' ? 'critical' : b.accion === 'UPDATE' ? 'warning' : 'info',
  module: b.tabla || 'system',
  description: b.descripcion,
  user: b.usuario,
  ip: b.ip,
  metadata: { tabla: b.tabla, accion: b.accion }
})) : SAMPLE_LOGS;

let logs = [...rawLogs];
let searchQuery = '';
let selectedLevel = '';
let selectedModule = '';
let selectedDate = '';
let currentPage = 1;
let itemsPerPage = 25;
let selectedIds = new Set();
let isLiveMode = true;

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════

const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur.toLocaleString('es-ES');
    if (cur >= target) clearInterval(t);
  }, 30);
};

// ═══════════════════════════════════════════════════════════════════
//  ✅ renderTable SIMPLIFICADA: Renderizado inmediato sin loading
// ═══════════════════════════════════════════════════════════════════
const renderTable = () => {
  const tbody = safeGetElement('logsTbody');
  const empty = safeGetElement('tableEmpty');
  
  if (!tbody) {
    console.error('❌ tbody no encontrado');
    return;
  }
  
  // Ocultar empty state por defecto
  if (empty) empty.hidden = true;
  
  // Filtrar datos
  const filtered = logs.filter(log =>
    (!searchQuery || log.description.toLowerCase().includes(searchQuery.toLowerCase()) || log.user.toLowerCase().includes(searchQuery.toLowerCase())) &&
    (!selectedLevel || log.level === selectedLevel) &&
    (!selectedModule || log.module === selectedModule) &&
    (!selectedDate || log.timestamp.startsWith(selectedDate))
  );
  
  // Paginar
  const totalPages = Math.ceil(filtered.length / itemsPerPage) || 1;
  if (currentPage > totalPages) currentPage = totalPages;
  
  const startIdx = (currentPage - 1) * itemsPerPage;
  const paginated = filtered.slice(startIdx, startIdx + itemsPerPage);
  
  // ✅ CASO: Sin resultados → mostrar empty state
  if (!paginated.length) {
    tbody.innerHTML = '';
    if (empty) {
      empty.hidden = false;
      announceToScreenReader('No se encontraron eventos con los filtros aplicados');
    }
    updatePagination(0, 0);
    return;
  }
  
  // Renderizar filas directamente (sin delay)
  tbody.innerHTML = paginated.map(log => {
    const levelDef = LOG_LEVELS[log.level] || LOG_LEVELS.info;
    const isSelected = selectedIds.has(log.id);
    
    return `
      <tr role="row" tabindex="-1" data-id="${log.id}" data-level="${log.level}" class="${isSelected ? 'selected' : ''}">
        <td class="checkbox-col">
          <input type="checkbox" class="log-checkbox" value="${log.id}" aria-label="Seleccionar evento ${log.id}" ${isSelected ? 'checked' : ''}>
        </td>
        <td><time datetime="${log.timestamp}">${fmtTimestamp(log.timestamp)}</time></td>
        <td><span class="badge-level ${levelDef.class}" role="status" aria-label="Nivel: ${levelDef.label}">${levelDef.icon} ${levelDef.label}</span></td>
        <td>${MODULES[log.module] || log.module}</td>
        <td><span class="log-desc" title="${log.description}">${log.description}</span></td>
        <td>${log.user}</td>
        <td class="actions-cell">
          <button class="btn-icon" title="Ver detalle" aria-label="Ver detalle del evento ${log.id}" data-action="view">🔍</button>
        </td>
      </tr>
    `;
  }).join('');
  
  // Agregar event listeners a las filas renderizadas
  tbody.querySelectorAll('tr[role="row"]').forEach(tr => {
    const logId = parseInt(tr.dataset.id, 10);
    const log = logs.find(l => l.id === logId);
    
    tr.addEventListener('click', (e) => {
      if (e.target.closest('.log-checkbox')) return;
      if (e.target.closest('[data-action="view"]')) {
        openLogModal(log);
      } else {
        toggleLogSelection(logId, tr);
      }
    });
    
    tr.querySelector('.log-checkbox')?.addEventListener('change', (e) => {
      toggleLogSelection(logId, tr, e.target.checked);
    });
    
    tr.querySelector('[data-action="view"]')?.addEventListener('click', (e) => {
      e.stopPropagation();
      openLogModal(log);
    });
  });
  
  // Actualizar UI
  updateBulkActions();
  updatePagination(filtered.length, paginated.length);
  updateTableCount(filtered.length);
  
  // Anunciar a screen readers
  announceToScreenReader(`${paginated.length} eventos cargados, página ${currentPage} de ${totalPages}`);
};

const updateTableCount = (count) => {
  const el = safeGetElement('tableCount');
  if (el) el.textContent = count.toLocaleString('es-ES');
};

const updateFilterBadge = () => {
  const badge = safeGetElement('filterBadge');
  const badgeText = safeGetElement('filterBadgeText');
  const searchInput = safeGetElement('searchLog');
  const searchClear = safeGetElement('searchClear');
  
  let activeCount = 0;
  if (searchInput?.value) activeCount++;
  if (selectedLevel) activeCount++;
  if (selectedModule) activeCount++;
  if (selectedDate) activeCount++;
  
  if (badge && badgeText) {
    if (activeCount > 0) {
      badge.hidden = false;
      badgeText.textContent = `${activeCount} filtro${activeCount > 1 ? 's' : ''} activo${activeCount > 1 ? 's' : ''}`;
    } else {
      badge.hidden = true;
    }
  }
  
  if (searchClear) {
    searchClear.hidden = !searchInput?.value;
  }
};

const updateBulkActions = () => {
  const bulkActions = safeGetElement('bulkActions');
  const bulkCount = safeGetElement('bulkCount');
  
  if (selectedIds.size > 0) {
    if (bulkActions) bulkActions.hidden = false;
    if (bulkCount) bulkCount.textContent = `${selectedIds.size} seleccionado${selectedIds.size > 1 ? 's' : ''}`;
  } else {
    if (bulkActions) bulkActions.hidden = true;
  }
};

const updatePagination = (total, count) => {
  const info = safeGetElement('paginationInfo');
  const buttons = safeGetElement('paginationButtons');
  const btnFirst = safeGetElement('btnFirst');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  const btnLast = safeGetElement('btnLast');
  const jumpInput = safeGetElement('jumpPage');
  
  if (!info || !buttons) return;
  
  const totalPages = Math.ceil(total / itemsPerPage) || 1;
  const start = total === 0 ? 0 : (currentPage - 1) * itemsPerPage + 1;
  const end = Math.min(currentPage * itemsPerPage, total);
  
  info.textContent = `Mostrando ${start}-${end} de ${total.toLocaleString('es-ES')} eventos`;
  
  if (btnFirst) {
    btnFirst.disabled = currentPage === 1;
    btnFirst.setAttribute('aria-disabled', currentPage === 1);
  }
  if (btnPrev) {
    btnPrev.disabled = currentPage === 1;
    btnPrev.setAttribute('aria-disabled', currentPage === 1);
  }
  if (btnNext) {
    btnNext.disabled = currentPage === totalPages;
    btnNext.setAttribute('aria-disabled', currentPage === totalPages);
  }
  if (btnLast) {
    btnLast.disabled = currentPage === totalPages;
    btnLast.setAttribute('aria-disabled', currentPage === totalPages);
  }
  if (jumpInput) {
    jumpInput.max = totalPages;
    jumpInput.value = currentPage;
  }
  
  buttons.innerHTML = '';
  
  const maxVisible = 5;
  let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
  let endPage = Math.min(totalPages, startPage + maxVisible - 1);
  if (endPage - startPage < maxVisible - 1) startPage = Math.max(1, endPage - maxVisible + 1);
  
  for (let i = startPage; i <= endPage; i++) {
    const btn = document.createElement('button');
    btn.className = `pagination-btn ${i === currentPage ? 'active' : ''}`;
    btn.textContent = i;
    btn.setAttribute('aria-label', `Ir a página ${i}`);
    btn.setAttribute('aria-current', i === currentPage ? 'page' : 'false');
    btn.addEventListener('click', () => { currentPage = i; renderTable(); });
    buttons.appendChild(btn);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL: DETALLE DE EVENTO
// ═══════════════════════════════════════════════════════════════════

const openLogModal = (log) => {
  const levelDef = LOG_LEVELS[log.level] || LOG_LEVELS.info;
  
  const levelBadge = safeGetElement('modalLevelBadge');
  const levelText = safeGetElement('modalLevelText');
  const title = safeGetElement('modalLogTitle');
  
  if (levelBadge) {
    levelBadge.className = `modal-level-badge ${levelDef.class}`;
    const icon = levelBadge.querySelector('.level-icon');
    if (icon) icon.textContent = levelDef.icon;
  }
  if (levelText) levelText.textContent = levelDef.label;
  if (title) title.textContent = `Evento #${log.id}`;
  
  safeGetElement('modalTimestamp').textContent = fmtTimestamp(log.timestamp);
  safeGetElement('modalTimestamp').setAttribute('datetime', log.timestamp);
  safeGetElement('modalModule').textContent = MODULES[log.module] || log.module;
  safeGetElement('modalUser').textContent = log.user;
  safeGetElement('modalIp').textContent = log.ip || '—';
  safeGetElement('modalDescription').textContent = log.description;
  
  const stackWrap = safeGetElement('modalStackTraceWrap');
  const stackPre = safeGetElement('modalStackTrace');
  if (stackWrap && stackPre) {
    if (log.stack) {
      stackWrap.hidden = false;
      stackPre.textContent = log.stack;
    } else {
      stackWrap.hidden = true;
    }
  }
  
  const metaWrap = safeGetElement('modalMetadataWrap');
  const metaPre = safeGetElement('modalMetadata');
  if (metaWrap && metaPre) {
    if (log.metadata && Object.keys(log.metadata).length > 0) {
      metaWrap.hidden = false;
      metaPre.textContent = JSON.stringify(log.metadata, null, 2);
    } else {
      metaWrap.hidden = true;
    }
  }
  
  const copyBtn = safeGetElement('modalLogCopy');
  const reportBtn = safeGetElement('modalLogReport');
  
  if (copyBtn) {
    copyBtn.onclick = async () => {
      try {
        const text = `Evento #${log.id}\nNivel: ${levelDef.label}\nMódulo: ${MODULES[log.module]}\nUsuario: ${log.user}\nFecha: ${fmtTimestamp(log.timestamp)}\n\n${log.description}`;
        await navigator.clipboard.writeText(text);
        showToast('📋 Detalles copiados al portapapeles');
      } catch {
        showToast('❌ Error al copiar', 'error');
      }
    };
  }
  
  if (reportBtn) {
    reportBtn.onclick = () => {
      showToast('🚨 Reporte enviado al equipo de soporte');
      closeLogModal();
    };
  }
  
  const modal = safeGetElement('modalLog');
  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    
    const firstFocusable = modal.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    if (firstFocusable) firstFocusable.focus();
    
    document.body.style.overflow = 'hidden';
    announceToScreenReader(`Detalle del evento ${log.id} cargado`);
  }
};

const closeLogModal = () => {
  const modal = safeGetElement('modalLog');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    
    const lastTrigger = document.querySelector('[data-last-trigger]');
    if (lastTrigger) {
      lastTrigger.focus();
      lastTrigger.removeAttribute('data-last-trigger');
    }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  SELECCIÓN MASIVA
// ═══════════════════════════════════════════════════════════════════

const toggleLogSelection = (id, row, forceState) => {
  const isChecked = forceState !== undefined ? forceState : !selectedIds.has(id);
  
  if (isChecked) {
    selectedIds.add(id);
    row?.classList.add('selected');
  } else {
    selectedIds.delete(id);
    row?.classList.remove('selected');
  }
  
  const selectAll = safeGetElement('selectAll');
  if (selectAll) {
    selectAll.checked = selectedIds.size === logs.length;
    selectAll.indeterminate = selectedIds.size > 0 && selectedIds.size < logs.length;
  }
  
  updateBulkActions();
};

const toggleSelectAll = (checked) => {
  const tbody = safeGetElement('logsTbody');
  if (!tbody) return;
  
  const visibleRows = tbody.querySelectorAll('tr[role="row"]');
  visibleRows.forEach(row => {
    const id = parseInt(row.dataset.id, 10);
    const checkbox = row.querySelector('.log-checkbox');
    
    if (checked) {
      selectedIds.add(id);
      row.classList.add('selected');
      if (checkbox) checkbox.checked = true;
    } else {
      selectedIds.delete(id);
      row.classList.remove('selected');
      if (checkbox) checkbox.checked = false;
    }
  });
  
  updateBulkActions();
};

const initBulkActions = () => {
  const selectAll = safeGetElement('selectAll');
  const btnMarkRead = safeGetElement('btnMarkRead');
  const btnArchive = safeGetElement('btnArchive');
  const btnClearSelection = safeGetElement('btnClearSelection');
  
  selectAll?.addEventListener('change', (e) => {
    toggleSelectAll(e.target.checked);
  });
  
  btnMarkRead?.addEventListener('click', () => {
    showToast(`👁️ ${selectedIds.size} evento${selectedIds.size > 1 ? 's' : ''} marcado${selectedIds.size > 1 ? 's' : ''} como leído${selectedIds.size > 1 ? 's' : ''}`);
    selectedIds.clear();
    renderTable();
  });
  
  btnArchive?.addEventListener('click', () => {
    showToast(`📦 ${selectedIds.size} evento${selectedIds.size > 1 ? 's' : ''} archivado${selectedIds.size > 1 ? 's' : ''}`);
    selectedIds.clear();
    renderTable();
  });
  
  btnClearSelection?.addEventListener('click', () => {
    selectedIds.clear();
    renderTable();
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS Y BÚSQUEDA
// ═══════════════════════════════════════════════════════════════════

const applyFilters = () => {
  searchQuery = safeGetElement('searchLog')?.value.toLowerCase() || '';
  selectedLevel = safeGetElement('filterLevel')?.value || '';
  selectedModule = safeGetElement('filterModule')?.value || '';
  selectedDate = safeGetElement('filterDate')?.value || '';
  
  currentPage = 1;
  renderTable();
  updateFilterBadge();
};

const resetFilters = () => {
  const searchInput = safeGetElement('searchLog');
  const filterLevel = safeGetElement('filterLevel');
  const filterModule = safeGetElement('filterModule');
  const filterDate = safeGetElement('filterDate');
  
  if (searchInput) searchInput.value = '';
  if (filterLevel) filterLevel.value = '';
  if (filterModule) filterModule.value = '';
  if (filterDate) filterDate.value = '';
  
  searchQuery = '';
  selectedLevel = '';
  selectedModule = '';
  selectedDate = '';
  currentPage = 1;
  
  renderTable();
  updateFilterBadge();
  showToast('🔄 Filtros reseteados');
};

// ═══════════════════════════════════════════════════════════════════
//  LIVE MODE & CLOCK
// ═══════════════════════════════════════════════════════════════════

const updateClock = () => {
  const el = safeGetElement('liveClock');
  if (el) {
    el.textContent = new Date().toLocaleTimeString('es-CO', {
      hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
    });
  }
};

const simulateLiveLogs = () => {
  if (!isLiveMode) return;
  
  if (Math.random() < 0.1) {
    const newLog = {
      id: Date.now(),
      timestamp: new Date().toISOString(),
      level: ['info', 'success', 'warning'][Math.floor(Math.random() * 3)],
      module: Object.keys(MODULES)[Math.floor(Math.random() * Object.keys(MODULES).length)],
      description: 'Evento automático generado en modo live',
      user: 'sistema',
      ip: '127.0.0.1',
    };
    
    logs.unshift(newLog);
    if (currentPage === 1) {
      renderTable();
      showToast('🔄 Nuevo evento en bitácora', 'info');
    }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  EXPORTACIÓN
// ═══════════════════════════════════════════════════════════════════

const openExportModal = () => {
  const modal = safeGetElement('modalExport');
  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    const closeBtn = safeGetElement('modalExportClose');
    if (closeBtn) closeBtn.focus();
    document.body.style.overflow = 'hidden';
  }
};

const closeExportModal = () => {
  const modal = safeGetElement('modalExport');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    
    const progress = safeGetElement('exportProgress');
    const progressBar = safeGetElement('exportProgressBar');
    if (progress) progress.hidden = true;
    if (progressBar) progressBar.style.width = '0%';
  }
};

const executeExport = async () => {
  const format = document.querySelector('input[name="exportFormat"]:checked')?.value || 'pdf';
  const range = document.querySelector('input[name="exportRange"]:checked')?.value || 'filtered';
  
  const progress = safeGetElement('exportProgress');
  const progressBar = safeGetElement('exportProgressBar');
  const progressText = safeGetElement('exportProgressText');
  const confirmBtn = safeGetElement('modalExportConfirm');
  
  if (progress) progress.hidden = false;
  if (progressText) progressText.textContent = 'Generando archivo...';
  if (confirmBtn) confirmBtn.disabled = true;
  
  let pct = 0;
  const interval = setInterval(() => {
    pct += Math.random() * 20;
    if (pct >= 100) {
      pct = 100;
      clearInterval(interval);
    }
    if (progressBar) progressBar.style.width = `${pct}%`;
    if (progressText && pct < 100) {
      const steps = ['Preparando datos...', 'Filtrando eventos...', 'Generando archivo...', 'Comprimiendo...', '¡Listo!'];
      progressText.textContent = steps[Math.min(Math.floor(pct / 20), steps.length - 1)];
    }
  }, 250);
  
  try {
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    clearInterval(interval);
    if (progressBar) progressBar.style.width = '100%';
    
    const fileName = `bitacora-${new Date().toISOString().split('T')[0]}.${format}`;
    showToast(`✅ ${fileName} descargado exitosamente`);
    
    setTimeout(closeExportModal, 1000);
    
  } catch (error) {
    console.warn('Error exportando:', error);
    showToast('❌ Error al generar el archivo', 'error');
  } finally {
    setTimeout(() => {
      if (confirmBtn) confirmBtn.disabled = false;
      if (progress) progress.hidden = true;
    }, 500);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
// ═══════════════════════════════════════════════════════════════════

async function fetchLogs(filters = {}) {
  try {
    return { logs: SAMPLE_LOGS, total: 3842, kpis: { total: 3842, critical: 7, warnings: 23 } };
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return { logs: SAMPLE_LOGS, total: 3842, kpis: { total: 3842, critical: 7, warnings: 23 } };
  }
}

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
    
    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

const initFilters = () => {
  const searchInput = safeGetElement('searchLog');
  const searchClear = safeGetElement('searchClear');
  const filterReset = safeGetElement('filterReset');
  const emptyResetBtn = safeGetElement('emptyResetBtn');
  
  searchInput?.addEventListener('input', debounce(applyFilters, 250));
  safeGetElement('filterLevel')?.addEventListener('change', applyFilters);
  safeGetElement('filterModule')?.addEventListener('change', applyFilters);
  safeGetElement('filterDate')?.addEventListener('change', applyFilters);
  
  searchClear?.addEventListener('click', () => {
    if (searchInput) {
      searchInput.value = '';
      searchInput.focus();
      applyFilters();
    }
  });
  
  const resetAll = () => { resetFilters(); };
  filterReset?.addEventListener('click', resetAll);
  emptyResetBtn?.addEventListener('click', resetAll);
  
  [searchInput, safeGetElement('filterLevel'), safeGetElement('filterModule'), safeGetElement('filterDate')]
    .forEach(el => el?.addEventListener('change', updateFilterBadge));
};

const initPagination = () => {
  const itemsPerPageSelect = safeGetElement('itemsPerPage');
  const jumpInput = safeGetElement('jumpPage');
  const jumpBtn = safeGetElement('jumpBtn');
  const btnFirst = safeGetElement('btnFirst');
  const btnLast = safeGetElement('btnLast');
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  
  itemsPerPageSelect?.addEventListener('change', (e) => {
    itemsPerPage = parseInt(e.target.value, 10) || 25;
    currentPage = 1;
    renderTable();
  });
  
  const jumpToPage = () => {
    const page = parseInt(jumpInput?.value, 10);
    const totalPages = Math.ceil(logs.length / itemsPerPage);
    if (page && page >= 1 && page <= totalPages) {
      currentPage = page;
      renderTable();
      jumpInput.value = '';
    } else {
      showToast(`⚠️ Página entre 1 y ${totalPages}`, 'warning');
    }
  };
  
  jumpBtn?.addEventListener('click', jumpToPage);
  jumpInput?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
  });
  
  btnFirst?.addEventListener('click', () => { if (currentPage > 1) { currentPage = 1; renderTable(); } });
  btnLast?.addEventListener('click', () => {
    const totalPages = Math.ceil(logs.length / itemsPerPage);
    if (currentPage < totalPages) { currentPage = totalPages; renderTable(); }
  });
  btnPrev?.addEventListener('click', () => { if (currentPage > 1) { currentPage--; renderTable(); } });
  btnNext?.addEventListener('click', () => {
    const totalPages = Math.ceil(logs.length / itemsPerPage);
    if (currentPage < totalPages) { currentPage++; renderTable(); }
  });
};

const initModals = () => {
  const modalLog = safeGetElement('modalLog');
  const modalLogClose = safeGetElement('modalLogClose');
  const modalLogCancel = safeGetElement('modalLogCancel');
  
  modalLogClose?.addEventListener('click', closeLogModal);
  modalLogCancel?.addEventListener('click', closeLogModal);
  modalLog?.addEventListener('click', (e) => { if (e.target === modalLog) closeLogModal(); });
  
  const btnExport = safeGetElement('btnExport');
  const modalExport = safeGetElement('modalExport');
  const modalExportClose = safeGetElement('modalExportClose');
  const modalExportCancel = safeGetElement('modalExportCancel');
  const modalExportConfirm = safeGetElement('modalExportConfirm');
  
  btnExport?.addEventListener('click', openExportModal);
  modalExportClose?.addEventListener('click', closeExportModal);
  modalExportCancel?.addEventListener('click', closeExportModal);
  modalExport?.addEventListener('click', (e) => { if (e.target === modalExport) closeExportModal(); });
  modalExportConfirm?.addEventListener('click', executeExport);
  
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      if (modalLog?.classList.contains('open')) { e.preventDefault(); closeLogModal(); }
      if (modalExport?.classList.contains('open')) { e.preventDefault(); closeExportModal(); }
    }
  });
};

const initLiveFeatures = () => {
  updateClock();
  setInterval(updateClock, 1000);
  
  const toggleLive = safeGetElement('toggleLive');
  toggleLive?.addEventListener('change', (e) => {
    isLiveMode = e.target.checked;
    showToast(isLiveMode ? '🔄 Actualización en vivo activada' : '⏸️ Actualización en vivo pausada');
  });
  
  setInterval(simulateLiveLogs, 10000);
  
  const btnRefresh = safeGetElement('btnRefresh');
  btnRefresh?.addEventListener('click', () => {
    renderTable();
    showToast('🔄 Bitácora actualizada');
  });
};

const initTableKeyboardNav = () => {
  const tbody = safeGetElement('logsTbody');
  if (!tbody) return;
  
  tbody.addEventListener('keydown', (e) => {
    const rows = Array.from(tbody.querySelectorAll('tr[role="row"]'));
    const currentRow = e.target.closest('tr[role="row"]');
    if (!currentRow) return;
    
    const currentIndex = rows.indexOf(currentRow);
    
    if (e.key === 'ArrowDown' && currentIndex < rows.length - 1) {
      e.preventDefault();
      rows[currentIndex + 1].focus();
    } else if (e.key === 'ArrowUp' && currentIndex > 0) {
      e.preventDefault();
      rows[currentIndex - 1].focus();
    } else if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      const btnView = currentRow.querySelector('[data-action="view"]');
      if (btnView) btnView.click();
    }
  });
  
  tbody.querySelectorAll('tr[role="row"]').forEach(row => {
    row.setAttribute('tabindex', '-1');
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Inicializar componentes de UI
  initSidebar();
  initFilters();
  initPagination();
  initModals();
  initLiveFeatures();
  initBulkActions();
  initTableKeyboardNav();
  
  // Cargar datos
  const data = await fetchLogs();
  logs = data.logs;
  
  // Actualizar KPIs
  animateCounter(safeGetElement('kpiTotal'), data.kpis?.total || 3842);
  animateCounter(safeGetElement('kpiCritical'), data.kpis?.critical || 7);
  animateCounter(safeGetElement('kpiWarnings'), data.kpis?.warnings || 23);
  
  // ✅ Renderizar tabla INMEDIATAMENTE (sin delay, sin loading)
  renderTable();
  updateFilterBadge();
  
  // Toast de bienvenida
  setTimeout(() => {
    showToast('✅ Bitácora cargada - Modo live activo');
  }, 500);
  
  // Cleanup
  window.addEventListener('beforeunload', () => {
    // Remover listeners en SPA real
  });
};

// ✅ Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);