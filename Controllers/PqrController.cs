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

    // Estados terminales: la opción A impide su reapertura o modificación.
    private static readonly HashSet<string> EstadosFinales = new(StringComparer.OrdinalIgnoreCase)
        { "resuelta", "cerrada", "rechazada" };

    private static bool TransicionPermitida(string actual, string siguiente)
    {
        if (EstadosFinales.Contains(actual)) return false;
        return actual switch
        {
            "recibida" => siguiente is "en_proceso" or "resuelta" or "cerrada" or "rechazada",
            "en_proceso" => siguiente is "resuelta" or "cerrada" or "rechazada",
            _ => false
        };
    }

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
            .Take(25)
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
            total = await _context.PQRs.CountAsync(),
            sinResponder = await _context.PQRs.CountAsync(p => p.Estado == "recibida"),
            enGestion = await _context.PQRs.CountAsync(p => p.Estado == "en_proceso"),
            vencidas = await _context.PQRs.CountAsync(p => p.Estado != "resuelta" && p.Estado != "cerrada" && p.Estado != "rechazada"
                && p.FechaCreacion < DateTime.Now.AddDays(-15))
        };

        ViewData["PqrsJson"] = System.Text.Json.JsonSerializer.Serialize(pqrs);
        ViewData["PqrStatsJson"] = System.Text.Json.JsonSerializer.Serialize(stats);
        return View("~/Views/Gestion_De_PQR/st-adm-17-gestion-pqr/index.cshtml", pqrs);
    }

    // =========================================================================
    // API REST - Consulta de PQR para actualización automática de la bandeja.
    // La tabla PQR ya existe; no se requiere modificar la base de datos.
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/api/pqr")]
    public async Task<IActionResult> ApiListarPqrs(
        [FromQuery] int pagina = 1,
        [FromQuery] string? buscar = null,
        [FromQuery] string? tipo = null,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        const int tamanoPagina = 25;
        pagina = Math.Max(1, pagina);
        buscar = buscar?.Trim();
        if (buscar?.Length > 100)
            return BadRequest(new { success = false, message = "La búsqueda no puede superar 100 caracteres." });

        // Estadísticas globales independientes de los filtros.
        var total = await _context.PQRs.AsNoTracking().CountAsync(ct);
        var sinResponder = await _context.PQRs.AsNoTracking().CountAsync(p => p.Estado == "recibida", ct);
        var enGestion = await _context.PQRs.AsNoTracking().CountAsync(p => p.Estado == "en_proceso", ct);
        var limite = DateTime.Now.AddDays(-15);
        var vencidas = await _context.PQRs.AsNoTracking().CountAsync(p =>
            p.Estado != "resuelta" && p.Estado != "cerrada" && p.Estado != "rechazada"
            && p.FechaCreacion < limite, ct);

        var consulta = _context.PQRs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var termino = buscar;
            var numero = termino.StartsWith("PQR-", StringComparison.OrdinalIgnoreCase)
                ? termino[4..] : termino;
            var esRadicado = int.TryParse(numero, out var idBuscado);
            consulta = consulta.Where(p =>
                p.Asunto.Contains(termino) ||
                (p.Paciente != null && (p.Paciente.Nombres.Contains(termino)
                    || p.Paciente.Apellidos.Contains(termino))) ||
                (esRadicado && p.IdPqr == idBuscado));
        }
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "todos")
        {
            if (!TiposValidos.Contains(tipo))
                return BadRequest(new { success = false, message = "Tipo de PQR no válido." });
            consulta = consulta.Where(p => p.Tipo == tipo);
        }
        if (!string.IsNullOrWhiteSpace(estado) && estado != "todos")
        {
            var estadoNormalizado = estado switch
            {
                "recibido" => "recibida", "en-proceso" => "en_proceso",
                "resuelto" => "resuelta", "cerrado" => "cerrada", "rechazado" => "rechazada",
                _ => estado
            };
            if (estadoNormalizado is not ("recibida" or "en_proceso" or "resuelta" or "cerrada" or "rechazada"))
                return BadRequest(new { success = false, message = "Estado de PQR no válido." });
            consulta = consulta.Where(p => p.Estado == estadoNormalizado);
        }

        var totalFiltrado = await consulta.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalFiltrado / (double)tamanoPagina));
        pagina = Math.Min(pagina, totalPaginas);
        var registros = await consulta
            .Include(p => p.Paciente)
            .Include(p => p.AtendidaPorUsuario)
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.IdPqr)
            .Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina)
            .ToListAsync(ct);

        var pqrs = registros.Select(p => new
        {
            id = p.IdPqr, ticket = $"PQR-{p.IdPqr:D4}",
            patient = p.Paciente != null ? $"{p.Paciente.Nombres} {p.Paciente.Apellidos}" : "Anónimo",
            documento = p.Paciente?.Documento ?? "N/A", email = p.Paciente?.Correo ?? "N/A",
            type = p.Tipo, subject = p.Asunto, description = p.Descripcion,
            status = p.Estado, priority = p.Prioridad,
            date = p.FechaCreacion.ToString("yyyy-MM-dd HH:mm"),
            fechaCreacionIso = p.FechaCreacion.ToString("o"),
            respuesta = p.Respuesta,
            fechaRespuesta = p.FechaRespuesta?.ToString("yyyy-MM-dd HH:mm"),
            atendidaPor = p.AtendidaPorUsuario != null ? $"{p.AtendidaPorUsuario.Nombre} {p.AtendidaPorUsuario.Apellidos}" : null,
            evidenciaAdjunto = p.EvidenciaAdjunto
        }).ToList();

        return Ok(new
        {
            success = true, pqrs,
            stats = new { total, sinResponder, enGestion, vencidas },
            paginacion = new { pagina, tamanoPagina, totalFiltrado, totalPaginas }
        });
    }

    // =========================================================================
    // API REST - Gestión real de PQR
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/api/pqr/{id:int}/historial")]
    public async Task<IActionResult> ApiHistorialPqr(int id, CancellationToken ct = default)
    {
        if (!await _context.PQRs.AsNoTracking().AnyAsync(p => p.IdPqr == id, ct))
            return NotFound(new { success = false, message = "PQR no encontrada." });

        var eventos = await _context.PqrHistorial.AsNoTracking()
            .Where(h => h.IdPqr == id)
            .OrderBy(h => h.FechaEvento).ThenBy(h => h.IdHistorial)
            .Select(h => new { h.IdHistorial, h.IdUsuario, h.EstadoAnterior, h.EstadoNuevo,
                h.Respuesta, h.TipoEvento, h.FechaEvento })
            .ToListAsync(ct);
        return Ok(new { success = true, eventos });
    }

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

            if (!TransicionPermitida(pqr.Estado, request.Estado))
            {
                return Conflict(new { success = false, message = EstadosFinales.Contains(pqr.Estado)
                    ? "La PQR está finalizada y no admite modificaciones ni reapertura."
                    : "La transición de estado solicitada no está permitida." });
            }

            var estadoAnterior = pqr.Estado;
            pqr.Estado = request.Estado;
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
            _context.PqrHistorial.Add(new PqrHistorial
            {
                IdPqr = id, IdUsuario = usuarioId > 0 ? usuarioId : null,
                EstadoAnterior = estadoAnterior, EstadoNuevo = request.Estado,
                TipoEvento = "estado", FechaEvento = DateTime.UtcNow
            });
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

            if (EstadosFinales.Contains(pqr.Estado))
            {
                return Conflict(new { success = false, message = "La PQR está finalizada y no admite nuevas respuestas." });
            }

            string siguienteEstado = !string.IsNullOrWhiteSpace(request.NuevoEstado)
                ? request.NuevoEstado
                : pqr.Estado == "recibida" ? "en_proceso" : pqr.Estado;
            if (siguienteEstado != pqr.Estado && !TransicionPermitida(pqr.Estado, siguienteEstado))
            {
                return Conflict(new { success = false, message = "La transición de estado solicitada no está permitida." });
            }

            string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out int adminId);

            var estadoAnterior = pqr.Estado;
            pqr.Respuesta = request.Respuesta.Trim();
            pqr.FechaRespuesta = DateTime.Now;
            pqr.AtendidaPor = adminId > 0 ? adminId : null;

            pqr.Estado = siguienteEstado;
            _context.PqrHistorial.Add(new PqrHistorial
            {
                IdPqr = id, IdUsuario = adminId > 0 ? adminId : null,
                EstadoAnterior = estadoAnterior, EstadoNuevo = siguienteEstado,
                Respuesta = pqr.Respuesta, TipoEvento = "respuesta", FechaEvento = DateTime.UtcNow
            });
            // La respuesta, el estado y la bitácora se guardan en una sola transacción de SaveChanges.
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
