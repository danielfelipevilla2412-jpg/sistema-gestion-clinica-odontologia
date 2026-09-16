/* ============================================
SmileTrack — Citas Finalizadas (st-aux-10-citas-finalizadas)
============================================
Autor: Johan Santamaria
Fecha: 29/07/2026

DESCRIPCIÓN:
Gestiona el renderizado dinámico de la tabla de citas finalizadas, el cálculo de métricas por estado, la animación de contadores y la exportación del resumen diario a CSV sin depender del servidor.

FUNCIONALIDADES PRINCIPALES:
- Renderizado dinámico de filas de tabla desde LocalStorage con badges de estado accesibles
- Exportación a CSV generada en el cliente (Blob + createObjectURL) sin petición al servidor
- Animación de conteo progresivo en tarjetas de métricas para reflejar el resumen visual de la jornada
- Clave de LocalStorage con fecha para evitar colisiones entre jornadas en el mismo dispositivo

DEPENDENCIAS TÉCNICAS:
- Controller: GestionCitasController y Stadm09Citas
- CSS: ~/css/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.css
- JS: ~/js/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.js
- Partial / Otros: citas-finalizadas.cshtml

NOTAS DE MANTENIMIENTO:
- Los comentarios internos explican el "por qué" de las decisiones de diseño/negocio, no el "qué" hace el código básico.
- La exportación usa Blob en lugar de llamar al servidor para no requerir autorización adicional y funcionar offline.
============================================ */

// WHY: safeGetElement previene excepciones fatales en la inicialización si un elemento no existe en el DOM
const safeGetElement = (id) => {
  if (window.CommonUtils?.safeGetElement) {
    return window.CommonUtils.safeGetElement(id);
  }
  const el = document.getElementById(id);
  if (!el) console.warn(`[SmileTrack] Elemento no encontrado: #${id}`);
  return el;
};

// WHY: Debounce protege contra eventos de input repetitivos que podrían generar exportaciones o escrituras redundantes
const debounce = (fn, delay) => {
  if (window.CommonUtils?.debounce) {
    return window.CommonUtils.debounce(fn, delay);
  }
  let timeoutId;
  return (...args) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn.apply(this, args), delay);
  };
};

// WHY: Las notificaciones no bloqueantes informan el resultado de la exportación sin interrumpir la revisión del resumen

// WHY: Envuelve los datos reales inyectados por el servidor (window.smiletrackCitasFinalizadasData,
// ver ConstruirCitasFinalizadasAsync en GestionCitasController.cs) en la misma interfaz que
// usaba el almacenamiento local, para no reescribir el resto del archivo.
const finalizedStorage = {
  load: () => (window.smiletrackCitasFinalizadasData?.citas) || [],

  // WHY: Centraliza el cálculo de contadores para no duplicar la lógica de filtrado entre la tabla y las tarjetas de resumen
  getCounts: (citas) => {
    return {
      atendidas: citas.filter(c => c.estado === 'Atendida').length,
      canceladas: citas.filter(c => c.estado === 'Cancelada').length,
      noAsistio: citas.filter(c => c.estado === 'No asistió').length
    };
  }
};

// WHY: Formatea la fecha en español para que el CSV exportado sea legible sin conversión manual por parte del auxiliar
const formatDateForExport = () => {
  const now = new Date();
  const months = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];
  return `${now.getDate()} de ${months[now.getMonth()]} ${now.getFullYear()}`;
};

// Inicializa menú móvil (delegado al módulo centralizado)
const initMobileMenu = () => {
  // El menú móvil, overlay y acordeón del sidebar son gestionados centralizadamente por ~/js/shared/sidebar.js
};

// WHY: Renderiza la tabla dinámicamente para poder asignar clases y aria-labels de estado sin lógica duplicada en Razor
const renderAppointments = (data) => {
  const tbody = safeGetElement('appointmentsBody');
  if (!tbody) return;

  if (data.length === 0) {
    const row = document.createElement('tr');
    row.className = 'empty-state-row';
    row.setAttribute('role', 'row');
    row.setAttribute('aria-label', 'Estado vacío');
    const cell = document.createElement('td');
    cell.colSpan = 5;
    cell.className = 'empty-state-cell';
    const content = document.createElement('div');
    content.className = 'empty-state-content';
    content.setAttribute('role', 'status');
    content.setAttribute('aria-live', 'polite');
    const icon = document.createElement('div');
    icon.className = 'empty-state-icon';
    icon.setAttribute('aria-hidden', 'true');
    icon.textContent = '📅';
    const message = document.createElement('p');
    message.className = 'empty-state-message';
    message.textContent = 'No hay citas finalizadas registradas.';
    content.append(icon, message);
    cell.appendChild(content);
    row.appendChild(cell);
    tbody.replaceChildren(row);
    return;
  }

  // WHY: Mapea el estado de la cita a una clase CSS semántica para que el color refleje el resultado clínico del turno
  tbody.replaceChildren(...data.map(apt => {
    const statusClass = apt.estado === 'Atendida' ? 'atendida' : 
                       apt.estado === 'Cancelada' ? 'cancelada' : 'no-asistio';
    const row = document.createElement('tr');
    row.setAttribute('role', 'row');
    const cell = (className, value) => {
      const element = document.createElement('td');
      element.className = className;
      element.textContent = value || '';
      return element;
    };
    const statusCell = document.createElement('td');
    const badge = document.createElement('span');
    badge.className = `status-badge ${statusClass}`;
    badge.setAttribute('role', 'status');
    badge.setAttribute('aria-label', `Estado: ${apt.estado}`);
    badge.textContent = apt.estado || 'Sin estado';
    statusCell.appendChild(badge);
    row.append(cell('td-hora', apt.hora), cell('td-paciente', apt.paciente),
      cell('td-profesional', apt.profesional), cell('td-servicio', apt.servicio), statusCell);
    return row;
  }));
};

