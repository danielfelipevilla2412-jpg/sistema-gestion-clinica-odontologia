/* ============================================
SmileTrack — Notificaciones Paciente (st-pac-03-notificaciones)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Controla la carga de notificaciones del paciente, acciones de marcar como leídas y filtrado de alertas.

FUNCIONALIDADES PRINCIPALES:
- Carga reactiva de la lista de notificaciones vía API fetch
- Toggle interactivo de estados leído/no leído e inserción de contadores dinámicos

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y NotificacionesPaciente
- CSS: ~/css/Gestion_De_Citas/st-pac-03-notificaciones/styles.css
- JS: ~/js/Gestion_De_Citas/st-pac-03-notificaciones/notificaciones.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
============================================ */

// WHY: safeGetElement evita excepciones fatales en tiempo de ejecución si un id no se encuentra en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) {
    console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  }
  return el;
};

// WHY: Debounce evita la sobrecarga del hilo principal ante eventos repetitivos como tecleos de búsqueda o redimensiones
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

const escapeHtml = (value) => String(value ?? '').replace(/[&<>'"]/g, (character) => ({
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  "'": '&#39;',
  '"': '&quot;'
}[character]));

// WHY: Muestra retroalimentación temporal autolimpiable para no interrumpir el flujo visual de la lista de alertas

// —— DATOS REALES (derivados de las citas del paciente) ——
// Ver ConstruirNotificacionesPacienteAsync en GestionCitasController.cs: no existe una
// tabla de notificaciones en el esquema, así que se generan a partir de citas reales.
const formatTiempoRelativo = (iso) => {
  if (!iso) return '';
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return '';
  const diffMs = Date.now() - fecha.getTime();
  const diffHoras = diffMs / (1000 * 60 * 60);
  if (diffHoras < 0) {
    // Fecha futura (recordatorio de cita próxima)
    const horasFuturas = Math.abs(diffHoras);
    if (horasFuturas < 24) return `En ${Math.round(horasFuturas)} horas`;
    return `En ${Math.round(horasFuturas / 24)} días`;
  }
  if (diffHoras < 1) return 'Hace unos minutos';
  if (diffHoras < 24) return `Hace ${Math.round(diffHoras)} horas`;
  const diffDias = Math.round(diffHoras / 24);
  if (diffDias === 1) return 'Ayer';
  if (diffDias < 7) return `Hace ${diffDias} días`;
  return `Hace ${Math.round(diffDias / 7)} semanas`;
};

const SAMPLE_NOTIFICACIONES = (window.smiletrackNotificacionesData?.notificaciones || [])
  .map(n => ({ ...n, time: formatTiempoRelativo(n.time) }));

let notificaciones = [...SAMPLE_NOTIFICACIONES];
let currentFilter = 'all';

// ── Badge class por estado ──
/**
 * Retorna la clase CSS para el badge según el estado
 * @param {string} badge - Estado del badge
 * @returns {string} Clase CSS del badge
 */
const badgeClass = (badge) => {
  const map = { 'pending':'badge-agendada', 'new':'badge-completada', 'read':'badge-cancelada' };
  return map[badge] || 'badge-cancelada'; // [MEJORA]: Fallback seguro
};

/**
 * Retorna la etiqueta legible para el badge
 * @param {string} badge - Estado del badge
 * @returns {string} Label del badge
 */
const badgeLabel = (badge) => {
  const map = { 'pending': 'Pendiente', 'new': 'Nueva', 'read': 'Leída' };
  return map[badge] || 'Leída'; // [MEJORA]: Fallback seguro
};

// ── Icono por tipo de notificación ──
/**
 * Retorna el emoji/icono según el tipo de notificación
 * @param {string} tipo - Tipo de notificación
 * @returns {string} Emoji representativo
 */
const getIconByType = (tipo) => {
  const map = { 'reminder':'📅', 'confirmed':'✅', 'cancelled':'❌', 'message':'💬' };
  return map[tipo] || '🔔'; // [MEJORA]: Fallback seguro
};

