/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: GestionProfesionalesController
 * ============================================
 * Archivo: Controllers/GestionDeProfesionales/ProfesionalesController.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Controlador MVC responsable de la gestión integral del cuerpo profesional (odontólogos,
 * especialistas y auxiliares). Administra la interfaz de usuario para el registro, edición,
 * consulta de perfiles y reportes clínicos de profesionales de la clínica.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Requiere rol "Administrador" para la edición/creación de perfiles y asignación de credenciales.
 * - Validación estricta de Registro Médico único y formato de correo corporativo/teléfono.
 * - Sincronización automática de credenciales de usuario (Hash de contraseña vía BCrypt/Helper)
 *   para garantizar la integridad entre la entidad Profesional y la entidad Usuario de acceso.
 * - Integración con IProfesionalService para separar la persistencia de la lógica de presentación.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, IProfesionalService, ProfesionalEstadoHelper, ILogger
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
using SmileTrack_MVC.Services;
using System.Globalization;
using System.Text.Json;

namespace SmileTrack_MVC.Controllers;

public partial class GestionProfesionalesController(
    AppDbContext context,
    ILogger<GestionProfesionalesController> logger,
    IProfesionalService profesionalService,
    IWebHostEnvironment environment) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<GestionProfesionalesController> _logger = logger;
    private readonly IProfesionalService _profesionalService = profesionalService;
    private readonly IWebHostEnvironment _environment = environment;
    private static readonly HashSet<string> TiposCredencial = new(StringComparer.Ordinal)
    {
        "Certificado médico", "Título de especialidad", "Estudios complementarios", "Registro profesional"
    };

    private const string MensajeErrorFallback =
        "Ocurrió un error inesperado al cargar la página. Por favor intente nuevamente. Si el problema persiste, contacte al soporte.";

    private static bool EsTelefonoValido(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return true;
        string digitsOnly = new string(telefono.Where(char.IsDigit).ToArray());
        return digitsOnly.Length is >= 7 and <= 15;
    }

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
            ViewData["CredencialesProfesionales"] = await _context.CredencialesProfesionales
                .AsNoTracking()
                .Include(d => d.Profesional)
                .OrderByDescending(d => d.FechaSubida)
                .ToListAsync(ct);
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
        // ── A6: Control de acceso por rol para reportes clínicos (RN-18) ────
        // Profesional solo puede ver sus propios datos.
        // Si no tiene fila asociada en la tabla Profesionales, se deniega.
        int? idProfesionalForzado = null;
        if (User.IsInRole("Profesional"))
        {
            string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                TempData["ErrorValidacion"] = "No fue posible identificar al profesional autenticado.";
                return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
            }

            var profCheck = await _context.Profesionales.AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);

            if (profCheck is null)
            {
                TempData["ErrorValidacion"] = "Su cuenta no está vinculada a un profesional activo. Contacte al administrador.";
                return View("~/Views/Gestion_De_Profesionales/st-adm-14-reportes-clinicos/index.cshtml");
            }

            // Forzar filtro al propio profesional; ignorar cualquier ?profesional= externo
            idProfesionalForzado = profCheck.IdProfesional;
            profesional = null;
        }

        try
        {
            await CargarDatosProfesionales(BuildReturnUrl(), null, ct);
            await CargarDatosReportesClinicos(page, pageSize, search, profesional, mes, idProfesionalForzado, ct);
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
    [Authorize(Roles = "Profesional,Administrador")]
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

                    // ═══════════════════════════════════════════════════════════════
                    // NUEVAS CONSULTAS PARA MEJORAS DEL DASHBOARD
                    // ═══════════════════════════════════════════════════════════════

                    // 1. PRÓXIMA CITA URGENTE (con datos del paciente incluidos)
                    var proximaCitaUrgente = await _context.Citas
                        .Include(c => c.Paciente)
                        .Include(c => c.Servicio)
                        .Include(c => c.Consultorio)
                        .AsNoTracking()
                        .Where(c =>
                            c.IdProfesional == profesional.IdProfesional &&
                            c.FechaHora >= DateTime.Now &&
                            (c.Estado == "Agendada" || c.Estado == "programada" || 
                             c.Estado == "Confirmada" || c.Estado == "confirmada"))
                        .OrderBy(c => c.FechaHora)
                        .FirstOrDefaultAsync(ct);

                    ViewData["OdoProximaCitaUrgente"] = proximaCitaUrgente;

                    // 2. CITAS PENDIENTES DE CONFIRMAR (estado "Agendada" o "programada")
                    var citasPendientesConfirmar = await _context.Citas
                        .AsNoTracking()
                        .CountAsync(c =>
                            c.IdProfesional == profesional.IdProfesional &&
                            c.FechaHora >= hoy &&
                            (c.Estado == "Agendada" || c.Estado == "programada"), ct);

                    ViewData["CitasPendientesConfirmar"] = citasPendientesConfirmar;

                    // 3. PACIENTES EN ESPERA (citas de hoy en estado "Confirmada" y hora ya pasada)
                    var ahora = DateTime.Now;
                    var pacientesEnEspera = await _context.Citas
                        .AsNoTracking()
                        .CountAsync(c =>
                            c.IdProfesional == profesional.IdProfesional &&
                            c.FechaHora.Date == hoy &&
                            c.FechaHora <= ahora &&
                            (c.Estado == "Confirmada" || c.Estado == "confirmada"), ct);

                    ViewData["PacientesEnEspera"] = pacientesEnEspera;

                    // 4. HISTORIAS CLÍNICAS PENDIENTES (citas atendidas hoy sin notas)
                    var historiasPendientes = await _context.Citas
                        .AsNoTracking()
                        .CountAsync(c =>
                            c.IdProfesional == profesional.IdProfesional &&
                            c.FechaHora.Date == hoy &&
                            (c.Estado == "Atendida" || c.Estado == "atendida") &&
                            string.IsNullOrWhiteSpace(c.Notas), ct);

                    ViewData["HistoriasPendientes"] = historiasPendientes;

                    // 5. RENDIMIENTO DEL DÍA (cálculo de tasa de asistencia)
                    var citasHoyTotal = citasDelMes.Count(c => c.FechaHora.Date == hoy);
                    var citasHoyAtendidas = citasDelMes.Count(c => 
                        c.FechaHora.Date == hoy && 
                        (c.Estado == "Atendida" || c.Estado == "atendida"));
                    
                    var tasaAsistencia = citasHoyTotal > 0 
                        ? (int)Math.Round((double)citasHoyAtendidas / citasHoyTotal * 100) 
                        : 0;

                    ViewData["TasaAsistenciaHoy"] = tasaAsistencia;

                    // 6. ÚLTIMA ATENCIÓN DEL PACIENTE DE LA PRÓXIMA CITA (para contexto)
                    if (proximaCitaUrgente != null)
                    {
                        var ultimaAtencion = await _context.Citas
                            .Include(c => c.Servicio)
                            .AsNoTracking()
                            .Where(c =>
                                c.IdPaciente == proximaCitaUrgente.IdPaciente &&
                                c.IdProfesional == profesional.IdProfesional &&
                                c.FechaHora < proximaCitaUrgente.FechaHora &&
                                (c.Estado == "Atendida" || c.Estado == "atendida"))
                            .OrderByDescending(c => c.FechaHora)
                            .FirstOrDefaultAsync(ct);

                        ViewData["UltimaAtencionProximaCita"] = ultimaAtencion;
                    }
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
    [Authorize(Roles = "Profesional,Administrador")]
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
                ViewData["CredencialesProfesional"] = profesional is null
                    ? new List<CredencialProfesional>()
                    : await _context.CredencialesProfesionales.AsNoTracking()
                        .Where(d => d.IdProfesional == profesional.IdProfesional)
                        .OrderByDescending(d => d.FechaSubida)
                        .ToListAsync(ct);
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

    [HttpPost]
    [Authorize(Roles = "Profesional")]
    [ValidateAntiForgeryToken]
    [Route("gestion-de-profesionales/credenciales/subir")]
    public async Task<IActionResult> SubirCredencialProfesional(
        IFormFile? archivo,
        string? tipo,
        CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId))
            return Forbid();

        var profesional = await _context.Profesionales
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.IdUsuario == userId, ct);
        if (profesional is null)
            return NotFound();

        if (archivo is null || archivo.Length is <= 0 or > 10_485_760 ||
            string.IsNullOrWhiteSpace(tipo) || !TiposCredencial.Contains(tipo) ||
            Path.GetFileName(archivo.FileName).Length > 255)
        {
            TempData["ErrorValidacion"] = "Selecciona un tipo de credencial y un archivo PDF o imagen de hasta 10 MB.";
            return RedirectToAction(nameof(Stodo09PerfilProfesional));
        }

        string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        string contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => string.Empty
        };
        if (contentType.Length == 0)
        {
            TempData["ErrorValidacion"] = "Solo se permiten archivos PDF, JPG, JPEG o PNG.";
            return RedirectToAction(nameof(Stodo09PerfilProfesional));
        }

        await using var source = archivo.OpenReadStream();
        var header = new byte[8];
        int read = await source.ReadAsync(header, ct);
        bool validSignature = extension switch
        {
            ".pdf" => read >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46 && header[4] == 0x2D,
            ".png" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            _ => false
        };
        if (!validSignature)
        {
            TempData["ErrorValidacion"] = "El contenido del archivo no coincide con un PDF o una imagen válida.";
            return RedirectToAction(nameof(Stodo09PerfilProfesional));
        }

        source.Position = 0;
        string fileName = $"{Guid.NewGuid():N}{extension}";
        string relativePath = Path.Combine("private_uploads", "credenciales-profesionales", profesional.IdProfesional.ToString(), fileName);
        string fullPath = Path.Combine(_environment.ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            await using (var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await source.CopyToAsync(destination, ct);

            _context.CredencialesProfesionales.Add(new CredencialProfesional
            {
                IdProfesional = profesional.IdProfesional,
                SubidoPor = userId,
                Tipo = tipo,
                NombreOriginal = Path.GetFileName(archivo.FileName),
                RutaPrivada = relativePath,
                ContentType = contentType,
                TamanoBytes = archivo.Length,
                EstadoRevision = "pendiente",
                FechaSubida = DateTime.UtcNow
            });
            profesional.CredencialesAprobadas = false;
            profesional.Estado = "inactivo";
            await _context.SaveChangesAsync(ct);
            TempData["Mensaje"] = "Documento cargado. El administrador revisará tus credenciales antes de activar tu perfil.";
        }
        catch
        {
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
            throw;
        }

        return RedirectToAction(nameof(Stodo09PerfilProfesional));
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Profesional")]
    [Route("gestion-de-profesionales/credenciales/{id:int}/archivo")]
    public async Task<IActionResult> DescargarCredencialProfesional(int id, CancellationToken ct = default)
    {
        var documento = await _context.CredencialesProfesionales
            .Include(d => d.Profesional)
            .FirstOrDefaultAsync(d => d.IdCredencial == id, ct);
        if (documento is null)
            return NotFound();

        if (User.IsInRole("Profesional") && !User.IsInRole("Administrador") &&
            (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int userId) ||
             documento.Profesional?.IdUsuario != userId))
            return Forbid();

        string credentialsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "private_uploads", "credenciales-profesionales")) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, documento.RutaPrivada));
        if (!fullPath.StartsWith(credentialsRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, documento.ContentType, documento.NombreOriginal);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    [Route("gestion-de-profesionales/credenciales/{id:int}/revisar")]
    public async Task<IActionResult> RevisarCredencialProfesional(
        int id,
        string? decision,
        string? observacion,
        CancellationToken ct = default)
    {
        string estado = decision?.Trim().ToLowerInvariant() ?? string.Empty;
        if (estado is not ("aprobado" or "rechazado"))
            return BadRequest();

        var documento = await _context.CredencialesProfesionales
            .Include(d => d.Profesional)
                .ThenInclude(p => p!.Usuario)
            .FirstOrDefaultAsync(d => d.IdCredencial == id, ct);
        if (documento?.Profesional is null)
            return NotFound();

        documento.EstadoRevision = estado;
        documento.ObservacionRevision = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        documento.RevisadoPor = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int reviewerId)
            ? reviewerId
            : null;
        documento.FechaRevision = DateTime.UtcNow;

        var profesional = documento.Profesional;
        if (estado == "rechazado")
        {
            profesional.CredencialesAprobadas = false;
            profesional.Estado = "inactivo";
        }
        else
        {
            bool tieneDocumentosSinAprobar = await _context.CredencialesProfesionales.AnyAsync(d =>
                d.IdProfesional == profesional.IdProfesional &&
                d.IdCredencial != documento.IdCredencial &&
                d.EstadoRevision == "pendiente", ct);
            if (!tieneDocumentosSinAprobar)
            {
                profesional.CredencialesAprobadas = true;
                profesional.Estado = "activo";
                if (profesional.Usuario is not null)
                    profesional.Usuario.Estado = "activo";

                var bloqueos = await _context.BloqueosProfesional
                    .Where(b => b.IdProfesional == profesional.IdProfesional && b.Motivo == "Profesional inactivo / desactivado")
                    .ToListAsync(ct);
                _context.BloqueosProfesional.RemoveRange(bloqueos);
            }
        }

        await _context.SaveChangesAsync(ct);
        TempData["Mensaje"] = estado == "aprobado"
            ? "Credencial aprobada."
            : "Credencial rechazada; el perfil permanece inactivo.";
        return RedirectToAction(nameof(Stadm07GestionProfesionales));
    }

    // ─── CRUD MIGRADO A LA API ──────────────────────────────────────────────
    // Las operaciones GuardarProfesional (POST/PUT) y EliminarProfesional (DELETE/PATCH)
    // fueron eliminadas en la Fase 2E porque el frontend ahora consume directamente
    // ProfesionalesApiController.cs.

    private async Task CargarDatosReportesClinicos(int page, int pageSize, string? search = null, string? profesional = null, string? mes = null, int? idProfesionalForzado = null, CancellationToken ct = default)
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

            // A6: si viene idProfesionalForzado (rol Profesional) lo usamos directamente;
            // si no (rol Administrador), permitir filtro por nombre desde el parámetro.
            if (idProfesionalForzado.HasValue)
            {
                citasQuery = citasQuery.Where(c => c.IdProfesional == idProfesionalForzado.Value);
            }
            else if (User.IsInRole("Profesional"))
            {
                // Fallback de seguridad: si llegamos aquí sin idProfesionalForzado siendo
                // Profesional, es un escenario no esperado — filtramos por 0 (sin resultados).
                citasQuery = citasQuery.Where(c => c.IdProfesional == 0);
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

            var todasCitasPacientes = await citasQuery
                .OrderByDescending(c => c.FechaHora)
                .AsNoTracking()
                .ToListAsync(ct);

            var citasUnicasPorPaciente = todasCitasPacientes
                .GroupBy(c => c.IdPaciente)
                .Select(g => g.OrderByDescending(c => c.FechaHora).First())
                .OrderByDescending(c => c.FechaHora)
                .ToList();

            // ── Cargar últimas notas clínicas por paciente (fuente real: tabla Nota_Clinica) ──
            var idsPacientes = citasUnicasPorPaciente.Select(c => c.IdPaciente).Distinct().ToList();
            Dictionary<int, (string? Diagnostico, string? Procedimiento, string? ProfesionalNombre)> ultimasNotas = new();

            if (idsPacientes.Count > 0)
            {
                var notasQuery = await _context.NotasClinicas
                    .AsNoTracking()
                    .Include(n => n.HistoriaClinica)
                    .Include(n => n.Profesional)
                    .ThenInclude(p => p!.Usuario)
                    .Where(n => idsPacientes.Contains(n.HistoriaClinica!.IdPaciente))
                    .OrderByDescending(n => n.Fecha)
                    .ToListAsync(ct);

                ultimasNotas = notasQuery
                    .Where(n => n.HistoriaClinica != null)
                    .GroupBy(n => n.HistoriaClinica!.IdPaciente)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var nota = g.First();
                            var profesionalNota = nota.Profesional;
                            var profesionalNombre = profesionalNota == null
                                ? null
                                : profesionalNota.Usuario != null
                                    ? $"{profesionalNota.Usuario.Nombre} {profesionalNota.Usuario.Apellidos}".Trim()
                                    : $"{profesionalNota.Nombres} {profesionalNota.Apellidos}".Trim();

                            return (nota.Diagnostico, nota.Procedimiento, ProfesionalNombre: profesionalNombre);
                        }
                    );
            }

            var totalReales = citasUnicasPorPaciente.Count;
            var pagedCitas = new PagedResult<Cita>
            {
                Page = page < 1 ? 1 : page,
                PageSize = pageSize < 1 ? 10 : pageSize,
                TotalCount = totalReales,
                Items = citasUnicasPorPaciente
                    .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 500))
                    .Take(Math.Clamp(pageSize, 1, 500))
                    .ToList()
            };

            var profesionalesOptions = await _context.Profesionales
                .Where(p => p.Estado == "activo")
                .Select(p => (p.Nombres ?? "") + " " + (p.Apellidos ?? ""))
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct()
                .OrderBy(name => name)
                .ToListAsync(ct);

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

                ultimasNotas.TryGetValue(cita.IdPaciente, out var nota);

                var profesionalCitaNombre = cita.Profesional is null
                    ? null
                    : !string.IsNullOrWhiteSpace(cita.Profesional.Usuario?.Nombre) || !string.IsNullOrWhiteSpace(cita.Profesional.Usuario?.Apellidos)
                        ? $"{cita.Profesional.Usuario?.Nombre} {cita.Profesional.Usuario?.Apellidos}".Trim()
                        : $"{cita.Profesional.Nombres} {cita.Profesional.Apellidos}".Trim();

                string diagnostico = !string.IsNullOrWhiteSpace(nota.Diagnostico)
                    ? nota.Diagnostico!
                    : !string.IsNullOrWhiteSpace(cita.Notas)
                        ? cita.Notas.Trim()
                        : !string.IsNullOrWhiteSpace(cita.MotivoConsulta)
                            ? cita.MotivoConsulta.Trim()
                            : "Sin diagnóstico registrado";

                string procedimiento = !string.IsNullOrWhiteSpace(nota.Procedimiento)
                    ? nota.Procedimiento!
                    : !string.IsNullOrWhiteSpace(cita.Servicio?.Nombre)
                        ? cita.Servicio.Nombre
                        : !string.IsNullOrWhiteSpace(cita.MotivoConsulta)
                            ? cita.MotivoConsulta.Trim()
                            : "—";

                string? alerta = !string.IsNullOrWhiteSpace(cita.Notas)
                    ? cita.Notas.Trim()
                    : !string.IsNullOrWhiteSpace(cita.MotivoConsulta)
                        ? $"Motivo de consulta: {cita.MotivoConsulta.Trim()}"
                        : null;

                return new ReporteClinicoViewModel
                {
                    Id = cita.IdCita,
                    NombrePaciente = $"{cita.Paciente?.Nombres} {cita.Paciente?.Apellidos}".Trim(),
                    Documento = cita.Paciente?.Documento ?? string.Empty,
                    Edad = edad,
                    UltimaConsulta = cita.FechaHora,
                    Diagnostico = diagnostico,
                    Procedimiento = procedimiento,
                    ProfesionalNombre = !string.IsNullOrWhiteSpace(nota.ProfesionalNombre)
                        ? nota.ProfesionalNombre!
                        : !string.IsNullOrWhiteSpace(profesionalCitaNombre)
                            ? profesionalCitaNombre
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
            if (idProfesionalForzado.HasValue)
            {
                consultasQuery = consultasQuery.Where(c => c.IdProfesional == idProfesionalForzado.Value);
            }
            else if (User.IsInRole("Profesional"))
            {
                consultasQuery = consultasQuery.Where(c => c.IdProfesional == 0);
            }

            ViewData["ConsultasMes"] = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);

            // Tasa de asistencia = % citas atendidas sobre total del mes
            // (antes llamado SatisfaccionPromedio, renombrado por alineación funcional)
            int totalCitasMes = await consultasQuery.CountAsync(c => c.FechaHora >= inicioMes && c.FechaHora < finMes, ct);
            int citasAtendidasMes = await consultasQuery.CountAsync(
                c => c.FechaHora >= inicioMes && c.FechaHora < finMes
                  && (c.Estado == "Atendida" || c.Estado == "atendida"), ct);
            int tasaAsistencia = totalCitasMes > 0
                ? (int)Math.Round(citasAtendidasMes * 100.0 / totalCitasMes, 0)
                : 0;
            ViewData["TasaAsistencia"] = tasaAsistencia;
            ViewData["SatisfaccionPromedio"] = tasaAsistencia; // backward compat

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
        ViewData["TasaAsistencia"] = 0;
        ViewData["SatisfaccionPromedio"] = 0; // backward compat
        ViewData["SearchFilter"] = string.Empty;
        ViewData["ProfesionalFilter"] = string.Empty;
        ViewData["MesFilter"] = string.Empty;
    }

    private async Task CargarDatosProfesionales(string returnUrl, PaginationQuery? query = null, CancellationToken ct = default)
    {
        try
        {
            var pagination = query ?? new PaginationQuery();
            var resultado = await _profesionalService.ObtenerVistaMVCAsync(pagination, ct);

            ViewData["StatTotal"] = resultado.Stats.StatTotal;
            ViewData["StatActivos"] = resultado.Stats.StatActivos;
            ViewData["StatVacaciones"] = resultado.Stats.StatVacaciones;
            ViewData["StatInactivos"] = resultado.Stats.StatInactivos;
            ViewData["Profesionales"] = resultado.Items;
            ViewData["ProfesionalesPage"] = resultado.Paginacion;
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
