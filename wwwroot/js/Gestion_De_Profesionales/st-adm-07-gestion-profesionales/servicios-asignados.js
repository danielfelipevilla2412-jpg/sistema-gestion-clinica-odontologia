(() => {
    'use strict';

    const init = () => {
        const translate = (key) => window.I18N?.t(key) || key;
        const selector = document.getElementById('servicioId');
        const precio = document.getElementById('precioPersonalizado');
        const estado = document.getElementById('estadoSeleccionServicio');
        const boton = document.getElementById('guardarServicioAsignado');
        const profesionalSelector = document.getElementById('profesionalId');

        function actualizarFormulario() {
            if (!selector || !precio || !estado || !boton) return;
            const opcion = selector.selectedOptions[0];
            if (!selector.value || !opcion) {
                precio.value = '';
                estado.textContent = translate('asignados.select_service_hint');
                boton.textContent = translate('asignados.choose_service_button');
                boton.disabled = true;
                return;
            }
            const asignado = opcion.dataset.asignado === 'true';
            const activo = opcion.dataset.activo === 'true';
            // Cargar siempre el valor existente: vacío significa usar precio base.
            precio.value = asignado ? (opcion.dataset.precio || '') : '';
            const statusKey = !asignado
                ? 'asignados.new_assignment'
                : activo ? 'asignados.active_assignment' : 'asignados.inactive_assignment';
            estado.textContent = translate(statusKey);
            const buttonKey = !asignado
                ? 'asignados.assign_service'
                : activo ? 'asignados.update_assignment' : 'asignados.update_reactivate';
            boton.textContent = translate(buttonKey);
            boton.disabled = false;
        }

        const localizeProfessionalOptions = () => {
            if (!profesionalSelector) return;

            const statusKeys = {
                activo: 'gestionprofesionales_status_activo',
                vacaciones: 'gestionprofesionales_status_vacaciones',
                inactivo: 'gestionprofesionales_status_inactivo'
            };
            profesionalSelector.querySelectorAll('option[data-nombres]').forEach(option => {
                const rawStatus = option.dataset.estado || '';
                const statusKey = statusKeys[rawStatus.trim().toLowerCase()];
                const status = statusKey ? translate(statusKey) : rawStatus;
                option.textContent = `${option.dataset.nombres} ${option.dataset.apellidos} (${status})`;
            });
        };

        const localizeOptions = () => {
            if (!selector) return;
            selector.querySelectorAll('option[data-nombre]').forEach(option => {
                const stateKey = option.dataset.asignado !== 'true'
                    ? 'asignados.unassigned'
                    : option.dataset.activo === 'true'
                        ? 'asignados.already_active'
                        : 'asignados.already_inactive';
                option.textContent = `${option.dataset.nombre} — ${option.dataset.precioBase} · ${translate(stateKey)}`;
            });
        };

        selector?.addEventListener('change', actualizarFormulario);
        document.addEventListener('i18n:change', () => {
            localizeProfessionalOptions();
            localizeOptions();
            actualizarFormulario();
        });
        localizeProfessionalOptions();
        localizeOptions();
        actualizarFormulario();
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    } else {
        init();
    }
})();
