using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Controllers;

public class PqrController : Controller
{
    private readonly AppDbContext _context;

    public PqrController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Paciente,Administrador,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/st-pac-04-nueva-pqr")]
    public async Task<IActionResult> Stpac04NuevaPqr()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == userId);
        var misPqrs = paciente != null
            ? await _context.PQRs.Where(p => p.IdPaciente == paciente.IdPaciente).OrderByDescending(p => p.FechaCreacion).ToListAsync()
            : new List<PqrEntity>();

        ViewData["MisPqrsJson"] = System.Text.Json.JsonSerializer.Serialize(misPqrs);
        return View("~/Views/Gestion_De_PQR/st-pac-04-nueva-pqr/index.cshtml");
    }

    [HttpPost]
    [Authorize(Roles = "Paciente,Administrador,Profesional,Recepcionista,Auxiliar")]
    [Route("gestion-de-pqr/crear")]
    public async Task<IActionResult> CrearPqr([FromForm] string tipo, [FromForm] string asunto, [FromForm] string descripcion)
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdUsuario == userId)
            ?? await _context.Pacientes.FirstOrDefaultAsync();

        if (paciente == null)
        {
            return BadRequest(new { success = false, message = "No se encontró un registro de paciente asociado." });
        }

        var nuevaPqr = new PqrEntity
        {
            IdPaciente = paciente.IdPaciente,
            IdUsuario = userId > 0 ? userId : null,
            Tipo = tipo ?? "peticion",
            Asunto = asunto ?? "Sin Asunto",
            Descripcion = descripcion ?? "",
            Estado = "recibida",
            Prioridad = "media",
            FechaCreacion = DateTime.Now
        };

        _context.PQRs.Add(nuevaPqr);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "PQR registrada exitosamente.", id = nuevaPqr.IdPqr });
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("gestion-de-pqr/st-adm-17-gestion-pqr")]
    public async Task<IActionResult> Stadm17GestionPqr()
    {
        var pqrsDb = await _context.PQRs
            .Include(p => p.Paciente)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync();


        var pqrs = pqrsDb.Select(p => new
        {
            id = p.IdPqr,
            ticket = $"PQR-{p.IdPqr:D4}",
            patient = p.Paciente != null ? $"{p.Paciente.Nombres} {p.Paciente.Apellidos}" : "Anonimo",
            type = p.Tipo,
            subject = p.Asunto,
            description = p.Descripcion,
            status = p.Estado,
            priority = p.Prioridad,
            date = p.FechaCreacion.ToString("yyyy-MM-dd HH:mm")
        }).ToList();

        ViewData["PqrsJson"] = System.Text.Json.JsonSerializer.Serialize(pqrs);
        return View("~/Views/Gestion_De_PQR/st-adm-17-gestion-pqr/index.cshtml", pqrs);
    }
}
