namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// M7 (RF-24): Representa una sugerencia de emparejamiento entre un candidato de la lista
/// de espera y un profesional disponible para la franja liberada.
/// No asigna la cita automáticamente — requiere confirmación de recepción (CU-CIT-06).
/// </summary>
public sealed class SugerenciaListaEsperaDto
{
    /// <summary>ID del registro en Lista_Espera_Cita.</summary>
    public int IdListaEspera { get; set; }

    /// <summary>Paciente en espera.</summary>
    public int IdPaciente { get; set; }
    public string NombrePaciente { get; set; } = string.Empty;

    /// <summary>Servicio solicitado (puede ser null si el paciente no especificó).</summary>
    public int? IdServicio { get; set; }
    public string? NombreServicio { get; set; }

    /// <summary>Franja que se liberó y que coincide con la preferencia del candidato.</summary>
    public DateTime FechaHoraLiberada { get; set; }

    /// <summary>
    /// Profesionales disponibles para esa franja y servicio.
    /// La recepción elige uno y llama a ConfirmarYAsignar.
    /// </summary>
    public List<ProfesionalDisponibleDto> ProfesionalesDisponibles { get; set; } = [];

    /// <summary>Notas del candidato en lista de espera.</summary>
    public string? Notas { get; set; }
}
