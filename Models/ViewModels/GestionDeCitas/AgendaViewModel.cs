namespace SmileTrack_MVC.Models.ViewModels;

public sealed class AgendaViewModel
{
    public DateTime WeekStart { get; init; }
    public string SemanaLabel { get; init; } = string.Empty;
    public IReadOnlyList<AgendaDiaViewModel> Dias { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Pacientes { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Profesionales { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Consultorios { get; init; } = [];
    public IReadOnlyList<SelectOptionViewModel> Servicios { get; init; } = [];
    public int? ProfessionalId { get; init; }
    public int? OfficeId { get; init; }
    public int DuracionCitaMinutos { get; init; }
    public IReadOnlySet<DayOfWeek> DiasAtencion { get; init; } = new HashSet<DayOfWeek>();
    public string HorarioApertura { get; init; } = "07:00";
    public string HorarioCierre { get; init; } = "18:00";
    public string DiasAtencionTexto { get; init; } = "Lunes a sábado";
}

public sealed class SelectOptionViewModel
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
}
