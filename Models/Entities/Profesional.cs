using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Profesional")]
    public class Profesional
    {
        [Key]
        [Column("id_profesional")]
        public int IdProfesional { get; set; }

        [Column("id_usuario")]
        public int? IdUsuario { get; set; }

        [Required]
        [Column("nombres")]
        [StringLength(100)]
        public string Nombres { get; set; } = string.Empty;

        [Required]
        [Column("apellidos")]
        [StringLength(100)]
        public string Apellidos { get; set; } = string.Empty;

        [Required]
        [Column("registro_medico")]
        [StringLength(50)]
        public string RegistroMedico { get; set; } = string.Empty;

        [Column("descripcion")]
        [StringLength(255)]
        public string? Descripcion { get; set; }

        [Column("categoria")]
        [StringLength(100)]
        public string? Categoria { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(15)]
        public string Estado { get; set; } = "activo";

        [Column("fecha_ingreso")]
        public DateTime? FechaIngreso { get; set; }

        [ForeignKey(nameof(IdUsuario))]
        public Usuario? Usuario { get; set; }

        public ICollection<Profesional_Especialidad> Especialidades { get; set; } = [];

        [NotMapped]
        public string NombreProfesional => Usuario != null ? $"{Usuario.Nombre} {Usuario.Apellidos}".Trim() : $"{Nombres} {Apellidos}".Trim();
    }
}
