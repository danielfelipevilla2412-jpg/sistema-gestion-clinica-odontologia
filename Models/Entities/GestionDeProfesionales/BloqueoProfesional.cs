namespace SmileTrack_MVC.Models.Entities;

/// <summary>
/// Registra bloqueos de agenda de un profesional (junta, urgencia, etc.).
/// El bloqueo define un rango DATETIME de inicio a fin, que puede cruzar varios días.
/// Mapeado a la tabla Bloqueo_Profesional existente en BD
/// (P-01 / U-06 — tablas huérfanas resueltas).
/// </summary>
public class BloqueoProfesional
{
    public int IdBloqueo { get; set; }

    /// <summary>ID del profesional bloqueado.</summary>
    public int IdProfesional { get; set; }

    /// <summary>Fecha y hora de inicio del bloqueo.</summary>
    public DateTime FechaInicio { get; set; }

    /// <summary>Fecha y hora de fin del bloqueo.</summary>
    public DateTime FechaFin { get; set; }

    /// <summary>Motivo del bloqueo.</summary>
    public string? Motivo { get; set; }

    /// <summary>ID del usuario administrador que aprobó el bloqueo (opcional).</summary>
    public int? AprobadoPor { get; set; }

    // ── Navegación ──────────────────────────────────────────────────────
    public Profesional? Profesional { get; set; }
}
