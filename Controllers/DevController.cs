using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmileTrack_MVC.Controllers;

[Authorize(Roles = "Administrador")]
[Route("dev")]
public sealed class DevController : Controller
{
    [HttpGet("styleguide")]
    public IActionResult Styleguide()
    {
        return View("~/Views/Dev/Styleguide.cshtml");
    }
}
