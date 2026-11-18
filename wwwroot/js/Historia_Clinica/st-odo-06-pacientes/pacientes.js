(() => {
  'use strict';

  const state = {
    pacientes: [],
    servicios: [],
    page: 1,
    pageSize: 10,
    total: 0,
    totalPages: 1,
    requestVersion: 0,
    filters: {
      search: '',
      status: '',
      allergies: '',
      appointment: '',
      service: ''
    }
  };

  const byId = id => document.getElementById(id);

  const t = (key, values) => {
    if (!window.I18N || typeof window.I18N.t !== 'function') {
      throw new Error('El servicio de traducción no está disponible.');
    }
    return window.I18N.t(key, values);
  };

  const notify = (messageKey, type = 'error') => {
    const toast = window.ToastService;
    if (!toast || typeof toast[type] !== 'function') {
      console.error('[SmileTrack] El servicio de notificaciones no está disponible.');
      return;
    }

    const titleKey = type === 'warning' ? 'toast.warning_title' : 'toast.error_title';
    toast[type](t(titleKey), t(messageKey));
  };

  const createElement = (tagName, className, text) => {
    const element = document.createElement(tagName);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
  };

  const dateValue = value => {
    if (!value) return null;
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  };

  const formatDate = (value, options = { day: '2-digit', month: '2-digit', year: 'numeric' }) => {
    const date = dateValue(value);
    return date
      ? new Intl.DateTimeFormat(t('odo06.date_locale'), options).format(date)
      : '—';
  };

  const normalizePatient = patient => {
    const now = new Date();
    const consultations = Array.isArray(patient.consultas)
      ? patient.consultas.slice().sort((left, right) => new Date(right.fecha) - new Date(left.fecha))
      : [];
    const attended = consultations.filter(consultation => {
      const date = dateValue(consultation.fecha);
      return date && date <= now;
    });
    const upcoming = patient.proximaCita && dateValue(patient.proximaCita.fecha)
      ? patient.proximaCita
      : null;
    const latestVisit = attended[0] || consultations[0] || null;
    const rawStatus = String(patient.estado || 'activo').toLowerCase();

    return {
      id: patient.id,
      nombre: String(patient.nombre || ''),
      cedula: String(patient.cedula || ''),
      email: String(patient.email || ''),
      telefono: String(patient.telefono || ''),
      ultimaISO: latestVisit?.fecha || null,
      servicio: latestVisit?.proc || '',
      proximaISO: upcoming?.fecha || null,
      estado: rawStatus === 'inactivo' || rawStatus === 'retirado'
        ? 'alta'
        : patient.enSeguimiento ? 'seguimiento' : 'activo'
    };
  };

  const statusLabel = status => {
    const labels = {
      activo: 'odo06.stat_active',
      seguimiento: 'odo06.stat_followup',
      alta: 'odo06.stat_discharged'
    };
    return t(labels[status] || 'odo06.stat_active');
  };

  const statusClass = status => {
    const classes = {
      activo: 'activo',
      seguimiento: 'en-proceso',
      alta: 'inactivo'
    };
    return classes[status] || 'neutral';
  };

  const avatarColor = name => {
    const initial = String(name || '').codePointAt(0) || 0;
    return String((initial % 5) + 1);
  };

  const patientUrl = id => `/gestion-de-pacientes/${encodeURIComponent(String(id))}`;

  const navigateToPatient = (id, destination) => {
    try {
      sessionStorage.setItem('st_paciente_id', String(id));
    } catch (error) {
      console.error('[SmileTrack] No fue posible guardar el contexto del paciente:', error);
    }

    const route = destination === 'history'
      ? `/historia-clinica/st-odo-03-historial?pacienteId=${encodeURIComponent(String(id))}`
      : patientUrl(id);
    window.location.assign(route);
  };

  const buildEmptyRow = (titleKey, messageKey) => {
    const row = document.createElement('tr');
    const cell = createElement('td');
    cell.colSpan = 6;

    const emptyState = createElement('div', 'empty-state');
    const icon = createElement('span', 'material-symbols-outlined empty-icon', 'person_search');
    icon.setAttribute('aria-hidden', 'true');
    emptyState.append(icon, createElement('h3', 'empty-state-title', t(titleKey)));
    if (messageKey) emptyState.append(createElement('p', 'empty-state-text', t(messageKey)));

    cell.appendChild(emptyState);
    row.appendChild(cell);
    return row;
  };

  const renderPatientRow = patient => {
    const row = createElement('tr');
    row.tabIndex = 0;
    row.dataset.patientId = String(patient.id);
    row.setAttribute('aria-label', t('odo06.patient_row_aria', { name: patient.nombre }));

    const patientCell = createElement('td', 'td-paciente');
    const avatar = createElement('span', 'p-avatar', getInitials(patient.nombre));
    avatar.dataset.color = avatarColor(patient.nombre);
    avatar.setAttribute('aria-hidden', 'true');
    const patientInfo = createElement('span');
    patientInfo.append(
      createElement('span', 'p-name', patient.nombre),
      createElement('span', 'p-cedula', patient.cedula)
    );
    patientCell.append(avatar, patientInfo);

    const lastVisitCell = createElement('td', 'td-fecha');
    lastVisitCell.textContent = formatDate(patient.ultimaISO);

    const serviceCell = createElement('td');
    serviceCell.textContent = patient.servicio || t('odo06.service_unavailable');

    const nextAppointmentCell = createElement('td', `td-cita${patient.proximaISO ? '' : ' sin-cita'}`);
    nextAppointmentCell.textContent = patient.proximaISO
      ? formatDate(patient.proximaISO)
      : t('odo06.no_appointment');

    const statusCell = createElement('td');
    const statusBadge = createElement('span', `badge-estado status-badge ${statusClass(patient.estado)}`, statusLabel(patient.estado));
    statusBadge.setAttribute('role', 'status');
    statusCell.appendChild(statusBadge);

    const actionsCell = createElement('td');
    const actions = createElement('div', 'actions-cell');
    const profileButton = createElement('button', 'btn-secondary action-btn btn-action', t('odo06.action_profile'));
    profileButton.type = 'button';
    profileButton.dataset.action = 'profile';
    profileButton.setAttribute('aria-label', t('odo06.action_profile_aria', { name: patient.nombre }));
    profileButton.title = t('odo06.action_profile');

    const historyButton = createElement('button', 'btn-secondary action-btn btn-action btn-historial', t('odo06.action_history'));
    historyButton.type = 'button';
    historyButton.dataset.action = 'history';
    historyButton.setAttribute('aria-label', t('odo06.action_history_aria', { name: patient.nombre }));
    historyButton.title = t('odo06.action_history');
    actions.append(profileButton, historyButton);
    actionsCell.appendChild(actions);

    row.append(patientCell, lastVisitCell, serviceCell, nextAppointmentCell, statusCell, actionsCell);
    return row;
  };

  const updatePagination = () => {
    const controls = byId('paginationControls');
    const showing = byId('pageShowing');
    const total = byId('pageTotal');
    const previous = byId('btnPrev');
    const next = byId('btnNext');
    const pageButtons = byId('pageNumBtns');

    if (!controls || !showing || !total || !previous || !next || !pageButtons) return;

    const firstItem = state.total === 0 ? 0 : (state.page - 1) * state.pageSize + 1;
    const lastItem = Math.min(state.total, (state.page - 1) * state.pageSize + state.pacientes.length);
    showing.textContent = String(lastItem === 0 ? 0 : firstItem);
    total.textContent = String(state.total);
    controls.hidden = state.total === 0;
    previous.disabled = state.page <= 1;
    next.disabled = state.page >= state.totalPages;

    pageButtons.replaceChildren();
    const firstPage = Math.max(1, Math.min(state.page - 3, state.totalPages - 6));
    const lastPage = Math.min(state.totalPages, firstPage + 6);
    for (let pageNumber = firstPage; pageNumber <= lastPage; pageNumber += 1) {
      const button = createElement('button', `pagination-btn${pageNumber === state.page ? ' active' : ''}`, String(pageNumber));
      button.type = 'button';
      button.dataset.page = String(pageNumber);
      button.setAttribute('aria-label', t('odo06.page_number', { number: pageNumber }));
      if (pageNumber === state.page) button.setAttribute('aria-current', 'page');
      pageButtons.appendChild(button);
    }
  };

  const updateCounts = stats => {
    const values = {
      statTotal: stats.total,
      statActivos: stats.activos,
      statSeguimiento: stats.seguimiento,
      statAltas: stats.altas
    };

    Object.entries(values).forEach(([id, value]) => {
      const element = byId(id);
      const count = Number(value);
      if (!element || !Number.isFinite(count)) return;
      element.dataset.target = String(count);
      element.textContent = String(count);
    });
  };

  const renderTable = () => {
    const tbody = byId('pacientesTbody');
    if (!tbody) return;

    if (state.pacientes.length === 0) {
      tbody.replaceChildren(buildEmptyRow('odo06.no_results_title', 'odo06.no_results_text'));
    } else {
      tbody.replaceChildren(...state.pacientes.map(renderPatientRow));
    }

    updatePagination();
    const hasFilters = Object.values(state.filters).some(Boolean);
    const results = byId('filterResults');
    if (results) {
      results.textContent = hasFilters
        ? t('odo06.result_count', { count: state.total })
        : '';
    }
  };

  const populateServiceFilter = () => {
    const select = byId('filterServicio');
    if (!select) return;

    const selectedService = select.value;
    select.querySelectorAll('option[data-service-option]').forEach(option => option.remove());
    state.servicios.forEach(service => {
      const option = createElement('option', '', service);
      option.value = service;
      option.dataset.serviceOption = 'true';
      select.appendChild(option);
    });

    select.value = state.servicios.includes(selectedService) ? selectedService : '';
  };

  const readFilters = () => {
    state.filters.search = byId('searchInput')?.value.trim() || '';
    state.filters.status = byId('filterEstado')?.value || '';
    state.filters.allergies = byId('filterAlergias')?.value || '';
    state.filters.appointment = byId('filterCita')?.value || '';
    state.filters.service = byId('filterServicio')?.value || '';
  };

  const fetchPatients = async () => {
    const version = ++state.requestVersion;
    const size = byId('pageSizeSelect')?.value || '10';
    state.pageSize = Number(size) || 10;

    const parameters = new URLSearchParams({
      page: String(state.page),
      pageSize: String(state.pageSize)
    });
    if (state.filters.search) parameters.set('search', state.filters.search);
    if (state.filters.status) parameters.set('estado', state.filters.status);
    if (state.filters.allergies) parameters.set('alergias', state.filters.allergies);
    if (state.filters.appointment) parameters.set('cita', state.filters.appointment);
    if (state.filters.service) parameters.set('servicio', state.filters.service);

    try {
      const response = await fetch(`/historia-clinica/st-odo-06-pacientes/data?${parameters}`, {
        headers: { Accept: 'application/json' }
      });
      if (!response.ok) {
        throw new Error(`La consulta de pacientes respondió HTTP ${response.status}.`);
      }

      const result = await response.json();
      if (!result || !Array.isArray(result.items) || !result.stats) {
        throw new Error('La respuesta de pacientes tiene un formato no válido.');
      }
      if (version !== state.requestVersion) return false;

      state.total = Number(result.total);
      state.totalPages = Math.max(1, Number(result.totalPages) || 1);
      state.servicios = Array.isArray(result.servicios) ? result.servicios.map(String) : [];
      state.pacientes = result.items.map(normalizePatient);
      updateCounts(result.stats);
      populateServiceFilter();
      return true;
    } catch (error) {
      if (version !== state.requestVersion) return false;
      console.error('[SmileTrack] No se pudieron cargar los pacientes:', error);
      notify('odo06.load_error');
      const tbody = byId('pacientesTbody');
      if (tbody) tbody.replaceChildren(buildEmptyRow('odo06.load_error_title', 'odo06.load_error'));
      state.total = 0;
      state.totalPages = 1;
      updatePagination();
      return false;
    }
  };

  const loadAndRender = async () => {
    if (await fetchPatients()) renderTable();
  };

  const debounce = (callback, delay) => {
    let timeoutId;
    return (...args) => {
      window.clearTimeout(timeoutId);
      timeoutId = window.setTimeout(() => callback(...args), delay);
    };
  };

  const navigatePage = async page => {
    if (page < 1 || page > state.totalPages) return;
    state.page = page;
    await loadAndRender();
  };

  const applyFilters = async () => {
    readFilters();
    state.page = 1;
    byId('btnClearFilters')?.classList.toggle('active', Object.values(state.filters).some(Boolean));
    await loadAndRender();
  };

  const clearFilters = async () => {
    ['searchInput', 'filterEstado', 'filterAlergias', 'filterCita', 'filterServicio'].forEach(id => {
      const element = byId(id);
      if (element) element.value = '';
    });
    state.filters = { search: '', status: '', allergies: '', appointment: '', service: '' };
    byId('btnClearFilters')?.classList.remove('active');
    state.page = 1;
    await loadAndRender();
  };

  const getInitials = name => String(name || '')
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(part => Array.from(part)[0] || '')
    .join('')
    .toLocaleUpperCase(t('odo06.date_locale'));

  const bindEvents = () => {
    byId('searchInput')?.addEventListener('input', debounce(applyFilters, 250));
    ['filterEstado', 'filterAlergias', 'filterCita', 'filterServicio'].forEach(id => {
      byId(id)?.addEventListener('change', applyFilters);
    });
    byId('pageSizeSelect')?.addEventListener('change', () => {
      state.page = 1;
      loadAndRender();
    });
    byId('btnClearFilters')?.addEventListener('click', clearFilters);
    byId('btnPrev')?.addEventListener('click', () => navigatePage(state.page - 1));
    byId('btnNext')?.addEventListener('click', () => navigatePage(state.page + 1));
    byId('pageNumBtns')?.addEventListener('click', event => {
      const button = event.target.closest('button[data-page]');
      if (button) navigatePage(Number(button.dataset.page));
    });

    byId('pacientesTbody')?.addEventListener('click', event => {
      const row = event.target.closest('tr[data-patient-id]');
      if (!row) return;
      const action = event.target.closest('button[data-action]')?.dataset.action;
      navigateToPatient(row.dataset.patientId, action === 'history' ? 'history' : 'profile');
    });
    byId('pacientesTbody')?.addEventListener('keydown', event => {
      const row = event.target.closest('tr[data-patient-id]');
      if (!row || event.target !== row || (event.key !== 'Enter' && event.key !== ' ')) return;
      event.preventDefault();
      navigateToPatient(row.dataset.patientId, 'profile');
    });

    document.addEventListener('i18n:change', () => {
      renderTable();
      populateServiceFilter();
    });
  };

  const initialize = async () => {
    try {
      bindEvents();
      await loadAndRender();
    } catch (error) {
      console.error('[SmileTrack] No fue posible inicializar la vista de pacientes:', error);
      notify('odo06.load_error');
    }
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else {
    initialize();
  }
})();
