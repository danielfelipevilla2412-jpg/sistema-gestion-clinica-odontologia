/**
 * SMILETRACK — Servicios Prestados (script.js)
 * Sidebar móvil accesible + Persistencia + API-ready
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';
const STORAGE_KEY = 'smiletrack_services';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const $ = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const showToast = (msg, type = 'success') => {
  const toast = $('toast');
  if (!toast) return;
  toast.textContent = msg;
  toast.className = `fixed bottom-5 left-1/2 -translate-x-1/2 px-5 py-3 rounded-lg shadow-lg text-sm font-body transition-all z-50 ${
    type === 'error' ? 'bg-red-600' : type === 'warning' ? 'bg-orange-500' : 'bg-gray-800'
  } text-white show`;
  setTimeout(() => toast.classList.remove('show'), 3000);
};

const fmtMoney = (n) => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(n);

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA
// ═══════════════════════════════════════════════════════════════════

const storage = {
  load: () => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  },
  save: (data) => {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(data)); return true; }
    catch (e) { console.error('Error guardando:', e); return false; }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS DE EJEMPLO
// ═══════════════════════════════════════════════════════════════════

const defaultData = {
  services: [
    { id: 1, name: 'Control de tratamiento', description: 'Pieza 23 – Seguimiento endodoncia', category: 'preventiva', historicalPrice: 30000, currentPrice: 30000, status: 'realizado' },
    { id: 2, name: 'Control general', description: 'Valoración y diagnóstico', category: 'preventiva', historicalPrice: 45000, currentPrice: 50000, status: 'realizado' }
  ],
  summary: { subtotal: 80000, discount: 5000, tax: 0, total: 75000 }
};

// ═══════════════════════════════════════════════════════════════════
//  ESTADO
// ═══════════════════════════════════════════════════════════════════

let data = storage.load() || defaultData;

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL (ACCESIBLE)
// ═══════════════════════════════════════════════════════════════════

const initSidebar = () => {
  const sidebar = $('sidebar');
  const backdrop = $('sidebar-backdrop');
  const toggle = $('menu-toggle');
  
  if (!sidebar || !backdrop || !toggle) return;
  
  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    backdrop.classList.toggle('hidden', !show);
    toggle.setAttribute('aria-expanded', show);
    if (show) sidebar.querySelector('.nav-item')?.focus();
    else toggle.focus();
  };
  
  toggle.addEventListener('click', () => toggleMenu(true));
  backdrop.addEventListener('click', () => toggleMenu(false));
  sidebar.querySelectorAll('.nav-item').forEach(link => {
    link.addEventListener('click', () => { if (window.innerWidth < 1024) toggleMenu(false); });
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
  window.addEventListener('resize', () => { if (window.innerWidth >= 1024) toggleMenu(false); });
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: RESUMEN DE FACTURACIÓN
// ═══════════════════════════════════════════════════════════════════

const renderSummary = () => {
  const { subtotal, discount, tax, total } = data.summary;
  
  const elSubtotal = $('subtotal');
  const elDiscount = $('discount');
  const elTax = $('tax');
  const elTotal = $('total');
  
  if (elSubtotal) elSubtotal.textContent = fmtMoney(subtotal);
  if (elDiscount) elDiscount.textContent = `-${fmtMoney(discount)}`;
  if (elTax) elTax.textContent = fmtMoney(tax);
  if (elTotal) elTotal.textContent = fmtMoney(total);
};

// ═══════════════════════════════════════════════════════════════════
//  FEEDBACK DE ESTADO
// ═══════════════════════════════════════════════════════════════════

const showStatus = (msg) => {
  const banner = $('statusBanner');
  const text = $('statusText');
  if (!banner || !text) return;
  
  text.textContent = `${msg} - ${new Date().toLocaleString('es-ES')}`;
  banner.classList.remove('hidden');
  
  setTimeout(() => banner.classList.add('hidden'), 5000);
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE EVENTOS
// ═══════════════════════════════════════════════════════════════════

const initAddService = () => {
  $('btnAddService')?.addEventListener('click', () => {
    showToast('📝 Funcionalidad de agregar servicio en desarrollo', 'warning');
  });
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS (Listas para backend C#)
// ═══════════════════════════════════════════════════════════════════

const fetchServices = async () => {
  try {
    // const res = await fetch(`${API_BASE}/odontologo/services`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    return storage.load() || defaultData;
  } catch (e) {
    console.warn('Fallback a datos locales:', e);
    return defaultData;
  }
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initAddService();
  
  data = await fetchServices();
  renderSummary();
  
  window.addEventListener('beforeunload', () => { /* Cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', init);