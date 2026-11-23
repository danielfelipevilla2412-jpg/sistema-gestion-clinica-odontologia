using SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public interface IPacienteNotificacionQueryService
{
    Task<PacienteNotificacionesViewModel> ObtenerNotificacionesAsync(
        int? idUsuario,
        CancellationToken ct = default);

    Task<int> ObtenerConteoNoLeidasAsync(
        int idUsuario,
        CancellationToken ct = default);
}
