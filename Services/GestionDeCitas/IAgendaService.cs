using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public interface IAgendaService
{
    Task<AgendaViewModel> ObtenerAgendaAsync(
        DateTime? weekStart,
        int? professionalId,
        int? officeId,
        CancellationToken cancellationToken = default);
}
