namespace SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

public class AsistenciaProcedimientoViewModel
{
    public int? CitaId { get; set; }
    public string? Estado { get; set; }
    public string? EstadoNormalizado { get; set; }
    public DateTime? InicioProcedimiento { get; set; }
    public string Paciente { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;
    public string Profesional { get; set; } = string.Empty;
    public string Consultorio { get; set; } = string.Empty;
    public int? ConsultorioId { get; set; }
    public string? Alergia { get; set; }
    public string? Antecedentes { get; set; }
}
