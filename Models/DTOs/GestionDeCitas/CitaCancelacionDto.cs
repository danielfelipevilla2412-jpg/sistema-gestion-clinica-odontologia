using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO para cancelar una cita.
/// RN-23: El motivo de cancelación es obligatorio, no puede estar vacío y
///        tiene un máximo de 500 caracteres para fines de auditoría.
/// </summary>
public sealed class CitaCancelacionDto
{
    [Required(ErrorMessage = "El motivo de cancelación es obligatorio (RN-23).")]
    [MinLength(1, ErrorMessage = "El motivo de cancelación no puede estar vacío.")]
    [StringLength(500, ErrorMessage = "El motivo de cancelación no puede superar 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
