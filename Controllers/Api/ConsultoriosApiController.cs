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
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Models.Entities;
using System.Security.Claims;
using System.Text.Json;

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

    [HttpGet("{id:int}/estado-operativo")]
    public async Task<IActionResult> ObtenerEstadoOperativo(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "ID de consultorio inválido." });

        var consultorio = await _context.Consultorios.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdConsultorio == id, ct);
        if (consultorio is null)
            return NotFound(new { success = false, message = "Consultorio no encontrado." });

        var operativo = await _context.EstadosOperativosConsultorio.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdConsultorio == id, ct);
        var historial = await _context.ConsultoriosHistorial.AsNoTracking()
            .Where(h => h.IdConsultorio == id)
            .OrderByDescending(h => h.FechaCambio)
            .Take(20)
            .Select(h => new ConsultorioHistorialApiDto { Estado = h.Estado, Motivo = h.Motivo, FechaCambio = h.FechaCambio })
            .ToListAsync(ct);

        return Ok(new { success = true, data = new ConsultorioEstadoOperativoApiDto
        {
            IdConsultorio = id,
            Estado = consultorio.Estado,
            Checklist = operativo is null ? [] : JsonSerializer.Deserialize<object[]>(operativo.ChecklistJson) ?? [],
            Observaciones = operativo?.Observaciones,
            ActualizadoEn = operativo?.ActualizadoEn,
            Historial = historial
        }});
    }

    [HttpPut("{id:int}/estado-operativo")]
    [CookieAwareValidateAntiforgeryToken]
    public async Task<IActionResult> GuardarEstadoOperativo(
        int id,
        [FromBody] ConsultorioEstadoOperativoApiRequest request,
        CancellationToken ct = default)
    {
        if (id <= 0 || request is null || !ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de estado operativo inválidos." });

        var consultorio = await _context.Consultorios.FirstOrDefaultAsync(c => c.IdConsultorio == id, ct);
        if (consultorio is null)
            return NotFound(new { success = false, message = "Consultorio no encontrado." });

        string estado = request.Estado.Trim().ToLowerInvariant() switch
        {
            "disponible" => "disponible",
            "mantenimiento" => "mantenimiento",
            "no-disponible" or "no_disponible" => "no_disponible",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(estado))
            return BadRequest(new { success = false, message = "El estado del consultorio no es válido." });

        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedUserId) ? parsedUserId : null;
        var operativo = await _context.EstadosOperativosConsultorio
            .FirstOrDefaultAsync(e => e.IdConsultorio == id, ct);
        if (operativo is null)
        {
            operativo = new ConsultorioEstadoOperativo { IdConsultorio = id };
            _context.EstadosOperativosConsultorio.Add(operativo);
        }

        consultorio.Estado = estado;
        operativo.ChecklistJson = JsonSerializer.Serialize(request.Checklist ?? []);
        operativo.Observaciones = request.Observaciones?.Trim();
        operativo.ActualizadoPor = userId;
        operativo.ActualizadoEn = DateTime.UtcNow;
        _context.ConsultoriosHistorial.Add(new ConsultorioHistorial
        {
            IdConsultorio = id,
            Estado = estado,
            IdUsuario = userId,
            Motivo = operativo.Observaciones,
            FechaCambio = operativo.ActualizadoEn
        });
        await _context.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Estado operativo guardado correctamente." });
    }
}

/// <summary>Request body para POST /api/consultorios/{id}/confirmar-estado.</summary>
public sealed class ConfirmarEstadoRequest
{
    public string? Estado { get; set; }
    public string? Observaciones { get; set; }
}
