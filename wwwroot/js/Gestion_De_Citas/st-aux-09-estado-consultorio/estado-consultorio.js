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
  if (window.CommonUtils?.safeGetElement) {
    return window.CommonUtils.safeGetElement(id);
  }
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  if (window.CommonUtils?.debounce) {
    return window.CommonUtils.debounce(fn, delay);
  }
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación sin interrumpir el flujo clínico del auxiliar

// WHY: La clave incluye consultorio y fecha para evitar colisiones entre sesiones de distintos consultorios en el mismo dispositivo
const consultorioData = window.smiletrackEstadoConsultorioData || {};
let consultorioSyncTimer = null;
let consultorioServerState = null;
const consultorioStorage = {
  // WHY: Se calcula dinámicamente para usar el consultorio actual en todo momento
  get key() {
    const currentData = window.smiletrackEstadoConsultorioData || {};
    return `smiletrack_consultorio_${currentData.consultorioId || 'sin_consultorio'}_${new Date().toISOString().slice(0, 10)}`;
  },
  
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
      history: []   // el historial real viene del servidor
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
      scheduleConsultorioSync();
    }
  },
  
  // WHY: Permite agregar ítems dinámicos al checklist sin recargar la vista
  addChecklistItem: (text) => {
    const state = consultorioStorage.load();
    state.checklist.push({ text, checked: false });
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  },
  
  // WHY: Registra el estado seleccionado para sintonizar la UI con el estado persistido al recargar la vista
  updateStatus: (status) => {
    const state = consultorioStorage.load();
    state.status = status;
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  },
  
  // WHY: Guarda las observaciones del auxiliar para que no se pierdan al navegar entre vistas
  updateObservations: (text) => {
    const state = consultorioStorage.load();
    state.observations = text;
    consultorioStorage.save(state);
    scheduleConsultorioSync();
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
    scheduleConsultorioSync();
  }
};

const scheduleConsultorioSync = () => {
  clearTimeout(consultorioSyncTimer);
  consultorioSyncTimer = setTimeout(saveConsultorioState, 400);
};

