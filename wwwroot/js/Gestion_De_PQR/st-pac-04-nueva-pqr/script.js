// ===== CHROME COMPARTIDO (sidebar / hamburger / toast) =====
const safeGetElement = (id) => document.getElementById(id);

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast${type === 'error' ? ' error' : type === 'warning' ? ' warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
  };

  hamburger.addEventListener('click', () => toggleMenu(!sidebar.classList.contains('open')));
  overlay.addEventListener('click', () => toggleMenu(false));
};

document.addEventListener('DOMContentLoaded', initSidebar);


// ===== LOGICA PROPIA DEL FORMULARIO PQR =====

// Mapeo entre el tipo mostrado en las tarjetas y el valor esperado por el backend
const PQR_TYPE_MAP = { petition: 'peticion', complaint: 'queja', claim: 'reclamo' };
const PQR_TYPE_LABEL = { peticion: 'Petición', queja: 'Queja', reclamo: 'Reclamo' };
let selectedPqrType = 'petition';

// Select Request Type
function selectRequestType(element, type) {
    // Remove selected class from all cards
    document.querySelectorAll('.request-card').forEach(card => {
        card.classList.remove('selected');
    });

    // Add selected class to clicked card
    element.classList.add('selected');

    // Store selected type
    selectedPqrType = type;
}

// Renderiza el listado de "Radicados recientes" con datos reales de la BD
function renderRecentPqrs() {
    const container = document.getElementById('recentPqrList');
    if (!container) return;

    const pqrs = Array.isArray(window.RAZOR_MIS_PQRS) ? window.RAZOR_MIS_PQRS : [];
    if (pqrs.length === 0) {
        container.innerHTML = '<div class="pqr-empty-state">Aún no tienes solicitudes radicadas.</div>';
        return;
    }

    const badgeByStatus = {
        recibida: { label: 'Recibido', cls: 'process' },
        en_proceso: { label: 'En proceso', cls: 'process' },
        resuelta: { label: 'Resuelto', cls: 'resolved' },
        cerrada: { label: 'Cerrado', cls: 'closed' },
        rechazada: { label: 'Rechazado', cls: 'closed' }
    };

    container.innerHTML = pqrs.slice(0, 5).map(p => {
        const badge = badgeByStatus[p.Estado] || { label: p.Estado || 'Sin estado', cls: 'process' };
        const tipoLabel = PQR_TYPE_LABEL[p.Tipo] || p.Tipo || '';
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
                    <i class="fas ${tieneRespuesta ? 'fa-check-circle' : 'fa-clock'}"></i>
                    ${tieneRespuesta ? 'Tiene respuesta' : 'Pendiente de respuesta'}
                </div>
                <button type="button" class="btn-view-pqr" data-pqr-id="${p.IdPqr}">Ver detalle</button>
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
        recibida: { label: 'Recibido', cls: 'process' },
        en_proceso: { label: 'En proceso', cls: 'process' },
        resuelta: { label: 'Resuelto', cls: 'resolved' },
        cerrada: { label: 'Cerrado', cls: 'closed' },
        rechazada: { label: 'Rechazado', cls: 'closed' }
    };
    const status = statusMap[pqr.Estado] || { label: pqr.Estado || 'Sin estado', cls: 'process' };

    document.getElementById('pqrDetailTicket').textContent = `PQR-${String(pqr.IdPqr).padStart(4, '0')}`;
    document.getElementById('pqrDetailTitle').textContent = pqr.Asunto || 'Detalle de la PQR';
    const statusElement = document.getElementById('pqrDetailStatus');
    statusElement.textContent = status.label;
    statusElement.className = `badge ${status.cls}`;
    document.getElementById('pqrDetailMeta').textContent = `${PQR_TYPE_LABEL[pqr.Tipo] || pqr.Tipo || ''} · Radicada ${formatPqrDate(pqr.FechaCreacion)}`;
    document.getElementById('pqrDetailSubject').textContent = pqr.Asunto || 'Sin asunto';
    document.getElementById('pqrDetailDescription').textContent = pqr.Descripcion || 'Sin descripción.';

    const responseElement = document.getElementById('pqrDetailResponse');
    const responseDate = document.getElementById('pqrDetailResponseDate');
    if (pqr.Respuesta && pqr.Respuesta.trim()) {
        responseElement.textContent = pqr.Respuesta;
        responseDate.textContent = pqr.FechaRespuesta
            ? `Respondida el ${formatPqrDate(pqr.FechaRespuesta)}`
            : 'Respuesta registrada';
    } else {
        responseElement.textContent = 'Esta solicitud aún no tiene una respuesta registrada.';
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
    renderRecentPqrs();
    document.getElementById('pqrDetailClose')?.addEventListener('click', closePqrDetail);
    document.getElementById('pqrDetailOverlay')?.addEventListener('click', event => {
        if (event.target.id === 'pqrDetailOverlay') closePqrDetail();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closePqrDetail();
    });
});

// Lee el token CSRF de la cookie XSRF-TOKEN (config: Program.cs -> AddAntiforgery)
function getPqrAntiforgeryToken() {
    const match = document.cookie.match(/(^|; )XSRF-TOKEN=([^;]+)/);
    return match ? decodeURIComponent(match[2]) : null;
}

// Form Submission — envía la PQR real al backend (PqrController.CrearPqr)
document.getElementById('pqrForm').addEventListener('submit', async function (e) {
    e.preventDefault();

    const submitBtn = this.querySelector('.btn-submit');
    const originalContent = submitBtn.innerHTML;
    submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Procesando...';
    submitBtn.disabled = true;

    const asunto = document.getElementById('pqrAsunto')?.value?.trim() || 'Sin asunto';
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
            showToast(`Solicitud radicada exitosamente. Radicado: PQR-${String(result.id).padStart(4, '0')}`);
            this.reset();
            resetFileUpload();

            // Reflejar inmediatamente la nueva solicitud en "Radicados recientes".
            const recentList = document.getElementById('recentPqrList');
            if (recentList) {
                const tipoLabel = PQR_TYPE_LABEL[tipo] || tipo;
                const nuevoItem = document.createElement('div');
                nuevoItem.className = 'recent-item';
                nuevoItem.innerHTML = `
                    <div class="recent-code">PQR-${String(result.id).padStart(4, '0')}</div>
                    <div class="recent-title">${asunto}</div>
                    <div class="recent-date">${tipoLabel} · Hoy</div>
                    <span class="badge process">Recibido</span>
                `;
                recentList.prepend(nuevoItem);
            }
            document.querySelectorAll('.request-card').forEach(card => card.classList.remove('selected'));
            document.querySelector('.request-card.petition')?.classList.add('selected');
            selectedPqrType = 'petition';
        } else {
            showToast(result.message || 'No se pudo radicar la solicitud', 'error');
        }
    } catch (err) {
        console.error('Error al radicar PQR:', err);
        showToast('Error de conexión al radicar la solicitud', 'error');
    } finally {
        submitBtn.innerHTML = originalContent;
        submitBtn.disabled = false;
    }
});

