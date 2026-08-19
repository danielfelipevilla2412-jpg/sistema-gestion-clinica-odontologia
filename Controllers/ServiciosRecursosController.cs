using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Controllers;

public class ServiciosRecursosController : Controller
{
    private readonly AppDbContext _context;

    public ServiciosRecursosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("servicios-y-recursos/st-adm-10-servicios")]
    public async Task<IActionResult> Stadm10Servicios()
    {
        var serviciosDb = await _context.Servicios.OrderBy(s => s.Nombre).ToListAsync();

        // La tabla Servicio del script SQL no maneja categoría/duración/ícono; se completan
        // con valores por defecto para no romper el diseño de tarjetas del catálogo.
        var servicios = serviciosDb.Select(s => new
        {
            id = s.IdServicio,
            name = s.Nombre,
            description = string.IsNullOrWhiteSpace(s.Descripcion) ? "Sin descripción registrada." : s.Descripcion,
            category = "general",
            duration = 30,
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
}
