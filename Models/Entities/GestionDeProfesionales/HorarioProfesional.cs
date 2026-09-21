namespace SmileTrack_MVC.Models.Entities;

/// <summary>
/// Define el horario de atención semanal de un profesional.
/// Cada fila representa un bloque de disponibilidad para un día de la semana.
/// Se usa para validar que las citas se agendan dentro del horario real del profesional
/// (P-01 / U-06 — tablas huérfanas resueltas).
/// </summary>
public class HorarioProfesional
{
    public int IdHorario { get; set; }

    /// <summary>ID del profesional al que pertenece este horario.</summary>
    public int IdProfesional { get; set; }

    /// <summary>
    /// Día de la semana como texto (Lunes, Martes, … Domingo).
    /// Mapeado a VARCHAR(12) con CHECK constraint en la base de datos.
    /// </summary>
    public string DiaSemana { get; set; } = string.Empty;

    /// <summary>Hora de inicio del bloque de atención.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora de fin del bloque de atención.</summary>
    public TimeOnly HoraFin { get; set; }

    /// <summary>Indica si este bloque está activo.</summary>
    public bool Activo { get; set; } = true;

    // ── Navegación ──────────────────────────────────────────────────────
    public Profesional? Profesional { get; set; }
}
