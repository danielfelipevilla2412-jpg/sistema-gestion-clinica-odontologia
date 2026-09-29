/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Notificaciones del Paciente (st-pac-03-notificaciones)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-pac-03-notificaciones/notificaciones.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra el panel de notificaciones y recordatorios automatizados de citas para el paciente.
 * Permite marcar notificaciones como leídas, filtrar por tipo (confirmación, recordatorio, cancelación) y borrarlas.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Actualización dinámica del contador de notificaciones no leídas en el badge del encabezado.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Stpac03Notificaciones
 * - HTML: Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const DEBOUNCE_DELAY_MS = 180;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

const initialRawNotifications = window.smiletrackNotificacionesData?.notificaciones || [];
let notificationsList = initialRawNotifications.map(item => ({
  ...item,
  displayTime: formatRelativeTime(item.time)
}));

let currentFilterType = 'all';
let showOnlyUnreadState = false;
let lastActiveTriggerElement = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

function formatRelativeTime(isoString) {
  if (!isoString) return '';
  return window.CommonUtils?.formatTiempoRelativo ? window.CommonUtils.formatTiempoRelativo(isoString) : String(isoString);
}

const getBadgeClassForType = (badgeKey) => {
  const badgeMap = { 'pending': 'badge-agendada', 'new': 'badge-completada', 'read': 'badge-cancelada' };
  return badgeMap[badgeKey] || 'badge-cancelada';
};

const getBadgeLabelForType = (badgeKey) => {
  const labelMap = { 'pending': 'Pendiente', 'new': 'Nueva', 'read': 'Leída' };
  return labelMap[badgeKey] || 'Leída';
};

const getIconForNotificationType = (notificationType) => {
  const iconMap = {
    'reminder': 'notifications',
    'confirmed': 'check_circle',
    'cancelled': 'cancel',
    'rescheduled': 'event_repeat',
    'waitlist': 'hourglass_top',
    'invoice': 'payments',
    'message': 'message'
  };
  return iconMap[notificationType] || 'notifications';
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const markReadOnServer = async (notificationIds) => {
  const isSingle = notificationIds.length === 1;
  const endpointUrl = isSingle
    ? `/api/notificaciones/${notificationIds[0]}/leida`
    : '/api/notificaciones/leidas';

  const response = await fetch(endpointUrl, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    body: JSON.stringify(isSingle ? {} : notificationIds)
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || 'No fue posible guardar el estado de lectura.');
  }
};

const deleteOnServer = async (notificationId) => {
  const response = await fetch(`/api/notificaciones/${notificationId}`, {
    method: 'DELETE',
    credentials: 'same-origin',
    headers: { 'Accept': 'application/json' }
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || 'No fue posible eliminar la notificación.');
  }
};

const fetchNotificationsApi = async () => {
  try {
    const response = await fetch('/api/notificaciones', {
      headers: { 'Accept': 'application/json' },
      credentials: 'same-origin'
    });
    if (!response.ok) return;
    const payload = await response.json();
    if (payload.success && payload.data && Array.isArray(payload.data.notificaciones)) {
      notificationsList = payload.data.notificaciones.map(item => ({
        ...item,
        displayTime: formatRelativeTime(item.time)
      }));
      renderNotificationsList();
    }
  } catch (err) {
    console.warn('[SmileTrack] Fallback a datos SSR:', err);
  }
};

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const getFilteredNotifications = () => {
  const searchInput = safeGetElement('searchInput');
  const queryText = searchInput?.value.toLowerCase().trim() || '';
  
  return notificationsList.filter(item => {
    const matchQuery = !queryText || (
      (item.titulo || '').toLowerCase().includes(queryText) ||
      (item.desc || '').toLowerCase().includes(queryText)
    );
    const matchFilter = currentFilterType === 'all' || item.tipo === currentFilterType;
    const matchUnread = !showOnlyUnreadState || !item.leida;
    return matchQuery && matchFilter && matchUnread;
  });
};

