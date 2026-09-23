using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers;

// Yeray - ACTUALIZACIÓN: la lógica que antes vivía como métodos privados
// (GuardarOdontogramaInternoAsync, RegistrarNotaClinicaAsync,
// RegistrarControlPostoperatorioAsync, CrearHistoriaClinicaAsync) se movió a
// HistoriaClinicaService para que la comparta también la API REST nueva
// (Controllers/Api/HistoriaClinicaApiController.cs), sin duplicar reglas de negocio.
public class HistoriaClinicaController(
    AppDbContext context,
    IHistoriaClinicaService historiaService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly IHistoriaClinicaService _historiaService = historiaService;

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("historia-clinica/st-adm-historial")]
    public IActionResult StadmHistorial() => View("~/Views/Historia_Clinica/st-adm-historial/historial-adm.cshtml");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("historia-clinica/st-adm-historial/data")]
    public async Task<IActionResult> StadmHistorialData()
        => Json(await BuildPacientesHistorialAsync(idProfesionalFiltro: null));

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
            var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == cita.IdPaciente && h.Activa);
            var (status, instructions, observations) = LeerControlPostoperatorio(historia?.ObservacionesGenerales, cita.IdCita);

            vm = new
            {
                citaId = cita.IdCita,
                paciente = cita.Paciente?.NombresCompleto ?? "Paciente sin datos",
                procedimiento = cita.Servicio?.Nombre ?? "Procedimiento",
                fecha = cita.FechaHora,
                status,
                instructions,
                observations
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

        var (success, message) = await _historiaService.RegistrarControlPostoperatorioAsync(request);

        return success
            ? Json(new { success = true })
            : Json(new { success = false, message });
    }

    private static (string status, List<object> instructions, string observations) LeerControlPostoperatorio(string? observacionesGenerales, int citaId)
    {
        var instruccionesPorDefecto = new List<object>
        {
            new { text = "No comer próximas 2h", @checked = false },
            new { text = "Medicamento cada 8h", @checked = false },
            new { text = "Evitar T° extremas", @checked = false },
            new { text = "Control en 7 días", @checked = false }
        };

        if (string.IsNullOrWhiteSpace(observacionesGenerales))
            return ("stable", instruccionesPorDefecto, "");

        try
        {
            var raiz = JsonNode.Parse(observacionesGenerales) as JsonObject;
            var control = raiz?["controlesPostoperatorios"]?[citaId.ToString()] as JsonObject;
            if (control is null) return ("stable", instruccionesPorDefecto, "");

            string status = control["status"]?.GetValue<string>() ?? "stable";
            string observations = control["observations"]?.GetValue<string>() ?? "";
            var instructions = control["instructions"]?.AsArray()?.Select(n => (object)new
            {
                text = n?["text"]?.GetValue<string>() ?? "",
                @checked = n?["checked"]?.GetValue<bool>() ?? false
            }).ToList() ?? instruccionesPorDefecto;

            return (status, instructions, observations);
        }
        catch
        {
            return ("stable", instruccionesPorDefecto, "");
        }
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
        var paciente = pacienteId is not null
            ? await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == pacienteId)
            : await _context.Pacientes.OrderBy(p => p.IdPaciente).FirstOrDefaultAsync();

        var historia = paciente is not null
            ? await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == paciente.IdPaciente && h.Activa)
            : null;

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
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/data")]
    public async Task<IActionResult> Staux08DocumentosClinicosData([FromQuery] int? pacienteId)
    {
        // Sin paciente: devolver lista vacía (el JS ya maneja el empty state)
        if (pacienteId is null)
            return Json(Array.Empty<object>());

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
                fecha         = d.FechaSubida.ToString("yyyy-MM-dd"),
                nombreArchivo = d.NombreOriginal,
                subidoPor     = d.SubidoPorUsuario != null
                                    ? $"{d.SubidoPorUsuario.Nombre} {d.SubidoPorUsuario.Apellidos}"
                                    : "Sin registrar",
                contentType   = d.ContentType,
                tamanoBytes   = d.TamanoBytes,
                observacion   = d.Observacion ?? ""
            })
            .ToListAsync();

        return Json(documentos);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray - Endpoint de subida de archivos clínicos (2025)
    //
    // ANTES: el JS mostraba un setTimeout() que simulaba la subida localmente
    //        y avisaba "la subida a servidor aún no está implementada".
    // AHORA: recibe el archivo via multipart/form-data, lo valida (tipo y tamaño),
    //        lo guarda en wwwroot/uploads/documentos-clinicos/<idHistoria>/,
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
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/subir")]
    public async Task<IActionResult> SubirDocumentoClinco(
        [FromForm] int pacienteId,
        [FromForm] string tipo,
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

        string[] tiposPermitidos = ["image/jpeg", "image/png", "application/pdf"];
        string contentType = archivo.ContentType.ToLowerInvariant();
        if (!tiposPermitidos.Contains(contentType))
            return BadRequest(new { success = false, message = "Tipo de archivo no permitido. Solo JPG, PNG y PDF." });

        if (string.IsNullOrWhiteSpace(tipo))
            return BadRequest(new { success = false, message = "El tipo de documento es obligatorio." });

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
        string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".pdf" }.Contains(extension))
            extension = contentType == "application/pdf" ? ".pdf"
                      : contentType == "image/png"       ? ".png"
                      : ".jpg";

        string nombreFisico  = $"{Guid.NewGuid()}{extension}";
        string carpetaRelativa = Path.Combine("uploads", "documentos-clinicos", historia.IdHistoria.ToString());
        string carpetaFisica   = Path.Combine(env.WebRootPath, carpetaRelativa);

        Directory.CreateDirectory(carpetaFisica);

        string rutaFisica    = Path.Combine(carpetaFisica, nombreFisico);
        string rutaRelativa  = Path.Combine(carpetaRelativa, nombreFisico).Replace('\\', '/');

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
            Tipo          = tipo.Trim(),
            NombreOriginal = Path.GetFileName(archivo.FileName),
            RutaRelativa  = rutaRelativa,
            ContentType   = contentType,
            TamanoBytes   = archivo.Length,
            FechaSubida   = DateTime.UtcNow,
            Observacion   = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim()
        };

        _context.DocumentosClinicos.Add(documento);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Si falla la BD, limpiar el archivo ya guardado en disco
            if (System.IO.File.Exists(rutaFisica))
                System.IO.File.Delete(rutaFisica);

            return StatusCode(500, new { success = false, message = "No fue posible guardar el documento.", detail = ex.Message });
        }

        return Ok(new
        {
            success       = true,
            message       = "Documento subido correctamente.",
            id            = documento.IdDocumento,
            tipo          = documento.Tipo,
            fecha         = documento.FechaSubida.ToString("yyyy-MM-dd"),
            nombreArchivo = documento.NombreOriginal,
            tamanoBytes   = documento.TamanoBytes
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray - Endpoint de descarga de archivos clínicos (2025)
    //
    // Devuelve el archivo físico con Content-Disposition: attachment para forzar
    // la descarga. Solo puede acceder un Auxiliar que esté autenticado.
    // Se valida que el idDocumento exista en BD antes de buscar el archivo en disco.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Auxiliar,Profesional,Administrador")]
    [Route("historia-clinica/documentos-clinicos/descargar/{id:int}")]
    public async Task<IActionResult> DescargarDocumentoClinco(
        int id,
        [FromServices] IWebHostEnvironment env)
    {
        var documento = await _context.DocumentosClinicos
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDocumento == id);

        if (documento is null)
            return NotFound(new { success = false, message = "Documento no encontrado." });

        string rutaFisica = Path.Combine(env.WebRootPath, documento.RutaRelativa.Replace('/', Path.DirectorySeparatorChar));

        if (!System.IO.File.Exists(rutaFisica))
            return NotFound(new { success = false, message = "El archivo no se encuentra en el servidor." });

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(rutaFisica);

        return File(bytes, documento.ContentType, documento.NombreOriginal);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial")]
    public async Task<IActionResult> Stodo03Historial([FromQuery] int? pacienteId, [FromQuery] int? historiaId)
    {
        var vm = await BuildHistorialPacienteViewModelAsync(pacienteId, historiaId);
        return View("~/Views/Historia_Clinica/st-odo-03-historial/gestion-historial.cshtml", vm);
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
        var vm = await BuildOdontogramaViewModelAsync(pacienteId, historiaId);
        // Propagar la cita al ViewModel → la vista la inyecta en config JS
        vm.CitaId = citaId;
        return View("~/Views/Historia_Clinica/st-odo-04-odontograma/odontograma-digital.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-04-odontograma/guardar")]
    public async Task<IActionResult> GuardarOdontograma([FromBody] OdontogramaGuardarRequest request)
    {
        if (request is null)
            return Json(new { success = false, message = "No se recibieron datos del odontograma." });

        int? pacienteId = request.PacienteId ?? await ObtenerPacientePredeterminadoAsync();
        if (pacienteId is null)
            return Json(new { success = false, message = "No hay pacientes registrados en el sistema." });

        // Obtener el profesional actual para trazabilidad
        int? idProfesional = await ObtenerIdProfesionalActualAsync();

        var (success, message, historiaId) = await _historiaService.GuardarOdontogramaInternoAsync(
            pacienteId.Value, request, idProfesional);

        if (!success)
            return Json(new { success = false, message });

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
        // Obtener la historia clínica activa del paciente
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);

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

        return Json(new { success = true, registros = registrosDiente, citas });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/guardar-nota")]
    public async Task<IActionResult> GuardarNotaClinica([FromBody] NotaClinicaGuardarRequest request)
    {
        if (request is null || request.PacienteId is null)
            return Json(new { success = false, message = "No se recibió el paciente para la nota clínica." });

        string? idUsuarioStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? idUsuario = int.TryParse(idUsuarioStr, out int uid) ? uid : null;
        var profesional = idUsuario.HasValue
            ? await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == idUsuario)
            : null;
        string doctor = profesional is not null ? $"Dr(a). {profesional.Nombres} {profesional.Apellidos}" : "Profesional";

        var (success, message, nota) = await _historiaService.RegistrarNotaClinicaAsync(
            request.PacienteId.Value, request, profesional?.IdProfesional, doctor);

        if (!success || nota is null)
            return Json(new { success = false, message });

        // Se mantiene la misma forma que ya consume el JS de st-odo-03-historial.
        return Json(new
        {
            success = true,
            nota = new
            {
                titulo = nota.Procedimiento ?? nota.Diagnostico ?? "Nota clínica",
                fecha = nota.Fecha.ToString("yyyy-MM-dd"),
                doctor = nota.Doctor,
                diagnostico = nota.Diagnostico ?? "",
                procedimiento = nota.Procedimiento ?? "",
                proximaCita = nota.ProximaCita,
                estado = nota.Estado
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/guardar-formulario")]
    public async Task<IActionResult> GuardarFormularioHistoria([FromBody] HistoriaFormularioGuardarRequest request)
    {
        if (request is null || request.PacienteId is null)
            return Json(new { success = false, message = "No hay un paciente seleccionado para guardar la historia." });

        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId.Value && h.Activa);

        historia ??= await _historiaService.CrearHistoriaClinicaAsync(request.PacienteId.Value);

        JsonObject raiz;
        try
        {
            raiz = JsonNode.Parse(historia.ObservacionesGenerales ?? "{}") as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            raiz = new JsonObject();
        }

        raiz["formularioHistoria"] = JsonSerializer.SerializeToNode(new HistoriaFormularioViewModel
        {
            MotivoConsulta = request.MotivoConsulta?.Trim() ?? "",
            EnfermedadActual = request.EnfermedadActual?.Trim() ?? "",
            Habitos = request.Habitos?.Trim() ?? "",
            Hallazgos = request.Hallazgos?.Trim() ?? "",
            OdontogramaObservaciones = request.OdontogramaObservaciones?.Trim() ?? "",
            ExamenesComplementarios = request.ExamenesComplementarios?.Trim() ?? "",
            DiagnosticoPrincipal = request.DiagnosticoPrincipal?.Trim() ?? "",
            DiagnosticoSecundario = request.DiagnosticoSecundario?.Trim() ?? "",
            EvolucionClinica = request.EvolucionClinica?.Trim() ?? "",
            Prescripcion = request.Prescripcion?.Trim() ?? ""
        });

        historia.ObservacionesGenerales = raiz.ToJsonString();
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Historia clínica guardada correctamente." });
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-06-pacientes")]
    public IActionResult Stodo06Pacientes() => View("~/Views/Historia_Clinica/st-odo-06-pacientes/index.cshtml");

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-06-pacientes/data")]
    public async Task<IActionResult> Stodo06PacientesData()
    {
        int? idProfesional = await ObtenerIdProfesionalActualAsync();
        return Json(await BuildPacientesHistorialAsync(idProfesionalFiltro: idProfesional));
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
        // Yeray (2025) - ACTUALIZACIÓN: se agrega cálculo de fecha estimada.
        //
        // ANTES: el campo "estimado" siempre devolvía null con el comentario
        //        "se puede pasar en el futuro". La vista mostraba "—" en todos
        //        los tratamientos en curso.
        //
        // AHORA: para tratamientos "en-curso" se proyecta la fecha estimada así:
        //
        //   1. Se toman las fechas de las sesiones COMPLETADAS y se mide el
        //      intervalo promedio en días entre sesión y sesión.
        //
        //   2. Se cuentan las sesiones que faltan (totalSesiones - completadas).
        //
        //   3. Proyección:
        //        última sesión completada + (sesiones pendientes × promedio días)
        //
        //   4. Si solo hay 1 sesión completada (no se puede promediar),
        //      se usa 30 días como intervalo conservador por defecto.
        //
        //   5. Si no hay ninguna sesión completada aún (progreso 0),
        //      se usa la fecha de inicio + (totalSesiones × 30 días).
        //
        //   Para tratamientos "completado" o "pausado" → estimado = null
        //   (ya tienen "finalizado" o no tiene sentido proyectar un pausado).

        var estadosCompletados = new[] { "completada", "realizada", "atendida" };
        int? idProfesional = await ObtenerIdProfesionalActualAsync();

        var citasQuery = _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .Where(c => c.IdServicio != null);

        if (idProfesional is not null)
            citasQuery = citasQuery.Where(c => c.IdProfesional == idProfesional);

        var citas = await citasQuery.ToListAsync();

        var data = citas
            .GroupBy(c => new { c.IdPaciente, c.IdServicio })
            .Select(g =>
            {
                var ordenadas  = g.OrderBy(c => c.FechaHora).ToList();
                var ultima     = ordenadas[^1];
                int total      = ordenadas.Count;

                int completadas = ordenadas.Count(c =>
                    estadosCompletados.Contains(c.Estado.ToLowerInvariant()));

                int progreso = total == 0 ? 0
                    : (int)Math.Round(completadas * 100.0 / total);

                string estado = progreso >= 100 ? "completado"
                    : ultima.Estado.Equals("cancelada", StringComparison.OrdinalIgnoreCase)
                        ? "pausado"
                        : "en-curso";

                // ── Fecha estimada ────────────────────────────────────────────
                // Solo se calcula para tratamientos en curso; los demás devuelven null.
                DateTime? estimado = null;

                if (estado == "en-curso")
                {
                    int pendientes = total - completadas; // sesiones que faltan

                    // Citas ya completadas ordenadas por fecha (para medir intervalos)
                    var sesionesHechas = ordenadas
                        .Where(c => estadosCompletados.Contains(c.Estado.ToLowerInvariant()))
                        .OrderBy(c => c.FechaHora)
                        .ToList();

                    double promedioDias;

                    if (sesionesHechas.Count >= 2)
                    {
                        // Calcular el promedio real de días entre sesiones consecutivas
                        double totalDias = 0;
                        for (int i = 1; i < sesionesHechas.Count; i++)
                            totalDias += (sesionesHechas[i].FechaHora - sesionesHechas[i - 1].FechaHora).TotalDays;

                        promedioDias = totalDias / (sesionesHechas.Count - 1);
                    }
                    else
                    {
                        // Sin suficiente historial: intervalo conservador de 30 días
                        promedioDias = 30;
                    }

                    // Punto de anclaje: última sesión completada o inicio del tratamiento
                    var ancla = sesionesHechas.Count > 0
                        ? sesionesHechas[^1].FechaHora
                        : ordenadas[0].FechaHora;

                    // Proyectar: ancla + (sesiones pendientes × promedio)
                    estimado = ancla.AddDays(pendientes * promedioDias);
                }
                // ─────────────────────────────────────────────────────────────

                return new
                {
                    id            = $"{g.Key.IdPaciente}-{g.Key.IdServicio}",
                    nombre        = ordenadas[0].Servicio?.Nombre ?? "Servicio",
                    tipo          = ordenadas[0].Servicio?.Nombre ?? "Servicio",
                    pacienteId    = g.Key.IdPaciente,
                    paciente      = ordenadas[0].Paciente != null
                                        ? ordenadas[0].Paciente!.NombresCompleto
                                        : "Paciente sin datos",
                    cedula        = ordenadas[0].Paciente?.Documento ?? "",
                    odontologo    = ultima.Profesional is not null
                                        ? $"Dr(a). {ultima.Profesional.Nombres} {ultima.Profesional.Apellidos}"
                                        : "Sin asignar",
                    estado,
                    progreso,
                    inicio        = ordenadas[0].FechaHora,
                    estimado,                                           // ← antes siempre null
                    finalizado    = estado == "completado" ? ultima.FechaHora : (DateTime?)null,
                    sesiones      = completadas,
                    totalSesiones = total,
                    nota          = ultima.Notas ?? ""
                };
            })
            .OrderByDescending(x => x.inicio)
            .ToList();

        return Json(data);
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("historia-clinica/st-rec-historial")]
    public IActionResult StrecHistorial() => View("~/Views/Historia_Clinica/st-rec-historial/historial-rec.cshtml");

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("historia-clinica/st-rec-historial/data")]
    public async Task<IActionResult> StrecHistorialData()
        => Json(await BuildPacientesHistorialAsync(idProfesionalFiltro: null));

    [HttpGet]
[Authorize(Roles = "Paciente")]
[Route("historia-clinica/st-pac-02-historial")]
public async Task<IActionResult> Stpac02Historial()
{
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
private async Task<HistorialPacienteViewModel> BuildHistorialPacienteViewModelAsync(int? pacienteId, int? historiaId)
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

    // Registros de consultas pasadas (derivados de Cita, para el odontograma de solo lectura)
    vm.Registros = citas
        .Where(c => c.FechaHora <= ahora)
        .Select(c => new RegistroHistorialItem
        {
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
            .Where(n => n.IdHistoria == vm.Odontograma.HistoriaId.Value)
            .OrderByDescending(n => n.Fecha)
            .Select(n => new NotaClinicaHistorialItem
            {
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
            Fecha = DateTime.TryParse(n.Fecha, out var fechaNota) ? fechaNota : DateTime.MinValue,
            Categoria = "nota",
            Titulo = n.Titulo,
            Descripcion = string.Join(" · ", new[] { n.Diagnostico, n.Procedimiento }.Where(x => !string.IsNullOrWhiteSpace(x))),
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
            EnlaceDocumento = "/" + d.RutaRelativa.TrimStart('/')
        }));
    }

    // FASE 1 — Incluir citas ya realizadas en la misma línea de tiempo.
    // Así el historial reúne entradas clínicas y atención programada en orden real.
    vm.LineaDeTiempo.AddRange(vm.Registros.Select(r => new EventoHistoriaClinicaItem
    {
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
        Fecha = c.FechaRegistro,
        Categoria = "control",
        Titulo = "Control postoperatorio",
        Descripcion = c.Observaciones ?? "Sin observaciones registradas",
        Profesional = "Seguimiento clínico",
        Estado = c.Status
    }));

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
                CodigoHC = "HC-SIN-ASIGNAR",
                FechaNacimiento = string.Empty,
                ProfesionalNombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "Profesional",
                ProfesionalCorreo = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                ObservacionesGenerales = null
            };
        }

        var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdHistoria == historiaId)
            ?? await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == paciente.IdPaciente && h.Activa)
            ?? await _historiaService.CrearHistoriaClinicaAsync(paciente.IdPaciente);

        string profesionalNombre = User.FindFirst(ClaimTypes.Name)?.Value ?? "Profesional";
        string profesionalCorreo = User.FindFirst(ClaimTypes.Email)?.Value ?? "";

        return new OdontogramaViewModel
        {
            PacienteId = paciente.IdPaciente,
            HistoriaId = historia.IdHistoria,
            PacienteNombre = paciente.NombresCompleto,
            CodigoHC = $"HC-{historia.IdHistoria:0000}",
            FechaNacimiento = paciente.FechaNacimiento.ToString("yyyy-MM-dd"),
            ProfesionalNombre = profesionalNombre,
            ProfesionalCorreo = profesionalCorreo,
            ObservacionesGenerales = historia.ObservacionesGenerales,
            EstadoPersistido = historia.ObservacionesGenerales
        };
    }

    private async Task<int?> ObtenerPacientePredeterminadoAsync()
    {
        var paciente = await _context.Pacientes.OrderBy(p => p.IdPaciente).FirstOrDefaultAsync();
        return paciente?.IdPaciente;
    }

    private async Task<int?> ObtenerIdProfesionalActualAsync()
    {
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out int userId)) return null;
        var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        return profesional?.IdProfesional;
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
    private async Task<object> BuildPacientesHistorialAsync(int? idProfesionalFiltro)
    {
        var citasQuery = _context.Citas
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .AsQueryable();

        if (idProfesionalFiltro is not null)
            citasQuery = citasQuery.Where(c => c.IdProfesional == idProfesionalFiltro);

        var citas = await citasQuery.OrderByDescending(c => c.FechaHora).ToListAsync();
        var idsPacientesConCita = citas.Select(c => c.IdPaciente).ToHashSet();

        var pacientesQuery = _context.Pacientes.AsQueryable();
        if (idProfesionalFiltro is not null)
            pacientesQuery = pacientesQuery.Where(p => idsPacientesConCita.Contains(p.IdPaciente));

        var pacientes = await pacientesQuery.OrderBy(p => p.Nombres).ToListAsync();

        var resultado = pacientes.Select(p =>
        {
            var citasPaciente = citas.Where(c => c.IdPaciente == p.IdPaciente).ToList();
            var ultima = citasPaciente.FirstOrDefault();
            string odontologo = ultima?.Profesional is not null
                ? $"Dr(a). {ultima.Profesional.Nombres} {ultima.Profesional.Apellidos}"
                : "Sin asignar";
            bool tieneAlerta = !string.IsNullOrWhiteSpace(p.Alergias);

            return new
            {
                id = p.IdPaciente,
                nombre = p.NombresCompleto,
                cedula = p.Documento,
                email = p.Correo,
                telefono = p.Telefono,
                fechaNac = p.FechaNacimiento,
                odontologo,
                odontologoKey = ultima?.Profesional is not null ? Slug($"{ultima.Profesional.Nombres}{ultima.Profesional.Apellidos}") : "",
                ultimaConsulta = ultima?.FechaHora,
                estado = string.IsNullOrWhiteSpace(p.Estado) ? "activo" : p.Estado,
                alerta = tieneAlerta,
                alertaTexto = tieneAlerta ? p.Alergias : "",
                consultas = citasPaciente.Select(c => new
                {
                    fecha = c.FechaHora,
                    proc = c.Servicio?.Nombre ?? c.Notas ?? "Consulta",
                    odo = c.Profesional is not null ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}" : "Sin asignar",
                    nota = c.Notas ?? ""
                }),
                tratamientos = Array.Empty<object>(),
                documentos = Array.Empty<object>()
            };
        }).ToList();

        return resultado;
    }
}
