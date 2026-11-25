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

// Traduce una clave si el diccionario la conoce; si no, devuelve el texto de respaldo.
const t = (key, fallback) => (window.I18N && window.I18N.has(key)) ? window.I18N.t(key) : fallback;

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
let showOnlyUnreadState = true;
let lastActiveTriggerElement = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

// El token antiforgery lo renderiza @Html.AntiForgeryToken() como input oculto en la vista.
const getCsrfToken = () =>
  document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const escapeHtml = (value) =>
  window.CommonUtils?.escapeHtml ? window.CommonUtils.escapeHtml(value) : String(value ?? '');

function formatRelativeTime(isoString) {
  if (!isoString) return '';
  return window.CommonUtils?.formatTiempoRelativo ? window.CommonUtils.formatTiempoRelativo(isoString) : String(isoString);
}

const getBadgeClassForType = (badgeKey) => {
  const badgeMap = { 'pending': 'badge-agendada', 'new': 'badge-completada', 'read': 'badge-completada' };
  return badgeMap[badgeKey] || 'badge-cancelada';
};

const getBadgeLabelForType = (badgeKey) => {
  const labelMap = { 'pending': t('status.pending', 'Pendiente'), 'new': t('status.new', 'Nueva'), 'read': t('pac_notif.badge_read', 'Leída') };
  return labelMap[badgeKey] || t('pac_notif.badge_read', 'Leída');
};

