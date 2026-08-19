/**
 * ============================================
 * SmileTrack — Controller: Gestión de Citas
 * ============================================
 * Autor: Johan Santamaria
 * Fecha: 2026-07-30
 *
 * PROPÓSITO:
 * Centraliza la lógica de negocio para todos los módulos
 * relacionados con citas: dashboards, agenda, gestión integral,
 * paneles auxiliares y vistas de profesionales/pacientes.
 *
 * REGLAS PRINCIPALES:
 * - Una cita tiene una duración fija de 60 minutos.
 * - FechaHora siempre se calcula a partir de Fecha + HoraInicio.
 * - HoraFin es un dato derivado y no debe utilizarse para alterar FechaHora.
 * - Los conflictos de agenda se validan considerando bloques de 60 minutos.
 * - Las operaciones administrativas de creación/cancelación requieren
 *   rol Administrador o Recepcionista.
 * - Las operaciones de paciente/profesional aplican ownership mediante claims.
 *
 * PATRONES APLICADOS:
 * - Constructor injection
 * - CancellationToken
 * - Validación de ModelState
 * - Try/catch estratificado
 * - TempData para mensajes amigables
 * - Auditoría de modificaciones
 * - Soft delete para cancelación
 * ============================================
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services.Email;
using System.Net;
using System.Security.Claims;

namespace SmileTrack_MVC.Controllers;

public class GestionCitasController(
    AppDbContext context,
    ILogger<GestionCitasController> logger,
    IEmailService emailService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionCitasController> _logger = logger;
    private readonly IEmailService _emailService = emailService;

    /*
     * REGLA DE NEGOCIO:
     * Todas las citas del sistema duran exactamente 60 minutos.
     *
     * IMPORTANTE:
     * Esta constante representa la fuente de verdad del backend.
     * El cliente no puede decidir arbitrariamente la duración.
     */
    private const int DuracionCitaMinutos = 60;

    private const string MensajeErrorFallback =
        "Ocurrió un error inesperado al cargar la página. " +
        "Por favor intente nuevamente. Si el problema persiste, contacte al soporte.";

    public sealed class CitaApiUpdateDto
    {
        public int IdCita { get; set; }

        public int IdPaciente { get; set; }

        public int? IdProfesional { get; set; }

        public int? IdServicio { get; set; }

        public int? IdConsultorio { get; set; }

        public int? IdEstado { get; set; }

        public DateTime FechaHora { get; set; }

        public string? Estado { get; set; }

        public string? Notas { get; set; }
    }

    // ================================================================
    // DASHBOARD ADMINISTRADOR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/st-adm-01-dashboard")]
    public async Task<IActionResult> Stadm01Dashboard(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-adm-01-dashboard",
                null,
                ct);

            await CargarDatosDashboard(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Solicitud cancelada Stadm01Dashboard para usuario {Usuario}",
                User.Identity?.Name ?? "anonimo");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos en Stadm01Dashboard");

            TempData["ErrorValidacion"] =
                "Error al consultar datos. Intente nuevamente.";

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml");
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Error SQL {Number} en Stadm01Dashboard",
                ex.Number);

            TempData["ErrorValidacion"] =
                "Servicio temporalmente no disponible. Intente en unos minutos.";

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error crítico cargando Stadm01Dashboard para usuario {Usuario}",
                User.Identity?.Name ?? "anonimo");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml");
        }
    }

    // ================================================================
    // AGENDA GENERAL
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/st-adm-08-agenda")]
    public async Task<IActionResult> Stadm08Agenda(
        [FromQuery] int? editId,
        [FromQuery] DateTime? weekStart,
        [FromQuery] int? professionalId,
        [FromQuery] int? officeId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-adm-08-agenda",
                null,
                ct);

            await CargarDatosAgenda(
                weekStart,
                professionalId,
                officeId,
                ct);

            ViewBag.Pacientes = await _context.Pacientes
                .AsNoTracking()
                .Where(p => p.Estado == "activo")
                .OrderBy(p => p.Apellidos)
                .ThenBy(p => p.Nombres)
                .Select(p => new
                {
                    p.IdPaciente,
                    DisplayName = $"{p.Apellidos}, {p.Nombres}"
                })
                .ToListAsync(ct);

            ViewBag.Profesionales = await _context.Profesionales
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Where(p => p.Estado == "activo")
                .Select(p => new
                {
                    p.IdProfesional,
                    DisplayName =
                        (p.Nombres + " " + p.Apellidos).Trim() != string.Empty
                            ? (p.Nombres + " " + p.Apellidos).Trim()
                            : (
                                p.Usuario != null
                                    ? (p.Usuario.Nombre + " " + p.Usuario.Apellidos).Trim()
                                    : "Sin Nombre"
                            )
                })
                .OrderBy(p => p.DisplayName)
                .ToListAsync(ct);

            ViewBag.Consultorios = await _context.Consultorios
                .AsNoTracking()
                .Where(c => c.Estado == "disponible" || c.Estado == "activo")
                .Select(c => new
                {
                    c.IdConsultorio,
                    c.Nombre
                })
                .OrderBy(c => c.Nombre)
                .ToListAsync(ct);

            ViewBag.Servicios = await _context.Servicios
                .AsNoTracking()
                .Where(s => s.Estado == "activo")
                .Select(s => new
                {
                    s.IdServicio,
                    s.Nombre
                })
                .OrderBy(s => s.Nombre)
                .ToListAsync(ct);

            ViewData["WeekStart"] =
                (weekStart?.Date ?? DateTime.Today)
                .ToString("yyyy-MM-dd");

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Solicitud cancelada Stadm08Agenda");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos en Stadm08Agenda");

            TempData["ErrorValidacion"] =
                "Error al consultar datos. Intente nuevamente.";

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml");
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Error SQL {Number} en Stadm08Agenda",
                ex.Number);

            TempData["ErrorValidacion"] =
                "Servicio temporalmente no disponible. Intente en unos minutos.";

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error crítico cargando Stadm08Agenda");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml");
        }
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

                if (!int.TryParse(claim, out int idProfesional) ||
                    idProfesional <= 0)
                {
                    return Forbid();
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
    // GESTIÓN INTEGRAL DE CITAS
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/st-adm-09-citas")]
    public async Task<IActionResult> Stadm09Citas(
        [FromQuery] int? editId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        [FromQuery] string? profesional = null,
        [FromQuery] string? fecha = null,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-adm-09-citas",
                new PaginationQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    Search = search,
                    Estado = estado,
                    Profesional = profesional,
                    Fecha = fecha
                },
                ct);

            await CargarKpisGestionCitas(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cargando Stadm09Citas.");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-09-citas/index.cshtml");
        }
    }

    // ================================================================
    // GUARDAR / ACTUALIZAR CITA DESDE FORMULARIO MVC
    // ================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/guardar-cita")]
    public async Task<IActionResult> GuardarCita(
        [FromForm] CitaViewModel model,
        CancellationToken ct = default)
    {
        string returnUrlSafe =
            !string.IsNullOrWhiteSpace(model?.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : "/gestion-de-citas/st-adm-09-citas";

        int idCitaOperacion =
            model?.IdCita ?? 0;

        string operacion =
            idCitaOperacion > 0
                ? "Actualizacion"
                : "Creacion";

        if (model == null)
        {
            TempData["ErrorValidacion"] =
                "Los datos de la cita son obligatorios.";

            return Redirect(returnUrlSafe);
        }

        if (!ModelState.IsValid)
        {
            string mensaje =
                ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
                ?? "Datos inválidos en el formulario.";

            _logger.LogWarning(
                "GuardarCita: ModelState inválido. Operacion={Operacion}, Error={Error}",
                operacion,
                mensaje);

            TempData["ErrorValidacion"] =
                mensaje;

            return Redirect(returnUrlSafe);
        }

        try
        {
            if (!model.HoraInicio.HasValue)
            {
                TempData["ErrorValidacion"] =
                    "La hora de inicio es obligatoria.";

                return Redirect(returnUrlSafe);
            }

            /*
             * FUENTE DE VERDAD:
             * FechaHora siempre se calcula aquí.
             */
            model.FechaHora =
                model.Fecha.Date.Add(
                    model.HoraInicio.Value);

            if (model.FechaHora <
                DateTime.Now.AddMinutes(-5))
            {
                TempData["ErrorValidacion"] =
                    "La fecha y hora de la cita no son válidas o corresponden a un horario pasado.";

                return Redirect(returnUrlSafe);
            }
            // ------------------------------------------------------------
            // PACIENTE
            // ------------------------------------------------------------

            bool pacienteExiste =
                await _context.Pacientes.AnyAsync(
                    p =>
                        p.IdPaciente == model.IdPaciente &&
                        p.Estado == "activo",
                    ct);

            if (!pacienteExiste)
            {
                TempData["ErrorValidacion"] =
                    "El paciente seleccionado no existe o se encuentra inactivo.";

                return Redirect(returnUrlSafe);
            }

            // ------------------------------------------------------------
            // PROFESIONAL
            // ------------------------------------------------------------

            if (model.IdProfesional <= 0 ||
                !await _context.Profesionales.AnyAsync(
                    p =>
                        p.IdProfesional ==
                        model.IdProfesional &&
                        p.Estado == "activo",
                    ct))
            {
                TempData["ErrorValidacion"] =
                    "El profesional seleccionado no existe o se encuentra inactivo.";

                return Redirect(returnUrlSafe);
            }

            // ------------------------------------------------------------
            // SERVICIO
            // ------------------------------------------------------------

            if (model.IdServicio <= 0 ||
                !await _context.Servicios.AnyAsync(
                    s =>
                        s.IdServicio ==
                        model.IdServicio &&
                        s.Estado == "activo",
                    ct))
            {
                TempData["ErrorValidacion"] =
                    "El servicio seleccionado no existe o se encuentra inactivo.";

                return Redirect(returnUrlSafe);
            }

            // ------------------------------------------------------------
            // CONSULTORIO
            // ------------------------------------------------------------

            if (model.IdConsultorio <= 0 ||
                !await _context.Consultorios.AnyAsync(
                    c =>
                        c.IdConsultorio ==
                        model.IdConsultorio &&
                        (
                            c.Estado == "disponible" ||
                            c.Estado == "activo"
                        ),
                    ct))
            {
                TempData["ErrorValidacion"] =
                    "El consultorio seleccionado no existe o no está disponible.";

                return Redirect(returnUrlSafe);
            }

            // ------------------------------------------------------------
            // ESTADO
            // ------------------------------------------------------------

            if (model.IdEstado <= 0 ||
                !await _context.EstadosCita.AnyAsync(
                    e =>
                        e.IdEstado ==
                        model.IdEstado,
                    ct))
            {
                TempData["ErrorValidacion"] =
                    "El estado de la cita seleccionado no es válido.";

                return Redirect(returnUrlSafe);
            }

            // ------------------------------------------------------------
            // BLOQUE DE 60 MINUTOS
            // ------------------------------------------------------------

            DateTime inicio = model.FechaHora;

            DateTime fin =
                inicio.AddMinutes(
                    DuracionCitaMinutos);

            // ------------------------------------------------------------
            // CREACIÓN / ACTUALIZACIÓN
            // ------------------------------------------------------------

            Cita? cita;

            if (model.IdCita > 0)
            {
                cita = await _context.Citas
                    .FirstOrDefaultAsync(
                        c => c.IdCita == model.IdCita.Value,
                        ct);

                if (cita == null)
                {
                    TempData["ErrorValidacion"] =
                        "La cita que intenta actualizar no existe.";

                    _logger.LogWarning(
                        "GuardarCita: Intento actualizar cita inexistente IdCita={IdCita}",
                        model.IdCita.Value);

                    return Redirect(returnUrlSafe);
                }

                bool hayConflicto =
                    await _context.Citas.AnyAsync(
                        c =>
                            c.IdCita != model.IdCita.Value &&
                            c.IdProfesional == model.IdProfesional &&
                            c.Estado != "cancelada" &&
                            c.Estado != "Cancelada" &&
                            c.Estado != "cancelado" &&
                            c.FechaHora < fin &&
                            c.FechaHora.AddMinutes(DuracionCitaMinutos) > inicio,
                        ct);

                if (hayConflicto)
                {
                    TempData["ErrorValidacion"] =
                        "El profesional ya tiene una cita asignada en ese horario.";

                    return Redirect(returnUrlSafe);
                }

                cita.IdPaciente =
                    model.IdPaciente;

                cita.IdProfesional =
                    model.IdProfesional;

                cita.IdServicio =
                    model.IdServicio;

                cita.IdConsultorio =
                    model.IdConsultorio;

                cita.IdEstado =
                    model.IdEstado;

                cita.FechaHora =
                    model.FechaHora;

                cita.Estado =
                    await BuildEstadoNombreAsync(
                        model.IdEstado,
                        model.Estado,
                        cita.Estado,
                        ct);

                cita.Estado =
                    EstadoCitaHelper.ResolveEstadoNombre(
                        cita.Estado,
                        "programada");

                cita.Notas =
                    BuildNotasCita(model);

                _context.Citas.Update(cita);
            }
            else
            {
                bool hayConflicto =
                    await _context.Citas.AnyAsync(
                        c =>
                            c.IdProfesional ==
                            model.IdProfesional &&
                            c.Estado != "cancelada" &&
                            c.Estado != "Cancelada" &&
                            c.Estado != "cancelado" &&
                            c.FechaHora < fin &&
                            c.FechaHora.AddMinutes(
                                DuracionCitaMinutos) > inicio,
                        ct);

                if (hayConflicto)
                {
                    TempData["ErrorValidacion"] =
                        "El profesional ya tiene una cita asignada en ese horario.";

                    return Redirect(returnUrlSafe);
                }

                cita = new Cita
                {
                    IdPaciente =
                        model.IdPaciente,

                    IdProfesional =
                        model.IdProfesional,

                    IdServicio =
                        model.IdServicio,

                    IdConsultorio =
                        model.IdConsultorio,

                    IdEstado =
                        model.IdEstado,

                    FechaHora =
                        model.FechaHora,

                    Estado =
                        EstadoCitaHelper.ResolveEstadoNombre(
                            await BuildEstadoNombreAsync(
                                model.IdEstado,
                                model.Estado,
                                "programada",
                                ct),
                            "programada"),

                    Notas =
                        BuildNotasCita(model)
                };

                _context.Citas.Add(cita);
            }

            // ------------------------------------------------------------
            // GUARDAR EN BD
            // ------------------------------------------------------------

            int guardados = await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "GuardarCita ejecutado correctamente. Operacion={Operacion}, IdCita={IdCita}, FechaHora={FechaHora}, IdPaciente={IdPaciente}, IdProfesional={IdProfesional}, IdServicio={IdServicio}, IdConsultorio={IdConsultorio}, IdEstado={IdEstado}, Estado={Estado}, RegistrosAfectados={RegistrosAfectados}",
                operacion,
                cita.IdCita,
                cita.FechaHora,
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio,
                cita.IdEstado,
                cita.Estado,
                guardados);

            if (guardados <= 0)
            {
                TempData["ErrorValidacion"] =
                    "No se pudieron guardar los cambios.";

                return Redirect(returnUrlSafe);
            }


            // ------------------------------------------------------------
            // AUDITORÍA
            // ------------------------------------------------------------

            await RegistrarAuditoriaAsync(
                accion:
                    operacion == "Creacion"
                        ? "INSERT"
                        : "UPDATE",

                tablaAfectada:
                    "Cita",

                idRegistro:
                    cita.IdCita,

                descripcion:
                    operacion == "Creacion"
                        ? $"Cita creada. IdPaciente={cita.IdPaciente}, " +
                          $"IdProfesional={cita.IdProfesional}, " +
                          $"Fecha={cita.FechaHora:yyyy-MM-dd HH:mm}"
                        : $"Cita actualizada. IdCita={cita.IdCita}, " +
                          $"IdPaciente={cita.IdPaciente}, " +
                          $"IdProfesional={cita.IdProfesional}, " +
                          $"Fecha={cita.FechaHora:yyyy-MM-dd HH:mm}",

                datosNuevos:
                    $"{{\"IdPaciente\":{cita.IdPaciente}," +
                    $"\"IdProfesional\":{cita.IdProfesional}," +
                    $"\"IdServicio\":{cita.IdServicio}," +
                    $"\"IdConsultorio\":{cita.IdConsultorio}," +
                    $"\"IdEstado\":{cita.IdEstado}," +
                    $"\"Estado\":\"{cita.Estado}\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"}}",

                ct: ct);

            // ------------------------------------------------------------
            // NOTIFICACIÓN POR EMAIL
            // ------------------------------------------------------------

            string estadoNormalizado =
                NormalizarEstado(cita.Estado);

            if (estadoNormalizado == "confirmada" ||
                estadoNormalizado == "cancelada")
            {
                await EnviarNotificacionCitaAsync(
                    cita.IdCita,
                    estadoNormalizado,
                    ct);
            }

            _logger.LogInformation(
                "{Operacion} de cita correcta: IdCita={IdCita}, Usuario={Usuario}",
                operacion,
                cita.IdCita,
                User.Identity?.Name ?? "anonimo");

            TempData["MensajeExito"] =
                operacion == "Creacion"
                    ? "La cita se ha agendado correctamente."
                    : "La cita se ha actualizado correctamente.";
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación cancelada al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "La operación fue cancelada antes de finalizar.";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Conflicto de concurrencia al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "Conflicto de datos: otro usuario modificó esta cita.";
        }
        catch (DbUpdateException ex) when (
            EsViolacionIndiceUnico(
                ex,
                out string? indice))
        {
            _logger.LogError(
                ex,
                "Violación UNIQUE {Indice} al guardar cita Id={Id}",
                indice,
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "No se pudo guardar: ya existe una cita con estas características.";
        }
        catch (DbUpdateException ex) when (
            EsViolacionIntegridadReferencial(ex))
        {
            _logger.LogError(
                ex,
                "Violación de integridad referencial al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "No se pudo guardar porque uno de los datos relacionados no es válido.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "Ocurrió un error al guardar la cita en la base de datos.";
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException al guardar cita Id={Id}. Number={Number}",
                idCitaOperacion,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Excepción inesperada al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    // ================================================================
    // CANCELAR CITA MVC
    // ================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/eliminar-cita")]
    public async Task<IActionResult> EliminarCita(
        [FromForm] int IdCita,
        [FromForm] string? ReturnUrl,
        CancellationToken ct = default)
    {
        string returnUrlSafe =
            !string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl)
                ? ReturnUrl
                : "/gestion-de-citas/st-adm-09-citas";

        try
        {
            if (IdCita <= 0)
            {
                TempData["ErrorValidacion"] =
                    "Identificador de cita inválido.";

                return Redirect(returnUrlSafe);
            }

            var cita = await _context.Citas
                .FirstOrDefaultAsync(
                    c => c.IdCita == IdCita,
                    ct);

            if (cita == null)
            {
                TempData["ErrorValidacion"] =
                    "La cita que intenta cancelar no existe.";

                return Redirect(returnUrlSafe);
            }

            if (EsEstadoCancelado(cita.Estado))
            {
                TempData["ErrorValidacion"] =
                    "La cita ya se encuentra cancelada.";

                return Redirect(returnUrlSafe);
            }

            /*
             * SOFT DELETE:
             * No eliminamos físicamente el registro.
             */
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

            _context.Citas.Update(cita);

            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"Cita cancelada. IdPaciente={cita.IdPaciente}, " +
                    $"FechaHora={cita.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"Estado\":\"cancelada\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"}}",
                ct: ct);

            await EnviarNotificacionCitaAsync(
                cita.IdCita,
                "cancelada",
                ct);

            TempData["MensajeExito"] =
                "La cita fue cancelada exitosamente.";
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación cancelada EliminarCita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "La operación fue cancelada.";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrencia al cancelar cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "La cita fue modificada recientemente.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException al cancelar cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "Ocurrió un error al intentar cancelar la cita.";
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException al cancelar cita Id={Id}. Number={Number}",
                IdCita,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Error inesperado al cancelar cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    // ================================================================
    // PANEL AUXILIAR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-citas/st-aux-01-panel-operativo/panel-operativo")]
    public async Task<IActionResult> Staux01PanelOperativo(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo",
                null,
                ct);

            ViewData["PanelOperativoData"] =
                await ConstruirPanelOperativoAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux01PanelOperativo");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-01-panel-operativo/panel-operativo.cshtml");
        }
    }

    private async Task<object> ConstruirPanelOperativoAsync(
        CancellationToken ct)
    {
        var hoy = DateTime.Now.Date;

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora.Date == hoy)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        string MapEstadoLabel(string estado) =>
            NormalizarEstado(estado) switch
            {
                "atendida" or
                "completada" or
                "realizada"
                    => "Atendida",

                "cancelada" or
                "no_asistida" or
                "no-show"
                    => "Cancelada",

                _ => "Pendiente"
            };

        var pacientesHoy = citasHoy
            .Select(c => new
            {
                id = c.IdCita,
                hora = c.FechaHora.ToString("HH:mm"),
                paciente = c.Paciente?.NombresCompleto ?? "Paciente sin datos",
                profesional =
                    c.Profesional is not null
                        ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                        : "Sin asignar",

                alergia =
                    string.IsNullOrWhiteSpace(c.Paciente?.Alergias)
                        ? null
                        : c.Paciente!.Alergias,

                consultorio =
                    c.Consultorio?.Nombre ?? "Sin asignar",

                estado = MapEstadoLabel(c.Estado),

                highlight =
                    !string.IsNullOrWhiteSpace(
                        c.Paciente?.Alergias),

                telefono = c.Paciente?.Telefono,
                email = c.Paciente?.Correo,

                sangre =
                    string.IsNullOrWhiteSpace(
                        c.Paciente?.GrupoSanguineo)
                        ? "N/D"
                        : c.Paciente!.GrupoSanguineo,

                edad =
                    c.Paciente?.FechaNacimiento is not null
                        ? $"{CalcularEdad(c.Paciente.FechaNacimiento)} años"
                        : "Edad no registrada",

                medicamentos = Array.Empty<string>(),

                antecedentes =
                    string.IsNullOrWhiteSpace(
                        c.Paciente?.AntecedentesMedicos)
                        ? "Sin antecedentes registrados"
                        : c.Paciente!.AntecedentesMedicos,

                servicio =
                    c.Servicio?.Nombre ??
                    "Servicio no especificado"
            })
            .ToList();

        int completadas =
            citasHoy.Count(
                c => MapEstadoLabel(c.Estado) ==
                     "Atendida");

        int canceladas =
            citasHoy.Count(
                c => MapEstadoLabel(c.Estado) ==
                     "Cancelada");

        int pendientes =
            citasHoy.Count -
            completadas -
            canceladas;

        int consultoriosDisponibles =
            await _context.Consultorios.CountAsync(
                c =>
                    c.Estado == "disponible" ||
                    c.Estado == "activo",
                ct);

        var proxima = citasHoy
            .FirstOrDefault(
                c =>
                    c.FechaHora > DateTime.Now &&
                    MapEstadoLabel(c.Estado) == "Pendiente");

        object? proximaCita =
            proxima == null
                ? null
                : new
                {
                    minutosRestantes =
                        Math.Max(
                            0,
                            (int)(
                                proxima.FechaHora -
                                DateTime.Now)
                                .TotalMinutes),

                    hora =
                        proxima.FechaHora.ToString(
                            "hh:mm tt"),

                    paciente =
                        proxima.Paciente?.NombresCompleto ??
                        "Paciente sin datos",

                    tipo =
                        proxima.Servicio?.Nombre ??
                        "Consulta",

                    profesional =
                        proxima.Profesional is not null
                            ? $"Dr(a). {proxima.Profesional.Nombres} {proxima.Profesional.Apellidos}"
                            : "Sin asignar",

                    consultorio =
                        proxima.Consultorio?.Nombre ??
                        "Sin asignar"
                };

        var alertas = new List<object>();

        foreach (var c in citasHoy.Where(
                     c =>
                         !string.IsNullOrWhiteSpace(
                             c.Paciente?.Alergias)))
        {
            alertas.Add(
                new
                {
                    tipo = "warning",
                    titulo = "Paciente con alergia",
                    desc =
                        $"{c.Paciente!.NombresCompleto} — " +
                        $"Alérgico a {c.Paciente.Alergias}"
                });
        }

        return new
        {
            fechaHoy =
                hoy.ToString(
                    "dddd d 'de' MMMM yyyy",
                    new System.Globalization.CultureInfo("es-CO")),

            kpis = new
            {
                citasHoy = citasHoy.Count,
                completadas,
                pendientes,
                consultoriosDisponibles
            },

            proximaCita,
            citas = pacientesHoy,
            alertas
        };
    }

    // ================================================================
    // AGENDA APOYO
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-citas/st-aux-02-agenda-apoyo")]
    public async Task<IActionResult> Staux02AgendaApoyo(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-02-agenda-apoyo",
                null,
                ct);

            ViewData["AgendaApoyoData"] =
                await ConstruirAgendaApoyoAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux02AgendaApoyo");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-02-agenda-apoyo/agenda-apoyo.cshtml");
        }
    }

    private async Task<object> ConstruirAgendaApoyoAsync(
        CancellationToken ct)
    {
        var hoy = DateTime.Now.Date;

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(c => c.FechaHora.Date == hoy)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        static string InferirTipoCita(
            string? nombreServicio)
        {
            var n =
                (nombreServicio ?? string.Empty)
                .ToLowerInvariant();

            if (n.Contains("urgencia") ||
                n.Contains("emergencia") ||
                n.Contains("dolor"))
            {
                return "urgencia";
            }

            if (n.Contains("limpieza") ||
                n.Contains("valoraci") ||
                n.Contains("control") ||
                n.Contains("revisi"))
            {
                return "consulta";
            }

            return "procedimiento";
        }

        string MapEstadoLabel(string estado) =>
            NormalizarEstado(estado) switch
            {
                "atendida" or
                "completada" or
                "realizada"
                    => "Atendida",

                "cancelada" or
                "no_asistida" or
                "no-show"
                    => "Cancelada",

                _ => "Pendiente"
            };

        var citas = citasHoy.Select(
            c => new
            {
                id = c.IdCita,
                hora = c.FechaHora.ToString("HH:mm"),
                paciente =
                    c.Paciente?.NombresCompleto ??
                    "Paciente sin datos",

                profesional =
                    c.Profesional is not null
                        ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                        : "Sin asignar",

                tipo =
                    InferirTipoCita(
                        c.Servicio?.Nombre),

                alergia =
                    string.IsNullOrWhiteSpace(
                        c.Paciente?.Alergias)
                        ? null
                        : c.Paciente!.Alergias,

                estado =
                    MapEstadoLabel(c.Estado)
            })
            .ToList();

        return new
        {
            fechaHoy =
                hoy.ToString(
                    "ddd d MMM yyyy",
                    new System.Globalization.CultureInfo("es-CO")),

            citas
        };
    }

    // ================================================================
    // HISTORIAL PARCIAL AUXILIAR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-citas/st-aux-05-historial-parcial")]
    public async Task<IActionResult> Staux05HistorialParcial(
        [FromQuery] int? editId,
        [FromQuery] int? pacienteId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-05-historial-parcial",
                null,
                ct);

            ViewData["HistorialParcialData"] =
                await ConstruirHistorialParcialAsync(
                    pacienteId,
                    ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux05HistorialParcial");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-05-historial-parcial/historial-parcial.cshtml");
        }
    }

    private async Task<object> ConstruirHistorialParcialAsync(
        int? pacienteId,
        CancellationToken ct)
    {
        const int limite = 3;

        var paciente =
            pacienteId is not null
                ? await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p => p.IdPaciente == pacienteId,
                        ct)
                : await _context.Citas
                    .OrderByDescending(c => c.FechaHora)
                    .Select(c => c.Paciente)
                    .FirstOrDefaultAsync(ct);

        if (paciente is null)
        {
            return new
            {
                paciente = new
                {
                    id = (int?)null,
                    nombre = "Sin paciente asignado",
                    tipoDoc = "",
                    documento = "",
                    alergias = Array.Empty<string>(),
                    medicamentos = Array.Empty<string>(),
                    grupoSanguineo = "N/D",
                    ultimaActualizacion = ""
                },

                consultas = Array.Empty<object>(),
                limite
            };
        }

        var consultas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(
                c =>
                    c.IdPaciente == paciente.IdPaciente &&
                    c.FechaHora <= DateTime.Now)
            .OrderByDescending(c => c.FechaHora)
            .Take(limite)
            .Select(
                c => new
                {
                    id = c.IdCita,
                    fecha = c.FechaHora,

                    profesional =
                        c.Profesional != null
                            ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                            : "Sin asignar",

                    diagnostico =
                        c.Notas ?? "",

                    procedimiento =
                        c.Servicio != null
                            ? c.Servicio.Nombre
                            : "Consulta"
                })
            .ToListAsync(ct);

        return new
        {
            paciente = new
            {
                id = paciente.IdPaciente,
                nombre = paciente.NombresCompleto,
                tipoDoc = paciente.TipoDocumento,
                documento = paciente.Documento,

                alergias =
                    string.IsNullOrWhiteSpace(
                        paciente.Alergias)
                        ? Array.Empty<string>()
                        : paciente.Alergias.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries),

                medicamentos = Array.Empty<string>(),

                grupoSanguineo =
                    string.IsNullOrWhiteSpace(
                        paciente.GrupoSanguineo)
                        ? "N/D"
                        : paciente.GrupoSanguineo,

                ultimaActualizacion =
                    consultas.Count > 0
                        ? consultas[0].fecha.ToString(
                            "dd MMM",
                            new System.Globalization.CultureInfo("es-CO"))
                        : ""
            },

            consultas,
            limite
        };
    }

    // ================================================================
    // ASISTENCIA PROCEDIMIENTO
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-citas/st-aux-06-asistencia-procedi")]
    public async Task<IActionResult> Staux06AsistenciaProcedi(
        [FromQuery] int? editId,
        [FromQuery] int? citaId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-06-asistencia-procedi",
                null,
                ct);

            ViewData["AsistenciaProcedData"] =
                await ConstruirAsistenciaProcedimientoAsync(
                    citaId,
                    ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux06AsistenciaProcedi");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-06-asistencia-procedi/asistencia-proc.cshtml");
        }
    }

    private async Task<object> ConstruirAsistenciaProcedimientoAsync(
        int? citaId,
        CancellationToken ct)
    {
        var cita =
            citaId is not null
                ? await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Servicio)
                    .Include(c => c.Profesional)
                    .Include(c => c.Consultorio)
                    .FirstOrDefaultAsync(
                        c => c.IdCita == citaId,
                        ct)
                : await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Servicio)
                    .Include(c => c.Profesional)
                    .Include(c => c.Consultorio)
                    .Where(
                        c =>
                            c.FechaHora.Date ==
                            DateTime.Now.Date)
                    .OrderBy(c => c.FechaHora)
                    .FirstOrDefaultAsync(ct);

        if (cita is null)
        {
            return new
            {
                citaId = (int?)null,
                paciente = "Sin procedimiento asignado",
                procedimiento = "",
                profesional = "",
                consultorio = "",
                alergia = (string?)null,
                antecedentes = (string?)null
            };
        }

        return new
        {
            citaId = cita.IdCita,

            paciente =
                cita.Paciente?.NombresCompleto ??
                "Paciente sin datos",

            procedimiento =
                cita.Servicio?.Nombre ??
                "Procedimiento",

            profesional =
                cita.Profesional is not null
                    ? $"Dr(a). {cita.Profesional.Nombres} {cita.Profesional.Apellidos}"
                    : "Sin asignar",

            consultorio =
                cita.Consultorio?.Nombre ??
                "Sin asignar",

            alergia =
                string.IsNullOrWhiteSpace(
                    cita.Paciente?.Alergias)
                    ? null
                    : cita.Paciente!.Alergias,

            antecedentes =
                string.IsNullOrWhiteSpace(
                    cita.Paciente?.AntecedentesMedicos)
                    ? null
                    : cita.Paciente!.AntecedentesMedicos
        };
    }

    // ================================================================
    // ESTADO CONSULTORIO
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-citas/st-aux-09-estado-consultorio")]
    public async Task<IActionResult> Staux09EstadoConsultorio(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-09-estado-consultorio",
                null,
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux09EstadoConsultorio");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml");
        }
    }

    // ================================================================
    // CITAS FINALIZADAS
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Recepcionista")]
    [Route("gestion-de-citas/st-aux-10-citas-finalizadas")]
    public async Task<IActionResult> Staux10CitasFinalizadas(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-10-citas-finalizadas",
                null,
                ct);

            ViewData["CitasFinalizadasData"] =
                await ConstruirCitasFinalizadasAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux10CitasFinalizadas");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-10-citas-finalizadas/citas-finalizadas.cshtml");
        }
    }

    private async Task<object> ConstruirCitasFinalizadasAsync(
        CancellationToken ct)
    {
        var hoy = DateTime.Now.Date;

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(c => c.FechaHora.Date == hoy)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        static string MapEstadoLabel(string estado) =>
            NormalizarEstado(estado) switch
            {
                "atendida" or
                "completada" or
                "realizada"
                    => "Atendida",

                "cancelada"
                    => "Cancelada",

                "no_asistida" or
                "no-show"
                    => "No asistió",

                _ => "Pendiente"
            };

        var citas =
            citasHoy
                .Select(
                    c => new
                    {
                        id = c.IdCita,
                        hora =
                            c.FechaHora.ToString("HH:mm"),

                        paciente =
                            c.Paciente?.NombresCompleto ??
                            "Paciente sin datos",

                        profesional =
                            c.Profesional is not null
                                ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                : "Sin asignar",

                        servicio =
                            c.Servicio?.Nombre ??
                            "Servicio no especificado",

                        estado =
                            MapEstadoLabel(c.Estado)
                    })
                .Where(c => c.estado != "Pendiente")
                .ToList();

        return new
        {
            citas
        };
    }

    // ================================================================
    // AGENDA PROFESIONAL
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("gestion-de-citas/st-odo-02-agenda")]
    public async Task<IActionResult> Stodo02Agenda(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-odo-02-agenda",
                null,
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Stodo02Agenda");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml");
        }
    }

    // ================================================================
    // MIS CITAS PACIENTE
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Paciente")]
    [Route("gestion-de-citas/st-pac-01-mis-citas")]
    public async Task<IActionResult> Stpac01MisCitas(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-pac-01-mis-citas",
                null,
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Stpac01MisCitas");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-pac-01-mis-citas/index.cshtml");
        }
    }

    // ================================================================
    // NOTIFICACIONES PACIENTE
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Paciente")]
    [Route("gestion-de-citas/st-pac-03-notificaciones")]
    public async Task<IActionResult> Stpac03Notificaciones(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-pac-03-notificaciones",
                null,
                ct);

            ViewData["NotificacionesData"] =
                await ConstruirNotificacionesPacienteAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Stpac03Notificaciones");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-pac-03-notificaciones/index.cshtml");
        }
    }

    private async Task<object> ConstruirNotificacionesPacienteAsync(
        CancellationToken ct)
    {
        string? userIdStr =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        int? idUsuario =
            int.TryParse(
                userIdStr,
                out int uid)
                ? uid
                : null;

        var paciente =
            idUsuario is not null
                ? await _context.Pacientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        p => p.IdUsuario == idUsuario,
                        ct)
                : null;

        if (paciente == null)
        {
            return new
            {
                notificaciones =
                    Array.Empty<object>()
            };
        }

        var citas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(
                c =>
                    c.IdPaciente ==
                    paciente.IdPaciente)
            .OrderByDescending(c => c.FechaHora)
            .Take(20)
            .ToListAsync(ct);

        var ahora = DateTime.Now;

        var notificaciones =
            new List<(object Notif, DateTime Time)>();

        int idx = 1;

        foreach (var c in citas)
        {
            string profesional =
                c.Profesional is not null
                    ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                    : "tu profesional asignado";

            string estado =
                NormalizarEstado(c.Estado);

            if (c.FechaHora > ahora &&
                (estado == "programada" ||
                 estado == "confirmada"))
            {
                notificaciones.Add(
                    (
                        new
                        {
                            id = idx++,
                            tipo = "reminder",
                            titulo = "Recordatorio de cita",

                            desc =
                                $"Tu cita con {profesional} es el " +
                                $"{c.FechaHora:dd 'de' MMMM} " +
                                $"a las {c.FechaHora:hh:mm tt}" +
                                $"{(
                                    c.Consultorio != null
                                        ? " - " + c.Consultorio.Nombre
                                        : ""
                                )}",

                            time = c.FechaHora,
                            leida = false,
                            badge = "pending"
                        },

                        c.FechaHora
                    ));
            }
            else if (estado == "confirmada")
            {
                notificaciones.Add(
                    (
                        new
                        {
                            id = idx++,
                            tipo = "confirmed",
                            titulo = "Cita confirmada",

                            desc =
                                $"Tu cita del {c.FechaHora:dd 'de' MMMM} " +
                                "fue confirmada exitosamente.",

                            time = c.FechaHora,
                            leida = false,
                            badge = "new"
                        },

                        c.FechaHora
                    ));
            }
            else if (estado == "cancelada")
            {
                notificaciones.Add(
                    (
                        new
                        {
                            id = idx++,
                            tipo = "cancelled",
                            titulo = "Cita cancelada",

                            desc =
                                $"Tu cita del {c.FechaHora:dd 'de' MMMM} " +
                                "fue cancelada.",

                            time = c.FechaHora,
                            leida = false,
                            badge = "read"
                        },

                        c.FechaHora
                    ));
            }
        }

        return new
        {
            notificaciones =
                notificaciones
                    .OrderByDescending(n => n.Time)
                    .Select(n => n.Notif)
                    .ToList()
        };
    }

    // ================================================================
    // DASHBOARD RECEPCIÓN
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-citas/st-rec-01-dashboard")]
    public async Task<IActionResult> Strec01Dashboard(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-rec-01-dashboard",
                null,
                ct);

            ViewData["DashboardRecepcionData"] =
                await ConstruirDashboardRecepcionAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Strec01Dashboard");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-rec-01-dashboard/index.cshtml");
        }
    }

    private async Task<object> ConstruirDashboardRecepcionAsync(
        CancellationToken ct)
    {
        var hoy = DateTime.Now.Date;

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora.Date == hoy)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        var appointments =
            citasHoy.Select(
                c =>
                {
                    string estado =
                        NormalizarEstado(c.Estado);

                    var result =
                        estado switch
                        {
                            "atendida" or
                            "completada" or
                            "realizada"
                                => (
                                    status: "Atendida",
                                    statusClass: "status-atendida",
                                    actions: new[] { "eye" }
                                ),

                            "en_consulta" or
                            "en_proceso"
                                => (
                                    status: "En consulta",
                                    statusClass: "status-consulta",
                                    actions: new[] { "pencil" }
                                ),

                            "cancelada"
                                => (
                                    status: "Cancelada",
                                    statusClass: "status-pendiente",
                                    actions: new[] { "eye" }
                                ),

                            _
                                => (
                                    status: "Pendiente",
                                    statusClass: "status-pendiente",
                                    actions: new[] { "pencil", "file-invoice" }
                                )
                        };

                    return new
                    {
                        time =
                            c.FechaHora.ToString("HH:mm"),

                        patient =
                            c.Paciente?.NombresCompleto ??
                            "Paciente sin datos",

                        doctor =
                            c.Profesional is not null
                                ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                : "Sin asignar",

                        service =
                            c.Servicio?.Nombre ??
                            "Servicio no especificado",

                        status = result.status,
                        statusClass = result.statusClass,
                        highlight =
                            result.status == "En consulta",
                        actions = result.actions
                    };
                })
                .ToList();

        int confirmadas =
            citasHoy.Count(
                c =>
                    NormalizarEstado(c.Estado) is
                        "confirmada" or
                        "atendida" or
                        "completada" or
                        "realizada");

        int pendientes =
            citasHoy.Count(
                c =>
                    NormalizarEstado(c.Estado) ==
                    "programada");

        decimal facturasPendientes =
            await _context.Facturas
                .Where(
                    f =>
                        f.Estado == "pendiente" ||
                        f.Estado == "parcial")
                .SumAsync(
                    f => f.Total,
                    ct);

        var ahora = DateTime.Now;

        var proximas =
            citasHoy
                .Where(
                    c =>
                        c.FechaHora > ahora &&
                        c.FechaHora <= ahora.AddMinutes(30) &&
                        !EsEstadoCancelado(c.Estado))
                .Select(
                    c => new
                    {
                        hora =
                            c.FechaHora.ToString("hh:mm tt"),

                        fechaIso =
                            c.FechaHora.ToString(
                                "yyyy-MM-ddTHH:mm"),

                        texto =
                            $"{c.Paciente?.NombresCompleto ?? "Paciente sin datos"} - " +
                            $"{c.Servicio?.Nombre ?? "Consulta"} - " +
                            $"{(
                                c.Profesional is not null
                                    ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                    : "Sin asignar"
                            )}" +
                            $"{(
                                c.Consultorio is not null
                                    ? " - " + c.Consultorio.Nombre
                                    : ""
                            )}"
                    })
                .ToList();

        return new
        {
            appointments,

            fechaHoraTexto =
                hoy.ToString(
                    "dddd d 'de' MMMM yyyy",
                    new System.Globalization.CultureInfo("es-CO")),

            horaActualIso =
                ahora.ToString(
                    "yyyy-MM-ddTHH:mm"),

            horaActualTexto =
                ahora.ToString("hh:mm tt"),

            stats = new
            {
                citasHoy = citasHoy.Count,
                confirmadas,
                pendientes,

                facturasPendientes =
                    facturasPendientes >= 1000
                        ? $"${facturasPendientes / 1000:0.#}k"
                        : $"${facturasPendientes:0}"
            },

            proximasCitas = proximas
        };
    }

    // ================================================================
    // GESTIÓN RECEPCIÓN
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-citas/st-rec-03-gestion-citas")]
    public async Task<IActionResult> Strec03GestionCitas(
        [FromQuery] int? editId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        [FromQuery] string? profesional = null,
        [FromQuery] string? fecha = null,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-rec-03-gestion-citas",
                new PaginationQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    Search = search,
                    Estado = estado,
                    Profesional = profesional,
                    Fecha = fecha
                },
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Strec03GestionCitas.");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-rec-03-gestion-citas/index.cshtml");
        }
    }

    // ================================================================
    // RECORDATORIOS
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-citas/st-rec-05-recordatorios")]
    public async Task<IActionResult> Strec05Recordatorios(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-rec-05-recordatorios",
                null,
                ct);

            ViewData["RecordatoriosData"] =
                await ConstruirRecordatoriosAsync(ct);

            return View(
                "~/Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Strec05Recordatorios.");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml");
        }
    }

    private async Task<object> ConstruirRecordatoriosAsync(
        CancellationToken ct)
    {
        var hoy = DateTime.Now.Date;
        var manana = hoy.AddDays(1);
        var pasadoManana = hoy.AddDays(2);

        var citasProximas =
            await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Where(
                    c =>
                        c.FechaHora.Date >= manana &&
                        c.FechaHora.Date <= pasadoManana &&
                        (
                            c.Estado == "programada" ||
                            c.Estado == "Programada" ||
                            c.Estado == "agendada" ||
                            c.Estado == "Agendada" ||
                            c.Estado == "confirmada" ||
                            c.Estado == "Confirmada"
                        ))
                .OrderBy(c => c.FechaHora)
                .ToListAsync(ct);

        static string Iniciales(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "??";

            var partes =
                nombre.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            return partes.Length >= 2
                ? $"{partes[0][0]}{partes[1][0]}"
                    .ToUpperInvariant()
                : nombre[..Math.Min(2, nombre.Length)]
                    .ToUpperInvariant();
        }

        var pendientes =
            citasProximas.Select(
                c => new
                {
                    id = c.IdCita,

                    paciente =
                        c.Paciente?.NombresCompleto ??
                        "Paciente sin datos",

                    iniciales =
                        Iniciales(
                            c.Paciente?.NombresCompleto),

                    fechaHora = c.FechaHora,

                    esManana =
                        c.FechaHora.Date == manana,

                    canal =
                        !string.IsNullOrWhiteSpace(
                            c.Paciente?.Correo)
                            ? "email"
                            : "sms",

                    confirmada =
                        NormalizarEstado(
                            c.Estado) == "confirmada"
                })
                .ToList();

        int sinConfirmar =
            pendientes.Count(
                p =>
                    !p.confirmada &&
                    p.esManana);

        int facturasVencidas =
            await _context.Facturas.CountAsync(
                f =>
                    f.Estado == "pendiente" &&
                    f.FechaFactura <
                        hoy.AddDays(-15),
                ct);

        return new
        {
            pendientes,
            historialEnviados =
                Array.Empty<object>(),
            sinConfirmar,
            facturasVencidas
        };
    }

    // ================================================================
    // CARGA DE DATOS DE AGENDA
    // ================================================================

    private async Task CargarDatosAgenda(
        DateTime? weekStart = null,
        int? professionalId = null,
        int? officeId = null,
        CancellationToken ct = default)
    {
        try
        {
            var hoy = DateTime.Today;

            var inicioSemana =
                weekStart?.Date ??
                hoy;

            if (inicioSemana.DayOfWeek !=
                DayOfWeek.Monday)
            {
                inicioSemana =
                    inicioSemana.AddDays(
                        -(int)inicioSemana.DayOfWeek +
                        (int)DayOfWeek.Monday);
            }

            var agendaDias =
                new List<
                    global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel>();

            for (int i = 0; i < 7; i++)
            {
                var fecha =
                    inicioSemana.AddDays(i);

                try
                {
                    var citasDiaQuery =
                        _context.Citas
                            .AsNoTracking()
                            .Include(c => c.Paciente)
                            .Include(c => c.Profesional)
                            .ThenInclude(p => p!.Usuario)
                            .Include(c => c.Consultorio)
                            .Include(c => c.Servicio)
                            .Include(c => c.EstadoCita)
                            .Where(
                                c =>
                                    c.FechaHora.Date ==
                                    fecha.Date);

                    if (professionalId.HasValue)
                    {
                        citasDiaQuery =
                            citasDiaQuery.Where(
                                c =>
                                    c.IdProfesional ==
                                    professionalId.Value);
                    }

                    if (officeId.HasValue)
                    {
                        citasDiaQuery =
                            citasDiaQuery.Where(
                                c =>
                                    c.IdConsultorio ==
                                    officeId.Value);
                    }

                    var citasDia =
                        await citasDiaQuery
                            .OrderBy(c => c.FechaHora)
                            .ToListAsync(ct);

                    agendaDias.Add(
                        new global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel
                        {
                            Fecha = fecha,

                            NombreDia =
                                fecha.ToString(
                                    "ddd",
                                    new System.Globalization.CultureInfo("es-ES")),

                            NumeroDia =
                                fecha.Day.ToString(),

                            EsHoy =
                                fecha.Date ==
                                hoy.Date,

                            Cerrado =
                                fecha.DayOfWeek ==
                                DayOfWeek.Sunday,

                            Citas =
                                citasDia.Select(
                                    cita =>
                                        new global::SmileTrack_MVC.Models.ViewModels.AgendaCitaViewModel
                                        {
                                            Id =
                                                cita.IdCita,

                                            IdPaciente =
                                                cita.IdPaciente,

                                            IdProfesional =
                                                cita.IdProfesional ??
                                                0,

                                            IdConsultorio =
                                                cita.IdConsultorio ??
                                                0,

                                            IdServicio =
                                                cita.IdServicio ??
                                                0,

                                            Fecha =
                                                cita.FechaHora.Date,

                                            Hora =
                                                cita.FechaHora
                                                    .ToString("HH:mm"),

                                            HoraInicio =
                                                cita.FechaHora
                                                    .ToString("HH:mm"),

                                            HoraFin =
                                                cita.FechaHora
                                                    .AddMinutes(
                                                        DuracionCitaMinutos)
                                                    .ToString("HH:mm"),

                                            Paciente =
                                                $"{cita.Paciente?.Nombres} " +
                                                $"{cita.Paciente?.Apellidos}"
                                                .Trim(),

                                            Servicio =
                                                cita.Servicio?.Nombre ??
                                                "Consulta",

                                            Consultorio =
                                                cita.Consultorio?.Nombre ??
                                                "Sin asignar",

                                            Estado =
                                                cita.EstadoCita?.NombreEstado ??
                                                cita.Estado,

                                            ClaseEstado =
                                                NormalizarEstado(
                                                    cita.EstadoCita?.NombreEstado ??
                                                    cita.Estado) switch
                                                {
                                                    "atendida"
                                                        => "attended",

                                                    "cancelada"
                                                        => "cancelled",

                                                    "confirmada"
                                                        => "confirmed",

                                                    "agendada"
                                                        => "confirmed",

                                                    "programada"
                                                        => "confirmed",

                                                    _
                                                        => "confirmed"
                                                },

                                            Notas =
                                                cita.Notas ??
                                                string.Empty
                                        })
                                    .ToList()
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error cargando agenda para {Fecha}",
                        fecha);

                    agendaDias.Add(
                        new global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel
                        {
                            Fecha = fecha,
                            NombreDia =
                                fecha.ToString(
                                    "ddd",
                                    new System.Globalization.CultureInfo("es-ES")),
                            NumeroDia =
                                fecha.Day.ToString(),
                            EsHoy =
                                fecha.Date ==
                                hoy.Date,
                            Cerrado =
                                fecha.DayOfWeek ==
                                DayOfWeek.Sunday,
                            Citas = []
                        });
                }
            }

            ViewData["AgendaDias"] =
                agendaDias;

            ViewData["SemanaLabel"] =
                $"{inicioSemana:dd/MM} - " +
                $"{inicioSemana.AddDays(6):dd/MM}";
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "CargarDatosAgenda cancelado.");

            ViewData["AgendaDias"] =
                new List<
                    global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel>();
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException CargarDatosAgenda.");

            ViewData["AgendaDias"] =
                new List<
                    global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error general en CargarDatosAgenda.");

            ViewData["AgendaDias"] =
                new List<
                    global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel>();

            ViewData["SemanaLabel"] =
                "Agenda no disponible temporalmente";
        }
    }

    // ================================================================
    // KPI GESTIÓN INTEGRAL DE CITAS
    // ================================================================

    private async Task CargarKpisGestionCitas(
        CancellationToken ct = default)
    {
        try
        {
            var hoy = DateTime.Today;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            var inicioSemanaActual =
                hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
            var finSemanaActual = inicioSemanaActual.AddDays(7);
            var inicioSemanaAnterior = inicioSemanaActual.AddDays(-7);

            var citasMes = await _context.Citas
                .AsNoTracking()
                .Where(c =>
                    c.FechaHora >= inicioMes &&
                    c.FechaHora < finMes)
                .ToListAsync(ct);

            int total = citasMes.Count;

            int programadas = citasMes.Count(c =>
            {
                var estado = NormalizarEstado(c.Estado);
                return estado is "programada" or "agendada" or "confirmada";
            });

            int canceladas = citasMes.Count(c =>
                EsEstadoCancelado(c.Estado));

            int atendidas = citasMes.Count(c =>
                NormalizarEstado(c.Estado) == "atendida");

            int programadasSemanaActual = citasMes.Count(c =>
            {
                if (c.FechaHora < inicioSemanaActual ||
                    c.FechaHora >= finSemanaActual)
                    return false;

                return NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada";
            });

            int programadasSemanaAnterior = citasMes.Count(c =>
            {
                if (c.FechaHora < inicioSemanaAnterior ||
                    c.FechaHora >= inicioSemanaActual)
                    return false;

                return NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada";
            });

            int diferenciaSemana =
                programadasSemanaActual - programadasSemanaAnterior;

            int tasaCancelacion = total > 0
                ? (int)Math.Round(canceladas * 100.0 / total)
                : 0;

            int tasaAsistencia = total > 0
                ? (int)Math.Round(atendidas * 100.0 / total)
                : 0;

            // Claves usadas por index.cshtml.
            ViewData["StatKpiMesTotal"] = total;
            ViewData["StatKpiMesProgramadas"] = programadas;
            ViewData["StatKpiMesCanceladas"] = canceladas;
            ViewData["StatKpiMesAtendidas"] = atendidas;
            ViewData["StatKpiDifSemana"] = diferenciaSemana;
            ViewData["StatKpiTasaCancelacion"] = tasaCancelacion;
            ViewData["StatKpiTasaAsistencia"] = tasaAsistencia;

            // Compatibilidad con otras vistas que puedan usar estas claves.
            ViewData["KpiTotalCitas"] = total;
            ViewData["KpiProgramadas"] = programadas;
            ViewData["KpiCanceladas"] = canceladas;
            ViewData["KpiAtendidas"] = atendidas;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cargando KPI de gestión integral de citas.");

            ViewData["StatKpiMesTotal"] = 0;
            ViewData["StatKpiMesProgramadas"] = 0;
            ViewData["StatKpiMesCanceladas"] = 0;
            ViewData["StatKpiMesAtendidas"] = 0;
            ViewData["StatKpiDifSemana"] = 0;
            ViewData["StatKpiTasaCancelacion"] = 0;
            ViewData["StatKpiTasaAsistencia"] = 0;

            ViewData["KpiTotalCitas"] = 0;
            ViewData["KpiProgramadas"] = 0;
            ViewData["KpiCanceladas"] = 0;
            ViewData["KpiAtendidas"] = 0;
        }
    }

    // ================================================================
    // CARGA CENTRALIZADA DE CITAS
    // ================================================================

    private async Task CargarDatosCitas(
        int? editId,
        string returnUrl,
        PaginationQuery? query = null,
        CancellationToken ct = default)
    {
        try
        {
            var pagination =
                query ??
                new PaginationQuery();

            int page =
                pagination.Page < 1
                    ? 1
                    : pagination.Page;

            int pageSize =
                pagination.PageSize < 1
                    ? 10
                    : Math.Min(
                        pagination.PageSize,
                        100);

            IQueryable<Cita> citasQuery =
                _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Profesional)
                    .ThenInclude(p => p!.Usuario)
                    .Include(c => c.Profesional)
                    .ThenInclude(p => p!.Especialidades)
                    .ThenInclude(pe => pe.Especialidad)
                    .Include(c => c.Servicio)
                    .Include(c => c.Consultorio)
                    .Include(c => c.EstadoCita);

            if (!string.IsNullOrWhiteSpace(
                    pagination.Search))
            {
                string searchTerm =
                    pagination.Search.Trim();

                string pattern =
                    $"%{searchTerm}%";

                citasQuery =
                    citasQuery.Where(
                        c =>
                            (
                                c.Paciente != null &&
                                (
                                    EF.Functions.Like(
                                        c.Paciente.Nombres,
                                        pattern) ||

                                    EF.Functions.Like(
                                        c.Paciente.Apellidos,
                                        pattern)
                                )
                            )

                            ||

                            (
                                c.Profesional != null &&
                                c.Profesional.Usuario != null &&
                                (
                                    EF.Functions.Like(
                                        c.Profesional.Usuario.Nombre,
                                        pattern) ||

                                    EF.Functions.Like(
                                        c.Profesional.Usuario.Apellidos,
                                        pattern)
                                )
                            )

                            ||

                            (
                                c.Notas != null &&
                                EF.Functions.Like(
                                    c.Notas,
                                    pattern)
                            )

                            ||

                            (
                                c.Servicio != null &&
                                EF.Functions.Like(
                                    c.Servicio.Nombre,
                                    pattern)
                            )
                        );
            }

            if (!string.IsNullOrWhiteSpace(pagination.Estado))
            {
                string estado = NormalizarEstado(pagination.Estado);

                string[] estadosPermitidos = estado switch
                {
                    "programada" => ["programada", "Programada", "agendada", "Agendada"],
                    "confirmada" => ["confirmada", "Confirmada"],
                    "atendida" => ["atendida", "Atendida", "completada", "Completada", "realizada", "Realizada"],
                    "cancelada" => ["cancelada", "Cancelada", "cancelado", "Cancelado"],
                    _ => [pagination.Estado.Trim()]
                };

                citasQuery = citasQuery.Where(c =>
                    (c.Estado != null && estadosPermitidos.Contains(c.Estado)) ||
                    (c.EstadoCita != null &&
                     c.EstadoCita.NombreEstado != null &&
                     estadosPermitidos.Contains(c.EstadoCita.NombreEstado)));
            }

            if (int.TryParse(
                    pagination.Profesional,
                    out int idProfesional) &&
                idProfesional > 0)
            {
                citasQuery =
                    citasQuery.Where(
                        c =>
                            c.IdProfesional ==
                            idProfesional);
            }

            if (!string.IsNullOrWhiteSpace(
                    pagination.Fecha) &&
                DateTime.TryParseExact(
                    pagination.Fecha.Trim(),
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var fechaValida))
            {
                var inicio =
                    fechaValida.Date;

                var fin =
                    inicio.AddDays(1);

                citasQuery =
                    citasQuery.Where(
                        c =>
                            c.FechaHora >= inicio &&
                            c.FechaHora < fin);
            }

            citasQuery =
                citasQuery
                    .OrderByDescending(
                        c => c.FechaHora);

            var paged =
                await citasQuery.ToPagedResultAsync(
                    page,
                    pageSize,
                    ct);

            ViewData["Citas"] =
                paged.Items.ToList();

            ViewData["CitasPage"] =
                paged;

            ViewData["PaginationQuery"] =
                pagination;

            ViewData["SearchFilter"] =
                pagination.Search ??
                string.Empty;

            ViewData["EstadoFilter"] =
                pagination.Estado ??
                string.Empty;

            ViewData["ProfesionalFilter"] =
                pagination.Profesional ??
                string.Empty;

            ViewData["FechaFilter"] =
                pagination.Fecha ??
                string.Empty;

            ViewData["Pacientes"] =
                await _context.Pacientes
                    .AsNoTracking()
                    .Where(
                        p =>
                            p.Estado ==
                            "activo")
                    .OrderBy(p => p.Apellidos)
                    .ThenBy(p => p.Nombres)
                    .ToListAsync(ct);

            ViewData["Profesionales"] =
                await _context.Profesionales
                    .AsNoTracking()
                    .Include(p => p.Usuario)
                    .Where(
                        p =>
                            p.Estado ==
                            "activo")
                    .OrderBy(p => p.Apellidos)
                    .ToListAsync(ct);

            ViewData["ProfesionalesFilterOptions"] =
                await _context.Profesionales
                    .AsNoTracking()
                    .Include(p => p.Usuario)
                    .Where(
                        p =>
                            p.Estado ==
                            "activo")
                    .OrderBy(p => p.Apellidos)
                    .ToListAsync(ct);

            ViewData["Consultorios"] =
                await _context.Consultorios
                    .AsNoTracking()
                    .Where(
                        c =>
                            c.Estado ==
                                "disponible" ||
                            c.Estado ==
                                "activo")
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(ct);

            ViewData["EstadosCita"] =
                await _context.EstadosCita
                    .AsNoTracking()
                    .OrderBy(
                        e => e.NombreEstado)
                    .ToListAsync(ct);

            ViewData["Servicios"] =
                await _context.Servicios
                    .AsNoTracking()
                    .Where(
                        s =>
                            s.Estado ==
                            "activo")
                    .OrderBy(s => s.Nombre)
                    .ToListAsync(ct);

            ViewData["ReturnUrl"] =
                returnUrl;

            if (editId is > 0)
            {
                ViewData["EditingCita"] =
                    await _context.Citas
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            c =>
                                c.IdCita ==
                                editId.Value,
                            ct);
            }
            else
            {
                ViewData["EditingCita"] =
                    null;
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "CargarDatosCitas cancelado.");

            InicializarViewDataCitasVacia(
                returnUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "InvalidOperationException en CargarDatosCitas.");

            InicializarViewDataCitasVacia(
                returnUrl);

            TempData["ErrorValidacion"] =
                "Error al aplicar los filtros de búsqueda.";
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException en CargarDatosCitas.");

            InicializarViewDataCitasVacia(
                returnUrl);

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error general en CargarDatosCitas.");

            InicializarViewDataCitasVacia(
                returnUrl);
        }
    }

    // ================================================================
    // DASHBOARD PRINCIPAL
    // ================================================================

    private async Task CargarDatosDashboard(
        CancellationToken ct = default)
    {
        try
        {
            var hoy =
                DateTime.Today;

            var inicioMes =
                new DateTime(
                    hoy.Year,
                    hoy.Month,
                    1);

            var finMes =
                inicioMes.AddMonths(1);

            ViewData["TotalPacientes"] =
                await _context.Pacientes
                    .CountAsync(ct);

            ViewData["CitasHoy"] =
                await _context.Citas
                    .CountAsync(
                        c =>
                            c.FechaHora.Date ==
                            hoy,
                        ct);

            ViewData["ProfesionalesActivos"] =
                await _context.Profesionales
                    .CountAsync(
                        p =>
                            p.Estado ==
                            "activo",
                        ct);

            var citasDelMes =
                await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Servicio)
                    .Where(
                        c =>
                            c.FechaHora >=
                                inicioMes &&
                            c.FechaHora <
                                finMes)
                    .ToListAsync(ct);

            decimal ingresos =
                citasDelMes
                    .Where(
                        c =>
                            NormalizarEstado(
                                c.Estado) ==
                            "atendida")
                    .Sum(
                        c =>
                            c.Servicio?.Precio ??
                            0m);

            ViewData["IngresosDelMes"] =
                ingresos;

            ViewData["FacturasPendientes"] =
                new List<(
                    string Codigo,
                    string Descripcion,
                    decimal Monto)>();

            int daysInMonth =
                DateTime.DaysInMonth(
                    hoy.Year,
                    hoy.Month);

            int totalCapacity =
                daysInMonth * 40;

            int totalCitas =
                citasDelMes.Count;

            ViewData["PctOcupacion"] =
                totalCapacity > 0
                    ? (int)Math.Min(
                        100,
                        (double)totalCitas /
                        totalCapacity *
                        100)
                    : 0;

            int atendidas =
                citasDelMes.Count(
                    c =>
                        NormalizarEstado(
                            c.Estado) ==
                        "atendida");

            int confirmadas =
                citasDelMes.Count(
                    c =>
                        NormalizarEstado(
                            c.Estado) ==
                        "confirmada");

            int programadas =
                citasDelMes.Count(
                    c =>
                        NormalizarEstado(
                            c.Estado) ==
                        "programada");

            int canceladas =
                citasDelMes.Count(
                    c =>
                        EsEstadoCancelado(
                            c.Estado));

            int maxEstado =
                new[]
                {
                    atendidas,
                    confirmadas,
                    programadas,
                    canceladas
                }.DefaultIfEmpty(0).Max();

            ViewData["CitasAtendidas"] =
                atendidas;

            ViewData["CitasConfirmadas"] =
                confirmadas;

            ViewData["CitasProgramadas"] =
                programadas;

            ViewData["CitasCanceladas"] =
                canceladas;

            ViewData["PctAtendidas"] =
                CalcularPorcentaje(
                    atendidas,
                    maxEstado);

            ViewData["PctConfirmadas"] =
                CalcularPorcentaje(
                    confirmadas,
                    maxEstado);

            ViewData["PctProgramadas"] =
                CalcularPorcentaje(
                    programadas,
                    maxEstado);

            ViewData["PctCanceladas"] =
                CalcularPorcentaje(
                    canceladas,
                    maxEstado);

            var topIds =
                await _context.Citas
                    .Where(
                        c =>
                            c.FechaHora >=
                                inicioMes &&
                            c.FechaHora <
                                finMes &&
                            c.IdProfesional != null &&
                            c.IdProfesional != 0)
                    .GroupBy(
                        c =>
                            c.IdProfesional)
                    .Select(
                        g =>
                            new
                            {
                                IdProfesional =
                                    g.Key,

                                Total =
                                    g.Count()
                            })
                    .OrderByDescending(
                        g => g.Total)
                    .Take(3)
                    .ToListAsync(ct);

            var idsList =
                topIds
                    .Where(
                        x =>
                            x.IdProfesional != null)
                    .Select(
                        x =>
                            x.IdProfesional!.Value)
                    .ToList();

            var profesionales =
                await _context.Profesionales
                    .AsNoTracking()
                    .Include(p => p.Usuario)
                    .Include(p => p.Especialidades)
                    .ThenInclude(pe => pe.Especialidad)
                    .Where(
                        p =>
                            idsList.Contains(
                                p.IdProfesional))
                    .ToListAsync(ct);

            var topProfesionales =
                topIds.Select(
                    t =>
                    {
                        var profesional =
                            profesionales.FirstOrDefault(
                                p =>
                                    p.IdProfesional ==
                                    t.IdProfesional);

                        return new
                        {
                            Nombre =
                                profesional?.Usuario?.Nombre ??
                                profesional?.Nombres ??
                                "Sin nombre",

                            Especialidad =
                                profesional?
                                    .Especialidades
                                    .FirstOrDefault(
                                        pe =>
                                            pe.Principal)
                                    ?.Especialidad
                                    ?.Nombre ??
                                "General",

                            TotalCitas =
                                t.Total
                        };
                    })
                    .ToList();

            ViewData["TopProfesionales"] =
                topProfesionales;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "CargarDatosDashboard cancelado.");

            InicializarViewDataDashboardVacio();
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(
                ex,
                "SqlException CargarDatosDashboard.");

            InicializarViewDataDashboardVacio();

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error general CargarDatosDashboard.");

            InicializarViewDataDashboardVacio();
        }
    }

    // ================================================================
    // EMAIL DE CITA
    // ================================================================

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

    private static bool EsEstadoCancelado(
        string? estado)
    {
        return NormalizarEstado(estado) is
            "cancelada" or
            "cancelado";
    }

    private static int CalcularPorcentaje(
        int valor,
        int total)
    {
        if (total <= 0)
            return 0;

        return (int)Math.Round(
            valor * 100.0 / total,
            0);
    }

    private static int CalcularEdad(
        DateTime fechaNacimiento)
    {
        var hoy =
            DateTime.Today;

        int edad =
            hoy.Year -
            fechaNacimiento.Year;

        if (fechaNacimiento.Date >
            hoy.AddYears(-edad))
        {
            edad--;
        }

        return edad;
    }

    private static string? BuildNotasCita(
        CitaViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(
                model.MotivoConsulta))
        {
            return model.MotivoConsulta.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
                model.Notas))
        {
            return model.Notas.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
                model.NotasPrevias))
        {
            return model.NotasPrevias.Trim();
        }

        return null;
    }

    private async Task<string> BuildEstadoNombreAsync(
        int? idEstado,
        string? estadoFallback,
        string estadoActual,
        CancellationToken ct)
    {
        if (idEstado.HasValue &&
            idEstado.Value > 0)
        {
            var estado = await _context.EstadosCita.FindAsync(
    [idEstado.Value],
    ct);

            if (estado != null)
            {
                return EstadoCitaHelper.ResolveEstadoNombre(
                    estado.NombreEstado,
                    estadoFallback);
            }
        }

        if (!string.IsNullOrWhiteSpace(
                estadoFallback))
        {
            return EstadoCitaHelper.ResolveEstadoNombre(
                estadoFallback.Trim(),
                estadoFallback.Trim());
        }

        return EstadoCitaHelper.ResolveEstadoNombre(
            estadoActual,
            "programada");
    }

    private void InicializarViewDataCitasVacia(
        string returnUrl)
    {
        ViewData["Citas"] =
            new List<Cita>();

        ViewData["CitasPage"] =
            PagedResult<Cita>.Empty(
                1,
                10);

        ViewData["PaginationQuery"] =
            new PaginationQuery();

        ViewData["SearchFilter"] =
            string.Empty;

        ViewData["EstadoFilter"] =
            string.Empty;

        ViewData["ProfesionalFilter"] =
            string.Empty;

        ViewData["FechaFilter"] =
            string.Empty;

        ViewData["Pacientes"] =
            new List<Paciente>();

        ViewData["Profesionales"] =
            new List<Profesional>();

        ViewData["ProfesionalesFilterOptions"] =
            new List<Profesional>();

        ViewData["Consultorios"] =
            new List<Consultorio>();

        ViewData["EstadosCita"] =
            new List<EstadoCita>();

        ViewData["Servicios"] =
            new List<Servicio>();

        ViewData["ReturnUrl"] =
            returnUrl;

        ViewData["EditingCita"] =
            null;

        ViewData["StatKpiMesTotal"] = 0;
        ViewData["StatKpiMesProgramadas"] = 0;
        ViewData["StatKpiMesCanceladas"] = 0;
        ViewData["StatKpiMesAtendidas"] = 0;
        ViewData["StatKpiDifSemana"] = 0;
        ViewData["StatKpiTasaCancelacion"] = 0;
        ViewData["StatKpiTasaAsistencia"] = 0;
        ViewData["KpiTotalCitas"] = 0;
        ViewData["KpiProgramadas"] = 0;
        ViewData["KpiCanceladas"] = 0;
        ViewData["KpiAtendidas"] = 0;
    }

    private void InicializarViewDataDashboardVacio()
    {
        ViewData["TotalPacientes"] = 0;
        ViewData["CitasHoy"] = 0;
        ViewData["ProfesionalesActivos"] = 0;
        ViewData["IngresosDelMes"] = 0m;

        ViewData["FacturasPendientes"] =
            new List<(
                string,
                string,
                decimal)>();

        ViewData["PctOcupacion"] = 0;
        ViewData["CitasAtendidas"] = 0;
        ViewData["CitasConfirmadas"] = 0;
        ViewData["CitasProgramadas"] = 0;
        ViewData["CitasCanceladas"] = 0;
        ViewData["PctAtendidas"] = 0;
        ViewData["PctConfirmadas"] = 0;
        ViewData["PctProgramadas"] = 0;
        ViewData["PctCanceladas"] = 0;

        ViewData["TopProfesionales"] =
            new List<object>();
    }

    private static bool EsViolacionIndiceUnico(
        DbUpdateException dbex,
        out string? indiceAfectado)
    {
        indiceAfectado = null;

        var sqlEx =
            dbex.InnerException as SqlException
            ??
            dbex.InnerException?.InnerException as SqlException;

        if (sqlEx == null)
        {
            return false;
        }

        if (sqlEx.Number == 2601 ||
            sqlEx.Number == 2627)
        {
            indiceAfectado =
                sqlEx.Message;

            return true;
        }

        return false;
    }

    private static bool EsViolacionIntegridadReferencial(
        DbUpdateException dbex)
    {
        var sqlEx =
            dbex.InnerException as SqlException
            ??
            dbex.InnerException?.InnerException as SqlException;

        if (sqlEx == null)
        {
            return false;
        }

        return sqlEx.Number is
            547 or 515;
    }
}