/**
 *  — Odontograma 3D de solo lectura para Historia Clínica.
 *
 * Motivo: st-odo-03 mostraba un canvas 2D distinto al odontograma clínico.
 * Este componente reutiliza el modelo 3D y el mapeo FDI ya persistido, pero no
 * registra clics clínicos, no muestra acciones de mapeo y nunca hace POST.
 */
(function () {
  'use strict';

  const MODELO_ADULTO = '7f5b381c66674e0a969e8db04d139666';
  const VERSION_SKETCHFAB = '1.12.1';
  const NOMBRES_FDI = {
    '11':'Incisivo central superior derecho','12':'Incisivo lateral superior derecho','13':'Canino superior derecho','14':'Primer premolar superior derecho','15':'Segundo premolar superior derecho','16':'Primer molar superior derecho','17':'Segundo molar superior derecho','18':'Tercer molar superior derecho',
    '21':'Incisivo central superior izquierdo','22':'Incisivo lateral superior izquierdo','23':'Canino superior izquierdo','24':'Primer premolar superior izquierdo','25':'Segundo premolar superior izquierdo','26':'Primer molar superior izquierdo','27':'Segundo molar superior izquierdo','28':'Tercer molar superior izquierdo',
    '31':'Incisivo central inferior izquierdo','32':'Incisivo lateral inferior izquierdo','33':'Canino inferior izquierdo','34':'Primer premolar inferior izquierdo','35':'Segundo premolar inferior izquierdo','36':'Primer molar inferior izquierdo','37':'Segundo molar inferior izquierdo','38':'Tercer molar inferior izquierdo',
    '41':'Incisivo central inferior derecho','42':'Incisivo lateral inferior derecho','43':'Canino inferior derecho','44':'Primer premolar inferior derecho','45':'Segundo premolar inferior derecho','46':'Primer molar inferior derecho','47':'Segundo molar inferior derecho','48':'Tercer molar inferior derecho'
  };
  const cacheInfoDiente = new Map();

  // CORRECCIÓN FASE 2 — Carga la API de Sketchfab de manera segura y espera a
  // que esté disponible. Antes se asumía que el <script> externo ya había
  // terminado de cargar; si la red era lenta, la vista quedaba vacía o solo
  // mostraba el estado de carga. Esta promesa permite inicializar el visor
  // independientemente del orden o la velocidad de descarga de la biblioteca.
  const obtenerApiSketchfab = () => new Promise((resolve, reject) => {
    if (window.Sketchfab) {
      resolve(window.Sketchfab);
      return;
    }

    const idScript = 'sketchfab-viewer-api-historial';
    let script = document.getElementById(idScript);
    const finalizar = () => window.Sketchfab
      ? resolve(window.Sketchfab)
      : reject(new Error('La API de Sketchfab no se expuso en window.'));

    if (script) {
      script.addEventListener('load', finalizar, { once: true });
      script.addEventListener('error', () => reject(new Error('No se pudo cargar Sketchfab.')), { once: true });
      return;
    }

    script = document.createElement('script');
    script.id = idScript;
    script.src = `https://static.sketchfab.com/api/sketchfab-viewer-${VERSION_SKETCHFAB}.js`;
    script.async = true;
    script.onload = finalizar;
    script.onerror = () => reject(new Error('No se pudo descargar Sketchfab.'));
    document.head.appendChild(script);
  });

  const escaparHtml = valor => String(valor ?? '').replace(/[&<>'"]/g, char => ({
    '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#039;', '"':'&quot;'
  }[char]));

  //  — Lee solo el estado serializado por el odontograma editable. No se
  // consulta localStorage, para que esta vista sea consistente entre equipos.
  const leerEstadoOdontograma = estadoRaw => {
    try {
      const estado = typeof estadoRaw === 'string' ? JSON.parse(estadoRaw) : estadoRaw;
      return {
        registros: estado?.registros && typeof estado.registros === 'object' ? estado.registros : {},
        mapeoFDI: estado?.mapeoFDI && typeof estado.mapeoFDI === 'object' ? estado.mapeoFDI : {}
      };
    } catch {
      return { registros: {}, mapeoFDI: {} };
    }
  };

  const obtenerUltimoTratamiento = (registros, instanceID) => {
    const tratamientos = registros?.[instanceID]?.tratamientos;
    return Array.isArray(tratamientos) && tratamientos.length ? tratamientos[tratamientos.length - 1] : null;
  };

  const ocultarTooltip = () => {
    const tooltip = document.getElementById('holo-tooltip-historial');
    if (tooltip) tooltip.style.display = 'none';
  };

  const colocarTooltip = (tooltip, iframe) => {
    const visor = iframe.getBoundingClientRect();
    tooltip.style.display = 'block';
    const medida = tooltip.getBoundingClientRect();
    const margen = 12;
    tooltip.style.left = `${Math.max(margen, visor.right - medida.width - margen)}px`;
    tooltip.style.top = `${Math.max(margen, visor.top + margen)}px`;
  };

  //  — Consulta trazabilidad real de Registro_Odontograma para completar
  // el tooltip con profesional y fecha. Se cachea por paciente+FDI durante la vista.
  const cargarInfoDiente = async (pacienteId, fdi) => {
    const llave = `${pacienteId}_${fdi}`;
    if (cacheInfoDiente.has(llave)) return cacheInfoDiente.get(llave);
    const respuesta = await fetch(`/historia-clinica/st-odo-04-odontograma/diente-info?pacienteId=${encodeURIComponent(pacienteId)}&numerofdi=${encodeURIComponent(fdi)}`);
    if (!respuesta.ok) throw new Error('No se pudo consultar el historial del diente.');
    const datos = await respuesta.json();
    cacheInfoDiente.set(llave, datos);
    return datos;
  };

  const renderDetalleReal = datos => {
    const registros = Array.isArray(datos?.registros) ? datos.registros : [];
    if (!registros.length) return '<div class="odo3d-tooltip-empty">Sin registros clínicos adicionales.</div>';
    return registros.slice(0, 4).map(registro => `
      <div class="odo3d-tooltip-item">
        <strong>${escaparHtml(registro.estado)}</strong>
        ${registro.observacion ? `<span>${escaparHtml(registro.observacion)}</span>` : ''}
        <small>${escaparHtml(registro.profesional)} · ${escaparHtml(registro.fecha)}</small>
      </div>`).join('');
  };

  window.inicializarOdontograma3DReadonly = function (datosHistoria) {
    const host = document.getElementById('odontogramaHost');
    if (!host) return;

    // CORRECCIÓN FASE 2 — Impide una segunda inicialización sobre el mismo
    // iframe. El componente se inicia de forma autónoma y gestión-historial.js
    // también puede invocarlo; sin esta marca se duplicaban visores/eventos.
    if (host.dataset.odontograma3dInicializado === 'true') return;
    host.dataset.odontograma3dInicializado = 'true';

    // FASE 2 — Reemplaza solamente el contenido 2D del host; el resto de la
    // pantalla (línea de tiempo y formulario de notas) conserva su funcionalidad.
    host.innerHTML = `
      <div class="odo3d-readonly-wrap">
        <div class="odo3d-readonly-label">Vista 3D de solo lectura</div>
        <div class="odo3d-readonly-loading" id="odo3dReadonlyLoading">Cargando odontograma 3D…</div>
        <div class="odo3d-readonly-error" id="odo3dReadonlyError" hidden>No fue posible cargar el modelo 3D.</div>
        <iframe id="odo3dReadonlyFrame" title="Odontograma 3D de solo lectura" allow="autoplay; fullscreen; xr-spatial-tracking"></iframe>
      </div>`;

    const iframe = document.getElementById('odo3dReadonlyFrame');
    const loading = document.getElementById('odo3dReadonlyLoading');
    const error = document.getElementById('odo3dReadonlyError');
    const tooltip = document.getElementById('holo-tooltip-historial');
    const { registros, mapeoFDI } = leerEstadoOdontograma(datosHistoria?.estadoPersistido);
    const pacienteId = datosHistoria?.pacienteId;

    const mostrarError = mensaje => {
      loading.hidden = true;
      error.hidden = false;
      error.textContent = mensaje;
    };

    // CORRECCIÓN FASE 2 — La creación del cliente se hace solo cuando la API
    // está lista y se captura cualquier fallo para reemplazar una zona vacía
    // por un mensaje visible y verificable para el profesional.
    obtenerApiSketchfab().then(SketchfabApi => {
      const client = new SketchfabApi(VERSION_SKETCHFAB, iframe);
      client.init(MODELO_ADULTO, {
        autostart: 1,
        ui_infos: 0, ui_watermark: 0, ui_help: 0, ui_settings: 0,
        ui_vr: 0, ui_fullscreen: 0, ui_annotations: 0, ui_stop: 0,
        success(api) {
          api.start();
          api.addEventListener('viewerready', () => {
            loading.hidden = true;

          //  — Solo hover. No se registra evento click, asignación FDI,
          // guardado, panel diagnóstico ni mutación de datos desde esta pantalla.
          api.addEventListener('nodeMouseEnter', node => {
            const instanceID = node?.instanceID;
            const fdi = instanceID == null ? null : mapeoFDI[instanceID];
            if (!tooltip || !fdi) return;

            const ultimo = obtenerUltimoTratamiento(registros, instanceID);
            tooltip.innerHTML = `
              <div class="odo3d-tooltip-title">${escaparHtml(fdi)} · ${escaparHtml(NOMBRES_FDI[fdi] || 'Pieza dental')}</div>
              <div class="odo3d-tooltip-state">${ultimo ? escaparHtml(ultimo.key || 'Registrado') : 'Sin tratamiento local'}</div>
              ${ultimo?.obs ? `<div class="odo3d-tooltip-observation">${escaparHtml(ultimo.obs)}</div>` : ''}
              <div class="odo3d-tooltip-loading">Consultando historial…</div>`;
            colocarTooltip(tooltip, iframe);

            if (!pacienteId) return;
            cargarInfoDiente(pacienteId, fdi).then(datos => {
              if (tooltip.style.display === 'none') return;
              const cargando = tooltip.querySelector('.odo3d-tooltip-loading');
              if (cargando) cargando.outerHTML = `<div class="odo3d-tooltip-history">${renderDetalleReal(datos)}</div>`;
            }).catch(() => {
              const cargando = tooltip.querySelector('.odo3d-tooltip-loading');
              if (cargando) cargando.textContent = 'No se pudo cargar el historial adicional.';
            });
          }, { pick: 'fast' });

            api.addEventListener('nodeMouseLeave', ocultarTooltip, { pick: 'fast' });
            iframe.addEventListener('mouseleave', ocultarTooltip);
          });
        },
        error() {
          mostrarError('No fue posible cargar el modelo 3D de Sketchfab.');
        }
      });
    }).catch(() => mostrarError('No se pudo cargar la biblioteca del visor 3D.'));
  };

  // CORRECCIÓN FASE 2 — Inicia el visor al estar disponible el DOM, sin
  // depender de que el resto de gestion-historial.js termine correctamente.
  // Así una falla no relacionada (por ejemplo, en la línea de tiempo) no
  // impide que el odontograma de solo lectura sea visible.
  const iniciarAutomaticamente = () => {
    window.inicializarOdontograma3DReadonly(window.smiletrackHistoriaData || {});
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciarAutomaticamente, { once: true });
  } else {
    iniciarAutomaticamente();
  }
})();
