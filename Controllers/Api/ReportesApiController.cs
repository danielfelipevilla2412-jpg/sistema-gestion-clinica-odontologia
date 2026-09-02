using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/v1/reportes")]
[Authorize(Roles = "Administrador", Policy = "ApiOrCookie")]
[Produces("application/json")]
public sealed class ReportesApiController(IReportesApiService service) : ControllerBase
{
    private readonly IReportesApiService _service = service;

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct = default)
    {
        try { return Ok(new { success = true, data = await _service.ObtenerResumenAsync(desde, hasta, ct) }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpGet("bitacora")]
    public async Task<IActionResult> GetBitacora([FromQuery] string? usuario, [FromQuery] string? accion, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.ObtenerBitacoraAsync(usuario, accion, desde, hasta, page, pageSize, ct);
        return result.Success ? Ok(new { success = true, data = result.Items, pagination = new { result.Page, result.PageSize, result.TotalCount, result.TotalPages } }) : StatusCode(500, new { success = false, message = result.Message });
    }
}
