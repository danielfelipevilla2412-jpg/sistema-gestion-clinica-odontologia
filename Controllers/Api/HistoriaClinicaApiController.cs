using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.Api.HistoriasClinicas;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

/// <summary>
/// API REST de Historia Clínica (api/v1/historias-clinicas).
/// Construida sobre las tablas reales (Registro_Odontograma, Nota_Clinica,
/// Control_Postoperatorio) en vez de leer el JSON de ObservacionesGenerales.
/// Reutiliza HistoriaClinicaService para no duplicar reglas de negocio frente
/// a las vistas MVC clásicas (HistoriaClinicaController).
/// </summary>
[ApiController]
[Route("api/v1/historias-clinicas")]
[Produces("application/json")]
public sealed class HistoriaClinicaApiController : ControllerBase
{
    private readonly IHistoriaClinicaService _service;
    private readonly ILogger<HistoriaClinicaApiController> _logger;

    public HistoriaClinicaApiController(
        IHistoriaClinicaService service,
        ILogger<HistoriaClinicaApiController> logger)
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

    private string GetCurrentUserName() =>
        User.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } nombre ? nombre : "Profesional";

    /// <summary>
    /// Verifica que el paciente autenticado con rol "Paciente" solo pueda leer
    /// su propia historia clínica (ownership por claims).
    /// </summary>
    private async Task<bool> PuedeAccederAsync(int pacienteId, CancellationToken ct)
    {
        if (!User.IsInRole("Paciente")) return true;

        int? idUsuario = GetCurrentUserId();
        if (idUsuario is null) return false;

        return await _service.EsPropioPacienteAsync(pacienteId, idUsuario.Value, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/historias-clinicas/{pacienteId}
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{pacienteId:int}")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar,Paciente", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByPaciente(int pacienteId, CancellationToken ct = default)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver esta historia clínica." });

        var dto = await _service.ObtenerHistoriaApiAsync(pacienteId, ct);
        if (dto is null)
            return NotFound(new { success = false, message = "El paciente no tiene una historia clínica activa." });

        return Ok(new { success = true, data = dto });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/v1/historias-clinicas
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "Administrador,Profesional,Auxiliar", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] HistoriaClinicaApiCreateDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CrearHistoriaApiAsync(dto.PacienteId, ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        return CreatedAtAction(
            nameof(GetByPaciente),
            new { pacienteId = dto.PacienteId },
            new { success = true, message = result.Message, data = result.Historia });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/historias-clinicas/{pacienteId}/notas
    // POST /api/v1/historias-clinicas/{pacienteId}/notas
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{pacienteId:int}/notas")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar,Paciente", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotas(int pacienteId, CancellationToken ct = default)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver estas notas clínicas." });

        var notas = await _service.ObtenerNotasApiAsync(pacienteId, ct);
        return Ok(new { success = true, data = notas });
    }

    [HttpPost("{pacienteId:int}/notas")]
    [Authorize(Roles = "Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateNota(int pacienteId, [FromBody] NotaClinicaApiCreateDto dto, CancellationToken ct = default)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CrearNotaApiAsync(pacienteId, dto, GetCurrentUserId(), GetCurrentUserName(), ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        return CreatedAtAction(
            nameof(GetNotas),
            new { pacienteId },
            new { success = true, message = result.Message, data = result.Nota });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/v1/historias-clinicas/{pacienteId}/odontograma
    // PUT  /api/v1/historias-clinicas/{pacienteId}/odontograma
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("{pacienteId:int}/odontograma")]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional,Auxiliar,Paciente", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOdontograma(int pacienteId, CancellationToken ct = default)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver este odontograma." });

        var odontograma = await _service.ObtenerOdontogramaApiAsync(pacienteId, ct);
        return Ok(new { success = true, data = odontograma });
    }

    [HttpPut("{pacienteId:int}/odontograma")]
    [Authorize(Roles = "Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveOdontograma(int pacienteId, [FromBody] OdontogramaGuardarRequest request, CancellationToken ct = default)
    {
        if (pacienteId <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (request is null)
            return BadRequest(new { success = false, message = "No se recibieron datos del odontograma." });

        var result = await _service.GuardarOdontogramaApiAsync(pacienteId, request, GetCurrentUserId(), ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        return Ok(new { success = true, message = result.Message, data = result.Historia });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH /api/v1/historias-clinicas/{id}/estado
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPatch("{id:int}/estado")]
    [Authorize(Roles = "Administrador,Profesional", Policy = "ApiOrCookie")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] HistoriaClinicaApiEstadoDto dto, CancellationToken ct = default)
    {
        if (id <= 0)
            return BadRequest(new { success = false, message = "Identificador inválido." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CambiarEstadoApiAsync(id, dto.Activa, ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message })
            };
        }

        return Ok(new { success = true, message = result.Message, data = result.Historia });
    }
}