using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels
{
    public class CitaAgendaDto : IValidatableObject
    {
        public int? IdCita { get; set; }

        [Required(ErrorMessage = "El paciente es obligatorio")]
        public int IdPaciente { get; set; }

        [Required(ErrorMessage = "El profesional es obligatorio")]
        public int IdProfesional { get; set; }

        [Required(ErrorMessage = "El consultorio es obligatorio")]
        public int IdConsultorio { get; set; }

        [Required(ErrorMessage = "El servicio es obligatorio")]
        public int IdServicio { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        [DataType(DataType.Date)]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La hora de inicio es obligatoria")]
        [DataType(DataType.Time)]
        public TimeSpan HoraInicio { get; set; }

        [Required(ErrorMessage = "La hora de fin es obligatoria")]
        [DataType(DataType.Time)]
        public TimeSpan HoraFin { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        public string Estado { get; set; } = "Programada";

        public string? Notas { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HoraFin <= HoraInicio)
            {
                yield return new ValidationResult(
                    "La hora de fin debe ser posterior a la hora de inicio.",
                    [nameof(HoraFin)]);
            }
            if ((HoraFin - HoraInicio).TotalMinutes != 60)
            {
                yield return new ValidationResult(
                    "La cita debe tener una duración de 60 minutos.",
                    [nameof(HoraFin)]);
            }
        }
    }
}
