using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

public sealed class ToggleVistasViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string vistaActiva = "lista", IReadOnlyList<ToggleVistaItem>? vistas = null)
    {
        return View(new ToggleVistasViewModel
        {
            VistaActiva = vistaActiva,
            Vistas = vistas ??
            [
                new ToggleVistaItem { Id = "semana", Etiqueta = "Semana", Icono = "calendar_view_week" },
                new ToggleVistaItem { Id = "dia", Etiqueta = "Día", Icono = "today" },
                new ToggleVistaItem { Id = "lista", Etiqueta = "Lista", Icono = "view_list" }
            ]
        });
    }
}
