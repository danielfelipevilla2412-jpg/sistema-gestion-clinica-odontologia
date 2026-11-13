(() => {
  const estados = {
    caries: 'Caries',
    endodoncia: 'Endodoncia',
    sellante: 'Sellante',
    placa: 'Placa',
    sano: 'Sano',
    obturacion: 'Obturación',
    restauracion: 'Restauración',
    corona: 'Corona',
    extraccion: 'Extracción',
    ausente: 'Ausente',
    implante: 'Implante',
    fractura: 'Fractura',
    protesis: 'Prótesis'
  };
  const arcadas = [
    { nombre: 'Superior', izquierda: ['18', '17', '16', '15', '14', '13', '12', '11'], derecha: ['21', '22', '23', '24', '25', '26', '27', '28'] },
    { nombre: 'Inferior', izquierda: ['48', '47', '46', '45', '44', '43', '42', '41'], derecha: ['31', '32', '33', '34', '35', '36', '37', '38'] }
  ];

  const render = (container, tratamientos, onSelect, onFocus) => {
    if (!container) return;
    container.replaceChildren();

    arcadas.forEach(arcada => {
      const section = document.createElement('section');
      section.className = 'fdi-arch-section';
      const heading = document.createElement('h3');
      heading.className = 'fdi-arch-title';
      heading.textContent = `Arcada ${arcada.nombre.toLowerCase()}`;
      const row = document.createElement('div');
      row.className = 'fdi-arch';

      [arcada.izquierda, arcada.derecha].forEach((cuadrante, index) => {
        const group = document.createElement('div');
        group.className = 'fdi-quadrant';
        group.setAttribute('aria-label', `Cuadrante ${index === 0 ? 'derecho' : 'izquierdo'} de la arcada ${arcada.nombre.toLowerCase()}`);

        cuadrante.forEach(numero => {
          const button = document.createElement('button');
          const estado = tratamientos[numero] || '';
          button.type = 'button';
          button.className = 'fdi-tooth';
          if (estado in estados) button.classList.add(`fdi-tooth--${estado}`);
          button.textContent = numero;
          button.dataset.numeroFdi = numero;
          button.title = `Diente ${numero}${estado in estados ? `: ${estados[estado]}` : ': sin estado registrado'}`;
          button.setAttribute('aria-label', button.title);
          button.addEventListener('click', () => onSelect?.(numero));
          button.addEventListener('mouseenter', () => onFocus?.(numero));
          button.addEventListener('focus', () => onFocus?.(numero));
          group.appendChild(button);
        });

        row.appendChild(group);
      });

      row.addEventListener('mouseleave', () => onFocus?.(null));
      section.append(heading, row);
      container.appendChild(section);
    });
  };

  window.SmileTrackOdontogramaFDI = { render, estados };
})();
