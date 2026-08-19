// ── DATOS REALES (API) ──────────────────────────────────────────────────
// Se cargan de forma asíncrona desde el servidor; ver cargarPacientes().
let pacientes = [];
const MESES = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

function fmtFecha(iso) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return `${String(d.getDate()).padStart(2, '0')} ${MESES[d.getMonth()]} ${d.getFullYear()}`;
}

// Normaliza el DTO recibido de /historia-clinica/st-adm-historial/data al formato
// que usa el render de esta vista (fechas ya formateadas para mostrar).
function normalizarPaciente(p) {
    return {
        ...p,
        fechaNac: fmtFecha(p.fechaNac),
        ultimaConsulta: fmtFecha(p.ultimaConsulta),
        consultas: (p.consultas || []).map(c => ({ ...c, fecha: fmtFecha(c.fecha) })),
        tratamientos: p.tratamientos || [],
        documentos: p.documentos || []
    };
}

async function cargarPacientes() {
    try {
        const resp = await fetch('/historia-clinica/st-adm-historial/data', { headers: { 'Accept': 'application/json' } });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const data = await resp.json();
        pacientes = data.map(normalizarPaciente);
    } catch (e) {
        console.error('No se pudo cargar el historial de pacientes:', e);
        pacientes = [];
        showToast('No se pudo cargar el historial de pacientes');
    }
    poblarFiltroOdontologos();
    pacientesFiltrados = [...pacientes];
    renderTabla(pacientesFiltrados);
    actualizarContadores();
    animarContadores();
}

// El select de odontólogo ya no trae opciones fijas en el HTML: se construye
// dinámicamente a partir de los odontólogos que aparecen en los datos reales.
function poblarFiltroOdontologos() {
    const select = document.getElementById('filterOdontologo');
    if (!select) return;
    const vistos = new Map();
    pacientes.forEach(p => { if (p.odontologoKey) vistos.set(p.odontologoKey, p.odontologo); });
    select.innerHTML = '<option value="">Todos los odontólogos</option>' +
        [...vistos.entries()].map(([key, nombre]) => `<option value="${key}">${nombre}</option>`).join('');
}

let pacientesFiltrados = [...pacientes];
let pacienteActivo = null;
let currentPage = 1;
let pageSize = 10;
let totalPages = 1;

function obtenerPagina(lista) {
    pageSize = document.getElementById('pageSizeSelect')?.value === 'all' ? lista.length : Number(document.getElementById('pageSizeSelect')?.value) || 10;
    totalPages = pageSize === 0 ? 1 : Math.max(1, Math.ceil(lista.length / pageSize));
    currentPage = Math.min(Math.max(1, currentPage), totalPages);
    if (pageSize === lista.length) return lista;
    const start = (currentPage - 1) * pageSize;
    return lista.slice(start, start + pageSize);
}

function actualizarPaginacion(totalRegistros) {
    const pageControls = document.getElementById('paginationControls');
    const pageShowing = document.getElementById('pageShowing');
    const pageTotal = document.getElementById('pageTotal');
    const pageInfo = document.getElementById('pageInfo');
    const btnPrev = document.getElementById('btnPrev');
    const btnNext = document.getElementById('btnNext');

    if (!pageControls || totalRegistros <= pageSize) {
        pageControls?.style.setProperty('display', 'none');
        return;
    }

    pageControls.style.display = 'flex';
    pageShowing.textContent = String(Math.min(pageSize, totalRegistros));
    pageTotal.textContent = String(totalRegistros);
    pageInfo.textContent = `Página ${currentPage} de ${totalPages}`;
    if (btnPrev) {
        btnPrev.disabled = currentPage <= 1;
        btnPrev.setAttribute('aria-disabled', String(currentPage <= 1));
    }
    if (btnNext) {
        btnNext.disabled = currentPage >= totalPages;
        btnNext.setAttribute('aria-disabled', String(currentPage >= totalPages));
    }
}

// ── RENDER TABLA ──────────────────────────────────────────────────────────
function renderTabla(lista) {
    const tbody = document.getElementById('tablaBody');
    const count = document.getElementById('resultsCount');
    const noRes = document.getElementById('noResults');
    tbody.innerHTML = '';
    const pagina = obtenerPagina(lista);
    count.textContent = `Mostrando ${pagina.length} de ${lista.length} paciente${lista.length !== 1 ? 's' : ''}`;
    if (lista.length === 0) {
        noRes.style.display = 'block';
        actualizarPaginacion(0);
        return;
    }
    noRes.style.display = 'none';
    pagina.forEach(p => {
const iniciales = p.nombre.split(' ').map(n => n[0]).join('').slice(0,2).toUpperCase();
const tr = document.createElement('tr');
if (pacienteActivo?.id === p.id) tr.classList.add('activa');
tr.innerHTML = `
   <td>
     <div class="td-paciente">
       <div class="pac-avatar">${iniciales}</div>
       <div>
         <div class="pac-name">${p.nombre}</div>
         <div class="pac-email">${p.email}</div>
       </div>
     </div>
   </td>
   <td>${p.cedula}</td>
   <td>${p.odontologo}</td>
   <td>${p.ultimaConsulta}</td>
   <td><span class="badge-alerta ${p.alerta ? 'si' : 'no'}">${p.alerta ? '⚠️ Sí' : '— No'}</span></td>
   <td><span class="badge-estado ${p.estado}">${p.estado.charAt(0).toUpperCase() + p.estado.slice(1)}</span></td>
   <td><button class="btn-ver" onclick="abrirPanel(${p.id})">📋 Historial</button></td>
`;

tbody.appendChild(tr);
    });
    actualizarPaginacion(lista.length);
}

