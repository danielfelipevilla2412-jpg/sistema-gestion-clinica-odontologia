/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: MobileSolicitudCitasController
 * ============================================
 * Archivo: Controllers/Api/MobileSolicitudCitasController.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Gestiona las solicitudes preliminares de cita realizadas desde la aplicación móvil.
 * Permite listar los servicios activos disponibles y recepcionar solicitudes de agendamiento
 * pendientes de aprobación/asignación por parte del personal administrativo o de recepción.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Filtra servicios disponibles únicamente en estado "activo".
 * - Valida la existencia del paciente autenticado antes de crear la solicitud.
 * - Registra la cita mediante el flujo centralizado con estado inicial "Solicitada".
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
using SmileTrack_MVC.Models.Entities;
<<<<<<< HEAD
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Services;
=======
using SmileTrack_MVC.Services.Perfiles;
>>>>>>> origin/historialclinico_pacientes

namespace SmileTrack_MVC.Controllers.Api;

public class SolicitudCitaMovil
{
    public int IdServicio { get; set; }
    public DateTime FechaHora { get; set; }
    public string? Notas { get; set; }
}

[ApiController]
[Route("api/mobile/solicitudes-citas")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileSolicitudCitasController : ControllerBase
{
    private readonly AppDbContext _context;
<<<<<<< HEAD
    private readonly ICitaService _citaService;

    public MobileSolicitudCitasController(AppDbContext context, ICitaService citaService)
    {
        _context = context;
        _citaService = citaService;
=======
    private readonly IPerfilPacienteService _perfilPacienteService;

    public MobileSolicitudCitasController(
        AppDbContext context,
        IPerfilPacienteService perfilPacienteService)
    {
        _context = context;
        _perfilPacienteService = perfilPacienteService;
>>>>>>> origin/historialclinico_pacientes
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
            solicitud.FechaHora <= DateTime.Now)
        {
            return BadRequest(new
            {
                message = "Selecciona un servicio y una fecha futura."
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
            var cita = await _citaService.SolicitarCitaPacienteAsync(
                paciente.IdPaciente,
                new CitaSolicitudPacienteDto
                {
                    IdServicio = solicitud.IdServicio,
                    Fecha = solicitud.FechaHora.Date,
                    Notas = solicitud.Notas
                },
                cancellationToken);

            return Ok(new
            {
                id = cita.IdCita,
                estado = cita.Estado,
                message = "Solicitud registrada correctamente. La clínica asignará el profesional y confirmará el horario."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
