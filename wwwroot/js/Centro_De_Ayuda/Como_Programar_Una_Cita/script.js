// Simple interaction for the "Helpful" (¿Te resultó útil?) buttons
document.addEventListener('DOMContentLoaded', () => {
    const thumbButtons = document.querySelectorAll('.rating-button');
    const topicButtons = document.querySelectorAll('.related-topic-button');
    const modal = document.getElementById('topicModal');
    const modalTitle = document.getElementById('modalTitle');
    const modalDescription = document.getElementById('modalDescription');
    const modalAdvice = document.getElementById('modalAdvice');
    const modalCloseBtn = document.getElementById('modalCloseBtn');
    const prevButton = document.getElementById('prevButton');
    const ratingResponseBox = document.getElementById('ratingResponseBox');

    thumbButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            // Remove previous active classes from all rating buttons
            thumbButtons.forEach(b => {
                b.classList.remove('bg-white', 'bg-green-600', 'bg-red-600', 'text-white', 'text-green-600', 'text-error');
                const prevIcon = b.querySelector('.material-symbols-outlined');
                if (prevIcon) {
                    prevIcon.style.fontVariationSettings = "'FILL' 0";
                    prevIcon.style.color = '';
                }
            });
            
            const icon = btn.querySelector('.material-symbols-outlined');
            const isThumbUp = btn.dataset.rating === 'success';
            const isThumbDown = btn.dataset.rating === 'error';

            if (isThumbUp) {
                btn.classList.add('bg-green-600', 'text-white');
                if (icon) {
                    icon.style.color = '#ffffff';
                }
                if (ratingResponseBox) {
                    ratingResponseBox.textContent = 'Respuesta exitosa';
                    ratingResponseBox.classList.remove('hidden');
                    ratingResponseBox.classList.remove('bg-red-50', 'border-red-200', 'text-red-900');
                    ratingResponseBox.classList.add('bg-emerald-50', 'border-emerald-200', 'text-emerald-900');
                }
            } else if (isThumbDown) {
                btn.classList.add('bg-red-600', 'text-white');
                if (icon) {
                    icon.style.color = '#ffffff';
                }
                if (ratingResponseBox) {
                    ratingResponseBox.textContent = 'Valoración fallida';
                    ratingResponseBox.classList.remove('hidden');
                    ratingResponseBox.classList.remove('bg-emerald-50', 'border-emerald-200', 'text-emerald-900');
                    ratingResponseBox.classList.add('bg-red-50', 'border-red-200', 'text-red-900');
                }
            }

            if (icon) {
                icon.style.fontVariationSettings = "'FILL' 1";
            }

            if (ratingResponseBox) {
                setTimeout(() => {
                    ratingResponseBox.classList.add('hidden');
                }, 3000);
            }
        });
    });

    const topicText = {
        'Editar citas existentes': 'Aquí puedes consultar cómo editar una cita agendada en SmileTrack. Selecciona la cita, haz los cambios necesarios y guarda las actualizaciones.',
        'Cancelar o reprogramar': 'Aprende a cancelar o reprogramar una cita. Revisa los pasos para confirmar el cambio con el paciente y actualizar la agenda.',
        'Gestión de consultorios': 'Consulta las opciones de gestión de consultorios para asignar salas, horarios y profesionales en tu sistema.',
        'Envío de recordatorios': 'Descubre cómo enviar recordatorios automáticos por correo o SMS para que tus pacientes no olviden sus citas.',
        'Hablar con Soporte': 'Si necesitas asistencia, nuestro equipo de soporte te guiará paso a paso. Describe tu problema y te ayudaremos a resolverlo rápidamente.',
        'Guía paso a paso': '1. Ve al Panel de Recepción. 2. Haz clic en "+ Nueva Cita". 3. Completa los datos del paciente, profesional, servicio y horario. 4. Guarda la cita para finalizar.'
    };

    const topicAdvice = {
        'Editar citas existentes': 'Para una mejor experiencia, ten a mano el número de cita y la información del paciente antes de editar.',
        'Cancelar o reprogramar': 'Confirma la nueva fecha y notifica al paciente para evitar cancelaciones de último momento.',
        'Gestión de consultorios': 'Organiza los espacios y turnos con anticipación para que el equipo tenga una agenda clara.',
        'Envío de recordatorios': 'Activa los recordatorios y revisa el canal preferido del paciente para reducir ausencias.',
        'Hablar con Soporte': 'Al hablar con soporte, explica tu problema con claridad y menciona lo que ya intentaste para recibir ayuda más rápida.',
        'Guía paso a paso': 'Sigue los pasos en la guía y usa "Anterior" si quieres regresar a la vista previa.'
    };

    const showModal = (topicName) => {
        modalTitle.textContent = topicName;
        modalDescription.innerHTML = topicText[topicName] || 'Aquí puedes ver más detalles relacionados con el tema seleccionado.';
        modalAdvice.textContent = topicAdvice[topicName] || 'Selecciona la opción de soporte y explica brevemente tu problema para recibir ayuda más rápida.';
        modal.classList.remove('hidden');
        modal.classList.add('flex');
    };

    const closeModal = () => {
        modal.classList.remove('flex');
        modal.classList.add('hidden');
    };

    topicButtons.forEach(button => {
        button.addEventListener('click', () => {
            showModal(button.dataset.topic);
        });
    });

    const supportButton = document.getElementById('supportButton');
    if (supportButton) {
        supportButton.addEventListener('click', () => {
            showModal('Hablar con Soporte');
        });
    }

    if (prevButton) {
        prevButton.addEventListener('click', () => {
            closeModal();
        });
    }

    modalCloseBtn.addEventListener('click', closeModal);
    modal.addEventListener('click', (event) => {
        if (event.target === modal) {
            closeModal();
        }
    });
});
