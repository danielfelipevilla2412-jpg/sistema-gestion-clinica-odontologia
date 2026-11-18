using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.HistoriasClinicas;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;

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
    private readonly AppDbContext _context;
    private readonly AuditoriaHistoriaClinicaService _auditoria;
    private readonly ILogger<HistoriaClinicaApiController> _logger;
    private readonly IPerfilPacienteService _perfilPacienteService;

    public HistoriaClinicaApiController(
        IHistoriaClinicaService service,
        AppDbContext context,
        AuditoriaHistoriaClinicaService auditoria,
        ILogger<HistoriaClinicaApiController> logger,
        IPerfilPacienteService perfilPacienteService)
    {
        _service = service;
        _context = context;
        _auditoria = auditoria;
        _logger = logger;
        _perfilPacienteService = perfilPacienteService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private int? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out int id) && id > 0 ? id : null;
    }

    private string GetCurrentUserName() =>
        User.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } nombre ? nombre : "Profesional";

    private async Task<IActionResult?> RespuestaSiPerfilIncompletoAsync(CancellationToken ct)
    {
        if (!User.IsInRole("Paciente"))
            return null;

        int? idUsuario = GetCurrentUserId();
        if (idUsuario.HasValue && await _perfilPacienteService.PerfilCompletoAsync(idUsuario.Value, ct))
            return null;

        return Conflict(new
        {
            success = false,
            code = "PROFILE_INCOMPLETE",
            message = "Confirma tus datos personales antes de consultar tu historia clínica.",
            profileUrl = "/perfiles/st-pac-perfil-paciente"
        });
    }

    /// <summary>
    /// Verifica que el paciente autenticado con rol "Paciente" solo pueda leer
    /// su propia historia clínica (ownership por claims).
    /// </summary>
    private async Task<bool> PuedeAccederAsync(int pacienteId, CancellationToken ct)
    {
        int? idUsuario = GetCurrentUserId();
        if (idUsuario is null) return false;

        if (User.IsInRole("Profesional"))
            return await _service.ProfesionalPuedeAccederPacienteAsync(pacienteId, idUsuario.Value, ct);

        if (User.IsInRole("Paciente"))
            return await _perfilPacienteService.PerfilCompletoAsync(idUsuario.Value, ct) &&
                await _service.EsPropioPacienteAsync(pacienteId, idUsuario.Value, ct);

        return true;
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

        if (await RespuestaSiPerfilIncompletoAsync(ct) is { } perfilIncompleto)
            return perfilIncompleto;

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver esta historia clínica." });

        var dto = await _service.ObtenerHistoriaApiAsync(pacienteId, ct);
        if (dto is null)
            return NotFound(new { success = false, message = "El paciente no tiene una historia clínica activa." });

        await _auditoria.RegistrarAsync(
            HttpContext,
            "SELECT",
            "Historia_Clinica",
            dto.IdHistoria,
            "Consulta de historia clínica desde API v1.");

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
        if (User.IsInRole("Profesional") && !await PuedeAccederAsync(dto.PacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para acceder a la historia de este paciente." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        bool historiaExistente = await _context.HistoriasClinicas
            .AnyAsync(h => h.IdPaciente == dto.PacienteId, ct);
        var result = await _service.CrearHistoriaApiAsync(dto.PacienteId, ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        if (result.Historia is not null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                historiaExistente ? "UPDATE" : "INSERT",
                "Historia_Clinica",
                result.Historia.IdHistoria,
                "Historia clínica creada o reactivada desde API v1.",
                ct);
        }

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

        if (await RespuestaSiPerfilIncompletoAsync(ct) is { } perfilIncompleto)
            return perfilIncompleto;

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver estas notas clínicas." });

        var notas = await _service.ObtenerNotasApiAsync(pacienteId, ct);
        int historiaId = await _context.HistoriasClinicas
            .Where(h => h.IdPaciente == pacienteId && h.Activa)
            .Select(h => h.IdHistoria)
            .FirstOrDefaultAsync(ct);
        if (historiaId > 0)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                historiaId,
                "Consulta de notas clínicas desde API v1.",
                ct);
        }

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

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para registrar notas en la historia de este paciente." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.CrearNotaApiAsync(pacienteId, dto, GetCurrentUserId(), GetCurrentUserName(), ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        if (result.Nota is not null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "INSERT",
                "Nota_Clinica",
                result.Nota.IdNota,
                $"Nota clínica agregada a la historia del paciente {pacienteId}.",
                ct);
        }

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

        if (await RespuestaSiPerfilIncompletoAsync(ct) is { } perfilIncompleto)
            return perfilIncompleto;

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para ver este odontograma." });

        var odontograma = await _service.ObtenerOdontogramaApiAsync(pacienteId, ct);
        int historiaId = await _context.HistoriasClinicas
            .Where(h => h.IdPaciente == pacienteId && h.Activa)
            .Select(h => h.IdHistoria)
            .FirstOrDefaultAsync(ct);
        if (historiaId > 0)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "SELECT",
                "Historia_Clinica",
                historiaId,
                "Consulta de odontograma desde API v1.",
                ct);
        }

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

        if (!await PuedeAccederAsync(pacienteId, ct))
            return StatusCode(403, new { success = false, message = "No tiene permiso para modificar el odontograma de este paciente." });

        if (request is null)
            return BadRequest(new { success = false, message = "No se recibieron datos del odontograma." });

        var result = await _service.GuardarOdontogramaApiAsync(pacienteId, request, GetCurrentUserId(), ct);

        if (!result.Success)
            return StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });

        if (result.Historia is not null)
        {
            await _auditoria.RegistrarAsync(
                HttpContext,
                "UPDATE",
                "Historia_Clinica",
                result.Historia.IdHistoria,
                "Odontograma actualizado desde API v1.",
                ct);
        }

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

        if (User.IsInRole("Profesional"))
        {
            int? idUsuario = GetCurrentUserId();
            if (idUsuario is null
                || !await _service.ProfesionalPuedeAccederHistoriaAsync(id, idUsuario.Value, ct))
            {
                return StatusCode(403, new { success = false, message = "No tiene permiso para modificar esta historia clínica." });
            }
        }

        var result = await _service.CambiarEstadoApiAsync(id, dto.Activa, ct);

        if (!result.Success)
        {
            return result.ErrorStatusCode switch
            {
                404 => NotFound(new { success = false, message = result.Message }),
                _ => StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message })
            };
        }

        await _auditoria.RegistrarAsync(
            HttpContext,
            "UPDATE",
            "Historia_Clinica",
            id,
            "Estado de historia clínica actualizado desde API v1.",
            ct);

        return Ok(new { success = true, message = result.Message, data = result.Historia });
    }
}