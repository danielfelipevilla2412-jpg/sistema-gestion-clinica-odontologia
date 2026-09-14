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

        return await q.OrderByDescending(x => x.FechaFactura).Select(x => new FacturaListItemDto
        {
            Id = x.IdFactura, Numero = x.NumeroFactura, IdPaciente = x.IdPaciente,
            Paciente = x.Paciente == null ? "Paciente" : x.Paciente.Nombres + " " + x.Paciente.Apellidos,
            Documento = x.Paciente == null ? "N/A" : x.Paciente.Documento,
            Fecha = x.FechaFactura, Subtotal = x.Subtotal, Total = x.Total,
            MontoPagado = x.MontoPagado, Pendiente = Math.Max(x.Total - x.MontoPagado, 0),
            Estado = x.Estado, Notas = x.Notas
        }).ToListAsync(ct);
    }

    public async Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var x = await _db.Facturas.AsNoTracking().Include(f => f.Paciente).Include(f => f.Detalles).FirstOrDefaultAsync(f => f.IdFactura == id, ct);
        return x == null ? null : Map(x);
    }

    public async Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct);
        ValidarDetalles(request.Detalles);
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, SubtotalLinea = d.Cantidad * d.PrecioUnitario }).ToList();
        var total = detalles.Sum(x => x.SubtotalLinea);
        var f = new Factura { NumeroFactura = await NuevoNumeroAsync(ct), FechaFactura = DateTime.Now, Subtotal = total, Total = total, Estado = "pendiente", IdPaciente = request.IdPaciente, Notas = Clean(request.Notas), GeneradaPor = idUsuario, MontoPagado = 0, Detalles = detalles };
        _db.Facturas.Add(f); await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(f.IdFactura, ct))!;
    }

    public async Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct); ValidarDetalles(request.Detalles);
        var f = await _db.Facturas.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return null;
        if (f.Estado is "pagada" or "anulada") throw new InvalidOperationException("Solo se pueden editar facturas pendientes o parciales.");
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdFactura = id, IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, SubtotalLinea = d.Cantidad * d.PrecioUnitario }).ToList();
        _db.DetallesFactura.RemoveRange(f.Detalles); f.Detalles = detalles; f.IdPaciente = request.IdPaciente; f.Notas = Clean(request.Notas); f.Subtotal = detalles.Sum(x => x.SubtotalLinea); f.Total = f.Subtotal;
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

    public async Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct); if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("No se puede pagar una factura anulada.");
        if (f.MontoPagado + request.MontoPagado > f.Total) throw new InvalidOperationException("El monto pagado no puede superar el total.");
        f.MontoPagado += request.MontoPagado; f.FechaPago = DateTime.Now; f.Estado = f.MontoPagado >= f.Total ? "pagada" : "parcial";
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    public async Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct); if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("La factura ya está anulada.");
        f.Estado = "anulada"; if (!string.IsNullOrWhiteSpace(request?.Motivo)) f.Notas = $"{f.Notas} | Anulada: {request.Motivo.Trim()}".Trim(' ', '|');
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    private async Task ValidarPaciente(int id, CancellationToken ct) { if (!await _db.Pacientes.AnyAsync(x => x.IdPaciente == id && x.Estado == "activo", ct)) throw new InvalidOperationException("El paciente seleccionado no es válido."); }
    private static void ValidarDetalles(IEnumerable<FacturaDetalleRequest> d) { if (!d.Any() || d.Any(x => x.Cantidad <= 0 || x.PrecioUnitario < 0 || string.IsNullOrWhiteSpace(x.Descripcion))) throw new InvalidOperationException("La factura debe tener ítems válidos."); }
    private static string? Clean(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
    private async Task<string> NuevoNumeroAsync(CancellationToken ct) { var year = DateTime.Now.Year; var n = await _db.Facturas.CountAsync(x => x.FechaFactura.Year == year, ct) + 1; string v; do { v = $"FAC-{year}-{n:D5}"; n++; } while (await _db.Facturas.AnyAsync(x => x.NumeroFactura == v, ct)); return v; }
    private static FacturaDto Map(Factura x) => new() { Id=x.IdFactura, Numero=x.NumeroFactura, IdPaciente=x.IdPaciente, Paciente=x.Paciente==null?"Paciente":$"{x.Paciente.Nombres} {x.Paciente.Apellidos}", Documento=x.Paciente?.Documento??"N/A", Fecha=x.FechaFactura, Subtotal=x.Subtotal, Total=x.Total, MontoPagado=x.MontoPagado, Pendiente=Math.Max(x.Total-x.MontoPagado,0), Estado=x.Estado, Notas=x.Notas, FechaPago=x.FechaPago, Detalles=x.Detalles.Select(d=>new FacturaDetalleDto { Id=d.IdDetalle, IdServicio=d.IdServicio, Descripcion=d.Descripcion, Cantidad=d.Cantidad, PrecioUnitario=d.PrecioUnitario, Subtotal=d.SubtotalLinea }).ToList() };
}
