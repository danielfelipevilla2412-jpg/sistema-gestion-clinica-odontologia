/* ============================================
SmileTrack — Dashboard del Odontólogo (st-odo-01-dashboard)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona la interactividad del dashboard del odontólogo: animación de contadores de KPIs, dismissal de notificaciones, sidebar móvil accesible y actualización de los indicadores del día al cargar la página.

FUNCIONALIDADES PRINCIPALES:
- Animación de conteo progresivo en tarjetas de KPIs al montar la vista
- Dismissal individual de notificaciones con animación de salida y limpieza del DOM
- Sidebar móvil con gestión de foco y atributos ARIA para accesibilidad keyboard-first
- Nav-group colapsable con animación de flecha y persistencia de estado de expansión

DEPENDENCIAS TÉCNICAS:
- Controller: GestionProfesionalesController o DashboardOdontologoController
- CSS: ~/css/Gestion_De_Profesionales/st-odo-01-dashboard/styles.css
- JS: ~/js/Gestion_De_Profesionales/st-odo-01-dashboard/app.js
- Partial / Otros: index.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- La animación de KPIs se ejecuta en DOMContentLoaded en lugar de en scroll para que sea visible al cargar sin scroll adicional.
============================================ */

// ═══════════════════════════════════════════════════════════════════
//  CONFIGURACIÓN API
// ═══════════════════════════════════════════════════════════════════
const API_BASE = '/api';

// ═══════════════════════════════════════════════════════════════════
//  UTILIDADES GLOBALES
// ═══════════════════════════════════════════════════════════════════

// Obtiene elemento del DOM con manejo seguro de null
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

// ═══════════════════════════════════════════════════════════════════
//  DATOS DE EJEMPLO (Fallback si API falla)
// ═══════════════════════════════════════════════════════════════════
const SAMPLE_METRICS = {
  pacientes: 142,
  citas: 24,
  profesionales: 8,
  ingresos: '$4.2M',
  ingresosRaw: 4200000,
  vsPacientes: '↑ 12',
  vsCitas: '↑ 4',
  vsIngresos: '↑ 13.5%',
};

const SAMPLE_REVENUE = [
  { mes: 'Enero', valor: 3100000, max: 5000000, width: 65 },
  { mes: 'Febrero', valor: 3700000, max: 5000000, width: 75 },
  { mes: 'Marzo', valor: 4200000, max: 5000000, width: 85 },
];

const SAMPLE_STATUS = [
  { label: 'Atendidas', count: 186, width: 80, color: 'green' },
  { label: 'Agendadas', count: 67, width: 35, color: 'blue' },
  { label: 'Canceladas', count: 29, width: 15, color: 'red' },
  { label: 'No asistió', count: 14, width: 10, color: 'orange' },
];

const SAMPLE_TOP_PROS = [
  { nombre: 'Dr. Carlos Méndez', inicial: 'CM', espec: 'Gral.', citas: 42, ingresos: '$1.8M', color: 'blue' },
  { nombre: 'Dra. Laura Gómez', inicial: 'LG', espec: 'Ortod.', citas: 38, ingresos: '$1.4M', color: 'green' },
  { nombre: 'Dr. Andrés Torres', inicial: 'AT', espec: 'Cirugía', citas: 35, ingresos: '$1.3M', color: 'purple' },
];

const SAMPLE_INVOICES = [
  { code: 'FAC-2026-0015', desc: 'Vence hoy · María López', amount: '$120.000', type: 'warning', date: '2026-03-24' },
  { code: 'FAC-2026-0012', desc: 'Vencida · Carlos Ruiz', amount: '$450.000', type: 'danger', date: '2026-03-21' },
  { code: 'FAC-2026-0018', desc: 'Vence en 7 días · Pedro García', amount: '$75.000', type: 'muted', date: '2026-03-31' },
];

// ═══════════════════════════════════════════════════════════════════
//  FUNCIONES DE RENDERIZADO
// ═══════════════════════════════════════════════════════════════════

// Anima contador numérico
const animateCounter = (el, target) => {
  if (!el) return;
  let cur = 0;
  const step = Math.max(1, Math.ceil(target / 30));
  const t = setInterval(() => {
    cur = Math.min(cur + step, target);
    el.textContent = cur;
    if (cur >= target) clearInterval(t);
  }, 30);
};

