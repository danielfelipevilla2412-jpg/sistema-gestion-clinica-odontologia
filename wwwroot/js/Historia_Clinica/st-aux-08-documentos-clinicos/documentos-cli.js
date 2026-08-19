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
  let currentDocFilename = '';
  let currentDocTitle = '';

  // ── Cargar documentos reales del paciente ──
  // NOTA: el esquema actual no tiene una tabla de "documentos clínicos" (adjuntos,
  // radiografías, etc.), así que el endpoint devuelve un arreglo vacío real en vez
  // de los 3 documentos inventados que antes estaban fijos en este HTML.
  async function cargarDocumentos() {
    const emptyState = document.getElementById('docsEmptyState');
    try {
      const resp = await fetch('/historia-clinica/st-aux-08-documentos-clinicos/data', { headers: { 'Accept': 'application/json' } });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const documentos = await resp.json();
      if (!documentos.length) {
        if (emptyState) emptyState.style.display = 'block';
        return;
      }
      documentos.forEach(d => addDocumentToList(d.tipo, d.fecha, d.nombreArchivo, d.subidoPor));
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

  // ── Upload de documento ──
  uploadForm?.addEventListener('submit', (e) => {
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

    // NOTA: no existe todavía un endpoint de subida de archivos ni una tabla de
    // documentos clínicos en el esquema (ver comentario en Staux08DocumentosClinicosData
    // del controlador). Este botón agrega el documento a la lista visible como
    // confirmación local, pero AÚN NO se persiste en la base de datos.
    uploadBtn.disabled = true;
    uploadBtn.textContent = '⏳ Subiendo...';
    
    setTimeout(() => {
      // Agregar a la lista (solo en memoria; no hay persistencia real todavía)
      document.getElementById('docsEmptyState')?.style.setProperty('display', 'none');
      addDocumentToList(docType, docDate, selectedFile.name);
      // Reset form
      uploadForm.reset();
      removeFile();
      uploadBtn.disabled = false;
      uploadBtn.textContent = 'Subir documento';
      showToast('Documento agregado localmente (la subida a servidor aún no está implementada)', 'warning');
    }, 600);
  });

  function addDocumentToList(type, date, filename, subidoPor) {
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
    item.innerHTML = `
      <span class="doc-icon">${icon}</span>
      <div class="doc-info">
        <p class="doc-name">${type}</p>
        <p class="doc-meta">${fmtDate}${subidoPor ? ' · ' + subidoPor : ''}</p>
      </div>
      <button class="doc-eye" aria-label="Ver documento">👁️</button>
    `;
    
    // Agregar evento de vista previa
    item.querySelector('.doc-eye').addEventListener('click', () => {
      viewDocument(filename, type);
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
  window.viewDocument = function(filename, title) {
    currentDocFilename = filename;
    currentDocTitle = title;
    
    modalTitle.textContent = title;
    
    // Icono según extensión
    const isPDF = filename.toLowerCase().endsWith('.pdf');
    const isImage = filename.toLowerCase().match(/\.(jpg|jpeg|png|gif)$/);
    
    if (isPDF) {
      modalIcon.textContent = '📄';
      modalPreview.innerHTML = `
        <span class="placeholder-icon">📄</span>
        <p class="placeholder-text">${title}</p>
        <p class="placeholder-subtext">Archivo PDF</p>
      `;
    } else if (isImage) {
      modalIcon.textContent = '🖼️';
      modalPreview.innerHTML = `
        <span class="placeholder-icon">🖼️</span>
        <p class="placeholder-text">${title}</p>
        <p class="placeholder-subtext">Archivo de imagen</p>
      `;
    } else {
      modalIcon.textContent = '📎';
      modalPreview.innerHTML = `
        <span class="placeholder-icon">📎</span>
        <p class="placeholder-text">${title}</p>
        <p class="placeholder-subtext">${filename}</p>
      `;
    }
    
    modalBackdrop.classList.add('active');
    document.body.style.overflow = 'hidden';
  };

  function closeModal() {
    modalBackdrop.classList.remove('active');
    document.body.style.overflow = '';
  }

  modalCloseBtn?.addEventListener('click', closeModal);
  modalCloseBtn2?.addEventListener('click', closeModal);
  modalBackdrop?.addEventListener('click', (e) => {
    if (e.target === modalBackdrop) closeModal();
  });

  // ── Descargar documento ──
  // NOTA: no hay almacenamiento real de archivos todavía (ver comentario en
  // Staux08DocumentosClinicosData del controlador), así que no hay un archivo real
  // que descargar. Se deja el flujo visual pero informando la limitación real.
  modalDownloadBtn?.addEventListener('click', () => {
    showToast('La descarga de archivos aún no está disponible (no hay almacenamiento configurado)', 'warning');
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
    // Fecha por defecto
    const dateInput = document.getElementById('docDate');
    if (dateInput) dateInput.value = new Date().toISOString().split('T')[0];
  }
  init();
});