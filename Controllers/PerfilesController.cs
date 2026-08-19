using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Controllers;

public class PerfilesController : Controller
{
    private readonly AppDbContext _context;

    public PerfilesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("perfiles/st-aux-11-perfil-auxiliar")]
    public async Task<IActionResult> Staux11PerfilAuxiliar()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == userId);
        return View("~/Views/Perfiles/st-aux-11-perfil-auxiliar/mi-perfil.cshtml", usuario);
    }

    [HttpGet]
    [Authorize(Roles = "Paciente")]
    [Route("perfiles/st-pac-perfil-paciente")]
    public async Task<IActionResult> StpacPerfilPaciente()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var paciente = await _context.Pacientes.Include(p => p.Usuario).FirstOrDefaultAsync(p => p.IdUsuario == userId);
        return View("~/Views/Perfiles/st-pac-perfil-Paciente/perfil-paciente.cshtml", paciente);
    }

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("perfiles/st-rec-06-perfil-recepcionista")]
    public async Task<IActionResult> Strec06PerfilRecepcionista()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == userId);
        return View("~/Views/Perfiles/st-rec-06-perfil-recepcionista/perfilrecepcionista.cshtml", usuario);
    }
}
