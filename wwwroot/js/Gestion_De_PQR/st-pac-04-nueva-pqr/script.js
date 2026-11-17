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

    const responseElement = document.getElementById('pqrDetailResponse');
    const responseDate = document.getElementById('pqrDetailResponseDate');
    if (pqr.Respuesta && pqr.Respuesta.trim()) {
        responseElement.textContent = pqr.Respuesta;
        responseDate.textContent = pqr.FechaRespuesta
            ? `${t('pac_pqr.answered_on', 'Respondida el')} ${formatPqrDate(pqr.FechaRespuesta)}`
            : t('pac_pqr.response_registered', 'Respuesta registrada');
    } else {
        responseElement.textContent = t('pac_pqr.no_response_yet', 'Esta solicitud aún no tiene una respuesta registrada.');
        responseDate.textContent = '';
    }

    overlay.hidden = false;
    document.body.classList.add('pqr-modal-open');
}

function closePqrDetail() {
    const overlay = document.getElementById('pqrDetailOverlay');
    if (!overlay) return;
    overlay.hidden = true;
    document.body.classList.remove('pqr-modal-open');
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
    document.getElementById('pqrDetailClose')?.addEventListener('click', closePqrDetail);
    document.getElementById('pqrDetailOverlay')?.addEventListener('click', event => {
        if (event.target.id === 'pqrDetailOverlay') closePqrDetail();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closePqrDetail();
    });
});

// Lee el RequestToken antiforgery generado por Razor en la vista.
function getPqrAntiforgeryToken() {
    const meta = document.querySelector('meta[name="csrf-request-token"]');
    return meta?.getAttribute('content') || null;
}

// Form Submission — envía la PQR real al backend (PqrController.CrearPqr)
document.getElementById('pqrForm').addEventListener('submit', async function (e) {
    e.preventDefault();

    const submitBtn = this.querySelector('.btn-submit');
    const originalContent = submitBtn.innerHTML;
    submitBtn.innerHTML = `<span class="material-symbols-outlined spin" aria-hidden="true">progress_activity</span> ${t('pac_pqr.processing', 'Procesando...')}`;
    submitBtn.disabled = true;

    const asunto = document.getElementById('pqrAsunto')?.value?.trim() || t('pac_pqr.no_subject', 'Sin asunto');
    const descripcion = document.getElementById('pqrDescripcion')?.value?.trim() || '';
    const tipo = PQR_TYPE_MAP[selectedPqrType] || 'peticion';
    const archivo = document.getElementById('fileInput')?.files?.[0] || null;

    const formData = new FormData();
    formData.append('tipo', tipo);
    formData.append('asunto', asunto);
    formData.append('descripcion', descripcion);
    if (archivo) {
        formData.append('evidencia', archivo);
    }

    try {
        const csrfToken = getPqrAntiforgeryToken();
        const response = await fetch('/gestion-de-pqr/crear', {
            method: 'POST',
            credentials: 'same-origin',
            headers: csrfToken ? { 'X-CSRF-TOKEN': csrfToken } : {},
            body: formData
        });
        const result = await response.json();

        if (response.ok && result.success) {
            showToast(`${t('pac_pqr.success_filed', 'Solicitud radicada exitosamente. Radicado: ')}PQR-${String(result.id).padStart(4, '0')}`);
            this.reset();
            resetFileUpload();

            // Reflejar inmediatamente la nueva solicitud en "Radicados recientes".
            const recentList = document.getElementById('recentPqrList');
            if (recentList) {
                const tipoLabel = pqrTypeLabel(tipo);
                const nuevoItem = document.createElement('div');
                nuevoItem.className = 'recent-item';
                nuevoItem.innerHTML = `
                    <div class="recent-code">PQR-${String(result.id).padStart(4, '0')}</div>
                    <div class="recent-title">${asunto}</div>
                    <div class="recent-date">${tipoLabel} · ${t('common.today', 'Hoy')}</div>
                    <span class="badge process">${t('pac_pqr.st_received', 'Recibido')}</span>
                `;
                recentList.prepend(nuevoItem);
            }
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

function resetFileUpload() {
    const input = document.getElementById('fileInput');
    if (input) input.value = '';

    const content = document.getElementById('fileUploadContent');
    if (!content) return;
    content.innerHTML = `
        <span class="material-symbols-outlined" aria-hidden="true">cloud_upload</span>
        <div data-i18n="pac_pqr.upload_hint">Haz clic para adjuntar un archivo (PDF, JPG, PNG — máx. 5 MB)</div>
    `;
    window.I18N?.apply?.(content);
}

// File Upload Preview
fileInput?.addEventListener('change', function () {
    if (this.files && this.files[0]) {
        const fileName = this.files[0].name;
        const fileSize = (this.files[0].size / 1024 / 1024).toFixed(2);

        if (fileSize > 5) {
            showToast(t('pac_pqr.file_too_large', 'El archivo excede el tamaño máximo permitido (5 MB)'), 'error');
            this.value = '';
            return;
        }

        const content = document.getElementById('fileUploadContent');
        if (!content) return;
        content.innerHTML = `
            <span class="material-symbols-outlined file-preview-icon" aria-hidden="true">check_circle</span>
            <div class="file-preview-name">${fileName}</div>
            <div class="file-preview-size">${fileSize} MB</div>
            <button type="button" class="file-remove">
                <span class="material-symbols-outlined" aria-hidden="true">delete</span> ${t('pac_pqr.remove_file', 'Eliminar archivo')}
            </button>
        `;
        content.querySelector('.file-remove')?.addEventListener('click', event => {
            event.stopPropagation();
            resetFileUpload();
        });
    }
});

// Character Counter for Textarea
const textarea = document.querySelector('textarea');
textarea?.addEventListener('input', function () {
    const maxLength = 1000;
    const currentLength = this.value.length;
    const counter = this.parentElement.querySelector('.char-counter');
    if (!counter) return;
    counter.textContent = `${currentLength} / ${maxLength}`;
    counter.classList.toggle('is-over-limit', currentLength > maxLength);
});
