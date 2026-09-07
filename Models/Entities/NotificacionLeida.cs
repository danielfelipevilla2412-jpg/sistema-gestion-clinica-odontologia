namespace SmileTrack_MVC.Models.Entities;

public sealed class NotificacionLeida
{
    public int IdNotificacionLeida { get; set; }
    public int IdPaciente { get; set; }
    public int IdCita { get; set; }
    public DateTime FechaLectura { get; set; } = DateTime.UtcNow;
}