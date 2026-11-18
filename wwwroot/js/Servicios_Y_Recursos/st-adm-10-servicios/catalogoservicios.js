(() => {
  'use strict';

  const translate = (key, vars) => window.I18N?.t(key, vars) || key;
  const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, character => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  }[character]));

  const showToast = (message, type = 'success') => {
    const toast = window.ToastService?.[type];
    if (typeof toast !== 'function') {
      console.error('[SmileTrack][Servicios] El servicio compartido de notificaciones no está disponible.');
      return;
    }

    const titleKey = type === 'error'
      ? 'servicios.toast_error'
      : type === 'warning' ? 'servicios.toast_warning'
        : type === 'success' ? 'servicios.toast_success' : 'servicios.toast_info';
    toast(translate(titleKey), escapeHtml(message));
  };

  const debounce = (fn, delay) => {
    let timeoutId;
    return (...args) => {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => fn(...args), delay);
    };
  };

  const categoryKeys = {
    prevencion: 'servicios.category_prevencion',
    estetica: 'servicios.category_estetica',
    cirugia: 'servicios.category_cirugia',
    ortodoncia: 'servicios.category_ortodoncia',
    endodoncia: 'servicios.category_endodoncia',
    periodoncia: 'servicios.category_periodoncia',
    general: 'servicios.category_general'
  };

  const normalizeCategory = (category) => {
    const key = String(category || '')
      .trim()
      .toLocaleLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[\s-]+/g, '_');
    const aliases = {
      preventiva: 'prevencion',
      odontologia_general: 'general'
    };
    const normalized = aliases[key] || key;
    return Object.prototype.hasOwnProperty.call(categoryKeys, normalized) ? normalized : null;
  };

  const getCategory = (category) => {
    const key = normalizeCategory(category);
    return {
      label: key ? translate(categoryKeys[key]) : String(category || translate('common.not_available')),
      className: key || 'other'
    };
  };

  const getStatus = (active) => active
    ? { label: translate('servicios.active'), className: 'activo' }
    : { label: translate('status.inactive'), className: 'inactivo' };

  const formatPrice = (value) => {
    const locale = window.I18N?.lang === 'en' ? 'en-US' : 'es-CO';
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: 'COP',
      maximumFractionDigits: 0
    }).format(Number(value) || 0);
  };

  let services = Array.isArray(window.RAZOR_SERVICIOS) ? [...window.RAZOR_SERVICIOS] : [];
  let searchQuery = '';
  let filterCategory = '';
  let filterStatus = '';
  let currentPage = 1;
  let editingServiceId = null;
  let drawerFocusTarget = null;
  let bodyOverflowBeforeDrawer = '';
  const itemsPerPage = 10;
  const counterTimers = new WeakMap();

  const servicesStorage = {
    getService: (id) => services.find(service => Number(service.id) === Number(id)),

    updateLocalCache: (id, updates) => {
      const index = services.findIndex(service => Number(service.id) === Number(id));
      if (index < 0) return false;
      services[index] = { ...services[index], ...updates };
      return true;
    }
  };

  const updateSaveButtonState = () => {
    const button = document.getElementById('serviceSaveButton');
    const category = document.getElementById('serviceCategory')?.value;
    const specialty = document.getElementById('serviceSpecialty')?.value;
    if (!button) return;
    const compatible = Array.isArray(window.RAZOR_COMPATIBILIDADES) &&
      window.RAZOR_COMPATIBILIDADES.some(item =>
        item.categoria === category && Number(item.idEspecialidad) === Number(specialty));
    button.disabled = !category || !specialty || !compatible;
  };

  const loadSpecialties = () => {
    const select = document.getElementById('serviceSpecialty');
    if (!select) return;

    const previousValue = select.value;
    const category = document.getElementById('serviceCategory')?.value;
    const allowedIds = new Set((window.RAZOR_COMPATIBILIDADES || [])
      .filter(item => item.categoria === category)
      .map(item => Number(item.idEspecialidad)));
    const specialties = (Array.isArray(window.RAZOR_ESPECIALIDADES)
      ? window.RAZOR_ESPECIALIDADES : [])
      .filter(item => allowedIds.has(Number(item.id)));
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = translate('servicios.select_specialty');
    placeholder.disabled = true;
    placeholder.selected = true;
    select.replaceChildren(placeholder);

    specialties.forEach(specialty => {
      const option = document.createElement('option');
      option.value = String(specialty.id);
      option.textContent = specialty.nombre;
      select.appendChild(option);
    });

    if (specialties.some(specialty => String(specialty.id) === previousValue)) {
      select.value = previousValue;
    } else {
      select.value = '';
    }
    select.disabled = specialties.length === 0;
    const hint = document.getElementById('serviceSpecialtyHint');
    if (hint) {
      hint.textContent = translate(specialties.length
        ? 'servicios.specialty_required_hint'
        : 'servicios.no_specialties');
    }
    updateSaveButtonState();
  };

  const getFilteredServices = () => {
    const query = searchQuery.trim().toLocaleLowerCase();
    return services.filter(service => {
      const name = String(service.name || '').toLocaleLowerCase();
      const description = String(service.description || '').toLocaleLowerCase();
      if (query && !name.includes(query) && !description.includes(query)) return false;
      if (filterCategory && normalizeCategory(service.category) !== filterCategory) return false;
      if (filterStatus && String(Boolean(service.active)) !== filterStatus) return false;
      return true;
    });
  };

  const updatePagination = (totalItems) => {
    const totalPages = Math.ceil(totalItems / itemsPerPage);
    currentPage = totalPages ? Math.min(Math.max(currentPage, 1), totalPages) : 1;

    const start = (currentPage - 1) * itemsPerPage;
    const showing = totalItems ? Math.min(itemsPerPage, totalItems - start) : 0;
    const pageShowing = document.getElementById('pageShowing');
    const pageTotal = document.getElementById('pageTotal');
    const pagination = document.getElementById('servicePagination');
    if (pageShowing) pageShowing.textContent = String(showing);
    if (pageTotal) pageTotal.textContent = String(totalItems);
    if (!pagination) return;

    const pages = Array.from({ length: totalPages }, (_, index) => {
      const page = index + 1;
      const current = page === currentPage;
      return `<button type="button" class="pagination-btn${current ? ' active' : ''}" data-page="${page}"${current ? ' aria-current="page"' : ''} aria-label="${escapeHtml(translate('servicios.page_number', { page }))}">${page}</button>`;
    }).join('');

    pagination.innerHTML = `
      <button type="button" class="pagination-btn" data-page="${Math.max(1, currentPage - 1)}" aria-label="${escapeHtml(translate('servicios.previous_page'))}"${currentPage <= 1 ? ' disabled' : ''}>
        <span class="material-symbols-outlined" aria-hidden="true">chevron_left</span>
      </button>
      ${pages}
      <button type="button" class="pagination-btn" data-page="${Math.min(totalPages || 1, currentPage + 1)}" aria-label="${escapeHtml(translate('servicios.next_page'))}"${!totalPages || currentPage >= totalPages ? ' disabled' : ''}>
        <span class="material-symbols-outlined" aria-hidden="true">chevron_right</span>
      </button>
    `;
  };

  const renderServices = () => {
    const body = document.getElementById('servicesBody');
    if (!body) return;

    const filtered = getFilteredServices();
    const totalPages = Math.ceil(filtered.length / itemsPerPage);
    currentPage = totalPages ? Math.min(Math.max(currentPage, 1), totalPages) : 1;
    const start = (currentPage - 1) * itemsPerPage;
    const pageData = filtered.slice(start, start + itemsPerPage);

    if (!pageData.length) {
      body.innerHTML = `
        <div class="services-empty-state empty-state" role="status">
          <span class="material-symbols-outlined empty-state-icon" aria-hidden="true">medical_services</span>
          <h3>${escapeHtml(translate('servicios.empty_title'))}</h3>
          <p>${escapeHtml(translate(filtered.length || searchQuery || filterCategory || filterStatus
            ? 'servicios.empty_filtered'
            : 'servicios.no_services'))}</p>
        </div>
      `;
      updatePagination(filtered.length);
      return;
    }

    body.innerHTML = pageData.map(service => {
      const name = String(service.name || '');
      const category = getCategory(service.category);
      const status = getStatus(Boolean(service.active));
      const specialty = service.specialtyName || translate('servicios.specialty_not_configured');
      const editLabel = translate('servicios.edit_action', { name });
      const toggleKey = service.active ? 'servicios.deactivate_action' : 'servicios.activate_action';
      const toggleLabel = translate(toggleKey, { name });

      return `
        <div class="table-row" role="row" tabindex="0" data-id="${escapeHtml(service.id)}" aria-label="${escapeHtml(translate('servicios.service_aria', { name }))}">
          <div class="table-col col-nombre" role="cell" data-label="${escapeHtml(translate('servicios.column_name'))}">
            <div class="service-info">
              <span class="material-symbols-outlined service-icon" aria-hidden="true">medical_services</span>
              <span class="service-name">${escapeHtml(name)}</span>
            </div>
          </div>
          <div class="table-col col-categoria" role="cell" data-label="${escapeHtml(translate('servicios.category'))}">
            <div>
              <span class="category-badge ${category.className}">${escapeHtml(category.label)}</span>
              <small class="service-specialty">${escapeHtml(specialty)}</small>
            </div>
          </div>
          <div class="table-col col-duracion" role="cell" data-label="${escapeHtml(translate('servicios.duration'))}">${escapeHtml(service.duration)} ${escapeHtml(translate('servicios.minutes_short'))}</div>
          <div class="table-col col-costo" role="cell" data-label="${escapeHtml(translate('servicios.price'))}">${escapeHtml(formatPrice(service.cost))}</div>
          <div class="table-col col-estado text-center" role="cell" data-label="${escapeHtml(translate('facturacion.status'))}">
            <span class="status-badge ${status.className}" role="status" aria-label="${escapeHtml(translate('servicios.status_aria', { status: status.label }))}">${escapeHtml(status.label)}</span>
          </div>
          <div class="table-col col-acciones text-right" role="cell" data-label="${escapeHtml(translate('table.actions'))}">
            <div class="actions-cell">
              <button type="button" class="action-btn btn-edit" data-action="edit" data-id="${escapeHtml(service.id)}" aria-label="${escapeHtml(editLabel)}" title="${escapeHtml(translate('servicios.edit'))}">
                <span class="material-symbols-outlined" aria-hidden="true">edit</span><span class="btn-text">${escapeHtml(translate('common.edit'))}</span>
              </button>
              <button type="button" class="action-btn btn-service-toggle${service.active ? '' : ' is-inactive'}" data-action="toggle" data-id="${escapeHtml(service.id)}" aria-label="${escapeHtml(toggleLabel)}" title="${escapeHtml(toggleLabel)}">
                <span class="material-symbols-outlined" aria-hidden="true">${service.active ? 'pause_circle' : 'play_circle'}</span><span class="btn-text">${escapeHtml(translate(service.active ? 'servicios.deactivate' : 'servicios.activate'))}</span>
              </button>
            </div>
          </div>
        </div>
      `;
    }).join('');

    updatePagination(filtered.length);
  };

  const animateCounter = (element, target) => {
    if (!element) return;
    clearInterval(counterTimers.get(element));
    let current = 0;
    const step = Math.max(1, Math.ceil(target / 30));
    const timer = setInterval(() => {
      current = Math.min(current + step, target);
      element.textContent = String(current);
      if (current >= target) {
        clearInterval(timer);
        counterTimers.delete(element);
      }
    }, 30);
    counterTimers.set(element, timer);
  };

  const updateStats = () => {
    const active = services.filter(service => Boolean(service.active)).length;
    animateCounter(document.getElementById('statTotal'), services.length);
    animateCounter(document.getElementById('statActive'), active);
    animateCounter(document.getElementById('statInactive'), services.length - active);
  };

  const setDrawerStatus = (active) => {
    const status = document.getElementById('serviceDrawerStatus');
    if (!status) return;
    const currentStatus = getStatus(Boolean(active));
    status.className = `status-badge ${currentStatus.className}`;
    status.textContent = currentStatus.label;
  };

  const openDrawer = (id = null) => {
    const drawer = document.getElementById('drawerOverlay');
    const form = document.getElementById('serviceForm');
    const title = document.getElementById('drawerTitle');
    const saveButton = document.getElementById('serviceSaveButton');
    if (!drawer || !form || !title || !saveButton) return;

    const service = id === null ? null : servicesStorage.getService(id);
    if (id !== null && !service) return;

    if (!drawer.classList.contains('open')) {
      drawerFocusTarget = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      bodyOverflowBeforeDrawer = document.body.style.overflow;
    }

    editingServiceId = service ? Number(service.id) : null;
    form.reset();
    title.textContent = translate(service ? 'servicios.edit' : 'servicios.new');
    saveButton.textContent = translate(service ? 'servicios.save_changes' : 'servicios.create');

    if (service) {
      document.getElementById('serviceName').value = service.name || '';
      document.getElementById('serviceDescription').value = service.description || '';
      document.getElementById('serviceCategory').value = normalizeCategory(service.category) || '';
      loadSpecialties();
      document.getElementById('serviceSpecialty').value = service.specialtyId ? String(service.specialtyId) : '';
      updateSaveButtonState();
      document.getElementById('serviceCost').value = service.cost;
      document.getElementById('serviceDuration').value = service.duration;
      setDrawerStatus(service.active);
    } else {
      loadSpecialties();
      document.getElementById('serviceDuration').value = '60';
      setDrawerStatus(true);
    }

    drawer.classList.add('open');
    drawer.setAttribute('aria-hidden', 'false');
    drawer.removeAttribute('inert');
    document.body.style.overflow = 'hidden';
    document.getElementById('serviceName')?.focus();
  };

  const closeDrawer = () => {
    const drawer = document.getElementById('drawerOverlay');
    if (!drawer?.classList.contains('open')) return;
    drawer.classList.remove('open');
    drawer.setAttribute('aria-hidden', 'true');
    drawer.setAttribute('inert', '');
    document.body.style.overflow = bodyOverflowBeforeDrawer;
    editingServiceId = null;
    if (drawerFocusTarget?.isConnected) drawerFocusTarget.focus();
    drawerFocusTarget = null;
  };

  const saveService = async (event) => {
    event.preventDefault();

    const serviceId = editingServiceId;
    const specialtyValue = document.getElementById('serviceSpecialty').value;
    const service = {
      name: document.getElementById('serviceName').value.trim(),
      description: document.getElementById('serviceDescription').value.trim(),
      category: document.getElementById('serviceCategory').value,
      specialtyId: specialtyValue ? Number(specialtyValue) : null,
      specialtyName: document.getElementById('serviceSpecialty').selectedOptions[0]?.textContent || null,
      cost: Number(document.getElementById('serviceCost').value),
      duration: Number(document.getElementById('serviceDuration').value)
    };

    if (!service.name || !service.category || !Number.isInteger(service.specialtyId)) {
      showToast(translate('servicios.required_fields_error'), 'warning');
      return;
    }
    if (!Number.isFinite(service.cost) || service.cost <= 0) {
      showToast(translate('servicios.price_error'), 'warning');
      return;
    }
    if (!Number.isInteger(service.duration) || service.duration < 5 || service.duration > 480) {
      showToast(translate('servicios.duration_error'), 'warning');
      return;
    }

    const saveButton = document.getElementById('serviceSaveButton');
    saveButton.disabled = true;
    try {
      const payload = {
        nombre: service.name,
        descripcion: service.description,
        precio: service.cost,
        category: service.category,
        idEspecialidad: service.specialtyId,
        duration: service.duration
      };

      if (serviceId !== null) {
        const existing = servicesStorage.getService(serviceId);
        let result;
        try {
          result = await window.apiRequest(`/servicios-y-recursos/api/servicios/${serviceId}`, {
            method: 'PUT',
            body: payload
          });
        } catch (error) {
          if (error?.status === 409 && error?.response?.requiereConfirmacion) {
            result = error.response;
          } else {
            throw error;
          }
        }
        if (result?.requiereConfirmacion) {
          const ids = (result.profesionales || []).join(', ');
          const accepted = window.confirm(`${result.message}\nProfesionales afectados (ID): ${ids}\n\n¿Desactivar estas asignaciones y guardar el servicio?`);
          if (!accepted) return;
          result = await window.apiRequest(`/servicios-y-recursos/api/servicios/${serviceId}`, {
            method: 'PUT',
            body: { ...payload, confirmarDesactivacion: true }
          });
        }
        if (result?.success !== true) {
          showToast(result?.message || translate('servicios.update_error'), 'error');
          return;
        }

        servicesStorage.updateLocalCache(serviceId, {
          ...service,
          id: serviceId,
          active: Boolean(existing?.active),
          icon: existing?.icon || 'medical_services'
        });
        showToast(translate('servicios.updated', { name: service.name }));
      } else {
        const result = await window.apiRequest('/servicios-y-recursos/api/servicios', {
          method: 'POST',
          body: payload
        });
        if (result?.success !== true || !result.data?.id) {
          showToast(result?.message || translate('servicios.create_error'), 'error');
          return;
        }

        services.unshift({
          ...service,
          id: Number(result.data.id),
          active: true,
          icon: 'medical_services'
        });
        currentPage = 1;
        showToast(translate('servicios.created', { name: service.name }));
      }

      renderServices();
      updateStats();
      closeDrawer();
    } catch (error) {
      console.error('Error guardando el servicio.', error);
      showToast(translate(serviceId === null ? 'servicios.create_error' : 'servicios.update_error'), 'error');
    } finally {
      updateSaveButtonState();
    }
  };

  const toggleServiceStatus = async (id) => {
    const service = servicesStorage.getService(id);
    if (!service) return;

    const active = !Boolean(service.active);
    try {
      const result = await window.apiRequest(`/servicios-y-recursos/api/servicios/${id}/estado`, {
        method: 'PUT',
        body: { estado: active ? 'activo' : 'inactivo' }
      });
      if (result?.success !== true) {
        showToast(result?.message || translate('servicios.status_error'), 'error');
        return;
      }

      servicesStorage.updateLocalCache(id, { active });
      renderServices();
      updateStats();
      showToast(translate(active ? 'servicios.activated' : 'servicios.deactivated', { name: service.name }));
    } catch (error) {
      console.error('Error cambiando el estado del servicio.', error);
      showToast(translate('servicios.status_error'), 'error');
    }
  };

  const initFilters = () => {
    document.getElementById('searchServices')?.addEventListener('input', debounce(event => {
      searchQuery = event.target.value;
      currentPage = 1;
      renderServices();
    }, 250));

    document.getElementById('filterCategory')?.addEventListener('change', event => {
      filterCategory = event.target.value;
      currentPage = 1;
      renderServices();
    });

    document.getElementById('filterStatus')?.addEventListener('change', event => {
      filterStatus = event.target.value;
      currentPage = 1;
      renderServices();
    });
  };

  const initPagination = () => {
    document.getElementById('servicePagination')?.addEventListener('click', event => {
      const button = event.target.closest('button[data-page]');
      if (!button || button.disabled) return;
      currentPage = Number(button.dataset.page);
      renderServices();
    });
  };

  const initTableActions = () => {
    const body = document.getElementById('servicesBody');
    if (!body) return;

    body.addEventListener('click', event => {
      const button = event.target.closest('button[data-action]');
      if (button) {
        event.stopPropagation();
        const id = Number(button.dataset.id);
        if (button.dataset.action === 'edit') openDrawer(id);
        else if (button.dataset.action === 'toggle') toggleServiceStatus(id);
        return;
      }

      const row = event.target.closest('.table-row[data-id]');
      if (row) openDrawer(Number(row.dataset.id));
    });

    body.addEventListener('keydown', event => {
      if (!['Enter', ' '].includes(event.key) || event.target.closest('button')) return;
      const row = event.target.closest('.table-row[data-id]');
      if (!row) return;
      event.preventDefault();
      openDrawer(Number(row.dataset.id));
    });
  };

  const initDrawer = () => {
    const drawer = document.getElementById('drawerOverlay');
    document.getElementById('drawerClose')?.addEventListener('click', closeDrawer);
    document.getElementById('drawerCancel')?.addEventListener('click', closeDrawer);
    document.getElementById('btnNewService')?.addEventListener('click', () => openDrawer());
    document.getElementById('serviceForm')?.addEventListener('submit', saveService);
    document.getElementById('serviceCategory')?.addEventListener('change', loadSpecialties);
    document.getElementById('serviceSpecialty')?.addEventListener('change', updateSaveButtonState);
    drawer?.addEventListener('click', event => {
      if (event.target === drawer) closeDrawer();
    });

    document.addEventListener('keydown', event => {
      if (!drawer?.classList.contains('open')) return;
      if (event.key === 'Escape') {
        event.preventDefault();
        closeDrawer();
        return;
      }
      if (event.key !== 'Tab') return;

      const focusable = Array.from(drawer.querySelectorAll(
        'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      )).filter(element => !element.hidden && element.getClientRects().length > 0);
      if (!focusable.length) {
        event.preventDefault();
        drawer.focus();
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

  const init = () => {
    loadSpecialties();
    initFilters();
    initPagination();
    initTableActions();
    initDrawer();
    updateStats();
    renderServices();
    document.addEventListener('i18n:change', () => {
      loadSpecialties();
      renderServices();
      updateStats();
    });
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    init();
  }
})();
