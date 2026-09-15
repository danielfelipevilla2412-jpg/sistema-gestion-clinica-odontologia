/* ============================================================================
   i18n-core — Componente de internacionalización reutilizable (Vanilla JS)
   --------------------------------------------------------------------------
   Sin dependencias. Se puede usar con <script> (global window.createI18n),
   AMD o CommonJS.

   USO MÍNIMO
   ----------
   const i18n = createI18n({
     storageKey: 'miapp_lang',
     languages : ['es', 'en'],
     fallback  : 'es',
     messages  : {
       es: { 'hola': 'Hola {nombre}' },
       en: { 'hola': 'Hi {nombre}' }
     }
   });
   i18n.t('hola', { nombre: 'Ana' });     // -> "Hola Ana"
   i18n.apply(document);                  // traduce el DOM
   i18n.set('en');                        // cambia idioma + re-renderiza + persiste

   MARCADO EN HTML
   ---------------
   <h1 data-i18n="hola"></h1>
   <input data-i18n-ph="buscar">
   <span data-i18n-html="rico"></span>
   <button data-i18n-title="ayuda"></button>
   <select data-i18n-switcher>...</select>   (cambia el idioma solo)
   <button data-i18n-switcher data-i18n-lang="en">EN</button>

   EVENTOS
   -------
   i18n.on('change', ({ lang, prev }) => { ... });   // API propia
   document.addEventListener('i18n:change', e => e.detail.lang);  // evento DOM
   ==========================================================================*/
