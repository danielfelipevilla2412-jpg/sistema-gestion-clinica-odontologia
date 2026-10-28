namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// Tipos de notificación según su prioridad
/// </summary>
public enum TipoNotificacion
{
    /// <summary>
    /// Notificación informativa (azul)
    /// </summary>
    Info,

    /// <summary>
    /// Notificación de advertencia (naranja)
    /// </summary>
    Warning,

    /// <summary>
    /// Notificación urgente (rojo)
    /// </summary>
    Urgent
}

/// <summary>
/// Representa una notificación individual en el panel
/// </summary>
public class NotificacionItem
{
    /// <summary>
    /// Tipo de notificación (Info, Warning, Urgent)
    /// </summary>
    public TipoNotificacion Tipo { get; set; } = TipoNotificacion.Info;

    /// <summary>
    /// Título de la notificación
    /// </summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje descriptivo de la notificación
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>
    /// Texto del botón de acción (opcional)
    /// </summary>
    public string? Accion { get; set; }

    /// <summary>
    /// URL a la que navega el botón de acción
    /// </summary>
    public string? UrlAccion { get; set; }

    /// <summary>
    /// ID único de la notificación (para dismissible)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Indica si la notificación puede ser descartada
    /// </summary>
    public bool Dismissible { get; set; } = true;
}
