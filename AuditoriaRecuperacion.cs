using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("AuditoriaRecuperacion")]
public class AuditoriaRecuperacion
{
    [Key]
    [Column("id_auditoria")]
    public int IdAuditoria { get; set; }

    [Column("id_usuario")]
    public int? IdUsuario { get; set; }

    [Required]
    [Column("correo_solicitado")]
    [StringLength(150)]
    public string CorreoSolicitado { get; set; } = string.Empty;

    [Required]
    [Column("accion")]
    [StringLength(30)]
    public string Accion { get; set; } = string.Empty;

    [Column("ip_origen")]
    [StringLength(45)]
    public string? IpOrigen { get; set; }

    [Column("fecha")]
    public DateTime Fecha { get; set; }
}
