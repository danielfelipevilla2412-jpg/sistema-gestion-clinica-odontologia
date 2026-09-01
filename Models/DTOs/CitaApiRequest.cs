using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO canónico para crear una cita desde la API REST.
/// Este contrato representa la capa de entrada del módulo de citas.
/// </summary>
public sealed class CitaApiRequest
{
    [Required(ErrorMessage = "El paciente es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El paciente seleccionado es inválido.")]
    public int IdPaciente { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El profesional seleccionado es inválido.")]
    public int? IdProfesional { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El servicio seleccionado es inválido.")]
    public int? IdServicio { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El consultorio seleccionado es inválido.")]
    public int? IdConsultorio { get; set; }

    [Required(ErrorMessage = "La fecha y hora de la cita son obligatorias.")]
    public DateTime FechaHora { get; set; }

    [Required(ErrorMessage = "El estado de la cita es obligatorio.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "El estado debe tener entre 1 y 30 caracteres.")]
    public string Estado { get; set; } = "programada";

    [StringLength(2000, ErrorMessage = "Las notas no pueden superar 2000 caracteres.")]
    public string? Notas { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El identificador de estado no es válido.")]
    public int? IdEstado { get; set; }
}
