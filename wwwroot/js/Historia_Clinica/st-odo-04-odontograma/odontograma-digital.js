 /**
 * SMILETRACK — ODONTOGRAMA DIGITAL 3D
 */

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES
// ═══════════════════════════════════════════════════════════════════

const safeGetElement = (id) => {
    const el = document.getElementById(id);
    if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
    return el;
};

const getAntiForgeryToken = () =>
    document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

const showToast = (message, type = 'success') => {
    const toast = safeGetElement('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
    if (toast._timeoutId) clearTimeout(toast._timeoutId);
    toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

const escapeHtml = (value) => String(value ?? '').replace(/[&<>'"]/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    "'": '&#39;',
    '"': '&quot;'
})[character]);

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const ESTADOS = [
    { key: 'caries',     label: 'Caries',     color: '#ff4d6d', ico: '⚠️' },
    { key: 'endodoncia', label: 'Endodoncia', color: '#378ADD', ico: '🔬' },
    { key: 'sellante',   label: 'Sellante',   color: '#f4a62a', ico: '🛡️' },
    { key: 'placa',      label: 'Placa',      color: '#1D9E75', ico: '🦠' },
    { key: 'sano',       label: 'Sano',       color: '#00d4ff', ico: '✅' },
    { key: 'obturacion', label: 'Obturación', color: '#0f766e', ico: '◉' },
    { key: 'restauracion', label: 'Restauración', color: '#10b981', ico: '◈' },
    { key: 'corona',     label: 'Corona',     color: '#a855f7', ico: '⬡' },
    { key: 'extraccion', label: 'Extracción', color: '#64748b', ico: '×' },
    { key: 'ausente',    label: 'Ausente',    color: '#94a3b8', ico: '—' },
    { key: 'implante',   label: 'Implante',   color: '#0369a1', ico: '▣' },
    { key: 'fractura',   label: 'Fractura',   color: '#dc2626', ico: '⌁' },
    { key: 'protesis',   label: 'Prótesis',   color: '#c2410c', ico: '◇' },
];

const MAPEO_FDI = {
    '11': 'Incisivo Central Superior Derecho',   '12': 'Incisivo Lateral Superior Derecho',
    '13': 'Canino Superior Derecho',             '14': 'Primer Premolar Superior Derecho',
    '15': 'Segundo Premolar Superior Derecho',   '16': 'Primer Molar Superior Derecho',
    '17': 'Segundo Molar Superior Derecho',      '18': 'Tercer Molar Superior Derecho',
    '21': 'Incisivo Central Superior Izquierdo', '22': 'Incisivo Lateral Superior Izquierdo',
    '23': 'Canino Superior Izquierdo',           '24': 'Primer Premolar Superior Izquierdo',
    '25': 'Segundo Premolar Superior Izquierdo', '26': 'Primer Molar Superior Izquierdo',
    '27': 'Segundo Molar Superior Izquierdo',    '28': 'Tercer Molar Superior Izquierdo',
    '31': 'Incisivo Central Inferior Izquierdo', '32': 'Incisivo Lateral Inferior Izquierdo',
    '33': 'Canino Inferior Izquierdo',           '34': 'Primer Premolar Inferior Izquierdo',
    '35': 'Segundo Premolar Inferior Izquierdo', '36': 'Primer Molar Inferior Izquierdo',
    '37': 'Segundo Molar Inferior Izquierdo',    '38': 'Tercer Molar Inferior Izquierdo',
    '41': 'Incisivo Central Inferior Derecho',   '42': 'Incisivo Lateral Inferior Derecho',
    '43': 'Canino Inferior Derecho',             '44': 'Primer Premolar Inferior Derecho',
    '45': 'Segundo Premolar Inferior Derecho',   '46': 'Primer Molar Inferior Derecho',
    '47': 'Segundo Molar Inferior Derecho',      '48': 'Tercer Molar Inferior Derecho'
};

// ═══════════════════════════════════════════════════════════════════
//  ESTADO GLOBAL
// ═══════════════════════════════════════════════════════════════════

let seleccionadoNodeId = null;
let seleccionadoNombre = "";
let baseDatosTratamientos = {};
let mapeoFDI = {};

const config = window.smiletrackOdontogramaConfig || {};
const paciente = {
    id: config.pacienteId || null,
    historiaId: config.historiaId || null,
    nombre: config.pacienteNombre || 'Paciente',
    codigoHC: config.codigoHC || '',
    fechaNacimiento: config.fechaNacimiento || '',
    // Yeray (2025): citaId activa inyectada por la vista si el profesional
    // llegó desde la agenda. Se usa en persistirEstado() para registrar
    // la trazabilidad diente↔cita en Registro_Odontograma.
    citaId: config.citaId || null,
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA
// ═══════════════════════════════════════════════════════════════════

function cargarDatos() {
    baseDatosTratamientos = {};
    mapeoFDI = Object.fromEntries(
        Object.keys(MAPEO_FDI).map(numeroFdi => [`fdi-${numeroFdi}`, numeroFdi])
    );

    try {
        if (config.estadoPersistido) {
            const persistido = typeof config.estadoPersistido === 'string' ? JSON.parse(config.estadoPersistido) : config.estadoPersistido;
            const registros = normalizarRegistros(persistido?.registros);
            const mapeoGuardado = persistido?.mapeoFDI || {};
            Object.entries(registros).forEach(([instanceId, registro]) => {
                const numeroFdi = mapeoGuardado[instanceId] || instanceId.match(/^fdi-(\d{2})$/)?.[1];
                if (numeroFdi in MAPEO_FDI) baseDatosTratamientos[`fdi-${numeroFdi}`] = registro;
            });
        }
    } catch (e) {
        console.error('No se pudieron cargar los datos persistidos del odontograma.', e);
        baseDatosTratamientos = {};
    }

    try {
        localStorage.removeItem('odontograma_historial');
        localStorage.removeItem('odontograma_mapeo_fdi');
    } catch (e) {
        console.warn('No se pudo borrar el estado local heredado del odontograma.', e);
    }
}

function normalizarRegistros(registros) {
    if (!registros || typeof registros !== 'object' || Array.isArray(registros)) {
        return {};
    }

    return Object.fromEntries(Object.entries(registros).map(([instanceID, datos]) => {
        const pieza = datos && typeof datos === 'object' && !Array.isArray(datos) ? datos : {};
        const tratamientos = pieza.tratamientos ?? pieza.Tratamientos;
        return [instanceID, {
            nombrePieza: pieza.nombrePieza ?? pieza.NombrePieza ?? '',
            tratamientos: Array.isArray(tratamientos)
                ? tratamientos.map(tratamiento => ({
                    key: tratamiento.key ?? tratamiento.Key ?? '',
                    obs: tratamiento.obs ?? tratamiento.Obs ?? '',
                    fecha: tratamiento.fecha ?? tratamiento.Fecha ?? ''
                }))
                : []
        }];
    }));
}

async function persistirEstado({ mostrarToast: shouldShowToast = true } = {}) {
    if (!Number.isInteger(Number(paciente.id)) || Number(paciente.id) <= 0) {
        if (shouldShowToast) showToast('No hay un paciente válido seleccionado.', 'error');
        return false;
    }

    try {
        const response = await fetch('/historia-clinica/st-odo-04-odontograma/guardar', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-CSRF-TOKEN': getAntiForgeryToken()
            },
            credentials: 'same-origin',
            body: JSON.stringify({
                pacienteId: paciente.id,
                // Yeray (2025): se incluye citaId para trazabilidad diente↔cita.
                // Si no hay cita activa (config.citaId es null/undefined) se omite,
                // lo que equivale al comportamiento anterior (IdCita = null en BD).
                citaId: config.citaId || null,
                registros: baseDatosTratamientos,
                mapeoFDI: mapeoFDI
            })
        });

        const data = await response.json().catch(() => ({}));
        if (!response.ok || data.success === false) {
            throw new Error(data.message || 'No se pudo guardar en el servidor');
        }

        if (shouldShowToast) showToast('✅ Cambios guardados correctamente', 'success');
        return true;
    } catch (e) {
        console.error('No se pudo guardar el odontograma en el servidor.', e);
        if (shouldShowToast) showToast('No se pudieron guardar los cambios en el servidor.', 'error');
        return false;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  PANEL DE DIAGNÓSTICO
// ═══════════════════════════════════════════════════════════════════

function abrirPanelDiagnostico() {
    const card = safeGetElement('cardPieza');
    if (!card) return;
    card.style.display = 'block';

    const titulo = safeGetElement('piezaTitulo');
    if (titulo) titulo.textContent = `DIAGNÓSTICO: ${seleccionadoNombre}`;

    const datosPrevios = baseDatosTratamientos[seleccionadoNodeId];
    const tratamientoActual = datosPrevios && datosPrevios.tratamientos && datosPrevios.tratamientos.length > 0
        ? datosPrevios.tratamientos[datosPrevios.tratamientos.length - 1]
        : { key: '', obs: '' };

    safeGetElement('obsTa').value = tratamientoActual.obs || '';

    const estadoEl = safeGetElement('piezaEstado');
    if (tratamientoActual.key) {
        const est = ESTADOS.find(x => x.key === tratamientoActual.key) || { label: 'Estado no reconocido', color: '#64748b' };
        estadoEl.innerHTML = `<strong>Estado actual:</strong> ${escapeHtml(est.label)}`;
        estadoEl.style.background = `${est.color}22`;
        estadoEl.style.border = `1.5px solid ${est.color}`;
        estadoEl.style.color = est.color;
    } else {
        estadoEl.innerHTML = '<strong>Estado actual:</strong> Sin registrar';
        estadoEl.style.background = 'var(--bg)';
        estadoEl.style.border = '1.5px solid var(--border)';
        estadoEl.style.color = 'var(--text-muted)';
    }

    const btnsContainer = safeGetElement('estadoBtns');
    btnsContainer.innerHTML = '';
    ESTADOS.forEach(e => {
        const b = document.createElement('button');
        b.className = 'eb';
        const sel = tratamientoActual.key === e.key;
        if (sel) {
            b.className = 'eb sel';
            b.style.cssText = `border-color:${e.color}; color:${e.color}; background:${e.color}22;`;
        }
        b.innerHTML = `<span class="eb-dot" style="background:${e.color}"></span>${e.label}`;
        b.onclick = () => {
            btnsContainer.setAttribute('data-selected-key', e.key);
            Array.from(btnsContainer.children).forEach(c => c.style.cssText = '');
            b.style.cssText = `border-color:${e.color}; color:${e.color}; background:${e.color}22; border-width:2px;`;
        };
        btnsContainer.appendChild(b);
        if (sel) btnsContainer.setAttribute('data-selected-key', e.key);
    });

    renderHistorialPieza();
    card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

function renderOdontogramaFDI() {
    const chart = safeGetElement('odontogramaFdiChart');
    if (!chart || !window.SmileTrackOdontogramaFDI) return;

    const tratamientos = Object.fromEntries(
        Object.entries(baseDatosTratamientos).map(([instanceId, pieza]) => [
            mapeoFDI[instanceId],
            pieza.tratamientos?.at(-1)?.key || ''
        ])
    );

    window.SmileTrackOdontogramaFDI.render(chart, tratamientos, numeroFdi => {
        seleccionadoNodeId = `fdi-${numeroFdi}`;
        seleccionadoNombre = `${numeroFdi} - ${MAPEO_FDI[numeroFdi]}`;
        abrirPanelDiagnostico();
    });
}

function renderHistorialPieza() {
    const hist = safeGetElement('piezaHist');
    if (!hist) return;
    const datos = baseDatosTratamientos[seleccionadoNodeId];
    if (!datos || !datos.tratamientos || datos.tratamientos.length === 0) {
        hist.innerHTML = '<div style="font-size:.8rem;color:var(--text-muted);padding:8px 0;">Sin historial registrado</div>';
        return;
    }
    hist.innerHTML = datos.tratamientos.map((t, i) => {
        const est = ESTADOS.find(x => x.key === t.key) || { label: 'Estado no reconocido', color: '#64748b' };
        const esActual = i === datos.tratamientos.length - 1;
        const fecha = new Date(t.fecha).toLocaleString('es-ES', {
            day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
        return `
            <div class="hist-entry ${esActual ? 'current' : ''}" role="listitem">
                <div>
                    <time class="hist-entry-date" datetime="${escapeHtml(t.fecha)}">${escapeHtml(fecha)}</time> →
                    <strong style="color:${est.color};">${escapeHtml(est.label)}</strong>
                    ${t.obs ? ' · ' + escapeHtml(t.obs) : ''}
                </div>
                ${esActual ? '<div style="font-size:.7rem;color:var(--primary);margin-top:2px;">● ACTUAL</div>' : ''}
            </div>
        `;
    }).join('');
}

function cerrarPanel() {
    safeGetElement('cardPieza').style.display = 'none';
    seleccionadoNodeId = null;
}

async function guardarRegistro() {
    const btnsContainer = safeGetElement('estadoBtns');
    const estadoKey = btnsContainer?.getAttribute('data-selected-key');
    const observacion = safeGetElement('obsTa').value.trim();

    if (!estadoKey) {
        showToast('Selecciona un estado clínico primero', 'warning');
        return;
    }
    if (!mapeoFDI[seleccionadoNodeId]) {
        showToast('Asigna un número FDI a esta pieza antes de registrar su estado.', 'warning');
        return;
    }

      // Yeray - Se agrega el nombre del profesional actual a cada registro.
    // config.profesionalNombre ya venía inyectado desde el backend (Model.ProfesionalNombre)
    // pero no se estaba guardando junto al tratamiento; ahora queda persistido con la fecha y el estado.
    const nuevo = { key: estadoKey, obs: observacion, fecha: new Date().toISOString(), profesional: config.profesionalNombre || null };

    const registroPrevio = baseDatosTratamientos[seleccionadoNodeId];
    if (!baseDatosTratamientos[seleccionadoNodeId]) {
        baseDatosTratamientos[seleccionadoNodeId] = {
            nombrePieza: seleccionadoNombre,
            tratamientos: []
        };
    }
    baseDatosTratamientos[seleccionadoNodeId].tratamientos.push(nuevo);

    renderUltimasMods();
    updateCounts();
    renderOdontogramaFDI();
    abrirPanelDiagnostico();
    const guardado = await persistirEstado({ mostrarToast: false });
    if (!guardado) {
        baseDatosTratamientos[seleccionadoNodeId].tratamientos.pop();
        if (!registroPrevio) delete baseDatosTratamientos[seleccionadoNodeId];
        renderUltimasMods();
        updateCounts();
        renderOdontogramaFDI();
        abrirPanelDiagnostico();
        showToast('El cambio no se guardó. Verifica la conexión e inténtalo de nuevo.', 'error');
        return;
    }

    showToast('Historial actualizado', 'success');
}

// ═══════════════════════════════════════════════════════════════════
//  ÚLTIMAS MODIFICACIONES
// ═══════════════════════════════════════════════════════════════════

function renderUltimasMods() {
    const el = safeGetElement('ultimasMods');
    if (!el) return;

    const todos = [];

    Object.entries(baseDatosTratamientos || {}).forEach(([id, datos]) => {
        const tratamientos = Array.isArray(datos?.tratamientos)
            ? datos.tratamientos
            : [];

        tratamientos.forEach(t => {
            todos.push({
                instanceID: id,
                nombrePieza: datos?.nombrePieza || `PIEZA ${id}`,
                ...t
            });
        });
    });

    todos.sort((a, b) => new Date(b.fecha) - new Date(a.fecha));
    const recientes = todos.slice(0, 8);

    if (recientes.length === 0) {
        el.innerHTML =
            '<div style="font-size:.8rem;color:var(--text-muted);padding:6px 0;">Sin modificaciones.</div>';
        return;
    }

    el.innerHTML = recientes.map(m => {
        const est = ESTADOS.find(x => x.key === m.key) || ESTADOS[0];
        const fecha = new Date(m.fecha).toLocaleString('es-ES', {
            day: '2-digit',
            month: 'short',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        return `
            <div class="mod-item" role="listitem">
                <span class="mod-num">${escapeHtml(m.nombrePieza)}</span> →
                <span class="mod-est" style="color:${est.color};">${escapeHtml(est.label)}</span> ·
                <time datetime="${escapeHtml(m.fecha)}">${escapeHtml(fecha)}</time>
            </div>
        `;
    }).join('');
}


// ═══════════════════════════════════════════════════════════════════
//  CONTADORES / STATS
// ═══════════════════════════════════════════════════════════════════

function updateCounts() {
    const piezas = Object.values(baseDatosTratamientos);
    const totalMapeado = Object.keys(MAPEO_FDI).length;

    let sanos = 0, caries = 0, tratados = 0;

    piezas.forEach(p => {
        if (!p.tratamientos || p.tratamientos.length === 0) return;
        const actual = p.tratamientos[p.tratamientos.length - 1].key;
        if (actual === 'sano') sanos++;
        else if (actual === 'caries') caries++;
        else tratados++;
    });

    animateCounter(safeGetElement('stat-sanos'), sanos);
    animateCounter(safeGetElement('stat-caries'), caries);
    animateCounter(safeGetElement('stat-tratamientos'), tratados);
    animateCounter(safeGetElement('stat-total'), totalMapeado);
}

function animateCounter(el, target) {
    if (!el) return;
    let cur = parseInt(el.textContent) || 0;
    const step = Math.max(1, Math.ceil(Math.abs(target - cur) / 20));
    const dir = target > cur ? 1 : -1;
    const t = setInterval(() => {
        cur += step * dir;
        if ((dir > 0 && cur >= target) || (dir < 0 && cur <= target)) {
            cur = target;
            clearInterval(t);
        }
        el.textContent = cur;
    }, 30);
}

// ═══════════════════════════════════════════════════════════════════
//  EXPORTAR
// ═══════════════════════════════════════════════════════════════════

function exportarHistorial() {
    if (Object.keys(baseDatosTratamientos).length === 0) {
        showToast('No hay registros para exportar', 'warning');
        return;
    }
    const datos = {
        fechaExportacion: new Date().toISOString(),
        paciente: paciente,
        totalPiezas: Object.keys(baseDatosTratamientos).length,
        registros: baseDatosTratamientos,
        mapeoFDI: mapeoFDI
    };
    const blob = new Blob([JSON.stringify(datos, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `odontograma_${paciente.codigoHC}_${Date.now()}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    showToast('📥 Historial exportado', 'success');
}

// ═══════════════════════════════════════════════════════════════════
//  GUARDAR CAMBIOS
// ═══════════════════════════════════════════════════════════════════

function guardarCambios() {
    const modal = safeGetElement('modalGuardar');
    if (modal) openModal(modal);
}

async function confirmarGuardado() {
    closeModal(safeGetElement('modalGuardar'));
    const btn = safeGetElement('btnGuardar');
    const orig = btn?.innerHTML || '💾 Guardar cambios';
    if (btn) {
        btn.innerHTML = '✓ Guardando...';
        btn.disabled = true;
    }

    try {
        await persistirEstado({ mostrarToast: true });
    } finally {
        if (btn) {
            btn.innerHTML = orig;
            btn.disabled = false;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
//  MODALES
// ═══════════════════════════════════════════════════════════════════

function openModal(modalEl) {
    if (!modalEl) return;
    modalEl.classList.add('open');
    modalEl.setAttribute('aria-hidden', 'false');
    modalEl.removeAttribute('inert');
    modalEl.querySelector('.modal-close')?.focus();
    document.body.style.overflow = 'hidden';
}

function closeModal(modalEl) {
    if (!modalEl) return;
    modalEl.classList.remove('open');
    modalEl.setAttribute('aria-hidden', 'true');
    modalEl.setAttribute('inert', '');
    document.body.style.overflow = '';
}

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR MÓVIL
// ═══════════════════════════════════════════════════════════════════

const toggleSidebar = (show) => {
    const sb = safeGetElement('sidebar');
    const ov = safeGetElement('overlay');
    const hb = safeGetElement('hamburger');
    if (!sb || !ov) return;

    if (show) {
        sb.classList.add('open');
        ov.classList.add('open');
        hb?.setAttribute('aria-expanded', 'true');
    } else {
        sb.classList.remove('open');
        ov.classList.remove('open');
        hb?.setAttribute('aria-expanded', 'false');
    }
};

// ═══════════════════════════════════════════════════════════════════
//  FECHA HEADER
// ═══════════════════════════════════════════════════════════════════

function updateHeaderDate() {
    const now = new Date();
    const days = ['Domingo','Lunes','Martes','Miércoles','Jueves','Viernes','Sábado'];
    const months = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
    const str = `${days[now.getDay()]}, ${now.getDate()} de ${months[now.getMonth()]} ${now.getFullYear()}`;
    const el = safeGetElement('headerDate');
    if (el) {
        el.textContent = str;
        el.setAttribute('datetime', now.toISOString().split('T')[0]);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  SIDEBAR ACCORDION
// ═══════════════════════════════════════════════════════════════════

function initSidebarAccordion() {
    document.querySelectorAll('.nav-group-header').forEach(header => {
        const items = header.nextElementSibling;
        const arrow = header.querySelector('.nav-arrow');
        if (!items || !arrow) return;

        const setExpanded = (isExpanded) => {
            items.style.maxHeight = isExpanded ? `${items.scrollHeight}px` : '0px';
            arrow.style.transform = isExpanded ? 'rotate(0deg)' : 'rotate(-90deg)';
            header.setAttribute('aria-expanded', String(isExpanded));
        };

        setExpanded(false); // Cerrado por defecto

        const toggle = () => {
            const isExpanded = header.getAttribute('aria-expanded') === 'true';
            setExpanded(!isExpanded);
        };

        header.addEventListener('click', toggle);
        header.addEventListener('keydown', e => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
        });
    });
}

// ═══════════════════════════════════════════════════════════════════
//  INIT
// ═══════════════════════════════════════════════════════════════════

function init() {
    // Sidebar
    safeGetElement('hamburger')?.addEventListener('click', () => toggleSidebar(true));
    safeGetElement('overlay')?.addEventListener('click', () => toggleSidebar(false));
    document.querySelectorAll('.nav-item').forEach(item => {
        item.addEventListener('click', () => {
            if (window.innerWidth <= 680) toggleSidebar(false);
        });
    });

    // Sidebar accordion
    initSidebarAccordion();

    // Header
    updateHeaderDate();
    const patientNameEl = safeGetElement('patientName');
    const patientHcEl = safeGetElement('patientHC');
    if (patientNameEl) patientNameEl.textContent = paciente.nombre;
    if (patientHcEl) patientHcEl.textContent = paciente.codigoHC;

    // Cargar datos
    cargarDatos();

    // Botones
    safeGetElement('btnGuardar')?.addEventListener('click', guardarCambios);

    // UI
    renderUltimasMods();
    updateCounts();

    renderOdontogramaFDI();
    safeGetElement('btnMapeo')?.addEventListener('click', () => {
        const chart = safeGetElement('odontogramaFdiChart');
        chart?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        chart?.querySelector('.fdi-tooth')?.focus({ preventScroll: true });
    });

    // Modal
    safeGetElement('modalGuardarClose')?.addEventListener('click', () => closeModal(safeGetElement('modalGuardar')));
    safeGetElement('modalGuardarCancel')?.addEventListener('click', () => closeModal(safeGetElement('modalGuardar')));
    safeGetElement('modalGuardarConfirm')?.addEventListener('click', confirmarGuardado);
    safeGetElement('modalGuardar')?.addEventListener('click', (e) => {
        if (e.target.id === 'modalGuardar') closeModal(e.target);
    });

    // Escape cierra modal/sidebar
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeModal(safeGetElement('modalGuardar'));
            toggleSidebar(false);
        }
    });
}

document.addEventListener('DOMContentLoaded', init);
