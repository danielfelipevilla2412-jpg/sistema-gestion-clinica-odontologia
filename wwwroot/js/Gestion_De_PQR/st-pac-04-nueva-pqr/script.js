// ===== LOGICA PROPIA DEL FORMULARIO PQR =====
// El chrome compartido (sidebar / hamburger / overlay) lo aportan _Layout + shared/sidebar.js
// y las notificaciones el ToastService de shared/toasts.js.

// Mapeo entre el tipo mostrado en las tarjetas y el valor esperado por el backend
const PQR_TYPE_MAP = { petition: 'peticion', complaint: 'queja', claim: 'reclamo' };
const PQR_TYPE_LABEL_KEY = { peticion: 'pqr.type_petition', queja: 'pqr.type_complaint', reclamo: 'pqr.type_claim' };
let selectedPqrType = 'petition';

// Traduce una clave si el diccionario la conoce; si no, devuelve el texto de respaldo.
const t = (key, fallback) => (window.I18N && window.I18N.has(key)) ? window.I18N.t(key) : fallback;
const pqrTypeLabel = (tipo) => tipo ? t(PQR_TYPE_LABEL_KEY[tipo], tipo) : '';

// Notificación mediante el servicio compartido de toasts provisto por el layout.
const showToast = (message, type = 'success') => {
    const notify = window.ToastService?.[type];
    if (typeof notify !== 'function') {
        console.error('[SmileTrack][PQR] El servicio compartido de notificaciones no está disponible.');
        return;
    }
    notify(message);
};

// Selecciona el tipo de solicitud en las tarjetas (listeners enlazados en initRequestTypes).
const selectRequestType = (element, type) => {
    // Remove selected class from all cards
    document.querySelectorAll('.request-card').forEach(card => {
        card.classList.remove('selected');
    });

    // Add selected class to clicked card
    element.classList.add('selected');

    // Store selected type
    selectedPqrType = type;
};

function initRequestTypes() {
    document.querySelectorAll('.request-card').forEach(card => {
        card.addEventListener('click', () => {
            selectRequestType(card, card.dataset.requestType || 'petition');
        });
    });
}

