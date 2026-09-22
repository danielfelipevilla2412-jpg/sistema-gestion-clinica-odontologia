using Microsoft.AspNetCore.Mvc;

namespace SmileTrack_MVC.ViewComponents;

/// <summary>
/// ViewComponent para buscador en tiempo real con debounce
/// Uso: @await Component.InvokeAsync("BuscadorTiempoReal", new { placeholder = "Buscar...", targetSelector = ".searchable-item" })
/// </summary>
public class BuscadorTiempoRealViewComponent : ViewComponent
{
    /// <summary>
    /// Invoca el componente de búsqueda
    /// </summary>
    /// <param name="placeholder">Texto del placeholder</param>
    /// <param name="targetSelector">Selector CSS de los elementos a buscar</param>
    /// <param name="id">ID del input (opcional, default: searchRealTime)</param>
    public IViewComponentResult Invoke(
        string placeholder = "Buscar...", 
        string targetSelector = ".searchable-item",
        string id = "searchRealTime")
    {
        ViewBag.Placeholder = placeholder;
        ViewBag.TargetSelector = targetSelector;
        ViewBag.InputId = id;
        
        return View();
    }
}
