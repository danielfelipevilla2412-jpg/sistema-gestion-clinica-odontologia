using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Api.Models;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Helpers;
using System.Net;
using System.Security.Claims;

namespace SmileTrack_MVC.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CitasApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CitasApiController> _logger;
    private readonly IEmailService _emailService;
    private readonly ICitaService _citaService;
    private const int DuracionCitaMinutos = 60;

    public CitasApiController(
        AppDbContext context,
        ILogger<CitasApiController> logger,
        IEmailService emailService,
        ICitaService citaService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _citaService = citaService;
    }

    // ================================================================
    // CREAR CITA DESDE AGENDA
    // ================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("api/appointments")]
    public async Task<IActionResult> CrearCitaDesdeAppointments(
        [FromBody] CitaAgendaDto dto,
        CancellationToken ct = default)
    {
        return await CrearCitaDesdeAgendaInterna(dto, ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("api/citas/agenda")]
    public async Task<IActionResult> CrearCitaDesdeAgenda(
        [FromBody] CitaAgendaDto dto,
        CancellationToken ct = default)
    {
        return await CrearCitaDesdeAgendaInterna(dto, ct);
    }

    private async Task<IActionResult> CrearCitaDesdeAgendaInterna(
        CitaAgendaDto dto,
        CancellationToken ct)
    {
        if (dto == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Los datos de la cita son obligatorios."
            });
        }

        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            _logger.LogWarning(
                "CrearCitaDesdeAgenda: ModelState inválido. Errores={Errores}",
                string.Join("|", errores));

            return BadRequest(new
            {
                success = false,
                message = "Datos inválidos para agendar la cita.",
                errors = errores
            });
        }

        try
        {
            // La hora final siempre se deriva de la hora inicial.
            var inicio = dto.Fecha.Date.Add(dto.HoraInicio);
            var fin = inicio.AddMinutes(DuracionCitaMinutos);

            if (inicio < DateTime.Now.AddMinutes(-5))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se puede agendar una cita en un horario pasado."
                });
            }

            if (!await _context.Pacientes.AnyAsync(
                    p => p.IdPaciente == dto.IdPaciente &&
                         p.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El paciente seleccionado no es válido."
                });
            }

            if (!await _context.Profesionales.AnyAsync(
                    p => p.IdProfesional == dto.IdProfesional &&
                         p.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El profesional seleccionado no está disponible."
                });
            }

            if (!await _context.Servicios.AnyAsync(
                    s => s.IdServicio == dto.IdServicio &&
                         s.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El servicio seleccionado no está disponible."
                });
            }

            if (!await _context.Consultorios.AnyAsync(
                    c => c.IdConsultorio == dto.IdConsultorio &&
                         (c.Estado == "disponible" ||
                          c.Estado == "activo"),
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El consultorio seleccionado no está disponible."
                });
            }

            string estadoSolicitud =
                string.IsNullOrWhiteSpace(dto.Estado)
                    ? "Programada"
                    : dto.Estado.Trim();

            var estadoEntidad = await _context.EstadosCita
                .FirstOrDefaultAsync(
                    e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                         (estadoSolicitud.ToLower() == "agendada" &&
                          e.NombreEstado.ToLower() == "programada"),
                    ct);

            if (estadoEntidad == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El estado de la cita no es válido."
                });
            }

            bool hayConflicto = await _context.Citas.AnyAsync(
                c =>
                    c.IdProfesional == dto.IdProfesional &&
                    c.IdCita != (dto.IdCita ?? 0) &&
                    c.Estado != "cancelada" &&
                    c.Estado != "Cancelada" &&
                    c.Estado != "cancelado" &&
                    c.FechaHora < fin &&
                    c.FechaHora.AddMinutes(DuracionCitaMinutos) > inicio,
                ct);

            if (hayConflicto)
            {
                _logger.LogInformation(
                    "Conflicto de agenda. Profesional={IdProfesional}, Fecha={Fecha}",
                    dto.IdProfesional,
                    inicio);

                return Conflict(new
                {
                    success = false,
                    message =
                        "El profesional ya tiene una cita asignada en ese horario. Seleccione otro horario."
                });
            }

            Cita citaEntidad;

            if (dto.IdCita.HasValue && dto.IdCita.Value > 0)
            {
                citaEntidad = await _context.Citas
                    .FirstOrDefaultAsync(
                        c => c.IdCita == dto.IdCita.Value,
                        ct)
                    ?? throw new InvalidOperationException(
                        "La cita no existe.");

                citaEntidad.IdPaciente = dto.IdPaciente;
                citaEntidad.IdProfesional = dto.IdProfesional;
                citaEntidad.IdConsultorio = dto.IdConsultorio;
                citaEntidad.IdServicio = dto.IdServicio;
                citaEntidad.FechaHora = inicio;
                citaEntidad.IdEstado = estadoEntidad.IdEstado;
                citaEntidad.Estado = estadoEntidad.NombreEstado;
                citaEntidad.Notas = dto.Notas?.Trim();

                _context.Citas.Update(citaEntidad);
            }
            else
            {
                citaEntidad = new Cita
                {
                    IdPaciente = dto.IdPaciente,
                    IdProfesional = dto.IdProfesional,
                    IdConsultorio = dto.IdConsultorio,
                    IdServicio = dto.IdServicio,
                    FechaHora = inicio,
                    IdEstado = estadoEntidad.IdEstado,
                    Estado = estadoEntidad.NombreEstado,
                    Notas = dto.Notas?.Trim()
                };

                _context.Citas.Add(citaEntidad);
            }

            int guardados = await _context.SaveChangesAsync(ct);

            if (guardados <= 0)
            {
                _logger.LogError(
                    "CrearCitaDesdeAgenda: SaveChanges no modificó registros.");

                return StatusCode(
                    (int)HttpStatusCode.InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "No se pudo guardar la cita. Intente nuevamente."
                    });
            }

            bool esActualizacion =
                dto.IdCita.HasValue && dto.IdCita.Value > 0;

            await RegistrarAuditoriaAsync(
                accion: esActualizacion ? "UPDATE" : "INSERT",
                tablaAfectada: "Cita",
                idRegistro: citaEntidad.IdCita,
                descripcion:
                    $"{(esActualizacion ? "Cita actualizada" : "Cita creada")} desde Agenda. " +
                    $"IdPaciente={citaEntidad.IdPaciente}, " +
                    $"IdProfesional={citaEntidad.IdProfesional}, " +
                    $"FechaHora={citaEntidad.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"Estado\":\"{citaEntidad.Estado}\"," +
                    $"\"FechaHora\":\"{citaEntidad.FechaHora:O}\"," +
                    $"\"IdPaciente\":{citaEntidad.IdPaciente}," +
                    $"\"IdProfesional\":{citaEntidad.IdProfesional}}}",
                ct: ct);

            _logger.LogInformation(
                "Cita guardada desde Agenda. IdCita={IdCita}, Usuario={Usuario}",
                citaEntidad.IdCita,
                User.Identity?.Name ?? "anonimo");

            return Ok(new
            {
                success = true,
                message = esActualizacion
                    ? "Cita actualizada exitosamente."
                    : "Cita agendada exitosamente.",
                id = citaEntidad.IdCita,
                idEstado = citaEntidad.IdEstado,
                estado = citaEntidad.Estado,
                updated = esActualizacion,
                duracionMinutos = DuracionCitaMinutos
            });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación cancelada al crear cita desde Agenda.");

            return StatusCode(
                (int)HttpStatusCode.BadRequest,
                new
                {
                    success = false,
                    message = "La operación fue cancelada."
                });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Conflicto de concurrencia al guardar cita desde Agenda.");

            return Conflict(new
            {
                success = false,
                message =
                    "Los datos cambiaron durante la operación. Actualice la página e inténtelo nuevamente."
            });
        }
        catch (DbUpdateException ex) when (
            EsViolacionIndiceUnico(ex, out _))
        {
            _logger.LogError(
                ex,
                "Violación UNIQUE al guardar cita desde Agenda.");

            return Conflict(new
            {
                success = false,
                message =
                    "Ya existe una cita registrada con estas características."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos al guardar cita desde Agenda.");

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Error de base de datos al guardar la cita."
                });
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException al guardar cita desde Agenda. Number={Number}",
                ex.Number);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message =
                        "Error de conectividad con la base de datos."
                });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Error inesperado al crear cita desde Agenda.");

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message =
                        "Error interno del servidor. El incidente fue registrado."
                });
        }
    }

    // ================================================================
    // API: LISTAR CITAS
    // ================================================================

    [HttpGet]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas")]
    public async Task<IActionResult> ApiListarCitas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            IQueryable<Cita> citasQuery = _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
                .Include(c => c.Servicio)
                .Include(c => c.Consultorio)
                .Include(c => c.EstadoCita);

            if (User.IsInRole("Paciente"))
            {
                string? claim = User.FindFirstValue("IdPaciente");

                if (!int.TryParse(claim, out int idPaciente) ||
                    idPaciente <= 0)
                {
                    return Forbid();
                }

                citasQuery = citasQuery.Where(
                    c => c.IdPaciente == idPaciente);
            }
            else if (User.IsInRole("Profesional"))
            {
                string? claim =
                    User.FindFirstValue("IdProfesional");

                int idProfesional;

                if (!int.TryParse(claim, out idProfesional) ||
                    idProfesional <= 0)
                {
                    // Compatibilidad con sesiones antiguas: si el claim no existe,
                    // resolver la relación segura desde el usuario autenticado.
                    string? userIdClaim =
                        User.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!int.TryParse(userIdClaim, out int idUsuario) ||
                        idUsuario <= 0)
                    {
                        return Forbid();
                    }

                    int? resolvedId = await _context.Profesionales
                        .AsNoTracking()
                        .Where(p => p.IdUsuario == idUsuario)
                        .Select(p => (int?)p.IdProfesional)
                        .FirstOrDefaultAsync(ct);

                    if (!resolvedId.HasValue || resolvedId.Value <= 0)
                    {
                        return Forbid();
                    }

                    idProfesional = resolvedId.Value;
                }

                citasQuery = citasQuery.Where(
                    c => c.IdProfesional == idProfesional);
            }

            int totalRecords =
                await citasQuery.CountAsync(ct);

            var citas = await citasQuery
                .OrderByDescending(c => c.FechaHora)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.IdCita,
                    c.IdPaciente,

                    Paciente =
                        c.Paciente == null
                            ? null
                            : new
                            {
                                NombreCompleto =
                                    string.Concat(
                                        c.Paciente.Nombres,
                                        " ",
                                        c.Paciente.Apellidos)
                                    .Trim()
                            },

                    c.IdProfesional,

                    Profesional =
                        c.Profesional == null
                            ? null
                            : new
                            {
                                NombreCompleto =
                                    c.Profesional.Usuario != null
                                        ? string.Concat(
                                            c.Profesional.Usuario.Nombre,
                                            " ",
                                            c.Profesional.Usuario.Apellidos)
                                            .Trim()
                                        : string.Concat(
                                            c.Profesional.Nombres,
                                            " ",
                                            c.Profesional.Apellidos)
                                            .Trim()
                            },

                    c.IdServicio,

                    Servicio =
                        c.Servicio == null
                            ? null
                            : new
                            {
                                c.Servicio.Nombre
                            },

                    c.IdConsultorio,
                    c.IdEstado,
                    EstadoCatalogo = c.EstadoCita == null ? null : c.EstadoCita.NombreEstado,

                    c.FechaHora,

                    HoraInicio = c.FechaHora.TimeOfDay,

                    HoraFin =
                        c.FechaHora
                            .AddMinutes(DuracionCitaMinutos)
                            .TimeOfDay,

                    c.Estado,
                    c.Notas
                })
                .ToListAsync(ct);

            return Ok(new
            {
                success = true,
                data = citas,
                total = totalRecords,
                page,
                pageSize,
                duracionMinutos = DuracionCitaMinutos
            });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Solicitud cancelada ApiListarCitas.");

            return BadRequest(new
            {
                success = false,
                message = "La operación fue cancelada."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error en ApiListarCitas.");

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Error interno al listar citas."
                });
        }
    }

    // ================================================================
    // API: ACTUALIZAR CITA
    // ================================================================

    [HttpPut]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}")]
    public async Task<IActionResult> ApiActualizarCita(
        int id,
        [FromBody] CitaApiUpdateDto dto,
        CancellationToken ct = default)
    {
        if (dto == null ||
            id != dto.IdCita ||
            dto.IdPaciente <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Datos de cita inválidos."
            });
        }

        try
        {
            var cita = await _context.Citas
                .FirstOrDefaultAsync(c => c.IdCita == id, ct);

            if (cita == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Cita no encontrada."
                });
            }

            if (User.IsInRole("Paciente"))
            {
                string? claim =
                    User.FindFirstValue("IdPaciente");

                if (!int.TryParse(claim, out int idPaciente) ||
                    idPaciente != cita.IdPaciente)
                {
                    return Forbid();
                }
            }
            else if (User.IsInRole("Profesional"))
            {
                string? claim =
                    User.FindFirstValue("IdProfesional");

                if (!int.TryParse(claim, out int idProfesional) ||
                    cita.IdProfesional != idProfesional)
                {
                    return Forbid();
                }
            }
            else if (!User.IsInRole("Administrador") &&
                     !User.IsInRole("Recepcionista"))
            {
                return Forbid();
            }

            if (!await _context.Pacientes.AnyAsync(
                    p => p.IdPaciente == dto.IdPaciente &&
                         p.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El paciente seleccionado no es válido."
                });
            }

            if (dto.IdProfesional is <= 0 ||
                !await _context.Profesionales.AnyAsync(
                    p => p.IdProfesional == dto.IdProfesional &&
                         p.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El profesional seleccionado no es válido."
                });
            }

            if (dto.IdServicio is <= 0 ||
                !await _context.Servicios.AnyAsync(
                    s => s.IdServicio == dto.IdServicio &&
                         s.Estado == "activo",
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El servicio seleccionado no es válido."
                });
            }

            if (dto.IdConsultorio is <= 0 ||
                !await _context.Consultorios.AnyAsync(
                    c => c.IdConsultorio == dto.IdConsultorio &&
                         (c.Estado == "disponible" ||
                          c.Estado == "activo"),
                    ct))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El consultorio seleccionado no está disponible."
                });
            }

            DateTime inicio = dto.FechaHora;
            DateTime fin = inicio.AddMinutes(DuracionCitaMinutos);

            if (inicio < DateTime.Now.AddMinutes(-5))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No se puede mover la cita a un horario pasado."
                });
            }

            bool hayConflicto =
                await _context.Citas.AnyAsync(
                    c =>
                        c.IdCita != id &&
                        c.IdProfesional == dto.IdProfesional &&
                        c.Estado != "cancelada" &&
                        c.Estado != "Cancelada" &&
                        c.Estado != "cancelado" &&
                        c.FechaHora < fin &&
                        c.FechaHora.AddMinutes(DuracionCitaMinutos) > inicio,
                    ct);

            if (hayConflicto)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "El profesional ya tiene otra cita en ese horario."
                });
            }

            cita.IdPaciente = dto.IdPaciente;
            cita.IdProfesional = dto.IdProfesional;
            cita.IdServicio = dto.IdServicio;
            cita.IdConsultorio = dto.IdConsultorio;
            cita.FechaHora = inicio;

            if (dto.IdEstado is > 0)
            {
                var estadoApi = await _context.EstadosCita
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.IdEstado == dto.IdEstado.Value,
                        ct);

                if (estadoApi == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "El estado de la cita no es válido."
                    });
                }

                cita.IdEstado = estadoApi.IdEstado;
                cita.Estado = estadoApi.NombreEstado;
            }
            else if (!string.IsNullOrWhiteSpace(dto.Estado))
            {
                string estadoSolicitud = dto.Estado.Trim();

                var estadoApi = await _context.EstadosCita
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                             (estadoSolicitud.ToLower() == "agendada" &&
                              e.NombreEstado.ToLower() == "programada"),
                        ct);

                if (estadoApi == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "El estado de la cita no es válido."
                    });
                }

                cita.IdEstado = estadoApi.IdEstado;
                cita.Estado = estadoApi.NombreEstado;
            }

            cita.Notas = dto.Notas?.Trim();

            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"Cita actualizada mediante API. " +
                    $"IdPaciente={cita.IdPaciente}, " +
                    $"IdProfesional={cita.IdProfesional}, " +
                    $"FechaHora={cita.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"Estado\":\"{cita.Estado}\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"," +
                    $"\"IdPaciente\":{cita.IdPaciente}," +
                    $"\"IdProfesional\":{cita.IdProfesional}}}",
                ct: ct);

            return Ok(new
            {
                success = true,
                message = "Cita actualizada exitosamente.",
                id = cita.IdCita,
                idEstado = cita.IdEstado,
                estado = cita.Estado,
                duracionMinutos = DuracionCitaMinutos
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrencia ApiActualizarCita IdCita={Id}",
                id);

            return Conflict(new
            {
                success = false,
                message =
                    "La cita fue modificada por otro usuario."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException ApiActualizarCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "No se pudo actualizar la cita."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error ApiActualizarCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Error interno al actualizar la cita."
                });
        }
    }

    // ================================================================
    // API: EDITAR NOTAS DE CITA (PROFESIONAL)
    // ================================================================

    public sealed class CitaNotasDto
    {
        public int IdCita { get; set; }
        public string? Notas { get; set; }
    }

    [HttpPut]
    [Authorize(Roles = "Profesional")]
    [Route("api/citas/{id:int}/notas")]
    public async Task<IActionResult> ApiActualizarNotasCita(
        int id,
        [FromBody] CitaNotasDto dto,
        CancellationToken ct = default)
    {
        if (dto == null || id != dto.IdCita || id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Datos de notas inválidos."
            });
        }

        try
        {
            string? claim = User.FindFirstValue("IdProfesional");

            if (!int.TryParse(claim, out int idProfesional) ||
                idProfesional <= 0)
            {
                return Forbid();
            }

            var cita = await _context.Citas
                .FirstOrDefaultAsync(c => c.IdCita == id, ct);

            if (cita == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Cita no encontrada."
                });
            }

            if (cita.IdProfesional != idProfesional)
            {
                return Forbid();
            }

            string notasNuevas = (dto.Notas ?? string.Empty).Trim();

            if (notasNuevas.Length > 4000)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Las notas no pueden superar los 4000 caracteres."
                });
            }

            string notasAnteriores = cita.Notas ?? string.Empty;

            cita.Notas = string.IsNullOrWhiteSpace(notasNuevas)
                ? null
                : notasNuevas;

            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion: "Profesional actualizó las notas de la cita.",
                datosAnteriores:
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        cita.IdCita,
                        Notas = notasAnteriores
                    }),
                datosNuevos:
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        cita.IdCita,
                        Notas = cita.Notas ?? string.Empty
                    }),
                ct: ct);

            return Ok(new
            {
                success = true,
                message = "Notas actualizadas correctamente.",
                id = cita.IdCita,
                notas = cita.Notas ?? string.Empty
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrencia ApiActualizarNotasCita IdCita={Id}",
                id);

            return Conflict(new
            {
                success = false,
                message = "La cita fue modificada por otro usuario. Recarga la agenda e inténtalo nuevamente."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException ApiActualizarNotasCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "No se pudieron guardar las notas de la cita."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error ApiActualizarNotasCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Error interno al actualizar las notas de la cita."
                });
        }
    }
