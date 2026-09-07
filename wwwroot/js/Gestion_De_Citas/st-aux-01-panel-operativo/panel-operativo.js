/* ============================================
SmileTrack — Panel Operativo (st-aux-01-panel-operativo)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Maneja el comportamiento interactivo del panel de auxiliar: renderizado de KPIs, visualización detallada del expediente del paciente en ventana modal y aviso de disponibilidad del resumen operativo.

FUNCIONALIDADES PRINCIPALES:
- Renderizado interactivo de métricas (KPIs), alerta de próxima cita y barra de progreso
- Event delegation para la apertura de modales de paciente con visualización de antecedentes médicos y alergias
- Gestión de modales accesibles (focus trap, Escape key y bloqueo de scroll)
- Aviso explícito cuando el reporte PDF no está disponible en el servidor

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.css
- JS: ~/js/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.js
- Partial / Otros: panel-operativo.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- PanelController consume exclusivamente datos serializados por el servidor.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación al usuario sin entorpecer el flujo de trabajo

// WHY: modalManager centraliza foco, teclado y estado ARIA del diálogo.
const modalManager = {
  opener: null,
  previousOverflow: '',
  keydownHandler: null,

  getFocusable: (modal) => Array.from(modal.querySelectorAll(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
  )),

  open: (modalId, opener = document.activeElement) => {
    const modal = safeGetElement(modalId);
    if (!modal) return;

    modalManager.opener = opener instanceof HTMLElement ? opener : null;
    modalManager.previousOverflow = document.body.style.overflow;
    modal.classList.add('active');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');

    const focusable = modalManager.getFocusable(modal);
    if (focusable[0]) focusable[0].focus();

    modalManager.keydownHandler = (event) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        modalManager.close(modalId);
        return;
      }

      if (event.key !== 'Tab') return;
      const elements = modalManager.getFocusable(modal);
      if (!elements.length) return;

      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', modalManager.keydownHandler);
    document.body.style.overflow = 'hidden';
  },

  close: (modalId) => {
    const modal = safeGetElement(modalId);
    if (!modal) return;

    modal.classList.remove('active');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');

    if (modalManager.keydownHandler) {
      document.removeEventListener('keydown', modalManager.keydownHandler);
      modalManager.keydownHandler = null;
    }
    document.body.style.overflow = modalManager.previousOverflow;
    if (modalManager.opener && document.contains(modalManager.opener)) {
      modalManager.opener.focus();
    }
    modalManager.opener = null;
  }
};

// ═══════════════════════════════════════════════════════════════════
// WHY: Clase que encapsula el acceso a datos para desacoplar la lógica de presentación de la capa de API
// ═══════════════════════════════════════════════════════════════════
// Controlador de datos: envuelve los datos reales inyectados por el servidor
// (window.smiletrackPanelData, generado por IPanelOperativoService)
// en vez de simular pacientes/alertas de ejemplo.
class PanelController {
  constructor() {
    const data = window.smiletrackPanelData || {};
    this._fechaHoy = data.fechaHoy || '';
    this._proximaCita = data.proximaCita || null;
    this._kpis = data.kpis || { citasHoy: 0, completadas: 0, pendientes: 0, consultoriosDisponibles: 0 };
    this._pacientes = data.citas || [];
    this._alertas = data.alertas || [];
  }

  // Devuelve resumen del panel con fecha, próxima cita y KPIs
  async getResumen() {
    return { fechaHoy: this._fechaHoy, proximaCita: this._proximaCita ? { ...this._proximaCita } : null, kpis: { ...this._kpis } };
  }

  // Devuelve lista de citas para la tabla
  async getCitas() {
    return this._pacientes.map(p => ({
      id: p.id, hora: p.hora, paciente: p.paciente, profesional: p.profesional,
      alergia: p.alergia, consultorio: p.consultorio, estado: p.estado, highlight: p.highlight,
    }));
  }

  // Busca paciente por ID para mostrar en modal
  async getPaciente(id) {
    return this._pacientes.find(p => p.id === id) || null;
  }

  // Devuelve alertas del día para el panel lateral
  async getAlertas() {
    return [...this._alertas];
  }

  // Calcula progreso de citas completadas vs total
  async getProgreso() {
    const { completadas, citasHoy } = this._kpis;
    return { completadas, total: citasHoy, porcentaje: citasHoy > 0 ? Math.round((completadas / citasHoy) * 100) : 0 };
  }

  // No existe un endpoint real de generación de PDF todavía.
  async descargarResumen() {
    return { ok: false, nombre: null };
  }
}

// Instancia única del controlador para toda la aplicación
const panelCtrl = new PanelController();

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Header y alerta próxima cita
// ═══════════════════════════════════════════════════════════════════
const renderHeader = (resumen) => {
  const meta = safeGetElement('pageMeta');
  if (meta) meta.textContent = `Resumen del día · ${resumen.fechaHoy}`;

  const summary = safeGetElement('pageSummary');
  const pc = resumen.proximaCita;
  const titulo = safeGetElement('apTitulo');
  const detalle = safeGetElement('apDetalle');

  const resumenTexto = resumen.kpis
    ? `${resumen.kpis.citasHoy ?? 0} citas · ${(resumen.kpis.pendientes ?? 0)} pendientes · ${(resumen.kpis.completadas ?? 0)} completadas`
    : 'Sin información del día';

  if (summary) summary.textContent = resumenTexto;

  if (!pc) {
    if (titulo) titulo.textContent = 'Sin próximas citas pendientes hoy';
    if (detalle) detalle.textContent = 'El flujo operativo está tranquilo por el momento.';
    return;
  }

  if (titulo) titulo.textContent = `Próxima cita en ${pc.minutosRestantes} minutos`;
  if (detalle) detalle.textContent = `${pc.hora} · ${pc.paciente} · ${pc.tipo} · ${pc.profesional} · ${pc.consultorio}`;
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: KPI cards
// ═══════════════════════════════════════════════════════════════════
const renderKPIs = (kpis) => {
  const grid = safeGetElement('kpiGrid');
  if (!grid) return;

  const cards = [
    { num: kpis.citasHoy, label: 'Citas hoy', color: 'purple' },
    { num: kpis.completadas, label: 'Completadas', color: 'green' },
    { num: kpis.pendientes, label: 'Pendientes', color: 'orange' },
    { num: kpis.consultoriosDisponibles, label: 'Consultorios activos/disponibles', color: 'blue' },
  ];

  grid.replaceChildren(...cards.map(c => {
    const card = document.createElement('div');
    card.className = 'kpi-card';
    card.dataset.color = c.color;
    const value = document.createElement('span');
    value.className = 'kpi-value';
    value.textContent = String(c.num);
    const label = document.createElement('span');
    label.className = 'kpi-label';
    label.textContent = c.label;
    card.append(value, label);
    return card;
  }));
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Barra de progreso con accesibilidad ARIA
// ═══════════════════════════════════════════════════════════════════
const renderProgreso = (progreso) => {
  const barra = safeGetElement('progresoBarra');
  const label = safeGetElement('progresoLabel');
  const progressEl = barra?.closest('[role="progressbar"]');
  
  if (barra) barra.style.width = `${progreso.porcentaje}%`;
  if (label) label.textContent = `${progreso.completadas} de ${progreso.total} citas completadas hoy`;
  if (progressEl) {
    progressEl.setAttribute('aria-valuenow', progreso.porcentaje);
    progressEl.setAttribute('aria-valuetext', `${progreso.porcentaje}% completado`);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Tabla de citas con atributos ARIA
// ═══════════════════════════════════════════════════════════════════
const renderCitas = (citas) => {
  const tbody = safeGetElement('citasBody');
  if (!tbody) return;

  if (!citas.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 7;
    cell.className = 'empty-state-cell';

    const empty = document.createElement('div');
    empty.className = 'empty-state';

    const icon = document.createElement('span');
    icon.className = 'material-symbols-outlined';
    icon.setAttribute('aria-hidden', 'true');
    icon.textContent = 'event_busy';

    const label = document.createElement('span');
    label.textContent = 'No hay citas programadas para hoy.';

    empty.append(icon, label);
    cell.appendChild(empty);
    row.appendChild(cell);
    tbody.replaceChildren(row);
    return;
  }

  tbody.replaceChildren(...citas.map(c => {
    const row = document.createElement('tr');
    row.className = c.highlight ? 'row-highlight' : '';
    row.setAttribute('role', 'row');
    const cell = (className, text) => {
      const element = document.createElement('td');
      element.className = className;
      element.textContent = text;
      return element;
    };
    row.append(cell('col-hora', c.hora), cell('col-paciente', c.paciente), cell('', c.profesional));

    const allergyCell = document.createElement('td');
    allergyCell.className = 'col-alergia';
    const allergy = document.createElement('span');
    allergy.setAttribute('aria-label', c.alergia ? `Alergia: ${c.alergia}` : 'Sin alergias');
    if (c.alergia) {
      allergy.className = 'badge-alergia';
      allergy.textContent = `🚨 ${c.alergia}`;
    } else {
      allergy.className = 'sin-alergia';
      allergy.textContent = '—';
    }
    allergyCell.appendChild(allergy);

    const statusCell = document.createElement('td');
    statusCell.className = 'col-estado';
    const status = document.createElement('span');
    status.className = `badge-estado ${c.estado === 'Atendida' ? 'badge-atendida' : c.estado === 'Pendiente' ? 'badge-pendiente' : 'badge-cancelada'}`;
    status.setAttribute('role', 'status');
    status.setAttribute('aria-label', `Estado: ${c.estado}`);
    status.textContent = `● ${c.estado}`;
    statusCell.appendChild(status);

    const actionsCell = document.createElement('td');
    const button = document.createElement('button');
    button.className = 'btn-icon action-btn btn-view';
    button.title = 'Ver paciente';
    button.dataset.action = 'view';
    button.dataset.id = String(c.id);
    button.setAttribute('aria-label', `Ver detalles de ${c.paciente}`);
    button.appendChild(document.createTextNode('👁️ '));
    const buttonText = document.createElement('span');
    buttonText.className = 'btn-text';
    buttonText.textContent = 'Ver';
    button.appendChild(buttonText);
    actionsCell.appendChild(button);

    row.append(allergyCell, cell('', c.consultorio), statusCell, actionsCell);
    return row;
  }));
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Alertas del día con role list
// ═══════════════════════════════════════════════════════════════════
const renderAlertas = (alertas) => {
  const list = safeGetElement('alertasList');
  if (!list) return;

  if (!alertas.length) {
    const empty = document.createElement('div');
    empty.className = 'alerta-item empty-alerta';
    empty.setAttribute('role', 'listitem');

    const icon = document.createElement('div');
    icon.className = 'alerta-icon info';
    icon.setAttribute('aria-hidden', 'true');
    icon.textContent = 'ℹ️';

    const content = document.createElement('div');
    content.className = 'alerta-content';

    const title = document.createElement('p');
    title.className = 'alerta-title';
    title.textContent = 'Sin alertas del día';

    const desc = document.createElement('p');
    desc.className = 'alerta-desc';
    desc.textContent = 'El turno clínico está estable hasta el momento.';

    content.append(title, desc);
    empty.append(icon, content);
    list.replaceChildren(empty);
    return;
  }

  list.replaceChildren(...alertas.map(a => {
    const item = document.createElement('div');
    item.className = 'alerta-item';
    item.setAttribute('role', 'listitem');
    const icon = document.createElement('div');
    icon.className = `alerta-icon ${a.tipo === 'warning' ? 'warning' : 'info'}`;
    icon.setAttribute('aria-hidden', 'true');
    icon.textContent = a.tipo === 'warning' ? '⚠️' : 'ℹ️';
    const content = document.createElement('div');
    content.className = 'alerta-content';
    const title = document.createElement('p');
    title.className = 'alerta-title';
    title.textContent = a.titulo;
    const description = document.createElement('p');
    description.className = 'alerta-desc';
    description.textContent = a.desc;
    content.append(title, description);
    item.append(icon, content);
    return item;
  }));
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL: Ver paciente con datos completos
// ═══════════════════════════════════════════════════════════════════
const verPaciente = async (id, opener) => {
  const p = await panelCtrl.getPaciente(id);
  if (!p) return;

  const initials = p.paciente.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();

  // WHY: Mapeo dinámico de datos del paciente hacia nodos del DOM para evitar repetición de código y errores manuales
  const fields = {
    modalAvatar: initials,
    modalName: p.paciente,
    modalRole: `Paciente · ID #${String(p.id).padStart(3, '0')}`,
    modalTelefono: p.telefono,
    modalEmail: p.email,
    modalSangre: p.sangre,
    modalEdad: p.edad,
    modalServicio: p.servicio,
    modalProfesional: p.profesional,
    modalConsultorio: p.consultorio,
    modalHora: p.hora,
    modalAntecedentes: p.antecedentes
  };

  Object.entries(fields).forEach(([id, value]) => {
    const el = safeGetElement(id);
    if (el) el.textContent = value;
  });

  // WHY: Destaca en color rojo las alergias críticas para mitigar riesgos en la atención clínica
  const alergiaEl = safeGetElement('modalAlergias');
  if (alergiaEl) {
    if (p.alergia) {
      alergiaEl.textContent = `🚨 ${p.alergia}`;
      alergiaEl.className = 'modal-alergia-tag';
      alergiaEl.setAttribute('aria-label', `Alergia: ${p.alergia}`);
    } else {
      alergiaEl.textContent = '✓ Sin alergias registradas';
      alergiaEl.className = 'modal-alergia-tag sin-alergia';
      alergiaEl.setAttribute('aria-label', 'Sin alergias registradas');
    }
  }

  // WHY: Muestra medicamentos como etiquetas visuales dinámicas para lectura rápida
  const mediEl = safeGetElement('modalMedicamentos');
  if (mediEl) {
    if (p.medicamentos?.length) {
      mediEl.replaceChildren(...p.medicamentos.map(m => {
        const tag = document.createElement('span');
        tag.className = 'modal-medi-tag';
        tag.setAttribute('role', 'listitem');
        tag.textContent = m;
        return tag;
      }));
      mediEl.setAttribute('role', 'list');
    } else {
      const tag = document.createElement('span');
      tag.className = 'modal-medi-tag sin-medicamentos';
      tag.textContent = p.medicamentosDisponibles
        ? 'Sin medicamentos actuales'
        : 'Información de medicamentos no registrada';
      mediEl.replaceChildren(tag);
      mediEl.removeAttribute('role');
    }
  }

  // Abre modal con gestión de accesibilidad
  modalManager.open('modalBackdrop', opener);
};

// ═══════════════════════════════════════════════════════════════════
//  EVENTOS: Manejo de acciones en tabla y modal
// ═══════════════════════════════════════════════════════════════════
const initTableActions = () => {
  const tbody = safeGetElement('citasBody');
  if (!tbody) return;

  // WHY: Event delegation permite capturar clics en filas agregadas dinámicamente de forma eficiente
  tbody.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-action]');
    if (!btn) return;

    const action = btn.dataset.action;
    const id = btn.dataset.id;

    if (action === 'view' && id) {
      verPaciente(parseInt(id, 10), btn);
    }
  });

  // WHY: Permite disparar la acción mediante teclado para usuarios de tecnologías de asistencia
  tbody.addEventListener('keydown', (e) => {
    if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('[data-action]')) {
      e.preventDefault();
      e.target.click();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  EVENTOS: Manejo de modales con teclado y cierre
// ═══════════════════════════════════════════════════════════════════
const initModalHandlers = () => {
  const modal = safeGetElement('modalBackdrop');
  const closeBtn = safeGetElement('modalCloseBtn');
  const closeBtn2 = safeGetElement('modalCloseBtn2');
  const verHistoriaBtn = safeGetElement('modalVerHistoria');

  // Cerrar modal con botón X
  if (closeBtn) closeBtn.addEventListener('click', () => modalManager.close('modalBackdrop'));
  if (closeBtn2) closeBtn2.addEventListener('click', () => modalManager.close('modalBackdrop'));
  
  // Cerrar modal al hacer click en overlay
  if (modal) {
    modal.addEventListener('click', (e) => {
      if (e.target === modal) modalManager.close('modalBackdrop');
    });
  }
  
  // La ruta existente recibe el paciente por query string.
  if (verHistoriaBtn) {
    verHistoriaBtn.addEventListener('click', () => {
      const patientId = modalManager.opener?.dataset.id;
      if (!patientId) {
        window.ToastService.warning('No se pudo identificar el paciente');
        return;
      }
      window.location.href = `/gestion-de-citas/st-aux-05-historial-parcial?pacienteId=${encodeURIComponent(patientId)}`;
    });
  }
};

// ═══════════════════════════════════════════════════════════════════
//  EVENTOS: Descargar resumen con feedback visual
// ═══════════════════════════════════════════════════════════════════
const initDescargarResumen = () => {
  const btn = safeGetElement('btnDescargar');
  if (!btn) return;

  btn.addEventListener('click', async () => {
    try {
      const res = await panelCtrl.descargarResumen();
      if (!res.ok) {
        window.ToastService.info('La descarga del resumen aún no está disponible en el servidor');
        return;
      }
      window.ToastService.success(`Resumen generado: ${res.nombre}`);
    } catch {
      window.ToastService.error('Error al generar resumen');
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  INIT: Función principal de inicialización
// ═══════════════════════════════════════════════════════════════════
const init = async () => {
  try {
    // Inicializar componentes de UI
    initTableActions();
    initModalHandlers();
    initDescargarResumen();

    // Cargar y renderizar datos del panel
    const [resumen, citas, alertas, progreso] = await Promise.all([
      panelCtrl.getResumen(),
      panelCtrl.getCitas(),
      panelCtrl.getAlertas(),
      panelCtrl.getProgreso(),
    ]);

    renderHeader(resumen);
    renderKPIs(resumen.kpis);
    renderProgreso(progreso);
    renderCitas(citas);
    renderAlertas(alertas);
    
  } catch (e) {
    console.error('[SmileTrack] Error inicializando modulo', e);
    mostrarErrorUsuario(e.message || 'Error cargando módulo. Intente recargar.');
  }
};

function mostrarErrorUsuario(mensaje) {
  let div = document.getElementById('smiletrack-error-bar');
  if (!div) {
    div = document.createElement('div');
    div.id = 'smiletrack-error-bar';
    div.style.cssText = 'position:fixed;top:0;left:0;right:0;z-index:99999;background:#dc2626;color:white;padding:14px 20px;text-align:center;font-family:system-ui,-apple-system,sans-serif;font-size:15px;box-shadow:0 4px 12px rgba(0,0,0,.15);border-bottom:3px solid #991b1b;';
    div.setAttribute('role', 'alert');
    document.body.appendChild(div);
  }
  div.replaceChildren();
  const message = document.createElement('span');
  message.textContent = `[SmileTrack] ${mensaje}`;
  const close = document.createElement('button');
  close.type = 'button';
  close.textContent = '×';
  close.setAttribute('aria-label', 'Cerrar mensaje de error');
  close.addEventListener('click', () => { div.style.display = 'none'; });
  div.append(message, close);
  div.style.display = 'block';
}

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);