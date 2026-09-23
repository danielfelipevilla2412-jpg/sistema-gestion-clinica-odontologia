using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

/// <summary>
/// ViewComponent para mostrar panel de accesos rápidos
/// Uso: @await Component.InvokeAsync("AccesosRapidos", new { accesos = listaAccesos })
/// </summary>
public class AccesosRapidosViewComponent : ViewComponent
{
    /// <summary>
    /// Invoca el componente con una lista de accesos rápidos
    /// </summary>
    /// <param name="accesos">Lista de accesos rápidos</param>
    /// <param name="titulo">Título opcional del panel</param>
    /// <param name="columnas">Número de columnas (auto, 2, 3, 4)</param>
    public IViewComponentResult Invoke(
        List<AccesoRapidoItem> accesos,
        string? titulo = null,
        string columnas = "auto")
    {
        var viewModel = new AccesosRapidosViewModel
        {
            Accesos = accesos ?? new List<AccesoRapidoItem>(),
            Titulo = titulo,
            Columnas = columnas
        };

        return View(viewModel);
    }
}
