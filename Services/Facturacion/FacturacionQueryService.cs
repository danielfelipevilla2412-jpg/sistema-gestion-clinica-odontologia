using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.Facturacion;

public sealed class FacturacionQueryService : IFacturacionQueryService
{
    private readonly AppDbContext _db;

    public FacturacionQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(
        string? buscar,
        string? estado,
        DateTime? desde,
        DateTime? hasta,
        int? idCita,
        int? idProfesional,
        CancellationToken ct)
    {
        var query = _db.Facturas
            .AsNoTracking()
            .Include(factura => factura.Paciente)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();
            query = query.Where(factura =>
                factura.NumeroFactura.Contains(buscar) ||
                (factura.Paciente != null &&
                 (factura.Paciente.Nombres + " " + factura.Paciente.Apellidos).Contains(buscar)) ||
                (factura.Paciente != null && factura.Paciente.Documento.Contains(buscar)));
        }

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(factura => factura.Estado == estado.Trim().ToLower());
        if (desde.HasValue)
            query = query.Where(factura => factura.FechaFactura >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(factura => factura.FechaFactura < hasta.Value.Date.AddDays(1));
        if (idCita.HasValue)
            query = query.Where(factura => factura.IdCita == idCita.Value);
        if (idProfesional.HasValue)
            query = query.Where(factura => factura.IdProfesional == idProfesional.Value);

        var invoices = await query
            .OrderByDescending(factura => factura.FechaFactura)
            .Select(factura => new FacturaListItemDto
            {
                Id = factura.IdFactura,
                Numero = factura.NumeroFactura,
                IdPaciente = factura.IdPaciente,
                IdCita = factura.IdCita,
                IdProfesional = factura.IdProfesional,
                Paciente = factura.Paciente == null
                    ? "Paciente"
                    : factura.Paciente.Nombres + " " + factura.Paciente.Apellidos,
                Documento = factura.Paciente == null ? "N/A" : factura.Paciente.Documento,
                Fecha = factura.FechaFactura,
                Subtotal = factura.Subtotal,
                Descuento = factura.Descuento,
                Total = factura.Total,
                MontoPagado = factura.MontoPagado,
                Pendiente = Math.Max(factura.Total - factura.MontoPagado, 0),
                FechaPago = factura.FechaPago,
                Estado = factura.Estado,
                Notas = factura.Notas
            })
            .ToListAsync(ct);

        if (invoices.Count == 0)
            return invoices;

        var invoiceIds = invoices.Select(invoice => invoice.Id).ToArray();
        var serviceRows = await _db.DetallesFactura
            .AsNoTracking()
            .Where(detail => invoiceIds.Contains(detail.IdFactura))
            .Select(detail => new { detail.IdFactura, detail.Descripcion })
            .ToListAsync(ct);
        var servicesByInvoice = serviceRows
            .GroupBy(row => row.IdFactura)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(row => row.Descripcion)));

        foreach (var invoice in invoices)
            invoice.Servicios = servicesByInvoice.GetValueOrDefault(invoice.Id, string.Empty);

        return invoices;
    }

    public async Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var factura = await _db.Facturas
            .AsNoTracking()
            .Include(item => item.Paciente)
            .Include(item => item.Detalles)
            .Include(item => item.Pagos)
            .FirstOrDefaultAsync(item => item.IdFactura == id, ct);

        return factura is null ? null : Map(factura);
    }

    public async Task<IReadOnlyList<PagoDto>?> ListarPagosAsync(int id, CancellationToken ct)
    {
        bool existe = await _db.Facturas.AnyAsync(factura => factura.IdFactura == id, ct);
        if (!existe)
            return null;

        return await _db.Pagos
            .AsNoTracking()
            .Where(pago => pago.IdFactura == id)
            .OrderByDescending(pago => pago.FechaUtc)
            .Select(pago => new PagoDto
            {
                Id = pago.IdPago,
                Monto = pago.Monto,
                FechaPago = pago.FechaUtc.ToLocalTime(),
                MetodoPago = pago.MetodoPago,
                Referencia = pago.Referencia,
                RegistradoPor = pago.RegistradoPor,
                Observaciones = pago.Observaciones
            })
            .ToListAsync(ct);
    }

    private static FacturaDto Map(Factura factura) => new()
    {
        Id = factura.IdFactura,
        Numero = factura.NumeroFactura,
        IdPaciente = factura.IdPaciente,
        IdCita = factura.IdCita,
        IdProfesional = factura.IdProfesional,
        Paciente = factura.Paciente == null
            ? "Paciente"
            : $"{factura.Paciente.Nombres} {factura.Paciente.Apellidos}",
        Documento = factura.Paciente?.Documento ?? "N/A",
        Fecha = factura.FechaFactura,
        Subtotal = factura.Subtotal,
        Descuento = factura.Descuento,
        Total = factura.Total,
        MontoPagado = factura.MontoPagado,
        Pendiente = Math.Max(factura.Total - factura.MontoPagado, 0),
        Estado = factura.Estado,
        Notas = factura.Notas,
        FechaPago = factura.FechaPago,
        Detalles = factura.Detalles.Select(detail => new FacturaDetalleDto
        {
            Id = detail.IdDetalle,
            IdServicio = detail.IdServicio,
            Descripcion = detail.Descripcion,
            Cantidad = detail.Cantidad,
            PrecioUnitario = detail.PrecioUnitario,
            Descuento = detail.Descuento,
            Subtotal = detail.SubtotalLinea
        }).ToList(),
        Pagos = factura.Pagos.Select(pago => new PagoDto
        {
            Id = pago.IdPago,
            Monto = pago.Monto,
            FechaPago = pago.FechaUtc.ToLocalTime(),
            MetodoPago = pago.MetodoPago,
            Referencia = pago.Referencia,
            RegistradoPor = pago.RegistradoPor,
            Observaciones = pago.Observaciones
        }).ToList()
    };
}
