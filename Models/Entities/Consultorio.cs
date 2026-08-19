using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Consultorio")]
    public class Consultorio
    {
        [Key]
        [Column("id_consultorio")]
        public int IdConsultorio { get; set; }

        [Required]
        [Column("nombre")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Column("ubicacion")]
        [StringLength(150)]
        public string? Ubicacion { get; set; }

        [Column("tipo")]
        [StringLength(50)]
        public string? Tipo { get; set; }

        [Column("nombre_estado")]
        [StringLength(50)]
        public string? NombreEstado { get; set; }

        [Column("capacidad")]
        public int? Capacidad { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(15)]
        public string Estado { get; set; } = "disponible";
    }
}
