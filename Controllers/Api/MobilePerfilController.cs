using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Services.Perfiles;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/perfil")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobilePerfilController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ActualizacionPerfilPacienteService _actualizacionPerfil;

    public MobilePerfilController( AppDbContext context, ActualizacionPerfilPacienteService actualizacionPerfil)
{
    _context = context;
    _actualizacionPerfil = actualizacionPerfil;
}

    [HttpGet]
    public async Task<IActionResult> ObtenerPerfil(
        CancellationToken cancellationToken)
    {
        string? idUsuarioTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idUsuarioTexto, out int idUsuario))
        {
            return Unauthorized();
        }

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.IdUsuario == idUsuario,
                cancellationToken);

        if (usuario is null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado."
            });
        }

        return Ok(new
        {
            nombre = $"{usuario.Nombre} {usuario.Apellidos}".Trim(),
            correo = usuario.Correo,
            estado = usuario.Estado
        });
    }


[HttpGet("datos-personales")]
public async Task<IActionResult> ObtenerDatosPersonales(
    CancellationToken cancellationToken)
{
    string? idTexto =
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!int.TryParse(idTexto, out int idUsuario))
        return Unauthorized();

    var usuario = await _context.Usuarios
        .AsNoTracking()
        .FirstOrDefaultAsync(
            u => u.IdUsuario == idUsuario,
            cancellationToken);

    if (usuario is null)
        return NotFound(new
        {
            message = "Usuario no encontrado."
        });

    var paciente = await _context.Pacientes
        .AsNoTracking()
        .FirstOrDefaultAsync(
            p => p.IdUsuario == idUsuario,
            cancellationToken);

    bool documentoProvisional =
        paciente is not null &&
        (paciente.Documento.StartsWith("TEMP",
            StringComparison.OrdinalIgnoreCase) ||
         paciente.Documento.StartsWith("USR-",
            StringComparison.OrdinalIgnoreCase));

    return Ok(new
    {
        nombre = $"{usuario.Nombre} {usuario.Apellidos}".Trim(),
        correo = usuario.Correo,
        tipoDocumento = documentoProvisional
            ? ""
            : paciente?.TipoDocumento ?? "",
        documento = documentoProvisional
            ? ""
            : paciente?.Documento ?? "",
        fechaNacimiento =
            paciente?.FechaNacimiento ??
            usuario.FechaNacimiento
    });
}


[HttpPut("datos-personales")]
[Authorize(Roles = "Paciente")]
public async Task<IActionResult> ActualizarDatosPersonales(
    [FromBody] ActualizarDatosPacienteDto datos,
    CancellationToken cancellationToken)
{
    string? idTexto =
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!int.TryParse(idTexto, out int idUsuario))
    {
        return Unauthorized(new
        {
            message = "Sesión no válida."
        });
    }

    var resultado = await _actualizacionPerfil.ActualizarAsync(
        idUsuario,
        datos,
        HttpContext,
        cancellationToken);

    return StatusCode(resultado.StatusCode, new
    {
        success = resultado.Success,
        message = resultado.Message
    });
}


}