// ================================================================
// API: CAMBIAR ESTADO DE CITA (PROFESIONAL)
// ================================================================
//
// Flujo permitido para el profesional:
//
//   programada -> confirmada
//   programada -> cancelada
//   programada -> no_asistida
//
//   confirmada -> en_proceso
//   confirmada -> cancelada
//   confirmada -> no_asistida
//
//   en_proceso -> atendida
//
// Estados terminales:
//   atendida, cancelada, no_asistida
//
// El profesional nunca puede cambiar el paciente, profesional,
// servicio, consultorio o fecha desde este endpoint.
// Solo modifica el estado de su propia cita.
//

public sealed class CambiarEstadoCitaDto
{
    public string? Estado { get; set; }
}

[HttpPut]
[Authorize(Roles = "Profesional")]
[Route("api/citas/{id:int}/estado")]
public async Task<IActionResult> ApiActualizarEstadoCita(
    int id,
    [FromBody] CambiarEstadoCitaDto dto,
    CancellationToken ct = default)
{
    if (id <= 0 || dto == null ||
        string.IsNullOrWhiteSpace(dto.Estado))
    {
        return BadRequest(new
        {
            success = false,
            message = "El estado de la cita es obligatorio."
        });
    }

    try
    {
        // 1) Resolver el profesional autenticado.
        string? claimProfesional =
            User.FindFirstValue("IdProfesional");

        int idProfesional;

        if (!int.TryParse(
                claimProfesional,
                out idProfesional) ||
            idProfesional <= 0)
        {
            // Compatibilidad con sesiones antiguas:
            // resolver por IdUsuario autenticado.
            string? claimUsuario =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    claimUsuario,
                    out int idUsuario) ||
                idUsuario <= 0)
            {
                return Forbid();
            }

            int? profesionalResuelto =
                await _context.Profesionales
                    .AsNoTracking()
                    .Where(p =>
                        p.IdUsuario == idUsuario &&
                        p.Estado == "activo")
                    .Select(p =>
                        (int?)p.IdProfesional)
                    .FirstOrDefaultAsync(ct);

            if (!profesionalResuelto.HasValue ||
                profesionalResuelto.Value <= 0)
            {
                return Forbid();
            }

            idProfesional =
                profesionalResuelto.Value;
        }

        // 2) Buscar la cita.
        var cita =
            await _context.Citas
                .FirstOrDefaultAsync(
                    c => c.IdCita == id,
                    ct);

        if (cita == null)
        {
            return NotFound(new
            {
                success = false,
                message = "La cita no existe."
            });
        }

        // 3) Ownership: el profesional solo modifica sus propias citas.
        if (cita.IdProfesional != idProfesional)
        {
            return Forbid();
        }

        // 4) Normalizar estados.
        string estadoActual =
            NormalizarEstado(cita.Estado);

        string nuevoEstado =
            NormalizarEstado(dto.Estado);

        if (string.IsNullOrWhiteSpace(nuevoEstado))
        {
            return BadRequest(new
            {
                success = false,
                message = "El estado seleccionado no es válido."
            });
        }

        // 5) Evitar saltos arbitrarios del ciclo de vida.
        if (!EsTransicionEstadoPermitida(
                estadoActual,
                nuevoEstado))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    ConstruirMensajeTransicionNoPermitida(
                        estadoActual,
                        nuevoEstado),
                estadoActual,
                estadoSolicitado = nuevoEstado
            });
        }

        // 6) Resolver el estado contra el catálogo Estado_Cita.
        var estadosCatalogo =
            await _context.EstadosCita
                .AsNoTracking()
                .ToListAsync(ct);

        var estadoDestino =
            estadosCatalogo.FirstOrDefault(
                e =>
                    NormalizarEstado(
                        e.NombreEstado) ==
                    nuevoEstado);

        if (estadoDestino == null)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "El estado seleccionado no existe en el catálogo de estados de citas."
            });
        }

        string estadoAnteriorTexto =
            cita.Estado ?? string.Empty;

        int? idEstadoAnterior =
            cita.IdEstado;

        // 7) Sincronizar IdEstado + Estado textual.
        cita.IdEstado =
            estadoDestino.IdEstado;

        cita.Estado =
            estadoDestino.NombreEstado;

        await _context.SaveChangesAsync(ct);

        // 8) Auditoría.
        string datosAnteriores =
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    cita.IdCita,
                    IdEstado = idEstadoAnterior,
                    Estado = estadoAnteriorTexto
                });

        string datosNuevos =
            System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    cita.IdCita,
                    IdEstado = cita.IdEstado,
                    Estado = cita.Estado
                });

        await RegistrarAuditoriaAsync(
            accion: "UPDATE",
            tablaAfectada: "Cita",
            idRegistro: cita.IdCita,
            descripcion:
                $"Profesional cambió el estado de la cita de " +
                $"'{estadoAnteriorTexto}' a '{cita.Estado}'.",
            datosAnteriores:
                datosAnteriores,
            datosNuevos:
                datosNuevos,
            ct: ct);

        // 9) Notificar únicamente los cambios que corresponden.
        string estadoNotificacion =
            NormalizarEstado(cita.Estado);

        if (estadoNotificacion == "confirmada" ||
            estadoNotificacion == "cancelada")
        {
            await EnviarNotificacionCitaAsync(
                cita.IdCita,
                estadoNotificacion,
                ct);
        }

        _logger.LogInformation(
            "Estado de cita actualizado por profesional. " +
            "IdCita={IdCita}, IdProfesional={IdProfesional}, " +
            "EstadoAnterior={EstadoAnterior}, EstadoNuevo={EstadoNuevo}",
            cita.IdCita,
            idProfesional,
            estadoActual,
            nuevoEstado);

        return Ok(new
        {
            success = true,
            id = cita.IdCita,
            idEstado = cita.IdEstado,
            estado = cita.Estado,
            estadoAnterior = estadoAnteriorTexto,
            message =
                $"Cita actualizada a '{cita.Estado}' correctamente."
        });
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning(
            "Cambio de estado cancelado. IdCita={IdCita}",
            id);

        return BadRequest(new
        {
            success = false,
            message = "La operación fue cancelada."
        });
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.LogError(
            ex,
            "Concurrencia ApiActualizarEstadoCita IdCita={IdCita}",
            id);

        return Conflict(new
        {
            success = false,
            message =
                "La cita fue modificada por otro usuario. Recarga la agenda e inténtalo nuevamente."
        });
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(
            ex,
            "DbUpdateException ApiActualizarEstadoCita IdCita={IdCita}",
            id);

        return StatusCode(
            (int)HttpStatusCode.InternalServerError,
            new
            {
                success = false,
                message =
                    "No se pudo guardar el nuevo estado de la cita."
            });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error ApiActualizarEstadoCita IdCita={IdCita}",
            id);

        return StatusCode(
            (int)HttpStatusCode.InternalServerError,
            new
            {
                success = false,
                message =
                    "Error interno al actualizar el estado de la cita."
            });
    }
}

