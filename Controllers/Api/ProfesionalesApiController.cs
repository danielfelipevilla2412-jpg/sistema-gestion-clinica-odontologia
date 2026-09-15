using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Services;
using System.Security.Claims;

namespace SmileTrack_MVC.Controllers.Api;

/// <summary>
/// DTO para recibir un bloque de horario semanal desde el frontend (perfil.js).
/// Los campos Day/DayFull son informativos; DiaSemana es el valor normalizado para BD.
/// </summary>
public sealed class HorarioSemanalDto
{
    /// <summary>Abreviatura del día (p. ej. "Lun"). Informativo.</summary>
    public string? Day { get; set; }

    /// <summary>Nombre completo del día en español (p. ej. "Lunes"). Se usa para BD.</summary>
    public string? DiaSemana { get; set; }

    /// <summary>Nombre completo del día (alias alternativo enviado por perfil.js).</summary>
    public string? DayFull { get; set; }

    /// <summary>Si el día es laboral.</summary>
    public bool Active { get; set; }

    /// <summary>Hora de inicio en formato "HH:mm".</summary>
    public string? Start { get; set; }

    /// <summary>Hora de fin en formato "HH:mm".</summary>
    public string? End { get; set; }
}


/// <summary>
/// API REST de Gestión de Profesionales.
/// Todos los endpoints requieren rol Administrador y autenticación por cookie o JWT.
/// </summary>
[ApiController]
[Route("api/profesionales")]
[Authorize(Roles = "Administrador,Recepcionista,Profesional", Policy = "ApiOrCookie")]
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
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
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
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
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
    [Authorize(Roles = "Administrador")]
    [CookieAwareValidateAntiforgeryToken]
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
    [Authorize(Roles = "Administrador")]
    [CookieAwareValidateAntiforgeryToken]
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
    [Authorize(Roles = "Administrador")]
    [CookieAwareValidateAntiforgeryToken]
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
    [Authorize(Roles = "Administrador")]
    [CookieAwareValidateAntiforgeryToken]
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
    [Authorize(Roles = "Administrador,Recepcionista,Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHorarios(
        int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        bool esAdministrador =
            User.IsInRole("Administrador") || User.IsInRole("Recepcionista");
        var result = await _service.ObtenerHorariosAsync(
            id,
            GetCurrentUserId(),
            esAdministrador,
            ct);

        return result.Success
            ? Ok(new { success = true, data = result.Data })
            : result.ErrorStatusCode == 403
                ? Forbid()
                : NotFound(new { success = false, message = result.Message });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/profesionales/{id}/horarios
    // Actualiza el horario semanal del profesional (PRO-01 bugfix)
    // Rol Profesional: sólo puede actualizar su propio horario
    // Rol Administrador: puede actualizar cualquier profesional
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPut("{id:int}/horarios")]
    [Authorize(Roles = "Administrador,Profesional", Policy = "ApiOrCookie")]
    [CookieAwareValidateAntiforgeryToken]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateHorarios(
        int id,
        [FromBody] List<HorarioSemanalDto> horarios,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (horarios is null)
            return BadRequest(new { success = false, message = "El cuerpo de la solicitud es requerido." });

        var currentUserId = GetCurrentUserId();
        var result = await _service.ActualizarHorariosAsync(
            id,
            horarios.Select(h => new HorarioSemanalApiRequest
            {
                Day = h.Day,
                DiaSemana = h.DiaSemana,
                DayFull = h.DayFull,
                Active = h.Active,
                Start = h.Start,
                End = h.End
            }).ToList(),
            currentUserId,
            User.IsInRole("Administrador"),
            ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500,
                new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}/ausencias
    // Lista las ausencias registradas para el profesional (P-01 / U-06)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/ausencias")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAusencias(
        int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var result = await _service.ObtenerAusenciasAsync(id, ct);
        return result.Success
            ? Ok(new { success = true, data = result.Data })
            : NotFound(new { success = false, message = result.Message });
    }

    [HttpPost("{id:int}/ausencias")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [CookieAwareValidateAntiforgeryToken]
    public async Task<IActionResult> CreateAusencia(
        int id,
        [FromBody] AusenciaProfesionalApiRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de ausencia inválidos.", errors = ModelState });

        var result = await _service.CrearAusenciaAsync(id, request, GetCurrentUserId(), ct);
        return result.Success
            ? StatusCode(StatusCodes.Status201Created, new { success = true, message = result.Message, data = result.Data })
            : StatusCode(result.ErrorStatusCode ?? 400, new { success = false, message = result.Message });
    }

    [HttpPut("{id:int}/ausencias/{idAusencia:int}")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [CookieAwareValidateAntiforgeryToken]
    public async Task<IActionResult> UpdateAusencia(
        int id,
        int idAusencia,
        [FromBody] AusenciaProfesionalApiRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Datos de ausencia inválidos.", errors = ModelState });

        var result = await _service.ActualizarAusenciaAsync(id, idAusencia, request, GetCurrentUserId(), ct);
        return result.Success
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : StatusCode(result.ErrorStatusCode ?? 400, new { success = false, message = result.Message });
    }

    [HttpDelete("{id:int}/ausencias/{idAusencia:int}")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [CookieAwareValidateAntiforgeryToken]
    public async Task<IActionResult> DeleteAusencia(
        int id,
        int idAusencia,
        CancellationToken ct = default)
    {
        var result = await _service.EliminarAusenciaAsync(id, idAusencia, GetCurrentUserId(), ct);
        return result.Success
            ? Ok(new { success = true, message = result.Message })
            : StatusCode(result.ErrorStatusCode ?? 400, new { success = false, message = result.Message });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/profesionales/{id}/servicios
    // Lista los servicios que puede ejecutar el profesional (P-01 / U-06)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{id:int}/servicios")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServicios(
        int id,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var result = await _service.ObtenerServiciosAsync(id, ct);
        return result.Success
            ? Ok(new { success = true, data = result.Data })
            : NotFound(new { success = false, message = result.Message });
    }
}
