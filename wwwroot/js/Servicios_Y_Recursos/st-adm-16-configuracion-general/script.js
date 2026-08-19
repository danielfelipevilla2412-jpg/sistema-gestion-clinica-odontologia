/**
 * SMILETRACK — CONFIGURACIÓN GENERAL (script.js)
 * API-ready + Accesibilidad + Persistencia fallback
 * Toggles accesibles + Validación de formulario + Feedback visual
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

const fmtTimestamp = () => {
  const now = new Date();
  const meses = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
  let hours = now.getHours();
  const ampm = hours >= 12 ? 'PM' : 'AM';
  hours = hours % 12 || 12;
  const minutes = String(now.getMinutes()).padStart(2, '0');
  return `${now.getDate()} ${meses[now.getMonth()]} ${now.getFullYear()} - ${hours}:${minutes} ${ampm}`;
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON LOCALSTORAGE
// ═══════════════════════════════════════════════════════════════════

const configStorage = {
  key: 'smiletrack_config_admin',
  
  load: () => {
    const stored = localStorage.getItem(configStorage.key);
    if (stored) {
      try { return JSON.parse(stored); }
      catch (e) { console.warn('Error al cargar configuración, usando valores por defecto'); }
    }
    return {
      appointment: { duration: 30, open: '07:00', close: '18:00' },
      center: {
        name: 'SmileTrack Dental Clinic',
        nit: '901.482.350-4',
        address: 'Avenida de la Salud #45-12, Piso 4'
      },
      notifications: { email: true, sms: false, system: true }
    };
  },
  
  save: (data) => {
    try { localStorage.setItem(configStorage.key, JSON.stringify(data)); return true; }
    catch (e) { console.error('Error al guardar configuración:', e); return false; }
  },
  
  update: (section, updates) => {
    const data = configStorage.load();
    data[section] = { ...data[section], ...updates };
    return configStorage.save(data);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  DATOS Y ESTADO
// ═══════════════════════════════════════════════════════════════════

let config = configStorage.load();

// ═══════════════════════════════════════════════════════════════════
//  RENDER: CONFIGURACIÓN INICIAL
// ═══════════════════════════════════════════════════════════════════

const renderConfig = () => {
  if (safeGetElement('defaultDuration')) safeGetElement('defaultDuration').textContent = `${config.appointment.duration} min`;
  if (safeGetElement('openTime')) safeGetElement('openTime').textContent = fmtTime(config.appointment.open);
  if (safeGetElement('closeTime')) safeGetElement('closeTime').textContent = fmtTime(config.appointment.close);
  
  if (safeGetElement('centerName')) safeGetElement('centerName').value = config.center.name;
  if (safeGetElement('centerNIT')) safeGetElement('centerNIT').value = config.center.nit;
  if (safeGetElement('centerAddress')) safeGetElement('centerAddress').value = config.center.address;
  
  updateToggle('toggleEmail', config.notifications.email);
  updateToggle('toggleSMS', config.notifications.sms);
  updateToggle('toggleSystem', config.notifications.system);
};

const fmtTime = (time24) => {
  if (!time24) return '—';
  const [h, m] = time24.split(':');
  const hour = parseInt(h);
  const period = hour >= 12 ? 'PM' : 'AM';
  const displayHour = hour > 12 ? hour - 12 : (hour === 0 ? 12 : hour);
  return `${String(displayHour).padStart(2, '0')}:${m} ${period}`;
};

const updateToggle = (id, active) => {
  const toggle = safeGetElement(id);
  if (toggle) {
    toggle.setAttribute('aria-checked', active);
    toggle.classList.toggle('active', active);
  }
};

// ═══════════════════════════════════════════════════════════════════
//  TOGGLES DE NOTIFICACIONES — ACCESIBLES
// ═══════════════════════════════════════════════════════════════════

const initToggles = () => {
  const toggles = [
    { id: 'toggleEmail', key: 'email' },
    { id: 'toggleSMS', key: 'sms' },
    { id: 'toggleSystem', key: 'system' }
  ];
  
  toggles.forEach(({ id, key }) => {
    const toggle = safeGetElement(id);
    toggle?.addEventListener('click', () => {
      const isActive = toggle.getAttribute('aria-checked') === 'true';
      const newValue = !isActive;
      
      toggle.setAttribute('aria-checked', newValue);
      toggle.classList.toggle('active', newValue);
      
      configStorage.update('notifications', { [key]: newValue });
      config = configStorage.load();
      
      showToast(`🔔 Recordatorios por ${key === 'email' ? 'correo' : key === 'sms' ? 'SMS' : 'sistema'} ${newValue ? 'activados' : 'desactivados'}`);
    });
    
    toggle?.addEventListener('keydown', (e) => {
      if (['Enter', ' '].includes(e.key)) {
        e.preventDefault();
        toggle.click();
      }
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  EDICIÓN RÁPIDA DE CONFIGURACIÓN DE CITAS
// ═══════════════════════════════════════════════════════════════════

const initQuickEdit = () => {
  document.querySelectorAll('.btn-edit').forEach(btn => {
    btn.addEventListener('click', (e) => {
      const field = e.currentTarget.dataset.field;
      const current = config.appointment[field];
      const labels = { duration: 'Duración (min)', open: 'Hora de apertura (HH:MM)', close: 'Hora de cierre (HH:MM)' };
      const patterns = { duration: /^\d+$/, time: /^([01]?\d|2[0-3]):[0-5]\d$/ };
      
      const newValue = prompt(`Editar ${labels[field]}:`, field === 'duration' ? current : fmtTime(current));
      if (newValue === null) return;
      
      if (field === 'duration') {
        if (!patterns.duration.test(newValue) || parseInt(newValue) < 5 || parseInt(newValue) > 180) {
          showToast('⚠️ Duración debe ser entre 5 y 180 minutos', 'warning');
          return;
        }
        config.appointment[field] = parseInt(newValue);
      } else {
        if (!patterns.time.test(newValue)) {
          showToast('⚠️ Formato de hora inválido. Use HH:MM (24h)', 'warning');
          return;
        }
        config.appointment[field] = newValue;
      }
      
      configStorage.update('appointment', { [field]: config.appointment[field] });
      config = configStorage.load();
      renderConfig();
      showToast(`✅ ${labels[field]} actualizado a ${field === 'duration' ? `${newValue} min` : fmtTime(newValue)}`);
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FORMULARIO: DATOS DEL CENTRO — CON VALIDACIÓN
// ═══════════════════════════════════════════════════════════════════

const initCenterForm = () => {
  const form = safeGetElement('centerForm');
  const btnSave = safeGetElement('btnSaveChanges');
  const alertBanner = safeGetElement('successAlert');
  const alertMsg = safeGetElement('alertMessage');
  const alertClose = safeGetElement('alertClose');
  
  if (!form || !btnSave) return;
  
  const inputs = form.querySelectorAll('.form-input');
  inputs.forEach(input => {
    input.addEventListener('blur', () => validateInput(input));
    input.addEventListener('input', () => {
      if (input.classList.contains('invalid')) validateInput(input);
    });
  });
  
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    let isValid = true;
    inputs.forEach(input => {
      if (!validateInput(input)) isValid = false;
    });
    
    if (!isValid) {
      showToast('⚠️ Por favor corrige los errores del formulario', 'warning');
      return;
    }
    
    const originalHTML = btnSave.innerHTML;
    btnSave.disabled = true;
    btnSave.innerHTML = '⏳ Guardando...';
    
    try {
      configStorage.update('center', {
        name: safeGetElement('centerName').value,
        nit: safeGetElement('centerNIT').value,
        address: safeGetElement('centerAddress').value
      });
      config = configStorage.load();
      
      if (alertBanner && alertMsg) {
        alertMsg.textContent = `Cambios guardados correctamente - ${fmtTimestamp()}`;
        alertBanner.style.display = 'flex';
        alertBanner.setAttribute('aria-hidden', 'false');
      }
      
      showToast('✅ Configuración del centro actualizada');
      alertBanner?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      
    } catch (error) {
      console.error('Error al guardar configuración:', error);
      showToast('❌ Error al guardar cambios', 'error');
    } finally {
      setTimeout(() => {
        btnSave.disabled = false;
        btnSave.innerHTML = originalHTML;
      }, 500);
    }
  });
  
  alertClose?.addEventListener('click', () => {
    if (alertBanner) {
      alertBanner.style.display = 'none';
      alertBanner.setAttribute('aria-hidden', 'true');
    }
  });
};

const validateInput = (input) => {
  const value = input.value.trim();
  const id = input.id;
  
  input.classList.remove('invalid');
  
  if (id === 'centerName' && value.length < 3) {
    input.classList.add('invalid');
    return false;
  }
  if (id === 'centerNIT' && !/^\d{3}\.\d{3}\.\d{3}-\d$/.test(value)) {
    input.classList.add('invalid');
    return false;
  }
  if (id === 'centerAddress' && value.length < 5) {
    input.classList.add('invalid');
    return false;
  }
  
  return true;
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

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
    if (show) { const firstLink = sidebar.querySelector('.nav-item'); if (firstLink) firstLink.focus(); }
    else { hamburger.focus(); }
  };
  
  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));
  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => { if (window.innerWidth <= 680) toggleMenu(false); });
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && sidebar.classList.contains('open')) { e.preventDefault(); toggleMenu(false); }});
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS
// ═══════════════════════════════════════════════════════════════════

async function fetchConfig() {
  try {
    return configStorage.load();
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return configStorage.load();
  }
}

async function updateConfigAPI(section, data) {
  try {
    configStorage.update(section, data);
    return true;
  } catch (error) {
    console.warn('Error al actualizar configuración en API:', error);
    return false;
  }
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN PRINCIPAL
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  initSidebar();
  initToggles();
  initQuickEdit();
  initCenterForm();
  
  config = await fetchConfig();
  renderConfig();
  
  window.addEventListener('beforeunload', () => { /* Cleanup en SPA real */ });
};

document.addEventListener('DOMContentLoaded', init);