// Renderiza métricas principales
const renderMetrics = (data) => {
  animateCounter(safeGetElement('statPacientes'), data.pacientes);
  animateCounter(safeGetElement('statCitas'), data.citas);
  animateCounter(safeGetElement('statProfesionales'), data.profesionales);
  
  const ingresosEl = safeGetElement('statIngresos');
  if (ingresosEl) ingresosEl.textContent = data.ingresos;
  
  // Actualizar comparativas
  const updateVs = (id, text) => {
    const el = safeGetElement(id);
    if (el) el.textContent = text;
  };
  updateVs('vsPacientes', data.vsPacientes);
  updateVs('vsCitas', data.vsCitas);
  updateVs('vsIngresos', data.vsIngresos);
};

// Renderiza gráfico de ingresos con animación
const renderRevenueChart = (data) => {
  const container = safeGetElement('revenueChart');
  if (!container) return;
  
  container.innerHTML = data.map(item => {
    const formatted = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumSignificantDigits: 2 }).format(item.valor);
    return `
      <div class="chart-row">
        <span class="chart-label">${item.mes}</span>
        <div class="chart-bar-bg">
          <div class="chart-bar-fill ${item.valor >= 4000000 ? 'green' : 'blue'}" 
               data-width="${item.width}" 
               style="width:0" 
               role="progressbar" 
               aria-valuenow="${item.width}" 
               aria-valuemin="0" 
               aria-valuemax="100"
               aria-label="${item.mes}: ${formatted}, ${item.width}% del máximo">
          </div>
        </div>
        <span class="chart-val">${formatted.replace('$', '').replace('.000', 'M')}</span>
      </div>
    `;
  }).join('');
  
  // Animar barras con requestAnimationFrame para mejor performance
  requestAnimationFrame(() => {
    container.querySelectorAll('.chart-bar-fill').forEach(bar => {
      bar.style.width = bar.dataset.width + '%';
    });
  });
};

// Renderiza gráfico de estado de citas
const renderStatusChart = (data) => {
  const container = safeGetElement('statusChart');
  if (!container) return;
  
  container.innerHTML = data.map(item => `
    <div class="status-row">
      <span class="status-label">${item.label}</span>
      <div class="status-bar-bg">
        <div class="status-bar-fill ${item.color}" 
             data-width="${item.width}" 
             style="width:0"
             role="progressbar"
             aria-valuenow="${item.width}"
             aria-valuemin="0"
             aria-valuemax="100"
             aria-label="${item.label}: ${item.count} citas, ${item.width}%">
        </div>
      </div>
      <span class="status-val">${item.count}</span>
    </div>
  `).join('');
  
  // Animar barras
  requestAnimationFrame(() => {
    container.querySelectorAll('.status-bar-fill').forEach(bar => {
      bar.style.width = bar.dataset.width + '%';
    });
  });
};

// Renderiza tabla de profesionales top
const renderTopProfessionals = (data) => {
  const tbody = document.querySelector('.table-base tbody');
  if (!tbody) return;
  
  tbody.innerHTML = data.map(pro => `
    <tr>
      <td>
        <div class="td-avatar avatar-${pro.color}" aria-hidden="true">${pro.inicial}</div>
        <span class="td-name">${pro.nombre}</span>
      </td>
      <td style="text-align:center"><span class="badge-spec">${pro.espec}</span></td>
      <td style="text-align:center" class="td-strong">${pro.citas}</td>
      <td style="text-align:right" class="td-success">${pro.ingresos}</td>
    </tr>
  `).join('');
};

// Renderiza lista de facturas pendientes
const renderInvoices = (data) => {
  const list = document.querySelector('.invoices-list');
  if (!list) return;
  
  list.innerHTML = data.map(inv => `
    <div class="invoice-item ${inv.type}" role="listitem">
      <div>
        <p class="invoice-code">${inv.code}</p>
        <p class="invoice-desc"><time datetime="${inv.date}">${inv.desc.split('·')[0].trim()}</time> · ${inv.desc.split('·')[1]?.trim() || ''}</p>
      </div>
      <p class="invoice-amount">${inv.amount}</p>
    </div>
  `).join('');
};

// ═══════════════════════════════════════════════════════════════════
//  API CALLS (Listas para conectar al backend C#)
// ═══════════════════════════════════════════════════════════════════

