using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;

namespace SmileTrack_MVC.Services.GestionDeProfesionales;

public sealed class ProfesionalComisionService : IProfesionalComisionService
{
    private readonly AppDbContext _context;

    public ProfesionalComisionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReporteComisionProfesionalDto> CalcularComisionesAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        decimal porcentajeComision = 40,
        CancellationToken ct = default)
    {
        if (fechaFin < fechaInicio)
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la fecha de inicio.");
        if (porcentajeComision is < 0 or > 100)
            throw new InvalidOperationException("El porcentaje de comisión debe estar entre 0 y 100.");

        var profesional = await _context.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProfesional == idProfesional, ct);

        if (profesional is null)
            throw new InvalidOperationException("El profesional especificado no existe.");

        var facturas = await _context.Facturas
            .AsNoTracking()
            .Include(f => f.Detalles)
            .ThenInclude(d => d.Servicio)
            .Where(f => f.IdProfesional == idProfesional
                     && f.FechaFactura >= fechaInicio
                     && f.FechaFactura <= fechaFin
                     && f.Estado != "anulada")
            .ToListAsync(ct);

        if (facturas.Count == 0)
        {
            var citasAtendidas = await _context.Citas
                .AsNoTracking()
                .Include(c => c.Servicio)
                .Where(c => c.IdProfesional == idProfesional
                         && c.FechaHora >= fechaInicio
                         && c.FechaHora <= fechaFin
                         && c.Estado == "Atendida")
                .ToListAsync(ct);

            if (citasAtendidas.Count > 0)
            {
                var gruposCitas = citasAtendidas
                    .GroupBy(c => c.IdServicio)
                    .Select(g =>
                    {
                        var servicio = g.First().Servicio;
                        string nombreServicio = servicio?.Nombre ?? "Servicio Odontológico";
                        decimal precioUnitario = servicio?.Precio ?? 100000m;
                        int cantidad = g.Count();
                        decimal subtotal = cantidad * precioUnitario;
                        decimal honorarios = subtotal * (porcentajeComision / 100m);

                        return new ReporteComisionServicioDto
                        {
                            IdServicio = g.Key ?? 0,
                            NombreServicio = nombreServicio,
                            CantidadAtendida = cantidad,
                            PrecioUnitario = precioUnitario,
                            SubtotalFacturado = subtotal,
                            HonorariosGenerados = honorarios
                        };
                    })
                    .ToList();

                decimal totalFacturado = gruposCitas.Sum(g => g.SubtotalFacturado);
                decimal totalHonorarios = gruposCitas.Sum(g => g.HonorariosGenerados);

                return new ReporteComisionProfesionalDto
                {
                    IdProfesional = idProfesional,
                    NombreProfesional = $"{profesional.Nombres} {profesional.Apellidos}".Trim(),
                    FechaInicio = fechaInicio,
                    FechaFin = fechaFin,
                    TotalCitasAtendidas = citasAtendidas.Count,
                    MontoTotalFacturado = totalFacturado,
                    MontoTotalRecaudado = totalFacturado,
                    PorcentajeComision = porcentajeComision,
                    MontoTotalHonorarios = totalHonorarios,
                    MontoComisionPendiente = 0m,
                    DetalleServicios = gruposCitas
                };
            }
        }

        var detallesPagados = facturas
            .Where(f => f.Estado == "pagada" && f.MontoPagado > 0)
            .SelectMany(f => f.Detalles.Select(d => new { Factura = f, Detalle = d }))
            .ToList();

        var gruposServicio = detallesPagados
            .GroupBy(x => x.Detalle.IdServicio)
            .Select(g =>
            {
                var primerServicio = g.First().Detalle.Servicio;
                string nombreServicio = primerServicio?.Nombre ?? "Servicio Odontológico";
                decimal precioUnitario = g.First().Detalle.PrecioUnitario;
                int cantidad = g.Sum(x => x.Detalle.Cantidad);
                decimal subtotal = g.Sum(x => x.Detalle.SubtotalLinea);
                decimal honorarios = subtotal * (porcentajeComision / 100m);

                return new ReporteComisionServicioDto
                {
                    IdServicio = g.Key ?? 0,
                    NombreServicio = nombreServicio,
                    CantidadAtendida = cantidad,
                    PrecioUnitario = precioUnitario,
                    SubtotalFacturado = subtotal,
                    HonorariosGenerados = honorarios
                };
            })
            .ToList();

        decimal montoTotalFacturado = facturas.Sum(f => f.Total);
        decimal montoTotalRecaudado = facturas.Sum(f => f.MontoPagado);
        decimal montoTotalHonorarios = gruposServicio.Sum(g => g.HonorariosGenerados);

        return new ReporteComisionProfesionalDto
        {
            IdProfesional = idProfesional,
            NombreProfesional = $"{profesional.Nombres} {profesional.Apellidos}".Trim(),
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            TotalCitasAtendidas = facturas.Where(f => f.IdCita.HasValue).Select(f => f.IdCita!.Value).Distinct().Count(),
            MontoTotalFacturado = montoTotalFacturado,
            MontoTotalRecaudado = montoTotalRecaudado,
            PorcentajeComision = porcentajeComision,
            MontoTotalHonorarios = montoTotalHonorarios,
            MontoComisionPendiente = 0m,
            DetalleServicios = gruposServicio
        };
    }
}
