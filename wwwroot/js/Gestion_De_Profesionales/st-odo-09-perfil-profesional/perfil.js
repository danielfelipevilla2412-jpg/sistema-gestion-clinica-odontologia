/* ============================================
SmileTrack — Perfil Profesional (st-odo-09-perfil-profesional)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona la lógica interactiva del perfil del profesional logueado: guardado de datos mediante API fetch o LocalStorage fallback, cambio de pestañas de navegación interna y validaciones de seguridad en los formularios.

FUNCIONALIDADES PRINCIPALES:
- Guardado asíncrono (fetch) de datos personales y credenciales de seguridad con feedback visual
- Navegación interactiva por pestañas (Datos Personales / Configuración de Seguridad)
- Validaciones en cliente para contraseñas seguras y campos de contacto correctos
- Gestión de alertas y notificaciones del sistema con dismiss automático

DEPENDENCIAS TÉCNICAS:
- Controller: GestionProfesionalesController
- CSS: ~/css/Gestion_De_Profesionales/st-odo-09-perfil-profesional/styles.css
- JS: ~/js/Gestion_De_Profesionales/st-odo-09-perfil-profesional/perfil.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
============================================ */

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

// ═══════════════════════════════════════════════════════════════════
//  DATOS DE EJEMPLO (Fallback si API falla)
// ═══════════════════════════════════════════════════════════════════
const SAMPLE_PROFILE = {
  nombre: 'Dr. Carlos Méndez',
  especialidades: ['Odontología General', 'Estética Dental'],
  registro: 'RM-2026-001',
  telefono: '300 123 4567',
  email: 'dr.mendez@smiletrack.co',
  fechaIngreso: '2022-03-15',
  estado: 'activo',
  horario: [
    { day: 'Lun', dayFull: 'Lunes', active: true, start: '08:00', end: '12:00' },
    { day: 'Mar', dayFull: 'Martes', active: true, start: '08:00', end: '12:00' },
    { day: 'Mié', dayFull: 'Miércoles', active: true, start: '14:00', end: '18:00' },
    { day: 'Jue', dayFull: 'Jueves', active: true, start: '08:00', end: '12:00' },
    { day: 'Vie', dayFull: 'Viernes', active: true, start: '08:00', end: '12:00' },
    { day: 'Sáb', dayFull: 'Sábado', active: false, start: '', end: '' },
    { day: 'Dom', dayFull: 'Domingo', active: false, start: '', end: '' }
  ]
};

let profileData = { ...SAMPLE_PROFILE };
let currentEditingDay = null;

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════

// Calcula horas entre dos tiempos
const calculateHours = (start, end) => {
  if (!start || !end) return 0;
  const [startH, startM] = start.split(':').map(Number);
  const [endH, endM] = end.split(':').map(Number);
  const hours = (endH + endM / 60) - (startH + startM / 60);
  return Math.max(0, hours);
};

// Obtiene clase CSS para slot de tiempo
const getTimeSlotClass = (start) => {
  const [h] = start.split(':').map(Number);
  return h < 12 ? 'morning' : 'afternoon';
};

// Obtiene ícono para slot de tiempo
const getTimeSlotIcon = (start) => {
  const [h] = start.split(':').map(Number);
  return h < 12 ? '🌅' : '🌇';
};

