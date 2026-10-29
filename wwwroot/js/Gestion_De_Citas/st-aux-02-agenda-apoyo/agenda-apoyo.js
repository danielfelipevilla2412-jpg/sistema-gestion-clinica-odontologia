/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Agenda de Apoyo Clínico (st-aux-02-agenda-apoyo)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Maneja el comportamiento interactivo de la agenda de apoyo para el equipo auxiliar.
 * Permite filtrar por profesional y tipo de tratamiento, resaltar pacientes con alergias críticas
 * y gestionar la asistencia a sillón o soporte quirúrgico.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtrado combinado dinámico con soporte accesible ARIA para navegación por teclado.
 * - Resaltado visual de advertencias de salud (alergias, observaciones) para seguridad del paciente.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux02AgendaApoyo
 * - HTML: Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const DEFAULT_FILTER_ALL = 'todos';

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

class AgendaSupportController {
  constructor() {
    const data = window.smiletrackAgendaApoyoData || {};
    this._fechaHoy = data.fechaHoy || '';
    this._citasBase = data.citas || [];
    this._filtroProfesional = DEFAULT_FILTER_ALL;
    this._filtroTipo = DEFAULT_FILTER_ALL;
  }

  async getCitas(profesional = DEFAULT_FILTER_ALL, tipo = DEFAULT_FILTER_ALL) {
    let resultado = [...this._citasBase];
    
    if (profesional !== DEFAULT_FILTER_ALL) {
      const normalizar = (valor) => String(valor || '').trim().toLocaleLowerCase('es-CO');
      resultado = resultado.filter(c => normalizar(c.profesional) === normalizar(profesional));
    }
    if (tipo !== DEFAULT_FILTER_ALL) {
      resultado = resultado.filter(c => c.tipo === tipo);
    }
    return resultado;
  }

  getFechaHoy() { return this._fechaHoy; }
}

const agendaSupportControllerInstance = new AgendaSupportController();

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const formatDateSpanishLabel = (valueString) => {
  if (!valueString) return '—';
  const dateObj = new Date(valueString + 'T00:00:00');
  return Number.isNaN(dateObj.getTime()) ? valueString : dateObj.toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
};

const createBadgeElement = (classNameText, labelText, ariaLabelText) => {
  const badgeSpan = document.createElement('span');
  badgeSpan.className = classNameText;
  badgeSpan.setAttribute('role', 'status');
  badgeSpan.setAttribute('aria-label', ariaLabelText);
  badgeSpan.textContent = labelText;
  return badgeSpan;
};

const createTypeBadge = (typeString) => {
  const badgeMap = {
    consulta: ['badge-consulta', 'Consulta'],
    procedimiento: ['badge-procedimiento', 'Procedimiento'],
    urgencia: ['badge-urgencia', 'Urgencia'],
  };
  const [cssClass, labelText] = badgeMap[typeString] || ['', typeString || 'Sin tipo'];
  return createBadgeElement(`badge-tipo ${cssClass}`, labelText, `Tipo: ${labelText}`);
};

const createAllergiesBadge = (allergyText) => {
  if (allergyText) {
    return createBadgeElement('badge-alergia', `Alerta: ${allergyText}`, `Alergia: ${allergyText}`);
  }
  const emptyBadge = document.createElement('span');
  emptyBadge.style.color = 'var(--text-muted)';
  emptyBadge.setAttribute('aria-label', 'Sin alergias registradas');
  emptyBadge.textContent = '—';
  return emptyBadge;
};