// ── Obtener notificaciones filtradas ──
/**
 * Filtra notificaciones según búsqueda y filtro activo
 * @returns {Array} Array de notificaciones filtradas
 */
const getFiltered = () => {
  // [MEJORA]: Uso de safeGetElement para validación segura
  const searchInput = safeGetElement('searchInput');
  const q = searchInput?.value.toLowerCase().trim() || '';
  
  return notificaciones.filter(n => {
    const matchQ = !q || (
      n.titulo.toLowerCase().includes(q) ||
      n.desc.toLowerCase().includes(q) ||
      n.time.toLowerCase().includes(q)
    );
    const matchFilter = currentFilter === 'all' || n.tipo === currentFilter;
    return matchQ && matchFilter;
  });
};

// ── Crear elemento de notificación (separado para mantenibilidad) ──
/**
 * Crea el elemento DOM para una notificación individual
 * @param {Object} item - Datos de la notificación
 * @returns {HTMLLIElement} Elemento li con la notificación
 */
const createNotificationItem = (item) => {
  const li = document.createElement('li');
  li.className = `notification-card${item.leida ? ' notification-card--read' : ''}`;
  li.dataset.type = item.tipo;
  li.dataset.id = item.id;
  
  // [MEJORA]: Atributos ARIA para accesibilidad de item interactivo
  if (!item.leida) {
    li.setAttribute('role', 'article');
    li.setAttribute('aria-label', `Notificación sin leer: ${item.titulo}`);
    li.setAttribute('tabindex', '0');
  }
  
  li.innerHTML = `
    <div class="notification-card__icon" aria-hidden="true">${escapeHtml(getIconByType(item.tipo))}</div>
    <div class="notification-card__body">
      <div class="notification-card__header">
        <h3 class="notification-card__title">${escapeHtml(item.titulo)}</h3>
        <span class="badge ${escapeHtml(badgeClass(item.badge))}" aria-label="Estado: ${escapeHtml(badgeLabel(item.badge))}">${escapeHtml(badgeLabel(item.badge))}</span>
      </div>
      <p class="notification-card__desc">${escapeHtml(item.desc)}</p>
      <time class="notification-card__time" datetime="${escapeHtml(item.time)}">${escapeHtml(item.time)}</time>
    </div>
    ${!item.leida ? '<span class="notification-card__dot" aria-label="No leída" role="status"></span>' : ''}
  `;
  
  return li;
};

// ── Render lista de notificaciones ──
/**
 * Renderiza la lista de notificaciones con los datos filtrados
 * [MEJORA]: Event delegation centralizado para mejor performance
 */
const renderNotifications = () => {
  const data = getFiltered();
  const container = safeGetElement('notificationsList');
  const emptyState = safeGetElement('emptyState');
  
  // [MEJORA]: Validaciones de seguridad para elementos del DOM
  if (!container) return;
  
  container.replaceChildren();
  
  const countLabel = safeGetElement('countLabel');
  if (countLabel) countLabel.textContent = `${data.length} resultado${data.length !== 1 ? 's' : ''}`;
  
  // Manejo de empty state con accesibilidad
  if (!data.length) {
    if (emptyState) {
      emptyState.style.display = 'flex';
      emptyState.setAttribute('aria-hidden', 'false');
    }
    return;
  }
  
  if (emptyState) {
    emptyState.style.display = 'none';
    emptyState.setAttribute('aria-hidden', 'true');
  }
  
  data.forEach(item => {
    const li = createNotificationItem(item);
    container.appendChild(li);
  });
};

const markReadOnServer = async (ids) => {
  const response = await fetch(ids.length === 1
    ? `/api/notificaciones/${ids[0]}/leida`
    : '/api/notificaciones/leidas', {
      method: 'PUT',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
      body: JSON.stringify(ids.length === 1 ? {} : ids)
    });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || 'No fue posible guardar el estado de lectura.');
  }
};

