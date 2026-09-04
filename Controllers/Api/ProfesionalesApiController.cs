using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Services;
using System.Security.Claims;

namespace SmileTrack_MVC.Controllers.Api;

/// <summary>
/// API REST de Gestión de Profesionales.
/// Todos los endpoints requieren rol Administrador y autenticación por cookie o JWT.
/// </summary>
[ApiController]
[Route("api/profesionales")]
[Authorize(Roles = "Administrador")]
[CookieAwareValidateAntiforgeryToken]
[Produces("application/json")]
public sealed class ProfesionalesApiController : ControllerBase
{
    private readonly IProfesionalService _service;
    private readonly ILogger<ProfesionalesApiController> _logger;

    public ProfesionalesApiController(
        IProfesionalService service,
        ILogger<ProfesionalesApiController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out int id) && id > 0 ? id : null;
    }

    private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales
    // Listado paginado con filtros: page, pageSize, search, especialidad, estado
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? especialidad = null,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var result = await _service.ObtenerAsync(page, pageSize, search, especialidad, estado, ct);

        if (!result.Success)
            return StatusCode(500, new { success = false, message = result.Message });

        return Ok(new
        {
            success = true,
            data = result.Items,
            pagination = new
            {
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/especialidades
    // IMPORTANTE: debe ir ANTES de {id:int} para que el router no lo confunda
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("especialidades")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEspecialidades(CancellationToken ct = default)
    {
        var lista = await _service.ObtenerEspecialidadesAsync(ct);
        return Ok(new { success = true, data = lista });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var dto = await _service.ObtenerPorIdAsync(id, ct);

        if (dto is null)
            return NotFound(new { success = false, message = "Profesional no encontrado." });

        return Ok(new { success = true, data = dto });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/profesionales
    // Crea un nuevo profesional con su usuario de acceso
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] ProfesionalApiRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            string? firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage;
            return UnprocessableEntity(new
            {
                success = false,
                message = firstError ?? "Datos inválidos en la solicitud."
            });
        }

        var result = await _service.CrearAsync(
            request, GetCurrentUserId(), GetClientIp(), ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 422,
                new { success = false, message = result.Message });

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data?.IdProfesional },
            new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/profesionales/{id}
    // Actualiza un profesional existente
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ProfesionalApiRequest request,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
        {
            string? firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .FirstOrDefault()?.ErrorMessage;
            return UnprocessableEntity(new
            {
                success = false,
                message = firstError ?? "Datos inválidos en la solicitud."
            });
        }

        var result = await _service.ActualizarAsync(
            id, request, GetCurrentUserId(), GetClientIp(), ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                409 => Conflict(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 422,
                    new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH /api/profesionales/{id}/estado
    // Cambia únicamente el estado del profesional
    // Body: { "estado": "vacaciones" }
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CambiarEstado(
        int id,
        [FromBody] ProfesionalEstadoApiRequest body,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (string.IsNullOrWhiteSpace(body?.Estado))
            return UnprocessableEntity(new
            {
                success = false,
                message = "El campo 'estado' es requerido."
            });

        var result = await _service.CambiarEstadoAsync(
            id, body.Estado, GetCurrentUserId(), GetClientIp(), ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                409 => Conflict(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 422,
                    new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE /api/profesionales/{id}
    // Baja lógica: Profesional.Estado = "inactivo"
    // No borra físicamente ningún registro
    // ─────────────────────────────────────────────────────────────────────────

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var result = await _service.DesactivarAsync(
            id, GetCurrentUserId(), GetClientIp(), ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                409 => Conflict(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 500,
                    new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}/horarios
    // Lista los bloques de horario semanal del profesional (P-01 / U-06)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/horarios")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHorarios(
        int id,
        [FromServices] SmileTrack_MVC.Data.AppDbContext context,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        bool existe = await context.Profesionales
            .AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);

        if (!existe)
            return NotFound(new { success = false, message = "Profesional no encontrado." });

        var horarios = await context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.IdProfesional == id && h.Activo)
            .OrderBy(h => h.DiaSemana)
            .ThenBy(h => h.HoraInicio)
            .Select(h => new
            {
                h.IdHorario,
                h.DiaSemana,
                HoraInicio = h.HoraInicio.ToString("HH:mm"),
                HoraFin    = h.HoraFin.ToString("HH:mm"),
                h.Activo
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = horarios });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}/ausencias
    // Lista las ausencias registradas para el profesional (P-01 / U-06)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/ausencias")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAusencias(
        int id,
        [FromServices] SmileTrack_MVC.Data.AppDbContext context,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        bool existe = await context.Profesionales
            .AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);

        if (!existe)
            return NotFound(new { success = false, message = "Profesional no encontrado." });

        var ausencias = await context.AusenciasProfesional
            .AsNoTracking()
            .Where(a => a.IdProfesional == id)
            .OrderByDescending(a => a.FechaInicio)
            .Select(a => new
            {
                a.IdAusencia,
                a.Tipo,
                FechaInicio = a.FechaInicio.ToString("yyyy-MM-dd"),
                FechaFin    = a.FechaFin.ToString("yyyy-MM-dd"),
                a.Duracion,
                a.Observaciones
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = ausencias });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}/servicios
    // Lista los servicios que puede ejecutar el profesional (P-01 / U-06)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/servicios")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServicios(
        int id,
        [FromServices] SmileTrack_MVC.Data.AppDbContext context,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        bool existe = await context.Profesionales
            .AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);

        if (!existe)
            return NotFound(new { success = false, message = "Profesional no encontrado." });

        var servicios = await context.ProfesionalServicios
            .AsNoTracking()
            .Where(ps => ps.IdProfesional == id && ps.Activo)
            .Include(ps => ps.Servicio)
            .Select(ps => new
            {
                ps.IdProfesional,
                ps.IdServicio,
                NombreServicio       = ps.Servicio != null ? ps.Servicio.Nombre : string.Empty,
                PrecioBase           = ps.Servicio != null ? ps.Servicio.Precio : 0m,
                ps.PrecioPersonalizado,
                PrecioEfectivo       = ps.PrecioPersonalizado ?? (ps.Servicio != null ? ps.Servicio.Precio : 0m),
                ps.Activo
            })
            .OrderBy(ps => ps.NombreServicio)
            .ToListAsync(ct);

        return Ok(new { success = true, data = servicios });
    }
}
