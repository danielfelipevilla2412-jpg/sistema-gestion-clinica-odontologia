/* ============================================
   SmileTrack — Toasts Compartidos (shared/toasts.js)
   ============================================
   Sistema de notificaciones globales.
   ============================================ */

const ToastService = (function () {
    let container = null;

    function createContainerIfNeeded() {
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container';
            container.id = 'toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    /**
     * Muestra un toast en pantalla.
     * @param {Object} options Opciones del toast.
     * @param {string} options.title Título del toast.
     * @param {string} options.message Mensaje del toast.
     * @param {string} [options.type='info'] Tipo: 'success', 'error', 'warning', 'info'.
     * @param {number} [options.duration=5000] Duración en milisegundos.
     */
    function show(options) {
        const { title, message, type = 'info', duration = 5000 } = options;

        const cont = createContainerIfNeeded();

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.setAttribute('role', 'alert');

        let iconStr = 'info';
        if (type === 'success') iconStr = 'check_circle';
        else if (type === 'error') iconStr = 'error';
        else if (type === 'warning') iconStr = 'warning';

        const icon = document.createElement('span');
        icon.className = 'material-symbols-outlined toast-icon';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = iconStr;

        const content = document.createElement('div');
        content.className = 'toast-content';

        const titleElement = document.createElement('h4');
        titleElement.className = 'toast-title';
        titleElement.textContent = title;

        const messageElement = document.createElement('p');
        messageElement.className = 'toast-message';
        messageElement.textContent = message;

        content.append(titleElement, messageElement);

        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'toast-close';
        closeBtn.setAttribute('aria-label', 'Cerrar notificación');
        closeBtn.textContent = '×';

        toast.append(icon, content, closeBtn);

        cont.appendChild(toast);

        // Forzar reflujo para la animación
        void toast.offsetWidth;
        toast.classList.add('show');

        const removeToast = () => {
            toast.classList.remove('show');
            toast.classList.add('hide');
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 400); // Esperar a que termine la animación
        };

        closeBtn.addEventListener('click', removeToast);

        if (duration > 0) {
            setTimeout(removeToast, duration);
        }
    }

    const normalizeToastArgs = (title, message, duration) => {
        // Permite usar:
        // ToastService.warning("Mensaje")
        // o:
        // ToastService.warning("Título", "Mensaje", 5000)

        if (message === undefined) {
            return {
                title: 'Aviso',
                message: title,
                duration: duration ?? 5000
            };
        }

        return {
            title,
            message,
            duration: duration ?? 5000
        };
    };

    return {
        success: (title, message, duration) => {
            const toast = normalizeToastArgs(title, message, duration);
            show({ ...toast, type: 'success' });
        },

        error: (title, message, duration) => {
            const toast = normalizeToastArgs(title, message, duration);
            show({ ...toast, type: 'error' });
        },

        warning: (title, message, duration) => {
            const toast = normalizeToastArgs(title, message, duration);
            show({ ...toast, type: 'warning' });
        },

        info: (title, message, duration) => {
            const toast = normalizeToastArgs(title, message, duration);
            show({ ...toast, type: 'info' });
        }
    };
})();

// Exponer de forma global para usar en cualquier JS de vista
window.ToastService = ToastService;
