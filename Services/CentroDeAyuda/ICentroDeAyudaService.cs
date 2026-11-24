using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Models.Api;

namespace SmileTrack_MVC.Services.CentroDeAyuda;

public interface ICentroDeAyudaService
{
    Task<(IReadOnlyList<CentroAyudaTicketDto> Items, int TotalCount)> ObtenerTicketsAsync(
        int page, int pageSize, string? search, string? estado, string? categoria, CancellationToken ct);

    Task<CentroAyudaTicketDto?> ObtenerTicketAsync(int id, CancellationToken ct);

    Task<(IReadOnlyList<ChatbotTicketSummary> Items, int TotalCount)> ObtenerTicketsUsuarioAsync(
        int usuarioId, int page, int pageSize, CancellationToken ct);

    Task<ChatbotTicketSummary?> ObtenerTicketUsuarioAsync(
        string referencia, int usuarioId, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error)> CrearTicketAsync(
        CentroAyudaTicketRequest request, int usuarioId, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> ActualizarTicketAsync(
        int id, CentroAyudaTicketUpdateRequest request, int usuarioId, CancellationToken ct);

    Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> CambiarEstadoAsync(
        int id, string estado, int usuarioId, CancellationToken ct);

    Task<(bool Success, string? Error, int StatusCode)> EliminarTicketAsync(int id, CancellationToken ct);
}
