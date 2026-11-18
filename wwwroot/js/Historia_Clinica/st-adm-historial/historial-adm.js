(() => {
  'use strict';

  const translate = (key, values) => window.I18N?.t(key, values) || key;
  const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, character => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  }[character]));

  const state = {
    patients: [],
    activePatient: null,
    currentPage: 1,
    pageSize: 10,
    totalPages: 1,
    totalRecords: 0,
    requestVersion: 0,
    dentists: [],
    focusTarget: null,
    bodyOverflow: ''
  };

  const formatDate = value => {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return new Intl.DateTimeFormat(window.I18N?.lang === 'en' ? 'en-US' : 'es-CO', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    }).format(date);
  };

  const normalizePatient = patient => ({
    ...patient,
    fechaNacOriginal: patient.fechaNac,
    ultimaConsultaOriginal: patient.ultimaConsulta,
    fechaNac: formatDate(patient.fechaNac),
    ultimaConsulta: formatDate(patient.ultimaConsulta),
    consultas: (patient.consultas || []).map(consultation => ({
      ...consultation,
      fechaOriginal: consultation.fecha,
      fecha: formatDate(consultation.fecha)
    })),
    tratamientos: patient.tratamientos || [],
    documentos: patient.documentos || []
  });

  const showError = () => {
    const toast = window.ToastService?.error;
    if (typeof toast !== 'function') {
      console.error('[SmileTrack][Historia] El servicio compartido de notificaciones no está disponible.');
      return;
    }
    toast(translate('historia_admin.error_title'), translate('historia_admin.load_error'));
  };

  const populateDentistFilter = () => {
    const select = document.getElementById('filterOdontologo');
    if (!select) return;
    const selected = select.value;
    const options = state.dentists.map(({ key, nombre }) =>
      `<option value="${escapeHtml(key)}">${escapeHtml(nombre)}</option>`).join('');
    select.innerHTML = `<option value="">${escapeHtml(translate('historia_admin.all_dentists'))}</option>${options}`;
    if (state.dentists.some(dentist => dentist.key === selected)) select.value = selected;
  };

  const updateStatistics = stats => {
    const values = {
      statTotal: stats?.total ?? 0,
      statHoy: stats?.hoy ?? 0,
      statAlerta: stats?.alertas ?? 0,
      statOdo: stats?.odontologos ?? 0
    };
    Object.entries(values).forEach(([id, value]) => {
      const element = document.getElementById(id);
      if (element) {
        element.textContent = String(value);
        element.dataset.target = String(value);
      }
    });
  };

  const updatePagination = () => {
    const controls = document.getElementById('paginationControls');
    const showing = document.getElementById('pageShowing');
    const total = document.getElementById('pageTotal');
    const pageInfo = document.getElementById('pageInfo');
    const previous = document.getElementById('btnPrev');
    const next = document.getElementById('btnNext');
    const pageCount = Math.max(1, state.totalPages);

    controls.hidden = state.totalRecords <= state.pageSize;
    showing.textContent = String(state.patients.length);
    total.textContent = String(state.totalRecords);
    pageInfo.textContent = `${state.currentPage} ${translate('historia_admin.of')} ${pageCount}`;
    previous.disabled = state.currentPage <= 1;
    next.disabled = state.currentPage >= pageCount;
  };

  const renderTable = () => {
    const tbody = document.getElementById('tablaBody');
    const empty = document.getElementById('noResults');
    const count = document.getElementById('resultsCount');
    const patientWord = translate(state.totalRecords === 1 ? 'historia_admin.patient_singular' : 'historia_admin.patients');
    count.textContent = translate('historia_admin.results_count', {
      shown: state.patients.length,
      total: state.totalRecords,
      patients: patientWord
    });
    empty.hidden = state.totalRecords !== 0;
    tbody.replaceChildren();

    state.patients.forEach(patient => {
      const initials = String(patient.nombre || '')
        .split(/\s+/)
        .filter(Boolean)
        .map(name => name[0])
        .join('')
        .slice(0, 2)
        .toLocaleUpperCase();
      const patientId = Number(patient.id);
      const avatarColors = ['blue', 'green', 'yellow', 'purple', 'slate'];
      const colorIndex = Math.abs(Number.isSafeInteger(patientId) ? patientId : 0) % avatarColors.length;
      const avatarColor = avatarColors[colorIndex];
      const status = String(patient.estado || '').trim().toLocaleLowerCase();
      const statusLabel = status ? `${status[0].toLocaleUpperCase()}${status.slice(1)}` : translate('common.not_available');
      const row = document.createElement('tr');
      if (state.activePatient?.id === patient.id) row.classList.add('activa');
      row.innerHTML = `
        <td data-label="${escapeHtml(translate('historia_admin.patient'))}">
          <div class="patient-info">
            <span class="patient-avatar" data-color="${avatarColor}" aria-hidden="true">${escapeHtml(initials)}</span>
            <span class="patient-details">
              <span class="patient-name">${escapeHtml(patient.nombre)}</span>
              <span class="patient-id">${escapeHtml(patient.email || translate('common.not_available'))}</span>
            </span>
          </div>
        </td>
        <td data-label="${escapeHtml(translate('historia_admin.document'))}">${escapeHtml(patient.cedula)}</td>
        <td data-label="${escapeHtml(translate('historia_admin.dentist'))}">${escapeHtml(patient.odontologo)}</td>
        <td data-label="${escapeHtml(translate('historia_admin.last_visit'))}">${escapeHtml(patient.ultimaConsulta)}</td>
        <td data-label="${escapeHtml(translate('historia_admin.alert'))}">
          <span class="badge-alerta ${patient.alerta ? 'si' : 'no'}">${escapeHtml(translate(patient.alerta ? 'historia_admin.yes' : 'historia_admin.no'))}</span>
        </td>
        <td data-label="${escapeHtml(translate('facturacion.status'))}">
          <span class="status-badge ${status === 'activo' ? 'activo' : 'inactivo'}">${escapeHtml(statusLabel)}</span>
        </td>
        <td class="actions-cell" data-label="${escapeHtml(translate('table.actions'))}">
          <button type="button" class="action-btn btn-history" data-action="view" data-id="${Number.isSafeInteger(patientId) ? patientId : ''}" aria-label="${escapeHtml(translate('historia_admin.view_history', { name: patient.nombre }))}" title="${escapeHtml(translate('historia_admin.view_history', { name: patient.nombre }))}">
            <span class="material-symbols-outlined action-icon" aria-hidden="true">clinical_notes</span>
            <span class="btn-text">${escapeHtml(translate('historia_admin.view'))}</span>
          </button>
        </td>`;
      tbody.appendChild(row);
    });

    updatePagination();
  };

  const loadPatients = async () => {
    const version = ++state.requestVersion;
    const selectedPageSize = document.getElementById('pageSizeSelect').value;
    state.pageSize = selectedPageSize === 'all' ? 100 : Number(selectedPageSize) || 10;

    const params = new URLSearchParams({
      page: String(state.currentPage),
      pageSize: String(state.pageSize)
    });
    const filters = {
      search: document.getElementById('searchInput').value.trim(),
      odontologo: document.getElementById('filterOdontologo').value,
      alerta: document.getElementById('filterAlerta').value,
      estado: document.getElementById('filterEstado').value
    };
    Object.entries(filters).forEach(([key, value]) => {
      if (value) params.set(key, value);
    });

    const tbody = document.getElementById('tablaBody');
    tbody.setAttribute('aria-busy', 'true');
    try {
      const response = await fetch(`/historia-clinica/st-adm-historial/data?${params}`, {
        headers: { Accept: 'application/json' }
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const data = await response.json();
      if (version !== state.requestVersion) return;

      state.patients = (data.items || []).map(normalizePatient);
      state.totalRecords = data.total || 0;
      state.totalPages = data.totalPages || 1;
      state.dentists = data.odontologos || [];
      updateStatistics(data.stats);
      populateDentistFilter();
    } catch (error) {
      if (version !== state.requestVersion) return;
      console.error('No se pudo cargar el historial de pacientes.', error);
      state.patients = [];
      state.totalRecords = 0;
      state.totalPages = 1;
      showError();
    } finally {
      if (version === state.requestVersion) {
        tbody.setAttribute('aria-busy', 'false');
        renderTable();
      }
    }
  };

  const switchTab = tabName => {
    document.querySelectorAll('.tab-btn').forEach(button => {
      const selected = button.dataset.tab === tabName;
      button.classList.toggle('active', selected);
      button.setAttribute('aria-selected', String(selected));
      button.tabIndex = selected ? 0 : -1;
    });
    document.querySelectorAll('.tab-content').forEach(panel => {
      const selected = panel.id === `tab-${tabName}`;
      panel.classList.toggle('active', selected);
      panel.hidden = !selected;
    });
  };

  const renderInfoGrid = patient => {
    const items = [
      ['historia_admin.birth_date', patient.fechaNac],
      ['historia_admin.phone', patient.telefono],
      ['historia_admin.dentist', patient.odontologo],
      ['facturacion.status', patient.estado]
    ];
    document.getElementById('panelInfoGrid').innerHTML = items.map(([label, value]) => `
      <div class="info-item">
        <span class="info-label">${escapeHtml(translate(label))}</span>
        <span class="info-val">${escapeHtml(value || translate('common.not_available'))}</span>
      </div>`).join('');
  };

  const renderConsultations = patient => {
    document.getElementById('consultasList').innerHTML = patient.consultas.length
      ? patient.consultas.map(consultation => `
        <article class="consulta-item">
          <time class="consulta-fecha">${escapeHtml(consultation.fecha)}</time>
          <h3 class="consulta-proc">${escapeHtml(consultation.proc)}</h3>
          <p class="consulta-odo">${escapeHtml(consultation.odo)}</p>
          <p class="consulta-nota">${escapeHtml(consultation.nota || translate('historia_admin.no_consultation_notes'))}</p>
        </article>`).join('')
      : `<div class="empty-state"><h3>${escapeHtml(translate('historia_admin.no_consultations'))}</h3></div>`;
  };

  const renderTreatments = patient => {
    document.getElementById('tratamientosList').innerHTML = patient.tratamientos.length
      ? patient.tratamientos.map(treatment => {
        const progress = Math.min(100, Math.max(0, Number(treatment.progreso) || 0));
        return `
          <article class="trat-item">
            <h3 class="trat-nombre">${escapeHtml(treatment.nombre)}</h3>
            <div class="trat-bar-wrap" role="progressbar" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="100">
              <span class="trat-bar" style="width:${progress}%"></span>
            </div>
            <div class="trat-meta"><span>${progress}%</span><span>${escapeHtml(treatment.sesiones)} ${escapeHtml(translate('historia_admin.sessions'))}</span></div>
          </article>`;
      }).join('')
      : `<div class="empty-state"><h3>${escapeHtml(translate('historia_admin.no_treatments'))}</h3></div>`;
  };

  const renderDocuments = patient => {
    document.getElementById('documentosList').innerHTML = patient.documentos.length
      ? patient.documentos.map(document => {
        const type = String(document.tipo || '').toLocaleLowerCase();
        return `
          <article class="doc-item">
            <span class="material-symbols-outlined doc-icon" aria-hidden="true">${type === 'pdf' ? 'picture_as_pdf' : 'image'}</span>
            <span class="doc-details">
              <span class="doc-nombre">${escapeHtml(document.nombre)}</span>
              <span class="doc-fecha">${escapeHtml(formatDate(document.fecha))}</span>
            </span>
            <span class="doc-badge">${escapeHtml(type.toLocaleUpperCase())}</span>
          </article>`;
      }).join('')
      : `<div class="empty-state"><h3>${escapeHtml(translate('historia_admin.no_documents'))}</h3></div>`;
  };

  const refreshLocalizedDates = patient => {
    patient.fechaNac = formatDate(patient.fechaNacOriginal);
    patient.ultimaConsulta = formatDate(patient.ultimaConsultaOriginal);
    patient.consultas.forEach(consultation => {
      consultation.fecha = formatDate(consultation.fechaOriginal);
    });
  };

  const openPanel = id => {
    const patient = state.patients.find(item => Number(item.id) === id);
    if (!patient) return;
    const panel = document.getElementById('panelLateral');
    const overlay = document.getElementById('panelOverlay');
    state.activePatient = patient;
    state.focusTarget = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    state.bodyOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    document.getElementById('panelNombre').textContent = patient.nombre || '';
    document.getElementById('panelCedula').textContent = `${patient.tipoDocumento || 'CC'} ${patient.cedula || ''}`.trim();
    renderInfoGrid(patient);
    const alert = document.getElementById('panelAlerta');
    alert.hidden = !patient.alerta;
    document.getElementById('panelAlertaTexto').textContent = patient.alertaTexto || '';
    renderConsultations(patient);
    renderTreatments(patient);
    renderDocuments(patient);
    switchTab('consultas');

    panel.classList.add('open');
    panel.setAttribute('aria-hidden', 'false');
    panel.removeAttribute('inert');
    overlay.classList.add('open');
    overlay.setAttribute('aria-hidden', 'false');
    document.getElementById('panelClose').focus();
  };

  const closePanel = () => {
    const panel = document.getElementById('panelLateral');
    if (panel.getAttribute('aria-hidden') === 'true') return;
    const patientId = state.activePatient?.id;
    panel.classList.remove('open');
    panel.setAttribute('aria-hidden', 'true');
    panel.setAttribute('inert', '');
    const overlay = document.getElementById('panelOverlay');
    overlay.classList.remove('open');
    overlay.setAttribute('aria-hidden', 'true');
    document.body.style.overflow = state.bodyOverflow;
    state.activePatient = null;
    renderTable();
    const replacementTarget = patientId === undefined
      ? null
      : document.querySelector(`#tablaBody button[data-id="${Number(patientId)}"]`);
    if (replacementTarget) replacementTarget.focus();
    else if (state.focusTarget?.isConnected) state.focusTarget.focus();
    state.focusTarget = null;
  };

  const debounce = (callback, delay) => {
    let timer;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => callback(...args), delay);
    };
  };

  const init = () => {
    const search = document.getElementById('searchInput');
    const dentist = document.getElementById('filterOdontologo');
    const alert = document.getElementById('filterAlerta');
    const status = document.getElementById('filterEstado');
    const pageSize = document.getElementById('pageSizeSelect');
    const reloadFromFirstPage = () => {
      state.currentPage = 1;
      loadPatients();
    };

    search.addEventListener('input', debounce(reloadFromFirstPage, 250));
    [dentist, alert, status].forEach(select => select.addEventListener('change', reloadFromFirstPage));
    pageSize.addEventListener('change', reloadFromFirstPage);
    document.getElementById('btnPrev').addEventListener('click', () => {
      if (state.currentPage > 1) {
        state.currentPage--;
        loadPatients();
      }
    });
    document.getElementById('btnNext').addEventListener('click', () => {
      if (state.currentPage < state.totalPages) {
        state.currentPage++;
        loadPatients();
      }
    });
    document.getElementById('tablaBody').addEventListener('click', event => {
      const button = event.target.closest('button[data-action="view"]');
      const id = Number(button?.dataset.id);
      if (button && Number.isSafeInteger(id)) openPanel(id);
    });
    document.getElementById('panelClose').addEventListener('click', closePanel);
    document.getElementById('panelOverlay').addEventListener('click', closePanel);
    document.querySelector('.panel-tabs').addEventListener('click', event => {
      const button = event.target.closest('button[data-tab]');
      if (button) switchTab(button.dataset.tab);
    });
    document.querySelector('.panel-tabs').addEventListener('keydown', event => {
      if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
      const tabs = Array.from(document.querySelectorAll('.tab-btn'));
      const index = tabs.indexOf(document.activeElement);
      if (index < 0) return;
      event.preventDefault();
      const next = event.key === 'Home' ? 0
        : event.key === 'End' ? tabs.length - 1
          : (index + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length;
      tabs[next].focus();
      switchTab(tabs[next].dataset.tab);
    });
    document.addEventListener('keydown', event => {
      const panel = document.getElementById('panelLateral');
      if (panel.getAttribute('aria-hidden') === 'true') return;
      if (event.key === 'Escape') {
        event.preventDefault();
        closePanel();
        return;
      }
      if (event.key !== 'Tab') return;
      const focusable = Array.from(panel.querySelectorAll('button:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'))
        .filter(element => !element.hidden && element.getClientRects().length > 0);
      if (!focusable.length) {
        event.preventDefault();
        panel.focus();
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
    document.addEventListener('i18n:change', () => {
      populateDentistFilter();
      state.patients.forEach(refreshLocalizedDates);
      renderTable();
      if (state.activePatient) {
        renderInfoGrid(state.activePatient);
        renderConsultations(state.activePatient);
        renderTreatments(state.activePatient);
        renderDocuments(state.activePatient);
      }
    });
    loadPatients();
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    init();
  }
})();
