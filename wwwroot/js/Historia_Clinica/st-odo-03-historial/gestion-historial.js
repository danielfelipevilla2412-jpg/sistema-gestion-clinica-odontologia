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

// Yeray (2025) - MIGRACIÓN: parseNotasClinicasPersistidas ya no necesita
// parsear el JSON de ObservacionesGenerales. Las notas clínicas llegan como
// server.notasClinicas (array ya formateado por BuildHistorialPacienteViewModelAsync
// leyendo directamente la tabla Nota_Clinica).
// Se conserva la función por compatibilidad, pero devuelve [] porque las notas
// ya vienen en server.notasClinicas.
const parseNotasClinicasPersistidas = (_estadoPersistidoRaw) => [];

// Fuente de datos real: inyectada por el servidor en window.smiletrackHistoriaData
// (ver Views/Historia_Clinica/st-odo-03-historial/gestion-historial.cshtml).
const historiaStorage = {
  // Yeray (2025) - MIGRACIÓN: las notas clínicas ya no se parsean del JSON de
  // odontograma. Ahora vienen en server.notasClinicas, poblado por el servidor
  // desde la tabla Nota_Clinica en BuildHistorialPacienteViewModelAsync.
  // El historial que ve el profesional es: notas clínicas de BD + citas pasadas.
  load: () => {
    const server = window.smiletrackHistoriaData || {};
    const { tratamientos, observaciones } = parseOdontogramaPersistido(server.estadoPersistido);

    // Notas clínicas reales desde Nota_Clinica (inyectadas por el servidor)
    const notasClinicas = Array.isArray(server.notasClinicas) ? server.notasClinicas : [];

    return {
      paciente: {
        id: server.pacienteId ?? null,
        nombre: server.nombre || 'Sin paciente asignado',
        //  — Consumir la ficha enviada por Razor. Si no hay datos reales en BD,
        // se mantiene vacío en lugar de inventar valores por defecto.
        documento: server.paciente?.documento || '',
        tipoDoc: server.paciente?.tipoDocumento || '',
        genero: server.paciente?.genero || '',
        telefono: server.paciente?.telefono || '',
        correo: server.paciente?.correo || '',
        direccion: [server.paciente?.direccion, server.paciente?.ciudad].filter(Boolean).join(', ') || '',
        contactoEmergencia: [server.paciente?.contactoEmergencia, server.paciente?.telefonoEmergencia].filter(Boolean).join(' · ') || '',
        antecedentesMedicos: server.antecedentesMedicos || '',
        proximaCita: server.proximaCita || null,
        fechaNacimiento: server.fechaNacimiento || null,
        grupoSanguineo: server.grupoSanguineo || '',
        codigoHC: server.codigoHC || '',
        alergias: server.alergias || [],
        medicamentos: server.medicamentos || [],
        odontograma: tratamientos,
        observaciones,
        // Notas clínicas reales de BD + historial de citas del paciente
        historial: Array.isArray(server.lineaDeTiempo) && server.lineaDeTiempo.length
          ? server.lineaDeTiempo
          : [...notasClinicas, ...(server.historial || [])],
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

//  — Estado de solo lectura de la línea de tiempo. Se conserva para que
// los filtros cambien la vista sin volver a consultar ni modificar la BD.
let lineaDeTiempoActual = [];

const escaparHtml = (valor) => String(valor ?? '').replace(/[&<>'"]/g, caracter => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#039;', '"': '&quot;'
}[caracter]));

const etiquetaCategoria = (categoria) => ({
  nota: 'Nota clínica', odontograma: 'Odontograma', consulta: 'Cita',
  control: 'Control', documento: 'Documento'
}[categoria] || 'Evento clínico');

//  — Renderiza eventos de distintas fuentes con filtro por categoría.
// También escapa texto de BD para evitar que una observación se interprete como HTML.
const renderHistorial = (historial, filtro = 'todo') => {
  const list = safeGetElement('historialList');
  if (!list) return;

  lineaDeTiempoActual = Array.isArray(historial) ? historial : [];
  const eventos = filtro === 'todo'
    ? lineaDeTiempoActual
    : lineaDeTiempoActual.filter(h => h.categoria === filtro);
  
  if (!eventos.length) {
    list.innerHTML = '<p style="font-size:.85rem;color:var(--text-muted);padding:12px 0;text-align:center;">No hay eventos clínicos para este filtro.</p>';
    return;
  }
  
  list.innerHTML = eventos.map(h => {
    const fecha = formatFecha(h.fecha, true);
    const desc = h.descripcion || h.procedimiento || h.diagnostico || '';
    const categoria = h.categoria || 'consulta';
    const enlaceDocumento = typeof h.enlaceDocumento === 'string' && h.enlaceDocumento.startsWith('/')
      ? `<a class="hist-link" href="${escaparHtml(h.enlaceDocumento)}" target="_blank" rel="noopener">Abrir documento</a>`
      : '';
    return `
      <div class="hist-item" role="listitem">
        <div class="hist-bullet" aria-hidden="true">🦷</div>
        <div class="hist-body">
          <div class="hist-top">
            <span class="hist-title">${escaparHtml(h.titulo)}</span>
            <span class="hist-badge" role="status" aria-label="Estado: ${escaparHtml(h.estado)}">● ${escaparHtml(h.estado || 'Registrado')}</span>
          </div>
          <div class="hist-meta"><time datetime="${escaparHtml(h.fecha)}">${fecha}</time> · ${escaparHtml(h.profesional || h.doctor || 'Sin asignar')} · <span class="hist-category">${etiquetaCategoria(categoria)}</span></div>
          ${desc ? `<div class="hist-desc">${escaparHtml(desc)}</div>` : ''}
          ${enlaceDocumento}
        </div>
      </div>`;
  }).join('');
};

//  — Pinta la ficha clínica lateral con los datos serializados por Razor.
// Es estrictamente informativa: no añade listeners ni operaciones de escritura.
const renderResumenPaciente = (paciente) => {
  const asignar = (id, valor) => {
    const elemento = safeGetElement(id);
    if (elemento) elemento.textContent = valor || 'No registrado';
  };

  asignar('patientDocument', [paciente.tipoDoc, paciente.documento].filter(Boolean).join(' '));
  asignar('patientGender', paciente.genero);
  asignar('patientPhone', paciente.telefono);
  asignar('patientEmail', paciente.correo);
  asignar('patientAddress', paciente.direccion);
  asignar('patientEmergency', paciente.contactoEmergencia);
  asignar('patientBackground', paciente.antecedentesMedicos);
  const cita = paciente.proximaCita;
  asignar('patientNextAppointment', cita?.fecha
    ? `${formatFecha(cita.fecha.slice(0, 10), true)}${cita.profesional ? ` · ${cita.profesional}` : ''}`
    : 'Sin cita programada');
};

//  — Rellena el formulario de historial con los datos reales del paciente y la
// última nota clínica guardada. Cuando no existe paciente seleccionado, conserva
// el estado en blanco para que el profesional pueda completar la historia desde 0.
const poblarFormularioHistoria = (datosHistoria = window.smiletrackHistoriaData || {}) => {
  const listaNotas = Array.isArray(datosHistoria.notasClinicas) ? datosHistoria.notasClinicas : [];
  const ultimaNota = listaNotas[0] || {};
  const setValue = (id, value) => {
    const input = document.getElementById(id);
    if (input) input.value = value ?? '';
  };

  const alergias = Array.isArray(datosHistoria.alergias) ? datosHistoria.alergias : [];
  const observacionesOdontograma = datosHistoria.observaciones || {};
  const textoObservacionesOdonto = Object.values(observacionesOdontograma)
    .filter(Boolean)
    .join(' • ');

  const origenEvolucion = [ultimaNota.diagnostico, ultimaNota.procedimiento]
    .filter(Boolean)
    .join(' · ');

  setValue('motivo_consulta', datosHistoria.motivoConsulta || '');
  setValue('enfermedad_actual', datosHistoria.enfermedadActual || origenEvolucion || '');
  setValue('antecedentes_medicos', datosHistoria.antecedentesMedicos || '');
  setValue('habitos', datosHistoria.habitos || '');
  setValue('alergias', alergias.length ? alergias.join(', ') : datosHistoria.alergiasTexto || '');
  setValue('hallazgos', datosHistoria.hallazgos || '');
  setValue('odontograma_observaciones', datosHistoria.odontogramaObservaciones || textoObservacionesOdonto || '');
  setValue('examenes_complementarios', datosHistoria.examenesComplementarios || '');
  setValue('diagnostico_principal', datosHistoria.diagnosticoPrincipal || '');
  setValue('diagnostico_secundario', datosHistoria.diagnosticoSecundario || '');
  setValue('evolucion_clinica', datosHistoria.evolucionClinica || origenEvolucion || '');
  setValue('prescripcion', datosHistoria.prescripcion || '');
};

const renderPacienteInfoShell = (paciente) => {
  const card = document.querySelector('.patient-summary-card');
  if (!card) return;

  if (!paciente || !paciente.id) {
    card.classList.add('empty-patient-card');
    card.innerHTML = `
      <div class="patient-summary-inner">
        <div class="patient-info-block empty-state-block">
          <div class="patient-avatar empty-avatar">—</div>
          <div class="patient-meta">
            <div class="patient-title-row empty-title-row">
              <h2>Paciente no seleccionado</h2>
            </div>
            <div class="patient-details-row empty-details-row">
              <span><strong>Documento:</strong> <span class="mono placeholder-line">&nbsp;</span></span>
              <span><strong>Edad:</strong> <span class="placeholder-line">&nbsp;</span></span>
              <span><strong>Sexo:</strong> <span class="placeholder-line">&nbsp;</span></span>
              <span><strong>Grupo:</strong> <span class="placeholder-line">&nbsp;</span></span>
              <span><strong>Teléfono:</strong> <span class="placeholder-line">&nbsp;</span></span>
              <span><strong>Email:</strong> <span class="placeholder-line">&nbsp;</span></span>
            </div>
          </div>
        </div>
      </div>`;
    return;
  }

  const fechaNacimiento = paciente.fechaNacimiento ? new Date(paciente.fechaNacimiento) : null;
  const edad = fechaNacimiento && !Number.isNaN(fechaNacimiento.getTime()) ? `${calcEdad(paciente.fechaNacimiento)} años` : 'No registrada';

  card.classList.remove('empty-patient-card');
  card.innerHTML = `
    <div class="patient-summary-inner">
      <div class="patient-info-block">
        <div class="patient-avatar">${escaparHtml((paciente.nombre || 'P').charAt(0).toUpperCase())}</div>
        <div class="patient-meta">
          <div class="patient-title-row">
            <h2>${escaparHtml(paciente.nombre || 'Paciente')}</h2>
            <span class="status-pill">${escaparHtml(paciente.grupoSanguineo || 'N/D')}</span>
          </div>
          <div class="patient-details-row">
            <span><strong>Documento:</strong> ${escaparHtml([paciente.tipoDoc, paciente.documento].filter(Boolean).join(' ') || 'No registrado')}</span>
            <span><strong>Edad:</strong> ${escaparHtml(edad)}</span>
            <span><strong>Sexo:</strong> ${escaparHtml(paciente.genero || 'No registrado')}</span>
            <span><strong>Grupo:</strong> ${escaparHtml(paciente.grupoSanguineo || 'N/D')}</span>
            <span><strong>Teléfono:</strong> ${escaparHtml(paciente.telefono || 'No registrado')}</span>
            <span><strong>Email:</strong> ${escaparHtml(paciente.correo || 'No registrado')}</span>
          </div>
        </div>
      </div>
    </div>`;
};

//  — Activa filtros locales para consulta rápida de notas, citas,
// odontograma y documentos, sin recargar ni alterar el historial guardado.
const initFiltrosLineaDeTiempo = () => {
  const contenedor = safeGetElement('timelineFilters');
  if (!contenedor) return;

  contenedor.addEventListener('click', event => {
    const boton = event.target.closest('[data-filter]');
    if (!boton) return;
    contenedor.querySelectorAll('[data-filter]').forEach(item => item.classList.toggle('active', item === boton));
    renderHistorial(lineaDeTiempoActual, boton.dataset.filter || 'todo');
  });
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
          ...(token ? { 'X-CSRF-TOKEN': token } : {})
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

      // Yeray (2025) - MIGRACIÓN: antes se actualizaba el JSON de estadoPersistido
      // para reflejar la nota en memoria (parseNotasClinicasPersistidas lo leía).
      // Ahora se actualiza directamente server.notasClinicas, que es el array
      // que historiaStorage.load() usa desde la migración a tablas reales.
      window.smiletrackHistoriaData = window.smiletrackHistoriaData || {};
      window.smiletrackHistoriaData.notasClinicas = [
        result.nota,
        ...(window.smiletrackHistoriaData.notasClinicas || [])
      ];
      //  — Reflejar inmediatamente la nota recién guardada en la línea
      // de tiempo; al recargar, la misma nota se vuelve a obtener desde BD.
      window.smiletrackHistoriaData.lineaDeTiempo = [
        {
          fecha: `${result.nota.fecha}T12:00:00`,
          categoria: 'nota',
          titulo: result.nota.titulo,
          descripcion: [result.nota.diagnostico, result.nota.procedimiento].filter(Boolean).join(' · '),
          profesional: result.nota.doctor,
          estado: result.nota.estado,
          enlaceDocumento: null
        },
        ...(window.smiletrackHistoriaData.lineaDeTiempo || [])
      ];

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

const initFormularioClinico = () => {
  const form = safeGetElement('historia-clinica-form');
  if (!form) return;

  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const pacienteId = window.smiletrackHistoriaData?.pacienteId;
    if (!pacienteId) {
      showToast('Selecciona un paciente antes de guardar la historia', 'warning');
      return;
    }

    const value = id => safeGetElement(id)?.value?.trim() || '';
    const button = form.querySelector('button[type="submit"]');
    if (button) button.disabled = true;

    try {
      const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
      const response = await fetch('/historia-clinica/st-odo-03-historial/guardar-formulario', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { 'RequestVerificationToken': token } : {})
        },
        body: JSON.stringify({
          pacienteId,
          motivoConsulta: value('motivo_consulta'),
          enfermedadActual: value('enfermedad_actual'),
          habitos: value('habitos'),
          hallazgos: value('hallazgos'),
          odontogramaObservaciones: value('odontograma_observaciones'),
          examenesComplementarios: value('examenes_complementarios'),
          diagnosticoPrincipal: value('diagnostico_principal'),
          diagnosticoSecundario: value('diagnostico_secundario'),
          evolucionClinica: value('evolucion_clinica'),
          prescripcion: value('prescripcion')
        })
      });
      const result = await response.json();
      if (!response.ok || !result.success) throw new Error(result.message || 'No se pudo guardar la historia');
      showToast('Historia clínica guardada correctamente', 'success');
    } catch (error) {
      console.error('Error al guardar la historia clínica:', error);
      showToast(error.message || 'No se pudo guardar la historia clínica', 'error');
    } finally {
      if (button) button.disabled = false;
    }
  });
};

