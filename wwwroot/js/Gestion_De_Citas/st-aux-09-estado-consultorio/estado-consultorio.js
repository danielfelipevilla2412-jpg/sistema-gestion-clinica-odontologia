/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Estado del Consultorio (st-aux-09-estado-consultorio)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Administra la disponibilidad y estado operacional de los consultorios odontológicos (Disponible, En Desinfección, Mantenimiento).
 * Permite al equipo auxiliar completar checklists de higienización entre citas y registrar observaciones.
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Progresión de checklist con cálculo porcentual de preparación del consultorio.
 * - Registro del timestamp de desinfección antes de liberar el consultorio para la siguiente cita.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux09EstadoConsultorio
 * - HTML: Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const SYNC_DEBOUNCE_MS = 400;
const MAX_HISTORY_ENTRIES = 10;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

let consultorioSyncTimerRef = null;
let consultorioServerStateRef = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const getRequestHeaders = () => {
  const headers = { 'Content-Type': 'application/json' };
  const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
  if (tokenElement) {
    headers['RequestVerificationToken'] = tokenElement.value;
    headers['X-CSRF-TOKEN'] = tokenElement.value;
  }
  return headers;
};

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

const formatHistoryTimestamp = (isoString) => {
  const dateObj = new Date(isoString);
  const monthNames = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
  return `${String(dateObj.getDate()).padStart(2,'0')} ${monthNames[dateObj.getMonth()]} ${dateObj.getFullYear()} ${String(dateObj.getHours()).padStart(2,'0')}:${String(dateObj.getMinutes()).padStart(2,'0')}`;
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

const scheduleConsultorioSync = () => {
  clearTimeout(consultorioSyncTimerRef);
  consultorioSyncTimerRef = setTimeout(saveConsultorioState, SYNC_DEBOUNCE_MS);
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

async function loadConsultorios() {
  const selectElement = safeGetElement('selectConsultorio');
  if (!selectElement) return;

  try {
    const response = await fetch('/api/consultorios', {
      credentials: 'same-origin',
      headers: getRequestHeaders()
    });

    if (!response.ok) throw new Error(`Error ${response.status}`);

    const result = await response.json();
    const consultoriosList = result.data || [];

    selectElement.replaceChildren();

    consultoriosList.forEach(item => {
      const optionElement = document.createElement('option');
      optionElement.value = item.id;
      optionElement.textContent = `${item.nombre}${item.ubicacion ? ` - ${item.ubicacion}` : ''}`;
      selectElement.appendChild(optionElement);
    });

    const currentId = window.smiletrackEstadoConsultorioData?.consultorioId;
    if (currentId) {
      selectElement.value = currentId;
    }

    selectElement.addEventListener('change', handleConsultorioChange);

  } catch (error) {
    console.error('[SmileTrack] Error al cargar consultorios:', error);
    if (window.ToastService) window.ToastService.error('Error', 'No se pudo cargar la lista de consultorios');
    
    if (window.smiletrackEstadoConsultorioData?.consultorioId) {
      const fallbackOption = document.createElement('option');
      fallbackOption.value = window.smiletrackEstadoConsultorioData.consultorioId;
      fallbackOption.textContent = window.smiletrackEstadoConsultorioData.nombre || 'Consultorio actual';
      selectElement.replaceChildren(fallbackOption);
    }
  }
}

async function handleConsultorioChange(event) {
  const selectElement = event.target;
  const newConsultorioId = parseInt(selectElement.value, 10);
  
  if (!newConsultorioId || isNaN(newConsultorioId)) return;

  selectElement.disabled = true;

  try {
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

    window.location.href = `/gestion-de-citas/st-aux-09-estado-consultorio?consultorioId=${newConsultorioId}`;

  } catch (error) {
    console.error('[SmileTrack] Error al cambiar consultorio:', error);
    if (window.ToastService) window.ToastService.error('Error', error.message || 'No se pudo cambiar de consultorio');
    
    const currentId = window.smiletrackEstadoConsultorioData?.consultorioId;
    if (currentId) {
      selectElement.value = currentId;
    }
    selectElement.disabled = false;
  }
}

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const consultorioStorage = {
  get key() {
    const currentData = window.smiletrackEstadoConsultorioData || {};
    return `smiletrack_consultorio_${currentData.consultorioId || 'sin_consultorio'}_${new Date().toISOString().slice(0, 10)}`;
  },
  
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
      history: []
    };
  },
  
  save: (state) => {
    try {
      localStorage.setItem(consultorioStorage.key, JSON.stringify(state));
      return true;
    } catch (e) {
      console.error('Error al guardar estado del consultorio:', e);
      return false;
    }
  },
  
  updateChecklistItem: (index, checked) => {
    const state = consultorioStorage.load();
    if (state.checklist[index]) {
      state.checklist[index].checked = checked;
      consultorioStorage.save(state);
      scheduleConsultorioSync();
    }
  },
  
  addChecklistItem: (text) => {
    const state = consultorioStorage.load();
    state.checklist.push({ text, checked: false });
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  },
  
  updateStatus: (status) => {
    const state = consultorioStorage.load();
    state.status = status;
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  },
  
  updateObservations: (text) => {
    const state = consultorioStorage.load();
    state.observations = text;
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  },
  
  addToHistory: (user, detail) => {
    const state = consultorioStorage.load();
    state.history.unshift({
      time: new Date().toISOString(),
      user,
      detail
    });
    if (state.history.length > MAX_HISTORY_ENTRIES) state.history.pop();
    consultorioStorage.save(state);
    scheduleConsultorioSync();
  }
};

