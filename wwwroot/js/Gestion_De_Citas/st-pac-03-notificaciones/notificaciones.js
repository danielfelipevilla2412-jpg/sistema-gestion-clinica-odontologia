/* ============================================
SmileTrack — Notificaciones Paciente (st-pac-03-notificaciones)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Controla la carga de notificaciones del paciente, acciones de marcar como leídas, visualización de detalle en modal, archivado/eliminación y filtrado omnicanal de alertas.
============================================ */

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) {
    console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  }
  return el;
};

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

const formatTiempoRelativo = (iso) => {
  if (!iso) return '';
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return String(iso);
  const diffMs = Date.now() - fecha.getTime();
  const diffHoras = diffMs / (1000 * 60 * 60);
  if (diffHoras < 0) {
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

const rawData = window.smiletrackNotificacionesData?.notificaciones || [];
let notificaciones = rawData.map(n => ({
  ...n,
  displayTime: formatTiempoRelativo(n.time)
}));

let currentFilter = 'all';
let showOnlyUnread = false;

const badgeClass = (badge) => {
  const map = { 'pending': 'badge-agendada', 'new': 'badge-completada', 'read': 'badge-cancelada' };
  return map[badge] || 'badge-cancelada';
};

const badgeLabel = (badge) => {
  const map = { 'pending': 'Pendiente', 'new': 'Nueva', 'read': 'Leída' };
  return map[badge] || 'Leída';
};

const getIconByType = (tipo) => {
  const map = {
    'reminder': 'notifications',
    'confirmed': 'check_circle',
    'cancelled': 'cancel',
    'rescheduled': 'event_repeat',
    'waitlist': 'hourglass_top',
    'invoice': 'payments',
    'message': 'message'
  };
  return map[tipo] || 'notifications';
};

const getFiltered = () => {
  const searchInput = safeGetElement('searchInput');
  const q = searchInput?.value.toLowerCase().trim() || '';
  
  return notificaciones.filter(n => {
    const matchQ = !q || (
      (n.titulo || '').toLowerCase().includes(q) ||
      (n.desc || '').toLowerCase().includes(q)
    );
    const matchFilter = currentFilter === 'all' || n.tipo === currentFilter;
    const matchUnread = !showOnlyUnread || !n.leida;
    return matchQ && matchFilter && matchUnread;
  });
};

const createNotificationItem = (item) => {
  const li = document.createElement('li');
  li.className = `notification-card${item.leida ? ' notification-card--read' : ''}`;
  li.dataset.type = item.tipo;
  li.dataset.id = item.id;
  
  if (!item.leida) {
    li.setAttribute('role', 'article');
    li.setAttribute('aria-label', `Notificación sin leer: ${item.titulo}`);
    li.setAttribute('tabindex', '0');
  }
  
  li.innerHTML = `
    <div class="notification-card__icon" aria-hidden="true">
      <span class="material-symbols-outlined">${escapeHtml(getIconByType(item.tipo))}</span>
    </div>
    <div class="notification-card__body">
      <div class="notification-card__header">
        <h3 class="notification-card__title">${escapeHtml(item.titulo)}</h3>
        <span class="badge ${escapeHtml(badgeClass(item.badge))}" aria-label="Estado: ${escapeHtml(badgeLabel(item.badge))}">${escapeHtml(badgeLabel(item.badge))}</span>
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
  
  return li;
};

const renderNotifications = () => {
  const data = getFiltered();
  const container = safeGetElement('notificationsList');
  const emptyState = safeGetElement('emptyState');
  
  if (!container) return;
  
  container.replaceChildren();
  
  const countLabel = safeGetElement('countLabel');
  if (countLabel) countLabel.textContent = `${data.length} resultado${data.length !== 1 ? 's' : ''}`;
  
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

const deleteOnServer = async (id) => {
  const response = await fetch(`/api/notificaciones/${id}`, {
    method: 'DELETE',
    credentials: 'same-origin',
    headers: { 'Accept': 'application/json' }
  });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok || payload.success === false) {
    throw new Error(payload.message || 'No fue posible eliminar la notificación.');
  }
};

let lastActiveTrigger = null;

const openModalDetail = (notif, triggerEl) => {
  lastActiveTrigger = triggerEl || document.activeElement;
  const modal = safeGetElement('modalNotifDetail');
  if (!modal) return;

  safeGetElement('modalNotifTitle').textContent = notif.titulo || 'Detalle de Notificación';
  safeGetElement('modalNotifDesc').textContent = notif.desc || '';
  safeGetElement('modalNotifTime').textContent = `${formatTiempoRelativo(notif.time)} (${new Date(notif.time).toLocaleString()})`;
  safeGetElement('modalNotifCanal').textContent = notif.canal || 'Interno (In-App)';
  
  const iconBadge = safeGetElement('modalNotifIcon');
  if (iconBadge) iconBadge.textContent = getIconByType(notif.tipo);

  const badgeEl = safeGetElement('modalNotifBadge');
  if (badgeEl) {
    badgeEl.className = `badge ${badgeClass(notif.badge)}`;
    badgeEl.textContent = badgeLabel(notif.badge);
  }

  const actionBtn = safeGetElement('btnModalAction');
  if (actionBtn) {
    if (notif.urlAccion) {
      actionBtn.href = notif.urlAccion;
      actionBtn.style.display = 'inline-flex';
    } else {
      actionBtn.style.display = 'none';
    }
  }

  modal.classList.add('open');
  modal.setAttribute('aria-hidden', 'false');
  modal.removeAttribute('inert');
  document.body.style.overflow = 'hidden';

  const closeBtn = safeGetElement('btnModalClose');
  if (closeBtn) closeBtn.focus();
};

const closeModalDetail = () => {
  const modal = safeGetElement('modalNotifDetail');
  if (!modal) return;
  modal.classList.remove('open');
  modal.setAttribute('aria-hidden', 'true');
  modal.setAttribute('inert', '');
  document.body.style.overflow = '';
  if (lastActiveTrigger && typeof lastActiveTrigger.focus === 'function' && document.contains(lastActiveTrigger)) {
    lastActiveTrigger.focus();
  }
  lastActiveTrigger = null;
};

const handleNotificationClick = async (e) => {
  const viewBtn = e.target.closest('.btn-view-detail');
  const markBtn = e.target.closest('.btn-mark-read');
  const deleteBtn = e.target.closest('.btn-delete-notif');
  const card = e.target.closest('.notification-card');
  if (!card) return;

  const id = parseInt(card.dataset.id, 10);
  if (isNaN(id)) return;
  const notif = notificaciones.find(n => n.id === id);
  if (!notif) return;

  if (viewBtn || (!markBtn && !deleteBtn)) {
    openModalDetail(notif, viewBtn || card);
    if (!notif.leida) {
      try {
        await markReadOnServer([id]);
        notif.leida = true;
        notif.badge = 'read';
        renderNotifications();
      } catch (err) {
        console.warn('Error al marcar leída:', err);
      }
    }
    return;
  }

  if (markBtn) {
    e.stopPropagation();
    try {
      await markReadOnServer([id]);
      notif.leida = true;
      notif.badge = 'read';
      renderNotifications();
      if (window.ToastService) window.ToastService.success('Notificación marcada como leída');
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
    return;
  }

  if (deleteBtn) {
    e.stopPropagation();
    try {
      await deleteOnServer(id);
      notificaciones = notificaciones.filter(n => n.id !== id);
      renderNotifications();
      if (window.ToastService) window.ToastService.success('Notificación eliminada');
    } catch (error) {
      if (window.ToastService) window.ToastService.error(error.message);
    }
  }
};

const markAllAsRead = async () => {
  const unreadList = notificaciones.filter(n => !n.leida);
  if (!unreadList.length) {
    if (window.ToastService) window.ToastService.success('No hay notificaciones sin leer');
    return;
  }
  
  try {
    await markReadOnServer(unreadList.map(n => n.id));
    notificaciones.forEach(n => { n.leida = true; n.badge = 'read'; });
    renderNotifications();
    if (window.ToastService) window.ToastService.success('Todas las notificaciones marcadas como leídas');
  } catch (error) {
    if (window.ToastService) window.ToastService.error(error.message);
  }
};

const handleChipClick = (chip) => {
  document.querySelectorAll('.chip').forEach(c => {
    c.classList.remove('chip--active');
    c.setAttribute('aria-pressed', 'false');
  });
  
  chip.classList.add('chip--active');
  chip.setAttribute('aria-pressed', 'true');
  
  currentFilter = chip.dataset.filter;
  renderNotifications();
};

const toggleOnlyUnread = () => {
  showOnlyUnread = !showOnlyUnread;
  const btn = safeGetElement('btnToggleUnread');
  const lbl = safeGetElement('lblToggleUnread');
  if (btn) {
    btn.setAttribute('aria-pressed', String(showOnlyUnread));
    btn.classList.toggle('btn-primary', showOnlyUnread);
    btn.classList.toggle('btn-secondary', !showOnlyUnread);
  }
  if (lbl) lbl.textContent = showOnlyUnread ? 'Ver todas' : 'Solo no leídas';
  renderNotifications();
};

const fetchNotificationsApi = async () => {
  try {
    const res = await fetch('/api/notificaciones', {
      headers: { 'Accept': 'application/json' },
      credentials: 'same-origin'
    });
    if (!res.ok) return;
    const payload = await res.json();
    if (payload.success && payload.data && Array.isArray(payload.data.notificaciones)) {
      notificaciones = payload.data.notificaciones.map(n => ({
        ...n,
        displayTime: formatTiempoRelativo(n.time)
      }));
      renderNotifications();
    }
  } catch (err) {
    console.warn('[SmileTrack] Fallback a datos SSR:', err);
  }
};

const initModalEvents = () => {
  const modal = safeGetElement('modalNotifDetail');
  safeGetElement('modalNotifClose')?.addEventListener('click', closeModalDetail);
  safeGetElement('btnModalClose')?.addEventListener('click', closeModalDetail);
  modal?.addEventListener('click', (e) => {
    if (e.target === modal) closeModalDetail();
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal?.classList.contains('open')) {
      e.preventDefault();
      closeModalDetail();
    }
  });
};

const init = () => {
  renderNotifications();
  initModalEvents();
  
  const notificationsList = safeGetElement('notificationsList');
  if (notificationsList) {
    notificationsList.addEventListener('click', handleNotificationClick);
    notificationsList.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleNotificationClick(e);
      }
    });
  }
  
  document.querySelectorAll('.chip').forEach(chip => {
    chip.addEventListener('click', () => handleChipClick(chip));
  });
  
  const searchEl = safeGetElement('searchInput');
  if (searchEl) {
    const debouncedRender = debounce(renderNotifications, 180);
    searchEl.addEventListener('input', debouncedRender);
  }
  
  safeGetElement('btnMarkAllRead')?.addEventListener('click', markAllAsRead);
  safeGetElement('btnToggleUnread')?.addEventListener('click', toggleOnlyUnread);

  fetchNotificationsApi();
};

document.addEventListener('DOMContentLoaded', init);
