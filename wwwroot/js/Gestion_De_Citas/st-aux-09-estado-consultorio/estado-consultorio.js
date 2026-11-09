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

const DEFAULT_CHECKLIST = Object.freeze([
  { text: 'Limpieza y desinfección de superficies', checked: false },
  { text: 'Instrumental esterilizado y empaquetado', checked: false },
  { text: 'Residuos biológicos eliminados', checked: false },
  { text: 'Guantes y tapabocas reabastecidos', checked: false },
  { text: 'Historia clínica lista para próximo', checked: false },
  { text: 'Equipos verificados y encendidos', checked: false },
  { text: 'Consultorio ventilado', checked: false }
]);

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
    const result = await response.json();
    if (result.statusChanged) {
      await refreshConsultorioHistory(serverData.consultorioId);
    }
  } catch (error) {
    console.warn('[SmileTrack] No se pudo sincronizar el estado del consultorio:', error);
    if (window.ToastService) window.ToastService.error('Error', 'No se pudo guardar el estado del consultorio.');
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

    const citaId = new URLSearchParams(window.location.search).get('citaId');
    const query = new URLSearchParams({ consultorioId: String(newConsultorioId) });
    if (citaId) query.set('citaId', citaId);
    window.location.href = `/gestion-de-citas/st-aux-09-estado-consultorio?${query}`;

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
        const parsed = JSON.parse(stored);
        if (parsed && Array.isArray(parsed.checklist) && parsed.checklist.length > 0) {
          return parsed;
        }
      } catch (e) {
        console.warn('[SmileTrack] Error al cargar estado local del consultorio, usando valores por defecto');
      }
    }
    return {
      checklist: DEFAULT_CHECKLIST.map(item => ({ ...item })),
      status: 'disponible',
      observations: ''
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
  } else if (!state.checklist || state.checklist.length === 0) {
    state.checklist = DEFAULT_CHECKLIST.map(item => ({ ...item }));
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
  const historyListContainer = safeGetElement('consultorioHistory');
  if (!historyListContainer) return;

  renderConsultorioHistory(
    window.smiletrackEstadoConsultorioData?.historial || [],
    historyListContainer
  );
};