const createStatusBadge = (statusString) => {
  const normalizedStatus = CommonUtils.normalizeAppointmentStatus(statusString);
  const statusCssMap = {
    atendida: 'badge-atendida',
    pendiente: 'badge-pendiente',
    cancelada: 'badge-cancelada',
    no_asistida: 'badge-no-asistio'
  };
  return createBadgeElement(`badge-estado ${statusCssMap[normalizedStatus] || 'badge-pendiente'}`, `● ${statusString}`, `Estado: ${statusString}`);
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const applyCombinedFilters = async () => {
  const appointmentsData = await agendaSupportControllerInstance.getCitas(
    agendaSupportControllerInstance._filtroProfesional,
    agendaSupportControllerInstance._filtroTipo
  );
  renderAgendaSupportTable(appointmentsData);
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const renderAgendaSupportTable = (appointmentsList) => {
  const tableBody = safeGetElement('agendaBody');
  const emptyStateElement = safeGetElement('tableEmpty');
  if (!tableBody) return;

  if (!appointmentsList.length) {
    tableBody.replaceChildren();
    if (emptyStateElement) {
      emptyStateElement.style.display = 'block';
      emptyStateElement.setAttribute('aria-label', 'No hay citas que coincidan con los filtros aplicados');
    }
    return;
  }
  if (emptyStateElement) emptyStateElement.style.display = 'none';

  const createTableCell = (classNameText, cellText) => {
    const tableCell = document.createElement('td');
    tableCell.className = classNameText;
    tableCell.textContent = cellText || '';
    return tableCell;
  };

  tableBody.replaceChildren(...appointmentsList.map(item => {
    const tableRow = document.createElement('tr');
    tableRow.setAttribute('role', 'row');
    tableRow.append(
      createTableCell('td-fecha', item.fecha || '—'),
      createTableCell('td-hora', item.hora || '—'),
      createTableCell('td-paciente', item.paciente || 'Paciente sin datos'),
      createTableCell('td-profesional', item.profesional || 'Sin asignar')
    );
    const typeCell = document.createElement('td');
    typeCell.appendChild(createTypeBadge(item.tipo));
    const allergyCell = document.createElement('td');
    allergyCell.appendChild(createAllergiesBadge(item.alergia));
    const statusCell = document.createElement('td');
    statusCell.appendChild(createStatusBadge(item.estado));
    tableRow.append(typeCell, allergyCell, statusCell);
    return tableRow;
  }));
};

const updateHeaderMetadata = () => {
  const metaElement = safeGetElement('phMeta');
  if (metaElement) {
    const urlParams = new URLSearchParams(window.location.search);
    const dateParam = urlParams.get('fecha') || agendaSupportControllerInstance.getFechaHoy();
    const weekStartParam = urlParams.get('weekStart');

    const metaDescriptionText = weekStartParam
      ? `Semana del ${formatDateSpanishLabel(weekStartParam)} al ${formatDateSpanishLabel(new Date(new Date(weekStartParam + 'T00:00:00').getTime() + 6 * 86400000).toISOString().slice(0, 10))}`
      : `Citas del día ${formatDateSpanishLabel(dateParam)}`;

    metaElement.textContent = metaDescriptionText;
    metaElement.setAttribute('data-meta-text', metaDescriptionText);
    metaElement.setAttribute('aria-label', `Información: ${metaDescriptionText}`);
  }
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const setupTypeFilterButtons = () => {
  const filterButtonsList = document.querySelectorAll('.filter-btn[data-value]');
  
  filterButtonsList.forEach(buttonElement => {
    buttonElement.addEventListener('click', () => {
      const filterValue = buttonElement.dataset.value;
      agendaSupportControllerInstance._filtroTipo = filterValue;
      
      filterButtonsList.forEach(btn => {
        btn.classList.remove('active');
        btn.setAttribute('aria-checked', 'false');
      });
      buttonElement.classList.add('active');
      buttonElement.setAttribute('aria-checked', 'true');
      
      applyCombinedFilters();
      
      if (window.ToastService) window.ToastService.success(`Filtro aplicado: ${buttonElement.textContent.trim()}`, 'info');
    });
    
    buttonElement.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        buttonElement.click();
      }
    });
  });
};

