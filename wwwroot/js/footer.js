// Inyecta el footer dinámicamente solo si no fue incluido en el HTML del layout
document.addEventListener('DOMContentLoaded', () => {
    if (document.getElementById('dynamic-footer')) return;

    const footerHTML = `
    <footer id="dynamic-footer" class="site-footer" role="contentinfo">
      <p>© 2025 Smile Track. Todos los derechos reservados.</p>
      <p class="footer-sub">Proyecto Formativo ADSO – SENA</p>
    </footer>
  `;

    document.body.insertAdjacentHTML('beforeend', footerHTML);

    const clearAuthStorage = () => {
      try {
        localStorage.removeItem('token');
        localStorage.removeItem('authToken');
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        sessionStorage.removeItem('token');
        sessionStorage.removeItem('authToken');
        sessionStorage.removeItem('accessToken');
        sessionStorage.removeItem('refreshToken');
      } catch (error) {
        console.warn('No se pudo limpiar el almacenamiento local de autenticación.', error);
      }
    };

    const getCsrfToken = () => {
      const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
      return match ? decodeURIComponent(match[2]) : null;
    };

    document.querySelectorAll('.nav-item--logout').forEach((link) => {
      link.addEventListener('click', async (event) => {
        event.preventDefault();
        clearAuthStorage();
        try {
          const token = getCsrfToken();
          await fetch('/acceso-y-seguridad/logout', {
            method: 'POST',
            credentials: 'same-origin',
            headers: token ? { 'X-CSRF-TOKEN': token } : {}
          });
        } catch (error) {
          console.warn('Error al invalidar sesión en el backend.', error);
        }
        window.location.href = '/acceso-y-seguridad/login';
      });
    });
});