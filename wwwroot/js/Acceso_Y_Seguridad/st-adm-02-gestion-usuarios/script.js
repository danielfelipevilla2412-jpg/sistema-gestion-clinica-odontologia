/**
 * SMILETRACK — GESTIÓN USUARIOS (usuarios.js)
 * API-ready + Accesibilidad + Persistencia fallback
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

// Obtiene elemento del DOM con manejo seguro de null
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Reduce llamadas a función en eventos frecuentes
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// Muestra notificación temporal con auto-cierre
const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;

  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS DE EJEMPLO (Fallback si API falla)
// ═══════════════════════════════════════════════════════════════════
const SAMPLE_USERS = [
  { id: 1, name: 'Administrador', initials: 'AD', email: 'admin@smiletrack.co', role: 'Administrador', status: 'Activo', lastAccess: '2026-05-27T10:00:00', color: 'purple' },
  { id: 2, name: 'María Rodríguez', initials: 'MR', email: 'mrodriguez@smiletrack.co', role: 'Recepcionista', status: 'Activo', lastAccess: '2026-05-27T08:00:00', color: 'orange' },
  { id: 3, name: 'Dr. Carlos Méndez', initials: 'CM', email: 'cmendez@smiletrack.co', role: 'Profesional', status: 'Activo', lastAccess: '2026-05-27T07:00:00', color: 'green' },
  { id: 4, name: 'Sara Jiménez', initials: 'SJ', email: 'sjimenez@smiletrack.co', role: 'Auxiliar', status: 'Activo', lastAccess: '2026-05-24T14:00:00', color: 'pink' },
  { id: 5, name: 'Juan Pérez', initials: 'JP', email: 'juan@correo.com', role: 'Paciente', status: 'Inactivo', lastAccess: null, color: 'blue' },
  { id: 6, name: 'Ana Torres', initials: 'AT', email: 'atorres@smiletrack.co', role: 'Profesional', status: 'Bloqueado', lastAccess: '2026-05-20T09:00:00', color: 'red' },
  { id: 7, name: 'Luis Herrera', initials: 'LH', email: 'lherrera@smiletrack.co', role: 'Administrador', status: 'Activo', lastAccess: '2026-05-26T16:00:00', color: 'purple' },
  { id: 8, name: 'Carmen López', initials: 'CL', email: 'clopez@smiletrack.co', role: 'Recepcionista', status: 'Activo', lastAccess: '2026-05-27T09:30:00', color: 'orange' },
  { id: 9, name: 'Pedro García', initials: 'PG', email: 'pgarcia@smiletrack.co', role: 'Auxiliar', status: 'Inactivo', lastAccess: '2026-05-15T11:00:00', color: 'pink' },
  { id: 10, name: 'Laura Sánchez', initials: 'LS', email: 'lsanchez@smiletrack.co', role: 'Profesional', status: 'Activo', lastAccess: '2026-05-27T06:00:00', color: 'green' },
  { id: 11, name: 'Miguel Torres', initials: 'MT', email: 'mtorres@smiletrack.co', role: 'Paciente', status: 'Activo', lastAccess: '2026-05-26T18:00:00', color: 'blue' },
  { id: 12, name: 'Sofía Martínez', initials: 'SM', email: 'smartinez@smiletrack.co', role: 'Administrador', status: 'Bloqueado', lastAccess: '2026-05-10T10:00:00', color: 'purple' },
  { id: 13, name: 'Andrés Gómez', initials: 'AG', email: 'agomez@smiletrack.co', role: 'Profesional', status: 'Activo', lastAccess: '2026-05-27T05:00:00', color: 'green' },
  { id: 14, name: 'Isabel Ruiz', initials: 'IR', email: 'iruiz@smiletrack.co', role: 'Recepcionista', status: 'Activo', lastAccess: '2026-05-27T08:30:00', color: 'orange' },
  { id: 15, name: 'Roberto Díaz', initials: 'RD', email: 'rdiaz@smiletrack.co', role: 'Auxiliar', status: 'Activo', lastAccess: '2026-05-26T15:00:00', color: 'pink' },
];

let users = Array.isArray(window.RAZOR_USERS) && window.RAZOR_USERS.length > 0 ? window.RAZOR_USERS : [...SAMPLE_USERS];
let searchQuery = '';
let selectedRole = '';
let selectedStatus = '';
let currentPage = 1;
const itemsPerPage = 5;

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════

// Anima contador numérico
const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

// Obtiene clase CSS para badge de rol
const getRoleBadgeClass = (role) => {
  const map = {
    'Administrador': 'admin',
    'Recepcionista': 'recep',
    'Profesional': 'prof',
    'Auxiliar': 'aux',
    'Paciente': 'paciente',
  };
  return map[role] || 'paciente';
};

// Obtiene clase CSS para badge de estado
const getStatusBadgeClass = (status) => {
  const map = {
    'Activo': 'activo',
    'Inactivo': 'inactivo',
    'Bloqueado': 'bloqueado',
  };
  return map[status] || 'inactivo';
};

// Formatea fecha para último acceso
const fmtLastAccess = (iso) => {
  if (!iso) return 'Nunca';
  const now = new Date();
  const date = new Date(iso);
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);
  
  if (diffMins < 60) return `Hace ${diffMins} min`;
  if (diffHours < 24) return `Hace ${diffHours} h`;
  if (diffDays < 7) return `Hace ${diffDays} d`;
  return date.toLocaleDateString('es-ES', { day: 'numeric', month: 'short' });
};

// Renderiza tabla de usuarios con accesibilidad
const renderTable = (data) => {
  const tbody = safeGetElement('usersTbody');
  if (!tbody) return;
  
  // Filtrar datos
  const filtered = data.filter(u =>
    (!searchQuery || u.name.toLowerCase().includes(searchQuery.toLowerCase()) || u.email.toLowerCase().includes(searchQuery.toLowerCase())) &&
    (!selectedRole || u.role === selectedRole) &&
    (!selectedStatus || u.status === selectedStatus)
  );
  
  // Paginar
  const totalPages = Math.ceil(filtered.length / itemsPerPage) || 1;
  if (currentPage > totalPages) currentPage = totalPages;
  
  const startIdx = (currentPage - 1) * itemsPerPage;
  const paginated = filtered.slice(startIdx, startIdx + itemsPerPage);
  
  tbody.innerHTML = '';
  
  if (!paginated.length) {
    tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:32px;color:var(--text-muted);">No se encontraron usuarios con los filtros aplicados.</td></tr>`;
    updatePagination(0, 0);
    return;
  }
  
  paginated.forEach(u => {
    const tr = document.createElement('tr');
    tr.setAttribute('role', 'row');
    
    const roleClass = getRoleBadgeClass(u.role);
    const statusClass = getStatusBadgeClass(u.status);
    
    tr.innerHTML = `
      <td class="td-usuario">
        <div class="u-avatar" style="background:var(--${u.color}-500)" aria-hidden="true">${u.initials}</div>
        <span class="u-name">${u.name}</span>
      </td>
      <td>${u.email}</td>
      <td><span class="badge-role ${roleClass}">${u.role}</span></td>
      <td><span class="badge-status ${statusClass}" role="status" aria-label="Estado: ${u.status}">${u.status}</span></td>
      <td><time datetime="${u.lastAccess || ''}">${fmtLastAccess(u.lastAccess)}</time></td>
      <td>
        <div class="actions-cell">
          <button class="btn-icon view" title="Ver detalles" aria-label="Ver detalles de ${u.name}" onclick="viewUser(${u.id})">👁️</button>
          <button class="btn-icon edit" title="Editar usuario" aria-label="Editar ${u.name}" onclick="editUser(${u.id})">✏️</button>
          <button class="btn-icon lock" title="${u.status === 'Bloqueado' ? 'Desbloquear' : 'Bloquear'}" aria-label="${u.status === 'Bloqueado' ? 'Desbloquear' : 'Bloquear'} ${u.name}" onclick="toggleStatus(${u.id})">${u.status === 'Bloqueado' ? '🔓' : '🔒'}</button>
        </div>
      </td>
    `;
    
    tbody.appendChild(tr);
  });
  
  // Actualizar paginación
  updatePagination(filtered.length, paginated.length);
};

// Actualiza contadores de estadísticas
const updateStats = () => {
  const total = users.length;
  const active = users.filter(u => u.status === 'Activo').length;
  const blocked = users.filter(u => u.status === 'Bloqueado').length;
  const inactive = users.filter(u => u.status === 'Inactivo').length;
  
  animateCounter(safeGetElement('statTotal'), total);
  animateCounter(safeGetElement('statActive'), active);
  animateCounter(safeGetElement('statBlocked'), blocked);
  animateCounter(safeGetElement('statInactive'), inactive);
};

// Actualiza botones de paginación
const updatePagination = (total, count) => {
  const info = safeGetElement('paginationInfo');
  const buttons = safeGetElement('paginationButtons');
  if (!info || !buttons) return;
  
  const totalPages = Math.ceil(total / itemsPerPage) || 1;
  const start = total === 0 ? 0 : (currentPage - 1) * itemsPerPage + 1;
  const end = Math.min(currentPage * itemsPerPage, total);
  
  info.textContent = `Mostrando ${start}-${end} de ${total} usuarios`;
  
  buttons.innerHTML = '';
  
  // Botón anterior
  const btnPrev = document.createElement('button');
  btnPrev.textContent = '«';
  btnPrev.setAttribute('aria-label', 'Página anterior');
  btnPrev.disabled = currentPage === 1;
  btnPrev.className = currentPage === 1 ? '' : '';
  btnPrev.addEventListener('click', () => { if (currentPage > 1) { currentPage--; renderTable(users); } });
  buttons.appendChild(btnPrev);
  
  // Botones numéricos
  for (let i = 1; i <= totalPages; i++) {
    const btn = document.createElement('button');
    btn.textContent = i;
    btn.setAttribute('aria-label', `Ir a página ${i}`);
    btn.setAttribute('aria-current', i === currentPage ? 'page' : 'false');
    if (i === currentPage) btn.classList.add('active');
    btn.addEventListener('click', () => { currentPage = i; renderTable(users); });
    buttons.appendChild(btn);
  }
  
  // Botón siguiente
  const btnNext = document.createElement('button');
  btnNext.textContent = '»';
  btnNext.setAttribute('aria-label', 'Página siguiente');
  btnNext.disabled = currentPage === totalPages;
  btnNext.addEventListener('click', () => { if (currentPage < totalPages) { currentPage++; renderTable(users); } });
  buttons.appendChild(btnNext);
};

// ═══════════════════════════════════════════════════════════════════
//  ACCIONES DE USUARIO
// ═══════════════════════════════════════════════════════════════════

// Ver detalles de usuario (simulado)
window.viewUser = (id) => {
  const user = users.find(u => u.id === id);
  if (user) showToast(`👁️ Visualizando: ${user.name}`);
};

// Editar usuario (simulado)
window.editUser = (id) => {
  const user = users.find(u => u.id === id);
  if (user) showToast(`✏️ Editando: ${user.name}`);
};

// Alternar estado de usuario (bloquear/desbloquear)
window.toggleStatus = (id) => {
  const user = users.find(u => u.id === id);
  if (!user) return;
  
  if (user.status === 'Bloqueado') {
    user.status = 'Activo';
    showToast(`🔓 ${user.name} desbloqueado`);
  } else {
    user.status = 'Bloqueado';
    showToast(`🔒 ${user.name} bloqueado`);
  }
  
  updateStats();
  renderTable(users);
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL: CREAR USUARIO
// ═══════════════════════════════════════════════════════════════════

// Abre modal de crear usuario
const openModal = () => {
  const modal = safeGetElement('modalAddUser');
  if (modal) {
    modal.classList.add('open');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    
    // Enfocar primer input
    const firstInput = modal.querySelector('input');
    if (firstInput) firstInput.focus();
    
    // Bloquear scroll del body
    document.body.style.overflow = 'hidden';
  }
};

// Cierra modal de crear usuario
const closeModal = () => {
  const modal = safeGetElement('modalAddUser');
  if (modal) {
    modal.classList.remove('open');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    
    // Restaurar scroll
    document.body.style.overflow = '';
    
    // Resetear formulario
    const form = safeGetElement('formAddUser');
    if (form) form.reset();
  }
};

// Crea nuevo usuario
const createUser = (e) => {
  e.preventDefault();
  
  const name = safeGetElement('userName')?.value.trim();
  const email = safeGetElement('userEmail')?.value.trim();
  const role = safeGetElement('userRole')?.value;
  const status = safeGetElement('userStatus')?.value;
  
  if (!name || !email || !role || !status) {
    showToast('⚠️ Completa todos los campos', 'warning');
    return;
  }
  
  // Generar iniciales
  const initials = name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
  
  // Color aleatorio para avatar
  const colors = ['purple', 'green', 'orange', 'pink', 'blue', 'red'];
  const color = colors[Math.floor(Math.random() * colors.length)];
  
  // Nuevo usuario
  const newUser = {
    id: users.length + 1,
    name,
    initials,
    email,
    role,
    status,
    lastAccess: null,
    color,
  };
  
  // Agregar al inicio
  users.unshift(newUser);
  
  // Actualizar UI
  closeModal();
  updateStats();
  renderTable(users);
  
  showToast(`✅ ${name} creado exitosamente`);
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS (Listas para conectar al backend C#)
// ═══════════════════════════════════════════════════════════════════

// Obtiene lista de usuarios desde API
async function fetchUsers() {
  try {
    // En producción: fetch real a API
    // const res = await fetch(`${API_BASE}/admin/users`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    
    // Simulación con fallback
    return SAMPLE_USERS;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_USERS;
  }
}

// Crea usuario en API
async function createUserAPI(userData) {
  try {
    // En producción: POST real a API
    // const res = await fetch(`${API_BASE}/admin/users`, {
    //   method: 'POST',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(userData),
    // });
    // if (!res.ok) throw new Error('Create failed');
    // return await res.json();
    
    // Simulación
    return { success: true, id: Date.now() };
  } catch (error) {
    console.warn('Error creando usuario en API:', error);
    return null;
  }
}

// Actualiza usuario en API
async function updateUserAPI(id, updates) {
  try {
    // En producción: PATCH real a API
    // await fetch(`${API_BASE}/admin/users/${id}`, {
    //   method: 'PATCH',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(updates),
    // });
    
    // Simulación
    return true;
  } catch (error) {
    console.warn('Error actualizando usuario en API:', error);
    return false;
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

// Inicializa sidebar móvil con gestión de foco y ARIA
const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');

  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);
    
    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  // ✅ Navegación: cerrar menú en móvil, SIN bloquear enlaces
  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMenu(false);
      }
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// Inicializa filtros de búsqueda
const initFilters = () => {
  const searchInput = safeGetElement('searchUser');
  const filterRole = safeGetElement('filterRole');
  const filterStatus = safeGetElement('filterStatus');
  
  searchInput?.addEventListener('input', debounce((e) => {
    searchQuery = e.target.value;
    currentPage = 1;
    renderTable(users);
  }, 250));
  
  filterRole?.addEventListener('change', (e) => {
    selectedRole = e.target.value;
    currentPage = 1;
    renderTable(users);
  });
  
  filterStatus?.addEventListener('change', (e) => {
    selectedStatus = e.target.value;
    currentPage = 1;
    renderTable(users);
  });
};

// Inicializa modal de crear usuario
const initModal = () => {
  const btnAdd = safeGetElement('btnAddUser');
  const modalClose = safeGetElement('modalClose');
  const modalCancel = safeGetElement('modalCancel');
  const form = safeGetElement('formAddUser');
  const modal = safeGetElement('modalAddUser');
  
  // Abrir modal
  btnAdd?.addEventListener('click', openModal);
  
  // Cerrar modal
  modalClose?.addEventListener('click', closeModal);
  modalCancel?.addEventListener('click', closeModal);
  modal?.addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closeModal();
  });
  
  // Submit del formulario
  form?.addEventListener('submit', createUser);
  
  // Soporte para teclado en modal
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal?.classList.contains('open')) {
      e.preventDefault();
      closeModal();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Inicializar componentes de UI
  initSidebar();
  initFilters();
  initModal();
  
  // Cargar datos iniciales
  users = await fetchUsers();
  
  // Renderizar tabla y estadísticas
  updateStats();
  renderTable(users);
  
  // Limpieza al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);