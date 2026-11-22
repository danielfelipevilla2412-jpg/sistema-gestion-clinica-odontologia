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

    function markRead(notice) {
        if (notice.leido) return;
        fetch('/gestion-de-pqr/api/notificaciones/leida', {
            method: 'POST',
            credentials: 'same-origin',
            keepalive: true,
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': inbox.dataset.token },
            body: JSON.stringify(notice.clave)
        }).then(response => {
            if (response.ok && !response.redirected) {
                notice.leido = true;
                render();
                refresh();
            }
        }).catch(() => {});
    }

    function render() {
        const unread = notices.filter(notice => !notice.leido).length;
        count.hidden = unread === 0;
        count.textContent = String(unread);
        summary.setAttribute('aria-label', unread ? `${title}, ${unread} sin leer` : title);

        list.replaceChildren(...notices.map(notice => {
            const row = document.createElement('li');
            const link = document.createElement('a');
            link.href = notice.url;
            link.textContent = `${notice.radicado} · ${notice.asunto}`;
            const label = document.createElement('small');
            label.textContent = admin ? 'Nueva solicitud' : 'Respuesta recibida';
            if (!notice.leido) label.textContent += ' · Sin leer';
            link.append(label);
            link.addEventListener('click', () => markRead(notice));
            link.addEventListener('auxclick', event => { if (event.button === 1) markRead(notice); });
            row.append(link);
            return row;
        }));
        status.hidden = notices.length > 0;
        status.textContent = admin ? 'No hay solicitudes nuevas pendientes.' : 'Aún no tienes respuestas.';
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
