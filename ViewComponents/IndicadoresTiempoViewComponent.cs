using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

public sealed class IndicadoresTiempoViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(IReadOnlyList<IndicadorTiempoItem>? indicadores = null)
    {
        return View(new IndicadoresTiempoViewModel
        {
            Indicadores = indicadores ?? []
        });
    }
}