// Renderiza grilla de horario semanal
const renderSchedule = () => {
  const grid = safeGetElement('scheduleGrid');
  if (!grid) return;

  let totalHours = 0;
  let activeDays = 0;

  grid.innerHTML = profileData.horario.map((dayData, index) => {
    const hours = dayData.active ? calculateHours(dayData.start, dayData.end) : 0;
    totalHours += hours;
    if (dayData.active) activeDays++;

    const statusClass = dayData.active ? 'active' : 'inactive';
    
    let timeSlotsHTML = '';
    if (dayData.active && dayData.start && dayData.end) {
      const slotClass = getTimeSlotClass(dayData.start);
      const icon = getTimeSlotIcon(dayData.start);
      timeSlotsHTML = `
        <span class="time-slot ${slotClass}">
          <span class="time-slot-icon" aria-hidden="true">${icon}</span>
          <time datetime="${dayData.start}">${dayData.start}</time> - <time datetime="${dayData.end}">${dayData.end}</time>
        </span>
      `;
    } else {
      timeSlotsHTML = '<div class="no-schedule">— Descanso —</div>';
    }

    const hoursText = dayData.active ? `${hours}h` : '—';

    return `
      <div class="schedule-day ${statusClass}" 
           role="listitem"
           tabindex="0"
           aria-label="${dayData.dayFull}: ${dayData.active ? `${hours} horas, ${dayData.start} a ${dayData.end}` : 'No disponible'}"
           data-index="${index}">
        <div class="day-name">${dayData.day}</div>
        <div class="time-blocks">${timeSlotsHTML}</div>
        <div class="hours-badge" aria-label="${hours} horas">${hoursText}</div>
      </div>
    `;
  }).join('');

  // Agregar event listeners a días del horario
  grid.querySelectorAll('.schedule-day').forEach(dayEl => {
    const index = parseInt(dayEl.dataset.index, 10);
    
    dayEl.addEventListener('click', () => openScheduleModal(index));
    dayEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        openScheduleModal(index);
      }
    });
  });

  // Actualizar resumen
  const totalHoursEl = safeGetElement('totalHours');
  const totalDaysEl = safeGetElement('totalDays');
  if (totalHoursEl) totalHoursEl.textContent = `${totalHours} horas`;
  if (totalDaysEl) totalDaysEl.textContent = `${activeDays} días laborales`;
};

// ═══════════════════════════════════════════════════════════════════
//  MODAL - EDITAR HORARIO
// ═══════════════════════════════════════════════════════════════════

// Abre modal para editar día del horario
const openScheduleModal = (index) => {
  currentEditingDay = index;
  const dayData = profileData.horario[index];
  
  // Set modal content
  const modalDayIcon = safeGetElement('modalDayIcon');
  const modalDayName = safeGetElement('modalDayName');
  const modalDayActive = safeGetElement('modalDayActive');
  const modalStartTime = safeGetElement('modalStartTime');
  const modalEndTime = safeGetElement('modalEndTime');
  
  if (modalDayIcon) modalDayIcon.textContent = dayData.day;
  if (modalDayName) modalDayName.textContent = dayData.dayFull;
  if (modalDayActive) modalDayActive.checked = dayData.active;
  if (modalStartTime) modalStartTime.value = dayData.start || '08:00';
  if (modalEndTime) modalEndTime.value = dayData.end || '12:00';
  
  // Update UI based on active state
  updateModalUI(dayData.active);
  updatePreview();
  
  // Show modal
  const modal = safeGetElement('scheduleModal');
  if (modal) {
    modal.classList.add('active');
    modal.setAttribute('aria-hidden', 'false');
    modal.removeAttribute('inert');
    
    // Enfocar primer elemento interactivo
    const firstInput = modal.querySelector('input, button');
    if (firstInput) firstInput.focus();
    
    // Bloquear scroll del body
    document.body.style.overflow = 'hidden';
  }
};

// Cierra modal de edición de horario
const closeScheduleModal = () => {
  const modal = safeGetElement('scheduleModal');
  if (modal) {
    modal.classList.remove('active');
    modal.setAttribute('aria-hidden', 'true');
    modal.setAttribute('inert', '');
    
    // Restaurar scroll
    document.body.style.overflow = '';
  }
  currentEditingDay = null;
};

// Actualiza UI del modal según estado activo
const updateModalUI = (isActive) => {
  const timeSection = safeGetElement('modalTimeSection');
  const toggleText = safeGetElement('toggleText');
  
  if (timeSection) {
    timeSection.classList.toggle('disabled', !isActive);
  }
  if (toggleText) {
    toggleText.textContent = isActive ? 'Día laboral' : 'No disponible';
    toggleText.classList.toggle('active', isActive);
    toggleText.classList.toggle('inactive', !isActive);
  }
};

