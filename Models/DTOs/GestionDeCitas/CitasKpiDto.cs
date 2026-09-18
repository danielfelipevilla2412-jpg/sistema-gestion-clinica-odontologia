namespace SmileTrack_MVC.Models.DTOs;

public sealed class CitasKpiDto
{
    public int Total { get; init; }
    public int Programadas { get; init; }
    public int Canceladas { get; init; }
    public int Atendidas { get; init; }
    public int DiferenciaSemana { get; init; }
    public int TasaCancelacion { get; init; }
    public int TasaAsistencia { get; init; }
}
