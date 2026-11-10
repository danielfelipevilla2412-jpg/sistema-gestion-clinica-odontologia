using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

// Yeray: Representa una sesión individual para que el progreso sea contable y cada registro pueda anularse con trazabilidad.
[Table("Plan_Sesion")]
public sealed class PlanSesion
{
    [Key]
    [Column("id_sesion")]
    public int IdSesion { get; set; }

    [Required]
    [Column("id_plan")]
    public int IdPlan { get; set; }

    [Required]
    [Column("fecha_sesion")]
    public DateTime FechaSesion { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("id_profesional_registro")]
    public int IdProfesionalRegistro { get; set; }

    [Column("id_cita")]
    public int? IdCita { get; set; }

    [Column("id_nota")]
    public int? IdNota { get; set; }

    [Required]
    [Column("registrada_en")]
    public DateTime RegistradaEn { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("anulada")]
    public bool Anulada { get; set; }

    [Column("motivo_anulacion")]
    [StringLength(500)]
    public string? MotivoAnulacion { get; set; }

    [Column("anulada_en")]
    public DateTime? AnuladaEn { get; set; }

    [Column("id_profesional_anula")]
    public int? IdProfesionalAnula { get; set; }

    public PlanTratamiento? Plan { get; set; }
    public Cita? Cita { get; set; }
    public NotaClinica? Nota { get; set; }
    public Profesional? ProfesionalRegistro { get; set; }
    public Profesional? ProfesionalAnula { get; set; }
}