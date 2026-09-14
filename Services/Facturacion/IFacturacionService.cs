using SmileTrack_MVC.Models.Api.Facturacion;

namespace SmileTrack_MVC.Services.Facturacion;

public interface IFacturacionService
{
    Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(string? buscar, string? estado, DateTime? desde, DateTime? hasta, CancellationToken ct);
    Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct);
    Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, CancellationToken ct);
    Task<bool> EliminarAsync(int id, CancellationToken ct);
    Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, CancellationToken ct);
    Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, CancellationToken ct);
}