const renderConsultorioHistory = (entries, container) => {
  const items = entries.map(entry => {
    const time = entry.time || entry.fechaCambio || entry.FechaCambio;
    const oldState = entry.estadoAnterior || entry.EstadoAnterior || '';
    const newState = entry.estadoNuevo || entry.EstadoNuevo || entry.estado || entry.Estado || '';
    const oldStateId = entry.idEstadoAnterior ?? entry.IdEstadoAnterior;
    const newStateId = entry.idEstadoNuevo ?? entry.IdEstadoNuevo;
    const user = entry.user || entry.usuario || entry.Usuario || 'Usuario no disponible';
    const detail = entry.detail || entry.motivo || entry.Motivo || '';
    const itemElement = document.createElement('li');
    itemElement.setAttribute('role', 'listitem');
    const timeParagraph = document.createElement('p');
    timeParagraph.className = 'history-time';
    const timeElement = document.createElement('time');
    timeElement.dateTime = time;
    timeElement.textContent = formatHistoryTimestamp(time);
    timeParagraph.appendChild(timeElement);
    const detailParagraph = document.createElement('p');
    detailParagraph.className = 'history-detail';
    detailParagraph.textContent = `${oldState || 'Estado inicial'}${oldStateId ? ` (#${oldStateId})` : ''} → ${newState}${newStateId ? ` (#${newStateId})` : ''} · ${user}${detail ? ` · ${detail}` : ''}`;
    itemElement.append(timeParagraph, detailParagraph);
    return itemElement;
  });

  if (items.length === 0) {
    const emptyElement = document.createElement('li');
    emptyElement.className = 'empty-history';
    emptyElement.textContent = 'Aún no hay cambios de estado registrados.';
    items.push(emptyElement);
  }
  container.replaceChildren(...items);
};

const initTodayAppointments = () => {
  const listElement = safeGetElement('todayAppointments');
  if (!listElement) return;

  const data = window.smiletrackEstadoConsultorioData || {};
  const selectedAppointmentId = Number(data.citaSeleccionadaId || 0);
  const appointments = Array.isArray(data.citas) ? data.citas : [];
  const items = appointments.map(appointment => {
    const item = document.createElement('li');
    item.className = Number(appointment.id) === selectedAppointmentId
      ? 'today-appointment selected'
      : 'today-appointment';

    const appointmentLink = document.createElement('a');
    appointmentLink.href = `/gestion-de-citas/st-aux-06-asistencia-procedi?citaId=${encodeURIComponent(appointment.id)}`;
    appointmentLink.className = 'appointment-link';

    const time = document.createElement('strong');
    time.className = 'appointment-time';
    time.textContent = appointment.hora || '';

    const details = document.createElement('span');
    details.className = 'appointment-details';
    details.textContent = `${appointment.paciente || 'Paciente'} · ${appointment.servicio || 'Sin servicio'}`;

    const professional = document.createElement('span');
    professional.className = 'appointment-professional';
    professional.textContent = `${appointment.profesional || 'Sin asignar'} · ${appointment.estado || ''}`;

    appointmentLink.append(time, details, professional);
    item.appendChild(appointmentLink);
    return item;
  });

  if (items.length === 0) {
    const emptyElement = document.createElement('li');
    emptyElement.className = 'empty-appointments';
    emptyElement.textContent = 'No hay citas activas para hoy en este consultorio.';
    items.push(emptyElement);
  }
  listElement.replaceChildren(...items);
};

const refreshConsultorioHistory = async (consultorioId) => {
  if (!consultorioId) return false;
  try {
    const response = await fetch(`/api/consultorios/${consultorioId}/estado-operativo`, {
      credentials: 'same-origin',
      headers: getRequestHeaders()
    });
    if (!response.ok) throw new Error(`status ${response.status}`);
    const result = await response.json();
    const history = result.data?.historial || result.data?.Historial || [];
    const historyList = safeGetElement('consultorioHistory');
    if (!historyList) return false;
    renderConsultorioHistory(history, historyList);
    return true;
  } catch (error) {
    console.error('[SmileTrack] No se pudo actualizar el historial del consultorio:', error);
    if (window.ToastService) window.ToastService.warning('El cambio se guardó, pero no se pudo actualizar el historial en pantalla.');
    return false;
  }
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

      clearTimeout(consultorioSyncTimerRef);
      btnPreparation.disabled = true;
      btnPreparation.textContent = 'Enviando...';

      try {
        const serverData2 = window.smiletrackEstadoConsultorioData;
        const currentState = consultorioStorage.load();
        const obsValue = document.getElementById('obsTextarea')?.value ?? '';

        if (serverData2?.consultorioId) {
          const res = await fetch(`/api/consultorios/${serverData2.consultorioId}/confirmar-estado`, {
            method: 'POST',
            headers: getRequestHeaders(),
            credentials: 'same-origin',
            body: JSON.stringify({
              estado: 'disponible',
              checklist: currentState.checklist,
              observaciones: obsValue,
              forzarHistorial: true
            })
          });

          if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Error al comunicarse con el servidor.');
          }

          consultorioStorage.updateStatus('disponible');
          const optionDisponible = document.querySelector('.status-option[data-value="disponible"]');
          if (optionDisponible) {
            document.querySelectorAll('.status-option').forEach(opt => {
              opt.classList.remove('selected');
              opt.setAttribute('aria-checked', 'false');
              opt.setAttribute('tabindex', '-1');
            });
            optionDisponible.classList.add('selected');
            optionDisponible.setAttribute('aria-checked', 'true');
            optionDisponible.setAttribute('tabindex', '0');
          }
        }

        await refreshConsultorioHistory(serverData2?.consultorioId);

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
      const statusValue = selectedOption?.dataset.value || consultorioStorage.load().status;

      clearTimeout(consultorioSyncTimerRef);
      btnStatus.disabled = true;
      btnStatus.textContent = 'Enviando...';

      try {
        const serverData2 = window.smiletrackEstadoConsultorioData;
        const currentState = consultorioStorage.load();
        const obsValue = document.getElementById('obsTextarea')?.value ?? '';

        if (serverData2?.consultorioId) {
          const res = await fetch(`/api/consultorios/${serverData2.consultorioId}/confirmar-estado`, {
            method: 'POST',
            headers: getRequestHeaders(),
            credentials: 'same-origin',
            body: JSON.stringify({
              estado: statusValue,
              checklist: currentState.checklist,
              observaciones: obsValue
            })
          });

          if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Error al comunicarse con el servidor.');
          }
        }

        await refreshConsultorioHistory(serverData2?.consultorioId);

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
    initTodayAppointments();

    const serverData = window.smiletrackEstadoConsultorioData;
    if (serverData) {
        const subtitleElement = document.getElementById('consultorioSubtitle');
        if (subtitleElement) {
            const subtitleParts = [serverData.nombre || 'Consultorio'];
            if (serverData.ubicacion) subtitleParts.push(serverData.ubicacion);
            subtitleElement.textContent = subtitleParts.join(' · ');
        }
    }
    
    window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeEstadoConsultorioModule);