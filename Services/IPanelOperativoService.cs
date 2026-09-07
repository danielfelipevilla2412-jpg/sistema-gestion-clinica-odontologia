using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public interface IPanelOperativoService
{
    Task<PanelOperativoViewModel> ObtenerAsync(
        CancellationToken ct = default);
}
