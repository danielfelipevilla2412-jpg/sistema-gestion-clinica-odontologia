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

        [Column("motivo_consulta")]
        public string? MotivoConsulta { get; set; }

        [Column("motivo_cancelacion")]
        [StringLength(500)]
        public string? MotivoCancelacion { get; set; }

        [Column("notas_previas")]
        public string? NotasPrevias { get; set; }

        [Column("tipo_cita")]
        [StringLength(20)]
        public string? TipoCita { get; set; }

        [Column("fecha_creacion")]
        public DateTime? FechaCreacion { get; set; }

        [Column("creado_por")]
        public int? CreadoPor { get; set; }

        [Column("archivo_adjunto")]
        [StringLength(255)]
        public string? ArchivoAdjunto { get; set; }

        [Column("duracion_minutos")]
        public int DuracionMinutos { get; set; } = 60;

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
        FechaHora.AddMinutes(DuracionMinutos > 0 ? DuracionMinutos : 60).TimeOfDay;

        [Column("id_consultorio")]
        public int? IdConsultorio { get; set; }

        [Column("id_estado")]
        public int? IdEstado { get; set; }

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

        public ICollection<Factura> Facturas { get; set; } = [];
    }
}
