/**
 * SMILETRACK — CATÁLOGO DE SERVICIOS (script.js)
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

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON LOCALSTORAGE
// ═══════════════════════════════════════════════════════════════════

const servicesStorage = {
  key: 'smiletrack_servicios_admin',
  
  load: () => {
    if (Array.isArray(window.RAZOR_SERVICIOS) && window.RAZOR_SERVICIOS.length > 0) {
      return window.RAZOR_SERVICIOS;
    }
    const stored = localStorage.getItem(servicesStorage.key);
    if (stored) {
      try { return JSON.parse(stored); }
      catch (e) { console.warn('Error al cargar servicios, usando datos de ejemplo'); }
    }
    // Datos de ejemplo con categorías y estados
    return [
      { id: 1, name: 'Limpieza Dental Profunda', description: 'Procedimiento clínico de remoción de placa bacteriana mediante ultrasonido y curetaje manual.', category: 'prevencion', duration: 45, cost: 85.00, active: true, icon: '🪥' },
      { id: 2, name: 'Blanqueamiento LED', description: 'Tratamiento estético con luz LED para aclarar el tono dental hasta 8 tonos.', category: 'estetica', duration: 60, cost: 250.00, active: true, icon: '✨' },
      { id: 3, name: 'Extracción Muela Juicio', description: 'Procedimiento quirúrgico para extracción de terceros molares impactados.', category: 'cirugia', duration: 90, cost: 180.00, active: false, icon: '🦷' },
      { id: 4, name: 'Ajuste de Brackets', description: 'Control periódico de ortodoncia para ajuste de arcos y bandas.', category: 'ortodoncia', duration: 30, cost: 60.00, active: true, icon: '⛓️' },
      { id: 5, name: 'Endodoncia Unirradicular', description: 'Tratamiento de conductos en piezas con una raíz.', category: 'endodoncia', duration: 75, cost: 220.00, active: true, icon: '🔧' },
      { id: 6, name: 'Profilaxis Básica', description: 'Limpieza preventiva con pulido y fluorización.', category: 'prevencion', duration: 30, cost: 45.00, active: true, icon: '🪥' },
      { id: 7, name: 'Carillas de Porcelana', description: 'Restauraciones estéticas mínimamente invasivas.', category: 'estetica', duration: 120, cost: 450.00, active: true, icon: '✨' },
      { id: 8, name: 'Cirugía de Encía', description: 'Procedimiento periodontal para corregir recesión gingival.', category: 'cirugia', duration: 60, cost: 150.00, active: false, icon: '🦷' },
      { id: 9, name: 'Contención Retenedora', description: 'Fabricación e instalación de retenedores fijos o removibles.', category: 'ortodoncia', duration: 45, cost: 120.00, active: true, icon: '⛓️' },
      { id: 10, name: 'Endodoncia Multirradicular', description: 'Tratamiento de conductos en piezas con múltiples raíces.', category: 'endodoncia', duration: 90, cost: 320.00, active: true, icon: '🔧' },
      { id: 11, name: 'Selladores de Fosetas', description: 'Aplicación preventiva de resinas en superficies oclusales.', category: 'prevencion', duration: 20, cost: 30.00, active: true, icon: '🪥' },
      { id: 12, name: 'Diseño de Sonrisa Digital', description: 'Planificación estética digital con simulación 3D.', category: 'estetica', duration: 45, cost: 180.00, active: true, icon: '✨' }
    ];
  },
  
  save: (data) => {
    try { localStorage.setItem(servicesStorage.key, JSON.stringify(data)); return true; }
    catch (e) { console.error('Error al guardar servicios:', e); return false; }
  },
  
  addService: (service) => {
    const data = servicesStorage.load();
    service.id = data.length > 0 ? Math.max(...data.map(s => s.id)) + 1 : 1;
    data.unshift(service);
    servicesStorage.save(data);
    return service;
  },
  
  updateService: (id, updates) => {
    const data = servicesStorage.load();
    const idx = data.findIndex(s => s.id === id);
    if (idx !== -1) {
      data[idx] = { ...data[idx], ...updates };
      servicesStorage.save(data);
      return true;
    }
    return false;
  },
  
  getService: (id) => servicesStorage.load().find(s => s.id === id)
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO
// ═══════════════════════════════════════════════════════════════════

let services = servicesStorage.load();
let searchQuery = '';
let filterCategory = '';
let filterStatus = '';
let currentPage = 1;
let editingServiceId = null;
const itemsPerPage = 4;

const categoryLabels = {
  prevencion: { label: 'Prevención', class: 'prevencion' },
  estetica: { label: 'Estética', class: 'estetica' },
  cirugia: { label: 'Cirugía', class: 'cirugia' },
  ortodoncia: { label: 'Ortodoncia', class: 'ortodoncia' },
  endodoncia: { label: 'Endodoncia', class: 'endodoncia' }
};

const statusLabels = {
  true: { label: 'Activo', class: 'activo' },
  false: { label: 'Inactivo', class: 'inactivo' }
};

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const fmtCost = (cost) => `$${parseFloat(cost).toFixed(2)}`;

// ═══════════════════════════════════════════════════════════════════
//  FILTRADO COMBINADO
// ═══════════════════════════════════════════════════════════════════

const getFilteredServices = () => {
  return services.filter(s => {
    // Filtro por búsqueda
    if (searchQuery && !s.name.toLowerCase().includes(searchQuery.toLowerCase())) return false;
    
    // Filtro por categoría
    if (filterCategory && s.category !== filterCategory) return false;
    
    // Filtro por estado
    if (filterStatus && String(s.active) !== filterStatus) return false;
    
    return true;
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: TABLA DE SERVICIOS
// ═══════════════════════════════════════════════════════════════════

const renderServices = () => {
  const body = safeGetElement('servicesBody');
  if (!body) return;
  
  const filtered = getFilteredServices();
  
  if (!filtered.length) {
    body.innerHTML = '<div class="empty-state" role="status">No se encontraron servicios con los criterios de búsqueda.</div>';
    return;
  }
  
  // Paginación
  const start = (currentPage - 1) * itemsPerPage;
  const pageData = filtered.slice(start, start + itemsPerPage);
  
  body.innerHTML = pageData.map(s => {
    const category = categoryLabels[s.category] || categoryLabels.prevencion;
    const status = statusLabels[String(s.active)];
    
    return `
      <div class="table-row" role="row" tabindex="0" aria-label="Servicio ${s.name}">
        <div class="table-col col-nombre" role="cell" data-label="Nombre del Servicio">
          <div class="service-info">
            <span class="service-icon" aria-hidden="true">${s.icon}</span>
            <span class="service-name">${s.name}</span>
          </div>
        </div>
        <div class="table-col col-categoria" role="cell" data-label="Categoría">
          <span class="category-badge ${category.class}" role="status" aria-label="Categoría: ${category.label}">${category.label}</span>
        </div>
        <div class="table-col col-duracion" role="cell" data-label="Duración">${s.duration} min</div>
        <div class="table-col col-costo" role="cell" data-label="Costo">${fmtCost(s.cost)}</div>
        <div class="table-col col-estado text-center" role="cell" data-label="Estado">
          <span class="status-badge ${status.class}" role="status" aria-label="Estado: ${status.label}">${status.label}</span>
        </div>
        <div class="table-col col-acciones text-right" role="cell" data-label="Acciones">
          <div class="actions-cell">
            <button class="action-btn btn-edit" aria-label="Editar servicio ${s.name}" data-id="${s.id}" title="Editar">✏️</button>
            <button class="action-btn btn-toggle ${s.active ? '' : 'btn-delete'}" 
                    aria-label="${s.active ? 'Desactivar' : 'Activar'} servicio ${s.name}" 
                    data-id="${s.id}" 
                    title="${s.active ? 'Bloquear' : 'Activar'}">
              ${s.active ? '🔓' : '🔒'}
            </button>
          </div>
        </div>
      </div>
    `;
  }).join('');
  
  // Event listeners para acciones
  body.querySelectorAll('.btn-edit').forEach(btn => {
    btn.addEventListener('click', (e) => openDrawer(parseInt(e.currentTarget.dataset.id)));
    btn.addEventListener('keydown', (e) => { if (['Enter',' '].includes(e.key)) { e.preventDefault(); openDrawer(parseInt(e.currentTarget.dataset.id)); }});
  });
  body.querySelectorAll('.btn-toggle').forEach(btn => {
    btn.addEventListener('click', (e) => toggleServiceStatus(parseInt(e.currentTarget.dataset.id)));
    btn.addEventListener('keydown', (e) => { if (['Enter',' '].includes(e.key)) { e.preventDefault(); toggleServiceStatus(parseInt(e.currentTarget.dataset.id)); }});
  });
  
  updatePagination(filtered.length);
};

// ═══════════════════════════════════════════════════════════════════
//  DRAWER: EDITAR SERVICIO
// ═══════════════════════════════════════════════════════════════════

const openDrawer = (id = null) => {
  const drawer = safeGetElement('drawerOverlay');
  const form = safeGetElement('serviceForm');
  const title = safeGetElement('drawerTitle');
  const toggle = safeGetElement('serviceActiveToggle');
  
  if (!drawer || !form || !title) return;
  
  editingServiceId = id;
  
  if (id) {
    // Modo edición
    const service = servicesStorage.getService(id);
    if (!service) return;
    
    title.textContent = 'Editar Servicio';
    safeGetElement('serviceName').value = service.name;
    safeGetElement('serviceDescription').value = service.description || '';
    safeGetElement('serviceCategory').value = service.category;
    safeGetElement('serviceCost').value = service.cost;
    safeGetElement('serviceDuration').value = service.duration;
    
    // Toggle switch
    toggle.setAttribute('aria-checked', service.active);
    toggle.classList.toggle('active', service.active);
  } else {
    // Modo nuevo
    title.textContent = 'Nuevo Servicio';
    form.reset();
    toggle.setAttribute('aria-checked', 'true');
    toggle.classList.add('active');
  }
  
  // Mostrar drawer
  drawer.classList.add('open');
  drawer.setAttribute('aria-hidden', 'false');
  drawer.removeAttribute('inert');
  document.body.style.overflow = 'hidden';
  
  // Enfocar primer input
  const firstInput = form.querySelector('input, textarea, select');
  if (firstInput) firstInput.focus();
};

const closeDrawer = () => {
  const drawer = safeGetElement('drawerOverlay');
  if (drawer) {
    drawer.classList.remove('open');
    drawer.setAttribute('aria-hidden', 'true');
    drawer.setAttribute('inert', '');
    document.body.style.overflow = '';
  }
  editingServiceId = null;
};

// Toggle switch accesible
const initToggleSwitch = () => {
  const toggle = safeGetElement('serviceActiveToggle');
  toggle?.addEventListener('click', () => {
    const isActive = toggle.getAttribute('aria-checked') === 'true';
    toggle.setAttribute('aria-checked', !isActive);
    toggle.classList.toggle('active', !isActive);
  });
  toggle?.addEventListener('keydown', (e) => {
    if (['Enter',' '].includes(e.key)) {
      e.preventDefault();
      toggle.click();
    }
  });
};

// Guardar servicio (nuevo o editar)
const saveService = (e) => {
  e.preventDefault();
  
  const service = {
    name: safeGetElement('serviceName')?.value,
    description: safeGetElement('serviceDescription')?.value,
    category: safeGetElement('serviceCategory')?.value,
    cost: parseFloat(safeGetElement('serviceCost')?.value) || 0,
    duration: parseInt(safeGetElement('serviceDuration')?.value) || 0,
    active: safeGetElement('serviceActiveToggle')?.getAttribute('aria-checked') === 'true',
    icon: '🦷' // Icono por defecto
  };
  
  if (!service.name || !service.category) {
    showToast('⚠️ Nombre y categoría son obligatorios', 'warning');
    return;
  }
  
  if (editingServiceId) {
    // Actualizar existente
    if (servicesStorage.updateService(editingServiceId, service)) {
      services = servicesStorage.load();
      renderServices();
      updateStats();
      closeDrawer();
      showToast(`✅ Servicio "${service.name}" actualizado`);
    }
  } else {
    // Crear nuevo
    const newService = servicesStorage.addService(service);
    services = servicesStorage.load();
    renderServices();
    updateStats();
    closeDrawer();
    showToast(`✅ Servicio "${newService.name}" creado`);
  }
};

// Alternar estado de servicio (activo/inactivo)
const toggleServiceStatus = (id) => {
  const service = servicesStorage.getService(id);
  if (!service) return;
  
  const newStatus = !service.active;
  if (servicesStorage.updateService(id, { active: newStatus })) {
    services = servicesStorage.load();
    renderServices();
    updateStats();
    showToast(`🔄 Servicio "${service.name}" ${newStatus ? 'activado' : 'desactivado'}`);
  }
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

const animateCounter = (el, target) => {
  if (!el) return;
  let current = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const timer = setInterval(() => {
    current = Math.min(current + step, target);
    el.textContent = current;
    if (current >= target) clearInterval(timer);
  }, 30);
};

const updateStats = () => {
  const total = services.length;
  const active = services.filter(s => s.active).length;
  const inactive = total - active;
  
  animateCounter(safeGetElement('statTotal'), total);
  animateCounter(safeGetElement('statActive'), active);
  animateCounter(safeGetElement('statInactive'), inactive);
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
  const searchInput = safeGetElement('searchServices');
  searchInput?.addEventListener('input', debounce((e) => {
    searchQuery = e.target.value.toLowerCase();
    currentPage = 1;
    renderServices();
  }, 250));
};

const initFilters = () => {
  const filterCategoryEl = safeGetElement('filterCategory');
  const filterStatusEl = safeGetElement('filterStatus');
  
  filterCategoryEl?.addEventListener('change', (e) => {
    filterCategory = e.target.value;
    currentPage = 1;
    renderServices();
  });
  
  filterStatusEl?.addEventListener('change', (e) => {
    filterStatus = e.target.value;
    currentPage = 1;
    renderServices();
  });
};

const initPagination = () => {
  const btnPrev = safeGetElement('btnPrev');
  const btnNext = safeGetElement('btnNext');
  
  btnPrev?.addEventListener('click', () => {
    if (currentPage > 1) { currentPage--; renderServices(); }
  });
  
  btnNext?.addEventListener('click', () => {
    const filtered = getFilteredServices();
    const totalPages = Math.ceil(filtered.length / itemsPerPage);
    if (currentPage < totalPages) { currentPage++; renderServices(); }
  });
};

const initDrawer = () => {
  const drawer = safeGetElement('drawerOverlay');
  const drawerClose = safeGetElement('drawerClose');
  const drawerCancel = safeGetElement('drawerCancel');
  const serviceForm = safeGetElement('serviceForm');
  
  // Cerrar drawer
  drawerClose?.addEventListener('click', closeDrawer);
  drawerCancel?.addEventListener('click', closeDrawer);
  drawer?.addEventListener('click', (e) => { if (e.target === drawer) closeDrawer(); });
  
  // Submit del formulario
  serviceForm?.addEventListener('submit', saveService);
  
  // Escape cierra drawer
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && drawer?.classList.contains('open')) {
      e.preventDefault();
      closeDrawer();
    }
  });
  
  // Toggle switch
  initToggleSwitch();
};

const initNewService = () => {
  const btn = safeGetElement('btnNewService');
  btn?.addEventListener('click', () => openDrawer(null));
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
// ═══════════════════════════════════════════════════════════════════

async function fetchServices() {
  try {
    // const res = await fetch(`${API_BASE}/admin/services`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    return servicesStorage.load();
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return servicesStorage.load();
  }
}

async function addServiceAPI(service) {
  try {
    // const res = await fetch(`${API_BASE}/admin/services`, {
    //   method: 'POST', headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(service),
    // });
    // if (!res.ok) throw new Error('Add failed');
    // return await res.json();
    return servicesStorage.addService(service);
  } catch (error) {
    console.warn('Error al agregar servicio en API:', error);
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
  initNewService();
  initDrawer();
  
  services = await fetchServices();
  updateStats();
  renderServices();
  
  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init);