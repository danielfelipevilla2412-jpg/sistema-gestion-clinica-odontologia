using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace SmileTrack_MVC.Controllers;

public class GestionPacientesController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<GestionPacientesController> _logger;
    private readonly PacienteFusionService _pacienteFusionService;

    public GestionPacientesController(
        AppDbContext context,
        ILogger<GestionPacientesController> logger,
        PacienteFusionService pacienteFusionService)
    {
        _context = context;
        _logger = logger;
        _pacienteFusionService = pacienteFusionService;
    }

    // =========================================================================
    // READ - Gestión de pacientes
    // =========================================================================

    // Yeray (2025) - MIGRACIÓN server-side search:
    // ANTES: cargaba TODOS los pacientes activos y sus citas en dos consultas,
    //        serializaba el array completo en window.RAZOR_PATIENTS y el JS
    //        filtraba/paginaba en cliente. Con muchos pacientes el HTML crecía.
    //
    // AHORA: solo carga la primera página (20 registros) para la carga inicial.
    //        Las búsquedas y cambios de página llaman a GET /gestion-de-pacientes/buscar
    //        que pagina en BD con Skip/Take. Los contadores del header (stats)
    //        se calculan con COUNT() separado para no depender de cuántos se cargan.
    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/st-adm-05-gestion-pacientes")]
    public async Task<IActionResult> Stadm05GestionPacientes([FromQuery] int? editar = null)
    {
        string[] colores = ["blue", "green", "yellow", "purple", "slate"];
        var ahora = DateTime.Now;
        const int pageSize = 20;

        // Totales para los stat-cards (COUNT en BD, no carga de filas)
        int totalActivos   = await _context.Pacientes.CountAsync(p => p.Estado != null && p.Estado.ToLower() == "activo");
        int conProximaCita = await _context.Pacientes.CountAsync(p =>
            p.Estado != null && p.Estado.ToLower() == "activo" && _context.Citas.Any(c =>
                c.IdPaciente == p.IdPaciente && c.FechaHora > ahora &&
                c.Estado != null && c.Estado.ToLower() != "cancelada" && c.Estado.ToLower() != "cancelado"));
        int sinAlergias    = await _context.Pacientes.CountAsync(p =>
            p.Estado != null && p.Estado.ToLower() == "activo" &&
            (p.Alergias == null || p.Alergias.Trim() == ""));
        int conHistorial   = await _context.HistoriasClinicas
            .Select(h => h.IdPaciente).Distinct().CountAsync();

        // Primera página de pacientes activos
        var pacientesDb = await _context.Pacientes
            .Where(p => p.Estado != null && p.Estado.ToLower() == "activo")
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .Take(pageSize)
            .ToListAsync();

        if (editar.HasValue && pacientesDb.All(p => p.IdPaciente != editar.Value))
        {
            var pacienteSolicitado = await _context.Pacientes
                .FirstOrDefaultAsync(p => p.IdPaciente == editar.Value);
            if (pacienteSolicitado is not null)
                pacientesDb.Add(pacienteSolicitado);
        }

        var idsPagina = pacientesDb.Select(p => p.IdPaciente).ToList();

        var citas = await _context.Citas
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .Where(c => idsPagina.Contains(c.IdPaciente))
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync();

        var pacientes = new List<PacienteViewModel>();

        for (int i = 0; i < pacientesDb.Count; i++)
        {
            var p = pacientesDb[i];
            var citasPaciente = citas.Where(c => c.IdPaciente == p.IdPaciente).ToList();

            var ultima  = citasPaciente.Where(c => c.FechaHora <= ahora)
                                       .OrderByDescending(c => c.FechaHora).FirstOrDefault();
            var proxima = citasPaciente.Where(c => c.FechaHora > ahora && c.Estado != "Cancelada")
                                       .OrderBy(c => c.FechaHora).FirstOrDefault();

            var alergias = string.IsNullOrWhiteSpace(p.Alergias)
                ? new List<string>()
                : p.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                        StringSplitOptions.TrimEntries).ToList();

            string iniciales = $"{p.Nombres.Trim().FirstOrDefault()}{p.Apellidos.Trim().FirstOrDefault()}"
                               .ToUpperInvariant();

            pacientes.Add(new PacienteViewModel
            {
                Id              = p.IdPaciente,
                Initials        = string.IsNullOrWhiteSpace(iniciales) ? "??" : iniciales,
                Nombres         = p.Nombres,
                Apellidos       = p.Apellidos,
                Name            = $"{p.Nombres} {p.Apellidos}".Trim(),
                TipoDocumento   = p.TipoDocumento,
                Documento       = p.Documento,
                Doc             = $"{p.TipoDocumento} {p.Documento}",
                FechaNacimiento = p.FechaNacimiento,
                Genero          = p.Genero,
                Telefono        = p.Telefono,
                Correo          = p.Correo,
                Ciudad          = p.Ciudad,
                GrupoSanguineo  = p.GrupoSanguineo,
                Direccion       = p.Direccion,
                ContactoEmergencia = p.ContactoEmergencia,
                TelefonoEmergencia = p.TelefonoEmergencia,
                AntecedentesMedicos = p.AntecedentesMedicos,
                MedicamentosTexto = p.Medicamentos,
                AlergiasTexto   = p.Alergias,
                Estado          = p.Estado,
                LastVisit       = ultima?.FechaHora,
                Diagnosis       = ultima?.Servicio?.Nombre ?? ultima?.Notas ?? string.Empty,
                NextVisit       = proxima?.FechaHora,
                Allergies       = alergias,
                Color           = colores[i % colores.Length],
                History         = citasPaciente
                    .Where(c => c.FechaHora <= ahora)
                    .OrderByDescending(c => c.FechaHora)
                    .Select(c => new PacienteHistorialViewModel
                    {
                        Date      = c.FechaHora,
                        Procedure = c.Servicio?.Nombre ?? c.Notas ?? "Consulta",
                        Doctor    = c.Profesional != null
                                        ? $"{c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                        : "Sin asignar"
                    }).ToList()
            });
        }

        // Pasar stats al ViewBag para los data-target de los stat-cards
        ViewBag.StatTotal      = totalActivos;
        ViewBag.StatHistory    = conHistorial;
        ViewBag.StatNext       = conProximaCita;
        ViewBag.StatNoAllergy  = sinAlergias;

        return View(
            "~/Views/Gestion_De_Pacientes/st-adm-05-gestion-pacientes/index.cshtml",
            pacientes);
    }

    // =========================================================================
    // VISTAS
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - Endpoint JSON de búsqueda paginada server-side
    //
    // MOTIVO: el listado principal cargaba TODOS los pacientes activos en memoria
    //         y el JS filtraba en cliente. Con muchos pacientes eso escala mal.
    //
    // SOLUCIÓN: este endpoint devuelve una página de pacientes con búsqueda por
    //           nombre/apellido/documento y filtro de estado, procesados en BD
    //           (SQL con Skip/Take). El JS lo llama con debounce cada vez que el
    //           usuario escribe en el buscador o cambia de página.
    //
    //           Los filtros locales (alergias, próxima cita, historial) siguen
    //           en cliente porque requieren datos de citas que no conviene repetir
    //           en cada keystroke — se aplican sobre la página recibida.
    //
    // RUTA: GET /gestion-de-pacientes/buscar
    //       ?search=texto &estado=activo &page=1 &pageSize=20
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/buscar")]
    public async Task<IActionResult> BuscarPacientes(
        [FromQuery] string? search,
        [FromQuery] string? estado,
        [FromQuery] string? alergias,
        [FromQuery] string? cita,
        [FromQuery] string? historial,
        [FromQuery] int? editar,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<SmileTrack_MVC.Models.Entities.Paciente> query =
            _context.Pacientes.AsNoTracking();

        if (editar.HasValue)
            query = query.Where(p => p.IdPaciente == editar.Value);

        // Filtro de estado: por defecto solo activos
        if (string.IsNullOrWhiteSpace(estado) ||
            string.Equals(estado, "activo", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Estado != null && p.Estado.ToLower() == "activo");
        }
        else if (!string.Equals(estado, "todos", StringComparison.OrdinalIgnoreCase))
        {
            string est = estado.Trim().ToLowerInvariant();
            query = query.Where(p => p.Estado != null && p.Estado.ToLower() == est);
        }

        // Búsqueda: nombre, apellido o documento
        if (!string.IsNullOrWhiteSpace(search))
        {
            string txt = search.Trim();
            query = query.Where(p =>
                p.Nombres.Contains(txt)   ||
                p.Apellidos.Contains(txt) ||
                p.Documento.Contains(txt) ||
                _context.Citas.Any(c => c.IdPaciente == p.IdPaciente &&
                    (c.Notas != null && c.Notas.Contains(txt) ||
                     c.Servicio != null && c.Servicio.Nombre.Contains(txt))) ||
                _context.NotasClinicas.Any(n => n.IdCita != null &&
                    _context.Citas.Any(c => c.IdCita == n.IdCita && c.IdPaciente == p.IdPaciente) &&
                    n.Diagnostico != null && n.Diagnostico.Contains(txt)));
        }

        if (string.Equals(alergias, "con", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.Alergias != null && p.Alergias.Trim() != "");
        else if (string.Equals(alergias, "sin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.Alergias == null || p.Alergias.Trim() == "");

        var ahora = DateTime.Now;
        if (string.Equals(cita, "con", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => _context.Citas.Any(c =>
                c.IdPaciente == p.IdPaciente && c.FechaHora > ahora &&
                c.Estado != null && c.Estado.ToLower() != "cancelada" && c.Estado.ToLower() != "cancelado"));
        else if (string.Equals(cita, "sin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => !_context.Citas.Any(c =>
                c.IdPaciente == p.IdPaciente && c.FechaHora > ahora &&
                c.Estado != null && c.Estado.ToLower() != "cancelada" && c.Estado.ToLower() != "cancelado"));

        if (string.Equals(historial, "con", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => _context.Citas.Any(c => c.IdPaciente == p.IdPaciente));
        else if (string.Equals(historial, "sin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => !_context.Citas.Any(c => c.IdPaciente == p.IdPaciente));

        int total = await query.CountAsync();

        var pacientesDb = await query
            .OrderBy(p => p.Apellidos)
            .ThenBy(p => p.Nombres)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var idsPagina = pacientesDb.Select(p => p.IdPaciente).ToList();

        // Citas en una sola consulta para la página (evita N+1)
        var citas = await _context.Citas
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .Where(c => idsPagina.Contains(c.IdPaciente))
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync();

        string[] colores = ["blue", "green", "yellow", "purple", "slate"];

        var data = pacientesDb.Select((p, i) =>
        {
            var citasPaciente = citas.Where(c => c.IdPaciente == p.IdPaciente).ToList();
            var ultima  = citasPaciente.Where(c => c.FechaHora <= ahora)
                                       .OrderByDescending(c => c.FechaHora).FirstOrDefault();
            var proxima = citasPaciente.Where(c => c.FechaHora > ahora && c.Estado != "Cancelada")
                                       .OrderBy(c => c.FechaHora).FirstOrDefault();
            var alergias = string.IsNullOrWhiteSpace(p.Alergias)
                ? new List<string>()
                : p.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                        StringSplitOptions.TrimEntries).ToList();
            string iniciales = $"{p.Nombres.Trim().FirstOrDefault()}{p.Apellidos.Trim().FirstOrDefault()}"
                               .ToUpperInvariant();
            int colorIdx = ((page - 1) * pageSize + i) % colores.Length;

            return new
            {
                Id             = p.IdPaciente,
                Nombres        = p.Nombres,
                Apellidos      = p.Apellidos,
                Initials       = string.IsNullOrWhiteSpace(iniciales) ? "??" : iniciales,
                Name           = p.NombresCompleto,
                TipoDocumento  = p.TipoDocumento,
                Documento      = p.Documento,
                Doc            = $"{p.TipoDocumento} {p.Documento}",
                FechaNacimiento = p.FechaNacimiento,
                Genero         = p.Genero,
                Telefono       = p.Telefono,
                Correo         = p.Correo,
                Ciudad         = p.Ciudad,
                Direccion      = p.Direccion,
                GrupoSanguineo = p.GrupoSanguineo,
                AlergiasTexto  = p.Alergias,
                AntecedentesMedicos = p.AntecedentesMedicos,
                ContactoEmergencia = p.ContactoEmergencia,
                TelefonoEmergencia = p.TelefonoEmergencia,
                MedicamentosTexto = p.Medicamentos,
                Estado         = p.Estado,
                LastVisit      = ultima?.FechaHora,
                Diagnosis      = ultima?.Servicio?.Nombre ?? ultima?.Notas ?? string.Empty,
                NextVisit      = proxima?.FechaHora,
                Allergies      = alergias,
                Color          = colores[colorIdx],
                History        = citasPaciente
                    .Where(c => c.FechaHora <= ahora)
                    .OrderByDescending(c => c.FechaHora)
                    .Select(c => new
                    {
                        Date      = c.FechaHora,
                        Procedure = c.Servicio?.Nombre ?? c.Notas ?? "Consulta",
                        Doctor    = c.Profesional != null
                                        ? $"{c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                        : "Sin asignar"
                    }).ToList()
            };
        }).ToList();

        return Json(new
        {
            items      = data,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    // Yeray (2025) - Vista de detalle individual de paciente
    //
    // MOTIVO: antes no existía ninguna vista de perfil completo en el módulo MVC.
    // El modal del listado mostraba datos limitados (solo los del ViewModel del
    // listado). Este endpoint devuelve una vista dedicada con toda la información
    // del paciente: datos personales, médicos, historial de citas y acceso
    // directo a la historia clínica.
    //
    // RUTA: GET /gestion-de-pacientes/{id}
    // ROLES: Administrador, Recepcionista, Profesional
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/{id:int}")]
    public async Task<IActionResult> DetallePaciente(int id)
    {
        var p = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdPaciente == id);

        if (p is null)
            return NotFound();

        var ahora = DateTime.Now;

        // Citas del paciente en una sola consulta
        var citas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .Where(c => c.IdPaciente == id)
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync();

        var ultima  = citas.Where(c => c.FechaHora <= ahora).FirstOrDefault();
        var proxima = citas.Where(c => c.FechaHora > ahora && c.Estado != "Cancelada")
                          .OrderBy(c => c.FechaHora).FirstOrDefault();

        var alergias = string.IsNullOrWhiteSpace(p.Alergias)
            ? new List<string>()
            : p.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var vm = new PacienteViewModel
        {
            Id              = p.IdPaciente,
            Nombres         = p.Nombres,
            Apellidos       = p.Apellidos,
            Name            = p.NombresCompleto,
            Initials        = $"{p.Nombres.Trim().FirstOrDefault()}{p.Apellidos.Trim().FirstOrDefault()}".ToUpperInvariant(),
            TipoDocumento   = p.TipoDocumento,
            Documento       = p.Documento,
            Doc             = $"{p.TipoDocumento} {p.Documento}",
            FechaNacimiento = p.FechaNacimiento,
            Genero          = p.Genero,
            Telefono        = p.Telefono,
            Correo          = p.Correo,
            Ciudad          = p.Ciudad,
            Direccion       = p.Direccion,
            GrupoSanguineo  = p.GrupoSanguineo,
            AlergiasTexto   = p.Alergias,
            Allergies       = alergias,
            Estado          = p.Estado,
            FechaRegistro   = p.FechaRegistro,
            // Campos médicos completos (antes solo disponibles en la API REST)
            AntecedentesMedicos  = p.AntecedentesMedicos,
            ContactoEmergencia   = p.ContactoEmergencia,
            TelefonoEmergencia   = p.TelefonoEmergencia,
            // Actividad
            TotalCitas       = citas.Count,
            CitasPendientes  = citas.Count(c => c.FechaHora > ahora && c.Estado != "Cancelada"),
            LastVisit        = ultima?.FechaHora,
            Diagnosis        = ultima?.Servicio?.Nombre ?? ultima?.Notas ?? string.Empty,
            NextVisit        = proxima?.FechaHora,
            // Yeray (2025) - ruta relativa del archivo adjunto (foto/documento)
            ArchivoAdjunto   = p.ArchivoAdjunto,
            History          = citas
                .Where(c => c.FechaHora <= ahora)
                .Select(c => new PacienteHistorialViewModel
                {
                    Date      = c.FechaHora,
                    Procedure = c.Servicio?.Nombre ?? c.Notas ?? "Consulta",
                    Doctor    = c.Profesional is not null
                                    ? $"{c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                    : "Sin asignar"
                })
                .ToList()
        };

        return View("~/Views/Gestion_De_Pacientes/detalle-paciente/index.cshtml", vm);
    }

    // Yeray: Limita la fusión a administración y exige antifalsificación para evitar cambios clínicos accidentales.
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pacientes/fusionar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FusionarPacientes(
        [FromForm] int idPrincipal,
        [FromForm] int idDuplicado,
        CancellationToken cancellationToken)
    {
        if (idPrincipal <= 0)
            return BadRequest();

        var resultado = await _pacienteFusionService.FusionarAsync(
            HttpContext,
            idPrincipal,
            idDuplicado,
            cancellationToken);

        TempData["PacienteFusionMensaje"] = resultado.Mensaje;
        TempData["PacienteFusionError"] = !resultado.Fusionada;
        return Redirect($"/gestion-de-pacientes/{idPrincipal}");
    }

    // Yeray (2025) - Listado de pacientes para Recepcionista
    //
    // PROBLEMA: el sidebar de Recepcionista apuntaba a la ruta del admin
    //   (/gestion-de-pacientes/st-adm-05-gestion-pacientes), por lo que la
    //   recepcionista veía la vista de administrador con el sidebar de admin.
    //
    // SOLUCIÓN: ruta propia /gestion-de-pacientes/st-rec-pacientes que
    //   reutiliza exactamente la misma lógica y vista del listado admin, pero
    //   renderiza el _SidebarRecepcionista a través de ViewData. El sidebar
    //   de Recepcionista ahora apunta a esta ruta en vez de la del admin.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-pacientes/st-rec-pacientes")]
    public async Task<IActionResult> StrecGestionPacientes()
    {
        // Reutiliza la misma lógica de Stadm05GestionPacientes, cambiando
        // únicamente el sidebar que se inyecta (via ViewData).
        ViewData["SidebarPartial"] = "_SidebarRecepcionista";
        return await Stadm05GestionPacientes();
    }

    // Los catálogos estáticos del formulario se definen en el ViewModel.
    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-pacientes/st-rec-02-registrar-paciente")]
    public IActionResult Strec02RegistrarPaciente()
    {
        return View(
            "~/Views/Gestion_De_Pacientes/st-rec-02-registrar-paciente/nuevo_paciente.cshtml",
            new RegistrarPacienteViewModel());
    }

    // =========================================================================
    // ALERGIAS ESTRUCTURADAS — CRUD sobre Alergia_Paciente
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - GET alergias de un paciente
    //
    // PARA QUÉ SIRVE: devuelve las alergias estructuradas (tabla Alergia_Paciente)
    // de un paciente. La vista de detalle y el panel de preparación pueden llamar
    // este endpoint para mostrar alergias con severidad y tipo en vez del texto CSV.
    //
    // No reemplaza Paciente.Alergias — coexiste con él.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar")]
    [Route("gestion-de-pacientes/{idPaciente:int}/alergias")]
    public async Task<IActionResult> ObtenerAlergias(int idPaciente)
    {
        if (idPaciente <= 0)
            return BadRequest(new { ok = false, message = "Identificador inválido." });

        bool existe = await _context.Pacientes.AnyAsync(p => p.IdPaciente == idPaciente);
        if (!existe)
            return NotFound(new { ok = false, message = "Paciente no encontrado." });

        var alergias = await _context.AlergiasPaciente
            .AsNoTracking()
            .Where(a => a.IdPaciente == idPaciente && a.Activa)
            .OrderBy(a => a.Severidad)   // grave primero en orden DESC se hace en el JS
            .ThenBy(a => a.Sustancia)
            .Select(a => new
            {
                id           = a.IdAlergia,
                sustancia    = a.Sustancia,
                tipo         = a.Tipo,
                severidad    = a.Severidad,
                reaccion     = a.Reaccion,
                fechaRegistro = a.FechaRegistro.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { ok = true, data = alergias });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - POST registrar nueva alergia estructurada
    //
    // PARA QUÉ SIRVE: crea una fila en Alergia_Paciente con sustancia, tipo,
    // severidad y reacción. También actualiza el campo de texto Paciente.Alergias
    // para mantener compatibilidad con el código que ya lo usa.
    //
    // ROLES: Administrador, Recepcionista, Profesional (los mismos que pueden
    // editar un paciente).
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/{idPaciente:int}/alergias/agregar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarAlergia(
        int idPaciente,
        [FromForm] string sustancia,
        [FromForm] string tipo,
        [FromForm] string severidad,
        [FromForm] string? reaccion)
    {
        if (string.IsNullOrWhiteSpace(sustancia) || sustancia.Trim().Length > 150)
            return BadRequest(new { ok = false, message = "La sustancia es obligatoria y no puede superar 150 caracteres." });

        // Validar valores contra los CHECK de la BD
        string[] tiposValidos      = ["medicamento", "alimento", "ambiental", "latex", "otro"];
        string[] severidadesValidas = ["leve", "moderada", "grave"];

        string tipoNorm      = tipo?.Trim().ToLowerInvariant()      ?? "otro";
        string severidadNorm = severidad?.Trim().ToLowerInvariant() ?? "leve";
        if (reaccion?.Trim().Length > 300)
            return BadRequest(new { ok = false, message = "La reacción no puede superar 300 caracteres." });

        if (!tiposValidos.Contains(tipoNorm))
            return BadRequest(new { ok = false, message = "Tipo de alergia no válido." });

        if (!severidadesValidas.Contains(severidadNorm))
            return BadRequest(new { ok = false, message = "Severidad no válida." });

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente);

        if (paciente is null)
            return NotFound(new { ok = false, message = "Paciente no encontrado." });

        // Verificar que no exista ya la misma sustancia activa
        bool yaExiste = await _context.AlergiasPaciente
            .AnyAsync(a => a.IdPaciente == idPaciente &&
                           a.Sustancia == sustancia.Trim() &&
                           a.Activa);

        if (yaExiste)
            return Conflict(new { ok = false, message = "Ya existe una alergia activa con esa sustancia." });

        var alergia = new AlergiaPaciente
        {
            IdPaciente    = idPaciente,
            Sustancia     = sustancia.Trim(),
            Tipo          = tipoNorm,
            Severidad     = severidadNorm,
            Reaccion      = string.IsNullOrWhiteSpace(reaccion) ? null : reaccion.Trim(),
            FechaRegistro = DateTime.UtcNow,
            Activa        = true
        };

        _context.AlergiasPaciente.Add(alergia);

        // Sincronizar con el campo de texto Paciente.Alergias para compatibilidad
        // con el código que sigue leyendo ese campo (preparacion, detalle, API).
        var alergiasList = string.IsNullOrWhiteSpace(paciente.Alergias)
            ? new List<string>()
            : paciente.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                          StringSplitOptions.TrimEntries).ToList();

        if (!alergiasList.Contains(sustancia.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            alergiasList.Add(sustancia.Trim());
            paciente.Alergias = string.Join(", ", alergiasList);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                ok      = false,
                message = "No fue posible registrar la alergia."
            });
        }

        return Ok(new
        {
            ok        = true,
            message   = "Alergia registrada correctamente.",
            id        = alergia.IdAlergia,
            sustancia = alergia.Sustancia,
            tipo      = alergia.Tipo,
            severidad = alergia.Severidad
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - POST desactivar alergia (soft-delete)
    //
    // PARA QUÉ SIRVE: marca la alergia como inactiva (activa = false) sin borrarla
    // físicamente, preservando el historial. También actualiza Paciente.Alergias
    // para mantener la sincronización con el campo de texto.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/alergias/{idAlergia:int}/desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesactivarAlergia(int idAlergia)
    {
        var alergia = await _context.AlergiasPaciente
            .Include(a => a.Paciente)
            .FirstOrDefaultAsync(a => a.IdAlergia == idAlergia);

        if (alergia is null)
            return NotFound(new { ok = false, message = "Alergia no encontrada." });

        if (!alergia.Activa)
            return BadRequest(new { ok = false, message = "La alergia ya está inactiva." });

        alergia.Activa = false;

        // Quitar la sustancia del campo de texto para mantener sincronía
        if (alergia.Paciente is not null &&
            !string.IsNullOrWhiteSpace(alergia.Paciente.Alergias))
        {
            var lista = alergia.Paciente.Alergias
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !s.Equals(alergia.Sustancia, StringComparison.OrdinalIgnoreCase))
                .ToList();

            alergia.Paciente.Alergias = lista.Count > 0
                ? string.Join(", ", lista)
                : null;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                ok      = false,
                message = "No fue posible desactivar la alergia."
            });
        }

        return Ok(new { ok = true, message = "Alergia desactivada correctamente." });
    }

    // =========================================================================
    // CREATE - Registrar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPaciente(
        [FromForm] string? tipoDoc,
        [FromForm] string? documento,
        [FromForm] string? nombres,
        [FromForm] string? apellidos,
        [FromForm] DateTime fechaNacimiento,
        [FromForm] string? genero,
        [FromForm] string? telefono,
        [FromForm] string? correo,
        [FromForm] string? grupoSanguineo,
        [FromForm] string? ciudad,
        [FromForm] string? alergias,
        [FromForm] string? medicamentos,
        [FromForm] string? direccion,
        [FromForm] string? contactoEmergencia,
        [FromForm] string? telefonoEmergencia,
        [FromForm] string? antecedentesMedicos)
    {
        try
        {
            var tipoDocumento = tipoDoc?.Trim().ToUpperInvariant();
            var documentoNormalizado = documento?.Trim().ToUpperInvariant();
            var nombresNormalizados = nombres?.Trim();
            var apellidosNormalizados = apellidos?.Trim();

            if (tipoDocumento is not ("CC" or "TI" or "CE" or "PAS" or "NIT"))
                return BadRequest(new { ok = false, message = "El tipo de documento no es válido." });

            if (string.IsNullOrWhiteSpace(documentoNormalizado) ||
                documentoNormalizado.Length > 20 ||
                (tipoDocumento == "PAS"
                    ? !System.Text.RegularExpressions.Regex.IsMatch(documentoNormalizado, "^[A-Z0-9]{5,20}$")
                    : !System.Text.RegularExpressions.Regex.IsMatch(documentoNormalizado, "^[0-9]{7,15}$")))
            {
                return BadRequest(new { ok = false, message = "El documento no es válido para el tipo seleccionado." });
            }

            if (string.IsNullOrWhiteSpace(nombresNormalizados) || nombresNormalizados.Length > 100 ||
                string.IsNullOrWhiteSpace(apellidosNormalizados) || apellidosNormalizados.Length > 100)
                return BadRequest(new { ok = false, message = "Nombres y apellidos son obligatorios y deben tener hasta 100 caracteres." });

            if (fechaNacimiento.Date < new DateTime(1900, 1, 1) || fechaNacimiento.Date > DateTime.Today)
                return BadRequest(new { ok = false, message = "La fecha de nacimiento no es válida." });

            if ((tipoDocumento == "TI" || fechaNacimiento.Date > DateTime.Today.AddYears(-18)) &&
                (string.IsNullOrWhiteSpace(contactoEmergencia) ||
                 !System.Text.RegularExpressions.Regex.IsMatch(
                     (telefonoEmergencia ?? "").Replace(" ", ""),
                     "^[0-9]{7,15}$")))
                return BadRequest(new { ok = false, message = "Los menores de edad y pacientes con documento TI requieren un acudiente y su teléfono." });

            if (!string.IsNullOrWhiteSpace(correo) &&
                (correo.Trim().Length > 150 || !new EmailAddressAttribute().IsValid(correo.Trim())))
                return BadRequest(new { ok = false, message = "El correo electrónico no es válido." });

            if (telefono?.Trim().Length > 20 || telefonoEmergencia?.Trim().Length > 20 ||
                direccion?.Trim().Length > 255 || ciudad?.Trim().Length > 100 ||
                contactoEmergencia?.Trim().Length > 100)
                return BadRequest(new { ok = false, message = "Uno o más datos de contacto superan la longitud permitida." });

            var grupoNormalizado = string.IsNullOrWhiteSpace(grupoSanguineo)
                ? null
                : grupoSanguineo.Trim().ToUpperInvariant();
            if (grupoNormalizado is not null &&
                grupoNormalizado is not ("O+" or "O-" or "A+" or "A-" or "B+" or "B-" or "AB+" or "AB-"))
                return BadRequest(new { ok = false, message = "El grupo sanguíneo no es válido." });

            if (TieneSustanciaAlergiaNoValida(alergias))
                return BadRequest(new { ok = false, message = "Cada alergia debe tener como máximo 150 caracteres." });

            bool existe =
                await _context.Pacientes.AnyAsync(
                    p =>
                        p.Documento ==
                        documentoNormalizado);

            if (existe)
            {
                return Conflict(
                    new
                    {
                        ok = false,
                        message =
                            "Ya existe un paciente con ese documento."
                    });
            }

            // Validación consistente con CHECK de SQL Server.
            string? generoNormalizado = null;

            if (!string.IsNullOrWhiteSpace(genero))
            {
                generoNormalizado =
                    genero.Trim().ToUpperInvariant();

                if (generoNormalizado is not ("M" or "F" or "O"))
                {
                    return BadRequest(
                        new
                        {
                            ok = false,
                            message =
                                "El género enviado no es válido."
                        });
                }
            }

            var paciente =
                new SmileTrack_MVC.Models.Entities.Paciente
                {
                    TipoDocumento =
                        tipoDocumento,

                    Documento =
                        documentoNormalizado,

                    Nombres =
                        nombresNormalizados,

                    Apellidos =
                        apellidosNormalizados,

                    FechaNacimiento =
                        fechaNacimiento,

                    Genero =
                        generoNormalizado,

                    Telefono =
                        string.IsNullOrWhiteSpace(telefono)
                            ? null
                            : telefono.Trim(),

                    Correo =
                        string.IsNullOrWhiteSpace(correo)
                            ? null
                            : correo.Trim(),

                    GrupoSanguineo =
                        grupoNormalizado,

                    Ciudad =
                        string.IsNullOrWhiteSpace(ciudad)
                            ? null
                            : ciudad.Trim(),

                    Direccion =
                        string.IsNullOrWhiteSpace(direccion)
                            ? null
                            : direccion.Trim(),

                    ContactoEmergencia =
                        string.IsNullOrWhiteSpace(contactoEmergencia)
                            ? null
                            : contactoEmergencia.Trim(),

                    TelefonoEmergencia =
                        string.IsNullOrWhiteSpace(telefonoEmergencia)
                            ? null
                            : telefonoEmergencia.Trim(),

                    Alergias =
                        string.IsNullOrWhiteSpace(alergias)
                            ? null
                            : alergias.Trim(),

                    Medicamentos =
                        string.IsNullOrWhiteSpace(medicamentos)
                            ? null
                            : medicamentos.Trim(),

                    AntecedentesMedicos =
                        string.IsNullOrWhiteSpace(antecedentesMedicos)
                            ? null
                            : antecedentesMedicos.Trim(),

                    Estado =
                        "activo",

                    FechaRegistro =
                        DateTime.UtcNow.Date
                };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.Pacientes.Add(
                paciente);

            await _context.SaveChangesAsync();

            await PacienteAlergiaSync.SynchronizeAsync(_context, paciente, paciente.Alergias);
            var historia = new HistoriaClinica
            {
                IdPaciente = paciente.IdPaciente,
                FechaApertura = DateTime.UtcNow,
                Activa = true,
                ObservacionesGenerales = string.Empty
            };
            _context.HistoriasClinicas.Add(historia);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(
                new
                {
                    ok = true,
                    idPaciente =
                        paciente.IdPaciente,

                    codigoHistoria = $"HC-{historia.IdHistoria:D6}",

                    message =
                        "Paciente registrado correctamente."
                });
        }
        catch (DbUpdateException ex) when (EsConflictoDocumento(ex))
        {
            return Conflict(new { ok = false, message = "Ya existe un paciente con ese documento." });
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,
                    message =
                        "No fue posible registrar el paciente."
                });
        }
    }

    // =========================================================================
    // UPDATE - Actualizar paciente
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - ActualizarPaciente: se agregan los 3 campos faltantes.
    //
    // ANTES: el endpoint solo actualizaba Nombres, Apellidos, Telefono, Correo,
    //        Ciudad, Alergias, Estado y Genero. Los campos ContactoEmergencia,
    //        TelefonoEmergencia y AntecedentesMedicos solo se podían editar vía
    //        la API REST (PATCH api/v1/pacientes/{id}).
    //
    // AHORA: se agregan los 3 parámetros faltantes al endpoint MVC para que
    //        el modal de edición del listado también pueda modificarlos.
    //        La lógica es null-safe igual que los demás campos: si llega null
    //        o vacío, el campo se pone a null en BD; si llega valor, se guarda.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/actualizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarPaciente(
        [FromForm] int idPaciente,
        [FromForm] string? nombres,
        [FromForm] string? apellidos,
        [FromForm] string? telefono,
        [FromForm] string? correo,
        [FromForm] string? ciudad,
        [FromForm] string? alergias,
        [FromForm] string? medicamentos,
        [FromForm] string? estado,
        [FromForm] string? genero,
        // Yeray (2025) - campos nuevos que antes solo actualizaba la API REST
        [FromForm] string? contactoEmergencia,
        [FromForm] string? telefonoEmergencia,
        [FromForm] string? antecedentesMedicos,
        [FromForm] string? tipoDoc,
        [FromForm] string? documento,
        [FromForm] DateTime? fechaNacimiento,
        [FromForm] string? grupoSanguineo,
        [FromForm] string? direccion)
    {
        try
        {
            var paciente =
                await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p =>
                            p.IdPaciente ==
                            idPaciente);

            if (paciente == null)
            {
                return NotFound(
                    new
                    {
                        ok = false,
                        message =
                            "Paciente no encontrado."
                    });
            }

            if ((!string.IsNullOrWhiteSpace(correo) &&
                 (correo.Trim().Length > 150 || !new EmailAddressAttribute().IsValid(correo.Trim()))) ||
                nombres?.Trim().Length > 100 ||
                apellidos?.Trim().Length > 100 ||
                telefono?.Trim().Length > 20 ||
                telefonoEmergencia?.Trim().Length > 20 ||
                ciudad?.Trim().Length > 100 ||
                direccion?.Trim().Length > 255 ||
                contactoEmergencia?.Trim().Length > 100)
                return BadRequest(new { ok = false, message = "Uno o más datos de contacto no son válidos." });

            if (((tipoDoc?.Trim().ToUpperInvariant() ?? paciente.TipoDocumento) == "TI" ||
                 (fechaNacimiento ?? paciente.FechaNacimiento).Date > DateTime.Today.AddYears(-18)) &&
                (string.IsNullOrWhiteSpace(contactoEmergencia ?? paciente.ContactoEmergencia) ||
                 !System.Text.RegularExpressions.Regex.IsMatch(
                     ((telefonoEmergencia ?? paciente.TelefonoEmergencia) ?? "").Replace(" ", ""),
                     "^[0-9]{7,15}$")))
                return BadRequest(new { ok = false, message = "Los menores de edad y pacientes con documento TI requieren un acudiente y su teléfono." });

            var puedeEditarIdentificacion =
                User.IsInRole("Administrador") || User.IsInRole("Recepcionista");

            if (!puedeEditarIdentificacion &&
                (tipoDoc is not null || documento is not null || fechaNacimiento.HasValue ||
                 grupoSanguineo is not null || direccion is not null))
                return Forbid();

            if (puedeEditarIdentificacion)
            {
                if (tipoDoc is not null)
                {
                    var tipoNormalizado = tipoDoc.Trim().ToUpperInvariant();
                    if (tipoNormalizado is not ("CC" or "TI" or "CE" or "PAS" or "NIT"))
                        return BadRequest(new { ok = false, message = "El tipo de documento no es válido." });
                    paciente.TipoDocumento = tipoNormalizado;
                }

                if (documento is not null)
                {
                    var documentoNormalizado = documento.Trim().ToUpperInvariant();
                    var tipoValidar = tipoDoc?.Trim().ToUpperInvariant() ?? paciente.TipoDocumento;
                    if (documentoNormalizado.Length > 20 ||
                        (tipoValidar == "PAS"
                            ? !System.Text.RegularExpressions.Regex.IsMatch(documentoNormalizado, "^[A-Z0-9]{5,20}$")
                            : !System.Text.RegularExpressions.Regex.IsMatch(documentoNormalizado, "^[0-9]{7,15}$")))
                        return BadRequest(new { ok = false, message = "El documento no es válido para el tipo seleccionado." });

                    if (await _context.Pacientes.AnyAsync(p =>
                            p.IdPaciente != idPaciente && p.Documento == documentoNormalizado))
                        return Conflict(new { ok = false, message = "Ya existe un paciente con ese documento." });

                    paciente.Documento = documentoNormalizado;
                }

                if (fechaNacimiento.HasValue)
                {
                    if (fechaNacimiento.Value.Date < new DateTime(1900, 1, 1) ||
                        fechaNacimiento.Value.Date > DateTime.Today)
                        return BadRequest(new { ok = false, message = "La fecha de nacimiento no es válida." });
                    paciente.FechaNacimiento = fechaNacimiento.Value.Date;
                }

                if (grupoSanguineo is not null)
                {
                    var grupoNormalizado = string.IsNullOrWhiteSpace(grupoSanguineo)
                        ? null
                        : grupoSanguineo.Trim().ToUpperInvariant();
                    if (grupoNormalizado is not null &&
                        grupoNormalizado is not ("O+" or "O-" or "A+" or "A-" or "B+" or "B-" or "AB+" or "AB-"))
                        return BadRequest(new { ok = false, message = "El grupo sanguíneo no es válido." });
                    paciente.GrupoSanguineo = grupoNormalizado;
                }

                if (direccion is not null)
                {
                    if (direccion.Trim().Length > 255)
                        return BadRequest(new { ok = false, message = "La dirección supera la longitud permitida." });
                    paciente.Direccion = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(nombres))
            {
                paciente.Nombres =
                    nombres.Trim();
            }

            if (!string.IsNullOrWhiteSpace(apellidos))
            {
                paciente.Apellidos =
                    apellidos.Trim();
            }

            paciente.Telefono =
                string.IsNullOrWhiteSpace(telefono)
                    ? null
                    : telefono.Trim();

            paciente.Correo =
                string.IsNullOrWhiteSpace(correo)
                    ? null
                    : correo.Trim();

            paciente.Ciudad =
                string.IsNullOrWhiteSpace(ciudad)
                    ? null
                    : ciudad.Trim();

            if (!string.IsNullOrWhiteSpace(genero))
            {
                var generoNormalizado =
                    genero.Trim().ToUpperInvariant();

                if (generoNormalizado is not ("M" or "F" or "O"))
                {
                    return BadRequest(
                        new
                        {
                            ok = false,
                            message =
                                "El género enviado no es válido."
                        });
                }

                paciente.Genero =
                    generoNormalizado;
            }

            if (TieneSustanciaAlergiaNoValida(alergias))
                return BadRequest(new { ok = false, message = "Cada alergia debe tener como máximo 150 caracteres." });
            await PacienteAlergiaSync.SynchronizeAsync(_context, paciente, alergias);

            if (medicamentos is not null)
                paciente.Medicamentos = string.IsNullOrWhiteSpace(medicamentos)
                    ? null
                    : medicamentos.Trim();

            // Yeray (2025) - campos nuevos: antes solo actualizables vía API REST.
            // Se aplica la misma lógica null-safe que los demás campos:
            // si el parámetro llega como null el campo del paciente NO se toca
            // (comportamiento PATCH). Si llega vacío "", se pone null en BD.
            if (contactoEmergencia is not null)
                paciente.ContactoEmergencia = string.IsNullOrWhiteSpace(contactoEmergencia)
                    ? null
                    : contactoEmergencia.Trim();

            if (telefonoEmergencia is not null)
                paciente.TelefonoEmergencia = string.IsNullOrWhiteSpace(telefonoEmergencia)
                    ? null
                    : telefonoEmergencia.Trim();

            if (antecedentesMedicos is not null)
                paciente.AntecedentesMedicos = string.IsNullOrWhiteSpace(antecedentesMedicos)
                    ? null
                    : antecedentesMedicos.Trim();

            if (User.IsInRole("Administrador") || User.IsInRole("Recepcionista"))
            {
                if (!string.IsNullOrWhiteSpace(estado))
                {
                    var estadoNormalizado =
                        estado.Trim().ToLowerInvariant();

                    if (estadoNormalizado is not
                        ("activo" or "inactivo" or "retirado"))
                    {
                        return BadRequest(
                            new
                            {
                                ok = false,
                                message =
                                    "El estado enviado no es válido."
                            });
                    }

                    if (estadoNormalizado != "activo" &&
                        await _context.Citas.AnyAsync(c =>
                            c.IdPaciente == idPaciente &&
                            c.FechaHora > DateTime.Now &&
                            c.Estado != "Cancelada" &&
                            c.Estado != "cancelada"))
                        return Conflict(new
                        {
                            ok = false,
                            message = "No se puede desactivar al paciente mientras tenga citas futuras activas. Cancela o reprograma las citas primero."
                        });

                    paciente.Estado =
                        estadoNormalizado;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,

                    message =
                        "Paciente actualizado correctamente.",

                    idPaciente =
                        paciente.IdPaciente
                });
        }
        catch (DbUpdateException ex) when (EsConflictoDocumento(ex))
        {
            return Conflict(new { ok = false, message = "Ya existe un paciente con ese documento." });
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message =
                        "No fue posible actualizar el paciente."
                });
        }
    }

    private static bool EsConflictoDocumento(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private static bool TieneSustanciaAlergiaNoValida(string? alergias) =>
        (alergias ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(sustancia => sustancia.Length > 150);

    // =========================================================================
    // ARCHIVO ADJUNTO — Upload del documento/foto del paciente
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - SubirArchivoAdjunto
    //
    // PARA QUÉ SIRVE:
    //   El campo Paciente.ArchivoAdjunto existía en la entidad y en la BD desde
    //   el inicio del proyecto, pero nunca tuvo endpoint ni UI. Siempre fue null.
    //
    //   Este endpoint recibe el archivo (foto de perfil del paciente o imagen de
    //   su documento de identidad), lo valida, lo guarda en disco y actualiza
    //   Paciente.ArchivoAdjunto con la ruta relativa desde wwwroot.
    //
    // ALMACENAMIENTO:
    //   Ruta física: wwwroot/uploads/pacientes/{idPaciente}/{guid}{ext}
    //   Valor en BD: uploads/pacientes/{idPaciente}/{guid}{ext}
    //   La URL pública es: /{valor_en_bd}  (servido por Static Files).
    //
    // TIPOS PERMITIDOS: image/jpeg, image/png, image/webp — máximo 5 MB.
    //   No se permiten PDFs aquí (para documentos extensos usar Documento_Clinico).
    //
    // SEGURIDAD:
    //   - Nombre físico = GUID + extensión validada (anti path-traversal).
    //   - Si ya había un archivo anterior se borra del disco antes de guardar el nuevo.
    //   - Solo Administrador, Recepcionista y Profesional pueden subir archivos.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/{idPaciente:int}/archivo-adjunto")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirArchivoAdjunto(
        int idPaciente,
        IFormFile archivo,
        [FromServices] IWebHostEnvironment env)
    {
        // ── Validaciones básicas ────────────────────────────────────────────
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { ok = false, message = "No se recibió ningún archivo." });

        const long maxBytes = 5 * 1024 * 1024; // 5 MB
        if (archivo.Length > maxBytes)
            return BadRequest(new { ok = false, message = "El archivo supera el límite de 5 MB." });

        string[] tiposPermitidos = ["image/jpeg", "image/png", "image/webp"];
        string contentType = archivo.ContentType.ToLowerInvariant();
        if (!tiposPermitidos.Contains(contentType))
            return BadRequest(new { ok = false, message = "Solo se permiten imágenes JPG, PNG o WEBP." });

        // ── Buscar paciente ─────────────────────────────────────────────────
        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente);

        if (paciente is null)
            return NotFound(new { ok = false, message = "Paciente no encontrado." });

        // ── Determinar extensión segura ──────────────────────────────────────
        string extension = contentType switch
        {
            "image/png"  => ".png",
            "image/webp" => ".webp",
            _            => ".jpg"
        };

        // ── Crear carpeta y ruta del archivo nuevo ───────────────────────────
        string carpetaRelativa = Path.Combine("uploads", "pacientes", idPaciente.ToString());
        string carpetaFisica   = Path.Combine(env.WebRootPath, carpetaRelativa);
        Directory.CreateDirectory(carpetaFisica);

        string nombreFisico = $"{Guid.NewGuid()}{extension}";
        string rutaRelativa = Path.Combine(carpetaRelativa, nombreFisico).Replace('\\', '/');
        string rutaFisica   = Path.Combine(carpetaFisica, nombreFisico);

        // ── Guardar el nuevo archivo en disco ────────────────────────────────
        await using (var stream = new FileStream(rutaFisica, FileMode.Create, FileAccess.Write))
        {
            await archivo.CopyToAsync(stream);
        }

        // ── Borrar el archivo anterior si existía ────────────────────────────
        if (!string.IsNullOrWhiteSpace(paciente.ArchivoAdjunto))
        {
            string rutaAnterior = Path.Combine(
                env.WebRootPath,
                paciente.ArchivoAdjunto.Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(rutaAnterior))
                System.IO.File.Delete(rutaAnterior);
        }

        // ── Actualizar BD ────────────────────────────────────────────────────
        paciente.ArchivoAdjunto = rutaRelativa;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron guardar los metadatos del archivo del paciente {PacienteId}.", idPaciente);
            // Si falla la BD, eliminar el archivo ya guardado para no dejar basura
            if (System.IO.File.Exists(rutaFisica))
                System.IO.File.Delete(rutaFisica);

            return StatusCode(500, new
            {
                ok      = false,
                message = "No fue posible guardar el archivo adjunto."
            });
        }

        return Ok(new
        {
            ok           = true,
            message      = "Archivo adjunto guardado correctamente.",
            rutaRelativa,
            urlPublica   = $"/{rutaRelativa}"
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - EliminarArchivoAdjunto
    //
    // PARA QUÉ SIRVE: elimina el archivo del disco y pone ArchivoAdjunto = null
    // en la BD. Permite "quitar" la foto/documento del paciente sin borrarlo.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/{idPaciente:int}/archivo-adjunto/eliminar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarArchivoAdjunto(
        int idPaciente,
        [FromServices] IWebHostEnvironment env)
    {
        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.IdPaciente == idPaciente);

        if (paciente is null)
            return NotFound(new { ok = false, message = "Paciente no encontrado." });

        if (string.IsNullOrWhiteSpace(paciente.ArchivoAdjunto))
            return BadRequest(new { ok = false, message = "El paciente no tiene archivo adjunto." });

        // Eliminar del disco
        string rutaFisica = Path.Combine(
            env.WebRootPath,
            paciente.ArchivoAdjunto.Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(rutaFisica))
            System.IO.File.Delete(rutaFisica);

        paciente.ArchivoAdjunto = null;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo eliminar el archivo adjunto del paciente {PacienteId}.", idPaciente);
            return StatusCode(500, new
            {
                ok      = false,
                message = "No fue posible eliminar el archivo adjunto."
            });
        }

        return Ok(new { ok = true, message = "Archivo adjunto eliminado correctamente." });
    }

    // =========================================================================
    // DELETE LÓGICO - Desactivar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-pacientes/desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesactivarPaciente(
        [FromForm] int idPaciente)
    {
        try
        {
            var paciente =
                await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p =>
                            p.IdPaciente ==
                            idPaciente);

            if (paciente == null)
            {
                return NotFound(
                    new
                    {
                        ok = false,
                        message =
                            "Paciente no encontrado."
                    });
            }

            if (string.Equals(
                    paciente.Estado,
                    "inactivo",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    new
                    {
                        ok = false,
                        message =
                            "El paciente ya se encuentra inactivo."
                    });
            }

            if (await _context.Citas.AnyAsync(c =>
                        c.IdPaciente == idPaciente &&
                        c.FechaHora > DateTime.Now &&
                        c.Estado != "Cancelada" &&
                        c.Estado != "cancelada"))
                    return Conflict(new
                    {
                        ok = false,
                        message = "No se puede desactivar al paciente mientras tenga citas futuras activas. Cancela o reprograma las citas primero."
                    });

            paciente.Estado =
                    "inactivo";

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,

                    message =
                        "Paciente desactivado correctamente.",

                    idPaciente =
                        paciente.IdPaciente
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo desactivar el paciente {PacienteId}.", idPaciente);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message = "No fue posible desactivar el paciente."
                });
        }
    }
}