using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("Credencial_Profesional")]
public sealed class CredencialProfesional
{
    [Key]
    [Column("id_credencial")]
    public int IdCredencial { get; set; }

    [Column("id_profesional")]
    public int IdProfesional { get; set; }

    [Column("subido_por")]
    public int? SubidoPor { get; set; }

    [Required]
    [StringLength(50)]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    [Column("nombre_original")]
    public string NombreOriginal { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Column("ruta_privada")]
    public string RutaPrivada { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Column("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [Column("tamano_bytes")]
    public long TamanoBytes { get; set; }

    [Required]
    [StringLength(15)]
    [Column("estado_revision")]
    public string EstadoRevision { get; set; } = "pendiente";

    [StringLength(500)]
    [Column("observacion_revision")]
    public string? ObservacionRevision { get; set; }

    [Column("fecha_subida")]
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

    [Column("revisado_por")]
    public int? RevisadoPor { get; set; }

    [Column("fecha_revision")]
    public DateTime? FechaRevision { get; set; }

    public Profesional? Profesional { get; set; }
    public Usuario? UsuarioSubida { get; set; }
    public Usuario? UsuarioRevision { get; set; }
}
