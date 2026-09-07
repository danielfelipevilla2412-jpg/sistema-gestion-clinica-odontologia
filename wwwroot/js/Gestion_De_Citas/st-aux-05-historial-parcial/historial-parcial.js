/* ============================================
SmileTrack — Historial Clínico Parcial (st-aux-05-historial-parcial)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Maneja la lógica interactiva del historial parcial del paciente: consulta de alertas, renderizado de la tabla de consultas anteriores con remoción de skeleton loaders, y accesibilidad ARIA.

FUNCIONALIDADES PRINCIPALES:
- Consulta asíncrona simulada de datos demográficos y antecedentes del paciente
- Renderizado interactivo de la tarjeta de alertas de alergias con indicadores visuales
- Poblamiento de la tabla de consultas con remoción dinámica de skeleton loaders
- Soporte para lectores de pantalla con actualización de etiquetas aria-live y descriptivas

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.css
- JS: ~/js/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.js
- Partial / Otros: historial-parcial.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- El controlador limita estrictamente las consultas a un máximo de 3 filas para mantener el perfil "parcial" por seguridad.
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

// ═══════════════════════════════════════════════════════════════════
//  HISTORIA PARCIAL CONTROLLER CON PERSISTENCIA
// ═══════════════════════════════════════════════════════════════════
class HistoriaParcialController {
  constructor() {
    const data = window.smiletrackHistorialParcialData || {};
    this._paciente = data.paciente || { id: null, nombre: 'Sin paciente asignado', tipoDoc: '', documento: '', alergias: [], medicamentos: [], grupoSanguineo: 'N/D', ultimaActualizacion: '' };
    this._limite = data.limite || 3;
    this._historial = (data.consultas || []).map(c => ({
      ...c,
      fecha: c.fecha ? new Date(c.fecha).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' }) : ''
    }));
  }

  // Datos reales inyectados por el servidor (ver ConstruirHistorialParcialAsync en
  // GestionCitasController.cs).
  async getPaciente() {
    return { ...this._paciente };
  }

  // Estructura la información médica crítica separadamente de los datos personales
  async getAlertas() {
    return {
      alergias: [...this._paciente.alergias],
      medicamentos: [...this._paciente.medicamentos],
      grupoSanguineo: this._paciente.grupoSanguineo,
    };
  }

  // El límite ya viene aplicado desde el servidor (Take(limite))
  async getConsultas() {
    return this._historial.slice(0, this._limite);
  }

  // Genera un texto consolidado de identificación para ubicar rápidamente la información
  getMetaString() {
    const p = this._paciente;
    return `${p.nombre} · ${p.tipoDoc} ${p.documento} · Últimas ${this._limite} consultas · Acceso parcial · Actualizado: ${p.ultimaActualizacion}`;
  }
}

// Instancia única del controlador para toda la aplicación
const hpCtrl = new HistoriaParcialController();

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL CON GESTIÓN DE FOCO Y ARIA
// ═══════════════════════════════════════════════════════════════════
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Alerta médica con actualización dinámica
// ═══════════════════════════════════════════════════════════════════
const renderAlerta = (alertas) => {
  // WHY: Actualiza nodos del DOM e inyecta etiquetas ARIA descriptivas para lectores de pantalla
  const updateElement = (id, value) => {
    const el = safeGetElement(id);
    if (el) {
      el.textContent = value || '—';
      // Actualiza aria-label para screen readers si el valor cambia
      el.setAttribute('aria-label', `${el.previousElementSibling?.textContent?.trim() || 'Valor'}: ${value || 'No disponible'}`);
    }
  };

  // Actualiza cada campo de alerta
  updateElement('alergia', alertas.alergias.join(', ') || 'Ninguna conocida');
  updateElement('medicamentos', alertas.medicamentos.join(', ') || 'Ninguno');
  updateElement('grupoSang', alertas.grupoSanguineo || '—');

  // WHY: Reduce el impacto visual si no hay alertas críticas, evitando falsas alarmas
  const card = safeGetElement('alertaMedica');
  if (card && !alertas.alergias.length && !alertas.medicamentos.length) {
    card.style.opacity = '.6';
    card.setAttribute('aria-label', 'Sin alertas médicas registradas para este paciente');
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Tabla de historial con skeleton loader
// ═══════════════════════════════════════════════════════════════════
const renderTabla = (filas) => {
  const tbody = safeGetElement('histBody');
  const footer = safeGetElement('tableFooter');
  if (!tbody) return;

  // Muestra mensaje si no hay datos
  if (!filas.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = 4;
    cell.style.cssText = 'text-align:center;padding:26px;color:var(--text-muted);font-size:.85rem;';
    cell.textContent = 'Sin consultas registradas.';
    row.appendChild(cell);
    tbody.replaceChildren(row);
    if (footer) footer.style.display = 'none';
    return;
  }

  // WHY: Reemplaza las celdas de carga (skeleton cells) por las de datos reales una vez completada la llamada asíncrona
  const crearCelda = (className, label, value) => {
    const cell = document.createElement('td');
    cell.className = className;
    cell.dataset.label = label;
    cell.textContent = value || 'Sin información';
    return cell;
  };
  tbody.replaceChildren(...filas.map(f => {
    const row = document.createElement('tr');
    row.append(
      crearCelda('td-fecha', 'Fecha', f.fecha),
      crearCelda('td-profesional', 'Profesional', f.profesional),
      crearCelda('', 'Diagnóstico', f.diagnostico),
      crearCelda('', 'Procedimiento', f.procedimiento)
    );
    return row;
  }));
};

// ═══════════════════════════════════════════════════════════════════
//  INIT: Función principal de inicialización
// ═══════════════════════════════════════════════════════════════════
const init = async () => {
    // Inicializar componentes de UI
    initMobileMenu();

    // WHY: Promise.all permite disparar peticiones concurrentes reduciendo el tiempo total de bloqueo de la UI
    const [alertas, consultas] = await Promise.all([
      hpCtrl.getAlertas(),
      hpCtrl.getConsultas(),
    ]);

    // Actualiza metadatos del paciente en el header
    const metaEl = safeGetElement('patientMeta');
    if (metaEl) {
      metaEl.textContent = hpCtrl.getMetaString();
      metaEl.setAttribute('aria-label', `Información del paciente: ${hpCtrl.getMetaString()}`);
    }

    // Renderiza alerta médica y tabla de historial
    renderAlerta(alertas);
    renderTabla(consultas);

    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);