// API: CANCELAR CITA
    // ================================================================

    [HttpDelete]
    [Authorize(Policy = "ApiOrCookie")]
    [Route("api/citas/{id:int}")]
    public async Task<IActionResult> ApiEliminarCita(
        int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Identificador de cita inválido."
            });
        }

        try
        {
            var cita = await _context.Citas
                .FirstOrDefaultAsync(c => c.IdCita == id, ct);

            if (cita == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Cita no encontrada."
                });
            }

            if (User.IsInRole("Paciente"))
            {
                string? claim =
                    User.FindFirstValue("IdPaciente");

                if (!int.TryParse(claim, out int idPaciente) ||
                    idPaciente != cita.IdPaciente)
                {
                    return Forbid();
                }
            }
            else if (User.IsInRole("Profesional"))
            {
                return Forbid();
            }
            else if (!User.IsInRole("Administrador") &&
                     !User.IsInRole("Recepcionista"))
            {
                return Forbid();
            }

            if (EsEstadoCancelado(cita.Estado))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La cita ya está cancelada."
                });
            }

            var estadoCancelada = await _context.EstadosCita
                .FirstOrDefaultAsync(
                    e => e.NombreEstado.ToLower() == "cancelada",
                    ct);

            if (estadoCancelada != null)
            {
                cita.IdEstado = estadoCancelada.IdEstado;
                cita.Estado = estadoCancelada.NombreEstado;
            }
            else
            {
                cita.Estado = "Cancelada";
            }

            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"Cita cancelada mediante API. " +
                    $"IdPaciente={cita.IdPaciente}, " +
                    $"FechaHora={cita.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"Estado\":\"cancelada\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"}}",
                ct: ct);

            return Ok(new
            {
                success = true,
                message = "Cita cancelada exitosamente.",
                id = cita.IdCita
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrencia ApiEliminarCita IdCita={Id}",
                id);

            return Conflict(new
            {
                success = false,
                message =
                    "La cita fue modificada recientemente."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException ApiEliminarCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "No se pudo cancelar la cita."
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error ApiEliminarCita IdCita={Id}",
                id);

            return StatusCode(
                (int)HttpStatusCode.InternalServerError,
                new
                {
                    success = false,
                    message = "Error interno al cancelar la cita."
                });
        }
    }

    // ================================================================

    private bool EsViolacionIndiceUnico(DbUpdateException ex, out string indexName)
    {
        indexName = string.Empty;
        if (ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627)
        {
            if (sqlEx.Message.Contains("IX_Cita_IdConsultorio")) indexName = "IX_Cita_IdConsultorio";
            return true;
        }
        return false;
    }

        private async Task EnviarNotificacionCitaAsync(
        int idCita,
        string nuevoEstado,
        CancellationToken ct)
    {
        try
        {
            var cita =
                await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Profesional)
                    .Include(c => c.Servicio)
                    .FirstOrDefaultAsync(
                        c =>
                            c.IdCita ==
                            idCita,
                        ct);

            if (cita?.Paciente == null)
            {
                return;
            }

            string? correo =
                cita.Paciente.Correo;

            if (string.IsNullOrWhiteSpace(
                    correo))
            {
                return;
            }

            await _emailService.SendCitaNotificacionAsync(
                recipientEmail: correo,

                nombrePaciente:
                    cita.Paciente.NombresCompleto,

                fechaCita:
                    cita.FechaHora,

                profesional:
                    cita.Profesional?.NombreProfesional ??
                    "Tu profesional",

                servicio:
                    cita.Servicio?.Nombre ??
                    "Consulta",

                nuevoEstado:
                    nuevoEstado);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            /*
             * El envío de correo no debe deshacer
             * la operación principal de BD.
             */
            _logger.LogWarning(
                ex,
                "No se pudo enviar notificación de cita IdCita={IdCita}",
                idCita);
        }
    }

    // ================================================================
    // AUDITORÍA
    // ================================================================

    private async Task RegistrarAuditoriaAsync(
        string accion,
        string tablaAfectada,
        int? idRegistro,
        string descripcion,
        string? datosAnteriores = null,
        string? datosNuevos = null,
        CancellationToken ct = default)
    {
        try
        {
            string? userIdStr =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            int? idUsuario =
                int.TryParse(
                    userIdStr,
                    out int uid)
                    ? uid
                    : null;

            string ipOrigen =
                HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString()
                ??
                HttpContext.Request.Headers[
                    "X-Forwarded-For"]
                    .FirstOrDefault()
                ??
                "desconocida";

            _context.Auditorias.Add(
                new Auditoria
                {
                    Accion =
                        accion,

                    TablaAfectada =
                        tablaAfectada,

                    IdRegistro =
                        idRegistro,

                    Descripcion =
                        descripcion.Length > 255
                            ? descripcion[..255]
                            : descripcion,

                    DatosAnteriores =
                        datosAnteriores,

                    DatosNuevos =
                        datosNuevos,

                    IpOrigen =
                        ipOrigen,

                    IdUsuario =
                        idUsuario,

                    Fecha =
                        DateTime.Now
                });

            await _context.SaveChangesAsync(
                ct);
        }
        catch (Exception ex)
        {
            /*
             * Best effort:
             * la auditoría no debe romper la operación principal.
             */
            _logger.LogWarning(
                ex,
                "No se pudo registrar auditoría. " +
                "Accion={Accion}, Tabla={Tabla}, IdRegistro={Id}",
                accion,
                tablaAfectada,
                idRegistro);
        }
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private static string NormalizarEstado(
        string? estado)
    {
        var normalizado =
            (estado ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

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

    private static bool EsTransicionEstadoPermitida(
    string estadoActual,
    string nuevoEstado)
{
    return estadoActual switch
    {
        // Cita recién agendada:
        // todavía puede confirmarse, cancelarse o marcarse como no asistida.
        "programada" =>
            nuevoEstado is
                "confirmada" or
                "cancelada" or
                "no_asistida",

        // Confirmada:
        // puede pasar a consulta, cancelarse o marcarse como no asistida.
        "confirmada" =>
            nuevoEstado is
                "en_proceso" or
                "cancelada" or
                "no_asistida",

        // En consulta:
        // solo puede finalizar como atendida.
        "en_proceso" =>
            nuevoEstado == "atendida",

        // Estados terminales: no se pueden revertir desde la agenda.
        "atendida" or
        "finalizada" or
        "cancelada" or
        "no_asistida" =>
            false,

        _ => false
    };
}

private static string ConstruirMensajeTransicionNoPermitida(
    string estadoActual,
    string nuevoEstado)
{
    string estadoActualUi =
        EstadoCitaHelper.ResolveEstadoNombre(
            estadoActual,
            "programada");

    string nuevoEstadoUi =
        EstadoCitaHelper.ResolveEstadoNombre(
            nuevoEstado,
            nuevoEstado);

    return estadoActual switch
    {
        "programada" =>
            "Una cita agendada solo puede pasar a " +
            "Confirmada, Cancelada o No asistió.",

        "confirmada" =>
            "Una cita confirmada solo puede pasar a " +
            "En consulta, Cancelada o No asistió.",

        "en_proceso" =>
            "Una cita en consulta solo puede pasar a Atendida.",

        "atendida" or
        "finalizada" =>
            "Una cita atendida ya está finalizada y no puede regresar a otro estado.",

        "cancelada" =>
            "Una cita cancelada es definitiva y no puede reactivarse desde la agenda del profesional.",

        "no_asistida" =>
            "Una cita marcada como No asistió es definitiva y no puede reactivarse desde la agenda del profesional.",

        _ =>
            $"No está permitido cambiar una cita de '{estadoActualUi}' a '{nuevoEstadoUi}'."
    };
}

private static bool EsEstadoCancelado(
        string? estado)
    {
        return NormalizarEstado(estado) is
            "cancelada" or
            "cancelado";
    }


}
