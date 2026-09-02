using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Implementación del servicio de citas con la lógica de negocio extraída del controlador,
/// manteniendo el mismo comportamiento del módulo legacy sin cambiar rutas ni contratos HTTP.
///
/// CAMBIOS v2 (resolución de hallazgos C-01, C-02, C-04):
///  - DuracionCitaMinutos pasa a ser configurable desde Configuracion_General
///    (clave "cita_duracion_minutos"). Si no existe, cae a 60 como fallback.
///  - HayConflictoHorarioAsync ahora delega a VerificarConflictoCompletoAsync.
///  - VerificarConflictoCompletoAsync valida TRES recursos: Profesional, Paciente
///    y Consultorio, eliminando la posibilidad de citas solapadas por cualquiera
///    de los tres.
///  - CrearAsync y ActualizarAsync ya no usan la constante hardcodeada; leen la
///    duración desde ConfiguracionGeneral una sola vez por operación.
/// </summary>
public class CitaService : ICitaService
{
    /// <summary>
    /// Fallback de duración cuando Configuracion_General no tiene la clave
    /// "cita_duracion_minutos" o su valor no es un entero positivo.
    /// </summary>
    private const int DuracionFallbackMinutos = 60;

    private readonly AppDbContext _context;
    private readonly ILogger<CitaService> _logger;

