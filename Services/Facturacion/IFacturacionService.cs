using SmileTrack_MVC.Models.Api.Facturacion;

namespace SmileTrack_MVC.Services.Facturacion;

public interface IFacturacionService
{
    Task<IReadOnlyList<FacturaListItemDto>> ListarAsync(string? buscar, string? estado, DateTime? desde, DateTime? hasta, int? idCita, int? idProfesional, CancellationToken ct);
    Task<FacturaDto?> ObtenerAsync(int id, CancellationToken ct);
    Task<CitaFacturacionDto?> ObtenerContextoCitaAsync(int idCita, CancellationToken ct);
    Task<FacturaDto> CrearAsync(CrearFacturaRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto> CrearDesdeCitaAsync(int idCita, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> ActualizarAsync(int id, ActualizarFacturaRequest request, int idUsuario, CancellationToken ct);
    Task<bool> EliminarAsync(int id, int idUsuario, CancellationToken ct);
    Task<IReadOnlyList<PagoDto>?> ListarPagosAsync(int id, CancellationToken ct);
    Task<FacturaDto?> RegistrarPagoAsync(int id, RegistrarPagoRequest request, int idUsuario, CancellationToken ct);
    Task<FacturaDto?> AnularAsync(int id, AnularFacturaRequest? request, int idUsuario, CancellationToken ct);
}