// File Upload Preview
document.getElementById('fileInput').addEventListener('change', function (e) {
    if (this.files && this.files[0]) {
        const fileName = this.files[0].name;
        const fileSize = (this.files[0].size / 1024 / 1024).toFixed(2);

        if (fileSize > 5) {
            alert('El archivo excede el tamaño máximo permitido (5 MB)');
            this.value = '';
            return;
        }

        const uploadDiv = this.parentElement;
        uploadDiv.innerHTML = `
            <i class="fas fa-check-circle" style="color: var(--success-green);"></i>
            <div><strong>${fileName}</strong></div>
            <div style="font-size: 12px; color: var(--text-light);">${fileSize} MB</div>
            <div style="margin-top: 10px; color: var(--primary-blue); cursor: pointer;" onclick="resetFileUpload()">
                <i class="fas fa-trash"></i> Eliminar archivo
            </div>
        `;
    }
});

function resetFileUpload() {
    const fileInput = document.getElementById('fileInput');
    fileInput.value = '';
    fileInput.parentElement.innerHTML = `
        <i class="fas fa-cloud-upload-alt"></i>
        <div>Haz clic para adjuntar un archivo (PDF, JPG, PNG — máx. 5 MB)</div>
    `;
}

// Character Counter for Textarea
const textarea = document.querySelector('textarea');
textarea.addEventListener('input', function () {
    const maxLength = 1000;
    const currentLength = this.value.length;
    const counter = this.parentElement.querySelector('div[style*="text-align: right"]');
    counter.textContent = `${currentLength} / ${maxLength}`;

    if (currentLength > maxLength) {
        counter.style.color = 'var(--danger-red)';
    } else {
        counter.style.color = 'var(--text-light)';
    }
});
