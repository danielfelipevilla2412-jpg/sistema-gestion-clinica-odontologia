namespace SmileTrack_MVC.Models.ViewModels.Components;

public sealed class ToggleVistasViewModel
{
    public string VistaActiva { get; init; } = "lista";
    public IReadOnlyList<ToggleVistaItem> Vistas { get; init; } = [];
}

public sealed class ToggleVistaItem
{
    public string Id { get; init; } = string.Empty;
    public string Etiqueta { get; init; } = string.Empty;
    public string Icono { get; init; } = "view_list";
}

public sealed class PanelRendimientoViewModel
{
    public int Porcentaje { get; init; }
    public string Etiqueta { get; init; } = "Rendimiento del día";
    public string Mensaje { get; init; } = string.Empty;
}

public sealed class IndicadoresTiempoViewModel
{
    public IReadOnlyList<IndicadorTiempoItem> Indicadores { get; init; } = [];
}

public sealed class IndicadorTiempoItem
{
    public string Estado { get; init; } = string.Empty;
    public string Etiqueta { get; init; } = string.Empty;
    public string Clase { get; init; } = string.Empty;
}
