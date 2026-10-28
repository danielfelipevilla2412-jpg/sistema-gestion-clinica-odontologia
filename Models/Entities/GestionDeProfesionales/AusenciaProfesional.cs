namespace SmileTrack_MVC.Models.Entities;

/// <summary>
/// Registra períodos de ausencia planificada de un profesional.
/// Permite bloquear el agendamiento de citas durante el rango de fechas indicado.
/// Mapeado a la tabla Ausencia_Profesional existente en BD
/// (P-01 / U-06 — tablas huérfanas resueltas).
/// </summary>
public class AusenciaProfesional
{
    public int IdAusencia { get; set; }

    /// <summary>ID del profesional ausente.</summary>
    public int IdProfesional { get; set; }

    /// <summary>
    /// Tipo de ausencia: vacaciones | incapacidad | permiso | otro.
    /// </summary>
    public string Tipo { get; set; } = "vacaciones";

    /// <summary>Fecha de inicio de la ausencia (inclusive).</summary>
    public DateOnly FechaInicio { get; set; }

    /// <summary>Fecha de fin de la ausencia (inclusive).</summary>
    public DateOnly FechaFin { get; set; }

    /// <summary>Duración en días (calculada o manual).</summary>
    public int? Duracion { get; set; }

    /// <summary>Observaciones internas del administrador.</summary>
    public string? Observaciones { get; set; }

    /// <summary>ID del usuario que aprobó la ausencia.</summary>
    public int? AprobadoPor { get; set; }

    // ── Navegación ──────────────────────────────────────────────────────
    public Profesional? Profesional { get; set; }
}
