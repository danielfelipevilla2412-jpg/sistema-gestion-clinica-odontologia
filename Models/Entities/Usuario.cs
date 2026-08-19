using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Usuario")]
    public class Usuario
    {
        [Key]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("creado_por")]
        public int? CreadoPor { get; set; }

        [Required]
        [Column("nombre")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [Column("apellidos")]
        [StringLength(100)]
        public string Apellidos { get; set; } = string.Empty;

        [Required]
        [Column("correo")]
        [StringLength(150)]
        public string Correo { get; set; } = string.Empty;

        [Required]
        [Column("contrasena")]
        [StringLength(255)]
        public string Contrasena { get; set; } = string.Empty;

        [Column("id_rol")]
        public int IdRol { get; set; }

        [Required]
        [Column("estado")]
        [StringLength(10)]
        public string Estado { get; set; } = "activo";

        [Column("fecha_nacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Column("ultimo_login")]
        public DateTime? UltimoLogin { get; set; }

        [Column("codigo_recuperacion")]
        [StringLength(10)]
        public string? CodigoRecuperacion { get; set; }

        [Column("fecha_expiracion_codigo")]
        public DateTime? FechaExpiracionCodigo { get; set; }

        [Column("intentos_fallidos")]
        public int IntentosFallidos { get; set; } = 0;

        [Column("ultimo_logout")]
        public DateTime? UltimoLogout { get; set; }

        [ForeignKey("IdRol")]
        public Rol Rol { get; set; } = null!;
    }
}
