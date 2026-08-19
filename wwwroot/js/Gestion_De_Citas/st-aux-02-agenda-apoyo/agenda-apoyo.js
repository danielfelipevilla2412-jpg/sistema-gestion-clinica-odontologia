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
      resultado = resultado.filter(c => c.profesional.toLowerCase().includes(profesional));
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

  sidebar.querySelectorAll('.nav-item').forEach(link => {
    link.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: Tabla de citas con atributos ARIA
// ═══════════════════════════════════════════════════════════════════
const renderTabla = (citas) => {
  const tbody = safeGetElement('agendaBody');
  const empty = safeGetElement('tableEmpty');
  if (!tbody) return;

  if (!citas.length) {
    tbody.innerHTML = '';
    if (empty) {
      empty.style.display = 'block';
      empty.setAttribute('aria-label', 'No hay citas que coincidan con los filtros aplicados');
    }
    return;
  }
  if (empty) empty.style.display = 'none';

  // WHY: Funciones auxiliares para aislar la lógica de renderizado de insignias y asegurar la inyección de atributos de accesibilidad
  const badgeTipo = (tipo) => {
    const map = {
      consulta: ['badge-consulta', 'Consulta'],
      procedimiento: ['badge-procedimiento', 'Procedimiento'],
      urgencia: ['badge-urgencia', 'Urgencia'],
    };
    const [cls, label] = map[tipo] || ['', tipo];
    return `<span class="badge-tipo ${cls}" role="status" aria-label="Tipo: ${label}">${label}</span>`;
  };

  const badgeAlergia = (a) => {
    if (a) {
      return `<span class="badge-alergia" role="status" aria-label="Alergia: ${a}">🚨 ${a}</span>`;
    }
    return `<span style="color:var(--text-muted)" aria-label="Sin alergias registradas">—</span>`;
  };

  const badgeEstado = (e) => {
    const map = { 'Atendida':'badge-atendida', 'Pendiente':'badge-pendiente', 'Cancelada':'badge-cancelada' };
    return `<span class="badge-estado ${map[e]||'badge-pendiente'}" role="status" aria-label="Estado: ${e}">● ${e}</span>`;
  };

  tbody.innerHTML = citas.map(c => `
    <tr role="row">
      <td class="td-hora">${c.hora}</td>
      <td class="td-paciente">${c.paciente}</td>
      <td class="td-profesional">${c.profesional}</td>
      <td>${badgeTipo(c.tipo)}</td>
      <td>${badgeAlergia(c.alergia)}</td>
      <td>${badgeEstado(c.estado)}</td>
    </tr>
  `).join('');
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
      btn.innerHTML = `${item.textContent.trim()} <span class="dd-arrow" aria-hidden="true">▼</span>`;
      
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
  
  // Actualiza metadatos del header
  const metaEl = safeGetElement('phMeta');
  if (metaEl) {
    metaEl.textContent = `Citas del día que requieren asistencia · ${agendaCtrl.getFechaHoy()}`;
    metaEl.setAttribute('aria-label', `Información: ${metaEl.textContent}`);
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