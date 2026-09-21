using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO para la petición de envío masivo o individual de recordatorios por correo a pacientes.
/// </summary>
public sealed class EnviarRecordatoriosDto
{
    [Required(ErrorMessage = "Debe especificar al menos una cita para enviar recordatorio.")]
    public List<int> IdsCitas { get; set; } = new();

    public string? MensajePersonalizado { get; set; }
}
