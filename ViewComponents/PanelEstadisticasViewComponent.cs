using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

/// <summary>
/// ViewComponent para mostrar panel de estadísticas/KPIs con animación
/// Uso: @await Component.InvokeAsync("PanelEstadisticas", new { estadisticas = listaStats })
/// </summary>
public class PanelEstadisticasViewComponent : ViewComponent
{
    /// <summary>
    /// Invoca el componente con una lista de estadísticas
    /// </summary>
    /// <param name="estadisticas">Lista de estadísticas a mostrar</param>
    /// <param name="titulo">Título opcional del panel</param>
    /// <param name="claseAdicional">Clase CSS adicional</param>
    public IViewComponentResult Invoke(
        List<EstadisticaItem> estadisticas, 
        string? titulo = null,
        string? claseAdicional = null)
    {
        var viewModel = new PanelEstadisticasViewModel
        {
            Estadisticas = estadisticas ?? new List<EstadisticaItem>(),
            Titulo = titulo,
            ClaseAdicional = claseAdicional
        };

        return View(viewModel);
    }
}
