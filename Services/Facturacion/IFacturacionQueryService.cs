using SmileTrack_MVC.Models.Api.Facturacion;

namespace SmileTrack_MVC.Services.Facturacion;

public interface IFacturacionQueryService
{
    Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(
        string? buscar,
        string? estado,
        DateTime? desde,
        DateTime? hasta,
        int? idCita,
        int? idProfesional,
        CancellationToken ct);

    Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<PagoDto>?> ListarPagosAsync(int id, CancellationToken ct);
}
