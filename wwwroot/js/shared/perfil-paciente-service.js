(function () {
  'use strict';

  class PerfilPacienteService {
    constructor() {
      this.cache = {};
      this.lastFetch = 0;
      this.cacheDuration = 5 * 60 * 1000;
      this.apiBase = '/api/perfil-paciente';
    }

    isCacheValid() {
      return this.lastFetch > 0 && Date.now() - this.lastFetch < this.cacheDuration;
    }

    invalidateCache() {
      this.cache = {};
      this.lastFetch = 0;
    }

    async request(resource, forceRefresh) {
      if (!forceRefresh && this.isCacheValid() && this.cache[resource]) return this.cache[resource];

      const response = await fetch(`${this.apiBase}/${resource}`, {
        credentials: 'include',
        headers: { Accept: 'application/json' }
      });

      if (response.status === 401 || response.status === 403) {
        this.invalidateCache();
        if (response.status === 401) window.location.assign('/acceso-y-seguridad/login');
        return null;
      }
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const payload = await response.json();
      const data = payload.success ? payload.data : null;
      if (data) {
        this.cache[resource] = data;
        this.lastFetch = Date.now();
      }
      return data;
    }

    getInfoBasica(forceRefresh = false) { return this.request('info-basica', forceRefresh); }
    getInfoMedica(forceRefresh = false) { return this.request('info-medica', forceRefresh); }
    getEstadisticas(forceRefresh = false) { return this.request('estadisticas', forceRefresh); }
    getResumenCompleto(forceRefresh = false) { return this.request('resumen-completo', forceRefresh); }

    formatNombreCompleto(info) {
      return info?.nombreCompleto || `${info?.nombres || ''} ${info?.apellidos || ''}`.trim() || 'Usuario';
    }

    formatTelefono(telefono) {
      const digits = String(telefono || '').replace(/\D/g, '');
      return digits.length === 10 ? `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}` : String(telefono || '');
    }
  }

  window.PerfilPacienteService = window.PerfilPacienteService || new PerfilPacienteService();
})();