namespace SmileTrack_MVC.Models.Entities;

public sealed class ConsultorioHistorial
{
    public int IdHistorial { get; set; }
    public int IdConsultorio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public byte? IdEstadoAnterior { get; set; }
    public byte? IdEstadoNuevo { get; set; }
    public int? IdUsuario { get; set; }
    public string? Motivo { get; set; }
    public DateTime FechaCambio { get; set; }

    public Consultorio? Consultorio { get; set; }
    public EstadoConsultorio? EstadoAnterior { get; set; }
    public EstadoConsultorio? EstadoNuevo { get; set; }
    public Usuario? Usuario { get; set; }
}
