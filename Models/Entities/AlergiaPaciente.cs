using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - Entidad AlergiaPaciente
//
// MOTIVO: el campo Paciente.Alergias era texto libre ("Penicilina, Ibuprofeno").
// Eso impedía:
//   - Buscar pacientes por sustancia específica.
//   - Registrar severidad (leve / moderada / grave).
//   - Registrar el tipo (medicamento / alimento / ambiental / látex / otro).
//   - Registrar qué reacción produce (urticaria, anafilaxia, etc.).
//
// ESTRATEGIA DE COEXISTENCIA:
//   El campo Paciente.Alergias no se elimina. Sigue usándose para compatibilidad
//   con el código existente (preparacion, detalle, listado). Esta tabla nueva es
//   ADICIONAL: permite tener alergias estructuradas para alertas y búsqueda,
//   mientras el campo de texto libre sigue funcionando para compatibilidad.
//
// RELACIONES:
//   Alergia_Paciente → Paciente  (CASCADE delete — si se borra el paciente,
//                                  se borran sus alergias estructuradas)
//
// ÍNDICE IX_AP_Paciente: permite listar las alergias de un paciente eficientemente.
// ─────────────────────────────────────────────────────────────────────────────

[Table("Alergia_Paciente")]
public class AlergiaPaciente
{
    // ── Clave primaria ────────────────────────────────────────────────────────

    [Key]
    [Column("id_alergia")]
    public int IdAlergia { get; set; }

    // ── FK al paciente ────────────────────────────────────────────────────────

    /// <summary>Paciente al que pertenece esta alergia.</summary>
    [Required]
    [Column("id_paciente")]
    public int IdPaciente { get; set; }

    // ── Datos de la alergia ───────────────────────────────────────────────────

    /// <summary>
    /// Sustancia o agente causante.
    /// Ejemplos: "Penicilina", "Mariscos", "Polen", "Látex".
    /// </summary>
    [Required]
    [Column("sustancia")]
    [StringLength(150)]
    public string Sustancia { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de alergia.
    /// Valores válidos: medicamento | alimento | ambiental | latex | otro
    /// CHECK en BD, ver script SQL.
    /// </summary>
    [Required]
    [Column("tipo")]
    [StringLength(15)]
    public string Tipo { get; set; } = "otro";

    /// <summary>
    /// Severidad clínica de la reacción alérgica.
    /// Valores válidos: leve | moderada | grave
    /// CHECK en BD, ver script SQL.
    /// </summary>
    [Required]
    [Column("severidad")]
    [StringLength(10)]
    public string Severidad { get; set; } = "leve";

    /// <summary>
    /// Descripción de la reacción observada.
    /// Ejemplo: "Urticaria generalizada", "Anafilaxia", "Edema labial".
    /// Opcional.
    /// </summary>
    [Column("reaccion")]
    [StringLength(300)]
    public string? Reaccion { get; set; }

    /// <summary>Fecha y hora en que se registró la alergia (UTC).</summary>
    [Required]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    /// <summary>Activa = true / false para desactivar sin borrar (soft-delete).</summary>
    [Required]
    [Column("activa")]
    public bool Activa { get; set; } = true;

    // ── Navegación ────────────────────────────────────────────────────────────
    public Paciente? Paciente { get; set; }
}
