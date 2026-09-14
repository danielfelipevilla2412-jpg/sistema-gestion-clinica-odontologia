namespace SmileTrack_MVC.Models.Entities;

public sealed class Notificacion
{
    public int IdNotificacion { get; set; }
    public int? IdPaciente { get; set; }
    public int? IdCita { get; set; }
    public string Tipo { get; set; } = "recordatorio";
    public string Titulo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string Canal { get; set; } = "interno";
    public string Estado { get; set; } = "pendiente";
    public DateTime? FechaProgramada { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public DateTime? FechaLectura { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime CreadaEn { get; set; }

    public Paciente? Paciente { get; set; }
    public Cita? Cita { get; set; }
}
