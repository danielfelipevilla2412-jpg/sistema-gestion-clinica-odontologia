using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels
{
    public class CitaViewModel
    {
        public int? IdCita { get; set; }

        [Required(ErrorMessage = "El paciente es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un paciente válido")]
        public int IdPaciente { get; set; }

        [Required(ErrorMessage = "El profesional es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un profesional válido")]
        public int IdProfesional { get; set; }

        [Required(ErrorMessage = "El consultorio es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un consultorio válido")]
        public int IdConsultorio { get; set; }

        [Required(ErrorMessage = "El servicio es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un servicio válido")]
        public int IdServicio { get; set; }

        [Required(ErrorMessage = "El estado es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un estado válido")]
        public int IdEstado { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        [DataType(DataType.Date, ErrorMessage = "Formato de fecha inválido")]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La hora de inicio es obligatoria")]
        public TimeSpan? HoraInicio { get; set; }

        public TimeSpan? HoraFin { get; set; }

        [DataType(DataType.DateTime, ErrorMessage = "Formato de fecha y hora inválido")]
        public DateTime FechaHora { get; set; }

        public string? Estado { get; set; } = "programada";

        [StringLength(500, ErrorMessage = "Las notas no pueden superar los 500 caracteres")]
        public string? Notas { get; set; }

        [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres")]
        public string? MotivoConsulta { get; set; }

        [StringLength(500, ErrorMessage = "Las notas previas no pueden superar los 500 caracteres")]
        public string? NotasPrevias { get; set; }

        [StringLength(100, ErrorMessage = "El tipo de cita no puede superar los 100 caracteres")]
        public string? TipoCita { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
