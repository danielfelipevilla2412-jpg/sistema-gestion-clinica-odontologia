/**
 * SMILETRACK — CONTROL POSTOPERATORIO (app.js)
 * Lógica con persistencia, accesibilidad y validaciones
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

// Estado en memoria del control postoperatorio, cargado desde el servidor
// (ver window.smiletrackPostopData, inyectado por control-post.cshtml). Se persiste
// de verdad al hacer clic en "Guardar registro" (antes solo se guardaba en
// localStorage y nunca llegaba a la base de datos).
const postopState = {
  citaId: window.smiletrackPostopData?.citaId ?? null,
  status: window.smiletrackPostopData?.status || 'stable',
  instructions: (window.smiletrackPostopData?.instructions || []).map(i => ({ text: i.text, checked: !!i.checked })),
  observations: window.smiletrackPostopData?.observations || ''
};

function renderPostopSubtitle() {
  const el = safeGetElement('postopSubtitle');
  if (!el) return;
  const d = window.smiletrackPostopData;
  if (!d || !d.citaId) {
    el.textContent = 'No hay una cita post-operatoria seleccionada';
    return;
  }
  const fecha = d.fecha ? new Date(d.fecha) : null;
  const fechaTexto = fecha && !Number.isNaN(fecha.getTime())
    ? fecha.toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' })
    : '';
  el.textContent = `${d.paciente} · ${d.procedimiento}${fechaTexto ? ' · Finalizado ' + fechaTexto : ''}`;
}

async function guardarControlPostoperatorio(silencioso = false) {
  if (!postopState.citaId) return;
  try {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const resp = await fetch('/historia-clinica/st-aux-07-control-postoperato/guardar', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { 'RequestVerificationToken': token } : {})
      },
      body: JSON.stringify(postopState)
    });
    const result = await resp.json();
    if (!result.success) throw new Error(result.message || 'No se pudo guardar');
    if (!silencioso) showToast('✅ Registro guardado en la base de datos', 'success');
  } catch (e) {
    console.error('Error al guardar el control postoperatorio:', e);
    if (!silencioso) showToast('No se pudo guardar el registro', 'error');
  }
}

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

// Inicializa botones de estado con persistencia y accesibilidad
const initStatusButtons = () => {
  const buttons = document.querySelectorAll('.status-btn');

  const savedStatus = postopState.status;

  buttons.forEach(btn => {
    // Aplica estado guardado
    const statusId = btn.id.replace('status', '').toLowerCase();
    const isActive = statusId === savedStatus;
    
    if (isActive) {
      btn.classList.add('active');
      btn.setAttribute('aria-checked', 'true');
    } else {
      btn.classList.remove('active');
      btn.setAttribute('aria-checked', 'false');
    }
    
    // Maneja click para cambio de estado
    btn.addEventListener('click', () => {
      // Remueve active de todos los botones
      buttons.forEach(b => {
        b.classList.remove('active');
        b.setAttribute('aria-checked', 'false');
      });
      
      // Activa el botón clickeado
      btn.classList.add('active');
      btn.setAttribute('aria-checked', 'true');
      
      postopState.status = btn.id.replace('status', '').toLowerCase();
      
      // Feedback visual
      showToast(`Estado actualizado: ${btn.querySelector('strong')?.textContent}`, 'info');
    });
    
    // Soporte para teclado en botones de estado
    btn.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        btn.click();
      }
    });
  });
};

// Inicializa lista de instrucciones con persistencia y animaciones
const initInstructions = () => {
  const list = safeGetElement('instructionsList');
  const input = safeGetElement('newInstructionInput');
  const addBtn = safeGetElement('btnAddInstruction');
  
  if (!list || !input || !addBtn) return;

  const savedInstructions = postopState.instructions;
  
  // Limpia lista actual y renderiza desde estado guardado
  list.innerHTML = '';
  savedInstructions.forEach((item, index) => {
    const li = document.createElement('li');
    li.setAttribute('role', 'listitem');
    
    li.innerHTML = `
      <label class="custom-checkbox">
        <input type="checkbox" ${item.checked ? 'checked' : ''} aria-label="${item.text}${item.checked ? ' - completado' : ''}" />
        <span class="checkmark" aria-hidden="true"></span>
        <span class="text">${item.text}</span>
      </label>
    `;
    
    // Maneja cambio de checkbox
    const checkbox = li.querySelector('input');
    checkbox.addEventListener('change', () => {
      postopState.instructions[index].checked = checkbox.checked;
      checkbox.setAttribute('aria-label', `${item.text}${checkbox.checked ? ' - completado' : ''}`);
    });
    
    list.appendChild(li);
  });
  
  // Maneja agregar nueva instrucción
  const addInstruction = () => {
    const text = input.value.trim();
    if (!text) {
      showToast('Por favor escribe una instrucción', 'warning');
      input.focus();
      return;
    }

    postopState.instructions.push({ text, checked: false });
    const newIndex = postopState.instructions.length - 1;
    
    // Crea nuevo elemento en la lista
    const li = document.createElement('li');
    li.setAttribute('role', 'listitem');
    
    li.innerHTML = `
      <label class="custom-checkbox">
        <input type="checkbox" aria-label="${text}" />
        <span class="checkmark" aria-hidden="true"></span>
        <span class="text">${text}</span>
      </label>
    `;
    
    // Animación de entrada
    li.style.opacity = '0';
    li.style.transform = 'translateY(-8px)';
    li.style.transition = 'opacity .2s, transform .2s';
    
    list.appendChild(li);
    
    // Trigger reflow y animar
    requestAnimationFrame(() => {
      li.style.opacity = '1';
      li.style.transform = 'translateY(0)';
    });
    
    // Maneja cambio del nuevo checkbox
    const checkbox = li.querySelector('input');
    checkbox.addEventListener('change', () => {
      postopState.instructions[newIndex].checked = checkbox.checked;
      checkbox.setAttribute('aria-label', `${text}${checkbox.checked ? ' - completado' : ''}`);
    });
    
    // Limpia input y enfoca
    input.value = '';
    input.focus();
    
    showToast('Instrucción agregada', 'success');
  };
  
  // Event listeners para agregar instrucción
  addBtn.addEventListener('click', addInstruction);
  input.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addInstruction();
    }
  });
  
  // Auto-guarda de observaciones con debounce
  const obsTextarea = safeGetElement('obsTextarea');
  if (obsTextarea) {
    obsTextarea.value = postopState.observations;
    
    // Guarda en memoria mientras el usuario escribe (se persiste en el servidor
    // al presionar "Guardar registro")
    const debouncedSave = debounce(() => {
      postopState.observations = obsTextarea.value;
    }, 500);
    
    obsTextarea.addEventListener('input', debouncedSave);
  }
};

// Inicializa botón de guardar registro
const initSaveButton = () => {
  const btnSave = safeGetElement('btnSaveRecord');
  if (!btnSave) return;
  
  btnSave.addEventListener('click', async () => {
    const original = btnSave.innerHTML;
    btnSave.innerHTML = '⏳ Guardando...';
    btnSave.disabled = true;

    await guardarControlPostoperatorio();

    btnSave.innerHTML = '✓ Guardado';
    setTimeout(() => {
      btnSave.innerHTML = original;
      btnSave.disabled = false;
    }, 2000);
  });
};

// Función principal de inicialización
const init = () => {
  renderPostopSubtitle();

  // Inicializar componentes de UI
  initMobileMenu();
  initStatusButtons();
  initInstructions();
  initSaveButton();
  
  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);