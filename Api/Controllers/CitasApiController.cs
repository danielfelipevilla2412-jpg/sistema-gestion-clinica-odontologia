using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using System.Security.Claims;

namespace SmileTrack_MVC.Api.Controllers;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class CookieAwareValidateAntiforgeryTokenAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        bool authenticatedBearer = context.HttpContext.User.Identities.Any(identity =>
            identity.IsAuthenticated &&
            string.Equals(identity.AuthenticationType, JwtBearerDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase));

        if (authenticatedBearer)
            return;

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new
            {
                success = false,
                message = "Token antiforgery inválido o ausente."
            });
        }
    }
}

[ApiController]
[Route("")]
public sealed class CitasApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CitasApiController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICitaService _citaService;

    public CitasApiController(AppDbContext context, ILogger<CitasApiController> logger, IEmailService emailService, ICitaService citaService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _citaService = citaService;
    }

    [HttpGet]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        [FromQuery] DateTime? fecha = null,
        [FromQuery] int? profesional = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        // No todos los usuarios autenticados pueden consultar la agenda. En
        // particular, Auxiliar no debe obtener el listado clínico completo.
        if (!User.IsInRole("Administrador") && !User.IsInRole("Recepcionista") &&
            !User.IsInRole("Paciente") && !User.IsInRole("Profesional"))
            return Forbid();
        string? role = null;
        int? idPaciente = null;
        int? idProfesional = null;
        int? idUsuario = null;

        if (User.IsInRole("Paciente"))
        {
            role = "Paciente";
            if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int value) || value <= 0)
                return Forbid();
            idPaciente = value;
        }
        else if (User.IsInRole("Profesional"))
        {
            role = "Profesional";
            if (int.TryParse(User.FindFirstValue("IdProfesional"), out int value) && value > 0)
                idProfesional = value;
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) && userId > 0)
                idUsuario = userId;
            else
                return Forbid();
        }

        int? filtroProfesional = role is null ? profesional : idProfesional;
        var (items, totalRecords) = await _citaService.ObtenerAsync(
            page,
            pageSize,
            role,
            idPaciente,
            filtroProfesional,
            idUsuario,
            search,
            estado,
            fecha,
            ct);
        int duracion = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
        var data = items.Select(c => new
        {
            c.IdCita,
            c.IdPaciente,
            Paciente = c.Paciente == null ? null : new { NombreCompleto = $"{c.Paciente.Nombres} {c.Paciente.Apellidos}".Trim() },
            c.IdProfesional,
            Profesional = c.Profesional == null ? null : new { NombreCompleto = c.Profesional.Usuario == null ? $"{c.Profesional.Nombres} {c.Profesional.Apellidos}".Trim() : $"{c.Profesional.Usuario.Nombre} {c.Profesional.Usuario.Apellidos}".Trim() },
            c.IdServicio,
            Servicio = c.Servicio == null ? null : new { c.Servicio.Nombre },
            c.IdConsultorio,
            Consultorio = c.Consultorio == null ? null : new { c.Consultorio.Nombre, c.Consultorio.Ubicacion },
            c.IdEstado,
            EstadoCatalogo = c.EstadoCita?.NombreEstado,
            c.FechaHora,
            HoraInicio = c.FechaHora.TimeOfDay,
            HoraFin = c.FechaHora.AddMinutes(duracion).TimeOfDay,
            c.Estado,
            c.Notas
        }).ToList();

        return Ok(new { success = true, data, total = totalRecords, page, pageSize, duracionMinutos = duracion });
    }

    [HttpGet]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador de cita inválido." });

        var cita = await _citaService.ObtenerPorIdAsync(id, ct);
        if (cita is null)
            return NotFound(new { success = false, message = "Cita no encontrada." });
        if (!TieneOwnership(cita))
            return Forbid();

        int duracion = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
        return Ok(new
        {
            success = true,
            data = new
            {
                cita.IdCita,
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio,
                cita.IdEstado,
                cita.FechaHora,
                HoraInicio = cita.FechaHora.TimeOfDay,
                HoraFin = cita.FechaHora.AddMinutes(duracion).TimeOfDay,
                cita.Estado,
                cita.Notas
            },
            duracionMinutos = duracion
        });
    }

    [HttpPost]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [Route("api/citas/agenda")]
    public Task<IActionResult> CrearCitaDesdeAgenda([FromBody] CitaAgendaDto dto, CancellationToken ct = default) => CrearDesdeAgenda(dto, ct);

    private async Task<IActionResult> CrearDesdeAgenda(CitaAgendaDto dto, CancellationToken ct)
    {
        if (dto is null || !ModelState.IsValid)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            var erroresPorCampo = ModelState
                .Where(kvp => kvp.Value != null && kvp.Value.Errors.Any())
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors
                        .Select(e => e.ErrorMessage)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList() as IReadOnlyList<string>
                );

            _logger.LogWarning(
                "[Agenda API] CrearDesdeAgenda: ModelState inválido. Errores={Errores}",
                string.Join(" | ", errores));

            return BadRequest(new
            {
                success = false,
                message = errores.Count == 1
                    ? errores[0]
                    : "Datos inválidos para agendar la cita. Revise los campos resaltados.",
                errors = errores,
                errorsByField = erroresPorCampo
            });
        }

        var request = new CitaApiRequest
        {
            IdPaciente = dto.IdPaciente,
            IdProfesional = dto.IdProfesional,
            IdServicio = dto.IdServicio,
            IdConsultorio = dto.IdConsultorio,
            FechaHora = dto.Fecha.Date.Add(dto.HoraInicio),
            Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Programada" : dto.Estado.Trim(),
            Notas = dto.Notas
        };

        try
        {
            bool updating = dto.IdCita is > 0;
            Cita cita = updating
                ? await _citaService.ActualizarAsync(dto.IdCita!.Value, new CitaApiUpdateDto
                {
                    IdCita = dto.IdCita.Value,
                    IdPaciente = request.IdPaciente,
                    IdProfesional = request.IdProfesional,
                    IdServicio = request.IdServicio,
                    IdConsultorio = request.IdConsultorio,
                    FechaHora = request.FechaHora,
                    Estado = request.Estado,
                    Notas = request.Notas
                }, ct) ?? throw new InvalidOperationException("La cita no existe.")
                : await _citaService.CrearAsync(request, ct);

            int duracion = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
            await RegistrarAuditoriaAsync(updating ? "UPDATE" : "INSERT", cita.IdCita, "Cita guardada desde Agenda.", ct);
            return Ok(new { success = true, message = updating ? "Cita actualizada exitosamente." : "Cita agendada exitosamente.", id = cita.IdCita, idEstado = cita.IdEstado, estado = cita.Estado, updated = updating, duracionMinutos = duracion });
        }
        catch (InvalidOperationException ex)
        {
            return EsConflicto(ex.Message) ? Conflict(new { success = false, message = ex.Message }) : BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] CitaApiUpdateDto dto, CancellationToken ct = default)
    {
        if (dto is null || id != dto.IdCita || !ModelState.IsValid || dto.IdPaciente <= 0)
            return BadRequest(new { success = false, message = "Datos de cita inválidos." });

        var existente = await _citaService.ObtenerPorIdAsync(id, ct);
        if (existente is null)
            return NotFound(new { success = false, message = "Cita no encontrada." });
        if (!TieneOwnership(existente))
            return Forbid();

        try
        {
            var actualizada = await _citaService.ActualizarAsync(id, dto, ct);
            if (actualizada is null)
                return NotFound(new { success = false, message = "Cita no encontrada." });
            int duracion = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
            await RegistrarAuditoriaAsync("UPDATE", actualizada.IdCita, "Cita actualizada mediante API.", ct);
            return Ok(new { success = true, message = "Cita actualizada exitosamente.", id = actualizada.IdCita, idEstado = actualizada.IdEstado, estado = actualizada.Estado, duracionMinutos = duracion });
        }
        catch (InvalidOperationException ex)
        {
            return EsConflicto(ex.Message) ? Conflict(new { success = false, message = ex.Message }) : BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Paciente", Policy = "ApiOrCookie")]
    [Route("api/citas/solicitar")]
    public async Task<IActionResult> SolicitarCita([FromBody] CitaSolicitudPacienteDto dto, CancellationToken ct = default)
    {
        if (dto is null || !ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de solicitud inválidos." });

        if (!int.TryParse(User.FindFirstValue("IdPaciente"), out int idPaciente) || idPaciente <= 0)
            return Forbid();

        try
        {
            var cita = await _citaService.SolicitarCitaPacienteAsync(idPaciente, dto, ct);
            await RegistrarAuditoriaAsync("INSERT", cita.IdCita, "Solicitud de cita creada por paciente.", ct);
            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = "Solicitud de cita enviada exitosamente. La clínica revisará y confirmará tu cita en breve.",
                id = cita.IdCita,
                estado = cita.Estado
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [Route("api/citas/solicitudes-pendientes")]
    public async Task<IActionResult> ObtenerSolicitudesPendientes(CancellationToken ct = default)
    {
        var solicitudes = await _citaService.ObtenerSolicitudesPendientesAsync(ct);
        var payload = solicitudes.Select(c => new
        {
            idCita = c.IdCita,
            idPaciente = c.IdPaciente,
            paciente = c.Paciente is null ? null : new { nombreCompleto = $"{c.Paciente.Nombres} {c.Paciente.Apellidos}".Trim() },
            fechaHora = c.FechaHora,
            fecha = c.FechaHora.Date,
            horaInicio = c.FechaHora.TimeOfDay,
            servicio = c.Servicio is null ? null : new { idServicio = c.Servicio.IdServicio, nombre = c.Servicio.Nombre },
            estado = c.Estado,
            notas = c.Notas,
            motivoConsulta = c.MotivoConsulta
        }).ToList();

        return Ok(new { success = true, data = payload, total = payload.Count });
    }

    [HttpGet]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas/profesionales-disponibles")]
    public async Task<IActionResult> ObtenerProfesionalesDisponibles(
        [FromQuery] DateTime fecha,
        [FromQuery] string horaInicio,
        [FromQuery] int? duracionMinutos = null,
        [FromQuery] int? idServicio = null,
        CancellationToken ct = default)
    {
        if (!TimeSpan.TryParse(horaInicio, out TimeSpan tsHora))
        {
            return BadRequest(new { success = false, message = "Formato de hora inválido. Debe ser HH:mm." });
        }

        int duracion = duracionMinutos is > 0 ? duracionMinutos.Value : await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
        var profesionales = await _citaService.ObtenerProfesionalesDisponiblesAsync(fecha, tsHora, duracion, idServicio, ct);

        return Ok(new { success = true, data = profesionales });
    }

    [HttpPut]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}/confirmar-asignacion")]
    public async Task<IActionResult> ConfirmarYAsignar(int id, [FromBody] CitaConfirmacionAsignacionDto dto, CancellationToken ct = default)
    {
        if (dto is null || id != dto.IdCita || !ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de confirmación inválidos." });

        try
        {
            var cita = await _citaService.ConfirmarYAsignarCitaAsync(id, dto, ct);
            if (cita is null)
                return NotFound(new { success = false, message = "Cita no encontrada." });

            await RegistrarAuditoriaAsync("UPDATE", cita.IdCita, "Cita confirmada y asignada por recepción.", ct);
            return Ok(new
            {
                success = true,
                message = "Cita confirmada y profesional asignado exitosamente.",
                id = cita.IdCita,
                estado = cita.Estado,
                idProfesional = cita.IdProfesional,
                idConsultorio = cita.IdConsultorio,
                fechaHora = cita.FechaHora
            });
        }
        catch (InvalidOperationException ex)
        {
            return EsConflicto(ex.Message) ? Conflict(new { success = false, message = ex.Message }) : BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [Route("api/citas/recordatorios/enviar")]
    public async Task<IActionResult> EnviarRecordatorios([FromBody] EnviarRecordatoriosDto dto, CancellationToken ct = default)
    {
        if (dto is null || !ModelState.IsValid || dto.IdsCitas == null || dto.IdsCitas.Count == 0)
            return BadRequest(new { success = false, message = "Debe seleccionar al menos una cita para enviar recordatorios." });

        var (enviados, fallidos) = await _citaService.EnviarRecordatoriosAsync(dto.IdsCitas, dto.MensajePersonalizado, ct);
        return Ok(new
        {
            success = true,
            enviados,
            fallidos,
            message = enviados > 0
                ? $"Se enviaron {enviados} recordatorio(s) exitosamente por correo electrónico."
                : "No fue posible enviar recordatorios (verifique que los pacientes tengan correo registrado)."
        });
    }

    /// <summary>
    /// Devuelve el catálogo de servicios odontológicos activos de la clínica.
    /// Usado por el modal de solicitud de cita del paciente para cargar los tipos de servicio
    /// disponibles desde la base de datos (en lugar de opciones hardcoded en el HTML).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/servicios")]
    public async Task<IActionResult> ObtenerServicios(CancellationToken ct = default)
    {
        var servicios = await _context.Servicios
            .Where(s => s.Estado == "activo" || string.IsNullOrEmpty(s.Estado))
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                idServicio = s.IdServicio,
                nombre = s.Nombre,
                descripcion = s.Descripcion,
                precio = s.Precio
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = servicios });
    }


    public sealed class CitaNotasDto { public int IdCita { get; set; } public string? Notas { get; set; } }

    [HttpPut]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Profesional", Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}/notas")]
    public async Task<IActionResult> ActualizarNotas(int id, [FromBody] CitaNotasDto dto, CancellationToken ct = default)
    {
        if (dto is null || id != dto.IdCita || id <= 0 || !ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de notas inválidos." });
        var cita = await _citaService.ObtenerPorIdAsync(id, ct);
        if (cita is null) return NotFound(new { success = false, message = "Cita no encontrada." });
        if (!EsProfesionalPropietario(cita)) return Forbid();
        try
        {
            var actualizada = await _citaService.ActualizarNotasAsync(id, dto.Notas ?? string.Empty, ct);
            return Ok(new { success = true, message = "Notas actualizadas correctamente.", id = actualizada!.IdCita, notas = actualizada.Notas ?? string.Empty });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    public sealed class CambiarEstadoCitaDto { public string? Estado { get; set; } }

    [HttpPut]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Roles = "Profesional", Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoCitaDto dto, CancellationToken ct = default)
    {
        if (id <= 0 || dto is null || !ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Estado))
            return BadRequest(new { success = false, message = "El estado de la cita es obligatorio." });
        var cita = await _citaService.ObtenerPorIdAsync(id, ct);
        if (cita is null) return NotFound(new { success = false, message = "La cita no existe." });
        if (!EsProfesionalPropietario(cita)) return Forbid();
        try
        {
            var actualizada = await _citaService.CambiarEstadoAsync(id, dto.Estado, ct);
            if (actualizada is null) return NotFound(new { success = false, message = "La cita no existe." });
            string estado = NormalizarEstado(actualizada.Estado);
            if (estado is "confirmada" or "cancelada") await EnviarNotificacionCitaAsync(actualizada.IdCita, estado, ct);
            return Ok(new { success = true, id = actualizada.IdCita, idEstado = actualizada.IdEstado, estado = actualizada.Estado, message = $"Cita actualizada a '{actualizada.Estado}' correctamente." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpDelete]
    [CookieAwareValidateAntiforgeryToken]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}")]
    public async Task<IActionResult> Cancelar(int id, CancellationToken ct = default)
    {
        if (id <= 0) return BadRequest(new { success = false, message = "Identificador de cita inválido." });
        var cita = await _citaService.ObtenerPorIdAsync(id, ct);
        if (cita is null) return NotFound(new { success = false, message = "Cita no encontrada." });
        if (User.IsInRole("Profesional")) return Forbid();
        if (User.IsInRole("Paciente") && !EsPacientePropietario(cita)) return Forbid();
        if (!User.IsInRole("Paciente") && !User.IsInRole("Administrador") && !User.IsInRole("Recepcionista")) return Forbid();
        try
        {
            TimeSpan? anticipacionMinima = User.IsInRole("Paciente") ? TimeSpan.FromHours(2) : null;
            if (!await _citaService.CancelarAsync(id, anticipacionMinima, ct)) return NotFound(new { success = false, message = "Cita no encontrada." });
            await RegistrarAuditoriaAsync("UPDATE", id, "Cita cancelada mediante API.", ct);
            return Ok(new { success = true, message = "Cita cancelada exitosamente.", id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    private bool TieneOwnership(Cita cita) => User.IsInRole("Administrador") || User.IsInRole("Recepcionista") || (User.IsInRole("Paciente") && EsPacientePropietario(cita)) || (User.IsInRole("Profesional") && EsProfesionalPropietario(cita));
    private bool EsPacientePropietario(Cita cita) => int.TryParse(User.FindFirstValue("IdPaciente"), out int id) && id == cita.IdPaciente;
    private bool EsProfesionalPropietario(Cita cita) => int.TryParse(User.FindFirstValue("IdProfesional"), out int id) && id == cita.IdProfesional;
    private static bool EsConflicto(string? message) => message?.Contains("horario", StringComparison.OrdinalIgnoreCase) == true;
    private static string NormalizarEstado(string? estado) => (estado ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "agendada" or "programado" => "programada",
        "confirmado" => "confirmada",
        "cancelado" => "cancelada",
        "no asistio" or "no asistió" or "no-show" => "no_asistida",
        "completada" or "finalizada" or "realizada" => "atendida",
        var value => value
    };

    private async Task RegistrarAuditoriaAsync(string accion, int idRegistro, string descripcion, CancellationToken ct)
    {
        try
        {
            _context.Auditorias.Add(new Auditoria { Accion = accion, TablaAfectada = "Cita", IdRegistro = idRegistro, Descripcion = descripcion, IdUsuario = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : null, IpOrigen = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida", Fecha = DateTime.Now });
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo registrar auditoría de cita IdCita={IdCita}", idRegistro); }
    }

    private async Task EnviarNotificacionCitaAsync(int idCita, string estado, CancellationToken ct)
    {
        try
        {
            var cita = await _context.Citas.AsNoTracking().Include(c => c.Paciente).Include(c => c.Profesional).Include(c => c.Servicio).FirstOrDefaultAsync(c => c.IdCita == idCita, ct);
            if (cita?.Paciente is null || string.IsNullOrWhiteSpace(cita.Paciente.Correo)) return;
            await _emailService.SendCitaNotificacionAsync(cita.Paciente.Correo, $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim(), cita.FechaHora, cita.Profesional == null ? "Tu profesional" : $"{cita.Profesional.Nombres} {cita.Profesional.Apellidos}".Trim(), cita.Servicio?.Nombre ?? "Consulta", estado);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudo enviar notificación de cita IdCita={IdCita}", idCita); }
    }
}
