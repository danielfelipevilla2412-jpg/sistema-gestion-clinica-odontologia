namespace SmileTrack_MVC.Domain.GestionDeCitas.EstadoCita;

/// <summary>
/// Cita en atención (En Curso).
///
/// TRANSICIONES VÁLIDAS:
/// ✅ Finalizar(resumen) → CitaFinalizada
/// ✅ Cancelar(motivo) → CitaCancelada
/// ❌ Confirmar() → INVALID
/// ❌ Rechazar() → INVALID
/// ❌ Atender() → INVALID (ya está siendo atendida)
/// </summary>
public sealed class CitaAtendida : IEstadoCita
{
    public int IdOdontologo { get; }
    public DateTime FechaInicio { get; }

    public string IdentificadorEstado => "Atendida";
    public string NombreEstado => "En Atención";
    public bool EsProlongable => true;
    public bool EstaCompletada => false;
    public bool EstaCancelada => false;
    public bool RequiereOdontologo => true;

    public CitaAtendida(int idOdontologo)
    {
        if (idOdontologo <= 0)
            throw new ArgumentException("Odontólogo inválido", nameof(idOdontologo));
        IdOdontologo = idOdontologo;
        FechaInicio = DateTime.Now;
    }

    public IEstadoCita Confirmar(int idOdontologo)
        => throw new InvalidOperationException("La cita ya está en atención");

    public IEstadoCita Rechazar(string motivo)
        => throw new InvalidOperationException("No se puede rechazar una cita en atención");

    public IEstadoCita Cancelar(string motivo)
    {
        ValidarMotivo(motivo);
        return new CitaCancelada(motivo);
    }

    public IEstadoCita Atender()
        => throw new InvalidOperationException("La cita ya está siendo atendida");

    public IEstadoCita Finalizar(string resumen)
    {
        if (string.IsNullOrWhiteSpace(resumen))
            throw new ArgumentException("Resumen requerido", nameof(resumen));
        return new CitaFinalizada(resumen);
    }

    public string ObtenerAccionesDisponibles()
        => "Finalizar (Completar consulta), Cancelar";

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
        => nuevoEstado is CitaFinalizada or CitaCancelada;

    private static void ValidarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo requerido", nameof(motivo));
        if (motivo.Length < 5 || motivo.Length > 500)
            throw new ArgumentException("Motivo debe estar entre 5 y 500 caracteres", nameof(motivo));
    }
}

/// <summary>
/// Cita completada y finalizada.
/// Estado terminal: no hay más transiciones posibles.
/// </summary>
public sealed class CitaFinalizada : IEstadoCita
{
    public string Resumen { get; }
    public DateTime FechaFinalizacion { get; }

    public string IdentificadorEstado => "Finalizada";
    public string NombreEstado => "Finalizada";
    public bool EsProlongable => false;
    public bool EstaCompletada => true;
    public bool EstaCancelada => false;
    public bool RequiereOdontologo => true;

    public CitaFinalizada(string resumen)
    {
        if (string.IsNullOrWhiteSpace(resumen))
            throw new ArgumentException("Resumen requerido", nameof(resumen));
        Resumen = resumen;
        FechaFinalizacion = DateTime.Now;
    }

    public IEstadoCita Confirmar(int idOdontologo)
        => throw new InvalidOperationException("Una cita finalizada no puede ser modificada");

    public IEstadoCita Rechazar(string motivo)
        => throw new InvalidOperationException("Una cita finalizada no puede ser modificada");

    public IEstadoCita Cancelar(string motivo)
        => throw new InvalidOperationException("No se puede cancelar una cita ya finalizada");

    public IEstadoCita Atender()
        => throw new InvalidOperationException("Una cita finalizada no puede ser atendida nuevamente");

    public IEstadoCita Finalizar(string resumen)
        => throw new InvalidOperationException("La cita ya está finalizada");

    public string ObtenerAccionesDisponibles()
        => "Ninguna (cita finalizada y archivada)";

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
        => false; // Estado terminal
}

/// <summary>
/// Cita cancelada por paciente, recepcionista o sistema.
/// Estado terminal: la cita no se llevará a cabo.
/// </summary>
public sealed class CitaCancelada : IEstadoCita
{
    public string Motivo { get; }
    public DateTime FechaCancelacion { get; }

    public string IdentificadorEstado => "Cancelada";
    public string NombreEstado => "Cancelada";
    public bool EsProlongable => false;
    public bool EstaCompletada => true;
    public bool EstaCancelada => true;
    public bool RequiereOdontologo => false;

    public CitaCancelada(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo requerido", nameof(motivo));
        Motivo = motivo;
        FechaCancelacion = DateTime.Now;
    }

    public IEstadoCita Confirmar(int idOdontologo)
        => throw new InvalidOperationException("No se puede confirmar una cita cancelada");

    public IEstadoCita Rechazar(string motivo)
        => throw new InvalidOperationException("No se puede rechazar una cita cancelada");

    public IEstadoCita Cancelar(string motivo)
        => throw new InvalidOperationException("La cita ya está cancelada");

    public IEstadoCita Atender()
        => throw new InvalidOperationException("No se puede atender una cita cancelada");

    public IEstadoCita Finalizar(string resumen)
        => throw new InvalidOperationException("No se puede finalizar una cita cancelada");

    public string ObtenerAccionesDisponibles()
        => "Ninguna (cita cancelada)";

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
        => false; // Estado terminal
}

/// <summary>
/// Cita rechazada por el sistema o recepcionista.
/// Estado terminal: la cita fue rechazada en fase Programada.
/// </summary>
public sealed class CitaRechazada : IEstadoCita
{
    public string Motivo { get; }
    public DateTime FechaRechazo { get; }

    public string IdentificadorEstado => "Rechazada";
    public string NombreEstado => "Rechazada";
    public bool EsProlongable => false;
    public bool EstaCompletada => true;
    public bool EstaCancelada => false;
    public bool RequiereOdontologo => false;

    public CitaRechazada(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo requerido", nameof(motivo));
        Motivo = motivo;
        FechaRechazo = DateTime.Now;
    }

    public IEstadoCita Confirmar(int idOdontologo)
        => throw new InvalidOperationException("No se puede confirmar una cita rechazada");

    public IEstadoCita Rechazar(string motivo)
        => throw new InvalidOperationException("La cita ya está rechazada");

    public IEstadoCita Cancelar(string motivo)
        => throw new InvalidOperationException("No se puede cancelar una cita rechazada. Ya fue rechazada desde el inicio.");

    public IEstadoCita Atender()
        => throw new InvalidOperationException("No se puede atender una cita rechazada");

    public IEstadoCita Finalizar(string resumen)
        => throw new InvalidOperationException("No se puede finalizar una cita rechazada");

    public string ObtenerAccionesDisponibles()
        => "Ninguna (cita rechazada)";

    public bool PudeTransicionarA(IEstadoCita nuevoEstado)
        => false; // Estado terminal
}
