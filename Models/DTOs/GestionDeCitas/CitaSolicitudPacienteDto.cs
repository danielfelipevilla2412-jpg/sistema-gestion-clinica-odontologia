using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// DTO utilizado cuando un paciente agenda una cita desde su portal.
/// El horario y profesional elegidos se validan de nuevo en el servidor antes de reservar.
/// </summary>
public sealed class CitaSolicitudPacienteDto
{
    /// <summary>ID del servicio activo elegido por el paciente.</summary>
    [Required(ErrorMessage = "Debe seleccionar un servicio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El servicio seleccionado no es válido.")]
    public int? IdServicio { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El profesional preferido no es válido.")]
    public int? IdProfesional { get; set; }

    [Required(ErrorMessage = "La fecha de la cita es obligatoria.")]
    public DateTime Fecha { get; set; }

    [Required(ErrorMessage = "La hora de la cita es obligatoria.")]
    [Range(typeof(TimeSpan), "00:00:00", "23:59:59", ErrorMessage = "La hora seleccionada no es válida.")]
    public TimeSpan HoraInicio { get; set; }

    [StringLength(4000, ErrorMessage = "Las notas no pueden superar los 4000 caracteres.")]
    public string? Notas { get; set; }
}

/// <summary>Datos para inscribir a un paciente en la lista de espera de un servicio.</summary>
public sealed class ListaEsperaSolicitudDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int? IdServicio { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdProfesionalPreferido { get; set; }

    [Required]
    public DateTime FechaDeseadaInicio { get; set; }

    [Required]
    public DateTime FechaDeseadaFin { get; set; }

    [StringLength(500)]
    public string? Notas { get; set; }
}
