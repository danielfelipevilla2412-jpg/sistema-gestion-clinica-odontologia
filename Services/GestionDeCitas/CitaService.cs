/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: CitaService
 * ============================================
 * Archivo: Services/GestionDeCitas/CitaService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Implementación de la capa de servicios de citas odontológicas. Encapsula las reglas de negocio
 * complejas como la detección de conflictos multirecurso, duraciones dinámicas configurables,
 * auditoría transaccional de cambios de estado y envío automático de notificaciones por correo.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Duración configurable vía tabla Configuracion_General ("cita_duracion_minutos") con fallback a 60 min.
 * - Validación integral de conflictos (VerificarConflictoCompletoAsync) evaluando simultáneamente
 *   Profesional, Paciente y Consultorio para prevenir doble agendamiento.
 * - Notificaciones por email automatizadas ante creación/modificación/cancelación de citas.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, ILogger, IEmailService, EstadoCitaHelper
 * ============================================
 */

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
        // Los días se almacenan con numeración ISO (1=Lunes…7=Domingo).
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
            {
                diasPermitidos = diasConfigurados.Where(dia => dia <= 6).ToHashSet();
            }
        }

        int diaIso = fechaHora.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)fechaHora.DayOfWeek;
        if (!diasPermitidos.Contains(diaIso))
        {
            string nombreDia = fechaHora.ToString("dddd",
                new System.Globalization.CultureInfo("es-CO"));

            return (false,
                $"La clínica no tiene atención los {nombreDia}. " +
                "Por favor seleccione un día de atención.");
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
                    !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                        ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
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
                    !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                        ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
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
                    !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                        ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
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

        // POR QUÉ: las citas cuya hora ya pasó y que se quedaron en un estado no final
        // (en espera / pendiente) no deben aparecer en el listado. Solo se muestran cuando el
        // usuario filtra explícitamente por fecha o por estado, para poder auditar el histórico.
        if (!fecha.HasValue && string.IsNullOrWhiteSpace(estado))
        {
            DateTime ahora = ColombiaClock.Now;
            string[] estadosFinales = EstadoCitaHelper.EstadosFinales;

            query = query.Where(c =>
                c.FechaHora >= ahora
                || (c.Estado != null && estadosFinales.Contains(c.Estado.ToLower()))
                || (c.EstadoCita != null
                    && c.EstadoCita.NombreEstado != null
                    && estadosFinales.Contains(c.EstadoCita.NombreEstado.ToLower())));
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

        if (request.FechaHora < ColombiaClock.Now.AddMinutes(-5))
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

        var citaCreada = await ExecuteWithSerializableTransactionAsync(async () =>
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

            string estadoNuevo = NormalizarEstado(estadoEntidad.NombreEstado);
            if (estadoNuevo is not ("programada" or "confirmada"))
                throw new InvalidOperationException("Una cita nueva solo puede crearse como Programada o Confirmada.");

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
                FechaCreacion = ColombiaClock.Now
            };

            _context.Citas.Add(cita);
            await _context.SaveChangesAsync(ct);

            return cita;
        }, ct);

        // ── Acciones post-guardado (fuera de la transacción Serializable) ────
        // Registrar historial y notificación DESPUÉS de liberar el lock para
        // no prolongar el tiempo de bloqueo sobre la tabla Citas.
        try
        {
            await RegistrarHistorialEstadoAsync(
                idCita: citaCreada.IdCita,
                idEstado: citaCreada.IdEstado,
                estadoTexto: citaCreada.Estado,
                idUsuario: null,
                motivo: "Cita creada",
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar el historial inicial de la cita {IdCita}.", citaCreada.IdCita);
        }

        try
        {
            await CrearNotificacionInternaCitaAsync(
                idCita: citaCreada.IdCita,
                idPaciente: citaCreada.IdPaciente,
                tipo: "confirmacion",
                titulo: "Cita agendada",
                contenido: $"Tu cita fue agendada para el {citaCreada.FechaHora:dd/MM/yyyy} a las {citaCreada.FechaHora:HH:mm}.",
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo crear la notificación de la cita {IdCita}.", citaCreada.IdCita);
        }

        return citaCreada;
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

        if (request.FechaHora < ColombiaClock.Now.AddMinutes(-5))
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

            if (cita.IdConsultorio != request.IdConsultorio)
            {
                _context.CitasConsultoriosHistorial.Add(new CitaConsultorioHistorial
                {
                    IdCita = cita.IdCita,
                    IdConsultorioAnterior = cita.IdConsultorio,
                    IdConsultorioNuevo = request.IdConsultorio,
                    IdUsuario = request.IdUsuarioModificacion,
                    FechaCambio = ColombiaClock.Now
                });
            }

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

    public async Task<Cita?> CambiarEstadoAsync(int id, string nuevoEstado, int? idUsuarioActor = null, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string estadoActual = NormalizarEstado(cita.Estado);
        string estadoNuevo = NormalizarEstado(nuevoEstado);

        if (string.IsNullOrWhiteSpace(estadoNuevo) || !EsTransicionEstadoPermitida(estadoActual, estadoNuevo))
            throw new InvalidOperationException(ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));

        if (estadoNuevo is "confirmada" or "en_sala_de_espera" or "en_proceso")
        {
            if (!cita.IdProfesional.HasValue || cita.IdProfesional.Value <= 0)
                throw new InvalidOperationException("No se puede confirmar o iniciar la atención de una cita sin profesional asignado.");

            if (!cita.IdConsultorio.HasValue || cita.IdConsultorio.Value <= 0)
                throw new InvalidOperationException("No se puede confirmar o iniciar la atención de una cita sin consultorio asignado.");
        }

        if (estadoNuevo is "en_proceso" && cita.FechaHora > ColombiaClock.Now)
            throw new InvalidOperationException("La atención solo puede iniciarse el día programado y cuando llegue la hora programada de la cita.");

        if (estadoNuevo is "atendida" or "no_asistida" && cita.FechaHora.Date > ColombiaClock.Today)
            throw new InvalidOperationException("Solo se puede marcar la cita como atendida o no asistida desde el día programado o después.");
        // REGLA DE NEGOCIO:
        // Una cita no puede confirmarse sin un profesional asignado.
        if (estadoNuevo == "confirmada" && cita.IdProfesional is null or <= 0)
        {
            throw new InvalidOperationException(
                "No se puede confirmar una cita sin un profesional asignado.");
        }
        var estadosCatalogo = await _context.EstadosCita
            .AsNoTracking()
            .ToListAsync(ct);
        var estadoDestino = estadosCatalogo.FirstOrDefault(e =>
            NormalizarEstado(e.NombreEstado) == estadoNuevo);

        if (estadoDestino is null)
            throw new InvalidOperationException("El estado seleccionado no existe en el catálogo de estados de citas.");

        // Guardar estado anterior ANTES del UPDATE
        string estadoAnteriorTexto = cita.Estado;

        cita.IdEstado = estadoDestino.IdEstado;
        cita.Estado = estadoDestino.NombreEstado;
        if (estadoNuevo == "en_proceso")
        {
            var asistencia = await _context.AsistenciasProcedimiento
                .FirstOrDefaultAsync(a => a.IdCita == cita.IdCita, ct);
            if (asistencia is null)
            {
                asistencia = new AsistenciaProcedimiento
                {
                    IdCita = cita.IdCita,
                    Inicio = ColombiaClock.Now,
                    ActualizadoEn = ColombiaClock.Now
                };
                _context.AsistenciasProcedimiento.Add(asistencia);
            }
            else if (asistencia.Inicio == default)
            {
                asistencia.Inicio = ColombiaClock.Now;
                asistencia.ActualizadoEn = ColombiaClock.Now;
            }
        }
        await _context.SaveChangesAsync(ct);

        // ── Historial de cambio de estado (M6: propagar actor) ───────────────
        await RegistrarHistorialEstadoAsync(
            idCita: cita.IdCita,
            idEstado: cita.IdEstado,
            estadoTexto: cita.Estado,
            idUsuario: idUsuarioActor,
            motivo: $"Estado cambiado de '{estadoAnteriorTexto}' a '{cita.Estado}'",
            ct: ct);

        // ── Notificación interna al paciente (solo estados relevantes) ───────
        string estadoNormalizado = NormalizarEstado(cita.Estado);
        if (estadoNormalizado is "confirmada" or "cancelada" or "atendida" or "no_asistida")
        {
            string tituloNotif = estadoNormalizado switch
            {
                "confirmada" => "Cita confirmada",
                "cancelada" => "Cita cancelada",
                "atendida" => "Cita completada",
                "no_asistida" => "Inasistencia registrada",
                _ => "Actualización de tu cita"
            };
            await CrearNotificacionInternaCitaAsync(
                idCita: cita.IdCita,
                idPaciente: cita.IdPaciente,
                tipo: "cambio_estado",
                titulo: tituloNotif,
                contenido: $"El estado de tu cita del {cita.FechaHora:dd/MM/yyyy} a las {cita.FechaHora:HH:mm} cambió a '{cita.Estado}'.",
                ct: ct);
        }

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
        string? motivo,
        TimeSpan? anticipacionMinima = null,
        CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return false;

        if (EsEstadoCancelado(cita.Estado))
            throw new InvalidOperationException("La cita ya está cancelada.");

        if (cita.FechaHora <= ColombiaClock.Now)
            throw new InvalidOperationException("La cita no puede cancelarse porque su fecha y hora anterior ya ha vencido.");

        if (anticipacionMinima.HasValue && cita.FechaHora - ColombiaClock.Now < anticipacionMinima.Value)
            throw new InvalidOperationException(
                $"No es posible cancelar citas con menos de {anticipacionMinima.Value.TotalHours:0} horas de anticipación.");

        // Guardar estado anterior ANTES de la cancelación
        string estadoAnteriorCancelar = cita.Estado;
        int idPacienteCancelar = cita.IdPaciente;
        DateTime fechaHoraCancelar = cita.FechaHora;

        // RN-23: motivo obligatorio, mínimo 5 y máximo 500 caracteres
        var motivoCancelacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivoCancelacion))
            throw new InvalidOperationException("El motivo de cancelación es obligatorio (RN-23). Debe indicar la razón de la cancelación.");
        if (motivoCancelacion.Length < 5)
            throw new InvalidOperationException("El motivo de cancelación debe tener al menos 5 caracteres.");
        if (motivoCancelacion.Length > 500)
            throw new InvalidOperationException("El motivo de cancelación no puede superar 500 caracteres.");

        cita.MotivoCancelacion = motivoCancelacion;

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

        // ── Historial de cancelación ─────────────────────────────────────────
        var motivoHistorial = $"Cita cancelada (estado anterior: '{estadoAnteriorCancelar}', motivo: '{motivoCancelacion}')";

        await RegistrarHistorialEstadoAsync(
            idCita: cita.IdCita,
            idEstado: cita.IdEstado,
            estadoTexto: cita.Estado,
            idUsuario: null,
            motivo: motivoHistorial,
            ct: ct);

        // ── Notificación interna de cancelación al paciente ──────────────────
        await CrearNotificacionInternaCitaAsync(
            idCita: cita.IdCita,
            idPaciente: idPacienteCancelar,
            tipo: "cancelacion",
            titulo: "Cita cancelada",
            contenido: $"Tu cita del {fechaHoraCancelar:dd/MM/yyyy} a las {fechaHoraCancelar:HH:mm} ha sido cancelada. Motivo: {motivoCancelacion}",
            ct: ct);

        // ── Notificación a pacientes en Lista de Espera ──────────────────────
        await NotificarListaEsperaFranjaLiberadaAsync(
            idServicio: cita.IdServicio,
            idProfesional: cita.IdProfesional,
            fechaHoraLiberada: fechaHoraCancelar,
            ct: ct);

        // M3 (RN-29): Intentar envío de correo al paciente al cancelar.
        // Una falla de correo NO revierte la cancelación — solo se registra como advertencia.
        await EnviarCorreoCancelacionAsync(cita.IdCita, fechaHoraCancelar, motivoCancelacion!, ct);

        return true;
    }

    // =========================================================================
    // AGENDAMIENTO DIRECTO POR PACIENTE
    // =========================================================================

    /// <inheritdoc/>
    public async Task<Cita> AgendarCitaPacienteAsync(
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

        if (dto.IdServicio is not > 0)
            throw new InvalidOperationException("Debe seleccionar un servicio para agendar la cita.");

        var servicio = await _context.Servicios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdServicio == dto.IdServicio && s.Estado == "activo", ct);

        if (servicio is null)
            throw new InvalidOperationException("El servicio seleccionado no existe o no está activo.");

        if (dto.IdProfesional is not > 0)
            throw new InvalidOperationException("Debe seleccionar un profesional para agendar la cita.");

        var horariosDisponibles = await ObtenerHorariosDisponiblesPacienteAsync(
            idPaciente,
            servicio.IdServicio,
            dto.IdProfesional.Value,
            dto.Fecha,
            ct);
        if (!horariosDisponibles.Contains(dto.HoraInicio))
        {
            throw new InvalidOperationException(
                "El horario seleccionado ya no está disponible. Actualiza la lista y elige uno de los horarios recomendados.");
        }

        DateTime fechaHora = dto.Fecha.Date.Add(dto.HoraInicio);
        int duracion = await ObtenerDuracionServicioAsync(servicio.IdServicio, ct);
        var consultorios = await _context.Consultorios
            .AsNoTracking()
            .Where(c => c.Estado == "disponible" || c.Estado == "activo")
            .OrderBy(c => c.IdConsultorio)
            .Select(c => c.IdConsultorio)
            .ToListAsync(ct);

        foreach (int idConsultorio in consultorios)
        {
            var conflicto = await VerificarConflictoCompletoAsync(
                dto.IdProfesional.Value,
                idPaciente,
                idConsultorio,
                fechaHora,
                duracion,
                ct: ct);
            if (conflicto.HayConflicto)
                continue;

            return await CrearAsync(new CitaApiRequest
            {
                IdPaciente = idPaciente,
                IdProfesional = dto.IdProfesional.Value,
                IdServicio = servicio.IdServicio,
                IdConsultorio = idConsultorio,
                FechaHora = fechaHora,
                Estado = "Programada",
                Notas = dto.Notas
            }, ct);
        }

        throw new InvalidOperationException(
            "El horario seleccionado ya no está disponible en ningún consultorio. Actualiza la lista y elige otro horario.");
    }

    /// <inheritdoc/>
    public async Task<List<TimeSpan>> ObtenerHorariosDisponiblesPacienteAsync(
        int idPaciente,
        int idServicio,
        int idProfesional,
        DateTime fecha,
        CancellationToken ct = default)
    {
        if (idPaciente <= 0 || idServicio <= 0 || idProfesional <= 0 || fecha.Date < ColombiaClock.Today)
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

        int? idEspecialidadServicio = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.IdServicio == idServicio && s.Estado == "activo")
            .Select(s => s.IdEspecialidad)
            .FirstOrDefaultAsync(ct);
        if (idEspecialidadServicio is not > 0)
            return [];

        bool profesionalHabilitado = await _context.Profesionales
            .AsNoTracking()
            .AnyAsync(p =>
                p.IdProfesional == idProfesional &&
                p.Estado == "activo" &&
                _context.ProfesionalServicios.Any(ps =>
                    ps.IdProfesional == idProfesional &&
                    ps.IdServicio == idServicio &&
                    ps.Activo) &&
                _context.ProfesionalEspecialidades.Any(pe =>
                    pe.IdProfesional == idProfesional &&
                    pe.IdEspecialidad == idEspecialidadServicio.Value),
                ct);
        if (!profesionalHabilitado)
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

    /// <inheritdoc/>
    public async Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default)
    {
        if (idServicio <= 0)
            return [];

        var profesionales = await (
            from asignacion in _context.ProfesionalServicios.AsNoTracking()
            join profesional in _context.Profesionales.AsNoTracking()
                on asignacion.IdProfesional equals profesional.IdProfesional
            join servicio in _context.Servicios.AsNoTracking()
                on asignacion.IdServicio equals servicio.IdServicio
            join especialidad in _context.ProfesionalEspecialidades.AsNoTracking()
                on new { asignacion.IdProfesional, IdEspecialidad = servicio.IdEspecialidad ?? 0 }
                equals new { especialidad.IdProfesional, especialidad.IdEspecialidad }
            where asignacion.IdServicio == idServicio
                && asignacion.Activo
                && profesional.Estado == "activo"
                && servicio.Estado == "activo"
            orderby profesional.Nombres, profesional.Apellidos
            select new
            {
                profesional.IdProfesional,
                profesional.Nombres,
                profesional.Apellidos
            })
            .Distinct()
            .ToListAsync(ct);

        return profesionales.Select(profesional => new ProfesionalDisponibleDto
        {
            IdProfesional = profesional.IdProfesional,
            NombreCompleto = $"{profesional.Nombres} {profesional.Apellidos}".Trim()
        }).ToList();
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

    public async Task<Cita?> RechazarSolicitudAsync(
        int idCita,
        string motivo,
        int? idUsuarioActor = null,
        CancellationToken ct = default)
    {
        if (idCita <= 0)
            throw new InvalidOperationException("Identificador de cita inválido.");
        string motivoNormalizado = motivo?.Trim() ?? string.Empty;
        if (motivoNormalizado.Length < 5 || motivoNormalizado.Length > 500)
            throw new InvalidOperationException("El motivo del rechazo debe tener entre 5 y 500 caracteres.");

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == idCita, ct);
        if (cita is null)
            return null;
        if (NormalizarEstado(cita.Estado) is not ("solicitada" or "pendiente"))
            throw new InvalidOperationException("Solo se pueden rechazar solicitudes que aún están pendientes de revisión.");

        var estadoRechazada = await _context.EstadosCita
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "rechazada", ct);
        cita.Estado = estadoRechazada?.NombreEstado ?? "Rechazada";
        cita.IdEstado = estadoRechazada?.IdEstado;
        await _context.SaveChangesAsync(ct);
        await RegistrarHistorialEstadoAsync(
            cita.IdCita,
            cita.IdEstado,
            cita.Estado,
            idUsuarioActor,
            $"Solicitud rechazada. Motivo: {motivoNormalizado}",
            ct);
        await CrearNotificacionInternaCitaAsync(
            cita.IdCita,
            cita.IdPaciente,
            "cita_rechazada",
            "Solicitud de cita rechazada",
            $"Tu solicitud de cita para el {cita.FechaHora:dd/MM/yyyy} fue rechazada. Motivo: {motivoNormalizado}",
            ct);
        _logger.LogInformation("Solicitud de cita {IdCita} rechazada por usuario {IdUsuario}.", cita.IdCita, idUsuarioActor);
        return cita;
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
        // Sin un servicio concreto no es posible verificar asignación ni especialidad.
        if (idServicio is not > 0)
            return [];

        if (duracionMinutos <= 0)
            duracionMinutos = await ObtenerDuracionCitaMinutosAsync(ct);

        DateTime inicio = fecha.Date.Add(horaInicio);
        DateTime fin = inicio.AddMinutes(duracionMinutos);

        // Validar horario general de la clínica
        var (esHorarioClinicaValido, _) = await ValidarHorarioClinicaAsync(inicio, duracionMinutos, ct);
        if (!esHorarioClinicaValido)
            return [];

        int? idEspecialidadServicio = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.IdServicio == idServicio && s.Estado == "activo")
            .Select(s => s.IdEspecialidad)
            .FirstOrDefaultAsync(ct);
        if (idEspecialidadServicio is not > 0)
            return [];

        // Filtrar por servicio y especialidad en la consulta para evitar recorrer
        // todos los profesionales y consultar la base por cada uno.
        var profesionales = await _context.Profesionales
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .Where(p =>
                p.Estado == "activo" &&
                _context.ProfesionalServicios.Any(ps =>
                    ps.IdProfesional == p.IdProfesional &&
                    ps.IdServicio == idServicio &&
                    ps.Activo) &&
                _context.ProfesionalEspecialidades.Any(pe =>
                    pe.IdProfesional == p.IdProfesional &&
                    pe.IdEspecialidad == idEspecialidadServicio.Value))
            .ToListAsync(ct);

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
                c.FechaHora.AddMinutes(
                    c.DuracionMinutos > 0 ? c.DuracionMinutos : duracionMinutos) > inicio)
            .Select(c => c.IdProfesional!.Value)
            .Distinct()
            .ToListAsync(ct);

        var noDisponibles = profesionalesAusentes
            .Concat(profesionalesBloqueados)
            .Concat(profesionalesOcupados)
            .ToHashSet();

        var resultado = new List<ProfesionalDisponibleDto>();
        foreach (var prof in profesionales)
        {
            if (!profesionalesConHorario.Contains(prof.IdProfesional) ||
                noDisponibles.Contains(prof.IdProfesional))
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
                Especialidades = string.IsNullOrWhiteSpace(especialidades) ? "Sin especialidad registrada" : especialidades,
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
        if (NormalizarEstado(cita.Estado) is not ("solicitada" or "pendiente"))
            throw new InvalidOperationException("La solicitud ya fue atendida y no está pendiente de asignación.");

        if (!await _context.Profesionales.AnyAsync(p => p.IdProfesional == dto.IdProfesional && p.Estado == "activo", ct))
            throw new InvalidOperationException("El profesional seleccionado no es válido o no está activo.");

        if (!await _context.Consultorios.AnyAsync(c => c.IdConsultorio == dto.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");

        DateTime fechaHora = dto.Fecha.Date.Add(dto.HoraInicio);
        if (fechaHora < ColombiaClock.Now.AddMinutes(-5))
            throw new InvalidOperationException("No se puede agendar una cita en fecha u hora pasada.");

        if (cita.IdServicio is not > 0)
            throw new InvalidOperationException("La solicitud no tiene un servicio válido asignado y no puede confirmarse.");

        int duracion = await ObtenerDuracionServicioAsync(cita.IdServicio.Value, ct);

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

        if (estadoConfirmada is null)
            throw new InvalidOperationException("No está configurado el estado 'Confirmada' en el catálogo de estados de citas.");

        cita.IdProfesional = dto.IdProfesional;
        cita.IdConsultorio = dto.IdConsultorio;
        cita.FechaHora = fechaHora;
        cita.IdEstado = estadoConfirmada.IdEstado;
        cita.Estado = estadoConfirmada.NombreEstado;
        cita.DuracionMinutos = duracion;
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
    public async Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> EnviarRecordatoriosAsync(
        List<int> idsCitas,
        string? mensajePersonalizado = null,
        CancellationToken ct = default)
    {
        if (idsCitas is null || idsCitas.Count == 0)
            return (0, 0, []);

        DateTime ahora = ColombiaClock.Now;
        int[] idsUnicos = idsCitas.Where(id => id > 0).Distinct().ToArray();
        if (idsUnicos.Length == 0)
            return (0, 0, []);

        string[] estadosRecordables =
        [
            "programada", "Programada", "agendada", "Agendada",
            "confirmada", "Confirmada", "confirmado", "Confirmado"
        ];
        var citas = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.EstadoCita)
            .Where(c => idsUnicos.Contains(c.IdCita)
                && c.FechaHora >= ahora
                && (c.EstadoCita != null
                    ? estadosRecordables.Contains(c.EstadoCita.NombreEstado)
                    : c.Estado != null && estadosRecordables.Contains(c.Estado)))
            .ToListAsync(ct);

        int enviados = 0;
        int fallidos = idsUnicos.Length - citas.Count;
        var idsEnviados = new List<int>();

        foreach (var cita in citas)
        {
            var registro = new RecordatorioCita
            {
                IdCita = cita.IdCita,
                Canal = "email",
                ProgramadoPara = ahora,
                CreadoEn = ahora,
                Intentos = 1
            };
            _context.RecordatoriosCita.Add(registro);

            string profNombre = cita.Profesional?.Usuario != null
                ? $"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}".Trim()
                : $"{cita.Profesional?.Nombres} {cita.Profesional?.Apellidos}".Trim();

            string pacienteNombre = $"{cita.Paciente?.Nombres} {cita.Paciente?.Apellidos}".Trim();

            try
            {
                if (cita.Paciente is null || string.IsNullOrWhiteSpace(cita.Paciente.Correo))
                    throw new InvalidOperationException("El paciente no tiene un correo electrónico registrado.");
                if (_emailService is null)
                    throw new InvalidOperationException("El servicio de correo electrónico no está configurado.");

                await _emailService.SendCitaNotificacionAsync(
                    cita.Paciente.Correo,
                    pacienteNombre,
                    cita.FechaHora,
                    string.IsNullOrWhiteSpace(profNombre) ? "Tu profesional" : profNombre,
                    cita.Servicio?.Nombre ?? "Consulta Odontológica",
                    "recordatorio",
                    ct,
                    mensajePersonalizado: mensajePersonalizado);

                registro.Estado = "enviado";
                registro.EnviadoEn = ColombiaClock.Now;
                registro.UltimoError = null;

                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "EMAIL_RECORDATORIO",
                    TablaAfectada = "Cita",
                    IdRegistro = cita.IdCita,
                    Descripcion = $"Recordatorio de cita enviado al correo {cita.Paciente.Correo}.",
                    IpOrigen = "ServicioRecordatorios",
                    Fecha = ColombiaClock.Now
                });

                enviados++;
                idsEnviados.Add(cita.IdCita);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enviando recordatorio por correo para Cita {IdCita}", cita.IdCita);
                registro.Estado = "fallido";
                registro.UltimoError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                fallidos++;
            }
        }

        await _context.SaveChangesAsync(ct);

        return (enviados, fallidos, idsEnviados);
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
        // La habilitación exige tres condiciones independientes:
        // profesional activo, asignación Profesional_Servicio activa y especialidad coincidente.
        if (idProfesional <= 0)
            return (false, "Debe seleccionar un profesional válido.");

        if (idServicio <= 0)
            return (false, "Debe seleccionar un servicio válido.");

        // DbContext no admite operaciones simultáneas; ejecutar cada consulta por separado.
        bool profesionalActivo = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == idProfesional && p.Estado == "activo", ct);

        var servicio = await _context.Servicios.AsNoTracking()
            .Where(s => s.IdServicio == idServicio && s.Estado == "activo")
            .Select(s => new { s.IdEspecialidad })
            .FirstOrDefaultAsync(ct);

        if (!profesionalActivo)
            return (false, "El profesional seleccionado no existe o no está activo.");

        if (servicio is null)
            return (false, "El servicio seleccionado no existe o no está activo.");

        if (servicio.IdEspecialidad is not int especialidadServicio)
            return (false, "El servicio no tiene una especialidad configurada y no puede asignarse.");

        DateTime fin = fechaHora.AddMinutes(duracionMinutos);
        DateOnly fecha = DateOnly.FromDateTime(fechaHora);

        bool asignacionActiva = await _context.ProfesionalServicios.AsNoTracking()
            .AnyAsync(ps => ps.IdProfesional == idProfesional
                         && ps.IdServicio == idServicio
                         && ps.Activo, ct);

        bool especialidadCoincide = await _context.ProfesionalEspecialidades.AsNoTracking()
            .AnyAsync(pe => pe.IdProfesional == idProfesional
                         && pe.IdEspecialidad == especialidadServicio, ct);

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

        if (!asignacionActiva)
            return (false, "El profesional no tiene una asignación activa para el servicio seleccionado.");

        if (!especialidadCoincide)
            return (false, "La especialidad del servicio no coincide con las especialidades registradas del profesional.");

        if (tieneAusencia)
            return (false, "El profesional no está disponible en la fecha seleccionada por una ausencia registrada.");

        if (tieneBloqueo)
            return (false, "El profesional no está disponible en el horario seleccionado por un bloqueo registrado.");

        if (horarios.Count == 0)
        {
            // RN-17: Un profesional sin horario configurado NO está disponible para
            // nuevas citas. No se hace fallback al horario general de la clínica.
            return (false, "El profesional no tiene horario de atención configurado y no puede recibir citas.");
        }

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

        // Filtramos en la BD por coincidencia directa de texto para evitar
        // traer toda la tabla EstadosCita a memoria en cada creación de cita.
        var candidatos = await _context.EstadosCita
            .AsNoTracking()
            .Where(e => e.NombreEstado != null)
            .ToListAsync(ct);

        return candidatos.FirstOrDefault(e =>
            NormalizarEstado(e.NombreEstado) == estadoNormalizado);
    }

    private static string NormalizarEstado(string? estado)
    {
        return EstadoCitaHelper.Normalize(estado);
    }

    private static bool EsTransicionEstadoPermitida(string estadoActual, string nuevoEstado)
    {
        return EstadoCitaHelper.IsTransitionAllowed(estadoActual, nuevoEstado);
    }

    private static string ConstruirMensajeTransicionNoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "solicitada" => "Una solicitud solo puede pasar a Programada o Confirmada. Para cancelar o rechazar la solicitud use la acción correspondiente.",
            "programada" => "Una cita programada solo puede pasar a Confirmada o En sala de espera. Para cancelar la cita use la función de cancelación.",
            "confirmada" => "Una cita confirmada solo puede pasar a En sala de espera o En consulta. Para cancelar la cita use la función de cancelación.",
            "en_sala_de_espera" => "Una cita en sala de espera solo puede pasar a En consulta. Para cancelar la cita use la función de cancelación.",
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

    // =========================================================================
    // TRAZABILIDAD — HISTORIAL DE ESTADOS (implementa Cita_Historial_Estado)
    // =========================================================================

    /// <summary>
    /// Registra un cambio de estado en <c>Cita_Historial_Estado</c>.
    /// Best-effort: si falla, solo se loguea y la operación principal continúa.
    /// </summary>
    /// <param name="idUsuario">
    /// Id del usuario que realizó el cambio. Puede ser <c>null</c> cuando
    /// la operación fue iniciada por el sistema (tareas programadas, etc.).
    /// El controlador debe pasar este valor cuando lo obtiene de los claims.
    /// </param>
    private async Task RegistrarHistorialEstadoAsync(
        int idCita,
        int? idEstado,
        string estadoTexto,
        int? idUsuario,
        string? motivo,
        CancellationToken ct)
    {
        try
        {
            _context.CitasHistorialEstado.Add(new CitaHistorialEstado
            {
                IdCita = idCita,
                IdEstado = idEstado,
                EstadoTexto = estadoTexto,
                IdUsuario = idUsuario,
                Motivo = motivo,
                FechaCambio = ColombiaClock.Now
            });
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // El historial es trazabilidad, no debe deshacer la operación principal.
            _logger.LogWarning(ex,
                "No se pudo registrar historial de estado. IdCita={IdCita}, EstadoTexto={Estado}",
                idCita, estadoTexto);
        }
    }

    // =========================================================================
    // NOTIFICACIONES INTERNAS (implementa tabla Notificacion)
    // =========================================================================

    /// <summary>
    /// Crea una notificación interna en la tabla <c>Notificacion</c>.
    /// Estas notificaciones son visibles para el paciente en <c>st-pac-03-notificaciones</c>.
    /// Best-effort: si falla, solo se loguea.
    /// </summary>
    private async Task CrearNotificacionInternaCitaAsync(
        int idCita,
        int idPaciente,
        string tipo,
        string titulo,
        string contenido,
        CancellationToken ct)
    {
        try
        {
            _context.Notificaciones.Add(new Notificacion
            {
                IdCita = idCita,
                IdPaciente = idPaciente,
                Tipo = tipo,
                Titulo = titulo,
                Contenido = contenido,
                Canal = "interno",
                Estado = "pendiente",
                FechaProgramada = ColombiaClock.Now,
                CreadaEn = ColombiaClock.Now
            });
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // La notificación no debe deshacer la operación principal.
            _logger.LogWarning(ex,
                "No se pudo crear notificación interna. IdCita={IdCita}, Tipo={Tipo}",
                idCita, tipo);
        }
    }

    // =========================================================================
    // SUGERENCIAS LISTA DE ESPERA (M7 / RF-24)
    // =========================================================================

    /// <inheritdoc/>
    public async Task<List<SugerenciaListaEsperaDto>> ObtenerSugerenciasListaEsperaAsync(
        DateTime fechaHoraLiberada,
        int? idServicio,
        int? idProfesional,
        CancellationToken ct = default)
    {
        try
        {
            var candidatos = await _context.ListaEsperaCitas
                .AsNoTracking()
                .Include(l => l.Paciente)
                .Include(l => l.Servicio)
                .Where(l => l.Estado == "pendiente"
                         && l.FechaDeseadaInicio <= fechaHoraLiberada
                         && l.FechaDeseadaFin >= fechaHoraLiberada
                         && (l.IdServicio == null || l.IdServicio == idServicio)
                         && (l.IdProfesionalPreferido == null || l.IdProfesionalPreferido == idProfesional))
                .OrderBy(l => l.FechaRegistro)
                .ToListAsync(ct);

            if (candidatos.Count == 0)
                return [];

            // Obtener duración y profesionales disponibles para esa franja
            int duracion = await ObtenerDuracionCitaMinutosAsync(ct);
            var disponibles = await ObtenerProfesionalesDisponiblesAsync(
                fechaHoraLiberada.Date,
                fechaHoraLiberada.TimeOfDay,
                duracion,
                idServicio,
                ct);

            return candidatos.Select(c => new SugerenciaListaEsperaDto
            {
                IdListaEspera = c.IdListaEspera,
                IdPaciente = c.IdPaciente,
                NombrePaciente = c.Paciente is null
                                        ? "Paciente desconocido"
                                        : $"{c.Paciente.Nombres} {c.Paciente.Apellidos}".Trim(),
                IdServicio = c.IdServicio,
                NombreServicio = c.Servicio?.Nombre,
                FechaHoraLiberada = fechaHoraLiberada,
                ProfesionalesDisponibles = disponibles,
                Notas = c.Notas
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudieron obtener sugerencias de lista de espera para {FechaHora}", fechaHoraLiberada);
            return [];
        }
    }

    /// <summary>
    /// M3 (RN-29): Intenta enviar correo de cancelación al paciente.
    /// Best-effort: si falla, solo registra la advertencia en el log.
    /// </summary>
    private async Task EnviarCorreoCancelacionAsync(
        int idCita,
        DateTime fechaHoraCita,
        string motivo,
        CancellationToken ct)
    {
        if (_emailService is null) return;

        try
        {
            var cita = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .Include(c => c.Servicio)
                .FirstOrDefaultAsync(c => c.IdCita == idCita, ct);

            if (cita?.Paciente is null || string.IsNullOrWhiteSpace(cita.Paciente.Correo))
                return;

            string nombrePaciente = $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim();
            string nombreProfesional = cita.Profesional is null
                ? "el profesional asignado"
                : $"{cita.Profesional.Nombres} {cita.Profesional.Apellidos}".Trim();
            string servicio = cita.Servicio?.Nombre ?? "Consulta";

            await _emailService.SendCitaNotificacionAsync(
                cita.Paciente.Correo,
                nombrePaciente,
                fechaHoraCita,
                nombreProfesional,
                servicio,
                "Cancelada",
                cancellationToken: ct,
                motivoCancelacion: motivo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo enviar correo de cancelación. IdCita={IdCita} — la cancelación NO se revierte (RN-29).",
                idCita);
        }
    }

    /// <summary>
    /// Escanea la lista de espera para notificar a pacientes interesados cuando se libera una franja horaria.
    /// </summary>
    private async Task NotificarListaEsperaFranjaLiberadaAsync(
        int? idServicio,
        int? idProfesional,
        DateTime fechaHoraLiberada,
        CancellationToken ct)
    {
        try
        {
            var candidatosEspera = await _context.ListaEsperaCitas
                .Where(l => l.Estado == "pendiente"
                         && l.FechaDeseadaInicio <= fechaHoraLiberada
                         && l.FechaDeseadaFin >= fechaHoraLiberada
                         && (l.IdServicio == null || l.IdServicio == idServicio)
                         && (l.IdProfesionalPreferido == null || l.IdProfesionalPreferido == idProfesional))
                .ToListAsync(ct);

            foreach (var candidato in candidatosEspera)
            {
                candidato.Estado = "notificado";

                _context.Notificaciones.Add(new Notificacion
                {
                    IdPaciente = candidato.IdPaciente,
                    Tipo = "lista_espera_liberada",
                    Titulo = "Franja horaria disponible",
                    Contenido = $"Se ha liberado un turno el {fechaHoraLiberada:dd/MM/yyyy} a las {fechaHoraLiberada:HH:mm}. ¡Ingresa ahora para agendar tu cita!",
                    Canal = "interno",
                    Estado = "pendiente",
                    FechaProgramada = ColombiaClock.Now,
                    CreadaEn = ColombiaClock.Now
                });
            }

            if (candidatosEspera.Count > 0)
            {
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al escanear lista de espera para franja liberada {FechaHora}", fechaHoraLiberada);
        }
    }
}
