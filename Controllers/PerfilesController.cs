using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class PerfilesController : Controller
{
    private readonly AppDbContext _context;

    public PerfilesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("perfiles/st-aux-11-perfil-auxiliar")]
    public async Task<IActionResult> Staux11PerfilAuxiliar()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == userId);
        return View("~/Views/Perfiles/st-aux-11-perfil-auxiliar/mi-perfil.cshtml", usuario);
    }

    [HttpGet]
    [Authorize(Roles = "Paciente")]
    [Route("perfiles/st-pac-perfil-paciente")]
    public async Task<IActionResult> StpacPerfilPaciente()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var paciente = await _context.Pacientes
        .Include(p => p.Usuario)
        .Include(p => p.ProfesionalAsignado)
        .FirstOrDefaultAsync(p => p.IdUsuario == userId);

        if (paciente is null ||
            EsDocumentoProvisional(paciente.Documento, userId) ||
            paciente.Usuario?.FechaNacimiento is not DateTime fechaCuenta ||
            paciente.FechaNacimiento.Date != fechaCuenta.Date)
            return RedirectToAction(nameof(CompletarPerfilPaciente));

        return View("~/Views/Perfiles/st-pac-perfil-Paciente/perfil-paciente.cshtml", paciente);
    }

    [HttpGet]
    [Authorize(Roles = "Paciente")]
    [Route("perfiles/st-pac-completar-perfil")]
    public async Task<IActionResult> CompletarPerfilPaciente(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            return Unauthorized();

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == userId, cancellationToken);
        if (usuario is null)
            return NotFound();

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdUsuario == userId, cancellationToken);

        return View("~/Views/Perfiles/completar-perfil-paciente.cshtml", new CompletarPerfilPacienteViewModel
        {
            NombreCompleto = $"{usuario.Nombre} {usuario.Apellidos}".Trim(),
            Correo = usuario.Correo,
            TipoDocumento = paciente?.TipoDocumento ?? string.Empty,
            Documento = EsDocumentoProvisional(paciente?.Documento, userId) ? string.Empty : paciente?.Documento ?? string.Empty,
            FechaNacimiento = usuario.FechaNacimiento
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Paciente")]
    [Route("perfiles/st-pac-completar-perfil")]
    public async Task<IActionResult> CompletarPerfilPaciente(
        CompletarPerfilPacienteViewModel model,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            return Unauthorized();

        if (model.FechaNacimiento is null || model.FechaNacimiento.Value.Date > DateTime.UtcNow.Date)
            ModelState.AddModelError(nameof(model.FechaNacimiento), "Ingresa una fecha de nacimiento válida.");
        else if (model.FechaNacimiento.Value.Date < new DateTime(1900, 1, 1))
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento no es válida.");

        string tipoDocumento = model.TipoDocumento?.Trim().ToUpperInvariant() ?? string.Empty;
        if (tipoDocumento is not ("CC" or "TI" or "CE" or "PAS"))
            ModelState.AddModelError(nameof(model.TipoDocumento), "Selecciona un tipo de documento válido.");

        string documento = model.Documento?.Trim().ToUpperInvariant() ?? string.Empty;
        string patronDocumento = tipoDocumento == "PAS" ? "^[A-Z0-9]{5,20}$" : "^[0-9]{7,15}$";
        if (tipoDocumento is "CC" or "TI" or "CE" or "PAS" &&
            !System.Text.RegularExpressions.Regex.IsMatch(documento, patronDocumento))
        {
            ModelState.AddModelError(nameof(model.Documento), "El documento no es válido para el tipo seleccionado.");
        }

        if (!ModelState.IsValid)
        {
            await CargarDatosCuentaAsync(model, userId, cancellationToken);
            return View("~/Views/Perfiles/completar-perfil-paciente.cshtml", model);
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == userId, cancellationToken);
        if (usuario is null)
            return NotFound();

        bool documentoEnUso = await _context.Pacientes.AnyAsync(
            p => p.Documento == documento && p.IdUsuario != userId,
            cancellationToken);
        if (documentoEnUso)
        {
            ModelState.AddModelError(nameof(model.Documento), "Ese documento ya está registrado.");
            await CargarDatosCuentaAsync(model, userId, cancellationToken);
            return View("~/Views/Perfiles/completar-perfil-paciente.cshtml", model);
        }

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.IdUsuario == userId, cancellationToken);
        if (paciente is null)
        {
            paciente = new Paciente
            {
                IdUsuario = userId,
                Nombres = usuario.Nombre,
                Apellidos = usuario.Apellidos,
                Correo = usuario.Correo,
                FechaRegistro = DateTime.UtcNow.Date,
                Estado = "activo"
            };
            _context.Pacientes.Add(paciente);
        }

        paciente.TipoDocumento = tipoDocumento;
        paciente.Documento = documento;
        paciente.FechaNacimiento = model.FechaNacimiento!.Value.Date;
        usuario.FechaNacimiento = paciente.FechaNacimiento;

        await _context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(StpacPerfilPaciente));
    }

    private async Task CargarDatosCuentaAsync(
        CompletarPerfilPacienteViewModel model,
        int userId,
        CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == userId, cancellationToken);
        if (usuario is null)
            return;

        model.NombreCompleto = $"{usuario.Nombre} {usuario.Apellidos}".Trim();
        model.Correo = usuario.Correo;
    }

    private static bool EsDocumentoProvisional(string? documento, int userId) =>
        !string.IsNullOrWhiteSpace(documento) &&
        (string.Equals(documento, $"TEMP{userId}", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(documento, $"USR-{userId:000000}", StringComparison.OrdinalIgnoreCase));

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("perfiles/st-rec-06-perfil-recepcionista")]
    public async Task<IActionResult> Strec06PerfilRecepcionista()
    {
        string? userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int userId);
        var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == userId);
        return View("~/Views/Perfiles/st-rec-06-perfil-recepcionista/perfilrecepcionista.cshtml", usuario);
    }
}