// ── FILTRAR ───────────────────────────────────────────────────────────────
function filtrarPacientes() {
    currentPage = 1;
    const q        = document.getElementById('searchInput').value.toLowerCase().trim();
    const odo      = document.getElementById('filterOdontologo').value;
    const alerta   = document.getElementById('filterAlerta').value;
    const estado   = document.getElementById('filterEstado').value;
    pacientesFiltrados = pacientes.filter(p => {
        const matchQ      = !q || p.nombre.toLowerCase().includes(q) || p.cedula.includes(q) || p.email.toLowerCase().includes(q);
        const matchOdo    = !odo || p.odontologoKey === odo;
        const matchAlerta = !alerta || (alerta === 'si' ? p.alerta : !p.alerta);
        const matchEstado = !estado || p.estado === estado;
        return matchQ && matchOdo && matchAlerta && matchEstado;
    });
    renderTabla(pacientesFiltrados);
}

// ── PANEL LATERAL ─────────────────────────────────────────────────────────
function abrirPanel(id) {
const p = pacientes.find(x => x.id === id);
if (!p) return;
pacienteActivo = p;
renderTabla(pacientesFiltrados); // resaltar fila activa
// Header
document.getElementById('panelNombre').textContent  = p.nombre;
document.getElementById('panelCedula').textContent  = `CC ${p.cedula}`;
// Info grid
document.getElementById('panelInfoGrid').innerHTML = `<div class="info-item"><div class="info-label">Fecha nacimiento</div><div class="info-val">${p.fechaNac}</div></div><div class="info-item"><div class="info-label">Teléfono</div><div class="info-val">${p.telefono}</div></div><div class="info-item"><div class="info-label">Odontólogo</div><div class="info-val">${p.odontologo}</div></div><div class="info-item"><div class="info-label">Estado</div><div class="info-val">${p.estado.charAt(0).toUpperCase() + p.estado.slice(1)}</div></div>`;
// Alerta
const alertaEl = document.getElementById('panelAlerta');
if (p.alerta) {
alertaEl.style.display = 'flex';
document.getElementById('panelAlertaTexto').textContent = p.alertaTexto;
} else {
alertaEl.style.display = 'none';
}
// Consultas
const cList = document.getElementById('consultasList');
cList.innerHTML = p.consultas.map(c => `<div class="consulta-item"><div class="consulta-fecha">${c.fecha}</div><div class="consulta-proc">${c.proc}</div><div class="consulta-odo">👨‍️ ${c.odo}</div><div class="consulta-nota">${c.nota}</div></div>`).join('');
// Tratamientos
const tList = document.getElementById('tratamientosList');
tList.innerHTML = p.tratamientos.map(t => `<div class="trat-item"><div class="trat-nombre">${t.nombre}</div><div class="trat-bar-wrap"><div class="trat-bar" style="width:${t.progreso}%"></div></div><div class="trat-meta"><span>${t.progreso}% completado</span><span>${t.sesiones} sesiones</span></div></div>`).join('');
// Documentos
const dList = document.getElementById('documentosList');
dList.innerHTML = p.documentos.map(d => `<div class="doc-item"><div class="doc-icon">${d.tipo === 'pdf' ? '📄' : '️'}</div><div><div class="doc-nombre">${d.nombre}</div><div class="doc-fecha">${d.fecha}</div></div><span class="doc-badge ${d.tipo}">${d.tipo.toUpperCase()}</span></div>`).join('');
// Activar primera tab
switchTab('consultas', document.querySelector('.tab-btn'));
// Abrir panel
document.getElementById('panelLateral').classList.add('open');
document.getElementById('panelOverlay').classList.add('open');
const mainElement = document.querySelector('.main');
if (mainElement) mainElement.classList.add('panel-open');
}

function cerrarPanel() {
    pacienteActivo = null;
    document.getElementById('panelLateral').classList.remove('open');
    document.getElementById('panelOverlay').classList.remove('open');
    const mainElement = document.querySelector('.main');
    if (mainElement) mainElement.classList.remove('panel-open');
    renderTabla(pacientesFiltrados);
}

function toggleMenu(show) {
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('overlay');
    const hamburger = document.getElementById('hamburger');
    if (!sidebar || !overlay) return;
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    if (hamburger) hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
}

function closeMenu() {
    toggleMenu(false);
}

