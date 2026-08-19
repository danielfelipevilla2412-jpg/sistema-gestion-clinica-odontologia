/* ============================================
   SmileTrack — Modales Compartidos (shared/modals.js)
   ============================================
   Controlador genérico de modales.
   ============================================ */

const ModalService = (function () {
    
    /**
     * Muestra un modal de confirmación estándar.
     * Si no existe en el DOM, lo crea dinámicamente.
     * @param {Object} options
     * @param {string} options.title Título del modal
     * @param {string} options.message Mensaje/cuerpo del modal (acepta HTML)
     * @param {string} [options.confirmText='Confirmar'] Texto botón confirmación
     * @param {string} [options.cancelText='Cancelar'] Texto botón cancelar
     * @param {boolean} [options.isDanger=false] Si es true, el botón será rojo
     * @param {Function} options.onConfirm Callback al confirmar (puede devolver Promise para estado de carga)
     */
    function confirm(options) {
        const { 
            title = 'Confirmación', 
            message = '¿Estás seguro?', 
            confirmText = 'Confirmar', 
            cancelText = 'Cancelar', 
            isDanger = false,
            onConfirm 
        } = options;

        // Crear o reutilizar overlay genérico
        let overlay = document.getElementById('global-confirm-modal');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'global-confirm-modal';
            overlay.className = 'modal-overlay';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');
            document.body.appendChild(overlay);
        }

        overlay.innerHTML = `
            <div class="modal-content">
                <div class="modal-header">
                    <h3 class="modal-title">${title}</h3>
                    <button class="modal-close-btn" aria-label="Cerrar modal"><span class="material-symbols-outlined">close</span></button>
                </div>
                <div class="modal-body">
                    ${message}
                </div>
                <div class="modal-footer">
                    <button class="btn-modal-cancel">${cancelText}</button>
                    <button class="btn-modal-confirm ${isDanger ? 'danger' : ''}">
                        <span class="material-symbols-outlined modal-spinner">progress_activity</span>
                        <span class="btn-text">${confirmText}</span>
                    </button>
                </div>
            </div>
        `;

        const btnClose = overlay.querySelector('.modal-close-btn');
        const btnCancel = overlay.querySelector('.btn-modal-cancel');
        const btnConfirm = overlay.querySelector('.btn-modal-confirm');

        const closeModal = () => {
            overlay.classList.remove('open');
            setTimeout(() => {
                if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
            }, 300);
        };

        btnClose.addEventListener('click', closeModal);
        btnCancel.addEventListener('click', closeModal);
        
        // Cerrar con Escape
        const handleEscape = (e) => {
            if (e.key === 'Escape') {
                closeModal();
                document.removeEventListener('keydown', handleEscape);
            }
        };
        document.addEventListener('keydown', handleEscape);

        btnConfirm.addEventListener('click', async () => {
            if (onConfirm) {
                try {
                    // Si onConfirm devuelve una promesa, mostramos spinner
                    btnConfirm.classList.add('loading');
                    await onConfirm();
                    closeModal();
                } catch (error) {
                    console.error("Error en confirmación:", error);
                    // Si algo falla, quitamos spinner pero no cerramos para que vea el error si se muestra (ej via toast)
                    btnConfirm.classList.remove('loading');
                }
            } else {
                closeModal();
            }
        });

        // Mostrar con animación
        void overlay.offsetWidth;
        overlay.classList.add('open');
        btnConfirm.focus();
    }

    return {
        confirm
    };
})();

window.ModalService = ModalService;
