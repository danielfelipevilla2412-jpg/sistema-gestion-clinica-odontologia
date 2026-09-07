/**
 * SMILETRACK — PREPARACIÓN DE CONSULTA
 *
 * Yeray (2025) - Refactorización completa.
 *
 * ANTES: todos los datos estaban hardcodeados. La clave de localStorage
 *        era fija ("smiletrack_checklist_pedro_garcia_20260320") y la
 *        confirmación solo imprimía en consola.
 *
 * AHORA:
 *   - Lee la configuración de window.smiletrackPreparacionConfig (inyectada
 *     por la vista Razor con datos reales de la BD).
 *   - La clave de localStorage es dinámica por citaId: cada cita tiene su
 *     propio estado de checklist independiente.
 *   - El selector de citas recarga la página con ?citaId=N para cambiar
 *     de cita sin perder el estado.
 *   - La confirmación llama a POST /confirmar con el checklist y las
 *     observaciones, registrando la preparación en la HC del paciente.
 *   - Los badges de estado del consultorio (Limpieza, Esterilización,
 *     Materiales) se actualizan automáticamente según los ítems marcados.
 */

// ── Config inyectada por el servidor ────────────────────────────────────────
const cfg = window.smiletrackPreparacionConfig || {};
const CITA_ID      = cfg.citaId      ?? null;
const SIN_CITAS    = cfg.sinCitasHoy ?? true;
const CHECKLIST_KEY = `smiletrack_preparacion_cita_${CITA_ID ?? 'sin-cita'}`;
const OBS_KEY       = `${CHECKLIST_KEY}_obs`;

// ── Utilidades ───────────────────────────────────────────────────────────────

const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

const debounce = (fn, delay) => {
  let tid;
  return (...args) => { clearTimeout(tid); tid = setTimeout(() => fn(...args), delay); };
};

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
  clearTimeout(toast._tid);
  toast._tid = setTimeout(() => toast.classList.remove('show'), 3200);
};

// ── Persistencia del checklist ───────────────────────────────────────────────

const checklistStorage = {
  load: () => {
    try {
      const stored = localStorage.getItem(CHECKLIST_KEY);
      return stored ? JSON.parse(stored) : new Array(7).fill(false);
    } catch { return new Array(7).fill(false); }
  },
  save: (states) => {
    try { localStorage.setItem(CHECKLIST_KEY, JSON.stringify(states)); } catch {}
  },
  getProgress: (states) => {
    const completed = states.filter(Boolean).length;
    return { completed, total: states.length, percentage: Math.round((completed / states.length) * 100) };
  }
};

// ── Actualizar badges de estado del consultorio ──────────────────────────────
// Los ítems 0=Limpieza, 1=Esterilización, 5=Materiales controlan los badges.
const actualizarBadgesEstado = (states) => {
  const setbadge = (id, txtId, listo) => {
    const badge = document.getElementById(id);
    const txt   = document.getElementById(txtId);
    if (!badge || !txt) return;
    badge.className = `status-badge ${listo ? 'listo' : ''}`;
    const dot = badge.querySelector('.status-dot');
    if (dot) dot.className = `status-dot ${listo ? 'green' : 'orange'}`;
    txt.textContent = listo ? 'Listo' : 'Pendiente';
  };

  setbadge('statusLimpieza',       'statusLimpiezaTxt',       states[0]);
  setbadge('statusEsterilizacion', 'statusEsterilizacionTxt', states[1]);
  setbadge('statusMateriales',     'statusMaterialesTxt',     states[5]);
};

// ── Inicializar checklist ─────────────────────────────────────────────────────
const initChecklist = () => {
  const checklist     = safeGetElement('checklist');
  const progressFill  = safeGetElement('progressFill');
  const progressText  = safeGetElement('progressText');
  if (!checklist) return;

  const states = checklistStorage.load();
  const items  = checklist.querySelectorAll('.checklist-item');

  // Aplicar estado guardado
  items.forEach((item, idx) => {
    const cb = item.querySelector('input[type="checkbox"]');
    if (!cb) return;
    const checked = states[idx] ?? false;
    cb.checked = checked;
    item.classList.toggle('checked', checked);
    cb.setAttribute('aria-checked', String(checked));
  });

  actualizarBadgesEstado(states);

  const updateProgress = () => {
    const s       = Array.from(items).map(it => it.querySelector('input[type="checkbox"]')?.checked ?? false);
    const prog    = checklistStorage.getProgress(s);
    if (progressFill) {
      progressFill.style.width = `${prog.percentage}%`;
      progressFill.closest('[role="progressbar"]')?.setAttribute('aria-valuenow', String(prog.percentage));
    }
    if (progressText) {
      progressText.textContent = `${prog.completed} de ${prog.total} ítems completados`;
    }
    checklistStorage.save(s);
    actualizarBadgesEstado(s);
  };

  updateProgress();

  checklist.addEventListener('change', (e) => {
    if (e.target.type !== 'checkbox') return;
    const item    = e.target.closest('.checklist-item');
    const checked = e.target.checked;
    item?.classList.toggle('checked', checked);
    e.target.setAttribute('aria-checked', String(checked));
    updateProgress();
    showToast(checked ? 'Ítem completado ✓' : 'Ítem desmarcado', checked ? 'success' : 'info');
  });
};

