using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Contrato base para la lógica de negocio de profesionales.
/// La implementación concreta encapsula validaciones, persistencia
/// y estadísticas compartidas entre la API REST y las vistas MVC.
/// </summary>
public interface IProfesionalService
{
    Task<ProfesionalesApiResult> ObtenerAsync(
        int page,
        int pageSize,
        string? search,
        string? especialidad,
        string? estado,
        CancellationToken ct = default);

    Task<(List<Profesional> Items, PagedResult<Profesional> Paginacion, ProfesionalesStats Stats)>
        ObtenerVistaMVCAsync(
            PaginationQuery q,
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