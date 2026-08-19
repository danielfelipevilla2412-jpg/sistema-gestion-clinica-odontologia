/**
 * SmileTrack — Gestión de Roles y Permisos (script.js)
 * ✅ Premium: Accesibilidad AAA + Performance + UX Avanzada
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════
const CONFIG = {
  API_BASE: '/api',
  STORAGE_KEY: 'smiletrack_roles_v3',
  STORAGE_VERSION: '3.0',
  DEBOUNCE_MS: 150,
  ANIMATION_MS: 250,
  MAX_HISTORY: 20,
  CRITICAL_PERMISSIONS: [
    { role: 'admin', module: 'usuarios' },
    { role: 'admin', module: 'config' },
    { role: 'profesional', module: 'historia' },
  ],
};

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const $ = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const $$ = (selector, parent = document) => Array.from(parent.querySelectorAll(selector));

const debounce = (fn, delay) => {
  let timeout;
  return (...args) => {
    clearTimeout(timeout);
    timeout = setTimeout(() => fn.apply(this, args), delay);
  };
};

const throttle = (fn, limit) => {
  let inThrottle;
  return (...args) => {
    if (!inThrottle) {
      fn.apply(this, args);
      inThrottle = true;
      setTimeout(() => inThrottle = false, limit);
    }
  };
};

const announce = (message, priority = 'polite') => {
  const announcer = $('announcer');
  if (announcer) {
    announcer.setAttribute('aria-live', priority);
    announcer.textContent = '';
    setTimeout(() => { announcer.textContent = message; }, 100);
  }
};

const showToast = (message, type = 'success', duration = 4000) => {
  const toast = $('toast');
  const icon = $('toastIcon');
  const msg = $('toastMsg');
  if (!toast) return;

  const icons = { success: '✅', error: '❌', warning: '⚠️', info: 'ℹ️' };
  icon.textContent = icons[type] || icons.success;
  msg.textContent = message;
  toast.className = `toast ${type} show`;
  toast.hidden = false;
  toast.setAttribute('aria-hidden', 'false');

  clearTimeout(toast._timeout);
  toast._timeout = setTimeout(() => {
    toast.classList.remove('show');
    setTimeout(() => {
      toast.hidden = true;
      toast.setAttribute('aria-hidden', 'true');
    }, 250);
  }, duration);

  announce(message, 'assertive');
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO
// ═══════════════════════════════════════════════════════════════════

const MODULES = [
  { id: 'usuarios', name: 'Gestión de Usuarios', permissions: ['ver', 'crear', 'editar', 'eliminar'], critical: true },
  { id: 'pacientes', name: 'Gestión de Pacientes', permissions: ['ver', 'crear', 'editar', 'eliminar', 'historial'] },
  { id: 'citas', name: 'Gestión de Citas', permissions: ['ver', 'crear', 'editar', 'cancelar', 'confirmar'] },
  { id: 'historia', name: 'Historia Clínica', permissions: ['ver', 'crear', 'editar', 'odontograma'], critical: true },
  { id: 'facturacion', name: 'Facturación y Pagos', permissions: ['ver', 'crear', 'editar', 'anular'] },
  { id: 'reportes', name: 'Reportes', permissions: ['ver_financieros', 'ver_clinicos', 'exportar'] },
  { id: 'config', name: 'Configuración', permissions: ['ver', 'editar'], critical: true },
];

const ROLES = [
  { id: 'admin', name: 'Administrador', color: 'admin' },
  { id: 'profesional', name: 'Profesional', color: 'odo' },
  { id: 'auxiliar', name: 'Auxiliar', color: 'aux' },
  { id: 'recepcion', name: 'Recepción', color: 'rec' },
  { id: 'paciente', name: 'Paciente', color: 'pac' },
];

const LEVELS = {
  crud: { label: 'CRUD', desc: 'Crear, Leer, Actualizar, Eliminar', color: 'crud' },
  cru: { label: 'CRU', desc: 'Crear, Leer, Actualizar', color: 'cru' },
  ru: { label: 'R+U', desc: 'Leer y Actualizar', color: 'ru' },
  r: { label: 'Lectura', desc: 'Solo visualizar', color: 'r' },
  c: { label: 'Crear', desc: 'Solo registrar nuevos', color: 'c' },
  none: { label: 'Sin acceso', desc: 'Sin permisos', color: 'none' },
};

let state = {
  roles: {},
  pendingChanges: [],
  changeHistory: [],
  undoStack: [],
  redoStack: [],
  filters: { search: '', role: '', module: '' },
  sort: { by: 'name', dir: 'asc' },
  selection: { mode: 'none', cells: [] },
  templates: {},
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON VERSIONADO
// ═══════════════════════════════════════════════════════════════════

const storage = {
  load: () => {
    try {
      const raw = localStorage.getItem(CONFIG.STORAGE_KEY);
      if (!raw) { initializeDefaults(); return; }
      
      const data = JSON.parse(raw);
      
      // Migración de versiones
      if (data.version !== CONFIG.STORAGE_VERSION) {
        console.log(`Migrando de ${data.version} a ${CONFIG.STORAGE_VERSION}`);
        data = migrateData(data);
      }
      
      state.roles = data.roles || {};
      state.changeHistory = data.history || [];
      state.templates = data.templates || {};
      
      return true;
    } catch (e) {
      console.warn('Error cargando datos, usando defaults', e);
      initializeDefaults();
      return false;
    }
  },
  
  save: () => {
    try {
      localStorage.setItem(CONFIG.STORAGE_KEY, JSON.stringify({
        version: CONFIG.STORAGE_VERSION,
        roles: state.roles,
        history: state.changeHistory.slice(-CONFIG.MAX_HISTORY),
        templates: state.templates,
        lastUpdate: new Date().toISOString()
      }));
      return true;
    } catch (e) {
      console.error('Error guardando datos', e);
      showToast('❌ Error al guardar configuración', 'error');
      return false;
    }
  },
  
  addChange: (change) => {
    state.pendingChanges.push(change);
    state.undoStack.push({ ...change, timestamp: Date.now() });
    state.redoStack = []; // Clear redo on new change
    updatePendingBadge();
    addToHistory(change);
  },
  
  undo: () => {
    if (state.undoStack.length === 0) return false;
    const change = state.undoStack.pop();
    state.redoStack.push(change);
    
    // Revert change
    if (state.roles[change.roleId]?.[change.moduleId]) {
      state.roles[change.roleId][change.moduleId].level = change.oldLevel;
    }
    
    // Remove from pending
    state.pendingChanges = state.pendingChanges.filter(c => 
      !(c.roleId === change.roleId && c.moduleId === change.moduleId)
    );
    
    updatePendingBadge();
    renderMatrix();
    showToast('↩️ Cambio deshecho');
    announce('Cambio deshecho');
    return true;
  },
  
  redo: () => {
    if (state.redoStack.length === 0) return false;
    const change = state.redoStack.pop();
    state.undoStack.push(change);
    
    // Apply change
    if (!state.roles[change.roleId]) state.roles[change.roleId] = {};
    state.roles[change.roleId][change.moduleId] = {
      level: change.newLevel,
      permissions: getPermissionsForLevel(change.newLevel, 
        MODULES.find(m => m.id === change.moduleId)?.permissions || [])
    };
    
    // Add to pending if not already there
    if (!state.pendingChanges.some(c => c.roleId === change.roleId && c.moduleId === change.moduleId)) {
      state.pendingChanges.push(change);
    }
    
    updatePendingBadge();
    renderMatrix();
    showToast('↪️ Cambio rehecho');
    announce('Cambio rehecho');
    return true;
  },
};

const migrateData = (data) => {
  // Placeholder para migraciones futuras
  if (!data.version) {
    // v1 → v2
    data.version = '2.0';
  }
  if (data.version === '2.0') {
    // v2 → v3: Add history field
    data.history = [];
    data.version = '3.0';
  }
  return data;
};

const initializeDefaults = () => {
  ROLES.forEach(role => {
    if (!state.roles[role.id]) {
      state.roles[role.id] = {};
      MODULES.forEach(mod => {
        let defaultLevel = 'none';
        if (role.id === 'admin') defaultLevel = 'crud';
        else if (role.id === 'profesional' && ['historia', 'citas', 'pacientes'].includes(mod.id)) defaultLevel = 'cru';
        else if (role.id === 'recepcion' && ['citas', 'pacientes', 'facturacion'].includes(mod.id)) defaultLevel = 'cru';
        else if (role.id === 'auxiliar' && ['historia', 'citas'].includes(mod.id)) defaultLevel = 'ru';
        else if (role.id === 'paciente' && ['historia', 'citas'].includes(mod.id)) defaultLevel = 'r';
        
        state.roles[role.id][mod.id] = {
          level: defaultLevel,
          permissions: getPermissionsForLevel(defaultLevel, mod.permissions)
        };
      });
    }
  });
};

const getPermissionsForLevel = (level, availablePerms) => {
  const map = {
    'crud': [...availablePerms],
    'cru': availablePerms.filter(p => p !== 'eliminar'),
    'ru': availablePerms.filter(p => ['ver', 'editar', 'historial', 'odontograma'].includes(p)),
    'r': availablePerms.filter(p => p === 'ver' || p.includes('ver_')),
    'c': availablePerms.filter(p => p === 'crear'),
    'none': []
  };
  return map[level] || [];
};

const addToHistory = (change) => {
  state.changeHistory.unshift({
    ...change,
    timestamp: new Date().toISOString(),
    roleName: roleLabel(change.roleId),
    moduleName: moduleLabel(change.moduleId)
  });
  renderHistory();
};

// ═══════════════════════════════════════════════════════════════════
//  RENDERIZADO DE MATRIZ (CON VIRTUALIZACIÓN BÁSICA)
// ═══════════════════════════════════════════════════════════════════

const renderMatrix = () => {
  const body = $('matrixBody');
  const empty = $('matrixEmpty');
  const loading = $('matrixLoading');
  if (!body) return;

  // Mostrar loading para UX
  if (loading) loading.hidden = false;
  if (empty) empty.hidden = true;

  // Simular pequeño delay para UX en carga inicial
  setTimeout(() => {
    // Filtrar y ordenar módulos
    let filtered = MODULES.filter(mod => {
      const matchesSearch = !state.filters.search || 
        mod.name.toLowerCase().includes(state.filters.search.toLowerCase()) ||
        mod.id.toLowerCase().includes(state.filters.search.toLowerCase());
      const matchesModule = !state.filters.module || mod.id === state.filters.module;
      return matchesSearch && matchesModule;
    });

    // Ordenar
    if (state.sort.by === 'name') {
      filtered.sort((a, b) => 
        state.sort.dir === 'asc' 
          ? a.name.localeCompare(b.name)
          : b.name.localeCompare(a.name)
      );
    }

    if (filtered.length === 0) {
      body.innerHTML = '';
      if (empty) empty.hidden = false;
      if (loading) loading.hidden = true;
      announce('No se encontraron permisos con los filtros aplicados');
      return;
    }
    if (empty) empty.hidden = true;

    // Renderizar filas (virtualización básica: solo renderizar visibles + buffer)
    const visibleRows = 15; // Ajustar según necesidad
    const rowsToRender = filtered.slice(0, visibleRows);
    
    body.innerHTML = rowsToRender.map(mod => {
      const cells = ROLES.map(role => {
        // Aplicar filtro por rol
        if (state.filters.role && role.id !== state.filters.role) {
          return `<div class="perm-cell none" role="gridcell" aria-disabled="true" tabindex="-1" title="Filtrado">—</div>`;
        }
        
        const perm = state.roles[role.id]?.[mod.id] || { level: 'none', permissions: [] };
        const level = perm.level;
        const perms = perm.permissions;
        const isSelected = state.selection.cells.some(c => c.roleId === role.id && c.moduleId === mod.id);
        
        // Tooltip descriptivo
        const tooltip = `${moduleLabel(mod.id)} — ${levelLabel(level)}: ${perms.join(', ') || 'ninguno'}`;
        
        return `
          <button class="perm-cell ${level}${isSelected ? ' selected' : ''}" 
                  data-role="${role.id}" 
                  data-module="${mod.id}"
                  data-level="${level}"
                  role="gridcell"
                  tabindex="0"
                  title="${tooltip}"
                  aria-label="${roleLabel(role.id)}: ${levelLabel(level)} en ${mod.name}. Presiona Enter para editar, Espacio para ciclo rápido"
                  aria-pressed="false"
                  ${isSelected ? 'aria-selected="true"' : ''}>
            ${level.toUpperCase()}
          </button>
        `;
      }).join('');
      
      return `
        <div class="matrix-row" role="row" tabindex="-1" data-module="${mod.id}">
          <div class="matrix-row-module" role="rowheader">${mod.name}</div>
          <div class="matrix-row-roles" role="rowgroup">
            ${cells}
          </div>
        </div>
      `;
    }).join('');

    // Agregar event listeners
    setupMatrixEvents();
    
    // Indicador de más filas si aplica
    if (filtered.length > visibleRows) {
      body.insertAdjacentHTML('beforeend', 
        `<div class="matrix-row" role="row" aria-label="${filtered.length - visibleRows} filas más disponibles. Usa scroll para verlas.">
          <div class="matrix-row-module">... y ${filtered.length - visibleRows} módulos más</div>
          <div class="matrix-row-roles"></div>
        </div>`
      );
    }
    
    if (loading) loading.hidden = true;
    announce(`${filtered.length} módulos cargados en la matriz`);
    
  }, CONFIG.ANIMATION_MS);
};

const setupMatrixEvents = () => {
  const cells = $$('.perm-cell:not([aria-disabled="true"])', $('matrixBody'));
  
  cells.forEach(cell => {
    // Click para editar
    cell.addEventListener('click', (e) => {
      if (e.ctrlKey || e.metaKey) {
        // Selección múltiple
        toggleCellSelection(cell);
      } else if (e.shiftKey && state.selection.cells.length > 0) {
        // Selección de rango
        selectRange(cell);
      } else {
        // Edición individual
        if (state.selection.mode === 'bulk' && state.selection.cells.length > 1) {
          openBulkEditModal();
        } else {
          openPermissionModal(
            cell.dataset.role, 
            cell.dataset.module, 
            cell.dataset.level
          );
        }
      }
    });
    
    // Keyboard navigation
    cell.addEventListener('keydown', (e) => {
      const row = cell.closest('.matrix-row');
      const rowIndex = Array.from(row.parentNode.children).indexOf(row);
      const colIndex = Array.from(cell.parentNode.children).indexOf(cell);
      
      switch(e.key) {
        case 'ArrowRight':
          e.preventDefault();
          focusCell(rowIndex, colIndex + 1);
          break;
        case 'ArrowLeft':
          e.preventDefault();
          focusCell(rowIndex, colIndex - 1);
          break;
        case 'ArrowDown':
          e.preventDefault();
          focusCell(rowIndex + 1, colIndex);
          break;
        case 'ArrowUp':
          e.preventDefault();
          focusCell(rowIndex - 1, colIndex);
          break;
        case 'Home':
          e.preventDefault();
          focusCell(rowIndex, 0);
          break;
        case 'End':
          e.preventDefault();
          focusCell(rowIndex, ROLES.length - 1);
          break;
        case 'Enter':
          e.preventDefault();
          openPermissionModal(cell.dataset.role, cell.dataset.module, cell.dataset.level);
          break;
        case ' ':
          e.preventDefault();
          cyclePermissionLevel(cell);
          break;
      }
    });
    
    // Space para ciclo rápido
    cell.addEventListener('keypress', (e) => {
      if (e.key === ' ' && !e.target.closest('input')) {
        e.preventDefault();
        cyclePermissionLevel(cell);
      }
    });
  });
};

const focusCell = (rowIndex, colIndex) => {
  const rows = $$('.matrix-row', $('matrixBody'));
  if (!rows[rowIndex]) return;
  
  const cells = $$('.perm-cell:not([aria-disabled="true"])', rows[rowIndex].querySelector('.matrix-row-roles'));
  if (cells[colIndex]) {
    cells[colIndex].focus();
  }
};

const cyclePermissionLevel = (cell) => {
  const levels = ['crud', 'cru', 'ru', 'r', 'c', 'none'];
  const current = cell.dataset.level;
  const currentIndex = levels.indexOf(current);
  const next = levels[(currentIndex + 1) % levels.length];
  
  applyPermissionChange(cell.dataset.role, cell.dataset.module, next);
  cell.dataset.level = next;
  cell.className = `perm-cell ${next}`;
  cell.textContent = next.toUpperCase();
  
  // Actualizar tooltip
  const mod = MODULES.find(m => m.id === cell.dataset.module);
  const perms = state.roles[cell.dataset.role]?.[cell.dataset.module]?.permissions || [];
  cell.title = `${moduleLabel(mod.id)} — ${levelLabel(next)}: ${perms.join(', ') || 'ninguno'}`;
  
  announce(`${roleLabel(cell.dataset.role)} en ${moduleLabel(mod.id)}: ${levelLabel(next)}`);
};

const toggleCellSelection = (cell) => {
  const key = `${cell.dataset.role}|${cell.dataset.module}`;
  const idx = state.selection.cells.findIndex(c => `${c.roleId}|${c.moduleId}` === key);
  
  if (idx === -1) {
    state.selection.cells.push({ roleId: cell.dataset.role, moduleId: cell.dataset.module });
    cell.setAttribute('aria-selected', 'true');
    cell.classList.add('selected');
  } else {
    state.selection.cells.splice(idx, 1);
    cell.setAttribute('aria-selected', 'false');
    cell.classList.remove('selected');
  }
  
  state.selection.mode = state.selection.cells.length > 1 ? 'bulk' : 'none';
};

const selectRange = (endCell) => {
  if (state.selection.cells.length === 0) return;
  
  const start = state.selection.cells[0];
  const end = { roleId: endCell.dataset.role, moduleId: endCell.dataset.module };
  
  // Simple range selection (misma fila o columna)
  const startRowIndex = MODULES.findIndex(m => m.id === start.moduleId);
  const endRowIndex = MODULES.findIndex(m => m.id === end.moduleId);
  const startColIndex = ROLES.findIndex(r => r.id === start.roleId);
  const endColIndex = ROLES.findIndex(r => r.id === end.roleId);
  
  const minRow = Math.min(startRowIndex, endRowIndex);
  const maxRow = Math.max(startRowIndex, endRowIndex);
  const minCol = Math.min(startColIndex, endColIndex);
  const maxCol = Math.max(startColIndex, endColIndex);
  
  state.selection.cells = [];
  
  for (let r = minRow; r <= maxRow; r++) {
    for (let c = minCol; c <= maxCol; c++) {
      const moduleId = MODULES[r]?.id;
      const roleId = ROLES[c]?.id;
      if (moduleId && roleId) {
        state.selection.cells.push({ roleId, moduleId });
      }
    }
  }
  
  // Update UI
  $$('.perm-cell').forEach(cell => {
    const key = `${cell.dataset.role}|${cell.dataset.module}`;
    const isSelected = state.selection.cells.some(c => `${c.roleId}|${c.moduleId}` === key);
    cell.setAttribute('aria-selected', isSelected);
    cell.classList.toggle('selected', isSelected);
  });
  
  state.selection.mode = 'bulk';
};

// ═══════════════════════════════════════════════════════════════════
//  MODALES
// ═══════════════════════════════════════════════════════════════════

const openPermissionModal = (roleId, moduleId, currentLevel) => {
  const role = ROLES.find(r => r.id === roleId);
  const mod = MODULES.find(m => m.id === moduleId);
  if (!role || !mod) return;
  
  // Actualizar contenido
  $('modalModule').textContent = mod.name;
  $('modalRole').textContent = roleLabel(roleId);
  
  // Seleccionar radio actual
  $$('input[name="level"]').forEach(radio => {
    radio.checked = radio.value === currentLevel;
  });
  
  // Mostrar modal con focus trap
  const modal = $('modalPerm');
  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    
    // Focus trap
    trapFocus(modal);
    
    // Bloquear scroll
    document.body.style.overflow = 'hidden';
  }
  
  // Guardar contexto
  modal.dataset.roleId = roleId;
  modal.dataset.moduleId = moduleId;
  
  announce(`Editando permiso para ${roleLabel(roleId)} en ${mod.name}`);
};

const closePermissionModal = () => {
  const modal = $('modalPerm');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    releaseFocusTrap();
  }
};

const openBulkEditModal = () => {
  // Placeholder para edición masiva
  showToast('ℹ️ Edición masiva: selecciona un nivel y se aplicará a todas las celdas seleccionadas');
  // Implementar modal específico si se requiere
};

const openConfirmModal = () => {
  const modal = $('modalConfirm');
  const preview = $('changesPreview');
  const count = $('changesCount');
  
  if (!modal || !preview || !count) return;
  
  count.textContent = state.pendingChanges.length;
  
  preview.innerHTML = state.pendingChanges.map(change => `
    <div class="change-item" role="listitem">
      <span class="role-badge ${change.roleId}" aria-hidden="true">${roleLabel(change.roleId)}</span>
      <span class="change-arrow" aria-hidden="true">→</span>
      <strong>${moduleLabel(change.moduleId)}</strong>
      <span class="visually-hidden">cambiado de</span>
      <span aria-hidden="true">${change.oldLevel.toUpperCase()} → ${change.newLevel.toUpperCase()}</span>
    </div>
  `).join('');
  
  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  
  trapFocus(modal);
  document.body.style.overflow = 'hidden';
  
  $('modalConfirmCancel')?.focus();
  
  announce(`${state.pendingChanges.length} cambios listos para guardar`);
};

const closeConfirmModal = () => {
  const modal = $('modalConfirm');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    document.body.style.overflow = '';
    releaseFocusTrap();
  }
};

// Focus trap utilities
let lastFocusedElement = null;
const focusableSelectors = 'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

const trapFocus = (modal) => {
  lastFocusedElement = document.activeElement;
  const focusable = $$(focusableSelectors, modal);
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  
  const handleTab = (e) => {
    if (e.key !== 'Tab') return;
    
    if (e.shiftKey) {
      if (document.activeElement === first) {
        e.preventDefault();
        last.focus();
      }
    } else {
      if (document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
  };
  
  modal._handleTab = handleTab;
  modal.addEventListener('keydown', handleTab);
  
  if (first) first.focus();
};

const releaseFocusTrap = () => {
  const modal = $('modalPerm') || $('modalConfirm');
  if (modal?._handleTab) {
    modal.removeEventListener('keydown', modal._handleTab);
    delete modal._handleTab;
  }
  if (lastFocusedElement) {
    lastFocusedElement.focus?.();
    lastFocusedElement = null;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  LÓGICA DE PERMISOS
// ═══════════════════════════════════════════════════════════════════

const applyPermissionChange = (roleId, moduleId, newLevel) => {
  const oldLevel = state.roles[roleId]?.[moduleId]?.level || 'none';
  if (oldLevel === newLevel) return false;
  
  // Validar permisos críticos
  if (oldLevel !== 'none' && newLevel === 'none' && isCritical(roleId, moduleId)) {
    if (!confirm(`⚠️ Estás quitando acceso crítico a "${moduleLabel(moduleId)}" para "${roleLabel(roleId)}". ¿Continuar?`)) {
      return false;
    }
  }
  
  // Aplicar cambio
  if (!state.roles[roleId]) state.roles[roleId] = {};
  const mod = MODULES.find(m => m.id === moduleId);
  state.roles[roleId][moduleId] = {
    level: newLevel,
    permissions: getPermissionsForLevel(newLevel, mod?.permissions || [])
  };
  
  // Registrar cambio
  storage.addChange({
    roleId, moduleId, oldLevel, newLevel,
    timestamp: new Date().toISOString()
  });
  
  return true;
};

const isCritical = (roleId, moduleId) => {
  return CONFIG.CRITICAL_PERMISSIONS.some(c => c.role === roleId && c.module === moduleId);
};

const roleLabel = (id) => ROLES.find(r => r.id === id)?.name || id;
const moduleLabel = (id) => MODULES.find(m => m.id === id)?.name || id;
const levelLabel = (level) => LEVELS[level]?.label || level;

// ═══════════════════════════════════════════════════════════════════
//  UI UPDATES
// ═══════════════════════════════════════════════════════════════════

const updatePendingBadge = () => {
  const badge = $('pendingBadge');
  const count = $('pendingCount');
  const saveBtn = $('btnSave');
  
  if (!badge || !count || !saveBtn) return;
  
  const pending = state.pendingChanges.length;
  if (pending > 0) {
    badge.classList.remove('hidden');
    count.textContent = pending;
    saveBtn.disabled = false;
    saveBtn.setAttribute('aria-disabled', 'false');
    saveBtn.innerHTML = `<span aria-hidden="true">💾</span> <span>Guardar ${pending}</span>`;
  } else {
    badge.classList.add('hidden');
    saveBtn.disabled = true;
    saveBtn.setAttribute('aria-disabled', 'true');
    saveBtn.innerHTML = `<span aria-hidden="true">💾</span> <span>Guardar</span>`;
  }
};

const renderHistory = () => {
  const list = $('historyList');
  if (!list) return;
  
  if (state.changeHistory.length === 0) {
    list.innerHTML = '<li class="history-item">Sin cambios recientes</li>';
    return;
  }
  
  list.innerHTML = state.changeHistory.slice(0, CONFIG.MAX_HISTORY).map(change => `
    <li class="history-item">
      <span class="role-badge ${change.roleId}" aria-hidden="true">${change.roleName}</span>
      <span>${change.moduleName}</span>
      <span class="visually-hidden">cambiado de</span>
      <span aria-hidden="true">${change.oldLevel.toUpperCase()}→${change.newLevel.toUpperCase()}</span>
      <time class="history-time" datetime="${change.timestamp}" title="${new Date(change.timestamp).toLocaleString()}">
        ${formatTimeAgo(change.timestamp)}
      </time>
    </li>
  `).join('');
};

const formatTimeAgo = (timestamp) => {
  const diff = Date.now() - new Date(timestamp).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'Ahora';
  if (minutes < 60) return `hace ${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `hace ${hours}h`;
  return `hace ${Math.floor(hours / 24)}d`;
};

const renderActiveFilters = () => {
  const container = $('activeFilters');
  if (!container) return;
  
  const chips = [];
  if (state.filters.search) chips.push({ label: `Buscar: "${state.filters.search}"`, key: 'search' });
  if (state.filters.role) chips.push({ label: `Rol: ${roleLabel(state.filters.role)}`, key: 'role', value: state.filters.role });
  if (state.filters.module) chips.push({ label: `Módulo: ${moduleLabel(state.filters.module)}`, key: 'module', value: state.filters.module });
  
  if (chips.length === 0) {
    container.classList.add('hidden');
    return;
  }
  
  container.classList.remove('hidden');
  container.innerHTML = chips.map(chip => `
    <span class="filter-chip" role="listitem">
      ${chip.label}
      <button type="button" data-filter="${chip.key}" data-value="${chip.value || ''}" aria-label="Quitar filtro ${chip.label}">×</button>
    </span>
  `).join('');
  
  // Event listeners para quitar filtros
  $$('.filter-chip button', container).forEach(btn => {
    btn.addEventListener('click', () => {
      const key = btn.dataset.filter;
      const value = btn.dataset.value;
      
      if (key === 'search') state.filters.search = '';
      else if (key === 'role') state.filters.role = '';
      else if (key === 'module') state.filters.module = '';
      
      // Actualizar inputs
      if ($('searchMatrix')) $('searchMatrix').value = state.filters.search;
      if ($('filterRole')) $('filterRole').value = state.filters.role;
      if ($('filterModule')) $('filterModule').value = state.filters.module;
      
      renderMatrix();
      renderActiveFilters();
      showToast('🔄 Filtro removido');
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  EXPORTAR / IMPORTAR
// ═══════════════════════════════════════════════════════════════════

const exportConfig = (format = 'json') => {
  const data = {
    version: CONFIG.STORAGE_VERSION,
    exportedAt: new Date().toISOString(),
    roles: state.roles,
    metadata: {
      totalRoles: ROLES.length,
      totalModules: MODULES.length,
      changesCount: state.pendingChanges.length
    }
  };
  
  let content, filename, mimeType;
  
  if (format === 'csv') {
    const headers = ['Rol', 'Módulo', 'Nivel', 'Permisos'];
    const rows = [];
    ROLES.forEach(role => {
      MODULES.forEach(mod => {
        const perm = state.roles[role.id]?.[mod.id];
        if (perm) {
          rows.push([
            roleLabel(role.id),
            mod.name,
            perm.level.toUpperCase(),
            perm.permissions.join('; ')
          ]);
        }
      });
    });
    content = [headers, ...rows].map(r => r.map(c => `"${c}"`).join(',')).join('\n');
    filename = `smiletrack-permisos-${new Date().toISOString().split('T')[0]}.csv`;
    mimeType = 'text/csv;charset=utf-8';
  } else {
    content = JSON.stringify(data, null, 2);
    filename = `smiletrack-permisos-${new Date().toISOString().split('T')[0]}.json`;
    mimeType = 'application/json';
  }
  
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
  
  showToast(`📥 Exportado: ${filename}`);
  announce(`Configuración exportada a ${filename}`);
};

// ═══════════════════════════════════════════════════════════════════
//  EVENT HANDLERS
// ═══════════════════════════════════════════════════════════════════

const initEvents = () => {
  // Búsqueda
  $('searchMatrix')?.addEventListener('input', debounce((e) => {
    state.filters.search = e.target.value.toLowerCase();
    $('searchClear').hidden = !e.target.value;
    renderMatrix();
    renderActiveFilters();
  }, CONFIG.DEBOUNCE_MS));
  
  $('searchClear')?.addEventListener('click', () => {
    $('searchMatrix').value = '';
    state.filters.search = '';
    $('searchClear').hidden = true;
    renderMatrix();
    renderActiveFilters();
    $('searchMatrix').focus();
  });
  
  // Filtros
  $('filterRole')?.addEventListener('change', (e) => {
    state.filters.role = e.target.value;
    renderMatrix();
    renderActiveFilters();
  });
  
  $('filterModule')?.addEventListener('change', (e) => {
    state.filters.module = e.target.value;
    renderMatrix();
    renderActiveFilters();
  });
  
  $('btnResetFilters')?.addEventListener('click', () => {
    state.filters = { search: '', role: '', module: '' };
    $('searchMatrix').value = '';
    $('filterRole').value = '';
    $('filterModule').value = '';
    $('searchClear').hidden = true;
    renderMatrix();
    renderActiveFilters();
    showToast('🔄 Filtros reseteados');
  });
  
  $('emptyResetBtn')?.addEventListener('click', () => {
    $('btnResetFilters').click();
  });
  
  // Ordenar módulos
  $('btnSortModules')?.addEventListener('click', () => {
    state.sort.dir = state.sort.dir === 'asc' ? 'desc' : 'asc';
    $('btnSortModules').setAttribute('aria-sort', state.sort.dir === 'asc' ? 'ascending' : 'descending');
    renderMatrix();
  });
  
  // Panel de ayuda
  $('btnShortcuts')?.addEventListener('click', () => {
    const panel = $('helpPanel');
    if (panel) {
      panel.classList.toggle('hidden');
      if (!panel.classList.contains('hidden')) {
        panel.querySelector('.help-close')?.focus();
      }
    }
  });
  
  $('helpClose')?.addEventListener('click', () => {
    $('helpPanel')?.classList.add('hidden');
    $('btnShortcuts')?.focus();
  });
  
  // Historial
  $('btnClearHistory')?.addEventListener('click', () => {
    if (confirm('¿Limpiar historial de cambios?')) {
      state.changeHistory = [];
      renderHistory();
      showToast('🗑️ Historial limpiado');
    }
  });
  
  // Modales
  $('modalPermClose')?.addEventListener('click', closePermissionModal);
  $('modalPermCancel')?.addEventListener('click', closePermissionModal);
  $('modalPerm')?.addEventListener('click', (e) => {
    if (e.target.id === 'modalPerm') closePermissionModal();
  });
  
  $('formPerm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const modal = $('modalPerm');
    const roleId = modal.dataset.roleId;
    const moduleId = modal.dataset.moduleId;
    const newLevel = document.querySelector('input[name="level"]:checked')?.value;
    
    if (roleId && moduleId && newLevel) {
      if (applyPermissionChange(roleId, moduleId, newLevel)) {
        renderMatrix();
        showToast(`✅ ${roleLabel(roleId)} → ${moduleLabel(moduleId)}: ${levelLabel(newLevel)}`);
      }
    }
    closePermissionModal();
  });
  
  // Guardar configuración
  $('btnSave')?.addEventListener('click', () => {
    if (state.pendingChanges.length === 0) return;
    openConfirmModal();
  });
  
  $('modalConfirmCancel')?.addEventListener('click', closeConfirmModal);
  $('modalConfirm')?.addEventListener('click', (e) => {
    if (e.target.id === 'modalConfirm') closeConfirmModal();
  });
  
  $('modalConfirmApply')?.addEventListener('click', async () => {
    const saveBtn = $('btnSave');
    const original = saveBtn.innerHTML;
    saveBtn.disabled = true;
    saveBtn.innerHTML = '<span aria-hidden="true">⏳</span> Guardando...';
    
    try {
      // Simular API call
      await new Promise(r => setTimeout(r, 800));
      
      // Persistir
      storage.save();
      state.pendingChanges = [];
      
      // Actualizar UI
      renderMatrix();
      updatePendingBadge();
      showToast(`✅ ${state.changeHistory[0]?.moduleName || 'Configuración'} guardada`);
      
    } catch (err) {
      console.error('Error guardando', err);
      showToast('❌ Error al guardar', 'error');
    } finally {
      saveBtn.disabled = state.pendingChanges.length === 0;
      saveBtn.innerHTML = original;
      closeConfirmModal();
    }
  });
  
  // Exportar
  $('btnExport')?.addEventListener('click', () => exportConfig('json'));
  
  // Toast close
  $('toastClose')?.addEventListener('click', () => {
    const toast = $('toast');
    toast.classList.remove('show');
    setTimeout(() => { toast.hidden = true; }, 250);
  });
  
  // Keyboard shortcuts globales
  document.addEventListener('keydown', (e) => {
    // Ctrl/Cmd + S: Guardar
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's' && state.pendingChanges.length > 0) {
      e.preventDefault();
      $('btnSave')?.click();
    }
    
    // Ctrl/Cmd + Z: Undo
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z' && !e.shiftKey) {
      e.preventDefault();
      storage.undo();
    }
    
    // Ctrl/Cmd + Shift + Z o Ctrl + Y: Redo
    if ((e.ctrlKey || e.metaKey) && (e.key.toLowerCase() === 'y' || (e.key.toLowerCase() === 'z' && e.shiftKey))) {
      e.preventDefault();
      storage.redo();
    }
    
    // Shift + ?: Ayuda
    if (e.shiftKey && e.key === '?') {
      e.preventDefault();
      const panel = $('helpPanel');
      if (panel) {
        panel.classList.remove('hidden');
        panel.querySelector('.help-close')?.focus();
      }
    }
    
    // Esc: Cerrar modales / panel
    if (e.key === 'Escape') {
      closePermissionModal();
      closeConfirmModal();
      $('helpPanel')?.classList.add('hidden');
    }
  });
  
  // Matriz: keyboard navigation root
  $('matrixBody')?.addEventListener('keydown', (e) => {
    if (e.key === 'Tab') return; // Dejar que el navegador maneje tab
    
    const firstCell = $$('.perm-cell:not([aria-disabled="true"])')[0];
    if (firstCell && document.activeElement === $('matrixBody')) {
      e.preventDefault();
      firstCell.focus();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Cargar datos
  storage.load();
  
  // Inicializar UI
  initMobileMenu();
  initEvents();
  
  // Renderizar
  renderMatrix();
  renderActiveFilters();
  renderHistory();
  updatePendingBadge();
  
  // Mensaje de bienvenida
  setTimeout(() => {
    showToast('✅ Matriz de permisos cargada');
    announce('Gestión de roles cargada. Usa las flechas para navegar, Enter para editar.');
  }, 300);
  
  // Cleanup
  window.addEventListener('beforeunload', () => {
    if (state.pendingChanges.length > 0) {
      // Podrías mostrar confirmación aquí
    }
  });
};

// Mobile menu (shared utility)
const initMobileMenu = () => {
  const sidebar = $('sidebar');
  const hamburger = $('hamburger');
  const overlay = $('overlay');
  
  if (!sidebar || !hamburger || !overlay) return;
  
  const toggle = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);
    document.body.style.overflow = show ? 'hidden' : '';
    
    if (show) {
      sidebar.querySelector('.nav-item')?.focus();
    } else {
      hamburger.focus();
    }
  };
  
  hamburger.addEventListener('click', () => toggle(true));
  overlay.addEventListener('click', () => toggle(false));
  
  $$('.nav-item', sidebar).forEach(link => {
    link.addEventListener('click', () => {
      if (window.innerWidth < 1024) toggle(false);
    });
  });
  
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggle(false);
    }
  });
  
  window.addEventListener('resize', () => {
    if (window.innerWidth >= 1024 && sidebar.classList.contains('open')) {
      toggle(false);
    }
  });
};

// Ejecutar
document.addEventListener('DOMContentLoaded', init);