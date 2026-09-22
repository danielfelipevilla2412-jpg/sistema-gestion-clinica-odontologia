namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// Representa un item individual de estadística para el panel de KPIs
/// </summary>
public class EstadisticaItem
{
    /// <summary>
    /// Color del card: blue, green, orange, purple, red
    /// </summary>
    public string Color { get; set; } = "blue";

    /// <summary>
    /// Valor numérico de la estadística
    /// </summary>
    public int Valor { get; set; }

    /// <summary>
    /// Etiqueta principal del KPI
    /// </summary>
    public string Etiqueta { get; set; } = string.Empty;

    /// <summary>
    /// Subetiqueta opcional (texto secundario)
    /// </summary>
    public string? SubEtiqueta { get; set; }

    /// <summary>
    /// Tooltip explicativo (se muestra en hover)
    /// </summary>
    public string? Tooltip { get; set; }

    /// <summary>
    /// Formato del número (default: entero, puede ser "currency", "percentage")
    /// </summary>
    public string Formato { get; set; } = "number";
}
