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

const initializeAgendaDashboard = () => {
    initWeekNavigation();
    initFilterOffice();
    initAppointmentModals();
    initNewAppointmentModal();
};

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAgendaDashboard);
} else {
    initializeAgendaDashboard();
}

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
    let selectedPatientId = null;

    // Escuchar clic en tarjetas de cita
    document.querySelectorAll('.appointment:not(.available)').forEach(card => {
        card.addEventListener('click', () => {
            selectedApptId = card.getAttribute('data-id');
            selectedPatientId = card.getAttribute('data-patient-id');
            const patientName = card.getAttribute('data-patient-name') || 'Paciente sin nombre';
            const serviceName = card.getAttribute('data-service-name') || 'Servicio no especificado';
            const officeName = card.getAttribute('data-office-name') || 'Sin consultorio';
            const dateStr = card.getAttribute('data-date') || '';
            const startTime = card.getAttribute('data-start-time') || '';
            const endTime = card.getAttribute('data-end-time') || '';
            const status = card.getAttribute('data-status') || 'Agendada';
            const notes = card.getAttribute('data-notes') || 'Sin observaciones.';
            const professionalName = card.getAttribute('data-professional-name') || '';
            const professionalEmail = card.getAttribute('data-professional-email') || '';
            const professionalPhone = card.getAttribute('data-professional-phone') || '';
            const professionalRegistry = card.getAttribute('data-professional-registry') || '';
            const professionalUserStatus = card.getAttribute('data-professional-user-status') || '';

            const extraProfesional = [];
            if (professionalEmail) extraProfesional.push(`<div><strong>Correo:</strong> ${escapeHtml(professionalEmail)}</div>`);
            if (professionalPhone) extraProfesional.push(`<div><strong>Teléfono:</strong> ${escapeHtml(professionalPhone)}</div>`);
            if (professionalRegistry) extraProfesional.push(`<div><strong>Registro médico:</strong> ${escapeHtml(professionalRegistry)}</div>`);
            if (professionalUserStatus) extraProfesional.push(`<div><strong>Estado cuenta:</strong> ${escapeHtml(professionalUserStatus)}</div>`);
            const bloqueProfesional = (professionalName || extraProfesional.length > 0)
                ? `<div style="padding:10px; background:var(--bg); border-radius:var(--radius-sm); font-size:0.85rem;">
                       ${professionalName ? `<div><strong>Profesional:</strong> ${escapeHtml(professionalName)}</div>` : ''}
                       ${extraProfesional.join('')}
                   </div>`
                : '';

            modalContent.innerHTML = `
                <div style="display:flex; flex-direction:column; gap:12px;">
                    <div style="background:var(--primary-light); padding:12px; border-radius:var(--radius-sm);">
                        <h3 style="font-size:1.1rem; color:var(--primary-dark); margin:0;">${escapeHtml(patientName)}</h3>
                        <p style="font-size:0.82rem; color:var(--text-muted); margin-top:2px;">
                            ${selectedPatientId ? `<strong>ID:</strong> ${escapeHtml(selectedPatientId)} · ` : ''}📅 ${escapeHtml(dateStr)} | ⏰ ${escapeHtml(startTime)} - ${escapeHtml(endTime)}
                        </p>
                    </div>
                    ${bloqueProfesional}
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

            const historialUrl = selectedPatientId ? `/historia-clinica/st-odo-03-historial?pacienteId=${encodeURIComponent(selectedPatientId)}&citaId=${encodeURIComponent(selectedApptId || '')}` : '/historia-clinica/st-odo-06-pacientes';
            const odontogramaUrl = selectedPatientId ? `/historia-clinica/st-odo-03-historial?pacienteId=${encodeURIComponent(selectedPatientId)}&citaId=${encodeURIComponent(selectedApptId || '')}` : '/historia-clinica/st-odo-06-pacientes';

            const btnVerHistorial = document.createElement('button');
            btnVerHistorial.type = 'button';
            btnVerHistorial.className = 'btn-secondary';
            btnVerHistorial.innerHTML = '<span class="material-symbols-outlined" aria-hidden="true">history</span> Ver historial';
            btnVerHistorial.addEventListener('click', () => {
                window.location.href = historialUrl;
            });

            const modalActions = modal.querySelector('.modal-actions-grid');
            if (modalActions) {
                const existingHistoryButton = modalActions.querySelector('[data-history-patient="true"]');
                if (existingHistoryButton) existingHistoryButton.remove();
                btnVerHistorial.setAttribute('data-history-patient', 'true');
                modalActions.insertBefore(btnVerHistorial, modalActions.firstChild);
            }

            // Configurar enlace "Iniciar Atención" hacia odontograma/historia clínica
            if (btnIniciarAtencion) {
                btnIniciarAtencion.onclick = (e) => {
                    e.preventDefault();
                    if (!puedeCambiarEstadoPorFecha(dateStr, 'En consulta')) {
                        showToast('⚠️ No puedes iniciar atención antes del día programado de la cita.', 'error');
                        return;
                    }
                    cambiarEstadoCita(selectedApptId, 'En consulta', () => {
                        window.location.href = historialUrl;
                    });
                };
            }

            modal.classList.add('open');
            modal.setAttribute('aria-hidden', 'false');
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
            if (!selectedApptId) return;
            const citaFecha = (document.querySelector('.appointment[data-id="' + selectedApptId + '"]') || document.querySelector('[data-id="' + selectedApptId + '"]'))?.getAttribute('data-date');
            if (!puedeCambiarEstadoPorFecha(citaFecha, 'Atendida')) {
                showToast('⚠️ Solo puedes marcar la cita como atendida desde el día programado o después.', 'error');
                return;
            }
            cambiarEstadoCita(selectedApptId, 'Atendida', () => {
                cerrarModal(modal);
                window.location.reload();
            });
        });
    }

    if (btnCancelar) {
        btnCancelar.addEventListener('click', () => {
            if (!selectedApptId) return;
            const citaFecha = (document.querySelector('.appointment[data-id="' + selectedApptId + '"]') || document.querySelector('[data-id="' + selectedApptId + '"]'))?.getAttribute('data-date');
            if (!puedeCambiarEstadoPorFecha(citaFecha, 'No asistió')) {
                showToast('⚠️ Solo puedes registrar inasistencia desde el día de la cita o después.', 'error');
                return;
            }
            cambiarEstadoCita(selectedApptId, 'No asistió', () => {
                cerrarModal(modal);
                window.location.reload();
            });
        });
    }
}

// ═══════════════════════════════════════════════════════════════════
// CAMBIAR ESTADO DE CITA VÍA API REST / FORM POST
// ═══════════════════════════════════════════════════════════════════
function puedeCambiarEstadoPorFecha(fechaCita, nuevoEstado) {
    if (!fechaCita) return false;

    const estadosBloqueados = ['Atendida', 'No asistió', 'No asistida'];
    const estado = String(nuevoEstado || '').trim();
    if (estado !== 'En consulta' && !estadosBloqueados.includes(estado)) {
        return true;
    }

    const fechaProgramada = new Date(`${fechaCita}T00:00:00`);
    const hoy = new Date();
    hoy.setHours(0, 0, 0, 0);

    if (fechaProgramada < hoy) {
        return false;
    }

    return true;
}

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
        modalNew.classList.add('open');
        modalNew.setAttribute('aria-hidden', 'false');
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
    modalEl.classList.remove('open');
    modalEl.setAttribute('aria-hidden', 'true');
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


/* ═══════════════════════════════════════════════════════════════
   MEJORAS 2026-09-15: FUNCIONALIDADES AVANZADAS AGENDA
   ═══════════════════════════════════════════════════════════════ */

/**
 * Sistema de Toggle de Vistas (Semana/Día/Lista)
 */
const inicializarToggleVistas = () => {
    const viewButtons = document.querySelectorAll('.view-btn');
    const calendarWrapper = document.querySelector('.calendar-scroll-wrapper');
    
    if (!viewButtons.length || !calendarWrapper) return;

    viewButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const view = btn.dataset.view;
            
            // Actualizar botones activos
            viewButtons.forEach(b => {
                b.classList.remove('active');
                b.setAttribute('aria-pressed', 'false');
            });
            btn.classList.add('active');
            btn.setAttribute('aria-pressed', 'true');
            
            // Cambiar vista del calendario
            calendarWrapper.dataset.view = view;
            
            // Guardar preferencia en localStorage
            localStorage.setItem('agendaView', view);
            
            // Anunciar cambio para lectores de pantalla
            anunciarCambio(`Vista cambiada a ${btn.textContent.trim()}`);
            
            console.log(`📅 Vista cambiada a: ${view}`);
        });
    });

    // Restaurar vista guardada
    const savedView = localStorage.getItem('agendaView');
    if (savedView) {
        const savedBtn = document.querySelector(`.view-btn[data-view="${savedView}"]`);
        if (savedBtn) {
            savedBtn.click();
        }
    }
};

/**
 * Búsqueda en Tiempo Real de Pacientes
 */
const inicializarBusquedaPacientes = () => {
    const searchInput = document.getElementById('searchPatient');
    if (!searchInput) return;

    let searchTimeout;

    searchInput.addEventListener('input', (e) => {
        clearTimeout(searchTimeout);
        
        searchTimeout = setTimeout(() => {
            const query = e.target.value.toLowerCase().trim();
            const appointments = document.querySelectorAll('.appointment:not(.available)');
            let matchCount = 0;
            
            appointments.forEach(appt => {
                const paciente = (appt.dataset.patientName || '').toLowerCase();
                const servicio = (appt.dataset.serviceName || '').toLowerCase();
                const notas = (appt.dataset.notes || '').toLowerCase();
                
                const matches = !query || 
                    paciente.includes(query) || 
                    servicio.includes(query) ||
                    notas.includes(query);
                
                if (matches) {
                    appt.classList.remove('search-hidden');
                    if (query) {
                        appt.classList.add('search-match');
                        matchCount++;
                    } else {
                        appt.classList.remove('search-match');
                    }
                } else {
                    appt.classList.add('search-hidden');
                    appt.classList.remove('search-match');
                }
            });
            
            // Mostrar resultado de búsqueda
            if (query && matchCount === 0) {
                mostrarToast('No se encontraron citas con ese criterio', 'info');
            } else if (query) {
                anunciarCambio(`${matchCount} cita${matchCount !== 1 ? 's' : ''} encontrada${matchCount !== 1 ? 's' : ''}`);
            }
            
            console.log(`🔍 Búsqueda: "${query}" - ${matchCount} resultados`);
        }, 300);
    });

    // Limpiar búsqueda con Escape
    searchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            searchInput.value = '';
            searchInput.dispatchEvent(new Event('input'));
            searchInput.blur();
        }
    });
};

/**
 * Filtro por Estado de Cita
 */
const inicializarFiltroEstado = () => {
    const filterStatus = document.getElementById('filterStatus');
    if (!filterStatus) return;

    filterStatus.addEventListener('change', (e) => {
        const estado = e.target.value.toLowerCase();
        const appointments = document.querySelectorAll('.appointment:not(.available)');
        let visibleCount = 0;
        
        appointments.forEach(appt => {
            const apptStatus = (appt.dataset.status || '').toLowerCase();
            
            if (!estado || apptStatus.includes(estado)) {
                appt.style.display = '';
                visibleCount++;
            } else {
                appt.style.display = 'none';
            }
        });
        
        // Anunciar filtrado
        const estadoTexto = estado || 'todos los estados';
        anunciarCambio(`Mostrando ${visibleCount} citas con estado: ${estadoTexto}`);
        
        console.log(`🎯 Filtro de estado: ${estadoTexto} - ${visibleCount} citas`);
    });
};

/**
 * Indicadores de Tiempo Real en Citas
 */
const actualizarIndicadoresTiempo = () => {
    const ahora = new Date();
    const appointments = document.querySelectorAll('.appointment[data-start-time]');
    let citasActualizadas = 0;
    
    appointments.forEach(appt => {
        const fecha = appt.dataset.date;
        const horaInicio = appt.dataset.startTime;
        const horaFin = appt.dataset.endTime;
        const estado = appt.dataset.status;
        
        if (!fecha || !horaInicio || !horaFin) return;
        
        const inicio = new Date(`${fecha}T${horaInicio}`);
        const fin = new Date(`${fecha}T${horaFin}`);
        
        // Remover clases anteriores
        appt.classList.remove('in-progress', 'upcoming', 'overdue');
        
        const indicator = appt.querySelector('.appt-status-indicator');
        if (!indicator) return;
        
        // EN CURSO
        if (ahora >= inicio && ahora <= fin) {
            appt.classList.add('in-progress');
            indicator.textContent = 'EN CURSO';
            indicator.setAttribute('aria-label', 'Cita en curso');
            citasActualizadas++;
        }
        // PRÓXIMA (menos de 15 minutos)
        else if (ahora < inicio) {
            const minutos = Math.floor((inicio - ahora) / 60000);
            if (minutos <= 15) {
                appt.classList.add('upcoming');
                indicator.textContent = `En ${minutos} min`;
                indicator.setAttribute('aria-label', `Cita en ${minutos} minutos`);
                citasActualizadas++;
            }
        }
        // RETRASADA (pasó la hora y no está atendida)
        else if (ahora > fin && !['atendida', 'cancelada', 'completada'].includes(estado.toLowerCase())) {
            appt.classList.add('overdue');
            indicator.textContent = 'RETRASADA';
            indicator.setAttribute('aria-label', 'Cita retrasada');
            citasActualizadas++;
        }
    });
    
    if (citasActualizadas > 0) {
        console.log(`⏰ ${citasActualizadas} indicadores de tiempo actualizados`);
    }
};

/**
 * Drag & Drop para Reagendar Citas
 */
const inicializarDragAndDrop = () => {
    let draggedAppointment = null;

    // Hacer citas arrastrables
    const appointments = document.querySelectorAll('.appointment[draggable="true"]');
    appointments.forEach(appt => {
        appt.addEventListener('dragstart', (e) => {
            draggedAppointment = e.target;
            e.target.classList.add('dragging');
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', e.target.dataset.id);
            
            console.log(`🔄 Arrastrando cita ID: ${e.target.dataset.id}`);
        });
        
        appt.addEventListener('dragend', (e) => {
            e.target.classList.remove('dragging');
            draggedAppointment = null;
        });
    });

    // Hacer días como drop targets
    const days = document.querySelectorAll('.calendar-day:not(.closed)');
    days.forEach(day => {
        day.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            day.classList.add('drag-over');
        });
        
        day.addEventListener('dragleave', (e) => {
            if (e.target === day) {
                day.classList.remove('drag-over');
            }
        });
        
        day.addEventListener('drop', async (e) => {
            e.preventDefault();
            day.classList.remove('drag-over');
            
            if (!draggedAppointment) return;
            
            const nuevaFecha = day.dataset.date;
            const citaId = draggedAppointment.dataset.id;
            const fechaOriginal = draggedAppointment.dataset.date;
            const paciente = draggedAppointment.dataset.patientName;
            const horaOriginal = draggedAppointment.dataset.startTime || '09:00';
            
            if (nuevaFecha === fechaOriginal) {
                mostrarToast('La cita ya está en esa fecha', 'info');
                return;
            }
            
            const confirmar = confirm(
                `¿Reagendar cita de ${paciente} para el ${formatearFecha(nuevaFecha)}?`
            );
            
            if (confirmar) {
                await reagendarCita(citaId, `${nuevaFecha}T${horaOriginal}:00`);
            }
        });
    });
};

/**
 * Reagendar Cita (API Call)
 */
const reagendarCita = async (citaId, fechaHora) => {
    try {
        mostrarLoading(true);
        
        const response = await fetch(`/api/citas/${citaId}/reagendar`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': obtenerAntiForgeryToken()
            },
            body: JSON.stringify({ fechaHora })
        });
        
        if (!response.ok) {
            throw new Error(`Error ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        
        mostrarToast('✅ Cita reagendada exitosamente', 'success');
        
        // Recargar la página después de un delay
        setTimeout(() => {
            window.location.reload();
        }, 1500);
        
        console.log('✅ Cita reagendada:', result);
        
    } catch (error) {
        console.error('❌ Error reagendando cita:', error);
        mostrarToast('Error al reagendar la cita. Intente nuevamente.', 'error');
    } finally {
        mostrarLoading(false);
    }
};

/**
 * Actualizar Estadísticas Dinámicamente
 */
const actualizarEstadisticas = () => {
    const appointments = document.querySelectorAll('.appointment:not(.available)');
    const stats = {
        total: 0,
        programada: 0,
        confirmada: 0,
        atendida: 0,
        cancelada: 0
    };
    
    appointments.forEach(appt => {
        if (appt.style.display === 'none') return; // No contar ocultos por filtros
        
        stats.total++;
        const estado = (appt.dataset.status || '').toLowerCase();
        
        if (estado.includes('programada') || estado.includes('agendada')) {
            stats.programada++;
        } else if (estado.includes('confirmada')) {
            stats.confirmada++;
        } else if (estado.includes('atendida')) {
            stats.atendida++;
        } else if (estado.includes('cancelada')) {
            stats.cancelada++;
        }
    });
    
    // Actualizar valores en el DOM si existen paneles de stats
    const updateStatValue = (selector, value) => {
        const el = document.querySelector(selector);
        if (el) el.textContent = value;
    };
    
    updateStatValue('.quick-stat-item[data-status="total"] strong', stats.total);
    updateStatValue('.quick-stat-item[data-status="programada"] strong', stats.programada);
    updateStatValue('.quick-stat-item[data-status="confirmada"] strong', stats.confirmada);
    updateStatValue('.quick-stat-item[data-status="atendida"] strong', stats.atendida);
    
    console.log('📊 Estadísticas actualizadas:', stats);
};

/**
 * Exportar Agenda a PDF
 */
const configurarExportacionPDF = () => {
    const btnExportPDF = document.getElementById('btnExportPDF');
    if (!btnExportPDF) return;
    
    btnExportPDF.addEventListener('click', async () => {
        const weekLabel = document.getElementById('weekLabel');
        const weekStart = weekLabel ? weekLabel.dataset.weekStart : null;
        
        if (!weekStart) {
            mostrarToast('Error obteniendo fecha de la semana', 'error');
            return;
        }
        
        const url = `/api/agenda/exportar-pdf?weekStart=${weekStart}`;
        window.open(url, '_blank');
        
        console.log('📄 Exportando agenda a PDF');
    });
};

/**
 * Atajos de Teclado
 */
const configurarAtajosTeclado = () => {
    document.addEventListener('keydown', (e) => {
        // Solo si no está en un input
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;
        
        // Ctrl/Cmd + F: Enfocar búsqueda
        if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
            e.preventDefault();
            const searchInput = document.getElementById('searchPatient');
            if (searchInput) {
                searchInput.focus();
                searchInput.select();
            }
        }
        
        // V: Cambiar vista
        if (e.key === 'v' || e.key === 'V') {
            const viewButtons = document.querySelectorAll('.view-btn');
            const activeBtn = document.querySelector('.view-btn.active');
            if (activeBtn && viewButtons.length > 0) {
                const currentIndex = Array.from(viewButtons).indexOf(activeBtn);
                const nextIndex = (currentIndex + 1) % viewButtons.length;
                viewButtons[nextIndex].click();
            }
        }
        
        // H: Ir a hoy
        if (e.key === 'h' || e.key === 'H') {
            const btnToday = document.getElementById('btnToday');
            if (btnToday) btnToday.click();
        }
        
        // Flecha izquierda: Semana anterior
        if (e.key === 'ArrowLeft' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            const btnPrev = document.getElementById('btnPrev');
            if (btnPrev) btnPrev.click();
        }
        
        // Flecha derecha: Semana siguiente
        if (e.key === 'ArrowRight' && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            const btnNext = document.getElementById('btnNext');
            if (btnNext) btnNext.click();
        }
    });
    
    console.log('⌨️  Atajos de teclado configurados: Ctrl+F (buscar), V (cambiar vista), H (hoy), Ctrl+← → (navegar semanas)');
};

/**
 * Funciones Auxiliares
 */
const formatearFecha = (fechaISO) => {
    const fecha = new Date(fechaISO + 'T00:00:00');
    return fecha.toLocaleDateString('es-CO', { 
        weekday: 'long', 
        day: 'numeric', 
        month: 'long' 
    });
};

const obtenerAntiForgeryToken = () => {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
};

const mostrarLoading = (show) => {
    const calendarBody = document.querySelector('.calendar-body');
    if (calendarBody) {
        calendarBody.classList.toggle('loading', show);
    }
};

const anunciarCambio = (mensaje) => {
    // Crear anuncio para lectores de pantalla
    let announcer = document.getElementById('live-announcer');
    if (!announcer) {
        announcer = document.createElement('div');
        announcer.id = 'live-announcer';
        announcer.setAttribute('role', 'status');
        announcer.setAttribute('aria-live', 'polite');
        announcer.setAttribute('aria-atomic', 'true');
        announcer.style.cssText = 'position:absolute;left:-10000px;width:1px;height:1px;overflow:hidden;';
        document.body.appendChild(announcer);
    }
    announcer.textContent = mensaje;
};

const mostrarToast = (mensaje, tipo = 'info') => {
    // Usar ToastService si está disponible
    if (window.ToastService) {
        window.ToastService[tipo](mensaje);
        return;
    }
    
    // Fallback: crear toast simple
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.textContent = mensaje;
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: ${tipo === 'success' ? '#22c55e' : tipo === 'error' ? '#ef4444' : '#3b82f6'};
        color: white;
        padding: 1rem 1.5rem;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        z-index: 10000;
        animation: slideIn 0.3s ease;
    `;
    
    document.body.appendChild(toast);
    
    setTimeout(() => {
        toast.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
};

/**
 * Inicialización de Todas las Mejoras
 */
const inicializarMejorasAgenda = () => {
    console.log('🚀 Inicializando mejoras de la agenda...');
    
    // Inicializar componentes
    inicializarToggleVistas();
    inicializarBusquedaPacientes();
    inicializarFiltroEstado();
    inicializarDragAndDrop();
    configurarExportacionPDF();
    configurarAtajosTeclado();
    
    // Actualizar indicadores de tiempo
    actualizarIndicadoresTiempo();
    setInterval(actualizarIndicadoresTiempo, 60000); // Cada minuto
    
    // Actualizar estadísticas
    actualizarEstadisticas();
    
    // Agregar estilos de animación si no existen
    if (!document.getElementById('agenda-animations')) {
        const style = document.createElement('style');
        style.id = 'agenda-animations';
        style.textContent = `
            @keyframes slideIn {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
            @keyframes slideOut {
                from { transform: translateX(0); opacity: 1; }
                to { transform: translateX(100%); opacity: 0; }
            }
        `;
        document.head.appendChild(style);
    }
    
    console.log('✅ Agenda mejorada iniciada correctamente');
    console.log('📋 Funcionalidades disponibles:');
    console.log('   • Toggle de vistas (Semana/Día/Lista)');
    console.log('   • Búsqueda en tiempo real');
    console.log('   • Filtro por estado');
    console.log('   • Indicadores de tiempo real');
    console.log('   • Drag & Drop para reagendar');
    console.log('   • Atajos de teclado');
};

// Ejecutar mejoras después de la inicialización principal
document.addEventListener('DOMContentLoaded', () => {
    // Esperar a que termine la inicialización original
    setTimeout(inicializarMejorasAgenda, 1000);
});

// Limpiar al salir (para mejor rendimiento)
window.addEventListener('beforeunload', () => {
    document.querySelectorAll('[style*="animation"]').forEach(el => {
        el.style.animation = 'none';
    });
});
