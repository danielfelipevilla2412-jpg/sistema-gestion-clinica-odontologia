namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO que representa un profesional disponible en una fecha y rango horario específicos.
/// Utilizado para poblar dinámicamente el selector de profesionales en la UI.
/// </summary>
public sealed class ProfesionalDisponibleDto
{
    public int IdProfesional { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Especialidades { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
}
