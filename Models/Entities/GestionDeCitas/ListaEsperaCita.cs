using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Lista_Espera_Cita")]
    public class ListaEsperaCita
    {
        [Key]
        [Column("id_lista_espera")]
        public int IdListaEspera { get; set; }

        [Required]
        [Column("id_paciente")]
        public int IdPaciente { get; set; }

        [Column("id_servicio")]
        public int? IdServicio { get; set; }

        [Column("id_profesional_preferido")]
        public int? IdProfesionalPreferido { get; set; }

        [Required]
        [Column("fecha_deseada_inicio")]
        public DateTime FechaDeseadaInicio { get; set; }

        [Required]
        [Column("fecha_deseada_fin")]
        public DateTime FechaDeseadaFin { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(20)]
        public string Estado { get; set; } = "pendiente";

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        [Column("notas")]
        [StringLength(500)]
        public string? Notas { get; set; }

        public Paciente? Paciente { get; set; }
        public Servicio? Servicio { get; set; }
        public Profesional? ProfesionalPreferido { get; set; }
    }
}