// ── Manejador centralizado de clicks en notificaciones ──
/**
 * [MEJORA]: Event delegation para manejar clicks en lista dinámica
 * Evita attach de listeners individuales por item (mejor performance)
 * @param {Event} e - Evento de click
 */
const handleNotificationClick = async (e) => {
  // [MEJORA]: Encontrar el card más cercano (soporta clicks en hijos)
  const card = e.target.closest('.notification-card');
  if (!card) return;
  
  const id = parseInt(card.dataset.id, 10);
  // [MEJORA]: Validar que id sea número válido
  if (isNaN(id)) return;
  
  // [MEJORA]: Ignorar si ya está leída o si se hizo click en botón
  if (card.classList.contains('notification-card--read') || e.target.closest('button')) {
    return;
  }
  
  const notif = notificaciones.find(n => n.id === id);
  if (notif) {
    try {
      await markReadOnServer([id]);
      notif.leida = true;
      notif.badge = 'read';
      renderNotifications();
      window.ToastService.success('Notificación marcada como leída');
    } catch (error) {
      window.ToastService.error(error.message);
    }
  }
};

// ── Marcar todas como leídas ──
/**
 * Marca todas las notificaciones como leídas y actualiza UI
 */
const markAllAsRead = async () => {
  const hayNoLeidas = notificaciones.some(n => !n.leida);
  if (!hayNoLeidas) {
    window.ToastService.success('No hay notificaciones sin leer');
    return;
  }
  
  try {
    await markReadOnServer(notificaciones.filter(n => !n.leida).map(n => n.id));
    notificaciones.forEach(n => { n.leida = true; n.badge = 'read'; });
    renderNotifications();
    window.ToastService.success('Todas las notificaciones marcadas como leídas');
  } catch (error) {
    window.ToastService.error(error.message);
  }
};

// ── Manejo de chips de filtro ──
/**
 * Actualiza el filtro activo y actualiza la UI
 * @param {HTMLElement} chip - Elemento chip clickeado
 */
const handleChipClick = (chip) => {
  // [MEJORA]: Actualizar aria-pressed para accesibilidad
  document.querySelectorAll('.chip').forEach(c => {
    c.classList.remove('chip--active');
    c.setAttribute('aria-pressed', 'false');
  });
  
  chip.classList.add('chip--active');
  chip.setAttribute('aria-pressed', 'true');
  
  currentFilter = chip.dataset.filter;
  renderNotifications();
};

// ── Manejo de menú móvil (hamburger) ──
/**
 * Inicializa eventos del menú móvil con gestión de accesibilidad
 */
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// ── Init principal ──
/**
 * Función principal de inicialización de la página
 */
const init = () => {
  // Renderizado inicial
  renderNotifications();
  
  // [MEJORA]: Event delegation para lista de notificaciones (performance)
  const notificationsList = safeGetElement('notificationsList');
  if (notificationsList) {
    notificationsList.addEventListener('click', handleNotificationClick);
    // [MEJORA]: Soporte para activación con teclado (Enter/Space)
    notificationsList.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleNotificationClick(e);
      }
    });
  }
  
  // Inicializar chips de filtro
  document.querySelectorAll('.chip').forEach(chip => {
    chip.addEventListener('click', () => handleChipClick(chip));
  });
  
  // [MEJORA]: Búsqueda con debounce extraído como utilidad (consistente con st-pac-01)
  const searchEl = safeGetElement('searchInput');
  if (searchEl) {
    const debouncedRender = debounce(renderNotifications, 180);
    searchEl.addEventListener('input', debouncedRender);
  }
  
  // Botón "Marcar todas como leídas"
  const btnMarkAll = safeGetElement('btnMarkAllRead');
  if (btnMarkAll) btnMarkAll.addEventListener('click', markAllAsRead);
  
  // Inicializar menú móvil con accesibilidad
  initMobileMenu();
  
  // [MEJORA]: Limpieza de listeners al unload (buena práctica para SPAs)
  window.addEventListener('beforeunload', () => {
    // En una SPA real, aquí se removerían listeners para evitar memory leaks
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);