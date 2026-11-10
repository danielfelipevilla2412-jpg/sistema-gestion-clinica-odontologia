using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO utilizado cuando un paciente solicita una cita médica desde su portal.
/// No permite asignar hora exacta si requiere confirmación ni profesional directamente;
/// la recepcionista revisa la solicitud, asigna un profesional y confirma el horario.
/// El IdServicio es obligatorio: cada solicitud debe identificar un servicio activo.
/// La recepcionista asigna profesional, consultorio y hora definitiva al confirmar.
/// </summary>
public sealed class CitaSolicitudPacienteDto
{
    /// <summary>ID del servicio activo solicitado por el paciente.</summary>
    [Required(ErrorMessage = "Debe seleccionar un servicio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El servicio seleccionado no es válido.")]
    public int? IdServicio { get; set; }

    [Required(ErrorMessage = "La fecha preferida es obligatoria.")]
    public DateTime Fecha { get; set; }

    [StringLength(4000, ErrorMessage = "Las notas no pueden superar los 4000 caracteres.")]
    public string? Notas { get; set; }
}
