(() => {
  'use strict';

  const requiredElement = id => {
    const element = document.getElementById(id);
    if (!element) {
      throw new Error(`No se encontró el elemento requerido #${id}.`);
    }
    return element;
  };

  const translate = key => {
    if (!window.I18N || typeof window.I18N.t !== 'function') {
      throw new Error('El servicio de traducción no está disponible.');
    }
    return window.I18N.t(key);
  };

  const normalize = value => (value || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();

  const initialize = () => {
    const search = requiredElement('buscarServicio');
    const specialty = requiredElement('filtroEspecialidad');
    const status = requiredElement('filtroEstado');
    const empty = requiredElement('sinResultados');
    const panel = requiredElement('detallePanel');
    const overlay = requiredElement('detalleOverlay');
    const closeButton = requiredElement('cerrarDetalle');
    const cards = Array.from(document.querySelectorAll('.service-card'));
    let activeCard = null;
    let previousFocus = null;

    const filterServices = () => {
      const query = normalize(search.value);
      let visibleCount = 0;

      cards.forEach(card => {
        const searchableText = normalize(`${card.dataset.name} ${card.dataset.description}`);
        const matches = searchableText.includes(query)
          && (!specialty.value || card.dataset.specialty === specialty.value)
          && (!status.value || card.dataset.status === status.value);
        card.hidden = !matches;
        if (matches) visibleCount += 1;
      });

      empty.hidden = cards.length === 0 || visibleCount > 0;
    };

    const renderDetails = card => {
      const data = card.querySelector('.service-data')?.dataset;
      if (!data) {
        throw new Error('No se encontraron los detalles del servicio seleccionado.');
      }

      requiredElement('detalleTitulo').textContent = data.name || translate('odo05.not_specified');
      requiredElement('detalleDescripcion').textContent = data.description || translate('odo05.description_not_registered');
      requiredElement('detalleEspecialidad').textContent = data.specialty || translate('odo05.not_specified');
      requiredElement('detalleCategoria').textContent = data.category || translate('odo05.not_specified');
      requiredElement('detalleDuracion').textContent = data.duration
        ? `${data.duration} ${translate('odo05.minutes')}`
        : translate('odo05.not_specified');
      requiredElement('detalleBase').textContent = data.base
        ? `${data.base} COP`
        : translate('odo05.not_specified');
      requiredElement('detallePrecio').textContent = data.price
        ? `${data.price} COP`
        : translate('odo05.not_specified');
      requiredElement('detallePersonalizado').textContent = data.custom === 'true'
        ? translate('common.yes')
        : translate('common.no');

      const statusElement = requiredElement('detalleEstado');
      const isAvailable = data.status === 'activo';
      statusElement.className = `status-badge badge-estado ${isAvailable ? 'activo' : 'inactivo'}`;
      statusElement.textContent = translate(isAvailable ? 'odo05.status_available' : 'odo05.status_unavailable');
    };

    const openDetails = button => {
      const card = button.closest('.service-card');
      if (!card) {
        throw new Error('No se encontró la tarjeta del servicio seleccionado.');
      }

      activeCard = card;
      previousFocus = button;
      renderDetails(card);
      panel.hidden = false;
      overlay.hidden = false;
      panel.setAttribute('aria-hidden', 'false');
      overlay.setAttribute('aria-hidden', 'false');
      document.body.classList.add('professional-services-detail-open');
      closeButton.focus();
    };

    const closeDetails = () => {
      if (panel.hidden) return;

      panel.hidden = true;
      overlay.hidden = true;
      panel.setAttribute('aria-hidden', 'true');
      overlay.setAttribute('aria-hidden', 'true');
      document.body.classList.remove('professional-services-detail-open');
      activeCard = null;
      previousFocus?.focus();
      previousFocus = null;
    };

    search.addEventListener('input', filterServices);
    specialty.addEventListener('change', filterServices);
    status.addEventListener('change', filterServices);

    document.querySelectorAll('.details-button').forEach(button => {
      button.addEventListener('click', () => openDetails(button));
    });

    closeButton.addEventListener('click', closeDetails);
    overlay.addEventListener('click', closeDetails);

    document.addEventListener('keydown', event => {
      if (panel.hidden) return;

      if (event.key === 'Escape') {
        event.preventDefault();
        closeDetails();
        return;
      }

      if (event.key !== 'Tab') return;

      const focusable = Array.from(panel.querySelectorAll(
        'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
      ));
      if (!focusable.length) {
        event.preventDefault();
        panel.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });

    document.addEventListener('i18n:change', () => {
      if (activeCard) renderDetails(activeCard);
    });
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
  } else {
    initialize();
  }
})();
