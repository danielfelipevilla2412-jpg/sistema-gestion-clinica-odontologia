using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
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
    private readonly SmileTrack_MVC.Services.Email.IEmailService? _emailService;

    public CitaService(
        AppDbContext context,
        ILogger<CitaService> logger,
        SmileTrack_MVC.Services.Email.IEmailService? emailService = null)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    // =========================================================================
    // KPI DE GESTIÓN
    // =========================================================================

    public async Task<CitasKpiDto> ObtenerKpisGestionAsync(
        DateTime fechaReferencia,
        CancellationToken ct = default)
    {
        var inicioMes = new DateTime(fechaReferencia.Year, fechaReferencia.Month, 1);
        var finMes = inicioMes.AddMonths(1);
        var inicioSemana = fechaReferencia.Date.AddDays(-(((int)fechaReferencia.DayOfWeek + 6) % 7));
        var finSemana = inicioSemana.AddDays(7);
        var inicioSemanaAnterior = inicioSemana.AddDays(-7);

        var citas = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora >= inicioMes && c.FechaHora < finMes)
            .Select(c => new { c.FechaHora, c.Estado })
            .ToListAsync(ct);

        int programadas = citas.Count(c => NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int canceladas = citas.Count(c => EsEstadoCancelado(c.Estado));
        int atendidas = citas.Count(c => NormalizarEstado(c.Estado) == "atendida");
        int actual = citas.Count(c => c.FechaHora >= inicioSemana && c.FechaHora < finSemana && NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int anterior = citas.Count(c => c.FechaHora >= inicioSemanaAnterior && c.FechaHora < inicioSemana && NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int total = citas.Count;

        return new CitasKpiDto
        {
            Total = total,
            Programadas = programadas,
            Canceladas = canceladas,
            Atendidas = atendidas,
            DiferenciaSemana = actual - anterior,
            TasaCancelacion = total > 0 ? (int)Math.Round(canceladas * 100.0 / total) : 0,
            TasaAsistencia = total > 0 ? (int)Math.Round(atendidas * 100.0 / total) : 0
        };
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
        int duracionMinutos = DuracionFallbackMinutos,
        CancellationToken ct = default)
    {
        if (duracionMinutos <= 0)
            duracionMinutos = DuracionFallbackMinutos;

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
                "No se puede validar el horario de la clínica.");

            return (false,
                "No fue posible validar el horario de la clínica. Intente nuevamente más tarde.");
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
        TimeOnly horaFinSolicitada = TimeOnly.FromDateTime(fechaHora.AddMinutes(duracionMinutos));

        if (horasolicitada < horaApertura)
        {
            return (false,
                $"La cita no puede agendarse antes de las {horaApertura:HH:mm}, " +
                "que es el horario de apertura de la clínica.");
        }

        if (horasolicitada >= horaCierre || horaFinSolicitada > horaCierre)
        {
            return (false,
            $"La cita debe finalizar antes de las {horaCierre:HH:mm}, " +
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
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado" &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracionMinutos) > inicio,
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
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado" &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracionMinutos) > inicio,
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
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado" &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracionMinutos) > inicio,
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
        string? search = null,
        string? estado = null,
        DateTime? fecha = null,
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchTerm = search.Trim();
            query = query.Where(c =>
                (c.Paciente != null &&
                 (c.Paciente.Nombres.Contains(searchTerm) || c.Paciente.Apellidos.Contains(searchTerm))) ||
                (c.Profesional != null &&
                 (c.Profesional.Nombres.Contains(searchTerm) || c.Profesional.Apellidos.Contains(searchTerm))) ||
                (c.Servicio != null && c.Servicio.Nombre.Contains(searchTerm)) ||
                (c.Notas != null && c.Notas.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            string estadoNormalizado = NormalizarEstado(estado);
            string[] estadosPermitidos = estadoNormalizado switch
            {
                "programada" => ["programada", "Programada", "agendada", "Agendada"],
                "confirmada" => ["confirmada", "Confirmada"],
                "atendida" => ["atendida", "Atendida", "completada", "Completada", "realizada", "Realizada"],
                "cancelada" => ["cancelada", "Cancelada", "cancelado", "Cancelado"],
                "no_asistida" => ["no_asistida", "No asistida", "no asistió", "No asistió", "no-show"],
                _ => [estado.Trim()]
            };
            query = query.Where(c =>
                estadosPermitidos.Contains(c.Estado) ||
                (c.EstadoCita != null && estadosPermitidos.Contains(c.EstadoCita.NombreEstado)));
        }

        if (fecha.HasValue)
        {
            DateTime inicio = fecha.Value.Date;
            query = query.Where(c => c.FechaHora >= inicio && c.FechaHora < inicio.AddDays(1));
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

    private async Task<T> ExecuteWithSerializableTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct)
    {
        if (!_context.Database.IsRelational())
            return await operation();

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
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

        if (!await _context.Pacientes.AnyAsync(
                p => p.IdPaciente == request.IdPaciente && p.Estado == "activo",
                ct))
        {
            throw new InvalidOperationException("El paciente seleccionado no es válido.");
        }

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
        int duracion = await ObtenerDuracionServicioAsync(request.IdServicio!.Value, ct);
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, duracion, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario!);

        var disponibilidad = await ValidarDisponibilidadProfesionalAsync(
            request.IdProfesional!.Value,
            request.IdServicio!.Value,
            request.FechaHora,
            duracion,
            ct);
        if (!disponibilidad.EsValida)
            throw new InvalidOperationException(disponibilidad.Mensaje!);

        return await ExecuteWithSerializableTransactionAsync(async () =>
        {
            var conflicto = await VerificarConflictoCompletoAsync(
                request.IdProfesional!.Value,
                request.IdPaciente,
                request.IdConsultorio,
                request.FechaHora,
                duracion,
                null,
                ct);

            if (conflicto.HayConflicto)
                throw new InvalidOperationException(conflicto.Mensaje);

            string estadoSolicitud = string.IsNullOrWhiteSpace(request.Estado) ? "programada" : request.Estado.Trim();
            var estadoEntidad = await ResolverEstadoCatalogoAsync(estadoSolicitud, ct);

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
                Notas = request.Notas?.Trim(),
                MotivoConsulta = request.Notas?.Trim(),
                DuracionMinutos = duracion,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Citas.Add(cita);
            await _context.SaveChangesAsync(ct);
            return cita;
        }, ct);
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

        if (!await _context.Pacientes.AnyAsync(
                p => p.IdPaciente == request.IdPaciente && p.Estado == "activo",
                ct))
        {
            throw new InvalidOperationException("El paciente seleccionado no es válido.");
        }

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        EstadoCita? estadoSolicitado = null;
        if (request.IdEstado is > 0)
        {
            estadoSolicitado = await _context.EstadosCita.AsNoTracking()
                .FirstOrDefaultAsync(e => e.IdEstado == request.IdEstado.Value, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            string estadoTexto = request.Estado.Trim();
            estadoSolicitado = await ResolverEstadoCatalogoAsync(estadoTexto, ct);
        }

        if ((request.IdEstado is > 0 || !string.IsNullOrWhiteSpace(request.Estado)) && estadoSolicitado is null)
            throw new InvalidOperationException("El estado de la cita no es válido.");

        if (estadoSolicitado is not null)
        {
            string estadoActual = NormalizarEstado(cita.Estado);
            string estadoNuevo = NormalizarEstado(estadoSolicitado.NombreEstado);
            if (estadoActual != estadoNuevo && !EsTransicionEstadoPermitida(estadoActual, estadoNuevo))
                throw new InvalidOperationException(ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));
        }

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
        int duracion = await ObtenerDuracionServicioAsync(request.IdServicio!.Value, ct);
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, duracion, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario!);

        var disponibilidad = await ValidarDisponibilidadProfesionalAsync(
            request.IdProfesional!.Value,
            request.IdServicio!.Value,
            request.FechaHora,
            duracion,
            ct);
        if (!disponibilidad.EsValida)
            throw new InvalidOperationException(disponibilidad.Mensaje!);

        return await ExecuteWithSerializableTransactionAsync(async () =>
        {
            var conflicto = await VerificarConflictoCompletoAsync(
                request.IdProfesional!.Value,
                request.IdPaciente,
                request.IdConsultorio,
                request.FechaHora,
                duracion,
                id,
                ct);

            if (conflicto.HayConflicto)
                throw new InvalidOperationException(conflicto.Mensaje);

            cita.IdPaciente = request.IdPaciente;
            cita.IdProfesional = request.IdProfesional;
            cita.IdServicio = request.IdServicio;
            cita.IdConsultorio = request.IdConsultorio;
            cita.FechaHora = request.FechaHora;
            cita.DuracionMinutos = duracion;

            if (estadoSolicitado is not null)
            {
                cita.IdEstado = estadoSolicitado.IdEstado;
                cita.Estado = estadoSolicitado.NombreEstado;
            }

            cita.Notas = request.Notas?.Trim();
            cita.MotivoConsulta = request.Notas?.Trim();
            await _context.SaveChangesAsync(ct);
            return cita;
        }, ct);
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

        var estadosCatalogo = await _context.EstadosCita
            .AsNoTracking()
            .ToListAsync(ct);
        var estadoDestino = estadosCatalogo.FirstOrDefault(e =>
            NormalizarEstado(e.NombreEstado) == estadoNuevo);

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

    public async Task<bool> CancelarAsync(
        int id,
        TimeSpan? anticipacionMinima = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return false;

        if (EsEstadoCancelado(cita.Estado))
            throw new InvalidOperationException("La cita ya está cancelada.");

        if (anticipacionMinima.HasValue && cita.FechaHora - DateTime.Now < anticipacionMinima.Value)
            throw new InvalidOperationException(
                $"No es posible cancelar citas con menos de {anticipacionMinima.Value.TotalHours:0} horas de anticipación.");

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
    // SOLICITUD DE CITA POR PACIENTE
    // =========================================================================

    /// <inheritdoc/>
    public async Task<Cita> SolicitarCitaPacienteAsync(
        int idPaciente,
        CitaSolicitudPacienteDto dto,
        CancellationToken ct = default)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        if (idPaciente <= 0)
            throw new InvalidOperationException("Identificador de paciente inválido.");

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente && p.Estado == "activo", ct);

        if (paciente is null)
            throw new InvalidOperationException("El paciente no existe o no se encuentra activo.");

        // La solicitud no incluye una hora: se persiste como petición pendiente
        // y se asigna una hora tentativa. Aceptar hoy después de esa hora creaba
        // citas visibles en el pasado.
        if (dto.Fecha.Date <= DateTime.Today)
            throw new InvalidOperationException("No se pueden solicitar citas para hoy ni fechas pasadas; seleccione una fecha posterior para que la clínica pueda confirmar el horario.");

        // Validar servicio solo si fue especificado por el paciente
        Servicio? servicio = null;
        if (dto.IdServicio is > 0)
        {
            servicio = await _context.Servicios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdServicio == dto.IdServicio && s.Estado == "activo", ct);

            if (servicio is null)
                throw new InvalidOperationException("El servicio seleccionado no es válido o no está activo.");
        }

        // Buscar estado 'Solicitada' (con fallback a 'Agendada' o 'Programada')
        var estadoSolicitada = await _context.EstadosCita
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "solicitada", ct)
            ?? await _context.EstadosCita
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "agendada" || e.NombreEstado.ToLower() == "programada", ct);

        // La hora tentativa se fija por defecto al inicio de jornada para la fecha solicitada
        var fechaHoraTentativa = dto.Fecha.Date.AddHours(8);

        var cita = new Cita
        {
            IdPaciente = idPaciente,
            IdServicio = dto.IdServicio,
            FechaHora = fechaHoraTentativa,
            IdProfesional = null,
            IdConsultorio = null,
            IdEstado = estadoSolicitada?.IdEstado,
            Estado = estadoSolicitada?.NombreEstado ?? "Solicitada",
            Notas = dto.Notas?.Trim()
        };

        _context.Citas.Add(cita);
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    /// <inheritdoc/>
    public async Task<List<Cita>> ObtenerSolicitudesPendientesAsync(CancellationToken ct = default)
    {
        var estadosSolicitados = await _context.EstadosCita
            .AsNoTracking()
            .Where(e => e.NombreEstado != null && (e.NombreEstado.ToLower() == "solicitada" || e.NombreEstado.ToLower() == "pendiente"))
            .Select(e => e.IdEstado)
            .ToListAsync(ct);

        return await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Where(c =>
                (c.IdEstado.HasValue && estadosSolicitados.Contains(c.IdEstado.Value)) ||
                c.Estado != null && (
                    c.Estado.ToLower() == "solicitada" ||
                    c.Estado.ToLower() == "pendiente" ||
                    c.Estado.ToLower() == "solicitado"))
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);
    }

    // =========================================================================
    // DISPONIBILIDAD DE PROFESIONALES
    // =========================================================================

    /// <inheritdoc/>
    public async Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesDisponiblesAsync(
        DateTime fecha,
        TimeSpan horaInicio,
        int duracionMinutos = 60,
        int? idServicio = null,
        CancellationToken ct = default)
    {
        if (duracionMinutos <= 0)
            duracionMinutos = await ObtenerDuracionCitaMinutosAsync(ct);

        DateTime inicio = fecha.Date.Add(horaInicio);
        DateTime fin = inicio.AddMinutes(duracionMinutos);

        // Validar horario general de la clínica
        var (esHorarioClinicaValido, _) = await ValidarHorarioClinicaAsync(inicio, duracionMinutos, ct);
        if (!esHorarioClinicaValido)
            return [];

        // Obtener profesionales activos
        var profesionales = await _context.Profesionales
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .Where(p => p.Estado == "activo")
            .ToListAsync(ct);

        var resultado = new List<ProfesionalDisponibleDto>();

        foreach (var prof in profesionales)
        {
            // 1. Validar reglas de disponibilidad individual (horario, ausencia, bloqueo, asignación servicio)
            var (esValida, _) = await ValidarDisponibilidadProfesionalAsync(
                prof.IdProfesional,
                idServicio ?? 0,
                inicio,
                duracionMinutos,
                ct);

            if (!esValida)
                continue;

            // 2. Validar que no tenga cita solapada activa
            bool tieneCitaSolapada = await _context.Citas
                .AsNoTracking()
                .AnyAsync(c =>
                    c.IdProfesional == prof.IdProfesional &&
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelado" &&
                    c.Estado != "cancelado" &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(duracionMinutos) > inicio,
                    ct);

            if (tieneCitaSolapada)
                continue;

            string nombre = prof.Usuario is not null
                ? $"{prof.Usuario.Nombre} {prof.Usuario.Apellidos}".Trim()
                : $"{prof.Nombres} {prof.Apellidos}".Trim();

            string especialidades = string.Join(", ", prof.Especialidades
                .Where(e => e.Especialidad is not null)
                .Select(e => e.Especialidad!.Nombre));

            resultado.Add(new ProfesionalDisponibleDto
            {
                IdProfesional = prof.IdProfesional,
                NombreCompleto = string.IsNullOrWhiteSpace(nombre) ? "Profesional" : nombre,
                Especialidades = string.IsNullOrWhiteSpace(especialidades) ? "Odontología General" : especialidades,
                FotoUrl = null
            });
        }

        return resultado;
    }

    // =========================================================================
    // CONFIRMAR Y ASIGNAR CITA (RECEPCIÓN)
    // =========================================================================

    /// <inheritdoc/>
    public async Task<Cita?> ConfirmarYAsignarCitaAsync(
        int idCita,
        CitaConfirmacionAsignacionDto dto,
        CancellationToken ct = default)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        var cita = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .FirstOrDefaultAsync(c => c.IdCita == idCita, ct);

        if (cita is null)
            return null;

        if (!await _context.Profesionales.AnyAsync(p => p.IdProfesional == dto.IdProfesional && p.Estado == "activo", ct))
            throw new InvalidOperationException("El profesional seleccionado no es válido o no está activo.");

        if (!await _context.Consultorios.AnyAsync(c => c.IdConsultorio == dto.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");

        DateTime fechaHora = dto.Fecha.Date.Add(dto.HoraInicio);
        if (fechaHora < DateTime.Now.AddMinutes(-5))
            throw new InvalidOperationException("No se puede agendar una cita en fecha u hora pasada.");

        int duracion = await ObtenerDuracionCitaMinutosAsync(ct);

        // Validar horario clínica
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(fechaHora, duracion, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario!);

        // Validar disponibilidad del profesional
        var disp = await ValidarDisponibilidadProfesionalAsync(dto.IdProfesional, cita.IdServicio ?? 0, fechaHora, duracion, ct);
        if (!disp.EsValida)
            throw new InvalidOperationException(disp.Mensaje!);

        // Validar conflicto de recursos excluyendo la cita actual
        var conflicto = await VerificarConflictoCompletoAsync(
            idProfesional: dto.IdProfesional,
            idPaciente: cita.IdPaciente,
            idConsultorio: dto.IdConsultorio,
            fechaHora: fechaHora,
            duracionMinutos: duracion,
            idCitaExcluir: idCita,
            ct: ct);

        if (conflicto.HayConflicto)
            throw new InvalidOperationException(conflicto.Mensaje);

        // Obtener estado 'Confirmada'
        var estadoConfirmada = await _context.EstadosCita
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "confirmada", ct);

        cita.IdProfesional = dto.IdProfesional;
        cita.IdConsultorio = dto.IdConsultorio;
        cita.FechaHora = fechaHora;
        cita.IdEstado = estadoConfirmada?.IdEstado ?? cita.IdEstado;
        cita.Estado = estadoConfirmada?.NombreEstado ?? "Confirmada";
        if (!string.IsNullOrWhiteSpace(dto.Notas))
            cita.Notas = dto.Notas.Trim();

        await _context.SaveChangesAsync(ct);

        // Notificar al paciente por correo si hay servicio de email configurado
        if (_emailService is not null && cita.Paciente is not null && !string.IsNullOrWhiteSpace(cita.Paciente.Correo))
        {
            var profesional = await _context.Profesionales
                .Include(p => p.Usuario)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdProfesional == dto.IdProfesional, ct);

            string profNombre = profesional?.Usuario != null
                ? $"{profesional.Usuario.Nombre} {profesional.Usuario.Apellidos}".Trim()
                : $"{profesional?.Nombres} {profesional?.Apellidos}".Trim();

            try
            {
                await _emailService.SendCitaNotificacionAsync(
                    cita.Paciente.Correo,
                    $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim(),
                    cita.FechaHora,
                    string.IsNullOrWhiteSpace(profNombre) ? "Profesional asignado" : profNombre,
                    cita.Servicio?.Nombre ?? "Consulta Odontológica",
                    "confirmada",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible enviar correo de confirmación de cita {IdCita}", idCita);
            }
        }

        return cita;
    }

    // =========================================================================
    // ENVIAR RECORDATORIOS POR CORREO
    // =========================================================================

    /// <inheritdoc/>
    public async Task<(int Enviados, int Fallidos)> EnviarRecordatoriosAsync(
        List<int> idsCitas,
        string? mensajePersonalizado = null,
        CancellationToken ct = default)
    {
        if (idsCitas is null || idsCitas.Count == 0)
            return (0, 0);

        var citas = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Where(c => idsCitas.Contains(c.IdCita) &&
                        c.Estado != "Cancelada" &&
                        c.Estado != "cancelada")
            .ToListAsync(ct);

        int enviados = 0;
        int fallidos = 0;

        foreach (var cita in citas)
        {
            if (cita.Paciente is null || string.IsNullOrWhiteSpace(cita.Paciente.Correo))
            {
                fallidos++;
                continue;
            }

            string profNombre = cita.Profesional?.Usuario != null
                ? $"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}".Trim()
                : $"{cita.Profesional?.Nombres} {cita.Profesional?.Apellidos}".Trim();

            string pacienteNombre = $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim();

            try
            {
                if (_emailService is not null)
                {
                    await _emailService.SendCitaNotificacionAsync(
                        cita.Paciente.Correo,
                        pacienteNombre,
                        cita.FechaHora,
                        string.IsNullOrWhiteSpace(profNombre) ? "Tu profesional" : profNombre,
                        cita.Servicio?.Nombre ?? "Consulta Odontológica",
                        "recordatorio",
                        ct);
                }

                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "EMAIL_RECORDATORIO",
                    TablaAfectada = "Cita",
                    IdRegistro = cita.IdCita,
                    Descripcion = $"Recordatorio de cita enviado al correo {cita.Paciente.Correo}.",
                    IpOrigen = "ServicioRecordatorios",
                    Fecha = DateTime.Now
                });

                enviados++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enviando recordatorio por correo para Cita {IdCita}", cita.IdCita);
                fallidos++;
            }
        }

        if (enviados > 0)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar auditoría de recordatorios enviados.");
            }
        }

        return (enviados, fallidos);
    }

    // =========================================================================
    // HELPERS PRIVADOS ESTÁTICOS
    // =========================================================================

    private async Task<(bool EsValida, string? Mensaje)> ValidarDisponibilidadProfesionalAsync(
        int idProfesional,
        int idServicio,
        DateTime fechaHora,
        int duracionMinutos,
        CancellationToken ct)
    {
        if (idServicio > 0)
        {
            bool tieneAsignaciones = await _context.ProfesionalServicios
                .AsNoTracking()
                .AnyAsync(ps => ps.IdProfesional == idProfesional && ps.Activo, ct);

            if (tieneAsignaciones)
            {
                bool servicioAsignado = await _context.ProfesionalServicios
                    .AsNoTracking()
                    .AnyAsync(ps => ps.IdProfesional == idProfesional && ps.IdServicio == idServicio && ps.Activo, ct);

                if (!servicioAsignado)
                {
                    return (false, "El servicio seleccionado no está asignado al profesional.");
                }
            }
        }

        DateOnly fecha = DateOnly.FromDateTime(fechaHora);
        bool ausente = await _context.AusenciasProfesional.AsNoTracking().AnyAsync(
            ausencia => ausencia.IdProfesional == idProfesional &&
                        ausencia.FechaInicio <= fecha &&
                        ausencia.FechaFin >= fecha,
            ct);
        if (ausente)
            return (false, "El profesional no está disponible en la fecha seleccionada por una ausencia registrada.");

        DateTime fin = fechaHora.AddMinutes(duracionMinutos);
        bool bloqueado = await _context.BloqueosProfesional.AsNoTracking().AnyAsync(
            bloqueo => bloqueo.IdProfesional == idProfesional &&
                       bloqueo.FechaInicio < fin &&
                       bloqueo.FechaFin > fechaHora,
            ct);
        if (bloqueado)
            return (false, "El profesional no está disponible en el horario seleccionado por un bloqueo registrado.");

        var horarios = await _context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.IdProfesional == idProfesional && h.Activo)
            .ToListAsync(ct);

        if (horarios.Count == 0)
            return (false, "El profesional no tiene un horario de atención configurado.");

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

    private async Task<int> ObtenerDuracionServicioAsync(int idServicio, CancellationToken ct)
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

    private async Task<EstadoCita?> ResolverEstadoCatalogoAsync(string? estadoTexto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(estadoTexto))
            return null;

        var estadoNormalizado = NormalizarEstado(estadoTexto);
        if (string.IsNullOrWhiteSpace(estadoNormalizado))
            return null;

        var estadosCatalogo = await _context.EstadosCita
            .AsNoTracking()
            .ToListAsync(ct);

        return estadosCatalogo.FirstOrDefault(e =>
            NormalizarEstado(e.NombreEstado) == estadoNormalizado);
    }

    private static string NormalizarEstado(string? estado)
    {
        return EstadoCitaHelper.Normalize(estado);
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
