/* ============================================
SmileTrack — Asistencia en Procedimiento (st-aux-06-asistencia-procedi)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona el timer del procedimiento, la persistencia del estado de las etapas y la interactividad de las píldoras de control (Limpieza, Esterilización, Equipos) con feedback visual en tiempo real.

FUNCIONALIDADES PRINCIPALES:
- Timer de procedimiento con incremento cada 60 segundos persistido en LocalStorage
- Estado de las píldoras de etapas persistido en LocalStorage para continuidad ante refrescos de página
- Toggle visual de estado completado/pendiente de etapas con actualización de atributos aria-pressed
- Notificaciones toast no bloqueantes ante confirmación de cambio de estado de etapas

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.css
- JS: ~/js/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.js
- Partial / Otros: asistencia-proc.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- La clave de LocalStorage incluye la fecha y hora de inicio del procedimiento para evitar colisiones entre sesiones.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Debounce evita saturar la API con peticiones redundantes ante cambios veloces del usuario
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación al usuario sin interrumpir el flujo durante el procedimiento

// WHY: La clave incluye fecha y hora para evitar colisiones entre sesiones de diferentes procedimientos en el mismo dispositivo
const procedureStorage = {
  // La clave incluye el id real de la cita para no mezclar el estado entre procedimientos distintos
  key: `smiletrack_procedure_${window.smiletrackAsistenciaProcedData?.citaId || 'sin_cita'}`,
  
  // WHY: Carga el estado desde LocalStorage para continuar el seguimiento tras refrescos de página o re-apertura del navegador
  load: () => {
    const stored = localStorage.getItem(procedureStorage.key);
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch (e) {
        console.warn('Error al cargar estado del procedimiento, usando valores por defecto');
      }
    }
    // Procedimiento recién iniciado (0 minutos, ahora mismo)
    return {
      minutes: 0,
      startTime: new Date().toISOString(),
      pills: {
        limpieza: false,
        esterilizacion: false,
        equipos: false
      }
    };
  },
  
  // WHY: Persiste el estado inmediatamente después de cada cambio para garantizar integridad si se cierra el navegador inesperadamente
  save: (state) => {
    try {
      localStorage.setItem(procedureStorage.key, JSON.stringify(state));
      return true;
    } catch (e) {
      console.error('Error al guardar estado del procedimiento:', e);
      return false;
    }
  },
  
  // WHY: Guarda los minutos del timer de forma aislada para minimizar escrituras en LocalStorage
  updateMinutes: (minutes) => {
    const state = procedureStorage.load();
    state.minutes = minutes;
    procedureStorage.save(state);
  },
  
  // WHY: Actualiza solo la píldora modificada en lugar de reescribir el estado completo, optimizando escrituras
  updatePill: (pillId, completed) => {
    const state = procedureStorage.load();
    state.pills[pillId] = completed;
    procedureStorage.save(state);
  }
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

// WHY: Inicializa el timer del procedimiento cargando el valor persistido y ejecutando un intervalo de 60s
const initTimer = () => {
  const timerValue = safeGetElement('timerValue');
  if (!timerValue) return;
  
  // WHY: Carga el valor guardado en lugar de iniciar desde 0 para garantizar continuidad ante refrescos de página
  const state = procedureStorage.load();
  let minutes = state.minutes;
  
  // Actualiza display inicial
  timerValue.textContent = minutes;

  // Refleja la hora real de inicio guardada (antes era un dato estático hardcodeado)
  const statusTime = document.querySelector('.status-time time');
  if (statusTime && state.startTime) {
    const inicio = new Date(state.startTime);
    statusTime.setAttribute('datetime', state.startTime);
    statusTime.textContent = inicio.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
  }
  
  // WHY: Incrementa el timer en tiempo real para reflejar la duración real del procedimiento
  const timerInterval = setInterval(() => {
    minutes++;
    timerValue.textContent = minutes;
    procedureStorage.updateMinutes(minutes);
  }, 60000);
  
  // WHY: Limpia el intervalo al descargar la página para prevenir memory leaks en entornos de larga ejecución
  window.addEventListener('beforeunload', () => {
    clearInterval(timerInterval);
  });
};

// WHY: Centraliza la lógica de las píldoras para garantizar sincronización entre UI, aria-pressed y LocalStorage
const initProcedurePills = () => {
  const pills = [
    { el: safeGetElement('pillLimpieza'), id: 'limpieza', label: 'Limpieza' },
    { el: safeGetElement('pillEsterilizacion'), id: 'esterilizacion', label: 'Esterilización' },
    { el: safeGetElement('pillEquipos'), id: 'equipos', label: 'Equipos' }
  ];
  
  // WHY: Restaura el estado visual al montar el componente para continuar donde se dejó ante refrescos de página
  const savedState = procedureStorage.load().pills;
  pills.forEach(({ el, id, label }) => {
    if (!el) return;
    
    // Aplica estado guardado
    const isCompleted = savedState[id] || false;
    if (isCompleted) {
      el.classList.add('completed');
      el.setAttribute('aria-pressed', 'true');
      el.setAttribute('aria-label', `${label} completada`);
    }
    
    // WHY: Mantiene sincronizados aria-pressed y la clase CSS para cumplir requisitos WCAG de controles toggle
    el.addEventListener('click', () => {
      const wasCompleted = el.classList.contains('completed');
      const isNowCompleted = !wasCompleted;
      
      // Actualiza estado visual
      el.classList.toggle('completed', isNowCompleted);
      el.setAttribute('aria-pressed', isNowCompleted);
      el.setAttribute('aria-label', isNowCompleted ? `${label} completada` : `Marcar ${label} como completada`);
      
      // Guarda en localStorage
      procedureStorage.updatePill(id, isNowCompleted);
      
      // Feedback visual con toast
      showToast(`${label} ${isNowCompleted ? 'completada ✓' : 'marcada como pendiente'}`, isNowCompleted ? 'success' : 'warning');
    });
    
    // Soporte para teclado en píldoras
    el.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        el.click();
      }
    });
  });
};

// Función principal de inicialización
// Renderiza los datos reales de la cita/procedimiento inyectados por el servidor
// (ver ConstruirAsistenciaProcedimientoAsync en GestionCitasController.cs).
const renderDatosCita = () => {
  const d = window.smiletrackAsistenciaProcedData || {};

  const subtitle = safeGetElement('apSubtitle');
  if (subtitle) subtitle.textContent = d.citaId ? `${d.procedimiento} — ${d.profesional} — ${d.consultorio}` : 'No hay un procedimiento en curso asignado';

  const set = (id, val) => { const el = safeGetElement(id); if (el) el.textContent = val || '—'; };
  set('apPaciente', d.paciente);
  set('apProfesional', d.profesional);
  set('apServicio', d.procedimiento);
  set('apConsultorio', d.consultorio);

  const banner = safeGetElement('apAlertBanner');
  const bannerText = safeGetElement('apAlertBannerText');
  if (d.alergia && banner && bannerText) {
    bannerText.innerHTML = `<strong>ALERTA</strong> — ${d.paciente} — Alérgico a ${d.alergia}`;
    banner.style.display = '';
  }

  const alergiaItem = safeGetElement('apAlergiaItem');
  const alergiaTexto = safeGetElement('apAlergiaTexto');
  const antecedentesItem = safeGetElement('apAntecedentesItem');
  const antecedentesTexto = safeGetElement('apAntecedentesTexto');
  const sinAlertas = safeGetElement('apSinAlertas');

  let hayAlertas = false;
  if (d.alergia && alergiaItem && alergiaTexto) { alergiaTexto.textContent = d.alergia; alergiaItem.style.display = ''; hayAlertas = true; }
  if (d.antecedentes && antecedentesItem && antecedentesTexto) { antecedentesTexto.textContent = d.antecedentes; antecedentesItem.style.display = ''; hayAlertas = true; }
  if (hayAlertas && sinAlertas) sinAlertas.style.display = 'none';
};

const init = () => {
    // Inicializar componentes de UI
    renderDatosCita();
    initMobileMenu();
    initTimer();
    initProcedurePills();
    
    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);