using SmileTrack_MVC.Models.Api.Profesionales;

namespace SmileTrack_MVC.Services;

public interface IProfesionalService
{
    Task<ProfesionalesApiResult> ObtenerAsync(
        int page,
        int pageSize,
        string? search,
        string? especialidad,
        string? estado,
        CancellationToken ct = default);

    Task<ProfesionalApiDto?> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default);

    Task<List<ProfesionalEspecialidadApiDto>> ObtenerEspecialidadesAsync(
        CancellationToken ct = default);

    Task<ProfesionalApiOperationResult> CrearAsync(
        ProfesionalApiRequest request,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default);

    Task<ProfesionalApiOperationResult> ActualizarAsync(
        int id,
        ProfesionalApiRequest request,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default);

    Task<ProfesionalApiOperationResult> CambiarEstadoAsync(
        int id,
        string estado,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default);

    Task<ProfesionalApiOperationResult> DesactivarAsync(
        int id,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default);
}