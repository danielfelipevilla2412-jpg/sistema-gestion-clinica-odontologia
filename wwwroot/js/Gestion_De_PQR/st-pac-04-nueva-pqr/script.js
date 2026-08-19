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
    if (pqrs.length === 0) return; // conserva los ejemplos estáticos si el paciente aún no tiene PQR

    const badgeByStatus = {
        recibida: { label: 'Recibido', cls: 'process' },
        en_proceso: { label: 'En proceso', cls: 'process' },
        resuelta: { label: 'Resuelto', cls: 'resolved' },
        cerrada: { label: 'Cerrado', cls: 'closed' }
    };

    container.innerHTML = pqrs.slice(0, 5).map(p => {
        const badge = badgeByStatus[p.Estado] || { label: p.Estado, cls: 'process' };
        const tipoLabel = PQR_TYPE_LABEL[p.Tipo] || p.Tipo;
        const fecha = p.FechaCreacion ? new Date(p.FechaCreacion).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
        return `
            <div class="recent-item">
                <div class="recent-code">PQR-${String(p.IdPqr).padStart(4, '0')}</div>
                <div class="recent-title">${p.Asunto || ''}</div>
                <div class="recent-date">${tipoLabel} · ${fecha}</div>
                <span class="badge ${badge.cls}">${badge.label}</span>
            </div>`;
    }).join('');
}

document.addEventListener('DOMContentLoaded', renderRecentPqrs);

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

    const formData = new FormData();
    formData.append('tipo', tipo);
    formData.append('asunto', asunto);
    formData.append('descripcion', descripcion);

    try {
        const response = await fetch('/gestion-de-pqr/crear', {
            method: 'POST',
            credentials: 'same-origin',
            body: formData
        });
        const result = await response.json();

        if (response.ok && result.success) {
            alert(`¡Solicitud radicada exitosamente!\n\nNúmero de radicado: PQR-${String(result.id).padStart(4, '0')}`);
            this.reset();
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
