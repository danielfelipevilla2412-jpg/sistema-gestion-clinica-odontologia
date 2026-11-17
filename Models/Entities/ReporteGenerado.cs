namespace SmileTrack_MVC.Models.Entities;

public sealed class ReporteGenerado
{
    public int IdReporte { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public DateTime FechaGeneracionUtc { get; set; }
    public int? GeneradoPor { get; set; }
    public string DatosJson { get; set; } = string.Empty;
}
