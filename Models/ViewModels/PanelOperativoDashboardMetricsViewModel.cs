namespace SmileTrack_MVC.Models.ViewModels;

public sealed class PanelOperativoDashboardMetricsViewModel
{
    public int TotalPacientes { get; init; }
    public int CitasHoy { get; init; }
    public int ProfesionalesActivos { get; init; }
    public decimal IngresosDelMes { get; init; }
    public int PorcentajeOcupacion { get; init; }
    public int CitasAtendidas { get; init; }
    public int CitasConfirmadas { get; init; }
    public int CitasProgramadas { get; init; }
    public int CitasCanceladas { get; init; }
    public int PorcentajeAtendidas { get; init; }
    public int PorcentajeConfirmadas { get; init; }
    public int PorcentajeProgramadas { get; init; }
    public int PorcentajeCanceladas { get; init; }
}
