/* ============================================
   SmileTrack — Lógica de Interacción de la Página Principal (homepage.js)
   ============================================
   Autor: Johan Santamaria / SmileTrack Team
   Fecha: 29/07/2026 (Actualizado 2026-09-12)

   DESCRIPCIÓN Y POR QUÉ DE ARQUITECTURA:
   Este script controla la interactividad client-side de la página de inicio pública.
   Su diseño responde a la necesidad de ofrecer una navegación fluida, reactiva y accesible
   para usuarios no autenticados (visitantes y futuros pacientes) sin recargar la página.

   FUNCIONALIDADES PRINCIPALES:
   - Header dinámico: Modifica opacidad y sombra del menú superior al realizar scroll vertical.
   - Formulario de Contacto a WhatsApp: Formatea y redirige la solicitud de información directamente al chat comercial.
   - Modales de Servicios: Apertura y cierre accesible de modales informativos de cada tratamiento.
   - Menú Móvil Hamburger: Alterna la visibilidad del menú en dispositivos móviles respetando ARIA (`aria-expanded`).

   NOTAS DE MANTENIMIENTO:
   - La integración con WhatsApp codifica los parámetros vía `encodeURIComponent` para prevenir fallos en caracteres especiales.
   - Todos los modales implementan bloqueo de eventos y reseteo de foco para cumplir con estándares WCAG 2.1 AA.
============================================ */