const createNotificationCardElement = (item) => {
  const listItemElement = document.createElement('li');
  listItemElement.className = `notification-card${item.leida ? ' notification-card--read' : ''}`;
  listItemElement.dataset.type = item.tipo;
  listItemElement.dataset.id = item.id;
  
  if (!item.leida) {
    listItemElement.setAttribute('role', 'article');
    listItemElement.setAttribute('aria-label', `Notificación sin leer: ${item.titulo}`);
    listItemElement.setAttribute('tabindex', '0');
  }
  
  listItemElement.innerHTML = `
    <div class="notification-card__icon" aria-hidden="true">
      <span class="material-symbols-outlined">${escapeHtml(getIconForNotificationType(item.tipo))}</span>
    </div>
    <div class="notification-card__body">
      <div class="notification-card__header">
        <h3 class="notification-card__title">${escapeHtml(item.titulo)}</h3>
        <span class="badge ${escapeHtml(getBadgeClassForType(item.badge))}" aria-label="Estado: ${escapeHtml(getBadgeLabelForType(item.badge))}">${escapeHtml(getBadgeLabelForType(item.badge))}</span>
      </div>
      <p class="notification-card__desc">${escapeHtml(item.desc)}</p>
      <div class="notification-card__footer" style="display:flex; justify-content:space-between; align-items:center; margin-top:8px;">
        <time class="notification-card__time" datetime="${escapeHtml(item.time)}">${escapeHtml(item.displayTime)}</time>
        <div class="card-actions" style="display:flex; gap:6px;">
          <button type="button" class="btn-icon btn-view-detail" data-id="${item.id}" title="Ver detalle" aria-label="Ver detalle de la notificación">
            <span class="material-symbols-outlined" style="font-size:1.1rem;">visibility</span>
          </button>
          ${!item.leida ? `
          <button type="button" class="btn-icon btn-mark-read" data-id="${item.id}" title="Marcar como leída" aria-label="Marcar como leída">
            <span class="material-symbols-outlined" style="font-size:1.1rem;">done</span>
          </button>` : ''}
          <button type="button" class="btn-icon btn-delete-notif" data-id="${item.id}" title="Eliminar" aria-label="Eliminar notificación">
            <span class="material-symbols-outlined" style="font-size:1.1rem; color:var(--danger, #ef4444);">delete</span>
          </button>
        </div>
      </div>
    </div>
    ${!item.leida ? '<span class="notification-card__dot" aria-label="No leída" role="status"></span>' : ''}
  `;
  
  return listItemElement;
};

