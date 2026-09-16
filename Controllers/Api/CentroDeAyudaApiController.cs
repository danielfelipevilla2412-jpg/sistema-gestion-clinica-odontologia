using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Services.CentroDeAyuda;
using System.Security.Claims;

namespace SmileTrack_MVC.Controllers.Api;

/// <summary>
/// API REST del Centro de Ayuda. Gestiona exclusivamente los tickets de soporte.
/// </summary>
[ApiController]
[Route("api/centro-de-ayuda")]
[Authorize(Roles = "Administrador")]
[AutoValidateAntiforgeryToken]
[Produces("application/json")]
public sealed class CentroDeAyudaApiController : ControllerBase
{
    private readonly ICentroDeAyudaService _service;

    public CentroDeAyudaApiController(ICentroDeAyudaService service)
    {
        _service = service;
    }

    // CRUD de Centro de Ayuda (API): listado, detalle, creación, actualización, cambio de estado y eliminación de tickets.

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }

    /// <summary>GET /api/centro-de-ayuda/tickets</summary>
    [HttpGet("tickets")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? estado = null,
        [FromQuery] string? categoria = null,
        CancellationToken ct = default)
    {
        var (items, total) = await _service.ObtenerTicketsAsync(
            page, pageSize, search, estado, categoria, ct);

        var safePage = Math.Max(page, 1);
        var safeSize = Math.Clamp(pageSize, 1, 100);

        return Ok(new
        {
            success = true,
            data = items,
            pagination = new
            {
                page = safePage,
                pageSize = safeSize,
                totalCount = total,
                totalPages = (int)Math.Ceiling(total / (double)safeSize)
            }
        });
    }

    /// <summary>GET /api/centro-de-ayuda/tickets/{id}</summary>
    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var ticket = await _service.ObtenerTicketAsync(id, ct);
        return ticket is null
            ? NotFound(new { success = false, message = "Ticket no encontrado." })
            : Ok(new { success = true, data = ticket });
    }

    /// <summary>POST /api/centro-de-ayuda/tickets</summary>
    [HttpPost("tickets")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CentroAyudaTicketRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(ValidationError());

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        try
        {
            var (data, error) = await _service.CrearTicketAsync(request, userId.Value, ct);
            if (error is not null)
                return UnprocessableEntity(new { success = false, message = error });

            return CreatedAtAction(
                nameof(GetById),
                new { id = data!.IdTicket },
                new { success = true, message = "Ticket creado correctamente.", data });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
    }

    /// <summary>PUT /api/centro-de-ayuda/tickets/{id}</summary>
    [HttpPut("tickets/{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] CentroAyudaTicketUpdateRequest request,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return UnprocessableEntity(ValidationError());

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        try
        {
            var (data, error, status) = await _service.ActualizarTicketAsync(
                id, request, userId.Value, ct);

            return status switch
            {
                404 => NotFound(new { success = false, message = error }),
                422 => UnprocessableEntity(new { success = false, message = error }),
                _ => Ok(new { success = true, message = "Ticket actualizado correctamente.", data })
            };
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
    }

    /// <summary>PATCH /api/centro-de-ayuda/tickets/{id}/estado</summary>
    [HttpPatch("tickets/{id:int}/estado")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] CentroAyudaEstadoRequest request,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return UnprocessableEntity(ValidationError());

        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "No fue posible identificar al usuario." });

        var (data, error, status) = await _service.CambiarEstadoAsync(
            id, request.Estado, userId.Value, ct);

        return status switch
        {
            404 => NotFound(new { success = false, message = error }),
            422 => UnprocessableEntity(new { success = false, message = error }),
            _ => Ok(new { success = true, message = "Estado actualizado correctamente.", data })
        };
    }

    /// <summary>DELETE /api/centro-de-ayuda/tickets/{id}</summary>
    [HttpDelete("tickets/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var result = await _service.EliminarTicketAsync(id, ct);

        if (!result.Success)
            return NotFound(new { success = false, message = result.Error });

        return NoContent();
    }

    private object ValidationError()
    {
        var message = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
            ?? "Datos inválidos en la solicitud.";

        return new { success = false, message };
    }
}
