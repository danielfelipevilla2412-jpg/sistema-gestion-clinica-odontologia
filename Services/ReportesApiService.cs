using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Reportes;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

public sealed class ReportesApiService(AppDbContext context, ILogger<ReportesApiService> logger) : IReportesApiService
{
    private readonly AppDbContext _context = context;
    private readonly ILogger<ReportesApiService> _logger = logger;

    public async Task<ReporteResumenApiDto> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        DateTime start = (desde ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        DateTime endExclusive = (hasta ?? DateTime.UtcNow.Date).Date.AddDays(1);
        if (endExclusive <= start) throw new ArgumentException("La fecha final debe ser posterior a la fecha inicial.");

        var citas = _context.Citas.AsNoTracking().Where(c => c.FechaHora >= start && c.FechaHora < endExclusive);
        int totalCitas = await citas.CountAsync(ct);
        int atendidas = await citas.CountAsync(c => c.Estado == "Atendida" || c.Estado == "Completada", ct);
        int canceladas = await citas.CountAsync(c => c.Estado == "Cancelada", ct);
        int noAsistieron = await citas.CountAsync(c => c.Estado == "No asistio" || c.Estado == "No Asistió", ct);
        decimal pagados = await _context.Facturas.AsNoTracking().Where(f => f.FechaFactura >= start && f.FechaFactura < endExclusive && f.Estado == "pagada").SumAsync(f => (decimal?)f.Total, ct) ?? 0m;
        decimal pendientes = await _context.Facturas.AsNoTracking().Where(f => f.FechaFactura >= start && f.FechaFactura < endExclusive && f.Estado != "pagada" && f.Estado != "anulada").SumAsync(f => (decimal?)f.Total, ct) ?? 0m;
        int nuevos = await _context.Pacientes.AsNoTracking().CountAsync(p => p.FechaRegistro >= start && p.FechaRegistro < endExclusive, ct);
        int totalPacientes = await _context.Pacientes.AsNoTracking().CountAsync(ct);

        var citasPorDia = await citas.GroupBy(c => c.FechaHora.Date).Select(g => new ReporteSerieApiDto { Fecha = g.Key, Cantidad = g.Count() }).OrderBy(x => x.Fecha).ToListAsync(ct);
        var servicios = await citas.Where(c => c.IdServicio != null).GroupBy(c => new { c.IdServicio, Nombre = c.Servicio != null ? c.Servicio.Nombre : "Sin servicio", Precio = c.Servicio != null ? c.Servicio.Precio : 0m }).Select(g => new ReporteServicioApiDto { IdServicio = g.Key.IdServicio!.Value, Nombre = g.Key.Nombre, CantidadCitas = g.Count(), PrecioReferencial = g.Key.Precio }).OrderByDescending(x => x.CantidadCitas).ToListAsync(ct);

        return new ReporteResumenApiDto { Desde = start, Hasta = endExclusive.AddTicks(-1), TotalPacientes = totalPacientes, PacientesNuevos = nuevos, TotalCitas = totalCitas, CitasAtendidas = atendidas, CitasCanceladas = canceladas, CitasNoAsistieron = noAsistieron, IngresosPagados = pagados, IngresosPendientes = pendientes, TasaAsistencia = totalCitas == 0 ? 0 : Math.Round(atendidas * 100d / totalCitas, 2), CitasPorDia = citasPorDia, Servicios = servicios };
    }

    public async Task<BitacoraApiResult> ObtenerBitacoraAsync(string? usuario, string? accion, DateTime? desde, DateTime? hasta, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            IQueryable<Auditoria> query = _context.Auditorias.AsNoTracking().Include(a => a.Usuario);
            if (!string.IsNullOrWhiteSpace(usuario)) { string text = usuario.Trim(); query = query.Where(a => a.Usuario != null && (a.Usuario.Nombre.Contains(text) || a.Usuario.Apellidos.Contains(text) || a.Usuario.Correo.Contains(text))); }
            if (!string.IsNullOrWhiteSpace(accion)) { string value = accion.Trim(); query = query.Where(a => a.Accion == value); }
            if (desde.HasValue) query = query.Where(a => a.Fecha >= desde.Value.Date);
            if (hasta.HasValue) query = query.Where(a => a.Fecha < hasta.Value.Date.AddDays(1));
            int total = await query.CountAsync(ct);
            var data = await query.OrderByDescending(a => a.Fecha).Skip((page - 1) * pageSize).Take(pageSize).Select(a => new BitacoraApiDto { IdAuditoria = a.IdAuditoria, Fecha = a.Fecha, Accion = a.Accion, TablaAfectada = a.TablaAfectada, IdRegistro = a.IdRegistro, Descripcion = a.Descripcion, IpOrigen = a.IpOrigen, IdUsuario = a.IdUsuario, Usuario = a.Usuario == null ? "Sistema" : (a.Usuario.Nombre + " " + a.Usuario.Apellidos).Trim() }).ToListAsync(ct);
            return BitacoraApiResult.Ok(data, total, page, pageSize);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Error consultando bitácora."); return BitacoraApiResult.Fail("No fue posible consultar la bitácora."); }
    }
}
