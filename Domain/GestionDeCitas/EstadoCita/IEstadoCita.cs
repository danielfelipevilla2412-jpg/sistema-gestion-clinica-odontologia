namespace SmileTrack_MVC.Domain.GestionDeCitas.EstadoCita;

/// <summary>
/// Interfaz que define el contrato para los diferentes estados de una Cita.
/// Implementa el patrón State (Gang of Four) para máquina de estados type-safe.
///
/// PROPÓSITO:
/// Encapsular la lógica de transiciones de estado de forma segura. Cada estado
/// conoce cuáles son sus transiciones válidas, impossibilitando estados inválidos
/// a nivel de compilación.
///
/// VENTAJAS vs int IdEstado:
/// - ✅ Imposible representar estados inválidos
/// - ✅ Transiciones validadas (compile-time + runtime)
/// - ✅ Cada estado es responsable de sí mismo (Single Responsibility)
/// - ✅ Facilita agregar lógica por estado (ej: qué hacer al confirmar)
/// - ✅ Tests unitarios por estado
///
/// EJEMPLO DE USO:
/// var cita = new Cita { Estado = new CitaProgramada() };
/// cita.Estado = cita.Estado.Confirmar(idOdontologo); // ✅ Válido
/// cita.Estado = new CitaFinalizada(); // ✅ Válido pero mejor usar TransicionarA()
///
/// // Esto es imposible de compilar:
/// // new CitaProgramada().Atender(); // ❌ Error CS0117
///
/// DIAGRAMA DE TRANSICIONES:
///         ┌──────────────────────┐
///         ▼                      │
///     Programada                 │
///     ├─ Confirmar()            │
///     │  ↓                      │
///     Confirmada               │
///     ├─ Rechazar()            │
///     │  ↓                      │
///     Rechazada                │
///     ├─ Atender()             │
///     │  ↓                      │
///     Atendida                 │
///     ├─ Finalizar()           │
///     │  ↓                      │
///     Finalizada              │
///     │                        │
///     └─ Cancelar() ────────────  (desde cualquier estado excepto Finalizada)
/// </summary>
public interface IEstadoCita
{
    /// <summary>Identificador único del estado para base de datos.</summary>
    string IdentificadorEstado { get; }

    /// <summary>Nombre legible del estado para mostrar en UI.</summary>
    string NombreEstado { get; }

    /// <summary>Si la cita puede extender su duración en este estado.</summary>
    bool EsProlongable { get; }

    /// <summary>Si la cita se ha completado (Finalizada).</summary>
    bool EstaCompletada { get; }

    /// <summary>Si la cita fue cancelada.</summary>
    bool EstaCancelada { get; }

    /// <summary>Si la cita está en un estado que requiere odontólogo asignado.</summary>
    bool RequiereOdontologo { get; }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TRANSICIONES POSIBLES - Cada método retorna el nuevo estado o lanza excepción
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Intenta confirmar la cita (cambiar a Confirmada).
    /// </summary>
    /// <param name="idOdontologo">ID del odontólogo que confirmará la cita (>0)</param>
    /// <returns>Nuevo estado (CitaConfirmada)</returns>
    /// <exception cref="InvalidOperationException">Si la transición no es válida desde este estado</exception>
    IEstadoCita Confirmar(int idOdontologo);

    /// <summary>
    /// Intenta rechazar la cita (cambiar a Rechazada).
    /// </summary>
    /// <param name="motivo">Razón del rechazo (requerido, 5-500 caracteres)</param>
    /// <returns>Nuevo estado (CitaRechazada)</returns>
    /// <exception cref="InvalidOperationException">Si la transición no es válida desde este estado</exception>
    /// <exception cref="ArgumentException">Si motivo es inválido</exception>
    IEstadoCita Rechazar(string motivo);

    /// <summary>
    /// Intenta cancelar la cita (cambiar a Cancelada).
    /// Válida desde cualquier estado EXCEPTO Finalizada.
    /// </summary>
    /// <param name="motivo">Razón de la cancelación (requerido, 5-500 caracteres)</param>
    /// <returns>Nuevo estado (CitaCancelada)</returns>
    /// <exception cref="InvalidOperationException">Si la cita ya está Finalizada</exception>
    /// <exception cref="ArgumentException">Si motivo es inválido</exception>
    IEstadoCita Cancelar(string motivo);

    /// <summary>
    /// Intenta marcar la cita como "En Curso" (cambiar a Atendida).
    /// Sólo válida desde Confirmada.
    /// </summary>
    /// <returns>Nuevo estado (CitaAtendida)</returns>
    /// <exception cref="InvalidOperationException">Si la transición no es válida desde este estado</exception>
    IEstadoCita Atender();

    /// <summary>
    /// Intenta finalizar la cita (cambiar a Finalizada).
    /// Sólo válida desde Atendida.
    /// </summary>
    /// <param name="resumen">Resumen de lo atendido (requerido)</param>
    /// <returns>Nuevo estado (CitaFinalizada)</returns>
    /// <exception cref="InvalidOperationException">Si la transición no es válida desde este estado</exception>
    /// <exception cref="ArgumentException">Si resumen es inválido</exception>
    IEstadoCita Finalizar(string resumen);

    // ═══════════════════════════════════════════════════════════════════════════════
    // MÉTODOS DE CONSULTA - Para lógica que depende del estado
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene descripción legible de qué acciones son posibles desde este estado.
    /// </summary>
    string ObtenerAccionesDisponibles();

    /// <summary>
    /// Valida si es posible transicionar a un estado específico.
    /// Útil para UI (mostrar/ocultar botones).
    /// </summary>
    /// <param name="nuevoEstado">Estado destino</param>
    /// <returns>true si la transición es posible, false en caso contrario</returns>
    bool PudeTransicionarA(IEstadoCita nuevoEstado);
}

/// <summary>
/// Tipo enumerado con todos los estados posibles de una cita.
/// Usado para la base de datos y validaciones.
/// </summary>
public enum TipoEstadoCita
{
    /// <summary>Cita registrada pero no confirmada.</summary>
    Programada = 1,

    /// <summary>Cita confirmada por odontólogo/recepcionista.</summary>
    Confirmada = 2,

    /// <summary>Cita rechazada (no se atenderá).</summary>
    Rechazada = 3,

    /// <summary>Cita en atención (en curso).</summary>
    Atendida = 4,

    /// <summary>Cita completada y cerrada.</summary>
    Finalizada = 5,

    /// <summary>Cita cancelada por paciente/clínica después de ser programada.</summary>
    Cancelada = 6
}
