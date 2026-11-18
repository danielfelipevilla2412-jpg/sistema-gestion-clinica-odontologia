// =============================================
// SMILETRACK — DOCUMENTOS CLÍNICOS (app.js)
// Funcionalidad completa: upload, preview, download
// =============================================

document.addEventListener('DOMContentLoaded', () => {

  // ── Referencias ──
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('overlay');
  const hamburger = document.getElementById('hamburger');
  const toast = document.getElementById('toast');
  
  const dropZone = document.getElementById('dropZone');
  const fileInput = document.getElementById('fileInput');
  const dropContent = document.getElementById('dropContent');
  const filePreview = document.getElementById('filePreview');
  const fileName = document.getElementById('fileName');
  const fileSize = document.getElementById('fileSize');
  const fileIcon = document.getElementById('fileIcon');
  const btnRemoveFile = document.getElementById('btnRemoveFile');
  
  const uploadForm = document.getElementById('uploadForm');
  const uploadBtn = document.getElementById('uploadBtn');
  const docsList = document.getElementById('docsList');
  
  const modalBackdrop = document.getElementById('modalBackdrop');
  const modalIcon = document.getElementById('modalIcon');
  const modalTitle = document.getElementById('modalTitle');
  const modalPreview = document.getElementById('modalPreview');
  const modalCloseBtn = document.getElementById('modalCloseBtn');
  const modalCloseBtn2 = document.getElementById('modalCloseBtn2');
  const modalDownloadBtn = document.getElementById('modalDownloadBtn');

  let selectedFile = null;
  let currentPreviewUrl = null;
  let previewAbortController = null;

  // ── Cargar documentos reales del paciente ──
  // Yeray (2025): antes devolvía siempre lista vacía porque no había tabla.
  // Ahora consulta /data?pacienteId=N que lee Documento_Clinico en BD.
  async function cargarDocumentos() {
    const emptyState = document.getElementById('docsEmptyState');
    // Leer pacienteId inyectado por el servidor en el ViewData
    const pacienteId = document.getElementById('pacienteIdInput')?.value || '';
    try {
      const url = '/historia-clinica/st-aux-08-documentos-clinicos/data' +
                  (pacienteId ? `?pacienteId=${encodeURIComponent(pacienteId)}` : '');
      const resp = await fetch(url, { headers: { 'Accept': 'application/json' } });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const documentos = await resp.json();
      if (!documentos.length) {
        if (emptyState) emptyState.style.display = 'block';
        return;
      }
      if (emptyState) emptyState.style.display = 'none';
      documentos.forEach(d => addDocumentToList(d.tipo, d.fecha, d.nombreArchivo, d.subidoPor, d.id));
    } catch (e) {
      console.error('No se pudieron cargar los documentos clínicos:', e);
      if (emptyState) {
        emptyState.textContent = 'No se pudieron cargar los documentos clínicos.';
        emptyState.style.display = 'block';
      }
    }
  }
  cargarDocumentos();

  // ── Sidebar móvil ──
  function toggleSidebar(show) {
    if (!sidebar || !overlay) return;
    if (show) {
      sidebar.classList.add('open');
      overlay.classList.add('open');
      hamburger.classList.add('open');
    } else {
      sidebar.classList.remove('open');
      overlay.classList.remove('open');
      hamburger.classList.remove('open');
    }
  }
  if (hamburger) hamburger.addEventListener('click', () => toggleSidebar(true));
  if (overlay) overlay.addEventListener('click', () => toggleSidebar(false));
  document.querySelectorAll('.nav-item').forEach(item => {
    item.addEventListener('click', () => { if (window.innerWidth <= 680) toggleSidebar(false); });
  });

  // ── Toast notifications ──
  function showToast(message, type = 'success') {
    toast.textContent = message;
    toast.className = `toast ${type === 'error' ? 'error' : type === 'warning' ? 'warning' : ''} show`;
    if (toast._tid) clearTimeout(toast._tid);
    toast._tid = setTimeout(() => toast.classList.remove('show'), 3000);
  }

  // ── Drag & Drop ──
  dropZone?.addEventListener('click', (e) => {
    if (!e.target.closest('.file-remove')) fileInput?.click();
  });
  dropZone?.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('dragover');
  });
  dropZone?.addEventListener('dragleave', () => {
    dropZone.classList.remove('dragover');
  });
  dropZone?.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('dragover');
    if (e.dataTransfer.files[0]) handleFile(e.dataTransfer.files[0]);
  });
  fileInput?.addEventListener('change', (e) => {
    if (e.target.files[0]) handleFile(e.target.files[0]);
  });
  btnRemoveFile?.addEventListener('click', (e) => {
    e.stopPropagation();
    removeFile();
  });

  function handleFile(file) {
    // Validar tamaño (10MB)
    if (file.size > 10 * 1024 * 1024) {
      showToast('El archivo excede 10MB', 'error');
      return;
    }
    // Validar tipo
    const allowed = ['image/jpeg','image/png','application/pdf'];
    if (!allowed.includes(file.type)) {
      showToast('Tipo no permitido (JPG/PNG/PDF)', 'error');
      return;
    }
    selectedFile = file;
    // Mostrar preview
    dropContent.style.display = 'none';
    filePreview.style.display = 'flex';
    dropZone.classList.add('has-file');
    fileName.textContent = file.name;
    fileSize.textContent = formatFileSize(file.size);
    fileIcon.textContent = getFileIcon(file.type);
  }

  function removeFile() {
    selectedFile = null;
    if (fileInput) fileInput.value = '';
    dropContent.style.display = 'flex';
    filePreview.style.display = 'none';
    dropZone.classList.remove('has-file');
  }

  function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024*1024) return (bytes/1024).toFixed(1) + ' KB';
    return (bytes/(1024*1024)).toFixed(1) + ' MB';
  }

  function getFileIcon(type) {
    if (type === 'application/pdf') return '📄';
    if (type.startsWith('image/')) return '🖼️';
    return '📎';
  }

  // ── Upload de documento — ahora persiste en el servidor ──
  // Yeray (2025): antes usaba un setTimeout() que solo agregaba el item localmente
  // y avisaba "la subida a servidor aún no está implementada". Ahora usa
  // multipart/form-data hacia /subir, que guarda el archivo en disco y los
  // metadatos en Documento_Clinico.
  uploadForm?.addEventListener('submit', async (e) => {
    e.preventDefault();
    
    // Validar campos
    const docType = document.getElementById('docType').value;
    const docDate = document.getElementById('docDate').value;
    
    let valid = true;
    if (!docType) {
      document.getElementById('docType').classList.add('error');
      valid = false;
    } else {
      document.getElementById('docType').classList.remove('error');
    }
    if (!docDate) {
      document.getElementById('docDate').classList.add('error');
      valid = false;
    } else {
      document.getElementById('docDate').classList.remove('error');
    }
    if (!selectedFile) {
      showToast('Selecciona un archivo', 'warning');
      valid = false;
    }
    if (!valid) return;

    // Construir FormData con el archivo y los metadatos
    const pacienteId = document.getElementById('pacienteIdInput')?.value || '';
    const token      = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const fd = new FormData();
    fd.append('pacienteId', pacienteId);
    fd.append('tipo',       docType);
    fd.append('fechaDocumento', docDate);
    fd.append('observacion', document.getElementById('docObs')?.value || '');
    fd.append('archivo',    selectedFile, selectedFile.name);

    uploadBtn.disabled    = true;
    uploadBtn.textContent = '⏳ Subiendo...';

    try {
      const resp = await fetch('/historia-clinica/st-aux-08-documentos-clinicos/subir', {
        method: 'POST',
        headers: { 'X-CSRF-TOKEN': token, 'RequestVerificationToken': token },
        body: fd
      });

      const data = await resp.json().catch(() => ({}));

      if (!resp.ok || data.success === false) {
        showToast(data.message || 'Error al subir el archivo', 'error');
        return;
      }

      // Agregar a la lista con los datos reales devueltos por el servidor
      document.getElementById('docsEmptyState')?.style.setProperty('display', 'none');
      addDocumentToList(data.tipo, data.fecha, data.nombreArchivo, null, data.id);

      // Reset form
      uploadForm.reset();
      establecerFechaDocumentoActual();
      removeFile();
      showToast('✅ Documento guardado correctamente', 'success');

    } catch (err) {
      console.error('Error al subir documento:', err);
      showToast('No se pudo conectar con el servidor', 'error');
    } finally {
      uploadBtn.disabled    = false;
      uploadBtn.textContent = 'Subir documento';
    }
  });

  function addDocumentToList(type, date, filename, subidoPor, docId) {
    // Formatear fecha
    const d = new Date(date + 'T00:00:00');
    const months = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    const fmtDate = `${String(d.getDate()).padStart(2,'0')} ${months[d.getMonth()]} ${d.getFullYear()}`;
    
    // Icono según tipo
    let icon = '📄';
    if (type.includes('Radiografía')) icon = '🦷';
    else if (type.includes('Fotografía')) icon = '📸';
    else if (type.includes('coagulación') || type.includes('Examen')) icon = '🧪';
    else if (type.includes('Consentimiento')) icon = '📝';
    
    // Crear item
    const item = document.createElement('div');
    item.className = 'doc-item';
    const iconElement = document.createElement('span');
    iconElement.className = 'doc-icon';
    iconElement.textContent = icon;

    const info = document.createElement('div');
    info.className = 'doc-info';

    const name = document.createElement('p');
    name.className = 'doc-name';
    name.textContent = type;

    const meta = document.createElement('p');
    meta.className = 'doc-meta';
    meta.textContent = `${fmtDate}${subidoPor ? ' · ' + subidoPor : ''}`;

    info.append(name, meta);

    const eye = document.createElement('button');
    eye.className = 'doc-eye';
    eye.setAttribute('aria-label', 'Ver documento');
    eye.textContent = '👁️';

    item.append(iconElement, info, eye);
    
    // Agregar evento de vista previa
    eye.addEventListener('click', () => {
      viewDocument(filename, type, docId);
    });
    
    // Insertar al inicio con animación
    item.style.opacity = '0';
    item.style.transform = 'translateY(-8px)';
    docsList?.prepend(item);
    requestAnimationFrame(() => {
      item.style.transition = 'opacity .2s, transform .2s';
      item.style.opacity = '1';
      item.style.transform = 'translateY(0)';
    });
  }

  // ── Modal de vista previa ──
  window.viewDocument = async function(filename, title, docId) {
    // Guardar el id para la descarga real
    modalDownloadBtn.dataset.docId = docId || '';
    
    modalTitle.textContent = title;
    modalBackdrop.classList.add('active');
    document.body.style.overflow = 'hidden';
    previewAbortController?.abort();
    previewAbortController = new AbortController();
    releasePreviewUrl();
    modalIcon.textContent = '📄';
    setPreviewPlaceholder('⏳', title, 'Cargando vista previa...');

    try {
      if (!docId) throw new Error('No se recibió el identificador del documento.');
      const response = await fetch(
        `/historia-clinica/documentos-clinicos/preview/${encodeURIComponent(docId)}`,
        {
          headers: { 'Accept': 'image/jpeg, image/png, application/pdf' },
          signal: previewAbortController.signal
        }
      );
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const blob = await response.blob();
      currentPreviewUrl = URL.createObjectURL(blob);

      if (blob.type === 'application/pdf') {
        modalIcon.textContent = '📄';
        const frame = document.createElement('iframe');
        frame.className = 'clinical-preview-frame';
        frame.title = `Vista previa: ${title}`;
        frame.src = currentPreviewUrl;
        modalPreview.replaceChildren(frame);
      } else if (blob.type === 'image/jpeg' || blob.type === 'image/png') {
        modalIcon.textContent = '🖼️';
        const image = document.createElement('img');
        image.className = 'clinical-preview-image';
        image.alt = `Vista previa: ${title}`;
        image.src = currentPreviewUrl;
        modalPreview.replaceChildren(image);
      } else {
        throw new Error('El tipo de archivo no permite vista previa.');
      }
    } catch (error) {
      if (error.name === 'AbortError') return;
      console.error('No se pudo cargar la vista previa del documento:', error);
      releasePreviewUrl();
      setPreviewPlaceholder('⚠️', title, 'No se pudo cargar la vista previa.');
    }
  };

  function setPreviewPlaceholder(icon, title, description) {
    const iconElement = document.createElement('span');
    iconElement.className = 'placeholder-icon';
    iconElement.textContent = icon;

    const titleElement = document.createElement('p');
    titleElement.className = 'placeholder-text';
    titleElement.textContent = title;

    const descriptionElement = document.createElement('p');
    descriptionElement.className = 'placeholder-subtext';
    descriptionElement.textContent = description;

    modalPreview.replaceChildren(iconElement, titleElement, descriptionElement);
  }

  function closeModal() {
    modalBackdrop.classList.remove('active');
    document.body.style.overflow = '';
    previewAbortController?.abort();
    previewAbortController = null;
    releasePreviewUrl();
  }

  function releasePreviewUrl() {
    if (!currentPreviewUrl) return;
    URL.revokeObjectURL(currentPreviewUrl);
    currentPreviewUrl = null;
  }

  modalCloseBtn?.addEventListener('click', closeModal);
  modalCloseBtn2?.addEventListener('click', closeModal);
  modalBackdrop?.addEventListener('click', (e) => {
    if (e.target === modalBackdrop) closeModal();
  });

  // ── Descargar documento — ahora usa el endpoint real de descarga ──
  // Yeray (2025): antes mostraba un toast de "la descarga no está disponible".
  // Ahora redirige al endpoint /descargar/{id} que sirve el archivo físico.
  modalDownloadBtn?.addEventListener('click', () => {
    const docId = modalDownloadBtn.dataset.docId;
    if (!docId) {
      showToast('No se puede descargar este documento', 'warning');
      return;
    }
    window.location.href = `/historia-clinica/documentos-clinicos/descargar/${docId}`;
  });

  // ── Keyboard: Escape cierra modal ──
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      closeModal();
      toggleSidebar(false);
    }
  });

  // ── Init ──
  function init() {
    toggleSidebar(false);
    establecerFechaDocumentoActual();
  }

  function establecerFechaDocumentoActual() {
    const dateInput = document.getElementById('docDate');
    if (!dateInput) return;
    const hoy = new Date();
    hoy.setMinutes(hoy.getMinutes() - hoy.getTimezoneOffset());
    dateInput.value = hoy.toISOString().slice(0, 10);
  }

  init();
});