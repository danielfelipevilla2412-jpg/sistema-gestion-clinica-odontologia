/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: MobileCitasController
 * ============================================
 * Archivo: Controllers/Api/MobileCitasController.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Proporciona un endpoint API simplificado y autenticado exclusivamente vía JWT Bearer
 * para la aplicación móvil del paciente. Permite consultar el listado de "Mis Citas"
 * vinculadas directamente a la cuenta del usuario logueado.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Extracción estricta del claim NameIdentifier para mapear Paciente por IdUsuario.
 * - Filtra únicamente citas activas no eliminadas, ordenadas descendentemente por fecha/hora.
 * - Formatea la salida JSON ligera optimizada para dispositivos móviles (red reducida).
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, JwtBearerDefaults
 * ============================================
 */

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Controllers.Api;

[ApiController]
[Route("api/mobile/citas")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MobileCitasController : ControllerBase
{
    private readonly AppDbContext _context;

    public MobileCitasController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("mis-citas")]
    public async Task<IActionResult> MisCitas(CancellationToken cancellationToken)
    {
        string? idUsuarioTexto =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idUsuarioTexto, out int idUsuario))
        {
            return Unauthorized(new
            {
                message = "Token de usuario inválido."
            });
        }

        var paciente = await _context.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.IdUsuario == idUsuario,
                cancellationToken);

        if (paciente is null)
        {
            return NotFound(new
            {
                message = "No se encontró un paciente asociado al usuario."
            });
        }

        var citas = await _context.Citas
            .AsNoTracking()
            .Where(c =>
                c.IdPaciente == paciente.IdPaciente &&
                c.FechaHora >= DateTime.Today)
            .OrderBy(c => c.FechaHora)
            .Select(c => new
            {
                id = c.IdCita,
                servicio = c.Servicio != null
                    ? c.Servicio.Nombre
                    : "Consulta odontológica",

                profesional = c.Profesional != null
                    ? c.Profesional.Nombres + " " + c.Profesional.Apellidos
                    : "Profesional por asignar",

                fechaHora = c.FechaHora,
                estado = c.EstadoCita != null
                    ? c.EstadoCita.NombreEstado
                    : c.Estado,

                notas = c.Notas
            })
            .ToListAsync(cancellationToken);

        return Ok(citas);
    }
}