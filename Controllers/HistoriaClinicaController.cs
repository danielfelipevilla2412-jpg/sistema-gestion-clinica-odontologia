using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SmileTrack_MVC.Controllers;

// Yeray - ACTUALIZACIÓN: la lógica que antes vivía como métodos privados
// (GuardarOdontogramaInternoAsync, RegistrarNotaClinicaAsync,
// RegistrarControlPostoperatorioAsync, CrearHistoriaClinicaAsync) se movió a
// HistoriaClinicaService para que la comparta también la API REST nueva
// (Controllers/Api/HistoriaClinicaApiController.cs), sin duplicar reglas de negocio.
public class HistoriaClinicaController(
    AppDbContext context,
    IHistoriaClinicaService historiaService,
    AuditoriaHistoriaClinicaService auditoria,
    IConfiguration configuration,
    ILogger<HistoriaClinicaController> logger,
    IPerfilPacienteService perfilPacienteService) : Controller
{
    private static readonly HashSet<string> EstadosPlanTratamiento = new(StringComparer.Ordinal)
    {
        "propuesto", "aprobado", "en-curso", "pausado", "completado", "cancelado"
    };

    private readonly AppDbContext _context = context;
    private readonly IHistoriaClinicaService _historiaService = historiaService;
    private readonly AuditoriaHistoriaClinicaService _auditoria = auditoria;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<HistoriaClinicaController> _logger = logger;
    private readonly IPerfilPacienteService _perfilPacienteService = perfilPacienteService;

    private async Task<bool> PerfilPacienteConfirmadoAsync(CancellationToken ct = default)
    {
        if (!User.IsInRole("Paciente"))
            return true;

        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario) &&
            await _perfilPacienteService.PerfilCompletoAsync(idUsuario, ct);
    }

    private IActionResult RedirigirAPerfilPaciente()
    {
        TempData["PerfilPacienteAviso"] = "Confirma tus datos personales para consultar tu historia clínica.";
        return Redirect("/perfiles/st-pac-perfil-paciente");
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("historia-clinica/st-adm-historial")]
    public IActionResult StadmHistorial() => View("~/Views/Historia_Clinica/st-adm-historial/historial-adm.cshtml");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("historia-clinica/st-adm-historial/data")]
    public async Task<IActionResult> StadmHistorialData([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? odontologo = null,
        [FromQuery] string? alerta = null, [FromQuery] string? estado = null)
    {
        var resultado = await BuildPacientesHistorialAsync(null, page, pageSize, search, estado, alerta, odontologo: odontologo);
        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            null,
            "Consulta paginada del historial clínico desde administración.");
        return Json(resultado);
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-07-control-postoperato")]
    public async Task<IActionResult> Staux07ControlPostoperato([FromQuery] int? citaId)
    {
        var cita = citaId is not null
            ? await _context.Citas.Include(c => c.Paciente).Include(c => c.Servicio).Include(c => c.Profesional)
                .FirstOrDefaultAsync(c => c.IdCita == citaId)
            : await _context.Citas.Include(c => c.Paciente).Include(c => c.Servicio).Include(c => c.Profesional)
                .Where(c => c.Estado == "completada" || c.Estado == "realizada" || c.Estado == "atendida")
                .OrderByDescending(c => c.FechaHora)
                .FirstOrDefaultAsync();

        object vm;
        if (cita is null)
        {
            vm = new { citaId = (int?)null, paciente = "Sin citas post-operatorias registradas", procedimiento = "", fecha = (DateTime?)null, status = "stable", instructions = new object[0], observations = "" };
        }
        else
        {
            var control = await _context.ControlesPostoperatorios
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IdCita == cita.IdCita);
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Control_Postoperatorio",
                control?.IdControl,
                "Consulta del control postoperatorio desde la vista auxiliar.");
            var instruccionesPorDefecto = new List<object>
            {
                new { text = "No comer próximas 2h", @checked = false },
                new { text = "Medicamento cada 8h", @checked = false },
                new { text = "Evitar T° extremas", @checked = false },
                new { text = "Control en 7 días", @checked = false }
            };
            var instrucciones = string.IsNullOrWhiteSpace(control?.InstruccionesJson)
                ? null
                : JsonSerializer.Deserialize<List<ControlPostoperatorioInstruccion>>(control.InstruccionesJson);

            vm = new
            {
                citaId = cita.IdCita,
                paciente = cita.Paciente?.NombresCompleto ?? "Paciente sin datos",
                procedimiento = cita.Servicio?.Nombre ?? "Procedimiento",
                fecha = cita.FechaHora,
                status = control?.Status ?? "stable",
                instructions = instrucciones?.Select(i => (object)new { text = i.Text ?? "", @checked = i.Checked }).ToList()
                    ?? instruccionesPorDefecto,
                observations = control?.Observaciones ?? ""
            };
        }

        ViewData["PostopData"] = JsonSerializer.Serialize(vm);
        return View("~/Views/Historia_Clinica/st-aux-07-control-postoperato/control-post.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-07-control-postoperato/guardar")]
    public async Task<IActionResult> GuardarControlPostoperatorio([FromBody] ControlPostoperatorioGuardarRequest request)
    {
        if (request is null || request.CitaId is null)
            return Json(new { success = false, message = "No se recibió la cita del control postoperatorio." });

        bool controlExistente = await _context.ControlesPostoperatorios
            .AnyAsync(c => c.IdCita == request.CitaId.Value);
        var (success, message) = await _historiaService.RegistrarControlPostoperatorioAsync(request);

        if (success)
        {
            var control = await _context.ControlesPostoperatorios
                .FirstOrDefaultAsync(c => c.IdCita == request.CitaId.Value);
            if (control is not null)
            {
                await _auditoria.RegistrarAsync(
                    HttpContext,
                    controlExistente ? "UPDATE" : "INSERT",
                    "Control_Postoperatorio",
                    control.IdControl,
                    "Control postoperatorio guardado desde la vista auxiliar.");
            }
        }

        return success
            ? Json(new { success = true })
            : Json(new { success = false, message });
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-07-control-postoperato/data")]
    public async Task<IActionResult> Staux07ControlPostoperatoData()
    {
        // Seguimiento post-operatorio: citas ya completadas en los últimos 30 días.
        // No existe una tabla dedicada a "control postoperatorio" en el esquema,
        // así que se deriva de citas reales con estado "completada"/"realizada".
        var desde = DateTime.Now.AddDays(-30);
        var citas = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Where(c => c.FechaHora >= desde && c.FechaHora <= DateTime.Now &&
                        (c.Estado == "completada" || c.Estado == "realizada" || c.Estado == "atendida"))
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Cita",
            null,
            "Consulta del listado de controles postoperatorios.");

        var data = citas.Select(c => new
        {
            id = c.IdCita,
            paciente = c.Paciente is not null ? c.Paciente.NombresCompleto : "Paciente sin datos",
            documento = c.Paciente?.Documento,
            procedimiento = c.Servicio?.Nombre ?? "Procedimiento",
            profesional = c.Profesional is not null ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}" : "Sin asignar",
            fecha = c.FechaHora,
            notas = c.Notas
        });

        return Json(data);
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos")]
    public async Task<IActionResult> Staux08DocumentosClinicos([FromQuery] int? pacienteId)
    {
        var paciente = pacienteId is > 0
            ? await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == pacienteId)
            : null;

        if (pacienteId is > 0 && paciente is null)
            return NotFound();
        if (paciente is not null && !await PuedeProfesionalAccederAPacienteAsync(paciente.IdPaciente))
            return Forbid();

        var historia = paciente is not null
            ? await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == paciente.IdPaciente && h.Activa)
            : null;

        if (historia is not null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                historia.IdHistoria,
                "Consulta de documentos clínicos desde la vista auxiliar.");
        }

        ViewData["PacienteNombre"] = paciente?.NombresCompleto ?? "Sin paciente asignado";
        ViewData["PacienteCodigoHC"] = historia is not null ? $"HC-{historia.IdHistoria:D6}" : "Sin historia clínica";
        ViewData["PacienteId"] = paciente?.IdPaciente;

        return View("~/Views/Historia_Clinica/st-aux-08-documentos-clinicos/documentos-cli.cshtml");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray - Endpoint de datos reales para Documentos Clínicos (2025)
    //
    // ANTES: devolvía Array.Empty<object>() porque no existía ninguna tabla.
    // AHORA: consulta Documento_Clinico filtrada por idHistoria del paciente,
    //        devuelve los metadatos que el JS de documentos-cli.js ya espera
    //        (tipo, fecha, nombreArchivo, subidoPor).
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Auxiliar,Profesional")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/data")]
    public async Task<IActionResult> Staux08DocumentosClinicosData([FromQuery] int? pacienteId)
    {
        // Sin paciente: devolver lista vacía (el JS ya maneja el empty state)
        if (pacienteId is null)
            return Json(Array.Empty<object>());

        if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId.Value))
            return Forbid();

        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);

        if (historia is null)
            return Json(Array.Empty<object>());

        var documentos = await _context.DocumentosClinicos
            .AsNoTracking()
            .Include(d => d.SubidoPorUsuario)
            .Where(d => d.IdHistoria == historia.IdHistoria)
            .OrderByDescending(d => d.FechaSubida)
            .Select(d => new
            {
                id            = d.IdDocumento,
                tipo          = d.Tipo,
                d.FechaDocumento,
                d.FechaSubida,
                nombreArchivo = d.NombreOriginal,
                subidoPor     = d.SubidoPorUsuario != null
                                    ? $"{d.SubidoPorUsuario.Nombre} {d.SubidoPorUsuario.Apellidos}"
                                    : "Sin registrar",
                contentType   = d.ContentType,
                tamanoBytes   = d.TamanoBytes,
                observacion   = d.Observacion ?? ""
            })
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Documento_Clinico",
            null,
            $"Consulta de documentos clínicos de la historia {historia.IdHistoria}.");

        return Json(documentos.Select(d => new
        {
            d.id,
            d.tipo,
            fecha = (d.FechaDocumento ?? DateOnly.FromDateTime(d.FechaSubida)).ToString("yyyy-MM-dd"),
            d.nombreArchivo,
            d.subidoPor,
            d.contentType,
            d.tamanoBytes,
            d.observacion
        }));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray - Endpoint de subida de archivos clínicos (2025)
    //
    // ANTES: el JS mostraba un setTimeout() que simulaba la subida localmente
    //        y avisaba "la subida a servidor aún no está implementada".
    // AHORA: recibe el archivo via multipart/form-data, lo valida (tipo y tamaño),
    //        lo guarda fuera de wwwroot, en el almacenamiento privado clínico,
    //        y persiste los metadatos en Documento_Clinico.
    //
    // SEGURIDAD:
    //   - Solo acepta image/jpeg, image/png, application/pdf.
    //   - Máximo 10 MB por archivo.
    //   - El nombre físico en disco se genera con un GUID para evitar colisiones
    //     y ataques de path traversal; el nombre original se guarda solo en BD.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Auxiliar,Profesional")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/subir")]
    public async Task<IActionResult> SubirDocumentoClinco(
        [FromForm] int pacienteId,
        [FromForm] string tipo,
        [FromForm] DateOnly? fechaDocumento,
        [FromForm] string? observacion,
        IFormFile archivo,
        [FromServices] IWebHostEnvironment env)
    {
        // ── Validaciones básicas ────────────────────────────────────────────
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { success = false, message = "No se recibió ningún archivo." });

        const long maxBytes = 10 * 1024 * 1024; // 10 MB
        if (archivo.Length > maxBytes)
            return BadRequest(new { success = false, message = "El archivo supera el límite de 10 MB." });

        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "El paciente indicado no es válido." });

        if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId))
            return Forbid();

        string[] tiposDocumentoPermitidos =
        [
            "Radiografía panorámica",
            "Radiografía periapical",
            "Fotografía inicial",
            "Fotografía final",
            "Examen de coagulación",
            "Consentimiento informado",
            "Historia clínica",
            "Estudio clínico",
            "Otro"
        ];
        string tipoNormalizado = tipo?.Trim() ?? "";
        if (!tiposDocumentoPermitidos.Contains(tipoNormalizado, StringComparer.Ordinal))
            return BadRequest(new { success = false, message = "El tipo de documento no está permitido." });

        if (fechaDocumento is null)
            return BadRequest(new { success = false, message = "La fecha de expedición es obligatoria." });

        string? contentType = await DetectarContentTypeClinicoAsync(archivo);
        if (contentType is null)
            return BadRequest(new { success = false, message = "El contenido no coincide con un archivo JPG, PNG o PDF permitido." });

        string nombreOriginal = Path.GetFileName(archivo.FileName);
        if (nombreOriginal.Length > 255)
            return BadRequest(new { success = false, message = "El nombre del archivo supera el límite permitido." });

        string? observacionNormalizada = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        if (observacionNormalizada?.Length > 500)
            return BadRequest(new { success = false, message = "La descripción no puede superar 500 caracteres." });

        // ── Obtener o crear historia clínica ────────────────────────────────
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);

        if (historia is null)
        {
            bool pacienteExiste = await _context.Pacientes.AnyAsync(p => p.IdPaciente == pacienteId);
            if (!pacienteExiste)
                return NotFound(new { success = false, message = "Paciente no encontrado." });

            historia = await _historiaService.CrearHistoriaClinicaAsync(pacienteId);
        }

        // ── Guardar archivo en disco ─────────────────────────────────────────
        // El nombre en disco usa un GUID + extensión real para evitar colisiones
        // y bloquear path traversal.
        string extension = contentType switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            _ => ".jpg"
        };

        string nombreFisico  = $"{Guid.NewGuid()}{extension}";
        string carpetaFisica = Path.Combine(
            ObtenerRaizDocumentosClinicos(env),
            historia.IdHistoria.ToString());

        Directory.CreateDirectory(carpetaFisica);

        string rutaFisica = Path.Combine(carpetaFisica, nombreFisico);
        string rutaRelativa = Path.Combine(historia.IdHistoria.ToString(), nombreFisico).Replace('\\', '/');

        await using (var stream = new FileStream(rutaFisica, FileMode.Create, FileAccess.Write))
        {
            await archivo.CopyToAsync(stream);
        }

        // ── Obtener el usuario actual ────────────────────────────────────────
        string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        int? idUsuario = int.TryParse(userIdStr, out int uid) ? uid : null;

        // ── Persistir metadatos en BD ────────────────────────────────────────
        var documento = new SmileTrack_MVC.Models.Entities.DocumentoClinico
        {
            IdHistoria    = historia.IdHistoria,
            SubidoPor     = idUsuario,
            Tipo          = tipoNormalizado,
            NombreOriginal = nombreOriginal,
            RutaRelativa  = rutaRelativa,
            ContentType   = contentType,
            TamanoBytes   = archivo.Length,
            FechaSubida   = DateTime.UtcNow,
            FechaDocumento = fechaDocumento.Value,
            Observacion   = observacionNormalizada
        };

        _context.DocumentosClinicos.Add(documento);

        try
        {
            _auditoria.Agregar(
                HttpContext,
                "INSERT",
                "Documento_Clinico",
                null,
                $"Documento clínico agregado a la historia {historia.IdHistoria}.");
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron guardar los metadatos del documento clínico (historiaId={HistoriaId}).", historia.IdHistoria);
            if (System.IO.File.Exists(rutaFisica))
                System.IO.File.Delete(rutaFisica);

            return StatusCode(500, new { success = false, message = "No fue posible guardar el documento." });
        }

        return Ok(new
        {
            success       = true,
            message       = "Documento subido correctamente.",
            id            = documento.IdDocumento,
            tipo          = documento.Tipo,
            fecha         = documento.FechaDocumento?.ToString("yyyy-MM-dd"),
            nombreArchivo = documento.NombreOriginal,
            tamanoBytes   = documento.TamanoBytes
        });
    }

    private static async Task<string?> DetectarContentTypeClinicoAsync(IFormFile archivo)
    {
        byte[] firma = new byte[8];
        await using var stream = archivo.OpenReadStream();
        int leidos = await stream.ReadAsync(firma.AsMemory());

        if (leidos >= 8
            && firma.AsSpan().SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (leidos >= 3 && firma[0] == 0xFF && firma[1] == 0xD8 && firma[2] == 0xFF)
            return "image/jpeg";

        if (leidos >= 5
            && firma[0] == (byte)'%'
            && firma[1] == (byte)'P'
            && firma[2] == (byte)'D'
            && firma[3] == (byte)'F'
            && firma[4] == (byte)'-')
        {
            return "application/pdf";
        }

        return null;
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Auxiliar,Profesional")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/eliminar/{id:int}")]
    public async Task<IActionResult> EliminarDocumentoClinico(
        int id,
        [FromServices] IWebHostEnvironment env)
    {
        var documento = await _context.DocumentosClinicos
            .FirstOrDefaultAsync(d => d.IdDocumento == id);

        if (documento is null)
            return NotFound(new { success = false, message = "El estudio ya no existe." });

        string? rutaFisica = await ObtenerRutaDocumentoClinicoAsync(documento, env);

        string? rutaTemporal = rutaFisica is null
            ? null
            : rutaFisica + ".deleting-" + Guid.NewGuid().ToString("N");
        bool archivoMovido = false;

        try
        {
            if (rutaFisica is not null && rutaTemporal is not null && System.IO.File.Exists(rutaFisica))
            {
                System.IO.File.Move(rutaFisica, rutaTemporal);
                archivoMovido = true;
            }

            _auditoria.Agregar(
                HttpContext,
                "DELETE",
                "Documento_Clinico",
                documento.IdDocumento,
                $"Documento clínico eliminado de la historia {documento.IdHistoria}.");
            _context.DocumentosClinicos.Remove(documento);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el documento clínico {DocumentoId}.", id);
            if (archivoMovido && rutaTemporal is not null && rutaFisica is not null
                && System.IO.File.Exists(rutaTemporal) && !System.IO.File.Exists(rutaFisica))
                System.IO.File.Move(rutaTemporal, rutaFisica);

            return StatusCode(500, new { success = false, message = "No fue posible quitar el estudio." });
        }

        if (archivoMovido && rutaTemporal is not null)
            System.IO.File.Delete(rutaTemporal);

        return Ok(new { success = true, message = "Estudio quitado correctamente." });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray - Endpoint de descarga de archivos clínicos (2025)
    //
    // Los roles clínicos autorizados y el propio paciente dueño pueden descargarlo.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Auxiliar,Profesional,Administrador,Paciente")]
    [Route("historia-clinica/documentos-clinicos/descargar/{id:int}")]
    public async Task<IActionResult> DescargarDocumentoClinco(
        int id,
        [FromServices] IWebHostEnvironment env)
    {
        if (!await PerfilPacienteConfirmadoAsync())
            return RedirigirAPerfilPaciente();

        var documento = await _context.DocumentosClinicos
            .FirstOrDefaultAsync(d => d.IdDocumento == id);

        if (documento is null)
            return NotFound(new { success = false, message = "Documento no encontrado." });

        if (!await PuedeDescargarDocumentoClinicoAsync(documento.IdHistoria))
            return Forbid();

        string? rutaFisica = await ObtenerRutaDocumentoClinicoAsync(documento, env);
        if (rutaFisica is null)
            return NotFound(new { success = false, message = "El archivo no se encuentra en el servidor." });
        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { success = false, message = "El archivo no se encuentra en el servidor." });

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(rutaFisica);

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Documento_Clinico",
            documento.IdDocumento,
            "Descarga de documento clínico.");

        Response.Headers.CacheControl = "private, no-store";
        return File(bytes, documento.ContentType, documento.NombreOriginal);
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar,Profesional,Administrador,Paciente")]
    [Route("historia-clinica/documentos-clinicos/preview/{id:int}")]
    public async Task<IActionResult> PrevisualizarDocumentoClinico(
        int id,
        [FromServices] IWebHostEnvironment env)
    {
        if (!await PerfilPacienteConfirmadoAsync())
            return RedirigirAPerfilPaciente();

        var documento = await _context.DocumentosClinicos
            .FirstOrDefaultAsync(d => d.IdDocumento == id);

        if (documento is null)
            return NotFound();

        if (!await PuedeDescargarDocumentoClinicoAsync(documento.IdHistoria))
            return Forbid();

        if (documento.ContentType is not ("image/jpeg" or "image/png" or "application/pdf"))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);

        string? rutaFisica = await ObtenerRutaDocumentoClinicoAsync(documento, env);
        if (rutaFisica is null || !System.IO.File.Exists(rutaFisica))
            return NotFound();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Documento_Clinico",
            documento.IdDocumento,
            "Vista previa de documento clínico.");

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        var stream = new FileStream(rutaFisica, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, documento.ContentType);
    }

    private async Task<bool> PuedeDescargarDocumentoClinicoAsync(int historiaId)
    {
        string? idUsuarioClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (User.IsInRole("Profesional"))
        {
            return int.TryParse(idUsuarioClaim, out int idUsuarioProfesional)
                && await _historiaService.ProfesionalPuedeAccederHistoriaAsync(
                    historiaId,
                    idUsuarioProfesional);
        }

        if (!User.IsInRole("Paciente"))
            return true;

        string? idPacienteClaim = User.FindFirst("IdPaciente")?.Value;
        if (!int.TryParse(idUsuarioClaim, out int idUsuario)
            || !int.TryParse(idPacienteClaim, out int idPaciente))
        {
            return false;
        }

        return await _context.HistoriasClinicas
            .Where(h => h.IdHistoria == historiaId && h.IdPaciente == idPaciente)
            .AnyAsync(h => h.Paciente != null && h.Paciente.IdUsuario == idUsuario);
    }

    private string ObtenerRaizDocumentosClinicos(IWebHostEnvironment env)
    {
        string? configuredPath = _configuration["Storage:ClinicalDocumentsPath"];
        string rutaConfigurada = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(env.ContentRootPath, "App_Data", "clinical-documents")
            : configuredPath;
        string raiz = Path.GetFullPath(Path.IsPathRooted(rutaConfigurada)
            ? rutaConfigurada
            : Path.Combine(env.ContentRootPath, rutaConfigurada));
        string webRoot = Path.GetFullPath(env.WebRootPath);

        if (PathsAreEqual(raiz, webRoot) || IsPathWithin(raiz, webRoot))
            throw new InvalidOperationException("El almacenamiento de documentos clínicos debe estar fuera de wwwroot.");

        return raiz;
    }

    private async Task<string?> ObtenerRutaDocumentoClinicoAsync(
        DocumentoClinico documento,
        IWebHostEnvironment env)
    {
        string raizPrivada = ObtenerRaizDocumentosClinicos(env);

        if (documento.RutaRelativa.StartsWith("uploads/documentos-clinicos/", StringComparison.OrdinalIgnoreCase))
        {
            string raizPublicaClinica = Path.GetFullPath(Path.Combine(
                env.WebRootPath,
                "uploads",
                "documentos-clinicos"));
            string rutaPublica = Path.GetFullPath(Path.Combine(
                env.WebRootPath,
                documento.RutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsPathWithin(rutaPublica, raizPublicaClinica))
                throw new InvalidOperationException("La ruta histórica del documento clínico no es válida.");
            if (!System.IO.File.Exists(rutaPublica))
                return null;

            string nombreArchivo = Path.GetFileName(rutaPublica);
            string carpetaPrivada = Path.Combine(raizPrivada, documento.IdHistoria.ToString());
            string rutaPrivada = Path.Combine(carpetaPrivada, nombreArchivo);
            Directory.CreateDirectory(carpetaPrivada);

            if (!System.IO.File.Exists(rutaPrivada))
                System.IO.File.Copy(rutaPublica, rutaPrivada);

            documento.RutaRelativa = Path.Combine(documento.IdHistoria.ToString(), nombreArchivo).Replace('\\', '/');
            await _context.SaveChangesAsync();
            System.IO.File.Delete(rutaPublica);
            return rutaPrivada;
        }

        if (Path.IsPathRooted(documento.RutaRelativa))
            throw new InvalidOperationException("La ruta del documento clínico no es válida.");

        string rutaResuelta = Path.GetFullPath(Path.Combine(
            raizPrivada,
            documento.RutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

        if (!IsPathWithin(rutaResuelta, raizPrivada))
            throw new InvalidOperationException("La ruta del documento clínico no es válida.");

        return rutaResuelta;
    }

    private static bool IsPathWithin(string path, string directory)
    {
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))
            + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return Path.GetFullPath(path).StartsWith(prefix, comparison);
    }

    private static bool PathsAreEqual(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            comparison);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial")]
    public async Task<IActionResult> Stodo03Historial(
        [FromQuery] int? pacienteId,
        [FromQuery] int? historiaId,
        [FromQuery] int? citaId)
    {
        int? pacienteResuelto = await ResolverPacienteHistoriaAsync(pacienteId, historiaId);
        if (pacienteResuelto.HasValue
            && !await PuedeProfesionalAccederAPacienteAsync(pacienteResuelto.Value))
        {
            return Forbid();
        }

        var pacienteIdParaCita = await ResolverPacienteHistoriaAsync(pacienteId, historiaId);
        int? citaIdValido = citaId is > 0 && pacienteIdParaCita is > 0
            && await _context.Citas.AnyAsync(c => c.IdCita == citaId && c.IdPaciente == pacienteIdParaCita)
                ? citaId
                : null;
        var vm = await BuildHistorialPacienteViewModelAsync(pacienteId, historiaId, citaIdValido);
        if (citaIdValido is > 0)
        {
            vm.Odontograma.CitaId = citaIdValido;
        }

        if (vm.Odontograma.HistoriaId is int historiaIdResuelta && historiaIdResuelta > 0)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                historiaIdResuelta,
                "Consulta de historia clínica desde el historial MVC.");
        }

        return View("~/Views/Historia_Clinica/st-odo-03-historial/gestion-historial.cshtml", vm);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional,Paciente")]
    [Route("historia-clinica/st-odo-03-historial/exportar-pdf")]
    public async Task<IActionResult> ExportarHistoriaClinicaPdf(
        [FromQuery] int? pacienteId,
        CancellationToken cancellationToken)
    {
        if (!await PerfilPacienteConfirmadoAsync(cancellationToken))
            return RedirigirAPerfilPaciente();

        int pacienteIdResuelto;
        if (User.IsInRole("Paciente"))
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int idUsuario))
                return Forbid();

            var pacientePropioId = await _context.Pacientes
                .Where(p => p.IdUsuario == idUsuario)
                .Select(p => (int?)p.IdPaciente)
                .FirstOrDefaultAsync(cancellationToken);
            if (pacientePropioId is null
                || (pacienteId is > 0 && pacienteId.Value != pacientePropioId.Value)
                || !await _historiaService.EsPropioPacienteAsync(
                    pacientePropioId.Value,
                    idUsuario,
                    cancellationToken))
            {
                return Forbid();
            }

            pacienteIdResuelto = pacientePropioId.Value;
        }
        else
        {
            if (pacienteId is not > 0)
                return BadRequest(new { message = "Selecciona un paciente válido para exportar su historia." });
            if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId.Value))
                return Forbid();
            pacienteIdResuelto = pacienteId.Value;
        }

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPaciente == pacienteIdResuelto, cancellationToken);
        if (paciente is null)
            return NotFound();

        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteIdResuelto && h.Activa, cancellationToken);
        if (historia is null)
            return NotFound(new { message = "El paciente no tiene una historia clínica activa." });

        var consultas = await _context.ConsultasClinicas
            .AsNoTracking()
            .Include(c => c.Diagnosticos)
            .Include(c => c.Sintomas)
            .Include(c => c.Cita)
            .Include(c => c.ProximaCita)
            .Where(c => c.IdHistoria == historia.IdHistoria)
            .OrderByDescending(c => c.FechaConsulta ?? c.FechaRegistro)
            .ToListAsync(cancellationToken);
        var notas = await _context.NotasClinicas
            .AsNoTracking()
            .Include(n => n.Profesional)
            .Where(n => n.IdHistoria == historia.IdHistoria)
            .OrderByDescending(n => n.Fecha)
            .ToListAsync(cancellationToken);
        var odontograma = await _context.RegistrosOdontograma
            .AsNoTracking()
            .Where(r => r.IdHistoria == historia.IdHistoria)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync(cancellationToken);
        var documentos = await _context.DocumentosClinicos
            .AsNoTracking()
            .Where(d => d.IdHistoria == historia.IdHistoria)
            .OrderByDescending(d => d.FechaSubida)
            .Select(d => new { d.Tipo, d.NombreOriginal, d.FechaSubida, d.Observacion })
            .ToListAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            historia.IdHistoria,
            "Exportación PDF de historia clínica.");

        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(header =>
            {
                header.Item().Text("SmileTrack — Historia Clínica").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                header.Item().Text($"Historia HC-{historia.IdHistoria:0000} · Generada {DateTime.Now:dd/MM/yyyy HH:mm}");
            });
            page.Content().PaddingVertical(16).Column(content =>
            {
                content.Spacing(10);
                content.Item().Text("Datos del paciente").FontSize(13).Bold();
                content.Item().Text($"{paciente.NombresCompleto} · {paciente.TipoDocumento} {paciente.Documento}");
                content.Item().Text($"Fecha de nacimiento: {paciente.FechaNacimiento:dd/MM/yyyy} · Género: {paciente.Genero ?? "No registrado"}");
                content.Item().Text($"Teléfono: {paciente.Telefono ?? "No registrado"} · Correo: {paciente.Correo ?? "No registrado"}");
                content.Item().Text($"Grupo sanguíneo: {paciente.GrupoSanguineo ?? "No registrado"}");
                content.Item().Text($"Alergias registradas en ficha: {paciente.Alergias ?? "Ninguna registrada"}");
                content.Item().Text($"Medicamentos registrados en ficha: {paciente.Medicamentos ?? "Ninguno registrado"}");
                content.Item().Text($"Antecedentes de ficha: {paciente.AntecedentesMedicos ?? "Ninguno registrado"}");

                content.Item().PaddingTop(8).Text("Consultas clínicas").FontSize(13).Bold();
                if (consultas.Count == 0)
                {
                    content.Item().Text("No hay consultas clínicas estructuradas registradas.");
                }
                foreach (var consulta in consultas)
                {
                    content.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(item =>
                    {
                        item.Spacing(4);
                        item.Item().Text($"Consulta · {(consulta.FechaConsulta ?? consulta.FechaRegistro):dd/MM/yyyy HH:mm}")
                            .Bold();
                        item.Item().Text($"Motivo: {consulta.MotivoConsulta ?? "No registrado"}");
                        item.Item().Text($"Enfermedad actual: {consulta.EnfermedadActual ?? "No registrada"}");
                        item.Item().Text($"Antecedentes: {consulta.AntecedentesMedicos ?? "No registrados"}");
                        item.Item().Text($"Síntomas/signos: {string.Join(", ", consulta.Sintomas.OrderBy(s => s.Orden).Select(s => s.Descripcion))}");
                        item.Item().Text($"Hallazgos: {consulta.Hallazgos ?? "No registrados"}");
                        item.Item().Text($"Exámenes complementarios: {consulta.ExamenesComplementarios ?? "No registrados"}");
                        item.Item().Text($"Diagnósticos: {string.Join("; ", consulta.Diagnosticos.Select(d => d.CodigoCie10 is null ? d.Descripcion : $"{d.CodigoCie10} — {d.Descripcion}"))}");
                        item.Item().Text($"Evolución: {consulta.EvolucionClinica ?? "No registrada"}");
                        item.Item().Text($"Prescripción: {consulta.Prescripcion ?? "No registrada"}");
                        if (consulta.ProximaCita is not null)
                            item.Item().Text($"Próxima cita: {consulta.ProximaCita.FechaHora:dd/MM/yyyy HH:mm}");
                    });
                }

                content.Item().PaddingTop(8).Text("Notas clínicas").FontSize(13).Bold();
                if (notas.Count == 0)
                    content.Item().Text("No hay notas clínicas registradas.");
                foreach (var nota in notas)
                {
                    content.Item().Column(item =>
                    {
                        item.Spacing(3);
                        item.Item().Text($"{nota.Fecha:dd/MM/yyyy HH:mm} · {nota.Estado}").Bold();
                        item.Item().Text($"Profesional: {nota.Profesional?.Nombres} {nota.Profesional?.Apellidos}".Trim());
                        item.Item().Text($"Diagnóstico: {nota.Diagnostico ?? "No registrado"}");
                        item.Item().Text($"Procedimiento: {nota.Procedimiento ?? "No registrado"}");
                        item.Item().Text($"Próxima cita sugerida: {nota.ProximaCita ?? "No registrada"}");
                    });
                }

                content.Item().PaddingTop(8).Text("Odontograma").FontSize(13).Bold();
                if (odontograma.Count == 0)
                    content.Item().Text("No hay registros del odontograma.");
                foreach (var registro in odontograma)
                {
                    content.Item().Text(
                        $"{registro.FechaRegistro:dd/MM/yyyy HH:mm} · Pieza {registro.NumeroFdi} · {registro.Estado}" +
                        (string.IsNullOrWhiteSpace(registro.Observacion) ? "" : $" · {registro.Observacion}"));
                }

                content.Item().PaddingTop(8).Text("Documentos clínicos adjuntos (metadatos)").FontSize(13).Bold();
                if (documentos.Count == 0)
                    content.Item().Text("No hay documentos clínicos adjuntos.");
                foreach (var documento in documentos)
                {
                    content.Item().Text(
                        $"{documento.FechaSubida:dd/MM/yyyy} · {documento.Tipo} · {documento.NombreOriginal}" +
                        (string.IsNullOrWhiteSpace(documento.Observacion) ? "" : $" · {documento.Observacion}"));
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Documento clínico confidencial · Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        })).GeneratePdf();

        return File(
            pdf,
            "application/pdf",
            $"historia-clinica-{paciente.Documento}-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-04-odontograma")]
    public async Task<IActionResult> Stodo04Odontograma(
        [FromQuery] int? pacienteId,
        [FromQuery] int? historiaId,
        // Yeray (2025): parámetro nuevo para enlazar el odontograma a una cita
        // concreta. Si el profesional llega desde la agenda (?citaId=N), el
        // guardado registra la cita en Registro_Odontograma.IdCita.
        [FromQuery] int? citaId)
    {
        int? pacienteResuelto = await ResolverPacienteHistoriaAsync(pacienteId, historiaId);
        if (pacienteResuelto.HasValue
            && !await PuedeProfesionalAccederAPacienteAsync(pacienteResuelto.Value))
        {
            return Forbid();
        }

        var vm = await BuildOdontogramaViewModelAsync(pacienteId, historiaId);
        // Propagar la cita al ViewModel → la vista la inyecta en config JS
        vm.CitaId = citaId;

        if (vm.HistoriaId is int historiaIdResuelta && historiaIdResuelta > 0)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                historiaIdResuelta,
                "Consulta de historia clínica desde el odontograma MVC.");
        }

        return View("~/Views/Historia_Clinica/st-odo-04-odontograma/odontograma-digital.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-04-odontograma/guardar")]
    public async Task<IActionResult> GuardarOdontograma([FromBody] OdontogramaGuardarRequest request)
    {
        if (request is null)
            return BadRequest(new { success = false, message = "No se recibieron datos del odontograma." });

        if (request.PacienteId is not > 0)
            return BadRequest(new { success = false, message = "Selecciona un paciente válido." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId.Value))
            return Forbid();

        // Obtener el profesional actual para trazabilidad
        int? idProfesional = await ObtenerIdProfesionalActualAsync();

        var (success, message, historiaId) = await _historiaService.GuardarOdontogramaInternoAsync(
            request.PacienteId.Value, request, idProfesional);

        if (!success)
        {
            if (message == "El paciente indicado no existe.")
                return NotFound(new { success = false, message });

            return message == "La historia clínica del paciente está inactiva."
                ? Conflict(new { success = false, message })
                : BadRequest(new { success = false, message });
        }

        if (historiaId is > 0)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "UPDATE",
                "Historia_Clinica",
                historiaId.Value,
                "Odontograma actualizado desde la vista MVC.");
        }

        return Json(new
        {
            success = true,
            historiaId,
            message
        });
    }

    // Yeray - Nuevo endpoint para tooltip del odontograma
    // Consulta Registro_Odontograma por numero FDI y devuelve historial real del diente
    // con profesional que lo trató, fecha y citas relacionadas desde BD
    /// <summary>
    /// Devuelve el historial de registros de un diente específico (por número FDI)
    /// para mostrar en el tooltip del odontograma con datos reales de BD.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-04-odontograma/diente-info")]
    public async Task<IActionResult> ObtenerInfoDiente(
        [FromQuery] int pacienteId,
        [FromQuery] string numerofdi)
    {
        if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId))
            return Forbid();

        // Obtener la historia clínica activa del paciente
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            historia?.IdHistoria,
            "Consulta de historial dental desde el odontograma.");

        if (historia is null)
            return Json(new { success = true, registros = Array.Empty<object>(), citas = Array.Empty<object>() });

        // Historial de estados del diente desde Registro_Odontograma
        var registrosDiente = await _context.RegistrosOdontograma
            .Include(r => r.Profesional)
            .Where(r => r.IdHistoria == historia.IdHistoria && r.NumeroFdi == numerofdi)
            .OrderByDescending(r => r.FechaRegistro)
            .Select(r => new
            {
                estado       = r.Estado,
                observacion  = r.Observacion ?? "",
                fecha        = r.FechaRegistro.ToString("dd MMM yyyy · HH:mm"),
                profesional  = r.Profesional != null
                    ? $"Dr(a). {r.Profesional.Nombres} {r.Profesional.Apellidos}"
                    : "Sin asignar"
            })
            .ToListAsync();

        // Citas completadas del paciente para contexto adicional
        var citas = await _context.Citas
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.IdPaciente == pacienteId
                && (c.Estado == "Atendida" || c.Estado == "Completada" || c.Estado == "atendida"))
            .OrderByDescending(c => c.FechaHora)
            .Take(5)
            .Select(c => new
            {
                fecha       = c.FechaHora.ToString("dd MMM yyyy · HH:mm"),
                servicio    = c.Servicio != null ? c.Servicio.Nombre : "Servicio no especificado",
                profesional = c.Profesional != null
                    ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                    : "Sin asignar",
                consultorio = c.Consultorio != null
                    ? $"{c.Consultorio.Nombre}"
                    : "Sin consultorio"
            })
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            historia.IdHistoria,
            "Consulta del historial de un diente desde el odontograma.");

        return Json(new { success = true, registros = registrosDiente, citas });
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/observaciones-cita")]
    public async Task<IActionResult> ObtenerObservacionesCita([FromQuery] int pacienteId, [FromQuery] int citaId)
    {
        if (pacienteId <= 0 || citaId <= 0)
            return BadRequest(new { success = false, message = "Paciente y cita son obligatorios." });

        if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId))
            return Forbid();

        var historia = await _context.HistoriasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);
        if (historia is null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                null,
                "Consulta de registros de odontograma asociados a una cita.");
            return Ok(new { success = true, registros = Array.Empty<object>() });
        }

        bool citaValida = await _context.Citas.AnyAsync(c =>
            c.IdCita == citaId && c.IdPaciente == pacienteId);
        if (!citaValida)
            return BadRequest(new { success = false, message = "La cita no pertenece al paciente indicado." });

        var registros = await _context.RegistrosOdontograma.AsNoTracking()
            .Where(r => r.IdHistoria == historia.IdHistoria && r.IdCita == citaId)
            .GroupBy(r => r.NumeroFdi)
            .Select(g => g.OrderByDescending(r => r.FechaRegistro).First())
            .OrderBy(r => r.NumeroFdi)
            .Select(r => new { r.NumeroFdi, r.Estado, r.Observacion })
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            historia.IdHistoria,
            "Consulta de registros de odontograma asociados a una cita.");
        return Ok(new { success = true, registros });
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/odontograma")]
    public async Task<IActionResult> ObtenerOdontogramaHasta([FromQuery] int pacienteId, [FromQuery] DateOnly hasta)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "El paciente no es válido." });

        if (!await PuedeProfesionalAccederAPacienteAsync(pacienteId))
            return Forbid();

        var historia = await _context.HistoriasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);
        if (historia is null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                null,
                "Consulta histórica del odontograma.");
            return Ok(new { success = true, hasta = hasta.ToString("yyyy-MM-dd"), registros = Array.Empty<object>() });
        }

        DateTime limiteExclusivo = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var registros = await _context.RegistrosOdontograma.AsNoTracking()
            .Where(r => r.IdHistoria == historia.IdHistoria && r.FechaRegistro < limiteExclusivo)
            .GroupBy(r => r.NumeroFdi)
            .Select(g => g.OrderByDescending(r => r.FechaRegistro).First())
            .OrderBy(r => r.NumeroFdi)
            .Select(r => new
            {
                r.NumeroFdi,
                r.Estado,
                r.Observacion,
                r.FechaRegistro
            })
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            historia.IdHistoria,
            "Consulta histórica del odontograma.");
        return Ok(new { success = true, hasta = hasta.ToString("yyyy-MM-dd"), registros });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/guardar-nota")]
    public async Task<IActionResult> GuardarNotaClinica([FromBody] NotaClinicaGuardarRequest request)
    {
        if (request is null || request.PacienteId is null)
            return Json(new { success = false, message = "No se recibió el paciente para la nota clínica." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId.Value))
            return Forbid();

        string? idUsuarioStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? idUsuario = int.TryParse(idUsuarioStr, out int uid) ? uid : null;
        var profesional = idUsuario.HasValue
            ? await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == idUsuario)
            : null;
        string doctor = profesional is not null ? $"Dr(a). {profesional.Nombres} {profesional.Apellidos}" : "Profesional";

        var (success, message, nota) = await _historiaService.RegistrarNotaClinicaAsync(
            request.PacienteId.Value, request, profesional?.IdProfesional, doctor);

        if (!success || nota is null)
            return message == "Esta cita ya tiene una nota registrada."
                ? Conflict(new { success = false, message })
                : BadRequest(new { success = false, message });

        _auditoria.Agregar(
            HttpContext,
            "INSERT",
            "Nota_Clinica",
            nota.IdNota,
            $"Nota clínica agregada a la historia del paciente {request.PacienteId.Value}.");
        await _context.SaveChangesAsync();

        // Se mantiene la misma forma que ya consume el JS de st-odo-03-historial.
        return Json(new
        {
            success = true,
            nota = new
            {
                titulo = nota.Procedimiento ?? nota.Diagnostico ?? "Nota clínica",
                idNota = nota.IdNota,
                fecha = nota.Fecha.ToString("yyyy-MM-dd"),
                doctor = nota.Doctor,
                diagnostico = nota.Diagnostico ?? "",
                procedimiento = nota.Procedimiento ?? "",
                citaId = request.CitaId,
                proximaCita = nota.ProximaCita,
                estado = nota.Estado
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/actualizar-nota")]
    public async Task<IActionResult> ActualizarNotaClinica([FromBody] NotaClinicaActualizarRequest request)
    {
        if (request is null || request.PacienteId <= 0 || request.NotaId <= 0)
            return BadRequest(new { success = false, message = "La nota y el paciente son obligatorios." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId))
            return Forbid();

        if (request.Diagnostico?.Length > 500 || request.Procedimiento?.Length > 500 || request.ProximaCita?.Length > 50)
            return BadRequest(new { success = false, message = "La nota supera la longitud permitida." });

        var historia = await _context.HistoriasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId && h.Activa);
        if (historia is null)
            return NotFound(new { success = false, message = "No existe una historia clínica activa." });

        var nota = await _context.NotasClinicas.FirstOrDefaultAsync(n =>
            n.IdNota == request.NotaId && n.IdHistoria == historia.IdHistoria);
        if (nota is null)
            return NotFound(new { success = false, message = "La nota clínica no existe." });
        if (nota.Estado is "Preparación" or "Anulada")
            return Conflict(new { success = false, message = "Esta nota no se puede editar." });

        nota.Diagnostico = string.IsNullOrWhiteSpace(request.Diagnostico) ? null : request.Diagnostico.Trim();
        nota.Procedimiento = string.IsNullOrWhiteSpace(request.Procedimiento) ? null : request.Procedimiento.Trim();
        nota.ProximaCita = string.IsNullOrWhiteSpace(request.ProximaCita) ? null : request.ProximaCita.Trim();
        _auditoria.Agregar(
            HttpContext,
            "UPDATE",
            "Nota_Clinica",
            nota.IdNota,
            $"Nota clínica actualizada en la historia {historia.IdHistoria}.");
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Nota clínica actualizada correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/anular-nota")]
    public async Task<IActionResult> AnularNotaClinica([FromBody] NotaClinicaActualizarRequest request)
    {
        if (request is null || request.PacienteId <= 0 || request.NotaId <= 0)
            return BadRequest(new { success = false, message = "La nota y el paciente son obligatorios." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId))
            return Forbid();

        var historia = await _context.HistoriasClinicas.AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId && h.Activa);
        if (historia is null)
            return NotFound(new { success = false, message = "No existe una historia clínica activa." });

        var nota = await _context.NotasClinicas.FirstOrDefaultAsync(n =>
            n.IdNota == request.NotaId && n.IdHistoria == historia.IdHistoria);
        if (nota is null)
            return NotFound(new { success = false, message = "La nota clínica no existe." });
        if (nota.Estado is "Preparación" or "Anulada")
            return Conflict(new { success = false, message = "Esta nota no se puede anular." });

        nota.Estado = "Anulada";
        _auditoria.Agregar(
            HttpContext,
            "UPDATE",
            "Nota_Clinica",
            nota.IdNota,
            $"Nota clínica anulada en la historia {historia.IdHistoria}.");
        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Nota clínica anulada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/finalizar-consulta")]
    public async Task<IActionResult> FinalizarConsulta(
        [FromBody] FinalizarConsultaRequest request,
        CancellationToken ct)
    {
        if (request is null || request.PacienteId <= 0 || request.CitaId <= 0 || request.Nota is null)
            return BadRequest(new { success = false, message = "Paciente, cita y nota son obligatorios." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId))
            return Forbid();

        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        var profesional = idProfesional.HasValue
            ? await _context.Profesionales.FirstOrDefaultAsync(p => p.IdProfesional == idProfesional.Value, ct)
            : null;
        string doctor = profesional is null
            ? "Profesional"
            : $"Dr(a). {profesional.Nombres} {profesional.Apellidos}";

        var (success, message) = await _historiaService.FinalizarConsultaAsync(
            request,
            idProfesional,
            doctor,
            ct);

        if (!success)
            return Conflict(new { success = false, message });

        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId && h.Activa, ct);
        if (historia is not null)
        {
            var consultaClinica = await _context.ConsultasClinicas
                .FirstOrDefaultAsync(c => c.IdHistoria == historia.IdHistoria && c.IdCita == request.CitaId, ct);
            var notaClinicaId = await _context.NotasClinicas
                .Where(n => n.IdCita == request.CitaId && n.IdHistoria == historia.IdHistoria)
                .Select(n => (int?)n.IdNota)
                .FirstOrDefaultAsync(ct);
            if (consultaClinica is not null && notaClinicaId is not null && consultaClinica.IdNota is null)
            {
                consultaClinica.IdNota = notaClinicaId;
                await _context.SaveChangesAsync(ct);
                await _auditoria.RegistrarAsync(
                    HttpContext,
                    "UPDATE",
                    "Consulta_Clinica",
                    consultaClinica.IdConsultaClinica,
                    "Consulta clínica vinculada con la nota generada al finalizar la cita.",
                    ct);
            }

            await _auditoria.RegistrarAsync(
                HttpContext,
                "UPDATE",
                "Historia_Clinica",
                historia.IdHistoria,
                $"Consulta finalizada para el paciente {request.PacienteId}.",
                ct);
        }

        return Ok(new { success = true, message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/guardar-formulario")]
    public async Task<IActionResult> GuardarFormularioHistoria([FromBody] HistoriaFormularioGuardarRequest request)
    {
        if (request is null || request.PacienteId is not > 0)
            return BadRequest(new { success = false, message = "No hay un paciente seleccionado para guardar la historia." });

        if (!await PuedeProfesionalAccederAPacienteAsync(request.PacienteId.Value))
            return Forbid();

        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId.Value && h.Activa);

        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        Cita? cita = null;
        int? idNotaDeCita = null;
        if (request.CitaId is > 0)
        {
            cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == request.CitaId.Value);
            if (cita is null)
                return NotFound(new { success = false, message = "La cita indicada no existe." });
            if (cita.IdPaciente != request.PacienteId.Value
                || (cita.IdProfesional is not null && cita.IdProfesional != idProfesional))
                return Forbid();
            idNotaDeCita = await _context.NotasClinicas
                .Where(n => n.IdCita == cita.IdCita)
                .Select(n => (int?)n.IdNota)
                .FirstOrDefaultAsync();
        }

        var codigosSolicitados = new[] { request.DiagnosticoPrincipalCodigo }
            .Concat(request.DiagnosticosSecundariosCodigos ?? [])
            .Where(codigo => !string.IsNullOrWhiteSpace(codigo))
            .Select(codigo => codigo!.Trim().Replace(".", "", StringComparison.Ordinal).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codigosSolicitados.Count > 21)
            return UnprocessableEntity(new { success = false, message = "Puedes registrar hasta 20 diagnósticos secundarios." });
        var codigoPrincipalNormalizado = request.DiagnosticoPrincipalCodigo?
            .Trim().Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(codigoPrincipalNormalizado))
            return UnprocessableEntity(new { success = false, message = "Selecciona un diagnóstico principal del catálogo CIE-10." });

        var catalogo = await _context.DiagnosticosCie10
            .AsNoTracking()
            .Where(d => codigosSolicitados.Contains(d.Codigo.Replace(".", "")))
            .ToDictionaryAsync(
                d => d.Codigo.Replace(".", ""),
                StringComparer.OrdinalIgnoreCase);
        if (catalogo.Count != codigosSolicitados.Count)
            return UnprocessableEntity(new { success = false, message = "Uno o más códigos CIE-10 no pertenecen al catálogo cargado." });

        Cita? proximaCita = null;
        if (request.ProximaCitaId is > 0)
        {
            proximaCita = await _context.Citas.FirstOrDefaultAsync(c =>
                c.IdCita == request.ProximaCitaId.Value
                && c.IdPaciente == request.PacienteId.Value);
            if (proximaCita is null
                || proximaCita.FechaHora <= DateTime.Now
                || EstadoCitaHelper.Normalize(proximaCita.Estado) == "cancelada"
                || proximaCita.IdCita == request.CitaId)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    message = "La próxima cita debe ser una cita futura vigente del paciente."
                });
            }
        }

        historia ??= await _historiaService.CrearHistoriaClinicaAsync(request.PacienteId.Value);

        ConsultaClinica? consulta = null;
        if (request.ConsultaClinicaId is > 0)
        {
            consulta = await _context.ConsultasClinicas
                .Include(c => c.Diagnosticos)
                .FirstOrDefaultAsync(c => c.IdConsultaClinica == request.ConsultaClinicaId.Value
                    && c.IdHistoria == historia.IdHistoria);
            if (consulta is null || (request.CitaId is > 0 && consulta.IdCita != request.CitaId))
                return NotFound(new { success = false, message = "La consulta clínica indicada no existe." });
        }

        if (consulta is null && cita is not null)
        {
            consulta = await _context.ConsultasClinicas
                .Include(c => c.Diagnosticos)
                .FirstOrDefaultAsync(c => c.IdCita == cita.IdCita);
            if (consulta is null && idNotaDeCita is not null)
            {
                consulta = await _context.ConsultasClinicas
                    .Include(c => c.Diagnosticos)
                    .FirstOrDefaultAsync(c => c.IdNota == idNotaDeCita);
                if (consulta is not null)
                {
                    if (consulta.IdHistoria != historia.IdHistoria
                        || (consulta.IdCita is not null && consulta.IdCita != cita.IdCita))
                        return Conflict(new { success = false, message = "La nota ya está vinculada a otra consulta clínica." });
                    consulta.IdCita = cita.IdCita;
                }
            }
        }

        bool esNuevo = consulta is null;
        consulta ??= new ConsultaClinica
        {
            IdHistoria = historia.IdHistoria,
            IdCita = cita?.IdCita,
            IdNota = idNotaDeCita,
            IdProfesional = idProfesional,
            FechaConsulta = cita?.FechaHora ?? DateTime.UtcNow,
            Origen = "captura"
        };
        consulta.IdProfesional = idProfesional;
        consulta.IdProximaCita = proximaCita?.IdCita;
        consulta.MotivoConsulta = request.MotivoConsulta?.Trim();
        consulta.EnfermedadActual = request.EnfermedadActual?.Trim();
        consulta.AntecedentesMedicos = request.AntecedentesMedicos?.Trim();
        consulta.Habitos = request.Habitos?.Trim();
        consulta.Alergias = request.Alergias?.Trim();
        consulta.Hallazgos = request.Hallazgos?.Trim();
        consulta.OdontogramaObservaciones = request.OdontogramaObservaciones?.Trim();
        consulta.ExamenesComplementarios = request.ExamenesComplementarios?.Trim();
        consulta.EvolucionClinica = request.EvolucionClinica?.Trim();
        consulta.Prescripcion = request.Prescripcion?.Trim();

        if (esNuevo)
            _context.ConsultasClinicas.Add(consulta);
        else
            consulta.Sintomas = [];

        if (!esNuevo)
        {
            _context.SintomasConsulta.RemoveRange(
                await _context.SintomasConsulta.Where(s => s.IdConsultaClinica == consulta.IdConsultaClinica).ToListAsync());
            _context.DiagnosticosConsulta.RemoveRange(
                consulta.Diagnosticos.Where(d => d.CodigoCie10 is not null));
        }

        consulta.Sintomas = (request.SintomasSignos ?? [])
            .Where(sintoma => !string.IsNullOrWhiteSpace(sintoma))
            .Select(sintoma => sintoma.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .Select((sintoma, orden) => new SintomaConsulta { Descripcion = sintoma, Orden = orden })
            .ToList();

        if (!string.IsNullOrWhiteSpace(codigoPrincipalNormalizado)
            && catalogo.TryGetValue(codigoPrincipalNormalizado, out var diagnosticoPrincipal))
        {
            consulta.Diagnosticos.Add(new DiagnosticoConsulta
            {
                CodigoCie10 = diagnosticoPrincipal.Codigo,
                Descripcion = diagnosticoPrincipal.Descripcion,
                EsPrincipal = true
            });
        }

        foreach (var codigo in (request.DiagnosticosSecundariosCodigos ?? [])
            .Where(codigo => !string.IsNullOrWhiteSpace(codigo))
            .Select(codigo => codigo.Trim().Replace(".", "", StringComparison.Ordinal).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (codigo == codigoPrincipalNormalizado)
                continue;

            var diagnostico = catalogo[codigo];
            consulta.Diagnosticos.Add(new DiagnosticoConsulta
            {
                CodigoCie10 = diagnostico.Codigo,
                Descripcion = diagnostico.Descripcion,
                EsPrincipal = false
            });
        }

        await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(
            HttpContext,
            esNuevo ? "INSERT" : "UPDATE",
            "Consulta_Clinica",
            consulta.IdConsultaClinica,
            "Formulario clínico estructurado guardado desde la vista MVC.");

        return Json(new
        {
            success = true,
            consultaClinicaId = consulta.IdConsultaClinica,
            message = "Historia clínica guardada correctamente."
        });
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/cie10")]
    public async Task<IActionResult> BuscarDiagnosticosCie10([FromQuery] string? busqueda)
    {
        var termino = busqueda?.Trim();
        bool consultaInicialK = string.IsNullOrWhiteSpace(termino) || string.Equals(termino, "K", StringComparison.OrdinalIgnoreCase);
        if (!consultaInicialK && termino!.Length < 2)
            return Json(Array.Empty<object>());
        string busquedaFinal = consultaInicialK ? "K" : termino!;
        var codigoSinPuntos = busquedaFinal.Replace(".", "", StringComparison.Ordinal);

        var resultados = await _context.DiagnosticosCie10
            .AsNoTracking()
            .Where(d => consultaInicialK
                ? d.Codigo.StartsWith("K")
                : EF.Functions.Like(d.Codigo, $"%{busquedaFinal}%")
                || EF.Functions.Like(d.Codigo.Replace(".", ""), $"%{codigoSinPuntos}%")
                || EF.Functions.Like(d.Descripcion, $"%{busquedaFinal}%"))
            // Yeray: Prioriza la categoría K para mostrar primero diagnósticos odontológicos sin ocultar otros CIE-10.
            .OrderBy(d => d.Codigo.StartsWith("K") ? 0 : 1)
            .ThenBy(d => d.Codigo)
            .Take(30)
            .Select(d => new { codigo = d.Codigo, descripcion = d.Descripcion })
            .ToListAsync();

        return Json(resultados);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-06-pacientes")]
    public IActionResult Stodo06Pacientes() => View("~/Views/Historia_Clinica/st-odo-06-pacientes/index.cshtml");

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-06-pacientes/data")]
    public async Task<IActionResult> Stodo06PacientesData([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? estado = null,
        [FromQuery] string? alergias = null, [FromQuery] string? cita = null, [FromQuery] string? servicio = null)
    {
        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        var resultado = await BuildPacientesHistorialAsync(idProfesional, page, pageSize, search, estado, alergias, cita, servicio);
        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            null,
            "Consulta paginada del listado de pacientes clínicos del profesional.");
        return Json(resultado);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-07-seguimiento-tratamiento")]
    public IActionResult Stodo07SeguimientoTratamiento() => View("~/Views/Historia_Clinica/st-odo-07-seguimiento-tratamiento/tratamientos.cshtml");

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-07-seguimiento-tratamiento/data")]
    public async Task<IActionResult> Stodo07SeguimientoTratamientoData()
    {
        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        if (idProfesional is null)
            return Forbid();

        var tratamientos = await _context.PlanesTratamiento
            .AsNoTracking()
            .Where(p => p.IdProfesional == idProfesional)
            .OrderByDescending(p => p.ActualizadoEn)
            .Select(p => new
            {
                id = p.IdPlan,
                nombre = p.Servicio!.Nombre,
                tipo = p.Servicio!.Categoria,
                servicioId = p.IdServicio,
                pacienteId = p.HistoriaClinica!.IdPaciente,
                historiaId = p.IdHistoria,
                paciente = p.HistoriaClinica!.Paciente!.Nombres + " " + p.HistoriaClinica.Paciente!.Apellidos,
                cedula = p.HistoriaClinica.Paciente!.Documento,
                odontologo = p.Profesional == null ? "Sin asignar" : "Dr(a). " + p.Profesional.Nombres + " " + p.Profesional.Apellidos,
                estado = p.Estado,
                inicio = p.FechaRegistro,
                finalizado = p.Estado == "completado" ? p.ActualizadoEn : (DateTime?)null,
                numeroFdi = p.NumeroFdi,
                nota = p.Observacion,
                sesionesEstimadas = p.SesionesEstimadas,
                sesionesRealizadas = _context.SesionesPlan.Count(s => s.IdPlan == p.IdPlan && !s.Anulada)
            })
            .ToListAsync();

        var planIds = tratamientos.Select(p => p.id).ToArray();
        var sesiones = await _context.SesionesPlan
            .AsNoTracking()
            .Include(s => s.ProfesionalRegistro)
            .Include(s => s.ProfesionalAnula)
            .Where(s => planIds.Contains(s.IdPlan))
            .OrderByDescending(s => s.FechaSesion)
            .Select(s => new
            {
                id = s.IdSesion,
                planId = s.IdPlan,
                fecha = s.FechaSesion,
                citaId = s.IdCita,
                notaId = s.IdNota,
                anulada = s.Anulada,
                motivoAnulacion = s.MotivoAnulacion,
                profesional = s.ProfesionalRegistro!.Nombres + " " + s.ProfesionalRegistro.Apellidos,
                profesionalAnula = s.ProfesionalAnula == null ? null : s.ProfesionalAnula.Nombres + " " + s.ProfesionalAnula.Apellidos
            })
            .ToListAsync();

        var pacientes = await _context.Pacientes
            .AsNoTracking()
            .Where(p => p.IdProfesionalAsignado == idProfesional
                || _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.IdProfesional == idProfesional))
            .Select(p => new
            {
                id = p.IdPaciente,
                nombre = p.Nombres + " " + p.Apellidos,
                documento = p.Documento
            })
            .OrderBy(p => p.nombre)
            .ToListAsync();

        // Yeray: Devuelve solo citas y notas de pacientes visibles al profesional para ofrecer vínculos opcionales válidos.
        var pacienteIds = pacientes.Select(p => p.id).ToArray();
        var historiasPaciente = await _context.HistoriasClinicas
            .AsNoTracking()
            .Where(h => pacienteIds.Contains(h.IdPaciente))
            .Select(h => new { h.IdHistoria, h.IdPaciente })
            .ToListAsync();
        var historiaIds = historiasPaciente.Select(h => h.IdHistoria).ToArray();
        var citasDisponibles = await _context.Citas
            .AsNoTracking()
            .Where(c => pacienteIds.Contains(c.IdPaciente) && c.IdProfesional == idProfesional)
            .OrderByDescending(c => c.FechaHora)
            .Select(c => new { id = c.IdCita, pacienteId = c.IdPaciente, fecha = c.FechaHora })
            .ToListAsync();
        var notasDisponibles = await _context.NotasClinicas
            .AsNoTracking()
            .Where(n => historiaIds.Contains(n.IdHistoria))
            .OrderByDescending(n => n.Fecha)
            .Select(n => new { id = n.IdNota, historiaId = n.IdHistoria, fecha = n.Fecha })
            .ToListAsync();

        var servicios = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.Estado.ToLower() == "activo")
            .OrderBy(s => s.Nombre)
            .Select(s => new { id = s.IdServicio, nombre = s.Nombre, categoria = s.Categoria })
            .ToListAsync();

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Plan_Tratamiento",
            null,
            "Consulta del seguimiento de tratamientos.");

        return Json(new { tratamientos, sesiones, citas = citasDisponibles, notas = notasDisponibles, pacientes, servicios });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-07-seguimiento-tratamiento/guardar")]
    public async Task<IActionResult> GuardarPlanTratamiento([FromBody] PlanTratamientoGuardarRequest request)
    {
        if (request is null || request.PacienteId is not > 0 || request.ServicioId is not > 0)
            return BadRequest(new { success = false, message = "Selecciona un paciente y un servicio válidos." });
        if (!EstadosPlanTratamiento.Contains(request.Estado))
            return UnprocessableEntity(new { success = false, message = "El estado del plan no es válido." });

        var numeroFdi = string.IsNullOrWhiteSpace(request.NumeroFdi) ? null : request.NumeroFdi.Trim();
        if (numeroFdi is not null && !NumeroFdiHelper.EsPiezaPermanente(numeroFdi))
            return UnprocessableEntity(new { success = false, message = "La pieza dental debe ser un número FDI permanente válido." });
        if (request.Observacion?.Length > 1000)
            return UnprocessableEntity(new { success = false, message = "La observación no puede superar 1000 caracteres." });
        if (request.SesionesEstimadas < 0)
            return UnprocessableEntity(new { success = false, message = "Las sesiones estimadas no pueden ser negativas." });

        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        if (idProfesional is null || !await PuedeProfesionalAccederAPacienteAsync(request.PacienteId.Value))
            return Forbid();

        var servicio = await _context.Servicios
            .FirstOrDefaultAsync(s => s.IdServicio == request.ServicioId.Value && s.Estado.ToLower() == "activo");
        if (servicio is null)
            return UnprocessableEntity(new { success = false, message = "El servicio seleccionado no existe o está inactivo." });

        PlanTratamiento? plan = null;
        int sesionesEstimadasAnteriores = 0;
        if (request.IdPlan is > 0)
        {
            plan = await _context.PlanesTratamiento
                .Include(p => p.HistoriaClinica)
                .FirstOrDefaultAsync(p => p.IdPlan == request.IdPlan.Value);
            if (plan is null)
                return NotFound(new { success = false, message = "El plan de tratamiento no existe." });
            if (plan.IdProfesional != idProfesional
                || plan.HistoriaClinica?.IdPaciente != request.PacienteId.Value)
                return Forbid();
            sesionesEstimadasAnteriores = plan.SesionesEstimadas;
        }
        else
        {
            var pacienteExiste = await _context.Pacientes.AnyAsync(p => p.IdPaciente == request.PacienteId.Value);
            if (!pacienteExiste)
                return NotFound(new { success = false, message = "El paciente no existe." });
            var historia = await _context.HistoriasClinicas
                .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId.Value && h.Activa);
            historia ??= await _historiaService.CrearHistoriaClinicaAsync(request.PacienteId.Value);
            plan = new PlanTratamiento
            {
                IdHistoria = historia.IdHistoria,
                IdProfesional = idProfesional,
                FechaRegistro = DateTime.UtcNow
            };
            _context.PlanesTratamiento.Add(plan);
        }

        plan.IdServicio = request.ServicioId.Value;
        plan.NumeroFdi = numeroFdi;
        plan.Estado = request.Estado;
        plan.Observacion = request.Observacion?.Trim();
        plan.SesionesEstimadas = request.SesionesEstimadas;
        plan.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _auditoria.RegistrarAsync(
            HttpContext,
            request.IdPlan is > 0 ? "UPDATE" : "INSERT",
            "Plan_Tratamiento",
            plan.IdPlan,
            $"Plan guardado; sesiones estimadas {sesionesEstimadasAnteriores} -> {plan.SesionesEstimadas}.");

        return Json(new
        {
            success = true,
            idPlan = plan.IdPlan,
            message = "Plan de tratamiento guardado.",
            advertencia = await _context.SesionesPlan.CountAsync(s => s.IdPlan == plan.IdPlan && !s.Anulada) > plan.SesionesEstimadas
                ? "Las sesiones realizadas superan la estimación; ajusta las sesiones estimadas del plan."
                : null
        });
    }

    // Yeray: Registra cada sesión como evento para que el conteo sea trazable y no dependa de citas ni facturación.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-07-seguimiento-tratamiento/{idPlan:int}/sesiones")]
    public async Task<IActionResult> RegistrarSesionPlan(
        int idPlan,
        [FromBody] PlanSesionRegistrarRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.FechaSesion > DateTime.UtcNow.AddMinutes(5))
            return UnprocessableEntity(new { success = false, message = "Indica una fecha válida de sesión realizada." });

        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        if (idProfesional is null)
            return Forbid();

        var plan = await _context.PlanesTratamiento
            .AsNoTracking()
            .Where(p => p.IdPlan == idPlan && p.IdProfesional == idProfesional)
            .Select(p => new { p.IdPlan, p.IdHistoria, p.SesionesEstimadas, PacienteId = p.HistoriaClinica!.IdPaciente })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
            return NotFound(new { success = false, message = "El plan no existe o no pertenece al profesional." });
        if (!await PuedeProfesionalAccederAPacienteAsync(plan.PacienteId))
            return Forbid();

        if (request.CitaId is > 0 && !await _context.Citas.AnyAsync(c =>
                c.IdCita == request.CitaId && c.IdPaciente == plan.PacienteId && c.IdProfesional == idProfesional,
                cancellationToken))
        {
            return UnprocessableEntity(new { success = false, message = "La cita no corresponde al paciente y profesional del plan." });
        }

        if (request.NotaId is > 0 && !await _context.NotasClinicas.AnyAsync(n =>
                n.IdNota == request.NotaId && n.IdHistoria == plan.IdHistoria,
                cancellationToken))
        {
            return UnprocessableEntity(new { success = false, message = "La nota no pertenece a la historia clínica del plan." });
        }

        if (request.CitaId is > 0 && request.NotaId is > 0 && !await _context.NotasClinicas.AnyAsync(n =>
                n.IdNota == request.NotaId && n.IdCita == request.CitaId,
                cancellationToken))
        {
            return UnprocessableEntity(new { success = false, message = "La nota no está vinculada a la cita seleccionada." });
        }

        // Yeray: La sesión y su auditoría se confirman juntas para no dejar eventos clínicos sin trazabilidad.
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var sesion = new PlanSesion
                {
                    IdPlan = idPlan,
                    FechaSesion = request.FechaSesion ?? DateTime.UtcNow,
                    IdProfesionalRegistro = idProfesional.Value,
                    IdCita = request.CitaId,
                    IdNota = request.NotaId,
                    RegistradaEn = DateTime.UtcNow
                };
                _context.SesionesPlan.Add(sesion);
                await _context.SaveChangesAsync(cancellationToken);
                await _auditoria.RegistrarAsync(
                    HttpContext,
                    "INSERT",
                    "Plan_Sesion",
                    sesion.IdSesion,
                    $"Sesión {sesion.IdSesion} registrada para el plan {idPlan}.",
                    cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });

        int sesionesRealizadas = await _context.SesionesPlan
            .CountAsync(s => s.IdPlan == idPlan && !s.Anulada, cancellationToken);
        bool superaEstimacion = sesionesRealizadas > plan.SesionesEstimadas;
        return Json(new
        {
            success = true,
            message = superaEstimacion
                ? "Sesión registrada. Las realizadas superan la estimación; ajusta el plan."
                : "Sesión realizada registrada.",
            sesionesRealizadas,
            sesionesEstimadas = plan.SesionesEstimadas,
            superaEstimacion
        });
    }

    // Yeray: Anula sin borrar la sesión y exige motivo para mantener una corrección auditable.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-07-seguimiento-tratamiento/sesiones/{idSesion:int}/anular")]
    public async Task<IActionResult> AnularSesionPlan(
        int idSesion,
        [FromBody] PlanSesionAnularRequest? request,
        CancellationToken cancellationToken)
    {
        string motivo = request?.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 3 || motivo.Length > 500)
            return UnprocessableEntity(new { success = false, message = "Indica un motivo de anulación de 3 a 500 caracteres." });

        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        if (idProfesional is null)
            return Forbid();

        var sesionInfo = await _context.SesionesPlan
            .AsNoTracking()
            .Where(s => s.IdSesion == idSesion)
            .Select(s => new
            {
                s.IdSesion,
                s.IdPlan,
                s.Anulada,
                s.Plan!.IdProfesional,
                PacienteId = s.Plan.HistoriaClinica!.IdPaciente
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (sesionInfo is null)
            return NotFound(new { success = false, message = "La sesión no existe." });
        if (sesionInfo.IdProfesional != idProfesional
            || !await PuedeProfesionalAccederAPacienteAsync(sesionInfo.PacienteId))
            return Forbid();
        if (sesionInfo.Anulada)
            return Conflict(new { success = false, message = "La sesión ya está anulada." });

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var sesion = await _context.SesionesPlan
                    .FirstAsync(s => s.IdSesion == idSesion, cancellationToken);
                if (sesion.Anulada)
                    throw new InvalidOperationException("La sesión ya está anulada.");

                sesion.Anulada = true;
                sesion.MotivoAnulacion = motivo;
                sesion.AnuladaEn = DateTime.UtcNow;
                sesion.IdProfesionalAnula = idProfesional.Value;
                await _context.SaveChangesAsync(cancellationToken);
                string descripcion = $"Sesión anulada; plan={sesion.IdPlan}; motivo={motivo}";
                await _auditoria.RegistrarAsync(
                    HttpContext,
                    "UPDATE",
                    "Plan_Sesion",
                    idSesion,
                    descripcion[..Math.Min(500, descripcion.Length)],
                    cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });

        return Json(new { success = true, message = "Sesión anulada con motivo registrado." });
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("historia-clinica/st-rec-historial")]
    public IActionResult StrecHistorial() => View("~/Views/Historia_Clinica/st-rec-historial/historial-rec.cshtml");

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("historia-clinica/st-rec-historial/data")]
    public async Task<IActionResult> StrecHistorialData([FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? odontologo = null,
        [FromQuery] string? alerta = null, [FromQuery] string? estado = null)
    {
        var resultado = await BuildPacientesHistorialAsync(null, page, pageSize, search, estado, alerta, odontologo: odontologo);
        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            null,
            "Consulta paginada del historial clínico desde recepción.");
        return Json(resultado);
    }

    [HttpGet]
