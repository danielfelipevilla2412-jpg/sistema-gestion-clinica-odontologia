namespace SmileTrack_MVC.Models.Entities;

public sealed class AsistenciaProcedimiento
{
    public int IdAsistencia { get; set; }
    public int IdCita { get; set; }
    public int Minutos { get; set; }
    public DateTime Inicio { get; set; }
    public bool Limpieza { get; set; }
    public bool Esterilizacion { get; set; }
    public bool Equipos { get; set; }
    public int? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; }

    public Cita? Cita { get; set; }
    public Usuario? Usuario { get; set; }
}
