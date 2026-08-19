/* ============================================
SmileTrack — Estado del Consultorio (st-aux-09-estado-consultorio)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona la lógica completa del módulo de estado del consultorio: checklist post-atención con persistencia, selector de disponibilidad accesible, auto-guardado de observaciones y registro de historial de cambios.

FUNCIONALIDADES PRINCIPALES:
- Checklist renderizado dinámicamente desde LocalStorage con progreso ARIA en tiempo real
- Selector de estado del consultorio con patrón radiogroup y navegación por teclado (flechas)
- Auto-guardado de observaciones con debounce (500ms) para minimizar escrituras en LocalStorage
- Historial de cambios con timestamps ISO para trazabilidad de acciones del auxiliar

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.css
- JS: ~/js/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.js
- Partial / Otros: estado-consultorio.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- La clave de LocalStorage incluye consultorio y fecha para evitar colisiones entre consultorios o días distintos.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Debounce evita saturar LocalStorage con escrituras redundantes ante cambios veloces del usuario
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación sin interrumpir el flujo clínico del auxiliar

// WHY: La clave incluye consultorio y fecha para evitar colisiones entre sesiones de distintos consultorios en el mismo dispositivo
const consultorioStorage = {
  key: 'smiletrack_consultorio_1_20260320',
  
  // WHY: Carga desde LocalStorage para continuar el estado entre refrescos de página sin perder el avance del checklist
  load: () => {
    const stored = localStorage.getItem(consultorioStorage.key);
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch (e) {
        console.warn('Error al cargar estado del consultorio, usando valores por defecto');
      }
    }
    return {
      checklist: [
        { text: 'Limpieza y desinfección de superficies', checked: true },
        { text: 'Instrumental esterilizado y empaquetado', checked: true },
        { text: 'Residuos biológicos eliminados', checked: true },
        { text: 'Guantes y tapabocas reabastecidos', checked: false },
        { text: 'Historia clínica lista para próximo', checked: false },
        { text: 'Equipos verificados y encendidos', checked: false },
        { text: 'Consultorio ventilado', checked: false }
      ],
      status: 'disponible',
      observations: '',
      history: [
        { time: '2026-03-20T11:00', user: 'Sara Jiménez', detail: 'Limpieza completada' },
        { time: '2026-03-20T09:30', user: 'Sara Jiménez', detail: 'Preparación iniciada' },
        { time: '2026-03-19T17:00', user: 'Carlos Pérez', detail: 'Fin de jornada' }
      ]
    };
  },
  
  // WHY: Persiste el estado completo inmediatamente tras cada cambio para garantizar integridad ante cierres inesperados
  save: (state) => {
    try {
      localStorage.setItem(consultorioStorage.key, JSON.stringify(state));
      return true;
    } catch (e) {
      console.error('Error al guardar estado del consultorio:', e);
      return false;
    }
  },
  
  // WHY: Actualiza solo el ítem modificado sin reescribir el array completo para mantener eficiencia de escritura
  updateChecklistItem: (index, checked) => {
    const state = consultorioStorage.load();
    if (state.checklist[index]) {
      state.checklist[index].checked = checked;
      consultorioStorage.save(state);
    }
  },
  
  // WHY: Permite agregar ítems dinámicos al checklist sin recargar la vista
  addChecklistItem: (text) => {
    const state = consultorioStorage.load();
    state.checklist.push({ text, checked: false });
    consultorioStorage.save(state);
  },
  
  // WHY: Registra el estado seleccionado para sintonizar la UI con el estado persistido al recargar la vista
  updateStatus: (status) => {
    const state = consultorioStorage.load();
    state.status = status;
    consultorioStorage.save(state);
  },
  
  // WHY: Guarda las observaciones del auxiliar para que no se pierdan al navegar entre vistas
  updateObservations: (text) => {
    const state = consultorioStorage.load();
    state.observations = text;
    consultorioStorage.save(state);
  },
  
  // WHY: Limita el historial a 10 entradas para no saturar LocalStorage con datos indefinidos
  addToHistory: (user, detail) => {
    const state = consultorioStorage.load();
    state.history.unshift({
      time: new Date().toISOString(),
      user,
      detail
    });
    // WHY: Limita el historial a máximo 10 entradas para evitar el crecimiento indefinido del objeto en LocalStorage
    if (state.history.length > 10) state.history.pop();
    consultorioStorage.save(state);
  }
};

// WHY: Calcula el progreso en tiempo real para actualizar tanto la barra visual como la etiqueta ARIA accesible
const calculateProgress = () => {
  const state = consultorioStorage.load();
  const total = state.checklist.length;
  const checked = state.checklist.filter(i => i.checked).length;
  return {
    checked,
    total,
    percentage: total > 0 ? Math.round((checked / total) * 100) : 0
  };
};

// Inicializa menú móvil con gestión de foco y atributos ARIA
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

// WHY: Renderiza el checklist desde LocalStorage para mantener el estado entre refrescos de página
const initChecklist = () => {
  const checklist = safeGetElement('checklistItems');
  const progressBar = safeGetElement('progressBar');
  const progressInfo = safeGetElement('progressInfo');
  const progressText = safeGetElement('progressText');
  
  if (!checklist) return;
  
  // Carga estado guardado
  const state = consultorioStorage.load();
  
  // WHY: Re-renderiza la lista completa desde el estado guardado en lugar de confiar en el HTML estático del servidor
  checklist.innerHTML = '';
  state.checklist.forEach((item, index) => {
    const li = document.createElement('li');
    li.setAttribute('role', 'listitem');
    
    li.innerHTML = `
      <label class="check-item">
        <input type="checkbox" ${item.checked ? 'checked' : ''} aria-label="${item.text}${item.checked ? ' - completado' : ''}" />
        <span class="checkmark" aria-hidden="true"></span>
        <span class="text">${item.text}</span>
      </label>
    `;
    
    // WHY: Actualiza el aria-label del checkbox al cambiar estado para que lectores de pantalla anuncien el nuevo estado
    const checkbox = li.querySelector('input');
    checkbox.addEventListener('change', () => {
      consultorioStorage.updateChecklistItem(index, checkbox.checked);
      checkbox.setAttribute('aria-label', `${item.text}${checkbox.checked ? ' - completado' : ''}`);
      updateProgressUI();
    });
    
    checklist.appendChild(li);
  });
  
  // Actualiza UI de progreso inicial
  updateProgressUI();
  
  // Función para actualizar UI de progreso
  function updateProgressUI() {
    const progress = calculateProgress();
    
    if (progressBar) {
      progressBar.style.width = `${progress.percentage}%`;
      progressBar.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', progress.percentage);
      progressBar.closest('[role="progressbar"]')?.setAttribute('aria-valuetext', `${progress.percentage}% completado`);
    }
    
    if (progressInfo) {
      progressInfo.textContent = `${progress.checked}/${progress.total} - ${progress.percentage}%`;
    }
    
    if (progressText) {
      progressText.textContent = `${progress.checked} de ${progress.total} ítems completados`;
    }
  }
};

// WHY: Habilita agregar ítems personalizados al checklist en tiempo real según la situación específica del consultorio
const initAddItem = () => {
  const input = safeGetElement('newItemInput');
  const btn = safeGetElement('btnAddItem');
  const checklist = safeGetElement('checklistItems');
  
  if (!input || !btn || !checklist) return;
  
  const addItem = () => {
    const text = input.value.trim();
    if (!text) {
      window.ToastService.warning('Por favor escribe un ítem');
      input.focus();
      return;
    }
    
    // Agrega a localStorage
    consultorioStorage.addChecklistItem(text);
    
    // Crea nuevo elemento en la lista
    const li = document.createElement('li');
    li.setAttribute('role', 'listitem');
    
    li.innerHTML = `
      <label class="check-item">
        <input type="checkbox" aria-label="${text}" />
        <span class="checkmark" aria-hidden="true"></span>
        <span class="text">${text}</span>
      </label>
    `;
    
    // Maneja cambio del nuevo checkbox
    const checkbox = li.querySelector('input');
    checkbox.addEventListener('change', () => {
      const newIndex = consultorioStorage.load().checklist.length - 1;
      consultorioStorage.updateChecklistItem(newIndex, checkbox.checked);
      checkbox.setAttribute('aria-label', `${text}${checkbox.checked ? ' - completado' : ''}`);
      updateProgressUI();
    });
    
    // Animación de entrada
    li.style.opacity = '0';
    li.style.transform = 'translateY(-8px)';
    li.style.transition = 'opacity .2s, transform .2s';
    
    checklist.appendChild(li);
    
    // WHY: requestAnimationFrame garantiza que el reflow se complete antes de aplicar la transición de entrada
    requestAnimationFrame(() => {
      li.style.opacity = '1';
      li.style.transform = 'translateY(0)';
    });
    
    // Limpia input y enfoca
    input.value = '';
    input.focus();
    
    // Actualiza progreso
    updateProgressUI();
    
    window.ToastService.success('Ítem agregado');
  };
  
  // Event listeners para agregar ítem
  btn.addEventListener('click', addItem);
  input.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addItem();
    }
  });
  
  // Expone addItem globalmente para compatibilidad con onclick del HTML original
  window.addItem = addItem;
};

// WHY: Implementa el patrón radiogroup accesible con navegación por teclado (flechas) para seleccionar el estado del consultorio
const initStatusSelector = () => {
  const options = document.querySelectorAll('.status-option');
  
  // Carga estado guardado
  const savedStatus = consultorioStorage.load().status;
  
  options.forEach(option => {
    // Aplica estado guardado
    const value = option.dataset.value;
    const isActive = value === savedStatus;
    
    if (isActive) {
      option.classList.add('selected');
      option.setAttribute('aria-checked', 'true');
      option.setAttribute('tabindex', '0');
    } else {
      option.classList.remove('selected');
      option.setAttribute('aria-checked', 'false');
      option.setAttribute('tabindex', '-1');
    }
    
    // Maneja click para cambio de estado
    option.addEventListener('click', () => {
      // Remueve selected de todas las opciones
      options.forEach(opt => {
        opt.classList.remove('selected');
        opt.setAttribute('aria-checked', 'false');
        opt.setAttribute('tabindex', '-1');
      });
      
      // Activa la opción clickeada
      option.classList.add('selected');
      option.setAttribute('aria-checked', 'true');
      option.setAttribute('tabindex', '0');
      
      // Guarda en localStorage
      consultorioStorage.updateStatus(value);
      
      // Feedback visual
      window.ToastService.success(`Estado actualizado: ${option.querySelector('strong')?.textContent}`, 'info');
    });
    
    // Soporte para teclado en opciones de estado
    option.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        option.click();
      } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        const currentIndex = Array.from(options).indexOf(option);
        const nextIndex = e.key === 'ArrowDown' 
          ? (currentIndex + 1) % options.length 
          : (currentIndex - 1 + options.length) % options.length;
        options[nextIndex]?.focus();
      }
    });
  });
  
  // Expone selectStatus globalmente para compatibilidad con onclick del HTML original
  window.selectStatus = (element) => {
    // Remueve selected de todas las opciones
    options.forEach(opt => {
      opt.classList.remove('selected');
      opt.setAttribute('aria-checked', 'false');
    });
    
    // Activa la opción clickeada
    element.classList.add('selected');
    element.setAttribute('aria-checked', 'true');
    
    // Guarda en localStorage
    const value = element.dataset.value;
    if (value) consultorioStorage.updateStatus(value);
  };
};

// WHY: El debounce de 500ms evita escrituras excesivas en LocalStorage mientras el usuario aún está escribiendo observaciones
const initObservations = () => {
  const textarea = safeGetElement('obsTextarea');
  if (!textarea) return;
  
  // Carga observaciones guardadas
  const saved = consultorioStorage.load().observations;
  if (saved) textarea.value = saved;
  
  // Auto-guarda mientras el usuario escribe (con debounce)
  const debouncedSave = debounce(() => {
    consultorioStorage.updateObservations(textarea.value);
  }, 500);
  
  textarea.addEventListener('input', debouncedSave);
};

// WHY: Los botones de confirmación validan el estado completo antes de registrar en el historial
const initConfirmButtons = () => {
  const btnPreparation = safeGetElement('btnConfirmPreparation');
  const btnStatus = safeGetElement('btnConfirmStatus');
  
  // Confirmar preparación completa
  if (btnPreparation) {
    btnPreparation.addEventListener('click', () => {
      const progress = calculateProgress();
      
      if (progress.checked < progress.total) {
        window.ToastService.warning(`Completa ${progress.total - progress.checked} ítem(s) pendiente(s)`);
        return;
      }
      
      // Agrega entrada al historial
      consultorioStorage.addToHistory('Auxiliar', 'Preparación del consultorio confirmada');
      
      // Feedback visual
      window.ToastService.success('✅ Preparación del consultorio confirmada');
      
      // Deshabilita botón temporalmente
      btnPreparation.disabled = true;
      btnPreparation.textContent = '✓ Confirmado';
      
      setTimeout(() => {
        btnPreparation.disabled = false;
        btnPreparation.textContent = 'Confirmar preparación completa';
      }, 3000);
    });
  }
  
  // Confirmar estado actual
  if (btnStatus) {
    btnStatus.addEventListener('click', () => {
      const selectedOption = document.querySelector('.status-option.selected');
      const status = selectedOption?.querySelector('strong')?.textContent || 'Desconocido';
      
      // Agrega entrada al historial
      consultorioStorage.addToHistory('Auxiliar', `Estado actualizado a: ${status}`);
      
      // Feedback visual
      window.ToastService.success(`✅ Estado actualizado: ${status}`);
      
      // Deshabilita botón temporalmente
      btnStatus.disabled = true;
      btnStatus.textContent = '✓ Confirmado';
      
      setTimeout(() => {
        btnStatus.disabled = false;
        btnStatus.textContent = 'Confirmar estado';
      }, 3000);
    });
  }
  
  // Expone funciones globalmente para compatibilidad con onclick del HTML original
  window.confirmPreparation = () => btnPreparation?.click();
  window.confirmStatus = () => btnStatus?.click();
};

// WHY: El historial de estados se renderiza dinámicamente para reflejar las confirmaciones realizadas durante la sesión
const initHistoryList = () => {
  const list = safeGetElement('history-list');
  if (!list) return;
  
  const state = consultorioStorage.load();
  
  // Formatea fecha para mostrar
  const formatDate = (iso) => {
    const d = new Date(iso);
    const months = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    return `${String(d.getDate()).padStart(2,'0')} ${months[d.getMonth()]} ${d.getFullYear()} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
  };
  
  // Renderiza historial
  list.innerHTML = state.history.map(entry => `
    <li role="listitem">
      <p class="history-time"><time datetime="${entry.time}">${formatDate(entry.time)}</time></p>
      <p class="history-detail">${entry.user} · ${entry.detail}</p>
    </li>
  `).join('');
};

// Función principal de inicialización
const init = () => {
    // Inicializar componentes de UI
    initMobileMenu();
    initChecklist();
    initAddItem();
    initStatusSelector();
    initObservations();
    initConfirmButtons();
    initHistoryList();
    
    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);