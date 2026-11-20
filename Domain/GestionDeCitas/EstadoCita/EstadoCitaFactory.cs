namespace SmileTrack_MVC.Domain.GestionDeCitas.EstadoCita;

/// <summary>
/// Factory para crear instancias de IEstadoCita a partir de identificadores
/// y datos persistidos en la base de datos.
///
/// Centraliza la lógica de reconstrucción de estados para garantizar
/// consistencia y evitar errores de mapeado manual.
///
/// EJEMPLO DE USO:
///   var estadoDb = dbContext.Citas.Find(1).IdEstado; // 2 (Confirmada)
///   var estado = EstadoCitaFactory.ObtenerEstado(estadoDb);
///   // estado es CitaConfirmada
/// </summary>
public static class EstadoCitaFactory
{
    /// <summary>
    /// Construye la instancia correcta de IEstadoCita basado en el identificador.
    /// </summary>
    /// <param name="identificadorEstado">
    /// Identificador del estado: "Programada", "Confirmada", "Atendida", etc.
    /// </param>
    /// <param name="datosAdicionales">
    /// Diccionario opcional con datos para reconstruir estado (ej: IdOdontologo, resumen).
    /// </param>
    /// <returns>Instancia de IEstadoCita</returns>
    /// <exception cref="ArgumentException">Si el identificador es inválido</exception>
    public static IEstadoCita ObtenerEstado(
        string identificadorEstado,
        Dictionary<string, object>? datosAdicionales = null)
    {
        datosAdicionales ??= new Dictionary<string, object>();

        return identificadorEstado?.ToLowerInvariant() switch
        {
            "programada" => new CitaProgramada(),

            "confirmada" => new CitaConfirmada(
                ObtenerInt(datosAdicionales, "IdOdontologo")),

            "atendida" => new CitaAtendida(
                ObtenerInt(datosAdicionales, "IdOdontologo")),

            "finalizada" => new CitaFinalizada(
                ObtenerString(datosAdicionales, "Resumen")),

            "cancelada" => new CitaCancelada(
                ObtenerString(datosAdicionales, "Motivo")),

            "rechazada" => new CitaRechazada(
                ObtenerString(datosAdicionales, "Motivo")),

            null or "" => throw new ArgumentException(
                "El identificador de estado no puede estar vacío",
                nameof(identificadorEstado)),

            _ => throw new ArgumentException(
                $"Identificador de estado desconocido: '{identificadorEstado}'. " +
                "Estados válidos: Programada, Confirmada, Atendida, Finalizada, Cancelada, Rechazada",
                nameof(identificadorEstado))
        };
    }

    /// <summary>
    /// Construye la instancia correcta basado en el enum TipoEstadoCita.
    /// </summary>
    public static IEstadoCita ObtenerEstado(
        TipoEstadoCita tipo,
        Dictionary<string, object>? datosAdicionales = null)
    {
        var identificador = tipo switch
        {
            TipoEstadoCita.Programada => "Programada",
            TipoEstadoCita.Confirmada => "Confirmada",
            TipoEstadoCita.Atendida => "Atendida",
            TipoEstadoCita.Finalizada => "Finalizada",
            TipoEstadoCita.Cancelada => "Cancelada",
            TipoEstadoCita.Rechazada => "Rechazada",
            _ => throw new ArgumentException($"Tipo desconocido: {tipo}", nameof(tipo))
        };

        return ObtenerEstado(identificador, datosAdicionales);
    }

    /// <summary>
    /// Convierte TipoEstadoCita a IdEstado (usado en BD).
    /// </summary>
    public static int ObtenerIdEstadoDb(TipoEstadoCita tipo)
        => (int)tipo;

    /// <summary>
    /// Obtiene el TipoEstadoCita a partir de un identificador de string.
    /// </summary>
    public static TipoEstadoCita ObtenerTipo(string identificadorEstado)
    {
        return identificadorEstado?.ToLowerInvariant() switch
        {
            "programada" => TipoEstadoCita.Programada,
            "confirmada" => TipoEstadoCita.Confirmada,
            "atendida" => TipoEstadoCita.Atendida,
            "finalizada" => TipoEstadoCita.Finalizada,
            "cancelada" => TipoEstadoCita.Cancelada,
            "rechazada" => TipoEstadoCita.Rechazada,
            null or "" => throw new ArgumentException("Estado vacío", nameof(identificadorEstado)),
            _ => throw new ArgumentException($"Estado inválido: {identificadorEstado}", nameof(identificadorEstado))
        };
    }

    /// <summary>
    /// Obtiene el nombre legible del estado en español.
    /// </summary>
    public static string ObtenerNombreEstado(TipoEstadoCita tipo)
    {
        return tipo switch
        {
            TipoEstadoCita.Programada => "Programada",
            TipoEstadoCita.Confirmada => "Confirmada",
            TipoEstadoCita.Atendida => "En Atención",
            TipoEstadoCita.Finalizada => "Finalizada",
            TipoEstadoCita.Cancelada => "Cancelada",
            TipoEstadoCita.Rechazada => "Rechazada",
            _ => "Desconocido"
        };
    }

    /// <summary>
    /// Obtiene todos los estados posibles desde el estado actual.
    /// Útil para poblar dropdowns en formularios.
    /// </summary>
    public static IEnumerable<TipoEstadoCita> ObtenerTransicionesDisponibles(IEstadoCita estadoActual)
    {
        if (estadoActual is CitaProgramada)
            yield return TipoEstadoCita.Confirmada;
        yield return TipoEstadoCita.Rechazada;
        yield return TipoEstadoCita.Cancelada;

        if (estadoActual is CitaConfirmada)
            yield return TipoEstadoCita.Atendida;

        if (estadoActual is CitaAtendida)
            yield return TipoEstadoCita.Finalizada;
    }

    // ═ HELPERS ═════════════════════════════════════════════════════════════════

    private static int ObtenerInt(Dictionary<string, object> datos, string clave)
    {
        if (datos.TryGetValue(clave, out var valor))
        {
            return valor switch
            {
                int intValue => intValue,
                string strValue when int.TryParse(strValue, out var parsed) => parsed,
                _ => throw new ArgumentException(
                    $"No se pudo convertir {clave} a int: {valor}",
                    nameof(clave))
            };
        }
        throw new ArgumentException($"Dato requerido no encontrado: {clave}", clave);
    }

    private static string ObtenerString(Dictionary<string, object> datos, string clave)
    {
        if (datos.TryGetValue(clave, out var valor))
        {
            return valor?.ToString() ?? throw new ArgumentException(
                $"Valor de {clave} es nulo",
                nameof(clave));
        }
        throw new ArgumentException($"Dato requerido no encontrado: {clave}", clave);
    }
}