const updateProgressUI = () => {
  const progressBar = safeGetElement('progressBar');
  const progressInfo = safeGetElement('progressInfo');
  const progressText = safeGetElement('progressText');
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
};

const initChecklist = () => {
  const checklistContainer = safeGetElement('checklistItems');
  if (!checklistContainer) return;
  
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverChecklist = serverData.checklist;
  let state = consultorioStorage.load();
  
  if (serverChecklist && Array.isArray(serverChecklist) && serverChecklist.length > 0) {
    state.checklist = serverChecklist.map(item => ({
      text: item.text || item.Text || String(item),
      checked: Boolean(item.checked || item.Checked)
    }));
    consultorioStorage.save(state);
  }
  
  checklistContainer.replaceChildren();
  state.checklist.forEach((item, index) => {
    const listItem = document.createElement('li');
    listItem.setAttribute('role', 'listitem');
    const labelElement = document.createElement('label');
    labelElement.className = 'check-item';
    const checkboxElement = document.createElement('input');
    checkboxElement.type = 'checkbox';
    checkboxElement.checked = Boolean(item.checked);
    checkboxElement.setAttribute('aria-label', `${item.text}${item.checked ? ' - completado' : ''}`);
    const checkmarkSpan = document.createElement('span');
    checkmarkSpan.className = 'checkmark';
    checkmarkSpan.setAttribute('aria-hidden', 'true');
    const textSpan = document.createElement('span');
    textSpan.className = 'text';
    textSpan.textContent = item.text;
    labelElement.append(checkboxElement, checkmarkSpan, textSpan);
    listItem.appendChild(labelElement);
    
    checkboxElement.addEventListener('change', () => {
      consultorioStorage.updateChecklistItem(index, checkboxElement.checked);
      checkboxElement.setAttribute('aria-label', `${item.text}${checkboxElement.checked ? ' - completado' : ''}`);
      updateProgressUI();
    });
    
    checklistContainer.appendChild(listItem);
  });
  
  updateProgressUI();
};