(function (root, factory) {
  'use strict';
  if (typeof define === 'function' && define.amd) define([], factory);
  else if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.createI18n = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function createI18n(userConfig) {
    var cfg = Object.assign({
      storageKey   : 'app_lang',          // clave de localStorage (null = no persistir)
      languages    : ['es'],              // idiomas soportados
      fallback     : null,               // idioma base (por defecto languages[0])
      initial      : null,               // forzar idioma inicial (ignora storage/navegador)
      detectBrowser: true,               // usar navigator.language si no hay preferencia guardada
      messages     : {},                 // { es: {...}, en: {...} }
      prefix       : '{',                // interpolación: {var}
      suffix       : '}',
      switcherSel  : '[data-i18n-switcher]',
      applyOnInit  : true,               // traducir el DOM al crearse
      setHtmlLang  : true,               // <html lang="..">
      missing      : null                // fn(clave, lang) para reportar faltantes
    }, userConfig || {});

    var LANGS   = cfg.languages.slice();
    var FALLBACK = cfg.fallback || LANGS[0];
    var DICT    = {};
    Object.keys(cfg.messages || {}).forEach(function (l) { DICT[l] = cfg.messages[l] || {}; });

    var listeners = { change: [] };
    var lang = resolveInitial();

    /* -------- idioma inicial -------- */
    function resolveInitial() {
      if (cfg.initial && LANGS.indexOf(cfg.initial) > -1) return cfg.initial;
      if (cfg.storageKey) {
        try {
          var saved = localStorage.getItem(cfg.storageKey);
          if (saved && LANGS.indexOf(saved) > -1) return saved;
        } catch (e) {}
      }
      if (cfg.detectBrowser && typeof navigator !== 'undefined') {
        var nav = (navigator.language || '').slice(0, 2).toLowerCase();
        if (LANGS.indexOf(nav) > -1) return nav;
      }
      return FALLBACK;
    }

    /* -------- traducción -------- */
    function t(clave, vars) {
      var tabla = DICT[lang] || {};
      var base  = DICT[FALLBACK] || {};
      var txt;
      if (tabla[clave] != null)      txt = tabla[clave];
      else if (base[clave] != null)  txt = base[clave];
      else {
        if (typeof cfg.missing === 'function') cfg.missing(clave, lang);
        txt = humanizeKey(clave);
      }

      if (vars) Object.keys(vars).forEach(function (k) {
        txt = txt.split(cfg.prefix + k + cfg.suffix).join(vars[k]);
      });
      return txt;
    }

    function humanizeKey(clave) {
      var s = String(clave || '').trim();
      if (/\b(bc|breadcrumb|admin)\b/i.test(s) || /_bc_/i.test(s)) {
        return (lang === 'en') ? 'Administration' : 'Administración';
      }
      var clean = s
        .replace(/^(adm\d+|common|nav|status|table|form|citas|historia|facturacion|servicios|config|pqr|gestion[a-z_]*|detalle_paciente|notifications|login|reportes|perfil)[_.]?/i, '')
        .replace(/[._-]+/g, ' ')
        .replace(/\b(aria|ph|btn|stat|title|subtitle|label|text|all|active)\b/gi, '')
        .replace(/\s+/g, ' ')
        .trim();
      if (!clean || clean.length < 2) return (lang === 'en') ? 'Details' : 'Detalles';
      return clean.charAt(0).toUpperCase() + clean.slice(1);
    }

    function has(clave, l) { return !!(DICT[l || lang] && DICT[l || lang][clave] != null); }

    /* -------- aplicar al DOM -------- */
    function apply(scope) {
      scope = scope || document;
      harvestMissing(scope);
      each(scope, '[data-i18n]',       function (el, key) { el.textContent = t(key); });
      each(scope, '[data-i18n-html]',  function (el, key) { el.innerHTML   = t(key); });
      each(scope, '[data-i18n-ph]',    function (el, key) { el.setAttribute('placeholder', t(key)); });
      each(scope, '[data-i18n-title]', function (el, key) { el.setAttribute('title', t(key)); });
      each(scope, '[data-i18n-value]', function (el, key) { el.setAttribute('value', t(key)); });
      each(scope, '[data-i18n-aria-label]', function (el, key) { el.setAttribute('aria-label', t(key)); });

      if (cfg.setHtmlLang && typeof document !== 'undefined' && document.documentElement)
        document.documentElement.setAttribute('lang', lang);

      syncSwitchers(scope);
    }

    function harvestMissing(scope) {
      if (!scope || typeof scope.querySelectorAll !== 'function') return;
      var selectors = [
        ['data-i18n', function (el) { return el.textContent.trim(); }],
        ['data-i18n-html', function (el) { return el.innerHTML.trim(); }],
        ['data-i18n-ph', function (el) { return el.getAttribute('placeholder') || ''; }],
        ['data-i18n-title', function (el) { return el.getAttribute('title') || ''; }],
        ['data-i18n-value', function (el) { return el.getAttribute('value') || ''; }],
        ['data-i18n-aria-label', function (el) { return el.getAttribute('aria-label') || ''; }]
      ];
      selectors.forEach(function (entry) {
        var attr = entry[0];
        var nodes = scope.querySelectorAll('[' + attr + ']');
        for (var i = 0; i < nodes.length; i++) {
          var key = nodes[i].getAttribute(attr);
          if (!key || has(key, FALLBACK)) continue;
          var original = entry[1](nodes[i]);
          if (!original) continue;
          DICT[FALLBACK][key] = original;
          if (!DICT.en) DICT.en = {};
          if (DICT.en[key] == null) DICT.en[key] = humanizeKey(key);
        }
      });
    }

    function each(scope, sel, fn) {
      var nodes = scope.querySelectorAll(sel);
      for (var i = 0; i < nodes.length; i++) {
        var attr = sel.slice(1, -1);            // '[data-i18n-ph]' -> 'data-i18n-ph'
        fn(nodes[i], nodes[i].getAttribute(attr));
      }
    }

    /* -------- cambio de idioma -------- */
    function set(nuevo) {
      if (LANGS.indexOf(nuevo) === -1 || nuevo === lang) return;
      var prev = lang;
      lang = nuevo;
      if (cfg.storageKey) { try { localStorage.setItem(cfg.storageKey, nuevo); } catch (e) {} }
      apply(document);
      emit('change', { lang: lang, prev: prev, t: t });
    }

    /* -------- selectores de idioma automáticos -------- */
    function bindSwitchers(scope) {
      var nodes = (scope || document).querySelectorAll(cfg.switcherSel);
      for (var i = 0; i < nodes.length; i++) {
        var el = nodes[i];
        if (el.__i18nBound) continue;
        el.__i18nBound = true;
        if (el.tagName === 'SELECT') {
          el.addEventListener('change', function () { set(this.value); });
        } else {
          el.addEventListener('click', function () {
            var l = this.getAttribute('data-i18n-lang') || this.value;
            if (l) set(l);
          });
        }
      }
    }

    function syncSwitchers(scope) {
      var nodes = (scope || document).querySelectorAll(cfg.switcherSel);
      for (var i = 0; i < nodes.length; i++) {
        var el = nodes[i];
        if (el.tagName === 'SELECT') { if (el.value !== lang) el.value = lang; }
        else {
          var l = el.getAttribute('data-i18n-lang') || el.value;
          el.setAttribute('aria-pressed', String(l === lang));
          el.classList.toggle('is-active', l === lang);
        }
      }
    }

    /* -------- eventos -------- */
    function on(evt, fn)  { (listeners[evt] = listeners[evt] || []).push(fn); return api; }
    function off(evt, fn) {
      if (!listeners[evt]) return api;
      listeners[evt] = listeners[evt].filter(function (f) { return f !== fn; });
      return api;
    }
    function emit(evt, detail) {
      (listeners[evt] || []).forEach(function (fn) { try { fn(detail); } catch (e) { console.error(e); } });
      if (typeof document !== 'undefined' && typeof CustomEvent === 'function')
        document.dispatchEvent(new CustomEvent('i18n:' + evt, { detail: detail }));
    }

    /* -------- diccionarios en runtime -------- */
    function add(l, obj) {
      DICT[l] = Object.assign(DICT[l] || {}, obj || {});
      if (LANGS.indexOf(l) === -1) LANGS.push(l);
      return api;
    }

    // Carga diccionarios remotos: loadFrom('assets/i18n/{lng}.json')
    function loadFrom(pattern, langs) {
      var list = langs || LANGS;
      return Promise.all(list.map(function (l) {
        return fetch(pattern.replace('{lng}', l))
          .then(function (r) { return r.ok ? r.json() : {}; })
          .then(function (obj) { add(l, obj); })
          .catch(function () {});
      })).then(function () { if (cfg.applyOnInit) apply(document); return api; });
    }

    /* -------- init -------- */
    function init() {
      if (typeof document === 'undefined') return;
      bindSwitchers(document);
      if (cfg.applyOnInit) apply(document);
    }
    if (typeof document !== 'undefined') {
      if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
      else init();
    }

    var api = {
      t: t, set: set, apply: apply, has: has, add: add, loadFrom: loadFrom,
      on: on, off: off, bindSwitchers: bindSwitchers,
      get lang()      { return lang; },
      get languages() { return LANGS.slice(); },
      get fallback()  { return FALLBACK; },
      config: cfg
    };
    return api;
  }

  return createI18n;
});
