using SmileTrack_MVC.Models.Api.Pacientes;

namespace SmileTrack_MVC.Services;

public interface IPacienteService
{
    Task<PacientesApiResult> ObtenerAsync(
        string? search,
        string? estado,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PacienteApiDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default);

    Task<PacienteApiOperationResult> CrearAsync(
        PacienteApiCreateDto dto,
        CancellationToken ct = default);

    Task<PacienteApiOperationResult> ActualizarAsync(
        int id,
        PacienteApiUpdateDto dto,
        CancellationToken ct = default);

    Task<PacienteApiOperationResult> CambiarEstadoAsync(
        int id,
        string estado,
        CancellationToken ct = default);
}