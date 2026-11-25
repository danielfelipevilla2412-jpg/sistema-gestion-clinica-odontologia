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
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Exceptions;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.DTOs.GestionDeCitas;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Services.GestionDeCitas;

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
    private readonly ICitaQueryService _queries;
    private readonly ICitaDisponibilidadService _disponibilidad;
    private readonly IRecordatorioCitaService _recordatorioCitaService;

    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;

    public CitaService(
        AppDbContext context,
        ILogger<CitaService> logger,
        SmileTrack_MVC.Services.Email.IEmailService? emailService = null)
        : this(
            context,
            logger,
            new CitaQueryService(context),
            new CitaDisponibilidadService(context, NullLogger<CitaDisponibilidadService>.Instance),
            emailService)
    {
    }

    public CitaService(
        AppDbContext context,
        ILogger<CitaService> logger,
        ICitaQueryService queries,
        SmileTrack_MVC.Services.Email.IEmailService? emailService = null)
        : this(
            context,
            logger,
            queries,
            new CitaDisponibilidadService(context, NullLogger<CitaDisponibilidadService>.Instance),
            emailService)
    {
    }

    public CitaService(
        AppDbContext context,
        ILogger<CitaService> logger,
        ICitaQueryService queries,
        ICitaDisponibilidadService disponibilidad,
        SmileTrack_MVC.Services.Email.IEmailService? emailService = null,
        IRecordatorioCitaService? recordatorioCitaService = null,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _logger = logger;
        _queries = queries;
        _disponibilidad = disponibilidad;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _recordatorioCitaService = recordatorioCitaService ??
            new RecordatorioCitaService(
                context,
                emailService,
                NullLogger<RecordatorioCitaService>.Instance);
    }

    // =========================================================================
    // KPI DE GESTIÓN
    // =========================================================================

    public Task<CitasKpiDto> ObtenerKpisGestionAsync(
        DateTime fechaReferencia,
        CancellationToken ct = default)
    {
        return _queries.ObtenerKpisGestionAsync(fechaReferencia, ct);
    }

    // =========================================================================
    // DURACIÓN CONFIGURABLE (C-04)
    // =========================================================================

    /// <inheritdoc/>
    public Task<int> ObtenerDuracionCitaMinutosAsync(CancellationToken ct = default)
        => _disponibilidad.ObtenerDuracionCitaMinutosAsync(ct);

    // =========================================================================
    // VALIDACIÓN HORARIO DE ATENCIÓN CLÍNICA (C-05)
    // =========================================================================

    /// <inheritdoc/>
    public Task<(bool EsValido, string? Mensaje)> ValidarHorarioClinicaAsync(
        DateTime fechaHora,
        int duracionMinutos = DuracionFallbackMinutos,
        CancellationToken ct = default)
        => _disponibilidad.ValidarHorarioClinicaAsync(fechaHora, duracionMinutos, ct);
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
        int duracion = await _disponibilidad.ObtenerDuracionCitaMinutosAsync(ct);

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

    public Task<(List<Cita> Items, int TotalRecords)> ObtenerAsync(
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
        return _queries.ObtenerAsync(
            page,
            pageSize,
            role,
            idPaciente,
            idProfesional,
            idUsuario,
            search,
            estado,
            fecha,
            ct);
    }

    // =========================================================================
    // OBTENER POR ID
    // =========================================================================

    public Task<Cita?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return _queries.ObtenerPorIdAsync(id, ct);
    }

    public Task<PagedResult<Cita>> ObtenerListadoGestionAsync(
        PaginationQuery pagination,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return _queries.ObtenerListadoGestionAsync(pagination, page, pageSize, ct);
    }

    private async Task<T> ExecuteWithSerializableTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct,
        bool clearTrackerOnRetry = false)
    {
        if (!_context.Database.IsRelational())
            return await operation();

        var strategy = _context.Database.CreateExecutionStrategy();
        int attempts = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            // A transient SQL failure rolls back the transaction, but EF may retain
            // modified entities from the failed attempt. Reload them on retry so the
            // business validation observes the committed database state.
            if (clearTrackerOnRetry && attempts++ > 0)
                _context.ChangeTracker.Clear();

            await using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private static string LimitarDescripcionAuditoria(string descripcion) =>
        descripcion.Length <= 255 ? descripcion : descripcion[..255];

    // =========================================================================
    // CREAR CITA (C-01, C-04)
    // =========================================================================

    private async Task ValidarPermisoRecepcionAsync(string accion, CancellationToken ct)
    {
        var actor = _httpContextAccessor?.HttpContext?.User;
        if (actor?.IsInRole("Recepcionista") == true && !actor.IsInRole("Administrador")
            && !await PermisoReagendamiento.PermitidoAsync(_context, ct, accion))
            throw new InvalidOperationException($"El rol Recepcionista no tiene permiso para {accion} citas.");
    }

    public Task<Cita> CrearAsync(
        CitaApiRequest request,
        CancellationToken ct = default,
        int? idUsuarioActor = null,
        string? ipOrigen = null)
    {
        return CrearCitaAsync(request, false, ct, idUsuarioActor, ipOrigen);
    }

    private async Task<Cita> CrearCitaAsync(
        CitaApiRequest request,
        bool asignarConsultorio,
        CancellationToken ct,
        int? idUsuarioActor,
        string? ipOrigen)
    {
        await ValidarPermisoRecepcionAsync("agendar", ct);
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

        if (request.FechaHora < ColombiaClock.Now.AddMinutes(-5))
        {
            throw new InvalidOperationException("No se puede agendar una cita en un horario pasado.");
        }

        // ── Validación de horario de atención clínica (C-05) ─────────────
        int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(request.IdServicio!.Value, ct);
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, duracion, ct);
        if (!horarioValido)
            throw new CitaConflictException(mensajeHorario!);

        var disponibilidad = await _disponibilidad.ValidarDisponibilidadProfesionalAsync(
            request.IdProfesional!.Value,
            request.IdServicio!.Value,
            request.FechaHora,
            duracion,
            ct);
        if (!disponibilidad.EsValida)
            throw new CitaConflictException(disponibilidad.Mensaje!);

        var citaCreada = await ExecuteWithSerializableTransactionAsync(async () =>
        {
            var idConsultorio = asignarConsultorio
                ? await BuscarConsultorioLibreAsync(request.FechaHora, duracion, null, ct)
                : request.IdConsultorio;
            if (asignarConsultorio && idConsultorio is null)
                throw new CitaConflictException("No hay consultorios disponibles para el horario seleccionado.");
            if (idConsultorio is > 0 && !await _context.Consultorios.AnyAsync(
                    c => c.IdConsultorio == idConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
                throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
            if (idConsultorio is <= 0)
                throw new InvalidOperationException("El consultorio seleccionado no está disponible.");

            var conflicto = await VerificarConflictoCompletoAsync(
                request.IdProfesional!.Value,
                request.IdPaciente,
                idConsultorio,
                request.FechaHora,
                duracion,
                null,
                ct);

            if (conflicto.HayConflicto)
                throw new CitaConflictException(conflicto.Mensaje);

            string estadoSolicitud = string.IsNullOrWhiteSpace(request.Estado) ? "programada" : request.Estado.Trim();
            var estadoEntidad = await ResolverEstadoCatalogoAsync(estadoSolicitud, ct);

            if (estadoEntidad is null)
                throw new InvalidOperationException("El estado de la cita no es válido.");

            string estadoNuevo = NormalizarEstado(estadoEntidad.NombreEstado);
            if (estadoNuevo == "cancelada")
                await ValidarPermisoRecepcionAsync("cancelar", ct);
            if (estadoNuevo is not ("programada" or "confirmada"))
                throw new InvalidOperationException("Una cita nueva solo puede crearse como Programada o Confirmada.");

            var cita = new Cita
            {
                IdPaciente = request.IdPaciente,
                IdProfesional = request.IdProfesional,
                IdServicio = request.IdServicio,
                IdConsultorio = idConsultorio,
                FechaHora = request.FechaHora,
                IdEstado = estadoEntidad.IdEstado,
                Estado = estadoEntidad.NombreEstado,
                Notas = request.Notas?.Trim(),
                MotivoConsulta = request.Notas?.Trim(),
                DuracionMinutos = duracion,
                FechaCreacion = ColombiaClock.Now,
                CreadoPor = idUsuarioActor
            };

            _context.Citas.Add(cita);
            AgregarHistorialEstado(cita, idUsuarioActor, "Cita creada");
            await _context.SaveChangesAsync(ct);
            await SincronizarRecordatoriosAsync(cita, nuevaProgramacion: true, ct);
            _context.Auditorias.Add(new Auditoria
            {
                Accion = "INSERT",
                TablaAfectada = "Cita",
                IdRegistro = cita.IdCita,
                Descripcion = LimitarDescripcionAuditoria($"Cita creada. IdPaciente={cita.IdPaciente}, IdProfesional={cita.IdProfesional}, Fecha={cita.FechaHora:yyyy-MM-dd HH:mm}"),
                DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
                {
                    cita.IdPaciente,
                    cita.IdProfesional,
                    cita.IdServicio,
                    cita.IdConsultorio,
                    cita.IdEstado,
                    cita.Estado,
                    cita.FechaHora
                }),
                IdUsuario = idUsuarioActor,
                IpOrigen = ipOrigen ?? "CitaService",
                Fecha = ColombiaClock.Now
            });
            await _context.SaveChangesAsync(ct);

            return cita;
        }, ct);

        // Las notificaciones permanecen fuera de la transacción de agenda.
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

    public async Task<Cita?> ActualizarAsync(int id, CitaApiUpdateDto request, CancellationToken ct = default,
        int? operadorId = null, string? ipOrigen = null, string? motivoHistorial = null)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        request.IdUsuarioModificacion ??= operadorId;

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

        string estadoAnteriorTexto = cita.Estado;
        DateTime fechaHoraAnterior = cita.FechaHora;
        bool fechaModificada = request.FechaHora != cita.FechaHora;
        if (fechaModificada)
        {
            await ValidarPermisoRecepcionAsync("reagendar", ct);
            var actor = _httpContextAccessor?.HttpContext?.User;
            if (actor?.IsInRole("Profesional") == true && !actor.IsInRole("Administrador"))
            {
                if (!int.TryParse(actor.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var usuario)
                    || usuario <= 0 || !cita.IdProfesional.HasValue
                    || request.IdProfesional != cita.IdProfesional
                    || !await _context.Profesionales.AnyAsync(p => p.IdProfesional == cita.IdProfesional && p.IdUsuario == usuario, ct))
                    throw new InvalidOperationException("Solo puedes reagendar citas que tienes asignadas.");

                if (!await PermisoReagendamiento.PermitidoAsync(_context, ct, profesional: true))
                    throw new InvalidOperationException("El rol Profesional no tiene permiso para reagendar citas.");
            }
        }
        if (fechaModificada &&
            (cita.FechaHora < ColombiaClock.Now ||
             EstadoCitaHelper.EsEstadoFinal(cita.Estado) ||
             NormalizarEstado(cita.Estado) is "cancelada" or "no_asistida" or "rechazada"))
        {
            throw new InvalidOperationException(
                "No se puede reprogramar una cita cuyo horario original ya pasó o cuyo estado es definitivo.");
        }

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
            if (estadoActual != estadoNuevo && estadoNuevo == "cancelada")
                await ValidarPermisoRecepcionAsync("cancelar", ct);
            if (estadoActual != estadoNuevo && !EstadoCitaHelper.IsTransitionAllowed(estadoActual, estadoNuevo))
                throw new InvalidOperationException(EstadoCitaHelper.ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));
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
        int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(request.IdServicio!.Value, ct);
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(request.FechaHora, duracion, ct);
        if (!horarioValido)
            throw new CitaConflictException(mensajeHorario!);

        var disponibilidad = await _disponibilidad.ValidarDisponibilidadProfesionalAsync(
            request.IdProfesional!.Value,
            request.IdServicio!.Value,
            request.FechaHora,
            duracion,
            ct);
        if (!disponibilidad.EsValida)
            throw new CitaConflictException(disponibilidad.Mensaje!);

        var citaActualizada = await ExecuteWithSerializableTransactionAsync(async () =>
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
                throw new CitaConflictException(conflicto.Mensaje);

            var datosAnteriores = new
            {
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio,
                cita.IdEstado,
                cita.Estado,
                cita.FechaHora,
                cita.Notas
            };
            string estadoAnterior = cita.Estado;
            bool eraRecordable = EsEstadoRecordable(estadoAnterior);
            bool cambioEstado = estadoSolicitado is not null &&
                NormalizarEstado(estadoAnterior) != NormalizarEstado(estadoSolicitado.NombreEstado);

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
            bool cambioHorario = cita.Reprogramar(request.FechaHora);
            cita.DuracionMinutos = duracion;

            if (estadoSolicitado is not null)
            {
                cita.IdEstado = estadoSolicitado.IdEstado;
                cita.Estado = estadoSolicitado.NombreEstado;
            }

            bool nuevaProgramacion = cambioHorario || (!eraRecordable && EsEstadoRecordable(cita.Estado));
            if (!eraRecordable && EsEstadoRecordable(cita.Estado))
                cita.NuevaProgramacion();
            await SincronizarRecordatoriosAsync(cita, nuevaProgramacion, ct);

            if (cambioEstado || !string.IsNullOrWhiteSpace(motivoHistorial))
            {
                string motivo = string.IsNullOrWhiteSpace(motivoHistorial)
                    ? $"Estado cambiado de '{estadoAnterior}' a '{cita.Estado}'"
                    : motivoHistorial.Trim();
                AgregarHistorialEstado(
                    cita,
                    request.IdUsuarioModificacion ?? operadorId,
                    motivo);
            }

            cita.Notas = request.Notas?.Trim();
            cita.MotivoConsulta = request.Notas?.Trim();
            _context.Auditorias.Add(new Auditoria
            {
                Accion = "UPDATE",
                TablaAfectada = "Cita",
                IdRegistro = cita.IdCita,
                Descripcion = LimitarDescripcionAuditoria($"Cita actualizada. IdPaciente={cita.IdPaciente}, Fecha={cita.FechaHora:yyyy-MM-dd HH:mm}"),
                DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(datosAnteriores),
                DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
                {
                    cita.IdPaciente,
                    cita.IdProfesional,
                    cita.IdServicio,
                    cita.IdConsultorio,
                    cita.IdEstado,
                    cita.Estado,
                    cita.FechaHora,
                    cita.Notas
                }),
                IdUsuario = operadorId ?? request.IdUsuarioModificacion,
                IpOrigen = ipOrigen ?? "CitaService",
                Fecha = ColombiaClock.Now
            });
            await _context.SaveChangesAsync(ct);
            return cita;
        }, ct);

        return citaActualizada;
    }

    // =========================================================================
    // CAMBIAR ESTADO
    // =========================================================================

    public async Task<Cita?> CambiarEstadoAsync(int id, string nuevoEstado, int? idUsuarioActor = null, CancellationToken ct = default,
        string? ipOrigen = null)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string estadoActual = NormalizarEstado(cita.Estado);
        string estadoNuevo = NormalizarEstado(nuevoEstado);
        if (estadoNuevo == "cancelada" && NormalizarEstado(cita.Estado) != estadoNuevo)
            await ValidarPermisoRecepcionAsync("cancelar", ct);

        if ((estadoNuevo is "en_proceso" or "atendida") &&
            (estadoNuevo == "atendida" ? cita.FechaHora.Date > ColombiaClock.Today : cita.FechaHora > ColombiaClock.Now))
        {
            throw new InvalidOperationException(
                "La atención solo puede iniciarse o finalizarse desde el día programado y a partir de la hora de la cita.");
        }

        if (string.IsNullOrWhiteSpace(estadoNuevo) || !EstadoCitaHelper.IsTransitionAllowed(estadoActual, estadoNuevo))
            throw new InvalidOperationException(EstadoCitaHelper.ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));

        if (estadoNuevo is "confirmada" or "en_sala_de_espera" or "en_proceso")
        {
            if (!cita.IdProfesional.HasValue || cita.IdProfesional.Value <= 0)
                throw new InvalidOperationException("No se puede confirmar o iniciar la atención de una cita sin profesional asignado.");

            if (!cita.IdConsultorio.HasValue || cita.IdConsultorio.Value <= 0)
                throw new InvalidOperationException("No se puede confirmar o iniciar la atención de una cita sin consultorio asignado.");
        }

        if (estadoNuevo is "en_proceso" && cita.FechaHora > ColombiaClock.Now)
            throw new InvalidOperationException("La atención solo puede iniciarse el día programado y cuando llegue la hora programada de la cita.");

        if (estadoNuevo == "atendida" && cita.FechaHora.Date > ColombiaClock.Today)
            throw new InvalidOperationException("Solo se puede marcar la cita como atendida desde el día programado o después.");
        if (estadoNuevo == "no_asistida" && cita.FechaHora > ColombiaClock.Now)
            throw new InvalidOperationException("No se puede marcar la cita como no asistida antes de la hora programada en el día programado.");
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

        // Guardar el estado anterior antes de actualizar la entidad.
        string estadoAnteriorTexto = cita.Estado;
        int? idEstadoAnterior = cita.IdEstado;
        bool eraRecordable = EsEstadoRecordable(estadoAnteriorTexto);

        cita.IdEstado = estadoDestino.IdEstado;
        cita.Estado = estadoDestino.NombreEstado;
        bool nuevaProgramacion = !eraRecordable && EsEstadoRecordable(cita.Estado);
        if (nuevaProgramacion)
            cita.NuevaProgramacion();
        await SincronizarRecordatoriosAsync(cita, nuevaProgramacion, ct);
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

        _context.CitasHistorialEstado.Add(new CitaHistorialEstado
        {
            IdCita = cita.IdCita,
            IdEstado = cita.IdEstado,
            EstadoTexto = cita.Estado,
            IdUsuario = idUsuarioActor,
            Motivo = $"Estado cambiado de '{estadoAnteriorTexto}' a '{cita.Estado}'",
            FechaCambio = ColombiaClock.Now
        });
        _context.Auditorias.Add(new Auditoria
        {
            Accion = "UPDATE",
            TablaAfectada = "Cita",
            IdRegistro = cita.IdCita,
            Descripcion = LimitarDescripcionAuditoria($"Estado cambiado de '{estadoAnteriorTexto}' a '{cita.Estado}'."),
            DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(new
            {
                IdEstado = idEstadoAnterior,
                Estado = estadoAnteriorTexto
            }),
            DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
            {
                IdEstado = cita.IdEstado,
                Estado = cita.Estado
            }),
            IdUsuario = idUsuarioActor,
            IpOrigen = ipOrigen ?? "CitaService",
            Fecha = ColombiaClock.Now
        });
        await _context.SaveChangesAsync(ct);

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
        TimeSpan? anticipacionMinima = null,
        CancellationToken ct = default,
        int? idUsuarioActor = null,
        string? ipOrigen = null)
    {
        return await CancelarAsync(
            id,
            motivo: "Cancelación de cita",
            anticipacionMinima: anticipacionMinima,
            ct: ct,
            idUsuarioActor: idUsuarioActor,
            ipOrigen: ipOrigen);
    }

    public async Task<bool> CancelarAsync(
        int id,
        string? motivo,
        TimeSpan? anticipacionMinima = null,
        CancellationToken ct = default,
        int? idUsuarioActor = null,
        string? ipOrigen = null)
    {
        await ValidarPermisoRecepcionAsync("cancelar", ct);
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return false;

        if (EstadoCitaHelper.EsEstadoCancelado(cita.Estado))
            throw new InvalidOperationException("La cita ya está cancelada.");
        if (EstadoCitaHelper.EsEstadoFinal(cita.Estado))
            throw new InvalidOperationException("No se puede cancelar una cita que ya fue finalizada.");

        if (cita.FechaHora <= ColombiaClock.Now)
            throw new InvalidOperationException("La cita no puede cancelarse porque su fecha y hora anterior ya ha vencido.");

        if (anticipacionMinima.HasValue && cita.FechaHora - ColombiaClock.Now < anticipacionMinima.Value)
            throw new InvalidOperationException(
                $"No es posible cancelar citas con menos de {anticipacionMinima.Value.TotalHours:0} horas de anticipación.");

        // Guardar estado anterior ANTES de la cancelación
        string estadoAnteriorCancelar = cita.Estado;
        int? idEstadoAnteriorCancelar = cita.IdEstado;
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

        await SincronizarRecordatoriosAsync(cita, nuevaProgramacion: false, ct);

        string motivoHistorial = $"Cita cancelada (estado anterior: '{estadoAnteriorCancelar}', motivo: '{motivoCancelacion}')";
        AgregarHistorialEstado(cita, idUsuarioActor, motivoHistorial);
        _context.Auditorias.Add(new Auditoria
        {
            Accion = "UPDATE",
            TablaAfectada = "Cita",
            IdRegistro = cita.IdCita,
            Descripcion = LimitarDescripcionAuditoria($"Cita cancelada. Motivo: {motivoCancelacion}"),
            DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(new
            {
                IdEstado = idEstadoAnteriorCancelar,
                Estado = estadoAnteriorCancelar,
                cita.FechaHora,
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio
            }),
            DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
            {
                IdEstado = cita.IdEstado,
                Estado = cita.Estado,
                cita.FechaHora,
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio,
                MotivoCancelacion = motivoCancelacion
            }),
            IdUsuario = idUsuarioActor,
            IpOrigen = ipOrigen ?? "CitaService",
            Fecha = ColombiaClock.Now
        });
        await _context.SaveChangesAsync(ct);

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
    // SOLICITUD DE CITA POR PACIENTE
    // =========================================================================

    /// <inheritdoc/>
    public async Task<Cita> AgendarCitaPacienteAsync(
        int idPaciente,
        CitaSolicitudPacienteDto dto,
        CancellationToken ct = default,
        int? idUsuarioActor = null,
        string? ipOrigen = null)
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
        if (dto.IdProfesional is <= 0)
            throw new InvalidOperationException("El profesional preferido no es válido.");
        if (dto.HoraInicio < TimeSpan.Zero || dto.HoraInicio >= TimeSpan.FromDays(1))
            throw new InvalidOperationException("La hora solicitada no es válida.");
        if (dto.Notas?.Length > 4000)
            throw new InvalidOperationException("Las notas no pueden superar los 4000 caracteres.");

        var servicio = await _context.Servicios
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdServicio == dto.IdServicio && s.Estado == "activo", ct);

        if (servicio is null)
            throw new InvalidOperationException("El servicio seleccionado no existe o no está activo.");

        DateTime fechaHora = dto.Fecha.Date.Add(dto.HoraInicio);
        if (fechaHora <= ColombiaClock.Now)
            throw new InvalidOperationException("La fecha y hora solicitadas deben ser futuras.");

        int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(servicio.IdServicio, ct);
        var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(fechaHora, duracion, ct);
        if (!horarioValido)
            throw new InvalidOperationException(mensajeHorario ?? "El horario solicitado está fuera del horario de atención.");

        var disponibles = await _disponibilidad.ObtenerProfesionalesDisponiblesAsync(
            fechaHora.Date, fechaHora.TimeOfDay, duracion, servicio.IdServicio, ct);
        var profesional = disponibles.OrderBy(p => p.IdProfesional)
            .FirstOrDefault(p => dto.IdProfesional == null || p.IdProfesional == dto.IdProfesional);
        if (profesional is null)
            throw new CitaConflictException("No hay un profesional disponible para el horario seleccionado.");

        return await CrearCitaAsync(new CitaApiRequest
        {
            IdPaciente = idPaciente,
            IdProfesional = profesional.IdProfesional,
            IdServicio = servicio.IdServicio,
            FechaHora = fechaHora,
            Estado = "Confirmada",
            Notas = dto.Notas
        }, true, ct, idUsuarioActor, ipOrigen);
    }

    private Task<int?> BuscarConsultorioLibreAsync(DateTime inicio, int duracion, int? idExcluir, CancellationToken ct)
    {
        var fin = inicio.AddMinutes(duracion);
        var excluir = idExcluir ?? 0;
        var ocupadas = _context.Citas.Where(c =>
            c.IdCita != excluir && c.IdConsultorio.HasValue
            && !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower())
            && c.FechaHora < fin
            && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : duracion) > inicio);

        return _context.Consultorios.AsNoTracking()
            .Where(c => (c.Estado == "disponible" || c.Estado == "activo")
                && !ocupadas.Any(a => a.IdConsultorio == c.IdConsultorio))
            .OrderBy(c => c.IdConsultorio)
            .Select(c => (int?)c.IdConsultorio)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ListaEsperaCita> RegistrarListaEsperaAsync(
        int idPaciente,
        ListaEsperaSolicitudDto dto,
        CancellationToken ct = default)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));
        if (idPaciente <= 0 ||
            !await _context.Pacientes.AnyAsync(p => p.IdPaciente == idPaciente && p.Estado == "activo", ct))
            throw new InvalidOperationException("El paciente no existe o no se encuentra activo.");
        if (dto.IdServicio is not > 0 ||
            !await _context.Servicios.AnyAsync(s => s.IdServicio == dto.IdServicio && s.Estado == "activo", ct))
            throw new InvalidOperationException("Debe seleccionar un servicio activo.");
        if (dto.IdProfesionalPreferido is <= 0)
            throw new InvalidOperationException("El profesional preferido no es válido.");

        DateTime inicio = dto.FechaDeseadaInicio.Date;
        DateTime fin = dto.FechaDeseadaFin.Date.AddDays(1).AddTicks(-1);
        if (inicio < ColombiaClock.Today || fin < inicio)
            throw new InvalidOperationException("El rango de fechas de la lista de espera debe ser futuro y válido.");
        if (dto.Notas?.Length > 500)
            throw new InvalidOperationException("Las notas no pueden superar 500 caracteres.");

        bool existeInscripcion = await _context.ListaEsperaCitas.AnyAsync(l =>
            l.IdPaciente == idPaciente &&
            l.IdServicio == dto.IdServicio &&
            l.Estado == "pendiente" &&
            l.FechaDeseadaInicio <= fin &&
            l.FechaDeseadaFin >= inicio,
            ct);
        if (existeInscripcion)
            throw new InvalidOperationException("Ya tienes una inscripción pendiente para ese servicio en un rango de fechas que se cruza.");

        if (dto.IdProfesionalPreferido is > 0)
        {
            var elegibles = await _disponibilidad.ObtenerProfesionalesElegiblesPorServicioAsync(dto.IdServicio.Value, ct);
            if (!elegibles.Any(p => p.IdProfesional == dto.IdProfesionalPreferido.Value))
                throw new InvalidOperationException("El profesional preferido no está habilitado para el servicio seleccionado.");
        }

        var entrada = new ListaEsperaCita
        {
            IdPaciente = idPaciente,
            IdServicio = dto.IdServicio.Value,
            IdProfesionalPreferido = dto.IdProfesionalPreferido,
            FechaDeseadaInicio = inicio,
            FechaDeseadaFin = fin,
            Estado = "pendiente",
            FechaRegistro = ColombiaClock.Now,
            Notas = string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas.Trim()
        };

        _context.ListaEsperaCitas.Add(entrada);
        await _context.SaveChangesAsync(ct);
        return entrada;
    }

    public async Task<List<TimeSpan>> ObtenerHorariosSolicitudPacienteAsync(
        int idPaciente,
        int idServicio,
        int? idProfesionalPreferido,
        DateTime fecha,
        CancellationToken ct = default)
    {
        var horarios = await _disponibilidad.ObtenerHorariosSolicitudPacienteAsync(
            idPaciente, idServicio, idProfesionalPreferido, fecha, ct);
        if (horarios.Count == 0)
            return horarios;

        int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(idServicio, ct);
        var disponibles = new List<TimeSpan>();
        foreach (var hora in horarios)
        {
            if (await BuscarConsultorioLibreAsync(fecha.Date.Add(hora), duracion, null, ct) is not null)
                disponibles.Add(hora);
        }
        return disponibles;
    }

    /// <inheritdoc/>
    public Task<List<TimeSpan>> ObtenerHorariosDisponiblesPacienteAsync(
        int idPaciente,
        int idServicio,
        int idProfesional,
        DateTime fecha,
        CancellationToken ct = default)
        => _disponibilidad.ObtenerHorariosDisponiblesPacienteAsync(
            idPaciente, idServicio, idProfesional, fecha, ct);

    /// <inheritdoc/>
    public Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesPorServicioAsync(
        int idServicio,
        CancellationToken ct = default)
        => _disponibilidad.ObtenerProfesionalesPorServicioAsync(idServicio, ct);

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
        CancellationToken ct = default,
        string? ipOrigen = null)
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

        var datosAnteriores = new
        {
            cita.IdEstado,
            cita.Estado,
            cita.IdPaciente,
            cita.IdServicio,
            cita.IdProfesionalPreferido,
            cita.FechaHora
        };
        var estadoRechazada = await _context.EstadosCita
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "rechazada", ct);
        cita.Estado = estadoRechazada?.NombreEstado ?? "Rechazada";
        cita.IdEstado = estadoRechazada?.IdEstado;
        await SincronizarRecordatoriosAsync(cita, nuevaProgramacion: false, ct);
        AgregarHistorialEstado(
            cita,
            idUsuarioActor,
            $"Solicitud rechazada. Motivo: {motivoNormalizado}");
        _context.Auditorias.Add(new Auditoria
        {
            Accion = "UPDATE",
            TablaAfectada = "Cita",
            IdRegistro = cita.IdCita,
            Descripcion = LimitarDescripcionAuditoria($"Solicitud de cita rechazada. Motivo: {motivoNormalizado}"),
            DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(datosAnteriores),
            DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
            {
                cita.IdEstado,
                cita.Estado,
                cita.IdPaciente,
                cita.IdServicio,
                cita.IdProfesionalPreferido,
                cita.FechaHora,
                MotivoRechazo = motivoNormalizado
            }),
            IdUsuario = idUsuarioActor,
            IpOrigen = ipOrigen ?? "CitaService",
            Fecha = ColombiaClock.Now
        });
        await _context.SaveChangesAsync(ct);
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
    public Task<List<ProfesionalDisponibleDto>> ObtenerProfesionalesDisponiblesAsync(
        DateTime fecha,
        TimeSpan horaInicio,
        int duracionMinutos = 60,
        int? idServicio = null,
        CancellationToken ct = default)
        => _disponibilidad.ObtenerProfesionalesDisponiblesAsync(
            fecha, horaInicio, duracionMinutos, idServicio, ct);

    // =========================================================================
    // CONFIRMAR Y ASIGNAR CITA (RECEPCIÓN)
    // =========================================================================

    /// <inheritdoc/>
    public async Task<Cita?> ConfirmarYAsignarCitaAsync(
        int idCita,
        CitaConfirmacionAsignacionDto dto,
        CancellationToken ct = default,
        int? idUsuarioActor = null,
        string? ipOrigen = null)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        await ValidarPermisoRecepcionAsync("agendar", ct);
        var confirmada = await ExecuteWithSerializableTransactionAsync<Cita?>(async () =>
        {
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

            int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(cita.IdServicio.Value, ct);

            // Validar horario clínica
            var (horarioValido, mensajeHorario) = await ValidarHorarioClinicaAsync(fechaHora, duracion, ct);
            if (!horarioValido)
                throw new CitaConflictException(mensajeHorario!);

            // Validar disponibilidad del profesional
            var disp = await _disponibilidad.ValidarDisponibilidadProfesionalAsync(dto.IdProfesional, cita.IdServicio ?? 0, fechaHora, duracion, ct);
            if (!disp.EsValida)
                throw new CitaConflictException(disp.Mensaje!);

            var habilitados = _context.Consultorios.AsNoTracking()
                .Where(c => c.Estado == "disponible" || c.Estado == "activo");
            var idConsultorio = dto.IdConsultorio;
            if (idConsultorio.HasValue)
            {
                if (!await habilitados.AnyAsync(c => c.IdConsultorio == idConsultorio.Value, ct))
                    throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
            }
            else
            {
                idConsultorio = await BuscarConsultorioLibreAsync(fechaHora, duracion, idCita, ct);

                if (!idConsultorio.HasValue)
                    throw new CitaConflictException("No hay consultorios disponibles para el horario solicitado.");
            }

            // Validar conflicto de recursos excluyendo la cita actual
            var conflicto = await VerificarConflictoCompletoAsync(
                idProfesional: dto.IdProfesional,
                idPaciente: cita.IdPaciente,
                idConsultorio: idConsultorio,
                fechaHora: fechaHora,
                duracionMinutos: duracion,
                idCitaExcluir: idCita,
                ct: ct);

            if (conflicto.HayConflicto)
                throw new CitaConflictException(conflicto.Mensaje);

            // Obtener estado 'Confirmada'
            var estadoConfirmada = await _context.EstadosCita
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "confirmada", ct);

            if (estadoConfirmada is null)
                throw new InvalidOperationException("No está configurado el estado 'Confirmada' en el catálogo de estados de citas.");

            var datosAnteriores = new
            {
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdProfesionalPreferido,
                cita.IdServicio,
                cita.IdConsultorio,
                cita.IdEstado,
                cita.Estado,
                cita.FechaHora
            };
            cita.IdProfesional = dto.IdProfesional;
            cita.IdProfesionalPreferido = null;
            cita.IdConsultorio = idConsultorio;
            cita.FechaHora = fechaHora;
            cita.IdEstado = estadoConfirmada.IdEstado;
            cita.Estado = estadoConfirmada.NombreEstado;
            cita.DuracionMinutos = duracion;
            if (!string.IsNullOrWhiteSpace(dto.Notas))
                cita.Notas = dto.Notas.Trim();

            AgregarHistorialEstado(
                cita,
                idUsuarioActor,
                $"Solicitud confirmada y asignada a profesional #{dto.IdProfesional}.");
            _context.Auditorias.Add(new Auditoria
            {
                Accion = "UPDATE",
                TablaAfectada = "Cita",
                IdRegistro = cita.IdCita,
                Descripcion = "Cita confirmada y asignada por recepción.",
                DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(datosAnteriores),
                DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
                {
                    cita.IdPaciente,
                    cita.IdProfesional,
                    cita.IdProfesionalPreferido,
                    cita.IdServicio,
                    cita.IdConsultorio,
                    cita.IdEstado,
                    cita.Estado,
                    cita.FechaHora
                }),
                IdUsuario = idUsuarioActor,
                IpOrigen = ipOrigen ?? "CitaService",
                Fecha = ColombiaClock.Now
            });
            await _context.SaveChangesAsync(ct);
            return cita;
        }, ct);

        if (confirmada is null)
            return null;

        // Notificar al paciente por correo si hay servicio de email configurado
        if (_emailService is not null && confirmada.Paciente is not null && !string.IsNullOrWhiteSpace(confirmada.Paciente.Correo))
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
                    confirmada.Paciente.Correo,
                    $"{confirmada.Paciente.Nombres} {confirmada.Paciente.Apellidos}".Trim(),
                    confirmada.FechaHora,
                    string.IsNullOrWhiteSpace(profNombre) ? "Profesional asignado" : profNombre,
                    confirmada.Servicio?.Nombre ?? "Consulta Odontológica",
                    "confirmada",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No fue posible enviar correo de confirmación de cita {IdCita}", idCita);
            }
        }

        return confirmada;
    }
    // =========================================================================
    // ENVIAR RECORDATORIOS POR CORREO
    // =========================================================================

    /// <inheritdoc/>
    public Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> EnviarRecordatoriosAsync(
        List<int> idsCitas,
        string? mensajePersonalizado = null,
        CancellationToken ct = default)
    {
        if (idsCitas is null)
        {
            return Task.FromResult<(int Enviados, int Fallidos, List<int> IdsEnviados)>(
                (0, 0, []));
        }

        return _recordatorioCitaService.EnviarAsync(idsCitas, mensajePersonalizado, ct);
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

    private async Task SincronizarRecordatoriosAsync(
        Cita cita,
        bool nuevaProgramacion,
        CancellationToken ct)
    {
        DateTime ahora = ColombiaClock.Now;
        bool recordable = cita.FechaHora > ahora && EsEstadoRecordable(cita.Estado);

        var pendientesAnteriores = await _context.RecordatoriosCita
            .Where(r => r.IdCita == cita.IdCita && r.Estado == "pendiente" &&
                (!recordable || (nuevaProgramacion &&
                    (r.ProgramacionId == null || r.ProgramacionId != cita.ProgramacionId))))
            .ToListAsync(ct);

        foreach (var recordatorio in pendientesAnteriores)
        {
            recordatorio.Estado = "cancelado";
            recordatorio.ProximoIntentoEn = null;
            recordatorio.ReservaId = null;
            recordatorio.ReservaHasta = null;
        }

        if (!recordable || !nuevaProgramacion)
            return;

        bool existeParaProgramacion = await _context.RecordatoriosCita
            .AnyAsync(r => r.IdCita == cita.IdCita && r.Canal == "email" &&
                           r.ProgramacionId == cita.ProgramacionId, ct);
        if (existeParaProgramacion)
            return;

        DateTime fechaEnvio = cita.FechaHora.AddHours(-24);
        if (fechaEnvio < ahora)
            fechaEnvio = ahora;

        _context.RecordatoriosCita.Add(new RecordatorioCita
        {
            IdCita = cita.IdCita,
            ProgramacionId = cita.ProgramacionId,
            Canal = "email",
            Estado = "pendiente",
            ProgramadoPara = fechaEnvio,
            ProximoIntentoEn = fechaEnvio,
            CreadoEn = ahora
        });
    }

    private static bool EsEstadoRecordable(string? estado)
    {
        string normalizado = NormalizarEstado(estado);
        return EstadoCitaHelper.EstadosRecordables.Contains(normalizado);
    }

    private static string NormalizarEstado(string? estado)
    {
        return EstadoCitaHelper.Normalize(estado);
    }

    // =========================================================================
    // TRAZABILIDAD — HISTORIAL DE ESTADOS (implementa Cita_Historial_Estado)
    // =========================================================================

    private void AgregarHistorialEstado(Cita cita, int? idUsuario, string motivo)
    {
        _context.CitasHistorialEstado.Add(new CitaHistorialEstado
        {
            Cita = cita,
            IdEstado = cita.IdEstado,
            EstadoTexto = cita.Estado,
            IdUsuario = idUsuario,
            Motivo = motivo,
            FechaCambio = ColombiaClock.Now
        });
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
                Estado = "enviada",
                FechaProgramada = ColombiaClock.Now,
                FechaEnvio = ColombiaClock.Now,
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
    public async Task<ListaEsperaCita> RegistrarListaEsperaAsync(
        int idPaciente,
        RegistrarListaEsperaDto request,
        CancellationToken ct = default)
    {
        if (request.FechaDeseadaInicio <= DateTime.Now || request.FechaDeseadaFin <= request.FechaDeseadaInicio)
            throw new InvalidOperationException("El rango de fechas de la lista de espera no es válido.");

        bool pacienteActivo = await _context.Pacientes.AsNoTracking()
            .AnyAsync(p => p.IdPaciente == idPaciente && p.Estado == "activo", ct);
        if (!pacienteActivo)
            throw new InvalidOperationException("El paciente seleccionado no está activo.");

        if (request.IdServicio.HasValue && !await _context.Servicios.AsNoTracking()
                .AnyAsync(s => s.IdServicio == request.IdServicio.Value && s.Estado == "activo", ct))
            throw new InvalidOperationException("El servicio seleccionado no está disponible.");

        if (request.IdProfesionalPreferido.HasValue && !await _context.Profesionales.AsNoTracking()
                .AnyAsync(p => p.IdProfesional == request.IdProfesionalPreferido.Value && p.Estado == "activo", ct))
            throw new InvalidOperationException("El profesional seleccionado no está disponible.");

        var entrada = new ListaEsperaCita
        {
            IdPaciente = idPaciente,
            IdServicio = request.IdServicio,
            IdProfesionalPreferido = request.IdProfesionalPreferido,
            FechaDeseadaInicio = request.FechaDeseadaInicio,
            FechaDeseadaFin = request.FechaDeseadaFin,
            Estado = "pendiente",
            FechaRegistro = DateTime.UtcNow,
            Notas = request.Notas?.Trim()
        };

        _context.ListaEsperaCitas.Add(entrada);
        await _context.SaveChangesAsync(ct);
        return entrada;
    }

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
            int duracion = await _disponibilidad.ObtenerDuracionCitaMinutosAsync(ct);
            var disponibles = await _disponibilidad.ObtenerProfesionalesDisponiblesAsync(
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

    /// <summary>Asigna la franja liberada al primer candidato compatible y notifica al paciente.</summary>
    private async Task NotificarListaEsperaFranjaLiberadaAsync(
        int? idServicio,
        int? idProfesional,
        DateTime fechaHoraLiberada,
        CancellationToken ct)
    {
        if (idServicio is not > 0 || idProfesional is not > 0)
            return;

        var candidatos = await _context.ListaEsperaCitas
            .AsNoTracking()
            .Where(l => l.Estado == "pendiente"
                     && l.FechaDeseadaInicio <= fechaHoraLiberada
                     && l.FechaDeseadaFin >= fechaHoraLiberada
                     && (l.IdServicio == null || l.IdServicio == idServicio)
                     && (l.IdProfesionalPreferido == null || l.IdProfesionalPreferido == idProfesional))
            .OrderBy(l => l.FechaRegistro)
            .Select(l => l.IdListaEspera)
            .ToListAsync(ct);

        foreach (int idListaEspera in candidatos)
        {
            Cita? citaAsignada;
            try
            {
                citaAsignada = await IntentarAsignarListaEsperaAsync(
                    idListaEspera,
                    idServicio.Value,
                    idProfesional.Value,
                    fechaHoraLiberada,
                    ct);
            }
            catch (Exception ex) when (ex is CitaConflictException or InvalidOperationException or DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                _logger.LogInformation(
                    ex,
                    "La solicitud de lista de espera {IdListaEspera} no pudo ocupar la franja liberada {FechaHora}.",
                    idListaEspera,
                    fechaHoraLiberada);
                continue;
            }

            if (citaAsignada is null)
                continue;

            try
            {
                await CrearNotificacionInternaCitaAsync(
                    citaAsignada.IdCita,
                    citaAsignada.IdPaciente,
                    "lista_espera_asignada",
                    "Cita asignada desde lista de espera",
                    $"Se asignó automáticamente tu cita para el {fechaHoraLiberada:dd/MM/yyyy} a las {fechaHoraLiberada:HH:mm}.",
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "No se pudo notificar la cita asignada {IdCita}.", citaAsignada.IdCita);
            }

            return;
        }
    }

    private async Task<Cita?> IntentarAsignarListaEsperaAsync(
        int idListaEspera,
        int idServicio,
        int idProfesional,
        DateTime fechaHora,
        CancellationToken ct)
    {
        return await ExecuteWithSerializableTransactionAsync<Cita?>(async () =>
        {
            var entrada = await _context.ListaEsperaCitas
                .FirstOrDefaultAsync(l => l.IdListaEspera == idListaEspera &&
                                          l.Estado == "pendiente", ct);
            if (entrada is null ||
                (entrada.IdServicio.HasValue && entrada.IdServicio != idServicio) ||
                (entrada.IdProfesionalPreferido.HasValue && entrada.IdProfesionalPreferido != idProfesional) ||
                entrada.FechaDeseadaInicio > fechaHora ||
                entrada.FechaDeseadaFin < fechaHora)
                return null;

            if (!await _context.Pacientes.AnyAsync(
                    p => p.IdPaciente == entrada.IdPaciente && p.Estado == "activo", ct))
                return null;

            var servicio = await _context.Servicios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IdServicio == idServicio && s.Estado == "activo", ct);
            if (servicio is null)
                return null;

            int duracion = await _disponibilidad.ObtenerDuracionServicioAsync(idServicio, ct);
            var (horarioValido, _) = await ValidarHorarioClinicaAsync(fechaHora, duracion, ct);
            if (!horarioValido)
                return null;

            var disponibilidad = await _disponibilidad.ValidarDisponibilidadProfesionalAsync(
                idProfesional, idServicio, fechaHora, duracion, ct);
            if (!disponibilidad.EsValida)
                return null;

            var consultorios = await _context.Consultorios
                .AsNoTracking()
                .Where(c => c.Estado == "disponible" || c.Estado == "activo")
                .OrderBy(c => c.IdConsultorio)
                .Select(c => c.IdConsultorio)
                .ToListAsync(ct);
            if (consultorios.Count == 0)
                return null;

            var estadoConfirmado = await ResolverEstadoCatalogoAsync("Confirmada", ct);
            if (estadoConfirmado is null)
                throw new InvalidOperationException("No está configurado el estado 'Confirmada' en el catálogo de citas.");

            foreach (int idConsultorio in consultorios)
            {
                var conflicto = await VerificarConflictoCompletoAsync(
                    idProfesional,
                    entrada.IdPaciente,
                    idConsultorio,
                    fechaHora,
                    duracion,
                    ct: ct);
                if (conflicto.HayConflicto)
                    continue;

                var cita = new Cita
                {
                    IdPaciente = entrada.IdPaciente,
                    IdProfesional = idProfesional,
                    IdServicio = idServicio,
                    IdConsultorio = idConsultorio,
                    FechaHora = fechaHora,
                    IdEstado = estadoConfirmado.IdEstado,
                    Estado = estadoConfirmado.NombreEstado,
                    Notas = entrada.Notas,
                    DuracionMinutos = duracion,
                    FechaCreacion = ColombiaClock.Now
                };
                _context.Citas.Add(cita);
                AgregarHistorialEstado(
                    cita,
                    null,
                    $"Asignación automática desde lista de espera {idListaEspera}.");
                await _context.SaveChangesAsync(ct);

                await SincronizarRecordatoriosAsync(cita, nuevaProgramacion: true, ct);

                entrada.Estado = "asignada";
                entrada.IdCitaAsignada = cita.IdCita;
                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "INSERT",
                    TablaAfectada = "Cita",
                    IdRegistro = cita.IdCita,
                    Descripcion = LimitarDescripcionAuditoria($"Cita asignada automáticamente desde lista de espera {idListaEspera}."),
                    DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        cita.IdPaciente,
                        cita.IdProfesional,
                        cita.IdServicio,
                        cita.IdConsultorio,
                        cita.IdEstado,
                        cita.Estado,
                        cita.FechaHora
                    }),
                    IdUsuario = null,
                    IpOrigen = "ListaEsperaAutomatica",
                    Fecha = ColombiaClock.Now
                });
                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "UPDATE",
                    TablaAfectada = "Lista_Espera_Cita",
                    IdRegistro = entrada.IdListaEspera,
                    Descripcion = LimitarDescripcionAuditoria($"Solicitud asignada automáticamente a la cita {cita.IdCita}."),
                    IdUsuario = null,
                    IpOrigen = "ListaEsperaAutomatica",
                    Fecha = ColombiaClock.Now
                });
                await _context.SaveChangesAsync(ct);
                return cita;
            }

            return null;
        }, ct);
    }
}