const setupProfessionalDropdownMenu = () => {
  const dropdownBtn = safeGetElement('btnProfesional');
  const dropdownMenu = safeGetElement('dropdownMenu');
  const menuItemsList = dropdownMenu?.querySelectorAll('.dd-item');
  
  if (!dropdownBtn || !dropdownMenu || !menuItemsList) return;
  
  dropdownBtn.addEventListener('click', () => {
    const isCurrentlyOpen = dropdownMenu.classList.toggle('open');
    dropdownBtn.setAttribute('aria-expanded', isCurrentlyOpen);
    if (isCurrentlyOpen) {
      menuItemsList[0]?.focus();
    }
  });
  
  document.addEventListener('click', (event) => {
    const dropdownWrap = document.querySelector('.dropdown-wrap');
    if (dropdownWrap && !dropdownWrap.contains(event.target)) {
      dropdownMenu.classList.remove('open');
      dropdownBtn.setAttribute('aria-expanded', 'false');
    }
  });
  
  menuItemsList.forEach(itemElement => {
    itemElement.addEventListener('click', () => {
      const professionalValue = itemElement.dataset.value;
      agendaSupportControllerInstance._filtroProfesional = professionalValue;
      
      const arrowSpan = document.createElement('span');
      arrowSpan.className = 'dd-arrow';
      arrowSpan.setAttribute('aria-hidden', 'true');
      arrowSpan.textContent = '▼';
      dropdownBtn.replaceChildren(document.createTextNode(itemElement.textContent.trim() + ' '), arrowSpan);
      
      menuItemsList.forEach(item => item.classList.remove('active'));
      itemElement.classList.add('active');
      
      dropdownMenu.classList.remove('open');
      dropdownBtn.setAttribute('aria-expanded', 'false');
      
      applyCombinedFilters();
      if (window.ToastService) window.ToastService.success(`Profesional: ${itemElement.textContent.trim()}`, 'info');
    });
    
    itemElement.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        itemElement.click();
      } else if (event.key === 'Escape') {
        dropdownMenu.classList.remove('open');
        dropdownBtn.setAttribute('aria-expanded', 'false');
        dropdownBtn.focus();
      } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const currentIndex = Array.from(menuItemsList).indexOf(itemElement);
        const nextIndex = event.key === 'ArrowDown' 
          ? (currentIndex + 1) % menuItemsList.length 
          : (currentIndex - 1 + menuItemsList.length) % menuItemsList.length;
        menuItemsList[nextIndex]?.focus();
      }
    });
  });
  
  dropdownBtn.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      dropdownBtn.click();
    } else if (event.key === 'ArrowDown' && dropdownMenu.classList.contains('open')) {
      event.preventDefault();
      menuItemsList[0]?.focus();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupMobileNavigationMenu = () => {
  // El menú móvil es gestionado centralizadamente por ~/js/shared/sidebar.js
};

const initializeAgendaApoyoModule = async () => {
  setupMobileNavigationMenu();
  setupTypeFilterButtons();
  setupProfessionalDropdownMenu();

  document.querySelector('.toggle-vistas')?.addEventListener('viewchange', (event) => {
    const urlParams = new URLSearchParams(window.location.search);
    const currentDate = urlParams.get('fecha') || new Date().toISOString().slice(0, 10);
    if (event.detail.view === 'semana') {
      urlParams.delete('fecha');
      urlParams.set('weekStart', urlParams.get('weekStart') || currentDate);
    } else if (event.detail.view === 'dia') {
      urlParams.delete('weekStart');
      urlParams.set('fecha', currentDate);
    } else {
      urlParams.delete('weekStart');
      urlParams.set('fecha', currentDate);
    }
    window.location.search = urlParams.toString();
  });
  
  updateHeaderMetadata();
  
  const initialAppointments = await agendaSupportControllerInstance.getCitas();
  renderAgendaSupportTable(initialAppointments);
  
  window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeAgendaApoyoModule);