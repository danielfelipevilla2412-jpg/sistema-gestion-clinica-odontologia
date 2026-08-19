using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;

public class GestionPacientesController : Controller
{
    private readonly AppDbContext _context;

    public GestionPacientesController(AppDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // READ - Gestión de pacientes
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/st-adm-05-gestion-pacientes")]
    public async Task<IActionResult> Stadm05GestionPacientes()
    {
        string[] colores =
        [
            "blue",
            "green",
            "yellow",
            "purple",
            "slate"
        ];

        var ahora = DateTime.Now;

        var pacientesDb = await _context.Pacientes
            .Where(p => p.Estado == "activo")
            .OrderBy(p => p.Apellidos)
            .ThenBy(p => p.Nombres)
            .ToListAsync();

        var idsPacientes =
            pacientesDb
                .Select(p => p.IdPaciente)
                .ToList();

        // Traemos todas las citas de una sola vez.
        // Esto evita consultas N+1.
        var citas = await _context.Citas
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .Where(c => idsPacientes.Contains(c.IdPaciente))
            .OrderByDescending(c => c.FechaHora)
            .ToListAsync();

        var pacientes =
            new List<PacienteViewModel>();

        for (int i = 0; i < pacientesDb.Count; i++)
        {
            var p = pacientesDb[i];

            var citasPaciente =
                citas
                    .Where(c => c.IdPaciente == p.IdPaciente)
                    .ToList();

            var ultima =
                citasPaciente
                    .Where(c => c.FechaHora <= ahora)
                    .OrderByDescending(c => c.FechaHora)
                    .FirstOrDefault();

            var proxima =
                citasPaciente
                    .Where(c =>
                        c.FechaHora > ahora &&
                        c.Estado != "Cancelada")
                    .OrderBy(c => c.FechaHora)
                    .FirstOrDefault();

            var alergias =
                string.IsNullOrWhiteSpace(p.Alergias)
                    ? new List<string>()
                    : p.Alergias
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries |
                            StringSplitOptions.TrimEntries)
                        .ToList();

            char inicialNombre =
                p.Nombres.Trim().FirstOrDefault();

            char inicialApellido =
                p.Apellidos.Trim().FirstOrDefault();

            string iniciales =
                $"{inicialNombre}{inicialApellido}"
                    .ToUpperInvariant();

            pacientes.Add(
                new PacienteViewModel
                {
                    Id = p.IdPaciente,

                    Initials =
                        string.IsNullOrWhiteSpace(iniciales)
                            ? "??"
                            : iniciales,

                    Nombres = p.Nombres,
                    Apellidos = p.Apellidos,

                    Name =
                        $"{p.Nombres} {p.Apellidos}".Trim(),

                    TipoDocumento =
                        p.TipoDocumento,

                    Documento =
                        p.Documento,

                    Doc =
                        $"{p.TipoDocumento} {p.Documento}",

                    FechaNacimiento =
                        p.FechaNacimiento,

                    Genero =
                        p.Genero,

                    Telefono =
                        p.Telefono,

                    Correo =
                        p.Correo,

                    Ciudad =
                        p.Ciudad,

                    GrupoSanguineo =
                        p.GrupoSanguineo,

                    AlergiasTexto =
                        p.Alergias,

                    Estado =
                        p.Estado,

                    LastVisit =
                        ultima?.FechaHora,

                    Diagnosis =
                        ultima?.Servicio?.Nombre ??
                        ultima?.Notas ??
                        string.Empty,

                    NextVisit =
                        proxima?.FechaHora,

                    Allergies =
                        alergias,

                    Color =
                        colores[i % colores.Length],

                    History =
                        citasPaciente
                            .Where(c => c.FechaHora <= ahora)
                            .OrderByDescending(c => c.FechaHora)
                            .Select(
                                c =>
                                    new PacienteHistorialViewModel
                                    {
                                        Date =
                                            c.FechaHora,

                                        Procedure =
                                            c.Servicio?.Nombre ??
                                            c.Notas ??
                                            "Consulta",

                                        Doctor =
                                            c.Profesional != null
                                                ? $"{c.Profesional.Nombres} {c.Profesional.Apellidos}"
                                                : "Sin asignar"
                                    })
                            .ToList()
                });
        }

        return View(
            "~/Views/Gestion_De_Pacientes/st-adm-05-gestion-pacientes/index.cshtml",
            pacientes);
    }

