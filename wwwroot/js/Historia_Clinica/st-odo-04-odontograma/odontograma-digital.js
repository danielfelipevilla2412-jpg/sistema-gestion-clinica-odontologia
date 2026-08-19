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

const showToast = (message, type = 'success') => {
    const toast = safeGetElement('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
    if (toast._timeoutId) clearTimeout(toast._timeoutId);
    toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN
// ═══════════════════════════════════════════════════════════════════

const ESTADOS = [
    { key: 'caries',     label: 'Caries',     color: '#ff4d6d', ico: '⚠️' },
    { key: 'endodoncia', label: 'Endodoncia', color: '#378ADD', ico: '🔬' },
    { key: 'sellante',   label: 'Sellante',   color: '#f4a62a', ico: '🛡️' },
    { key: 'placa',      label: 'Placa',      color: '#1D9E75', ico: '🦠' },
    { key: 'sano',       label: 'Sano',       color: '#00d4ff', ico: '✅' },
];

const MODELO_ADULTO = '7f5b381c66674e0a969e8db04d139666';

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

let apiActual = null;
let apiListo = false;
let seleccionadoNodeId = null;
let seleccionadoNombre = "";
let baseDatosTratamientos = {};
let mapeoFDI = {};
let mapaNodos = {};
let mapeoActivo = false;
let dienteActualMapeo = null;
let tooltipVisible = false;
let ultimoInstanceId = null;

const config = window.smiletrackOdontogramaConfig || {};
const paciente = {
    id: config.pacienteId || null,
    historiaId: config.historiaId || null,
    nombre: config.pacienteNombre || 'Paciente',
    codigoHC: config.codigoHC || 'HC-SIN-ASIGNAR',
    fechaNacimiento: config.fechaNacimiento || '',
};

// ═══════════════════════════════════════════════════════════════════
//  PERSISTENCIA
// ═══════════════════════════════════════════════════════════════════

function cargarDatos() {
    try {
        const hist = localStorage.getItem('odontograma_historial');
        if (hist) baseDatosTratamientos = JSON.parse(hist);

        const mapeo = localStorage.getItem('odontograma_mapeo_fdi');
        if (mapeo) mapeoFDI = JSON.parse(mapeo);

        if (config.estadoPersistido) {
            const persistido = typeof config.estadoPersistido === 'string' ? JSON.parse(config.estadoPersistido) : config.estadoPersistido;
            if (persistido?.registros) baseDatosTratamientos = persistido.registros;
            if (persistido?.mapeoFDI) mapeoFDI = persistido.mapeoFDI;
        }

        console.log('✅ Datos cargados:', Object.keys(baseDatosTratamientos).length, 'piezas,', Object.keys(mapeoFDI).length, 'mapeos');
    } catch (e) {
        console.error('❌ Error al cargar datos:', e);
        baseDatosTratamientos = {};
        mapeoFDI = {};
    }
}

function guardarDatos() {
    try {
        localStorage.setItem('odontograma_historial', JSON.stringify(baseDatosTratamientos));
        localStorage.setItem('odontograma_mapeo_fdi', JSON.stringify(mapeoFDI));
    } catch (e) {
        console.error('❌ Error al guardar datos:', e);
    }
}

async function persistirEstado({ mostrarToast: shouldShowToast = true } = {}) {
    guardarDatos();

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
        console.error('❌ Error al sincronizar con el backend:', e);
        if (shouldShowToast) {
            showToast('⚠️ Se guardaron los cambios localmente, pero no se pudo sincronizar con el servidor', 'warning');
        }
        return false;
    }
}

// ═══════════════════════════════════════════════════════════════════
//  NOMENCLATURA
// ═══════════════════════════════════════════════════════════════════

function obtenerNombreNodo(instanceID) {
    return mapaNodos[instanceID] || `PIEZA ${instanceID}`;
}

function obtenerNombrePieza(nodeName, instanceID) {
    if (mapeoFDI[instanceID]) {
        const num = mapeoFDI[instanceID];
        return `${num} - ${MAPEO_FDI[num] || 'Pieza Dental'}`;
    }
    return nodeName ? nodeName.toUpperCase() : `PIEZA ${instanceID}`;
}

// ═══════════════════════════════════════════════════════════════════
//  TOOLTIP HOVER
// ═══════════════════════════════════════════════════════════════════

function generarTooltipContent(hoverName, instanceID) {
    const historialPieza = baseDatosTratamientos[instanceID];
    let content = `<div class="tooltip-header">${hoverName}</div>`;

    if (historialPieza && historialPieza.tratamientos && historialPieza.tratamientos.length > 0) {
        const tratamientos = historialPieza.tratamientos;
        const actual = tratamientos[tratamientos.length - 1];
        const est = ESTADOS.find(x => x.key === actual.key);

        content += `
            <div class="tooltip-section">
                <div class="tooltip-label">Estado Actual</div>
                <div class="tooltip-current">
                    <div class="tooltip-current-status" style="color:${est.color};">● ${est.label.toUpperCase()}</div>
                    ${actual.obs ? `<div class="tooltip-current-obs">"${actual.obs}"</div>` : ''}
                </div>
            </div>
        `;

        if (tratamientos.length > 1) {
            content += `<div class="tooltip-section"><div class="tooltip-history-title">📊 Historial (${tratamientos.length})</div>`;
            for (let i = tratamientos.length - 1; i >= 0; i--) {
                const t = tratamientos[i];
                const e = ESTADOS.find(x => x.key === t.key);
                const f = new Date(t.fecha).toLocaleString('es-ES', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
                content += `
                    <div class="tooltip-history-item">
                        <div class="tooltip-history-status" style="color:${e.color};">● ${e.label}</div>
                        ${t.obs ? `<div class="tooltip-history-obs">"${t.obs}"</div>` : ''}
                        <div class="tooltip-history-date">${f}</div>
                    </div>
                `;
            }
            content += '</div>';
        }
    } else {
        content += `<div class="tooltip-empty">Sin registros de tratamiento</div>`;
    }
    return content;
}

function actualizarPosicionTooltip(e) {
    const tooltip = safeGetElement('holo-tooltip');
    if (!tooltip) return;
    let x = e.clientX + 20, y = e.clientY + 20;
    const rect = tooltip.getBoundingClientRect();
    if (x + 320 > window.innerWidth) x = e.clientX - 340;
    if (y + rect.height > window.innerHeight) y = e.clientY - rect.height - 20;
    tooltip.style.left = x + 'px';
    tooltip.style.top = y + 'px';
}

// ═══════════════════════════════════════════════════════════════════
//  MAPEO FDI
// ═══════════════════════════════════════════════════════════════════

function toggleMapeoFDI() {
    mapeoActivo = !mapeoActivo;
    const panel = safeGetElement('mapeo-panel');
    const banner = safeGetElement('modo-mapeo-banner');
    const btn = safeGetElement('btnMapeo');

    if (mapeoActivo) {
        panel.classList.add('visible');
        banner.classList.add('visible');
        btn.textContent = '❌ Salir de Mapeo';
        generarBotonesMapeo();
        actualizarProgresoMapeo();
    } else {
        panel.classList.remove('visible');
        banner.classList.remove('visible');
        btn.textContent = '🔧 Mapear FDI';
        dienteActualMapeo = null;
        safeGetElement('mapeo-current').classList.remove('visible');
    }
}

function resetearMapeo() {
    if (confirm('¿Seguro que quieres borrar todo el mapeo FDI?')) {
        mapeoFDI = {};
        guardarDatos();
        generarBotonesMapeo();
        actualizarProgresoMapeo();
        showToast('Mapeo reseteado', 'success');
    }
}

function generarBotonesMapeo() {
    const grid = safeGetElement('mapeo-grid');
    if (!grid) return;
    grid.innerHTML = Object.keys(MAPEO_FDI).map(num => {
        const mapeado = Object.values(mapeoFDI).includes(num);
        return `<button class="mapeo-btn ${mapeado ? 'mapped' : ''}" onclick="asignarFDI('${num}')" title="${MAPEO_FDI[num]}">${num}</button>`;
    }).join('');
}

async function asignarFDI(numeroFDI) {
    if (!dienteActualMapeo) {
        showToast('Primero haz clic en un diente del modelo 3D', 'warning');
        return;
    }
    mapeoFDI[dienteActualMapeo.instanceID] = numeroFDI;
    guardarDatos();
    const nombre = `${numeroFDI} - ${MAPEO_FDI[numeroFDI]}`;
    generarBotonesMapeo();
    actualizarProgresoMapeo();
    const current = safeGetElement('mapeo-current');
    if (current) current.classList.remove('visible');
    dienteActualMapeo = null;
    await persistirEstado({ mostrarToast: false });
    showToast(`✅ Asignado: ${nombre}`, 'success');
}

function actualizarProgresoMapeo() {
    const el = safeGetElement('mapeo-progress');
    if (el) el.textContent = `Dientes mapeados: ${Object.keys(mapeoFDI).length} / 32`;
}

// ═══════════════════════════════════════════════════════════════════
//  INICIALIZACIÓN VISOR SKETCHFAB
// ═══════════════════════════════════════════════════════════════════

function inicializarVisor() {
    const iframe = safeGetElement('api-frame');
    if (!iframe) {
        console.error('❌ No se encontró el iframe del visor');
        return;
    }

    const loading = safeGetElement('viewerLoading');
    const error = safeGetElement('viewerError');
    const tooltip = safeGetElement('holo-tooltip');

    // Asignar la URL correcta al iframe
    const modelURL = `https://sketchfab.com/models/${MODELO_ADULTO}/embed`;
    iframe.src = modelURL;

    console.log('🔄 Cargando modelo desde:', modelURL);

    const client = new Sketchfab(iframe);

    client.init(MODELO_ADULTO, {
        ui_infos: 0, ui_watermark: 0, ui_controls: 1, ui_help: 0,
        ui_settings: 0, ui_vr: 0, ui_fullscreen: 0, ui_annotations: 0, ui_stop: 0,

        success: function(api) {
            console.log('✅ API de Sketchfab inicializada');
            api.start();
            apiActual = api;

            // Ocultar loading cuando esté listo
            if (loading) loading.style.display = 'none';
            if (error) error.style.display = 'none';

            api.addEventListener('viewerready', function() {
                console.log('✅ Viewer ready');
                apiListo = true;

                api.getNodeMap(function(err, nodes) {
                    if (!err) {
                        mapaNodos = {};
                        Object.keys(nodes).forEach(nodeId => {
                            const node = nodes[nodeId];
                            if (node.name) mapaNodos[nodeId] = node.name;
                            if (node.name && (
                                node.name.toLowerCase().includes('screw') ||
                                node.name.toLowerCase().includes('implant') ||
                                node.name.toLowerCase().includes('metal')
                            )) {
                                api.hide(nodeId);
                            }
                        });
                        console.log('🦷 Nodos detectados:', Object.keys(mapaNodos).length);
                    }
                });

                // CLICK en diente
                api.addEventListener('click', function(info) {
                    if (!info || !info.instanceID) return;
                    const instanceID = info.instanceID;
                    const nombreNodo = obtenerNombreNodo(instanceID);

                    if (mapeoActivo) {
                        dienteActualMapeo = { instanceID, nodeName: nombreNodo };
                        const current = safeGetElement('mapeo-current');
                        current.classList.add('visible');
                        safeGetElement('mapeo-current-value').textContent = `${nombreNodo} (ID: ${instanceID})`;
                        return;
                    }

                    seleccionadoNodeId = instanceID;
                    seleccionadoNombre = obtenerNombrePieza(nombreNodo, instanceID);
                    abrirPanelDiagnostico();
                });

                // HOVER sobre diente
                api.addEventListener('hover', function(info) {
                    if (!tooltip) return;
                    if (info && info.instanceID) {
                        const hoverName = obtenerNombrePieza(null, info.instanceID);
                        if (ultimoInstanceId !== info.instanceID) {
                            ultimoInstanceId = info.instanceID;
                            tooltip.innerHTML = generarTooltipContent(hoverName, info.instanceID);
                        }
                        tooltip.style.display = 'block';
                        tooltipVisible = true;
                    } else {
                        tooltip.style.display = 'none';
                        tooltipVisible = false;
                        ultimoInstanceId = null;
                    }
                });

                document.addEventListener('mousemove', function(e) {
                    if (tooltipVisible) actualizarPosicionTooltip(e);
                });
            });
        },

        error: function(err) {
            console.error('❌ Error Sketchfab:', err);
            if (loading) loading.style.display = 'none';
            if (error) error.style.display = 'block';
            showToast('Error al cargar el modelo 3D', 'error');
        }
    });
}

// Reintentar carga
function reintentarCarga() {
    const loading = safeGetElement('viewerLoading');
    const error = safeGetElement('viewerError');
    if (loading) loading.style.display = 'block';
    if (error) error.style.display = 'none';
    inicializarVisor();
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

    safeGetElement('obsTa').value = tratamientoActual.obs;

    const estadoEl = safeGetElement('piezaEstado');
    if (tratamientoActual.key) {
        const est = ESTADOS.find(x => x.key === tratamientoActual.key);
        estadoEl.innerHTML = `<strong>Estado actual:</strong> ${est.label}`;
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

function renderHistorialPieza() {
    const hist = safeGetElement('piezaHist');
    if (!hist) return;
    const datos = baseDatosTratamientos[seleccionadoNodeId];
    if (!datos || !datos.tratamientos || datos.tratamientos.length === 0) {
        hist.innerHTML = '<div style="font-size:.8rem;color:var(--text-muted);padding:8px 0;">Sin historial registrado</div>';
        return;
    }
    hist.innerHTML = datos.tratamientos.map((t, i) => {
        const est = ESTADOS.find(x => x.key === t.key);
        const esActual = i === datos.tratamientos.length - 1;
        const fecha = new Date(t.fecha).toLocaleString('es-ES', {
            day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit'
        });
        return `
            <div class="hist-entry ${esActual ? 'current' : ''}" role="listitem">
                <div>
                    <time class="hist-entry-date" datetime="${t.fecha}">${fecha}</time> →
                    <strong style="color:${est.color};">${est.label}</strong>
                    ${t.obs ? ' · ' + t.obs : ''}
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

    const nuevo = { key: estadoKey, obs: observacion, fecha: new Date().toISOString() };

    if (!baseDatosTratamientos[seleccionadoNodeId]) {
        baseDatosTratamientos[seleccionadoNodeId] = {
            nombrePieza: seleccionadoNombre,
            tratamientos: []
        };
    }
    baseDatosTratamientos[seleccionadoNodeId].tratamientos.push(nuevo);
    guardarDatos();

    renderUltimasMods();
    updateCounts();
    abrirPanelDiagnostico();
    await persistirEstado({ mostrarToast: false });
    showToast('✅ Historial actualizado', 'success');
}

// ═══════════════════════════════════════════════════════════════════
//  ÚLTIMAS MODIFICACIONES
// ═══════════════════════════════════════════════════════════════════

function renderUltimasMods() {
    const el = safeGetElement('ultimasMods');
    if (!el) return;

    const todos = [];
    Object.entries(baseDatosTratamientos).forEach(([id, datos]) => {
        datos.tratamientos.forEach(t => {
            todos.push({ instanceID: id, nombrePieza: datos.nombrePieza, ...t });
        });
    });

    todos.sort((a, b) => new Date(b.fecha) - new Date(a.fecha));
    const recientes = todos.slice(0, 8);

    if (recientes.length === 0) {
        el.innerHTML = '<div style="font-size:.8rem;color:var(--text-muted);padding:6px 0;">Sin modificaciones.</div>';
        return;
    }

    el.innerHTML = recientes.map(m => {
        const est = ESTADOS.find(x => x.key === m.key);
        const fecha = new Date(m.fecha).toLocaleString('es-ES', {
            day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
        });
        return `
            <div class="mod-item" role="listitem">
                <span class="mod-num">${m.nombrePieza}</span> →
                <span class="mod-est" style="color:${est.color};">${est.label}</span> ·
                <time datetime="${m.fecha}">${fecha}</time>
            </div>
        `;
    }).join('');
}

// ═══════════════════════════════════════════════════════════════════
//  CONTADORES / STATS
// ═══════════════════════════════════════════════════════════════════

function updateCounts() {
    const piezas = Object.values(baseDatosTratamientos);
    const totalMapeado = Object.keys(mapeoFDI).length || 32;

    let sanos = 0, caries = 0, tratados = 0;

    piezas.forEach(p => {
        if (!p.tratamientos || p.tratamientos.length === 0) return;
        const actual = p.tratamientos[p.tratamientos.length - 1].key;
        if (actual === 'sano') sanos++;
        else if (actual === 'caries') caries++;
        else tratados++;
    });

    const piezasRegistradas = piezas.length;
    sanos += Math.max(0, totalMapeado - piezasRegistradas);

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
//  EXPORTAR / LIMPIAR
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

async function limpiarTodo() {
    if (Object.keys(baseDatosTratamientos).length === 0) {
        showToast('No hay registros para limpiar', 'warning');
        return;
    }
    if (confirm('⚠️ ¿Eliminar TODO el historial? Esta acción no se puede deshacer.')) {
        baseDatosTratamientos = {};
        guardarDatos();
        renderUltimasMods();
        updateCounts();
        cerrarPanel();
        await persistirEstado({ mostrarToast: false });
        showToast('🗑️ Historial limpiado', 'success');
    }
}

// ═══════════════════════════════════════════════════════════════════
//  GUARDAR CAMBIOS
// ═══════════════════════════════════════════════════════════════════

function guardarCambios() {
    const modal = safeGetElement('modalGuardar');
    if (modal) openModal(modal);
}

async function confirmarGuardado() {
    console.log('📤 Guardando en backend:', baseDatosTratamientos);
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

    // Visor 3D
    inicializarVisor();

    // UI
    renderUltimasMods();
    updateCounts();

    // Botones
    safeGetElement('btnMapeo')?.addEventListener('click', toggleMapeoFDI);
    safeGetElement('btnGuardar')?.addEventListener('click', guardarCambios);

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