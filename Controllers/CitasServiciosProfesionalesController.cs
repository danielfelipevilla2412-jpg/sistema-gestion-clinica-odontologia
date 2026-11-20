using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;

namespace SmileTrack_MVC.Controllers;

/// <summary>Catálogo de combinaciones válidas para el formulario de citas.</summary>
[Authorize(Roles = "Administrador,Recepcionista")]
public sealed class CitasServiciosProfesionalesController : Controller
{
    private readonly AppDbContext _context;

    public CitasServiciosProfesionalesController(AppDbContext context)
        => _context = context;

    [HttpGet("gestion-de-citas/api/asignaciones-profesionales")]
    public async Task<IActionResult> ObtenerAsignaciones(CancellationToken ct)
    {
        var asignaciones = await (
            from ps in _context.ProfesionalServicios.AsNoTracking()
            join p in _context.Profesionales.AsNoTracking()
                on ps.IdProfesional equals p.IdProfesional
            join s in _context.Servicios.AsNoTracking()
                on ps.IdServicio equals s.IdServicio
            where ps.Activo
                && p.Estado == "activo"
                && s.Estado == "activo"
                && s.IdEspecialidad.HasValue
                && _context.ProfesionalEspecialidades.Any(pe =>
                    pe.IdProfesional == ps.IdProfesional &&
                    pe.IdEspecialidad == s.IdEspecialidad.Value)
            select new
            {
                idProfesional = ps.IdProfesional,
                idServicio = ps.IdServicio,
                precioAplicado = ps.PrecioPersonalizado ?? s.Precio,
                precioPersonalizado = ps.PrecioPersonalizado.HasValue
            })
            .Distinct()
            .ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";

        return Ok(asignaciones);
    }
}
