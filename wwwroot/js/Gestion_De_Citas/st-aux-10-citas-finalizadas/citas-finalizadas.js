/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Citas Finalizadas (st-aux-10-citas-finalizadas)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Despliega el resumen de citas concluidas de la jornada.
 * Permite al personal de apoyo revisar el histórico del día y exportar el informe de atenciones a CSV.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Filtra únicamente citas en estado "atendida" o "finalizada".
 * - Generación de archivo CSV cliente sin recarga de página para facilitar auditoría rápida.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux10CitasFinalizadas
 * - HTML: Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const EXPORT_RESET_TIMEOUT_MS = 2000;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

const summaryIntervalsWeakMap = new WeakMap();

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const formatDateForExport = () => {
  const dateObj = new Date();
  const monthNames = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  return `${dateObj.getDate()} de ${monthNames[dateObj.getMonth()]} ${dateObj.getFullYear()}`;
};

const escapeCsvValue = (value) => {
  const textValue = String(value ?? '');
  return `"${textValue.replaceAll('"', '""')}"`;
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const exportFinalizedAppointmentsToCsv = (buttonElement) => {
  const originalHtml = buttonElement.innerHTML;
  buttonElement.innerHTML = '<span class="material-symbols-outlined action-icon" aria-hidden="true">hourglass_top</span><span class="btn-text">Generando...</span>';
  buttonElement.disabled = true;
  
  try {
    const appointmentsList = finalizedStorage.load();
    const countsSummary = finalizedStorage.getCounts(appointmentsList);
    
    const csvLines = [
      'Fecha de exportación,' + formatDateForExport(),
      '',
      'RESUMEN',
      'Estado,Cantidad',
      `Atendida,${countsSummary.atendidas}`,
      `Cancelada,${countsSummary.canceladas}`,
      `No asistió,${countsSummary.noAsistio}`,
      `Total,${appointmentsList.length}`,
      '',
      'DETALLE DE CITAS',
      'Hora,Paciente,Profesional,Servicio,Estado',
      ...appointmentsList.map(item => [item.hora, item.paciente, item.profesional, item.servicio, item.estado].map(escapeCsvValue).join(','))
    ].join('\n');
    
    const csvBlob = new Blob([csvLines], { type: 'text/csv;charset=utf-8;' });
    const downloadUrl = URL.createObjectURL(csvBlob);
    const downloadLink = document.createElement('a');
    downloadLink.href = downloadUrl;
    downloadLink.download = `smiletrack_citas_finalizadas_${new Date().toISOString().split('T')[0]}.csv`;
    downloadLink.style.display = 'none';
    document.body.appendChild(downloadLink);
    downloadLink.click();
    document.body.removeChild(downloadLink);
    URL.revokeObjectURL(downloadUrl);
    
    buttonElement.innerHTML = '<span class="material-symbols-outlined action-icon" aria-hidden="true">check_circle</span><span class="btn-text">Descargado</span>';
    buttonElement.style.background = '#dcfce7';
    buttonElement.style.borderColor = '#22c55e';
    buttonElement.style.color = '#166534';
    if (window.ToastService) {
      window.ToastService.success('Resumen descargado exitosamente');
    }
    
    setTimeout(() => {
      buttonElement.innerHTML = originalHtml;
      buttonElement.disabled = false;
      buttonElement.style.background = '';
      buttonElement.style.borderColor = '';
      buttonElement.style.color = '';
    }, EXPORT_RESET_TIMEOUT_MS);
    
  } catch (error) {
    console.error('Error al exportar:', error);
    buttonElement.innerHTML = originalHtml;
    buttonElement.disabled = false;
    if (window.ToastService) window.ToastService.error('Error al generar resumen');
  }
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const finalizedStorage = {
  load: () => (window.smiletrackCitasFinalizadasData?.citas) || [],

  getCounts: (appointments) => {
    return {
      atendidas: appointments.filter(item => CommonUtils.normalizeAppointmentStatus(item.estado) === 'atendida').length,
      canceladas: appointments.filter(item => CommonUtils.normalizeAppointmentStatus(item.estado) === 'cancelada').length,
      noAsistio: appointments.filter(item => CommonUtils.normalizeAppointmentStatus(item.estado) === 'no_asistida').length
    };
  }
};

const renderAppointmentsTable = (dataList) => {
  const tableBody = safeGetElement('appointmentsBody');
  if (!tableBody) return;

  if (dataList.length === 0) {
    const emptyRow = document.createElement('tr');
    emptyRow.className = 'empty-state-row';
    emptyRow.setAttribute('role', 'row');
    emptyRow.setAttribute('aria-label', 'Estado vacío');
    const emptyCell = document.createElement('td');
    emptyCell.colSpan = 5;
    emptyCell.className = 'empty-state-cell';
    const contentDiv = document.createElement('div');
    contentDiv.className = 'empty-state-content';
    contentDiv.setAttribute('role', 'status');
    contentDiv.setAttribute('aria-live', 'polite');
    const iconDiv = document.createElement('div');
    iconDiv.className = 'empty-state-icon';
    iconDiv.setAttribute('aria-hidden', 'true');
    iconDiv.textContent = '📅';
    const messageParagraph = document.createElement('p');
    messageParagraph.className = 'empty-state-message';
    messageParagraph.textContent = 'No hay citas finalizadas registradas.';
    contentDiv.append(iconDiv, messageParagraph);
    emptyCell.appendChild(contentDiv);
    emptyRow.appendChild(emptyCell);
    tableBody.replaceChildren(emptyRow);
    return;
  }

  tableBody.replaceChildren(...dataList.map(apt => {
    const normalizedStatus = CommonUtils.normalizeAppointmentStatus(apt.estado);
    const statusClass = normalizedStatus === 'atendida' ? 'atendida' :
               normalizedStatus === 'cancelada' ? 'cancelada' : 'no-asistio';
    const tableRow = document.createElement('tr');
    tableRow.setAttribute('role', 'row');
    const horaVisibleText = apt.esHoy === false && apt.fecha
      ? `${apt.fecha} ${apt.hora || ''}`.trim()
      : (apt.hora || '');
    const createCell = (className, value) => {
      const element = document.createElement('td');
      element.className = className;
      element.textContent = value || '';
      return element;
    };
    const statusCell = document.createElement('td');
    const statusBadgeSpan = document.createElement('span');
    statusBadgeSpan.className = `status-badge ${statusClass}`;
    statusBadgeSpan.setAttribute('role', 'status');
    statusBadgeSpan.setAttribute('aria-label', `Estado: ${apt.estado}`);
    statusBadgeSpan.textContent = apt.estado || 'Sin estado';
    statusCell.appendChild(statusBadgeSpan);
    tableRow.append(createCell('td-hora', horaVisibleText), createCell('td-paciente', apt.paciente),
      createCell('td-profesional', apt.profesional), createCell('td-servicio', apt.servicio), statusCell);
    return tableRow;
  }));
};

const updateSummaryCounters = () => {
  const appointmentsList = finalizedStorage.load();
  const countsSummary = finalizedStorage.getCounts(appointmentsList);
  
  const elementMap = {
    atendidas: safeGetElement('countAtendidas'),
    canceladas: safeGetElement('countCanceladas'),
    noAsistio: safeGetElement('countNoAsistio')
  };
  
  Object.entries(elementMap).forEach(([key, counterElement]) => {
    if (counterElement) {
      if (summaryIntervalsWeakMap.has(counterElement)) {
        clearInterval(summaryIntervalsWeakMap.get(counterElement));
      }
      const targetValue = countsSummary[key] ?? 0;
      const currentValue = parseInt(counterElement.textContent, 10) || 0;
      
      if (currentValue !== targetValue) {
        let stepValue = currentValue;
        const stepIncrement = targetValue > currentValue ? 1 : -1;
        const intervalTimer = setInterval(() => {
          stepValue += stepIncrement;
          counterElement.textContent = stepValue;
          if ((stepIncrement > 0 && stepValue >= targetValue) || (stepIncrement < 0 && stepValue <= targetValue)) {
            counterElement.textContent = targetValue;
            clearInterval(intervalTimer);
            summaryIntervalsWeakMap.delete(counterElement);
          }
        }, 30);
        summaryIntervalsWeakMap.set(counterElement, intervalTimer);
      }
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const setupExportButtonListener = () => {
  const exportBtn = safeGetElement('btnExport');
  if (!exportBtn) return;
  
  exportBtn.addEventListener('click', () => {
    exportFinalizedAppointmentsToCsv(exportBtn);
  });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupMobileMenu = () => {
  // El menú móvil es gestionado centralizadamente por ~/js/shared/sidebar.js
};

const initializeCitasFinalizadasModule = () => {
    setupMobileMenu();
    setupExportButtonListener();
    
    const appointmentsList = finalizedStorage.load();
    renderAppointmentsTable(appointmentsList);
    updateSummaryCounters();
    
    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeCitasFinalizadasModule);