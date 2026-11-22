using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class CitaDisponibilidadService : ICitaDisponibilidadService
{
    private const int DuracionFallbackMinutos = 60;

    private readonly AppDbContext _context;
    private readonly ILogger<CitaDisponibilidadService> _logger;

    public CitaDisponibilidadService(
        AppDbContext context,
        ILogger<CitaDisponibilidadService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> ObtenerDuracionCitaMinutosAsync(CancellationToken ct = default)
    {
        try
        {
            var cfg = await _context.ConfiguracionesGenerales
                .AsNoTracking()
                .Where(c => c.Clave == "cita_duracion_minutos")
                .Select(c => c.Valor)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(cfg) &&
                int.TryParse(cfg, out int duracion) &&
                duracion > 0)
            {
                return duracion;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo leer cita_duracion_minutos desde Configuracion_General. Se usará el fallback de {Fallback} minutos.",
                DuracionFallbackMinutos);
        }

        return DuracionFallbackMinutos;
    }

    public async Task<int> ObtenerDuracionServicioAsync(int idServicio, CancellationToken ct = default)
    {
        int duracionServicio = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.IdServicio == idServicio)
            .Select(s => s.DuracionMinutos)
            .FirstOrDefaultAsync(ct);

        return duracionServicio > 0
            ? duracionServicio
            : await ObtenerDuracionCitaMinutosAsync(ct);
    }

    public async Task<(bool EsValido, string? Mensaje)> ValidarHorarioClinicaAsync(
        DateTime fechaHora,
        int duracionMinutos = DuracionFallbackMinutos,
        CancellationToken ct = default)
    {
        if (duracionMinutos <= 0)
            duracionMinutos = DuracionFallbackMinutos;

        var claves = new[] { "horario_apertura", "horario_cierre", "dias_atencion" };

        Dictionary<string, string> config;
        try
        {
            config = await _context.ConfiguracionesGenerales
                .AsNoTracking()
                .Where(c => claves.Contains(c.Clave))
                .ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la configuración de horario clínico.");
            return (false,
                "No fue posible validar el horario de la clínica. Intente nuevamente más tarde.");
        }

        var diasPermitidos = new HashSet<int> { 1, 2, 3, 4, 5, 6 };
        if (config.TryGetValue("dias_atencion", out string? diasValor) &&
            !string.IsNullOrWhiteSpace(diasValor))
        {
            var diasConfigurados = diasValor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, out int n) ? n : -1)
                .Where(n => n >= 1 && n <= 7)
                .ToHashSet();

            if (diasConfigurados.Count > 0)
                diasPermitidos = diasConfigurados.Where(dia => dia <= 6).ToHashSet();
        }

        int diaIso = fechaHora.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)fechaHora.DayOfWeek;
        if (!diasPermitidos.Contains(diaIso))
        {
            string nombreDia = fechaHora.ToString("dddd",
                new System.Globalization.CultureInfo("es-CO"));

            return (false,
                $"La clínica no tiene atención los {nombreDia}. Por favor seleccione un día de atención.");
        }

        TimeOnly horaApertura = TimeOnly.FromTimeSpan(TimeSpan.FromHours(7));
        if (config.TryGetValue("horario_apertura", out string? aperturaValor) &&
            TimeOnly.TryParseExact(aperturaValor, "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out TimeOnly horaAperturaConfig))
        {
            horaApertura = horaAperturaConfig;
        }

        TimeOnly horaCierre = TimeOnly.FromTimeSpan(TimeSpan.FromHours(18));
        if (config.TryGetValue("horario_cierre", out string? cierreValor) &&
            TimeOnly.TryParseExact(cierreValor, "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out TimeOnly horaCierreConfig))
        {
            horaCierre = horaCierreConfig;
        }

        TimeOnly horaSolicitada = TimeOnly.FromDateTime(fechaHora);
        TimeOnly horaFinSolicitada = TimeOnly.FromDateTime(fechaHora.AddMinutes(duracionMinutos));

        if (horaSolicitada < horaApertura)
        {
            return (false,
                $"La cita no puede agendarse antes de las {horaApertura:HH:mm}, que es el horario de apertura de la clínica.");
        }

        if (horaSolicitada >= horaCierre || horaFinSolicitada > horaCierre)
        {
            return (false,
                $"La cita debe finalizar antes de las {horaCierre:HH:mm}, que es el horario de cierre de la clínica.");
        }

        return (true, null);
    }

    public async Task<List<Profesional>> ObtenerProfesionalesElegiblesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default)
    {
        var servicio = await _context.Servicios
            .AsNoTracking()
            .Include(s => s.Especialidad)
            .Where(s => s.IdServicio == idServicio && s.Estado == "activo")
            .Select(s => new { s.IdEspecialidad, NombreEspecialidad = s.Especialidad != null ? s.Especialidad.Nombre : null })
            .FirstOrDefaultAsync(ct);
        if (servicio?.IdEspecialidad is not > 0)
            return [];

        var profesionalesConServicioAsignado = await _context.ProfesionalServicios
            .AsNoTracking()
            .Where(ps => ps.IdServicio == idServicio && ps.Activo)
            .Select(ps => ps.IdProfesional)
            .ToHashSetAsync(ct);

        var profesionales = await _context.Profesionales
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .Where(p => p.Estado == "activo")
            .ToListAsync(ct);

        var serviciosDeshabilitados = await _context.ProfesionalServicios
            .AsNoTracking()
            .Where(ps => ps.IdServicio == idServicio && !ps.Activo)
            .Select(ps => ps.IdProfesional)
            .ToHashSetAsync(ct);

        return profesionales
            .Where(profesional =>
            {
                if (!profesionalesConServicioAsignado.Contains(profesional.IdProfesional) ||
                    serviciosDeshabilitados.Contains(profesional.IdProfesional))
                    return false;

                bool tieneEspecialidades = profesional.Especialidades.Count > 0;
                bool esServicioGeneral = servicio.NombreEspecialidad is not null &&
                    servicio.NombreEspecialidad.Contains("general", StringComparison.OrdinalIgnoreCase);
                return profesional.Especialidades.Any(pe =>
                           pe.IdEspecialidad == servicio.IdEspecialidad.Value) ||
                       (!tieneEspecialidades &&
                        esServicioGeneral);
            })
            .ToList();
    }

    public async Task<(bool EsValida, string? Mensaje)> ValidarDisponibilidadProfesionalAsync(
        int idProfesional,
        int idServicio,
        DateTime fechaHora,
        int duracionMinutos,
        CancellationToken ct = default)
    {
        if (idProfesional <= 0)
            return (false, "Debe seleccionar un profesional válido.");

        if (idServicio <= 0)
            return (false, "Debe seleccionar un servicio válido.");

        var servicio = await _context.Servicios.AsNoTracking()
            .Where(s => s.IdServicio == idServicio && s.Estado == "activo")
            .Select(s => new { s.IdEspecialidad })
            .FirstOrDefaultAsync(ct);

        if (servicio is null)
            return (false, "El servicio seleccionado no existe o no está activo.");
        if (servicio.IdEspecialidad is not > 0)
            return (false, "El servicio no tiene una especialidad configurada y no puede asignarse.");

        var profesionalesElegibles = await ObtenerProfesionalesElegiblesPorServicioAsync(idServicio, ct);
        if (!profesionalesElegibles.Any(p => p.IdProfesional == idProfesional))
            return (false, "El profesional no tiene una asignación activa del servicio o sus especialidades no coinciden.");

        DateTime fin = fechaHora.AddMinutes(duracionMinutos);
        DateOnly fecha = DateOnly.FromDateTime(fechaHora);

        bool tieneAusencia = await _context.AusenciasProfesional.AsNoTracking()
            .AnyAsync(a => a.IdProfesional == idProfesional &&
                           a.FechaInicio <= fecha &&
                           a.FechaFin >= fecha, ct);

        bool tieneBloqueo = await _context.BloqueosProfesional.AsNoTracking()
            .AnyAsync(b => b.IdProfesional == idProfesional &&
                           b.FechaInicio < fin &&
                           b.FechaFin > fechaHora, ct);

        var horarios = await _context.HorariosProfesional.AsNoTracking()
            .Where(h => h.IdProfesional == idProfesional && h.Activo)
            .ToListAsync(ct);

        if (tieneAusencia)
            return (false, "El profesional no está disponible en la fecha seleccionada por una ausencia registrada.");

        if (tieneBloqueo)
            return (false, "El profesional no está disponible en el horario seleccionado por un bloqueo registrado.");

        if (horarios.Count == 0)
            return (false, "El profesional no tiene horario de atención configurado y no puede recibir citas.");

        string dia = NombreDia(fechaHora.DayOfWeek);
        TimeOnly inicio = TimeOnly.FromDateTime(fechaHora);
        TimeOnly finCita = TimeOnly.FromDateTime(fin);
        bool dentroDeHorario = horarios.Any(h =>
            NormalizarTexto(h.DiaSemana) == NormalizarTexto(dia) &&
            h.HoraInicio <= inicio &&
            h.HoraFin >= finCita);

        return dentroDeHorario
            ? (true, null)
            : (false, "La cita está fuera del horario de atención del profesional.");
    }

    public async Task<List<TimeSpan>> ObtenerHorariosSolicitudPacienteAsync(
        int idPaciente,
        int idServicio,
        int? idProfesionalPreferido,
        DateTime fecha,
        CancellationToken ct = default)
    {
        if (idProfesionalPreferido is > 0)
        {
            return await ObtenerHorariosDisponiblesPacienteAsync(
                idPaciente, idServicio, idProfesionalPreferido.Value, fecha, ct);
        }

        var profesionales = await ObtenerProfesionalesElegiblesPorServicioAsync(idServicio, ct);
        var horarios = new SortedSet<TimeSpan>();
        foreach (var profesional in profesionales)
        {
            var disponibles = await ObtenerHorariosDisponiblesPacienteAsync(
                idPaciente, idServicio, profesional.IdProfesional, fecha, ct);
            horarios.UnionWith(disponibles);
        }

        return horarios.ToList();
    }

    public async Task<List<TimeSpan>> ObtenerHorariosDisponiblesPacienteAsync(
        int idPaciente,
        int idServicio,
        int idProfesional,
        DateTime fecha,
        CancellationToken ct = default)
    {
        if (idPaciente <= 0 || idServicio <= 0 || idProfesional <= 0 ||
            fecha.Date < ColombiaClock.Today)
            return [];

        int duracion = await ObtenerDuracionServicioAsync(idServicio, ct);
        var horariosProfesional = await _context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.IdProfesional == idProfesional && h.Activo)
            .ToListAsync(ct);

        string dia = NormalizarTexto(NombreDia(fecha.DayOfWeek));
        var candidatos = new SortedSet<TimeSpan>();
        foreach (var horario in horariosProfesional.Where(h =>
                     NormalizarTexto(h.DiaSemana) == dia))
        {
            TimeSpan hora = horario.HoraInicio.ToTimeSpan();
            TimeSpan finHorario = horario.HoraFin.ToTimeSpan();
            while (hora + TimeSpan.FromMinutes(duracion) <= finHorario)
            {
                candidatos.Add(hora);
                hora = hora.Add(TimeSpan.FromMinutes(30));
            }
        }

        if (candidatos.Count == 0)
            return [];

        var profesionalesElegibles = await ObtenerProfesionalesElegiblesPorServicioAsync(idServicio, ct);
        if (!profesionalesElegibles.Any(p => p.IdProfesional == idProfesional))
            return [];

        DateOnly fechaCita = DateOnly.FromDateTime(fecha);
        bool tieneAusencia = await _context.AusenciasProfesional
            .AsNoTracking()
            .AnyAsync(a =>
                a.IdProfesional == idProfesional &&
                a.FechaInicio <= fechaCita &&
                a.FechaFin >= fechaCita,
                ct);
        if (tieneAusencia)
            return [];

        var bloqueosProfesional = await _context.BloqueosProfesional
            .AsNoTracking()
            .Where(b =>
                b.IdProfesional == idProfesional &&
                b.FechaInicio < fecha.Date.AddDays(1) &&
                b.FechaFin > fecha.Date)
            .ToListAsync(ct);

        var consultorios = await _context.Consultorios
            .AsNoTracking()
            .Where(c => c.Estado == "disponible" || c.Estado == "activo")
            .OrderBy(c => c.IdConsultorio)
            .Select(c => c.IdConsultorio)
            .ToListAsync(ct);
        if (consultorios.Count == 0)
            return [];

        DateTime inicioBusqueda = fecha.Date;
        DateTime finBusqueda = inicioBusqueda.AddDays(1);
        var citasExistentes = await _context.Citas
            .AsNoTracking()
            .Include(c => c.EstadoCita)
            .Where(c =>
                (c.IdProfesional == idProfesional ||
                 c.IdPaciente == idPaciente ||
                 (c.IdConsultorio.HasValue && consultorios.Contains(c.IdConsultorio.Value))) &&
                c.FechaHora < finBusqueda &&
                c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracion) > inicioBusqueda)
            .ToListAsync(ct);

        var citasActivas = citasExistentes
            .Where(c => !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                ((c.EstadoCita?.NombreEstado ?? c.Estado) ?? string.Empty).Trim().ToLower()))
            .ToList();

        bool SeSolapa(Cita cita, DateTime inicio)
        {
            DateTime fin = inicio.AddMinutes(duracion);
            DateTime finCita = cita.FechaHora.AddMinutes(
                cita.DuracionMinutos > 0 ? cita.DuracionMinutos : duracion);
            return cita.FechaHora < fin && finCita > inicio;
        }

        var disponibles = new List<TimeSpan>();
        foreach (TimeSpan hora in candidatos)
        {
            DateTime fechaHora = fecha.Date.Add(hora);
            if (fechaHora <= ColombiaClock.Now)
                continue;

            var (horarioClinicaValido, _) = await ValidarHorarioClinicaAsync(fechaHora, duracion, ct);
            if (!horarioClinicaValido)
                continue;

            DateTime finHorario = fechaHora.AddMinutes(duracion);
            if (bloqueosProfesional.Any(b =>
                    b.FechaInicio < finHorario &&
                    b.FechaFin > fechaHora))
                continue;

            if (citasActivas.Any(c =>
                    (c.IdProfesional == idProfesional || c.IdPaciente == idPaciente) &&
                    SeSolapa(c, fechaHora)))
                continue;

            bool hayConsultorioDisponible = consultorios.Any(idConsultorio =>
                !citasActivas.Any(c =>
                    c.IdConsultorio == idConsultorio &&
                    SeSolapa(c, fechaHora)));
            if (hayConsultorioDisponible)
                disponibles.Add(hora);
        }

        return disponibles;
    }

    public async Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default)
    {
        if (idServicio <= 0)
            return [];

        var profesionales = await ObtenerProfesionalesElegiblesPorServicioAsync(idServicio, ct);

        return profesionales
            .OrderBy(profesional => profesional.Nombres)
            .ThenBy(profesional => profesional.Apellidos)
            .Select(profesional => new ProfesionalDisponibleDto
            {
                IdProfesional = profesional.IdProfesional,
                NombreCompleto = $"{profesional.Nombres} {profesional.Apellidos}".Trim()
            }).ToList();
    }

    public async Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesDisponiblesAsync(
        DateTime fecha,
        TimeSpan horaInicio,
        int duracionMinutos = 60,
        int? idServicio = null,
        CancellationToken ct = default)
    {
        if (idServicio is not > 0)
            return [];

        if (duracionMinutos <= 0)
            duracionMinutos = await ObtenerDuracionCitaMinutosAsync(ct);

        DateTime inicio = fecha.Date.Add(horaInicio);
        DateTime fin = inicio.AddMinutes(duracionMinutos);

        var (esHorarioClinicaValido, _) = await ValidarHorarioClinicaAsync(inicio, duracionMinutos, ct);
        if (!esHorarioClinicaValido)
            return [];

        var profesionales = await ObtenerProfesionalesElegiblesPorServicioAsync(idServicio.Value, ct);
        if (profesionales.Count == 0)
            return [];

        var profesionalIds = profesionales.Select(p => p.IdProfesional).ToList();
        var horariosProfesional = await _context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.Activo && profesionalIds.Contains(h.IdProfesional))
            .ToListAsync(ct);

        string dia = NormalizarTexto(NombreDia(inicio.DayOfWeek));
        TimeOnly horaInicioCita = TimeOnly.FromDateTime(inicio);
        TimeOnly horaFinCita = TimeOnly.FromDateTime(fin);
        var profesionalesConHorario = horariosProfesional
            .Where(h =>
                NormalizarTexto(h.DiaSemana) == dia &&
                h.HoraInicio <= horaInicioCita &&
                h.HoraFin >= horaFinCita)
            .Select(h => h.IdProfesional)
            .ToHashSet();

        if (profesionalesConHorario.Count == 0)
            return [];

        var profesionalesAusentes = await _context.AusenciasProfesional
            .AsNoTracking()
            .Where(a =>
                profesionalesConHorario.Contains(a.IdProfesional) &&
                a.FechaInicio <= DateOnly.FromDateTime(inicio) &&
                a.FechaFin >= DateOnly.FromDateTime(inicio))
            .Select(a => a.IdProfesional)
            .Distinct()
            .ToListAsync(ct);

        var profesionalesBloqueados = await _context.BloqueosProfesional
            .AsNoTracking()
            .Where(b =>
                profesionalesConHorario.Contains(b.IdProfesional) &&
                b.FechaInicio < fin &&
                b.FechaFin > inicio)
            .Select(b => b.IdProfesional)
            .Distinct()
            .ToListAsync(ct);

        var profesionalesOcupados = await _context.Citas
            .AsNoTracking()
            .Where(c =>
                c.IdProfesional.HasValue &&
                profesionalesConHorario.Contains(c.IdProfesional.Value) &&
                !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                    ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
                c.FechaHora < fin &&
                c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracionMinutos) > inicio)
            .Select(c => c.IdProfesional!.Value)
            .Distinct()
            .ToListAsync(ct);

        var noDisponibles = profesionalesAusentes
            .Concat(profesionalesBloqueados)
            .Concat(profesionalesOcupados)
            .ToHashSet();

        var resultado = new List<ProfesionalDisponibleDto>();
        foreach (var profesional in profesionales)
        {
            if (!profesionalesConHorario.Contains(profesional.IdProfesional) ||
                noDisponibles.Contains(profesional.IdProfesional))
                continue;

            string nombre = profesional.Usuario is not null
                ? $"{profesional.Usuario.Nombre} {profesional.Usuario.Apellidos}".Trim()
                : $"{profesional.Nombres} {profesional.Apellidos}".Trim();

            string especialidades = string.Join(", ", profesional.Especialidades
                .Where(e => e.Especialidad is not null)
                .Select(e => e.Especialidad!.Nombre));

            resultado.Add(new ProfesionalDisponibleDto
            {
                IdProfesional = profesional.IdProfesional,
                NombreCompleto = string.IsNullOrWhiteSpace(nombre) ? "Profesional" : nombre,
                Especialidades = string.IsNullOrWhiteSpace(especialidades) ? "Sin especialidad registrada" : especialidades,
                FotoUrl = null
            });
        }

        return resultado;
    }

    private static string NombreDia(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miercoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sabado",
        _ => "Domingo"
    };

    private static string NormalizarTexto(string valor)
    {
        return string.Concat(valor.Normalize(System.Text.NormalizationForm.FormD)
            .Where(caracter => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant();
    }
}
