using SmileTrack_MVC.Models.Api.Reportes;

namespace SmileTrack_MVC.Services;

public interface IReportesApiService
{
    Task<ReporteResumenApiDto> ObtenerResumenAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default);
    Task<BitacoraApiResult> ObtenerBitacoraAsync(string? usuario, string? accion, DateTime? desde, DateTime? hasta, int page, int pageSize, CancellationToken ct = default);
}
