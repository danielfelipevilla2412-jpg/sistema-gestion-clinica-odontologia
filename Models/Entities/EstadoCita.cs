using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Estado_Cita")]
    public class EstadoCita
    {
        [Key]
        [Column("id_estado")]
        public int IdEstado { get; set; }

        [Required]
        [Column("nombre_estado")]
        [StringLength(50)]
        public string NombreEstado { get; set; } = string.Empty;

        [Column("descripcion")]
        [StringLength(150)]
        public string? Descripcion { get; set; }
    }
}
