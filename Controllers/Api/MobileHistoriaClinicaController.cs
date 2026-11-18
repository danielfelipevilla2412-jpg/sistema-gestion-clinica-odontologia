using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/historia-clinica")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileHistoriaClinicaController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditoriaHistoriaClinicaService _auditoria;
    private readonly IPerfilPacienteService _perfilPacienteService;

    public MobileHistoriaClinicaController(
        AppDbContext context,
        AuditoriaHistoriaClinicaService auditoria,
        IPerfilPacienteService perfilPacienteService)
    {
        _context = context;
        _auditoria = auditoria;
        _perfilPacienteService = perfilPacienteService;
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

        if (!await _perfilPacienteService.PerfilCompletoAsync(idUsuario, cancellationToken))
        {
            return Conflict(new
            {
                code = "PROFILE_INCOMPLETE",
                message = "Confirma tus datos personales antes de consultar tu historia clínica.",
                profileUrl = "/perfiles/st-pac-perfil-paciente"
            });
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