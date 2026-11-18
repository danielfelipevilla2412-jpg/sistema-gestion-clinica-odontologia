using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;

namespace SmileTrack_MVC.Controllers;

public class PerfilesController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPerfilPacienteService _perfilPacienteService;
    private readonly AuditoriaHistoriaClinicaService _auditoria;

    public PerfilesController(
        AppDbContext context,
        IPerfilPacienteService perfilPacienteService,
        AuditoriaHistoriaClinicaService auditoria)
    {
        _context = context;
        _perfilPacienteService = perfilPacienteService;
        _auditoria = auditoria;
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

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == userId);
        if (usuario is null)
            return NotFound();

        var paciente = await _context.Pacientes
        .Include(p => p.Usuario)
        .Include(p => p.ProfesionalAsignado)
        .FirstOrDefaultAsync(p => p.IdUsuario == userId);

        paciente ??= new Paciente
        {
            IdUsuario = userId,
            Nombres = usuario.Nombre,
            Apellidos = usuario.Apellidos,
            Correo = usuario.Correo,
            TipoDocumento = string.Empty,
            Documento = string.Empty,
            FechaNacimiento = usuario.FechaNacimiento ?? DateTime.MinValue,
            Estado = "activo"
        };

        ViewBag.PerfilCompleto = await _perfilPacienteService.PerfilCompletoAsync(userId);
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
            FechaNacimiento = paciente?.FechaNacimiento ?? usuario.FechaNacimiento
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

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.IdUsuario == userId, cancellationToken);
        bool pacienteExistente = paciente is not null;

        var pacienteConDocumento = await _context.Pacientes
            .FirstOrDefaultAsync(p => p.Documento == documento && p.IdUsuario != userId, cancellationToken);

        if (pacienteConDocumento is not null)
        {
            bool puedeVincular = paciente is null &&
                pacienteConDocumento.IdUsuario is null &&
                string.Equals(pacienteConDocumento.Estado, "activo", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(pacienteConDocumento.TipoDocumento, tipoDocumento, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizarNombre(pacienteConDocumento.Nombres), NormalizarNombre(usuario.Nombre), StringComparison.Ordinal) &&
                string.Equals(NormalizarNombre(pacienteConDocumento.Apellidos), NormalizarNombre(usuario.Apellidos), StringComparison.Ordinal) &&
                pacienteConDocumento.FechaNacimiento.Date == model.FechaNacimiento!.Value.Date &&
                !EsDocumentoProvisionalGeneral(pacienteConDocumento.Documento);

            if (!puedeVincular)
            {
                ModelState.AddModelError(nameof(model.Documento),
                    "Encontramos una ficha clínica con ese documento, pero los datos no coinciden para vincularla automáticamente. Solicita revisión en recepción; no modificamos registros clínicos existentes desde aquí.");
                await CargarDatosCuentaAsync(model, userId, cancellationToken);
                return View("~/Views/Perfiles/completar-perfil-paciente.cshtml", model);
            }

            pacienteConDocumento.IdUsuario = userId;
            usuario.FechaNacimiento = pacienteConDocumento.FechaNacimiento.Date;
            _auditoria.Agregar(
                HttpContext,
                "UPDATE",
                "Paciente",
                pacienteConDocumento.IdPaciente,
                "Ficha clínica preexistente vinculada a la cuenta tras coincidencia de documento, nombres, apellidos y fecha de nacimiento.");
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(StpacPerfilPaciente));
        }

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

        if (pacienteExistente &&
            !EsDocumentoProvisionalGeneral(paciente.Documento) &&
            !string.IsNullOrWhiteSpace(paciente.Documento) &&
            (!string.Equals(paciente.TipoDocumento, tipoDocumento, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(paciente.Documento, documento, StringComparison.OrdinalIgnoreCase) ||
             paciente.FechaNacimiento.Date != model.FechaNacimiento!.Value.Date))
        {
            ModelState.AddModelError(string.Empty,
                "La ficha ya tiene datos de identidad registrados y no los modificaremos automáticamente. Solicita revisión en recepción para corregirlos.");
            await CargarDatosCuentaAsync(model, userId, cancellationToken);
            return View("~/Views/Perfiles/completar-perfil-paciente.cshtml", model);
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

    private static bool EsDocumentoProvisionalGeneral(string? documento) =>
        !string.IsNullOrWhiteSpace(documento) &&
        (documento.StartsWith("TEMP", StringComparison.OrdinalIgnoreCase) ||
         documento.StartsWith("USR-", StringComparison.OrdinalIgnoreCase));

    private static string NormalizarNombre(string? nombre) =>
        string.Join(' ', (nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

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
