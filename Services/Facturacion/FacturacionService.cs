using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Facturacion;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.Facturacion;

public sealed class FacturacionService : IFacturacionService
{
    private readonly AppDbContext _db;
    public FacturacionService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(string? buscar, string? estado, DateTime? desde, DateTime? hasta, int? idCita, int? idProfesional, CancellationToken ct)
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
        if (idCita.HasValue) q = q.Where(x => x.IdCita == idCita.Value);
        if (idProfesional.HasValue) q = q.Where(x => x.IdProfesional == idProfesional.Value);
        if (desde.HasValue) q = q.Where(x => x.FechaFactura >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(x => x.FechaFactura < hasta.Value.Date.AddDays(1));

        return await q.OrderByDescending(x => x.FechaFactura).Select(x => new FacturaListItemDto
        {
            Id = x.IdFactura, Numero = x.NumeroFactura, IdPaciente = x.IdPaciente,
            IdCita = x.IdCita, IdProfesional = x.IdProfesional,
            Paciente = x.Paciente == null ? "Paciente" : x.Paciente.Nombres + " " + x.Paciente.Apellidos,
            Documento = x.Paciente == null ? "N/A" : x.Paciente.Documento,
            Fecha = x.FechaFactura, Subtotal = x.Subtotal, Total = x.Total,
            MontoPagado = x.MontoPagado, Pendiente = Math.Max(x.Total - x.MontoPagado, 0),
            Estado = x.Estado, Notas = x.Notas
        }).ToListAsync(ct);
    }