// Renderiza el listado de "Radicados recientes" con datos reales de la BD
function renderRecentPqrs() {
    const container = document.getElementById('recentPqrList');
    if (!container) return;

    const pqrs = Array.isArray(window.RAZOR_MIS_PQRS) ? window.RAZOR_MIS_PQRS : [];
    if (pqrs.length === 0) {
        container.innerHTML = `<div class="pqr-empty-state">${escapeHtml(t('pac_pqr.empty_recent', 'Aún no tienes solicitudes radicadas.'))}</div>`;
        return;
    }

    const badgeByStatus = {
        recibida: { label: t('pac_pqr.st_received', 'Recibido'), cls: 'process' },
        en_proceso: { label: t('pac_pqr.st_in_progress', 'En proceso'), cls: 'process' },
        resuelta: { label: t('pac_pqr.st_resolved', 'Resuelto'), cls: 'resolved' },
        cerrada: { label: t('pac_pqr.st_closed', 'Cerrado'), cls: 'closed' },
        rechazada: { label: t('pac_pqr.st_rejected', 'Rechazado'), cls: 'closed' }
    };

    container.innerHTML = pqrs.slice(0, 5).map(p => {
        const badge = badgeByStatus[p.Estado] || { label: p.Estado || t('pac_pqr.no_status', 'Sin estado'), cls: 'process' };
        const tipoLabel = pqrTypeLabel(p.Tipo);
        const fecha = p.FechaCreacion
            ? new Date(p.FechaCreacion).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' })
            : '';
        const tieneRespuesta = Boolean(p.Respuesta && p.Respuesta.trim());
        return `
            <div class="recent-item pqr-clickable" data-pqr-id="${p.IdPqr}">
                <div class="recent-code">PQR-${String(p.IdPqr).padStart(4, '0')}</div>
                <div class="recent-title">${escapeHtml(p.Asunto || '')}</div>
                <div class="recent-date">${escapeHtml(tipoLabel)} · ${fecha}</div>
                <span class="badge ${badge.cls}">${escapeHtml(badge.label)}</span>
                <div class="recent-response-status ${tieneRespuesta ? 'answered' : ''}">
                    <span class="material-symbols-outlined" aria-hidden="true">${tieneRespuesta ? 'check_circle' : 'schedule'}</span>
                    ${tieneRespuesta ? t('pac_pqr.has_response', 'Tiene respuesta') : t('pac_pqr.pending_response', 'Pendiente de respuesta')}
                </div>
                <button type="button" class="btn-view-pqr" data-pqr-id="${p.IdPqr}">${escapeHtml(t('pqr.view_details', 'Ver detalle'))}</button>
            </div>`;
    }).join('');

    container.querySelectorAll('.btn-view-pqr').forEach(button => {
        button.addEventListener('click', event => {
            event.stopPropagation();
            const pqr = pqrs.find(item => String(item.IdPqr) === button.dataset.pqrId);
            if (pqr) openPqrDetail(pqr);
        });
    });

    container.querySelectorAll('.pqr-clickable').forEach(item => {
        item.addEventListener('click', () => {
            const pqr = pqrs.find(entry => String(entry.IdPqr) === item.dataset.pqrId);
            if (pqr) openPqrDetail(pqr);
        });
    });
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

let pqrHistoryRequest = 0;

function openPqrDetail(pqr) {
    const overlay = document.getElementById('pqrDetailOverlay');
    if (!overlay) return;

    const statusMap = {
        recibida: { label: t('pac_pqr.st_received', 'Recibido'), cls: 'process' },
        en_proceso: { label: t('pac_pqr.st_in_progress', 'En proceso'), cls: 'process' },
        resuelta: { label: t('pac_pqr.st_resolved', 'Resuelto'), cls: 'resolved' },
        cerrada: { label: t('pac_pqr.st_closed', 'Cerrado'), cls: 'closed' },
        rechazada: { label: t('pac_pqr.st_rejected', 'Rechazado'), cls: 'closed' }
    };
    const status = statusMap[pqr.Estado] || { label: pqr.Estado || t('pac_pqr.no_status', 'Sin estado'), cls: 'process' };

    document.getElementById('pqrDetailTicket').textContent = `PQR-${String(pqr.IdPqr).padStart(4, '0')}`;
    document.getElementById('pqrDetailTitle').textContent = pqr.Asunto || t('pac_pqr.detail_title', 'Detalle de la PQR');
    const statusElement = document.getElementById('pqrDetailStatus');
    statusElement.textContent = status.label;
    statusElement.className = `badge ${status.cls}`;
    document.getElementById('pqrDetailMeta').textContent = `${pqrTypeLabel(pqr.Tipo)} · ${t('pac_pqr.filed_on', 'Radicada')} ${formatPqrDate(pqr.FechaCreacion)}`;
    document.getElementById('pqrDetailSubject').textContent = pqr.Asunto || t('pac_pqr.no_subject', 'Sin asunto');
    document.getElementById('pqrDetailDescription').textContent = pqr.Descripcion || t('pac_pqr.no_description', 'Sin descripción.');

    loadPqrResponses(pqr);

    overlay.hidden = false;
    document.body.classList.add('pqr-modal-open');
}

function closePqrDetail() {
    const overlay = document.getElementById('pqrDetailOverlay');
    if (!overlay) return;
    pqrHistoryRequest++;
    overlay.hidden = true;
    document.body.classList.remove('pqr-modal-open');
}

function renderPqrResponses(responses) {
    const list = document.getElementById('pqrDetailResponses');
    list.replaceChildren(...responses.map(response => {
        const item = document.createElement('li');
        const date = document.createElement('small');
        date.textContent = formatPqrDate(response.fecha);
        const text = document.createElement('p');
        text.textContent = response.texto;
        item.append(date, text);
        return item;
    }));
}

async function loadPqrResponses(pqr) {
    const request = ++pqrHistoryRequest;
    const status = document.getElementById('pqrDetailResponseStatus');
    const latest = pqr.Respuesta?.trim() ? [{ texto: pqr.Respuesta, fecha: pqr.FechaRespuesta }] : [];
    renderPqrResponses(latest);
    status.textContent = 'Cargando historial…';

    try {
        const response = await fetch(`/gestion-de-pqr/api/pqr/${encodeURIComponent(pqr.IdPqr)}/respuestas`, {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) throw new Error('No fue posible consultar las respuestas');
        const responses = await response.json();
        if (request !== pqrHistoryRequest) return;

        renderPqrResponses(responses);
        status.textContent = responses.length ? '' : 'Esta solicitud aún no tiene respuestas.';
    } catch {
        if (request === pqrHistoryRequest) status.textContent = 'No se pudo cargar el historial completo.';
    }
}

function formatPqrDate(value) {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return value;
    return date.toLocaleString('es-CO', {
        day: '2-digit', month: 'short', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

document.addEventListener('DOMContentLoaded', () => {
    initRequestTypes();
    renderRecentPqrs();
    const id = new URLSearchParams(location.search).get('pqr');
    const solicitud = window.RAZOR_MIS_PQRS.find(pqr => String(pqr.IdPqr) === id);
    if (solicitud) openPqrDetail(solicitud);
    document.getElementById('pqrDetailClose')?.addEventListener('click', closePqrDetail);
    document.getElementById('pqrDetailOverlay')?.addEventListener('click', event => {
        if (event.target.id === 'pqrDetailOverlay') closePqrDetail();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closePqrDetail();
    });
});

const pqrForm = document.getElementById('pqrForm');
const pqrFields = Array.from(pqrForm.querySelectorAll('input[name]:not([type="hidden"]), select[name], textarea[name]'));
const evidenceTypes = ['application/pdf', 'image/jpeg', 'image/png'];

function pqrFieldError(field) {
    const value = field.value.trim();
    if (field.required && !value) return 'Completa este campo.';
    if (value && field.minLength > 0 && value.length < field.minLength)
        return `Escribe al menos ${field.minLength} caracteres.`;
    if (field.validity.tooLong || (field.maxLength > 0 && value.length > field.maxLength))
        return `Usa hasta ${field.maxLength} caracteres.`;
    if (field.validity.rangeOverflow) return 'La fecha del servicio no puede ser futura.';
    if (field.validity.badInput) return 'Ingresa una fecha válida.';

    const file = field.files?.[0];
    if (file?.size > 5 * 1024 * 1024) return 'El archivo adjunto supera los 5 MB.';
    if (file && file.size === 0) return 'El archivo adjunto está vacío.';
    if (file && !evidenceTypes.includes(file.type)) return 'Adjunta un archivo PDF, JPG o PNG.';
    return '';
}

for (const field of pqrFields) {
    const error = document.createElement('small');
    error.id = `${field.id}Error`;
    error.className = 'pqr-field-error';
    error.setAttribute('aria-live', 'polite');
    field.closest('.form-group').append(error);
    const description = field.getAttribute('aria-describedby');
    field.setAttribute('aria-describedby', description ? `${description} ${error.id}` : error.id);
    for (const event of ['input', 'change', 'blur']) {
        field.addEventListener(event, () => showPqrFieldError(field, pqrFieldError(field)));
    }
}

function showPqrFieldError(field, message) {
    const error = document.getElementById(`${field.id}Error`);
    if (error.textContent !== message) error.textContent = message;
    if (message) field.setAttribute('aria-invalid', 'true');
    else field.removeAttribute('aria-invalid');
}

// Form Submission — envía la PQR real al backend (PqrController.CrearPqr)
pqrForm.addEventListener('submit', async function (e) {
    e.preventDefault();

    const firstInvalid = pqrFields.filter(field => {
        const message = pqrFieldError(field);
        showPqrFieldError(field, message);
        return Boolean(message);
    })[0];
    if (firstInvalid) {
        if (firstInvalid.hidden) fileUploadContainer?.focus();
        else firstInvalid.focus();
        return;
    }

    if (this.querySelector('.btn-submit').disabled) return;

    const submitBtn = this.querySelector('.btn-submit');
    const originalContent = submitBtn.innerHTML;
    submitBtn.innerHTML = `<span class="material-symbols-outlined spin" aria-hidden="true">progress_activity</span> ${t('pac_pqr.processing', 'Procesando...')}`;
    submitBtn.disabled = true;

    const formData = new FormData(this);
    const tipo = formData.get('tipo') || PQR_TYPE_MAP[selectedPqrType];
    const asunto = formData.get('asunto').trim();
    const descripcion = formData.get('descripcion').trim();
    formData.set('tipo', tipo);
    formData.set('asunto', asunto);
    formData.set('descripcion', descripcion);

    try {
        const response = await fetch(this.action, {
            method: 'POST',
            credentials: 'same-origin',
            body: formData
        });
        const result = await response.json();

        if (response.ok && result.success) {
            showToast(`${t('pac_pqr.success_filed', 'Solicitud radicada exitosamente. Radicado: ')}PQR-${String(result.id).padStart(4, '0')}`);
            this.reset();
            resetFileUpload();
            for (const field of pqrFields) showPqrFieldError(field, '');
            document.querySelector('.char-counter').textContent = '0 / 1000';

            // Reflejar inmediatamente la nueva solicitud en "Radicados recientes".
            window.RAZOR_MIS_PQRS.unshift({
                IdPqr: result.id,
                Asunto: asunto,
                Descripcion: descripcion,
                Tipo: tipo,
                Estado: 'recibida',
                FechaCreacion: new Date().toISOString()
            });
            renderRecentPqrs();
            document.querySelectorAll('.request-card').forEach(card => card.classList.remove('selected'));
            document.querySelector('.request-card.petition')?.classList.add('selected');
            selectedPqrType = 'petition';
        } else {
            showToast(result.message || t('pac_pqr.error_filed', 'No se pudo radicar la solicitud'), 'error');
        }
    } catch (err) {
        console.error('Error al radicar PQR:', err);
        showToast(t('pac_pqr.error_connection_filed', 'Error de conexión al radicar la solicitud'), 'error');
    } finally {
        submitBtn.innerHTML = originalContent;
        submitBtn.disabled = false;
    }
});

// ===== CARGA DE ARCHIVO (soporte adjunto) =====
const fileUploadContainer = document.getElementById('fileUpload');
const fileInput = document.getElementById('fileInput');

// El contenedor abre el selector nativo; el evento del propio input no debe reabrir el selector.
fileInput?.addEventListener('click', event => event.stopPropagation());
fileUploadContainer?.addEventListener('click', () => fileInput?.click());
fileUploadContainer?.addEventListener('keydown', event => {
    if (fileUploadContainer.tagName !== 'BUTTON' && event.target === fileUploadContainer && ['Enter', ' '].includes(event.key)) {
        event.preventDefault();
        fileInput?.click();
    }
});

function resetFileUpload(clearInput = true) {
    const input = document.getElementById('fileInput');
    if (input && clearInput) {
        input.value = '';
        showPqrFieldError(input, '');
    }

    const content = document.getElementById('fileUploadContent');
    if (!content) return;
    if (fileUploadContainer?.tagName === 'BUTTON') {
        content.replaceChildren();
        return;
    }
    content.innerHTML = `
        <span class="material-symbols-outlined" aria-hidden="true">cloud_upload</span>
        <div data-i18n="pac_pqr.upload_hint">Haz clic para adjuntar un archivo (PDF, JPG, PNG — máx. 5 MB)</div>
    `;
    window.I18N?.apply?.(content);
}

// File Upload Preview
fileInput?.addEventListener('change', function () {
    if (!fileUploadContainer) return;
    if (!this.files?.[0] || pqrFieldError(this)) {
        resetFileUpload(false);
        return;
    }
    if (this.files && this.files[0]) {
        const fileName = this.files[0].name;
        const fileSize = (this.files[0].size / 1024 / 1024).toFixed(2);

        const content = document.getElementById('fileUploadContent');
        if (!content) return;
        content.innerHTML = `
            <span class="material-symbols-outlined file-preview-icon" aria-hidden="true">check_circle</span>
            <div class="file-preview-name">${escapeHtml(fileName)}</div>
            <div class="file-preview-size">${fileSize} MB</div>
            <button type="button" class="file-remove">
                <span class="material-symbols-outlined" aria-hidden="true">delete</span> ${t('pac_pqr.remove_file', 'Eliminar archivo')}
            </button>
        `;
        content.querySelector('.file-remove')?.addEventListener('click', event => {
            event.stopPropagation();
            resetFileUpload();
            showPqrFieldError(fileInput, '');
            fileUploadContainer.focus();
        });
    }
});

// Character Counter for Textarea
const textarea = document.getElementById('pqrDescripcion');
textarea?.addEventListener('input', function () {
    const maxLength = 1000;
    const currentLength = this.value.length;
    const counter = this.parentElement.querySelector('.char-counter');
    if (!counter) return;
    counter.textContent = `${currentLength} / ${maxLength}`;
    counter.classList.toggle('is-over-limit', currentLength > maxLength);
});
