/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: MobileSolicitudCitasController
 * ============================================
 * Archivo: Controllers/Api/MobileSolicitudCitasController.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Permite que el paciente consulte servicios, profesionales y horarios disponibles y
 * agende una cita directamente desde la aplicación móvil.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Filtra servicios disponibles únicamente en estado "activo".
 * - Valida la existencia del paciente autenticado antes de crear la solicitud.
 * - Reserva la cita mediante el flujo centralizado con estado inicial "Programada".
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, ICitaService, JwtBearerDefaults
 * ============================================
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;

namespace SmileTrack_MVC.Controllers.Api;

public class SolicitudCitaMovil
{
    public int IdServicio { get; set; }
    public int IdProfesional { get; set; }
    public DateTime FechaHora { get; set; }
    public string? Notas { get; set; }
}

[ApiController]
[Route("api/mobile/solicitudes-citas")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileSolicitudCitasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICitaService _citaService;
    private readonly IPerfilPacienteService _perfilPacienteService;

    public MobileSolicitudCitasController(
        AppDbContext context,
        ICitaService citaService,
        IPerfilPacienteService perfilPacienteService)
    {
        _context = context;
        _citaService = citaService;
        _perfilPacienteService = perfilPacienteService;
    }

    [HttpGet("servicios")]
    public async Task<IActionResult> ObtenerServicios(
        CancellationToken cancellationToken)
    {
        var servicios = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.Estado == "activo")
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                id = s.IdServicio,
                nombre = s.Nombre
            })
            .ToListAsync(cancellationToken);

        return Ok(servicios);
    }

    [HttpGet("profesionales")]
    public async Task<IActionResult> ObtenerProfesionales(
        [FromQuery] int idServicio,
        CancellationToken cancellationToken)
    {
        var profesionales = await _citaService.ObtenerProfesionalesPorServicioAsync(
            idServicio, cancellationToken);
        return Ok(profesionales);
    }

    [HttpGet("horarios")]
    public async Task<IActionResult> ObtenerHorarios(
        [FromQuery] int idServicio,
        [FromQuery] int idProfesional,
        [FromQuery] DateTime fecha,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario) ||
            !await _perfilPacienteService.PerfilCompletoAsync(idUsuario, cancellationToken))
        {
            return Conflict(new
            {
                code = "PROFILE_INCOMPLETE",
                message = "Confirma tus datos personales antes de agendar una cita.",
                profileUrl = "/perfiles/st-pac-perfil-paciente"
            });
        }

        int? idPaciente = await _context.Pacientes
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(cancellationToken);
        if (idPaciente is not > 0)
            return NotFound(new { message = "No hay un paciente asociado a esta cuenta." });

        var horarios = await _citaService.ObtenerHorariosDisponiblesPacienteAsync(
            idPaciente.Value, idServicio, idProfesional, fecha, cancellationToken);
        return Ok(horarios.Select(hora => hora.ToString(@"hh\:mm")));
    }

    [HttpPost]
    public async Task<IActionResult> Solicitar(
        [FromBody] SolicitudCitaMovil solicitud,
        CancellationToken cancellationToken)
    {
        string? idUsuarioTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idUsuarioTexto, out int idUsuario))
        {
            return Unauthorized();
        }

        if (!await _perfilPacienteService.PerfilCompletoAsync(idUsuario, cancellationToken))
        {
            return Conflict(new
            {
                code = "PROFILE_INCOMPLETE",
                message = "Confirma tus datos personales antes de solicitar una cita.",
                profileUrl = "/perfiles/st-pac-perfil-paciente"
            });
        }

        if (solicitud.IdServicio <= 0 ||
            solicitud.IdProfesional <= 0 ||
            solicitud.FechaHora <= ColombiaClock.Now)
        {
            return BadRequest(new
            {
                message = "Selecciona un servicio, profesional y horario futuro."
            });
        }

        var paciente = await _context.Pacientes
            .FirstOrDefaultAsync(
                p => p.IdUsuario == idUsuario,
                cancellationToken);

        if (paciente is null)
        {
            return NotFound(new
            {
                message = "No hay un paciente asociado a esta cuenta."
            });
        }

        try
        {
            var cita = await _citaService.AgendarCitaPacienteAsync(
                paciente.IdPaciente,
                new CitaSolicitudPacienteDto
                {
                    IdServicio = solicitud.IdServicio,
                    IdProfesional = solicitud.IdProfesional,
                    Fecha = solicitud.FechaHora.Date,
                    HoraInicio = solicitud.FechaHora.TimeOfDay,
                    Notas = solicitud.Notas
                },
                cancellationToken);

            return Ok(new
            {
                success = true,
                id = cita.IdCita,
                estado = cita.Estado,
                message = "Cita agendada correctamente."
            });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("horario", StringComparison.OrdinalIgnoreCase))
            {
                var horariosDisponibles = await _citaService.ObtenerHorariosDisponiblesPacienteAsync(
                    paciente.IdPaciente,
                    solicitud.IdServicio,
                    solicitud.IdProfesional,
                    solicitud.FechaHora.Date,
                    cancellationToken);
                return Conflict(new { message = ex.Message, horariosDisponibles });
            }
            return BadRequest(new { message = ex.Message });
        }
    }
}