    // =========================================================================
    // VISTAS
    // =========================================================================

    [HttpGet]
    [Authorize(Roles = "Auxiliar")]
    [Route("gestion-de-pacientes/st-aux-03-preparacion-consulta")]
    public IActionResult Staux03PreparacionConsulta() =>
        View(
            "~/Views/Gestion_De_Pacientes/st-aux-03-preparacion-consulta/preparacion.cshtml");

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("gestion-de-pacientes/st-rec-02-registrar-paciente")]
    public IActionResult Strec02RegistrarPaciente() =>
        View(
            "~/Views/Gestion_De_Pacientes/st-rec-02-registrar-paciente/nuevo_paciente.cshtml");

    // =========================================================================
    // CREATE - Registrar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPaciente(
        [FromForm] string tipoDoc,
        [FromForm] string documento,
        [FromForm] string nombres,
        [FromForm] string apellidos,
        [FromForm] DateTime fechaNacimiento,
        [FromForm] string? genero,
        [FromForm] string? telefono,
        [FromForm] string? correo,
        [FromForm] string? grupoSanguineo,
        [FromForm] string? ciudad,
        [FromForm] string? alergias)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(documento))
            {
                return BadRequest(
                    new
                    {
                        ok = false,
                        message =
                            "El documento es obligatorio."
                    });
            }

            string documentoNormalizado =
                documento.Trim();

            bool existe =
                await _context.Pacientes.AnyAsync(
                    p =>
                        p.Documento ==
                        documentoNormalizado);

            if (existe)
            {
                return Conflict(
                    new
                    {
                        ok = false,
                        message =
                            "Ya existe un paciente con ese documento."
                    });
            }

            // Validación consistente con CHECK de SQL Server.
            string? generoNormalizado = null;

            if (!string.IsNullOrWhiteSpace(genero))
            {
                generoNormalizado =
                    genero.Trim().ToUpperInvariant();

                if (generoNormalizado is not ("M" or "F" or "O"))
                {
                    return BadRequest(
                        new
                        {
                            ok = false,
                            message =
                                "El género enviado no es válido."
                        });
                }
            }

            var paciente =
                new SmileTrack_MVC.Models.Entities.Paciente
                {
                    TipoDocumento =
                        tipoDoc.Trim(),

                    Documento =
                        documentoNormalizado,

                    Nombres =
                        nombres.Trim(),

                    Apellidos =
                        apellidos.Trim(),

                    FechaNacimiento =
                        fechaNacimiento,

                    Genero =
                        generoNormalizado,

                    Telefono =
                        string.IsNullOrWhiteSpace(telefono)
                            ? null
                            : telefono.Trim(),

                    Correo =
                        string.IsNullOrWhiteSpace(correo)
                            ? null
                            : correo.Trim(),

                    GrupoSanguineo =
                        string.IsNullOrWhiteSpace(grupoSanguineo)
                            ? null
                            : grupoSanguineo.Trim(),

                    Ciudad =
                        string.IsNullOrWhiteSpace(ciudad)
                            ? null
                            : ciudad.Trim(),

                    Alergias =
                        string.IsNullOrWhiteSpace(alergias)
                            ? null
                            : alergias.Trim(),

                    Estado =
                        "activo",

                    FechaRegistro =
                        DateTime.UtcNow.Date
                };

            _context.Pacientes.Add(
                paciente);

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,
                    idPaciente =
                        paciente.IdPaciente,

                    message =
                        "Paciente registrado correctamente."
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,
                    message =
                        "No fue posible registrar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
    }

    // =========================================================================
    // UPDATE - Actualizar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista,Profesional")]
    [Route("gestion-de-pacientes/actualizar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarPaciente(
        [FromForm] int idPaciente,
        [FromForm] string? nombres,
        [FromForm] string? apellidos,
        [FromForm] string? telefono,
        [FromForm] string? correo,
        [FromForm] string? ciudad,
        [FromForm] string? alergias,
        [FromForm] string? estado,
        [FromForm] string? genero)
    {
        try
        {
            var paciente =
                await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p =>
                            p.IdPaciente ==
                            idPaciente);

            if (paciente == null)
            {
                return NotFound(
                    new
                    {
                        ok = false,
                        message =
                            "Paciente no encontrado."
                    });
            }

            if (!string.IsNullOrWhiteSpace(nombres))
            {
                paciente.Nombres =
                    nombres.Trim();
            }

            if (!string.IsNullOrWhiteSpace(apellidos))
            {
                paciente.Apellidos =
                    apellidos.Trim();
            }

            paciente.Telefono =
                string.IsNullOrWhiteSpace(telefono)
                    ? null
                    : telefono.Trim();

            paciente.Correo =
                string.IsNullOrWhiteSpace(correo)
                    ? null
                    : correo.Trim();

            paciente.Ciudad =
                string.IsNullOrWhiteSpace(ciudad)
                    ? null
                    : ciudad.Trim();

            if (!string.IsNullOrWhiteSpace(genero))
            {
                var generoNormalizado =
                    genero.Trim().ToUpperInvariant();

                if (generoNormalizado is not ("M" or "F" or "O"))
                {
                    return BadRequest(
                        new
                        {
                            ok = false,
                            message =
                                "El género enviado no es válido."
                        });
                }

                paciente.Genero =
                    generoNormalizado;
            }

            paciente.Alergias =
                string.IsNullOrWhiteSpace(alergias)
                    ? null
                    : alergias.Trim();

            if (!string.IsNullOrWhiteSpace(estado))
            {
                var estadoNormalizado =
                    estado.Trim().ToLowerInvariant();

                if (estadoNormalizado is not
                    ("activo" or "inactivo" or "retirado"))
                {
                    return BadRequest(
                        new
                        {
                            ok = false,
                            message =
                                "El estado enviado no es válido."
                        });
                }

                paciente.Estado =
                    estadoNormalizado;
            }

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,

                    message =
                        "Paciente actualizado correctamente.",

                    idPaciente =
                        paciente.IdPaciente
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message =
                        "No fue posible actualizar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
    }

    // =========================================================================
    // DELETE LÓGICO - Desactivar paciente
    // =========================================================================

    [HttpPost]
    [Authorize(Roles = "Administrador,Recepcionista")]
    [Route("gestion-de-pacientes/desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DesactivarPaciente(
        [FromForm] int idPaciente)
    {
        try
        {
            var paciente =
                await _context.Pacientes
                    .FirstOrDefaultAsync(
                        p =>
                            p.IdPaciente ==
                            idPaciente);

            if (paciente == null)
            {
                return NotFound(
                    new
                    {
                        ok = false,
                        message =
                            "Paciente no encontrado."
                    });
            }

            if (string.Equals(
                    paciente.Estado,
                    "inactivo",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    new
                    {
                        ok = false,
                        message =
                            "El paciente ya se encuentra inactivo."
                    });
            }

            paciente.Estado =
                "inactivo";

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    ok = true,

                    message =
                        "Paciente desactivado correctamente.",

                    idPaciente =
                        paciente.IdPaciente
                });
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    ok = false,

                    message =
                        "No fue posible desactivar el paciente.",

                    detail =
                        ex.InnerException?.Message ??
                        ex.Message
                });
        }
    }
}