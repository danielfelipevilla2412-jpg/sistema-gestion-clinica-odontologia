using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.Api.Pacientes;
using SmileTrack_MVC.Services;


namespace SmileTrack_MVC.Controllers.Api;

/// <summary>
/// API REST de Gestión de Pacientes (api/v1/pacientes).
/// Capa nueva: no modifica ni reemplaza las acciones de formulario del
/// GestionPacientesController (MVC) — ambos canales conviven sobre la misma tabla.
/// </summary>
[ApiController]
[Route("api/v1/pacientes")]
[Produces("application/json")]
public sealed class GestionPacientesApiController : ControllerBase
{
    private readonly IPacienteService _service;
    private readonly ILogger<GestionPacientesApiController> _logger;

    public GestionPacientesApiController(
        IPacienteService service,
        ILogger<GestionPacientesApiController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// GET api/v1/pacientes — lista pacientes con paginación, búsqueda por
    /// nombre/apellido/documento y filtro de estado. Por defecto solo trae
    /// "activo"; usar estado=todos para incluir inactivos y retirados.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.ObtenerAsync(search, estado, page, pageSize, ct);

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

    /// <summary>GET api/v1/pacientes/{id} — obtiene un paciente puntual con su última y próxima cita.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        var dto = await _service.ObtenerPorIdAsync(id, ct);
        if (dto is null)
            return NotFound(new { success = false, message = "Paciente no encontrado." });

        return Ok(new { success = true, data = dto });
    }

    /// <summary>
    /// POST api/v1/pacientes — crea un paciente vía JSON. El DTO ya valida con
    /// DataAnnotations; la regla de documento duplicado (BD) vive en el Service.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] PacienteApiCreateDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CrearAsync(dto, ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data?.IdPaciente },
            new { success = true, message = result.Message, data = result.Data });
    }

    /// <summary>
    /// PATCH api/v1/pacientes/{id} — actualización parcial (solo se tocan los
    /// campos que vengan distintos de null). No es PUT: PUT implicaría
    /// reemplazar el recurso completo.
    /// </summary>
    [HttpPatch("{id:int}")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] PacienteApiUpdateDto dto,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.ActualizarAsync(id, dto, ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }

    /// <summary>PATCH api/v1/pacientes/{id}/estado — cambia el estado (nunca se borra físicamente).</summary>
    [HttpPatch("{id:int}/estado")]
    [Authorize(Roles = "Administrador,Recepcionista", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(
        int id,
        [FromBody] PacienteApiEstadoDto dto,
        CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CambiarEstadoAsync(id, dto.Estado, ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message, data = result.Data });
    }
} 