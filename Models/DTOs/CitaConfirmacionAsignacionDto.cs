using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO utilizado por la recepcionista o administrador para revisar, asignar un profesional y
/// consultorio, definir la hora y confirmar una cita solicitada por un paciente.
/// </summary>
public sealed class CitaConfirmacionAsignacionDto
{
    [Required(ErrorMessage = "El identificador de la cita es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Identificador de cita inválido.")]
    public int IdCita { get; set; }

    [Required(ErrorMessage = "El profesional es obligatorio para confirmar la cita.")]
    [Range(1, int.MaxValue, ErrorMessage = "Profesional seleccionado inválido.")]
    public int IdProfesional { get; set; }

    [Required(ErrorMessage = "El consultorio es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Consultorio seleccionado inválido.")]
    public int IdConsultorio { get; set; }

    [Required(ErrorMessage = "La fecha definitiva es obligatoria.")]
    public DateTime Fecha { get; set; }

    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public TimeSpan HoraInicio { get; set; }

    [StringLength(4000, ErrorMessage = "Las notas no pueden superar los 4000 caracteres.")]
    public string? Notas { get; set; }
}
