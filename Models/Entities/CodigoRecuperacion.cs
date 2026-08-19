using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("CodigoRecuperacion")]
public class CodigoRecuperacion
{
    [Key]
    [Column("id_codigo")]
    public int IdCodigo { get; set; }

    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Required]
    [Column("codigo_hash")]
    [StringLength(255)]
    public string CodigoHash { get; set; } = string.Empty;

    [Column("fecha_creacion")]
    public DateTime FechaCreacion { get; set; }

    [Column("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; }

    [Column("intentos_fallidos")]
    public int IntentosFallidos { get; set; }

    [Column("usado")]
    public bool Usado { get; set; }

    [Column("ip_origen")]
    [StringLength(45)]
    public string? IpOrigen { get; set; }
}
