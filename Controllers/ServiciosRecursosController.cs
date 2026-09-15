using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Stadm10Servicios()
    {
        var serviciosDb = await _context.Servicios.OrderBy(s => s.Nombre).ToListAsync();

        // El ícono es puramente decorativo y no se persiste; categoría, duración,
        // precio y estado ya vienen de la tabla Servicio en SQL Server.
        var servicios = serviciosDb.Select(s => new
        {
            id = s.IdServicio,
            name = s.Nombre,
            description = string.IsNullOrWhiteSpace(s.Descripcion) ? "Sin descripción registrada." : s.Descripcion,
            category = s.Categoria,
            duration = s.DuracionMinutos,
            cost = s.Precio,
            active = s.Estado == "activo",
            icon = "🦷"
        }).ToList();

        ViewData["ServiciosJson"] = System.Text.Json.JsonSerializer.Serialize(servicios);
        return View("~/Views/Servicios_Y_Recursos/st-adm-10-servicios/catalogoservicios.cshtml", serviciosDb);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/st-adm-16-configuracion-general")]
    public async Task<IActionResult> Stadm16ConfiguracionGeneral()
    {
        var configsDb = await _context.ConfiguracionesGenerales.ToListAsync();

        // El script.js de la vista trabaja con un objeto anidado (appointment/center/notifications)
        // en lugar de la lista plana clave-valor que maneja la BD; se traduce aquí.
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
    public async Task<IActionResult> Stodo05Servicios()
    {
        var serviciosDb = await _context.Servicios.Where(s => s.Estado == "activo").OrderBy(s => s.Nombre).ToListAsync();
        return View("~/Views/Servicios_Y_Recursos/st-odo-05-servicios/index.cshtml", serviciosDb);
    }

    // NOTA: Aún no existe una vista dedicada de Equipos ni de Inventario en el proyecto
    // (no hay .cshtml ni enlace en el sidebar para estas pantallas). Mientras esas vistas
    // no se construyan, se exponen como endpoints JSON de solo lectura para no apuntar
    // por error a la vista de "Catálogo de Servicios" con datos que no le corresponden.
    [HttpGet]
    [Authorize(Roles = "Administrador,Auxiliar")]
    [Route("servicios-y-recursos/api/equipos")]
    public async Task<IActionResult> ApiEquipos()
    {
        var equiposDb = await _context.Equipos.ToListAsync();
        return Json(equiposDb);
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("servicios-y-recursos/api/inventario")]
    public async Task<IActionResult> ApiInventario()
    {
        var inventarioDb = await _context.Inventarios.ToListAsync();
        return Json(inventarioDb);
    }

    // =========================================================================
    // API REST - CRUD real del catálogo de servicios (reemplaza la simulación
    // en localStorage que existía en catalogoservicios.js)
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiCrearServicio([FromBody] ServicioRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            bool nombreDuplicado = await _context.Servicios
                .AnyAsync(s => s.Nombre.ToLower() == request.Nombre.Trim().ToLower(), ct);

            if (nombreDuplicado)
            {
                return BadRequest(new { success = false, message = "Ya existe un servicio con ese nombre." });
            }

            var servicio = new Servicio
            {
                Nombre = request.Nombre.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
                Precio = request.Precio,
                Categoria = request.Category,
                DuracionMinutos = request.Duration,
                Estado = "activo"
            };

            _context.Servicios.Add(servicio);
            await _context.SaveChangesAsync(ct);

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
                    estado = servicio.Estado
                }
            });
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.IdServicio == id, ct);
            if (servicio == null)
            {
                return NotFound(new { success = false, message = "Servicio no encontrado." });
            }

            bool nombreDuplicado = await _context.Servicios
                .AnyAsync(s => s.IdServicio != id && s.Nombre.ToLower() == request.Nombre.Trim().ToLower(), ct);

            if (nombreDuplicado)
            {
                return BadRequest(new { success = false, message = "Ya existe otro servicio con ese nombre." });
            }

            servicio.Nombre = request.Nombre.Trim();
            servicio.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
            servicio.Precio = request.Precio;
            servicio.Categoria = request.Category;
            servicio.DuracionMinutos = request.Duration;

            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Servicio actualizado correctamente.", data = new { id = servicio.IdServicio } });
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.IdServicio == id, ct);
            if (servicio == null)
            {
                return NotFound(new { success = false, message = "Servicio no encontrado." });
            }

            servicio.Estado = request.Estado;
            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = $"Servicio marcado como {request.Estado}.", data = new { id, estado = servicio.Estado } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cambiando el estado del servicio {IdServicio}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible cambiar el estado del servicio." });
        }
    }

    [HttpDelete]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/api/servicios/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiEliminarServicio(int id, CancellationToken ct = default)
    {
        try
        {
            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.IdServicio == id, ct);
            if (servicio == null)
            {
                return NotFound(new { success = false, message = "Servicio no encontrado." });
            }

            bool tieneReferencias = await _context.Citas.AnyAsync(c => c.IdServicio == id, ct);

            if (tieneReferencias)
            {
                // No se puede eliminar físicamente por las FK (hay citas que referencian
                // este servicio); se desactiva en su lugar para no romper el historial.
                servicio.Estado = "inactivo";
                await _context.SaveChangesAsync(ct);
                return Ok(new
                {
                    success = true,
                    message = "El servicio tiene citas asociadas; se desactivó en lugar de eliminarlo.",
                    data = new { id, estado = "inactivo" }
                });
            }

            _context.Servicios.Remove(servicio);
            await _context.SaveChangesAsync(ct);

            return Ok(new { success = true, message = "Servicio eliminado correctamente.", data = new { id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando el servicio {IdServicio}.", id);
            return StatusCode(500, new { success = false, message = "No fue posible eliminar el servicio." });
        }
    }
}
