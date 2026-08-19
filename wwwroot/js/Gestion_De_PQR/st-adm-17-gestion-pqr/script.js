// ===== CHROME COMPARTIDO (sidebar / hamburger / toast) =====
const safeGetElement = (id) => document.getElementById(id);

const showToast = (message, type = 'success') => {
  const toast = safeGetElement('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.className = `toast${type === 'error' ? ' error' : type === 'warning' ? ' warning' : ''} show`;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast._timeoutId = setTimeout(() => toast.classList.remove('show'), 3000);
};

const initSidebar = () => {
  const hamburger = safeGetElement('hamburger');
  const sidebar = safeGetElement('sidebar');
  const overlay = safeGetElement('overlay');
  if (!hamburger || !sidebar || !overlay) return;

  const toggleMenu = (show) => {
    sidebar.classList.toggle('open', show);
    overlay.classList.toggle('open', show);
    hamburger.setAttribute('aria-expanded', String(show));
    overlay.setAttribute('aria-hidden', String(!show));
  };

  hamburger.addEventListener('click', () => toggleMenu(!sidebar.classList.contains('open')));
  overlay.addEventListener('click', () => toggleMenu(false));
};

document.addEventListener('DOMContentLoaded', () => {
  initSidebar();
  if (Array.isArray(window.RAZOR_PQRS) && window.RAZOR_PQRS.length > 0) {
    window.RAZOR_PQRS.forEach(p => {
      pqrsData[p.id] = {
        radicado: p.ticket,
        titulo: p.subject,
        tipo: p.type,
        estado: p.status,
        prioridad: p.priority,
        paciente: p.patient,
        documento: 'N/A',
        email: 'paciente@smiletrack.co',
        odontologo: 'Dra. Patricia Mora',
        fecha: p.date,
        tiempo: 'Reciente',
        descripcion: p.description,
        respuestas: []
      };
    });
  }
});


// ===== LOGICA PROPIA DE GESTION DE PQR =====
// Datos de los PQR
const pqrsData = {
    1: {
        radicado: 'PQR-2024-001847',
        titulo: 'Demora en entrega de historia clínica',
        tipo: 'Queja',
        estado: 'Resuelto',
        prioridad: 'Alta',
        paciente: 'Laura Milena Ríos',
        documento: '1023456789',
        email: 'laura.rios@gmail.com',
        odontologo: 'Dra. Patricia Mora',
        fecha: '14 de jun de 2024',
        tiempo: '747d 6h',
        descripcion: 'Solicité mi historia clínica hace más de 20 días hábiles y aún no he recibido respuesta ni documento.',
        respuestas: [
            { autor: 'Admin PQR', fecha: '15 de jun de 2024', contenido: 'Estimada Laura, hemos recibido su queja y estamos gestionando la solicitud con el área de archivo.' }
        ]
    },
    2: {
        radicado: 'PQR-2024-001782',
        titulo: 'Cobro incorrecto en tratamiento',
        tipo: 'Reclamo',
        estado: 'Pend. Info',
        prioridad: 'Alta',
        paciente: 'Carlos Andrés Puentes',
        documento: '79845632',
        email: 'carlos.puentes@email.com',
        odontologo: 'Dra. Patricia Mora',
        fecha: '10 de ene de 2024',
        tiempo: '753d 1h',
        descripcion: 'Me cobraron un procedimiento que no se realizó. Solicito la devolución del dinero.',
        respuestas: []
    },
    3: {
        radicado: 'PQR-2024-001621',
        titulo: 'Solicitud de certificado de atención',
        tipo: 'Petición',
        estado: 'Resuelto',
        prioridad: 'Baja',
        paciente: 'María Fernanda López',
        documento: '52014789',
        email: 'maria.lopez@email.com',
        odontologo: 'Dra. Ana García',
        fecha: '02 de may de 2024',
        tiempo: '770d 5h',
        descripcion: 'Necesito un certificado de atención para mi trabajo.',
        respuestas: [
            { autor: 'Admin PQR', fecha: '03 de may de 2024', contenido: 'Certificado enviado al correo electrónico registrado.' }
        ]
    },
    4: {
        radicado: 'PQR-2024-001540',
        titulo: 'Tiempo de espera excesivo',
        tipo: 'Queja',
        estado: 'Cerrado',
        prioridad: 'Media',
        paciente: 'Andrés Felipe Gómez',
        documento: '1012345678',
        email: 'andres.gomez@email.com',
        odontologo: 'Dr. Carlos Ruiz',
        fecha: '20 de abr de 2024',
        tiempo: '781d 23h',
        descripcion: 'Espere más de 2 horas para ser atendido en mi cita programada.',
        respuestas: [
            { autor: 'Admin PQR', fecha: '21 de abr de 2024', contenido: 'Lamentamos los inconvenientes. Hemos tomado medidas para mejorar nuestros tiempos de espera.' }
        ]
    },
    5: {
        radicado: 'PQR-2024-001380',
        titulo: 'Error en facturación electrónica',
        tipo: 'Reclamo',
        estado: 'Resuelto',
        prioridad: 'Media',
        paciente: 'Sandra Milena Torres',
        documento: '43215678',
        email: 'sandra.torres@email.com',
        odontologo: 'Dra. Patricia Mora',
        fecha: '18 de mar de 2024',
        tiempo: '804d 7h',
        descripcion: 'La factura electrónica tiene errores en los datos. Necesito que la corrijan.',
        respuestas: [
            { autor: 'Admin PQR', fecha: '19 de mar de 2024', contenido: 'Factura corregida y enviada nuevamente.' }
        ]
    },
    6: {
        radicado: 'PQR-2024-002010',
        titulo: 'Cambio de odontólogo tratante',
        tipo: 'Petición',
        estado: 'Recibido',
        prioridad: 'Media',
        paciente: 'Jorge Esteban Vargas',
        documento: '71234560',
        email: 'jorge.vargas@email.com',
        odontologo: 'Dr. Luis Martínez',
        fecha: '25 de jun de 2024',
        tiempo: '733d 6h',
        descripcion: 'Solicito cambio de odontólogo por motivos de horario.',
        respuestas: []
    }
};

let currentId = null;

// Show Management
function showManagement() {
    // Ya estamos en gestión
}

// Show Detail Panel
function showDetail(id) {
    currentId = id;
    const data = pqrsData[id];

    // Remove selected class from all rows
    document.querySelectorAll('tbody tr').forEach(row => {
        row.classList.remove('selected');
    });

    // Add selected class to current row
    const selectedRow = document.querySelector(`tr[data-id="${id}"]`);
    if (selectedRow) {
        selectedRow.classList.add('selected');
    }

    // Hide empty state and show content
    document.getElementById('emptyState').style.display = 'none';
    document.getElementById('detailContent').style.display = 'block';

    // Fill data
    document.getElementById('detailTitle').textContent = data.titulo;
    document.getElementById('detailRadicado').textContent = data.radicado;
    document.getElementById('detailTipo').textContent = data.tipo;
    document.getElementById('detailEstado').textContent = data.estado;
    document.getElementById('infoRadicado').textContent = data.fecha;
    document.getElementById('infoTiempo').textContent = data.tiempo;
    document.getElementById('infoPaciente').textContent = data.paciente;
    document.getElementById('infoDocumento').textContent = data.documento;
    document.getElementById('infoEmail').textContent = data.email;
    document.getElementById('detailDescripcion').textContent = data.descripcion;

    // Update odontologo
    const odontologoRows = document.querySelectorAll('.info-row');
    odontologoRows.forEach(row => {
        if (row.querySelector('.fa-user-md')) {
            row.querySelector('div:last-child').textContent = `Dr/a. ${data.odontologo}`;
        }
    });

    // Update badges colors
    updateBadgeColors(data);

    // Show panel on mobile
    if (window.innerWidth <= 1200) {
        document.getElementById('detailPanel').classList.add('show');
        document.querySelector('.overlay').classList.add('show');
    }

    // Update status buttons
    updateStatusButtons(data.estado);
}

// Update badge colors based on data
function updateBadgeColors(data) {
    const tipoBadge = document.getElementById('detailTipo');
    const estadoBadge = document.getElementById('detailEstado');

    // Update tipo badge
    tipoBadge.className = 'badge';
    if (data.tipo === 'Queja') tipoBadge.classList.add('badge-queja');
    else if (data.tipo === 'Reclamo') tipoBadge.classList.add('badge-reclamo');
    else if (data.tipo === 'Petición') tipoBadge.classList.add('badge-peticion');

    // Update estado badge
    estadoBadge.className = 'badge';
    if (data.estado === 'Resuelto') estadoBadge.classList.add('badge-resuelto');
    else if (data.estado === 'Pend. Info') estadoBadge.classList.add('badge-pendiente');
    else if (data.estado === 'Cerrado') estadoBadge.classList.add('badge-cerrado');
    else if (data.estado === 'Recibido') estadoBadge.classList.add('badge-recibido');
    else if (data.estado === 'En proceso') estadoBadge.classList.add('badge-proceso');
}

// Update status buttons
function updateStatusButtons(estado) {
    document.querySelectorAll('.status-btn').forEach(btn => {
        btn.classList.remove('active');
    });

    const estadoMap = {
        'Recibido': 'recibido',
        'En proceso': 'proceso',
        'Pend. Info': 'pendiente',
        'Resuelto': 'resuelto',
        'Cerrado': 'cerrado'
    };

    const btnClass = estadoMap[estado];
    if (btnClass) {
        const btn = document.querySelector(`.status-btn.${btnClass}`);
        if (btn) {
            btn.classList.add('active');
        }
    }
}

// Close Detail Panel - FUNCIÓN ACTUALIZADA
function closeDetail() {
    // Ocultar el contenido del detalle
    document.getElementById('detailContent').style.display = 'none';

    // Mostrar el estado vacío
    document.getElementById('emptyState').style.display = 'flex';

    // Quitar la clase 'show' del panel (para móviles)
    document.getElementById('detailPanel').classList.remove('show');

    // Ocultar el overlay
    document.querySelector('.overlay').classList.remove('show');

    // Quitar la selección de todas las filas
    document.querySelectorAll('tbody tr').forEach(row => {
        row.classList.remove('selected');
    });

    // Resetear el ID actual
    currentId = null;
}

// Change Status
function changeStatus(btn, status) {
    document.querySelectorAll('.status-btn').forEach(b => {
        b.classList.remove('active');
    });
    btn.classList.add('active');

    if (currentId) {
        const statusMap = {
            'recibido': 'Recibido',
            'proceso': 'En proceso',
            'pendiente': 'Pend. Info',
            'resuelto': 'Resuelto',
            'cerrado': 'Cerrado'
        };

        document.getElementById('detailEstado').textContent = statusMap[status];

        // Update table row
        const row = document.querySelector(`tr[data-id="${currentId}"]`);
        if (row) {
            const estadoCell = row.querySelector('td:nth-child(5)');
            estadoCell.innerHTML = `<span class="badge badge-${status}">${statusMap[status]}</span>`;

            row.setAttribute('data-estado', status);
        }
    }
}

// Filter Table
function filterTable() {
    const searchInput = document.getElementById('searchInput').value.toLowerCase();
    const filterTipo = document.getElementById('filterTipo').value;
    const filterEstado = document.getElementById('filterEstado').value;

    const rows = document.querySelectorAll('tbody tr');

    rows.forEach(row => {
        const paciente = row.querySelector('.paciente-info strong').textContent.toLowerCase();
        const radicado = row.querySelector('.radicado').textContent.toLowerCase();
        const asunto = row.cells[3].textContent.toLowerCase();
        const tipo = row.getAttribute('data-tipo');
        const estado = row.getAttribute('data-estado');

        const matchesSearch = paciente.includes(searchInput) ||
            radicado.includes(searchInput) ||
            asunto.includes(searchInput);
        const matchesTipo = filterTipo === 'todos' || tipo === filterTipo;
        const matchesEstado = filterEstado === 'todos' || estado === filterEstado;

        if (matchesSearch && matchesTipo && matchesEstado) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    });
}

// Handle window resize
window.addEventListener('resize', function () {
    if (window.innerWidth > 1200) {
        document.getElementById('detailPanel').classList.remove('show');
        document.querySelector('.overlay').classList.remove('show');
    }
});