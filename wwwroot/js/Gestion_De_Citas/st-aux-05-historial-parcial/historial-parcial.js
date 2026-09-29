/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Historial Parcial de Citas (st-aux-05-historial-parcial)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.js
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
 * - HTML: Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml
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
  }

  async getPaciente() {
    return { ...this._paciente };
  }

  async getAlertas() {
    return {
      alergias: [...this._paciente.alergias],
      medicamentos: [...this._paciente.medicamentos],
      grupoSanguineo: this._paciente.grupoSanguineo,
    };
  }

  async getConsultas() {
    return this._historial.slice(0, this._limite);
  }

  getMetaString() {
    const p = this._paciente;
    return `${p.nombre} · ${p.tipoDoc} ${p.documento} · Últimas ${this._limite} consultas · Acceso parcial · Actualizado: ${p.ultimaActualizacion}`;
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
    emptyCell.style.cssText = 'text-align:center;padding:26px;color:var(--text-muted);font-size:.85rem;';
    emptyCell.textContent = 'Sin consultas registradas.';
    emptyRow.appendChild(emptyCell);
    tableBody.replaceChildren(emptyRow);
    if (footerElement) footerElement.style.display = 'none';
    return;
  }

  const createTableCell = (classNameText, labelText, valueText) => {
    const cell = document.createElement('td');
    cell.className = classNameText;
    cell.dataset.label = labelText;
    cell.textContent = valueText || 'Sin información';
    return cell;
  };

  tableBody.replaceChildren(...rowsList.map(row => {
    const tableRow = document.createElement('tr');
    tableRow.append(
      createTableCell('td-fecha', 'Fecha', row.fecha),
      createTableCell('td-profesional', 'Profesional', row.profesional),
      createTableCell('', 'Diagnóstico', row.diagnostico),
      createTableCell('', 'Procedimiento', row.procedimiento)
    );
    return tableRow;
  }));
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

    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeHistorialParcialModule);