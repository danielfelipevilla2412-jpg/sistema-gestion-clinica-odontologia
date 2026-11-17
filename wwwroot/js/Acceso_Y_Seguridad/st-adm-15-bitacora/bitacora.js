(function () {
  'use strict';

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  return el;
};

const t = (key, vars) => window.I18N.t(key, vars);

const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn(...args), delay);
  };
};

const showToast = (messageKey, type = 'success', vars) => {
  const titleKey = type === 'error' ? 'audit.toast_error_title'
    : type === 'warning' ? 'audit.toast_warning_title'
      : type === 'info' ? 'audit.toast_info_title'
        : 'audit.toast_success_title';
  window.ToastService[type](t(titleKey), t(messageKey, vars));
};

const announceToScreenReader = (messageKey, vars) => {
  const announcer = safeGetElement('announcer');
  if (announcer) {
    announcer.textContent = t(messageKey, vars);
    setTimeout(() => { announcer.textContent = ''; }, 1000);
  }
};

const fmtTimestamp = (iso) => {
  if (!iso) return t('audit.not_available');
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return t('audit.not_available');
  return date.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false
  }).replace(',', '');
};

const LOG_LEVELS = {
  critical: { labelKey: 'audit.level_critical', class: 'critical', icon: 'error' },
  error: { labelKey: 'audit.level_error', class: 'error', icon: 'warning' },
  warning: { labelKey: 'audit.level_warning', class: 'warning', icon: 'warning' },
  info: { labelKey: 'audit.level_info', class: 'info', icon: 'info' },
  success: { labelKey: 'audit.level_success', class: 'success', icon: 'check_circle' },
  debug: { labelKey: 'audit.level_debug', class: 'debug', icon: 'bug_report' },
};

const levelLabel = (level) => t((LOG_LEVELS[level] || LOG_LEVELS.info).labelKey);
const moduleLabel = (module) => String(module || t('audit.not_available'));
const escapeHtml = (value) => String(value ?? '')
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#39;');

const mapAuditRecord = (record) => {
  const action = String(record.accion || '').toUpperCase();
  return {
    id: record.id ?? record.idAuditoria,
    timestamp: record.fecha,
    level: action === 'DELETE' ? 'critical' : action === 'UPDATE' ? 'warning' : action === 'INSERT' ? 'success' : 'info',
    module: record.tabla || record.tablaAfectada || 'system',
    description: record.descripcion || t('audit.not_available'),
    user: record.usuario || t('audit.not_available'),
    ip: record.ip || record.ipOrigen || '',
    metadata: { tabla: record.tabla || record.tablaAfectada, accion: record.accion }
  };
};

const rawLogs = Array.isArray(window.RAZOR_BITACORA) ? window.RAZOR_BITACORA.map(mapAuditRecord) : [];

let logs = [...rawLogs];
let searchQuery = '';
let selectedLevel = '';
let selectedModule = '';
let selectedDate = '';
let currentPage = 1;
let itemsPerPage = 25;
let selectedIds = new Set();
let isLiveMode = true;
let timestampSortDirection = 'descending';
let modalTrigger = null;
let previousBodyOverflow = '';
let liveRefreshTimer = null;
let refreshInProgress = false;

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════

const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO');
    if (cur >= target) clearInterval(t);
  }, 30);
};

const getFilteredLogs = () => logs.filter(log =>
  (!searchQuery || String(log.description || '').toLowerCase().includes(searchQuery) || String(log.user || '').toLowerCase().includes(searchQuery)) &&
  (!selectedLevel || log.level === selectedLevel) &&
  (!selectedModule || log.module === selectedModule) &&
  (!selectedDate || String(log.timestamp || '').startsWith(selectedDate))
).sort((a, b) => {
  const difference = Date.parse(a.timestamp) - Date.parse(b.timestamp);
  if (!Number.isFinite(difference)) return 0;
  return timestampSortDirection === 'ascending' ? difference : -difference;
});

const updateKpis = () => {
  animateCounter(safeGetElement('kpiTotal'), logs.length);
  animateCounter(safeGetElement('kpiCritical'), logs.filter(log => log.level === 'critical').length);
  animateCounter(safeGetElement('kpiWarnings'), logs.filter(log => log.level === 'warning').length);
  animateCounter(safeGetElement('kpiUsers'), new Set(logs.map(log => log.user).filter(Boolean)).size);
};

// ═══════════════════════════════════════════════════════════════════
//  ✅ renderTable SIMPLIFICADA: Renderizado inmediato sin loading
// ═══════════════════════════════════════════════════════════════════
const renderTable = () => {
  const tbody = safeGetElement('logsTbody');
  const empty = safeGetElement('tableEmpty');
  
  if (!tbody) {
    console.error('[SmileTrack] Audit log table body is missing.');
    return;
  }
  
  // Ocultar empty state por defecto
  if (empty) empty.hidden = true;
  
  const filtered = getFilteredLogs();
  
  // Paginar
  const totalPages = Math.ceil(filtered.length / itemsPerPage) || 1;
  if (currentPage > totalPages) currentPage = totalPages;
  
  const startIdx = (currentPage - 1) * itemsPerPage;
  const paginated = filtered.slice(startIdx, startIdx + itemsPerPage);
  
  // ✅ CASO: Sin resultados → mostrar empty state
  if (!paginated.length) {
    tbody.innerHTML = '';
    const selectAll = safeGetElement('selectAll');
    if (selectAll) {
      selectAll.checked = false;
      selectAll.indeterminate = false;
    }
    if (empty) {
      empty.hidden = false;
      announceToScreenReader('audit.no_results_announcement');
    }
    updateBulkActions();
    updateTableCount(0);
    updatePagination(0, 0);
    return;
  }
  
  // Renderizar filas directamente (sin delay)
  tbody.innerHTML = paginated.map(log => {
    const levelDef = LOG_LEVELS[log.level] || LOG_LEVELS.info;
    const isSelected = selectedIds.has(log.id);
    
    return `
      <tr role="row" tabindex="-1" data-id="${escapeHtml(log.id)}" data-level="${escapeHtml(log.level)}" class="${isSelected ? 'selected' : ''}">
        <td class="checkbox-col">
            <input type="checkbox" class="log-checkbox" value="${escapeHtml(log.id)}" aria-label="${escapeHtml(t('audit.select_event_aria', { id: log.id }))}" ${isSelected ? 'checked' : ''}>
        </td>
          <td><time datetime="${escapeHtml(log.timestamp)}">${escapeHtml(fmtTimestamp(log.timestamp))}</time></td>
          <td><span class="badge-level ${levelDef.class}" aria-label="${escapeHtml(t('audit.level_named_aria', { level: levelLabel(log.level) }))}"><span class="material-symbols-outlined" aria-hidden="true">${levelDef.icon}</span>${escapeHtml(levelLabel(log.level))}</span></td>
          <td>${escapeHtml(moduleLabel(log.module))}</td>
          <td><span class="log-desc" title="${escapeHtml(log.description)}">${escapeHtml(log.description)}</span></td>
          <td>${escapeHtml(log.user || t('audit.not_available'))}</td>
        <td class="actions-cell">
            <button type="button" class="action-btn" title="${escapeHtml(t('audit.view_details'))}" aria-label="${escapeHtml(t('audit.view_details_aria', { id: log.id }))}" data-action="view">
              <span class="material-symbols-outlined" aria-hidden="true">visibility</span>
            </button>
        </td>
      </tr>
    `;
  }).join('');
  
  // Agregar event listeners a las filas renderizadas
  tbody.querySelectorAll('tr[role="row"]').forEach(tr => {
    const logId = parseInt(tr.dataset.id, 10);
    const log = logs.find(l => String(l.id) === tr.dataset.id);
    if (!log) return;
    
    tr.addEventListener('click', (e) => {
      if (e.target.closest('.log-checkbox')) return;
      if (e.target.closest('[data-action="view"]')) return;
      toggleLogSelection(logId, tr);
    });
    
    tr.querySelector('.log-checkbox')?.addEventListener('change', (e) => {
      toggleLogSelection(logId, tr, e.target.checked);
    });
    
    tr.querySelector('[data-action="view"]')?.addEventListener('click', (e) => {
      e.stopPropagation();
      openLogModal(log, e.currentTarget);
    });
  });
  
  // Actualizar UI
  updateBulkActions();
  updatePagination(filtered.length, paginated.length);
  updateTableCount(filtered.length);
  const selectAll = safeGetElement('selectAll');
  if (selectAll) {
    const visibleCheckboxes = Array.from(tbody.querySelectorAll('.log-checkbox'));
    const selectedVisible = visibleCheckboxes.filter(checkbox => checkbox.checked).length;
    selectAll.checked = visibleCheckboxes.length > 0 && selectedVisible === visibleCheckboxes.length;
    selectAll.indeterminate = selectedVisible > 0 && selectedVisible < visibleCheckboxes.length;
  }
  
  // Anunciar a screen readers
  announceToScreenReader('audit.events_loaded_announcement', {
    count: paginated.length,
    page: currentPage,
    totalPages
  });
};