// Actualiza vista previa del horario
const updatePreview = () => {
  const isActive = safeGetElement('modalDayActive')?.checked;
  const start = safeGetElement('modalStartTime')?.value;
  const end = safeGetElement('modalEndTime')?.value;
  const preview = safeGetElement('modalPreview');
  
  if (!preview) return;
  
  if (isActive && start && end) {
    const hours = calculateHours(start, end);
    const previewTime = safeGetElement('previewTime');
    const previewHours = safeGetElement('previewHours');
    
    if (previewTime) previewTime.textContent = `${start} - ${end}`;
    if (previewHours) previewHours.textContent = `${hours} hora${hours !== 1 ? 's' : ''}`;
    preview.style.opacity = '1';
  } else {
    preview.style.opacity = '0.5';
    const previewTime = safeGetElement('previewTime');
    const previewHours = safeGetElement('previewHours');
    if (previewTime) previewTime.textContent = isActive ? 'Selecciona horario' : 'Día no laboral';
    if (previewHours) previewHours.textContent = isActive ? '' : 'Sin horario';
  }
};

// Guarda cambios del horario
const saveSchedule = () => {
  if (currentEditingDay === null) return;
  
  const dayData = profileData.horario[currentEditingDay];
  const isActive = safeGetElement('modalDayActive')?.checked;
  const startEl = safeGetElement('modalStartTime');
  const endEl = safeGetElement('modalEndTime');
  const start = startEl?.value;
  const end = endEl?.value;
  
  if (window.ValidationUtils) {
      if (startEl) window.ValidationUtils.clearError(startEl);
      if (endEl) window.ValidationUtils.clearError(endEl);
  }
  
  // Validate if active
  if (isActive) {
    if (!start || !end) {
      if (window.ValidationUtils && !start && startEl) window.ValidationUtils.showError(startEl, null, 'Por favor completa el horario');
      if (window.ValidationUtils && !end && endEl) window.ValidationUtils.showError(endEl, null, 'Por favor completa el horario');
      window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }
    
    const hours = calculateHours(start, end);
    if (hours <= 0) {
      if (window.ValidationUtils && endEl) {
          window.ValidationUtils.showError(endEl, null, 'La hora de fin debe ser mayor a la hora de inicio');
      }
      window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }
    
    if (hours > 12) {
      if (window.ValidationUtils && endEl) {
          window.ValidationUtils.showError(endEl, null, 'El horario máximo es de 12 horas por día');
      }
      window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
    }
    
    dayData.active = true;
    dayData.start = start;
    dayData.end = end;
  } else {
    dayData.active = false;
    dayData.start = '';
    dayData.end = '';
  }
  
  // Re-render schedule
  renderSchedule();
  closeScheduleModal();
  
  window.ToastService.success(`✅ Horario de ${dayData.dayFull} actualizado`);
};

// ═══════════════════════════════════════════════════════════════════
//  FORMULARIO: CAMBIAR CONTRASEÑA
// ═══════════════════════════════════════════════════════════════════

// Verifica fortaleza de contraseña
const checkPasswordStrength = (password) => {
  if (password.length === 0) return null;
  
  let strength = 0;
  if (password.length >= 8) strength++;
  if (password.length >= 12) strength++;
  if (/[a-z]/.test(password) && /[A-Z]/.test(password)) strength++;
  if (/\d/.test(password)) strength++;
  if (/[^a-zA-Z0-9]/.test(password)) strength++;
  
  if (strength <= 2) return 'weak';
  if (strength <= 4) return 'medium';
  return 'strong';
};

