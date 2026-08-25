using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using System.Security.Claims;

namespace SmileTrack_MVC.Controllers;

[Authorize(Roles = "Administrador")]
[Route("api/admin/usuarios")]
public sealed class GestionUsuariosController(
    AppDbContext context,
    IUsuarioAdminService usuarioService,
    ILogger<GestionUsuariosController> logger) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly IUsuarioAdminService _usuarioService = usuarioService;
    private readonly ILogger<GestionUsuariosController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct = default)
    {
        try
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .OrderBy(u => u.Apellidos)
                .ThenBy(u => u.Nombre)
                .Select(u => new
                {
                    id = u.IdUsuario,
                    name = (u.Nombre + " " + u.Apellidos).Trim(),
                    initials = ((u.Nombre.Length > 0 ? u.Nombre.Substring(0, 1) : "") +
                                (u.Apellidos.Length > 0 ? u.Apellidos.Substring(0, 1) : "")).ToUpper(),
                    email = u.Correo,
                    role = u.Rol != null ? u.Rol.NombreRol : "Sin Rol",
                    status = u.IntentosFallidos >= 3
                        ? "Bloqueado"
                        : (u.Estado == "activo" ? "Activo" : "Inactivo"),
                    lastAccess = u.UltimoLogin
                })
                .ToListAsync(ct);

            return Ok(new { success = true, data = usuarios });
        }
        catch (OperationCanceledException)
        {
            return BadRequest(new { success = false, message = "La operación fue cancelada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando usuarios administrativos.");
            return StatusCode(500, new { success = false, message = "No se pudieron cargar los usuarios." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(
        [FromBody] CrearUsuarioAdminRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        int? idOperador = ObtenerIdUsuarioActual();
        var result = await _usuarioService.CrearAsync(request, idOperador, ct);

        return result.Success
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : BadRequest(new { success = false, message = result.Message });
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(
        int id,
        [FromBody] ActualizarUsuarioAdminRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        int? idOperador = ObtenerIdUsuarioActual();
        var result = await _usuarioService.ActualizarAsync(id, request, idOperador, ct);

        return result.Success
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : BadRequest(new { success = false, message = result.Message });
    }

    [HttpPost("{id:int}/estado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        int id,
        [FromBody] CambiarEstadoUsuarioRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        int? idOperador = ObtenerIdUsuarioActual();
        var result = await _usuarioService.CambiarEstadoAsync(id, request, idOperador, ct);

        return result.Success
            ? Ok(new { success = true, message = result.Message, data = result.Data })
            : BadRequest(new { success = false, message = result.Message });
    }

    private int? ObtenerIdUsuarioActual()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out int id) && id > 0 ? id : null;
    }
}
