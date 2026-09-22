using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO utilizado cuando un paciente solicita una cita médica desde su portal.
/// No permite asignar hora exacta si requiere confirmación ni profesional directamente;
/// la recepcionista revisa la solicitud, asigna un profesional y confirma el horario.
/// El IdServicio es opcional: el paciente puede no saber exactamente qué servicio requiere;
/// la recepcionista lo asigna al confirmar.
/// </summary>
public sealed class CitaSolicitudPacienteDto
{
    /// <summary>
    /// ID del servicio solicitado. Puede ser null o 0 si el paciente no selecciona uno.
    /// </summary>
    public int? IdServicio { get; set; }

    [Required(ErrorMessage = "La fecha preferida es obligatoria.")]
    public DateTime Fecha { get; set; }

    [StringLength(4000, ErrorMessage = "Las notas no pueden superar los 4000 caracteres.")]
    public string? Notas { get; set; }
}
