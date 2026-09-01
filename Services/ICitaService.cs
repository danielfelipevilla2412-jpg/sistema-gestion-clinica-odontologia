using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Contrato base para la lógica de negocio de citas.
/// La implementación concreta debe encapsular validaciones, ownership y persistencia.
/// </summary>
public interface ICitaService
{
    Task<List<Cita>> ObtenerAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Cita?> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default);

    Task<Cita> CrearAsync(
        CitaApiRequest request,
        CancellationToken ct = default);

    Task<Cita?> ActualizarAsync(
        int id,
        CitaApiUpdateDto request,
        CancellationToken ct = default);

    Task<Cita?> CambiarEstadoAsync(
        int id,
        string nuevoEstado,
        CancellationToken ct = default);

    Task<Cita?> ActualizarNotasAsync(
        int id,
        string notas,
        CancellationToken ct = default);

    Task<bool> CancelarAsync(
        int id,
        CancellationToken ct = default);

    Task<bool> HayConflictoHorarioAsync(
        int? idProfesional,
        DateTime fechaHora,
        int? idCitaExcluir = null,
        CancellationToken ct = default);
}