const csvValue = (value) => {
  const text = String(value ?? '');
  return `"${text.replaceAll('"', '""')}"`;
};

// WHY: La animación de conteo progresivo hace que el auxiliar note el cambio sin leer texto, facilitando el scan rápido
const summaryIntervals = new WeakMap();

const updateSummary = () => {
  const citas = finalizedStorage.load();
  const counts = finalizedStorage.getCounts(citas);
  
  const els = {
    atendidas: safeGetElement('countAtendidas'),
    canceladas: safeGetElement('countCanceladas'),
    noAsistio: safeGetElement('countNoAsistio')
  };
  
  Object.entries(els).forEach(([key, el]) => {
    if (el) {
      if (summaryIntervals.has(el)) {
        clearInterval(summaryIntervals.get(el));
      }
      const target = counts[key] ?? 0;
      const current = parseInt(el.textContent) || 0;
      
      if (current !== target) {
        let step = current;
        const increment = target > current ? 1 : -1;
        const interval = setInterval(() => {
          step += increment;
          el.textContent = step;
          if ((increment > 0 && step >= target) || (increment < 0 && step <= target)) {
            el.textContent = target;
            clearInterval(interval);
            summaryIntervals.delete(el);
          }
        }, 30);
        summaryIntervals.set(el, interval);
      }
    }
  });
};

// WHY: El CSV se genera en el cliente con Blob para que la exportación funcione sin autenticación adicional ni latencia de red
const initExportButton = () => {
  const btn = safeGetElement('btnExport');
  if (!btn) return;
  
  btn.addEventListener('click', async () => {
    const original = btn.innerHTML;
    btn.innerHTML = '<span class="material-symbols-outlined action-icon" aria-hidden="true">hourglass_top</span><span class="btn-text">Generando...</span>';
    btn.disabled = true;
    
    try {
      // Carga citas desde storage
      const citas = finalizedStorage.load();
      const counts = finalizedStorage.getCounts(citas);
      
      // Genera contenido CSV
      const csvContent = [
        'Fecha de exportación,' + formatDateForExport(),
        '',
        'RESUMEN',
        'Estado,Cantidad',
        `Atendida,${counts.atendidas}`,
        `Cancelada,${counts.canceladas}`,
        `No asistió,${counts.noAsistio}`,
        `Total,${citas.length}`,
        '',
        'DETALLE DE CITAS',
        'Hora,Paciente,Profesional,Servicio,Estado',
        ...citas.map(c => [c.hora, c.paciente, c.profesional, c.servicio, c.estado].map(csvValue).join(','))
      ].join('\n');
      
      // Crea blob y descarga
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `smiletrack_citas_finalizadas_${new Date().toISOString().split('T')[0]}.csv`;
      link.style.display = 'none';
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
      
      // Feedback visual
      btn.innerHTML = '<span class="material-symbols-outlined action-icon" aria-hidden="true">check_circle</span><span class="btn-text">Descargado</span>';
      btn.style.background = '#dcfce7';
      btn.style.borderColor = '#22c55e';
      btn.style.color = '#166534';
      if (window.ToastService) {
        window.ToastService.success('Resumen descargado exitosamente');
      }
      
      // Restaura botón
      setTimeout(() => {
        btn.innerHTML = original;
        btn.disabled = false;
        btn.style.background = '';
        btn.style.borderColor = '';
        btn.style.color = '';
      }, 2000);
      
    } catch (error) {
      console.error('Error al exportar:', error);
      btn.innerHTML = original;
      btn.disabled = false;
      window.ToastService.error('Error al generar resumen');
    }
  });
};

// Función principal de inicialización
const init = () => {
    // Inicializar componentes de UI
    initMobileMenu();
    initExportButton();
    
    // Cargar y renderizar datos
    const citas = finalizedStorage.load();
    renderAppointments(citas);
    updateSummary();
    
    // Limpieza de listeners al unload para evitar memory leaks
    window.addEventListener('beforeunload', () => {
      // Remover listeners en implementación SPA real
    });
};

// Ejecutar al cargar DOM
document.addEventListener('DOMContentLoaded', init);