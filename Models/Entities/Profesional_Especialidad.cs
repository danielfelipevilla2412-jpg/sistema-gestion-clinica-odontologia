using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Profesional_Especialidad")]
    public class Profesional_Especialidad
    {
        [Key]
        [Column("id_profesional")]
        public int IdProfesional { get; set; }

        [Key]
        [Column("id_especialidad")]
        public int IdEspecialidad { get; set; }

        [Column("principal")]
        public bool Principal { get; set; }

        [ForeignKey(nameof(IdProfesional))]
        public Profesional? Profesional { get; set; }

        [ForeignKey(nameof(IdEspecialidad))]
        public Especialidad? Especialidad { get; set; }
    }
}
