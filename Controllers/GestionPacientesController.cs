using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class GestionPacientesController : Controller
{
    private readonly AppDbContext _context;

    public GestionPacientesController(AppDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> Stadm05GestionPacientes()
    {
        string[] colores = ["blue", "green", "yellow", "purple", "slate"];
        var ahora = DateTime.Now;
        const int pageSize = 20;

        // Totales para los stat-cards (COUNT en BD, no carga de filas)
        int totalActivos   = await _context.Pacientes.CountAsync(p => p.Estado == "activo");
        int conProximaCita = await _context.Citas.CountAsync(c =>
            c.FechaHora > ahora && c.Estado != "Cancelada");
        int sinAlergias    = await _context.Pacientes.CountAsync(p =>
            p.Estado == "activo" &&
            (p.Alergias == null || p.Alergias == ""));
        int conHistorial   = await _context.Citas
            .Select(c => c.IdPaciente).Distinct().CountAsync();

        // Primera página de pacientes activos
        var pacientesDb = await _context.Pacientes
            .Where(p => p.Estado == "activo")
            .OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres)
            .Take(pageSize)
            .ToListAsync();

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
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<SmileTrack_MVC.Models.Entities.Paciente> query =
            _context.Pacientes.AsNoTracking();

        // Filtro de estado: por defecto solo activos
        if (string.IsNullOrWhiteSpace(estado) ||
            string.Equals(estado, "activo", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.Estado == "activo");
        }
        else if (!string.Equals(estado, "todos", StringComparison.OrdinalIgnoreCase))
        {
            string est = estado.Trim().ToLowerInvariant();
            query = query.Where(p => p.Estado == est);
        }

        // Búsqueda: nombre, apellido o documento
        if (!string.IsNullOrWhiteSpace(search))
        {
            string txt = search.Trim();
            query = query.Where(p =>
                p.Nombres.Contains(txt)   ||
                p.Apellidos.Contains(txt) ||
                p.Documento.Contains(txt));
        }

        int total = await query.CountAsync();

        var pacientesDb = await query
            .OrderBy(p => p.Apellidos)
            .ThenBy(p => p.Nombres)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var idsPagina = pacientesDb.Select(p => p.IdPaciente).ToList();

        // Citas en una sola consulta para la página (evita N+1)
        var ahora = DateTime.Now;
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
                GrupoSanguineo = p.GrupoSanguineo,
                AlergiasTexto  = p.Alergias,
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

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-pacientes/st-aux-03-preparacion-consulta")]
    public async Task<IActionResult> Staux03PreparacionConsulta(
        [FromQuery] int? citaId)
    {
        // ─────────────────────────────────────────────────────────────────────
        // Yeray (2025) - Implementación completa del módulo de preparación.
        //
        // ANTES: devolvía View() sin ningún ViewModel. La vista tenía datos
        //        hardcodeados ("Pedro García", "Penicilina", etc.).
        //
        // AHORA:
        //   1. Carga la cita indicada por ?citaId= o, si no viene, la próxima
        //      del día (la más cercana en el futuro con estado programada/confirmada).
        //   2. Construye el ViewModel con datos reales del paciente, profesional,
        //      servicio, consultorio y última nota clínica de la HC.
        //   3. Incluye la lista de citas del día para el selector de citas.
        // ─────────────────────────────────────────────────────────────────────

        var hoy   = DateTime.Today;
        var manana = hoy.AddDays(1);
        var ahora = DateTime.Now;

        // Citas del día con todos los includes necesarios
        var citasHoy = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora >= hoy && c.FechaHora < manana &&
                        c.Estado != "cancelada" && c.Estado != "Cancelada")
            .OrderBy(c => c.FechaHora)
            .ToListAsync();

        // Seleccionar la cita a mostrar
        Cita? citaSeleccionada = null;
        if (citaId.HasValue)
        {
            citaSeleccionada = citasHoy.FirstOrDefault(c => c.IdCita == citaId.Value)
                            ?? await _context.Citas
                                .Include(c => c.Paciente)
                                .Include(c => c.Profesional)
                                .Include(c => c.Servicio)
                                .Include(c => c.Consultorio)
                                .FirstOrDefaultAsync(c => c.IdCita == citaId.Value);
        }
        else
        {
            // La próxima cita del día que aún no ha pasado, o la más reciente del día
            citaSeleccionada = citasHoy.FirstOrDefault(c => c.FechaHora >= ahora)
                            ?? citasHoy.LastOrDefault();
        }

        // ── Construir ViewModel ──────────────────────────────────────────────
        var vm = new PreparacionConsultaViewModel
        {
            CitasDelDia = citasHoy.Select(c => new CitaDelDiaDto
            {
                Id       = c.IdCita,
                Paciente = c.Paciente?.NombresCompleto ?? "Paciente",
                Hora     = c.FechaHora.ToString("HH:mm"),
                Servicio = c.Servicio?.Nombre ?? "Sin servicio",
                Estado   = c.Estado,
                EsActual = c.IdCita == (citaSeleccionada?.IdCita ?? 0)
            }).ToList()
        };

        if (citaSeleccionada is null)
            return View("~/Views/Gestion_De_Pacientes/st-aux-03-preparacion-consulta/preparacion.cshtml", vm);

        vm.CitaId        = citaSeleccionada.IdCita;
        vm.CitaFechaHora = citaSeleccionada.FechaHora;
        vm.CitaEstado    = citaSeleccionada.Estado;
        vm.CitaNotas     = citaSeleccionada.Notas;
        vm.MinutosHastaCita = (int)(citaSeleccionada.FechaHora - ahora).TotalMinutes;

        // Datos del paciente
        if (citaSeleccionada.Paciente is not null)
        {
            var pac = citaSeleccionada.Paciente;
            vm.PacienteId       = pac.IdPaciente;
            vm.PacienteNombre   = pac.NombresCompleto;
            vm.PacienteDocumento = pac.Documento;
            vm.GrupoSanguineo   = pac.GrupoSanguineo;
            vm.Alergias         = pac.Alergias;
            vm.AntecedentesMedicos = pac.AntecedentesMedicos;
            vm.AlergiasLista    = string.IsNullOrWhiteSpace(pac.Alergias)
                ? []
                : pac.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                          StringSplitOptions.TrimEntries).ToList();
        }

        // Profesional
        vm.ProfesionalNombre = citaSeleccionada.Profesional is not null
            ? $"Dr(a). {citaSeleccionada.Profesional.Nombres} {citaSeleccionada.Profesional.Apellidos}"
            : "Sin asignar";

        // Servicio y consultorio
        vm.ServicioNombre    = citaSeleccionada.Servicio?.Nombre    ?? "Sin especificar";
        vm.ConsultorioNombre = citaSeleccionada.Consultorio?.Nombre ?? "Sin consultorio";

        // Historia clínica y última nota
        if (vm.PacienteId.HasValue)
        {
            var historia = await _context.HistoriasClinicas
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.IdPaciente == vm.PacienteId.Value && h.Activa);

            if (historia is not null)
            {
                vm.CodigoHC = $"HC-{historia.IdHistoria:D6}";

                var ultimaNota = await _context.NotasClinicas
                    .AsNoTracking()
                    .Include(n => n.Profesional)
                    .Where(n => n.IdHistoria == historia.IdHistoria)
                    .OrderByDescending(n => n.Fecha)
                    .FirstOrDefaultAsync();

                if (ultimaNota is not null)
                {
                    vm.UltimaNotaClinica    = ultimaNota.Procedimiento ?? ultimaNota.Diagnostico;
                    vm.FechaUltimaNota      = ultimaNota.Fecha;
                    vm.ProfesionalUltimaNota = ultimaNota.Profesional is not null
                        ? $"Dr(a). {ultimaNota.Profesional.Nombres} {ultimaNota.Profesional.Apellidos}"
                        : null;
                }
            }
        }

        return View("~/Views/Gestion_De_Pacientes/st-aux-03-preparacion-consulta/preparacion.cshtml", vm);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - Endpoint JSON: citas del día para el selector de citas
    //
    // El JS lo llama cuando el auxiliar cambia de cita en el selector sin
    // recargar la página. Devuelve solo las citas del día activas.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-pacientes/st-aux-03-preparacion-consulta/citas-del-dia")]
    public async Task<IActionResult> Staux03CitasDelDia()
    {
        var hoy    = DateTime.Today;
        var manana = hoy.AddDays(1);

        var citas = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Include(c => c.Profesional)
            .Where(c => c.FechaHora >= hoy && c.FechaHora < manana &&
                        c.Estado != "cancelada" && c.Estado != "Cancelada")
            .OrderBy(c => c.FechaHora)
            .Select(c => new
            {
                id           = c.IdCita,
                paciente     = c.Paciente != null ? c.Paciente.NombresCompleto : "Paciente",
                hora         = c.FechaHora.ToString("HH:mm"),
                servicio     = c.Servicio != null ? c.Servicio.Nombre : "Sin servicio",
                consultorio  = c.Consultorio != null ? c.Consultorio.Nombre : "",
                profesional  = c.Profesional != null
                               ? $"Dr(a). {c.Profesional.Nombres} {c.Profesional.Apellidos}"
                               : "Sin asignar",
                estado       = c.Estado
            })
            .ToListAsync();

        return Json(citas);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Yeray (2025) - Endpoint POST: guardar checklist de preparación completado
    //
    // ANTES: el JS solo imprimía en consola "Preparación confirmada" y
    //        deshabilitaba el botón 3 segundos. No persistía nada.
    //
    // AHORA: al confirmar, se registra una nota clínica en la HC del paciente
    //        con tipo "Preparación de consulta", el texto del checklist marcado
    //        y las observaciones del auxiliar. Esto deja trazabilidad de que el
    //        auxiliar preparó el consultorio para esa cita.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-pacientes/st-aux-03-preparacion-consulta/confirmar")]
    public async Task<IActionResult> ConfirmarPreparacion(
        [FromBody] ConfirmarPreparacionRequest request)
    {
        if (request is null || request.CitaId is null)
            return BadRequest(new { success = false, message = "No se recibió la cita." });

        var cita = await _context.Citas
            .Include(c => c.Paciente)
            .FirstOrDefaultAsync(c => c.IdCita == request.CitaId);

        if (cita is null)
            return NotFound(new { success = false, message = "Cita no encontrada." });

        // Obtener o crear la HC del paciente
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == cita.IdPaciente && h.Activa);

        if (historia is null)
        {
            historia = new SmileTrack_MVC.Models.Entities.HistoriaClinica
            {
                IdPaciente          = cita.IdPaciente,
                FechaApertura       = DateTime.UtcNow,
                Activa              = true,
                ObservacionesGenerales = string.Empty
            };
            _context.HistoriasClinicas.Add(historia);
            await _context.SaveChangesAsync();
        }

        // Construir el texto del checklist para la nota
        var itemsCompletados = request.ChecklistItems?
            .Where(i => i.Checked)
            .Select(i => $"✓ {i.Text}")
            .ToList() ?? [];

        var itemsPendientes = request.ChecklistItems?
            .Where(i => !i.Checked)
            .Select(i => $"✗ {i.Text}")
            .ToList() ?? [];

        string resumenChecklist = string.Join(", ", itemsCompletados);
        if (itemsPendientes.Count > 0)
            resumenChecklist += $" | Pendientes: {string.Join(", ", itemsPendientes)}";

        string procedimientoTexto =
            $"Preparación de consulta completada. Checklist: {resumenChecklist}";

        string diagnosticoTexto =
            string.IsNullOrWhiteSpace(request.Observaciones)
                ? "Consultorio listo para el paciente."
                : request.Observaciones.Trim();

        // Registrar nota clínica con idProfesional null (es un auxiliar, no profesional)
        var nota = new SmileTrack_MVC.Models.Entities.NotaClinica
        {
            IdHistoria    = historia.IdHistoria,
            IdProfesional = null,
            Fecha         = DateTime.UtcNow,
            Diagnostico   = diagnosticoTexto,
            Procedimiento = procedimientoTexto,
            ProximaCita   = null,
            Estado        = "Preparación"
        };
        _context.NotasClinicas.Add(nota);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "No fue posible registrar la preparación.",
                detail  = ex.Message
            });
        }

        return Ok(new
        {
            success = true,
            message = "Preparación confirmada y registrada en la historia clínica.",
            idNota  = nota.IdNota
        });
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-pacientes/st-rec-02-registrar-paciente")]
    public IActionResult Strec02RegistrarPaciente() =>
        View(
            "~/Views/Gestion_De_Pacientes/st-rec-02-registrar-paciente/nuevo_paciente.cshtml");

    // =========================================================================
    // CREATE - Registrar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPaciente(
        [FromForm] string tipoDoc,
        [FromForm] string documento,
        [FromForm] string nombres,
        [FromForm] string apellidos,
        [FromForm] DateTime fechaNacimiento,
        [FromForm] string? genero,
        [FromForm] string? telefono,
        [FromForm] string? correo,
        [FromForm] string? grupoSanguineo,
        [FromForm] string? ciudad,
        [FromForm] string? alergias)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(documento))
            {
                return BadRequest(
                    new
                    {
                        ok = false,
                        message =
                            "El documento es obligatorio."
                    });
            }

            string documentoNormalizado =
                documento.Trim();

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
                        tipoDoc.Trim(),

                    Documento =
                        documentoNormalizado,

                    Nombres =
                        nombres.Trim(),

                    Apellidos =
                        apellidos.Trim(),

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
                        string.IsNullOrWhiteSpace(grupoSanguineo)
                            ? null
                            : grupoSanguineo.Trim(),

                    Ciudad =
                        string.IsNullOrWhiteSpace(ciudad)
                            ? null
                            : ciudad.Trim(),

                    Alergias =
                        string.IsNullOrWhiteSpace(alergias)
                            ? null
                            : alergias.Trim(),

                    Estado =
                        "activo",

                    FechaRegistro =
                        DateTime.UtcNow.Date
                };

            _context.Pacientes.Add(
                paciente);

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,
                    idPaciente =
                        paciente.IdPaciente,

                    message =
                        "Paciente registrado correctamente."
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,
                    message =
                        "No fue posible registrar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
    }

    // =========================================================================
    // UPDATE - Actualizar paciente
    // =========================================================================

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
        [FromForm] string? estado,
        [FromForm] string? genero)
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

            paciente.Alergias =
                string.IsNullOrWhiteSpace(alergias)
                    ? null
                    : alergias.Trim();

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

                paciente.Estado =
                    estadoNormalizado;
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
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message =
                        "No fue posible actualizar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
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
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message =
                        "No fue posible desactivar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
    }
}