const getIconForNotificationType = (notificationType) => {
  const iconMap = {
    'reminder': 'notifications',
    'confirmed': 'check_circle',
    'rejected': 'block',
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

const markReadOnServer = async (notification) => {
  const endpointUrl = notification.idNotificacion
    ? `/api/notificaciones/${notification.idNotificacion}/leida`
    : `/api/notificaciones/citas/${notification.idCita}/leida`;
  const response = await window.SmileTrack.utils.fetchWithTimeout(endpointUrl, {
    method: 'PUT',
    credentials: 'same-origin',
    headers: {
      'Accept': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken()
    }
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || t('pac_notif.error_mark_read', 'No fue posible guardar el estado de lectura.'));
  }
};

// Marca en lote (una sola petición) las notificaciones persistidas por su IdNotificacion.
// El endpoint sólo acepta IdNotificacion; las derivadas de citas usan markReadOnServer.
const markAllReadOnServer = async (idsNotificacion) => {
  const response = await window.SmileTrack.utils.fetchWithTimeout('/api/notificaciones/leidas', {
    method: 'PUT',
    credentials: 'same-origin',
    headers: {
      'Accept': 'application/json',
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken()
    },
    body: JSON.stringify(idsNotificacion)
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || t('pac_notif.error_mark_all_read', 'No fue posible marcar las notificaciones como leídas.'));
  }
};

const deleteOnServer = async (notification) => {
  if (!notification.idNotificacion) {
    throw new Error(t('pac_notif.error_delete_informative', 'Esta notificación informativa no se puede eliminar.'));
  }
  const response = await window.SmileTrack.utils.fetchWithTimeout(`/api/notificaciones/${notification.idNotificacion}`, {
    method: 'DELETE',
    credentials: 'same-origin',
    headers: {
      'Accept': 'application/json',
      'X-CSRF-TOKEN': getCsrfToken()
    }
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || t('pac_notif.error_delete', 'No fue posible eliminar la notificación.'));
  }
};

const fetchNotificationsApi = async () => {
  const listContainer = safeGetElement('notificationsList');
  listContainer?.setAttribute('aria-busy', 'true');
  try {
    const response = await window.SmileTrack.utils.fetchWithTimeout('/api/notificaciones', {
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
    // Fallback legítimo: si la API falla, se conservan los datos SSR ya renderizados.
    console.warn('[SmileTrack] Fallback a datos SSR:', err);
  } finally {
    listContainer?.setAttribute('aria-busy', 'false');
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
    listItemElement.setAttribute('aria-label', `${t('pac_notif.unread_aria_prefix', 'Notificación sin leer:')} ${item.titulo}`);
    listItemElement.setAttribute('tabindex', '0');
  }
  
  listItemElement.innerHTML = `
    <div class="notification-card__icon" aria-hidden="true">
      <span class="material-symbols-outlined">${escapeHtml(getIconForNotificationType(item.tipo))}</span>
    </div>
    <div class="notification-card__body">
      <div class="notification-card__header">
        <h3 class="notification-card__title">${escapeHtml(item.titulo)}</h3>
        <span class="badge ${escapeHtml(getBadgeClassForType(item.badge))}" aria-label="${t('pac_notif.status_aria_prefix', 'Estado:')} ${escapeHtml(getBadgeLabelForType(item.badge))}">${escapeHtml(getBadgeLabelForType(item.badge))}</span>
      </div>
      <p class="notification-card__desc">${escapeHtml(item.desc)}</p>
      <div class="notification-card__footer" style="display:flex; justify-content:space-between; align-items:center; margin-top:8px;">
        <time class="notification-card__time" datetime="${escapeHtml(item.time)}">${escapeHtml(item.displayTime)}</time>
        <div class="card-actions" style="display:flex; gap:6px;">
          <button type="button" class="btn-secondary btn-view-detail" data-id="${item.id}" title="${t('pqr.view_details', 'Ver detalle')}" aria-label="${t('pac_notif.view_detail_aria', 'Ver detalle de la notificación')}">
            <span class="material-symbols-outlined" aria-hidden="true">visibility</span>
            ${t('pqr.view_details', 'Ver detalle')}
          </button>
          ${!item.leida ? `
          <button type="button" class="btn-icon btn-mark-read" data-id="${item.id}" title="${t('pac_notif.mark_read', 'Marcar como leída')}" aria-label="${t('pac_notif.mark_read', 'Marcar como leída')}">
            <span class="material-symbols-outlined" style="font-size:1.1rem;">done</span>
          </button>` : ''}
          ${item.idNotificacion ? `<button type="button" class="btn-icon btn-delete-notif" data-id="${escapeHtml(item.id)}" title="${t('pac_notif.delete', 'Eliminar')}" aria-label="${t('pac_notif.delete_aria', 'Eliminar notificación')}">
            <span class="material-symbols-outlined" style="font-size:1.1rem; color:var(--danger, #ef4444);">delete</span>
          </button>` : ''}
        </div>
      </div>
    </div>
    ${!item.leida ? `<span class="notification-card__dot" aria-label="${t('pac_notif.unread_dot_aria', 'No leída')}" role="status"></span>` : ''}
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
  if (countLabel) countLabel.textContent = filteredData.length === 1
    ? `${filteredData.length} ${t('pac_notif.count_result', 'resultado')}`
    : `${filteredData.length} ${t('pac_notif.count_results', 'resultados')}`;
  
  const markAllButton = safeGetElement('btnMarkAllRead');
  if (markAllButton) markAllButton.disabled = notificationsList.every(item => item.leida);

  if (emptyStateElement) emptyStateElement.hidden = filteredData.length > 0;
  if (!filteredData.length) {
    const emptyText = safeGetElement('notificationEmptyText');
    const hasFilters = !!safeGetElement('searchInput')?.value.trim() || currentFilterType !== 'all';
    if (emptyText) emptyText.textContent = hasFilters
      ? t('pac_notif.empty_matches', 'Ninguna notificación coincide con los filtros.')
      : showOnlyUnreadState
        ? t('pac_notif.empty_unread', 'Estás al día. Consulta Todas para ver el historial.')
        : t('pac_notif.empty_history', 'Todavía no tienes notificaciones.');
    return;
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

  safeGetElement('modalNotifTitle').textContent = notif.titulo || t('notifications.modal_detail_title', 'Detalle de Notificación');
  safeGetElement('modalNotifDesc').textContent = notif.desc || '';
  safeGetElement('modalNotifTime').textContent = `${formatRelativeTime(notif.time)} (${new Date(notif.time).toLocaleString()})`;
  safeGetElement('modalNotifCanal').textContent = notif.canal || t('notifications.channel_inapp', 'Interno (In-App)');
  
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
      safeGetElement('notificationActionLabel').textContent = notif.idCita
        ? t('notifications.view_appointment', 'Ver cita')
        : t('notifications.go_to_section', 'Ir a la sección');
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
  } else {
    document.querySelector('[data-read-filter][aria-pressed="true"]')?.focus();
  }
  lastActiveTriggerElement = null;
};

const handleNotificationCardAction = async (event) => {
  const viewButton = event.target.closest('.btn-view-detail');
  const markButton = event.target.closest('.btn-mark-read');
  const deleteButton = event.target.closest('.btn-delete-notif');
  const cardElement = event.target.closest('.notification-card');
  if (!cardElement) return;

  const notificationItem = notificationsList.find(item => String(item.id) === cardElement.dataset.id);
  if (!notificationItem) return;

  if (viewButton || (!markButton && !deleteButton)) {
    openModalDetail(notificationItem, viewButton || cardElement);
    if (!notificationItem.leida) {
      try {
        await markReadOnServer(notificationItem);
        notificationItem.leida = true;
        notificationItem.badge = 'read';
        renderNotificationsList();
      } catch (err) {
        if (window.ToastService) window.ToastService.error(err.message || t('pac_notif.toast_mark_read_error', 'No se pudo marcar la notificación como leída'));
      }
    }
    return;
  }

  if (markButton) {
    event.stopPropagation();
    try {
      await markReadOnServer(notificationItem);
      notificationItem.leida = true;
      notificationItem.badge = 'read';
      renderNotificationsList();
      if (window.ToastService) window.ToastService.success(t('pac_notif.toast_mark_read_success', 'Notificación marcada como leída'));
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
    return;
  }

  if (deleteButton) {
    event.stopPropagation();
    try {
      await deleteOnServer(notificationItem);
      notificationsList = notificationsList.filter(item => item !== notificationItem);
      renderNotificationsList();
      if (window.ToastService) window.ToastService.success(t('pac_notif.toast_deleted', 'Notificación eliminada'));
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
  }
};

const markAllNotificationsAsRead = async () => {
  const unreadItems = notificationsList.filter(item => !item.leida);
  if (!unreadItems.length) {
    if (window.ToastService) window.ToastService.success(t('pac_notif.toast_no_unread', 'No hay notificaciones sin leer'));
    return;
  }

  // Las notificaciones persistidas se marcan en una sola petición (batch);
  // las derivadas de citas (sin idNotificacion) usan su endpoint individual.
  const dbIds = unreadItems.filter(item => item.idNotificacion).map(item => item.idNotificacion);
  const citaOnlyItems = unreadItems.filter(item => !item.idNotificacion);

  try {
    if (dbIds.length) {
      await markAllReadOnServer(dbIds);
      unreadItems
        .filter(item => item.idNotificacion)
        .forEach(item => { item.leida = true; item.badge = 'read'; });
    }

    const citaResults = await Promise.all(citaOnlyItems.map(async item => {
      try {
        await markReadOnServer(item);
        item.leida = true;
        item.badge = 'read';
        return true;
      } catch {
        return false;
      }
    }));

    renderNotificationsList();
    if (citaResults.every(Boolean)) {
      if (window.ToastService) window.ToastService.success(t('pac_notif.toast_all_read', 'Todas las notificaciones marcadas como leídas'));
    } else if (window.ToastService) {
      window.ToastService.error(t('pac_notif.toast_some_failed', 'Algunas notificaciones no se pudieron marcar como leídas'));
    }
  } catch (error) {
    if (window.ToastService) window.ToastService.error(error.message);
  }
};

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
  
  const modeButtons = document.querySelectorAll('[data-read-filter]');
  modeButtons.forEach(button => button.addEventListener('click', () => {
    showOnlyUnreadState = button.dataset.readFilter === 'unread';
    modeButtons.forEach(item => {
      const active = item === button;
      item.setAttribute('aria-pressed', String(active));
      item.classList.toggle('btn-primary', active);
      item.classList.toggle('btn-secondary', !active);
    });
    renderNotificationsList();
  }));
  safeGetElement('filterNotificationType')?.addEventListener('change', event => {
    currentFilterType = event.target.value;
    renderNotificationsList();
  });

  const searchInputElement = safeGetElement('searchInput');
  if (searchInputElement) {
    const debouncedRenderHandler = debounce(renderNotificationsList, DEBOUNCE_DELAY_MS);
    searchInputElement.addEventListener('input', debouncedRenderHandler);
  }
  
  safeGetElement('btnMarkAllRead')?.addEventListener('click', markAllNotificationsAsRead);

  fetchNotificationsApi();
};

document.addEventListener('DOMContentLoaded', initializeNotificationsModule);
