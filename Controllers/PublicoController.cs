using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class PublicoController : Controller
{
    private readonly AppDbContext _context;

    public PublicoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("/")]
    [Route("homepage")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Homepage(CancellationToken ct)
    {
        var services = await _context.Servicios
            .AsNoTracking()
            .Where(service => service.Estado.ToLower() == "activo")
            .OrderBy(service => service.Nombre)
            .Select(service => new PublicServiceViewModel
            {
                Name = service.Nombre,
                Description = service.Descripcion,
                Category = service.Categoria,
                Price = service.Precio,
                DurationMinutes = service.DuracionMinutos
            })
            .ToListAsync(ct);

        return View(
            "~/Views/Publico/homepage.cshtml",
            new PublicHomeViewModel { Services = services });
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("terminos")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // 24 horas
    public IActionResult Terminos() => View("~/Views/Publico/terminos.cshtml");

    [HttpGet]
    [AllowAnonymous]
    [Route("privacidad")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // 24 horas
    public IActionResult Privacidad() => View("~/Views/Publico/privacidad.cshtml");

    [HttpGet]
    [Route("shared")]
    [Authorize(Roles = "Administrador")]
    public IActionResult Shared() => View("~/Views/Shared/index.cshtml");

    // ── Política de tratamiento de datos (Ley 1581/2012) ──────────────────────
    [HttpGet]
    [AllowAnonymous]
    [Route("politica-tratamiento")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult PoliticaTratamiento() =>
        View("~/Views/Publico/politica-tratamiento.cshtml");
}