// Alterna visibilidad de contraseña
const togglePasswordVisibility = (inputId, button) => {
  const input = safeGetElement(inputId);
  if (!input) return;
  
  if (input.type === 'password') {
    input.type = 'text';
    button.textContent = '🙈';
    button.setAttribute('aria-pressed', 'true');
  } else {
    input.type = 'password';
    button.textContent = '👁';
    button.setAttribute('aria-pressed', 'false');
  }
};

// Maneja cambio de contraseña
const changePassword = async (event) => {
  event.preventDefault();
  
  const currentPasswordEl = safeGetElement('currentPassword');
  const newPasswordEl = safeGetElement('newPassword');
  const confirmPasswordEl = safeGetElement('confirmPassword');
  
  const currentPassword = currentPasswordEl?.value;
  const newPassword = newPasswordEl?.value;
  const confirmPassword = confirmPasswordEl?.value;

  if (window.ValidationUtils) {
      if (currentPasswordEl) window.ValidationUtils.clearError(currentPasswordEl);
      if (newPasswordEl) window.ValidationUtils.clearError(newPasswordEl);
      if (confirmPasswordEl) window.ValidationUtils.clearError(confirmPasswordEl);
  }
  
  let valid = true;

  // Validaciones básicas
  if (currentPassword?.length < 6) {
    if (window.ValidationUtils && currentPasswordEl) {
        window.ValidationUtils.showError(currentPasswordEl, null, 'La contraseña actual debe tener al menos 6 caracteres');
    }
    valid = false;
  }
  
  if (newPassword?.length < 8) {
    if (window.ValidationUtils && newPasswordEl) {
        window.ValidationUtils.showError(newPasswordEl, null, 'La nueva contraseña debe tener al menos 8 caracteres');
    }
    valid = false;
  }
  
  if (newPassword !== confirmPassword) {
    if (window.ValidationUtils && confirmPasswordEl) {
        window.ValidationUtils.showError(confirmPasswordEl, null, 'Las nuevas contraseñas no coinciden');
    }
    valid = false;
  }
  
  if (currentPassword === newPassword) {
    if (window.ValidationUtils && newPasswordEl) {
        window.ValidationUtils.showError(newPasswordEl, null, 'La nueva contraseña debe ser diferente a la actual');
    }
    valid = false;
  }

  if (!valid) {
      window.ToastService.warning('⚠️ Verifique los campos resaltados en rojo');
      return;
  }
  
  const btn = event.target.querySelector('.btn-update');
  if (btn) {
    btn.disabled = true;
    btn.textContent = '⏳ Actualizando...';
  }
  
  try {
    // En producción: llamada real a API
    // await fetch(`${API_BASE}/profile/password`, {
    //   method: 'POST',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify({ currentPassword, newPassword }),
    // });
    
    // Simulación
    await new Promise(resolve => setTimeout(resolve, 1500));
    
    // Éxito
    if (btn) {
      btn.textContent = '✓ Contraseña actualizada';
      btn.style.background = 'linear-gradient(135deg, #4caf50, #43a047)';
    }
    
    // Reset form
    const form = safeGetElement('passwordForm');
    if (form) form.reset();
    
    const strengthIndicator = safeGetElement('passwordStrength');
    if (strengthIndicator) strengthIndicator.className = 'password-strength';
    
    // Reset toggle buttons
    document.querySelectorAll('.toggle-password').forEach(btn => {
      btn.textContent = '👁';
      btn.setAttribute('aria-pressed', 'false');
    });
    
    window.ToastService.success('✅ Contraseña actualizada exitosamente');
    
    // Restaurar botón
    setTimeout(() => {
      if (btn) {
        btn.textContent = 'Actualizar contraseña';
        btn.disabled = false;
        btn.style.background = '';
      }
    }, 2000);
    
  } catch (error) {
    console.warn('Error actualizando contraseña:', error);
    window.ToastService.error('❌ Error al actualizar contraseña');
    if (btn) {
      btn.textContent = 'Actualizar contraseña';
      btn.disabled = false;
    }
  }
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS (Listas para conectar al backend C#)
// ═══════════════════════════════════════════════════════════════════

// Obtiene datos del perfil desde API
async function fetchProfile() {
  try {
    // En producción: fetch real a API
    // const res = await fetch(`${API_BASE}/profile`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    
    // Simulación con fallback
    return SAMPLE_PROFILE;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_PROFILE;
  }
}

// Guarda datos del perfil en API
async function saveProfile(data) {
  try {
    // En producción: POST real a API
    // const res = await fetch(`${API_BASE}/profile`, {
    //   method: 'PUT',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify(data),
    // });
    // if (!res.ok) throw new Error('Save failed');
    // return true;
    
    // Simulación
    return true;
  } catch (error) {
    console.warn('Error guardando perfil:', error);
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

// Inicializa formulario de contraseña
const initPasswordForm = () => {
  const form = safeGetElement('passwordForm');
  const newPasswordInput = safeGetElement('newPassword');
  
  // Toggle password visibility
  document.querySelectorAll('.toggle-password').forEach(button => {
    button.addEventListener('click', () => {
      const inputId = button.closest('.input-with-icon')?.querySelector('.form-input')?.id;
      if (inputId) togglePasswordVisibility(inputId, button);
    });
  });
  
  // Password strength checker
  if (newPasswordInput) {
    newPasswordInput.addEventListener('input', debounce(() => {
      const password = newPasswordInput.value;
      const strengthIndicator = safeGetElement('passwordStrength');
      
      if (!strengthIndicator) return;
      
      if (password.length === 0) {
        strengthIndicator.className = 'password-strength';
        strengthIndicator.textContent = '';
        return;
      }
      
      const strength = checkPasswordStrength(password);
      strengthIndicator.className = `password-strength ${strength}`;
      
      const labels = {
        weak: '🔴 Débil',
        medium: '🟡 Media',
        strong: '🟢 Fuerte'
      };
      strengthIndicator.textContent = labels[strength] || '';
    }, 150));
  }
  
  // Form submit
  if (form) {
    form.addEventListener('submit', changePassword);
  }
};

// Inicializa modal de horario
const initScheduleModal = () => {
  const modal = safeGetElement('scheduleModal');
  const modalClose = safeGetElement('modalClose');
  const modalCancel = safeGetElement('modalCancel');
  const modalSave = safeGetElement('modalSave');
  const modalDayActive = safeGetElement('modalDayActive');
  const modalStartTime = safeGetElement('modalStartTime');
  const modalEndTime = safeGetElement('modalEndTime');
  
  // Close handlers
  if (modalClose) modalClose.addEventListener('click', closeScheduleModal);
  if (modalCancel) modalCancel.addEventListener('click', closeScheduleModal);
  if (modal) {
    modal.addEventListener('click', (e) => {
      if (e.target === e.currentTarget) closeScheduleModal();
    });
  }
  
  // Save handler
  if (modalSave) modalSave.addEventListener('click', saveSchedule);
  
  // Toggle active state
  if (modalDayActive) {
    modalDayActive.addEventListener('change', () => {
      updateModalUI(modalDayActive.checked);
      updatePreview();
    });
  }
  
  // Time input listeners
  if (modalStartTime) {
    modalStartTime.addEventListener('change', updatePreview);
    modalStartTime.addEventListener('input', updatePreview);
  }
  if (modalEndTime) {
    modalEndTime.addEventListener('change', updatePreview);
    modalEndTime.addEventListener('input', updatePreview);
  }
  
  // Keyboard support
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && modal?.classList.contains('active')) {
      e.preventDefault();
      closeScheduleModal();
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Inicializar componentes de UI
  initSidebar();
  initPasswordForm();
  initScheduleModal();
  
  // Cargar datos del perfil
  profileData = await fetchProfile();
  
  // Renderizar horario
  renderSchedule();
  
  // Limpieza al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);