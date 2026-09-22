(function () {
    "use strict";

    const chatbot = document.getElementById("smiletrack-chatbot");
    if (!chatbot) return;

    const launcher = chatbot.querySelector(".chatbot__launcher");
    const panel = chatbot.querySelector(".chatbot__panel");
    const closeButton = chatbot.querySelector(".chatbot__close");
    const messages = chatbot.querySelector("[data-chatbot-messages]");
    const suggestions = chatbot.querySelector("[data-chatbot-suggestions]");
    const form = chatbot.querySelector("[data-chatbot-form]");
    const input = form.querySelector("input");
    let initialized = false;

    function addMessage(text, type, actions) {
        const message = document.createElement("div");
        message.className = `chatbot__message chatbot__message--${type}`;
        message.textContent = text;

        if (actions && actions.length) {
            const links = document.createElement("div");
            links.className = "chatbot__actions";
            actions.forEach((action) => {
                const link = document.createElement("a");
                link.className = "chatbot__action";
                link.href = action.url;
                link.textContent = action.label;
                links.appendChild(link);
            });
            message.appendChild(links);
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

    async function send(text, showUserMessage = true) {
        const value = (text || input.value).trim();
        if (!value) return;
        input.value = "";
        if (showUserMessage) addMessage(value, "user");
        input.disabled = true;

        try {
            const response = await fetch("/api/chatbot/message", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-CSRF-TOKEN": chatbot.dataset.chatbotToken
                },
                body: JSON.stringify({ message: value, currentPath: chatbot.dataset.chatbotPath })
            });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || "No fue posible consultar el asistente.");
            addMessage(data.message, "bot");
            renderSuggestions(data.suggestions);
        } catch (error) {
            addMessage(error.message || "No fue posible consultar el asistente.", "bot");
        } finally {
            input.disabled = false;
            input.focus();
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

    launcher.addEventListener("click", () => panel.hidden ? open() : close());
    closeButton.addEventListener("click", close);
    form.addEventListener("submit", (event) => { event.preventDefault(); send(); });

    function close() {
        panel.hidden = true;
        launcher.setAttribute("aria-expanded", "false");
    }
}());