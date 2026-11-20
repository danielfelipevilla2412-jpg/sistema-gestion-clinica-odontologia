namespace SmileTrack_MVC.Domain.GestionDeCitas.EstadoCita;

/// <summary>
/// Estado de una Cita confirmada por odontólogo/recepcionista.
///
/// TRANSICIONES VÁLIDAS DESDE ESTE ESTADO:
/// ✅ Atender() → CitaAtendida
/// ✅ Cancelar(motivo) → CitaCancelada
/// ❌ Confirmar() → INVALID (ya está confirmada)
/// ❌ Rechazar() → INVALID (ya está aceptada)
/// ❌ Finalizar() → INVALID (no está siendo atendida aún)
///
/// CARACTERÍSTICAS:
/// - Requiere odontólogo asignado
/// - Es prolongable (se puede extender duración en clínica)
/// - Estado "en vuelo" esperando que comience la atención
/// </summary>
public sealed class CitaConfirmada : IEstadoCita
{
    /// <summary>ID del odontólogo que confirmó la cita.</summary>
    public int IdOdontologo { get; }

    /// <summary>Fecha/hora cuando se confirmó la cita.</summary>
    public DateTime FechaConfirmacion { get; }

    public string IdentificadorEstado => "Confirmada";
    public string NombreEstado => "Confirmada";
    public bool EsProlongable => true;
    public bool EstaCompletada => false;
    public bool EstaCancelada => false;
    public bool RequiereOdontologo => true;

    public CitaConfirmada(int idOdontologo)
    {
        if (idOdontologo <= 0)
            throw new ArgumentException(
                "Odontólogo inválido. Debe proporcionar un ID > 0",
                nameof(idOdontologo));

        IdOdontologo = idOdontologo;
        FechaConfirmacion = DateTime.Now;
    }

    /// <summary>
    /// No se puede confirmar algo que ya está confirmado.
    /// </summary>
    public IEstadoCita Confirmar(int idOdontologo)
    {
        throw new InvalidOperationException(
            "La cita ya está confirmada. No se puede confirmar nuevamente. " +
            "Si necesita cambiar el odontólogo, cancele y cree una nueva cita.");
    }

    /// <summary>
    /// No se puede rechazar una cita ya confirmada.
    /// Use Cancelar en su lugar.
    /// </summary>
    public IEstadoCita Rechazar(string motivo)
    {
        throw new InvalidOperationException(
            "No se puede rechazar una cita confirmada. " +
            "Use la opción Cancelar para cancelar la cita.");
    }

    /// <summary>
    /// Cancela la cita confirmada (ej: paciente no puede asistir).
    /// </summary>
    public IEstadoCita Cancelar(string motivo)
    {
        ValidarMotivo(motivo);
        return new CitaCancelada(motivo);
    }

    /// <summary>
    /// Comienza la atención de la cita (pasa a En Curso).
    /// </summary>
    public IEstadoCita Atender()
    {
        return new CitaAtendida(IdOdontologo);
    }

    /// <summary>
    /// No se puede finalizar sin pasar por Atendida primero.
    /// </summary>
    public IEstadoCita Finalizar(string resumen)
    {
        throw new InvalidOperationException(
            "No se puede finalizar una cita que está 'Confirmada'. " +
            "Primero debe cambiar a estado 'Atendida' (En Curso).");
    }

    public string ObtenerAccionesDisponibles()
    {
        return "Atender (Iniciar consulta), Cancelar";
    }

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
    {
        return nuevoEstado is CitaAtendida or CitaCancelada;
    }

    // ═ HELPERS ═════════════════════════════════════════════════════════════════

    private static void ValidarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo requerido", nameof(motivo));

        if (motivo.Length < 5)
            throw new ArgumentException("Motivo debe tener mínimo 5 caracteres", nameof(motivo));

        if (motivo.Length > 500)
            throw new ArgumentException("Motivo no puede exceder 500 caracteres", nameof(motivo));
    }
}
