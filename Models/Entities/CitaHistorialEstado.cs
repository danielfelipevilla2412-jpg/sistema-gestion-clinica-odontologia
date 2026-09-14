namespace SmileTrack_MVC.Models.Entities;

public sealed class CitaHistorialEstado
{
    public int IdHistorial { get; set; }
    public int IdCita { get; set; }
    public int? IdEstado { get; set; }
    public string EstadoTexto { get; set; } = string.Empty;
    public int? IdUsuario { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaCambio { get; set; }

    public Cita? Cita { get; set; }
    public EstadoCita? Estado { get; set; }
    public Usuario? Usuario { get; set; }
}
