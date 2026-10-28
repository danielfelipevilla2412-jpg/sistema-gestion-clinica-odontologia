namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// Representa un acceso rápido individual
/// </summary>
public class AccesoRapidoItem
{
    /// <summary>
    /// Título del acceso rápido
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Descripción opcional
    /// </summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Icono Material Symbols
    /// </summary>
    public string Icono { get; set; } = "link";

    /// <summary>
    /// URL de destino
    /// </summary>
    public string Url { get; set; } = "#";

    /// <summary>
    /// Color del card: blue, green, orange, purple, red, teal, pink
    /// </summary>
    public string Color { get; set; } = "blue";

    /// <summary>
    /// Indica si abre en nueva ventana
    /// </summary>
    public bool NuevaVentana { get; set; } = false;
}
