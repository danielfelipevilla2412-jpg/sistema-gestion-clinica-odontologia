using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO canónico para actualizar una cita existente.
/// Mantiene el contrato actual de la API y evita exponer entidades EF directamente.
/// </summary>
public sealed class CitaApiUpdateDto
{
    [Required(ErrorMessage = "El identificador de la cita es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El identificador de la cita es inválido.")]
    public int IdCita { get; set; }

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

    [StringLength(30, ErrorMessage = "El estado debe tener como máximo 30 caracteres.")]
    public string? Estado { get; set; }

    [StringLength(2000, ErrorMessage = "Las notas no pueden superar 2000 caracteres.")]
    public string? Notas { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El identificador de estado no es válido.")]
    public int? IdEstado { get; set; }
}