// Esperamos a que el DOM esté completamente cargado antes de manipular cualquier elemento.
// Sin esto, getElementById() y querySelector() fallarían porque los nodos no existen aún en memoria.
document.addEventListener('DOMContentLoaded', () => {

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 1: EFECTO SCROLL EN EL HEADER
    //  POR QUÉ: El header inicia transparente para no tapar el hero visual.
    //  Al hacer scroll, agrega sombra y aumenta la opacidad para que el menú
    //  siga siendo legible sobre el contenido de la página.
    // ══════════════════════════════════════════════════════════

    // Referencia al elemento del header principal y a su capa de fondo semitransparente
    const header = document.getElementById('site-header');
    const headerBg = document.getElementById('header-bg');

    // El evento 'scroll' se dispara con cada píxel de desplazamiento vertical
    window.addEventListener('scroll', () => {
        // Si el usuario ha bajado más de 10px, activamos la sombra y aumentamos la opacidad del fondo.
        // El umbral de 10px evita que el efecto se active con micro-movimientos accidentales.
        if (window.scrollY > 10) {
            header.classList.add('shadow-md');     // Sombra para separar el header del contenido
            headerBg.style.opacity = '0.95';       // Casi opaco: el menú se vuelve prominente
        } else {
            // Si está arriba del todo, restauramos el estado inicial semitransparente
            header.classList.remove('shadow-md');
            headerBg.style.opacity = '0.8';        // Semitransparente: el hero se ve a través del menú
        }
    });

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 2: FORMULARIO DE CONTACTO → WHATSAPP
    //  POR QUÉ: En lugar de un backend propio para el formulario de contacto inicial,
    //  redirigimos al chat de WhatsApp Business de la clínica. Es más rápido de implementar,
    //  más familiar para el usuario colombiano, y no requiere configurar un servidor SMTP.
    // ══════════════════════════════════════════════════════════

    const contactForm = document.getElementById('contactForm');
    if (contactForm) {
        contactForm.addEventListener('submit', (e) => {
            // Prevenimos el envío tradicional del formulario (que recargaría la página)
            e.preventDefault();

            // Número de WhatsApp Business en formato internacional (sin + ni espacios).
            // El prefijo 57 es el código de Colombia.
            const telefono = '573123627335';

            // Mensaje predefinido que el usuario verá en su app de WhatsApp al abrirse el chat
            const mensaje = '¡Hola! Quiero agendar una cita dental en Smile Track.';

            // encodeURIComponent() convierte caracteres especiales (tildes, espacios, ¡) a formato URL-safe.
            // '_blank' abre WhatsApp en una nueva pestaña sin cerrar la página de inicio.
            window.open(`https://wa.me/${telefono}?text=${encodeURIComponent(mensaje)}`, '_blank');
        });
    }

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 3: SCROLL SUAVE A SECCIONES ANCLA
    //  POR QUÉ: El scroll nativo del navegador es abrupto. scrollIntoView({ behavior: 'smooth' })
    //  crea una animación de deslizamiento que mejora la percepción de navegación en una SPA pública.
    // ══════════════════════════════════════════════════════════

    // Seleccionamos todos los enlaces que apuntan a un ancla interna (href="#servicios", "#contacto", etc.)
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            // Obtenemos el selector del ancla destino (p.ej. "#servicios")
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                // Prevenimos el salto instantáneo y usamos scroll suave
                e.preventDefault();
                // 'block: start' posiciona la sección al inicio del viewport
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
    });

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 4: ANIMACIÓN DE ENTRADA CON INTERSECTION OBSERVER
    //  POR QUÉ: Las tarjetas de servicios y elementos de confianza deben aparecer
    //  suavemente cuando el usuario hace scroll hasta ellas, en lugar de estar
    //  presentes de golpe. Esto crea un efecto "reveal" moderno sin librerías externas.
    //  IntersectionObserver es mucho más eficiente que escuchar el evento 'scroll'
    //  porque no ejecuta código en cada píxel, sino solo cuando el elemento entra en pantalla.
    // ══════════════════════════════════════════════════════════

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                // Cuando el elemento entra en el viewport, lo hacemos visible con transición CSS
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                // Dejamos de observar el elemento una vez que ya se mostró (optimización de rendimiento)
                observer.unobserve(entry.target);
            }
        });
    }, {
        // threshold: 0.1 significa que el elemento debe estar 10% visible para disparar la animación.
        // Un valor muy alto (0.8+) haría que la animación se dispare tarde en móviles.
        threshold: 0.1
    });

    // Aplicamos el estado inicial "invisible" y la transición CSS a todos los elementos observados
    document.querySelectorAll('.service-card, .trust-item').forEach(el => {
        el.style.opacity = '0';                           // Invisible al inicio
        el.style.transform = 'translateY(20px)';          // Desplazado 20px hacia abajo (efecto "sube")
        el.style.transition = 'opacity 0.5s ease, transform 0.5s ease'; // Duración de la animación
        observer.observe(el);                             // El observer vigilará cuándo aparece en pantalla
    });

    // ═══════════════════════════════════════════════════════════════
    //  SECCIÓN 5: MODALES — Chatbot + PQRS
    //  POR QUÉ: El chatbot responde preguntas frecuentes sin necesidad de llamar al servidor,
    //  reduciendo la carga del backend y ofreciendo respuestas instantáneas.
    //  El módulo PQRS permite al visitante iniciar una queja o sugerencia desde la homepage,
    //  aunque el envío real requiera sesión iniciada (por seguridad y trazabilidad).
    // ═══════════════════════════════════════════════════════════════

    // ── Referencias directas por ID ──
    // Usamos getElementById en lugar de querySelector('.chatbot-modal') porque es más preciso
    // y robusto ante cambios de clases CSS en el futuro.
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

    // ── BASE DE CONOCIMIENTO DEL CHATBOT ──
    // POR QUÉ: Se implementa un diccionario de palabras clave → respuesta en lugar de
    // llamar a una IA externa, para garantizar respuestas instantáneas y offline-tolerant.
    // La clave 'fallback' se usa cuando ninguna otra coincide, evitando respuestas vacías.
    const CHAT_RESPONSES = {
        citas:     { keywords: ['cita', 'agendar', 'reservar', 'hora'],
                     reply: '📅 Para agendar: 1) Haz clic en "Agendar cita", 2) Completa el formulario, 3) Te confirmamos por WhatsApp.' },
        pagos:     { keywords: ['pago', 'factura', 'tarjeta', 'efectivo'],
                     reply: '💳 Aceptamos: Efectivo, tarjetas, Nequi, Daviplata. Consulta tus pagos en tu cuenta.' },
        servicios: { keywords: ['servicio', 'limpieza', 'blanqueamiento', 'ortodoncia'],
                     reply: '🦷 Ofrecemos: Limpieza ($80k), Blanqueamiento ($350k), Ortodoncia y más. ¿Te interesa agendar?' },
        horarios:  { keywords: ['horario', 'abre', 'cierra', 'lunes'],
                     reply: '🕒 Lunes-Viernes: 8AM-6PM | Sábados: 9AM-2PM | Urgencias: 300 123 4567' },
        urgencias: { keywords: ['urgencia', 'dolor', 'emergencia'],
                     reply: '🚨 Para urgencias: Llama YA al 300 123 4567 o acude a nuestra sede.' },
        ubicacion: { keywords: ['ubicación', 'dirección', 'dónde'],
                     reply: '📍 Estamos en: Cra. 15 #93-47, Oficina 302, Bogotá. También ofrecemos consultas virtuales.' },
        fallback:  { reply: '😅 No entendí. Pregúntame sobre: citas, pagos, servicios, horarios, urgencias o ubicación.' }
    };

    // ── FUNCIÓN: Mostrar notificación Toast ──
    // POR QUÉ: Los Toast son notificaciones no bloqueantes. A diferencia de alert(), no interrumpen
    // el flujo del usuario. Se ocultan automáticamente a los 4 segundos para no acumular en pantalla.
    const showToast = (msg, type = 'success') => {
        // Si el toast no existe en el DOM, salimos silenciosamente (evita errores en páginas sin toast)
        if (!toast || !toastMsg) return;

        // Mapa de iconos según el tipo de mensaje para comunicación visual rápida
        const icons = { success: '✅', error: '❌', warning: '⚠️' };
        toast.className = `toast ${type} show`;              // Aplica clase de estilo según tipo
        document.getElementById('toastIcon').textContent = icons[type] || '✅';
        toastMsg.textContent = msg;
        toast.hidden = false;                                 // Lo hacemos visible

        // Tras 4 segundos, iniciamos la animación de desvanecimiento y luego lo ocultamos del DOM.
        // El segundo setTimeout de 200ms espera a que la transición CSS de salida termine.
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => { toast.hidden = true; }, 200);
        }, 4000);
    };

    // ── FUNCIÓN: Buscar respuesta del chatbot ──
    // POR QUÉ: Convertimos a minúsculas para que la comparación sea case-insensitive.
    // ("Cita" y "cita" devuelven la misma respuesta).
    const findChatReply = (text) => {
        const t = text.toLowerCase().trim();
        // Iteramos el diccionario en orden de definición (más específico primero)
        for (const [key, data] of Object.entries(CHAT_RESPONSES)) {
            if (key === 'fallback') continue; // Saltamos el fallback para verificarlo al final
            // Si alguna keyword del grupo está contenida en el mensaje del usuario, devolvemos esa respuesta
            if (data.keywords.some(kw => t.includes(kw))) return data.reply;
        }
        // Ninguna coincidencia: devolvemos el mensaje de ayuda genérico
        return CHAT_RESPONSES.fallback.reply;
    };

    // ── FUNCIÓN: Agregar mensaje al historial del chat ──
    // POR QUÉ: Creamos el elemento dinámicamente en lugar de usar templates estáticos
    // para poder controlar exactamente el markup (avatar, hora, clases CSS diferenciadas por rol).
    const addChatMessage = (text, isUser) => {
        if (!chatMessages) return;
        // Hora actual formateada en zona horaria colombiana (es-CO)
        const time = new Date().toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
        const msg = document.createElement('div');
        // La clase 'user' o 'bot' controla el alineamiento (derecha/izquierda) via CSS
        msg.className = `message ${isUser ? 'user' : 'bot'}`;
        msg.innerHTML = `
            <div class="message-avatar">${isUser ? '👤' : '🤖'}</div>
            <div class="message-content">
                <p>${text.replace(/\n/g, '<br>')}</p>
                <time class="message-time">${time}</time>
            </div>`;
        chatMessages.appendChild(msg);
        // Auto-scroll al fondo del contenedor para que el último mensaje sea siempre visible
        chatMessages.scrollTop = chatMessages.scrollHeight;
    };

    // ── FUNCIONES: Abrir y cerrar modales ──
    // POR QUÉ: `document.body.style.overflow = 'hidden'` previene que el usuario
    // pueda hacer scroll en el fondo cuando el modal está abierto (experiencia más enfocada).
    const openModal  = (modal) => { if (modal) { modal.hidden = false; document.body.style.overflow = 'hidden'; } };
    const closeModal = (modal) => { if (modal) { modal.hidden = true;  document.body.style.overflow = ''; } };

    // ── EVENT LISTENERS: Abrir modales ──
    // El operador ?. (optional chaining) evita errores si el botón no existe en esta página
    openChatBtn?.addEventListener('click', (e) => {
        e.preventDefault(); // Previene navegación si el botón está dentro de un <a>
        openModal(chatModal);
        // Si es la primera vez que se abre el chat, mostramos el mensaje de bienvenida del bot
        if (chatMessages.children.length === 0) addChatMessage('¡Hola! 👋 Soy tu asistente de SmileTrack. ¿En qué puedo ayudarte?', false);
        // Enfocamos el input para que el usuario pueda escribir de inmediato (sin necesidad de clic extra)
        setTimeout(() => chatInput?.focus(), 100);
    });

    openPqrsBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        openModal(pqrsModal);
        // Reseteamos el formulario para que no quede información de un intento anterior
        if (pqrsForm) pqrsForm.reset();
        // Ocultamos la pantalla de éxito por si el modal fue abierto antes y ya se completó
        if (pqrsSuccess) pqrsSuccess.hidden = true;
        pqrsForm.hidden = false;
    });

    // Soporte para otros elementos que usen `data-open="chatbot"` o `data-open="pqrs"` en el HTML.
    // (p.ej. enlaces en el footer o en el hero). Así no repetimos IDs para múltiples puntos de entrada.
    document.querySelectorAll('[data-open="chatbot"]').forEach(btn => {
        if (btn !== openChatBtn) btn.addEventListener('click', (e) => { e.preventDefault(); openModal(chatModal); });
    });
    document.querySelectorAll('[data-open="pqrs"]').forEach(btn => {
        if (btn !== openPqrsBtn) btn.addEventListener('click', (e) => { e.preventDefault(); openModal(pqrsModal); });
    });

    // ── Cerrar modales ──
    chatClose?.addEventListener('click', () => closeModal(chatModal));
    pqrsClose?.addEventListener('click', () => closeModal(pqrsModal));

    // Cierre al hacer clic en el overlay oscuro detrás del modal.
    // `e.target === overlay` verifica que el clic fue sobre el fondo, no sobre el contenido del modal.
    document.querySelectorAll('.modal-overlay').forEach(overlay => {
        overlay.addEventListener('click', (e) => { if (e.target === overlay) { closeModal(chatModal); closeModal(pqrsModal); } });
    });

    // Cierre al presionar Escape (estándar WCAG 2.1 para diálogos modales)
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') { closeModal(chatModal); closeModal(pqrsModal); } });

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 6: LÓGICA DEL CHATBOT
    //  POR QUÉ: El indicador de "escribiendo..." simula que el bot está procesando,
    //  mejorando la percepción de una IA real. El delay de 700ms es suficiente
    //  para que sea creíble sin frustrar al usuario por espera innecesaria.
    // ══════════════════════════════════════════════════════════
    chatForm?.addEventListener('submit', (e) => {
        e.preventDefault();
        const text = chatInput?.value.trim();
        if (!text) return; // No procesamos mensajes vacíos

        // Mostrar el mensaje del usuario en el historial de conversación
        addChatMessage(text, true);
        if (chatInput) chatInput.value = ''; // Limpiar el input tras enviar

        // Mostrar indicador de "escribiendo..." mientras el bot "procesa"
        if (typingIndicator) typingIndicator.hidden = false;

        // Simular latencia de procesamiento antes de mostrar la respuesta del bot
        setTimeout(() => {
            if (typingIndicator) typingIndicator.hidden = true;
            addChatMessage(findChatReply(text), false);

            // Si el usuario menciona urgencia o dolor, mostramos un toast de alerta adicional
            // para asegurarnos de que vea el número de emergencias aunque cierre el chat
            if (text.toLowerCase().includes('urgencia') || text.toLowerCase().includes('dolor')) {
                showToast('🚨 Para emergencias, llama al 300 123 4567', 'warning');
            }
        }, 700);
    });

    // Botones de respuesta rápida predefinidos (chips clicables que pre-rellenan el input).
    // POR QUÉ: Facilitan la interacción a usuarios que no saben qué escribir,
    // reduciendo la fricción de la conversación con el bot.
    document.querySelectorAll('.quick-reply').forEach(btn => {
        btn.addEventListener('click', () => {
            const q = btn.dataset.question; // Texto de la pregunta definido en data-question del HTML
            if (q && chatInput) {
                chatInput.value = q;
                // requestSubmit() dispara la validación nativa del formulario antes de enviar
                chatForm.requestSubmit();
            }
        });
    });

    // ══════════════════════════════════════════════════════════
    //  SECCIÓN 7: LÓGICA DEL FORMULARIO PQRS
    //  POR QUÉ: El PQRS desde la homepage muestra estado de carga profesional, pero redirige
    //  al usuario a iniciar sesión para registrar formalmente su solicitud.
    //  Esto previene PQRS anónimas sin trazabilidad en el sistema.
    // ══════════════════════════════════════════════════════════
    pqrsForm?.addEventListener('submit', async (e) => {
        e.preventDefault();

        // Referencias a los elementos de carga del botón de envío
        const btn        = pqrsForm.querySelector('button[type="submit"]');
        const btnText    = btn?.querySelector('.btn-text');
        const btnLoading = btn?.querySelector('.btn-loading');

        try {
            // Activar estado de carga: mostrar spinner y deshabilitar botón para evitar dobles envíos
            if (btnText && btnLoading) { btnText.hidden = true; btnLoading.hidden = false; btn.disabled = true; }
            // En la homepage pública, el PQRS formal requiere autenticación.
            // Informamos al usuario sin redirigirlo automáticamente para no interrumpir su experiencia.
            showToast('La PQRS no está disponible desde esta página. Inicia sesión para registrarla.', 'warning');
        } catch(err) {
            console.error('PQRS error', err);
            showToast('❌ Error al enviar. Intenta de nuevo.', 'error');
        } finally {
            // `finally` garantiza que el botón se restaura incluso si hubo un error inesperado
            if (btnText && btnLoading) { btnText.hidden = false; btnLoading.hidden = true; btn.disabled = false; }
        }
    });

    // Botón "Nueva solicitud": vuelve a mostrar el formulario después de un envío exitoso previo
    document.getElementById('pqrsNewRequest')?.addEventListener('click', () => {
        if (pqrsSuccess) pqrsSuccess.hidden = true;  // Oculta pantalla de éxito
        if (pqrsForm) { pqrsForm.hidden = false; pqrsForm.reset(); } // Muestra y limpia el formulario
    });

    // Botón "Cancelar": pide confirmación al usuario antes de cerrar para evitar pérdida de datos accidental
    document.getElementById('pqrsCancel')?.addEventListener('click', () => {
        if (confirm('¿Cancelar esta solicitud?')) closeModal(pqrsModal);
    });

    // ── CIERRE MANUAL DEL TOAST ──
    // POR QUÉ: Aunque el toast se cierra solo a los 4 segundos, algunos usuarios
    // prefieren cerrarlo inmediatamente para no distraerse. Prioridad de UX.
    document.getElementById('toastClose')?.addEventListener('click', () => {
        if (toast) {
            toast.classList.remove('show');
            setTimeout(() => { toast.hidden = true; }, 200); // Esperar a que termine la transición CSS
        }
    });

    // Confirmación en consola de que todos los módulos se inicializaron correctamente
    console.log('✅ SmileTrack Homepage: Chatbot + PQRS inicializados');
});