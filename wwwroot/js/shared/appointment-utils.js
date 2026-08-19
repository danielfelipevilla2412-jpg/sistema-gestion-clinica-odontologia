/**
 * ============================================
 * SmileTrack — Utilidades de Citas Compartidas
 * ============================================
 * Autor: SmileTrack Team
 * 
 * PROPÓSITO:
 * Centralizar el mapeo de estados, colores de avatares, validaciones
 * compartidas, spinners en botones de submit y prevención de pérdida
 * de datos en formularios modales (form dirty detection).
 * ============================================
 */

window.AppointmentUtils = (() => {
    // ════════════════════════════════════════════════════════════════════
    //  MAPEO DE ESTADOS
    // ════════════════════════════════════════════════════════════════════
    const statusLabels = {
        programada: { label: 'Programada', class: 'programada' },
        confirmada: { label: 'Confirmada', class: 'confirmada' },
        atendida: { label: 'Atendida', class: 'atendida' },
        cancelada: { label: 'Cancelada', class: 'cancelada' },
        'no-show': { label: 'No asistió', class: 'cancelada' }
    };

    const mapEstadoServerToClient = (estado) => {
        if (!estado) return 'programada';
        const estadoNormalized = estado.toLowerCase().trim();
        if (estadoNormalized === 'atendida' || estadoNormalized === 'finalizada' || estadoNormalized === 'en_proceso') return 'atendida';
        if (estadoNormalized === 'no_asistida') return 'no-show';
        if (['programada', 'confirmada', 'cancelada'].includes(estadoNormalized)) return estadoNormalized;
        return 'programada';
    };

    const getStatusLabelAndClass = (statusKey) => statusLabels[statusKey] || statusLabels.programada;

    // ════════════════════════════════════════════════════════════════════
    //  AVATARES Y COLORES
    // ════════════════════════════════════════════════════════════════════
    const AVATAR_COLOR_PALETTE = ['blue', 'green', 'purple', 'red', 'slate'];
    const avatarColors = {
        blue: 'bg-blue-100 text-blue-600',
        green: 'bg-green-100 text-green-600',
        purple: 'bg-purple-100 text-purple-600',
        red: 'bg-red-100 text-red-600',
        slate: 'bg-slate-100 text-slate-600'
    };

    const getAvatarColorClass = (colorKey) => avatarColors[colorKey] || avatarColors.blue;

    const pickColorByInitials = (initials) => {
        if (!initials) return 'blue';
        let hash = 0;
        for (let i = 0; i < initials.length; i++) hash = ((hash << 5) - hash) + initials.charCodeAt(i);
        return AVATAR_COLOR_PALETTE[Math.abs(hash) % AVATAR_COLOR_PALETTE.length];
    };

    const getInitials = (fullName) => {
        if (!fullName) return 'XX';
        const parts = fullName.trim().split(/\s+/).filter(Boolean);
        if (parts.length === 0) return 'XX';
        if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    };

    // ════════════════════════════════════════════════════════════════════
    //  VALIDACIÓN DE CITAS
    // ════════════════════════════════════════════════════════════════════
    const validateAppointmentTime = (fecha, horaInicio, horaFin) => {
        const errors = [];

        if (!fecha || !horaInicio || !horaFin) {
            errors.push({
                field: 'general',
                message: 'La fecha y las horas de inicio y fin son obligatorias.'
            });

            return errors;
        }

        const inicio = new Date(`${fecha}T${horaInicio}`);
        const fin = new Date(`${fecha}T${horaFin}`);

        if (horaInicio >= horaFin) {
            errors.push({
                field: 'horaInicio',
                message: 'La hora de inicio debe ser anterior a la hora de fin.'
            });
        } else {
            const duracionMinutos = (fin - inicio) / 60000;

            if (duracionMinutos !== 60) {
                errors.push({
                    field: 'horaFin',
                    message: 'La cita debe tener una duración de 60 minutos.'
                });
            }
        }

        const minDate = new Date();
        minDate.setMinutes(minDate.getMinutes() - 5);

        if (inicio < minDate) {
            errors.push({
                field: 'fecha',
                message: 'No puedes agendar una cita en un horario pasado.'
            });
        }

        return errors;
    };

    // ════════════════════════════════════════════════════════════════════
    //  SPINNERS EN BOTONES DE SUBMIT
    // ════════════════════════════════════════════════════════════════════

    /**
     * Convierte un botón de submit en un botón con spinner mientras la acción está en progreso.
     * Previene dobles clics que generan registros duplicados.
     *
     * @param {HTMLButtonElement} btn   - El botón a proteger
     * @param {string}  [loadingText]   - Texto visible junto al spinner (default: "Guardando...")
     * @returns {{ restore: Function }} - Objeto con método restore() para rehabilitar el botón
     *
     * USO:
     *   const ctrl = AppointmentUtils.submitWithSpinner(btn, 'Guardando cita...');
     *   // Si la operación falla y necesitas restaurar el botón:
     *   ctrl.restore();
     */
    const submitWithSpinner = (btn, loadingText = 'Guardando...') => {
        if (!btn) return { restore: () => { } };

        const originalHTML = btn.innerHTML;
        const originalDisabled = btn.disabled;

        btn.disabled = true;
        btn.setAttribute('aria-busy', 'true');
        // Spinner inline SVG: no depende de librerías externas ni de fuentes de íconos
        btn.innerHTML = `<svg class="st-spinner" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" stroke-width="2.5" stroke-linecap="round" aria-hidden="true"
            style="width:16px;height:16px;animation:st-spin .7s linear infinite;flex-shrink:0;vertical-align:middle;">
            <path d="M12 2a10 10 0 0 1 10 10" />
        </svg> <span>${loadingText}</span>`;

        // Inyectar la animación keyframe una sola vez en el documento
        if (!document.getElementById('st-spinner-style')) {
            const style = document.createElement('style');
            style.id = 'st-spinner-style';
            style.textContent = '@keyframes st-spin { to { transform: rotate(360deg); } }';
            document.head.appendChild(style);
        }

        const restore = () => {
            btn.disabled = originalDisabled;
            btn.removeAttribute('aria-busy');
            btn.innerHTML = originalHTML;
        };

        return { restore };
    };

    /**
     * Aplica submitWithSpinner automáticamente a todos los botones [type="submit"]
     * dentro de un formulario dado.
     *
     * @param {HTMLFormElement} form
     * @param {string} [loadingText]
     */
    const bindFormSubmitSpinner = (form, loadingText = 'Guardando...') => {
        if (!form) return;
        form.addEventListener('submit', (e) => {
            // Solo aplicar si el formulario pasa la validación HTML5 nativa
            if (!form.checkValidity()) return;
            const submitBtn = form.querySelector('button[type="submit"]');
            submitWithSpinner(submitBtn, loadingText);
            // El spinner se restaura automáticamente en la próxima carga de la vista
            // (el servidor redirige o recarga tras el POST), así que no se necesita restore() aquí.
        });
    };

    // ════════════════════════════════════════════════════════════════════
    //  FORM DIRTY — PREVENCIÓN DE PÉRDIDA DE DATOS EN MODALES
    // ════════════════════════════════════════════════════════════════════

    /**
     * Vigila un formulario dentro de un modal y alerta al usuario si intenta
     * cerrarlo habiendo introducido datos sin guardar.
     *
     * Detecta cambios en: inputs, selects, textareas.
     * Se integra con _ConfirmModal.cshtml para mostrar el diálogo de confirmación.
     * Si _ConfirmModal no está disponible, usa window.confirm como fallback.
     *
     * @param {HTMLElement} modalOverlay  - El elemento .modal-overlay
     * @param {HTMLFormElement} form       - El formulario dentro del modal
     * @param {string[]} closeTriggerIds   - IDs de botones/elementos que cierran el modal
     *
     * LÓGICA:
     *   1. Al abrir el modal se captura un snapshot de los valores del form.
     *   2. Cuando el usuario intenta cerrar, se compara el estado actual con el snapshot.
     *   3. Si hay diferencias → mostrar confirmación antes de cerrar.
     *   4. Si no hay diferencias → cerrar directamente.
     *   5. Al guardar con éxito se resetea el snapshot (form ya no está dirty).
     */
    const watchFormDirty = (modalOverlay, form, closeTriggerIds = []) => {
        if (!modalOverlay || !form) return;

        let snapshot = null;
        let isDirty = false;

        /** Toma un snapshot de todos los campos del formulario */
        const takeSnapshot = () => {
            const data = {};
            form.querySelectorAll('input, select, textarea').forEach(el => {
                if (el.name) data[el.name] = el.type === 'checkbox' ? el.checked : el.value;
            });
            return JSON.stringify(data);
        };

        /** Compara el estado actual con el snapshot inicial */
        const checkDirty = () => {
            if (!snapshot) return false;
            const current = takeSnapshot();
            return current !== snapshot;
        };

        /**
         * Intenta cerrar el modal.
         * Si el form está dirty → abre _ConfirmModal o window.confirm.
         * @param {Function} closeFn - Función que realmente cierra el modal
         */
        const tryClose = (closeFn) => {
            isDirty = checkDirty();
            if (!isDirty) { closeFn(); return; }

            // Intentar usar _ConfirmModal si está disponible en la página
            const confirmModal = document.getElementById('confirmModal');
            if (confirmModal && window.ConfirmModalService) {
                window.ConfirmModalService.show({
                    title: '¿Descartar cambios?',
                    message: 'Tienes cambios sin guardar. Si cierras ahora perderás la información ingresada.',
                    confirmText: 'Sí, descartar',
                    cancelText: 'Seguir editando',
                    confirmClass: 'btn-danger',
                    onConfirm: () => { snapshot = null; closeFn(); }
                });
            } else {
                // Fallback: confirm nativo del navegador
                if (window.confirm('Tienes cambios sin guardar. ¿Deseas descartarlos?')) {
                    snapshot = null;
                    closeFn();
                }
            }
        };

        // Función que efectivamente cierra el modal
        const doClose = () => {
            modalOverlay.classList.remove('open');
            modalOverlay.setAttribute('aria-hidden', 'true');
            modalOverlay.setAttribute('inert', '');
            if (!document.querySelector('.modal-overlay.open')) {
                document.body.style.overflow = '';
            }
        };

        // ── Observar cambios en el formulario ────────────────────────────
        form.querySelectorAll('input, select, textarea').forEach(el => {
            el.addEventListener('change', () => { isDirty = checkDirty(); });
            el.addEventListener('input', () => { isDirty = checkDirty(); });
        });

        // ── Capturar snapshot cuando el modal se abre ────────────────────
        // Usa MutationObserver para detectar cuando el modal gana la clase .open
        const observer = new MutationObserver(() => {
            if (modalOverlay.classList.contains('open')) {
                snapshot = takeSnapshot();
                isDirty = false;
            }
        });
        observer.observe(modalOverlay, { attributes: true, attributeFilter: ['class'] });

        // ── Vincular botones de cierre ────────────────────────────────────
        closeTriggerIds.forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('click', (e) => {
                e.preventDefault();
                tryClose(doClose);
            });
        });

        // ── Cerrar al clicar fuera del modal (en el overlay) ─────────────
        modalOverlay.addEventListener('click', (e) => {
            if (e.target === modalOverlay) tryClose(doClose);
        });

        // ── Resetear dirty al enviar el form con éxito ────────────────────
        form.addEventListener('submit', () => { snapshot = null; isDirty = false; });

        return { resetDirty: () => { snapshot = takeSnapshot(); isDirty = false; } };
    };

    /**
     * Inicializa spinners y form-dirty en todos los formularios de citas
     * de la página actual de forma automática.
     *
     * Detecta los formularios por sus IDs convencionales del sistema.
     * Seguro llamar aunque no todos los formularios existan en la vista actual.
     */
    const initCitasForms = () => {
        // ── Spinners en todos los forms de citas ─────────────────────────
        ['formNewAppointment', 'formEditAppointment', 'formCita'].forEach(formId => {
            bindFormSubmitSpinner(document.getElementById(formId), 'Guardando cita...');
        });

        // ── Form dirty en modal Nueva Cita ────────────────────────────────
        watchFormDirty(
            document.getElementById('modalNewAppointment'),
            document.getElementById('formNewAppointment'),
            ['modalNewClose', 'modalNewCancel', 'modalNewApptClose', 'modalNewApptCancel']
        );

        // ── Form dirty en modal Editar Cita ───────────────────────────────
        watchFormDirty(
            document.getElementById('modalEditAppointment'),
            document.getElementById('formEditAppointment'),
            ['modalEditClose', 'modalEditCancel']
        );
    };

    // Auto-inicializar cuando el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initCitasForms);
    } else {
        initCitasForms();
    }

    return {
        mapEstadoServerToClient,
        getStatusLabelAndClass,
        getAvatarColorClass,
        pickColorByInitials,
        getInitials,
        validateAppointmentTime,
        submitWithSpinner,
        bindFormSubmitSpinner,
        watchFormDirty,
        initCitasForms
    };
})();
