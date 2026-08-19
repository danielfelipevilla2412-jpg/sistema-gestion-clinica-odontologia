using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class HistoriaClinicaController(AppDbContext context) : Controller
{
    private readonly AppDbContext _context = context;

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

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == request.CitaId);
        if (cita is null)
            return Json(new { success = false, message = "No se encontró la cita indicada." });

        var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == cita.IdPaciente && h.Activa)
            ?? await CrearHistoriaClinicaAsync(cita.IdPaciente);

        var actual = string.IsNullOrWhiteSpace(historia.ObservacionesGenerales)
            ? new JsonObject()
            : (JsonNode.Parse(historia.ObservacionesGenerales) as JsonObject) ?? new JsonObject();

        var controles = actual["controlesPostoperatorios"] as JsonObject ?? new JsonObject();
        var instructionsArray = new JsonArray();
        foreach (var ins in request.Instructions ?? [])
        {
            instructionsArray.Add(new JsonObject
            {
                ["text"] = ins.Text ?? "",
                ["checked"] = ins.Checked
            });
        }
        controles[request.CitaId.Value.ToString()] = new JsonObject
        {
            ["status"] = request.Status ?? "stable",
            ["instructions"] = instructionsArray,
            ["observations"] = request.Observations ?? ""
        };
        actual["controlesPostoperatorios"] = controles;

        historia.ObservacionesGenerales = actual.ToJsonString();
        await _context.SaveChangesAsync();

        return Json(new { success = true });
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

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("historia-clinica/st-aux-08-documentos-clinicos/data")]
    public IActionResult Staux08DocumentosClinicosData([FromQuery] int? pacienteId)
    {
        // NOTA: el esquema actual no tiene una tabla de "documentos clínicos" (adjuntos,
        // radiografías, PDFs, etc.). Antes esta vista mostraba documentos inventados en el JS.
        // Ahora se devuelve un arreglo vacío real (sin datos porque no hay ninguno cargado aún),
        // en vez de simular archivos que nunca existieron.
        return Json(Array.Empty<object>());
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
    public async Task<IActionResult> Stodo04Odontograma([FromQuery] int? pacienteId, [FromQuery] int? historiaId)
    {
        var vm = await BuildOdontogramaViewModelAsync(pacienteId, historiaId);
        return View("~/Views/Historia_Clinica/st-odo-04-odontograma/odontograma-digital.cshtml", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-04-odontograma/guardar")]
    public async Task<IActionResult> GuardarOdontograma([FromBody] OdontogramaGuardarRequest request)
    {
        if (request is null)
        {
            return Json(new { success = false, message = "No se recibieron datos del odontograma." });
        }

        int? pacienteId = request.PacienteId ?? await ObtenerPacientePredeterminadoAsync();
        if (pacienteId is null)
            return Json(new { success = false, message = "No hay pacientes registrados en el sistema." });
        var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa)
        ?? await CrearHistoriaClinicaAsync(pacienteId.Value);

        historia.ObservacionesGenerales = JsonSerializer.Serialize(new
        {
            registros = request.Registros,
            mapeoFDI = request.MapeoFDI,
            actualizadoEn = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Json(new { success = true, historiaId = historia.IdHistoria, message = "Odontograma guardado correctamente." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Profesional")]
    [Route("historia-clinica/st-odo-03-historial/guardar-nota")]
    public async Task<IActionResult> GuardarNotaClinica([FromBody] NotaClinicaGuardarRequest request)
    {
        if (request is null || request.PacienteId is null)
            return Json(new { success = false, message = "No se recibió el paciente para la nota clínica." });

        var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == request.PacienteId && h.Activa)
            ?? await CrearHistoriaClinicaAsync(request.PacienteId.Value);

        string? idUsuarioStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int? idUsuario = int.TryParse(idUsuarioStr, out int uid) ? uid : null;
        var profesional = idUsuario.HasValue
            ? await _context.Profesionales.FirstOrDefaultAsync(p => p.IdUsuario == idUsuario)
            : null;
        string doctor = profesional is not null ? $"Dr(a). {profesional.Nombres} {profesional.Apellidos}" : "Profesional";

        // El esquema no tiene una tabla dedicada a "notas clínicas" libres, así que se
        // guardan dentro del mismo JSON de ObservacionesGenerales que ya usa el odontograma,
        // preservando los registros/mapeoFDI existentes.
        var actual = string.IsNullOrWhiteSpace(historia.ObservacionesGenerales)
            ? new JsonObject()
            : (JsonNode.Parse(historia.ObservacionesGenerales) as JsonObject) ?? new JsonObject();

        var notas = actual["notasClinicas"] as JsonArray ?? new JsonArray();
        var nuevaNota = new JsonObject
        {
            ["titulo"] = request.Procedimiento ?? request.Diagnostico ?? "Nota clínica",
            ["fecha"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["doctor"] = doctor,
            ["diagnostico"] = request.Diagnostico ?? "",
            ["procedimiento"] = request.Procedimiento ?? "",
            ["proximaCita"] = request.ProximaCita,
            ["estado"] = "Realizado"
        };
        notas.Insert(0, nuevaNota);
        actual["notasClinicas"] = notas;
        actual["actualizadoEn"] = DateTime.UtcNow;

        historia.ObservacionesGenerales = actual.ToJsonString();
        await _context.SaveChangesAsync();

        return Json(new { success = true, nota = nuevaNota });
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
        // No existe una tabla de "tratamientos" con progreso/estado en el esquema actual.
        // El progreso y el estado se calculan a partir de las citas reales de cada
        // paciente agrupadas por servicio (no se inventa ningún porcentaje ni estado):
        //   - progreso = % de citas de ese servicio con estado "completada"/"realizada"/"atendida"
        //   - estado   = "completado" si progreso=100%, "pausado" si la cita más reciente
        //                está cancelada, "en-curso" en cualquier otro caso
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
                var ordenadas = g.OrderBy(c => c.FechaHora).ToList();
                var ultima = ordenadas[^1];
                int total = ordenadas.Count;
                int completadas = ordenadas.Count(c => estadosCompletados.Contains(c.Estado.ToLowerInvariant()));
                int progreso = total == 0 ? 0 : (int)Math.Round(completadas * 100.0 / total);
                string estado = progreso >= 100 ? "completado"
                    : ultima.Estado.Equals("cancelada", StringComparison.OrdinalIgnoreCase) ? "pausado"
                    : "en-curso";

                return new
                {
                    id = $"{g.Key.IdPaciente}-{g.Key.IdServicio}",
                    nombre = ordenadas[0].Servicio?.Nombre ?? "Servicio",
                    tipo = ordenadas[0].Servicio?.Nombre ?? "Servicio",
                    pacienteId = g.Key.IdPaciente,
                    paciente = ordenadas[0].Paciente != null ? ordenadas[0].Paciente!.NombresCompleto : "Paciente sin datos",
                    cedula = ordenadas[0].Paciente?.Documento ?? "",
                    odontologo = ultima.Profesional is not null ? $"Dr(a). {ultima.Profesional.Nombres} {ultima.Profesional.Apellidos}" : "Sin asignar",
                    estado,
                    progreso,
                    inicio = ordenadas[0].FechaHora,
                    estimado = estado == "en-curso" ? (DateTime?)null : null,
                    finalizado = estado == "completado" ? ultima.FechaHora : (DateTime?)null,
                    sesiones = completadas,
                    totalSesiones = total,
                    nota = ultima.Notas ?? ""
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
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    int? idUsuario = int.TryParse(userIdStr, out int uid) ? uid : null;

    var pacientePropio = idUsuario.HasValue
        ? await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == idUsuario)
        : null;

    var vm = await BuildHistorialPacienteViewModelAsync(pacientePropio?.IdPaciente, null);

    return View("~/Views/Historia_Clinica/st-pac-02-historial/index.cshtml", vm);
}

/// <summary>
/// Construye el ViewModel completo de historial clínico (odontograma + alertas +
/// registro de consultas) para un paciente dado, usado tanto por la vista del
/// profesional (st-odo-03-historial) como por la del propio paciente (st-pac-02-historial).
/// </summary>
private async Task<HistorialPacienteViewModel> BuildHistorialPacienteViewModelAsync(int? pacienteId, int? historiaId)
{
    var vm = new HistorialPacienteViewModel
    {
        Odontograma = await BuildOdontogramaViewModelAsync(pacienteId, historiaId)
    };

    var idPacienteResuelto = vm.Odontograma.PacienteId;
    if (idPacienteResuelto is null) return vm;

    var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == idPacienteResuelto);
    if (paciente is null) return vm;

    vm.GrupoSanguineo = string.IsNullOrWhiteSpace(paciente.GrupoSanguineo) ? "N/D" : paciente.GrupoSanguineo;
    vm.Alergias = string.IsNullOrWhiteSpace(paciente.Alergias)
        ? []
        : paciente.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    vm.AntecedentesMedicos = string.IsNullOrWhiteSpace(paciente.AntecedentesMedicos)
        ? "Sin antecedentes registrados"
        : paciente.AntecedentesMedicos;

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
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == pacienteId)
            ?? await _context.Pacientes.OrderBy(p => p.IdPaciente).FirstOrDefaultAsync();

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
            ?? await CrearHistoriaClinicaAsync(paciente.IdPaciente);

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
            ObservacionesGenerales = historia.ObservacionesGenerales
        };
    }

    private async Task<HistoriaClinica> CrearHistoriaClinicaAsync(int pacienteId)
    {
        var historiaExistente = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa);
        if (historiaExistente is not null)
        {
            return historiaExistente;
        }

        var historia = new HistoriaClinica
        {
            IdPaciente = pacienteId,
            FechaApertura = DateTime.UtcNow,
            Activa = true,
            ObservacionesGenerales = string.Empty
        };

        _context.HistoriasClinicas.Add(historia);
        await _context.SaveChangesAsync();
        return historia;
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