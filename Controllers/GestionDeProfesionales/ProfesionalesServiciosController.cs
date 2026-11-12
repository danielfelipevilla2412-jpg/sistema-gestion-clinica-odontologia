using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Models.Entities;
using System.Globalization;

namespace SmileTrack_MVC.Controllers;

// Extensión del controlador existente: comparte AppDbContext y autorización.
public partial class GestionProfesionalesController
{
    private const string RutaServiciosProfesional = "gestion-de-profesionales/servicios-asignados";

    [HttpGet(RutaServiciosProfesional)]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ServiciosAsignados(int? profesionalId, CancellationToken ct)
    {
        var profesionales = await _context.Profesionales.AsNoTracking()
            .OrderBy(p => p.Nombres).ThenBy(p => p.Apellidos).ToListAsync(ct);
        var seleccionado = profesionalId.HasValue
            ? profesionales.FirstOrDefault(p => p.IdProfesional == profesionalId.Value)
            : profesionales.FirstOrDefault(p => p.Estado == "activo");
        var servicios = await _context.Servicios.AsNoTracking()
            .Include(s => s.Especialidad).OrderBy(s => s.Nombre).ToListAsync(ct);
        var especialidades = seleccionado is null ? new List<int>() : await _context.ProfesionalEspecialidades
            .AsNoTracking().Where(pe => pe.IdProfesional == seleccionado.IdProfesional)
            .Select(pe => pe.IdEspecialidad).ToListAsync(ct);
        var nombresEspecialidades = especialidades.Count == 0
            ? new List<string>()
            : await _context.Especialidades.AsNoTracking()
                .Where(e => especialidades.Contains(e.IdEspecialidad))
                .OrderBy(e => e.Nombre)
                .Select(e => e.Nombre)
                .ToListAsync(ct);
        var asignaciones = seleccionado is null ? new List<ProfesionalServicio>() : await _context.ProfesionalServicios
            .AsNoTracking().Include(ps => ps.Servicio)
            .Where(ps => ps.IdProfesional == seleccionado.IdProfesional)
            .OrderBy(ps => ps.Servicio!.Nombre).ToListAsync(ct);
        ViewData["ProfesionalesLista"] = profesionales;
        ViewData["ProfesionalSeleccionado"] = seleccionado;
        ViewData["ServiciosLista"] = servicios;
        ViewData["EspecialidadesProfesional"] = especialidades;
        ViewData["NombresEspecialidadesProfesional"] = nombresEspecialidades;
        ViewData["AsignacionesLista"] = asignaciones;
        return View("~/Views/Gestion_De_Profesionales/st-adm-07-gestion-profesionales/servicios-asignados.cshtml");
    }

    [HttpPost(RutaServiciosProfesional + "/guardar")]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarServicioAsignado(int profesionalId, int servicioId,
        string? precioPersonalizado, CancellationToken ct)
    {
        var volver = RedirectToAction(nameof(ServiciosAsignados), new { profesionalId });
        var profesional = await _context.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProfesional == profesionalId, ct);
        var servicio = await _context.Servicios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdServicio == servicioId, ct);
        if (profesional is null || profesional.Estado != "activo" || servicio is null || servicio.Estado != "activo")
        {
            TempData["AsignacionError"] = "El profesional y el servicio deben existir y estar activos.";
            return volver;
        }
        if (!servicio.IdEspecialidad.HasValue || !await _context.ProfesionalEspecialidades.AnyAsync(
            pe => pe.IdProfesional == profesionalId && pe.IdEspecialidad == servicio.IdEspecialidad.Value, ct))
        {
            TempData["AsignacionError"] = "El profesional no tiene registrada la especialidad de este servicio.";
            return volver;
        }
        decimal? precio = null;
        if (!string.IsNullOrWhiteSpace(precioPersonalizado))
        {
            if (!decimal.TryParse(precioPersonalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
                || valor <= 0 || valor > 999999999m || decimal.Round(valor, 2) != valor)
            {
                TempData["AsignacionError"] = "El precio personalizado debe ser mayor que cero, con máximo dos decimales; o déjalo vacío para usar el precio base.";
                return volver;
            }
            precio = valor;
        }
        try
        {
            var asignacion = await _context.ProfesionalServicios.FindAsync(new object[] { profesionalId, servicioId }, ct);
            if (asignacion is null)
            {
                _context.ProfesionalServicios.Add(new ProfesionalServicio
                {
                    IdProfesional = profesionalId, IdServicio = servicioId,
                    PrecioPersonalizado = precio, Activo = true
                });
            }
            else
            {
                asignacion.PrecioPersonalizado = precio;
                asignacion.Activo = true;
            }
            await _context.SaveChangesAsync(ct);
            TempData["AsignacionExito"] = "Asignación guardada correctamente.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al guardar asignación de profesional {ProfesionalId} y servicio {ServicioId}", profesionalId, servicioId);
            TempData["AsignacionError"] = "No se pudo guardar la asignación. Revisa las restricciones de la base de datos.";
        }
        return volver;
    }

    [HttpPost(RutaServiciosProfesional + "/estado")]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoServicioAsignado(int profesionalId, int servicioId, bool activo, CancellationToken ct)
    {
        var volver = RedirectToAction(nameof(ServiciosAsignados), new { profesionalId });
        var asignacion = await _context.ProfesionalServicios.FindAsync(new object[] { profesionalId, servicioId }, ct);
        if (asignacion is null)
        {
            TempData["AsignacionError"] = "La asignación no existe.";
            return volver;
        }
        if (activo)
        {
            var profesional = await _context.Profesionales.AsNoTracking().FirstOrDefaultAsync(p => p.IdProfesional == profesionalId, ct);
            var servicio = await _context.Servicios.AsNoTracking().FirstOrDefaultAsync(s => s.IdServicio == servicioId, ct);
            if (profesional?.Estado != "activo" || servicio?.Estado != "activo" || !servicio.IdEspecialidad.HasValue ||
                !await _context.ProfesionalEspecialidades.AnyAsync(pe => pe.IdProfesional == profesionalId && pe.IdEspecialidad == servicio.IdEspecialidad.Value, ct))
            {
                TempData["AsignacionError"] = "No se puede reactivar: profesional, servicio y especialidad deben ser compatibles y estar activos.";
                return volver;
            }
        }
        asignacion.Activo = activo;
        try
        {
            await _context.SaveChangesAsync(ct);
            TempData["AsignacionExito"] = activo ? "Asignación reactivada." : "Asignación desactivada; se conserva su registro.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error cambiando estado de asignación");
            TempData["AsignacionError"] = "No se pudo actualizar el estado.";
        }
        return volver;
    }
}
