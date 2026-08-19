using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Cita")]
    public class Cita
    {
        [Key]
        [Column("id_cita")]
        public int IdCita { get; set; }

        [Required]
        [Column("id_paciente")]
        public int IdPaciente { get; set; }

        [Column("id_profesional")]
        public int? IdProfesional { get; set; }

        [Column("id_servicio")]
        public int? IdServicio { get; set; }

        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(30)]
        public string Estado { get; set; } = "programada";

        [Column("notas")]
        public string? Notas { get; set; }

        [NotMapped]
        public DateTime Fecha
        {
            get => FechaHora.Date;
            set => FechaHora = value.Date.Add(FechaHora.TimeOfDay);
        }

        [NotMapped]
        public TimeSpan? HoraInicio
        {
            get => FechaHora.TimeOfDay;
            set => FechaHora = FechaHora.Date.Add(value ?? TimeSpan.Zero);
        }

        [NotMapped]
        public TimeSpan HoraFin =>
        FechaHora.AddMinutes(60).TimeOfDay;

        [NotMapped]
        public string? MotivoConsulta
        {
            get => Notas;
            set => Notas = value;
        }

        [NotMapped]
        public string? NotasPrevias
        {
            get => Notas;
            set => Notas = value;
        }

        [NotMapped]
        public string? TipoCita { get; set; }

        public int? IdConsultorio { get; set; }

        public int? IdEstado { get; set; }

        [NotMapped]
        public DateTime? FechaCreacion { get; set; }

        [NotMapped]
        public int? CreadoPor { get; set; }

        [NotMapped]
        public string? ArchivoAdjunto { get; set; }

        [ForeignKey(nameof(IdPaciente))]
        public Paciente? Paciente { get; set; }

        [ForeignKey(nameof(IdProfesional))]
        public Profesional? Profesional { get; set; }

        [ForeignKey(nameof(IdServicio))]
        public Servicio? Servicio { get; set; }

        [ForeignKey(nameof(IdConsultorio))]
        public Consultorio? Consultorio { get; set; }

        [ForeignKey(nameof(IdEstado))]
        public EstadoCita? EstadoCita { get; set; }
    }
}
