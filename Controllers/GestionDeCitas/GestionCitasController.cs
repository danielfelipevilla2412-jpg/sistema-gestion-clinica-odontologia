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
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SmileTrack_MVC.Controllers;

public class GestionCitasController(
    AppDbContext context,
    ILogger<GestionCitasController> logger,
    IEmailService emailService,
    ICitaService citaService,
    IPanelOperativoService panelOperativoService,
    IAntiforgery antiforgery,
    ICitasDashboardService citasDashboardService,
    IAgendaService agendaService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionCitasController> _logger = logger;
    private readonly IEmailService _emailService = emailService;
    private readonly ICitaService _citaService = citaService;
    private readonly IPanelOperativoService _panelOperativoService = panelOperativoService;
    private readonly IAntiforgery _antiforgery = antiforgery;
    private readonly ICitasDashboardService _citasDashboardService = citasDashboardService;
    private readonly IAgendaService _agendaService = agendaService;

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
                .ObtenerDashboardAsync(DateTime.Today, ct);

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
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-citas/st-adm-01-dashboard/exportar-pdf")]
    public async Task<IActionResult> ExportarDashboardPdf(CancellationToken ct = default)
    {
        // Usa el mismo servicio que el dashboard para garantizar que los números
        // del PDF sean idénticos a los que el usuario vio en pantalla.
        var hoy   = DateTime.Today;
        var model = await _citasDashboardService.ObtenerDashboardAsync(hoy, ct);

        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes    = inicioMes.AddMonths(1);

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
                column.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
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
    [Route("gestion-de-citas/st-adm-08-agenda")]
    public async Task<IActionResult> Stadm08Agenda(
        [FromQuery] DateTime? weekStart,
        [FromQuery] int? professionalId,
        [FromQuery] int? officeId,
        CancellationToken ct = default)
    {
        try
        {
            _antiforgery.GetAndStoreTokens(HttpContext);

            var model = await _agendaService.ObtenerAgendaAsync(
                weekStart,
                professionalId,
                officeId,
                ct);

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                model);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Solicitud cancelada Stadm08Agenda");

            TempData["ErrorValidacion"] = MensajeErrorFallback;

            return View(
                "~/Views/Gestion_De_Citas/st-adm-08-agenda/index.cshtml",
                new AgendaViewModel());
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Error de base de datos en Stadm08Agenda");

            TempData["ErrorValidacion"] =
                "Error al consultar datos. Intente nuevamente.";

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
                var updateDto = new Models.DTOs.CitaApiUpdateDto
                {
                    IdCita = dto.IdCita!.Value,
                    IdPaciente = dto.IdPaciente,
                    IdProfesional = dto.IdProfesional,
                    IdServicio = dto.IdServicio,
                    IdConsultorio = dto.IdConsultorio,
                    FechaHora = fechaHora,
                    Estado = estadoNormalizado,
                    Notas = dto.Notas?.Trim()
                };

                cita = await _citaService.ActualizarAsync(dto.IdCita.Value, updateDto, ct)
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

                cita = await _citaService.CrearAsync(request, ct);
            }

            await RegistrarAuditoriaAsync(
                accion: esActualizacion ? "UPDATE" : "INSERT",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"{(esActualizacion ? "Cita actualizada" : "Cita creada")} desde Agenda Interna. " +
                    $"IdPaciente={cita.IdPaciente}, " +
                    $"IdProfesional={cita.IdProfesional}, " +
                    $"FechaHora={cita.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"Estado\":\"{cita.Estado}\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"," +
                    $"\"IdPaciente\":{cita.IdPaciente}," +
                    $"\"IdProfesional\":{cita.IdProfesional}," +
                    $"\"IdServicio\":{cita.IdServicio}," +
                    $"\"IdConsultorio\":{cita.IdConsultorio}}}",
                ct: ct);

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
        catch (InvalidOperationException ex)
        {
            bool esConflicto = ex.Message.Contains(
                "horario",
                StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains(
                    "conflicto",
                    StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains(
                    "solap",
                    StringComparison.OrdinalIgnoreCase);

            _logger.LogWarning(
                ex,
                "Validación de servicio en CrearCitaDesdeAgendaInterna. Conflicto={EsConflicto}",
                esConflicto);

            if (esConflicto)
            {
                return Conflict(new
                {
                    success = false,
                    message = ex.Message
                });
            }

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
            _logger.LogCritical(
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
            _logger.LogCritical(
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
            // PERSISTENCIA — LÓGICA DELEGADA 100% EN SERVICIO
            // Incluye: existencia entidades, duración configurable,
            // horario de clínica (M-15) y conflicto triple recurso
            // (Profesional ∧ Paciente ∧ Consultorio — U-07).
            // ------------------------------------------------------------

            Cita cita;

            if (model.IdCita > 0)
            {
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
                    IdEstado = model.IdEstado > 0 ? model.IdEstado : null
                };

                cita = await _citaService.ActualizarAsync(
                           model.IdCita.Value,
                           updateDto,
                           ct)
                       ?? throw new InvalidOperationException(
                           "La cita que intenta actualizar no existe.");

                operacion = "Actualizacion";
            }
            else
            {
                var request = new Models.DTOs.CitaApiRequest
                {
                    IdPaciente = model.IdPaciente,
                    IdProfesional = model.IdProfesional > 0 ? model.IdProfesional : null,
                    IdServicio = model.IdServicio > 0 ? model.IdServicio : null,
                    IdConsultorio = model.IdConsultorio > 0 ? model.IdConsultorio : null,
                    FechaHora = model.FechaHora,
                    Estado = EstadoCitaHelper.ResolveEstadoNombre(
                                 await BuildEstadoNombreAsync(
                                     model.IdEstado,
                                     model.Estado,
                                     "programada",
                                     ct),
                                 "programada"),
                    Notas = BuildNotasCita(model),
                    IdEstado = model.IdEstado > 0 ? model.IdEstado : null
                };

                cita = await _citaService.CrearAsync(request, ct);
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
    // CAMBIAR ESTADO DE CITA MVC (INLINE - SOLO CAMBIO DE ESTADO)
    // ================================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
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
            var cita = await _citaService.CambiarEstadoAsync(
                IdCita,
                estadoNormalizado,
                ct);

            if (cita is null)
            {
                TempData["ErrorValidacion"] =
                    "La cita no existe o no se pudo modificar su estado.";
                return Redirect(returnUrlSafe);
            }

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"Estado de cita cambiado a '{cita.Estado}' via formulario inline. " +
                    $"IdPaciente={cita.IdPaciente}, " +
                    $"FechaHora={cita.FechaHora:yyyy-MM-dd HH:mm}",
                datosNuevos:
                    $"{{\"IdEstado\":{cita.IdEstado}," +
                    $"\"Estado\":\"{cita.Estado}\"," +
                    $"\"FechaHora\":\"{cita.FechaHora:O}\"}}",
                ct: ct);

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
            _logger.LogCritical(
                ex,
                "SqlException al cambiar estado de cita Id={Id}. Number={Number}",
                IdCita,
                ex.Number);

            TempData["ErrorValidacion"] =
                "Error de conectividad con la base de datos.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
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

            bool ok;
            try
            {
                ok = await _citaService.CancelarAsync(IdCita, ct: ct);
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

            var cita = await _citaService.ObtenerPorIdAsync(IdCita, ct)
                       ?? throw new InvalidOperationException(
                           "La cita fue cancelada pero no se pudo recuperar para auditoría.");

            await RegistrarAuditoriaAsync(
                accion: "UPDATE",
                tablaAfectada: "Cita",
                idRegistro: cita.IdCita,
                descripcion:
                    $"Cita cancelada via MVC. IdPaciente={cita.IdPaciente}, " +
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
                    Estado       = h.EstadoTexto,
                    h.Motivo,
                    FechaCambio  = h.FechaCambio.ToString("dd/MM/yyyy HH:mm:ss"),
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
            var panelData = await _panelOperativoService.ObtenerAsync(ct);
            ViewData["PanelOperativoData"] = panelData;
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
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-02-agenda-apoyo",
                null,
                ct);

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

    private async Task<object> ConstruirAgendaApoyoAsync(
        DateTime? fecha,
        DateTime? weekStart,
        CancellationToken ct)
    {
        bool esSemana = weekStart.HasValue;
        var inicio = (weekStart ?? fecha ?? DateTime.Today).Date;
        var fin = esSemana ? inicio.AddDays(7) : inicio.AddDays(1);

        var citasAgenda = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(c => c.FechaHora >= inicio && c.FechaHora < fin)
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

                "cancelada" => "Cancelada",

                "no_asistida" or
                "no-show" or
                "no asistió"
                    => "No asistió",

                _ => "Pendiente"
            };

        var citas = citasAgenda.Select(
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
            fechaInicio =
                inicio.ToString(
                    "ddd d MMM yyyy",
                    new System.Globalization.CultureInfo("es-CO")),
            fechaFin = fin.AddDays(-1).ToString(
                "ddd d MMM yyyy",
                new System.Globalization.CultureInfo("es-CO")),
            modo = esSemana ? "semana" : "dia",

            citas
        };
    }

    // ================================================================
    // HISTORIAL PARCIAL AUXILIAR
    // ================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Administrador")]
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

        var paciente = pacienteId is not null
            ? await _context.Pacientes
                .FirstOrDefaultAsync(
                    p => p.IdPaciente == pacienteId,
                    ct)
            : await _context.Pacientes
                .OrderBy(p => p.IdPaciente)
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

        var consultasBase = await _context.Citas
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

        var notaClinica = await _context.NotasClinicas
            .AsNoTracking()
            .Include(n => n.HistoriaClinica)
            .Where(n => n.HistoriaClinica != null && n.HistoriaClinica.IdPaciente == paciente.IdPaciente)
            .OrderByDescending(n => n.Fecha)
            .FirstOrDefaultAsync(ct);

        var consultas = consultasBase
            .Select(c => new
            {
                c.id,
                c.fecha,
                c.profesional,
                diagnostico = notaClinica is null || string.IsNullOrWhiteSpace(notaClinica.Diagnostico)
                    ? "Sin notas clínicas registradas"
                    : notaClinica.Diagnostico,
                c.procedimiento
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
            await CargarDatosCitas(
                editId,
                "/gestion-de-citas/st-aux-06-asistencia-procedi",
                null,
                ct);

            ViewData["CitasAsistenciaSelector"] = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Where(c => c.FechaHora >= DateTime.Today && c.FechaHora < DateTime.Today.AddDays(1))
                .OrderBy(c => c.FechaHora)
                .ToListAsync(ct);

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
                    .Include(c => c.EstadoCita)
                    .FirstOrDefaultAsync(
                        c => c.IdCita == citaId,
                        ct)
                : await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Paciente)
                    .Include(c => c.Servicio)
                    .Include(c => c.Profesional)
                    .Include(c => c.Consultorio)
                    .Include(c => c.EstadoCita)
                    .Where(c =>
                        c.FechaHora >= DateTime.Now.AddMinutes(-60) &&
                        c.FechaHora <= DateTime.Now)
                    .OrderByDescending(c => c.FechaHora)
                    .FirstOrDefaultAsync(
                        c =>
                            c.Estado == "en_proceso" ||
                            c.Estado == "En proceso" ||
                            c.Estado == "en_consulta" ||
                            c.Estado == "En consulta" ||
                            (c.EstadoCita != null &&
                             (c.EstadoCita.NombreEstado == "en_proceso" ||
                              c.EstadoCita.NombreEstado == "En proceso" ||
                              c.EstadoCita.NombreEstado == "en_consulta" ||
                              c.EstadoCita.NombreEstado == "En consulta")),
                        ct);

        if (cita is not null && !citaId.HasValue)
        {
            var estadoRaw =
                (cita.EstadoCita?.NombreEstado ?? cita.Estado).Trim().ToLowerInvariant();
            var estado = estadoRaw is "en consulta" or "en_consulta"
                ? "en_proceso"
                : NormalizarEstado(estadoRaw);

            if (estado != "en_proceso")
            {
                cita = null;
            }
        }

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

            inicioProcedimiento = cita.FechaHora,

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
    [Authorize(Roles = "Auxiliar,Administrador")]
    [Route("gestion-de-citas/st-aux-09-estado-consultorio")]
    public async Task<IActionResult> Staux09EstadoConsultorio(
        [FromQuery] int? consultorioId,
        CancellationToken ct = default)
    {
        try
        {
            var data = await ConstruirEstadoConsultorioAsync(consultorioId, ct);
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
                historial = Array.Empty<object>()
            };
        }

        var estadoOperativo = await _context.EstadosOperativosConsultorio
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdConsultorio == consultorio.IdConsultorio, ct);

        var historial = await _context.ConsultoriosHistorial
            .AsNoTracking()
            .Where(h => h.IdConsultorio == consultorio.IdConsultorio)
            .OrderByDescending(h => h.FechaCambio)
            .Take(5)
            .Select(c => new
            {
                time = c.FechaCambio.ToString("o"),
                user = "Estado operativo",
                detail = string.IsNullOrWhiteSpace(c.Motivo)
                    ? c.Estado
                    : $"{c.Estado}: {c.Motivo}"
            })
            .ToListAsync(ct);

        object[] checklist = estadoOperativo is null
            ? []
            : JsonSerializer.Deserialize<object[]>(estadoOperativo.ChecklistJson) ?? [];

        return new
        {
            consultorioId = consultorio.IdConsultorio,
            nombre = consultorio.Nombre ?? $"Consultorio {consultorio.IdConsultorio}",
            ubicacion = consultorio.Ubicacion ?? "",
            ultimaActualizacion = estadoOperativo?.ActualizadoEn.ToString("o") ?? historial.FirstOrDefault()?.time ?? "",
            estadoActual = consultorio.Estado ?? "disponible",
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

        if (citasHoy.Count == 0)
        {
            citasHoy = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .Include(c => c.Servicio)
                .OrderByDescending(c => c.FechaHora)
                .Take(25)
                .ToListAsync(ct);
        }

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
                        fecha = c.FechaHora.ToString("dd/MM/yyyy"),
                        hora =
                            c.FechaHora.ToString("HH:mm"),
                        esHoy = c.FechaHora.Date == hoy,

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
            var idProfesionalClaim = User.FindFirst("IdProfesional")?.Value;
            if (!string.IsNullOrEmpty(idProfesionalClaim) && int.TryParse(idProfesionalClaim, out int idProf))
            {
                professionalId = idProf;
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
                    c.Estado == "Agendada" || c.Estado == "programada");
                ViewData["CitasConfirmadas"] = citasSemana.Count(c => 
                    c.Estado == "Confirmada" || c.Estado == "confirmada");
                ViewData["CitasAtendidas"] = citasSemana.Count(c => 
                    c.Estado == "Atendida" || c.Estado == "atendida");
                ViewData["CitasCanceladas"] = citasSemana.Count(c => 
                    c.Estado == "Cancelada" || c.Estado == "cancelada");

                // 2. PRÓXIMAS 3 CITAS (para vista rápida)
                var proximasCitas = citasSemana
                    .Where(c => c.FechaHora >= DateTime.Now &&
                               (c.Estado == "Agendada" || c.Estado == "programada" ||
                                c.Estado == "Confirmada" || c.Estado == "confirmada"))
                    .OrderBy(c => c.FechaHora)
                    .Take(3)
                    .ToList();

                ViewData["ProximasCitasRapidas"] = proximasCitas;

                // 3. HORAS MÁS OCUPADAS (para insights)
                var horasOcupadas = citasSemana
                    .Where(c => c.Estado != "Cancelada" && c.Estado != "cancelada")
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
    [Authorize(Roles = "Paciente,Administrador")]
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

        var citasIds = citas.Select(c => c.IdCita).ToList();
        var leidas = citasIds.Count == 0
            ? new HashSet<int>()
            : (await _context.NotificacionesLeidas
                .AsNoTracking()
                .Where(n => n.IdPaciente == paciente.IdPaciente && citasIds.Contains(n.IdCita))
                .Select(n => n.IdCita)
                .ToListAsync(ct))
                .ToHashSet();

        var ahora = DateTime.Now;

        var notificaciones =
            new List<(object Notif, DateTime Time)>();

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
                            id = c.IdCita,
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
                            leida = leidas.Contains(c.IdCita),
                            badge = leidas.Contains(c.IdCita) ? "read" : "pending"
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
                            id = c.IdCita,
                            tipo = "confirmed",
                            titulo = "Cita confirmada",

                            desc =
                                $"Tu cita del {c.FechaHora:dd 'de' MMMM} " +
                                "fue confirmada exitosamente.",

                            time = c.FechaHora,
                            leida = leidas.Contains(c.IdCita),
                            badge = leidas.Contains(c.IdCita) ? "read" : "new"
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
                            id = c.IdCita,
                            tipo = "cancelled",
                            titulo = "Cita cancelada",

                            desc =
                                $"Tu cita del {c.FechaHora:dd 'de' MMMM} " +
                                "fue cancelada.",

                            time = c.FechaHora,
                            leida = true,
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

    [HttpPut]
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

        bool pertenece = await _context.Citas.AsNoTracking()
            .AnyAsync(c => c.IdCita == id && c.IdPaciente == idPaciente.Value, ct);
        if (!pertenece) return NotFound(new { success = false, message = "Notificación no encontrada." });

        var lectura = await _context.NotificacionesLeidas
            .FirstOrDefaultAsync(n => n.IdPaciente == idPaciente.Value && n.IdCita == id, ct);
        if (lectura is null)
        {
            _context.NotificacionesLeidas.Add(new NotificacionLeida
            {
                IdPaciente = idPaciente.Value,
                IdCita = id,
                FechaLectura = DateTime.UtcNow
            });
        }
        else
        {
            lectura.FechaLectura = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, leida = true });
    }

    [HttpPut]
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

        var permitidos = await _context.Citas.AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente.Value && (ids == null || ids.Contains(c.IdCita)))
            .Select(c => c.IdCita)
            .ToListAsync(ct);
        var existentes = await _context.NotificacionesLeidas
            .Where(n => n.IdPaciente == idPaciente.Value && permitidos.Contains(n.IdCita))
            .ToListAsync(ct);
        var existentesIds = existentes.Select(n => n.IdCita).ToHashSet();
        foreach (var lectura in existentes) lectura.FechaLectura = DateTime.UtcNow;
        _context.NotificacionesLeidas.AddRange(permitidos.Where(id => !existentesIds.Contains(id)).Select(id => new NotificacionLeida
        {
            IdPaciente = idPaciente.Value,
            IdCita = id,
            FechaLectura = DateTime.UtcNow
        }));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, count = permitidos.Count });
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
        var ahora = DateTime.Now;
        var limite = ahora.AddMinutes(ventanaMinutos);

        var proximas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora > ahora && c.FechaHora <= limite && !EsEstadoCancelado(c.Estado))
            .OrderBy(c => c.FechaHora)
            .Select(c => new
            {
                hora = c.FechaHora.ToString("hh:mm tt"),
                fechaIso = c.FechaHora.ToString("yyyy-MM-ddTHH:mm"),
                texto = $"{(c.Paciente == null ? "Paciente sin datos" : c.Paciente.Nombres + " " + c.Paciente.Apellidos)} - " +
                    $"{(c.Servicio == null ? "Consulta" : c.Servicio.Nombre)} - " +
                    $"{(c.Profesional == null ? "Sin asignar" : "Dr(a). " + c.Profesional.Nombres + " " + c.Profesional.Apellidos)}" +
                    $"{(c.Consultorio == null ? "" : " - " + c.Consultorio.Nombre)}"
            })
            .ToListAsync(ct);

        return Ok(new { success = true, proximasCitas = proximas });
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

    private async Task CargarKpisRecepcionHoy(CancellationToken ct = default)
    {
        var inicio = DateTime.Today;
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
            var hoy = DateTime.Today;
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
                    // H4-fix: la tarjeta KPI dice "Pacientes activos" — el count
                    // debe excluir pacientes dados de baja para no inflar la métrica.
                    .CountAsync(p => p.Estado == "activo", ct);

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

            int totalCitas =
                citasDelMes.Count;

            var horariosActivos = await _context.HorariosProfesional
                .AsNoTracking()
                .Where(h => h.Activo)
                .ToListAsync(ct);
            var diasSemana = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
            {
                ["Lunes"] = DayOfWeek.Monday, ["Martes"] = DayOfWeek.Tuesday,
                ["Miércoles"] = DayOfWeek.Wednesday, ["Jueves"] = DayOfWeek.Thursday,
                ["Viernes"] = DayOfWeek.Friday, ["Sábado"] = DayOfWeek.Saturday,
                ["Domingo"] = DayOfWeek.Sunday
            };
            double horasDisponibles = horariosActivos.Sum(h =>
                diasSemana.TryGetValue(h.DiaSemana, out var dia)
                    ? Math.Max(0, (h.HoraFin - h.HoraInicio).TotalHours) *
                      Enumerable.Range(1, DateTime.DaysInMonth(hoy.Year, hoy.Month))
                          .Count(d => new DateTime(hoy.Year, hoy.Month, d).DayOfWeek == dia)
                    : 0);
            int duracionMinutos = await _citaService.ObtenerDuracionCitaMinutosAsync(ct);
            double capacidadCitas = duracionMinutos > 0 ? horasDisponibles * 60 / duracionMinutos : 0;

            ViewData["PctOcupacion"] =
                capacidadCitas > 0
                    ? (int)Math.Min(
                        100,
                        (double)totalCitas /
                        capacidadCitas *
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
        return EstadoCitaHelper.Normalize(estado);
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