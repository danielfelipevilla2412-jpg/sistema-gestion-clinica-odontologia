/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: CitaAgendaDto (Objeto de Transferencia)
 * ============================================
 * Archivo: Models/ViewModels/GestionDeCitas/CitaAgendaDto.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * DTO para la transferencia de datos en formularios de creación y edición rápida de citas
 * desde la agenda visual y los diálogos modales. Implementa `IValidatableObject` para validar que la hora
 * de inicio pertenezca a la jornada laboral.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Valida la estructura horaria (HoraInicio) ante el rango laboral de la clínica.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - IValidatableObject, DataAnnotations
 * ============================================
 */

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

        [DataType(DataType.Time)]
        public TimeSpan? HoraFin { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        public string Estado { get; set; } = "Programada";

        public string? Notas { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HoraFin.HasValue && HoraFin.Value <= HoraInicio)
            {
                yield return new ValidationResult(
                    "La hora de fin debe ser posterior a la hora de inicio.",
                    [nameof(HoraFin)]);
            }
        }
    }
}
