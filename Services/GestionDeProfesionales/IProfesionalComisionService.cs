using SmileTrack_MVC.Models.DTOs;

namespace SmileTrack_MVC.Services;

public interface IProfesionalComisionService
{
    Task<ReporteComisionProfesionalDto> CalcularComisionesAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        decimal porcentajeComision = 40,
        CancellationToken ct = default);
}
