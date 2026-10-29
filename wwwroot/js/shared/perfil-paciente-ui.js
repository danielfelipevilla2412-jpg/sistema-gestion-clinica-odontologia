(function () {
  'use strict';

  const escapeHtml = (value) => String(value ?? '').replace(/[&<>'"]/g, (character) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
  })[character]);

  class PerfilPacienteUI {
    constructor() {
      this.service = window.PerfilPacienteService;
    }

    async renderUserHeader(targetSelector) {
      const target = typeof targetSelector === 'string' ? document.querySelector(targetSelector) : targetSelector;
      if (!target) return;

      target.setAttribute('aria-busy', 'true');
      try {
        const [info, stats] = await Promise.all([
          this.service.getInfoBasica(),
          this.service.getEstadisticas()
        ]);
        if (!info) return;

        const initials = this.service.formatNombreCompleto(info).split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase();
        const next = stats?.proximaCita;
        target.innerHTML = `
          <section class="perfil-paciente-header" aria-label="Resumen del paciente">
            <div class="perfil-paciente-header__identity">
              <div class="perfil-paciente-header__avatar" aria-hidden="true">${escapeHtml(initials || 'U')}</div>
              <div>
                <p class="perfil-paciente-header__eyebrow">Perfil de paciente</p>
                <h2>${escapeHtml(this.service.formatNombreCompleto(info))}</h2>
                <p class="perfil-paciente-header__meta">${escapeHtml(info.tipoDocumento)} ${escapeHtml(info.documentoOculto)}${info.ciudad ? ` · ${escapeHtml(info.ciudad)}` : ''}</p>
              </div>
            </div>
            <div class="perfil-paciente-header__stats" aria-label="Estadísticas de citas">
              <span><strong>${stats?.totalCitas || 0}</strong> citas</span>
              <span><strong>${stats?.citasPendientes || 0}</strong> pendientes</span>
              <span><strong>${stats?.citasCompletadas || 0}</strong> completadas</span>
            </div>
            ${next ? `<p class="perfil-paciente-header__next">Próxima cita: <strong>${escapeHtml(new Date(next.fechaHora).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' }))}</strong> · ${escapeHtml(next.servicio)}</p>` : ''}
          </section>`;
      } catch (error) {
        console.error('[PerfilPacienteUI] No se pudo cargar el perfil', error);
      } finally {
        target.removeAttribute('aria-busy');
      }
    }
  }

  window.PerfilPacienteUI = window.PerfilPacienteUI || new PerfilPacienteUI();
  document.addEventListener('DOMContentLoaded', () => {
    const target = document.querySelector('[data-perfil-paciente-header]');
    if (target) window.PerfilPacienteUI.renderUserHeader(target);
  });
})();