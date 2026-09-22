document.querySelectorAll('input, textarea, select').forEach((element) => {
  const wrapper = element.closest('div.flex');
  if (!wrapper) return;

  element.addEventListener('focus', () => wrapper.classList.add('scale-[1.01]'));
  element.addEventListener('blur', () => wrapper.classList.remove('scale-[1.01]'));
});

const supportForm = document.getElementById('support-form');
const screenshotInput = document.getElementById('screenshot-input');
const chooseFileButton = document.getElementById('choose-file-btn');
const uploadButton = document.getElementById('upload-screenshot-btn');
const uploadPreview = document.getElementById('upload-preview');
const uploadPreviewImage = document.getElementById('upload-preview-image');
const uploadFileName = document.getElementById('upload-file-name');
const attachmentModal = document.getElementById('attachment-modal');
const attachmentModalClose = document.getElementById('attachment-modal-close');
const attachmentModalError = document.getElementById('attachment-modal-error');
const cancelButton = document.getElementById('cancel-ticket-btn');
const submitButton = supportForm?.querySelector('button[type="submit"]');
const submitButtonDefaultClasses = submitButton?.className || '';
const submitButtonOriginalHTML = submitButton?.innerHTML || '';
const uploadButtonDefaultClasses = uploadButton?.className || '';
const urgencyCard = document.getElementById('urgency-card');
const urgencyPanel = document.getElementById('urgency-detail-panel');
const trackingCard = document.getElementById('tracking-card');
const trackingPanel = document.getElementById('tracking-detail-panel');
const contactCard = document.getElementById('contact-card');
const contactPanel = document.getElementById('contact-detail-panel');
const scheduleCard = document.getElementById('schedule-card');
const schedulePanel = document.getElementById('schedule-detail-panel');
const faqCard = document.getElementById('faq-card');
const faqPanel = document.getElementById('faq-detail-panel');
const guidesToggle = document.getElementById('guides-toggle');
const guidesMenu = document.getElementById('guides-menu');
let submitTimer = null;
let resetTimer = null;

function resetAttachmentState() {
  if (screenshotInput) screenshotInput.value = '';
  if (uploadPreview) uploadPreview.classList.add('hidden');
  if (uploadPreviewImage) {
    uploadPreviewImage.removeAttribute('src');
    uploadPreviewImage.classList.add('hidden');
  }
  if (uploadFileName) uploadFileName.textContent = '';

  if (uploadButton) {
    uploadButton.innerHTML = '<span class="material-symbols-outlined">attachment</span> Adjuntar Captura de Pantalla';
    uploadButton.className = uploadButtonDefaultClasses;
  }
}

function resetSubmitState() {
  if (submitTimer) {
    clearTimeout(submitTimer);
    submitTimer = null;
  }
  if (resetTimer) {
    clearTimeout(resetTimer);
    resetTimer = null;
  }

  if (submitButton) {
    submitButton.innerHTML = submitButtonOriginalHTML;
    submitButton.className = submitButtonDefaultClasses;
    submitButton.disabled = false;
  }
}

function closeAttachmentModal() {
  attachmentModal?.classList.add('hidden');
  if (attachmentModalError) {
    attachmentModalError.textContent = '';
    attachmentModalError.classList.add('hidden');
  }
}

function showAttachmentError(message) {
  if (!attachmentModalError) return;
  attachmentModalError.textContent = message;
  attachmentModalError.classList.remove('hidden');
}

function showSelectedAttachment(file) {
  if (!file || !screenshotInput || !uploadPreview || !uploadFileName) return;

  if (file.size > 10 * 1024 * 1024) {
    showAttachmentError('El archivo no puede superar 10 MB.');
    return;
  }

  const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
  uploadFileName.textContent = `${file.name} (${sizeMB} MB)`;
  uploadPreview.classList.remove('hidden');
  uploadPreviewImage?.classList.add('hidden');

  if (file.type.startsWith('image/') && uploadPreviewImage) {
    const reader = new FileReader();
    reader.onload = (event) => {
      uploadPreviewImage.src = event.target.result;
      uploadPreviewImage.classList.remove('hidden');
    };
    reader.readAsDataURL(file);
  }

  if (uploadButton) {
    uploadButton.innerHTML = `<span class="material-symbols-outlined text-[#166534]">check_circle</span> <span class="truncate max-w-[200px]">${file.name}</span>`;
    uploadButton.classList.remove('border-dashed', 'text-[#424750]');
    uploadButton.classList.add('border-[#22c55e]', 'bg-[#ecfdf3]', 'text-[#166534]');
  }

  closeAttachmentModal();
}

