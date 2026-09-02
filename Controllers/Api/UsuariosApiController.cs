using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.Api.Administracion;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/v1/admin/usuarios")]
[Authorize(Roles = "Administrador", Policy = "ApiOrCookie")]
[Produces("application/json")]
public sealed class UsuariosApiController(IUsuariosApiService service) : ControllerBase
{
    private readonly IUsuariosApiService _service = service;
    private int? CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) && id > 0 ? id : null;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? estado, [FromQuery] int? idRol, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _service.ObtenerAsync(search, estado, idRol, page, pageSize, ct);
        return result.Success ? Ok(new { success = true, data = result.Items, pagination = new { result.Page, result.PageSize, result.TotalCount, result.TotalPages } }) : StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct = default) => Ok(new { success = true, data = await _service.ObtenerRolesAsync(ct) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default)
    {
        if (id <= 0) return BadRequest(new { success = false, message = "Identificador inválido." });
        var user = await _service.ObtenerPorIdAsync(id, ct);
        return user is null ? NotFound(new { success = false, message = "Usuario no encontrado." }) : Ok(new { success = true, data = user });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] UsuarioApiCreateDto dto, CancellationToken ct = default) => FromResult(await _service.CrearAsync(dto, CurrentUserId(), ct), 201);

    [HttpPatch("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UsuarioApiUpdateDto dto, CancellationToken ct = default) => id <= 0 ? BadRequest(new { success = false, message = "Identificador inválido." }) : FromResult(await _service.ActualizarAsync(id, dto, CurrentUserId(), ct));

    [HttpPatch("{id:int}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeState(int id, [FromBody] UsuarioApiEstadoDto dto, CancellationToken ct = default) => id <= 0 ? BadRequest(new { success = false, message = "Identificador inválido." }) : FromResult(await _service.CambiarEstadoAsync(id, dto.Estado, CurrentUserId(), ct));

    [HttpPost("{id:int}/restablecer-contrasena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] UsuarioApiRestablecerContrasenaDto dto, CancellationToken ct = default) => id <= 0 ? BadRequest(new { success = false, message = "Identificador inválido." }) : FromResult(await _service.RestablecerContrasenaAsync(id, dto.ContrasenaTemporal, CurrentUserId(), ct));

    [HttpPost("{id:int}/desbloquear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct = default) => id <= 0 ? BadRequest(new { success = false, message = "Identificador inválido." }) : FromResult(await _service.DesbloquearAsync(id, CurrentUserId(), ct));

    private IActionResult FromResult(UsuariosApiResult result, int successStatus = 200) => result.Success ? StatusCode(successStatus, new { success = true, message = result.Message, data = result.Data }) : StatusCode(result.ErrorStatusCode ?? 500, new { success = false, message = result.Message });
}
