namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// ViewModel para el componente PanelEstadisticas
/// Contiene una colección de estadísticas a mostrar
/// </summary>
public class PanelEstadisticasViewModel
{
    /// <summary>
    /// Lista de estadísticas a mostrar en el panel
    /// </summary>
    public List<EstadisticaItem> Estadisticas { get; set; } = new();

    /// <summary>
    /// Título opcional del panel de estadísticas
    /// </summary>
    public string? Titulo { get; set; }

    /// <summary>
    /// Clase CSS adicional para el contenedor
    /// </summary>
    public string? ClaseAdicional { get; set; }
}