    public CitaService(AppDbContext context, ILogger<CitaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // =========================================================================
    // DURACIÓN CONFIGURABLE (C-04)
    // =========================================================================

    /// <inheritdoc/>
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
                "No se pudo leer cita_duracion_minutos desde Configuracion_General. " +
                "Se usará el fallback de {Fallback} minutos.",
                DuracionFallbackMinutos);
        }

        return DuracionFallbackMinutos;
    }

    // =========================================================================
    // VALIDACIÓN HORARIO DE ATENCIÓN CLÍNICA (C-05)
    // =========================================================================

    /// <inheritdoc/>
    public async Task<(bool EsValido, string? Mensaje)> ValidarHorarioClinicaAsync(
        DateTime fechaHora,
        CancellationToken ct = default)
    {
        // Leer las tres claves de configuración de una sola query
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
            _logger.LogWarning(
                ex,
                "No se pudo leer la configuración de horario clínico. " +
                "Se omite la validación de horario.");

            // Si no hay config, se permite la cita (fail-open).
            return (true, null);
        }

        // ── Días de atención ─────────────────────────────────────────────
        // Los días se almacenan como "1,2,3,4,5,6" donde 1=Lunes…7=Domingo
        // (ISO 8601). DayOfWeek usa 0=Domingo…6=Sábado, por eso se convierte.
        if (config.TryGetValue("dias_atencion", out string? diasValor) &&
            !string.IsNullOrWhiteSpace(diasValor))
        {
            var diasPermitidos = diasValor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, out int n) ? n : -1)
                .Where(n => n >= 1 && n <= 7)
                .ToHashSet();

            if (diasPermitidos.Count > 0)
            {
                // Convertir DayOfWeek a numeración ISO (1=Lun…7=Dom)
                int diaIso = fechaHora.DayOfWeek == DayOfWeek.Sunday
                    ? 7
                    : (int)fechaHora.DayOfWeek;

                if (!diasPermitidos.Contains(diaIso))
                {
                    string nombreDia = fechaHora.ToString("dddd",
                        new System.Globalization.CultureInfo("es-CO"));

                    return (false,
                        $"La clínica no tiene atención los {nombreDia}. " +
                        "Por favor seleccione un día hábil de atención.");
                }
            }
        }

        // ── Hora de apertura ─────────────────────────────────────────────
        TimeOnly horaApertura = TimeOnly.FromTimeSpan(TimeSpan.FromHours(7)); // default 07:00
        if (config.TryGetValue("horario_apertura", out string? aperturaValor) &&
            TimeOnly.TryParseExact(aperturaValor, "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out TimeOnly horaAperturaConfig))
        {
            horaApertura = horaAperturaConfig;
        }

        // ── Hora de cierre ───────────────────────────────────────────────
        TimeOnly horaCierre = TimeOnly.FromTimeSpan(TimeSpan.FromHours(18)); // default 18:00
        if (config.TryGetValue("horario_cierre", out string? cierreValor) &&
            TimeOnly.TryParseExact(cierreValor, "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out TimeOnly horaCierreConfig))
        {
            horaCierre = horaCierreConfig;
        }

        TimeOnly horasolicitada = TimeOnly.FromDateTime(fechaHora);

        if (horasolicitada < horaApertura)
        {
            return (false,
                $"La cita no puede agendarse antes de las {horaApertura:HH:mm}, " +
                "que es el horario de apertura de la clínica.");
        }

        if (horasolicitada >= horaCierre)
        {
            return (false,
                $"La cita no puede agendarse a partir de las {horaCierre:HH:mm}, " +
                "que es el horario de cierre de la clínica.");
        }

        return (true, null);
    }
    // =========================================================================
    // CONFLICTO DE HORARIO — VALIDACIÓN INTEGRAL (C-01)
    // =========================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Evalúa en secuencia: Profesional → Paciente → Consultorio.
    /// Devuelve el primer conflicto encontrado con un mensaje de negocio claro.
    /// Los estados cancelados (cancelada / cancelado) se excluyen del chequeo.
    /// </remarks>
    public async Task<ConflictoCitaResult> VerificarConflictoCompletoAsync(
        int? idProfesional,
        int idPaciente,
        int? idConsultorio,
        DateTime fechaHora,
        int duracionMinutos = DuracionFallbackMinutos,
        int? idCitaExcluir = null,
        CancellationToken ct = default)
    {
        if (duracionMinutos <= 0)
            duracionMinutos = DuracionFallbackMinutos;

        DateTime inicio = fechaHora;
        DateTime fin = fechaHora.AddMinutes(duracionMinutos);
        int excluir = idCitaExcluir ?? 0;

        // ── 1. Conflicto de Profesional ───────────────────────────────────
        if (idProfesional is > 0)
        {
            bool conflictoProfesional = await _context.Citas
                .AsNoTracking()
                .AnyAsync(c =>
                    c.IdCita != excluir &&
                    c.IdProfesional == idProfesional &&
                    !EsEstadoCancelado(c.Estado) &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(duracionMinutos) > inicio,
                    ct);

            if (conflictoProfesional)
                return ConflictoCitaResult.ConflictoProfesional();
        }

        // ── 2. Conflicto de Paciente ──────────────────────────────────────
        if (idPaciente > 0)
        {
            bool conflictoPaciente = await _context.Citas
                .AsNoTracking()
                .AnyAsync(c =>
                    c.IdCita != excluir &&
                    c.IdPaciente == idPaciente &&
                    !EsEstadoCancelado(c.Estado) &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(duracionMinutos) > inicio,
                    ct);

            if (conflictoPaciente)
                return ConflictoCitaResult.ConflictoPaciente();
        }

        // ── 3. Conflicto de Consultorio ───────────────────────────────────
        if (idConsultorio is > 0)
        {
            bool conflictoConsultorio = await _context.Citas
                .AsNoTracking()
                .AnyAsync(c =>
                    c.IdCita != excluir &&
                    c.IdConsultorio == idConsultorio &&
                    !EsEstadoCancelado(c.Estado) &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(duracionMinutos) > inicio,
                    ct);

            if (conflictoConsultorio)
                return ConflictoCitaResult.ConflictoConsultorio();
        }

        return ConflictoCitaResult.SinConflicto();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Wrapper de compatibilidad. Delega a <see cref="VerificarConflictoCompletoAsync"/>
    /// con idPaciente=0 e idConsultorio=null para mantener contratos existentes.
    /// Los llamadores nuevos deben usar directamente <see cref="VerificarConflictoCompletoAsync"/>.
    /// </remarks>
    public async Task<bool> HayConflictoHorarioAsync(
        int? idProfesional,
        DateTime fechaHora,
        int? idCitaExcluir = null,
        CancellationToken ct = default)
    {
        int duracion = await ObtenerDuracionCitaMinutosAsync(ct);

        var resultado = await VerificarConflictoCompletoAsync(
            idProfesional: idProfesional,
            idPaciente: 0,            // omitir chequeo de paciente (compatibilidad)
            idConsultorio: null,      // omitir chequeo de consultorio (compatibilidad)
            fechaHora: fechaHora,
            duracionMinutos: duracion,
            idCitaExcluir: idCitaExcluir,
            ct: ct);

        return resultado.HayConflicto;
    }

    // =========================================================================
    // LISTADO PAGINADO
    // =========================================================================

    public async Task<(List<Cita> Items, int TotalRecords)> ObtenerAsync(
        int page,
        int pageSize,
        string? role,
        int? idPaciente = null,
        int? idProfesional = null,
        int? idUsuario = null,
        CancellationToken ct = default)
    {
        IQueryable<Cita> query = _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Include(c => c.EstadoCita);

        if (!string.IsNullOrWhiteSpace(role) && role.Equals("Paciente", StringComparison.OrdinalIgnoreCase))
        {
            if (idPaciente is > 0)
            {
                query = query.Where(c => c.IdPaciente == idPaciente.Value);
            }
            else
            {
                return (new List<Cita>(), 0);
            }
        }
        else if (!string.IsNullOrWhiteSpace(role) && role.Equals("Profesional", StringComparison.OrdinalIgnoreCase))
        {
            int resolvedIdProfesional = idProfesional ?? 0;

            if (resolvedIdProfesional <= 0 && idUsuario is > 0)
            {
                resolvedIdProfesional = await _context.Profesionales
                    .AsNoTracking()
                    .Where(p => p.IdUsuario == idUsuario.Value)
                    .Select(p => p.IdProfesional)
                    .FirstOrDefaultAsync(ct);
            }

            if (resolvedIdProfesional <= 0)
            {
                return (new List<Cita>(), 0);
            }

            query = query.Where(c => c.IdProfesional == resolvedIdProfesional);
        }

        int totalRecords = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.FechaHora)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 500))
            .Take(Math.Clamp(pageSize, 1, 500))
            .ToListAsync(ct);

        return (items, totalRecords);
    }

    // =========================================================================
    // OBTENER POR ID
    // =========================================================================

    public async Task<Cita?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Citas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCita == id, ct);
    }

    // =========================================================================
    // CREAR CITA (C-01, C-04)
    // =========================================================================

    public async Task<Cita> CrearAsync(CitaApiRequest request, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.IdPaciente <= 0)
            throw new InvalidOperationException("El paciente seleccionado no es válido.");

        if (request.IdProfesional is <= 0 ||
            !await _context.Profesionales.AnyAsync(p => p.IdProfesional == request.IdProfesional && p.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El profesional seleccionado no está disponible.");
        }

        if (request.IdServicio is <= 0 ||
            !await _context.Servicios.AnyAsync(s => s.IdServicio == request.IdServicio && s.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El servicio seleccionado no está disponible.");
        }

        if (request.IdConsultorio is <= 0 ||
            !await _context.Consultorios.AnyAsync(c => c.IdConsultorio == request.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
        {
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
        }

        if (request.FechaHora < DateTime.Now.AddMinutes(-5))
        {
            throw new InvalidOperationException("No se puede agendar una cita en un horario pasado.");
        }

        // ── Validación de horario de atención clínica (C-05) ─────────────
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario!);

        // ── Verificación de conflicto integral (C-01) ─────────────────────
        int duracion = await ObtenerDuracionCitaMinutosAsync(ct);

        var conflicto = await VerificarConflictoCompletoAsync(
            idProfesional: request.IdProfesional,
            idPaciente: request.IdPaciente,
            idConsultorio: request.IdConsultorio,
            fechaHora: request.FechaHora,
            duracionMinutos: duracion,
            idCitaExcluir: null,
            ct: ct);

        if (conflicto.HayConflicto)
            throw new InvalidOperationException(conflicto.Mensaje);

        string estadoSolicitud = string.IsNullOrWhiteSpace(request.Estado) ? "programada" : request.Estado.Trim();
        var estadoEntidad = await _context.EstadosCita
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                (estadoSolicitud.ToLower() == "agendada" && e.NombreEstado.ToLower() == "programada"),
                ct);

        if (estadoEntidad is null)
            throw new InvalidOperationException("El estado de la cita no es válido.");

        var cita = new Cita
        {
            IdPaciente = request.IdPaciente,
            IdProfesional = request.IdProfesional,
            IdServicio = request.IdServicio,
            IdConsultorio = request.IdConsultorio,
            FechaHora = request.FechaHora,
            IdEstado = estadoEntidad.IdEstado,
            Estado = estadoEntidad.NombreEstado,
            Notas = request.Notas?.Trim()
        };

        _context.Citas.Add(cita);
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    // =========================================================================
    // ACTUALIZAR CITA (C-01, C-04)
    // =========================================================================

    public async Task<Cita?> ActualizarAsync(int id, CitaApiUpdateDto request, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (id != request.IdCita || request.IdPaciente <= 0)
            throw new InvalidOperationException("Datos de cita inválidos.");

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        if (request.IdProfesional is <= 0 ||
            !await _context.Profesionales.AnyAsync(p => p.IdProfesional == request.IdProfesional && p.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El profesional seleccionado no es válido.");
        }

        if (request.IdServicio is <= 0 ||
            !await _context.Servicios.AnyAsync(s => s.IdServicio == request.IdServicio && s.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El servicio seleccionado no es válido.");
        }

        if (request.IdConsultorio is <= 0 ||
            !await _context.Consultorios.AnyAsync(c => c.IdConsultorio == request.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
        {
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
        }

        if (request.FechaHora < DateTime.Now.AddMinutes(-5))
        {
            throw new InvalidOperationException("No se puede mover la cita a un horario pasado.");
        }

        // ── Validación de horario de atención clínica (C-05) ─────────────
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario!);

        // ── Verificación de conflicto integral (C-01) ─────────────────────
        int duracion = await ObtenerDuracionCitaMinutosAsync(ct);

        var conflicto = await VerificarConflictoCompletoAsync(
            idProfesional: request.IdProfesional,
            idPaciente: request.IdPaciente,
            idConsultorio: request.IdConsultorio,
            fechaHora: request.FechaHora,
            duracionMinutos: duracion,
            idCitaExcluir: id,
            ct: ct);

        if (conflicto.HayConflicto)
            throw new InvalidOperationException(conflicto.Mensaje);

        cita.IdPaciente = request.IdPaciente;
        cita.IdProfesional = request.IdProfesional;
        cita.IdServicio = request.IdServicio;
        cita.IdConsultorio = request.IdConsultorio;
        cita.FechaHora = request.FechaHora;

        if (request.IdEstado is > 0)
        {
            var estadoApi = await _context.EstadosCita.AsNoTracking().FirstOrDefaultAsync(e => e.IdEstado == request.IdEstado.Value, ct);
            if (estadoApi is null)
                throw new InvalidOperationException("El estado de la cita no es válido.");

            cita.IdEstado = estadoApi.IdEstado;
            cita.Estado = estadoApi.NombreEstado;
        }
        else if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            string estadoSolicitud = request.Estado.Trim();
            var estadoApi = await _context.EstadosCita.AsNoTracking()
                .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                    (estadoSolicitud.ToLower() == "agendada" && e.NombreEstado.ToLower() == "programada"), ct);

            if (estadoApi is null)
                throw new InvalidOperationException("El estado de la cita no es válido.");

            cita.IdEstado = estadoApi.IdEstado;
            cita.Estado = estadoApi.NombreEstado;
        }

        cita.Notas = request.Notas?.Trim();
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    // =========================================================================
    // CAMBIAR ESTADO
    // =========================================================================

    public async Task<Cita?> CambiarEstadoAsync(int id, string nuevoEstado, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string estadoActual = NormalizarEstado(cita.Estado);
        string estadoNuevo = NormalizarEstado(nuevoEstado);

        if (string.IsNullOrWhiteSpace(estadoNuevo) || !EsTransicionEstadoPermitida(estadoActual, estadoNuevo))
            throw new InvalidOperationException(ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));

        var estadoDestino = await _context.EstadosCita.AsNoTracking().FirstOrDefaultAsync(
            e => NormalizarEstado(e.NombreEstado) == estadoNuevo, ct);

        if (estadoDestino is null)
            throw new InvalidOperationException("El estado seleccionado no existe en el catálogo de estados de citas.");

        cita.IdEstado = estadoDestino.IdEstado;
        cita.Estado = estadoDestino.NombreEstado;
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    // =========================================================================
    // ACTUALIZAR NOTAS
    // =========================================================================

    public async Task<Cita?> ActualizarNotasAsync(int id, string notas, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string notasNuevas = (notas ?? string.Empty).Trim();
        if (notasNuevas.Length > 4000)
            throw new InvalidOperationException("Las notas no pueden superar los 4000 caracteres.");

        cita.Notas = string.IsNullOrWhiteSpace(notasNuevas) ? null : notasNuevas;
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    // =========================================================================
    // CANCELAR (C-06)
    // =========================================================================

    public async Task<bool> CancelarAsync(int id, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return false;

        if (EsEstadoCancelado(cita.Estado))
            throw new InvalidOperationException("La cita ya está cancelada.");

        var estadoCancelada = await _context.EstadosCita.FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "cancelada", ct);
        if (estadoCancelada is not null)
        {
            cita.IdEstado = estadoCancelada.IdEstado;
            cita.Estado = estadoCancelada.NombreEstado;
        }
        else
        {
            cita.Estado = "Cancelada";
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    // =========================================================================
    // HELPERS PRIVADOS ESTÁTICOS
    // =========================================================================

    private static string NormalizarEstado(string? estado)
    {
        var normalizado = (estado ?? string.Empty).Trim().ToLowerInvariant();
        return normalizado switch
        {
            "agendada" => "programada",
            "programado" => "programada",
            "confirmado" => "confirmada",
            "cancelado" => "cancelada",
            "no asistio" => "no_asistida",
            "no asistió" => "no_asistida",
            "no-show" => "no_asistida",
            "completada" => "atendida",
            "realizada" => "atendida",
            _ => normalizado
        };
    }

    private static bool EsTransicionEstadoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "programada" => nuevoEstado is "confirmada" or "cancelada" or "no_asistida",
            "confirmada" => nuevoEstado is "en_proceso" or "cancelada" or "no_asistida",
            "en_proceso" => nuevoEstado == "atendida",
            "atendida" or "finalizada" or "cancelada" or "no_asistida" => false,
            _ => false
        };
    }

    private static string ConstruirMensajeTransicionNoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "programada" => "Una cita agendada solo puede pasar a Confirmada, Cancelada o No asistió.",
            "confirmada" => "Una cita confirmada solo puede pasar a En consulta, Cancelada o No asistió.",
            "en_proceso" => "Una cita en consulta solo puede pasar a Atendida.",
            "atendida" or "finalizada" => "Una cita atendida ya está finalizada y no puede regresar a otro estado.",
            "cancelada" => "Una cita cancelada es definitiva y no puede reactivarse desde la agenda del profesional.",
            "no_asistida" => "Una cita marcada como No asistió es definitiva y no puede reactivarse desde la agenda del profesional.",
            _ => $"No está permitido cambiar una cita de '{estadoActual}' a '{nuevoEstado}'."
        };
    }

    /// <summary>
    /// Evalúa si un texto de estado corresponde a alguna variante de "cancelada".
    /// Centralizado aquí para no repetir lógica en controladores.
    /// </summary>
    internal static bool EsEstadoCancelado(string? estado)
    {
        return NormalizarEstado(estado) is "cancelada" or "cancelado";
    }
}
