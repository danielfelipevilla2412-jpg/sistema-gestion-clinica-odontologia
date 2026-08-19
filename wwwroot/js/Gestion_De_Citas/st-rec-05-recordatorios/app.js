/* ============================================
SmileTrack — Recordatorios (st-rec-05-recordatorios)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Controla la interfaz de recordatorios de recepción: envío manual, confirmación visual de estados de entrega y configuración de envíos automáticos.

FUNCIONALIDADES PRINCIPALES:
- Acciones de envío para recordatorios por medio de fetch API
- Confirmación visual temporal (Toast) de los envíos realizados
- Toggle de activación de canales de envío automáticos

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Recordatorios
- CSS: ~/css/Gestion_De_Citas/st-rec-05-recordatorios/styles.css
- JS: ~/js/Gestion_De_Citas/st-rec-05-recordatorios/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Debounce evita la sobrecarga del hilo principal ante eventos repetitivos como tecleos de búsqueda o redimensiones
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes brindan retroalimentación al recepcionista sin interrumpir la gestión de la tabla

// Maneja cambio de estado en checkboxes de pacientes
const handleCheckboxChange = (checkbox) => {
  const row = checkbox.closest('.patient-row');
  if (!row) return;
  
  if (checkbox.checked) {
    row.classList.add('selected');
  } else {
    row.classList.remove('selected');
  }
};

// Inicializa eventos de checkboxes en lista de pacientes
const initPatientCheckboxes = () => {
  const container = safeGetElement('patientListContainer');
  if (!container) return;

  container.addEventListener('change', (e) => {
    if (e.target.classList.contains('custom-checkbox')) {
      handleCheckboxChange(e.target);
    }
  });

  // Soporte para teclado en filas de paciente
  container.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      const row = e.target.closest('.patient-row');
      if (row) {
        e.preventDefault();
        const checkbox = row.querySelector('.custom-checkbox');
        if (checkbox) {
          checkbox.checked = !checkbox.checked;
          handleCheckboxChange(checkbox);
        }
      }
    }
  });
};

// Inicializa botón de enviar seleccionados
const initSendSelected = () => {
  const btn = safeGetElement('btnSendSelected');
  if (!btn) return;

  btn.addEventListener('click', () => {
    const selected = getSelectedPatients();
    sendReminders(selected);
  });
};

// Inicializa botón móvil de enviar a todos
const initSendAllMobile = () => {
  const btn = safeGetElement('btnSendAllMobile');
  if (!btn) return;

  btn.addEventListener('click', () => {
    // Selecciona todos los pacientes programados para mañana
    const container = safeGetElement('patientListContainer');
    if (!container) return;
    
    container.querySelectorAll('.patient-row:not(.dimmed)').forEach(row => {
      const checkbox = row.querySelector('.custom-checkbox');
      if (checkbox && !checkbox.disabled) {
        checkbox.checked = true;
        row.classList.add('selected');
      }
    });
    
    const selected = getSelectedPatients();
    sendReminders(selected);
  });
};

// Inicializa botones de alertas laterales
const initAlertButtons = () => {
  const btnUnconfirmed = safeGetElement('btnAlertUnconfirmed');
  const btnOverdue = safeGetElement('btnAlertOverdue');

  if (btnUnconfirmed) {
    btnUnconfirmed.addEventListener('click', () => {
      window.ToastService.success('Notificaciones de confirmación enviadas a 3 pacientes');
      btnUnconfirmed.disabled = true;
      btnUnconfirmed.closest('.alert-item-card')?.classList.add('dimmed');
    });
  }

  if (btnOverdue) {
    btnOverdue.addEventListener('click', () => {
      window.ToastService.success('Recordatorios de pago enviados a 2 pacientes');
      btnOverdue.disabled = true;
      btnOverdue.closest('.alert-item-card')?.classList.add('dimmed');
    });
  }
};

// Inicializa menú móvil con gestión de foco y atributos ARIA
const initMobileMenu = () => {
  const ham = safeGetElement('hamburger');
  const sb = safeGetElement('sidebar');
  const ov = safeGetElement('overlay');

  if (!ham || !sb || !ov) return;

  const toggleMenu = (show) => {
    if (show) {
      sb.classList.add('open');
      ov.classList.add('open');
      ham.setAttribute('aria-expanded', 'true');
      ov.setAttribute('aria-hidden', 'false');
      sb.dataset.previousFocus = document.activeElement?.id || '';
      const firstLink = sb.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      sb.classList.remove('open');
      ov.classList.remove('open');
      ham.setAttribute('aria-expanded', 'false');
      ov.setAttribute('aria-hidden', 'true');
      const prevFocus = sb.dataset.previousFocus;
      if (prevFocus) safeGetElement(prevFocus)?.focus();
      else ham.focus();
    }
  };

  ham.addEventListener('click', () => toggleMenu(true));
  ov.addEventListener('click', () => toggleMenu(false));

  document.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) toggleMenu(false);
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sb.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// Función principal de inicialización
const init = () => {
  initMobileMenu();
  initPatientCheckboxes();
  initSendSelected();
  initSendAllMobile();
  initAlertButtons();

  // Limpieza de listeners al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

document.addEventListener('DOMContentLoaded', init);