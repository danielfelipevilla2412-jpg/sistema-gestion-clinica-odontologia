(function () {
    "use strict";

    const chatbot = document.getElementById("smiletrack-chatbot");
    if (!chatbot) return;

    const launcher = chatbot.querySelector(".chatbot__launcher");
    const panel = chatbot.querySelector(".chatbot__panel");
    const closeButton = chatbot.querySelector(".chatbot__close");
    const resetButton = chatbot.querySelector(".chatbot__reset");
    const messages = chatbot.querySelector("[data-chatbot-messages]");
    const suggestions = chatbot.querySelector("[data-chatbot-suggestions]");
    const form = chatbot.querySelector("[data-chatbot-form]");
    const input = form.querySelector("input");
    let initialized = false;
    const sessionKey = getChatbotSessionKey();

    function getChatbotSessionKey() {
        let key = "";
        try {
            key = window.sessionStorage.getItem("smiletrack-chatbot-session") || "";
        } catch {
            key = "";
        }
        if (!key) {
            key = (window.crypto && typeof window.crypto.randomUUID === "function")
                ? window.crypto.randomUUID().replace(/-/g, "")
                : `chat-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
            try {
                window.sessionStorage.setItem("smiletrack-chatbot-session", key);
            } catch {
                // The server also rejects an invalid or missing session header.
            }
        }
        return key;
    }

    function isSafePath(path) {
        return typeof path === "string" && path.startsWith("/") && !path.startsWith("//") && !path.includes("\\");
    }

    function addMessage(text, type, actions, sources) {
        const message = document.createElement("div");
        message.className = `chatbot__message chatbot__message--${type}`;
        message.textContent = text;

        if (actions && actions.length) {
            const links = document.createElement("div");
            links.className = "chatbot__actions";
            actions.forEach((action) => {
                if (action.type === "ticket-form") {
                    const button = document.createElement("button");
                    button.className = "chatbot__action chatbot__action--button";
                    button.type = "button";
                    button.textContent = action.label;
                    button.addEventListener("click", addTicketForm);
                    links.appendChild(button);
                    return;
                }

                if (action.type === "ticket-list") {
                    const button = document.createElement("button");
                    button.className = "chatbot__action chatbot__action--button";
                    button.type = "button";
                    button.textContent = action.label;
                    button.addEventListener("click", loadMyTickets);
                    links.appendChild(button);
                    return;
                }

                if (action.type === "ticket-detail") {
                    const button = document.createElement("button");
                    button.className = "chatbot__action chatbot__action--button";
                    button.type = "button";
                    button.textContent = action.label;
                    button.addEventListener("click", () => loadMyTicket(action.value));
                    links.appendChild(button);
                    return;
                }

                if (!isSafePath(action.url)) return;
                const link = document.createElement("a");
                link.className = "chatbot__action";
                link.href = action.url;
                link.textContent = action.label;
                links.appendChild(link);
            });
            message.appendChild(links);
        }

        if (sources && sources.length) {
            const sourceList = document.createElement("div");
            sourceList.className = "chatbot__sources";
            sourceList.setAttribute("aria-label", "Fuentes consultadas");
            sources.forEach((source) => {
                if (!isSafePath(source.url)) return;
                const link = document.createElement("a");
                link.className = "chatbot__source";
                link.href = source.url;
                link.textContent = source.title;
                sourceList.appendChild(link);
            });
            if (sourceList.childElementCount) message.appendChild(sourceList);
        }

        messages.appendChild(message);
        messages.scrollTop = messages.scrollHeight;
    }

    function renderSuggestions(items) {
        suggestions.replaceChildren();
        (items || []).forEach((item) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "chatbot__suggestion";
            button.textContent = item;
            button.addEventListener("click", () => send(item));
            suggestions.appendChild(button);
        });
    }

    async function readResponse(response) {
        try {
            return await response.json();
        } catch {
            return {};
        }
    }

    async function send(text, showUserMessage = true) {
        const value = (text || input.value).trim();
        if (!value) return;
        input.value = "";
        if (showUserMessage) addMessage(value, "user");
        input.disabled = true;
        form.setAttribute("aria-busy", "true");

        try {
            const response = await fetch("/api/chatbot/message", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-CSRF-TOKEN": chatbot.dataset.chatbotToken,
                    "X-Chatbot-Session": sessionKey
                },
                body: JSON.stringify({ message: value, currentPath: chatbot.dataset.chatbotPath })
            });
            const data = await readResponse(response);
            if (!response.ok) throw new Error(data.message || "No fue posible consultar el Centro de Ayuda.");
            addMessage(data.message, "bot", data.actions, data.sources);
            renderSuggestions(data.suggestions);
        } catch (error) {
            addMessage(error.message || "No fue posible consultar el Centro de Ayuda.", "bot");
        } finally {
            input.disabled = false;
            form.removeAttribute("aria-busy");
            input.focus();
        }
    }

    function createField(labelText, name, control) {
        const wrapper = document.createElement("label");
        wrapper.className = "chatbot__field";
        const label = document.createElement("span");
        label.textContent = labelText;
        control.name = name;
        control.required = true;
        wrapper.append(label, control);
        return wrapper;
    }

    function createSelect(options) {
        const select = document.createElement("select");
        options.forEach(([value, label]) => {
            const option = document.createElement("option");
            option.value = value;
            option.textContent = label;
            select.appendChild(option);
        });
        return select;
    }

    function addTicketForm() {
        if (messages.querySelector(".chatbot__ticket-form")) {
            messages.querySelector(".chatbot__ticket-form").scrollIntoView({ block: "nearest" });
            return;
        }

        const wrapper = document.createElement("div");
        wrapper.className = "chatbot__message chatbot__message--bot chatbot__ticket-message";

        const title = document.createElement("p");
        title.textContent = "Nuevo ticket de soporte";
        wrapper.appendChild(title);

        const ticketForm = document.createElement("form");
        ticketForm.className = "chatbot__ticket-form";

        const subject = document.createElement("input");
        subject.type = "text";
        subject.minLength = 5;
        subject.maxLength = 200;
        subject.placeholder = "Ej.: No puedo guardar una cita";
        ticketForm.appendChild(createField("Asunto", "subject", subject));

        ticketForm.appendChild(createField("Tipo de solicitud", "category", createSelect([
            ["", "Selecciona un tipo"],
            ["incidente", "Error o problema"],
            ["consulta", "Duda o consulta"],
            ["solicitud", "Solicitud de mejora"],
            ["otro", "Otro"]
        ])));
        ticketForm.appendChild(createField("Módulo afectado", "module", createSelect([
            ["", "Selecciona un módulo"],
            ["citas", "Gestión de Citas"],
            ["pacientes", "Gestión de Pacientes"],
            ["facturacion", "Facturación y Pagos"],
            ["reportes", "Reportes y Analítica"],
            ["sistema", "Otro módulo / No estoy seguro"]
        ])));
        ticketForm.appendChild(createField("Severidad", "severity", createSelect([
            ["", "Selecciona la severidad"],
            ["baja", "Baja"],
            ["media", "Media"],
            ["alta", "Alta"]
        ])));

        const description = document.createElement("textarea");
        description.minLength = 10;
        description.maxLength = 2000;
        description.rows = 4;
        description.placeholder = "¿Qué intentabas hacer y qué ocurrió?";
        ticketForm.appendChild(createField("Descripción", "description", description));

        const attachment = document.createElement("input");
        attachment.type = "file";
        attachment.accept = ".jpg,.jpeg,.png,.webp,.gif,.pdf,.doc,.docx,.txt";
        ticketForm.appendChild(createField("Adjunto opcional", "attachment", attachment));
        attachment.required = false;

        const review = document.createElement("div");
        review.className = "chatbot__ticket-review";
        review.hidden = true;
        const reviewText = document.createElement("p");
        review.appendChild(reviewText);
        const confirm = document.createElement("button");
        confirm.type = "button";
        confirm.className = "chatbot__ticket-submit";
        confirm.textContent = "Confirmar y enviar";
        review.appendChild(confirm);
        const edit = document.createElement("button");
        edit.type = "button";
        edit.className = "chatbot__ticket-edit";
        edit.textContent = "Editar";
        review.appendChild(edit);
        ticketForm.appendChild(review);

        const error = document.createElement("p");
        error.className = "chatbot__ticket-error";
        error.setAttribute("role", "alert");
        error.hidden = true;
        ticketForm.appendChild(error);

        const submit = document.createElement("button");
        submit.className = "chatbot__ticket-submit";
        submit.type = "submit";
        submit.textContent = "Enviar ticket";
        ticketForm.appendChild(submit);

        const fullFormLink = document.createElement("a");
        fullFormLink.className = "chatbot__full-form";
        fullFormLink.href = "/centro-de-ayuda/soporte";
        fullFormLink.textContent = "Abrir formulario completo para adjuntar evidencia";
        ticketForm.appendChild(fullFormLink);

        ticketForm.addEventListener("submit", async (event) => {
            event.preventDefault();
            if (!ticketForm.reportValidity()) return;

            const values = new FormData(ticketForm);
            const file = attachment.files && attachment.files[0];
            if (file && file.size > 5 * 1024 * 1024) {
                error.textContent = "El adjunto no puede superar 5 MB.";
                error.hidden = false;
                return;
            }

            reviewText.textContent =
                `Revisa antes de enviar:\nAsunto: ${values.get("subject")}\nTipo: ${values.get("category")}\nMódulo: ${values.get("module")}\nSeveridad: ${values.get("severity")}\nDescripción: ${values.get("description")}`;
            submit.hidden = true;
            review.hidden = false;
            error.hidden = true;
        });

        edit.addEventListener("click", () => {
            review.hidden = true;
            submit.hidden = false;
            subject.focus();
        });

        confirm.addEventListener("click", async () => {
            if (confirm.disabled) return;
            submit.disabled = true;
            confirm.disabled = true;
            error.hidden = true;
            const values = new FormData(ticketForm);
            values.set("confirmed", "true");
            values.set("idempotencyKey",
                (window.crypto && typeof window.crypto.randomUUID === "function")
                    ? window.crypto.randomUUID().replace(/-/g, "")
                    : `ticket-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`);
            try {
                const response = await fetch("/api/chatbot/tickets", {
                    method: "POST",
                    headers: {
                        "X-CSRF-TOKEN": chatbot.dataset.chatbotToken,
                        "X-Chatbot-Session": sessionKey
                    },
                    body: values
                });
                const data = await readResponse(response);
                if (!response.ok) throw new Error(data.message || "No fue posible registrar el ticket.");

                ticketForm.remove();
                const reference = data.data && data.data.referencia ? data.data.referencia : "";
                addMessage(reference
                    ? `Tu ticket quedó registrado con la referencia ${reference}. Puedes consultar su estado desde este asistente.`
                    : "Tu ticket quedó registrado correctamente. Puedes consultar su estado desde este asistente.", "bot", [
                    { label: "Consultar mis tickets", type: "ticket-list" },
                    { label: "Formulario completo y adjuntos", url: "/centro-de-ayuda/soporte", type: "link" }
                ]);
                renderSuggestions(["Consultar mis tickets", "Buscar guías", "Crear ticket de soporte"]);
            } catch (submissionError) {
                error.textContent = submissionError.message || "No fue posible registrar el ticket.";
                error.hidden = false;
                submit.disabled = false;
                confirm.disabled = false;
            }
        });

        wrapper.appendChild(ticketForm);
        messages.appendChild(wrapper);
        messages.scrollTop = messages.scrollHeight;
        subject.focus();
    }

    async function readJson(response, fallbackMessage) {
        try {
            return await response.json();
        } catch {
            return { message: fallbackMessage };
        }
    }

    async function loadMyTickets() {
        try {
            const response = await fetch("/api/chatbot/tickets/mine?page=1&pageSize=10", {
                headers: { "Accept": "application/json" }
            });
            const data = await readJson(response, "No fue posible consultar tus tickets.");
            if (!response.ok) throw new Error(data.message || "No fue posible consultar tus tickets.");

            const tickets = data.data || [];
            if (!tickets.length) {
                addMessage("No hay tickets asociados a tu cuenta.", "bot");
                return;
            }

            const details = tickets.map((ticket) =>
                `${ticket.reference}: ${ticket.subject} — ${ticket.status} (${ticket.module})`
            ).join("\n");
            const actions = tickets.map((ticket) => ({
                label: `Ver ${ticket.reference}`,
                type: "ticket-detail",
                value: ticket.reference
            }));
            addMessage(`Estos son tus tickets recientes:\n${details}`, "bot", actions);
        } catch (error) {
            addMessage(error.message || "No fue posible consultar tus tickets.", "bot");
        }
    }

    async function loadMyTicket(reference) {
        if (!reference || typeof reference !== "string") return;
        try {
            const response = await fetch(`/api/chatbot/tickets/mine/${encodeURIComponent(reference)}`, {
                headers: { "Accept": "application/json" }
            });
            const data = await readJson(response, "No fue posible consultar el ticket.");
            if (!response.ok) throw new Error(data.message || "No fue posible consultar el ticket.");

            const ticket = data.data;
            const responseText = ticket.response ? `\nRespuesta: ${ticket.response}` : "\nAún no hay respuesta de soporte.";
            addMessage(
                `Ticket ${ticket.reference}: ${ticket.subject}\nEstado: ${ticket.status}\nMódulo: ${ticket.module}${responseText}`,
                "bot");
        } catch (error) {
            addMessage(error.message || "No fue posible consultar el ticket.", "bot");
        }
    }

    function open() {
        panel.hidden = false;
        launcher.setAttribute("aria-expanded", "true");
        if (!initialized) {
            initialized = true;
            send("hola", false);
        }
        input.focus();
    }

    function close() {
        panel.hidden = true;
        launcher.setAttribute("aria-expanded", "false");
        launcher.focus();
    }

    async function resetConversation() {
        try {
            await fetch("/api/chatbot/conversation/reset", {
                method: "POST",
                headers: {
                    "X-CSRF-TOKEN": chatbot.dataset.chatbotToken,
                    "X-Chatbot-Session": sessionKey
                }
            });
        } finally {
            messages.replaceChildren();
            renderSuggestions(["¿Qué hace esta vista?", "Buscar guías", "Crear ticket de soporte"]);
            send("hola", false);
        }
    }

    launcher.addEventListener("click", () => panel.hidden ? open() : close());
    closeButton.addEventListener("click", close);
    if (resetButton) resetButton.addEventListener("click", resetConversation);
    document.addEventListener("click", (event) => {
        const link = event.target.closest('a[href="/acceso-y-seguridad/login"]');
        if (!link) return;
        try {
            window.sessionStorage.removeItem("smiletrack-chatbot-session");
        } catch {
            // Session cleanup is best-effort; the server key is also user-specific.
        }
    });
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && !panel.hidden) close();
    });
    form.addEventListener("submit", (event) => { event.preventDefault(); send(); });
}());
