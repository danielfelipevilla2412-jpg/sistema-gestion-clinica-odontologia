namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// ViewModel para el componente NotificacionesInteligentes
/// </summary>
public class NotificacionesViewModel
{
    /// <summary>
    /// Lista de notificaciones a mostrar
    /// </summary>
    public List<NotificacionItem> Notificaciones { get; set; } = new();

    /// <summary>
    /// Título del panel de notificaciones
    /// </summary>
    public string Titulo { get; set; } = "Notificaciones";

    /// <summary>
    /// Mensaje cuando no hay notificaciones
    /// </summary>
    public string MensajeVacio { get; set; } = "Todo al día";

    /// <summary>
    /// Icono cuando no hay notificaciones
    /// </summary>
    public string IconoVacio { get; set; } = "check_circle";

    /// <summary>
    /// Indica si hay notificaciones
    /// </summary>
    public bool TieneNotificaciones => Notificaciones.Any();

    /// <summary>
    /// Cantidad de notificaciones urgentes
    /// </summary>
    public int NotificacionesUrgentes => Notificaciones.Count(n => n.Tipo == TipoNotificacion.Urgent);
}