[Authorize(Roles = "Paciente")]
[Route("historia-clinica/st-pac-02-historial")]
public async Task<IActionResult> Stpac02Historial()
{
    if (!await PerfilPacienteConfirmadoAsync())
        return RedirigirAPerfilPaciente();

    // Yeray - Defensa específica para el rol Paciente.
    // Motivo: la ruta debe permitir el acceso solo a usuarios autenticados con
    // un paciente asociado; si no existe esa relación de BD, la vista anterior
    // caía en un estado ambiguo y el usuario no entendía qué estaba fallando.
    // Con esta validación solo se bloquea el acceso a la historia de pacientes
    // sin ficha vinculada, sin tocar otras rutas ni módulos externos.
    string? idPacienteClaim = User.FindFirst("IdPaciente")?.Value;
    int? idPacienteDesdeClaim = int.TryParse(idPacienteClaim, out var idPacienteClaimInt)
        ? idPacienteClaimInt
        : null;

    string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    int? idUsuario = int.TryParse(userIdStr, out int uid) ? uid : null;

    Paciente? pacientePropio = idPacienteDesdeClaim is not null
        ? await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == idPacienteDesdeClaim)
        : null;

    pacientePropio ??= idUsuario.HasValue
        ? await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == idUsuario)
        : null;

    // Sin ficha asociada se conserva la vista para mostrar el formulario vacío.
    // Cuando recepción vincula al paciente, la misma ruta carga la información clínica real.
    var vm = await BuildHistorialPacienteViewModelAsync(pacientePropio?.IdPaciente, null);
    await _auditoria.RegistrarAsync(
        HttpContext,
        "SELECT",
        "Historia_Clinica",
        vm.Odontograma.HistoriaId,
        "Consulta del historial clínico propio desde el portal de paciente.");

    return View("~/Views/Historia_Clinica/st-pac-02-historial/index.cshtml", vm);
}

