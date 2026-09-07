using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class PqrController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<PqrController> _logger;

    private static readonly string[] TiposValidos = ["peticion", "queja", "reclamo", "sugerencia"];
    private static readonly string[] TiposArchivoPermitidos = ["image/jpeg", "image/png", "application/pdf"];
    private const long TamanoMaximoArchivo = 5 * 1024 * 1024; // 5 MB

    public PqrController(AppDbContext context, ILogger<PqrController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/st-pac-04-nueva-pqr")]
    public async Task<IActionResult> Stpac04NuevaPqr()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        var misPqrs = paciente != null
            ? await _context.PQRs.Where(p => p.IdPaciente == paciente.IdPaciente).OrderByDescending(p => p.FechaCreacion).ToListAsync()
            : new List<PqrEntity>();

        ViewData["MisPqrsJson"] = System.Text.Json.JsonSerializer.Serialize(misPqrs);
        return View("~/Views/Gestion_De_PQR/st-pac-04-nueva-pqr/index.cshtml");
    }

    [HttpPost]
    [Authorize(Roles = "Paciente,Administrador,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPqr(
        [FromForm] string tipo,
        [FromForm] string asunto,
        [FromForm] string descripcion,
        IFormFile? evidencia,
        [FromServices] IWebHostEnvironment env)
    {
        // ── Validaciones básicas ────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(asunto) || asunto.Trim().Length < 3)
        {
            return BadRequest(new { success = false, message = "El asunto debe tener al menos 3 caracteres." });
        }

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length < 10)
        {
            return BadRequest(new { success = false, message = "La descripción debe tener al menos 10 caracteres." });
        }

        string tipoNormalizado = (tipo ?? "").Trim().ToLowerInvariant();
        if (!TiposValidos.Contains(tipoNormalizado))
        {
            tipoNormalizado = "peticion";
        }

        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);

        // El PQR siempre debe quedar asociado al paciente real que lo radica;
        // si el usuario autenticado no tiene un registro de Paciente asociado
        // (por ejemplo, personal administrativo probando el formulario), se
        // rechaza en vez de adjuntarlo a un paciente arbitrario de la base
        // de datos (bug anterior: `?? await _context.Pacientes.FirstOrDefaultAsync()`).
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        if (paciente == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "No se encontró un registro de paciente asociado a tu cuenta. Contacta a recepción para radicar la PQR."
            });
        }

        // ── Adjunto de evidencia (opcional) ─────────────────────────────────
        string? rutaRelativaEvidencia = null;
        if (evidencia is { Length: > 0 })
        {
            if (evidencia.Length > TamanoMaximoArchivo)
            {
                return BadRequest(new { success = false, message = "El archivo adjunto supera el límite de 5 MB." });
            }

            string contentType = evidencia.ContentType.ToLowerInvariant();
            if (!TiposArchivoPermitidos.Contains(contentType))
            {
                return BadRequest(new { success = false, message = "Tipo de archivo no permitido. Solo JPG, PNG y PDF." });
            }

            string carpetaRelativa = Path.Combine("uploads", "pqr", paciente.IdPaciente.ToString());
            string carpetaFisica = Path.Combine(env.WebRootPath, carpetaRelativa);
            Directory.CreateDirectory(carpetaFisica);

            string extension = Path.GetExtension(evidencia.FileName);
            string nombreFisico = $"{Guid.NewGuid():N}{extension}";
            string rutaFisica = Path.Combine(carpetaFisica, nombreFisico);

            await using (var stream = new FileStream(rutaFisica, FileMode.Create, FileAccess.Write))
            {
                await evidencia.CopyToAsync(stream);
            }

            rutaRelativaEvidencia = Path.Combine(carpetaRelativa, nombreFisico).Replace('\\', '/');
        }

        var nuevaPqr = new PqrEntity
        {
            IdPaciente = paciente.IdPaciente,
            IdUsuario = userId > 0 ? userId : null,
            Tipo = tipoNormalizado,
            Asunto = asunto.Trim(),
            Descripcion = descripcion.Trim(),
            Estado = "recibida",
            Prioridad = "media",
            FechaCreacion = DateTime.Now,
            EvidenciaAdjunto = rutaRelativaEvidencia
        };

        _context.PQRs.Add(nuevaPqr);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "PQR registrada exitosamente.", id = nuevaPqr.IdPqr });
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/st-adm-17-gestion-pqr")]
    public async Task<IActionResult> Stadm17GestionPqr()
    {
        var pqrsDb = await _context.PQRs
            .Include(p => p.Paciente)
            .Include(p => p.AtendidaPorUsuario)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();

        var pqrs = pqrsDb.Select(p => new
        {
            id = p.IdPqr,
            ticket = $"PQR-{p.IdPqr:D4}",
            patient = p.Paciente != null ? $"{p.Paciente.Nombres} {p.Paciente.Apellidos}" : "Anónimo",
            documento = p.Paciente?.Documento ?? "N/A",
            email = p.Paciente?.Correo ?? "N/A",
            type = p.Tipo,
            subject = p.Asunto,
            description = p.Descripcion,
            status = p.Estado,
            priority = p.Prioridad,
            date = p.FechaCreacion.ToString("yyyy-MM-dd HH:mm"),
            fechaCreacionIso = p.FechaCreacion.ToString("o"),
            respuesta = p.Respuesta,
            fechaRespuesta = p.FechaRespuesta.HasValue ? p.FechaRespuesta.Value.ToString("yyyy-MM-dd HH:mm") : null,
            atendidaPor = p.AtendidaPorUsuario != null ? $"{p.AtendidaPorUsuario.Nombre} {p.AtendidaPorUsuario.Apellidos}" : null,
            evidenciaAdjunto = p.EvidenciaAdjunto
        }).ToList();

        // Estadísticas reales (antes eran números fijos en el HTML: 6, 1, 1, 2)
        var stats = new
        {
            total = pqrsDb.Count,
            sinResponder = pqrsDb.Count(p => p.Estado == "recibida"),
            enGestion = pqrsDb.Count(p => p.Estado == "en_proceso"),
            vencidas = pqrsDb.Count(p => p.Estado != "resuelta" && p.Estado != "cerrada" && p.Estado != "rechazada"
                && (DateTime.Now - p.FechaCreacion).TotalDays > 15)
        };

        ViewData["PqrsJson"] = System.Text.Json.JsonSerializer.Serialize(pqrs);
        ViewData["PqrStatsJson"] = System.Text.Json.JsonSerializer.Serialize(stats);
        return View("~/Views/Gestion_De_PQR/st-adm-17-gestion-pqr/index.cshtml", pqrs);
    }

    // =========================================================================
    // API REST - Gestión real de PQR (reemplaza changeStatus/"Enviar respuesta"
    // que antes solo modificaban el DOM sin guardar nada en SQL Server)
    // =========================================================================

    [HttpPut]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/api/pqr/{id:int}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiActualizarEstado(int id, [FromBody] ActualizarEstadoPqrRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var pqr = await _context.PQRs.FirstOrDefaultAsync(p => p.IdPqr == id, ct);
            if (pqr == null)
            {
                return NotFound(new { success = false, message = "PQR no encontrada." });
            }

            pqr.Estado = request.Estado;
            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Estado actualizado correctamente.", data = new { id, estado = pqr.Estado } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando el estado de la PQR {IdPqr}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible actualizar el estado." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/api/pqr/{id:int}/responder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiResponderPqr(int id, [FromBody] ResponderPqrRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var pqr = await _context.PQRs.FirstOrDefaultAsync(p => p.IdPqr == id, ct);
            if (pqr == null)
            {
                return NotFound(new { success = false, message = "PQR no encontrada." });
            }

            string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int adminId);

            pqr.Respuesta = request.Respuesta.Trim();
            pqr.FechaRespuesta = DateTime.Now;
            pqr.AtendidaPor = adminId > 0 ? adminId : null;

            if (!string.IsNullOrWhiteSpace(request.NuevoEstado))
            {
                pqr.Estado = request.NuevoEstado;
            }
            else if (pqr.Estado == "recibida")
            {
                pqr.Estado = "en_proceso";
            }

            await _context.SaveChangesAsync(ct);

            var atendidaPorUsuario = adminId > 0 ? await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == adminId, ct) : null;

            return Ok(new
            {
                success = true,
                message = "Respuesta enviada correctamente.",
                data = new
                {
                    id,
                    estado = pqr.Estado,
                    respuesta = pqr.Respuesta,
                    fechaRespuesta = pqr.FechaRespuesta.Value.ToString("yyyy-MM-dd HH:mm"),
                    atendidaPor = atendidaPorUsuario != null ? $"{atendidaPorUsuario.Nombre} {atendidaPorUsuario.Apellidos}" : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error respondiendo la PQR {IdPqr}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible enviar la respuesta." });
        }
    }
}
