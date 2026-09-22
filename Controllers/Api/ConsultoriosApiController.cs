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
    /// GET /api/consultorios
    /// Obtiene la lista de consultorios activos disponibles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerConsultorios(CancellationToken ct = default)
    {
        var consultorios = await _context.Consultorios
            .AsNoTracking()
            .OrderBy(c => c.IdConsultorio)
            .Select(c => new
            {
                id = c.IdConsultorio,
                nombre = c.Nombre ?? $"Consultorio {c.IdConsultorio}",
                ubicacion = c.Ubicacion ?? string.Empty,
                estado = c.Estado ?? "disponible"
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = consultorios });
    }

    /// <summary>
    /// POST /api/consultorios/guardar-preferencia
    /// Guarda la preferencia del auxiliar sobre el consultorio seleccionado.
    /// </summary>
    [HttpPost("guardar-preferencia")]
    [CookieAwareValidateAntiforgeryToken]
    public async Task<IActionResult> GuardarPreferencia(
        [FromBody] GuardarPreferenciaRequest request,
        CancellationToken ct = default)
    {
        if (request?.ConsultorioId is null || request.ConsultorioId <= 0)
            return BadRequest(new { success = false, message = "ID de consultorio inválido." });

        // Verificar que el consultorio existe
        var existe = await _context.Consultorios
            .AnyAsync(c => c.IdConsultorio == request.ConsultorioId.Value, ct);

        if (!existe)
            return NotFound(new { success = false, message = "Consultorio no encontrado." });

        // Obtener usuario actual
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedUserId) 
            ? parsedUserId 
            : null;

        if (userId is null)
            return Unauthorized(new { success = false, message = "Usuario no autenticado." });

        // Buscar o crear preferencia del usuario
        var preferencia = await _context.UsuariosPreferenciasConsultorio
            .FirstOrDefaultAsync(p => p.IdUsuario == userId.Value, ct);

        if (preferencia is null)
        {
            preferencia = new UsuarioPreferenciaConsultorio
            {
                IdUsuario = userId.Value,
                IdConsultorio = request.ConsultorioId.Value,
                ActualizadoEn = DateTime.UtcNow
            };
            _context.UsuariosPreferenciasConsultorio.Add(preferencia);
        }
        else
        {
            preferencia.IdConsultorio = request.ConsultorioId.Value;
            preferencia.ActualizadoEn = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Usuario {UserId} guardó preferencia de consultorio: {ConsultorioId}",
            userId,
            request.ConsultorioId);

        return Ok(new { success = true, message = "Preferencia guardada correctamente." });
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

/// <summary>Request body para POST /api/consultorios/guardar-preferencia.</summary>
public sealed class GuardarPreferenciaRequest
{
    public int? ConsultorioId { get; set; }
}
