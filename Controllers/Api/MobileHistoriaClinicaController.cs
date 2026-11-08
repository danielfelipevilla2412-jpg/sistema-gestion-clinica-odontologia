using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/historia-clinica")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileHistoriaClinicaController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditoriaHistoriaClinicaService _auditoria;

    public MobileHistoriaClinicaController(
        AppDbContext context,
        AuditoriaHistoriaClinicaService auditoria)
    {
        _context = context;
        _auditoria = auditoria;
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

        if (historias.Count > 0)
        {
            foreach (var historia in historias)
            {
                _auditoria.Agregar(
                    HttpContext,
                    "SELECT",
                    "Historia_Clinica",
                    historia.id,
                    "Consulta de historia clínica desde la API móvil.");
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(historias);
    }
}