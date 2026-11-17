(() => {
  'use strict';

  const translate = key => window.I18N?.t(key) || key;
  const config = window.CONFIG_GENERAL;

  const notify = (type, key) => {
    const service = window.ToastService?.[type];
    if (typeof service !== 'function') {
      console.error('[SmileTrack][Configuración] El servicio compartido de notificaciones no está disponible.');
      return;
    }

    const title = translate(type === 'error'
      ? 'config.toast_error'
      : type === 'warning' ? 'config.toast_warning' : 'config.toast_success');
    service(title, translate(key));
  };

  const setSwitch = (id, value) => {
    const button = document.getElementById(id);
    if (button) button.setAttribute('aria-checked', String(Boolean(value)));
  };

  const render = () => {
    if (!config || !config.appointment || !config.center || !config.notifications) {
      throw new Error('La configuración inicial no tiene el formato esperado.');
    }

    document.getElementById('defaultDuration').value = String(config.appointment.duration);
    document.getElementById('openTime').value = config.appointment.open;
    document.getElementById('closeTime').value = config.appointment.close;
    document.getElementById('centerName').value = config.center.name;
    document.getElementById('centerNIT').value = config.center.nit;
    document.getElementById('centerAddress').value = config.center.address;
    setSwitch('toggleEmail', config.notifications.email);
    setSwitch('toggleSMS', config.notifications.sms);
    setSwitch('toggleSystem', config.notifications.system);
  };

  const readForm = () => ({
    appointment: {
      duration: Number(document.getElementById('defaultDuration').value),
      open: document.getElementById('openTime').value,
      close: document.getElementById('closeTime').value
    },
    center: {
      name: document.getElementById('centerName').value.trim(),
      nit: document.getElementById('centerNIT').value.trim(),
      address: document.getElementById('centerAddress').value.trim()
    },
    notifications: {
      email: document.getElementById('toggleEmail').getAttribute('aria-checked') === 'true',
      sms: document.getElementById('toggleSMS').getAttribute('aria-checked') === 'true',
      system: document.getElementById('toggleSystem').getAttribute('aria-checked') === 'true'
    }
  });

  const handleSubmit = async event => {
    event.preventDefault();
    const form = event.currentTarget;
    if (!form.reportValidity()) return;

    const updatedConfig = readForm();
    if (updatedConfig.appointment.open >= updatedConfig.appointment.close) {
      document.getElementById('closeTime').setCustomValidity(translate('config.hours_error'));
      document.getElementById('closeTime').reportValidity();
      document.getElementById('closeTime').setCustomValidity('');
      return;
    }

    const saveButton = document.getElementById('btnSaveChanges');
    saveButton.disabled = true;
    try {
      const result = await window.apiRequest('/servicios-y-recursos/api/configuracion-general', {
        method: 'PUT',
        body: updatedConfig
      });

      if (result?.success !== true) {
        notify('error', 'config.save_error');
        return;
      }

      Object.assign(config, updatedConfig);
      notify('success', 'config.saved');
    } catch (error) {
      console.error('Error guardando la configuración general.', error);
      notify('error', 'config.save_error');
    } finally {
      saveButton.disabled = false;
    }
  };

  const init = () => {
    render();
    document.getElementById('configForm')?.addEventListener('submit', handleSubmit);
    document.querySelectorAll('.toggle-switch').forEach(button => {
      button.addEventListener('click', () => {
        const checked = button.getAttribute('aria-checked') === 'true';
        button.setAttribute('aria-checked', String(!checked));
      });
    });
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init, { once: true });
  } else {
    init();
  }
})();
