using SmileTrack_MVC.Models.Api.Profesionales;

namespace SmileTrack_MVC.Services;

public interface IProfesionalHorarioService
{
    Task<ProfesionalApiCollectionResult<HorarioProfesionalApiDto>> ObtenerAsync(
        int id,
        int? usuarioActualId = null,
        bool esAdministrador = true,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>> ActualizarAsync(
        int id,
        IReadOnlyCollection<HorarioSemanalApiRequest> horarios,
        int? usuarioActualId,
        bool esAdministrador,
        CancellationToken ct = default);
}
