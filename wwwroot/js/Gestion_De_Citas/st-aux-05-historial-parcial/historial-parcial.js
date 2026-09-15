/* ============================================
SmileTrack — Historial Clínico Parcial (st-aux-05-historial-parcial)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Maneja la lógica interactiva del historial parcial del paciente: consulta de alertas, renderizado de la tabla de consultas anteriores con remoción de skeleton loaders, y accesibilidad ARIA.

FUNCIONALIDADES PRINCIPALES:
- Lectura de los datos reales del paciente y su historial, inyectados por el servidor vía SSR
  (window.smiletrackHistorialParcialData, ver ConstruirHistorialParcialAsync en
  GestionCitasController.cs). No hay ninguna llamada a la API desde esta vista: es de solo lectura.
- Renderizado interactivo de la tarjeta de alertas de alergias con indicadores visuales, escalando
  su severidad ARIA (role/aria-live) según si el paciente tiene alergias registradas o no.
- Poblamiento de la tabla de consultas con remoción dinámica de skeleton loaders.
- Soporte para lectores de pantalla con actualización de etiquetas aria-live y descriptivas.

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.css
- JS: ~/js/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.js
- Requiere: ~/js/shared/common.js (window.CommonUtils) cargado ANTES que este archivo
- Partial / Otros: historial-parcial.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- El controlador limita estrictamente las consultas a un máximo de 3 filas para mantener el perfil "parcial" por seguridad.
- AUDITORÍA (ver docs/mejoras/st-aux-05-historial-parcial.md):
  - safeGetElement ya no se reimplementa acá: usamos window.CommonUtils.safeGetElement, que hacía
    exactamente lo mismo (shared/common.js).
  - Se eliminó el debounce local: esta vista no tiene buscador ni filtro que lo necesite, era código
    muerto.
  - El formateo de fecha corta ahora usa window.CommonUtils.formatFechaLocal en vez de reinventar
    toLocaleDateString('es-CO', ...) — mismo formato "05 Sep 2026" que usa el resto del proyecto.
  - El Promise.all de getAlertas()/getConsultas() NO espera ninguna red real (los datos ya están en
    memoria desde el SSR): se conserva por consistencia estructural con vistas que sí hacen fetch,
    no porque haya una espera asíncrona genuina.
============================================ */

// WHY: reutilizamos la versión canónica de shared/common.js en vez de reimplementarla por vista.
const safeGetElement = window.CommonUtils.safeGetElement;

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
      fecha: c.fecha ? window.CommonUtils.formatFechaLocal(c.fecha) : ''
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
  // WHY: Actualiza nodos del DOM e inyecta etiquetas ARIA descriptivas para lectores de pantalla.
  // Los spans internos ya NO tienen aria-live propio (ver historial-parcial.cshtml): la región
  // aria-live única es #alertaMedica, para que el navegador anuncie el cambio con la severidad
  // (assertive/polite) que le asignamos más abajo, en vez del comportamiento indefinido que
  // resulta de anidar live regions.
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

  // WHY: la severidad ARIA del contenedor se decide con los datos reales del paciente, no de forma
  // estática en el HTML: un paciente sin alergias registradas no debe generar una alerta "assertive"
  // (interrupción inmediata) para usuarios de lector de pantalla — sería una falsa alarma médica.
  const card = safeGetElement('alertaMedica');
  if (card) {
    const hayAlergias = alertas.alergias.length > 0;
    card.setAttribute('role', hayAlergias ? 'alert' : 'status');
    card.setAttribute('aria-live', hayAlergias ? 'assertive' : 'polite');

    // WHY: Reduce el impacto visual si no hay alertas críticas, evitando falsas alarmas
    if (!hayAlergias && !alertas.medicamentos.length) {
      card.style.opacity = '.6';
      card.setAttribute('aria-label', 'Sin alertas médicas registradas para este paciente');
    } else {
      card.removeAttribute('aria-label');
    }
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