async function saveConsultorioState() {
  const serverData = window.smiletrackEstadoConsultorioData || {};
  if (!serverData.consultorioId) return;
  
  const state = consultorioStorage.load();
  try {
    const response = await fetch(`/api/consultorios/${serverData.consultorioId}/estado-operativo`, {
      method: 'PUT',
      credentials: 'same-origin',
      headers: getRequestHeaders(),
      body: JSON.stringify({ 
        estado: state.status, 
        checklist: state.checklist, 
        observaciones: state.observations 
      })
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
  } catch (error) {
    console.warn('[SmileTrack] No se pudo sincronizar el estado del consultorio:', error);
  }
}

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

// Inicializa menú móvil (delegado al módulo centralizado)
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// WHY: Renderiza el checklist desde LocalStorage para mantener el estado entre refrescos de página
const initChecklist = () => {
  const checklist = safeGetElement('checklistItems');
  const progressBar = safeGetElement('progressBar');
  const progressInfo = safeGetElement('progressInfo');
  const progressText = safeGetElement('progressText');
  
  if (!checklist) return;
  
  // Carga estado guardado o usa checklist del servidor si está disponible
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverChecklist = serverData.checklist;
  let state = consultorioStorage.load();
  
  // WHY: Si el servidor tiene un checklist guardado, usarlo en lugar del localStorage
  // Esto asegura que al cambiar de consultorio, se cargue el checklist correcto
  if (serverChecklist && Array.isArray(serverChecklist) && serverChecklist.length > 0) {
    state.checklist = serverChecklist.map(item => ({
      text: item.text || item.Text || String(item),
      checked: Boolean(item.checked || item.Checked)
    }));
    consultorioStorage.save(state);
  }
  
  // WHY: Re-renderiza la lista completa desde el estado guardado en lugar de confiar en el HTML estático del servidor
  checklist.replaceChildren();
  state.checklist.forEach((item, index) => {
    const li = document.createElement('li');
    li.setAttribute('role', 'listitem');
    const label = document.createElement('label');
    label.className = 'check-item';
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.checked = Boolean(item.checked);
    checkbox.setAttribute('aria-label', `${item.text}${item.checked ? ' - completado' : ''}`);
    const checkmark = document.createElement('span');
    checkmark.className = 'checkmark';
    checkmark.setAttribute('aria-hidden', 'true');
    const text = document.createElement('span');
    text.className = 'text';
    text.textContent = item.text;
    label.append(checkbox, checkmark, text);
    li.appendChild(label);
    
    // FASE-0 E-LEX-01: Se elimina redeclaración `const checkbox` que ya existía en L206
    // (SyntaxError en modo estricto). Se reutiliza la variable del mismo scope.
    // WHY: Actualiza el aria-label del checkbox al cambiar estado para que lectores de pantalla anuncien el nuevo estado
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
    
    const label = document.createElement('label');
    label.className = 'check-item';
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.setAttribute('aria-label', text);
    const checkmark = document.createElement('span');
    checkmark.className = 'checkmark';
    checkmark.setAttribute('aria-hidden', 'true');
    const textElement = document.createElement('span');
    textElement.className = 'text';
    textElement.textContent = text;
    label.append(checkbox, checkmark, textElement);
    li.appendChild(label);
    
    // Maneja cambio del nuevo checkbox (reutiliza la variable checkbox del scope superior)
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
  
  // Priorizar estado del servidor si existe
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverStatus = serverData.estadoActual;
  
  let savedStatus = consultorioStorage.load().status;
  
  // Si hay estado del servidor, usarlo y guardarlo
  if (serverStatus) {
    // Normalizar el estado del servidor para que coincida con los valores del selector
    const normalizedStatus = serverStatus.toLowerCase().replace(/_/g, '-');
    savedStatus = normalizedStatus;
    consultorioStorage.updateStatus(normalizedStatus);
  }
  
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
    // Remueve selected de todas las opciones y ajusta tabindex roving
    options.forEach(opt => {
      opt.classList.remove('selected');
      opt.setAttribute('aria-checked', 'false');
      opt.setAttribute('tabindex', '-1');
    });
    
    // Activa la opción clickeada
    element.classList.add('selected');
    element.setAttribute('aria-checked', 'true');
    element.setAttribute('tabindex', '0');
    
    // Guarda en localStorage
    const value = element.dataset.value;
    if (value) consultorioStorage.updateStatus(value);
  };
};

// WHY: El debounce de 500ms evita escrituras excesivas en LocalStorage mientras el usuario aún está escribiendo observaciones
const initObservations = () => {
  const textarea = safeGetElement('obsTextarea');
  if (!textarea) return;
  
  // Priorizar observaciones del servidor si existen
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverObs = serverData.observaciones;
  
  if (serverObs && serverObs.trim()) {
    // Usar observaciones del servidor
    textarea.value = serverObs;
    // Guardar en localStorage para mantener consistencia
    consultorioStorage.updateObservations(serverObs);
  } else {
    // Cargar observaciones guardadas localmente
    const saved = consultorioStorage.load().observations;
    if (saved) textarea.value = saved;
  }
  
  // Auto-guarda mientras el usuario escribe (con debounce)
  const debouncedSave = debounce(() => {
    consultorioStorage.updateObservations(textarea.value);
  }, 500);
  
  textarea.addEventListener('input', debouncedSave);
};

// Helper para obtener headers incluyendo AntiForgery token
const getRequestHeaders = () => {
  const headers = { 'Content-Type': 'application/json' };
  const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
  if (tokenEl) {
    headers['RequestVerificationToken'] = tokenEl.value;
    headers['X-CSRF-TOKEN'] = tokenEl.value;
  }
  return headers;
};

// WHY: Los botones de confirmación realizan peticiones asíncronas con feedback de carga y validación de respuesta
const initConfirmButtons = () => {
  const btnPreparation = safeGetElement('btnConfirmPreparation');
  const btnStatus = safeGetElement('btnConfirmStatus');
  
  // Confirmar preparación completa
  if (btnPreparation) {
    btnPreparation.addEventListener('click', async () => {
      const progress = calculateProgress();
      
      if (progress.checked < progress.total) {
        window.ToastService.warning(`Completa ${progress.total - progress.checked} ítem(s) pendiente(s)`);
        return;
      }

      btnPreparation.disabled = true;
      btnPreparation.textContent = 'Enviando...';

      try {
        const serverData2 = window.smiletrackEstadoConsultorioData;
        if (serverData2?.consultorioId) {
          const res = await fetch(`/api/consultorios/${serverData2.consultorioId}/confirmar-estado`, {
            method: 'POST',
            headers: getRequestHeaders(),
            credentials: 'same-origin',
            body: JSON.stringify({
              estado: consultorioStorage.load().status,
              observaciones: document.getElementById('obsTextarea')?.value ?? ''
            })
          });

          if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Error al comunicarse con el servidor.');
          }
        }

        // Agrega entrada al historial y actualiza UI
        consultorioStorage.addToHistory('Auxiliar', 'Preparación del consultorio confirmada');
        initHistoryList();

        window.ToastService.success('Preparación del consultorio confirmada');
        btnPreparation.textContent = '✓ Confirmado';
        
        setTimeout(() => {
          btnPreparation.disabled = false;
          btnPreparation.textContent = 'Confirmar preparación completa';
        }, 3000);
      } catch (err) {
        console.error('[SmileTrack] Error en confirmación de preparación:', err);
        window.ToastService.error('Error al guardar', err?.message || 'No se pudo registrar la preparación.');
        btnPreparation.disabled = false;
        btnPreparation.textContent = 'Confirmar preparación completa';
      }
    });
  }
  
  // Confirmar estado actual
  if (btnStatus) {
    btnStatus.addEventListener('click', async () => {
      const selectedOption = document.querySelector('.status-option.selected');
      const status = selectedOption?.querySelector('strong')?.textContent || 'Desconocido';

      btnStatus.disabled = true;
      btnStatus.textContent = 'Enviando...';

      try {
        const serverData2 = window.smiletrackEstadoConsultorioData;
        if (serverData2?.consultorioId) {
          const res = await fetch(`/api/consultorios/${serverData2.consultorioId}/confirmar-estado`, {
            method: 'POST',
            headers: getRequestHeaders(),
            credentials: 'same-origin',
            body: JSON.stringify({
              estado: consultorioStorage.load().status,
              observaciones: document.getElementById('obsTextarea')?.value ?? ''
            })
          });

          if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Error al comunicarse con el servidor.');
          }
        }

        // Agrega entrada al historial y actualiza UI
        consultorioStorage.addToHistory('Auxiliar', `Estado actualizado a: ${status}`);
        initHistoryList();

        window.ToastService.success(`Estado actualizado: ${status}`);
        btnStatus.textContent = '✓ Confirmado';
        
        setTimeout(() => {
          btnStatus.disabled = false;
          btnStatus.textContent = 'Confirmar estado';
        }, 3000);
      } catch (err) {
        console.error('[SmileTrack] Error en confirmación de estado:', err);
        window.ToastService.error('Error al actualizar', err?.message || 'No se pudo actualizar el estado.');
        btnStatus.disabled = false;
        btnStatus.textContent = 'Confirmar estado';
      }
    });
  }
  
  // Expone funciones globalmente para compatibilidad con onclick del HTML original
  window.confirmPreparation = () => btnPreparation?.click();
  window.confirmStatus = () => btnStatus?.click();
};

// WHY: El historial de estados se renderiza dinámicamente para reflejar las confirmaciones realizadas durante la sesión
const initHistoryList = () => {
  const list = document.querySelector('.history-list');
  if (!list) return;
  
  const state = consultorioStorage.load();
  
  // Formatea fecha para mostrar
  const formatDate = (iso) => {
    const d = new Date(iso);
    const months = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    return `${String(d.getDate()).padStart(2,'0')} ${months[d.getMonth()]} ${d.getFullYear()} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
  };
  
  // Renderiza historial
  list.replaceChildren(...state.history.map(entry => {
    const item = document.createElement('li');
    item.setAttribute('role', 'listitem');
    const time = document.createElement('p');
    time.className = 'history-time';
    const timeElement = document.createElement('time');
    timeElement.dateTime = entry.time;
    timeElement.textContent = formatDate(entry.time);
    time.appendChild(timeElement);
    const detail = document.createElement('p');
    detail.className = 'history-detail';
    detail.textContent = `${entry.user} · ${entry.detail}`;
    item.append(time, detail);
    return item;
  }));
};

// Función principal de inicialización
const init = async () => {
    // Cargar lista de consultorios
    await loadConsultorios();
    
    // Inicializar componentes de UI (ahora cada uno carga del servidor si está disponible)
    initMobileMenu();
    initChecklist();
    initAddItem();
    initStatusSelector();
    initObservations();
    initConfirmButtons();
    initHistoryList();

    // Cargar datos del servidor si están disponibles
    const serverData = window.smiletrackEstadoConsultorioData;
    if (serverData) {
        // Actualizar subtítulo del header
        const subtitle = document.getElementById('consultorioSubtitle');
        if (subtitle) {
            const partes = [serverData.nombre];
            if (serverData.ubicacion) partes.push(serverData.ubicacion);
            subtitle.textContent = partes.join(' · ');
        }

        // Re-renderizar historial con datos del servidor
        initHistoryList();
    }
    
    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
};

// Carga la lista de consultorios disponibles y permite al auxiliar cambiar de consultorio
async function loadConsultorios() {
  const select = safeGetElement('selectConsultorio');
  if (!select) return;

  try {
    const response = await fetch('/api/consultorios', {
      credentials: 'same-origin',
      headers: getRequestHeaders()
    });

    if (!response.ok) throw new Error(`Error ${response.status}`);

    const result = await response.json();
    const consultorios = result.data || [];

    // Limpiar opciones actuales
    select.replaceChildren();

    // Agregar opciones de consultorios
    consultorios.forEach(c => {
      const option = document.createElement('option');
      option.value = c.id;
      option.textContent = `${c.nombre}${c.ubicacion ? ` - ${c.ubicacion}` : ''}`;
      select.appendChild(option);
    });

    // Seleccionar el consultorio actual
    const currentId = window.smiletrackEstadoConsultorioData?.consultorioId;
    if (currentId) {
      select.value = currentId;
    }

    // Event listener para cambio de consultorio
    select.addEventListener('change', handleConsultorioChange);

  } catch (error) {
    console.error('[SmileTrack] Error al cargar consultorios:', error);
    window.ToastService?.error('Error', 'No se pudo cargar la lista de consultorios');
    
    // Fallback: mostrar consultorio actual si está disponible
    if (window.smiletrackEstadoConsultorioData?.consultorioId) {
      const option = document.createElement('option');
      option.value = window.smiletrackEstadoConsultorioData.consultorioId;
      option.textContent = window.smiletrackEstadoConsultorioData.nombre || 'Consultorio actual';
      select.replaceChildren(option);
    }
  }
}

// Maneja el cambio de consultorio seleccionado
async function handleConsultorioChange(event) {
  const select = event.target;
  const newConsultorioId = parseInt(select.value);
  
  if (!newConsultorioId || isNaN(newConsultorioId)) return;

  // Mostrar indicador de carga
  const originalText = select.options[select.selectedIndex]?.text || '';
  select.disabled = true;

  try {
    // Guardar preferencia en el backend
    const response = await fetch('/api/consultorios/guardar-preferencia', {
      method: 'POST',
      credentials: 'same-origin',
      headers: getRequestHeaders(),
      body: JSON.stringify({ consultorioId: newConsultorioId })
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.message || 'Error al guardar preferencia');
    }

    // Recargar la página con el nuevo consultorio
    window.location.href = `/gestion-de-citas/st-aux-09-estado-consultorio?consultorioId=${newConsultorioId}`;

  } catch (error) {
    console.error('[SmileTrack] Error al cambiar consultorio:', error);
    window.ToastService?.error('Error', error.message || 'No se pudo cambiar de consultorio');
    
    // Revertir selección
    const currentId = window.smiletrackEstadoConsultorioData?.consultorioId;
    if (currentId) {
      select.value = currentId;
    }
    select.disabled = false;
  }
}

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);