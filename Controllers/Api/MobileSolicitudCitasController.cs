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
 * - Registra la cita con estado inicial "Pendiente" asignando valores por defecto según catálogo.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, JwtBearerDefaults, Cita, Servicio
 * ============================================
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;

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

    public MobileSolicitudCitasController(AppDbContext context)
    {
        _context = context;
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

        bool servicioExiste = await _context.Servicios
            .AnyAsync(
                s => s.IdServicio == solicitud.IdServicio &&
                     s.Estado == "activo",
                cancellationToken);

        if (!servicioExiste)
        {
            return BadRequest(new
            {
                message = "El servicio seleccionado no está disponible."
            });
        }

       var estadoAgendada = await _context.EstadosCita
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.NombreEstado == "Agendada",
                cancellationToken);

        if (estadoAgendada is null)
        {
            return BadRequest(new
            {
                message = "No se encontró el estado Agendada en la base de datos."
            });
        }

        var cita = new Cita
        {
            IdPaciente = paciente.IdPaciente,
            IdServicio = solicitud.IdServicio,
            FechaHora = solicitud.FechaHora,
            IdEstado = estadoAgendada.IdEstado,
            Estado = estadoAgendada.NombreEstado,
            Notas = solicitud.Notas?.Trim()
        };

        _context.Citas.Add(cita);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Solicitud de cita registrada. La clínica asignará el profesional."
        });
    }
}