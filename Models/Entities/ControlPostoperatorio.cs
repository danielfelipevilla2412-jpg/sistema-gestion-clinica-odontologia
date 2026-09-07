using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

// Yeray - Nueva tabla Control_Postoperatorio.
// Antes el control postoperatorio de cada cita se guardaba como una entrada dentro
// del objeto JSON "controlesPostoperatorios", indexado a mano con el id de la cita
// convertido a texto (controlesPostoperatorios["57"] = {...}). Ahora "status" y
// "observaciones" son columnas reales con relación 1 a 1 a Cita. El checklist de
// instrucciones se queda en JSON (columna instrucciones_json) porque es una lista
// corta de forma fija por tratamiento; no amerita una tabla hija propia.
//
// Igual que con Nota_Clinica: mientras la vista st-aux-07-control-postoperato siga
// leyendo el JSON de Historia_Clinica, GuardarControlPostoperatorio sigue
// escribiendo ahí también. Esta tabla es la fuente real para la API nueva.
[Table("Control_Postoperatorio")]
public class ControlPostoperatorio
{
    [Key]
    [Column("id_control")]
    public int IdControl { get; set; }

    /// <summary>FK a Cita — una cita tiene, como máximo, un control postoperatorio.</summary>
    [Required]
    [Column("id_cita")]
    public int IdCita { get; set; }

    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = "stable";

    [Column("instrucciones_json")]
    public string? InstruccionesJson { get; set; }

    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [Required]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // ── Navegación ──────────────────────────────────────────
    public Cita? Cita { get; set; }
}
