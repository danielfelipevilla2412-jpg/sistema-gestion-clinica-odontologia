/* ============================================
SmileTrack — Agenda de Apoyo Clínico (st-aux-02-agenda-apoyo)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Maneja el comportamiento interactivo de la agenda de apoyo del auxiliar: filtrado combinado por profesional y tipo de cita, renderizado de badges, y soporte de teclado para navegación del dropdown.

FUNCIONALIDADES PRINCIPALES:
- Carga de citas y almacenamiento temporal mediante controlador
- Filtrado combinado dinámico (profesional + tipo de cita) con actualizaciones inmediatas de la interfaz
- Despliegue interactivo y accesible del menú de selección de profesionales (soporte de flechas y Escape)
- Renderizado de badges temáticos de alergias críticas y estado de la cita odontológica

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm08Agenda
- CSS: ~/css/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.css
- JS: ~/js/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.js
- Partial / Otros: agenda-apoyo.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- El dropdown de selección de profesionales implementa un patrón completo de focus trap y navegación por teclado ARIA.
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
//  AGENDA CONTROLLER CON PERSISTENCIA
// ═══════════════════════════════════════════════════════════════════
class AgendaController {
  constructor() {
    const data = window.smiletrackAgendaApoyoData || {};
    this._fechaHoy = data.fechaHoy || '';
    this._citasBase = data.citas || [];
    this._filtroProfesional = 'todos';
    this._filtroTipo = 'todos';
  }

  // Datos reales inyectados por el servidor (ver ConstruirAgendaApoyoAsync en
  // GestionCitasController.cs); el filtrado se hace en memoria sobre esos datos.
  async getCitas(profesional = 'todos', tipo = 'todos') {
    let resultado = [...this._citasBase];
    
    if (profesional !== 'todos') {
      const normalizar = (valor) => String(valor || '').trim().toLocaleLowerCase('es-CO');
      resultado = resultado.filter(c => normalizar(c.profesional) === normalizar(profesional));
    }
    if (tipo !== 'todos') {
      resultado = resultado.filter(c => c.tipo === tipo);
    }
    return resultado;
  }

  getFechaHoy() { return this._fechaHoy; }
}

// Instancia única del controlador
const agendaCtrl = new AgendaController();

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL CON GESTIÓN DE FOCO Y ARIA
// ═══════════════════════════════════════════════════════════════════
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Tabla de citas con atributos ARIA
// ═══════════════════════════════════════════════════════════════════
const renderTabla = (citas) => {
  const tbody = safeGetElement('agendaBody');
  const empty = safeGetElement('tableEmpty');
  if (!tbody) return;

  if (!citas.length) {
    tbody.replaceChildren();
    if (empty) {
      empty.style.display = 'block';
      empty.setAttribute('aria-label', 'No hay citas que coincidan con los filtros aplicados');
    }
    return;
  }
  if (empty) empty.style.display = 'none';

  // WHY: Funciones auxiliares para aislar la lógica de renderizado de insignias y asegurar la inyección de atributos de accesibilidad
  const crearBadge = (className, label, ariaLabel) => {
    const badge = document.createElement('span');
    badge.className = className;
    badge.setAttribute('role', 'status');
    badge.setAttribute('aria-label', ariaLabel);
    badge.textContent = label;
    return badge;
  };

  const badgeTipo = (tipo) => {
    const map = {
      consulta: ['badge-consulta', 'Consulta'],
      procedimiento: ['badge-procedimiento', 'Procedimiento'],
      urgencia: ['badge-urgencia', 'Urgencia'],
    };
    const [cls, label] = map[tipo] || ['', tipo || 'Sin tipo'];
    return crearBadge(`badge-tipo ${cls}`, label, `Tipo: ${label}`);
  };

  const badgeAlergia = (a) => {
    if (a) {
      return crearBadge('badge-alergia', `Alerta: ${a}`, `Alergia: ${a}`);
    }
    const badge = document.createElement('span');
    badge.style.color = 'var(--text-muted)';
    badge.setAttribute('aria-label', 'Sin alergias registradas');
    badge.textContent = '—';
    return badge;
  };

  const badgeEstado = (e) => {
    const estado = CommonUtils.normalizeAppointmentStatus(e);
    const map = {
      atendida: 'badge-atendida',
      pendiente: 'badge-pendiente',
      cancelada: 'badge-cancelada',
      no_asistida: 'badge-no-asistio'
    };
    return crearBadge(`badge-estado ${map[estado] || 'badge-pendiente'}`, `● ${e}`, `Estado: ${e}`);
  };

  const crearCelda = (className, text) => {
    const cell = document.createElement('td');
    cell.className = className;
    cell.textContent = text || '';
    return cell;
  };

  tbody.replaceChildren(...citas.map(c => {
    const row = document.createElement('tr');
    row.setAttribute('role', 'row');
    row.append(
      crearCelda('td-hora', c.hora),
      crearCelda('td-paciente', c.paciente),
      crearCelda('td-profesional', c.profesional)
    );
    const tipoCell = document.createElement('td');
    tipoCell.appendChild(badgeTipo(c.tipo));
    const alergiaCell = document.createElement('td');
    alergiaCell.appendChild(badgeAlergia(c.alergia));
    const estadoCell = document.createElement('td');
    estadoCell.appendChild(badgeEstado(c.estado));
    row.append(tipoCell, alergiaCell, estadoCell);
    return row;
  }));
};

// ═══════════════════════════════════════════════════════════════════
//  FILTROS CON PERSISTENCIA Y ACCESIBILIDAD
// ═══════════════════════════════════════════════════════════════════
const aplicarFiltros = async () => {
  const citas = await agendaCtrl.getCitas(agendaCtrl._filtroProfesional, agendaCtrl._filtroTipo);
  renderTabla(citas);
};

// WHY: Los filtros tipo radio simulan un comportamiento excluyente, garantizando una única selección activa a la vez
const initTipoFiltros = () => {
  const buttons = document.querySelectorAll('.filter-btn[data-value]');
  
  buttons.forEach(btn => {
    btn.addEventListener('click', () => {
      const value = btn.dataset.value;
      agendaCtrl._filtroTipo = value;
      
      // Actualiza estado visual y ARIA
      buttons.forEach(b => {
        b.classList.remove('active');
        b.setAttribute('aria-checked', 'false');
      });
      btn.classList.add('active');
      btn.setAttribute('aria-checked', 'true');
      
      // Aplica filtros
      aplicarFiltros();
      
      // Feedback visual
      window.ToastService.success(`Filtro aplicado: ${btn.textContent.trim()}`, 'info');
    });
    
    // WHY: Habilita activación mediante Enter o Barra Espaciadora para usuarios sin ratón
    btn.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        btn.click();
      }
    });
  });
};

// WHY: Habilita el control por teclado en el menú desplegable (focusing, ArrowDown, ArrowUp, Escape) para cumplir normas WCAG 2.1 AA
const initProfesionalDropdown = () => {
  const btn = safeGetElement('btnProfesional');
  const menu = safeGetElement('dropdownMenu');
  const items = menu?.querySelectorAll('.dd-item');
  
  if (!btn || !menu || !items) return;
  
  // Toggle dropdown con click
  btn.addEventListener('click', () => {
    const isOpen = menu.classList.toggle('open');
    btn.setAttribute('aria-expanded', isOpen);
    if (isOpen) {
      // Enfocar primer item al abrir
      items[0]?.focus();
    }
  });
  
  // Cerrar al hacer click fuera
  document.addEventListener('click', (e) => {
    const wrap = document.querySelector('.dropdown-wrap');
    if (wrap && !wrap.contains(e.target)) {
      menu.classList.remove('open');
      btn.setAttribute('aria-expanded', 'false');
    }
  });
  
  // Manejo de selección de items
  items.forEach(item => {
    item.addEventListener('click', () => {
      const value = item.dataset.value;
      agendaCtrl._filtroProfesional = value;
      
      // Actualiza texto del botón
      const arrow = document.createElement('span');
      arrow.className = 'dd-arrow';
      arrow.setAttribute('aria-hidden', 'true');
      arrow.textContent = '▼';
      btn.replaceChildren(document.createTextNode(item.textContent.trim() + ' '), arrow);
      
      // Actualiza estado visual
      items.forEach(i => i.classList.remove('active'));
      item.classList.add('active');
      
      // Cierra dropdown
      menu.classList.remove('open');
      btn.setAttribute('aria-expanded', 'false');
      
      // Aplica filtros
      aplicarFiltros();
      
      // Feedback visual
      window.ToastService.success(`Profesional: ${item.textContent.trim()}`, 'info');
    });
    
    // Soporte para teclado en items
    item.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        item.click();
      } else if (e.key === 'Escape') {
        menu.classList.remove('open');
        btn.setAttribute('aria-expanded', 'false');
        btn.focus();
      } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        const currentIndex = Array.from(items).indexOf(item);
        const nextIndex = e.key === 'ArrowDown' 
          ? (currentIndex + 1) % items.length 
          : (currentIndex - 1 + items.length) % items.length;
        items[nextIndex]?.focus();
      }
    });
  });
  
  // Soporte para teclado en botón
  btn.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      btn.click();
    } else if (e.key === 'ArrowDown' && menu.classList.contains('open')) {
      e.preventDefault();
      items[0]?.focus();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  INIT: Función principal de inicialización
// ═══════════════════════════════════════════════════════════════════
const init = async () => {
  // Inicializar componentes de UI
  initMobileMenu();
  initTipoFiltros();
  initProfesionalDropdown();

  document.querySelector('.toggle-vistas')?.addEventListener('viewchange', (event) => {
    const params = new URLSearchParams(window.location.search);
    const current = params.get('fecha') || new Date().toISOString().slice(0, 10);
    if (event.detail.view === 'semana') {
      params.delete('fecha');
      params.set('weekStart', params.get('weekStart') || current);
    } else if (event.detail.view === 'dia') {
      params.delete('weekStart');
      params.set('fecha', current);
    } else {
      params.delete('weekStart');
      params.set('fecha', current);
    }
    window.location.search = params.toString();
  });
  
  // Actualiza metadatos del header según la vista activa (día o semana)
  const metaEl = safeGetElement('phMeta');
  if (metaEl) {
    const params = new URLSearchParams(window.location.search);
    const fecha = params.get('fecha') || agendaCtrl.getFechaHoy();
    const weekStart = params.get('weekStart');

    const formatoFecha = (value) => {
      if (!value) return '—';
      const d = new Date(value + 'T00:00:00');
      return Number.isNaN(d.getTime()) ? value : d.toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
    };

    const metaText = weekStart
      ? `Semana del ${formatoFecha(weekStart)} al ${formatoFecha(new Date(new Date(weekStart + 'T00:00:00').getTime() + 6 * 86400000).toISOString().slice(0, 10))}`
      : `Citas del día ${formatoFecha(fecha)}`;

    metaEl.textContent = metaText;
    metaEl.setAttribute('data-meta-text', metaText);
    metaEl.setAttribute('aria-label', `Información: ${metaText}`);
  }
  
  // WHY: Dispara la primera carga de datos al iniciar el módulo
  const citas = await agendaCtrl.getCitas();
  renderTabla(citas);
  
  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);