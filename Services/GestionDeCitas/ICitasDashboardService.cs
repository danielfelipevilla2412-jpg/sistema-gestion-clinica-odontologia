using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Contrato para obtener los datos del dashboard de citas administrativo.
/// Separa la lógica de agregación del controlador MVC.
/// </summary>
public interface ICitasDashboardService
{
    /// <summary>
    /// Devuelve el ViewModel completo del dashboard calculado a partir de
    /// <paramref name="fechaReferencia"/> (normalmente DateTime.Today).
    /// </summary>
    Task<CitasDashboardViewModel> ObtenerDashboardAsync(
        DateTime          fechaReferencia,
        CancellationToken cancellationToken = default);
}
