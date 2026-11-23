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

    private bool UsaFormularioSoporte => User.IsInRole("Profesional") || User.IsInRole("Auxiliar") || User.IsInRole("Recepcionista") || User.IsInRole("Administrador");

    private static bool CedulaValida(Paciente paciente) =>
        !string.Equals(paciente.TipoDocumento, "CC", StringComparison.OrdinalIgnoreCase) ||
        paciente.Documento is { Length: >= 1 and <= 10 } &&
        paciente.Documento.All(digito => digito is >= '0' and <= '9');

    public PqrController(AppDbContext context, ILogger<PqrController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/st-pac-04-nueva-pqr")]
    public async Task<IActionResult> Stpac04NuevaPqr([FromQuery] int? pqr = null)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) || userId <= 0)
            return Forbid();

        if (UsaFormularioSoporte)
        {
            var usuario = await _context.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == userId);
            if (usuario == null)
                return Forbid();

            var reportes = await ConsultarPqrsPropias(userId)
                .OrderByDescending(p => p.IdPqr == pqr).ThenByDescending(p => p.FechaCreacion)
                .Take(5).ToListAsync();
            ViewData["MisPqrsJson"] = System.Text.Json.JsonSerializer.Serialize(reportes);
            return View("~/Views/Gestion_De_PQR/soporte-personal/index.cshtml", usuario);
        }

        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.IdUsuario == userId);
        var misPqrs = paciente != null
            ? await ConsultarPqrsPropias(userId).Where(p => p.IdPaciente == paciente.IdPaciente)
                .OrderByDescending(p => p.IdPqr == pqr).ThenByDescending(p => p.FechaCreacion)
                .Take(5).ToListAsync()
            : new List<PqrEntity>();

        ViewData["Profesionales"] = await _context.Profesionales.AsNoTracking()
            .Where(p => p.Estado == "activo").OrderBy(p => p.Nombres).ThenBy(p => p.Apellidos)
            .Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(p.Nombres + " " + p.Apellidos, p.IdProfesional.ToString()))
            .ToListAsync();
        ViewData["DocumentoValido"] = paciente != null && CedulaValida(paciente);
        ViewData["MisPqrsJson"] = System.Text.Json.JsonSerializer.Serialize(misPqrs);
        return View("~/Views/Gestion_De_PQR/st-pac-04-nueva-pqr/index.cshtml", paciente);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador,Paciente,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/api/notificaciones")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ApiNotificacionesPqr(CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) || userId <= 0)
            return Forbid();

        var administrador = User.IsInRole("Administrador");
        var avisos = await ConsultarAvisosPqrAsync(userId, administrador, ct);
        var claves = avisos.Select(a => a.Clave).ToArray();
        var leidos = claves.Length == 0 ? new List<string>() : await _context.PqrAvisosLeidos.AsNoTracking()
            .Where(a => a.IdUsuario == userId && claves.Contains(a.Clave))
            .Select(a => a.Clave).ToListAsync(ct);

        return Ok(avisos.Select(a => new
        {
            id = a.Id,
            asunto = a.Asunto,
            radicado = $"PQR-{a.Id:D4}",
            clave = a.Clave,
            leido = leidos.Contains(a.Clave),
            url = administrador
                ? $"/gestion-de-pqr/st-adm-17-gestion-pqr?pqr={a.Id}"
                : $"/gestion-de-pqr/st-pac-04-nueva-pqr?pqr={a.Id}"
        }));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Paciente,Profesional,Recepcionista,Auxiliar")]
    [ValidateAntiForgeryToken]
    [Route("gestion-de-pqr/api/notificaciones/leida")]
    public async Task<IActionResult> ApiMarcarAvisoPqrLeido([FromForm] string? clave, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) || userId <= 0)
            return Forbid();

        if (string.IsNullOrWhiteSpace(clave) || clave.Length > 64)
            return BadRequest();

        var avisos = await ConsultarAvisosPqrAsync(userId, User.IsInRole("Administrador"), ct);
        if (!avisos.Any(a => a.Clave == clave))
            return NotFound();

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.PQR_Aviso_Leido (id_usuario, clave)
            SELECT {userId}, {clave}
            WHERE NOT EXISTS (
                SELECT 1 FROM dbo.PQR_Aviso_Leido WITH (UPDLOCK, HOLDLOCK)
                WHERE id_usuario = {userId} AND clave = {clave}
            )
            """, ct);

        return NoContent();
    }

    private IQueryable<PqrEntity> ConsultarPqrsPropias(int userId)
    {
        var propias = _context.PQRs.AsNoTracking().Where(p => p.IdUsuario == userId);
        return UsaFormularioSoporte
            ? propias.Where(p => p.IdPaciente == null)
            : propias.Where(p => p.Paciente != null && p.Paciente.IdUsuario == userId);
    }

    private sealed record AvisoPqr(int Id, string Asunto, string Clave);

    private async Task<List<AvisoPqr>> ConsultarAvisosPqrAsync(int userId, bool administrador, CancellationToken ct)
    {
        var consulta = administrador
            ? _context.PQRs.AsNoTracking().Where(p => p.Estado == "recibida")
            : ConsultarPqrsPropias(userId).Where(p => p.FechaRespuesta.HasValue);

        var avisos = await consulta
            .OrderByDescending(p => administrador ? p.FechaCreacion : p.FechaRespuesta)
            .ThenByDescending(p => p.IdPqr)
            .Select(p => new { p.IdPqr, p.Asunto, p.FechaRespuesta })
            .Take(20).ToListAsync(ct);

        return avisos.Select(p => new AvisoPqr(p.IdPqr, p.Asunto,
            administrador ? $"nueva:{p.IdPqr}" : $"respuesta:{p.IdPqr}:{p.FechaRespuesta?.Ticks}")).ToList();
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
        [FromServices] IWebHostEnvironment env,
        [FromForm] int? idProfesional = null,
        [FromForm] DateOnly? fechaServicio = null,
        [FromForm] string? moduloSoftware = null)
    {
        // ── Validaciones básicas ────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(asunto) || asunto.Trim().Length is < 3 or > 200)
        {
            return BadRequest(new { success = false, message = "El asunto debe tener entre 3 y 200 caracteres." });
        }

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length is < 10 or > 1000)
        {
            return BadRequest(new { success = false, message = "La descripción debe tener entre 10 y 1000 caracteres." });
        }

        string tipoNormalizado = (tipo ?? "").Trim().ToLowerInvariant();
        if (!TiposValidos.Contains(tipoNormalizado))
        {
            tipoNormalizado = "peticion";
        }

        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Revisa el formato de los campos del formulario." });

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId) || userId <= 0)
            return Forbid();

        var esPersonal = UsaFormularioSoporte;
        if (!esPersonal && !User.IsInRole("Paciente"))
            return Forbid();

        var paciente = esPersonal ? null : await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        if (!esPersonal && paciente == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "No se encontró un registro de paciente asociado a tu cuenta. Contacta a recepción para radicar la PQR."
            });
        }

        // ── Adjunto de evidencia (opcional) ─────────────────────────────────
        moduloSoftware = moduloSoftware?.Trim();
        if (esPersonal)
        {
            if (!await _context.Usuarios.AnyAsync(u => u.IdUsuario == userId))
                return Forbid();

            if (string.IsNullOrEmpty(moduloSoftware) || moduloSoftware.Length > 100)
                return BadRequest(new { success = false, message = "Indica un módulo o pantalla de hasta 100 caracteres." });

            tipoNormalizado = "queja";
            idProfesional = null;
            fechaServicio = null;
        }
        else
        {
            if (!CedulaValida(paciente!))
                return BadRequest(new { success = false, message = "La cédula registrada debe contener hasta 10 dígitos. Solicita la corrección de tu ficha." });

            if (idProfesional.HasValue && !await _context.Profesionales.AnyAsync(p =>
                p.IdProfesional == idProfesional.Value && p.Estado == "activo"))
                return BadRequest(new { success = false, message = "Selecciona un profesional activo del listado." });

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
            if (fechaServicio > hoy)
                return BadRequest(new { success = false, message = "La fecha del servicio no puede ser futura." });

            moduloSoftware = null;
        }

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

            string carpetaRelativa = Path.Combine("uploads", "pqr", paciente?.IdPaciente.ToString() ?? $"soporte-{userId}");
            string carpetaFisica = Path.Combine(env.WebRootPath, carpetaRelativa);
            Directory.CreateDirectory(carpetaFisica);

            string extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".pdf"
            };
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
            IdPaciente = paciente?.IdPaciente,
            IdUsuario = userId,
            IdProfesional = idProfesional,
            FechaServicio = fechaServicio,
            ModuloSoftware = moduloSoftware,
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

    private static async Task<IReadOnlyList<object>> CargarBandejaAsync(
        IQueryable<PqrEntity> consulta, CancellationToken ct = default)
    {
        var registros = await consulta.Select(p => new
        {
            p.IdPqr,
            patient = p.Paciente != null ? p.Paciente.Nombres + " " + p.Paciente.Apellidos
                : p.Usuario != null ? p.Usuario.Nombre + " " + p.Usuario.Apellidos : "Anónimo",
            p.ModuloSoftware,
            p.FechaServicio,
            profesional = p.Profesional != null ? p.Profesional.Nombres + " " + p.Profesional.Apellidos : null,
            documento = p.Paciente == null ? "N/A" : p.Paciente.Documento ?? "N/A",
            email = (p.Paciente == null ? null : p.Paciente.Correo)
                ?? (p.Usuario == null ? null : p.Usuario.Correo) ?? "N/A",
            p.Tipo,
            p.Asunto,
            p.Descripcion,
            p.Estado,
            p.Prioridad,
            p.FechaCreacion,
            p.Respuesta,
            p.FechaRespuesta,
            atendidaPor = p.AtendidaPorUsuario != null
                ? p.AtendidaPorUsuario.Nombre + " " + p.AtendidaPorUsuario.Apellidos : null,
            p.EvidenciaAdjunto
        }).ToListAsync(ct);

        return registros.Select(p => new
        {
            id = p.IdPqr,
            ticket = $"PQR-{p.IdPqr:D4}",
            p.patient,
            moduloSoftware = p.ModuloSoftware,
            fechaServicio = p.FechaServicio?.ToString("yyyy-MM-dd"),
            p.profesional,
            p.documento,
            p.email,
            type = p.Tipo,
            subject = p.Asunto,
            description = p.Descripcion,
            status = p.Estado,
            priority = p.Prioridad,
            date = p.FechaCreacion.ToString("yyyy-MM-dd HH:mm"),
            fechaCreacionIso = p.FechaCreacion.ToString("o"),
            respuesta = p.Respuesta,
            fechaRespuesta = p.FechaRespuesta?.ToString("yyyy-MM-dd HH:mm"),
            p.atendidaPor,
            evidenciaAdjunto = p.EvidenciaAdjunto
        }).ToList();
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/st-adm-17-gestion-pqr")]
    public async Task<IActionResult> Stadm17GestionPqr()
    {
        var pqrs = await CargarBandejaAsync(_context.PQRs.AsNoTracking()
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.IdPqr)
            .Take(25));

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
        CancellationToken ct = default,
        [FromQuery] string? origen = null)
    {
        const int tamanoPagina = 25;
        pagina = Math.Max(1, pagina);
        buscar = buscar?.Trim();
        if (buscar?.Length > 100)
            return BadRequest(new { success = false, message = "La búsqueda no puede superar 100 caracteres." });

        origen = origen?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(origen) && origen is not ("todos" or "pacientes" or "soporte"))
            return BadRequest(new { success = false, message = "Origen de PQR no válido." });

        // Estadísticas globales independientes de los filtros.
        var total = await _context.PQRs.AsNoTracking().CountAsync(ct);
        var sinResponder = await _context.PQRs.AsNoTracking().CountAsync(p => p.Estado == "recibida", ct);
        var enGestion = await _context.PQRs.AsNoTracking().CountAsync(p => p.Estado == "en_proceso", ct);
        var limite = DateTime.Now.AddDays(-15);
        var vencidas = await _context.PQRs.AsNoTracking().CountAsync(p =>
            p.Estado != "resuelta" && p.Estado != "cerrada" && p.Estado != "rechazada"
            && p.FechaCreacion < limite, ct);

        var consulta = _context.PQRs.AsNoTracking().AsQueryable();
        consulta = origen switch
        {
            "pacientes" => consulta.Where(p => p.IdPaciente != null),
            "soporte" => consulta.Where(p => p.IdPaciente == null && p.ModuloSoftware != null),
            _ => consulta
        };
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var termino = buscar;
            var numero = termino.StartsWith("PQR-", StringComparison.OrdinalIgnoreCase)
                ? termino[4..] : termino;
            var esRadicado = int.TryParse(numero, out var idBuscado);
            consulta = esRadicado && termino.StartsWith("PQR-", StringComparison.OrdinalIgnoreCase)
                ? consulta.Where(p => p.IdPqr == idBuscado)
                : consulta.Where(p =>
                p.Asunto.Contains(termino) ||
                (p.ModuloSoftware != null && p.ModuloSoftware.Contains(termino)) ||
                (p.IdPaciente == null && p.Usuario != null &&
                    (p.Usuario.Nombre.Contains(termino) || p.Usuario.Apellidos.Contains(termino))) ||
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
        var pqrs = await CargarBandejaAsync(consulta
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.IdPqr)
            .Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina), ct);

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

    [HttpGet]
    [Authorize(Roles = "Paciente,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/api/pqr/{id:int}/respuestas")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ApiRespuestasPropias(int id, CancellationToken ct = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId) || usuarioId <= 0)
            return Forbid();

        var solicitud = await ConsultarPqrsPropias(usuarioId)
            .Where(p => p.IdPqr == id)
            .Select(p => new { p.Respuesta, p.FechaRespuesta })
            .SingleOrDefaultAsync(ct);

        if (solicitud == null)
            return NotFound();

        var eventos = await _context.PqrHistorial.AsNoTracking()
            .Where(h => h.IdPqr == id && h.TipoEvento == "respuesta"
                && h.Respuesta != null && h.Respuesta.Trim() != "")
            .OrderBy(h => h.FechaEvento).ThenBy(h => h.IdHistorial)
            .Select(h => new { h.Respuesta, h.FechaEvento })
            .ToListAsync(ct);

        var respuestas = eventos.Select(h => new
        {
            texto = h.Respuesta,
            fecha = (DateTime?)DateTime.SpecifyKind(h.FechaEvento, DateTimeKind.Utc)
        }).ToList();

        if (respuestas.Count == 0 && !string.IsNullOrWhiteSpace(solicitud.Respuesta))
            respuestas.Add(new { texto = solicitud.Respuesta, fecha = solicitud.FechaRespuesta });

        return Ok(respuestas);
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
