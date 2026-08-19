/**
 * SMILETRACK — PREPARACIÓN DE CONSULTA (app.js)
 * Lógica de checklist, persistencia y accesibilidad
 */

// Obtiene elemento del DOM con manejo seguro de null
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Reduce llamadas a función en eventos frecuentes de input
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// Muestra notificación temporal con auto-cierre y cleanup de timeout
const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;

  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// Gestiona persistencia del checklist con localStorage
const checklistStorage = {
  key: 'smiletrack_checklist_pedro_garcia_20260320',
  
  // Carga estado del checklist desde localStorage o usa valores por defecto
  load: () => {
    const stored = localStorage.getItem(checklistStorage.key);
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch (e) {
        console.warn('Error al cargar checklist, usando valores por defecto');
      }
    }
    // Estado inicial: primeros 3 ítems completados
    return [true, true, true, false, false, false, false];
  },
  
  // Guarda estado del checklist en localStorage
  save: (states) => {
    try {
      localStorage.setItem(checklistStorage.key, JSON.stringify(states));
      return true;
    } catch (e) {
      console.error('Error al guardar checklist:', e);
      return false;
    }
  },
  
  // Calcula progreso basado en ítems completados
  getProgress: (states) => {
    const completed = states.filter(s => s).length;
    const total = states.length;
    return {
      completed,
      total,
      percentage: Math.round((completed / total) * 100)
    };
  }
};

// Inicializa menú móvil con gestión de foco y atributos ARIA
const initMobileMenu = () => {
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  const hamburger = safeGetElement('hamburgerBtn');

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

// Inicializa checklist con persistencia y actualización de progreso
const initChecklist = () => {
  const checklist = safeGetElement('checklist');
  const progressFill = safeGetElement('progressFill');
  const progressText = safeGetElement('progressText');
  
  if (!checklist || !progressFill || !progressText) return;
  
  // Carga estado guardado
  const states = checklistStorage.load();
  const items = checklist.querySelectorAll('.checklist-item');
  
  // Aplica estado inicial a cada ítem
  items.forEach((item, index) => {
    const checkbox = item.querySelector('input[type="checkbox"]');
    if (checkbox && states[index] !== undefined) {
      checkbox.checked = states[index];
      item.classList.toggle('checked', states[index]);
      checkbox.setAttribute('aria-checked', states[index]);
    }
  });
  
  // Actualiza barra de progreso inicial
  updateProgress();
  
  // Maneja cambio de estado en checkboxes
  checklist.addEventListener('change', (e) => {
    if (e.target.type === 'checkbox') {
      const item = e.target.closest('.checklist-item');
      const index = Array.from(items).indexOf(item);
      
      if (index !== -1) {
        // Actualiza estado visual
        const isChecked = e.target.checked;
        item.classList.toggle('checked', isChecked);
        e.target.setAttribute('aria-checked', isChecked);
        
        // Guarda en localStorage
        states[index] = isChecked;
        checklistStorage.save(states);
        
        // Actualiza progreso
        updateProgress();
        
        // Feedback visual sutil
        showToast(isChecked ? 'Ítem completado' : 'Ítem desmarcado', 'info');
      }
    }
  });
  
  // Función para actualizar barra de progreso y texto
  function updateProgress() {
    const progress = checklistStorage.getProgress(states);
    
    if (progressFill) {
      progressFill.style.width = `${progress.percentage}%`;
      progressFill.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', progress.percentage);
      progressFill.closest('[role="progressbar"]')?.setAttribute('aria-valuetext', `${progress.percentage}% completado`);
    }
    
    if (progressText) {
      progressText.textContent = `${progress.completed} de ${progress.total} ítems completados`;
    }
  }
};

// Inicializa botón de confirmación con validación
const initConfirmButton = () => {
  const btn = safeGetElement('btnConfirmPreparation');
  const checklist = safeGetElement('checklist');
  
  if (!btn || !checklist) return;
  
  btn.addEventListener('click', () => {
    const items = checklist.querySelectorAll('.checklist-item input[type="checkbox"]');
    const allChecked = Array.from(items).every(cb => cb.checked);
    
    if (!allChecked) {
      showToast('Completa todos los ítems del checklist antes de confirmar', 'warning');
      
      // Enfocar primer ítem no completado para accesibilidad
      const firstUnchecked = Array.from(items).find(cb => !cb.checked);
      if (firstUnchecked) {
        firstUnchecked.closest('.checklist-item')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        firstUnchecked.focus();
      }
      return;
    }
    
    // Recoge observaciones si existen
    const observations = safeGetElement('observations')?.value.trim() || '';
    
    // En producción: enviar datos al backend
    console.log('Preparación confirmada:', {
      checklist: checklistStorage.load(),
      observations,
      timestamp: new Date().toISOString()
    });
    
    // Feedback de éxito
    showToast('✓ Preparación confirmada. Consultorio listo para el paciente', 'success');
    
    // Deshabilita botón temporalmente para evitar doble click
    btn.disabled = true;
    btn.textContent = '✓ Confirmado';
    
    // En producción: redirigir o actualizar estado
    setTimeout(() => {
      btn.disabled = false;
      btn.textContent = 'Confirmar preparación completa';
    }, 3000);
  });
};

// Inicializa textarea de observaciones con auto-guardado
const initObservations = () => {
  const textarea = safeGetElement('observations');
  if (!textarea) return;
  
  // Carga observaciones guardadas si existen
  const saved = localStorage.getItem(checklistStorage.key + '_observations');
  if (saved) textarea.value = saved;
  
  // Auto-guarda mientras el usuario escribe (con debounce)
  const debouncedSave = debounce(() => {
    localStorage.setItem(checklistStorage.key + '_observations', textarea.value);
  }, 500);
  
  textarea.addEventListener('input', debouncedSave);
};

// Función principal de inicialización
const init = () => {
  // Inicializar componentes de UI
  initMobileMenu();
  initChecklist();
  initConfirmButton();
  initObservations();
  
  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);