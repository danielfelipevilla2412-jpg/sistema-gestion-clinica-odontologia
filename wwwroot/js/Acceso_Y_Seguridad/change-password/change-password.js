document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('changePasswordForm');
  const currentInput = document.getElementById('contrasenaActual');
  const newPasswordInput = document.getElementById('nuevaContrasena');
  const confirmInput = document.getElementById('confirmarContrasena');
  const confirmError = document.getElementById('confirmError');
  const changeBtn = document.getElementById('changePasswordBtn');
  const reqLength = document.getElementById('reqLength');
  const reqUpper = document.getElementById('reqUpper');
  const reqNumber = document.getElementById('reqNumber');

  const validateAll = () => {
    const current = currentInput.value.trim();
    const next = newPasswordInput.value.trim();
    const confirm = confirmInput.value.trim();

    const hasLength = next.length >= 8;
    const hasUpper = /[A-Z]/.test(next);
    const hasNumber = /[0-9]/.test(next);
    const passwordsMatch = next === confirm && next.length > 0;

    reqLength.classList.toggle('valid', hasLength);
    reqUpper.classList.toggle('valid', hasUpper);
    reqNumber.classList.toggle('valid', hasNumber);

    if (confirm.length > 0 && !passwordsMatch) {
      confirmError.textContent = 'Las contraseñas no coinciden.';
      confirmError.classList.remove('hidden');
    } else {
      confirmError.textContent = '';
      confirmError.classList.add('hidden');
    }

    changeBtn.disabled = !(current && hasLength && hasUpper && hasNumber && passwordsMatch);
  };

  const togglePasswordButtons = document.querySelectorAll('.toggle-pass');
  togglePasswordButtons.forEach((btn) => {
    btn.addEventListener('click', () => {
      const input = btn.closest('.input-wrap')?.querySelector('input');
      const icon = btn.querySelector('.material-symbols-outlined');
      if (!input || !icon) return;
      const isHidden = input.type === 'password';
      input.type = isHidden ? 'text' : 'password';
      icon.textContent = isHidden ? 'visibility' : 'visibility_off';
      btn.setAttribute('aria-pressed', String(isHidden));
    });
  });

  currentInput.addEventListener('input', validateAll);
  newPasswordInput.addEventListener('input', validateAll);
  confirmInput.addEventListener('input', validateAll);

  if (form) {
    form.addEventListener('submit', (event) => {
      if (changeBtn.disabled) {
        event.preventDefault();
      }
    });
  }
});
