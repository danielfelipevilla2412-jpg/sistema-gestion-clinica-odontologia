using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

/// <summary>
/// ViewComponent para mostrar la próxima cita con countdown
/// Uso: @await Component.InvokeAsync("ProximaCitaWidget", new { idProfesional = 1 })
/// </summary>
public class ProximaCitaWidgetViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public ProximaCitaWidgetViewComponent(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Invoca el componente para mostrar la próxima cita
    /// </summary>
    /// <param name="idProfesional">ID del profesional (opcional)</param>
    /// <param name="idPaciente">ID del paciente (opcional)</param>
    /// <param name="incluirUltimaAtencion">Si debe incluir información de última atención</param>
    public async Task<IViewComponentResult> InvokeAsync(
        int? idProfesional = null, 
        int? idPaciente = null,
        bool incluirUltimaAtencion = true)
    {
        var ahora = DateTime.Now;

        // Query base: citas futuras no canceladas ni completadas
        var query = _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.FechaHora > ahora)
            .Where(c => c.IdEstadoCita != 4 && c.IdEstadoCita != 5); // No canceladas ni completadas

        // Filtrar por profesional o paciente
        if (idProfesional.HasValue)
        {
            query = query.Where(c => c.IdProfesional == idProfesional.Value);
        }
        
        if (idPaciente.HasValue)
        {
            query = query.Where(c => c.IdPaciente == idPaciente.Value);
        }

        // Obtener la próxima cita
        var proximaCita = await query
            .OrderBy(c => c.FechaHora)
            .FirstOrDefaultAsync();

        // Si no hay cita, retornar modelo vacío
        if (proximaCita == null)
        {
            return View(new ProximaCitaWidgetViewModel());
        }

        // Obtener última atención si se solicita
        string? ultimaAtencion = null;
        if (incluirUltimaAtencion && proximaCita.IdPaciente > 0)
        {
            var ultimaCitaAtendida = await _context.Citas
                .Where(c => c.IdPaciente == proximaCita.IdPaciente)
                .Where(c => c.IdEstadoCita == 3) // Atendida
                .OrderByDescending(c => c.FechaHora)
                .Select(c => c.FechaHora)
                .FirstOrDefaultAsync();

            ultimaAtencion = ultimaCitaAtendida != default 
                ? $"Última atención: {ultimaCitaAtendida:dd/MM/yyyy}" 
                : "Primera visita";
        }

        // Construir ViewModel
        var viewModel = new ProximaCitaWidgetViewModel
        {
            IdCita = proximaCita.IdCita,
            NombrePaciente = $"{proximaCita.Paciente?.Nombre} {proximaCita.Paciente?.Apellido}".Trim(),
            NombreServicio = proximaCita.Servicio?.Nombre ?? "Servicio sin nombre",
            FechaHora = proximaCita.FechaHora,
            NombreConsultorio = proximaCita.Consultorio?.Nombre ?? "Sin consultorio",
            UltimaAtencion = ultimaAtencion
        };

        return View(viewModel);
    }
}
