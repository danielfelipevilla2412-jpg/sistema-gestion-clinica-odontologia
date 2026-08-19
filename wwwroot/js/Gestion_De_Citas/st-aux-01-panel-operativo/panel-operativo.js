/* ============================================
SmileTrack — Panel Operativo (st-aux-01-panel-operativo)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Maneja el comportamiento interactivo del panel de auxiliar: renderizado de KPIs, visualización detallada del expediente del paciente en ventana modal y descarga simulada del resumen operativo.

FUNCIONALIDADES PRINCIPALES:
- Renderizado interactivo de métricas (KPIs), alerta de próxima cita y barra de progreso de asistencia
- Event delegation para la apertura de modales de paciente con visualización de antecedentes médicos y alergias
- Gestión de modales accesibles (focus trap, Escape key y bloqueo de scroll)
- Descarga asíncrona simulada de reporte operativo en formato PDF con retroalimentación visual

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.css
- JS: ~/js/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.js
- Partial / Otros: panel-operativo.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- La clase PanelController mockea los datos de pacientes y alertas del día con fines ilustrativos.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Debounce evita saturar la API con peticiones redundantes ante cambios veloces del usuario
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación al usuario sin entorpecer el flujo de trabajo

// WHY: modalManager centraliza la lógica de visualización de diálogos garantizando que se cumplan criterios de accesibilidad WCAG
const modalManager = {
  open: (modalId) => {
    const modal = safeGetElement(modalId);
    if (!modal) return;
    
    modal.classList.add('active');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    
    // WHY: El traslado del foco evita que la navegación por teclado se quede atrapada detrás del diálogo modal
    const focusable = modal.querySelector('button, [href], input, select, textarea');
    if (focusable) focusable.focus();
    
    // WHY: Bloquear el scroll previene la navegación accidental del contenido de fondo (scrollbar bleeding)
    document.body.style.overflow = 'hidden';
  },
  
  close: (modalId) => {
    const modal = safeGetElement(modalId);
    if (!modal) return;
    
    modal.classList.remove('active');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    
    // WHY: Devuelve el scroll al body al salir del diálogo
    document.body.style.overflow = '';
  }
};

// ═══════════════════════════════════════════════════════════════════
// WHY: Clase que encapsula el acceso a datos para desacoplar la lógica de presentación de la capa de API
// ═══════════════════════════════════════════════════════════════════
// Controlador de datos: envuelve los datos reales inyectados por el servidor
// (window.smiletrackPanelData, ver ConstruirPanelOperativoAsync en GestionCitasController.cs)
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

  // NOTA: no existe un endpoint real de generación de PDF todavía; se informa
  // honestamente en lugar de simular una descarga exitosa (ver initDescargarResumen).
  async descargarResumen() {
    return { ok: false, nombre: null };
  }
}

// Instancia única del controlador para toda la aplicación
const panelCtrl = new PanelController();

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL CON GESTIÓN DE FOCO Y ARIA
// ═══════════════════════════════════════════════════════════════════
const initMobileMenu = () => {
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  const hamburger = safeGetElement('hamburger');

  if (!sidebar || !overlay || !hamburger) return;

  const toggleMenu = (show) => {
    if (show) {
      sidebar.classList.add('open');
      overlay.classList.add('open');
      hamburger.setAttribute('aria-expanded', 'true');
      overlay.setAttribute('aria-hidden', 'false');
      
      // Enfocar primer enlace de navegación para accesibilidad
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      sidebar.classList.remove('open');
      overlay.classList.remove('open');
      hamburger.setAttribute('aria-expanded', 'false');
      overlay.setAttribute('aria-hidden', 'true');
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  // Cerrar menú al navegar en móvil
  sidebar.querySelectorAll('.nav-item').forEach(link => {
    link.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  // Cerrar menú con tecla Escape
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Header y alerta próxima cita
// ═══════════════════════════════════════════════════════════════════
const renderHeader = (resumen) => {
  const meta = safeGetElement('pageMeta');
  if (meta) meta.textContent = `Resumen en tiempo real · ${resumen.fechaHoy}`;

  const alertBar = safeGetElement('apTitulo')?.closest('.alert-bar, [role="status"]');
  const pc = resumen.proximaCita;
  const titulo = safeGetElement('apTitulo');
  const detalle = safeGetElement('apDetalle');

  if (!pc) {
    if (titulo) titulo.textContent = 'Sin próximas citas pendientes hoy';
    if (detalle) detalle.textContent = '';
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
    { num: kpis.consultoriosDisponibles, label: 'Consultorios disponibles', color: 'blue' },
  ];

  grid.innerHTML = cards.map(c => `
    <div class="kpi-card" data-color="${c.color}">
      <span class="kpi-value">${c.num}</span>
      <span class="kpi-label">${c.label}</span>
    </div>
  `).join('');
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

  tbody.innerHTML = citas.map(c => {
    const badgeEstado = c.estado === 'Atendida'
      ? `<span class="badge-estado badge-atendida" role="status" aria-label="Estado: Atendida">● ${c.estado}</span>`
      : c.estado === 'Pendiente'
      ? `<span class="badge-estado badge-pendiente" role="status" aria-label="Estado: Pendiente">● ${c.estado}</span>`
      : `<span class="badge-estado badge-cancelada" role="status" aria-label="Estado: Cancelada">● ${c.estado}</span>`;

    const badgeAlergia = c.alergia
      ? `<span class="badge-alergia" aria-label="Alergia: ${c.alergia}">🚨 ${c.alergia}</span>`
      : `<span style="color:var(--text-muted)" aria-label="Sin alergias">—</span>`;

    return `
      <tr class="${c.highlight ? 'row-highlight' : ''}" role="row">
        <td class="col-hora">${c.hora}</td>
        <td class="col-paciente">${c.paciente}</td>
        <td>${c.profesional}</td>
        <td class="col-alergia">${badgeAlergia}</td>
        <td>${c.consultorio}</td>
        <td class="col-estado">${badgeEstado}</td>
        <td>
          <button class="btn-icon action-btn btn-view" title="Ver paciente" data-action="view" data-id="${c.id}" aria-label="Ver detalles de ${c.paciente}">
            👁️ <span class="btn-text">Ver</span>
          </button>
        </td>
      </tr>
    `;
  }).join('');
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Alertas del día con role list
// ═══════════════════════════════════════════════════════════════════
const renderAlertas = (alertas) => {
  const list = safeGetElement('alertasList');
  if (!list) return;

  list.innerHTML = alertas.map(a => {
    const icoClass = a.tipo === 'warning' ? 'warning' : 'info';
    const icoEmoji = a.tipo === 'warning' ? '⚠️' : 'ℹ️';
    return `
      <div class="alerta-item" role="listitem">
        <div class="alerta-icon ${icoClass}" aria-hidden="true">${icoEmoji}</div>
        <div class="alerta-content">
          <p class="alerta-title">${a.titulo}</p>
          <p class="alerta-desc">${a.desc}</p>
        </div>
      </div>
    `;
  }).join('');
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL: Ver paciente con datos completos
// ═══════════════════════════════════════════════════════════════════
const verPaciente = async (id) => {
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
      mediEl.innerHTML = p.medicamentos.map(m => 
        `<span class="modal-medi-tag" role="listitem">${m}</span>`
      ).join('');
      mediEl.setAttribute('role', 'list');
    } else {
      mediEl.innerHTML = '<span class="modal-medi-tag sin-medicamentos">Sin medicamentos actuales</span>';
      mediEl.removeAttribute('role');
    }
  }

  // Abre modal con gestión de accesibilidad
  modalManager.open('modalBackdrop');
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
      verPaciente(parseInt(id));
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
  
  // Ver historia clínica (simulado)
  if (verHistoriaBtn) {
    verHistoriaBtn.addEventListener('click', () => {
      showToast('📋 Redirigiendo a Historia Parcial del Paciente…', 'info');
      modalManager.close('modalBackdrop');
    });
  }
  
  // Cerrar modal con tecla Escape
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      modalManager.close('modalBackdrop');
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  EVENTOS: Descargar resumen con feedback visual
// ═══════════════════════════════════════════════════════════════════
const initDescargarResumen = () => {
  const btn = safeGetElement('btnDescargar');
  if (!btn) return;

  btn.addEventListener('click', async () => {
    const original = btn.textContent;
    btn.textContent = '⏳ Generando...';
    btn.disabled = true;

    try {
      const res = await panelCtrl.descargarResumen();
      if (!res.ok) {
        btn.textContent = original;
        btn.disabled = false;
        window.ToastService.info('La generación de PDF aún no está disponible en el servidor');
        return;
      }
      btn.textContent = '✓ Descargado';
      btn.style.color = 'var(--green)';
      btn.style.borderColor = 'var(--green)';
      window.ToastService.success(`Resumen generado: ${res.nombre}`);

      setTimeout(() => {
        btn.textContent = original;
        btn.style.color = '';
        btn.style.borderColor = '';
        btn.disabled = false;
      }, 2000);
    } catch {
      btn.textContent = original;
      btn.disabled = false;
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
    initMobileMenu();
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
    
    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
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
  div.innerHTML = '<strong>[SmileTrack]</strong> ' + mensaje + ' <button onclick="document.getElementById(\'smiletrack-error-bar\').style.display=\'none\'" style="margin-left:16px;background:white;color:#dc2626;border:none;padding:4px 10px;border-radius:4px;cursor:pointer;font-weight:bold;">×</button>';
  div.style.display = 'block';
}

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);