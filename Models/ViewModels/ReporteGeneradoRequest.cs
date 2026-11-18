using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class ReporteGeneradoRequest
{
    [Required, StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Categoria { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$")]
    public string Periodo { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notas { get; set; }
}

public sealed class ReporteGeneradoSnapshot
{
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string? Notas { get; set; }
    public DateTime FechaGeneracionUtc { get; set; }
    public List<ReporteMetricaSnapshot> Metricas { get; set; } = [];
}

public sealed class ReporteMetricaSnapshot
{
    public string Indicador { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