if (uploadButton && screenshotInput) {
  uploadButton.addEventListener('click', (event) => {
    event.preventDefault();
    event.stopPropagation();
    attachmentModal?.classList.remove('hidden');
  });

  attachmentModalClose?.addEventListener('click', closeAttachmentModal);
  attachmentModal?.addEventListener('click', (event) => {
    if (event.target === attachmentModal) closeAttachmentModal();
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && !attachmentModal?.classList.contains('hidden')) closeAttachmentModal();
  });

  screenshotInput.addEventListener('change', (event) => {
    const file = event.target.files?.[0];
    if (file) {
      showSelectedAttachment(file);
    }
  });
}

if (urgencyCard && urgencyPanel) {
  urgencyCard.addEventListener('click', () => {
    const isOpen = urgencyPanel.classList.contains('hidden');
    urgencyPanel.classList.toggle('hidden', !isOpen);
    urgencyCard.setAttribute('aria-expanded', String(isOpen));
  });
}

if (trackingCard && trackingPanel) {
  trackingCard.addEventListener('click', () => {
    const isOpen = trackingPanel.classList.contains('hidden');
    trackingPanel.classList.toggle('hidden', !isOpen);
    trackingCard.setAttribute('aria-expanded', String(isOpen));
  });
}

if (contactCard && contactPanel) {
  contactCard.addEventListener('click', () => {
    const isOpen = contactPanel.classList.contains('hidden');
    contactPanel.classList.toggle('hidden', !isOpen);
    contactCard.setAttribute('aria-expanded', String(isOpen));
  });
}

if (scheduleCard && schedulePanel) {
  scheduleCard.addEventListener('click', () => {
    const isOpen = schedulePanel.classList.contains('hidden');
    schedulePanel.classList.toggle('hidden', !isOpen);
    scheduleCard.setAttribute('aria-expanded', String(isOpen));
  });
}

if (faqCard && faqPanel) {
  faqCard.addEventListener('click', () => {
    const isOpen = faqPanel.classList.contains('hidden');
    faqPanel.classList.toggle('hidden', !isOpen);
    faqCard.setAttribute('aria-expanded', String(isOpen));
  });
}

if (guidesToggle && guidesMenu) {
  guidesToggle.addEventListener('click', (event) => {
    event.stopPropagation();
    guidesMenu.classList.toggle('hidden');
  });
}

document.querySelectorAll('.guide-trigger').forEach((button) => {
  button.addEventListener('click', () => {
    const targetId = button.getAttribute('data-target');
    const targetPanel = targetId ? document.getElementById(targetId) : null;
    const isExpanded = button.getAttribute('aria-expanded') === 'true';

    document.querySelectorAll('.guide-trigger').forEach((trigger) => {
      trigger.setAttribute('aria-expanded', 'false');
      trigger.querySelector('span:last-child')?.classList.remove('rotate-180');
      const panel = trigger.getAttribute('data-target') ? document.getElementById(trigger.getAttribute('data-target')) : null;
      if (panel) panel.classList.add('hidden');
    });

    if (!isExpanded && targetPanel) {
      button.setAttribute('aria-expanded', 'true');
      button.querySelector('span:last-child')?.classList.add('rotate-180');
      targetPanel.classList.remove('hidden');
    }
  });
});

document.addEventListener('click', (event) => {
  if (guidesMenu && !guidesMenu.contains(event.target) && guidesToggle && !guidesToggle.contains(event.target)) {
    guidesMenu.classList.add('hidden');
  }
});

if (cancelButton) {
  cancelButton.addEventListener('click', () => {
    resetSubmitState();
    resetAttachmentState();
    window.location.href = '/centro-de-ayuda/guias-tutoriales';
  });
}


