namespace SmileTrack_MVC.Models.Entities;

public sealed class ConsultorioEstadoOperativo
{
    public int IdEstadoOperativo { get; set; }
    public int IdConsultorio { get; set; }
    public string ChecklistJson { get; set; } = "[]";
    public string? Observaciones { get; set; }
    public int? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; }

    public Consultorio? Consultorio { get; set; }
    public Usuario? Usuario { get; set; }
}
