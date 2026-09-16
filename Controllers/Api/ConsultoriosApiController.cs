/**
 * ============================================
 * SmileTrack — API Controller: Consultorios
 * ============================================
 * PROPÓSITO:
 * Expone endpoints REST para operaciones sobre consultorios.
 * Actualmente soporta el registro del estado confirmado por el auxiliar
 * desde la vista st-aux-09-estado-consultorio.
 * ============================================
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/consultorios")]
[Authorize(Roles = "Auxiliar,Administrador")]
[Produces("application/json")]
public sealed class ConsultoriosApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConsultoriosApiController> _logger;

    public ConsultoriosApiController(
        AppDbContext context,
        ILogger<ConsultoriosApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/consultorios/{id}/confirmar-estado
    /// Registra el estado operativo del consultorio confirmado por el auxiliar.
    /// </summary>
    [HttpPost("{id:int}/confirmar-estado")]
    public async Task<IActionResult> ConfirmarEstado(
        int id,
        [FromBody] ConfirmarEstadoRequest request,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "ID de consultorio inválido." });

        var consultorio = await _context.Consultorios.FindAsync(new object[] { id }, ct);
        if (consultorio is null)
            return NotFound(new { success = false, message = "Consultorio no encontrado." });

        // Actualizar el estado del consultorio si se proporciona un valor válido
        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            var estadoNormalizado = request.Estado.Trim().ToLowerInvariant();
            // Mapear los valores del selector de la UI al dominio de la entidad
            consultorio.Estado = estadoNormalizado switch
            {
                "disponible"    => "disponible",
                "mantenimiento" => "mantenimiento",
                "no-disponible" => "no_disponible",
                _               => consultorio.Estado
            };
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Estado de consultorio {Id} confirmado: {Estado} — Observaciones: {Obs}",
            id,
            request.Estado,
            request.Observaciones);

        return Ok(new { success = true, message = "Estado registrado correctamente." });
    }
}

/// <summary>Request body para POST /api/consultorios/{id}/confirmar-estado.</summary>
public sealed class ConfirmarEstadoRequest
{
    public string? Estado { get; set; }
    public string? Observaciones { get; set; }
}