const initAddItem = () => {
  const inputElement = safeGetElement('newItemInput');
  const addButton = safeGetElement('btnAddItem');
  const checklistContainer = safeGetElement('checklistItems');
  
  if (!inputElement || !addButton || !checklistContainer) return;
  
  const addItemHandler = () => {
    const text = inputElement.value.trim();
    if (!text) {
      if (window.ToastService) window.ToastService.warning('Por favor escribe un ítem');
      inputElement.focus();
      return;
    }
    
    consultorioStorage.addChecklistItem(text);
    
    const listItem = document.createElement('li');
    listItem.setAttribute('role', 'listitem');
    
    const labelElement = document.createElement('label');
    labelElement.className = 'check-item';
    const checkboxElement = document.createElement('input');
    checkboxElement.type = 'checkbox';
    checkboxElement.setAttribute('aria-label', text);
    const checkmarkSpan = document.createElement('span');
    checkmarkSpan.className = 'checkmark';
    checkmarkSpan.setAttribute('aria-hidden', 'true');
    const textSpan = document.createElement('span');
    textSpan.className = 'text';
    textSpan.textContent = text;
    labelElement.append(checkboxElement, checkmarkSpan, textSpan);
    listItem.appendChild(labelElement);
    
    checkboxElement.addEventListener('change', () => {
      const newIndex = consultorioStorage.load().checklist.length - 1;
      consultorioStorage.updateChecklistItem(newIndex, checkboxElement.checked);
      checkboxElement.setAttribute('aria-label', `${text}${checkboxElement.checked ? ' - completado' : ''}`);
      updateProgressUI();
    });
    
    listItem.style.opacity = '0';
    listItem.style.transform = 'translateY(-8px)';
    listItem.style.transition = 'opacity .2s, transform .2s';
    
    checklistContainer.appendChild(listItem);
    
    requestAnimationFrame(() => {
      listItem.style.opacity = '1';
      listItem.style.transform = 'translateY(0)';
    });
    
    inputElement.value = '';
    inputElement.focus();
    
    updateProgressUI();
    if (window.ToastService) window.ToastService.success('Ítem agregado');
  };
  
  addButton.addEventListener('click', addItemHandler);
  inputElement.addEventListener('keypress', (event) => {
    if (event.key === 'Enter') {
      event.preventDefault();
      addItemHandler();
    }
  });
  
  window.addItem = addItemHandler;
};

const initStatusSelector = () => {
  const optionsList = document.querySelectorAll('.status-option');
  
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverStatus = serverData.estadoActual;
  let savedStatus = consultorioStorage.load().status;
  
  if (serverStatus) {
    const normalizedStatus = serverStatus.toLowerCase().replace(/_/g, '-');
    savedStatus = normalizedStatus;
    consultorioStorage.updateStatus(normalizedStatus);
  }
  
  optionsList.forEach(optionElement => {
    const optionValue = optionElement.dataset.value;
    const isActive = optionValue === savedStatus;
    
    if (isActive) {
      optionElement.classList.add('selected');
      optionElement.setAttribute('aria-checked', 'true');
      optionElement.setAttribute('tabindex', '0');
    } else {
      optionElement.classList.remove('selected');
      optionElement.setAttribute('aria-checked', 'false');
      optionElement.setAttribute('tabindex', '-1');
    }
    
    optionElement.addEventListener('click', () => {
      optionsList.forEach(opt => {
        opt.classList.remove('selected');
        opt.setAttribute('aria-checked', 'false');
        opt.setAttribute('tabindex', '-1');
      });
      
      optionElement.classList.add('selected');
      optionElement.setAttribute('aria-checked', 'true');
      optionElement.setAttribute('tabindex', '0');
      
      consultorioStorage.updateStatus(optionValue);
      if (window.ToastService) window.ToastService.success(`Estado actualizado: ${optionElement.querySelector('strong')?.textContent}`, 'info');
    });
    
    optionElement.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        optionElement.click();
      } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const currentIndex = Array.from(optionsList).indexOf(optionElement);
        const nextIndex = event.key === 'ArrowDown' 
          ? (currentIndex + 1) % optionsList.length 
          : (currentIndex - 1 + optionsList.length) % optionsList.length;
        optionsList[nextIndex]?.focus();
      }
    });
  });
  
  window.selectStatus = (element) => {
    optionsList.forEach(opt => {
      opt.classList.remove('selected');
      opt.setAttribute('aria-checked', 'false');
      opt.setAttribute('tabindex', '-1');
    });
    
    element.classList.add('selected');
    element.setAttribute('aria-checked', 'true');
    element.setAttribute('tabindex', '0');
    
    const value = element.dataset.value;
    if (value) consultorioStorage.updateStatus(value);
  };
};

const initObservations = () => {
  const textareaElement = safeGetElement('obsTextarea');
  if (!textareaElement) return;
  
  const serverData = window.smiletrackEstadoConsultorioData || {};
  const serverObs = serverData.observaciones;
  
  if (serverObs && serverObs.trim()) {
    textareaElement.value = serverObs;
    consultorioStorage.updateObservations(serverObs);
  } else {
    const savedText = consultorioStorage.load().observations;
    if (savedText) textareaElement.value = savedText;
  }
  
  const debouncedSaveHandler = debounce(() => {
    consultorioStorage.updateObservations(textareaElement.value);
  }, 500);
  
  textareaElement.addEventListener('input', debouncedSaveHandler);
};

