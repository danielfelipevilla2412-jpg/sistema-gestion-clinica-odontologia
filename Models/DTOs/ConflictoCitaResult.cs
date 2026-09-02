namespace SmileTrack_MVC.Models.DTOs;

/// <summary>
/// Resultado del chequeo de conflictos de horario para una cita.
/// Reporta si hay solapamiento y qué recurso origina el conflicto,
/// permitiendo mostrar mensajes específicos al usuario.
/// </summary>
public sealed class ConflictoCitaResult
{
    /// <summary>Indica si existe al menos un conflicto de horario.</summary>
    public bool HayConflicto { get; init; }

    /// <summary>
    /// Tipo de recurso en conflicto.
    /// Vacío cuando <see cref="HayConflicto"/> es <c>false</c>.
    /// </summary>
    public TipoConflictoCita Tipo { get; init; }

    /// <summary>Mensaje descriptivo listo para mostrar al usuario.</summary>
    public string Mensaje { get; init; } = string.Empty;

    // ── Constructores de fábrica ──────────────────────────────────────────

    /// <summary>No hay conflicto.</summary>
    public static ConflictoCitaResult SinConflicto() => new()
    {
        HayConflicto = false,
        Tipo = TipoConflictoCita.Ninguno,
        Mensaje = string.Empty
    };

    /// <summary>Conflicto de disponibilidad del profesional.</summary>
    public static ConflictoCitaResult ConflictoProfesional() => new()
    {
        HayConflicto = true,
        Tipo = TipoConflictoCita.Profesional,
        Mensaje = "El profesional ya tiene una cita asignada en ese horario. Seleccione otro horario o profesional."
    };

    /// <summary>Conflicto de disponibilidad del paciente.</summary>
    public static ConflictoCitaResult ConflictoPaciente() => new()
    {
        HayConflicto = true,
        Tipo = TipoConflictoCita.Paciente,
        Mensaje = "El paciente ya tiene otra cita registrada en ese mismo horario."
    };

    /// <summary>Conflicto de ocupación del consultorio.</summary>
    public static ConflictoCitaResult ConflictoConsultorio() => new()
    {
        HayConflicto = true,
        Tipo = TipoConflictoCita.Consultorio,
        Mensaje = "El consultorio ya está ocupado por otra cita en ese horario. Seleccione otro consultorio u horario."
    };
}

/// <summary>Clasifica el recurso que origina el conflicto de horario.</summary>
public enum TipoConflictoCita
{
    /// <summary>Sin conflicto.</summary>
    Ninguno = 0,

    /// <summary>El profesional ya tiene cita en ese bloque.</summary>
    Profesional = 1,

    /// <summary>El paciente ya tiene cita en ese bloque.</summary>
    Paciente = 2,

    /// <summary>El consultorio ya está ocupado en ese bloque.</summary>
    Consultorio = 3
}
