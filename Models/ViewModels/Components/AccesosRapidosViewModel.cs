namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// ViewModel para el componente AccesosRapidos
/// </summary>
public class AccesosRapidosViewModel
{
    /// <summary>
    /// Lista de accesos rápidos a mostrar
    /// </summary>
    public List<AccesoRapidoItem> Accesos { get; set; } = new();

    /// <summary>
    /// Título opcional del panel
    /// </summary>
    public string? Titulo { get; set; }

    /// <summary>
    /// Cantidad de columnas en el grid (auto, 2, 3, 4)
    /// </summary>
    public string Columnas { get; set; } = "auto";
}