    public async Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct)
    {
        var x = await _db.Facturas.AsNoTracking().Include(f => f.Paciente).Include(f => f.Detalles).Include(f => f.Pagos).FirstOrDefaultAsync(f => f.IdFactura == id, ct);
        return x == null ? null : Map(x);
    }

    public async Task<CitaFacturacionDto?> ObtenerContextoCitaAsync(int idCita, CancellationToken ct)
    {
        var cita = await _db.Citas.AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Usuario)
            .Include(c => c.Facturas)
            .FirstOrDefaultAsync(c => c.IdCita == idCita, ct);

        if (cita is null)
            return null;

        var facturaActiva = cita.Facturas.FirstOrDefault(f => !string.Equals(f.Estado, "anulada", StringComparison.OrdinalIgnoreCase));
        var profesional = cita.Profesional?.NombreProfesional ?? "Sin profesional asignado";
        var estado = cita.Estado?.Trim() ?? string.Empty;
        var puedeFacturar = EsEstadoFacturable(estado) && facturaActiva is null && cita.Paciente is not null && cita.Servicio is not null;

        return new CitaFacturacionDto
        {
            IdCita = cita.IdCita,
            IdPaciente = cita.IdPaciente,
            Paciente = cita.Paciente?.NombresCompleto ?? "Paciente",
            IdServicio = cita.IdServicio,
            Servicio = cita.Servicio?.Nombre ?? "Servicio no definido",
            PrecioServicio = cita.Servicio?.Precio ?? 0m,
            IdProfesional = cita.IdProfesional,
            Profesional = profesional,
            FechaHora = cita.FechaHora,
            Estado = estado,
            IdFactura = facturaActiva?.IdFactura,
            EstadoFactura = facturaActiva?.Estado,
            PuedeFacturar = puedeFacturar
        };
    }

    public async Task<FacturaDto> CrearDesdeCitaAsync(int idCita, int idUsuario, CancellationToken ct)
    {
        var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        var cita = await _db.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .FirstOrDefaultAsync(c => c.IdCita == idCita, ct);

        if (cita is null)
            throw new InvalidOperationException("La cita no existe.");
        if (!EsEstadoFacturable(cita.Estado))
            throw new InvalidOperationException("Solo se pueden facturar citas atendidas o completadas.");
        if (cita.Paciente is null || !string.Equals(cita.Paciente.Estado, "activo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La cita no tiene un paciente activo válido.");
        if (cita.Servicio is null || cita.IdServicio is null)
            throw new InvalidOperationException("La cita no tiene un servicio válido.");
        if (await _db.Facturas.AnyAsync(f => f.IdCita == idCita && f.Estado != "anulada", ct))
            throw new InvalidOperationException("La cita ya tiene una factura activa.");

        var total = cita.Servicio.Precio;
        var factura = new Factura
        {
            NumeroFactura = await NuevoNumeroAsync(ct),
            FechaFactura = DateTime.Now,
            Subtotal = total,
            Total = total,
            Estado = "pendiente",
            IdPaciente = cita.IdPaciente,
            IdCita = cita.IdCita,
            IdProfesional = cita.IdProfesional,
            GeneradaPor = idUsuario,
            MontoPagado = 0,
            Detalles =
            [
                new DetalleFactura
                {
                    IdServicio = cita.IdServicio,
                    Descripcion = cita.Servicio.Nombre,
                    Cantidad = 1,
                    PrecioUnitario = total,
                    SubtotalLinea = total
                }
            ]
        };

        _db.Facturas.Add(factura);
        AgregarNotificacion(factura.IdPaciente, factura.IdCita, "Factura creada", $"Se creó la factura {factura.NumeroFactura} por {factura.Total:C}.");
        try
        {
            await _db.SaveChangesAsync(ct);
            AgregarAuditoria(idUsuario, factura.IdFactura, "INSERT", null, $"Factura {factura.NumeroFactura} creada desde cita {idCita}.");
            await _db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_Factura_Cita_Activa", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("La cita ya tiene una factura activa.", ex);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        return (await ObtenerAsync(factura.IdFactura, ct))!;
    }

    public async Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct);
        ValidarDetalles(request.Detalles);
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, SubtotalLinea = d.Cantidad * d.PrecioUnitario }).ToList();
        var total = detalles.Sum(x => x.SubtotalLinea);
        var f = new Factura { NumeroFactura = await NuevoNumeroAsync(ct), FechaFactura = DateTime.Now, Subtotal = total, Total = total, Estado = "pendiente", IdPaciente = request.IdPaciente, IdCita = request.IdCita, IdProfesional = request.IdProfesional, Notas = Clean(request.Notas), GeneradaPor = idUsuario, MontoPagado = 0, Detalles = detalles };
        _db.Facturas.Add(f);
        AgregarNotificacion(f.IdPaciente, f.IdCita, "Factura creada", $"Se creó la factura {f.NumeroFactura} por {f.Total:C}.");
        await _db.SaveChangesAsync(ct);
        AgregarAuditoria(idUsuario, f.IdFactura, "INSERT", null, $"Factura {f.NumeroFactura} creada.");
        await _db.SaveChangesAsync(ct);
        return (await ObtenerAsync(f.IdFactura, ct))!;
    }

    public async Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, int idUsuario, CancellationToken ct)
    {
        await ValidarPaciente(request.IdPaciente, ct); ValidarDetalles(request.Detalles);
        var f = await _db.Facturas.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return null;
        if (f.Estado is "pagada" or "anulada") throw new InvalidOperationException("Solo se pueden editar facturas pendientes o parciales.");
        var detalles = request.Detalles.Select(d => new DetalleFactura { IdFactura = id, IdServicio = d.IdServicio, Descripcion = d.Descripcion.Trim(), Cantidad = d.Cantidad, PrecioUnitario = d.PrecioUnitario, SubtotalLinea = d.Cantidad * d.PrecioUnitario }).ToList();
        _db.DetallesFactura.RemoveRange(f.Detalles); f.Detalles = detalles; f.IdPaciente = request.IdPaciente; f.Notas = Clean(request.Notas); f.Subtotal = detalles.Sum(x => x.SubtotalLinea); f.Total = f.Subtotal;
        if (f.MontoPagado > f.Total) throw new InvalidOperationException("El nuevo total no puede ser menor que lo ya pagado.");
        f.Estado = f.MontoPagado == 0 ? "pendiente" : f.MontoPagado >= f.Total ? "pagada" : "parcial";
        await _db.SaveChangesAsync(ct);
        AgregarAuditoria(idUsuario, f.IdFactura, "UPDATE", null, $"Factura {f.NumeroFactura} actualizada.");
        await _db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<bool> EliminarAsync(int id, int idUsuario, CancellationToken ct)
    {
        var f = await _db.Facturas.Include(x => x.Detalles).FirstOrDefaultAsync(x => x.IdFactura == id, ct);
        if (f == null) return false;
        if (f.MontoPagado > 0 || f.Estado != "pendiente") throw new InvalidOperationException("Solo se pueden eliminar facturas pendientes sin pagos.");
        _db.Facturas.Remove(f); await _db.SaveChangesAsync(ct);
        AgregarAuditoria(idUsuario, id, "DELETE", null, $"Factura {f.NumeroFactura} eliminada.");
        await _db.SaveChangesAsync(ct); return true;
    }

    public async Task<IReadOnlyList<PagoDto>?> ListarPagosAsync(int id, CancellationToken ct)
    {
        var existe = await _db.Facturas.AnyAsync(f => f.IdFactura == id, ct);
        if (!existe) return null;
        return await _db.Pagos.AsNoTracking().Where(p => p.IdFactura == id).OrderByDescending(p => p.FechaPago).Select(p => new PagoDto
        {
            Id = p.IdPago,
            Monto = p.Monto,
            FechaPago = p.FechaPago,
            MetodoPago = p.MetodoPago,
            Referencia = p.Referencia,
            RegistradoPor = p.RegistradoPor,
            Observaciones = p.Observaciones
        }).ToListAsync(ct);
    }

    public async Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, int idUsuario, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct); if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("No se puede pagar una factura anulada.");
        if (f.Estado == "pagada") throw new InvalidOperationException("La factura ya está pagada.");
        if (f.MontoPagado + request.MontoPagado > f.Total) throw new InvalidOperationException("El monto pagado no puede superar el total.");
        var pago = new Pago
        {
            IdFactura = id,
            Monto = request.MontoPagado,
            FechaPago = DateTime.Now,
            MetodoPago = string.IsNullOrWhiteSpace(request.MetodoPago) ? "efectivo" : request.MetodoPago.Trim().ToLowerInvariant(),
            Referencia = Clean(request.Referencia),
            RegistradoPor = idUsuario,
            Observaciones = Clean(request.Observaciones)
        };
        _db.Pagos.Add(pago);
        f.MontoPagado += request.MontoPagado; f.FechaPago = pago.FechaPago; f.Estado = f.MontoPagado >= f.Total ? "pagada" : "parcial";
        AgregarNotificacion(f.IdPaciente, f.IdCita, f.Estado == "pagada" ? "Factura pagada" : "Pago registrado", $"Se registró un pago de {request.MontoPagado:C} para la factura {f.NumeroFactura}.");
        await _db.SaveChangesAsync(ct);
        AgregarAuditoria(idUsuario, f.IdFactura, "PAYMENT", null, $"Pago registrado para factura {f.NumeroFactura}: {request.MontoPagado:C}.");
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    public async Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, int idUsuario, CancellationToken ct)
    {
        var f = await _db.Facturas.FirstOrDefaultAsync(x => x.IdFactura == id, ct); if (f == null) return null;
        if (f.Estado == "anulada") throw new InvalidOperationException("La factura ya está anulada.");
        f.Estado = "anulada"; if (!string.IsNullOrWhiteSpace(request?.Motivo)) f.Notas = $"{f.Notas} | Anulada: {request.Motivo.Trim()}".Trim(' ', '|');
        AgregarNotificacion(f.IdPaciente, f.IdCita, "Factura anulada", $"La factura {f.NumeroFactura} fue anulada.");
        await _db.SaveChangesAsync(ct);
        AgregarAuditoria(idUsuario, f.IdFactura, "VOID", null, $"Factura {f.NumeroFactura} anulada.");
        await _db.SaveChangesAsync(ct); return await ObtenerAsync(id, ct);
    }

    private async Task ValidarPaciente(int id, CancellationToken ct) { if (!await _db.Pacientes.AnyAsync(x => x.IdPaciente == id && x.Estado == "activo", ct)) throw new InvalidOperationException("El paciente seleccionado no es válido."); }
    private static void ValidarDetalles(IEnumerable<FacturaDetalleRequest> d) { if (!d.Any() || d.Any(x => x.Cantidad <= 0 || x.PrecioUnitario < 0 || string.IsNullOrWhiteSpace(x.Descripcion))) throw new InvalidOperationException("La factura debe tener ítems válidos."); }
    private static string? Clean(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
    private void AgregarNotificacion(int idPaciente, int? idCita, string titulo, string contenido)
    {
        _db.Notificaciones.Add(new Notificacion
        {
            IdPaciente = idPaciente,
            IdCita = idCita,
            Tipo = "factura",
            Titulo = titulo,
            Contenido = contenido,
            Canal = "interno",
            Estado = "pendiente",
            CreadaEn = DateTime.Now
        });
    }
    private void AgregarAuditoria(int idUsuario, int idRegistro, string accion, string? datosAnteriores, string descripcion)
    {
        _db.Auditorias.Add(new Auditoria
        {
            IdUsuario = idUsuario,
            TablaAfectada = "Factura",
            IdRegistro = idRegistro,
            Accion = accion,
            DatosAnteriores = datosAnteriores,
            Descripcion = descripcion,
            Fecha = DateTime.Now
        });
    }
    private async Task<string> NuevoNumeroAsync(CancellationToken ct) { var year = DateTime.Now.Year; var n = await _db.Facturas.CountAsync(x => x.FechaFactura.Year == year, ct) + 1; string v; do { v = $"FAC-{year}-{n:D5}"; n++; } while (await _db.Facturas.AnyAsync(x => x.NumeroFactura == v, ct)); return v; }
    private static bool EsEstadoFacturable(string? estado) => estado?.Trim().ToLowerInvariant() is "atendida" or "completada" or "finalizada";
    private static FacturaDto Map(Factura x) => new() { Id=x.IdFactura, Numero=x.NumeroFactura, IdPaciente=x.IdPaciente, IdCita=x.IdCita, IdProfesional=x.IdProfesional, Paciente=x.Paciente==null?"Paciente":$"{x.Paciente.Nombres} {x.Paciente.Apellidos}", Documento=x.Paciente?.Documento??"N/A", Fecha=x.FechaFactura, Subtotal=x.Subtotal, Total=x.Total, MontoPagado=x.MontoPagado, Pendiente=Math.Max(x.Total-x.MontoPagado,0), Estado=x.Estado, Notas=x.Notas, FechaPago=x.FechaPago, Detalles=x.Detalles.Select(d=>new FacturaDetalleDto { Id=d.IdDetalle, IdServicio=d.IdServicio, Descripcion=d.Descripcion, Cantidad=d.Cantidad, PrecioUnitario=d.PrecioUnitario, Subtotal=d.SubtotalLinea }).ToList(), Pagos=x.Pagos.Select(p=>new PagoDto { Id=p.IdPago, Monto=p.Monto, FechaPago=p.FechaPago, MetodoPago=p.MetodoPago, Referencia=p.Referencia, RegistradoPor=p.RegistradoPor, Observaciones=p.Observaciones }).ToList() };
}
