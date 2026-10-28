using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

public sealed class PanelRendimientoViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(int porcentaje, string etiqueta = "Rendimiento del día")
    {
        var valor = Math.Clamp(porcentaje, 0, 100);
        return View(new PanelRendimientoViewModel
        {
            Porcentaje = valor,
            Etiqueta = etiqueta,
            Mensaje = valor >= 80 ? "Excelente avance" : valor >= 50 ? "Avance en progreso" : "Requiere seguimiento"
        });
    }
}
