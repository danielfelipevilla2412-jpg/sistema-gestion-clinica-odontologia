/**
 * ============================================
 * SmileTrack — Entidad: UsuarioPreferenciaConsultorio
 * ============================================
 * PROPÓSITO:
 * Almacena la preferencia de consultorio seleccionado por cada usuario auxiliar.
 * Permite recordar el último consultorio que el auxiliar estaba gestionando.
 * ============================================
 */

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("UsuariosPreferenciasConsultorio")]
public class UsuarioPreferenciaConsultorio
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id_preferencia")]
    public int IdPreferencia { get; set; }

    [Required]
    [Column("id_usuario")]
    public int IdUsuario { get; set; }

    [Required]
    [Column("id_consultorio")]
    public int IdConsultorio { get; set; }

    [Column("actualizado_en")]
    public DateTime ActualizadoEn { get; set; }

    // Navegación
    [ForeignKey(nameof(IdUsuario))]
    public virtual Usuario? Usuario { get; set; }

    [ForeignKey(nameof(IdConsultorio))]
    public virtual Consultorio? Consultorio { get; set; }
}