const initAdjuntosClinicos = () => {
  const button = safeGetElement('btnAdjuntarEstudio');
  const input = safeGetElement('inputArchivosEstudio');
  const fileGrid = safeGetElement('fileGrid');
  const uploadBox = safeGetElement('uploadBox');

  if (!button || !input || !fileGrid || !uploadBox) return;

  const archivos = [];

  const formatBytes = (bytes) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const getIcon = (fileName = '') => {
    const extension = fileName.split('.').pop()?.toLowerCase();
    if (['jpg', 'jpeg', 'png'].includes(extension)) return '🖼️';
    if (extension === 'pdf') return '📄';
    return '📎';
  };

  const renderArchivos = () => {
    const cards = archivos.map((file, index) => `
      <div class="file-item" data-index="${index}">
        <div class="file-thumb">${getIcon(file.name)}</div>
        <div class="file-meta">
          <strong>${escaparHtml(file.name)}</strong>
          <span>${formatBytes(file.size)}</span>
        </div>
        <button type="button" class="soft-btn subtle remove-attachment" data-index="${index}" style="margin-top:10px;">Quitar</button>
      </div>
    `).join('');

    fileGrid.innerHTML = `${cards}${`
      <div class="upload-box" id="uploadBox" tabindex="0" role="button" aria-label="Arrastra o selecciona estudios">
        <div class="upload-ghost">⇪</div>
        <strong>Arrastra o selecciona estudios</strong>
        <span>Radiografías, fotos intraorales o documentos anexos</span>
      </div>
    `}`;

    const freshUploadBox = safeGetElement('uploadBox');
    if (!freshUploadBox) return;

    freshUploadBox.addEventListener('click', () => input.click());
    freshUploadBox.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        input.click();
      }
    });
    freshUploadBox.addEventListener('dragover', (event) => {
      event.preventDefault();
      freshUploadBox.classList.add('dragover');
    });
    freshUploadBox.addEventListener('dragleave', () => {
      freshUploadBox.classList.remove('dragover');
    });
    freshUploadBox.addEventListener('drop', (event) => {
      event.preventDefault();
      freshUploadBox.classList.remove('dragover');
      const dropped = Array.from(event.dataTransfer?.files || []);
      if (dropped.length) addArchivos(dropped);
    });

    fileGrid.querySelectorAll('.remove-attachment').forEach((removeButton) => {
      removeButton.addEventListener('click', (event) => {
        const { index } = event.currentTarget.dataset;
        archivos.splice(Number(index), 1);
        renderArchivos();
      });
    });
  };

  const addArchivos = (nuevosArchivos) => {
    const validFiles = Array.from(nuevosArchivos).filter((file) => {
      const isAllowed = ['image/jpeg', 'image/png', 'application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];
      const extension = file.name.split('.').pop()?.toLowerCase();
      const allowedExt = ['jpg', 'jpeg', 'png', 'pdf', 'doc', 'docx'];
      return isAllowed.includes(file.type) || (extension && allowedExt.includes(extension));
    });

    if (!validFiles.length) {
      showToast('Tipo de archivo no permitido. Usa JPG, PNG, PDF o DOCX', 'warning');
      input.value = '';
      return;
    }

    validFiles.forEach(file => archivos.push(file));
    renderArchivos();
    input.value = '';
  };

  button.addEventListener('click', () => input.click());
  input.addEventListener('change', (event) => {
    if (!event.target.files?.length) return;
    addArchivos(event.target.files);
  });

  uploadBox.addEventListener('click', () => input.click());
  uploadBox.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      input.click();
    }
  });
  uploadBox.addEventListener('dragover', (event) => {
    event.preventDefault();
    uploadBox.classList.add('dragover');
  });
  uploadBox.addEventListener('dragleave', () => {
    uploadBox.classList.remove('dragover');
  });
  uploadBox.addEventListener('drop', (event) => {
    event.preventDefault();
    uploadBox.classList.remove('dragover');
    const dropped = Array.from(event.dataTransfer?.files || []);
    if (dropped.length) addArchivos(dropped);
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
  initFormularioClinico();
  initAdjuntosClinicos();
  //  — Registrar los filtros antes de renderizar los datos clínicos.
  initFiltrosLineaDeTiempo();
  
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

  //  — Si existe paciente activo, se rellena la plantilla editable con la última
  // información clínica real disponible; si no hay paciente, se deja en blanco.
  renderPacienteInfoShell(p);
  poblarFormularioHistoria(window.smiletrackHistoriaData || {});
  
  // Renderizar componentes con datos cargados
  renderAlertas({
    alergias: [...p.alergias],
    medicamentos: [...p.medicamentos],
    grupoSanguineo: p.grupoSanguineo
  });
  // Mostrar ficha completa antes del odontograma y la línea de tiempo.
  renderResumenPaciente(p);
  
  renderHistorial([...p.historial].sort((a,b) => new Date(b.fecha) - new Date(a.fecha)));
  
  // Limpieza al unload
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);