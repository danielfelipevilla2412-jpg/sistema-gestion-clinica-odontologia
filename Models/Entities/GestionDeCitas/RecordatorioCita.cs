namespace SmileTrack_MVC.Models.Entities;

public sealed class RecordatorioCita
{
    public int IdRecordatorio { get; set; }
    public int IdCita { get; set; }
    public string Canal { get; set; } = "email";
    public string Estado { get; set; } = "pendiente";
    public DateTime ProgramadoPara { get; set; }
    public DateTime? EnviadoEn { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime CreadoEn { get; set; }

    public Cita? Cita { get; set; }
}
