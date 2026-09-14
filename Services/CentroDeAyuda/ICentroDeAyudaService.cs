using SmileTrack_MVC.Models.Api.CentroDeAyuda;

namespace SmileTrack_MVC.Services.CentroDeAyuda;

public interface ICentroDeAyudaService
{
    Task<(IReadOnlyList<CentroAyudaTicketDto> Items, int TotalCount)> ObtenerTicketsAsync(
        int page, int pageSize, string? search, string? estado, string? categoria, CancellationToken ct);

    Task<CentroAyudaTicketDto?> ObtenerTicketAsync(int id, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error)> CrearTicketAsync(
        CentroAyudaTicketRequest request, int usuarioId, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> ActualizarTicketAsync(
        int id, CentroAyudaTicketUpdateRequest request, int usuarioId, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> CambiarEstadoAsync(
        int id, string estado, int usuarioId, CancellationToken ct);

    Task<(bool Success, string? Error, int StatusCode)> EliminarTicketAsync(int id, CancellationToken ct);
}
