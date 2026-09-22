/* ================================================================
 * SmileTrack — CommonUtils (shared/common.js)
 * ================================================================
 * FASE-1: Capa compartida de utilidades para eliminar el ~70% de
 * código duplicado que existía entre las 19 vistas de los módulos
 * de Citas y Profesionales. Todas las funciones aquí expuestas son
 * la "versión canónica" más robusta seleccionada después de
 * inventariar cada implementación repetida en el código base.
 *
 * Namespace único: window.CommonUtils (evita colisiones en BOM).
 * ================================================================ */

(function (global) {
    'use strict';

    // ════════════════════════════════════════════════════════════════
    // 1. CONFIGURACIÓN GLOBAL COMPARTIDA
    // ════════════════════════════════════════════════════════════════
    const DEFAULT_API_BASE = global.APP_CONFIG?.ApiBase || '/api';
    const DEFAULT_PAGE_SIZE = 10;
    const MONTHS_ES_CO = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    const MONTHS_LONG_ES_CO = ['enero','febrero','marzo','abril','mayo','junio','julio','agosto','septiembre','octubre','noviembre','diciembre'];

    // ════════════════════════════════════════════════════════════════
    // 2. SELECTORES SEGUROS
    // ════════════════════════════════════════════════════════════════

    /**
     * Selecciona un elemento por ID y loguea advertencia si no existe.
     * @param {string} id
     * @param {ParentNode} [parent=document]
     * @returns {HTMLElement|null}
     */
    function safeGetElement(id, parent) {
        const root = parent || document;
        const el = root.getElementById ? root.getElementById(id) : root.querySelector(`#${id}`);
        if (!el) {
            console.warn(`[SmileTrack][CommonUtils] Elemento no encontrado: #${id}`);
        }
        return el;
    }

    /**
     * querySelector con fallback null-safe y warning.
     * @param {string} selector
     * @param {ParentNode} [parent=document]
     */
    function safeQuerySelector(selector, parent) {
        const root = parent || document;
        const el = root.querySelector(selector);
        if (!el) {
            console.warn(`[SmileTrack][CommonUtils] Selector sin coincidencia: "${selector}"`);
        }
        return el;
    }

    // ════════════════════════════════════════════════════════════════
    // 3. RENDIMIENTO: debounce + throttle
    // ════════════════════════════════════════════════════════════════

    /**
     * Debounce: retrasa fn hasta que pase `delay` ms sin nuevas llamadas.
     * @template {Function} F
     * @param {F} fn
     * @param {number} delay ms
     * @returns {F & { cancel: () => void }}
     */
    function debounce(fn, delay) {
        let timeoutId;
        const debounced = function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => fn.apply(this, args), delay);
        };
        debounced.cancel = () => clearTimeout(timeoutId);
        return debounced;
    }

    /**
     * Throttle: ejecuta fn como máximo 1 vez cada `delay` ms.
     * @template {Function} F
     * @param {F} fn
     * @param {number} delay ms
     * @returns {F & { cancel: () => void }}
     */
    function throttle(fn, delay) {
        let last = 0;
        let timeoutId;
        const throttled = function (...args) {
            const now = Date.now();
            const remaining = delay - (now - last);
            if (remaining <= 0) {
                clearTimeout(timeoutId);
                last = now;
                fn.apply(this, args);
            } else if (!timeoutId) {
                timeoutId = setTimeout(() => {
                    last = Date.now();
                    timeoutId = undefined;
                    fn.apply(this, args);
                }, remaining);
            }
        };
        throttled.cancel = () => { clearTimeout(timeoutId); timeoutId = undefined; };
        return throttled;
    }

    // ════════════════════════════════════════════════════════════════
    // 4. AUTENTICACIÓN + CSRF + API WRAPPER
    // ════════════════════════════════════════════════════════════════

    /**
     * Obtiene CSRF token desde input hidden Razor o cookie XSRF-TOKEN.
     * @returns {string|undefined}
     */
    function getCsrfToken() {
        const inputToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (inputToken) return inputToken;
        try {
            const cookie = document.cookie.split('; ').find(r => r.startsWith('XSRF-TOKEN='));
            if (cookie) return decodeURIComponent(cookie.split('=')[1]);
        } catch (_) { /* navegación privada / cookies deshabilitadas */ }
        return undefined;
    }

    /**
     * Headers estándar para API requests: JSON + CSRF + JWT Bearer (si existe).
     * Auth dual: JWT en sessionStorage o Cookie ASP.NET Identity (credentials: include).
     */
    function getAuthHeaders(extra) {
        const headers = Object.assign({
            'Content-Type': 'application/json',
            'Accept': 'application/json'
        }, extra || {});
        const csrf = getCsrfToken();
        if (csrf) headers['X-CSRF-TOKEN'] = csrf;
        try {
            const jwt = sessionStorage.getItem('st_jwt');
            if (jwt) headers['Authorization'] = `Bearer ${jwt}`;
        } catch (_) { /* sessionStorage deshabilitado */ }
        return headers;
    }

    /**
     * Construye query string segura (encoding UTF-8) desde objeto plano.
     * Elimina automáticamente keys con valor undefined/null/''.
     * @param {Record<string, any>} params
     * @returns {string} query-string sin leading `?`
     */
    function buildQuery(params) {
        if (!params) return '';
        const usp = new URLSearchParams();
        Object.entries(params).forEach(([k, v]) => {
            if (v === undefined || v === null || v === '') return;
            if (Array.isArray(v)) {
                v.forEach(item => usp.append(k, String(item)));
            } else {
                usp.append(k, String(v));
            }
        });
        return usp.toString();
    }

    /**
     * Fetch unificado con manejo de errores estándar + toast automático.
     * Normaliza respuesta JSON (formato ApiResponse del backend).
     *
     * @param {string} endpoint (relativo a DEFAULT_API_BASE o absoluto)
     * @param {Object} [opts]
     * @param {'GET'|'POST'|'PUT'|'PATCH'|'DELETE'} [opts.method]
     * @param {any} [opts.body] Objeto serializable a JSON
     * @param {Record<string, string>} [opts.headers]
     * @param {Record<string, any>} [opts.params] query string (será encodeado)
     * @param {RequestCredentials} [opts.credentials='same-origin']
     * @param {boolean} [opts.silent=false] Si true NO muestra toast de error
     * @param {boolean} [opts.raw=false] Si true retorna Response raw (no parse JSON)
     * @returns {Promise<any>}
     */
    async function fetchWithAuth(endpoint, opts) {
        opts = opts || {};
        const method = (opts.method || 'GET').toUpperCase();
        const creds = opts.credentials || (global.location?.protocol === 'https:' ? 'include' : 'same-origin');

        let url = endpoint.startsWith('http') ? endpoint : `${DEFAULT_API_BASE}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`;
        if (opts.params) {
            const qs = buildQuery(opts.params);
            if (qs) url += `${url.includes('?') ? '&' : '?'}${qs}`;
        }

        const reqInit = {
            method,
            credentials: creds,
            headers: getAuthHeaders(opts.headers || {})
        };

        if (['POST', 'PUT', 'PATCH'].includes(method) && opts.body !== undefined) {
            // Si body NO es JSON string, serializar; de lo contrario usar body directo
            if (opts.body instanceof FormData) {
                delete reqInit.headers['Content-Type']; // browser set boundary
                reqInit.body = opts.body;
            } else if (typeof opts.body === 'string') {
                reqInit.body = opts.body;
            } else {
                reqInit.body = JSON.stringify(opts.body);
            }
        }

        let resp;
        try {
            resp = await fetch(url, reqInit);
        } catch (netErr) {
            const msg = 'Sin conexión al servidor. Verifica tu red y vuelve a intentarlo.';
            if (!opts.silent && global.ToastService) {
                try { global.ToastService.error('❌ Error de conexión', msg); } catch (_) { /* sin toast service cargado aún */ }
            }
            const err = new Error(msg);
            err.cause = netErr;
            err.networkError = true;
            throw err;
        }

        if (opts.raw) return resp;

        // 401 no autorizado: redirigir a login (razonable para flujo SPA-lite)
        if (resp.status === 401) {
            if (!opts.silent && global.ToastService) {
                try { global.ToastService.warning('🔐 Sesión expirada', 'Redirigiendo a inicio de sesión...'); } catch (_) { }
            }
            setTimeout(() => { global.location.href = '/acceso-y-seguridad/login'; }, 800);
            const err = new Error('Sesión no autorizada (401)');
            err.status = 401;
            throw err;
        }

        let payload;
        const text = await resp.text();
        try {
            payload = text ? JSON.parse(text) : null;
        } catch (parseErr) {
            payload = { message: text };
        }

        if (!resp.ok) {
            const msg = (payload && (payload.message || payload.Message || payload.error || payload.title))
                || `Error ${resp.status} ${resp.statusText}`;
            if (!opts.silent && global.ToastService) {
                try { global.ToastService.error(`❌ ${resp.status >= 500 ? 'Error del servidor' : 'No fue posible completar la acción'}`, String(msg)); } catch (_) { }
            }
            const err = new Error(String(msg));
            err.status = resp.status;
            err.payload = payload;
            err.response = resp;
            throw err;
        }

        // Formato estándar backend: ApiResponse { success, message, data }
        if (payload && typeof payload === 'object' && 'success' in payload) {
            if (!payload.success) {
                const msg = payload.message || 'Operación no exitosa.';
                if (!opts.silent && global.ToastService) {
                    try { global.ToastService.warning('⚠️ Atención', msg); } catch (_) { }
                }
                const err = new Error(msg);
                err.status = resp.status;
                err.payload = payload;
                throw err;
            }
            // Retornar 'data' si existe, sino el payload completo
            return ('data' in payload) ? payload.data : payload;
        }

        return payload;
    }

    // ════════════════════════════════════════════════════════════════
    // 5. SEGURIDAD: escape HTML anti-XSS
    // ════════════════════════════════════════════════════════════════
    const _HTML_ESCAPE_MAP = {
        '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
    };
    /**
     * Escapa caracteres especiales HTML para prevenir XSS al usar innerHTML.
     * @param {any} value
     */
    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>'"]/g, c => _HTML_ESCAPE_MAP[c] || c);
    }

    // ════════════════════════════════════════════════════════════════
    // 6. FORMATEO FECHAS / HORAS (locale es-CO, sin depender de Intl completo)
    // ════════════════════════════════════════════════════════════════

    function _asDate(iso) {
        if (!iso) return null;
        const d = (iso instanceof Date) ? iso : new Date(iso);
        return Number.isNaN(d.getTime()) ? null : d;
    }

    /**
     * Formato corto: "05 Sep 2026"
     */
    function formatFechaLocal(iso) {
        const d = _asDate(iso);
        if (!d) return '';
        return `${String(d.getDate()).padStart(2,'0')} ${MONTHS_ES_CO[d.getMonth()]} ${d.getFullYear()}`;
    }

    /**
     * Formato fecha + hora: "05 Sep 2026 14:30"
     */
    function formatFechaHoraLocal(iso) {
        const d = _asDate(iso);
        if (!d) return '';
        return `${formatFechaLocal(d)} ${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
    }

    /**
     * Formato largo para reportes: "5 de septiembre de 2026"
     */
    function formatFechaLarga(iso) {
        const d = _asDate(iso);
        if (!d) return '';
        return `${d.getDate()} de ${MONTHS_LONG_ES_CO[d.getMonth()]} de ${d.getFullYear()}`;
    }

    /**
     * Formato de hora 24h: "14:30"
     */
    function formatHora(iso) {
        const d = _asDate(iso);
        if (!d) return '';
        return `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}`;
    }

    /**
     * Tiempo relativo soportando pasado y futuro ("Hace 5 min", "En 2 días").
     * Basado en la versión robusta de st-pac-03-notificaciones.
     */
    function formatTiempoRelativo(iso) {
        const d = _asDate(iso);
        if (!d) return '';
        const diffMs = Date.now() - d.getTime();
        const diffSeg = Math.round(diffMs / 1000);
        const absSeg = Math.abs(diffSeg);
        const futuro = diffSeg < 0;
        if (absSeg < 60) return futuro ? `En ${absSeg} seg` : 'Hace unos segundos';
        const min = Math.round(absSeg / 60);
        if (min < 60) return futuro ? `En ${min} min` : `Hace ${min} min`;
        const h = Math.round(min / 60);
        if (h < 24) return futuro ? `En ${h} h` : `Hace ${h} h`;
        const dias = Math.round(h / 24);
        if (dias < 30) return futuro ? `En ${dias} día${dias !== 1 ? 's' : ''}` : `Hace ${dias} día${dias !== 1 ? 's' : ''}`;
        // Más de 30 días → formato fecha corta
        return formatFechaLocal(d);
    }

    // ════════════════════════════════════════════════════════════════
    // 7. ANIMACIONES UI
    // ════════════════════════════════════════════════════════════════

    /**
     * Animar contador numérico con easing easeOutCubic (de st-pac-01-mis-citas).
     * @param {HTMLElement|null} el
     * @param {number} target
     * @param {Object} [opts]
     * @param {number} [opts.duration=900] ms
     * @param {boolean} [opts.integer=true]
     * @param {string} [opts.prefix='']
     * @param {string} [opts.suffix='']
     */
    function animateCounter(el, target, opts) {
        if (!el) return;
        opts = opts || {};
        const duration = opts.duration || 900;
        const isInt = opts.integer !== false;
        const prefix = opts.prefix || '';
        const suffix = opts.suffix || '';
        target = Number(target) || 0;

        const start = performance.now();
        const tick = (t) => {
            const p = Math.min((t - start) / duration, 1);
            const eased = 1 - Math.pow(1 - p, 3); // easeOutCubic
            const value = eased * target;
            el.textContent = `${prefix}${isInt ? Math.floor(value).toLocaleString('es-CO') : value.toFixed(1)}${suffix}`;
            if (p < 1) {
                requestAnimationFrame(tick);
            } else {
                el.textContent = `${prefix}${isInt ? Math.floor(target).toLocaleString('es-CO') : target.toFixed(1)}${suffix}`;
            }
        };
        requestAnimationFrame(tick);
    }

    /**
     * Animar todos los elementos [data-counter=""] que estén dentro del contenedor.
     * Espera IntersectionObserver si está disponible.
     * @param {ParentNode} [parent=document]
     */
    function animateCounters(parent) {
        const root = parent || document;
        const nodes = root.querySelectorAll('[data-counter]');
        if (!nodes.length) return;
        nodes.forEach(el => {
            const target = Number(el.getAttribute('data-counter') || el.textContent || 0);
            animateCounter(el, target, {
                duration: Number(el.getAttribute('data-counter-duration')) || 900,
                prefix: el.getAttribute('data-counter-prefix') || '',
                suffix: el.getAttribute('data-counter-suffix') || ''
            });
        });
    }

    // ════════════════════════════════════════════════════════════════
    // 8. SIDEBAR / MENÚ MÓVIL (versión canónica de st-rec-05 con focus trap)
    // ════════════════════════════════════════════════════════════════
    let _mobileMenuCleanup = null;

    function initMobileMenu() {
        const ham = safeGetElement('hamburger');
        const sb = safeGetElement('sidebar');
        const ov = safeGetElement('overlay');
        if (!ham || !sb || !ov) return;

        // Limpiar instalación previa (en navegación interna SPA-lite)
        if (typeof _mobileMenuCleanup === 'function') {
            try { _mobileMenuCleanup(); } catch (_) { }
            _mobileMenuCleanup = null;
        }

        const closeBtn = sb.querySelector('[data-sidebar-close]');
        let previousFocus = null;

        const openMenu = () => {
            previousFocus = document.activeElement;
            sb.classList.add('open');
            ov.classList.add('open');
            ham.setAttribute('aria-expanded', 'true');
            ov.setAttribute('aria-hidden', 'false');
            ham.setAttribute('aria-pressed', 'true');
            sb.dataset.previousFocus = previousFocus?.id || '';
            document.body.style.overflow = 'hidden';
            const firstFocusable = sb.querySelector('a[href], button:not([disabled]), input, select, [tabindex]:not([tabindex="-1"])');
            if (firstFocusable) setTimeout(() => firstFocusable.focus(), 50);
        };

        const closeMenu = () => {
            sb.classList.remove('open');
            ov.classList.remove('open');
            ham.setAttribute('aria-expanded', 'false');
            ham.setAttribute('aria-pressed', 'false');
            ov.setAttribute('aria-hidden', 'true');
            document.body.style.overflow = '';
            if (previousFocus && typeof previousFocus.focus === 'function') {
                try { previousFocus.focus(); } catch (_) { }
            }
            previousFocus = null;
        };

        const onHamburgerClick = (e) => { e.preventDefault(); if (sb.classList.contains('open')) closeMenu(); else openMenu(); };
        const onOverlayClick = (e) => { if (e.target === ov) closeMenu(); };
        const onKeyEsc = (e) => { if (e.key === 'Escape' && sb.classList.contains('open')) closeMenu(); };
        const onCloseBtn = (e) => { e.preventDefault(); closeMenu(); };

        ham.addEventListener('click', onHamburgerClick);
        ov.addEventListener('click', onOverlayClick);
        document.addEventListener('keydown', onKeyEsc);
        if (closeBtn) closeBtn.addEventListener('click', onCloseBtn);

        _mobileMenuCleanup = () => {
            ham.removeEventListener('click', onHamburgerClick);
            ov.removeEventListener('click', onOverlayClick);
            document.removeEventListener('keydown', onKeyEsc);
            if (closeBtn) closeBtn.removeEventListener('click', onCloseBtn);
        };
        // Enlazar cleanup a beforeunload por si acaso
        global.addEventListener('beforeunload', _mobileMenuCleanup, { once: true });
    }

    // ════════════════════════════════════════════════════════════════
    // 9. STATUS MAP ÚNICO CITAS (unifica las 3 versiones incompatibles que circulaban)
    //    Fuentes combinadas: shared/appointment-utils.js + st-pac-01 STATUS_MAP_SERVER +
    //    st-rec-03. Ahora TODAS las vistas deben usar este mapa centralizado.
    // ════════════════════════════════════════════════════════════════

    /** Claves canónicas de estado del servidor (snake_case) */
    const APPOINTMENT_STATUS = Object.freeze({
        PROGRAMADA: 'programada',
        CONFIRMADA: 'confirmada',
        EN_PROCESO: 'en_proceso',
        FINALIZADA: 'finalizada',
        ATENDIDA: 'atendida',
        CANCELADA: 'cancelada',
        NO_ASISTIDA: 'no_asistida',
        NO_SHOW: 'no-show'
    });

    /** Mapa Server → {label, cls} UI. */
    const STATUS_MAP_SERVER = Object.freeze({
        [APPOINTMENT_STATUS.PROGRAMADA]:  { label: 'Agendada',   cls: 'badge-agendada'   },
        [APPOINTMENT_STATUS.CONFIRMADA]:  { label: 'Confirmada', cls: 'badge-confirmada' },
        [APPOINTMENT_STATUS.EN_PROCESO]:  { label: 'En curso',   cls: 'badge-confirmada' },
        [APPOINTMENT_STATUS.FINALIZADA]:  { label: 'Completada', cls: 'badge-completada' },
        [APPOINTMENT_STATUS.ATENDIDA]:    { label: 'Completada', cls: 'badge-completada' },
        [APPOINTMENT_STATUS.CANCELADA]:   { label: 'Cancelada',  cls: 'badge-cancelada'  },
        [APPOINTMENT_STATUS.NO_ASISTIDA]: { label: 'No asistió', cls: 'badge-cancelada'  },
        [APPOINTMENT_STATUS.NO_SHOW]:     { label: 'No asistió', cls: 'badge-cancelada'  }
    });

    /** Mapa Label UI → Clave Server (para selects/filters cliente → server) */
    const STATUS_MAP_CLIENTE = Object.freeze({
        'Solicitada':  'solicitada',
        'Agendada':   APPOINTMENT_STATUS.PROGRAMADA,
        'Confirmada': APPOINTMENT_STATUS.CONFIRMADA,
        'En curso':   APPOINTMENT_STATUS.EN_PROCESO,
        'Completada': APPOINTMENT_STATUS.FINALIZADA,
        'Cancelada':  APPOINTMENT_STATUS.CANCELADA,
        'No asistió': APPOINTMENT_STATUS.NO_ASISTIDA
    });

    /** Normaliza alias del servidor al token canónico de la cita. */
    function normalizeAppointmentStatus(estado) {
        const normalized = String(estado ?? '').toLowerCase().trim()
            .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
            .replace(/[-\s]+/g, '_');

        return {
            solicitado: 'solicitada',
            agendada: APPOINTMENT_STATUS.PROGRAMADA,
            programado: APPOINTMENT_STATUS.PROGRAMADA,
            confirmado: APPOINTMENT_STATUS.CONFIRMADA,
            en_consulta: APPOINTMENT_STATUS.EN_PROCESO,
            finalizada: APPOINTMENT_STATUS.FINALIZADA,
            atendida: APPOINTMENT_STATUS.ATENDIDA,
            realizada: APPOINTMENT_STATUS.ATENDIDA,
            completada: APPOINTMENT_STATUS.ATENDIDA,
            cancelado: APPOINTMENT_STATUS.CANCELADA,
            no_asistio: APPOINTMENT_STATUS.NO_ASISTIDA,
            no_show: APPOINTMENT_STATUS.NO_ASISTIDA
        }[normalized] || normalized || APPOINTMENT_STATUS.PROGRAMADA;
    }

    /** Normaliza valor de estado server al key canónico. */
    function mapEstadoServerToClient(estado) {
        const normalized = normalizeAppointmentStatus(estado);
        if (normalized === APPOINTMENT_STATUS.FINALIZADA || normalized === APPOINTMENT_STATUS.ATENDIDA || normalized === APPOINTMENT_STATUS.EN_PROCESO) return APPOINTMENT_STATUS.FINALIZADA;
        if (STATUS_MAP_SERVER[normalized]) return normalized;
        return APPOINTMENT_STATUS.PROGRAMADA;
    }

    function mapEstadoClienteToServer(estado) {
        return STATUS_MAP_CLIENTE[estado] || normalizeAppointmentStatus(estado);
    }

    /** Retorna {label, cls} dado un valor de estado server/cliente. Nunca retorna undefined. */
    function getStatusInfo(estado) {
        const canon = mapEstadoServerToClient(estado);
        return STATUS_MAP_SERVER[canon] || STATUS_MAP_SERVER[APPOINTMENT_STATUS.PROGRAMADA];
    }

    /** Dado un valor de estado UI (label) retorna la clase badge CSS. Helper para legacy. */
    function getStatusBadgeClass(estadoLabelOrKey) {
        // Si ya es key server:
        if (STATUS_MAP_SERVER[estadoLabelOrKey]) return STATUS_MAP_SERVER[estadoLabelOrKey].cls;
        // Si es label cliente → traducir
        const canon = STATUS_MAP_CLIENTE[estadoLabelOrKey];
        if (canon) return STATUS_MAP_SERVER[canon].cls;
        // Normalizar por si acaso
        return getStatusInfo(estadoLabelOrKey).cls;
    }

    // ════════════════════════════════════════════════════════════════
    // 10. MISC HELPERS
    // ════════════════════════════════════════════════════════════════

    /** Prevenir doble submit en formularios. Retorna restore(). */
    function lockSubmitButton(btn, loadingText) {
        if (!btn) return { restore: () => {} };
        const html = btn.innerHTML;
        const disabled = btn.disabled;
        btn.disabled = true;
        const label = loadingText || 'Guardando...';
        btn.setAttribute('aria-busy', 'true');
        btn.innerHTML = `<svg class="st-spinner-inline" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true" style="width:16px;height:16px;animation:st-spin .7s linear infinite;display:inline-block;vertical-align:middle;margin-right:6px;"><path d="M12 2a10 10 0 0 1 10 10"/></svg> ${escapeHtml(label)}`;
        if (!document.getElementById('st-spinner-inline-style')) {
            const s = document.createElement('style');
            s.id = 'st-spinner-inline-style';
            s.textContent = '@keyframes st-spin{to{transform:rotate(360deg)}}';
            document.head.appendChild(s);
        }
        return {
            restore() {
                btn.disabled = disabled;
                btn.removeAttribute('aria-busy');
                btn.innerHTML = html;
            }
        };
    }

    /** Descargar un Blob como archivo (usado en CSV/PDF cliente). */
    function downloadBlob(blob, filename) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 2000);
    }

    /**
     * Escapa valor para campo CSV (maneja comas, comillas y saltos de línea).
     */
    function csvEscape(value) {
        const s = String(value ?? '');
        if (/[",\n\r]/.test(s)) {
            return `"${s.replace(/"/g, '""')}"`;
        }
        return s;
    }

    // ════════════════════════════════════════════════════════════════
    // EXPORTS (namespace único window.CommonUtils)
    // ════════════════════════════════════════════════════════════════
    const CommonUtils = Object.freeze({
        // Config
        DEFAULT_API_BASE,
        DEFAULT_PAGE_SIZE,
        MONTHS_ES_CO,
        MONTHS_LONG_ES_CO,
        // Selectores
        safeGetElement,
        safeQuerySelector,
        // Rendimiento
        debounce,
        throttle,
        // Auth / API
        getCsrfToken,
        getAuthHeaders,
        buildQuery,
        fetchWithAuth,
        // Seguridad
        escapeHtml,
        // Fechas
        formatFechaLocal,
        formatFechaHoraLocal,
        formatFechaLarga,
        formatHora,
        formatTiempoRelativo,
        // Animaciones
        animateCounter,
        animateCounters,
        // UI
        initMobileMenu,
        lockSubmitButton,
        downloadBlob,
        csvEscape,
        // Status map
        APPOINTMENT_STATUS,
        STATUS_MAP_SERVER,
        STATUS_MAP_CLIENTE,
        normalizeAppointmentStatus,
        mapEstadoServerToClient,
        mapEstadoClienteToServer,
        getStatusInfo,
        getStatusBadgeClass
    });

    global.CommonUtils = CommonUtils;

    // ── Backward-compat: alias individuales en window para módulos antiguos
    //    que aún no migraron. NOTA: se eliminarán en Fase-2 (migración TS).
    global.safeGetElement = safeGetElement;
    global.safeQuerySelector = safeQuerySelector;
    global.debounce = debounce;
    global.throttle = throttle;
    global.escapeHtml = escapeHtml;
    global.formatFechaLocal = formatFechaLocal;
    global.formatFechaHoraLocal = formatFechaHoraLocal;
    global.formatTiempoRelativo = formatTiempoRelativo;
    global.animateCounter = animateCounter;
    global.animateCounters = animateCounters;
    global.initMobileMenu = initMobileMenu;
    global.getAuthHeaders = getAuthHeaders;
    global.getCsrfToken = getCsrfToken;
    global.buildQuery = buildQuery;
    global.STATUS_MAP_SERVER = STATUS_MAP_SERVER;
    global.STATUS_MAP_CLIENTE = STATUS_MAP_CLIENTE;
    global.normalizeAppointmentStatus = normalizeAppointmentStatus;
    global.mapEstadoServerToClient = mapEstadoServerToClient;
    global.mapEstadoClienteToServer = mapEstadoClienteToServer;
    global.fetchWithAuth = fetchWithAuth;
    global.API_BASE_COMMON = DEFAULT_API_BASE;

    // Auto-inicializaciones seguras
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            animateCounters(document);
        });
    } else {
        animateCounters(document);
    }

})(window);
