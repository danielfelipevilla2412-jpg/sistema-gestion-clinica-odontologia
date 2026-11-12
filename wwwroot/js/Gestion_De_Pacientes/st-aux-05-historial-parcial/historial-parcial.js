/* ============================================
 * SmileTrack — Módulo: Gestión de Pacientes
 * Componente: Historial del Paciente (st-aux-05-historial-parcial)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Pacientes/st-aux-05-historial-parcial/historial-parcial.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Visualización del resumen de citas anteriores y observaciones clínicas del paciente en atención.
 * Proporciona lectura rápida de alergias, medicamentos actuales y notas de evoluciones pasadas al auxiliar.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Datos alimentados de forma segura mediante SSR en objeto `smiletrackHistorialParcialData`.
 * - Alertas visuales destacadas para pacientes con condiciones médicas especiales o alergias.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux05HistorialParcial
 * - HTML: Views/Gestion_De_Pacientes/st-aux-05-historial-parcial/historial-parcial.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const DEFAULT_MAX_RECORD_LIMIT = 3;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

class PartialHistoryController {
  constructor() {
    const serverData = window.smiletrackHistorialParcialData || {};
    this._paciente = serverData.paciente || { id: null, nombre: 'Sin paciente asignado', tipoDoc: '', documento: '', alergias: [], medicamentos: [], grupoSanguineo: 'N/D', ultimaActualizacion: '' };
    this._limite = serverData.limite || DEFAULT_MAX_RECORD_LIMIT;
    this._historial = (serverData.consultas || []).map(item => ({
      ...item,
      fecha: item.fecha ? window.CommonUtils.formatFechaLocal(item.fecha) : ''
    }));
    this._tratamientos = serverData.tratamientos || [];
    this._documentos = serverData.documentos || [];
  }

  async getPaciente() {
    return { ...this._paciente };
  }

  async getAlertas() {
    return {
      alergias: [...this._paciente.alergias],
      medicamentos: [...this._paciente.medicamentos],
      grupoSanguineo: this._paciente.grupoSanguineo,
      antecedentesMedicos: this._paciente.antecedentesMedicos || '',
    };
  }

  async getConsultas() {
    return this._historial.slice(0, this._limite);
  }

  getMetaString() {
    const p = this._paciente;
    if (!p.id) return 'Selecciona un paciente para consultar su información clínica.';
    const cantidad = this._historial.length;
    const etiquetaCitas = cantidad === 1 ? '1 cita atendida reciente' : `${cantidad} citas atendidas recientes`;
    return `${p.nombre} · ${p.tipoDoc} ${p.documento} · ${etiquetaCitas} · Acceso parcial · Actualizado: ${p.ultimaActualizacion || 'Sin actividad'}`;
  }
}

const partialHistoryControllerInstance = new PartialHistoryController();

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = window.CommonUtils.safeGetElement;

const updateElementWithAriaLabel = (elementId, valueText) => {
  const element = safeGetElement(elementId);
  if (element) {
    element.textContent = valueText || '—';
    const labelPrefix = element.previousElementSibling?.textContent?.trim() || 'Valor';
    element.setAttribute('aria-label', `${labelPrefix}: ${valueText || 'No disponible'}`);
  }
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

// Los métodos de acceso a datos están encapsulados en partialHistoryControllerInstance

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const renderMedicalAlertCard = (alertsData) => {
  updateElementWithAriaLabel('alergia', alertsData.alergias.join(', ') || 'Ninguna conocida');
  updateElementWithAriaLabel('medicamentos', alertsData.medicamentos.join(', ') || 'Ninguno');
  updateElementWithAriaLabel('grupoSang', alertsData.grupoSanguineo || '—');
  updateElementWithAriaLabel('antecedentesMedicos', alertsData.antecedentesMedicos || 'Sin antecedentes registrados');

  const cardElement = safeGetElement('alertaMedica');
  if (cardElement) {
    const hasAllergies = alertsData.alergias.length > 0;
    cardElement.setAttribute('role', hasAllergies ? 'alert' : 'status');
    cardElement.setAttribute('aria-live', hasAllergies ? 'assertive' : 'polite');

    if (!hasAllergies && !alertsData.medicamentos.length) {
      cardElement.style.opacity = '.6';
      cardElement.setAttribute('aria-label', 'Sin alertas médicas registradas para este paciente');
    } else {
      cardElement.removeAttribute('aria-label');
    }
  }
};

const renderHistoryTable = (rowsList) => {
  const tableBody = safeGetElement('histBody');
  const footerElement = safeGetElement('tableFooter');
  if (!tableBody) return;

  if (!rowsList.length) {
    const emptyRow = document.createElement('tr');
    const emptyCell = document.createElement('td');
    emptyCell.colSpan = 4;
    emptyCell.className = 'history-empty';
    emptyCell.textContent = partialHistoryControllerInstance._paciente.id
      ? 'Este paciente no tiene citas atendidas registradas.'
      : 'Selecciona un paciente para consultar su historial clínico.';
    emptyRow.appendChild(emptyCell);
    tableBody.replaceChildren(emptyRow);
    if (footerElement) {
      footerElement.hidden = true;
      footerElement.style.display = 'none';
    }
    return;
  }

  const createTableCell = (classNameText, labelText, valueText) => {
    const cell = document.createElement('td');
    cell.className = classNameText;
    cell.dataset.label = labelText;
    cell.textContent = valueText || 'Sin información';
    return cell;
  };

  const formatControlInstructions = (raw) => {
    if (!raw) return '';
    try {
      const instructions = JSON.parse(raw);
      if (Array.isArray(instructions)) {
        return instructions.map(item => `${item.text || ''}: ${item.checked ? 'Sí' : 'No'}`).filter(Boolean).join(', ');
      }
    } catch {
      return raw;
    }
    return raw;
  };

  tableBody.replaceChildren(...rowsList.flatMap(row => {
    const tableRow = document.createElement('tr');
    tableRow.append(
      createTableCell('td-fecha', 'Fecha', row.fecha),
      createTableCell('td-profesional', 'Profesional', row.profesional),
      createTableCell('', 'Diagnóstico', row.diagnostico),
      createTableCell('', 'Procedimiento', row.procedimiento)
    );

    const detailEntries = [
      ['Motivo de consulta', row.motivoConsulta],
      ['Enfermedad actual', row.enfermedadActual],
      ['Antecedentes médicos', row.antecedentesMedicos],
      ['Síntomas y signos', Array.isArray(row.sintomas) ? row.sintomas.join(', ') : ''],
      ['Hábitos', row.habitos],
      ['Alergias', row.alergias],
      ['Hallazgos', row.hallazgos],
      ['Observaciones odontológicas', row.odontogramaObservaciones],
      ['Exámenes complementarios', row.examenesComplementarios],
      ['Evolución clínica', row.evolucionClinica],
      ['Prescripción', row.prescripcion],
      ['Odontograma', Array.isArray(row.odontograma) ? row.odontograma.map(item => `${item.pieza} ${item.nombrePieza}: ${item.estado}${item.observacion ? ` (${item.observacion})` : ''}`).join('; ') : ''],
      ['Control postoperatorio', row.controlPostoperatorio
        ? [row.controlPostoperatorio.estado, row.controlPostoperatorio.observaciones, formatControlInstructions(row.controlPostoperatorio.instruccionesJson)].filter(Boolean).join(' · ')
        : ''],
    ].filter(([, value]) => typeof value === 'string' && value.trim());

    if (!detailEntries.length) return [tableRow];

    const detailRow = document.createElement('tr');
    detailRow.className = 'history-details-row';
    const detailCell = document.createElement('td');
    detailCell.colSpan = 4;
    const details = document.createElement('dl');
    details.className = 'history-details';
    detailEntries.forEach(([label, value]) => {
      const item = document.createElement('div');
      const term = document.createElement('dt');
      const description = document.createElement('dd');
      term.textContent = label;
      description.textContent = value;
      item.append(term, description);
      details.appendChild(item);
    });
    detailCell.appendChild(details);
    detailRow.appendChild(detailCell);
    return [tableRow, detailRow];
  }));

  if (footerElement) {
    footerElement.textContent = `Mostrando ${rowsList.length} de hasta ${DEFAULT_MAX_RECORD_LIMIT} atenciones recientes.`;
    footerElement.hidden = false;
    footerElement.style.display = 'block';
  }
};

const renderRelatedRecords = (elementId, items, type) => {
  const list = safeGetElement(elementId);
  if (!list) return;
  list.replaceChildren();
  if (!items.length) {
    const empty = document.createElement('li');
    empty.textContent = 'Sin registros disponibles.';
    list.appendChild(empty);
    return;
  }

  items.forEach((item) => {
    const entry = document.createElement('li');
    const heading = document.createElement('strong');
    const detail = document.createElement('span');
    if (type === 'treatment') {
      heading.textContent = [item.nombre, item.pieza ? `Pieza ${item.pieza}` : ''].filter(Boolean).join(' · ');
      detail.textContent = [item.estado, item.observacion, item.sesionesEstimadas ? `${item.sesionesEstimadas} sesiones estimadas` : ''].filter(Boolean).join(' · ');
      entry.append(heading, detail);
    } else {
      heading.textContent = [item.tipo, item.nombre].filter(Boolean).join(' · ');
      detail.textContent = [item.fecha, item.observacion].filter(Boolean).join(' · ');
      const link = document.createElement('a');
      link.href = item.url;
      link.textContent = 'Descargar documento';
      link.className = 'related-record-link';
      entry.append(heading, detail, link);
    }
    list.appendChild(entry);
  });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

// Módulo sin modales ni formularios interactivos directos

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupMobileNavigationMenu = () => {
  // El menú móvil es gestionado centralizadamente por ~/js/shared/sidebar.js
};

const initializeHistorialParcialModule = async () => {
    setupMobileNavigationMenu();

    const [alertsData, consultationsList] = await Promise.all([
      partialHistoryControllerInstance.getAlertas(),
      partialHistoryControllerInstance.getConsultas(),
    ]);

    const metaElement = safeGetElement('patientMeta');
    if (metaElement) {
      metaElement.textContent = partialHistoryControllerInstance.getMetaString();
      metaElement.setAttribute('aria-label', `Información del paciente: ${partialHistoryControllerInstance.getMetaString()}`);
    }

    renderMedicalAlertCard(alertsData);
    renderHistoryTable(consultationsList);
    renderRelatedRecords('tratamientosBody', partialHistoryControllerInstance._tratamientos, 'treatment');
    renderRelatedRecords('documentosBody', partialHistoryControllerInstance._documentos, 'document');

    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeHistorialParcialModule);