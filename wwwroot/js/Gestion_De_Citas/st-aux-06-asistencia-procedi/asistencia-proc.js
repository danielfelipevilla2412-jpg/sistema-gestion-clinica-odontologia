/* ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: Asistencia en Procedimiento (st-aux-06-asistencia-procedi)
 * ============================================
 * Archivo: wwwroot/js/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.js
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Asiste al auxiliar clínico durante la ejecución de un procedimiento odontológico en tiempo real.
 * Mantiene un cronómetro de la intervención y gestiona listas de verificación (esterilización, instrumentación).
 *
 * REGLAS DE NEGOCIO Y COMPORTAMIENTO CLIENTE:
 * - Registro del tiempo transcurrido mediante temporizador.
 * - Guardado de estado en sesión para evitar pérdida de datos si la página se recarga accidentalmente.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - Controller: GestionCitasController -> Staux06AsistenciaProcedi
 * - HTML: Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml
 * ============================================ */

// ═══════════════════════════════════════════════════════════════════
// 1. CONSTANTES Y CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const SYNC_DEBOUNCE_MS = 250;
const TIMER_INTERVAL_MS = 60000;

// ═══════════════════════════════════════════════════════════════════
// 2. ESTADO DE LA APLICACIÓN
// ═══════════════════════════════════════════════════════════════════

const procedureData = window.smiletrackAsistenciaProcedData || {};
let procedureSyncTimerRef = null;
let procedureTimerIntervalRef = null;

// ═══════════════════════════════════════════════════════════════════
// 3. UTILIDADES Y HELPERS
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (elementId) =>
  window.CommonUtils?.safeGetElement ? window.CommonUtils.safeGetElement(elementId) : document.getElementById(elementId);

const debounce = (fn, delay) =>
  window.CommonUtils?.debounce ? window.CommonUtils.debounce(fn, delay) : fn;

const procedureHeaders = () => {
  const headers = { 'Content-Type': 'application/json', Accept: 'application/json' };
  const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
  if (tokenElement?.value) headers['X-CSRF-TOKEN'] = tokenElement.value;
  return headers;
};

// ═══════════════════════════════════════════════════════════════════
// 4. SERVICIOS Y API
// ═══════════════════════════════════════════════════════════════════

async function persistProcedureState() {
  if (!procedureData.citaId) return;
  const state = procedureStorage.load();
  try {
    await fetch(`/api/citas/${procedureData.citaId}/asistencia-procedimiento`, {
      method: 'PUT',
      credentials: 'same-origin',
      headers: procedureHeaders(),
      body: JSON.stringify({
        minutos: state.minutes,
        inicio: state.startTime,
        limpieza: Boolean(state.pills.limpieza),
        esterilizacion: Boolean(state.pills.esterilizacion),
        equipos: Boolean(state.pills.equipos)
      })
    });
  } catch (error) {
    console.warn('[SmileTrack] No se pudo sincronizar asistencia procedural:', error);
  }
}

async function hydrateProcedureState() {
  if (!procedureData.citaId) return;
  try {
    const response = await fetch(`/api/citas/${procedureData.citaId}/asistencia-procedimiento`, {
      credentials: 'same-origin', headers: procedureHeaders()
    });
    if (!response.ok) return;
    const payload = await response.json();
    if (!payload.data) return;
    procedureStorage.save({
      minutes: payload.data.minutos || 0,
      startTime: payload.data.inicio || new Date().toISOString(),
      pills: {
        limpieza: Boolean(payload.data.limpieza),
        esterilizacion: Boolean(payload.data.esterilizacion),
        equipos: Boolean(payload.data.equipos)
      }
    });
  } catch (error) {
    console.warn('[SmileTrack] Se usará el respaldo local de asistencia:', error);
  }
}

// ═══════════════════════════════════════════════════════════════════
// 5. RENDERIZADO Y DOM
// ═══════════════════════════════════════════════════════════════════

const procedureStorage = {
  key: `smiletrack_procedure_${window.smiletrackAsistenciaProcedData?.citaId || 'sin_cita'}`,
  
  load: () => {
    const stored = localStorage.getItem(procedureStorage.key);
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch (e) {
        console.warn('Error al cargar estado del procedimiento, usando valores por defecto');
      }
    }
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
  
  save: (state) => {
    try {
      localStorage.setItem(procedureStorage.key, JSON.stringify(state));
      return true;
    } catch (e) {
      console.error('Error al guardar estado del procedimiento:', e);
      return false;
    }
  },
  
  updateMinutes: (minutes) => {
    const state = procedureStorage.load();
    state.minutes = minutes;
    procedureStorage.save(state);
    clearTimeout(procedureSyncTimerRef);
    procedureSyncTimerRef = setTimeout(persistProcedureState, SYNC_DEBOUNCE_MS);
  },
  
  updatePill: (pillId, completed) => {
    const state = procedureStorage.load();
    state.pills[pillId] = completed;
    procedureStorage.save(state);
    clearTimeout(procedureSyncTimerRef);
    procedureSyncTimerRef = setTimeout(persistProcedureState, SYNC_DEBOUNCE_MS);
  }
};

