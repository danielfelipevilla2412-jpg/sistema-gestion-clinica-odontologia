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

    Task<ProfesionalApiCollectionResult<HorarioProfesionalApiDto>> ObtenerHorariosAsync(
        int id,
        int? usuarioActualId = null,
        bool esAdministrador = true,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>> ActualizarHorariosAsync(
        int id,
        IReadOnlyCollection<HorarioSemanalApiRequest> horarios,
        int? usuarioActualId,
        bool esAdministrador,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>> ObtenerAusenciasAsync(
        int id,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> CrearAusenciaAsync(
        int id,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> ActualizarAusenciaAsync(
        int id,
        int idAusencia,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default);

    Task<ProfesionalApiOperationResult> EliminarAusenciaAsync(
        int id,
        int idAusencia,
        int? operadorId,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionResult<ServicioProfesionalApiDto>> ObtenerServiciosAsync(
        int id,
        CancellationToken ct = default);
}