const updateTableCount = (count) => {
  const el = safeGetElement('tableCount');
  if (el) el.textContent = count.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO');
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
      badgeText.textContent = t('audit.active_filter_count', { count: activeCount });
    } else {
      badge.hidden = true;
    }
  }
  
  if (searchClear) {
    searchClear.hidden = !searchInput?.value;
  }
};

const updateModuleOptions = () => {
  const select = safeGetElement('filterModule');
  if (!select) return;

  const selectedValue = selectedModule || select.value;
  const allOption = document.createElement('option');
  allOption.value = '';
  allOption.textContent = t('audit.all_modules');

  const options = [...new Set(logs.map(log => log.module).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b, window.I18N.lang));
  select.replaceChildren(allOption);
  options.forEach(module => {
    const option = document.createElement('option');
    option.value = module;
    option.textContent = moduleLabel(module);
    select.appendChild(option);
  });

  selectedModule = options.includes(selectedValue) ? selectedValue : '';
  select.value = selectedModule;
};

const updateBulkActions = () => {
  const bulkActions = safeGetElement('bulkActions');
  const bulkCount = safeGetElement('bulkCount');
  
  if (selectedIds.size > 0) {
    if (bulkActions) bulkActions.hidden = false;
    if (bulkCount) bulkCount.textContent = t('audit.selected_count', { count: selectedIds.size });
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
  const rangeStart = safeGetElement('paginationRangeStart');
  const rangeEnd = safeGetElement('paginationRangeEnd');
  const paginationTotal = safeGetElement('paginationTotal');
  if (rangeStart) rangeStart.textContent = start.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO');
  if (rangeEnd) rangeEnd.textContent = end.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO');
  if (paginationTotal) paginationTotal.textContent = total.toLocaleString(window.I18N.lang === 'en' ? 'en-US' : 'es-CO');
  
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
  if (total === 0) return;
  
  const maxVisible = 5;
  let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
  let endPage = Math.min(totalPages, startPage + maxVisible - 1);
  if (endPage - startPage < maxVisible - 1) startPage = Math.max(1, endPage - maxVisible + 1);
  
  for (let i = startPage; i <= endPage; i++) {
    const btn = document.createElement('button');
    btn.className = `pagination-btn ${i === currentPage ? 'active' : ''}`;
    btn.textContent = i;
    btn.setAttribute('aria-label', t('audit.go_to_page_aria', { page: i }));
    if (i === currentPage) btn.setAttribute('aria-current', 'page');
    btn.addEventListener('click', () => { currentPage = i; renderTable(); });
    buttons.appendChild(btn);
  }
};

const openDialog = (modal, trigger) => {
  if (!modal) return;
  modalTrigger = trigger || document.activeElement;
  previousBodyOverflow = document.body.style.overflow;
  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  document.body.classList.add('modal-open');
  document.body.style.overflow = 'hidden';

  const firstFocusable = modal.querySelector('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex]:not([tabindex="-1"])');
  if (firstFocusable) firstFocusable.focus();
};

const closeDialog = (modal) => {
  if (!modal) return;
  modal.classList.remove('open');
  modal.setAttribute('aria-hidden', 'true');
  modal.setAttribute('inert', '');

  if (!document.querySelector('.modal-overlay.open')) {
    document.body.classList.remove('modal-open');
    document.body.style.overflow = previousBodyOverflow;
  }

  const trigger = modalTrigger;
  modalTrigger = null;
  if (trigger && trigger.isConnected) trigger.focus();
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL: DETALLE DE EVENTO
// ═══════════════════════════════════════════════════════════════════

const openLogModal = (log, trigger) => {
  const levelDef = LOG_LEVELS[log.level] || LOG_LEVELS.info;
  
  const levelBadge = safeGetElement('modalLevelBadge');
  const levelText = safeGetElement('modalLevelText');
  const title = safeGetElement('modalLogTitle');
  
  if (levelBadge) {
    levelBadge.className = `modal-level-badge ${levelDef.class}`;
    const icon = levelBadge.querySelector('.level-icon');
    if (icon) icon.textContent = levelDef.icon;
  }
  if (levelText) levelText.textContent = levelLabel(log.level);
  if (title) title.textContent = t('audit.event_detail_title', { id: log.id });
  
  safeGetElement('modalTimestamp').textContent = fmtTimestamp(log.timestamp);
  safeGetElement('modalTimestamp').setAttribute('datetime', log.timestamp);
  safeGetElement('modalModule').textContent = moduleLabel(log.module);
  safeGetElement('modalUser').textContent = log.user || t('audit.not_available');
  safeGetElement('modalIp').textContent = log.ip || t('audit.not_available');
  safeGetElement('modalDescription').textContent = log.description || t('audit.not_available');
  
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
        const text = [
          t('audit.event_detail_title', { id: log.id }),
          `${t('audit.level')}: ${levelLabel(log.level)}`,
          `${t('audit.module')}: ${moduleLabel(log.module)}`,
          `${t('audit.user')}: ${log.user || t('audit.not_available')}`,
          `${t('audit.timestamp')}: ${fmtTimestamp(log.timestamp)}`,
          '',
          log.description || t('audit.not_available')
        ].join('\n');
        await navigator.clipboard.writeText(text);
        showToast('audit.copy_success');
      } catch {
        showToast('audit.copy_error', 'error');
      }
    };
  }
  
  if (reportBtn) {
    reportBtn.onclick = () => {
      showToast('audit.report_sent');
      closeLogModal();
    };
  }
  
  const modal = safeGetElement('modalLog');
  if (modal) {
    openDialog(modal, trigger);
    announceToScreenReader('audit.detail_loaded_announcement', { id: log.id });
  }
};

