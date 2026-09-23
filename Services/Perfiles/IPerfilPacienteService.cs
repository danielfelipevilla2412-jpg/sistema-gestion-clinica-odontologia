using SmileTrack_MVC.Models.DTOs;

namespace SmileTrack_MVC.Services.Perfiles;

public interface IPerfilPacienteService
{
    Task<InfoBasicaPacienteDto?> ObtenerInfoBasicaAsync(int idPaciente, CancellationToken ct = default);
    Task<InfoMedicaPacienteDto?> ObtenerInfoMedicaAsync(int idPaciente, CancellationToken ct = default);
    Task<EstadisticasPacienteDto> ObtenerEstadisticasAsync(int idPaciente, CancellationToken ct = default);
    Task<ResumenCompletoPacienteDto?> ObtenerResumenCompletoAsync(int idPaciente, CancellationToken ct = default);
}