const renderNotificationsList = () => {
  const filteredData = getFilteredNotifications();
  const listContainer = safeGetElement('notificationsList');
  const emptyStateElement = safeGetElement('emptyState');
  
  if (!listContainer) return;
  listContainer.replaceChildren();
  
  const countLabel = safeGetElement('countLabel');
  if (countLabel) countLabel.textContent = `${filteredData.length} resultado${filteredData.length !== 1 ? 's' : ''}`;
  
  if (!filteredData.length) {
    if (emptyStateElement) {
      emptyStateElement.style.display = 'flex';
      emptyStateElement.setAttribute('aria-hidden', 'false');
    }
    return;
  }
  
  if (emptyStateElement) {
    emptyStateElement.style.display = 'none';
    emptyStateElement.setAttribute('aria-hidden', 'true');
  }
  
  filteredData.forEach(item => {
    const cardElement = createNotificationCardElement(item);
    listContainer.appendChild(cardElement);
  });
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const openModalDetail = (notif, triggerElement) => {
  lastActiveTriggerElement = triggerElement || document.activeElement;
  const modalElement = safeGetElement('modalNotifDetail');
  if (!modalElement) return;

  safeGetElement('modalNotifTitle').textContent = notif.titulo || 'Detalle de Notificación';
  safeGetElement('modalNotifDesc').textContent = notif.desc || '';
  safeGetElement('modalNotifTime').textContent = `${formatRelativeTime(notif.time)} (${new Date(notif.time).toLocaleString()})`;
  safeGetElement('modalNotifCanal').textContent = notif.canal || 'Interno (In-App)';
  
  const iconBadge = safeGetElement('modalNotifIcon');
  if (iconBadge) iconBadge.textContent = getIconForNotificationType(notif.tipo);

  const badgeElement = safeGetElement('modalNotifBadge');
  if (badgeElement) {
    badgeElement.className = `badge ${getBadgeClassForType(notif.badge)}`;
    badgeElement.textContent = getBadgeLabelForType(notif.badge);
  }

  const actionButton = safeGetElement('btnModalAction');
  if (actionButton) {
    if (notif.urlAccion) {
      actionButton.href = notif.urlAccion;
      actionButton.style.display = 'inline-flex';
    } else {
      actionButton.style.display = 'none';
    }
  }

  modalElement.classList.add('open');
  modalElement.setAttribute('aria-hidden', 'false');
  modalElement.removeAttribute('inert');
  document.body.style.overflow = 'hidden';

  const closeBtn = safeGetElement('btnModalClose');
  if (closeBtn) closeBtn.focus();
};

const closeModalDetail = () => {
  const modalElement = safeGetElement('modalNotifDetail');
  if (!modalElement) return;
  modalElement.classList.remove('open');
  modalElement.setAttribute('aria-hidden', 'true');
  modalElement.setAttribute('inert', '');
  document.body.style.overflow = '';

  if (lastActiveTriggerElement && typeof lastActiveTriggerElement.focus === 'function' && document.contains(lastActiveTriggerElement)) {
    lastActiveTriggerElement.focus();
  }
  lastActiveTriggerElement = null;
};

const handleNotificationCardAction = async (event) => {
  const viewButton = event.target.closest('.btn-view-detail');
  const markButton = event.target.closest('.btn-mark-read');
  const deleteButton = event.target.closest('.btn-delete-notif');
  const cardElement = event.target.closest('.notification-card');
  if (!cardElement) return;

  const notificationId = parseInt(cardElement.dataset.id, 10);
  if (isNaN(notificationId)) return;
  const notificationItem = notificationsList.find(item => item.id === notificationId);
  if (!notificationItem) return;

  if (viewButton || (!markButton && !deleteButton)) {
    openModalDetail(notificationItem, viewButton || cardElement);
    if (!notificationItem.leida) {
      try {
        await markReadOnServer([notificationId]);
        notificationItem.leida = true;
        notificationItem.badge = 'read';
        renderNotificationsList();
      } catch (err) {
        console.warn('[SmileTrack] Error al marcar leída:', err);
      }
    }
    return;
  }

  if (markButton) {
    event.stopPropagation();
    try {
      await markReadOnServer([notificationId]);
      notificationItem.leida = true;
      notificationItem.badge = 'read';
      renderNotificationsList();
      if (window.ToastService) window.ToastService.success('Notificación marcada como leída');
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
    return;
  }

  if (deleteButton) {
    event.stopPropagation();
    try {
      await deleteOnServer(notificationId);
      notificationsList = notificationsList.filter(item => item.id !== notificationId);
      renderNotificationsList();
      if (window.ToastService) window.ToastService.success('Notificación eliminada');
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
  }
};

const markAllNotificationsAsRead = async () => {
  const unreadItems = notificationsList.filter(item => !item.leida);
  if (!unreadItems.length) {
    if (window.ToastService) window.ToastService.success('No hay notificaciones sin leer');
    return;
  }
  
  try {
    await markReadOnServer(unreadItems.map(item => item.id));
    notificationsList.forEach(item => { item.leida = true; item.badge = 'read'; });
    renderNotificationsList();
    if (window.ToastService) window.ToastService.success('Todas las notificaciones marcadas como leídas');
  } catch (error) {
    if (window.ToastService) window.ToastService.error(error.message);
  }
};

const handleFilterChipClick = (chipElement) => {
  document.querySelectorAll('.chip').forEach(chip => {
    chip.classList.remove('chip--active');
    chip.setAttribute('aria-pressed', 'false');
  });
  
  chipElement.classList.add('chip--active');
  chipElement.setAttribute('aria-pressed', 'true');
  
  currentFilterType = chipElement.dataset.filter;
  renderNotificationsList();
};

const toggleUnreadFilterMode = () => {
  showOnlyUnreadState = !showOnlyUnreadState;
  const toggleBtn = safeGetElement('btnToggleUnread');
  const toggleLabel = safeGetElement('lblToggleUnread');
  if (toggleBtn) {
    toggleBtn.setAttribute('aria-pressed', String(showOnlyUnreadState));
    toggleBtn.classList.toggle('btn-primary', showOnlyUnreadState);
    toggleBtn.classList.toggle('btn-secondary', !showOnlyUnreadState);
  }
  if (toggleLabel) toggleLabel.textContent = showOnlyUnreadState ? 'Ver todas' : 'Solo no leídas';
  renderNotificationsList();
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupModalEventListeners = () => {
  const modalElement = safeGetElement('modalNotifDetail');
  safeGetElement('modalNotifClose')?.addEventListener('click', closeModalDetail);
  safeGetElement('btnModalClose')?.addEventListener('click', closeModalDetail);
  modalElement?.addEventListener('click', (event) => {
    if (event.target === modalElement) closeModalDetail();
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && modalElement?.classList.contains('open')) {
      event.preventDefault();
      closeModalDetail();
    }
  });
};

const initializeNotificationsModule = () => {
  renderNotificationsList();
  setupModalEventListeners();
  
  const notificationsListContainer = safeGetElement('notificationsList');
  if (notificationsListContainer) {
    notificationsListContainer.addEventListener('click', handleNotificationCardAction);
    notificationsListContainer.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        handleNotificationCardAction(event);
      }
    });
  }
  
  document.querySelectorAll('.chip').forEach(chipElement => {
    chipElement.addEventListener('click', () => handleFilterChipClick(chipElement));
  });
  
  const searchInputElement = safeGetElement('searchInput');
  if (searchInputElement) {
    const debouncedRenderHandler = debounce(renderNotificationsList, DEBOUNCE_DELAY_MS);
    searchInputElement.addEventListener('input', debouncedRenderHandler);
  }
  
  safeGetElement('btnMarkAllRead')?.addEventListener('click', markAllNotificationsAsRead);
  safeGetElement('btnToggleUnread')?.addEventListener('click', toggleUnreadFilterMode);

  fetchNotificationsApi();
};

document.addEventListener('DOMContentLoaded', initializeNotificationsModule);