const closeLogModal = () => {
  closeDialog(safeGetElement('modalLog'));
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
    const visibleCheckboxes = Array.from(safeGetElement('logsTbody')?.querySelectorAll('.log-checkbox') || []);
    const selectedVisible = visibleCheckboxes.filter(checkbox => checkbox.checked).length;
    selectAll.checked = visibleCheckboxes.length > 0 && selectedVisible === visibleCheckboxes.length;
    selectAll.indeterminate = selectedVisible > 0 && selectedVisible < visibleCheckboxes.length;
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
    showToast('audit.mark_read_success', 'success', { count: selectedIds.size });
    selectedIds.clear();
    renderTable();
  });
  
  btnArchive?.addEventListener('click', () => {
    showToast('audit.archive_success', 'success', { count: selectedIds.size });
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
  showToast('audit.filters_reset');
};

// ═══════════════════════════════════════════════════════════════════
//  EXPORTACIÓN
// ═══════════════════════════════════════════════════════════════════

const openExportModal = (trigger) => {
  const modal = safeGetElement('modalExport');
  openDialog(modal, trigger);
};

const closeExportModal = () => {
  const modal = safeGetElement('modalExport');
  closeDialog(modal);
  const progress = safeGetElement('exportProgress');
  const progressBar = safeGetElement('exportProgressBar');
  if (progress) progress.hidden = true;
  if (progressBar) progressBar.style.width = '0%';
};

const executeExport = async () => {
  const format = document.querySelector('input[name="exportFormat"]:checked')?.value || 'pdf';
  const range = document.querySelector('input[name="exportRange"]:checked')?.value || 'filtered';
  
  const progress = safeGetElement('exportProgress');
  const progressBar = safeGetElement('exportProgressBar');
  const progressText = safeGetElement('exportProgressText');
  const confirmBtn = safeGetElement('modalExportConfirm');
  
  if (progress) progress.hidden = false;
  if (progressText) progressText.textContent = t('audit.export_generating');
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
      const steps = [
        'audit.export_preparing',
        'audit.export_filtering',
        'audit.export_generating',
        'audit.export_compressing',
        'audit.export_ready'
      ];
      progressText.textContent = t(steps[Math.min(Math.floor(pct / 20), steps.length - 1)]);
    }
  }, 250);
  
  try {
    await new Promise(resolve => setTimeout(resolve, 2000));
    
    clearInterval(interval);
    if (progressBar) progressBar.style.width = '100%';
    
    const fileName = `bitacora-${new Date().toISOString().split('T')[0]}.${format}`;
    showToast('audit.export_downloaded', 'success', { fileName, range: t(range === 'all' ? 'audit.export_all' : 'audit.export_filtered') });
    
    setTimeout(closeExportModal, 1000);
    
  } catch (error) {
    console.error('[SmileTrack] Audit export failed.', error);
    showToast('audit.export_error', 'error');
  } finally {
    setTimeout(() => {
      if (confirmBtn) confirmBtn.disabled = false;
      if (progress) progress.hidden = true;
    }, 500);
  }
};