const initHistoryList = () => {
  const historyListContainer = document.querySelector('.history-list');
  if (!historyListContainer) return;
  
  const state = consultorioStorage.load();
  
  historyListContainer.replaceChildren(...state.history.map(entry => {
    const itemElement = document.createElement('li');
    itemElement.setAttribute('role', 'listitem');
    const timeParagraph = document.createElement('p');
    timeParagraph.className = 'history-time';
    const timeElement = document.createElement('time');
    timeElement.dateTime = entry.time;
    timeElement.textContent = formatHistoryTimestamp(entry.time);
    timeParagraph.appendChild(timeElement);
    const detailParagraph = document.createElement('p');
    detailParagraph.className = 'history-detail';
    detailParagraph.textContent = `${entry.user} · ${entry.detail}`;
    itemElement.append(timeParagraph, detailParagraph);
    return itemElement;
  }));
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const initConfirmButtons = () => {
  const btnPreparation = safeGetElement('btnConfirmPreparation');
  const btnStatus = safeGetElement('btnConfirmStatus');
  
  if (btnPreparation) {
    btnPreparation.addEventListener('click', async () => {
      const progress = calculateProgress();
      
      if (progress.checked < progress.total) {
        if (window.ToastService) window.ToastService.warning(`Completa ${progress.total - progress.checked} ítem(s) pendiente(s)`);
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

        consultorioStorage.addToHistory('Auxiliar', 'Preparación del consultorio confirmada');
        initHistoryList();

        if (window.ToastService) window.ToastService.success('Preparación del consultorio confirmada');
        btnPreparation.textContent = '✓ Confirmado';
        
        setTimeout(() => {
          btnPreparation.disabled = false;
          btnPreparation.textContent = 'Confirmar preparación completa';
        }, 3000);
      } catch (err) {
        console.error('[SmileTrack] Error en confirmación de preparación:', err);
        if (window.ToastService) window.ToastService.error('Error al guardar', err?.message || 'No se pudo registrar la preparación.');
        btnPreparation.disabled = false;
        btnPreparation.textContent = 'Confirmar preparación completa';
      }
    });
  }
  
  if (btnStatus) {
    btnStatus.addEventListener('click', async () => {
      const selectedOption = document.querySelector('.status-option.selected');
      const statusLabelText = selectedOption?.querySelector('strong')?.textContent || 'Desconocido';

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

        consultorioStorage.addToHistory('Auxiliar', `Estado actualizado a: ${statusLabelText}`);
        initHistoryList();

        if (window.ToastService) window.ToastService.success(`Estado actualizado: ${statusLabelText}`);
        btnStatus.textContent = '✓ Confirmado';
        
        setTimeout(() => {
          btnStatus.disabled = false;
          btnStatus.textContent = 'Confirmar estado';
        }, 3000);
      } catch (err) {
        console.error('[SmileTrack] Error en confirmación de estado:', err);
        if (window.ToastService) window.ToastService.error('Error al actualizar', err?.message || 'No se pudo actualizar el estado.');
        btnStatus.disabled = false;
        btnStatus.textContent = 'Confirmar estado';
      }
    });
  }
  
  window.confirmPreparation = () => btnPreparation?.click();
  window.confirmStatus = () => btnStatus?.click();
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const initMobileMenu = () => {
  // El menú móvil es gestionado centralizadamente por ~/js/shared/sidebar.js
};

const initializeEstadoConsultorioModule = async () => {
    await loadConsultorios();
    
    initMobileMenu();
    initChecklist();
    initAddItem();
    initStatusSelector();
    initObservations();
    initConfirmButtons();
    initHistoryList();

    const serverData = window.smiletrackEstadoConsultorioData;
    if (serverData) {
        const subtitleElement = document.getElementById('consultorioSubtitle');
        if (subtitleElement) {
            const subtitleParts = [serverData.nombre];
            if (serverData.ubicacion) subtitleParts.push(serverData.ubicacion);
            subtitleElement.textContent = subtitleParts.join(' · ');
        }

        initHistoryList();
    }
    
    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeEstadoConsultorioModule);