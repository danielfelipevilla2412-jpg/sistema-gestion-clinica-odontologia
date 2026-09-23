using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmileTrack_MVC.Services.Perfiles;

namespace SmileTrack_MVC.Api.Controllers;

[ApiController]
[Route("api/perfil-paciente")]
[Authorize(Policy = "ApiOrCookie")]
[EnableRateLimiting("PerfilPaciente")]
public sealed class PerfilPacienteApiController : ControllerBase
{
    private readonly IPerfilPacienteService _service;

    public PerfilPacienteApiController(IPerfilPacienteService service)
    {
        _service = service;
    }

    [HttpGet("info-basica")]
    public async Task<IActionResult> ObtenerInfoBasica(CancellationToken ct = default)
    {
        var idPaciente = ObtenerIdPaciente();
        if (idPaciente is null) return Forbid();
        var info = await _service.ObtenerInfoBasicaAsync(idPaciente.Value, ct);
        return info is null ? NotFound(new { success = false, message = "Perfil no encontrado." }) : Ok(new { success = true, data = info });
    }

    [HttpGet("info-medica")]
    public async Task<IActionResult> ObtenerInfoMedica(CancellationToken ct = default)
    {
        var idPaciente = ObtenerIdPaciente();
        if (idPaciente is null) return Forbid();
        var info = await _service.ObtenerInfoMedicaAsync(idPaciente.Value, ct);
        return info is null ? NotFound(new { success = false, message = "Perfil no encontrado." }) : Ok(new { success = true, data = info });
    }

    [HttpGet("estadisticas")]
    public async Task<IActionResult> ObtenerEstadisticas(CancellationToken ct = default)
    {
        var idPaciente = ObtenerIdPaciente();
        if (idPaciente is null) return Forbid();
        return Ok(new { success = true, data = await _service.ObtenerEstadisticasAsync(idPaciente.Value, ct) });
    }

    [HttpGet("resumen-completo")]
    public async Task<IActionResult> ObtenerResumenCompleto(CancellationToken ct = default)
    {
        var idPaciente = ObtenerIdPaciente();
        if (idPaciente is null) return Forbid();
        var resumen = await _service.ObtenerResumenCompletoAsync(idPaciente.Value, ct);
        return resumen is null ? NotFound(new { success = false, message = "Perfil no encontrado." }) : Ok(new { success = true, data = resumen });
    }

    private int? ObtenerIdPaciente() => int.TryParse(User.FindFirstValue("IdPaciente"), out var id) && id > 0 ? id : null;
}