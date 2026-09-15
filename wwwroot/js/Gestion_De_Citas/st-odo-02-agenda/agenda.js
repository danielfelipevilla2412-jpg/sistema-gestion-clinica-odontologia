/* ============================================
   SmileTrack — Mi Agenda Odontólogo (st-odo-02-agenda)
   ============================================
   Autor: Johan Santamaria / Antigravity
   Fecha: 29/07/2026 (Actualizado 2026-09-12)
   DESCRIPCIÓN:
   Maneja la interactividad del grid semanal de la agenda del odontólogo,
   la navegación por semanas, filtrado por consultorio, modales de citas
   y transiciones de estado de atención en tiempo real conectadas a SQL Server.
============================================ */

document.addEventListener('DOMContentLoaded', () => {
    initWeekNavigation();
    initFilterOffice();
    initAppointmentModals();
    initNewAppointmentModal();
});

// ═══════════════════════════════════════════════════════════════════
// NAVEGACIÓN SEMANAL Y NAVEGACIÓN POR FECHAS
// ═══════════════════════════════════════════════════════════════════
function initWeekNavigation() {
    const btnPrev = document.getElementById('btnPrev');
    const btnNext = document.getElementById('btnNext');
    const btnToday = document.getElementById('btnToday');
    const weekLabel = document.getElementById('weekLabel');

    if (!weekLabel) return;

    const currentWeekStartStr = weekLabel.getAttribute('data-week-start');
    const currentWeekStart = currentWeekStartStr ? new Date(currentWeekStartStr) : new Date();

    if (btnPrev) {
        btnPrev.addEventListener('click', () => {
            const prevWeek = new Date(currentWeekStart);
            prevWeek.setDate(prevWeek.getDate() - 7);
            navigateWeek(prevWeek);
        });
    }

    if (btnNext) {
        btnNext.addEventListener('click', () => {
            const nextWeek = new Date(currentWeekStart);
            nextWeek.setDate(nextWeek.getDate() + 7);
            navigateWeek(nextWeek);
        });
    }

    if (btnToday) {
        btnToday.addEventListener('click', () => {
            navigateWeek(new Date());
        });
    }
}

function navigateWeek(date) {
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const dd = String(date.getDate()).padStart(2, '0');
    const dateStr = `${yyyy}-${mm}-${dd}`;

    const urlParams = new URLSearchParams(window.location.search);
    urlParams.set('weekStart', dateStr);
    window.location.search = urlParams.toString();
}

function initFilterOffice() {
    const filterOffice = document.getElementById('filterOffice');
    if (!filterOffice) return;

    filterOffice.addEventListener('change', (e) => {
        const urlParams = new URLSearchParams(window.location.search);
        if (e.target.value) {
            urlParams.set('officeId', e.target.value);
        } else {
            urlParams.delete('officeId');
        }
        window.location.search = urlParams.toString();
    });
}

// ═══════════════════════════════════════════════════════════════════
// MODAL DE DETALLE DE CITA Y ACCIONES CLÍNICAS RÁPIDAS
// ═══════════════════════════════════════════════════════════════════
function initAppointmentModals() {
    const modal = document.getElementById('modalAppointment');
    const modalClose = document.getElementById('modalApptClose');
    const modalContent = document.getElementById('modalApptContent');
    const btnIniciarAtencion = document.getElementById('btnIniciarAtencion');
    const btnMarcarAtendida = document.getElementById('btnMarcarAtendida');
    const btnCancelar = document.getElementById('modalApptCancelar');

    if (!modal || !modalContent) return;

    let selectedApptId = null;

    // Escuchar clic en tarjetas de cita
    document.querySelectorAll('.appointment:not(.available)').forEach(card => {
        card.addEventListener('click', () => {
            selectedApptId = card.getAttribute('data-id');
            const patientName = card.getAttribute('data-patient-name') || 'Paciente sin nombre';
            const serviceName = card.getAttribute('data-service-name') || 'Servicio no especificado';
            const officeName = card.getAttribute('data-office-name') || 'Sin consultorio';
            const dateStr = card.getAttribute('data-date') || '';
            const startTime = card.getAttribute('data-start-time') || '';
            const endTime = card.getAttribute('data-end-time') || '';
            const status = card.getAttribute('data-status') || 'Agendada';
            const notes = card.getAttribute('data-notes') || 'Sin observaciones.';

            modalContent.innerHTML = `
                <div style="display:flex; flex-direction:column; gap:12px;">
                    <div style="background:var(--primary-light); padding:12px; border-radius:var(--radius-sm);">
                        <h3 style="font-size:1.1rem; color:var(--primary-dark); margin:0;">${escapeHtml(patientName)}</h3>
                        <p style="font-size:0.82rem; color:var(--text-muted); margin-top:2px;">
                            📅 ${escapeHtml(dateStr)} | ⏰ ${escapeHtml(startTime)} - ${escapeHtml(endTime)}
                        </p>
                    </div>
                    <div style="display:grid; grid-template-columns:1fr 1fr; gap:10px; font-size:0.85rem;">
                        <div><strong>Servicio:</strong> ${escapeHtml(serviceName)}</div>
                        <div><strong>Consultorio:</strong> ${escapeHtml(officeName)}</div>
                        <div><strong>Estado actual:</strong> <span class="badge" style="font-weight:700;">${escapeHtml(status)}</span></div>
                    </div>
                    <div style="margin-top:4px;">
                        <strong>Observaciones previas:</strong>
                        <p style="font-size:0.83rem; color:var(--text-muted); background:var(--bg); padding:8px 12px; border-radius:var(--radius-sm); margin-top:4px;">
                            ${escapeHtml(notes)}
                        </p>
                    </div>
                </div>
            `;

            // Configurar enlace "Iniciar Atención" hacia odontograma/historia clínica
            if (btnIniciarAtencion) {
                btnIniciarAtencion.onclick = (e) => {
                    e.preventDefault();
                    cambiarEstadoCita(selectedApptId, 'En consulta', () => {
                        window.location.href = `/historia-clinica/st-odo-04-odontograma?citaId=${selectedApptId}`;
                    });
                };
            }

            modal.removeAttribute('hidden');
            modal.removeAttribute('inert');
        });
    });

    if (modalClose) {
        modalClose.addEventListener('click', () => cerrarModal(modal));
    }

    modal.addEventListener('click', (e) => {
        if (e.target === modal) cerrarModal(modal);
    });

    if (btnMarcarAtendida) {
        btnMarcarAtendida.addEventListener('click', () => {
            if (selectedApptId) cambiarEstadoCita(selectedApptId, 'Atendida', () => {
                cerrarModal(modal);
                window.location.reload();
            });
        });
    }

    if (btnCancelar) {
        btnCancelar.addEventListener('click', () => {
            if (selectedApptId) cambiarEstadoCita(selectedApptId, 'Cancelada', () => {
                cerrarModal(modal);
                window.location.reload();
            });
        });
    }
}

