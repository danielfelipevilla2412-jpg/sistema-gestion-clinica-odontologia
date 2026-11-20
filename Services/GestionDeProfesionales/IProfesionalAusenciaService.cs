using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.DTOs;

namespace SmileTrack_MVC.Services;

public interface IProfesionalAusenciaService
{
    Task<ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>> ObtenerAusenciasAsync(
        int id,
        CancellationToken ct = default);

    Task<ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>> ObtenerCitasAfectadasPorAusenciaAsync(
        int id,
        DateOnly fechaInicio,
        DateOnly fechaFin,
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

    Task<ResultadoReasignacionAusenciaDto> RegistrarAusenciaConReasignacionAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        string motivo,
        int? operadorId = null,
        string? observaciones = null,
        CancellationToken ct = default);
}