const refreshLogs = async (notifySuccess = false) => {
  if (refreshInProgress) return;
  refreshInProgress = true;
  try {
    const response = await fetch('/api/v1/reportes/bitacora?page=1&pageSize=100', {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin'
    });
    if (!response.ok) throw new Error(`Audit log request failed (${response.status}).`);

    const result = await response.json();
    if (result.success !== true || !Array.isArray(result.data)) {
      throw new Error('The audit log response has an invalid shape.');
    }

    logs = result.data.map(mapAuditRecord);
    selectedIds = new Set([...selectedIds].filter(id => logs.some(log => log.id === id)));
    updateModuleOptions();
    updateKpis();
    renderTable();
    updateFilterBadge();
    if (notifySuccess) showToast('audit.refreshed', 'info');
  } catch (error) {
    console.error('[SmileTrack] Audit log refresh failed.', error);
    isLiveMode = false;
    const toggleLive = safeGetElement('toggleLive');
    if (toggleLive) toggleLive.checked = false;
    if (liveRefreshTimer) clearInterval(liveRefreshTimer);
    liveRefreshTimer = null;
    showToast('audit.refresh_error', 'error');
  } finally {
    refreshInProgress = false;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

const initFilters = () => {
  const searchInput = safeGetElement('searchLog');
  const searchClear = safeGetElement('searchClear');
  const filterReset = safeGetElement('filterReset');
  const emptyResetBtn = safeGetElement('emptyResetBtn');
  const sortTimestamp = safeGetElement('sortTimestamp');
  const timestampHeader = safeGetElement('timestampHeader');
  updateModuleOptions();
  
  searchInput?.addEventListener('input', debounce(applyFilters, 250));
  safeGetElement('filterLevel')?.addEventListener('change', applyFilters);
  safeGetElement('filterModule')?.addEventListener('change', applyFilters);
  safeGetElement('filterDate')?.addEventListener('change', applyFilters);
  sortTimestamp?.addEventListener('click', () => {
    timestampSortDirection = timestampSortDirection === 'descending' ? 'ascending' : 'descending';
    timestampHeader?.setAttribute('aria-sort', timestampSortDirection);
    renderTable();
  });
  
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
    const totalPages = Math.ceil(getFilteredLogs().length / itemsPerPage);
    if (page && page >= 1 && page <= totalPages) {
      currentPage = page;
      renderTable();
      jumpInput.value = '';
    } else {
      showToast('audit.page_out_of_range', 'warning', { totalPages });
    }
  };
  
  jumpBtn?.addEventListener('click', jumpToPage);
  jumpInput?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
  });
  
  btnFirst?.addEventListener('click', () => { if (currentPage > 1) { currentPage = 1; renderTable(); } });
  btnLast?.addEventListener('click', () => {
    const totalPages = Math.ceil(getFilteredLogs().length / itemsPerPage);
    if (currentPage < totalPages) { currentPage = totalPages; renderTable(); }
  });
  btnPrev?.addEventListener('click', () => { if (currentPage > 1) { currentPage--; renderTable(); } });
  btnNext?.addEventListener('click', () => {
    const totalPages = Math.ceil(getFilteredLogs().length / itemsPerPage);
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
  
  btnExport?.addEventListener('click', (event) => openExportModal(event.currentTarget));
  modalExportClose?.addEventListener('click', closeExportModal);
  modalExportCancel?.addEventListener('click', closeExportModal);
  modalExport?.addEventListener('click', (e) => { if (e.target === modalExport) closeExportModal(); });
  modalExportConfirm?.addEventListener('click', executeExport);
  
  document.addEventListener('keydown', (e) => {
    const activeModal = document.querySelector('.modal-overlay.open');
    if (!activeModal) return;

    if (e.key === 'Escape') {
      e.preventDefault();
      if (activeModal === modalLog) closeLogModal();
      else if (activeModal === modalExport) closeExportModal();
    } else if (e.key === 'Tab') {
      const focusable = Array.from(activeModal.querySelectorAll(
        'button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), a[href], [tabindex]:not([tabindex="-1"])'
      )).filter((element) => !element.hasAttribute('hidden'));
      if (focusable.length === 0) {
        e.preventDefault();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
  });
};

const initLiveFeatures = () => {
  const toggleLive = safeGetElement('toggleLive');
  const setLiveRefresh = (enabled) => {
    isLiveMode = enabled;
    if (liveRefreshTimer) clearInterval(liveRefreshTimer);
    liveRefreshTimer = enabled ? setInterval(() => refreshLogs(), 30000) : null;
  };

  if (toggleLive) setLiveRefresh(toggleLive.checked);
  toggleLive?.addEventListener('change', (e) => {
    setLiveRefresh(e.target.checked);
    showToast(isLiveMode ? 'audit.live_enabled' : 'audit.live_paused');
  });

  const btnRefresh = safeGetElement('btnRefresh');
  btnRefresh?.addEventListener('click', () => refreshLogs(true));
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

const init = () => {
  // Inicializar componentes de UI
  initFilters();
  initPagination();
  initModals();
  initLiveFeatures();
  initBulkActions();
  initTableKeyboardNav();
  
  updateKpis();
  
  // ✅ Renderizar tabla INMEDIATAMENTE (sin delay, sin loading)
  renderTable();
  updateFilterBadge();
  
  // Toast de bienvenida
  setTimeout(() => {
    showToast('audit.loaded');
  }, 500);
  
};

document.addEventListener('DOMContentLoaded', init);
document.addEventListener('i18n:change', () => {
  updateKpis();
  updateModuleOptions();
  renderTable();
  updateFilterBadge();
});
})();