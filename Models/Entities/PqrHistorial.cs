namespace SmileTrack_MVC.Models.Entities;

/// <summary>Evento inmutable de seguimiento de una PQR.</summary>
public class PqrHistorial
{
    public long IdHistorial { get; set; }
    public int IdPqr { get; set; }
    public int? IdUsuario { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public string? Respuesta { get; set; }
    public string TipoEvento { get; set; } = "estado";
    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;
}