/// <summary>
/// Construye el ViewModel completo de historial clínico (odontograma + alertas +
/// registro de consultas + notas clínicas) para un paciente dado.
/// Usado por la vista del profesional (st-odo-03-historial) y por la del
/// propio paciente (st-pac-02-historial).
/// </summary>
/// <remarks>
/// Yeray (2025) - MIGRACIÓN: vm.NotasClinicas ahora se pobla consultando
/// directamente la tabla Nota_Clinica en lugar de parsear el JSON en
/// Historia_Clinica.ObservacionesGenerales. Eso elimina la dependencia del
/// "dual-write" para la vista de historial del profesional.
///
/// vm.Registros sigue derivándose de Cita (para el mini-odontograma de solo
/// lectura y el historial de consultas por fecha). Las notas clínicas libres
/// van separadas en vm.NotasClinicas.
/// </remarks>
private async Task<HistorialPacienteViewModel> BuildHistorialPacienteViewModelAsync(int? pacienteId, int? historiaId, int? citaId = null)
{
    var vm = new HistorialPacienteViewModel
    {
        Odontograma = await BuildOdontogramaViewModelAsync(pacienteId, historiaId)
    };

    var idPacienteResuelto = vm.Odontograma.PacienteId;
    if (!string.IsNullOrWhiteSpace(vm.Odontograma.ObservacionesGenerales))
    {
        try
        {
            var raiz = JsonNode.Parse(vm.Odontograma.ObservacionesGenerales) as JsonObject;
            var formulario = raiz?["formularioHistoria"] as JsonObject;
            if (formulario is not null)
            {
                vm.Formulario = formulario.Deserialize<HistoriaFormularioViewModel>() ?? new();
            }
        }
        catch (JsonException)
        {
            // Historias antiguas pueden contener solo el JSON del odontograma.
        }
    }

    if (idPacienteResuelto is null) return vm;

    var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == idPacienteResuelto);
    if (paciente is null) return vm;

    if (citaId is > 0)
    {
        vm.Formulario = new();
    }

    if (vm.Odontograma.HistoriaId is > 0)
    {
        var consultasQuery = _context.ConsultasClinicas
            .AsNoTracking()
            .Include(c => c.Diagnosticos)
            .Include(c => c.Sintomas)
            .Where(c => c.IdHistoria == vm.Odontograma.HistoriaId.Value);

        var consulta = citaId is > 0
            ? await consultasQuery.FirstOrDefaultAsync(c => c.IdCita == citaId.Value)
            : await consultasQuery.OrderByDescending(c => c.FechaConsulta ?? c.FechaRegistro)
                .FirstOrDefaultAsync();

        if (consulta is not null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Consulta_Clinica",
                consulta.IdConsultaClinica,
                "Consulta clínica estructurada consultada desde el historial MVC.");
            var diagnosticoPrincipal = consulta.Diagnosticos
                .FirstOrDefault(d => d.EsPrincipal && d.CodigoCie10 is not null)
                ?? consulta.Diagnosticos.FirstOrDefault(d => d.EsPrincipal);
            vm.Formulario = new HistoriaFormularioViewModel
            {
                ConsultaClinicaId = consulta.IdConsultaClinica,
                MotivoConsulta = consulta.MotivoConsulta ?? "",
                EnfermedadActual = consulta.EnfermedadActual ?? "",
                AntecedentesMedicos = consulta.AntecedentesMedicos ?? paciente.AntecedentesMedicos,
                SintomasSignos = consulta.Sintomas.OrderBy(s => s.Orden).Select(s => s.Descripcion).ToList(),
                Habitos = consulta.Habitos ?? "",
                Alergias = consulta.Alergias ?? "",
                Hallazgos = consulta.Hallazgos ?? "",
                OdontogramaObservaciones = consulta.OdontogramaObservaciones ?? "",
                ExamenesComplementarios = consulta.ExamenesComplementarios ?? "",
                DiagnosticoPrincipalCodigo = diagnosticoPrincipal?.CodigoCie10,
                DiagnosticoPrincipal = diagnosticoPrincipal?.Descripcion ?? "",
                DiagnosticosSecundariosCodigos = consulta.Diagnosticos
                    .Where(d => !d.EsPrincipal && d.CodigoCie10 is not null)
                    .Select(d => d.CodigoCie10!)
                    .ToList(),
                DiagnosticoSecundario = string.Join("; ", consulta.Diagnosticos
                    .Where(d => !d.EsPrincipal)
                    .Select(d => d.Descripcion)),
                EvolucionClinica = consulta.EvolucionClinica ?? "",
                Prescripcion = consulta.Prescripcion ?? "",
                ProximaCitaId = consulta.IdProximaCita
            };
        }
    }

    vm.GrupoSanguineo = string.IsNullOrWhiteSpace(paciente.GrupoSanguineo) ? "N/D" : paciente.GrupoSanguineo;
    vm.Alergias = string.IsNullOrWhiteSpace(paciente.Alergias)
        ? []
        : paciente.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    vm.Medicamentos = string.IsNullOrWhiteSpace(paciente.Medicamentos)
        ? []
        : paciente.Medicamentos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    vm.AntecedentesMedicos = string.IsNullOrWhiteSpace(paciente.AntecedentesMedicos)
        ? "Sin antecedentes registrados"
        : paciente.AntecedentesMedicos;
    // FASE 1 — Construir ficha clínica de solo lectura desde Paciente.
    // Motivo: los campos ya estaban en BD pero nunca se serializaban hacia
    // st-odo-03; ahora la vista puede mostrar identidad, contacto y emergencia.
    vm.Paciente = new PacienteResumenClinicoViewModel
    {
        TipoDocumento = paciente.TipoDocumento,
        Documento = paciente.Documento,
        Genero = string.IsNullOrWhiteSpace(paciente.Genero) ? "No registrado" : paciente.Genero,
        Telefono = string.IsNullOrWhiteSpace(paciente.Telefono) ? "No registrado" : paciente.Telefono,
        Correo = string.IsNullOrWhiteSpace(paciente.Correo) ? "No registrado" : paciente.Correo,
        Direccion = string.IsNullOrWhiteSpace(paciente.Direccion) ? "No registrada" : paciente.Direccion,
        Ciudad = string.IsNullOrWhiteSpace(paciente.Ciudad) ? "No registrada" : paciente.Ciudad,
        ContactoEmergencia = string.IsNullOrWhiteSpace(paciente.ContactoEmergencia) ? "No registrado" : paciente.ContactoEmergencia,
        TelefonoEmergencia = string.IsNullOrWhiteSpace(paciente.TelefonoEmergencia) ? "No registrado" : paciente.TelefonoEmergencia
    };

    var citas = await _context.Citas
        .Include(c => c.Servicio)
        .Include(c => c.Profesional)
        .Where(c => c.IdPaciente == paciente.IdPaciente)
        .OrderByDescending(c => c.FechaHora)
        .ToListAsync();

    var ahora = DateTime.Now;

    var proxima = citas
        .Where(c => c.FechaHora > ahora && c.Estado != "Cancelada")
        .OrderBy(c => c.FechaHora)
        .FirstOrDefault();

    vm.ProximaCitaFecha = proxima?.FechaHora;
    vm.ProximaCitaProfesional = proxima?.Profesional is not null
        ? $"Dr(a). {proxima.Profesional.Nombres} {proxima.Profesional.Apellidos}"
        : null;
    vm.CitasDisponibles = citas
        .Where(c => c.FechaHora > ahora
            && EstadoCitaHelper.Normalize(c.Estado) != "cancelada"
            && c.IdCita != citaId)
        .OrderBy(c => c.FechaHora)
        .Select(c => new CitaProximaOpcion
        {
            IdCita = c.IdCita,
            Etiqueta = $"{c.FechaHora:dd/MM/yyyy HH:mm} · {c.Servicio?.Nombre ?? "Consulta"}"
        })
        .ToList();

    // Registros de consultas pasadas (derivados de Cita, para el odontograma de solo lectura)
    vm.Registros = citas
        .Where(c => c.FechaHora <= ahora)
        .Select(c => new RegistroHistorialItem
        {
            CitaId = c.IdCita,
            Fecha = c.FechaHora,
            Tipo = InferirTipoServicio(c.Servicio?.Nombre),
            Descripcion = c.Servicio?.Nombre ?? c.Notas ?? "Consulta",
            Doctor = c.Profesional is not null ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}" : "Sin asignar",
            Estado = c.Estado
        })
        .ToList();

    // ── Yeray (2025) - Notas clínicas reales desde Nota_Clinica ─────────────
    // ANTES: el JS parseaba parseNotasClinicasPersistidas() sobre el JSON de
    //        ObservacionesGenerales. Si ese JSON estaba vacío o corrupto, las
    //        notas no aparecían aunque existieran en la tabla.
    // AHORA: se consultan directamente aquí y se inyectan en vm.NotasClinicas,
    //        que la vista serializa en window.smiletrackHistoriaData.notasClinicas.
    //        El JS ya no necesita parsear ningún JSON para obtenerlas.
    if (vm.Odontograma.HistoriaId is not null)
    {
        vm.NotasClinicas = await _context.NotasClinicas
            .AsNoTracking()
            .Include(n => n.Profesional)
            .Where(n => n.IdHistoria == vm.Odontograma.HistoriaId.Value &&
                        n.Estado != "Preparación")
            .OrderByDescending(n => n.Fecha)
            .Select(n => new NotaClinicaHistorialItem
            {
                IdNota       = n.IdNota,
                CitaId = n.IdCita,
                Titulo       = n.Procedimiento ?? n.Diagnostico ?? "Nota clínica",
                Fecha        = n.Fecha.ToString("yyyy-MM-dd"),
                Doctor       = n.Profesional != null
                                   ? $"Dr(a). {n.Profesional.Nombres} {n.Profesional.Apellidos}"
                                   : "Profesional",
                Diagnostico  = n.Diagnostico  ?? string.Empty,
                Procedimiento = n.Procedimiento ?? string.Empty,
                ProximaCita  = n.ProximaCita,
                Estado       = n.Estado
            })
            .ToListAsync();

        // FASE 1 — Línea de tiempo clínica unificada: cada elemento conserva su origen
        // para que la UI pueda filtrarlo y no confunda una nota, un documento
        // o el estado de una pieza dental.
        vm.LineaDeTiempo.AddRange(vm.NotasClinicas.Select(n => new EventoHistoriaClinicaItem
        {
            NotaId = n.IdNota,
            CitaId = n.CitaId,
            Fecha = DateTime.TryParse(n.Fecha, out var fechaNota) ? fechaNota : DateTime.MinValue,
            Categoria = "nota",
            Titulo = n.Titulo,
            Descripcion = string.Join(" · ", new[] { n.Diagnostico, n.Procedimiento }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Diagnostico = n.Diagnostico,
            Procedimiento = n.Procedimiento,
            Profesional = n.Doctor,
            Estado = n.Estado
        }));

        var registrosOdontograma = await _context.RegistrosOdontograma
            .AsNoTracking()
            .Include(r => r.Profesional)
            .Where(r => r.IdHistoria == vm.Odontograma.HistoriaId.Value)
            .OrderByDescending(r => r.FechaRegistro)
            .ToListAsync();

        vm.LineaDeTiempo.AddRange(registrosOdontograma.Select(r => new EventoHistoriaClinicaItem
        {
            CitaId = r.IdCita,
            Fecha = r.FechaRegistro,
            Categoria = "odontograma",
            Titulo = $"Pieza {r.NumeroFdi}: {r.NombrePieza ?? "registro odontológico"}",
            Descripcion = string.Join(" · ", new[] { r.Estado, r.Observacion }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Profesional = r.Profesional is null ? "Profesional no registrado" : $"Dr(a). {r.Profesional.Nombres} {r.Profesional.Apellidos}",
            Estado = r.Estado
        }));

        var documentos = await _context.DocumentosClinicos
            .AsNoTracking()
            .Where(d => d.IdHistoria == vm.Odontograma.HistoriaId.Value)
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();

        vm.LineaDeTiempo.AddRange(documentos.Select(d => new EventoHistoriaClinicaItem
        {
            Fecha = d.FechaSubida,
            Categoria = "documento",
            Titulo = d.Tipo,
            Descripcion = string.IsNullOrWhiteSpace(d.Observacion) ? d.NombreOriginal : $"{d.NombreOriginal} · {d.Observacion}",
            Profesional = "Documento clínico",
            Estado = "Disponible",
            EnlaceDocumento = $"/historia-clinica/documentos-clinicos/descargar/{d.IdDocumento}"
        }));
    }

    // FASE 1 — Incluir citas ya realizadas en la misma línea de tiempo.
    // Así el historial reúne entradas clínicas y atención programada en orden real.
    vm.LineaDeTiempo.AddRange(vm.Registros.Select(r => new EventoHistoriaClinicaItem
    {
        CitaId = r.CitaId,
        Fecha = r.Fecha,
        Categoria = "consulta",
        Titulo = r.Descripcion,
        Descripcion = r.Estado,
        Profesional = r.Doctor,
        Estado = r.Estado
    }));

    // FASE 1 — Consultar controles postoperatorios asociados al paciente.
    // Antes existían en Control_Postoperatorio pero no se veían desde la historia.
    var controles = await _context.ControlesPostoperatorios
        .AsNoTracking()
        .Include(c => c.Cita)
        .Where(c => c.Cita != null && c.Cita.IdPaciente == paciente.IdPaciente)
        .OrderByDescending(c => c.FechaRegistro)
        .ToListAsync();

    vm.LineaDeTiempo.AddRange(controles.Select(c => new EventoHistoriaClinicaItem
    {
        CitaId = c.IdCita,
        Fecha = c.FechaRegistro,
        Categoria = "control",
        Titulo = "Control postoperatorio",
        Descripcion = c.Observaciones ?? "Sin observaciones registradas",
        Profesional = "Seguimiento clínico",
        Estado = c.Status
    }));

    if (vm.Odontograma.HistoriaId is int historiaAtencionesId)
    {
        var citasAtendidas = citas
            .Where(c => EstadoCitaHelper.Normalize(c.Estado) == "atendida")
            .ToList();
        var idsCitasAtendidas = citasAtendidas.Select(c => c.IdCita).ToList();
        var consultasAtendidas = await _context.ConsultasClinicas
            .AsNoTracking()
            .Include(c => c.Diagnosticos)
            .Include(c => c.Sintomas)
            .Where(c => c.IdHistoria == historiaAtencionesId
                && c.IdCita.HasValue
                && idsCitasAtendidas.Contains(c.IdCita.Value))
            .ToListAsync();

        vm.EvolucionesPorCita = citasAtendidas
            .Select(cita =>
            {
                var consulta = consultasAtendidas.FirstOrDefault(c => c.IdCita == cita.IdCita);
                var notas = vm.NotasClinicas
                    .Where(n => n.CitaId == cita.IdCita && n.Estado != "Anulada")
                    .ToList();
                var diagnosticosConsulta = consulta?.Diagnosticos
                    .OrderByDescending(d => d.EsPrincipal)
                    .Select(d => d.Descripcion) ?? Enumerable.Empty<string>();

                return new EvolucionCitaHistorialItem
                {
                    HistoriaId = historiaAtencionesId,
                    CodigoHistoria = vm.Odontograma.CodigoHC,
                    CitaId = cita.IdCita,
                    Fecha = cita.FechaHora,
                    Servicio = cita.Servicio?.Nombre ?? "Consulta",
                    Profesional = cita.Profesional is null
                        ? "Sin asignar"
                        : $"Dr(a). {cita.Profesional.Nombres} {cita.Profesional.Apellidos}",
                    MotivoConsulta = consulta?.MotivoConsulta ?? cita.MotivoConsulta ?? "",
                    EnfermedadActual = consulta?.EnfermedadActual ?? "",
                    AntecedentesMedicos = consulta?.AntecedentesMedicos ?? "",
                    Habitos = consulta?.Habitos ?? "",
                    Alergias = consulta?.Alergias ?? "",
                    Sintomas = consulta?.Sintomas.OrderBy(s => s.Orden).Select(s => s.Descripcion).ToList() ?? [],
                    Diagnosticos = string.Join("; ", diagnosticosConsulta
                        .Concat(notas.Select(n => n.Diagnostico))
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    Hallazgos = consulta?.Hallazgos ?? "",
                    OdontogramaObservaciones = consulta?.OdontogramaObservaciones ?? "",
                    ExamenesComplementarios = consulta?.ExamenesComplementarios ?? "",
                    EvolucionClinica = consulta?.EvolucionClinica ?? "",
                    Procedimientos = string.Join("\n", notas
                        .Select(n => n.Procedimiento)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    Prescripcion = consulta?.Prescripcion ?? "",
                    RegistrosRelacionados = vm.LineaDeTiempo
                        .Where(evento => evento.CitaId == cita.IdCita
                            && evento.Categoria is "odontograma" or "control")
                        .ToList()
                };
            })
            .OrderByDescending(atencion => atencion.Fecha)
            .ToList();
    }

    // FASE 1 — Orden cronológico único, más reciente primero, para toda la vista.
    vm.LineaDeTiempo = vm.LineaDeTiempo.OrderByDescending(e => e.Fecha).ToList();

    return vm;
}

private static string InferirTipoServicio(string? nombreServicio)
{
    if (string.IsNullOrWhiteSpace(nombreServicio)) return "consulta";
        string n = nombreServicio.ToLowerInvariant();
    if (n.Contains("limpieza") || n.Contains("profilaxis")) return "limpieza";
    if (n.Contains("radiograf")) return "radiografia";
    if (n.Contains("ortodon") || n.Contains("endodon") || n.Contains("implante") || n.Contains("cirugia") || n.Contains("resina") || n.Contains("extrac")) return "tratamiento";
    return "consulta";
}
    private async Task<OdontogramaViewModel> BuildOdontogramaViewModelAsync(int? pacienteId, int? historiaId)
    {
        // FASE 1 — Nunca usar "el primer paciente" como respaldo: en una historia clínica
        // eso podría exponer datos de otra persona. La historia indicada puede
        // resolver al paciente; de lo contrario la vista queda en estado vacío.
        Paciente? paciente = null;
        if (pacienteId is not null)
        {
            paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == pacienteId);
        }
        else if (historiaId is not null)
        {
            paciente = await _context.HistoriasClinicas
                .AsNoTracking()
                .Include(h => h.Paciente)
                .Where(h => h.IdHistoria == historiaId.Value)
                .Select(h => h.Paciente)
                .FirstOrDefaultAsync();
        }

        // Si no hay pacientes aún (seed en background puede estar en progreso), retornar ViewModel seguro
        if (paciente is null)
        {
            return new OdontogramaViewModel
            {
                PacienteId = null,
                HistoriaId = null,
                PacienteNombre = "Sin paciente asignado",
                CodigoHC = string.Empty,
                FechaNacimiento = string.Empty,
                ProfesionalNombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "Profesional",
                ProfesionalCorreo = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                ObservacionesGenerales = null
            };
        }

        var historia = historiaId is > 0
            ? await _context.HistoriasClinicas.FirstOrDefaultAsync(
                h => h.IdHistoria == historiaId && h.IdPaciente == paciente.IdPaciente && h.Activa)
            : null;

        historia ??= await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == paciente.IdPaciente && h.Activa);

        string profesionalNombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "Profesional";
        string profesionalCorreo = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        string? estadoPersistido = historia?.ObservacionesGenerales;

        if (historia is not null)
        {
            JsonObject estadoClinico;
            try
            {
                estadoClinico = JsonNode.Parse(estadoPersistido ?? "{}") as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                estadoClinico = new JsonObject();
            }

            var mapeoJson = estadoClinico["mapeoFDI"] as JsonObject;
            var mapeo = mapeoJson?.ToDictionary(
                item => item.Key,
                item => item.Value?.ToString() ?? "") ?? new Dictionary<string, string>();
            var instanciasPorFdi = mapeo
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .GroupBy(item => item.Value, StringComparer.Ordinal)
                .ToDictionary(grupo => grupo.Key, grupo => grupo.First().Key, StringComparer.Ordinal);

            var registrosClinicos = await _context.RegistrosOdontograma
                .AsNoTracking()
                .Where(registro => registro.IdHistoria == historia.IdHistoria)
                .OrderByDescending(registro => registro.FechaRegistro)
                .ToListAsync();

            if (registrosClinicos.Count > 0 && instanciasPorFdi.Count > 0)
            {
                var registrosActuales = new Dictionary<string, OdontogramaRegistroPayload>();
                foreach (var registro in registrosClinicos
                    .GroupBy(item => item.NumeroFdi)
                    .Select(grupo => grupo.First()))
                {
                    if (!instanciasPorFdi.TryGetValue(registro.NumeroFdi, out string? instanciaId))
                        continue;

                    registrosActuales[instanciaId] = new OdontogramaRegistroPayload
                    {
                        NombrePieza = registro.NombrePieza,
                        Tratamientos =
                        [
                            new OdontogramaTratamientoPayload
                            {
                                Key = registro.Estado,
                                Obs = registro.Observacion,
                                Fecha = registro.FechaRegistro.ToString("O")
                            }
                        ]
                    };
                }

                estadoClinico["registros"] = JsonSerializer.SerializeToNode(
                    registrosActuales,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                estadoPersistido = estadoClinico.ToJsonString();
            }
        }

        return new OdontogramaViewModel
        {
            PacienteId = paciente.IdPaciente,
            HistoriaId = historia?.IdHistoria,
            PacienteNombre = paciente.NombresCompleto,
            CodigoHC = historia is null ? "Sin historia clínica" : $"HC-{historia.IdHistoria:0000}",
            FechaNacimiento = paciente.FechaNacimiento.ToString("yyyy-MM-dd"),
            ProfesionalNombre = profesionalNombre,
            ProfesionalCorreo = profesionalCorreo,
            ObservacionesGenerales = historia?.ObservacionesGenerales,
            EstadoPersistido = estadoPersistido
        };
    }

    private async Task<int?> ObtenerIdProfesionalActualAsync()
    {
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out int userId)) return null;
        var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        return profesional?.IdProfesional;
    }

    private async Task<bool> PuedeProfesionalAccederAPacienteAsync(int pacienteId)
    {
        if (!User.IsInRole("Profesional"))
            return true;

        string? userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdValue, out int userId)
            && await _historiaService.ProfesionalPuedeAccederPacienteAsync(pacienteId, userId);
    }

    private async Task<int?> ResolverPacienteHistoriaAsync(int? pacienteId, int? historiaId)
    {
        if (pacienteId is > 0)
            return pacienteId;
        if (historiaId is not > 0)
            return null;

        return await _context.HistoriasClinicas
            .Where(historia => historia.IdHistoria == historiaId)
            .Select(historia => (int?)historia.IdPaciente)
            .FirstOrDefaultAsync();
    }

    private static string Slug(string texto)
    {
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalizado)
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>
    /// Construye la lista de pacientes con su historial real (consultas desde Cita,
    /// alertas desde Paciente.Alergias). Reemplaza los arreglos de "datos de muestra"
    /// que antes vivían hardcodeados en el JS de st-adm-historial, st-rec-historial y
    /// st-odo-06-pacientes. "tratamientos" y "documentos" se devuelven vacíos porque no
    /// existe una tabla que respalde esos datos en el esquema actual (ver notas en los
    /// endpoints de seguimiento-tratamiento y documentos-clinicos).
    /// </summary>
    private async Task<object> BuildPacientesHistorialAsync(
        int? idProfesionalFiltro,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? estado = null,
        string? alerta = null,
        string? cita = null,
        string? servicio = null,
        string? odontologo = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var ahora = DateTime.Now;
        string[] estadosAtendidos = ["atendida", "atendido", "completada", "completado", "realizada", "realizado", "finalizada", "finalizado"];
        var pacientesQuery = _context.Pacientes.AsNoTracking().AsQueryable();

        if (idProfesionalFiltro is not null)
        {
            pacientesQuery = pacientesQuery.Where(p =>
                _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.IdProfesional == idProfesionalFiltro)
                || p.IdProfesionalAsignado == idProfesionalFiltro);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var texto = search.Trim();
            pacientesQuery = pacientesQuery.Where(p =>
                p.Nombres.Contains(texto) || p.Apellidos.Contains(texto) || p.Documento.Contains(texto) ||
                (p.Correo != null && p.Correo.Contains(texto)) ||
                _context.Citas.Any(c => c.IdPaciente == p.IdPaciente &&
                    ((c.Notas != null && c.Notas.Contains(texto)) ||
                     (c.Servicio != null && c.Servicio.Nombre.Contains(texto)))) ||
                _context.NotasClinicas.Any(n => n.Diagnostico != null && n.Diagnostico.Contains(texto) &&
                    _context.HistoriasClinicas.Any(h => h.IdHistoria == n.IdHistoria && h.IdPaciente == p.IdPaciente)));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var estadoNormalizado = estado.Trim().ToLowerInvariant();
            if (estadoNormalizado == "alta")
                pacientesQuery = pacientesQuery.Where(p => p.Estado != null &&
                    (p.Estado.ToLower() == "inactivo" || p.Estado.ToLower() == "retirado"));
            else if (estadoNormalizado == "seguimiento")
                pacientesQuery = pacientesQuery.Where(p => p.Estado != null && p.Estado.ToLower() == "activo" &&
                    _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.FechaHora <= ahora && c.Estado != null &&
                        estadosAtendidos.Contains(c.Estado.ToLower())) &&
                    _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.FechaHora > ahora &&
                        c.Estado != null && c.Estado.ToLower() != "cancelada" && c.Estado.ToLower() != "cancelado"));
            else if (estadoNormalizado is "activo" or "inactivo" or "retirado")
                pacientesQuery = pacientesQuery.Where(p => p.Estado != null && p.Estado.ToLower() == estadoNormalizado);
        }

        if (string.Equals(alerta, "si", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(alerta, "con", StringComparison.OrdinalIgnoreCase))
            pacientesQuery = pacientesQuery.Where(p => p.Alergias != null && p.Alergias.Trim() != "");
        else if (string.Equals(alerta, "no", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(alerta, "sin", StringComparison.OrdinalIgnoreCase))
            pacientesQuery = pacientesQuery.Where(p => p.Alergias == null || p.Alergias.Trim() == "");

        if (string.Equals(cita, "con", StringComparison.OrdinalIgnoreCase))
            pacientesQuery = pacientesQuery.Where(p => _context.Citas.Any(c => c.IdPaciente == p.IdPaciente &&
                c.FechaHora > ahora && c.Estado != "Cancelada" && c.Estado != "cancelada"));
        else if (string.Equals(cita, "sin", StringComparison.OrdinalIgnoreCase))
            pacientesQuery = pacientesQuery.Where(p => !_context.Citas.Any(c => c.IdPaciente == p.IdPaciente &&
                c.FechaHora > ahora && c.Estado != "Cancelada" && c.Estado != "cancelada"));

        if (!string.IsNullOrWhiteSpace(servicio))
            pacientesQuery = pacientesQuery.Where(p => _context.Citas.Any(c => c.IdPaciente == p.IdPaciente &&
                c.Servicio != null && c.Servicio.Nombre == servicio));

        if (!string.IsNullOrWhiteSpace(odontologo))
        {
            var profesionales = await _context.Profesionales.AsNoTracking()
                .Select(p => new { p.IdProfesional, p.Nombres, p.Apellidos })
                .ToListAsync();
            var idsProfesionales = profesionales
                .Where(p => Slug($"{p.Nombres}{p.Apellidos}") == odontologo)
                .Select(p => p.IdProfesional)
                .ToArray();
            pacientesQuery = pacientesQuery.Where(p => _context.Citas.Any(c =>
                c.IdPaciente == p.IdPaciente && idsProfesionales.Contains(c.IdProfesional ?? 0)));
        }

        int total = await pacientesQuery.CountAsync();
        var pacientes = await pacientesQuery.OrderBy(p => p.Nombres).ThenBy(p => p.Apellidos)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var idsPagina = pacientes.Select(p => p.IdPaciente).ToArray();
        var citas = await _context.Citas.AsNoTracking()
            .Include(c => c.Profesional).Include(c => c.Servicio)
            .Where(c => idsPagina.Contains(c.IdPaciente) &&
                (idProfesionalFiltro == null || c.IdProfesional == idProfesionalFiltro))
            .OrderByDescending(c => c.FechaHora).ToListAsync();
        var historiasPagina = await _context.HistoriasClinicas.AsNoTracking()
            .Where(h => idsPagina.Contains(h.IdPaciente))
            .Select(h => new { h.IdPaciente, h.IdHistoria })
            .ToListAsync();
        var historiaPorPaciente = historiasPagina.ToDictionary(h => h.IdPaciente, h => h.IdHistoria);
        var idsHistoriasPagina = historiasPagina.Select(h => h.IdHistoria).ToArray();
        var documentosPagina = await _context.DocumentosClinicos.AsNoTracking()
            .Where(d => idsHistoriasPagina.Contains(d.IdHistoria))
            .OrderByDescending(d => d.FechaSubida)
            .Select(d => new
            {
                d.IdHistoria,
                nombre = d.NombreOriginal,
                tipo = System.IO.Path.GetExtension(d.NombreOriginal).TrimStart('.').ToLower(),
                fecha = d.FechaDocumento.HasValue
                    ? d.FechaDocumento.Value.ToString("yyyy-MM-dd")
                    : d.FechaSubida.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
        var idsCitasPagina = citas.Select(c => c.IdCita).ToArray();
        var consultasClinicasPagina = idsHistoriasPagina.Length == 0 || idsCitasPagina.Length == 0
            ? []
            : await _context.ConsultasClinicas.AsNoTracking()
                .Include(c => c.Diagnosticos)
                .Include(c => c.Sintomas)
                .Where(c => idsHistoriasPagina.Contains(c.IdHistoria)
                    && c.IdCita.HasValue
                    && idsCitasPagina.Contains(c.IdCita.Value))
                .ToListAsync();
        var consultaClinicaPorCita = consultasClinicasPagina
            .Where(c => c.IdCita.HasValue)
            .ToDictionary(c => c.IdCita!.Value);
        var notasPagina = idsHistoriasPagina.Length == 0 || idsCitasPagina.Length == 0
            ? []
            : await _context.NotasClinicas.AsNoTracking()
                .Where(n => idsHistoriasPagina.Contains(n.IdHistoria)
                    && n.IdCita.HasValue
                    && idsCitasPagina.Contains(n.IdCita.Value)
                    && n.Estado != "Anulada")
                .OrderByDescending(n => n.Fecha)
                .ToListAsync();
        var notaPorCita = notasPagina
            .Where(n => n.IdCita.HasValue)
            .GroupBy(n => n.IdCita!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var resultado = pacientes.Select(p =>
        {
            var citasPaciente = citas.Where(c => c.IdPaciente == p.IdPaciente).ToList();
            var citasAtendidas = citasPaciente
                .Where(c => c.FechaHora <= ahora && EstadoCitaHelper.Normalize(c.Estado) == "atendida")
                .OrderByDescending(c => c.FechaHora)
                .ToList();
            bool tieneConsultaAtendida = citasAtendidas.Count > 0;
            bool tieneCitaFutura = citasPaciente.Any(c => c.FechaHora > ahora &&
                !new[] { "cancelada", "no_asistida", "rechazada" }.Contains(EstadoCitaHelper.Normalize(c.Estado)));
            var ultima = citasAtendidas.FirstOrDefault();
            string odontologo = ultima?.Profesional is not null
                ? $"Dr(a). {ultima.Profesional.Nombres} {ultima.Profesional.Apellidos}"
                : "Sin asignar";
            bool tieneAlerta = !string.IsNullOrWhiteSpace(p.Alergias);

            return new
            {
                id = p.IdPaciente,
                nombre = p.NombresCompleto,
                tipoDocumento = p.TipoDocumento,
                cedula = p.Documento,
                email = p.Correo,
                telefono = p.Telefono,
                fechaNac = p.FechaNacimiento,
                odontologo,
                odontologoKey = ultima?.Profesional is not null ? Slug($"{ultima.Profesional.Nombres}{ultima.Profesional.Apellidos}") : "",
                ultimaConsulta = ultima?.FechaHora,
                estado = string.IsNullOrWhiteSpace(p.Estado) ? "activo" : p.Estado,
                enSeguimiento = tieneConsultaAtendida && tieneCitaFutura,
                alerta = tieneAlerta,
                alertaTexto = tieneAlerta ? p.Alergias : "",
                consultas = citasAtendidas.Take(5).Select(c =>
                {
                    consultaClinicaPorCita.TryGetValue(c.IdCita, out var consultaClinica);
                    notaPorCita.TryGetValue(c.IdCita, out var nota);
                    var diagnosticos = consultaClinica?.Diagnosticos
                        .OrderByDescending(d => d.EsPrincipal)
                        .Select(d => d.Descripcion)
                        .Where(value => !string.IsNullOrWhiteSpace(value)) ?? Enumerable.Empty<string>();
                    return new
                    {
                        fecha = c.FechaHora,
                        proc = nota?.Procedimiento ?? c.Servicio?.Nombre ?? "Consulta",
                        odo = c.Profesional is not null ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}" : "Sin asignar",
                        diagnostico = nota?.Diagnostico ?? string.Join("; ", diagnosticos),
                        motivoConsulta = consultaClinica?.MotivoConsulta ?? c.MotivoConsulta ?? "",
                        enfermedadActual = consultaClinica?.EnfermedadActual ?? "",
                        antecedentesMedicos = consultaClinica?.AntecedentesMedicos ?? "",
                        sintomas = consultaClinica?.Sintomas.OrderBy(s => s.Orden).Select(s => s.Descripcion).ToArray() ?? [],
                        habitos = consultaClinica?.Habitos ?? "",
                        alergias = consultaClinica?.Alergias ?? "",
                        hallazgos = consultaClinica?.Hallazgos ?? "",
                        odontogramaObservaciones = consultaClinica?.OdontogramaObservaciones ?? "",
                        examenesComplementarios = consultaClinica?.ExamenesComplementarios ?? "",
                        evolucionClinica = consultaClinica?.EvolucionClinica ?? "",
                        prescripcion = consultaClinica?.Prescripcion ?? "",
                        nota = string.Join(" · ", new[]
                        {
                            consultaClinica?.EvolucionClinica,
                            nota?.Procedimiento,
                            consultaClinica?.Prescripcion
                        }.Where(value => !string.IsNullOrWhiteSpace(value)))
                    };
                }),
                tratamientos = Array.Empty<object>(),
                documentos = historiaPorPaciente.TryGetValue(p.IdPaciente, out var historiaPaginaId)
                    ? documentosPagina.Where(d => d.IdHistoria == historiaPaginaId)
                        .Select(d => new { d.nombre, d.tipo, d.fecha }).ToList()
                    : []
            };
        }).ToList();

        var odontologos = await _context.Profesionales.AsNoTracking()
            .Select(p => new { p.Nombres, p.Apellidos })
            .ToListAsync();
        var serviciosDisponibles = await _context.Servicios.AsNoTracking()
            .Select(s => s.Nombre)
            .Distinct()
            .OrderBy(nombre => nombre)
            .ToListAsync();
        var universoPacientes = _context.Pacientes.AsNoTracking().AsQueryable();
        if (idProfesionalFiltro is not null)
        {
            universoPacientes = universoPacientes.Where(p =>
                _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.IdProfesional == idProfesionalFiltro)
                || p.IdProfesionalAsignado == idProfesionalFiltro);
        }

        var inicioHoy = DateTime.Today;
        var inicioManana = inicioHoy.AddDays(1);
        var citasHoyQuery = _context.Citas.Where(c => c.FechaHora >= inicioHoy && c.FechaHora < inicioManana);
        if (idProfesionalFiltro is not null)
            citasHoyQuery = citasHoyQuery.Where(c => c.IdProfesional == idProfesionalFiltro);

        int pacientesEnSeguimiento = await universoPacientes.CountAsync(p => p.Estado != null && p.Estado.ToLower() == "activo" &&
            _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.FechaHora <= ahora && c.Estado != null &&
                estadosAtendidos.Contains(c.Estado.ToLower())) &&
            _context.Citas.Any(c => c.IdPaciente == p.IdPaciente && c.FechaHora > ahora &&
                c.Estado != null && c.Estado.ToLower() != "cancelada" && c.Estado.ToLower() != "cancelado"));
        var estadisticas = new
        {
            total = await universoPacientes.CountAsync(),
            hoy = await citasHoyQuery.Select(c => c.IdPaciente).Distinct().CountAsync(),
            alertas = await universoPacientes.CountAsync(p => p.Alergias != null && p.Alergias.Trim() != ""),
            odontologos = await _context.Profesionales.CountAsync(p => p.Estado != null && p.Estado.ToLower() == "activo"),
            activos = await universoPacientes.CountAsync(p => p.Estado != null && p.Estado.ToLower() == "activo"),
            seguimiento = pacientesEnSeguimiento,
            altas = await universoPacientes.CountAsync(p => p.Estado != null &&
                (p.Estado.ToLower() == "inactivo" || p.Estado.ToLower() == "retirado"))
        };

        return new
        {
            items = resultado,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
            stats = estadisticas,
            servicios = serviciosDisponibles,
            odontologos = odontologos
                .Select(p => new { key = Slug($"{p.Nombres}{p.Apellidos}"), nombre = $"Dr(a). {p.Nombres} {p.Apellidos}" })
                .OrderBy(p => p.nombre)
                .ToList()
        };
    }
}
