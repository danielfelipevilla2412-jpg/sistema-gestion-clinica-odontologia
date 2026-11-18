namespace SmileTrack_MVC.Models.Entities;

public sealed class CitaConsultorioHistorial
{
    public int IdHistorial { get; set; }
    public int IdCita { get; set; }
    public int? IdConsultorioAnterior { get; set; }
    public int? IdConsultorioNuevo { get; set; }
    public int? IdUsuario { get; set; }
    public DateTime FechaCambio { get; set; }

    public Cita? Cita { get; set; }
    public Consultorio? ConsultorioAnterior { get; set; }
    public Consultorio? ConsultorioNuevo { get; set; }
    public Usuario? Usuario { get; set; }
}
