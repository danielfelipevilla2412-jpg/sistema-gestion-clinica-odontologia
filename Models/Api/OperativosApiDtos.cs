using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api;

public sealed class AsistenciaProcedimientoApiDto
{
    public int IdCita { get; set; }
    public int Minutos { get; set; }
    public DateTime Inicio { get; set; }
    public bool Limpieza { get; set; }
    public bool Esterilizacion { get; set; }
    public bool Equipos { get; set; }
    public DateTime ActualizadoEn { get; set; }
}

public sealed class AsistenciaProcedimientoApiRequest
{
    [Range(0, 1440)]
    public int Minutos { get; set; }
    public DateTime? Inicio { get; set; }
    public bool Limpieza { get; set; }
    public bool Esterilizacion { get; set; }
    public bool Equipos { get; set; }
}

public sealed class ConsultorioEstadoOperativoApiDto
{
    public int IdConsultorio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public object[] Checklist { get; set; } = [];
    public string? Observaciones { get; set; }
    public DateTime? ActualizadoEn { get; set; }
    public List<ConsultorioHistorialApiDto> Historial { get; set; } = [];
}

public sealed class ConsultorioHistorialApiDto
{
    public string Estado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime FechaCambio { get; set; }
}

public sealed class ConsultorioEstadoOperativoApiRequest
{
    [Required]
    public string Estado { get; set; } = "disponible";
    public object[] Checklist { get; set; } = [];
    [StringLength(2000)]
    public string? Observaciones { get; set; }
}
