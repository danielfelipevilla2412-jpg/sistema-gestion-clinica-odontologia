namespace SmileTrack_MVC.Models.Entities;

public sealed class ConsultorioHistorial
{
    public int IdHistorial { get; set; }
    public int IdConsultorio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? IdUsuario { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaCambio { get; set; }

    public Consultorio? Consultorio { get; set; }
    public Usuario? Usuario { get; set; }
}