const renderDatosCita = () => {
  const dashboardData = window.smiletrackAsistenciaProcedData || {};

  const subtitleElement = safeGetElement('apSubtitle');
  if (subtitleElement) subtitleElement.textContent = dashboardData.citaId ? `${dashboardData.procedimiento} — ${dashboardData.profesional} — ${dashboardData.consultorio}` : 'No hay un procedimiento en curso asignado';

  const setElementText = (elementId, valText) => { const element = safeGetElement(elementId); if (element) element.textContent = valText || '—'; };
  setElementText('apPaciente', dashboardData.paciente);
  setElementText('apProfesional', dashboardData.profesional);
  setElementText('apServicio', dashboardData.procedimiento);
  setElementText('apConsultorio', dashboardData.consultorio);

  const bannerElement = safeGetElement('apAlertBanner');
  const bannerTextElement = safeGetElement('apAlertBannerText');
  if (dashboardData.alergia && bannerElement && bannerTextElement) {
    const strongTag = document.createElement('strong');
    strongTag.textContent = 'ALERTA';
    bannerTextElement.replaceChildren(strongTag, document.createTextNode(` — ${dashboardData.paciente} — Alérgico a ${dashboardData.alergia}`));
    bannerElement.style.display = '';
  }

  const alergiaItem = safeGetElement('apAlergiaItem');
  const alergiaTexto = safeGetElement('apAlergiaTexto');
  const antecedentesItem = safeGetElement('apAntecedentesItem');
  const antecedentesTexto = safeGetElement('apAntecedentesTexto');
  const sinAlertas = safeGetElement('apSinAlertas');

  let hasAlerts = false;
  if (dashboardData.alergia && alergiaItem && alergiaTexto) { alergiaTexto.textContent = dashboardData.alergia; alergiaItem.style.display = ''; hasAlerts = true; }
  if (dashboardData.antecedentes && antecedentesItem && antecedentesTexto) { antecedentesTexto.textContent = dashboardData.antecedentes; antecedentesItem.style.display = ''; hasAlerts = true; }
  if (hasAlerts && sinAlertas) sinAlertas.style.display = 'none';
};

// ═══════════════════════════════════════════════════════════════════
// 6. MANEJO DE MODALES Y FORMULARIOS
// ═══════════════════════════════════════════════════════════════════

const initTimer = () => {
  const timerValueElement = safeGetElement('timerValue');
  if (!timerValueElement) return;
  
  const state = procedureStorage.load();
  let minutes = state.minutes;
  
  timerValueElement.textContent = minutes;

  const statusTimeElement = document.querySelector('.status-time time');
  if (statusTimeElement && state.startTime) {
    const startTimeObj = new Date(state.startTime);
    statusTimeElement.setAttribute('datetime', state.startTime);
    statusTimeElement.textContent = startTimeObj.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
  }
  
  procedureTimerIntervalRef = setInterval(() => {
    minutes++;
    timerValueElement.textContent = minutes;
    procedureStorage.updateMinutes(minutes);
  }, TIMER_INTERVAL_MS);
  
  window.addEventListener('beforeunload', () => {
    if (procedureTimerIntervalRef) clearInterval(procedureTimerIntervalRef);
  });
};

const initProcedurePills = () => {
  const pillsConfig = [
    { element: safeGetElement('pillLimpieza'), id: 'limpieza', labelText: 'Limpieza' },
    { element: safeGetElement('pillEsterilizacion'), id: 'esterilizacion', labelText: 'Esterilización' },
    { element: safeGetElement('pillEquipos'), id: 'equipos', labelText: 'Equipos' }
  ];
  
  const savedPillsState = procedureStorage.load().pills;
  pillsConfig.forEach(({ element, id, labelText }) => {
    if (!element) return;
    
    const isCompleted = savedPillsState[id] || false;
    if (isCompleted) {
      element.classList.add('completed');
      element.setAttribute('aria-pressed', 'true');
      element.setAttribute('aria-label', `${labelText} completada`);
    }
    
    element.addEventListener('click', () => {
      const wasCompleted = element.classList.contains('completed');
      const isNowCompleted = !wasCompleted;
      
      element.classList.toggle('completed', isNowCompleted);
      element.setAttribute('aria-pressed', isNowCompleted);
      element.setAttribute('aria-label', isNowCompleted ? `${labelText} completada` : `Marcar ${labelText} como completada`);
      
      procedureStorage.updatePill(id, isNowCompleted);
      
      if (window.ToastService) {
        window.ToastService.success(`${labelText} ${isNowCompleted ? 'completada ✓' : 'marcada como pendiente'}`, 'info');
      }
    });
    
    element.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        element.click();
      }
    });
  });
};

// ═══════════════════════════════════════════════════════════════════
// 7. INICIALIZACIÓN Y EVENT LISTENERS
// ═══════════════════════════════════════════════════════════════════

const setupMobileNavigationMenu = () => {
  // El menú móvil es gestionado centralizadamente por ~/js/shared/sidebar.js
};

const initializeAsistenciaProcediModule = async () => {
  await hydrateProcedureState();
  renderDatosCita();
  setupMobileNavigationMenu();
  initTimer();
  initProcedurePills();
  
  window.addEventListener('beforeunload', () => { /* cleanup SPA */ });
};

document.addEventListener('DOMContentLoaded', initializeAsistenciaProcediModule);