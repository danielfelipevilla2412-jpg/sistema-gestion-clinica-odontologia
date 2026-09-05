/**
 * SMILETRACK — CATÁLOGO DE SERVICIOS (script.js)
 * Conectado a SQL Server vía API REST (ServiciosRecursosController)
 * Accesibilidad + Filtros funcionales + Drawer accesible
 */

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
//  FUENTE DE DATOS: SQL Server (renderizado por el servidor) + API REST
//  para las acciones de escritura (crear/editar/activar/desactivar/eliminar).
//  Ya no se usa localStorage como fuente de verdad de los servicios.
// ═══════════════════════════════════════════════════════════════════

const servicesStorage = {
  // Los datos vienen siempre del servidor (ServiciosRecursosController ->
  // AppDbContext.Servicios), serializados en ViewData["ServiciosJson"].
  load: () => Array.isArray(window.RAZOR_SERVICIOS) ? window.RAZOR_SERVICIOS : [],

  getService: (id) => servicesStorage.load().find(s => s.id === id),

  // Actualiza solo el estado en memoria para reflejar de inmediato el
  // resultado de una llamada a la API; la próxima recarga de página
  // siempre traerá el estado real desde SQL Server.
  updateLocalCache: (id, updates) => {
    const idx = services.findIndex(s => s.id === id);
    if (idx !== -1) {
      services[idx] = { ...services[idx], ...updates };
      return true;
    }
    return false;
  }
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
  endodoncia: { label: 'Endodoncia', class: 'endodoncia' },
  general: { label: 'General', class: 'general' }
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
    const category = categoryLabels[s.category] || categoryLabels.general;
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

// Guardar servicio (nuevo o editar) — persiste en SQL Server vía API real.
const saveService = async (e) => {
  e.preventDefault();

  const service = {
    name: safeGetElement('serviceName')?.value?.trim(),
    description: safeGetElement('serviceDescription')?.value?.trim(),
    category: safeGetElement('serviceCategory')?.value,
    cost: parseFloat(safeGetElement('serviceCost')?.value) || 0,
    duration: parseInt(safeGetElement('serviceDuration')?.value) || 0,
    active: safeGetElement('serviceActiveToggle')?.getAttribute('aria-checked') === 'true'
  };

  if (!service.name || !service.category) {
    showToast('⚠️ Nombre y categoría son obligatorios', 'warning');
    return;
  }

  const payload = {
    nombre: service.name,
    descripcion: service.description,
    precio: service.cost,
    category: service.category,
    duration: service.duration
  };

  try {
    if (editingServiceId) {
      // Actualizar existente
      const result = await window.apiRequest(`/servicios-y-recursos/api/servicios/${editingServiceId}`, {
        method: 'PUT',
        body: payload
      });

      if (!result || result.success !== true) {
        showToast(result?.message || 'No fue posible actualizar el servicio.', 'error');
        return;
      }

      servicesStorage.updateLocalCache(editingServiceId, service);
      renderServices();
      updateStats();
      closeDrawer();
      showToast(`✅ Servicio "${service.name}" actualizado`);
    } else {
      // Crear nuevo
      const result = await window.apiRequest('/servicios-y-recursos/api/servicios', {
        method: 'POST',
        body: payload
      });

      if (!result || result.success !== true) {
        showToast(result?.message || 'No fue posible crear el servicio.', 'error');
        return;
      }

      services.unshift({ ...service, id: result.data.id, icon: '🦷' });
      renderServices();
      updateStats();
      closeDrawer();
      showToast(`✅ Servicio "${service.name}" creado`);
    }
  } catch (error) {
    console.error('Error guardando servicio:', error);
    showToast('Error de conexión al guardar el servicio.', 'error');
  }
};

// Alternar estado de servicio (activo/inactivo) — persiste en SQL Server.
const toggleServiceStatus = async (id) => {
  const service = servicesStorage.getService(id);
  if (!service) return;

  const nuevoEstado = service.active ? 'inactivo' : 'activo';

  try {
    const result = await window.apiRequest(`/servicios-y-recursos/api/servicios/${id}/estado`, {
      method: 'PUT',
      body: { estado: nuevoEstado }
    });

    if (!result || result.success !== true) {
      showToast(result?.message || 'No fue posible cambiar el estado del servicio.', 'error');
      return;
    }

    servicesStorage.updateLocalCache(id, { active: nuevoEstado === 'activo' });
    renderServices();
    updateStats();
    showToast(`🔄 Servicio "${service.name}" ${nuevoEstado === 'activo' ? 'activado' : 'desactivado'}`);
  } catch (error) {
    console.error('Error cambiando estado del servicio:', error);
    showToast('Error de conexión al cambiar el estado.', 'error');
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
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initSearch();
  initFilters();
  initPagination();
  initNewService();
  initDrawer();

  services = servicesStorage.load();
  updateStats();
  renderServices();

  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init);
