using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;

namespace SmileTrack_MVC.Services;

public interface ICitaQueryService
{
    Task<CitasKpiDto> ObtenerKpisGestionAsync(
        DateTime fechaReferencia,
        CancellationToken ct = default);

    Task<(List<Cita> Items, int TotalRecords)> ObtenerAsync(
        int page,
        int pageSize,
        string? role,
        int? idPaciente = null,
        int? idProfesional = null,
        int? idUsuario = null,
        string? search = null,
        string? estado = null,
        DateTime? fecha = null,
        CancellationToken ct = default);

    Task<Cita?> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default);

    Task<PagedResult<Cita>> ObtenerListadoGestionAsync(
        PaginationQuery pagination,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