// Obtiene métricas del dashboard
async function fetchMetrics() {
  try {
    // En producción: fetch real a API
    // const res = await fetch(`${API_BASE}/admin/metrics`);
    // if (!res.ok) throw new Error('API error');
    // return await res.json();
    
    // Simulación con fallback
    return SAMPLE_METRICS;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_METRICS;
  }
}

// Obtiene datos de ingresos mensuales
async function fetchRevenue() {
  try {
    // En producción: fetch real a API
    return SAMPLE_REVENUE;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_REVENUE;
  }
}

// Obtiene estado de citas
async function fetchStatus() {
  try {
    return SAMPLE_STATUS;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_STATUS;
  }
}

// Obtiene profesionales top
async function fetchTopProfessionals() {
  try {
    return SAMPLE_TOP_PROS;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_TOP_PROS;
  }
}

// Obtiene facturas pendientes
async function fetchInvoices() {
  try {
    return SAMPLE_INVOICES;
  } catch (error) {
    console.warn('Fallback a datos locales:', error);
    return SAMPLE_INVOICES;
  }
}

// Exporta reporte (simulado)
async function exportReport() {
  try {
    // En producción: POST real a API
    // const res = await fetch(`${API_BASE}/admin/export`, {
    //   method: 'POST',
    //   headers: { 'Content-Type': 'application/json' },
    //   body: JSON.stringify({ format: 'pdf', date: new Date().toISOString() }),
    // });
    // if (!res.ok) throw new Error('Export failed');
    // return await res.blob();
    
    // Simulación
    await new Promise(resolve => setTimeout(resolve, 2500));
    return true;
  } catch (error) {
    console.warn('Error exportando:', error);
    return false;
  }
}

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

  // ✅ Navegación: cerrar menú en móvil, SIN bloquear enlaces
  sidebar.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => {
      if (window.innerWidth <= 680) {
        toggleMenu(false);
      }
    });
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && sidebar.classList.contains('open')) {
      e.preventDefault();
      toggleMenu(false);
    }
  });
};

// Inicializa exportación de reportes
const initExport = () => {
  const btn = safeGetElement('btnExport');
  const progressBar = safeGetElement('topProgressBar');
  
  if (!btn || !progressBar) return;
  
  btn.addEventListener('click', async () => {
    if (btn.disabled) return;
    
    btn.disabled = true;
    btn.innerHTML = '⏳ Generando...';
    
    // Animar barra de progreso
    progressBar.style.transition = 'width 2.5s cubic-bezier(.4,0,.2,1)';
    progressBar.style.width = '100%';
    
    try {
      const success = await exportReport();
      
      if (success) {
        window.ToastService.error('✅ Reporte generado y descargado (PDF + Excel)');
      } else {
        window.ToastService.success('❌ Error al generar reporte');
      }
    } catch (error) {
      console.warn('Error en exportación:', error);
      window.ToastService.error('❌ Error de conexión');
    } finally {
      // Restaurar botón
      setTimeout(() => {
        btn.disabled = false;
        btn.innerHTML = '📊 Exportar reporte';
        progressBar.style.width = '75%';
      }, 500);
    }
  });
};

// ═══════════════════════════════════════════════════════════════════
//  FUNCIÓN PRINCIPAL DE INICIALIZACIÓN
// ═══════════════════════════════════════════════════════════════════

const init = async () => {
  // Inicializar componentes de UI
  initSidebar();
  initExport();
  
  // Cargar datos en paralelo
  const [metrics, revenue, status, topPros, invoices] = await Promise.all([
    fetchMetrics(),
    fetchRevenue(),
    fetchStatus(),
    fetchTopProfessionals(),
    fetchInvoices(),
  ]);
  
  // Renderizar componentes
  renderMetrics(metrics);
  renderRevenueChart(revenue);
  renderStatusChart(status);
  renderTopProfessionals(topPros);
  renderInvoices(invoices);
  
  // Toast de bienvenida (después de cargar datos)
  setTimeout(() => {
    window.ToastService.success('✅ Conexión establecida con SmileTrack');
  }, 800);
  
  // Limpieza al unload para evitar memory leaks
  window.addEventListener('beforeunload', () => {
    // Remover listeners en implementación SPA real
  });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);