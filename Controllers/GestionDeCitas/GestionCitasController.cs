/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: GestionCitasController
 * ============================================
 * Archivo: Controllers/GestionDeCitas/GestionCitasController.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Centraliza la atención de peticiones MVC (Server-Side Rendering) e interacciones
 * web para la gestión integral de citas odontológicas. Actúa como capa de orquestación
 * entre las vistas Razor y los servicios de negocio (CitaService, AgendaService,
 * CitasDashboardService), garantizando la validación de autorización por roles y ownership.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Duración configurable de cita (60 min por defecto) validada ante disponibilidad de recursos.
 * - Validación triple de conflictos (Profesional, Paciente y Consultorio) para evitar solapamientos.
 * - Control estricto de acceso basado en claims y roles (Administrador, Recepcionista, Odontólogo, Auxiliar, Paciente).
 * - Sincronización de transacciones con Auditoría y Soft Delete para cancelación de citas.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, ICitaService, IAgendaService, ICitasDashboardService, IEmailService
 * ============================================
 */

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Exceptions;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Models.ViewModels.GestionDeCitas;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using SmileTrack_MVC.Services.GestionDeCitas;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace SmileTrack_MVC.Controllers;

public class GestionCitasController(
    AppDbContext context,
    ILogger<GestionCitasController> logger,
    IEmailService emailService,
    ICitaService citaService,
    IPanelOperativoService panelOperativoService,
    IAntiforgery antiforgery,
    ICitasDashboardService citasDashboardService,
    IAgendaService agendaService,
    IMemoryCache memoryCache,
    SmileTrack_MVC.Services.Perfiles.IPerfilPacienteService perfilPacienteService,
    IPacienteNotificacionQueryService pacienteNotificacionQueryService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionCitasController> _logger = logger;
    private readonly IEmailService _emailService = emailService;
    private readonly ICitaService _citaService = citaService;
    private readonly IPanelOperativoService _panelOperativoService = panelOperativoService;
    private readonly IAntiforgery _antiforgery = antiforgery;
    private readonly ICitasDashboardService _citasDashboardService = citasDashboardService;
    private readonly IAgendaService _agendaService = agendaService;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly SmileTrack_MVC.Services.Perfiles.IPerfilPacienteService _perfilPacienteService = perfilPacienteService;
    private readonly IPacienteNotificacionQueryService _pacienteNotificacionQueryService = pacienteNotificacionQueryService;

    /*
     * REGLA DE NEGOCIO:
     * La duración de las citas se gestiona centralizadamente en
     * ICitaService.ObtenerDuracionCitaMinutosAsync leyendo la clave
     * "cita_duracion_minutos" de la tabla Configuracion_General.
     *
     * IMPORTANTE:
     * Este controlador NO define una fuente de verdad propia para la duración.
     * Si necesita saberla, consulte al servicio (fallback 60 min).
     */

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
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-citas/st-adm-01-dashboard")]
    public async Task<IActionResult> Stadm01Dashboard(
        CancellationToken ct = default)
    {
        // CargarDatosCitas() fue eliminado: cargaba pacientes, citas paginadas,
        // consultorios y estados que esta vista nunca consumió.
        // Toda la lógica de datos vive ahora en ICitasDashboardService.
        try
        {
            var model = await _citasDashboardService
                .ObtenerDashboardAsync(ColombiaClock.Today, ct);

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml",
                model);
        }
        catch (OperationCanceledException)
        {
            // Dejar que el middleware maneje la cancelación — no loggear como error.
            throw;
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Error SQL cargando dashboard de citas.");

            TempData["ErrorValidacion"] =
                "No fue posible cargar las métricas. Intente nuevamente.";

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml",
                new CitasDashboardViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado cargando dashboard de citas para {Usuario}.",
                User.Identity?.Name ?? "anonimo");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-01-dashboard/index.cshtml",
                new CitasDashboardViewModel());
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Auxiliar")]
    [Route("gestion-de-citas/st-adm-01-dashboard/exportar-pdf")]
    public async Task<IActionResult> ExportarDashboardPdf(CancellationToken ct = default)
    {
        // Usa el mismo servicio que el dashboard para garantizar que los números
        // del PDF sean idénticos a los que el usuario vio en pantalla.
        var hoy = ColombiaClock.Today;
        var model = await _citasDashboardService.ObtenerDashboardAsync(hoy, ct);

        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes = inicioMes.AddMonths(1);

        QuestPDF.Settings.License = LicenseType.Community;

        byte[] pdf = Document.Create(document => document.Page(page =>
        {
            page.Margin(40);
            page.Header().Text("SmileTrack — Dashboard de Citas").FontSize(20).Bold();
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(
                    $"Período: {inicioMes:dd/MM/yyyy} – {finMes.AddDays(-1):dd/MM/yyyy}");
                column.Item().Text($"Generado: {ColombiaClock.Now:dd/MM/yyyy HH:mm}");
                column.Item().LineHorizontal(1);

                // KPIs principales
                column.Item().Text($"Pacientes activos:      {model.Kpis.PacientesActivos}");
                column.Item().Text($"Citas hoy:              {model.Kpis.CitasHoy}");
                column.Item().Text($"Profesionales activos:  {model.Kpis.ProfesionalesActivos}");
                column.Item().Text(
                    $"Valor estimado atendido: {model.Kpis.ValorEstimadoAtendidoMes:C0}");

                column.Item().LineHorizontal(1);

                // Distribución por estado
                column.Item().Text($"Total citas del mes:    {model.Estados.Total}");
                column.Item().Text(
                    $"  Atendidas:   {model.Estados.Atendidas} ({model.Estados.PorcentajeAtendidas}%)");
                column.Item().Text(
                    $"  Confirmadas: {model.Estados.Confirmadas} ({model.Estados.PorcentajeConfirmadas}%)");
                column.Item().Text(
                    $"  Programadas: {model.Estados.Programadas} ({model.Estados.PorcentajeProgramadas}%)");
                column.Item().Text(
                    $"  Canceladas:  {model.Estados.Canceladas} ({model.Estados.PorcentajeCanceladas}%)");
                column.Item().Text(
                    $"  Otros estados: {model.Estados.Otros} ({model.Estados.PorcentajeOtros}%)");
                column.Item().Text(
                    $"Ocupación estimada: {model.OcupacionPorcentaje}%");

                // Top profesionales
                if (model.TopProfesionales.Count > 0)
                {
                    column.Item().LineHorizontal(1);
                    column.Item().Text("Top profesionales del mes:").Bold();
                    foreach (var prof in model.TopProfesionales)
                    {
                        column.Item().Text(
                            $"  {prof.Nombre} ({prof.Especialidad}) — {prof.TotalCitas} citas");
                    }
                }
            });
            page.Footer().AlignCenter().Text("SmileTrack");
        })).GeneratePdf();

        return File(pdf, "application/pdf", $"reporte-dashboard-{hoy:yyyy-MM-dd}.pdf");
    }

    // ================================================================
    // AGENDA GENERAL
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-citas/agenda-profesionales")]
    [Route("gestion-de-citas/st-adm-08-agenda")]
    public async Task<IActionResult> Stadm08Agenda(
        [FromQuery] DateTime? weekStart,
        [FromQuery] int? professionalId,
        [FromQuery] int? officeId,
        [FromQuery] bool calendarOnly = false,
        CancellationToken ct = default)
    {
        try
        {
            if (!calendarOnly)
                _antiforgery.GetAndStoreTokens(HttpContext);

            var model = await _agendaService.ObtenerAgendaAsync(
                weekStart,
                professionalId,
                officeId,
                ct,
                includeCatalogs: !calendarOnly);

            if (calendarOnly)
                return PartialView("~/Views/Gestion_De_Citas/st-adm-08-agenda/_CalendarSection.cshtml", model);

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                model);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Solicitud cancelada Stadm08Agenda.");
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos en Stadm08Agenda");

            TempData["ErrorValidacion"] =
                "Error al consultar datos. Intente nuevamente.";

            if (calendarOnly)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "No fue posible actualizar el calendario.");

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Error SQL {Number} en Stadm08Agenda",
                ex.Number);

            TempData["ErrorValidacion"] =
                "Servicio temporalmente no disponible. Intente en unos minutos.";

            if (calendarOnly)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "No fue posible actualizar el calendario.");

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error crítico cargando Stadm08Agenda");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            if (calendarOnly)
                return StatusCode(StatusCodes.Status500InternalServerError, "No fue posible actualizar el calendario.");

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                new AgendaViewModel());
        }
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
                "CrearCitaDesdeAgendaInterna: ModelState inválido. Errores={Errores}",
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
            int? actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)
                ? userId
                : null;
            string ipOrigen = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
            int duracionMinutos =
                await _citaService.ObtenerDuracionCitaMinutosAsync(ct);

            var fechaHora = dto.Fecha.Date.Add(dto.HoraInicio);
            string estadoNormalizado =
                string.IsNullOrWhiteSpace(dto.Estado)
                    ? "Programada"
                    : dto.Estado.Trim();

            bool esActualizacion = dto.IdCita.HasValue && dto.IdCita.Value > 0;
            Cita cita;

            if (esActualizacion)
            {
                var citaAnterior = await _citaService.ObtenerPorIdAsync(dto.IdCita!.Value, ct)
                    ?? throw new InvalidOperationException("La cita que intenta actualizar no existe.");
                var updateDto = new Models.DTOs.CitaApiUpdateDto
                {
                    IdCita = dto.IdCita!.Value,
                    IdPaciente = dto.IdPaciente,
                    IdProfesional = dto.IdProfesional,
                    IdServicio = dto.IdServicio,
                    IdConsultorio = dto.IdConsultorio,
                    FechaHora = fechaHora,
                    Estado = estadoNormalizado,
                    Notas = dto.Notas?.Trim(),
                    IdUsuarioModificacion = actorId
                };

                cita = await _citaService.ActualizarAsync(dto.IdCita.Value, updateDto, ct, actorId, ipOrigen)
                    ?? throw new InvalidOperationException("La cita no existe.");
            }
            else
            {
                var request = new Models.DTOs.CitaApiRequest
                {
                    IdPaciente = dto.IdPaciente,
                    IdProfesional = dto.IdProfesional,
                    IdServicio = dto.IdServicio,
                    IdConsultorio = dto.IdConsultorio,
                    FechaHora = fechaHora,
                    Estado = estadoNormalizado,
                    Notas = dto.Notas?.Trim()
                };

                cita = await _citaService.CrearAsync(request, ct, actorId, ipOrigen);
            }

            _logger.LogInformation(
                "Cita guardada desde Agenda Interna (via Servicio). IdCita={IdCita}, Usuario={Usuario}",
                cita.IdCita,
                User.Identity?.Name ?? "anonimo");

            return Ok(new
            {
                success = true,
                message = esActualizacion
                    ? "Cita actualizada exitosamente."
                    : "Cita agendada exitosamente.",
                id = cita.IdCita,
                idEstado = cita.IdEstado,
                estado = cita.Estado,
                updated = esActualizacion,
                duracionMinutos = duracionMinutos
            });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación cancelada al crear cita desde Agenda Interna.");

            return BadRequest(new
            {
                success = false,
                message = "La operación fue cancelada."
            });
        }
        catch (CitaConflictException ex)
        {
            _logger.LogWarning(ex, "Conflicto de agenda en CrearCitaDesdeAgendaInterna.");

            return Conflict(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Validación de servicio en CrearCitaDesdeAgendaInterna.");

            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Conflicto de concurrencia al guardar cita desde Agenda Interna.");

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
                "Violación UNIQUE al guardar cita desde Agenda Interna.");

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
                "Error de base de datos al guardar cita desde Agenda Interna.");

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
            _logger.LogError(
                ex,
                "SqlException al guardar cita desde Agenda Interna. Number={Number}",
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
            _logger.LogError(
                ex,
                "Error inesperado al crear cita desde Agenda Interna.");

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
    // API: EDITAR NOTAS DE CITA (PROFESIONAL)
    // ================================================================

    public sealed class CitaNotasDto
    {
        public int IdCita { get; set; }
        public string? Notas { get; set; }
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

            var fechaMinima = ColombiaClock.Today;
            var editingCita = ViewData["EditingCita"] as Cita;
            var fechaInicial = editingCita?.Fecha.Date ?? fechaMinima;
            if (editingCita == null)
            {
                while (fechaInicial.DayOfWeek == DayOfWeek.Sunday)
                {
                    fechaInicial = fechaInicial.AddDays(1);
                }
            }
            ViewData["FechaMinimaCita"] = fechaMinima;
            ViewData["FechaInicialCita"] = fechaInicial;

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
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
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

        if (User.IsInRole("Profesional"))
        {
            if (!int.TryParse(User.FindFirstValue("IdProfesional"), out int idProfesionalActual) ||
                idProfesionalActual <= 0 ||
                model.IdProfesional != idProfesionalActual)
            {
                TempData["ErrorValidacion"] =
                    "Solo puede agendar citas para usted mismo.";

                return Redirect(returnUrlSafe);
            }

            if (model.IdCita is > 0)
            {
                var citaExistente = await _citaService.ObtenerPorIdAsync(model.IdCita.Value, ct);
                if (citaExistente is null || citaExistente.IdProfesional != idProfesionalActual)
                {
                    TempData["ErrorValidacion"] =
                        "No tiene permiso para modificar esta cita.";

                    return Redirect(returnUrlSafe);
                }
            }
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
            int? actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)
                ? userId
                : null;
            string ipOrigen = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
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
                ColombiaClock.Now.AddMinutes(-5))
            {
                TempData["ErrorValidacion"] =
                    "La fecha y hora de la cita no son válidas o corresponden a un horario pasado.";

                return Redirect(returnUrlSafe);
            }
            // ------------------------------------------------------------
            // PERSISTENCIA — LÓGICA DELEGADA 100% EN SERVICIO
            // Incluye: existencia entidades, duración configurable,
            // horario de clínica (M-15) y conflicto triple recurso
            // (Profesional ∧ Paciente ∧ Consultorio — U-07).
            // ------------------------------------------------------------

            Cita cita;

            if (model.IdCita > 0)
            {
                var citaExistente = await _citaService.ObtenerPorIdAsync(model.IdCita.Value, ct)
                    ?? throw new InvalidOperationException("La cita que intenta actualizar no existe.");
                var updateDto = new Models.DTOs.CitaApiUpdateDto
                {
                    IdCita = model.IdCita.Value,
                    IdPaciente = model.IdPaciente,
                    IdProfesional = model.IdProfesional > 0 ? model.IdProfesional : null,
                    IdServicio = model.IdServicio > 0 ? model.IdServicio : null,
                    IdConsultorio = model.IdConsultorio > 0 ? model.IdConsultorio : null,
                    FechaHora = model.FechaHora,
                    Estado = BuildNotasCita(model) is var b1 &&
                             await BuildEstadoNombreAsync(
                                 model.IdEstado,
                                 model.Estado,
                                 "programada",
                                 ct) is var b2
                                 ? EstadoCitaHelper.ResolveEstadoNombre(b2, "programada")
                                 : "programada",
                    Notas = BuildNotasCita(model),
                    IdEstado = model.IdEstado > 0 ? model.IdEstado : null,
                    IdUsuarioModificacion = actorId
                };

                if (!EstadoCitaHelper.PuedeTransicionarPorRol(
                    citaExistente.Estado,
                    updateDto.Estado,
                    User.IsInRole("Administrador"),
                    User.IsInRole("Recepcionista"),
                    User.IsInRole("Profesional")))
                {
                    TempData["ErrorValidacion"] =
                        "Su rol no puede realizar esta transición de estado.";
                    return Redirect(returnUrlSafe);
                }

                cita = await _citaService.ActualizarAsync(
                           model.IdCita.Value,
                           updateDto,
                           ct,
                           actorId,
                           ipOrigen)
                       ?? throw new InvalidOperationException(
                           "La cita que intenta actualizar no existe.");

                operacion = "Actualizacion";
            }
            else
            {
                var estadoInicial = EstadoCitaHelper.ResolveEstadoNombre(
                    await BuildEstadoNombreAsync(
                        model.IdEstado,
                        model.Estado,
                        "programada",
                        ct),
                    "programada");
                if (!EstadoCitaHelper.PuedeCrearPorRol(
                    estadoInicial,
                    User.IsInRole("Administrador")))
                {
                    TempData["ErrorValidacion"] =
                        "Solo administración puede crear una cita en ese estado.";
                    return Redirect(returnUrlSafe);
                }

                var request = new Models.DTOs.CitaApiRequest
                {
                    IdPaciente = model.IdPaciente,
                    IdProfesional = model.IdProfesional > 0 ? model.IdProfesional : null,
                    IdServicio = model.IdServicio > 0 ? model.IdServicio : null,
                    IdConsultorio = model.IdConsultorio > 0 ? model.IdConsultorio : null,
                    FechaHora = model.FechaHora,
                    Estado = estadoInicial,
                    Notas = BuildNotasCita(model),
                    IdEstado = model.IdEstado > 0 ? model.IdEstado : null
                };

                cita = await _citaService.CrearAsync(request, ct, actorId, ipOrigen);
                operacion = "Creacion";
            }

            _logger.LogInformation(
                "GuardarCita ejecutado correctamente via Servicio. Operacion={Operacion}, IdCita={IdCita}, FechaHora={FechaHora}, IdPaciente={IdPaciente}, IdProfesional={IdProfesional}, IdServicio={IdServicio}, IdConsultorio={IdConsultorio}, IdEstado={IdEstado}, Estado={Estado}",
                operacion,
                cita.IdCita,
                cita.FechaHora,
                cita.IdPaciente,
                cita.IdProfesional,
                cita.IdServicio,
                cita.IdConsultorio,
                cita.IdEstado,
                cita.Estado);

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
                "{Operacion} de cita correcta via Servicio: IdCita={IdCita}, Usuario={Usuario}",
                operacion,
                cita.IdCita,
                User.Identity?.Name ?? "anonimo");

            TempData["MensajeExito"] =
                operacion == "Creacion"
                    ? "La cita se ha agendado correctamente."
                    : "La cita se ha actualizado correctamente.";
        }
        catch (InvalidOperationException ioex)
        {
            _logger.LogWarning(
                ioex,
                "GuardarCita: Validación de servicio fallida. Operacion={Operacion}, Id={Id}",
                operacion,
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                string.IsNullOrWhiteSpace(ioex.Message)
                    ? "No se pudo guardar la cita por una restricción del sistema."
                    : ioex.Message;
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
            _logger.LogError(
                ex,
                "SqlException al guardar cita Id={Id}. Number={Number}",
                idCitaOperacion,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Excepción inesperada al guardar cita Id={Id}",
                idCitaOperacion);

            TempData["ErrorValidacion"] =
                "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    // ================================================================
    // CAMBIAR ESTADO DE CITA MVC (INLINE - SOLO CAMBIO DE ESTADO)
    // ================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar")]
    [Route("gestion-de-citas/cambiar-estado")]
    public async Task<IActionResult> CambiarEstadoCita(
        [FromForm] int IdCita,
        [FromForm] string? Estado,
        [FromForm] string? ReturnUrl,
        CancellationToken ct = default)
    {
        string returnUrlSafe =
            !string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl)
                ? ReturnUrl
                : "/gestion-de-citas/st-adm-09-citas";

        if (IdCita <= 0)
        {
            TempData["ErrorValidacion"] =
                "Identificador de cita inválido.";
            return Redirect(returnUrlSafe);
        }

        if (string.IsNullOrWhiteSpace(Estado))
        {
            TempData["ErrorValidacion"] =
                "El estado de la cita es obligatorio.";
            return Redirect(returnUrlSafe);
        }

        string estadoNormalizado = Estado.Trim();

        try
        {
            var citaActual = await _citaService.ObtenerPorIdAsync(IdCita, ct);
            if (citaActual is null)
            {
                TempData["ErrorValidacion"] = "La cita no existe o no se pudo modificar su estado.";
                return Redirect(returnUrlSafe);
            }

            if (!EstadoCitaHelper.PuedeTransicionarPorRol(
                citaActual.Estado,
                estadoNormalizado,
                User.IsInRole("Administrador"),
                User.IsInRole("Recepcionista"),
                User.IsInRole("Profesional"),
                User.IsInRole("Auxiliar")))
            {
                return Forbid();
            }

            if (User.IsInRole("Profesional"))
            {
                bool esPropietario =
                    int.TryParse(User.FindFirst("IdProfesional")?.Value, out int idProfesional) &&
                    citaActual.IdProfesional == idProfesional;
                if (!esPropietario &&
                    int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int idUsuario))
                {
                    esPropietario = await _context.Profesionales.AnyAsync(
                        p => p.IdUsuario == idUsuario && p.IdProfesional == citaActual.IdProfesional,
                        ct);
                }

                if (!esPropietario)
                    return Forbid();
            }

            int? idUsuarioActor = int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int usuarioActual)
                ? usuarioActual
                : null;
            string? ipActor = HttpContext.Connection.RemoteIpAddress?.ToString();

            var cita = await _citaService.CambiarEstadoAsync(
                IdCita,
                estadoNormalizado,
                idUsuarioActor,
                ct,
                ipActor);

            if (cita is null)
            {
                TempData["ErrorValidacion"] =
                    "La cita no existe o no se pudo modificar su estado.";
                return Redirect(returnUrlSafe);
            }

            string estadoParaNotificacion = NormalizarEstado(cita.Estado);
            if (estadoParaNotificacion is "confirmada" or "cancelada")
            {
                await EnviarNotificacionCitaAsync(
                    cita.IdCita,
                    estadoParaNotificacion,
                    ct);
            }

            TempData["MensajeExito"] =
                $"El estado de la cita se actualizó a '{cita.Estado}' correctamente.";
        }
        catch (InvalidOperationException ioex)
        {
            _logger.LogWarning(
                ioex,
                "CambiarEstadoCita: Validación de servicio fallida Id={Id}, Estado={Estado}",
                IdCita,
                estadoNormalizado);

            TempData["ErrorValidacion"] =
                string.IsNullOrWhiteSpace(ioex.Message)
                    ? "No se pudo cambiar el estado de la cita."
                    : ioex.Message;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación cancelada CambiarEstadoCita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "La operación fue cancelada.";
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(
                ex,
                "Concurrencia al cambiar estado de cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "La cita fue modificada recientemente.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "DbUpdateException al cambiar estado de cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "Ocurrió un error al intentar actualizar el estado de la cita.";
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "SqlException al cambiar estado de cita Id={Id}. Number={Number}",
                IdCita,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al cambiar estado de cita Id={Id}",
                IdCita);

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
        [FromForm] string? MotivoCancelacion,
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

            // ------------------------------------------------------------
            // PERSISTENCIA — LÓGICA DELEGADA EN SERVICIO
            // CancelarAsync valida existencia + estado ya cancelado
            // y actualiza soft delete en una sola operación.
            // ------------------------------------------------------------

            var citaAnterior = await _citaService.ObtenerPorIdAsync(IdCita, ct);
            if (citaAnterior is null)
            {
                TempData["ErrorValidacion"] =
                    "La cita que intenta cancelar no existe.";

                return Redirect(returnUrlSafe);
            }
            bool ok;
            try
            {
                int? operadorId = int.TryParse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier),
                    out int userId)
                    ? userId
                    : null;
                string? ipActor = HttpContext.Connection.RemoteIpAddress?.ToString();
                ok = await _citaService.CancelarAsync(
                    IdCita,
                    MotivoCancelacion,
                    ct: ct,
                    idUsuarioActor: operadorId,
                    ipOrigen: ipActor);
            }
            catch (InvalidOperationException ioex)
                when (ioex.Message.Contains("cancelada",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorValidacion"] =
                    "La cita ya se encuentra cancelada.";

                return Redirect(returnUrlSafe);
            }

            if (!ok)
            {
                TempData["ErrorValidacion"] =
                    "La cita que intenta cancelar no existe.";

                return Redirect(returnUrlSafe);
            }

            TempData["MensajeExito"] =
                "La cita fue cancelada exitosamente.";
        }
        catch (InvalidOperationException ioex)
        {
            _logger.LogWarning(
                ioex,
                "EliminarCita: Validación de servicio fallida Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                string.IsNullOrWhiteSpace(ioex.Message)
                    ? "No se pudo cancelar la cita."
                    : ioex.Message;
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
            _logger.LogError(
                ex,
                "SqlException al cancelar cita Id={Id}. Number={Number}",
                IdCita,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error inesperado al cancelar cita Id={Id}",
                IdCita);

            TempData["ErrorValidacion"] =
                "Ocurrió un error inesperado. El incidente fue registrado.";
        }

        return Redirect(returnUrlSafe);
    }

    // ================================================================
    // HISTORIAL DE ESTADOS DE UNA CITA (API interna para modal)
    // ================================================================

    /// <summary>
    /// Devuelve el historial de cambios de estado de una cita ordenado
    /// cronológicamente. Consumido por AJAX desde la pestaña "Historial"
    /// del modal de detalles en st-adm-09-citas.
    /// </summary>
    [HttpGet]
    [Route("gestion-de-citas/historial-estados/{id:int}")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    public async Task<IActionResult> HistorialEstadosCita(
        int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { error = "Identificador de cita inválido." });

        // Ownership (RN-25): un Profesional solo puede consultar el historial de sus propias citas.
        if (User.IsInRole("Profesional"))
        {
            var citaHistorial = await _context.Citas.AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdCita == id, ct);
            if (citaHistorial is null)
                return NotFound(new { error = "Cita no encontrada." });

            bool esPropietarioHistorial =
                int.TryParse(User.FindFirst("IdProfesional")?.Value, out int idProfesionalHistorial) &&
                citaHistorial.IdProfesional == idProfesionalHistorial;

            if (!esPropietarioHistorial &&
                int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int idUsuarioHistorial))
            {
                esPropietarioHistorial = await _context.Profesionales.AnyAsync(
                    p => p.IdUsuario == idUsuarioHistorial && p.IdProfesional == citaHistorial.IdProfesional, ct);
            }

            if (!esPropietarioHistorial)
                return Forbid();
        }

        try
        {
            var historial = await _context.CitasHistorialEstado
                .AsNoTracking()
                .Where(h => h.IdCita == id)
                .Include(h => h.Estado)
                .Include(h => h.Usuario)
                .OrderBy(h => h.FechaCambio)
                .Select(h => new
                {
                    h.IdHistorial,
                    Estado = h.EstadoTexto,
                    h.Motivo,
                    FechaCambio = h.FechaCambio.ToString("dd/MM/yyyy HH:mm:ss"),
                    NombreUsuario = h.Usuario != null
                        ? (h.Usuario.Nombre + " " + (h.Usuario.Apellidos ?? "")).Trim()
                        : "Sistema"
                })
                .ToListAsync(ct);

            return Json(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al obtener historial de estados. IdCita={IdCita}", id);

            return StatusCode(500, new { error = "Error al cargar el historial." });
        }
    }

    // ================================================================
    // PANEL AUXILIAR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-citas/st-aux-01-panel-operativo")]
    [Route("gestion-de-citas/st-aux-01-panel-operativo/panel-operativo")]
    public async Task<IActionResult> Staux01PanelOperativo(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosDashboard(ct);

            var panelData = await _panelOperativoService.ObtenerAsync(ct);
            ViewData["TopProfesionales"] = panelData.TopProfesionales;

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

    // ================================================================
    // AGENDA APOYO
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-citas/st-aux-02-agenda-apoyo")]
    public async Task<IActionResult> Staux02AgendaApoyo(
        [FromQuery] int? editId,
        [FromQuery] DateTime? fecha,
        [FromQuery] DateTime? weekStart,
        CancellationToken ct = default)
    {
        try
        {
            ViewData["AgendaApoyoFecha"] = (weekStart ?? fecha ?? ColombiaClock.Today).Date;
            ViewData["AgendaApoyoSemana"] = weekStart.HasValue;
            ViewData["AgendaApoyoData"] =
                await ConstruirAgendaApoyoAsync(fecha, weekStart, ct);

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

    private async Task<AgendaApoyoViewModel> ConstruirAgendaApoyoAsync(
        DateTime? fecha,
        DateTime? weekStart,
        CancellationToken ct)
    {
        bool esSemana = weekStart.HasValue;
        var inicio = (weekStart ?? fecha ?? ColombiaClock.Today).Date;
        var fin = esSemana ? inicio.AddDays(7) : inicio.AddDays(1);

        var citasAgenda = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.EstadoCita)
            .Where(c => c.FechaHora >= inicio && c.FechaHora < fin)
            .OrderBy(c => c.FechaHora).ThenBy(c => c.IdCita)
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
                n.Contains("consulta") ||
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

                "cancelada" => "Cancelada",

                "no_asistida" or
                "no-show" or
                "no asistió"
                    => "No asistió",

                "confirmada" => "Confirmada",
                "programada" => "Programada",
                _ => "Pendiente"
            };

        var citas = citasAgenda.Select(
            c => new AgendaApoyoCitaItemViewModel
            {
                Id = c.IdCita,
                FechaHora = c.FechaHora,
                Activa = !EstadoCitaHelper.EsEstadoFinal(c.EstadoCita?.NombreEstado ?? c.Estado)
                    && !EstadoCitaHelper.EsEstadoCancelado(c.Estado),
                Fecha = c.FechaHora.ToString("ddd dd/MM", new System.Globalization.CultureInfo("es-CO")),
                Hora = c.FechaHora.ToString("HH:mm"),
                Paciente =
                    c.Paciente?.NombresCompleto ??
                    "Paciente sin datos",

                Profesional =
                    c.Profesional is not null
                        ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                        : "Sin asignar",

                Tipo =
                    InferirTipoCita(
                        c.Servicio?.Nombre),

                Alergia =
                    string.IsNullOrWhiteSpace(
                        c.Paciente?.Alergias)
                        ? null
                        : c.Paciente!.Alergias,

                Estado =
                    MapEstadoLabel(c.EstadoCita?.NombreEstado ?? c.Estado)
            })
            .ToList();

        return new AgendaApoyoViewModel
        {
            Fecha = inicio.ToString("yyyy-MM-dd"),
            FechaInicio =
                inicio.ToString(
                    "ddd d MMM yyyy",
                    new System.Globalization.CultureInfo("es-CO")),
            FechaFin = fin.AddDays(-1).ToString(
                "ddd d MMM yyyy",
                new System.Globalization.CultureInfo("es-CO")),
            Modo = esSemana ? "semana" : "dia",

            Citas = citas
        };
    }

    // ================================================================
    // HISTORIAL PARCIAL AUXILIAR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-pacientes/st-aux-05-historial-parcial")]
    public async Task<IActionResult> Staux05HistorialParcial(
        [FromQuery] int? editId,
        [FromQuery] int? pacienteId,
        CancellationToken ct = default)
    {
        try
        {
            await CargarDatosCitas(
                editId,
                "/gestion-de-pacientes/st-aux-05-historial-parcial",
                null,
                ct);

            int? pacienteSeleccionadoId = pacienteId;
            if (pacienteSeleccionadoId is null && editId is > 0)
            {
                pacienteSeleccionadoId = await _context.Citas
                    .AsNoTracking()
                    .Where(c => c.IdCita == editId.Value)
                    .Select(c => (int?)c.IdPaciente)
                    .FirstOrDefaultAsync(ct);
            }

            ViewData["HistorialParcialData"] =
                await ConstruirHistorialParcialAsync(
                    pacienteSeleccionadoId,
                    ct);

            return View(
                "~/Views/Gestion_De_Pacientes/st-aux-05-historial-parcial/historial-parcial.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Staux05HistorialParcial");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Pacientes/st-aux-05-historial-parcial/historial-parcial.cshtml");
        }
    }

    private async Task<object> ConstruirHistorialParcialAsync(
        int? pacienteId,
        CancellationToken ct)
    {
        const int limite = 3;

        var paciente = pacienteId is not null
            ? await _context.Pacientes
                .FirstOrDefaultAsync(
                    p => p.IdPaciente == pacienteId,
                    ct)
            : null;

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
                    antecedentesMedicos = "",
                    alergias = Array.Empty<string>(),
                    medicamentos = Array.Empty<string>(),
                    grupoSanguineo = "N/D",
                    ultimaActualizacion = ""
                },

                consultas = Array.Empty<object>(),
                tratamientos = Array.Empty<object>(),
                documentos = Array.Empty<object>(),
                limite
            };
        }

        string[] estadosAtendidos =
        [
            "atendida", "atendido", "completada", "completado",
            "realizada", "realizado", "finalizada", "finalizado"
        ];

        var consultasBase = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Where(
                c =>
                    c.IdPaciente == paciente.IdPaciente &&
                    c.FechaHora <= ColombiaClock.Now &&
                    estadosAtendidos.Contains(c.Estado.ToLower()))
            .OrderByDescending(c => c.FechaHora)
            .Take(limite)
            .Select(
                c => new
                {
                    id = c.IdCita,
                    fecha = c.FechaHora,

                    profesional =
                        c.Profesional != null
                            ? $"Dr(a). {(c.Profesional.Usuario != null && c.Profesional.Usuario.Nombre != "" ? c.Profesional.Usuario.Nombre : c.Profesional.Nombres)} {(c.Profesional.Usuario != null && c.Profesional.Usuario.Apellidos != "" ? c.Profesional.Usuario.Apellidos : c.Profesional.Apellidos)}"
                            : "Sin asignar",

                    diagnostico =
                        c.Notas ?? "",

                    procedimiento =
                        c.Servicio != null
                            ? c.Servicio.Nombre
                            : "Consulta"
                })
            .ToListAsync(ct);

        var consultaIds = consultasBase.Select(c => c.id).ToArray();
        var historiaIds = await _context.HistoriasClinicas
            .AsNoTracking()
            .Where(h => h.IdPaciente == paciente.IdPaciente)
            .Select(h => h.IdHistoria)
            .ToArrayAsync(ct);
        var consultasClinicas = historiaIds.Length == 0 || consultaIds.Length == 0
            ? []
            : await _context.ConsultasClinicas
                .AsNoTracking()
                .Include(c => c.Diagnosticos)
                .Include(c => c.Sintomas)
                .Where(c => c.IdHistoria != 0
                    && historiaIds.Contains(c.IdHistoria)
                    && c.IdCita.HasValue
                    && consultaIds.Contains(c.IdCita.Value))
                .ToListAsync(ct);
        var consultaClinicaPorCita = consultasClinicas
            .Where(c => c.IdCita.HasValue)
            .ToDictionary(c => c.IdCita!.Value);

        var notasPorCita = await _context.NotasClinicas
            .AsNoTracking()
            .Where(n => historiaIds.Contains(n.IdHistoria)
                && n.IdCita.HasValue
                && consultaIds.Contains(n.IdCita.Value))
            .OrderByDescending(n => n.Fecha)
            .ToListAsync(ct);

        var notaRecientePorCita = notasPorCita
            .GroupBy(n => n.IdCita.GetValueOrDefault())
            .ToDictionary(grupo => grupo.Key, grupo => grupo.First());
        var odontogramaPorCita = consultaIds.Length == 0 || historiaIds.Length == 0
            ? []
            : (await _context.RegistrosOdontograma
                .AsNoTracking()
                .Where(r => historiaIds.Contains(r.IdHistoria)
                    && r.IdCita.HasValue
                    && consultaIds.Contains(r.IdCita.Value))
                .OrderByDescending(r => r.FechaRegistro)
                .ToListAsync(ct))
                .GroupBy(r => r.IdCita!.Value)
                .ToDictionary(group => group.Key, group => group.Select(r => new
                {
                    fecha = r.FechaRegistro,
                    pieza = r.NumeroFdi,
                    nombrePieza = r.NombrePieza ?? "Pieza dental",
                    estado = r.Estado,
                    observacion = r.Observacion ?? ""
                }).ToList());
        var controlesPorCita = consultaIds.Length == 0
            ? new Dictionary<int, ControlPostoperatorio>()
            : await _context.ControlesPostoperatorios
                .AsNoTracking()
                .Where(c => consultaIds.Contains(c.IdCita))
                .ToDictionaryAsync(c => c.IdCita, ct);
        var documentos = historiaIds.Length == 0
            ? []
            : await _context.DocumentosClinicos
                .AsNoTracking()
                .Where(d => historiaIds.Contains(d.IdHistoria))
                .OrderByDescending(d => d.FechaSubida)
                .Select(d => new
                {
                    id = d.IdDocumento,
                    tipo = d.Tipo,
                    nombre = d.NombreOriginal,
                    fecha = d.FechaDocumento.HasValue
                        ? d.FechaDocumento.Value.ToString("yyyy-MM-dd")
                        : d.FechaSubida.ToString("yyyy-MM-dd"),
                    observacion = d.Observacion ?? "",
                    url = $"/historia-clinica/documentos-clinicos/descargar/{d.IdDocumento}"
                })
                .ToListAsync(ct);
        var tratamientos = historiaIds.Length == 0
            ? []
            : await _context.PlanesTratamiento
                .AsNoTracking()
                .Include(plan => plan.Servicio)
                .Where(plan => historiaIds.Contains(plan.IdHistoria))
                .OrderByDescending(plan => plan.ActualizadoEn)
                .Select(plan => new
                {
                    nombre = plan.Servicio != null ? plan.Servicio.Nombre : "Plan de tratamiento",
                    estado = plan.Estado,
                    pieza = plan.NumeroFdi ?? "",
                    observacion = plan.Observacion ?? "",
                    sesionesEstimadas = plan.SesionesEstimadas
                })
                .ToListAsync(ct);

        var consultas = consultasBase
            .Select(c =>
            {
                notaRecientePorCita.TryGetValue(c.id, out var nota);
                consultaClinicaPorCita.TryGetValue(c.id, out var consultaClinica);
                var diagnosticosClinicos = consultaClinica?.Diagnosticos
                    .OrderByDescending(d => d.EsPrincipal)
                    .Select(d => d.Descripcion)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .ToArray() ?? [];

                return new
                {
                    c.id,
                    c.fecha,
                    c.profesional,
                    motivoConsulta = consultaClinica?.MotivoConsulta ?? "",
                    enfermedadActual = consultaClinica?.EnfermedadActual ?? "",
                    antecedentesMedicos = consultaClinica?.AntecedentesMedicos ?? "",
                    sintomas = consultaClinica?.Sintomas
                        .OrderBy(s => s.Orden)
                        .Select(s => s.Descripcion)
                        .ToArray() ?? [],
                    habitos = consultaClinica?.Habitos ?? "",
                    alergias = consultaClinica?.Alergias ?? "",
                    hallazgos = consultaClinica?.Hallazgos ?? "",
                    odontogramaObservaciones = consultaClinica?.OdontogramaObservaciones ?? "",
                    examenesComplementarios = consultaClinica?.ExamenesComplementarios ?? "",
                    evolucionClinica = consultaClinica?.EvolucionClinica ?? "",
                    prescripcion = consultaClinica?.Prescripcion ?? "",
                    odontograma = odontogramaPorCita.TryGetValue(c.id, out var registrosOdontograma)
                        ? registrosOdontograma
                        : [],
                    controlPostoperatorio = controlesPorCita.TryGetValue(c.id, out var control)
                        ? new
                        {
                            estado = control.Status,
                            observaciones = control.Observaciones ?? "",
                            instruccionesJson = control.InstruccionesJson ?? ""
                        }
                        : null,
                    diagnostico = !string.IsNullOrWhiteSpace(nota?.Diagnostico)
                        ? nota.Diagnostico
                        : diagnosticosClinicos.Length > 0
                            ? string.Join("; ", diagnosticosClinicos)
                            : string.IsNullOrWhiteSpace(c.diagnostico)
                                ? "Sin notas clínicas registradas"
                                : c.diagnostico,
                    procedimiento = string.IsNullOrWhiteSpace(nota?.Procedimiento)
                        ? c.procedimiento
                        : nota.Procedimiento
                };
            })
            .ToList();

        return new
        {
            paciente = new
            {
                id = paciente.IdPaciente,
                nombre = paciente.NombresCompleto,
                tipoDoc = paciente.TipoDocumento,
                documento = paciente.Documento,
                antecedentesMedicos = paciente.AntecedentesMedicos ?? "",

                alergias =
                    string.IsNullOrWhiteSpace(
                        paciente.Alergias)
                        ? Array.Empty<string>()
                        : paciente.Alergias.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries),

                medicamentos =
                    string.IsNullOrWhiteSpace(
                        paciente.Medicamentos)
                        ? Array.Empty<string>()
                        : paciente.Medicamentos.Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries),

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
            tratamientos,
            documentos,
            limite
        };
    }

    // ================================================================
    // ASISTENCIA PROCEDIMIENTO
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-citas/st-aux-06-asistencia-procedi")]
    public async Task<IActionResult> Staux06AsistenciaProcedi(
        [FromQuery] int? editId,
        [FromQuery] int? citaId,
        CancellationToken ct = default)
    {
        try
        {
            DateTime inicioDia = ColombiaClock.Today;
            DateTime finDia = inicioDia.AddDays(1);
            var citasElegibles = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.EstadoCita)
                .Where(c =>
                    c.FechaHora >= inicioDia &&
                    c.FechaHora < finDia)
                .OrderBy(c => c.FechaHora)
                .ToListAsync(ct);
            var citasAsistencia = citasElegibles
                .Where(c => NormalizarEstado(c.EstadoCita?.NombreEstado ?? c.Estado) is
                    "programada" or "confirmada" or "en_sala_de_espera" or "en_proceso")
                .ToList();
            ViewData["CitasAsistenciaSelector"] = citasAsistencia;

            ViewData["AsistenciaProcedData"] =
                await ConstruirAsistenciaProcedimientoAsync(
                    citaId,
                    citasAsistencia,
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

    private async Task<AsistenciaProcedimientoViewModel> ConstruirAsistenciaProcedimientoAsync(
        int? citaId,
        IReadOnlyCollection<Cita> citasAsistencia,
        CancellationToken ct)
    {
        Cita? cita = null;
        if (citaId.HasValue)
        {
            var seleccionada = citasAsistencia.FirstOrDefault(c => c.IdCita == citaId.Value);
            if (seleccionada is not null)
            {
                cita = await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Servicio)
                    .Include(c => c.Profesional)
                    .Include(c => c.Consultorio)
                    .Include(c => c.EstadoCita)
                    .FirstOrDefaultAsync(c =>
                        c.IdCita == seleccionada.IdCita &&
                        c.FechaHora >= ColombiaClock.Today &&
                        c.FechaHora < ColombiaClock.Today.AddDays(1), ct);
            }
        }
        else
        {
            DateTime ahora = ColombiaClock.Now;
            var enCursoHoy = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Servicio)
                .Include(c => c.Profesional)
                .Include(c => c.Consultorio)
                .Include(c => c.EstadoCita)
                .Where(c =>
                    c.FechaHora >= ColombiaClock.Today &&
                    c.FechaHora < ColombiaClock.Today.AddDays(1) &&
                    c.FechaHora <= ahora)
                .OrderByDescending(c => c.FechaHora)
                .ToListAsync(ct);
            cita = enCursoHoy.FirstOrDefault(c =>
                NormalizarEstado(c.EstadoCita?.NombreEstado ?? c.Estado) == "en_proceso");
        }

        if (cita is null)
        {
            return new AsistenciaProcedimientoViewModel
            {
                CitaId = null,
                Paciente = "Sin procedimiento asignado",
                Procedimiento = "",
                Profesional = "",
                Consultorio = "",
                ConsultorioId = null,
                Estado = null,
                EstadoNormalizado = null,
                InicioProcedimiento = null,
                Alergia = null,
                Antecedentes = null
            };
        }

        string estadoCita = cita.EstadoCita?.NombreEstado ?? cita.Estado;
        var asistencia = await _context.AsistenciasProcedimiento
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdCita == cita.IdCita, ct);
        DateTime? inicioProcedimiento = asistencia?.Inicio;
        if (inicioProcedimiento is null &&
            NormalizarEstado(estadoCita) == "en_proceso")
        {
            inicioProcedimiento = await _context.CitasHistorialEstado
                .AsNoTracking()
                .Where(h => h.IdCita == cita.IdCita &&
                            (h.EstadoTexto.ToLower() == "en consulta" ||
                             h.EstadoTexto.ToLower() == "en proceso" ||
                             h.EstadoTexto.ToLower() == "en_proceso"))
                .OrderByDescending(h => h.FechaCambio)
                .Select(h => (DateTime?)h.FechaCambio)
                .FirstOrDefaultAsync(ct);
        }

        return new AsistenciaProcedimientoViewModel
        {
            CitaId = cita.IdCita,
            Estado = estadoCita,
            EstadoNormalizado = NormalizarEstado(estadoCita),
            InicioProcedimiento = inicioProcedimiento,

            Paciente =
                cita.Paciente?.NombresCompleto ??
                "Paciente sin datos",

            Procedimiento =
                cita.Servicio?.Nombre ??
                "Procedimiento",

            Profesional =
                cita.Profesional is not null
                    ? $"Dr(a). {cita.Profesional.Nombres} {cita.Profesional.Apellidos}"
                    : "Sin asignar",

            Consultorio =
                cita.Consultorio?.Nombre ??
                "Sin asignar",

            ConsultorioId = cita.IdConsultorio,

            Alergia =
                string.IsNullOrWhiteSpace(
                    cita.Paciente?.Alergias)
                    ? null
                    : cita.Paciente!.Alergias,

            Antecedentes =
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
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-citas/st-aux-09-estado-consultorio")]
    public async Task<IActionResult> Staux09EstadoConsultorio(
        [FromQuery] int? consultorioId,
        [FromQuery] int? citaId,
        CancellationToken ct = default)
    {
        try
        {
            DateTime hoy = ColombiaClock.Today;
            DateTime manana = hoy.AddDays(1);
            Cita? citaSeleccionada = citaId is > 0
                ? await _context.Citas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.IdCita == citaId.Value &&
                        c.FechaHora >= hoy &&
                        c.FechaHora < manana &&
                        c.Estado.ToLower() != "cancelada" &&
                        c.Estado.ToLower() != "cancelado", ct)
                : null;

            if (citaId is > 0 && citaSeleccionada is null)
                return NotFound();

            // Una cita seleccionada aporta consultorio cuando la URL no lo especifica.
            if (!consultorioId.HasValue && citaSeleccionada?.IdConsultorio is > 0)
                consultorioId = citaSeleccionada.IdConsultorio;

            // Si no hay consultorio en la cita ni en la URL, buscar la preferencia del usuario.
            if (!consultorioId.HasValue)
            {
                int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedUserId)
                    ? parsedUserId
                    : null;

                if (userId.HasValue)
                {
                    var preferencia = await _context.UsuariosPreferenciasConsultorio
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdUsuario == userId.Value, ct);

                    if (preferencia != null)
                    {
                        consultorioId = preferencia.IdConsultorio;
                    }
                }
            }

            var data = await ConstruirEstadoConsultorioAsync(consultorioId, citaId, ct);
            ViewData["EstadoConsultorioData"] = data;

            return View(
                "~/Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Staux09EstadoConsultorio");
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View(
                "~/Views/Gestion_De_Citas/st-aux-09-estado-consultorio/estado-consultorio.cshtml");
        }
    }

    private async Task<object> ConstruirEstadoConsultorioAsync(
        int? consultorioId,
        int? citaId,
        CancellationToken ct)
    {
        // Si no se pasa consultorioId, tomar el primer consultorio activo
        var consultorio = consultorioId.HasValue
            ? await _context.Consultorios
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdConsultorio == consultorioId.Value, ct)
            : await _context.Consultorios
                .AsNoTracking()
                .OrderBy(c => c.IdConsultorio)
                .FirstOrDefaultAsync(ct);

        if (consultorio is null)
        {
            return new
            {
                consultorioId = (int?)null,
                nombre = "Sin consultorio asignado",
                ubicacion = "",
                ultimaActualizacion = "",
                estadoActual = "disponible",
                citas = Array.Empty<object>(),
                citaSeleccionadaId = citaId,
                historial = Array.Empty<object>()
            };
        }

        DateTime inicioDia = ColombiaClock.Today;
        DateTime finDia = inicioDia.AddDays(1);
        var citasDelConsultorio = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(c =>
                c.FechaHora >= inicioDia &&
                c.FechaHora < finDia &&
                c.IdConsultorio == consultorio.IdConsultorio &&
                c.Estado.ToLower() != "cancelada" &&
                c.Estado.ToLower() != "cancelado")
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        var citas = citasDelConsultorio
            .Select(c => new
            {
                id = c.IdCita,
                hora = c.FechaHora.ToString("HH:mm"),
                paciente = c.Paciente == null ? "Paciente" : c.Paciente.NombresCompleto,
                profesional = c.Profesional == null
                    ? "Sin asignar"
                    : $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}",
                servicio = c.Servicio == null ? "Sin servicio" : c.Servicio.Nombre,
                estado = c.Estado
            })
            .ToList();

        var estadoOperativo = await _context.EstadosOperativosConsultorio
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdConsultorio == consultorio.IdConsultorio, ct);

        var historialDb = await _context.ConsultoriosHistorial
            .AsNoTracking()
            .Include(h => h.Usuario)
            .Include(h => h.EstadoAnterior)
            .Include(h => h.EstadoNuevo)
            .Where(h => h.IdConsultorio == consultorio.IdConsultorio)
            .OrderByDescending(h => h.FechaCambio)
            .Take(5)
            .ToListAsync(ct);

        var historial = historialDb
            .Select(c => new
            {
                time = c.FechaCambio.ToString("o"),
                user = c.Usuario == null ? "Usuario no disponible" : $"{c.Usuario.Nombre} {c.Usuario.Apellidos}",
                idEstadoAnterior = c.IdEstadoAnterior,
                estadoAnterior = c.EstadoAnterior == null ? "" : c.EstadoAnterior.Nombre,
                idEstadoNuevo = c.IdEstadoNuevo,
                estadoNuevo = c.EstadoNuevo == null ? c.Estado : c.EstadoNuevo.Nombre,
                detail = c.Motivo ?? ""
            })
            .ToList();

        object[] checklist = [];
        if (estadoOperativo is not null && !string.IsNullOrWhiteSpace(estadoOperativo.ChecklistJson))
        {
            try
            {
                checklist = JsonSerializer.Deserialize<object[]>(estadoOperativo.ChecklistJson) ?? [];
            }
            catch
            {
                checklist = [];
            }
        }

        return new
        {
            consultorioId = consultorio.IdConsultorio,
            nombre = consultorio.Nombre ?? $"Consultorio {consultorio.IdConsultorio}",
            ubicacion = consultorio.Ubicacion ?? "",
            ultimaActualizacion = estadoOperativo?.ActualizadoEn.ToString("o") ?? historial.FirstOrDefault()?.time ?? "",
            estadoActual = consultorio.Estado ?? "disponible",
            citas,
            citaSeleccionadaId = citaId,
            checklist,
            observaciones = estadoOperativo?.Observaciones,
            historial = historial.Cast<object>().ToArray()
        };
    }

    // ================================================================
    // CITAS FINALIZADAS
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Recepcionista,Administrador")]
    [Route("gestion-de-citas/st-aux-10-citas-finalizadas")]
    public async Task<IActionResult> Staux10CitasFinalizadas(
        [FromQuery] int? editId,
        CancellationToken ct = default,
        [FromQuery] DateOnly? fecha = null)
    {
        var hoy = DateOnly.FromDateTime(ColombiaClock.Today);
        var jornada = fecha.HasValue && fecha.Value <= hoy ? fecha.Value : hoy;
        ViewData["FechaFinalizadas"] = jornada;
        ViewData["HoyFinalizadas"] = hoy;
        try
        {
            ViewData["CitasFinalizadasData"] =
                await ConstruirCitasFinalizadasAsync(jornada, ct);

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
        DateOnly jornada, CancellationToken ct)
    {
        var inicio = jornada.ToDateTime(TimeOnly.MinValue);
        var fin = jornada.ToDateTime(TimeOnly.MaxValue);

        static string MapEstadoLabel(string estado) =>
            EstadoCitaHelper.Normalize(estado) switch
            {
                "atendida" or
                "completada" or
                "realizada"
                    => "Atendida",

                "cancelada"
                    => "Cancelada",

                "no_asistida" or
                "no_asistio" or "no_asistió"
                    => "No asistió",

                _ => "Pendiente"
            };

        var citasHoy = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.EstadoCita)
            .Where(c => c.FechaHora >= inicio && c.FechaHora <= fin)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        var citas =
            citasHoy
                .Select(
                    c => new
                    {
                        id = c.IdCita,
                        fecha = c.FechaHora.ToString("dd/MM/yyyy"),
                        hora =
                            c.FechaHora.ToString("HH:mm"),
                        motivoCancelacion = c.MotivoCancelacion,

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
                            MapEstadoLabel(c.EstadoCita?.NombreEstado ?? c.Estado)
                    })
                .Where(c => c.estado != "Pendiente")
                .ToList();

        string? ultimaJornada = null;
        if (citas.Count == 0)
        {
            var estadosResumen = new[]
            {
                "atendida", "completada", "realizada", "finalizada", "cancelada", "cancelado",
                "no asistio", "no asistió", "no_asistida", "no_asistio", "no_asistió", "no-show", "no_show"
            };
            var ultimaFecha = await _context.Citas.AsNoTracking()
                .Where(c => c.FechaHora < inicio && estadosResumen.Contains(
                    ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : null) ?? c.Estado ?? "").Trim().ToLower()))
                .OrderByDescending(c => c.FechaHora)
                .Select(c => (DateTime?)c.FechaHora)
                .FirstOrDefaultAsync(ct);
            ultimaJornada = ultimaFecha?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return new
        {
            ultimaJornada,
            fechaResumen = jornada.ToString("yyyy-MM-dd"),
            citas
        };
    }

    // ================================================================
    // AGENDA PROFESIONAL
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Profesional,Administrador")]
    [Route("gestion-de-citas/st-odo-02-agenda")]
    public async Task<IActionResult> Stodo02Agenda(
        [FromQuery] DateTime? weekStart,
        [FromQuery] int? officeId,
        CancellationToken ct = default)
    {
        try
        {
            _antiforgery.GetAndStoreTokens(HttpContext);

            // Obtener el ID del profesional desde los claims del usuario autenticado
            int? professionalId = null;
            if (User.IsInRole("Profesional"))
            {
                var idProfesionalClaim = User.FindFirst("IdProfesional")?.Value;
                if (int.TryParse(idProfesionalClaim, out int idProf))
                {
                    professionalId = idProf;
                }
                else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario))
                {
                    professionalId = await _context.Profesionales
                        .AsNoTracking()
                        .Where(p => p.IdUsuario == idUsuario)
                        .Select(p => (int?)p.IdProfesional)
                        .FirstOrDefaultAsync(ct);
                }

                if (!professionalId.HasValue)
                    return Forbid();
            }

            var model = await _agendaService.ObtenerAgendaAsync(
                weekStart,
                professionalId,
                officeId,
                ct);

            // ═══════════════════════════════════════════════════════════════
            // MEJORAS: ESTADÍSTICAS ADICIONALES PARA AGENDA PROFESIONAL
            // ═══════════════════════════════════════════════════════════════

            if (professionalId.HasValue)
            {
                var inicioSemana = model.WeekStart;
                var finSemana = inicioSemana.AddDays(7);

                // 1. CONTEO DE CITAS POR ESTADO (para filtros)
                var citasSemana = await _context.Citas
                    .AsNoTracking()
                    .Where(c =>
                        c.IdProfesional == professionalId.Value &&
                        c.FechaHora >= inicioSemana &&
                        c.FechaHora < finSemana)
                    .ToListAsync(ct);

                ViewData["TotalCitasSemana"] = citasSemana.Count;
                ViewData["CitasProgramadas"] = citasSemana.Count(c =>
                    NormalizarEstado(c.Estado) == "programada");
                ViewData["CitasConfirmadas"] = citasSemana.Count(c =>
                    NormalizarEstado(c.Estado) == "confirmada");
                ViewData["CitasAtendidas"] = citasSemana.Count(c =>
                    NormalizarEstado(c.Estado) == "atendida");
                ViewData["CitasCanceladas"] = citasSemana.Count(c =>
                    NormalizarEstado(c.Estado) == "cancelada");

                // 2. PRÓXIMAS 3 CITAS (para vista rápida)
                var proximasCitas = citasSemana
                    .Where(c => c.FechaHora >= ColombiaClock.Now &&
                               NormalizarEstado(c.Estado) is "programada" or "confirmada")
                    .OrderBy(c => c.FechaHora)
                    .Take(3)
                    .ToList();

                ViewData["ProximasCitasRapidas"] = proximasCitas;

                // 3. HORAS MÁS OCUPADAS (para insights)
                var horasOcupadas = citasSemana
                    .Where(c => NormalizarEstado(c.Estado) != "cancelada")
                    .GroupBy(c => c.FechaHora.Hour)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => new { Hora = g.Key, Cantidad = g.Count() })
                    .ToList();

                ViewData["HorasMasOcupadas"] = horasOcupadas;

                // 4. PACIENTES FRECUENTES DE LA SEMANA
                var pacientesFrecuentes = citasSemana
                    .Where(c => c.IdPaciente > 0)
                    .GroupBy(c => c.IdPaciente)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { IdPaciente = g.Key, Cantidad = g.Count() })
                    .OrderByDescending(x => x.Cantidad)
                    .ToList();

                ViewData["PacientesFrecuentesCant"] = pacientesFrecuentes.Count;
            }

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml",
                model);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Solicitud cancelada Stodo02Agenda");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos en Stodo02Agenda");

            TempData["ErrorValidacion"] =
                "Error al consultar datos. Intente nuevamente.";

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (SqlException ex)
        {
            _logger.LogError(
                ex,
                "Error SQL {Number} en Stodo02Agenda",
                ex.Number);

            TempData["ErrorValidacion"] =
                "Servicio temporalmente no disponible. Intente en unos minutos.";

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error crítico cargando Stodo02Agenda");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-odo-02-agenda/index.cshtml",
                new AgendaViewModel());
        }
    }

    // ================================================================
    // MIS CITAS PACIENTE
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador")]
    [Route("gestion-de-citas/st-pac-01-mis-citas")]
    public async Task<IActionResult> Stpac01MisCitas(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            _antiforgery.GetAndStoreTokens(HttpContext);
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int idUsuarioPaciente);
            var perfilCompleto = idUsuarioPaciente > 0 && await _perfilPacienteService.PerfilCompletoAsync(idUsuarioPaciente, ct);
            ViewData["PerfilPacienteCompleto"] = perfilCompleto;

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
    [Authorize(Roles = "Paciente,Administrador")]
    [Route("gestion-de-citas/st-pac-03-notificaciones")]
    public async Task<IActionResult> Stpac03Notificaciones(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            _antiforgery.GetAndStoreTokens(HttpContext);
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

    private Task<SmileTrack_MVC.Models.ViewModels.GestionDeCitas.PacienteNotificacionesViewModel>
        ConstruirNotificacionesPacienteAsync(CancellationToken ct)
    {
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? idUsuario = int.TryParse(userIdStr, out int uid) ? uid : null;
        return _pacienteNotificacionQueryService.ObtenerNotificacionesAsync(idUsuario, ct);
    }

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador")]
    [Route("api/notificaciones")]
    public async Task<IActionResult> ObtenerNotificacionesApi(CancellationToken ct = default)
    {
        var data = await ConstruirNotificacionesPacienteAsync(ct);
        return Ok(new { success = true, data });
    }

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador")]
    [Route("api/notificaciones/unread-count")]
    public async Task<IActionResult> ObtenerConteoNoLeidasApi(CancellationToken ct = default)
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out int idUsuario)) return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        int unreadCount = await _pacienteNotificacionQueryService.ObtenerConteoNoLeidasAsync(idUsuario, ct);
        return Ok(new { success = true, unreadCount });
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    [Route("api/notificaciones/{id:int}/leida")]
    public async Task<IActionResult> MarcarNotificacionLeida(int id, CancellationToken ct = default)
    {
        if (id <= 0) return BadRequest(new { success = false, message = "Identificador inválido." });

        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out int idUsuario)) return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        int? idPaciente = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(ct);
        if (!idPaciente.HasValue) return Forbid();

        var ahora = ColombiaClock.Now;
        int? idCita = null;
        var notif = await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.IdNotificacion == id && n.IdPaciente == idPaciente.Value, ct);

        if (notif is not null)
        {
            notif.FechaLectura = ahora;
            notif.Estado = "leida";
            if (notif.IdCita.HasValue)
            {
                idCita = await _context.Citas.AsNoTracking()
                    .Where(c => c.IdCita == notif.IdCita.Value && c.IdPaciente == idPaciente.Value)
                    .Select(c => (int?)c.IdCita)
                    .FirstOrDefaultAsync(ct);
            }
        }
        else
        {
            idCita = await _context.Citas.AsNoTracking()
                .Where(c => c.IdCita == id && c.IdPaciente == idPaciente.Value)
                .Select(c => (int?)c.IdCita)
                .FirstOrDefaultAsync(ct);

            if (!idCita.HasValue)
                return NotFound(new { success = false, message = "No se encontró la notificación." });
        }

        if (idCita.HasValue)
        {
            var lectura = await _context.NotificacionesLeidas
                .FirstOrDefaultAsync(n => n.IdPaciente == idPaciente.Value && n.IdCita == idCita.Value, ct);
            if (lectura is null)
            {
                _context.NotificacionesLeidas.Add(new NotificacionLeida
                {
                    IdPaciente = idPaciente.Value,
                    IdCita = idCita.Value,
                    FechaLectura = ahora
                });
            }
            else
            {
                lectura.FechaLectura = ahora;
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, idCita = id, leida = true });
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    [Route("api/notificaciones/citas/{id:int}/leida")]
    public async Task<IActionResult> MarcarNotificacionCitaLeida(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out int idUsuario))
            return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        int? idPaciente = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(ct);
        if (!idPaciente.HasValue)
            return Forbid();

        bool perteneceAlPaciente = await _context.Citas.AsNoTracking()
            .AnyAsync(c => c.IdCita == id && c.IdPaciente == idPaciente.Value, ct);
        if (!perteneceAlPaciente)
            return NotFound(new { success = false, message = "Cita no encontrada." });

        var ahora = ColombiaClock.Now;
        var lectura = await _context.NotificacionesLeidas
            .FirstOrDefaultAsync(n => n.IdPaciente == idPaciente.Value && n.IdCita == id, ct);
        if (lectura is null)
        {
            _context.NotificacionesLeidas.Add(new NotificacionLeida
            {
                IdPaciente = idPaciente.Value,
                IdCita = id,
                FechaLectura = ahora
            });
        }
        else
        {
            lectura.FechaLectura = ahora;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, idCita = id, leida = true });
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    [Route("api/notificaciones/leidas")]
    public async Task<IActionResult> MarcarNotificacionesLeidas([FromBody] List<int>? ids, CancellationToken ct = default)
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out int idUsuario)) return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        int? idPaciente = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(ct);
        if (!idPaciente.HasValue) return Forbid();

        var requestedIds = ids?.Where(id => id > 0).Distinct().ToList();
        var dbNotifs = await _context.Notificaciones
            .Where(n => n.IdPaciente == idPaciente.Value &&
                        (requestedIds == null || requestedIds.Contains(n.IdNotificacion)))
            .ToListAsync(ct);
        var notificationIds = dbNotifs.Select(n => n.IdNotificacion).ToHashSet();
        var ahora = ColombiaClock.Now;
        foreach (var notification in dbNotifs)
        {
            notification.FechaLectura = ahora;
            notification.Estado = "leida";
        }

        var requestedAppointmentIds = requestedIds?
            .Where(id => !notificationIds.Contains(id))
            .ToList();
        IQueryable<Cita> citasQuery = _context.Citas.AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente.Value);
        if (requestedAppointmentIds is not null)
            citasQuery = citasQuery.Where(c => requestedAppointmentIds.Contains(c.IdCita));

        var citaIds = await citasQuery.Select(c => c.IdCita).ToListAsync(ct);
        var existingReads = await _context.NotificacionesLeidas
            .Where(read => read.IdPaciente == idPaciente.Value && citaIds.Contains(read.IdCita))
            .ToListAsync(ct);
        var existingReadIds = existingReads.Select(read => read.IdCita).ToHashSet();
        foreach (var read in existingReads)
            read.FechaLectura = ahora;

        _context.NotificacionesLeidas.AddRange(citaIds
            .Where(idCita => !existingReadIds.Contains(idCita))
            .Select(idCita => new NotificacionLeida
            {
                IdPaciente = idPaciente.Value,
                IdCita = idCita,
                FechaLectura = ahora
            }));

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, count = dbNotifs.Count + citaIds.Count });
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    [Route("api/notificaciones/{id:int}")]
    public async Task<IActionResult> EliminarNotificacion(int id, CancellationToken ct = default)
    {
        if (id <= 0) return BadRequest(new { success = false, message = "Identificador inválido." });

        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out int idUsuario)) return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        int? idPaciente = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(ct);
        if (!idPaciente.HasValue) return Forbid();

        var notif = await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.IdNotificacion == id && n.IdPaciente == idPaciente.Value, ct);
        if (notif is null)
            return NotFound(new { success = false, message = "Notificación no encontrada." });

        notif.Estado = "eliminada";
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id });
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-pacientes/st-aux-03-preparacion-consulta")]
    public async Task<IActionResult> Staux03PreparacionConsulta(
        [FromQuery] int? citaId,
        CancellationToken ct = default)
    {
        var ahora = ColombiaClock.Now;
        var inicioDia = ahora.Date;
        var finDia = inicioDia.AddDays(1);

        var citasDelDia = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora >= inicioDia && c.FechaHora < finDia)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        var citasActivas = citasDelDia
            .Where(c => !EstadoCitaHelper.EsEstadoCancelado(c.Estado))
            .ToList();

        var cita = citaId.HasValue
            ? citasActivas.FirstOrDefault(c => c.IdCita == citaId.Value)
            : citasActivas.FirstOrDefault(c => c.FechaHora >= ahora) ?? citasActivas.LastOrDefault();

        if (citaId.HasValue && cita is null)
            return NotFound();

        var viewModel = new PreparacionConsultaViewModel
        {
            CitasDelDia = citasActivas.Select(c => new CitaDelDiaDto
            {
                Id = c.IdCita,
                Paciente = c.Paciente?.NombresCompleto ?? "Paciente sin nombre",
                Hora = c.FechaHora.ToString("hh:mm tt"),
                Servicio = c.Servicio?.Nombre ?? "Sin servicio",
                Estado = c.Estado,
                EsActual = c.IdCita == cita?.IdCita
            }).ToList()
        };

        if (cita is not null)
        {
            var paciente = cita.Paciente;
            viewModel.CitaId = cita.IdCita;
            viewModel.CitaFechaHora = cita.FechaHora;
            viewModel.CitaEstado = cita.Estado;
            viewModel.CitaNotas = cita.Notas ?? cita.MotivoConsulta;
            viewModel.MinutosHastaCita = (int)(cita.FechaHora - ahora).TotalMinutes;
            viewModel.PacienteId = paciente?.IdPaciente;
            viewModel.PacienteNombre = paciente?.NombresCompleto ?? "Sin paciente asignado";
            viewModel.PacienteDocumento = paciente?.Documento;
            viewModel.GrupoSanguineo = paciente?.GrupoSanguineo;
            viewModel.Alergias = paciente?.Alergias;
            viewModel.AlergiasLista = string.IsNullOrWhiteSpace(paciente?.Alergias)
                ? []
                : paciente.Alergias.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            viewModel.AntecedentesMedicos = paciente?.AntecedentesMedicos;
            viewModel.ProfesionalNombre = cita.Profesional?.NombreProfesional ?? "Sin asignar";
            viewModel.ServicioNombre = cita.Servicio?.Nombre ?? "Sin especificar";
            viewModel.ConsultorioNombre = cita.Consultorio?.Nombre ?? "Sin consultorio";

            if (paciente is not null)
            {
                viewModel.AlergiasEstructuradas = await _context.AlergiasPaciente
                    .AsNoTracking()
                    .Where(a => a.IdPaciente == paciente.IdPaciente && a.Activa)
                    .OrderBy(a => a.Sustancia)
                    .Select(a => new AlergiaClinicaViewModel
                    {
                        Sustancia = a.Sustancia,
                        Tipo = a.Tipo,
                        Severidad = a.Severidad,
                        Reaccion = a.Reaccion
                    })
                    .ToListAsync(ct);

                var historiaId = await _context.HistoriasClinicas
                    .AsNoTracking()
                    .Where(h => h.IdPaciente == paciente.IdPaciente && h.Activa)
                    .OrderByDescending(h => h.FechaApertura)
                    .Select(h => (int?)h.IdHistoria)
                    .FirstOrDefaultAsync(ct);

                if (historiaId.HasValue)
                {
                    viewModel.CodigoHC = $"HC-{historiaId.Value:D6}";
                    var ultimaNota = await _context.NotasClinicas
                        .AsNoTracking()
                        .Include(n => n.Profesional)
                            .ThenInclude(p => p!.Usuario)
                        .Where(n => n.IdHistoria == historiaId.Value)
                        .OrderByDescending(n => n.Fecha)
                        .FirstOrDefaultAsync(ct);

                    if (ultimaNota is not null)
                    {
                        var textoNota = new[] { ultimaNota.Diagnostico, ultimaNota.Procedimiento }
                            .Where(texto => !string.IsNullOrWhiteSpace(texto));
                        viewModel.UltimaNotaClinica = string.Join(Environment.NewLine, textoNota);
                        viewModel.FechaUltimaNota = ultimaNota.Fecha;
                        viewModel.ProfesionalUltimaNota = ultimaNota.Profesional?.NombreProfesional;
                    }
                }
            }
        }

        ViewData["SidebarPartial"] = "_SidebarAuxiliar";
        ViewData["UseGestionModulesDesignSystem"] = true;
        ViewData["Title"] = "Preparación de Consulta";
        return View(
            "~/Views/Gestion_De_Pacientes/st-aux-03-preparacion-consulta/preparacion.cshtml",
            viewModel);
    }

    // ================================================================
    // DASHBOARD RECEPCIÓN
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Recepcionista,Administrador")]
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

            ViewData["DashboardRecData"] =
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

    [HttpGet]
    [Authorize(Roles = "Recepcionista,Administrador")]
    [Route("api/citas/proximas")]
    public async Task<IActionResult> ObtenerProximasCitasRecepcion(
        [FromQuery] int ventanaMinutos = 30,
        CancellationToken ct = default)
    {
        ventanaMinutos = Math.Clamp(ventanaMinutos, 1, 120);
        var ahora = ColombiaClock.Now;
        var limite = ahora.AddMinutes(ventanaMinutos);

        var citasEnVentana = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora > ahora && c.FechaHora <= limite)
            .OrderBy(c => c.FechaHora)
            .ToListAsync(ct);

        var proximas = citasEnVentana
            .Where(c => !EstadoCitaHelper.EsEstadoCancelado(c.Estado))
            .Select(c => new
            {
                hora = c.FechaHora.ToString("hh:mm tt"),
                fechaIso = c.FechaHora.ToString("yyyy-MM-ddTHH:mm"),
                texto = $"{(c.Paciente == null ? "Paciente sin datos" : c.Paciente.Nombres + " " + c.Paciente.Apellidos)} - " +
                    $"{(c.Servicio == null ? "Consulta" : c.Servicio.Nombre)} - " +
                    $"{(c.Profesional == null ? "Sin asignar" : "Dr(a). " + c.Profesional.Nombres + " " + c.Profesional.Apellidos)}" +
                    $"{(c.Consultorio == null ? "" : " - " + c.Consultorio.Nombre)}"
            })
            .ToList();

        return Ok(new { success = true, proximasCitas = proximas });
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista,Administrador")]
    [Route("api/citas/dashboard-resumen")]
    public async Task<IActionResult> ObtenerResumenDashboardRecepcion(
        CancellationToken ct = default)
    {
        return Ok(await ConstruirDashboardRecepcionAsync(ct));
    }

    private async Task<object> ConstruirDashboardRecepcionAsync(CancellationToken ct)
    {
        var dashboard = await _citasDashboardService.ObtenerDashboardRecepcionAsync(
            ColombiaClock.Today, ct);
        var ahora = ColombiaClock.Now;
        var estadosProgramados = new[] { "agendada", "programada", "programado", "confirmada", "confirmado" };
        var siguienteCita = dashboard.Stats.CitasHoy == 0
            ? await _context.Citas.AsNoTracking()
                .Where(c => c.FechaHora > ahora && estadosProgramados.Contains(
                    (c.EstadoCita == null ? c.Estado : c.EstadoCita.NombreEstado).Trim().ToLower()))
                .OrderBy(c => c.FechaHora).ThenBy(c => c.IdCita)
                .Select(c => new
                {
                    id = c.IdCita,
                    fecha = c.FechaHora,
                    profesional = c.Profesional == null
                        ? "Sin profesional asignado"
                        : c.Profesional.Nombres + " " + c.Profesional.Apellidos
                })
                .FirstOrDefaultAsync(ct)
            : null;

        return new
        {
            success = true,
            appointments = dashboard.Appointments,
            fechaHoraTexto = dashboard.FechaHoraTexto,
            horaActualIso = dashboard.HoraActualIso,
            horaActualTexto = dashboard.HoraActualTexto,
            stats = dashboard.Stats,
            alertas = dashboard.Alertas,
            siguienteCita,
            proximasCitas = dashboard.ProximasCitas
        };
    }

    // ================================================================
    // GESTIÓN RECEPCIÓN
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Recepcionista,Administrador")]
    [Route("gestion-de-citas/st-rec-03-gestion-citas")]
    public async Task<IActionResult> Strec03GestionCitas(
        [FromQuery] int? editId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        [FromQuery] string? profesional = null,
        [FromQuery] string? fecha = null,
        CancellationToken ct = default,
        [FromQuery] string? vista = null,
        [FromQuery] int? verId = null,
        [FromQuery] DateOnly? desde = null,
        [FromQuery] DateOnly? hasta = null)
    {
        try
        {
            vista = vista is "hoy" or "historial" or "todas" ? vista : "proximas";
            ViewData["VistaRecepcion"] = vista;
            ViewData["VerCitaId"] = verId;
            ViewData["CitasDesde"] = desde?.ToString("yyyy-MM-dd");
            ViewData["CitasHasta"] = hasta?.ToString("yyyy-MM-dd");
            foreach (var accion in new[] { "agendar", "reagendar", "cancelar" })
                ViewData[$"Recepcion_{accion}"] = User.IsInRole("Administrador")
                    || await PermisoReagendamiento.PermitidoAsync(_context, ct, accion);

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
                ct, vista, verId, desde, hasta);

            await CargarKpisRecepcionHoy(ct);

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
    [Authorize(Roles = "Recepcionista,Administrador")]
    [Route("gestion-de-citas/st-rec-05-recordatorios")]
    public async Task<IActionResult> Strec05Recordatorios(
        [FromQuery] int? editId,
        CancellationToken ct = default)
    {
        try
        {
            _antiforgery.GetAndStoreTokens(HttpContext);
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-rec-05-recordatorios",
                null,
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml",
                await ConstruirRecordatoriosAsync(ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error Strec05Recordatorios.");

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-rec-05-recordatorios/index.cshtml",
                new RecordatoriosViewModel());
        }
    }

    private async Task<RecordatoriosViewModel> ConstruirRecordatoriosAsync(
        CancellationToken ct)
    {
        var hoy = ColombiaClock.Today;
        var manana = hoy.AddDays(1);
        var pasadoManana = hoy.AddDays(2);
        string[] estadosRecordables =
        [
            "programada", "Programada", "agendada", "Agendada",
            "confirmada", "Confirmada", "confirmado", "Confirmado"
        ];

        var citasProximas =
            await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.EstadoCita)
                .Where(
                    c =>
                        c.FechaHora >= manana &&
                        c.FechaHora < pasadoManana.AddDays(1) &&
                        (c.EstadoCita != null
                            ? estadosRecordables.Contains(c.EstadoCita.NombreEstado)
                            : c.Estado != null && estadosRecordables.Contains(c.Estado)))
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
            citasProximas
            .Select(
            c => new RecordatorioPendienteDto
            {
                Id = c.IdCita,

                Paciente =
                    c.Paciente?.NombresCompleto ??
                    "Paciente sin datos",

                Iniciales =
                    Iniciales(
                        c.Paciente?.NombresCompleto),

                FechaHora = c.FechaHora,

                EsManana =
                    c.FechaHora.Date == manana,

                Canal = !string.IsNullOrWhiteSpace(c.Paciente?.Correo) ? "email" : "none",

                Confirmada =
                    NormalizarEstado(c.EstadoCita?.NombreEstado ?? c.Estado) == "confirmada"
            })
            .ToList();

        int sinConfirmar =
            pendientes.Count(
            p =>
                !p.Confirmada &&
                p.EsManana);

        int facturasVencidas =
            await _context.Facturas.CountAsync(
                f =>
                    f.Estado == "pendiente" &&
                    f.FechaFactura <
                        hoy.AddDays(-15),
                ct);

        int enviadosHoy = await _context.RecordatoriosCita
            .CountAsync(r => r.Estado == "enviado" && r.EnviadoEn >= hoy, ct);
        var historialEnviados = await _context.RecordatoriosCita
            .AsNoTracking()
            .Where(r => r.Estado == "enviado" && r.EnviadoEn != null)
            .Include(r => r.Cita)
                .ThenInclude(c => c!.Paciente)
            .OrderByDescending(r => r.EnviadoEn)
            .Take(5)
            .Select(r => new RecordatorioEnviadoDto
            {
                Paciente = r.Cita!.Paciente != null
                    ? r.Cita.Paciente.Nombres + " " + r.Cita.Paciente.Apellidos
                    : "Paciente",
                FechaCita = r.Cita!.FechaHora,
                EnviadoEn = r.EnviadoEn!.Value
            })
            .ToListAsync(ct);

        return new RecordatoriosViewModel
        {
            Pendientes = pendientes,
            HistorialEnviados = historialEnviados,
            EnviadosHoy = enviadosHoy,
            SinConfirmar = sinConfirmar,
            FacturasVencidas = facturasVencidas
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
            var hoy = ColombiaClock.Today;
            int duracionCitaMinutos =
                await _citaService.ObtenerDuracionCitaMinutosAsync(ct);

            var inicioSemana =
                weekStart?.Date ??
                hoy;

            if (inicioSemana.DayOfWeek !=
                DayOfWeek.Monday)
            {
                int diasDesdeLunes =
                    ((int)inicioSemana.DayOfWeek -
                        (int)DayOfWeek.Monday + 7) % 7;

                inicioSemana =
                    inicioSemana.AddDays(
                        -diasDesdeLunes);
            }

            var agendaDias =
                new List<
                    global::SmileTrack_MVC.Models.ViewModels.AgendaDiaViewModel>();

            var finSemana = inicioSemana.AddDays(7);
            var citasQuery = _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
                .Include(c => c.Consultorio)
                .Include(c => c.Servicio)
                .Include(c => c.EstadoCita)
                .Where(c => c.FechaHora >= inicioSemana && c.FechaHora < finSemana);

            if (professionalId.HasValue)
            {
                citasQuery = citasQuery.Where(c => c.IdProfesional == professionalId.Value);
            }

            if (officeId.HasValue)
            {
                citasQuery = citasQuery.Where(c => c.IdConsultorio == officeId.Value);
            }

            var citasSemana = await citasQuery
                .OrderBy(c => c.FechaHora)
                .ToListAsync(ct);

            for (int i = 0; i < 7; i++)
            {
                var fecha =
                    inicioSemana.AddDays(i);
                var citasDia = citasSemana
                    .Where(c => c.FechaHora.Date == fecha.Date)
                    .ToList();

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
                                                        duracionCitaMinutos)
                                                    .ToString("HH:mm"),

                                            Paciente =
                                                $"{cita.Paciente?.Nombres} " +
                                                $"{cita.Paciente?.Apellidos}"
                                                .Trim(),

                                            NombreProfesional =
                                                cita.Profesional?.Nombres != null
                                                    ? ($"{cita.Profesional.Nombres} {cita.Profesional.Apellidos}").Trim()
                                                    : (cita.Profesional?.Usuario != null
                                                        ? ($"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}").Trim()
                                                        : string.Empty),

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
            _logger.LogError(
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

    private async Task CargarKpisRecepcionHoy(CancellationToken ct = default)
    {
        var inicio = ColombiaClock.Today;
        var fin = inicio.AddDays(1);
        var estados = await _context.Citas
            .AsNoTracking()
            .Where(c => c.FechaHora >= inicio && c.FechaHora < fin)
            .Select(c => c.Estado)
            .ToListAsync(ct);

        var normalizados = estados
            .Select(EstadoCitaHelper.Normalize)
            .ToList();

        ViewData["StatRecHoy"] = normalizados.Count;
        ViewData["StatRecConfirmadas"] = normalizados.Count(e => e == "confirmada");
        ViewData["StatRecPendientes"] = normalizados.Count(e =>
            e is "solicitada" or "programada" or "agendada" or "pendiente");
        ViewData["StatRecCanceladasHoy"] = normalizados.Count(e => e == "cancelada");
    }

    private async Task CargarKpisGestionCitas(
        CancellationToken ct = default)
    {
        try
        {
            var hoy = ColombiaClock.Today;
            var kpis = await _citaService.ObtenerKpisGestionAsync(hoy, ct);

            // Claves usadas por index.cshtml.
            ViewData["StatKpiMesTotal"] = kpis.Total;
            ViewData["StatKpiMesProgramadas"] = kpis.Programadas;
            ViewData["StatKpiMesCanceladas"] = kpis.Canceladas;
            ViewData["StatKpiMesAtendidas"] = kpis.Atendidas;
            ViewData["StatKpiDifSemana"] = kpis.DiferenciaSemana;
            ViewData["StatKpiTasaCancelacion"] = kpis.TasaCancelacion;
            ViewData["StatKpiTasaAsistencia"] = kpis.TasaAsistencia;

            // Compatibilidad con otras vistas que puedan usar estas claves.
            ViewData["KpiTotalCitas"] = kpis.Total;
            ViewData["KpiProgramadas"] = kpis.Programadas;
            ViewData["KpiCanceladas"] = kpis.Canceladas;
            ViewData["KpiAtendidas"] = kpis.Atendidas;
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
        CancellationToken ct = default,
        string? vistaRecepcion = null,
        int? verId = null,
        DateOnly? desde = null,
        DateOnly? hasta = null)
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
                    "no_asistida" => ["no_asistida", "No asistida", "no_asistio", "No asistió", "no asistio", "no-show", "No Show", "inasistencia", "Inasistencia"],
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

            // POR QUÉ: las citas que ya vencieron y siguen en un estado no final (en espera /
            // pendiente) no deben listarse. Solo se muestran si el usuario filtra por fecha o por
            // estado, de modo que el histórico sigue siendo auditable.
            if (desde is { } fechaDesde && hasta is { } fechaHasta && fechaDesde <= fechaHasta)
            {
                var inicio = fechaDesde.ToDateTime(TimeOnly.MinValue);
                var fin = fechaHasta.ToDateTime(TimeOnly.MaxValue);
                var finalesPeriodo = EstadoCitaHelper.EstadosFinales;
                citasQuery = citasQuery.Where(c => c.FechaHora <= fin
                    && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : 30) > inicio
                    && !finalesPeriodo.Contains((c.EstadoCita == null ? c.Estado : c.EstadoCita.NombreEstado).Trim().ToLower()));
            }

            var ahoraRecepcion = ColombiaClock.Now;
            if (verId is > 0)
                citasQuery = citasQuery.Where(c => c.IdCita == verId.Value);
            else if (vistaRecepcion == "hoy")
                citasQuery = citasQuery.Where(c => c.FechaHora >= ahoraRecepcion.Date
                    && c.FechaHora < ahoraRecepcion.Date.AddDays(1));
            else if (vistaRecepcion is "proximas" or "historial")
            {
                var finales = EstadoCitaHelper.EstadosFinales;
                if (vistaRecepcion == "proximas")
                    citasQuery = citasQuery.Where(c => c.FechaHora >= ahoraRecepcion
                        && !finales.Contains((c.EstadoCita == null ? c.Estado : c.EstadoCita.NombreEstado).Trim().ToLower()));
                else
                    citasQuery = citasQuery.Where(c => c.FechaHora < ahoraRecepcion
                        || finales.Contains((c.EstadoCita == null ? c.Estado : c.EstadoCita.NombreEstado).Trim().ToLower()));
            }

            if (vistaRecepcion is null && string.IsNullOrWhiteSpace(pagination.Fecha) &&
                string.IsNullOrWhiteSpace(pagination.Estado))
            {
                var ahoraFiltro = ColombiaClock.Now;
                string[] estadosFinales =
                    EstadoCitaHelper.EstadosFinales;

                citasQuery =
                    citasQuery.Where(
                        c =>
                            c.FechaHora >= ahoraFiltro ||
                            (c.Estado != null &&
                             estadosFinales.Contains(
                                 c.Estado.ToLower())) ||
                            (c.EstadoCita != null &&
                             c.EstadoCita.NombreEstado != null &&
                             estadosFinales.Contains(
                                 c.EstadoCita.NombreEstado.ToLower())));
            }

            citasQuery = vistaRecepcion is "proximas" or "hoy"
                ? citasQuery.OrderBy(c => c.FechaHora).ThenBy(c => c.IdCita)
                : citasQuery.OrderByDescending(c => c.FechaHora).ThenByDescending(c => c.IdCita);

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
                await _memoryCache.GetOrCreateAsync(
                    CatalogCacheKeys.ConsultoriosDisponibles,
                    async entry =>
                    {
                        entry.SlidingExpiration = CatalogCacheKeys.SlidingExpiration;
                        return await _context.Consultorios
                            .AsNoTracking()
                            .Where(c => c.Estado == "disponible" || c.Estado == "activo")
                            .OrderBy(c => c.Nombre)
                            .ToListAsync(ct);
                    }) ?? throw new InvalidOperationException("No se pudo cargar el catálogo de consultorios.");

            ViewData["EstadosCita"] =
                await _memoryCache.GetOrCreateAsync(
                    CatalogCacheKeys.EstadosCita,
                    async entry =>
                    {
                        entry.SlidingExpiration = CatalogCacheKeys.SlidingExpiration;
                        return await _context.EstadosCita
                            .AsNoTracking()
                            .OrderBy(e => e.NombreEstado)
                            .ToListAsync(ct);
                    }) ?? throw new InvalidOperationException("No se pudo cargar el catálogo de estados de cita.");

            ViewData["Servicios"] =
                await _memoryCache.GetOrCreateAsync(
                    CatalogCacheKeys.Servicios,
                    async entry =>
                    {
                        entry.SlidingExpiration = CatalogCacheKeys.SlidingExpiration;
                        return await _context.Servicios
                            .AsNoTracking()
                            .Where(s => s.Estado == "activo")
                            .OrderBy(s => s.Nombre)
                            .ToListAsync(ct);
                    }) ?? throw new InvalidOperationException("No se pudo cargar el catálogo de servicios.");

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
            _logger.LogError(
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

            TempData["ErrorValidacion"] =
                MensajeErrorFallback;
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
            var metricas = await _citasDashboardService
                .ObtenerMetricasPanelOperativoAsync(ColombiaClock.Today, ct);

            ViewData["TotalPacientes"] = metricas.TotalPacientes;
            ViewData["CitasHoy"] = metricas.CitasHoy;
            ViewData["ProfesionalesActivos"] = metricas.ProfesionalesActivos;
            ViewData["IngresosDelMes"] = metricas.IngresosDelMes;
            ViewData["FacturasPendientes"] =
                new List<(string Codigo, string Descripcion, decimal Monto)>();
            ViewData["PctOcupacion"] = metricas.PorcentajeOcupacion;
            ViewData["CitasAtendidas"] = metricas.CitasAtendidas;
            ViewData["CitasConfirmadas"] = metricas.CitasConfirmadas;
            ViewData["CitasProgramadas"] = metricas.CitasProgramadas;
            ViewData["CitasCanceladas"] = metricas.CitasCanceladas;
            ViewData["PctAtendidas"] = metricas.PorcentajeAtendidas;
            ViewData["PctConfirmadas"] = metricas.PorcentajeConfirmadas;
            ViewData["PctProgramadas"] = metricas.PorcentajeProgramadas;
            ViewData["PctCanceladas"] = metricas.PorcentajeCanceladas;
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
            _logger.LogError(
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
    // HELPERS
    // ================================================================

    private static string NormalizarEstado(
        string? estado)
    {
        return EstadoCitaHelper.Normalize(estado);
    }

    private static int CalcularEdad(
        DateTime fechaNacimiento)
    {
        var hoy =
            ColombiaClock.Today;

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

    private int? ObtenerIdUsuarioActual() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario) ? idUsuario : null;

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
            new List<PanelOperativoTopProfesionalViewModel>();
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