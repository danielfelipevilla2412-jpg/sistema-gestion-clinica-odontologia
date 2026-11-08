using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("Plan_Tratamiento")]
public class PlanTratamiento
{
    [Key]
    [Column("id_plan")]
    public int IdPlan { get; set; }

    [Required]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    [Required]
    [Column("id_servicio")]
    public int IdServicio { get; set; }

    [Column("id_profesional")]
    public int? IdProfesional { get; set; }

    [Column("numero_fdi")]
    [StringLength(5)]
    public string? NumeroFdi { get; set; }

    [Required]
    [Column("estado")]
    [StringLength(20)]
    public string Estado { get; set; } = "propuesto";

    [Column("observacion")]
    [StringLength(1000)]
    public string? Observacion { get; set; }

    [Required]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("actualizado_en")]
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public HistoriaClinica? HistoriaClinica { get; set; }
    public Servicio? Servicio { get; set; }
    public Profesional? Profesional { get; set; }
}