// ═══════════════════════════════════════════════════════════════════
// CAMBIAR ESTADO DE CITA VÍA API REST / FORM POST
// ═══════════════════════════════════════════════════════════════════
async function cambiarEstadoCita(idCita, nuevoEstado, callback) {
    if (!idCita) return;

    try {
        if (window.apiRequest) {
            const formData = new FormData();
            formData.append('IdCita', idCita);
            formData.append('Estado', nuevoEstado);

            await window.apiRequest('/gestion-de-citas/cambiar-estado', {
                method: 'POST',
                body: formData
            });

            showToast(`✅ Cita actualizada a '${nuevoEstado}' correctamente.`, 'success');
            if (callback) callback();
        } else {
            // Fallback mediante envío de formulario normal
            const form = document.createElement('form');
            form.method = 'POST';
            form.action = '/gestion-de-citas/cambiar-estado';

            const csrfToken = getAntiforgeryToken();
            if (csrfToken) {
                const inputCsrf = document.createElement('input');
                inputCsrf.type = 'hidden';
                inputCsrf.name = '__RequestVerificationToken';
                inputCsrf.value = csrfToken;
                form.appendChild(inputCsrf);
            }

            const inputId = document.createElement('input');
            inputId.type = 'hidden';
            inputId.name = 'IdCita';
            inputId.value = idCita;
            form.appendChild(inputId);

            const inputEst = document.createElement('input');
            inputEst.type = 'hidden';
            inputEst.name = 'Estado';
            inputEst.value = nuevoEstado;
            form.appendChild(inputEst);

            const inputRet = document.createElement('input');
            inputRet.type = 'hidden';
            inputRet.name = 'ReturnUrl';
            inputRet.value = window.location.pathname + window.location.search;
            form.appendChild(inputRet);

            document.body.appendChild(form);
            form.submit();
        }
    } catch (err) {
        console.error('Error al cambiar estado:', err);
        showToast('❌ Ocurrió un error al actualizar el estado de la cita.', 'error');
    }
}

// ═══════════════════════════════════════════════════════════════════
// MODAL DE NUEVA CITA / SOBRECUSPO
// ═══════════════════════════════════════════════════════════════════
function initNewAppointmentModal() {
    const fab = document.getElementById('btnNewAppointment');
    const modalNew = document.getElementById('modalNewAppointment');
    const modalNewClose = document.getElementById('modalNewApptClose');
    const modalNewCancel = document.getElementById('modalNewApptCancel');

    if (!modalNew) return;

    const abrirNuevoModal = (dateStr = '') => {
        if (dateStr) {
            const dateInput = document.getElementById('newApptDate');
            if (dateInput) dateInput.value = dateStr;
        }
        modalNew.removeAttribute('hidden');
        modalNew.removeAttribute('inert');
    };

    if (fab) fab.addEventListener('click', () => abrirNuevoModal());

    document.querySelectorAll('.appointment.available').forEach(slot => {
        slot.addEventListener('click', () => {
            const dateStr = slot.getAttribute('data-date');
            abrirNuevoModal(dateStr);
        });
    });

    if (modalNewClose) modalNewClose.addEventListener('click', () => cerrarModal(modalNew));
    if (modalNewCancel) modalNewCancel.addEventListener('click', () => cerrarModal(modalNew));

    modalNew.addEventListener('click', (e) => {
        if (e.target === modalNew) cerrarModal(modalNew);
    });
}

function cerrarModal(modalEl) {
    if (!modalEl) return;
    modalEl.setAttribute('hidden', 'true');
    modalEl.setAttribute('inert', 'true');
}

function getAntiforgeryToken() {
    const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
    return match ? decodeURIComponent(match[2]) : null;
}

function escapeHtml(text) {
    if (!text) return '';
    return String(text)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function showToast(message, type = 'success') {
    const toast = document.getElementById('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.className = `toast show ${type}`;
    setTimeout(() => {
        toast.classList.remove('show');
    }, 3500);
}
