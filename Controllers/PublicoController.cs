using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmileTrack_MVC.Controllers;

public class PublicoController : Controller
{
    [Route("/")]
    [Route("homepage")]
    public IActionResult Homepage() => View("~/Views/Publico/homepage.cshtml");

    [Route("terminos")]
    public IActionResult Terminos() => View("~/Views/Publico/terminos.cshtml");

    [Route("privacidad")]
    public IActionResult Privacidad() => View("~/Views/Publico/privacidad.cshtml");

    [Route("shared")]
    public IActionResult Shared() => View("~/Views/Shared/index.cshtml");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("dev/generar-hash")]
    public IActionResult GenerarHash(string? pwd)
    {
        if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(pwd))
            return BadRequest("Debe proporcionar la contraseña que desea convertir en hash.");

        string hash = BCrypt.Net.BCrypt.HashPassword(pwd);
        return Content($"Contraseña: {pwd}\nHash: {hash}");
    }
}
