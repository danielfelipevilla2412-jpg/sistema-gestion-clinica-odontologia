using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.Facturacion;

public sealed class FacturacionService : IFacturacionService
{
    private readonly AppDbContext _db;
    public FacturacionService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(string? buscar, string? estado, DateTime? desde, DateTime? hasta, CancellationToken ct)
    {
        var q = _db.Facturas.AsNoTracking().Include(x => x.Paciente).AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();
            q = q.Where(x => x.NumeroFactura.Contains(buscar) ||
                (x.Paciente != null && (x.Paciente.Nombres + " " + x.Paciente.Apellidos).Contains(buscar)) ||
                (x.Paciente != null && x.Paciente.Documento.Contains(buscar)));
        }
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(x => x.Estado == estado.Trim().ToLower());
        if (desde.HasValue) q = q.Where(x => x.FechaFactura >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(x => x.FechaFactura < hasta.Value.Date.AddDays(1));

        var invoices = await q.OrderByDescending(x => x.FechaFactura).Select(x => new FacturaListItemDto
        {
            Id = x.IdFactura, Numero = x.NumeroFactura, IdPaciente = x.IdPaciente,
            Paciente = x.Paciente == null ? "Paciente" : x.Paciente.Nombres + " " + x.Paciente.Apellidos,
            Documento = x.Paciente == null ? "N/A" : x.Paciente.Documento,
            Fecha = x.FechaFactura, Subtotal = x.Subtotal, Descuento = x.Descuento, Total = x.Total,
            MontoPagado = x.MontoPagado, Pendiente = Math.Max(x.Total - x.MontoPagado, 0),
            FechaPago = x.FechaPago, Estado = x.Estado, Notas = x.Notas
        }).ToListAsync(ct);

        if (invoices.Count == 0) return invoices;

        var invoiceIds = invoices.Select(x => x.Id).ToArray();
        var serviceRows = await _db.DetallesFactura.AsNoTracking()
            .Where(x => invoiceIds.Contains(x.IdFactura))
            .Select(x => new { x.IdFactura, x.Descripcion })
            .ToListAsync(ct);
        var servicesByInvoice = serviceRows
            .GroupBy(x => x.IdFactura)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(x => x.Descripcion)));

        foreach (var invoice in invoices)
            invoice.Servicios = servicesByInvoice.GetValueOrDefault(invoice.Id, string.Empty);

        return invoices;
    }

    public async Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var x = await _db.Facturas.AsNoTracking().Include(f => f.Paciente).Include(f => f.Detalles).Include(f => f.Pagos).FirstOrDefaultAsync(f => f.IdFactura == id, ct);
        return x == null ? null : Map(x);
    }

    public async Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct);
        ValidarDetalles(request.Detalles);
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, Descuento = d.Descuento, SubtotalLinea = d.Cantidad * (d.PrecioUnitario - d.Descuento) }).ToList();
        var subtotal = detalles.Sum(x => x.Cantidad * x.PrecioUnitario);
        var descuento = detalles.Sum(x => x.Cantidad * x.Descuento);
        var total = detalles.Sum(x => x.SubtotalLinea);
        var f = new Factura { NumeroFactura = await NuevoNumeroAsync(ct), FechaFactura = DateTime.Now, Subtotal = subtotal, Descuento = descuento, Total = total, Estado = "pendiente", IdPaciente = request.IdPaciente, Notas = Clean(request.Notas), GeneradaPor = idUsuario, MontoPagado = 0, Detalles = detalles };
        _db.Facturas.Add(f); await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(f.IdFactura, ct))!;
    }

    public async Task<FacturaDto> CrearDesdeCitaAsync(int idCita, int idUsuario, CancellationToken ct)
    {
        var cita = await _db.Citas
            .Include(x => x.Servicio)
            .FirstOrDefaultAsync(x => x.IdCita == idCita, ct)
            ?? throw new InvalidOperationException("No se encontró la cita indicada.");

        if (!string.Equals(cita.Estado, "atendida", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(cita.Estado, "completada", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solo se pueden facturar citas atendidas o completadas.");

        if (cita.Servicio is null || !cita.IdServicio.HasValue)
            throw new InvalidOperationException("La cita no tiene un servicio con precio asociado.");

        var facturaActivaExiste = await _db.Facturas.AnyAsync(
            x => x.IdCita == idCita && x.Estado.ToLower() != "anulada",
            ct);
        if (facturaActivaExiste)
            throw new InvalidOperationException("La cita ya tiene una factura activa.");

        var precio = cita.Servicio.Precio;
        var factura = new Factura
        {
            NumeroFactura = await NuevoNumeroAsync(ct),
            FechaFactura = DateTime.Now,
            Subtotal = precio,
            Total = precio,
            Estado = "pendiente",
            IdPaciente = cita.IdPaciente,
            IdCita = cita.IdCita,
            IdProfesional = cita.IdProfesional,
            GeneradaPor = idUsuario,
            Detalles =
            [
                new DetalleFactura
                {
                    IdServicio = cita.IdServicio,
                    Descripcion = cita.Servicio.Nombre,
                    Cantidad = 1,
                    PrecioUnitario = precio,
                    SubtotalLinea = precio
                }
            ]
        };

        _db.Facturas.Add(factura);
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(factura.IdFactura, ct))!;
    }

    public async Task<FacturaDto> CrearConPagoAsync(CrearFacturaConPagoRequest request, int idUsuario, CancellationToken ct)
    {
        if (!string.Equals(request.MetodoPago?.Trim(), "efectivo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Este método requiere una pasarela de pago configurada. Por ahora solo se pueden registrar pagos en efectivo.");

        await ValidarPaciente(request.IdPaciente, ct);
        ValidarDetalles(request.Detalles);

        var detalles = request.Detalles.Select(d => new DetalleFactura
        {
            IdServicio = d.IdServicio,
            Descripcion = d.Descripcion.Trim(),
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Descuento = d.Descuento,
            SubtotalLinea = d.Cantidad * (d.PrecioUnitario - d.Descuento)
        }).ToList();
        var subtotal = detalles.Sum(x => x.Cantidad * x.PrecioUnitario);
        var descuento = detalles.Sum(x => x.Cantidad * x.Descuento);
        var total = detalles.Sum(x => x.SubtotalLinea);
        if (request.MontoPagado <= 0)
            throw new InvalidOperationException("El monto del abono debe ser mayor que cero.");
        if (request.MontoPagado > total)
            throw new InvalidOperationException("El monto del abono no puede superar el saldo pendiente de la factura.");

        var fechaPago = DateTime.Now;
        var invoice = new Factura
        {
            NumeroFactura = await NuevoNumeroAsync(ct),
            FechaFactura = fechaPago,
            Subtotal = subtotal,
            Descuento = descuento,
            Total = total,
            Estado = request.MontoPagado == total ? "pagada" : "parcial",
            IdPaciente = request.IdPaciente,
            GeneradaPor = idUsuario,
            MontoPagado = request.MontoPagado,
            FechaPago = fechaPago,
            Detalles = detalles
        };
        invoice.Pagos.Add(new PagoFactura
        {
            Monto = request.MontoPagado,
            MetodoPago = "efectivo",
            Estado = "aprobado",
            Referencia = $"CAJ-{Guid.NewGuid():N}"[..36],
            RegistradoPor = idUsuario,
            FechaUtc = DateTime.UtcNow
        });

        _db.Facturas.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(invoice.IdFactura, ct))!;
    }

    public async Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct); ValidarDetalles(request.Detalles);
        var f = await _db.Facturas.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return null;
        if (f.Estado is "pagada" or "anulada") throw new InvalidOperationException("Solo se pueden editar facturas pendientes o parciales.");
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdFactura = id, IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, Descuento = d.Descuento, SubtotalLinea = d.Cantidad * (d.PrecioUnitario - d.Descuento) }).ToList();
        _db.DetallesFactura.RemoveRange(f.Detalles); f.Detalles = detalles; f.IdPaciente = request.IdPaciente; f.Notas = Clean(request.Notas); f.Subtotal = detalles.Sum(x => x.Cantidad * x.PrecioUnitario); f.Descuento = detalles.Sum(x => x.Cantidad * x.Descuento); f.Total = detalles.Sum(x => x.SubtotalLinea);
        if (f.MontoPagado > f.Total) throw new InvalidOperationException("El nuevo total no puede ser menor que lo ya pagado.");
        f.Estado = f.MontoPagado == 0 ? "pendiente" : f.MontoPagado >= f.Total ? "pagada" : "parcial";
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken ct)
    {
        var f = await _db.Facturas.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return false;
        if (f.MontoPagado > 0 || f.Estado != "pendiente") throw new InvalidOperationException("Solo se pueden eliminar facturas pendientes sin pagos.");
        _db.Facturas.Remove(f); await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, int idUsuario, CancellationToken ct)
    {
        var metodoPago = request.MetodoPago?.Trim().ToLowerInvariant();
        if (metodoPago is not ("efectivo" or "tarjeta" or "transferencia"))
            throw new InvalidOperationException("Este método requiere una pasarela de pago configurada. Usa efectivo, tarjeta o transferencia.");
        if (request.MontoPagado <= 0)
            throw new InvalidOperationException("El monto del pago debe ser mayor que cero.");

        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("No se puede pagar una factura anulada.");
        if (f.MontoPagado + request.MontoPagado > f.Total) throw new InvalidOperationException("El monto pagado no puede superar el saldo pendiente.");

        var pago = new PagoFactura
        {
            IdFactura = id,
            Monto = request.MontoPagado,
            MetodoPago = metodoPago,
            Estado = "aprobado",
            Referencia = $"CAJ-{Guid.NewGuid():N}"[..36],
            RegistradoPor = idUsuario,
            FechaUtc = DateTime.UtcNow
        };

        f.MontoPagado += request.MontoPagado;
        f.FechaPago = DateTime.Now;
        f.Estado = f.MontoPagado == f.Total ? "pagada" : "parcial";
        _db.PagosFacturas.Add(pago);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("La factura cambió mientras registrabas el pago. Actualiza la información e inténtalo de nuevo.");
        }

        return await ObtenerAsync(id, ct);
    }

    public async Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct); if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("La factura ya está anulada.");
        f.Estado = "anulada"; if (!string.IsNullOrWhiteSpace(request?.Motivo)) f.Notas = $"{f.Notas} | Anulada: {request.Motivo.Trim()}".Trim(' ', '|');
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    private async Task ValidarPaciente(int id, CancellationToken ct) { if (!await _db.Pacientes.AnyAsync(x => x.IdPaciente == id && x.Estado == "activo", ct)) throw new InvalidOperationException("El paciente seleccionado no es válido."); }
    private static void ValidarDetalles(IEnumerable<FacturaDetalleRequest> d) { if (!d.Any() || d.Any(x => x.Cantidad <= 0 || x.PrecioUnitario < 0 || x.Descuento < 0 || x.Descuento > x.PrecioUnitario || string.IsNullOrWhiteSpace(x.Descripcion))) throw new InvalidOperationException("La factura debe tener ítems válidos y descuentos no superiores al precio unitario."); }
    private static string? Clean(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
    private async Task<string> NuevoNumeroAsync(CancellationToken ct) { var year = DateTime.Now.Year; var n = await _db.Facturas.CountAsync(x => x.FechaFactura.Year == year, ct) + 1; string v; do { v = $"FAC-{year}-{n:D5}"; n++; } while (await _db.Facturas.AnyAsync(x => x.NumeroFactura == v, ct)); return v; }
    private static FacturaDto Map(Factura x) => new() { Id=x.IdFactura, Numero=x.NumeroFactura, IdPaciente=x.IdPaciente, IdCita=x.IdCita, Paciente=x.Paciente==null?"Paciente":$"{x.Paciente.Nombres} {x.Paciente.Apellidos}", Documento=x.Paciente?.Documento??"N/A", Fecha=x.FechaFactura, Subtotal=x.Subtotal, Descuento=x.Descuento, Total=x.Total, MontoPagado=x.MontoPagado, Pendiente=Math.Max(x.Total-x.MontoPagado,0), Estado=x.Estado, Notas=x.Notas, FechaPago=x.FechaPago, Detalles=x.Detalles.Select(d=>new FacturaDetalleDto { Id=d.IdDetalle, IdServicio=d.IdServicio, Descripcion=d.Descripcion, Cantidad=d.Cantidad, PrecioUnitario=d.PrecioUnitario, Descuento=d.Descuento, Subtotal=d.SubtotalLinea }).ToList(), Pagos=x.Pagos.OrderBy(p=>p.FechaUtc).Select(p=>new FacturaPagoDto { Id=p.IdPago, Monto=p.Monto, MetodoPago=p.MetodoPago, Estado=p.Estado, Referencia=p.Referencia, FechaUtc=p.FechaUtc }).ToList() };
}
