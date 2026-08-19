/**
 * SMILETRACK — HOMEPAGE INTERACTIVITY + MODALES
 */

document.addEventListener('DOMContentLoaded', () => {

    // ── Header scroll effect ──
    const header = document.getElementById('site-header');
    const headerBg = document.getElementById('header-bg');
    window.addEventListener('scroll', () => {
        if (window.scrollY > 10) {
            header.classList.add('shadow-md');
            headerBg.style.opacity = '0.95';
        } else {
            header.classList.remove('shadow-md');
            headerBg.style.opacity = '0.8';
        }
    });

    // ── Form → WhatsApp ──
    const contactForm = document.getElementById('contactForm');
    if (contactForm) {
        contactForm.addEventListener('submit', (e) => {
            e.preventDefault();
            const telefono = '573123627335';
            const mensaje = '¡Hola! Quiero agendar una cita dental en Smile Track.';
            window.open(`https://wa.me/${telefono}?text=${encodeURIComponent(mensaje)}`, '_blank');
        });
    }

    // ── Smooth scroll ──
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                e.preventDefault();
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
    });

    // ── Intersection Observer ──
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.1 });
    document.querySelectorAll('.service-card, .trust-item').forEach(el => {
        el.style.opacity = '0';
        el.style.transform = 'translateY(20px)';
        el.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
        observer.observe(el);
    });

    // ═══════════════════════════════════════════════════════════════
    //  MODALES: Chatbot + PQRS (CÓDIGO SIMPLIFICADO Y FUNCIONAL)
    // ═══════════════════════════════════════════════════════════════
    
    // Referencias directas por ID (más confiable)
    const chatModal = document.getElementById('chatbotModal');
    const pqrsModal = document.getElementById('pqrsModal');
    const openChatBtn = document.getElementById('openChatBtn');
    const openPqrsBtn = document.getElementById('openPqrsBtn');
    const chatClose = document.getElementById('chatbotClose');
    const pqrsClose = document.getElementById('pqrsClose');
    const chatForm = document.getElementById('chatbotForm');
    const chatInput = document.getElementById('chatbotInput');
    const chatMessages = document.getElementById('chatbotMessages');
    const typingIndicator = document.getElementById('typingIndicator');
    const pqrsForm = document.getElementById('pqrsForm');
    const pqrsSuccess = document.getElementById('pqrsSuccess');
    const pqrsTicket = document.getElementById('pqrsTicket');
    const pqrsResponseEmail = document.getElementById('pqrsResponseEmail');
    const toast = document.getElementById('toast');
    const toastMsg = document.getElementById('toastMsg');
    
    // Base de conocimiento del chatbot
    const CHAT_RESPONSES = {
        citas: { keywords: ['cita', 'agendar', 'reservar', 'hora'], reply: '📅 Para agendar: 1) Haz clic en "Agendar cita", 2) Completa el formulario, 3) Te confirmamos por WhatsApp.' },
        pagos: { keywords: ['pago', 'factura', 'tarjeta', 'efectivo'], reply: '💳 Aceptamos: Efectivo, tarjetas, Nequi, Daviplata. Consulta tus pagos en tu cuenta.' },
        servicios: { keywords: ['servicio', 'limpieza', 'blanqueamiento', 'ortodoncia'], reply: '🦷 Ofrecemos: Limpieza ($80k), Blanqueamiento ($350k), Ortodoncia y más. ¿Te interesa agendar?' },
        horarios: { keywords: ['horario', 'abre', 'cierra', 'lunes'], reply: '🕒 Lunes-Viernes: 8AM-6PM | Sábados: 9AM-2PM | Urgencias: 300 123 4567' },
        urgencias: { keywords: ['urgencia', 'dolor', 'emergencia'], reply: '🚨 Para urgencias: Llama YA al 300 123 4567 o acude a nuestra sede.' },
        ubicacion: { keywords: ['ubicación', 'dirección', 'dónde'], reply: '📍 Estamos en: Cra. 15 #93-47, Oficina 302, Bogotá. También ofrecemos consultas virtuales.' },
        fallback: { reply: '😅 No entendí. Pregúntame sobre: citas, pagos, servicios, horarios, urgencias o ubicación.' }
    };

    // Función para mostrar toast
    const showToast = (msg, type = 'success') => {
        if (!toast || !toastMsg) return;
        const icons = { success: '✅', error: '❌', warning: '⚠️' };
        toast.className = `toast ${type} show`;
        document.getElementById('toastIcon').textContent = icons[type] || '✅';
        toastMsg.textContent = msg;
        toast.hidden = false;
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => { toast.hidden = true; }, 200);
        }, 4000);
    };

    // Función para encontrar respuesta del chatbot
    const findChatReply = (text) => {
        const t = text.toLowerCase().trim();
        for (const [key, data] of Object.entries(CHAT_RESPONSES)) {
            if (key === 'fallback') continue;
            if (data.keywords.some(kw => t.includes(kw))) return data.reply;
        }
        return CHAT_RESPONSES.fallback.reply;
    };

    // Función para agregar mensaje al chat
    const addChatMessage = (text, isUser) => {
        if (!chatMessages) return;
        const time = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
        const msg = document.createElement('div');
        msg.className = `message ${isUser ? 'user' : 'bot'}`;
        msg.innerHTML = `
            <div class="message-avatar">${isUser ? '👤' : '🤖'}</div>
            <div class="message-content">
                <p>${text.replace(/\n/g, '<br>')}</p>
                <time class="message-time">${time}</time>
            </div>`;
        chatMessages.appendChild(msg);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    };

    // ── ABRIR/CERRAR MODALES ──
    const openModal = (modal) => { if (modal) { modal.hidden = false; document.body.style.overflow = 'hidden'; } };
    const closeModal = (modal) => { if (modal) { modal.hidden = true; document.body.style.overflow = ''; } };

    // Event listeners para abrir modales (selectores directos por ID)
    openChatBtn?.addEventListener('click', (e) => { e.preventDefault(); openModal(chatModal); 
        if (chatMessages.children.length === 0) addChatMessage('¡Hola! 👋 Soy tu asistente de SmileTrack. ¿En qué puedo ayudarte?', false);
        setTimeout(() => chatInput?.focus(), 100);
    });
    
    openPqrsBtn?.addEventListener('click', (e) => { e.preventDefault(); openModal(pqrsModal); 
        if (pqrsForm) pqrsForm.reset(); if (pqrsSuccess) pqrsSuccess.hidden = true; pqrsForm.hidden = false;
    });

    // También escuchar por data-open attribute (para enlaces en footer, etc.)
    document.querySelectorAll('[data-open="chatbot"]').forEach(btn => {
        if (btn !== openChatBtn) btn.addEventListener('click', (e) => { e.preventDefault(); openModal(chatModal); });
    });
    document.querySelectorAll('[data-open="pqrs"]').forEach(btn => {
        if (btn !== openPqrsBtn) btn.addEventListener('click', (e) => { e.preventDefault(); openModal(pqrsModal); });
    });

    // Cerrar modales
    chatClose?.addEventListener('click', () => closeModal(chatModal));
    pqrsClose?.addEventListener('click', () => closeModal(pqrsModal));
    document.querySelectorAll('.modal-overlay').forEach(overlay => {
        overlay.addEventListener('click', (e) => { if (e.target === overlay) { closeModal(chatModal); closeModal(pqrsModal); } });
    });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') { closeModal(chatModal); closeModal(pqrsModal); } });

    // ── CHATBOT LOGIC ──
    chatForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        const text = chatInput?.value.trim();
        if (!text) return;
        addChatMessage(text, true);
        if (chatInput) chatInput.value = '';
        if (typingIndicator) typingIndicator.hidden = false;
        setTimeout(() => {
            if (typingIndicator) typingIndicator.hidden = true;
            addChatMessage(findChatReply(text), false);
            if (text.toLowerCase().includes('urgencia') || text.toLowerCase().includes('dolor')) {
                showToast('🚨 Para emergencias, llama al 300 123 4567', 'warning');
            }
        }, 700);
    });

    // Botones rápidos del chatbot
    document.querySelectorAll('.quick-reply').forEach(btn => {
        btn.addEventListener('click', () => {
            const q = btn.dataset.question;
            if (q && chatInput) { chatInput.value = q; chatForm.requestSubmit(); }
        });
    });

    // ── PQRS FORM LOGIC ──
    pqrsForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const btn = pqrsForm.querySelector('button[type="submit"]');
        const btnText = btn?.querySelector('.btn-text');
        const btnLoading = btn?.querySelector('.btn-loading');
        
        try {
            if (btnText && btnLoading) { btnText.hidden = true; btnLoading.hidden = false; btn.disabled = true; }
            // Simular envío
            await new Promise(res => setTimeout(res, 1200));
            // Mostrar éxito
            if (pqrsForm) pqrsForm.hidden = true;
            if (pqrsSuccess) {
                pqrsSuccess.hidden = false;
                pqrsTicket.textContent = `ST-${new Date().getFullYear()}-${Math.random().toString(36).slice(2,6).toUpperCase()}`;
                pqrsResponseEmail.textContent = document.getElementById('pqrsEmail')?.value || 'tu@email.com';
            }
            showToast('✅ PQRS registrada exitosamente', 'success');
        } catch(err) {
            console.error('PQRS error', err);
            showToast('❌ Error al enviar. Intenta de nuevo.', 'error');
        } finally {
            if (btnText && btnLoading) { btnText.hidden = false; btnLoading.hidden = true; btn.disabled = false; }
        }
    });

    // PQRS: nueva solicitud / cancelar
    document.getElementById('pqrsNewRequest')?.addEventListener('click', () => {
        if (pqrsSuccess) pqrsSuccess.hidden = true;
        if (pqrsForm) { pqrsForm.hidden = false; pqrsForm.reset(); }
    });
    document.getElementById('pqrsCancel')?.addEventListener('click', () => {
        if (confirm('¿Cancelar esta solicitud?')) closeModal(pqrsModal);
    });

    // Toast close
    document.getElementById('toastClose')?.addEventListener('click', () => {
        if (toast) { toast.classList.remove('show'); setTimeout(() => { toast.hidden = true; }, 200); }
    });

    console.log('✅ SmileTrack Homepage: Chatbot + PQRS inicializados');
});