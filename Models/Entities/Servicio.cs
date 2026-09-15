using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Servicio")]
    public class Servicio
    {
        [Key]
        [Column("id_servicio")]
        public int IdServicio { get; set; }

        [Required]
        [Column("nombre")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Column("precio")]
        public decimal Precio { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(10)]
        public string Estado { get; set; } = "activo";

        [Required]
        [Column("categoria")]
        [StringLength(50)]
        public string Categoria { get; set; } = "general";

        [Column("duracion_minutos")]
        public int DuracionMinutos { get; set; } = 30;
    }
}
