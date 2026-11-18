namespace SmileTrack_MVC.Models.Entities;

public sealed class EstadoConsultorio
{
    public byte IdEstadoConsultorio { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
