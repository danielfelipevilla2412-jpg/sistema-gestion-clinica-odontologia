using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class ServiciosRecursosController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<ServiciosRecursosController> _logger;

    public ServiciosRecursosController(AppDbContext context, ILogger<ServiciosRecursosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/st-adm-10-servicios")]
    public async Task<IActionResult> Stadm10Servicios(CancellationToken ct)
    {
        // Consultar servicios incluyendo su especialidad.
        var serviciosDb = await _context.Servicios
            .AsNoTracking()
            .Include(s => s.Especialidad)
            .OrderBy(s => s.Nombre)
            .ToListAsync(ct);

        // Preparar los servicios para JavaScript.
        var servicios = serviciosDb.Select(s => new
        {
            id = s.IdServicio,
            name = s.Nombre,
            description = string.IsNullOrWhiteSpace(s.Descripcion)
                ? "Sin descripción registrada."
                : s.Descripcion,
            category = s.Categoria,
            duration = s.DuracionMinutos,
            cost = s.Precio,
            active = s.Estado == "activo",
            specialtyId = s.IdEspecialidad,
            specialtyName = s.Especialidad?.Nombre,
            icon = "🦷"
        }).ToList();

        // Consultar las especialidades disponibles.
        var especialidades = await _context.Especialidades
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new
            {
                id = e.IdEspecialidad,
                nombre = e.Nombre
            })
            .ToListAsync(ct);

        // Enviar ambos catálogos a Razor.
        ViewData["ServiciosJson"] =
            System.Text.Json.JsonSerializer.Serialize(servicios);

        ViewData["EspecialidadesJson"] =
            System.Text.Json.JsonSerializer.Serialize(especialidades);

        return View(
            "~/Views/Servicios_Y_Recursos/st-adm-10-servicios/catalogoservicios.cshtml",
            serviciosDb
        );
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/st-adm-16-configuracion-general")]
    public async Task<IActionResult> Stadm16ConfiguracionGeneral(CancellationToken ct)
    {
        var configsDb = await _context.ConfiguracionesGenerales.AsNoTracking().ToListAsync(ct);
        string ObtenerValor(string clave, string valorPorDefecto) =>
            configsDb.FirstOrDefault(c => c.Clave == clave)?.Valor ?? valorPorDefecto;

        var configAnidada = new
        {
            appointment = new
            {
                duration = int.TryParse(ObtenerValor("cita_duracion_default", "30"), out int dur) ? dur : 30,
                open = ObtenerValor("horario_apertura", "07:00"),
                close = ObtenerValor("horario_cierre", "18:00")
            },
            center = new
            {
                name = ObtenerValor("nombre_clinica", "SmileTrack Clínica Odontológica"),
                nit = ObtenerValor("nit_clinica", "901.482.350-4"),
                address = ObtenerValor("direccion_clinica", "Avenida de la Salud #45-12, Piso 4")
            },
            notifications = new
            {
                email = ObtenerValor("notif_email", "true") == "true",
                sms = ObtenerValor("notif_sms", "false") == "true",
                system = ObtenerValor("notif_sistema", "true") == "true"
            }
        };

        ViewData["ConfigsJson"] = System.Text.Json.JsonSerializer.Serialize(configAnidada);
        return View("~/Views/Servicios_Y_Recursos/st-adm-16-configuracion-general/index.cshtml", configsDb);
    }

    [HttpGet]
    [Authorize(Roles = "Profesional")]
    [Route("servicios-y-recursos/st-odo-05-servicios")]
    public async Task<IActionResult> Stodo05Servicios(CancellationToken ct)
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var usuarioId)) return Challenge();

        var profesional = await _context.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdUsuario == usuarioId, ct);
        if (profesional is null)
        {
            return View("~/Views/Servicios_Y_Recursos/st-odo-05-servicios/index.cshtml",
                new List<SmileTrack_MVC.Models.Entities.ProfesionalServicio>());
        }

        ViewData["NombreProfesional"] = $"{profesional.Nombres} {profesional.Apellidos}".Trim();
        var asignaciones = await _context.ProfesionalServicios.AsNoTracking()
            .Include(ps => ps.Servicio).ThenInclude(s => s!.Especialidad)
            .Where(ps => ps.IdProfesional == profesional.IdProfesional)
            .OrderBy(ps => ps.Servicio!.Nombre)
            .ToListAsync(ct);
        return View("~/Views/Servicios_Y_Recursos/st-odo-05-servicios/index.cshtml", asignaciones);
    }

    // Endpoints de consulta. Las vistas específicas de equipos e inventario aún no forman parte de este módulo.
    [HttpGet]
    [Authorize(Roles = "Administrador,Auxiliar")]
    [Route("servicios-y-recursos/api/equipos")]
    public async Task<IActionResult> ApiEquipos(CancellationToken ct)
    {
        var equiposDb = await _context.Equipos.AsNoTracking().ToListAsync(ct);
        return Json(equiposDb);
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("servicios-y-recursos/api/inventario")]
    public async Task<IActionResult> ApiInventario(CancellationToken ct)
    {
        var inventarioDb = await _context.Inventarios.AsNoTracking().ToListAsync(ct);
        return Json(inventarioDb);
    }

    // El CRUD del catálogo se ejecuta exclusivamente mediante procedimientos almacenados.
    // Servicio.cs y AppDbContext mapean las columnas reales de dbo.Servicio; por eso el
    // procedimiento de creación debe devolver las columnas con sus nombres SQL originales.
    // El catálogo se persiste mediante EF Core. El procedimiento sp_Servicio_Crear
    // no forma parte del script versionado del proyecto y puede conservar reglas antiguas
    // en una instalación SQL. La regla vigente es Precio > 0 COP.
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiCrearServicio([FromBody] ServicioRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var nombre = request.Nombre.Trim();
        var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        if (request.Precio <= 0)
            return BadRequest(new { success = false, message = "El precio debe ser mayor que $0 COP." });

        try
        {
            if (!await _context.Especialidades.AsNoTracking()
                    .AnyAsync(e => e.IdEspecialidad == request.IdEspecialidad, ct))
                return BadRequest(new { success = false, message = "La especialidad seleccionada no existe." });

            if (await _context.Servicios.AsNoTracking().AnyAsync(s => s.Nombre == nombre, ct))
                return Conflict(new { success = false, message = "Ya existe un servicio con ese nombre." });

            var servicio = new Servicio
            {
                Nombre = nombre,
                Descripcion = descripcion,
                Precio = request.Precio,
                Categoria = request.Category,
                DuracionMinutos = request.Duration,
                IdEspecialidad = request.IdEspecialidad,
                Estado = "activo"
            };
            _context.Servicios.Add(servicio);
            await _context.SaveChangesAsync(ct);
            await _context.Entry(servicio).Reference(s => s.Especialidad).LoadAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Servicio creado correctamente.",
                data = new
                {
                    id = servicio.IdServicio,
                    nombre = servicio.Nombre,
                    descripcion = servicio.Descripcion,
                    precio = servicio.Precio,
                    categoria = servicio.Categoria,
                    duracion = servicio.DuracionMinutos,
                    estado = servicio.Estado,
                    idEspecialidad = servicio.IdEspecialidad,
                    especialidad = servicio.Especialidad?.Nombre
                }
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new { success = false, message = "Ya existe un servicio con ese nombre o una restricción única impide crearlo." });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 547 || sql.Number == 50002))
        {
            _logger.LogWarning(ex, "Restricción SQL al crear servicio. Número={Numero}, Procedimiento={Procedimiento}", sql.Number, sql.Procedure);
            return BadRequest(new { success = false, message = "La base de datos rechazó los datos del servicio. Compruebe sus restricciones y especialidad." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando servicio.");
            return StatusCode(500, new { success = false, message = "No fue posible crear el servicio." });
        }
    }

    [HttpPut]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiActualizarServicio(int id, [FromBody] ServicioRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var nombre = request.Nombre.Trim();
        if (request.Precio <= 0)
            return BadRequest(new { success = false, message = "El precio debe ser mayor que $0 COP." });

        try
        {
            var servicio = await _context.Servicios.SingleOrDefaultAsync(s => s.IdServicio == id, ct);
            if (servicio is null)
                return NotFound(new { success = false, message = "El servicio no existe." });

            if (!await _context.Especialidades.AsNoTracking()
                    .AnyAsync(e => e.IdEspecialidad == request.IdEspecialidad, ct))
                return BadRequest(new { success = false, message = "La especialidad seleccionada no existe." });

            if (await _context.Servicios.AsNoTracking()
                    .AnyAsync(s => s.IdServicio != id && s.Nombre == nombre, ct))
                return Conflict(new { success = false, message = "Ya existe otro servicio con ese nombre." });

            servicio.Nombre = nombre;
            servicio.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
            servicio.Precio = request.Precio;
            servicio.Categoria = request.Category;
            servicio.DuracionMinutos = request.Duration;
            servicio.IdEspecialidad = request.IdEspecialidad;
            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Servicio actualizado correctamente.", data = new { id } });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new { success = false, message = "Ya existe otro servicio con ese nombre o una restricción única impide actualizarlo." });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 547 || sql.Number == 50002))
        {
            _logger.LogWarning(ex, "Restricción SQL al actualizar servicio {IdServicio}. Número={Numero}, Procedimiento={Procedimiento}", id, sql.Number, sql.Procedure);
            return BadRequest(new { success = false, message = "La base de datos rechazó la actualización. Compruebe las restricciones del servicio." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando el servicio {IdServicio}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible actualizar el servicio." });
        }
    }

    [HttpPut]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios/{id:int}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiCambiarEstadoServicio(int id, [FromBody] ServicioEstadoRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_Servicio_CambiarEstado @IdServicio={id}, @Estado={request.Estado}", ct);
            return Ok(new { success = true, message = $"Servicio marcado como {request.Estado}.", data = new { id, estado = request.Estado } });
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cambiando el estado del servicio {IdServicio}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible cambiar el estado del servicio." });
        }
    }

    // La eliminación es lógica: nunca se borra físicamente un servicio del catálogo,
    // porque puede estar relacionado con citas, facturas o profesionales.
    [HttpDelete]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiEliminarServicio(int id, CancellationToken ct = default)
    {
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_Servicio_CambiarEstado @IdServicio={id}, @Estado={"inactivo"}", ct);
            return Ok(new
            {
                success = true,
                message = "Servicio desactivado correctamente. Se conserva su historial.",
                data = new { id, estado = "inactivo" }
            });
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error desactivando el servicio {IdServicio}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible desactivar el servicio." });
        }
    }
}
