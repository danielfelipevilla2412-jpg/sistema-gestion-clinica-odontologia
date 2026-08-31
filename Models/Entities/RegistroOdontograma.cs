using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

/// <summary>
/// Registra el estado clínico de un diente específico dentro de una Historia Clínica.
/// Reemplaza el JSON en Historia_Clinica.observaciones_generales con registros
/// estructurados y con trazabilidad real (quién, cuándo, en qué cita).
/// </summary>
[Table("Registro_Odontograma")]
public class RegistroOdontograma
{
    [Key]
    [Column("id_registro")]
    public int IdRegistro { get; set; }

    /// <summary>FK a Historia_Clinica — una HC puede tener muchos registros</summary>
    [Required]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    /// <summary>
    /// Número FDI del diente (ej: "11", "36", "55").
    /// Adultos: 11-48. 
    /// </summary>
    [Required]
    [Column("numero_fdi")]
    [StringLength(5)]
    public string NumeroFdi { get; set; } = string.Empty;

    /// <summary>Nombre legible del diente (ej: "Incisivo central superior derecho")</summary>
    [Column("nombre_pieza")]
    [StringLength(150)]
    public string? NombrePieza { get; set; }

    /// <summary>Estado clínico registrado (sano, caries, endodoncia, corona, extraccion, etc.)</summary>
    [Required]
    [Column("estado")]
    [StringLength(50)]
    public string Estado { get; set; } = string.Empty;

    /// <summary>Observación libre del profesional sobre ese diente</summary>
    [Column("observacion")]
    public string? Observacion { get; set; }

    /// <summary>Fecha y hora exactas del registro</summary>
    [Required]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>FK a Profesional — quién hizo el registro (trazabilidad)</summary>
    [Column("id_profesional")]
    public int? IdProfesional { get; set; }

    /// <summary>FK a Cita — en qué cita se realizó el tratamiento</summary>
    [Column("id_cita")]
    public int? IdCita { get; set; }

    // ── Navegación ──────────────────────────────────────────
    public HistoriaClinica? HistoriaClinica { get; set; }
    public Profesional?     Profesional     { get; set; }
    public Cita?            Cita            { get; set; }
}