namespace SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

public class AgendaApoyoViewModel
{
    public string Fecha { get; set; } = string.Empty;
    public string FechaInicio { get; set; } = string.Empty;
    public string FechaFin { get; set; } = string.Empty;
    public string Modo { get; set; } = "dia";
    public List<AgendaApoyoCitaItemViewModel> Citas { get; set; } = new();
}

public class AgendaApoyoCitaItemViewModel
{
    public int Id { get; set; }
    public string Fecha { get; set; } = string.Empty;
    public string Hora { get; set; } = string.Empty;
    public string Paciente { get; set; } = string.Empty;
    public string Profesional { get; set; } = string.Empty;
    public string Tipo { get; set; } = "consulta";
    public string? Alergia { get; set; }
    public string Estado { get; set; } = "Pendiente";
}
