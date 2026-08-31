namespace SmileTrack_MVC.Models.Api.Reportes;

public sealed class ReporteResumenApiDto
{
    public DateTime Desde { get; init; }
    public DateTime Hasta { get; init; }
    public int TotalPacientes { get; init; }
    public int PacientesNuevos { get; init; }
    public int TotalCitas { get; init; }
    public int CitasAtendidas { get; init; }
    public int CitasCanceladas { get; init; }
    public int CitasNoAsistieron { get; init; }
    public decimal IngresosPagados { get; init; }
    public decimal IngresosPendientes { get; init; }
    public double TasaAsistencia { get; init; }
    public List<ReporteSerieApiDto> CitasPorDia { get; init; } = [];
    public List<ReporteServicioApiDto> Servicios { get; init; } = [];
}

public sealed class ReporteSerieApiDto
{
    public DateTime Fecha { get; init; }
    public int Cantidad { get; init; }
}

public sealed class ReporteServicioApiDto
{
    public int IdServicio { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int CantidadCitas { get; init; }
    public decimal PrecioReferencial { get; init; }
}

public sealed class BitacoraApiDto
{
    public int IdAuditoria { get; init; }
    public DateTime Fecha { get; init; }
    public string Accion { get; init; } = string.Empty;
    public string TablaAfectada { get; init; } = string.Empty;
    public int? IdRegistro { get; init; }
    public string? Descripcion { get; init; }
    public string? IpOrigen { get; init; }
    public int? IdUsuario { get; init; }
    public string Usuario { get; init; } = "Sistema";
}

public sealed class BitacoraApiResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public List<BitacoraApiDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public static BitacoraApiResult Ok(List<BitacoraApiDto> items, int total, int page, int pageSize) => new() { Success = true, Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    public static BitacoraApiResult Fail(string message) => new() { Success = false, Message = message };
}
