using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public sealed class AgendaService(
    AppDbContext context,
    ICitaService citaService) : IAgendaService
{
    private readonly AppDbContext _context = context;
    private readonly ICitaService _citaService = citaService;

    public async Task<AgendaViewModel> ObtenerAgendaAsync(
        DateTime? weekStart,
        int? professionalId,
        int? officeId,
        CancellationToken cancellationToken = default)
    {
        var inicioSemana = ObtenerLunes(weekStart?.Date ?? DateTime.Today);
        var finSemana = inicioSemana.AddDays(7);
        var duracion = await _citaService
            .ObtenerDuracionCitaMinutosAsync(cancellationToken);
        var diasAtencion = await ObtenerDiasAtencionAsync(cancellationToken);
        var horario = await ObtenerHorarioClinicaAsync(cancellationToken);

        // Si no se especificó un filtro de profesional (carga inicial al iniciar sesión),
        // se establece por defecto el profesional correspondiente a prof@smiletrack.co.
        if (!professionalId.HasValue)
        {
            var defaultProfId = await _context.Profesionales
                .AsNoTracking()
                .Where(p => p.Usuario != null && p.Usuario.Correo == "prof@smiletrack.co")
                .Select(p => (int?)p.IdProfesional)
                .FirstOrDefaultAsync(cancellationToken);

            if (defaultProfId.HasValue && defaultProfId.Value > 0)
            {
                professionalId = defaultProfId;
            }
        }

        var query = _context.Citas
            .AsNoTracking()
            .Where(c => c.FechaHora >= inicioSemana && c.FechaHora < finSemana);

        if (professionalId is > 0)
            query = query.Where(c => c.IdProfesional == professionalId.Value);

        if (officeId is > 0)
            query = query.Where(c => c.IdConsultorio == officeId.Value);

        var citas = await query
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Consultorio)
            .Include(c => c.Servicio)
            .Include(c => c.EstadoCita)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(cancellationToken);

        var dias = Enumerable.Range(0, 7)
            .Select(offset => CrearDia(inicioSemana.AddDays(offset), citas, duracion, diasAtencion))
            .ToList();

        return new AgendaViewModel
        {
            WeekStart = inicioSemana,
            SemanaLabel = $"{inicioSemana:dd/MM} - {finSemana.AddDays(-1):dd/MM}",
            Dias = dias,
            Pacientes = await ObtenerPacientesAsync(cancellationToken),
            Profesionales = await ObtenerProfesionalesAsync(cancellationToken),
            Consultorios = await ObtenerConsultoriosAsync(cancellationToken),
            Servicios = await ObtenerServiciosAsync(cancellationToken),
            ProfessionalId = professionalId,
            OfficeId = officeId,
            DuracionCitaMinutos = duracion,
            DiasAtencion = diasAtencion,
            HorarioApertura = horario.Apertura,
            HorarioCierre = horario.Cierre,
            DiasAtencionTexto = ObtenerDiasTexto(diasAtencion)
        };
    }

    private static AgendaDiaViewModel CrearDia(
        DateTime fecha,
        IReadOnlyCollection<Cita> citas,
        int duracion,
        IReadOnlySet<DayOfWeek> diasAtencion)
    {
        return new AgendaDiaViewModel
        {
            Fecha = fecha,
            NombreDia = fecha.ToString("ddd", new System.Globalization.CultureInfo("es-CO")),
            NumeroDia = fecha.Day.ToString(),
            EsHoy = fecha.Date == DateTime.Today,
            Cerrado = !diasAtencion.Contains(fecha.DayOfWeek),
            Citas = citas
                .Where(c => c.FechaHora >= fecha.Date && c.FechaHora < fecha.Date.AddDays(1))
                .Select(c => MapearCita(c, duracion))
                .ToList()
        };
    }

    private static AgendaCitaViewModel MapearCita(Cita cita, int duracion)
    {
        var nombrePaciente = $"{cita.Paciente?.Nombres} {cita.Paciente?.Apellidos}".Trim();

        var nombresProf = cita.Profesional?.Usuario?.Nombre?.Trim();
        var apellidosProf = cita.Profesional?.Usuario?.Apellidos?.Trim();
        if (string.IsNullOrWhiteSpace(nombresProf)) nombresProf = cita.Profesional?.Nombres?.Trim();
        if (string.IsNullOrWhiteSpace(apellidosProf)) apellidosProf = cita.Profesional?.Apellidos?.Trim();
        var nombreProfesional = $"{nombresProf} {apellidosProf}".Trim();

        var correoProfesional = cita.Profesional?.Usuario?.Correo ?? string.Empty;
        var telefonoProfesional = cita.Profesional?.Telefono ?? string.Empty;
        var registroMedicoProfesional = cita.Profesional?.RegistroMedico ?? string.Empty;
        var estadoUsuarioProfesional = cita.Profesional?.Usuario?.Estado
                                      ?? cita.Profesional?.Estado
                                      ?? string.Empty;

        var estado = cita.EstadoCita?.NombreEstado ?? cita.Estado;
        return new AgendaCitaViewModel
        {
            Id = cita.IdCita,
            IdPaciente = cita.IdPaciente,
            IdProfesional = cita.IdProfesional ?? 0,
            IdConsultorio = cita.IdConsultorio ?? 0,
            IdServicio = cita.IdServicio ?? 0,
            Fecha = cita.FechaHora.Date,
            Hora = cita.FechaHora.ToString("HH:mm"),
            HoraInicio = cita.FechaHora.ToString("HH:mm"),
            HoraFin = cita.FechaHora.AddMinutes(duracion).ToString("HH:mm"),
            Paciente = string.IsNullOrWhiteSpace(nombrePaciente) ? "Paciente sin datos" : nombrePaciente,
            NombreProfesional = string.IsNullOrWhiteSpace(nombreProfesional) ? "Sin profesional" : nombreProfesional,
            CorreoProfesional = correoProfesional,
            TelefonoProfesional = telefonoProfesional,
            RegistroMedicoProfesional = registroMedicoProfesional,
            EstadoUsuarioProfesional = estadoUsuarioProfesional,
            Servicio = cita.Servicio?.Nombre ?? "Consulta",
            Consultorio = cita.Consultorio?.Nombre ?? "Sin asignar",
            Estado = estado,
            ClaseEstado = ObtenerClaseEstado(estado),
            Notas = cita.Notas ?? string.Empty
        };
    }

    private async Task<IReadOnlyList<SelectOptionViewModel>> ObtenerPacientesAsync(CancellationToken ct) =>
        await _context.Pacientes.AsNoTracking()
            .Where(p => p.Estado == "activo")
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .Select(p => new SelectOptionViewModel { Id = p.IdPaciente, Text = $"{p.Apellidos}, {p.Nombres}" })
            .ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOptionViewModel>> ObtenerProfesionalesAsync(CancellationToken ct)
    {
        var profesionales = await _context.Profesionales.AsNoTracking()
            .Where(p => p.Estado == "activo")
            .Select(p => new
            {
                p.IdProfesional,
                p.Nombres,
                p.Apellidos,
                NombreUsuario = p.Usuario != null ? p.Usuario.Nombre : null,
                ApellidosUsuario = p.Usuario != null ? p.Usuario.Apellidos : null
            })
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .ToListAsync(ct);

        return profesionales.Select(p => new SelectOptionViewModel
        {
            Id = p.IdProfesional,
            Text = (
                (string.IsNullOrWhiteSpace(p.NombreUsuario) ? p.Nombres : p.NombreUsuario) + " " +
                (string.IsNullOrWhiteSpace(p.ApellidosUsuario) ? p.Apellidos : p.ApellidosUsuario)
            ).Trim()
        }).ToList();
    }

    private async Task<IReadOnlyList<SelectOptionViewModel>> ObtenerConsultoriosAsync(CancellationToken ct) =>
        await _context.Consultorios.AsNoTracking()
            .Where(c => c.Estado == "activo" || c.Estado == "disponible")
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectOptionViewModel { Id = c.IdConsultorio, Text = c.Nombre })
            .ToListAsync(ct);

    private async Task<IReadOnlyList<SelectOptionViewModel>> ObtenerServiciosAsync(CancellationToken ct) =>
        await _context.Servicios.AsNoTracking()
            .Where(s => s.Estado == "activo")
            .OrderBy(s => s.Nombre)
            .Select(s => new SelectOptionViewModel { Id = s.IdServicio, Text = s.Nombre })
            .ToListAsync(ct);

    private async Task<IReadOnlySet<DayOfWeek>> ObtenerDiasAtencionAsync(CancellationToken ct)
    {
        var valor = await _context.ConfiguracionesGenerales
            .AsNoTracking()
            .Where(c => c.Clave == "dias_atencion")
            .Select(c => c.Valor)
            .FirstOrDefaultAsync(ct);

        var dias = new HashSet<DayOfWeek>();
        foreach (var token in (valor ?? "1,2,3,4,5,6").Split(','))
        {
            if (int.TryParse(token.Trim(), out var dia) && dia is >= 1 and <= 7)
                dias.Add(dia == 7 ? DayOfWeek.Sunday : (DayOfWeek)dia);
        }

        return dias;
    }

    private async Task<(string Apertura, string Cierre)> ObtenerHorarioClinicaAsync(CancellationToken ct)
    {
        var valores = await _context.ConfiguracionesGenerales
            .AsNoTracking()
            .Where(c => c.Clave == "horario_apertura" || c.Clave == "horario_cierre")
            .ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);

        return (
            valores.GetValueOrDefault("horario_apertura", "07:00"),
            valores.GetValueOrDefault("horario_cierre", "18:00"));
    }

    private static string ObtenerDiasTexto(IReadOnlySet<DayOfWeek> dias)
    {
        var nombres = new[] { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
        return string.Join(", ", Enum.GetValues<DayOfWeek>()
            .Where(dias.Contains)
            .Select(dia => nombres[(int)dia]));
    }

    private static DateTime ObtenerLunes(DateTime fecha)
    {
        var diferencia = ((int)fecha.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return fecha.AddDays(-diferencia).Date;
    }

    private static string ObtenerClaseEstado(string? estado) =>
        (estado ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "atendida" or "completada" or "realizada" => "attended",
            "cancelada" or "cancelado" or "no_asistida" or "no asistio" or "no asistió" or "no-show" => "cancelled",
            "confirmada" => "confirmed",
            "programada" or "agendada" or "pendiente" => "reserved",
            _ => "reserved"
        };
}
