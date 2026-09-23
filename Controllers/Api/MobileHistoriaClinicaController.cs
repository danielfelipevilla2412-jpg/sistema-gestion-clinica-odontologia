using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/historia-clinica")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileHistoriaClinicaController : ControllerBase
{
    private readonly AppDbContext _context;

    public MobileHistoriaClinicaController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerHistorial(
        CancellationToken cancellationToken)
    {
        string? idUsuarioTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idUsuarioTexto, out int idUsuario))
        {
            return Unauthorized();
        }

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.IdUsuario == idUsuario,
                cancellationToken);

        if (paciente is null)
        {
            return NotFound(new
            {
                message = "No existe un paciente asociado a esta cuenta."
            });
        }

        var historias = await _context.HistoriasClinicas
            .AsNoTracking()
            .Where(h => h.IdPaciente == paciente.IdPaciente)
            .OrderByDescending(h => h.FechaApertura)
            .Select(h => new
            {
                id = h.IdHistoria,
                fechaApertura = h.FechaApertura,
                observaciones = h.ObservacionesGenerales
                    ?? "Sin observaciones registradas.",
                activa = h.Activa
            })
            .ToListAsync(cancellationToken);

        return Ok(historias);
    }
}