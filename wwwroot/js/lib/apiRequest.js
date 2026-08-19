// Utilidad central para llamadas API que prioriza JWT en cookie "SmileTrack-JWT" y usa Cookie Auth como fallback
function getAntiforgeryToken() {
  const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
  return match ? decodeURIComponent(match[2]) : null;
}

export async function apiRequest(path, options = {}) {
  const url = (window.APP_CONFIG && window.APP_CONFIG.ApiBase ? window.APP_CONFIG.ApiBase : '') + path;
  const opts = { method: options.method || 'GET', headers: options.headers || {}, body: options.body, credentials: options.credentials || 'same-origin' };

  const method = (opts.method || 'GET').toUpperCase();
  if (!['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method)) {
    const csrfToken = getAntiforgeryToken();
    if (csrfToken) {
      opts.headers = { ...opts.headers, 'X-CSRF-TOKEN': csrfToken };
    }
  }

  try {
    // Leer cookie httpOnly no es posible desde JS; intentar primero sessionStorage fallback (legacy)
    let token = null;
    try { token = sessionStorage.getItem('st_jwt'); } catch {}

    // Si no hay token en sessionStorage intentar leer cookie no-httpOnly (si la app no la marca httpOnly)
    if (!token) {
      const match = document.cookie.match(new RegExp('(^| )SmileTrack-JWT=([^;]+)'));
      if (match) token = decodeURIComponent(match[2]);
    }

    if (token) {
      opts.headers = { ...opts.headers, 'Authorization': `Bearer ${token}`, 'Accept': 'application/json' };
    } else {
      // No token: confía en cookie de sesión (credentials: same-origin)
      opts.headers = { ...opts.headers, 'Accept': 'application/json' };
    }

    if (opts.body && typeof opts.body === 'object' && !(opts.body instanceof FormData)) {
      opts.headers['Content-Type'] = opts.headers['Content-Type'] || 'application/json';
      opts.body = JSON.stringify(opts.body);
    }

    const res = await fetch(url, opts);
    if (res.status === 401) {
      // Redirigir al login en caso de expiración
      window.location.href = '/acceso-y-seguridad/login';
      return null;
    }

    const contentType = res.headers.get('content-type') || '';
    if (contentType.includes('application/json')) return await res.json();
    return await res.text();
  } catch (err) {
    console.error('[apiRequest] Error:', err);
    throw err;
  }
}
