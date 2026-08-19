/**
 * SMILETRACK — HISTORIA CLÍNICA (historial.js)
 * Lógica con persistencia, accesibilidad y validaciones
 */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

// Obtiene elemento del DOM con manejo seguro
const safeGetElement = (id) => {
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// Reduce llamadas a función en eventos frecuentes
const debounce = (fn, delay) => {
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// Muestra notificación temporal con auto-cierre
const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;

  toast.textContent = message;
  toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;

  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// Valida campo de formulario y muestra errores
const validateField = (input) => {
  const group = input.closest('.form-group');
  if (!group) return true;
  
  const errorSpan = group.querySelector('.error-message');
  let valid = true;
  
  if (input.required && !input.value.trim()) {
    valid = false;
  }
  
  if (!valid) {
    input.classList.add('error');
    if (errorSpan) {
      errorSpan.textContent = 'Este campo es requerido';
      errorSpan.classList.add('visible');
    }
    input.setAttribute('aria-invalid', 'true');
  } else {
    input.classList.remove('error');
    if (errorSpan) errorSpan.classList.remove('visible');
    input.removeAttribute('aria-invalid');
  }
  
  return valid;
};

// Valida todos los campos del formulario
const validateForm = (form) => {
  const inputs = form.querySelectorAll('input[required], textarea[required]');
  let allValid = true;
  
  inputs.forEach(input => {
    if (!validateField(input)) allValid = false;
  });
  
  return allValid;
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA CON LOCALSTORAGE
// ═══════════════════════════════════════════════════════════════════

// Convierte el JSON persistido del odontograma (registros por instanceID + mapeoFDI)
// en los mapas {numeroDiente: estadoKey} / {numeroDiente: observación} que usa el render
// de solo lectura de esta vista. Mismo formato que guarda st-odo-04-odontograma.
const parseOdontogramaPersistido = (estadoPersistidoRaw) => {
  const tratamientos = {};
  const observaciones = {};
  if (!estadoPersistidoRaw) return { tratamientos, observaciones };

  try {
    const persistido = typeof estadoPersistidoRaw === 'string' ? JSON.parse(estadoPersistidoRaw) : estadoPersistidoRaw;
    const registros = persistido?.registros || {};
    const mapeoFDI = persistido?.mapeoFDI || {};

    Object.entries(registros).forEach(([instanceID, registro]) => {
      const numeroDiente = mapeoFDI[instanceID];
      if (!numeroDiente) return;
      const tratamientosPieza = registro?.tratamientos || registro?.Tratamientos || [];
      if (!tratamientosPieza.length) return;
      const ultimo = tratamientosPieza[tratamientosPieza.length - 1];
      const key = ultimo?.key || ultimo?.Key;
      const obs = ultimo?.obs || ultimo?.Obs;
      if (key) tratamientos[numeroDiente] = key;
      if (obs) observaciones[numeroDiente] = obs;
    });
  } catch (e) {
    console.warn('No se pudo interpretar el odontograma guardado', e);
  }

  return { tratamientos, observaciones };
};

// Extrae el historial de notas clínicas libres guardadas junto al odontograma
// (no existe una tabla dedicada; se persisten como parte de ObservacionesGenerales).
const parseNotasClinicasPersistidas = (estadoPersistidoRaw) => {
  if (!estadoPersistidoRaw) return [];
  try {
    const persistido = typeof estadoPersistidoRaw === 'string' ? JSON.parse(estadoPersistidoRaw) : estadoPersistidoRaw;
    return persistido?.notasClinicas || [];
  } catch (e) {
    return [];
  }
};

// Fuente de datos real: inyectada por el servidor en window.smiletrackHistoriaData
// (ver Views/Historia_Clinica/st-odo-03-historial/gestion-historial.cshtml).
const historiaStorage = {
  // Carga los datos reales del paciente enviados por el servidor.
  load: () => {
    const server = window.smiletrackHistoriaData || {};
    const { tratamientos, observaciones } = parseOdontogramaPersistido(server.estadoPersistido);
    const notasClinicas = parseNotasClinicasPersistidas(server.estadoPersistido);

    return {
      paciente: {
        id: server.pacienteId ?? null,
        nombre: server.nombre || 'Sin paciente asignado',
        documento: '',
        tipoDoc: '',
        fechaNacimiento: server.fechaNacimiento || null,
        grupoSanguineo: server.grupoSanguineo || 'N/D',
        codigoHC: server.codigoHC || 'HC-SIN-ASIGNAR',
        alergias: server.alergias || [],
        medicamentos: server.medicamentos || [],
        odontograma: tratamientos,
        observaciones,
        // Notas clínicas libres + historial real de citas del paciente
        historial: [...notasClinicas, ...(server.historial || [])],
      }
    };
  }
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE UTILIDAD
// ═══════════════════════════════════════════════════════════════════

// Formatea fecha ISO a formato legible
const formatFecha = (isoDate, largo = false) => {
  if (!isoDate) return '';
  const d = new Date(isoDate + 'T12:00:00');
  const opts = largo
    ? { day:'2-digit', month:'long', year:'numeric' }
    : { day:'2-digit', month:'short', year:'numeric' };
  return d.toLocaleDateString('es-CO', opts);
};

// Calcula edad a partir de fecha de nacimiento
const calcEdad = (fechaIso) => {
  if (!fechaIso) return null;
  const hoy = new Date();
  const nac = new Date(fechaIso);
  if (Number.isNaN(nac.getTime())) return null;
  let edad = hoy.getFullYear() - nac.getFullYear();
  const m = hoy.getMonth() - nac.getMonth();
  if (m < 0 || (m === 0 && hoy.getDate() < nac.getDate())) edad--;
  return edad;
};

// Determina tipo de dentición según edad
const tipoDenticion = (fechaNacimiento) => {
  const edad = calcEdad(fechaNacimiento);
  return edad !== null && edad < 13 ? 'nino' : 'adulto';
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: ALERTAS MÉDICAS
// ═══════════════════════════════════════════════════════════════════

const renderAlertas = (alertas) => {
  const setVal = (id, val) => { 
    const el = safeGetElement(id); 
    if (el) {
      el.textContent = val || '—';
      el.setAttribute('aria-label', `${el.previousElementSibling?.textContent?.trim() || 'Valor'}: ${val || 'No registrado'}`);
    }
  };
  
  setVal('alergias', alertas.alergias?.join(', '));
  setVal('medicamentos', alertas.medicamentos?.join(', '));
  setVal('grupoSang', alertas.grupoSanguineo);
  
  const card = safeGetElement('alertCard');
  if (card && !alertas.alergias?.length && !alertas.medicamentos?.length) {
    card.style.display = 'none';
    card.setAttribute('aria-hidden', 'true');
  }
};

// ═══════════════════════════════════════════════════════════════════
//  RENDER: HISTORIAL DE CONSULTAS
// ═══════════════════════════════════════════════════════════════════

const renderHistorial = (historial) => {
  const list = safeGetElement('historialList');
  if (!list) return;
  
  if (!historial?.length) {
    list.innerHTML = '<p style="font-size:.85rem;color:var(--text-muted);padding:12px 0;text-align:center;">Sin consultas registradas</p>';
    return;
  }
  
  list.innerHTML = historial.map(h => {
    const fecha = formatFecha(h.fecha, true);
    const desc = h.procedimiento || h.diagnostico || '';
    return `
      <div class="hist-item" role="listitem">
        <div class="hist-bullet" aria-hidden="true">🦷</div>
        <div class="hist-body">
          <div class="hist-top">
            <span class="hist-title">${h.titulo}</span>
            <span class="hist-badge" role="status" aria-label="Estado: ${h.estado}">● ${h.estado}</span>
          </div>
          <div class="hist-meta"><time datetime="${h.fecha}">${fecha}</time> · ${h.doctor}</div>
          ${desc ? `<div class="hist-desc">${desc}</div>` : ''}
        </div>
      </div>`;
  }).join('');
};

// ═══════════════════════════════════════════════════════════════════
//  ODONTOGRAMA — Configuración y dibujo (solo lectura)
// ═══════════════════════════════════════════════════════════════════

const ESTADOS_ODO = [
  { key:'caries', label:'Caries', color:'#ef4444', tipo:'dot' },
  { key:'endodoncia', label:'Endodoncia', color:'#3b82f6', tipo:'bar' },
  { key:'sellante', label:'Sellante', color:'#f59e0b', tipo:'dot' },
  { key:'ausente', label:'Ausente', color:'#94a3b8', tipo:'x' },
  { key:'placa', label:'Placa', color:'#22c55e', tipo:'bar' },
  { key:'sano', label:'Sano', color:'#06b6d4', tipo:'dot' },
  { key:'corona', label:'Corona', color:'#a855f7', tipo:'dot' },
  { key:'restauracion', label:'Restauración', color:'#10b981', tipo:'bar' },
];

const ODO_CONFIG = {
  adulto: {
    TW: 28, TH: 36,
    groups: [
      { rows: [
          { label:'SUPERIOR IZQUIERDO (18-11)', nums:[18,17,16,15,14,13,12,11] },
          { label:'SUPERIOR DERECHO (21-28)', nums:[21,22,23,24,25,26,27,28] },
      ]},
      { rows: [
          { label:'INFERIOR IZQUIERDO (38-31)', nums:[38,37,36,35,34,33,32,31] },
          { label:'INFERIOR DERECHO (41-48)', nums:[41,42,43,44,45,46,47,48] },
      ]},
    ],
  },
  nino: {
    TW: 32, TH: 40,
    groups: [
      { rows: [
          { label:'SUPERIOR IZQUIERDO (55-51)', nums:[55,54,53,52,51] },
          { label:'SUPERIOR DERECHO (61-65)', nums:[61,62,63,64,65] },
      ]},
      { rows: [
          { label:'INFERIOR IZQUIERDO (85-81)', nums:[85,84,83,82,81] },
          { label:'INFERIOR DERECHO (71-75)', nums:[71,72,73,74,75] },
      ]},
    ],
  },
};

const isMolar = (n) => { const d = n%10; return d>=6 && d<=8; };
const isPremolar = (n) => { const d = n%10; return d===4 || d===5; };
const isCanine = (n) => n%10===3;

const rrPath = (ctx,x,y,w,h,r) => {
  ctx.beginPath();
  ctx.moveTo(x+r,y); ctx.lineTo(x+w-r,y);
  ctx.quadraticCurveTo(x+w,y, x+w,y+r);
  ctx.lineTo(x+w,y+h-r);
  ctx.quadraticCurveTo(x+w,y+h, x+w-r,y+h);
  ctx.lineTo(x+r,y+h);
  ctx.quadraticCurveTo(x,y+h, x,y+h-r);
  ctx.lineTo(x,y+r);
  ctx.quadraticCurveTo(x,y, x+r,y);
  ctx.closePath();
};

const applyTooth = (ctx, lw=1) => {
  ctx.fillStyle='rgba(0,18,42,.9)'; ctx.fill();
  ctx.strokeStyle='rgba(0,160,220,.6)'; ctx.lineWidth=lw; ctx.stroke();
};

const drawMolar = (ctx,ox,oy,tw,th) => {
  rrPath(ctx,ox,oy,tw,th,4); applyTooth(ctx);
  ctx.strokeStyle='rgba(0,180,255,.18)'; ctx.lineWidth=.5;
  const cx=ox+tw/2, cy=oy+th/2-2;
  ctx.beginPath(); ctx.moveTo(cx-4,cy-2); ctx.lineTo(cx+4,cy-2); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(cx,cy-5); ctx.lineTo(cx,cy+3); ctx.stroke();
};

const drawPremolar = (ctx,ox,oy,tw,th) => {
  rrPath(ctx,ox,oy,tw,th,4); applyTooth(ctx);
  ctx.strokeStyle='rgba(0,180,255,.15)'; ctx.lineWidth=.5;
  const cx=ox+tw/2, cy=oy+th/2-2;
  ctx.beginPath(); ctx.moveTo(cx,cy-4); ctx.lineTo(cx,cy+3); ctx.stroke();
};

const drawCanine = (ctx,ox,oy,tw,th) => {
  const cx=ox+tw/2;
  ctx.beginPath();
  ctx.moveTo(ox+3,oy); ctx.lineTo(ox+tw-3,oy);
  ctx.quadraticCurveTo(ox+tw,oy,ox+tw,oy+3);
  ctx.lineTo(ox+tw,oy+th-8);
  ctx.quadraticCurveTo(ox+tw-1,oy+th+2,cx,oy+th+2);
  ctx.quadraticCurveTo(ox+1,oy+th+2,ox,oy+th-8);
  ctx.lineTo(ox,oy+3); ctx.quadraticCurveTo(ox,oy,ox+3,oy); ctx.closePath();
  applyTooth(ctx);
};

const drawIncisor = (ctx,ox,oy,tw,th) => {
  const cx=ox+tw/2;
  ctx.beginPath();
  ctx.moveTo(ox+2,oy); ctx.lineTo(ox+tw-2,oy);
  ctx.quadraticCurveTo(ox+tw,oy,ox+tw,oy+2);
  ctx.lineTo(ox+tw,oy+th-4);
  ctx.quadraticCurveTo(ox+tw-.5,oy+th,cx,oy+th);
  ctx.quadraticCurveTo(ox+.5,oy+th,ox,oy+th-4);
  ctx.lineTo(ox,oy+2); ctx.quadraticCurveTo(ox,oy,ox+2,oy); ctx.closePath();
  applyTooth(ctx);
};

const drawTooth = (canvas, num, tratamientos, obs) => {
  const ctx = canvas.getContext('2d');
  const W = canvas.width, H = canvas.height;
  ctx.clearRect(0,0,W,H);
  const pad=3, tw=W-pad*2, th=H-pad*2-8, ox=pad, oy=pad+2;

  if (isMolar(num)) drawMolar(ctx,ox,oy,tw,th);
  else if (isPremolar(num)) drawPremolar(ctx,ox,oy,tw,th);
  else if (isCanine(num)) drawCanine(ctx,ox,oy,tw,th);
  else drawIncisor(ctx,ox,oy,tw,th);

  const eKey = tratamientos[num];
  if (eKey) {
    const e = ESTADOS_ODO.find(x => x.key === eKey);
    if (e) {
      const cx = W/2, cy = oy+6;
      ctx.shadowColor = e.color; ctx.shadowBlur = 8;
      if (e.tipo === 'dot') {
        ctx.beginPath(); ctx.arc(cx,cy,3.5,0,2*Math.PI);
        ctx.fillStyle = e.color; ctx.fill();
      } else if (e.tipo === 'bar') {
        ctx.fillStyle = e.color;
        ctx.beginPath(); rrPath(ctx,cx-5,cy-2,10,3,1.5); ctx.fill();
      } else {
        ctx.strokeStyle = e.color; ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(cx-4,cy-4); ctx.lineTo(cx+4,cy+4);
        ctx.moveTo(cx+4,cy-4); ctx.lineTo(cx-4,cy+4);
        ctx.stroke();
      }
      ctx.shadowBlur = 0;
    }
  }

  ctx.font = 'bold 7px sans-serif';
  ctx.fillStyle = 'rgba(0,200,255,.75)';
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillText(num, W/2, oy+th-4);

  const e = ESTADOS_ODO.find(x => x.key === eKey);
  canvas.title = `Diente ${num}${e ? ' — '+e.label : ''}${obs ? '\n'+obs : ''}`;
};

const renderOdontograma = (dto) => {
  const host = safeGetElement('odontogramaHost');
  if (!host) return;

  const cfg = ODO_CONFIG[dto.tipo] || ODO_CONFIG.adulto;

  // Keyframe scan
  if (!document.getElementById('_scan_kf')) {
    const st = document.createElement('style');
    st.id = '_scan_kf';
    st.textContent = '@keyframes scan{0%{transform:translateY(0)}100%{transform:translateY(500px)}}';
    document.head.appendChild(st);
  }

  const hw = document.createElement('div');
  hw.style.cssText = 'background:#030d1a;border-radius:12px;padding:14px 12px;font-family:sans-serif;position:relative;overflow:hidden;width:100%;';

  // Scan line
  const sl = document.createElement('div');
  sl.style.cssText = 'position:absolute;left:0;right:0;height:1px;top:0;z-index:1;pointer-events:none;background:linear-gradient(90deg,transparent,rgba(0,200,255,.22),transparent);animation:scan 3s linear infinite;';
  hw.appendChild(sl);

  // Header
  const hdr = document.createElement('div');
  hdr.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;position:relative;z-index:2;';
  hdr.innerHTML = `
    <div>
      <div style="font-size:11px;font-weight:600;color:#00d4ff;letter-spacing:.08em;text-transform:uppercase">SmileTrack — Odontograma</div>
      <div style="font-size:9px;color:rgba(0,212,255,.45);margin-top:2px">${dto.nombrePaciente} · ${dto.tipo==='adulto'?'Dentición permanente':'Dentición temporal (niño)'}</div>
    </div>
    <div style="font-size:9px;padding:3px 10px;border-radius:20px;border:1px solid rgba(0,212,255,.3);color:rgba(0,212,255,.7);background:rgba(0,212,255,.07);">
      🦷 ${dto.tipo==='adulto'?'Adulto':'Niño'}
    </div>`;
  hw.appendChild(hdr);

  // Grupos de dientes
  cfg.groups.forEach((g,gi) => {
    const labRow = document.createElement('div');
    labRow.style.cssText = 'display:flex;justify-content:center;gap:4px;margin-bottom:3px;position:relative;z-index:2;';
    g.rows.forEach(r => {
      const sp = document.createElement('span');
      sp.style.cssText = 'font-size:7px;color:rgba(0,212,255,.35);letter-spacing:.04em;text-transform:uppercase;flex:1;text-align:center;';
      sp.textContent = r.label;
      labRow.appendChild(sp);
    });
    hw.appendChild(labRow);

    const rowDiv = document.createElement('div');
    rowDiv.style.cssText = 'display:flex;justify-content:center;align-items:stretch;gap:2px;position:relative;z-index:2;margin-bottom:8px;';

    g.rows.forEach((r,ri) => {
      const half = document.createElement('div');
      half.style.cssText = 'display:flex;gap:2px;';
      r.nums.forEach(num => {
        const cv = document.createElement('canvas');
        cv.width = cfg.TW; cv.height = cfg.TH;
        cv.style.cssText = 'display:block;border-radius:3px;cursor:default;';
        drawTooth(cv, num, dto.tratamientos, dto.observaciones[num]||null);
        half.appendChild(cv);
      });
      rowDiv.appendChild(half);
      if (ri === 0) {
        const mid = document.createElement('div');
        mid.style.cssText = 'width:1px;background:rgba(0,212,255,.2);align-self:stretch;flex-shrink:0;margin:0 3px;border-radius:1px;';
        rowDiv.appendChild(mid);
      }
    });
    hw.appendChild(rowDiv);

    if (gi === 0) {
      const sep = document.createElement('div');
      sep.style.cssText = 'height:7px;border-top:1px dashed rgba(0,212,255,.12);margin:0 0 10px;position:relative;z-index:2;';
      hw.appendChild(sep);
    }
  });

  // Leyenda
  const leg = document.createElement('div');
  leg.style.cssText = 'display:flex;flex-wrap:wrap;gap:6px;justify-content:center;margin-top:10px;position:relative;z-index:2;';
  ESTADOS_ODO.forEach(e => {
    const item = document.createElement('div');
    item.style.cssText = 'display:flex;align-items:center;gap:3px;font-size:9px;color:rgba(180,220,255,.55);';
    const ic = e.tipo==='dot'
      ? `<span style="width:7px;height:7px;border-radius:50%;background:${e.color};display:inline-block;"></span>`
      : e.tipo==='bar'
        ? `<span style="width:11px;height:3px;border-radius:2px;background:${e.color};display:inline-block;"></span>`
        : `<span style="font-size:10px;color:${e.color};">✕</span>`;
    item.innerHTML = `${ic} ${e.label}`;
    leg.appendChild(item);
  });
  hw.appendChild(leg);

  // Nota readonly
  const note = document.createElement('div');
  note.style.cssText = 'text-align:center;font-size:9px;color:rgba(0,212,255,.3);margin-top:10px;letter-spacing:.04em;position:relative;z-index:2;';
  note.textContent = '⚠ Solo lectura — edición desde el módulo Odontograma';
  hw.appendChild(note);

  host.innerHTML = '';
  host.appendChild(hw);
};

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN DE COMPONENTES
// ═══════════════════════════════════════════════════════════════════

// Inicializa sidebar móvil con gestión de foco y ARIA
const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');

  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', show);
    overlay.setAttribute('aria-hidden', !show);
    
    if (show) {
      const firstLink = sidebar.querySelector('.nav-item');
      if (firstLink) firstLink.focus();
    } else {
      hamburger.focus();
    }
  };

  hamburger.addEventListener('click', () => toggleMenu(true));
  overlay.addEventListener('click', () => toggleMenu(false));

  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', (e) => {
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

// Inicializa scroll suave al formulario
const initScrollToForm = () => {
  const btn = safeGetElement('btnNewEntry');
  const card = safeGetElement('cardForm');
  
  if (btn && card) {
    btn.addEventListener('click', () => {
      card.scrollIntoView({ behavior:'smooth', block:'start' });
      card.style.outline = '2px solid var(--primary)';
      setTimeout(() => { card.style.outline = ''; }, 1600);
      
      // Enfocar primer campo del formulario
      const firstInput = card.querySelector('input, textarea');
      if (firstInput) setTimeout(() => firstInput.focus(), 500);
    });
  }
};

// Inicializa formulario con validación y persistencia
const initForm = () => {
  const form = safeGetElement('historialForm');
  if (!form) return;
  
  // Validación en tiempo real
  form.querySelectorAll('input[required], textarea[required]').forEach(input => {
    input.addEventListener('blur', () => validateField(input));
    input.addEventListener('input', () => {
      if (input.classList.contains('error')) validateField(input);
    });
  });
  
  // Manejo de envío del formulario
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    if (!validateForm(form)) {
      showToast('Completa los campos requeridos', 'warning');
      return;
    }
    
    const fDiag = safeGetElement('fDiag');
    const fProc = safeGetElement('fProc');
    const fCita = safeGetElement('fCita');
    const btn = safeGetElement('btnGuardar');
    
    const diag = fDiag?.value.trim();
    const proc = fProc?.value.trim();
    const cita = fCita?.value;
    
    const pacienteId = window.smiletrackHistoriaData?.pacienteId;
    if (!pacienteId) {
      showToast('No hay un paciente seleccionado para guardar la nota', 'error');
      return;
    }

    try {
      btn.disabled = true;
      btn.textContent = 'Guardando...';

      const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
      const resp = await fetch('/historia-clinica/st-odo-03-historial/guardar-nota', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { 'RequestVerificationToken': token } : {})
        },
        body: JSON.stringify({
          pacienteId,
          diagnostico: diag,
          procedimiento: proc,
          proximaCita: cita || null
        })
      });
      const result = await resp.json();
      if (!result.success) throw new Error(result.message || 'No se pudo guardar la nota');

      // Reflejar la nota persistida (y todo el historial guardado) en memoria
      const estadoActualizado = window.smiletrackHistoriaData?.estadoPersistido
        ? JSON.parse(window.smiletrackHistoriaData.estadoPersistido)
        : {};
      estadoActualizado.notasClinicas = [result.nota, ...(estadoActualizado.notasClinicas || [])];
      window.smiletrackHistoriaData.estadoPersistido = JSON.stringify(estadoActualizado);

      const data = historiaStorage.load();
      renderHistorial(data.paciente.historial);

      // Limpiar formulario
      if (fDiag) fDiag.value = '';
      if (fProc) fProc.value = '';
      if (fCita) fCita.value = '';

      // Feedback visual
      btn.textContent = '✓ Guardado';
      btn.style.background = 'var(--green)';
      showToast('Entrada clínica guardada', 'success');
      
      setTimeout(() => {
        btn.textContent = 'Guardar entrada clínica';
        btn.style.background = '';
        btn.disabled = false;
      }, 2200);

      // Scroll al historial
      safeGetElement('cardHist')?.scrollIntoView({ behavior:'smooth', block:'start' });

    } catch (err) {
      console.error('Error al guardar:', err);
      showToast('Error al guardar entrada', 'error');
      btn.disabled = false;
      btn.textContent = 'Guardar entrada clínica';
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Inicializar componentes de UI
  initSidebar();
  initScrollToForm();
  initForm();
  
  // Cargar datos desde localStorage
  const data = historiaStorage.load();
  const p = data.paciente;
  
  // Actualizar metadatos del header
  const metaEl = safeGetElement('patientMeta');
  if (metaEl) {
    const edad = calcEdad(p.fechaNacimiento);
    const edadTexto = edad !== null ? `${edad} años` : 'edad no registrada';
    metaEl.textContent = `${p.nombre} · ${edadTexto} · ${p.grupoSanguineo}`;
    metaEl.setAttribute('aria-label', `Paciente: ${p.nombre}, ${edadTexto}, grupo sanguíneo ${p.grupoSanguineo}`);
  }
  
  // Actualizar botón de código HC
  const hcBtn = safeGetElement('hcBtn');
  if (hcBtn) hcBtn.textContent = `🗂 ${p.codigoHC}`;
  
  // Renderizar componentes con datos cargados
  renderAlertas({
    alergias: [...p.alergias],
    medicamentos: [...p.medicamentos],
    grupoSanguineo: p.grupoSanguineo
  });
  
  renderOdontograma({
    nombrePaciente: p.nombre,
    tipo: tipoDenticion(p.fechaNacimiento),
    tratamientos: { ...p.odontograma },
    observaciones: { ...p.observaciones }
  });
  
  renderHistorial([...p.historial].sort((a,b) => new Date(b.fecha) - new Date(a.fecha)));
  
  // Limpieza al unload
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);