function initNavGroups() {
    const groupHeaders = document.querySelectorAll('.nav-group-header');
    if (!groupHeaders.length) return;

    groupHeaders.forEach(header => {
        header.setAttribute('aria-expanded', 'false');
        header.addEventListener('click', function () {
            const group = this.closest('.nav-group');
            if (!group) return;
            group.classList.toggle('open');
            this.setAttribute('aria-expanded', String(group.classList.contains('open')));
        });

        header.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                this.click();
            }
        });
    });

    const activeLink = document.querySelector('.nav-item.active');
    if (activeLink) {
        const activeGroup = activeLink.closest('.nav-group');
        if (activeGroup) {
            activeGroup.classList.add('open');
            const header = activeGroup.querySelector('.nav-group-header');
            if (header) header.setAttribute('aria-expanded', 'true');
        }
    }
}

// ── TABS ──────────────────────────────────────────────────────────────────
function switchTab(tab, btn) {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    if (btn) btn.classList.add('active');
    else document.querySelectorAll('.tab-btn')[0].classList.add('active');
    const el = document.getElementById(`tab-${tab}`);
    if (el) el.classList.add('active');
}

// ── CONTADORES ─────────────────────────────────────────────────────────────
function actualizarContadores() {
    const total = pacientes.length;
    const hoy = pacientes.filter(p => {
        const fecha = new Date(p.ultimaConsulta);
        if (Number.isNaN(fecha.getTime())) return false;
        const now = new Date();
        return fecha.getDate() === now.getDate() && fecha.getMonth() === now.getMonth() && fecha.getFullYear() === now.getFullYear();
    }).length;
    const alerta = pacientes.filter(p => p.alerta).length;
    const odoActivos = new Set(pacientes.filter(p => p.estado === 'activo').map(p => p.odontologo)).size;

    const valores = {
        statTotal: total,
        statHoy: hoy,
        statAlerta: alerta,
        statOdo: odoActivos
    };

    Object.entries(valores).forEach(([id, value]) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.dataset.target = String(value);
        el.textContent = '0';
    });
}

function animarContadores() {
    document.querySelectorAll('.stat-number').forEach(el => {
        const target = parseInt(el.dataset.target);
        if (Number.isNaN(target)) return;
        let current = 0;
        const step = Math.max(1, Math.ceil(target / 40));
        const timer = setInterval(() => {
            current = Math.min(current + step, target);
            el.textContent = current;
            if (current >= target) clearInterval(timer);
        }, 30);
    });
}

// ── TOAST ─────────────────────────────────────────────────────────────────
function showToast(msg) {
const t = document.getElementById('toast');
t.textContent = msg;
t.classList.add('show');
setTimeout(() => t.classList.remove('show'), 3000);
}

// ── SIDEBAR MÓVIL ─────────────────────────────────────────────────────────
function toggleSidebar() {
document.getElementById('sidebar').classList.toggle('open');
document.getElementById('overlay').classList.toggle('open');
}

function closeSidebar() {
    document.getElementById('sidebar').classList.remove('open');
    document.getElementById('overlay').classList.remove('open');
}

function inicializarHistoricoAdmin() {
    const searchInput = document.getElementById('searchInput');
    const filterOdontologo = document.getElementById('filterOdontologo');
    const filterAlerta = document.getElementById('filterAlerta');
    const filterEstado = document.getElementById('filterEstado');
    const pageSizeSelect = document.getElementById('pageSizeSelect');
    const btnPrev = document.getElementById('btnPrev');
    const btnNext = document.getElementById('btnNext');
    const hamburger = document.getElementById('hamburger');
    const overlay = document.getElementById('overlay');
    const panelOverlay = document.getElementById('panelOverlay');
    const panelClose = document.getElementById('panelClose');

    if (searchInput) searchInput.addEventListener('input', filtrarPacientes);
    if (filterOdontologo) filterOdontologo.addEventListener('change', filtrarPacientes);
    if (filterAlerta) filterAlerta.addEventListener('change', filtrarPacientes);
    if (filterEstado) filterEstado.addEventListener('change', filtrarPacientes);
    if (pageSizeSelect) pageSizeSelect.addEventListener('change', () => {
        currentPage = 1;
        renderTabla(pacientesFiltrados);
    });
    if (btnPrev) btnPrev.addEventListener('click', () => {
        if (currentPage > 1) {
            currentPage -= 1;
            renderTabla(pacientesFiltrados);
        }
    });
    if (btnNext) btnNext.addEventListener('click', () => {
        if (currentPage < totalPages) {
            currentPage += 1;
            renderTabla(pacientesFiltrados);
        }
    });
    if (hamburger) hamburger.addEventListener('click', () => toggleMenu(!document.getElementById('sidebar')?.classList.contains('open')));
    if (overlay) overlay.addEventListener('click', closeMenu);
    if (panelOverlay) panelOverlay.addEventListener('click', cerrarPanel);
    if (panelClose) panelClose.addEventListener('click', cerrarPanel);
    initNavGroups();
}

// ── INIT ──────────────────────────────────────────────────────────────────
function iniciarHistorialAdmin() {
    inicializarHistoricoAdmin();
    cargarPacientes();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciarHistorialAdmin);
} else {
    iniciarHistorialAdmin();
}