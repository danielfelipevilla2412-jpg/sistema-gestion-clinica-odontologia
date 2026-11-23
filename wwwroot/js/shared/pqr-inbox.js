(() => {
    const inbox = document.getElementById('pqrInbox');
    if (!inbox) return;

    const list = inbox.querySelector('.pqr-inbox-list');
    const status = inbox.querySelector('.pqr-inbox-status');
    const count = inbox.querySelector('.pqr-inbox-count');
    const summary = inbox.querySelector('summary');
    const title = summary.title;
    const admin = inbox.dataset.admin === 'true';
    let notices = [];
    let loading = false;

    async function markRead(notice) {
        try {
            const response = await fetch('/gestion-de-pqr/api/notificaciones/leida', {
                method: 'POST',
                credentials: 'same-origin',
                keepalive: true,
                signal: AbortSignal.timeout(10000),
                headers: { Accept: 'application/json', 'X-CSRF-TOKEN': inbox.dataset.token },
                body: new URLSearchParams({ clave: notice.clave })
            });
            if (response.status !== 204 || response.redirected) {
                const code = response.redirected ? 'sesión no disponible' : `HTTP ${response.status}`;
                throw new Error(code);
            }
            notices = notices.filter(item => item.clave !== notice.clave);
            render();
            return true;
        } catch (error) {
            status.hidden = false;
            status.textContent = `No se pudo guardar la lectura (${error.message}). Intenta de nuevo.`;
            return false;
        }
    }

    function render() {
        const pending = notices.filter(notice => !notice.leido);
        const unread = pending.length;
        count.hidden = unread === 0;
        count.textContent = String(unread);
        summary.setAttribute('aria-label', unread ? `${title}, ${unread} sin leer` : title);

        list.replaceChildren(...pending.map(notice => {
            const row = document.createElement('li');
            const link = document.createElement('a');
            link.href = notice.url;
            link.textContent = `${notice.radicado} · ${notice.asunto}`;
            const label = document.createElement('small');
            label.textContent = admin ? 'Nueva solicitud' : 'Respuesta recibida';
            link.append(label);
            link.addEventListener('click', async event => {
                if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
                    void markRead(notice);
                    return;
                }
                event.preventDefault();
                if (link.getAttribute('aria-busy') === 'true') return;
                link.setAttribute('aria-busy', 'true');
                if (await markRead(notice)) {
                    window.location.assign(link.href);
                } else {
                    link.removeAttribute('aria-busy');
                }
            });
            link.addEventListener('auxclick', event => { if (event.button === 1) markRead(notice); });
            row.append(link);
            return row;
        }));
        status.hidden = unread > 0;
        status.textContent = admin ? 'No hay solicitudes nuevas pendientes.' : 'No tienes respuestas sin leer.';
    }

    async function refresh() {
        if (loading || document.hidden) return;
        loading = true;
        try {
            const response = await fetch('/gestion-de-pqr/api/notificaciones', {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { Accept: 'application/json' }
            });
            if (!response.ok || response.redirected) throw new Error('No fue posible consultar los avisos');
            notices = await response.json();
            render();
        } catch {
            notices = [];
            render();
            status.hidden = false;
            status.textContent = 'No se pudieron actualizar los avisos. Vuelve a abrir el panel.';
        } finally {
            loading = false;
        }
    }

    inbox.addEventListener('toggle', () => { if (inbox.open) refresh(); });
    inbox.addEventListener('keydown', event => {
        if (event.key === 'Escape' && inbox.open) {
            inbox.open = false;
            summary.focus();
            event.stopPropagation();
        }
    });
    document.addEventListener('visibilitychange', refresh);
    window.addEventListener('pageshow', event => {
        if (event.persisted) refresh();
    });
    window.addEventListener('pagehide', () => {
        notices = [];
        render();
    });
    refresh();
    setInterval(refresh, 30000);
})();