// ── Selector de citas del día ────────────────────────────────────────────────
const initSelectorCita = () => {
  const sel = safeGetElement('selectorCita');
  if (!sel) return;

  sel.addEventListener('change', (e) => {
    const id = e.target.value;
    if (id) window.location.href =
      `/gestion-de-pacientes/st-aux-03-preparacion-consulta?citaId=${id}`;
  });
};

// ── Observaciones con auto-guardado ─────────────────────────────────────────
const initObservations = () => {
  const ta = safeGetElement('observations');
  if (!ta) return;
  const saved = localStorage.getItem(OBS_KEY);
  if (saved) ta.value = saved;
  ta.addEventListener('input', debounce(() => {
    localStorage.setItem(OBS_KEY, ta.value);
  }, 500));
};

// ── Confirmación — llama al endpoint POST /confirmar ─────────────────────────
const initConfirmButton = () => {
  const btn       = safeGetElement('btnConfirmPreparation');
  const checklist = safeGetElement('checklist');
  if (!btn || !checklist) return;

  btn.addEventListener('click', async () => {
    const items = checklist.querySelectorAll('.checklist-item');

    // Recoger estado actual de cada ítem
    const checklistItems = Array.from(items).map(item => ({
      text:    item.querySelector('span:last-child')?.textContent?.trim() ?? '',
      checked: item.querySelector('input[type="checkbox"]')?.checked ?? false
    }));

    const allChecked = checklistItems.every(i => i.checked);
    if (!allChecked) {
      showToast('Completa todos los ítems antes de confirmar', 'warning');
      const firstUnchecked = Array.from(items).find(it => !it.querySelector('input[type="checkbox"]')?.checked);
      firstUnchecked?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      firstUnchecked?.querySelector('input')?.focus();
      return;
    }

    if (SIN_CITAS || !CITA_ID) {
      showToast('No hay una cita activa para confirmar', 'warning');
      return;
    }

    const observaciones = safeGetElement('observations')?.value.trim() ?? '';
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

    btn.disabled    = true;
    btn.textContent = '⏳ Guardando...';

    try {
      const resp = await fetch(
        '/gestion-de-pacientes/st-aux-03-preparacion-consulta/confirmar',
        {
          method:  'POST',
          headers: {
            'Content-Type':             'application/json',
            'X-CSRF-TOKEN':             token,
            'RequestVerificationToken': token
          },
          credentials: 'same-origin',
          body: JSON.stringify({ citaId: CITA_ID, checklistItems, observaciones })
        }
      );

      const data = await resp.json().catch(() => ({}));

      if (!resp.ok || data.success === false) {
        throw new Error(data.message || 'Error al guardar la preparación.');
      }

      // Limpiar localStorage de esta cita
      localStorage.removeItem(CHECKLIST_KEY);
      localStorage.removeItem(OBS_KEY);

      btn.textContent = '✓ Confirmado';
      btn.style.background = '#22c55e';
      showToast('✅ Preparación confirmada y registrada en la historia clínica.', 'success');

      // Restablecer botón después de 4 segundos
      setTimeout(() => {
        btn.disabled    = false;
        btn.textContent = 'Confirmar preparación completa';
        btn.style.background = '';
      }, 4000);

    } catch (err) {
      console.error('[SmileTrack][Preparacion] Error al confirmar:', err);
      showToast(err.message || 'No se pudo guardar la preparación.', 'error');
      btn.disabled    = false;
      btn.textContent = 'Confirmar preparación completa';
    }
  });
};

// ── Menú móvil ───────────────────────────────────────────────────────────────
const initMobileMenu = () => {
  const sidebar   = safeGetElement('sidebar');
  const overlay   = safeGetElement('overlay');
  const hamburger = safeGetElement('hamburgerBtn');
  if (!sidebar || !overlay || !hamburger) return;

  const toggle = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
    if (show) sidebar.querySelector('.nav-item')?.focus();
    else hamburger.focus();
  };

  hamburger.addEventListener('click', () => toggle(true));
  overlay.addEventListener('click',   () => toggle(false));
  sidebar.querySelectorAll('.nav-item').forEach(lnk => {
    lnk.addEventListener('click', () => { if (window.innerWidth <= 680) toggle(false); });
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) { e.preventDefault(); toggle(false); }
  });
};

// ── Init ─────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  initMobileMenu();
  initChecklist();
  initSelectorCita();
  initObservations();
  initConfirmButton();
});
