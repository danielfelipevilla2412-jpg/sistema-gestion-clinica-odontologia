document.querySelectorAll('input, textarea, select').forEach((element) => {
  const wrapper = element.closest('div.flex');
  if (!wrapper) return;

  element.addEventListener('focus', () => wrapper.classList.add('scale-[1.01]'));
  element.addEventListener('blur', () => wrapper.classList.remove('scale-[1.01]'));
});

const supportForm = document.getElementById('support-form');
const screenshotInput = document.getElementById('screenshot-input');
const uploadButton = document.getElementById('upload-screenshot-btn');
const uploadPreview = document.getElementById('upload-preview');
const uploadPreviewImage = document.getElementById('upload-preview-image');
const uploadFileName = document.getElementById('upload-file-name');
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
const chatbotModal = document.getElementById('chatbotModal');
const chatbotClose = document.getElementById('chatbotClose');
const openChatbotButton = document.getElementById('open-chatbot-btn');
const openChatInlineButton = document.getElementById('open-chat-inline-btn');
const guidesToggle = document.getElementById('guides-toggle');
const guidesMenu = document.getElementById('guides-menu');
const chatbotMessages = document.getElementById('chatbotMessages');
const chatbotForm = document.getElementById('chatbotForm');
const chatbotInput = document.getElementById('chatbotInput');
const typingIndicator = document.getElementById('typingIndicator');
const chatInput = chatbotInput;
const chatForm = chatbotForm;
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

if (uploadButton && screenshotInput) {
  uploadButton.addEventListener('click', () => screenshotInput.click());

  screenshotInput.addEventListener('change', (event) => {
    const file = event.target.files?.[0];
    if (!file) return;

    uploadFileName.textContent = file.name;
    uploadPreview.classList.remove('hidden');

    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (event) => {
        uploadPreviewImage.src = event.target.result;
        uploadPreviewImage.classList.remove('hidden');
      };
      reader.readAsDataURL(file);
    } else {
      uploadPreviewImage.classList.add('hidden');
    }

    uploadButton.innerHTML = '<span class="material-symbols-outlined">check_circle</span> Captura seleccionada';
    uploadButton.classList.remove('border-dashed', 'text-[#424750]');
    uploadButton.classList.add('border-[#22c55e]', 'bg-[#ecfdf3]', 'text-[#166534]');
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

function showChatbotMessage(message, sender = 'bot') {
  if (!chatbotMessages) return;

  const wrapper = document.createElement('div');
  wrapper.className = `st-message ${sender}`;
  wrapper.innerHTML = `
    <div class="st-message-avatar">${sender === 'user' ? 'T' : '🤖'}</div>
    <div class="st-message-bubble">
      <div class="st-message-content">${message}</div>
      <div class="st-message-time">${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div>
    </div>
  `;

  chatbotMessages.appendChild(wrapper);
  chatbotMessages.scrollTop = chatbotMessages.scrollHeight;
}

function openChatbot() {
  if (chatbotModal) {
    chatbotModal.hidden = false;
    chatbotModal.classList.add('is-open');
  }
  if (chatbotInput) chatbotInput.focus();
  if (!chatbotMessages?.children.length) {
    showChatbotMessage('Hola, ¿en qué puedo ayudarte hoy?');
  }
}

function closeChatbot() {
  if (chatbotModal) {
    chatbotModal.hidden = true;
    chatbotModal.classList.remove('is-open');
  }
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

if (openChatbotButton) {
  openChatbotButton.addEventListener('click', openChatbot);
}

if (openChatInlineButton) {
  openChatInlineButton.addEventListener('click', openChatbot);
}

if (chatbotClose) {
  chatbotClose.addEventListener('click', closeChatbot);
}

if (chatbotModal) {
  chatbotModal.addEventListener('click', (event) => {
    if (event.target === chatbotModal) closeChatbot();
  });
}

// Botones rápidos del chatbot
document.querySelectorAll('.quick-reply').forEach((btn) => {
  btn.addEventListener('click', () => {
    const q = btn.dataset.question || btn.dataset.q;
    if (q && chatInput && chatForm) {
      chatInput.value = q;
      chatForm.requestSubmit();
    }
  });
});

if (chatbotForm) {
  chatbotForm.addEventListener('submit', (event) => {
    event.preventDefault();
    const value = chatbotInput?.value?.trim();
    if (!value) return;

    showChatbotMessage(value, 'user');
    if (chatbotInput) chatbotInput.value = '';
    if (typingIndicator) typingIndicator.hidden = false;

    setTimeout(() => {
      if (typingIndicator) typingIndicator.hidden = true;
      const respuesta = value.toLowerCase().includes('cita')
        ? 'Puedes agendar una cita desde la sección de Citas del panel principal.'
        : value.toLowerCase().includes('pago')
          ? 'Aceptamos pagos por transferencia, tarjeta y efectivo en sucursal.'
          : value.toLowerCase().includes('servicio')
            ? 'Ofrecemos servicios odontológicos preventivos, restaurativos y de especialidad.'
            : value.toLowerCase().includes('ubic')
              ? 'Estamos ubicados en la ciudad principal y también atendemos por videollamada.'
              : 'Gracias por contactarnos. Un especialista te ayudará a resolver tu consulta.';
      showChatbotMessage(respuesta, 'bot');
    }, 700);
  });
}

if (cancelButton) {
  cancelButton.addEventListener('click', () => {
    resetSubmitState();
    resetAttachmentState();
    if (supportForm) supportForm.reset();
  });
}

if (supportForm) {
  supportForm.addEventListener('submit', (e) => {
    e.preventDefault();
    if (!submitButton) return;

    resetSubmitState();
    submitButton.innerHTML = '<span class="material-symbols-outlined animate-spin">sync</span> Procesando...';
    submitButton.classList.add('opacity-80', 'cursor-not-allowed');
    submitButton.disabled = true;

    submitTimer = setTimeout(() => {
      submitButton.innerHTML = '<span class="material-symbols-outlined">check_circle</span> Ticket Enviado';
      submitButton.classList.remove('bg-[#0060a8]');
      submitButton.classList.add('bg-[#16a34a]');
      resetTimer = setTimeout(() => {
        resetSubmitState();
        resetAttachmentState();
        supportForm.reset();
      }, 3000);
    }, 1500);
  });
}

// Mobile menu toggle functionality
const sidebar = document.getElementById('sidebar');
const overlay = document.getElementById('sidebar-overlay');
const mobileMenuBtn = document.getElementById('mobile-menu-btn');
const closeSidebarBtn = document.getElementById('close-sidebar-btn');

function openSidebar() {
  sidebar.classList.remove('-translate-x-full');
  overlay.classList.remove('hidden');
  setTimeout(() => {
    overlay.classList.remove('opacity-0');
    overlay.classList.add('opacity-100');
  }, 10);
}

function closeSidebar() {
  sidebar.classList.add('-translate-x-full');
  overlay.classList.remove('opacity-100');
  overlay.classList.add('opacity-0');
  setTimeout(() => {
    overlay.classList.add('hidden');
  }, 300);
}

if (mobileMenuBtn) {
  mobileMenuBtn.addEventListener('click', openSidebar);
}

if (closeSidebarBtn) {
  closeSidebarBtn.addEventListener('click', closeSidebar);
}

if (overlay) {
  overlay.addEventListener('click', closeSidebar);
}

