using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Controllers;

public partial class GestionProfesionalesController(
    AppDbContext context,
    ILogger<GestionProfesionalesController> logger,
    IProfesionalService profesionalService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionProfesionalesController> _logger = logger;
    private readonly IProfesionalService _profesionalService = profesionalService;

    private const string MensajeErrorFallback =
        "Ocurrió un error inesperado al cargar la página. Por favor intente nuevamente. Si el problema persiste, contacte al soporte.";

    private static bool EsTelefonoValido(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return true;
        string digitsOnly = new string(telefono.Where(char.IsDigit).ToArray());
        return digitsOnly.Length is >= 7 and <= 15;
    }

    private static bool EsRegistroMedicoValido(string? registro)
    {
        if (string.IsNullOrWhiteSpace(registro)) return false;
        registro = registro.Trim();
        if (registro.Length < 3 || registro.Length > 30) return false;
        return RegistroMedicoRegex().IsMatch(registro);
    }

    [GeneratedRegex(@"^[A-Za-z0-9\-\. ]+$")]
    private static partial Regex RegistroMedicoRegex();

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-profesionales/st-adm-07-gestion-profesionales")]
    public async Task<IActionResult> Stadm07GestionProfesionales([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? especialidad = null, [FromQuery] string? estado = null, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(BuildReturnUrl(), new PaginationQuery
            {
                Page = page,
                PageSize = pageSize,
                Search = search,
                Estado = estado,
                Profesional = especialidad
            }, ct);
            return View("~/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stadm07GestionProfesionales (pagina={Pagina}, search={Search})", page, search);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Profesional")]
    [Route("gestion-de-profesionales/st-adm-14-reportes-clinicos")]
    public async Task<IActionResult> Stadm14ReportesClinicos([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? profesional = null, [FromQuery] string? mes = null, CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(BuildReturnUrl(), null, ct);
            await CargarDatosReportesClinicos(page, pageSize, search, profesional, mes, ct);
            return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stadm14ReportesClinicos (pagina={Pagina}, mes={Mes})", page, mes);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("gestion-de-profesionales/st-odo-01-dashboard")]
    public async Task<IActionResult> Stodo01Dashboard(CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(BuildReturnUrl(), null, ct);

            string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                if (profesional != null)
                {
                    var hoy = DateTime.Today;
                    var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
                    var finMes = inicioMes.AddMonths(1);

                    var citasDelMes = await _context.Citas
                        .Include(c => c.Servicio)
                        .Where(c => c.IdProfesional == profesional.IdProfesional && c.FechaHora >= inicioMes && c.FechaHora < finMes)
                        .AsNoTracking()
                        .ToListAsync(ct);

                    ViewData["OdoPacientesActivos"] = citasDelMes.Select(c => c.IdPaciente).Distinct().Count();
                    ViewData["OdoCitasHoy"] = citasDelMes.Count(c => c.FechaHora.Date == hoy);

                    decimal ingresosMes = citasDelMes
                        .Where(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase) && c.Servicio != null)
                        .Sum(c => c.Servicio!.Precio);
                    ViewData["OdoIngresosMes"] = ingresosMes;

                    ViewData["OdoCitasAtendidas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasProgramadas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Agendada", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Estado, "programada", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasCanceladas"] = citasDelMes.Count(c => string.Equals(c.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoCitasNoAsistio"] = citasDelMes.Count(c => string.Equals(c.Estado, "No asistio", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Estado, "no asistió", StringComparison.OrdinalIgnoreCase));
                    ViewData["OdoTotalCitasMes"] = citasDelMes.Count;

                    ViewData["OdoProximasCitas"] = citasDelMes
                        .Where(c =>
                            c.FechaHora.Date == hoy &&
                            (
                                string.Equals(c.Estado, "Agendada", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Estado, "programada", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Estado, "Confirmada", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Estado, "confirmada", StringComparison.OrdinalIgnoreCase)
                            ))
                        .OrderBy(c => c.FechaHora)
                        .Take(4)
                        .ToList();

                    ViewData["OdoUltimosPacientes"] = citasDelMes
                        .Where(c =>
                            string.Equals(c.Estado, "Atendida", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Estado, "atendida", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Estado, "finalizada", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Estado, "completada", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Estado, "realizada", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(c => c.FechaHora)
                        .Take(3)
                        .ToList();

                    // Ingresos reales de los últimos 3 meses del profesional autenticado.
                    var inicioPeriodo = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-2);
                    var finPeriodo = inicioMes;

                    var citasPeriodo = await _context.Citas
                        .AsNoTracking()
                        .Include(c => c.Servicio)
                        .Where(c =>
                            c.IdProfesional == profesional.IdProfesional &&
                            c.FechaHora >= inicioPeriodo &&
                            c.FechaHora < finMes)
                        .ToListAsync(ct);

                    var revenueMonths = Enumerable.Range(0, 3)
                        .Select(offset => inicioPeriodo.AddMonths(offset))
                        .Select(monthStart =>
                        {
                            var nextMonth = monthStart.AddMonths(1);
                            var amount = citasPeriodo
                                .Where(c =>
                                    c.FechaHora >= monthStart &&
                                    c.FechaHora < nextMonth &&
                                    (
                                        c.Estado?.Trim().ToLowerInvariant() == "atendida" ||
                                        c.Estado?.Trim().ToLowerInvariant() == "finalizada" ||
                                        c.Estado?.Trim().ToLowerInvariant() == "completada" ||
                                        c.Estado?.Trim().ToLowerInvariant() == "realizada"
                                    ))
                                .Sum(c => c.Servicio?.Precio ?? 0m);

                            return new
                            {
                                Mes = monthStart.ToString("MMMM", new System.Globalization.CultureInfo("es-CO")),
                                Valor = amount
                            };
                        })
                        .ToList();

                    ViewData["OdoRevenueMonthsJson"] =
                        System.Text.Json.JsonSerializer.Serialize(revenueMonths);

                    ViewData["OdoFechaActual"] = hoy;
                }
            }

            return View("~/Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stodo01Dashboard para profesional {Usuario}", User.Identity?.Name ?? "anonimo");
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-odo-01-dashboard/index.cshtml");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("gestion-de-profesionales/st-odo-09-perfil-profesional")]
    public async Task<IActionResult> Stodo09PerfilProfesional(CancellationToken ct = default)
    {
        try
        {
            await CargarDatosProfesionales(BuildReturnUrl(), null, ct);

            string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var profesional = await _context.Profesionales
                    .Include(p => p.Usuario)
                    .Include(p => p.Especialidades)
                    .ThenInclude(pe => pe.Especialidad)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);

                ViewData["LoggedProfesional"] = profesional;
            }

            return View("~/Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando Stodo09PerfilProfesional");
            TempData["ErrorValidacion"] = MensajeErrorFallback;
            return View("~/Views/Gestion_De_Profesionales/st-odo-09-perfil-profesional/index.cshtml");
        }
    }

    // ─── CRUD MIGRADO A LA API ──────────────────────────────────────────────
    // Las operaciones GuardarProfesional (POST/PUT) y EliminarProfesional (DELETE/PATCH)
    // fueron eliminadas en la Fase 2E porque el frontend ahora consume directamente
    // ProfesionalesApiController.cs.

    private async Task CargarDatosReportesClinicos(int page, int pageSize, string? search = null, string? profesional = null, string? mes = null, CancellationToken ct = default)
    {
        try
        {
            var hoy = DateTime.Today;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var finMes = inicioMes.AddMonths(1);

            var citasQuery = _context.Citas
                .Include(c => c.Paciente)
                .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
                .Include(c => c.Servicio)
                .Where(c => c.Paciente != null)
                .AsNoTracking()
                .AsQueryable();

            if (User.IsInRole("Profesional"))
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var prof = await _context.Profesionales.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                    if (prof != null)
                    {
                        citasQuery = citasQuery.Where(c => c.IdProfesional == prof.IdProfesional);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTerm = search.Trim();
                citasQuery = citasQuery.Where(c =>
                    (c.Paciente!.Nombres != null && c.Paciente.Nombres.Contains(searchTerm)) ||
                    (c.Paciente.Apellidos != null && c.Paciente.Apellidos.Contains(searchTerm)) ||
                    (c.Paciente.Documento != null && c.Paciente.Documento.Contains(searchTerm))
                );
            }

            if (!string.IsNullOrWhiteSpace(profesional))
            {
                string profTerm = profesional.Trim();
                citasQuery = citasQuery.Where(c =>
                    c.Profesional != null && (
                        ((c.Profesional.Nombres ?? "") + " " + (c.Profesional.Apellidos ?? "")).Contains(profTerm) ||
                        (c.Profesional.Usuario != null && ((c.Profesional.Usuario.Nombre ?? "") + " " + (c.Profesional.Usuario.Apellidos ?? "")).Contains(profTerm))
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(mes) && DateTime.TryParseExact(mes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var filterMonth))
            {
                var filterStart = filterMonth;
                var filterEnd = filterMonth.AddMonths(1);
                citasQuery = citasQuery.Where(c => c.FechaHora >= filterStart && c.FechaHora < filterEnd);
            }

            var pacientesQuery = citasQuery
                .GroupBy(c => c.IdPaciente)
                .Select(g => g.OrderByDescending(c => c.FechaHora).First())
                .OrderByDescending(c => c.FechaHora);

            var pagedCitas = await pacientesQuery.ToPagedResultAsync(page, pageSize, ct);

            var profesionalesOptions = await _context.Profesionales
                .Where(p => p.Estado == "activo")
                .Select(p => (p.Nombres ?? "") + " " + (p.Apellidos ?? ""))
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(ct);

            var todasCitasPacientes = await citasQuery.AsNoTracking().ToListAsync(ct);

            var reportes = pagedCitas.Items.Select(cita =>
            {
                var pacienteCitas = todasCitasPacientes.Where(c => c.IdPaciente == cita.IdPaciente).ToList();

                var nacimiento = cita.Paciente?.FechaNacimiento;
                int edad = nacimiento.HasValue
                    ? hoy.Year - nacimiento.Value.Year - (hoy < nacimiento.Value.AddYears(hoy.Year - nacimiento.Value.Year) ? 1 : 0)
                    : 0;

                var proximaCita = pacienteCitas
                    .Where(c => c.FechaHora.Date >= hoy)
                    .OrderBy(c => c.FechaHora)
                    .FirstOrDefault();

                string? alerta = string.IsNullOrWhiteSpace(cita.Notas) ? null : "observacion";

                return new ReporteClinicoViewModel
                {
                    Id = cita.IdCita,
                    NombrePaciente = $"{cita.Paciente?.Nombres} {cita.Paciente?.Apellidos}".Trim(),
                    Documento = cita.Paciente?.Documento ?? string.Empty,
                    Edad = edad,
                    UltimaConsulta = cita.FechaHora,
                    Diagnostico = cita.MotivoConsulta ?? "Consulta general",
                    ProfesionalNombre = cita.Profesional?.Usuario != null
                        ? $"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}".Trim()
                        : "Sin profesional",
                    ProximaCita = proximaCita?.FechaHora,
                    Alerta = alerta,
                    Avatar = string.Concat((cita.Paciente?.Nombres ?? "P").Take(2).Select(ch => char.ToUpperInvariant(ch))),
                    Color = pacienteCitas.Count % 2 == 0 ? "green" : "blue"
                };
            }).ToList();

            var pagedReportes = new PagedResult<ReporteClinicoViewModel>
            {
                Page = pagedCitas.Page,
                PageSize = pagedCitas.PageSize,
                TotalCount = pagedCitas.TotalCount,
                Items = reportes
            };

            ViewData["ReportesClinicos"] = reportes;
            ViewData["ReportesClinicosPage"] = pagedReportes;
            ViewData["ProfesionalesReportes"] = profesionalesOptions;
            ViewData["TotalPacientesReportes"] = await _context.Pacientes.CountAsync(ct);

            var consultasQuery = _context.Citas.AsNoTracking();
            if (User.IsInRole("Profesional"))
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var prof = await _context.Profesionales.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
                    if (prof != null)
                    {
                        consultasQuery = consultasQuery.Where(c => c.IdProfesional == prof.IdProfesional);
                    }
                }
            }

            ViewData["ConsultasMes"] = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);

            // Calcular SatisfaccionPromedio como % de citas atendidas sobre el total del mes
            // (opción (a) acordada: dato real calculado desde BD, no hardcoded)
            int totalCitasMes = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);
            int citasAtendidasMes = await consultasQuery.CountAsync(
                c => c.FechaHora >= inicioMes && c.FechaHora < finMes
                  && (c.Estado == "Atendida" || c.Estado == "atendida"), ct);
            ViewData["SatisfaccionPromedio"] = totalCitasMes > 0
                ? (int)Math.Round(citasAtendidasMes * 100.0 / totalCitasMes, 0)
                : 0;

            ViewData["SearchFilter"] = search;
            ViewData["ProfesionalFilter"] = profesional;
            ViewData["MesFilter"] = mes;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CargarDatosReportesClinicos cancelado");
            InicializarViewDataReportesVacios();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en CargarDatosReportesClinicos. Se devuelven datos vacios.");
            InicializarViewDataReportesVacios();
        }
    }

    private void InicializarViewDataReportesVacios()
    {
        ViewData["ReportesClinicos"] = new List<ReporteClinicoViewModel>();
        ViewData["ReportesClinicosPage"] = PagedResult<ReporteClinicoViewModel>.Empty(1, 10);
        ViewData["ProfesionalesReportes"] = new List<string>();
        ViewData["TotalPacientesReportes"] = 0;
        ViewData["ConsultasMes"] = 0;
        ViewData["SatisfaccionPromedio"] = 0;
        ViewData["SearchFilter"] = string.Empty;
        ViewData["ProfesionalFilter"] = string.Empty;
        ViewData["MesFilter"] = string.Empty;
    }

    private async Task CargarDatosProfesionales(string returnUrl, PaginationQuery? query = null, CancellationToken ct = default)
    {
        try
        {
            var pagination = query ?? new PaginationQuery();
            int page = pagination.Page < 1 ? 1 : pagination.Page;
            int pageSize = pagination.PageSize < 1 ? 10 : pagination.PageSize;

            ViewData["StatTotal"] = await _context.Profesionales.CountAsync(ct);
            ViewData["StatActivos"] = await _context.Profesionales.CountAsync(p => p.Estado == "activo", ct);
            ViewData["StatVacaciones"] = await _context.Profesionales.CountAsync(p => p.Estado == "vacaciones", ct);
            ViewData["StatInactivos"] = await _context.Profesionales.CountAsync(p => p.Estado == "inactivo", ct);

            var profesionalesQuery = _context.Profesionales
                .Include(p => p.Usuario)
                .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(pagination.Search))
            {
                string searchTerm = pagination.Search.Trim();
                profesionalesQuery = profesionalesQuery.Where(p =>
                    (p.Usuario != null && ((p.Usuario.Nombre != null && p.Usuario.Nombre.Contains(searchTerm)) || (p.Usuario.Apellidos != null && p.Usuario.Apellidos.Contains(searchTerm)))) ||
                    (p.Nombres != null && p.Nombres.Contains(searchTerm)) ||
                    (p.Apellidos != null && p.Apellidos.Contains(searchTerm)) ||
                    (p.RegistroMedico != null && p.RegistroMedico.Contains(searchTerm)) ||
                    (p.Especialidades.Any(pe => pe.Especialidad != null && pe.Especialidad.Nombre.Contains(searchTerm))));
            }

            if (!string.IsNullOrWhiteSpace(pagination.Profesional))
            {
                string especialidad = pagination.Profesional.Trim();
                profesionalesQuery = profesionalesQuery.Where(p => p.Especialidades.Any(pe => pe.Especialidad != null && pe.Especialidad.Nombre == especialidad));
            }

            if (!string.IsNullOrWhiteSpace(pagination.Estado))
            {
                string estado = pagination.Estado.Trim();
                profesionalesQuery = profesionalesQuery.Where(p => p.Estado == estado);
            }

            profesionalesQuery = profesionalesQuery.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres);

            var paged = await profesionalesQuery.ToPagedResultAsync(page, pageSize, ct);

            ViewData["Profesionales"] = paged.Items.ToList();
            ViewData["ProfesionalesPage"] = paged;
            ViewData["PaginationQuery"] = pagination;
            ViewData["SearchFilter"] = pagination.Search ?? string.Empty;
            ViewData["EspecialidadFilter"] = pagination.Profesional ?? string.Empty;
            ViewData["EstadoFilter"] = pagination.Estado ?? string.Empty;
            ViewData["Especialidades"] = await _context.Especialidades.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync(ct);
            ViewData["ReturnUrl"] = returnUrl;

            ViewData["EditingProfesional"] = null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("CargarDatosProfesionales cancelado");
            InicializarViewDataProfesionalesVacio(returnUrl);
        }
        catch (SqlException sqlex)
        {
            _logger.LogCritical(sqlex, "SqlException en CargarDatosProfesionales. Number={Number}", sqlex.Number);
            InicializarViewDataProfesionalesVacio(returnUrl);
            TempData["ErrorValidacion"] = MensajeErrorFallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error general en CargarDatosProfesionales. Se devuelven listas vacias.");
            InicializarViewDataProfesionalesVacio(returnUrl);
        }
    }

    private void InicializarViewDataProfesionalesVacio(string returnUrl)
    {
        ViewData["StatTotal"] = 0;
        ViewData["StatActivos"] = 0;
        ViewData["StatVacaciones"] = 0;
        ViewData["StatInactivos"] = 0;
        ViewData["Profesionales"] = new List<Profesional>();
        ViewData["ProfesionalesPage"] = PagedResult<Profesional>.Empty(1, 10);
        ViewData["PaginationQuery"] = new PaginationQuery();
        ViewData["SearchFilter"] = string.Empty;
        ViewData["EspecialidadFilter"] = string.Empty;
        ViewData["EstadoFilter"] = string.Empty;
        ViewData["Especialidades"] = new List<Especialidad>();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["EditingProfesional"] = null;
    }

    private static bool EsViolacionIndiceUnico(DbUpdateException dbex, out string? indiceAfectado)
    {
        indiceAfectado = null;
        var sqlEx = dbex.InnerException as SqlException ?? dbex.InnerException?.InnerException as SqlException;
        if (sqlEx == null) return false;
        if (sqlEx.Number is 2601 or 2627)
        {
            indiceAfectado = sqlEx.Message;
            return true;
        }
        return false;
    }

    private string BuildReturnUrl()
    {
        var queryString = HttpContext.Request.QueryString;
        return HttpContext.Request.Path + queryString;
    }

    private static bool EsViolacionIntegridadReferencial(DbUpdateException dbex)
    {
        var sqlEx = dbex.InnerException as SqlException ?? dbex.InnerException?.InnerException as SqlException;
        if (sqlEx == null) return false;
        return sqlEx.Number is 547 or 515;
    }
}
