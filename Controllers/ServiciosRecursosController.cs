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
        var serviciosDb = await _context.Servicios.AsNoTracking()
            .Where(s => s.Estado == "activo").OrderBy(s => s.Nombre).ToListAsync(ct);
        return View("~/Views/Servicios_Y_Recursos/st-odo-05-servicios/index.cshtml", serviciosDb);
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
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiCrearServicio([FromBody] ServicioRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_Servicio_Crear @Nombre={request.Nombre.Trim()}, @Descripcion={descripcion}, @Precio={request.Precio}, @Categoria={request.Category}, @DuracionMinutos={request.Duration}, @IdEspecialidad={request.IdEspecialidad}",
                ct);

            // Se consulta por el nombre normalizado después de ejecutar el procedimiento.
            // Esto evita que EF Core intente materializar el SELECT del procedimiento,
            // cuyos alias pueden variar entre versiones del script SQL.
            var nombreNormalizado = request.Nombre.Trim();
            var creado = await _context.Servicios
                .AsNoTracking()
                .Include(s => s.Especialidad)
                .SingleAsync(s => s.Nombre == nombreNormalizado, ct);
            return Ok(new
            {
                success = true,
                message = "Servicio creado correctamente.",
                data = new
                {
                    id = creado.IdServicio,
                    nombre = creado.Nombre,
                    descripcion = creado.Descripcion,
                    precio = creado.Precio,
                    categoria = creado.Categoria,
                    duracion = creado.DuracionMinutos,
                    estado = creado.Estado,
                    idEspecialidad = creado.IdEspecialidad,
                    especialidad = creado.Especialidad?.Nombre
                }
            });
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            return BadRequest(new { success = false, message = ex.Message });
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

        try
        {
            var descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.sp_Servicio_Actualizar @IdServicio={id}, @Nombre={request.Nombre.Trim()}, @Descripcion={descripcion}, @Precio={request.Precio}, @Categoria={request.Category}, @DuracionMinutos={request.Duration}, @IdEspecialidad={request.IdEspecialidad}",
                ct);

            return Ok(new { success = true, message = "Servicio actualizado correctamente.", data = new { id } });
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            return BadRequest(new { success = false, message = ex.Message });
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
