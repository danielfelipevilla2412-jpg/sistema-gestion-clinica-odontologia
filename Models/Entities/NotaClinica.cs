using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

// Yeray - Nueva tabla Nota_Clinica.
// Antes cada nota clínica se guardaba como un elemento dentro del arreglo
// JSON "notasClinicas", empacado como texto en Historia_Clinica.observaciones_generales.
// Eso hacía imposible filtrar u ordenar notas con SQL sin traer el blob completo
// y parsearlo en C#. Ahora cada nota es una fila real con su propia fecha,
// profesional y diagnóstico, consultable directamente.
//
// Mientras el visor de historial (st-odo-03-historial) siga leyendo el JSON para
// pintar la pantalla, GuardarNotaClinica sigue escribiendo también en el JSON
// (ver comentario "Yeray" en HistoriaClinicaController). Esta tabla es la fuente
// real para la API nueva; el JSON queda como compatibilidad temporal con esa vista.
[Table("Nota_Clinica")]
public class NotaClinica
{
    [Key]
    [Column("id_nota")]
    public int IdNota { get; set; }

    /// <summary>FK a Historia_Clinica — una HC puede tener muchas notas.</summary>
    [Required]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    /// <summary>FK a Profesional — quién registró la nota (trazabilidad real).</summary>
    [Column("id_profesional")]
    public int? IdProfesional { get; set; }

    [Required]
    [Column("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [Column("diagnostico")]
    public string? Diagnostico { get; set; }

    [Column("procedimiento")]
    public string? Procedimiento { get; set; }

    /// <summary>Texto libre para la próxima cita sugerida (no es una fecha real de Cita).</summary>
    [Column("proxima_cita")]
    [StringLength(50)]
    public string? ProximaCita { get; set; }

    [Required]
    [Column("estado")]
    [StringLength(20)]
    public string Estado { get; set; } = "Realizado";

    // ── Navegación ──────────────────────────────────────────
    public HistoriaClinica? HistoriaClinica { get; set; }
    public Profesional? Profesional { get; set; }
}
