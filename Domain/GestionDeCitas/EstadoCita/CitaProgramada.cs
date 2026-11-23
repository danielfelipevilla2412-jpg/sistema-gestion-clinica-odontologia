namespace SmileTrack_MVC.Domain.GestionDeCitas.EstadoCita;

/// <summary>
/// Estado inicial de una Cita cuando se crea sin confirmar.
///
/// TRANSICIONES VÁLIDAS DESDE ESTE ESTADO:
/// ✅ Confirmar(idOdontologo) → CitaConfirmada
/// ✅ Rechazar(motivo) → CitaRechazada
/// ✅ Cancelar(motivo) → CitaCancelada
/// ❌ Atender() → INVALID
/// ❌ Finalizar() → INVALID
///
/// CARACTERÍSTICAS:
/// - No requiere odontólogo aún (puede estar sin asignar)
/// - No es prolongable (no se puede extender duración)
/// - Se puede cambiar a Rechazada o Cancelada sin confirmación previa
/// </summary>
public sealed class CitaProgramada : IEstadoCita
{
    public string IdentificadorEstado => "Programada";
    public string NombreEstado => "Programada";
    public bool EsProlongable => false;
    public bool EstaCompletada => false;
    public bool EstaCancelada => false;
    public bool RequiereOdontologo => false;

    /// <summary>
    /// Transiciona de Programada a Confirmada.
    /// </summary>
    public IEstadoCita Confirmar(int idOdontologo)
    {
        if (idOdontologo <= 0)
            throw new ArgumentException(
                "Odontólogo inválido. Debe proporcionar un ID > 0",
                nameof(idOdontologo));

        return new CitaConfirmada(idOdontologo);
    }

    /// <summary>
    /// Rechaza la cita antes de ser confirmada.
    /// </summary>
    public IEstadoCita Rechazar(string motivo)
    {
        ValidarMotivo(motivo);
        return new CitaRechazada(motivo);
    }

    /// <summary>
    /// Cancela la cita desde estado Programada.
    /// </summary>
    public IEstadoCita Cancelar(string motivo)
    {
        ValidarMotivo(motivo);
        return new CitaCancelada(motivo);
    }

    /// <summary>
    /// No se puede atender una cita que no está confirmada.
    /// </summary>
    public IEstadoCita Atender()
    {
        throw new InvalidOperationException(
            "No se puede atender una cita que está en estado 'Programada'. " +
            "Primero debe ser confirmada por el odontólogo.");
    }

    /// <summary>
    /// No se puede finalizar una cita que no está siendo atendida.
    /// </summary>
    public IEstadoCita Finalizar(string resumen)
    {
        throw new InvalidOperationException(
            "No se puede finalizar una cita que está en estado 'Programada'. " +
            "Primero debe ser confirmada y atendida.");
    }

    public string ObtenerAccionesDisponibles()
    {
        return "Confirmar, Rechazar, Cancelar";
    }

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
    {
        return nuevoEstado is CitaConfirmada or CitaRechazada or CitaCancelada;
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
