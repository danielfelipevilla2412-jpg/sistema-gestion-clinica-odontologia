/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Citas
 * Componente: EstadoCitaHelper (Clase Helper)
 * ============================================
 * Archivo: Helpers/EstadoCitaHelper.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Centraliza la normalización y resolución de estados de las citas odontológicas. Evita
 * inconsistencias de formato (mayúsculas, tildes, sinónimos como "completada" vs "atendida")
 * entre la base de datos, los servicios backend y la interfaz gráfica.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Normaliza sinónimos de estados a las claves canónicas reconocidas por la BD ("programada", "en_proceso", "atendida", "cancelada", etc.).
 * - Elimina tildes, convierte a minúsculas y reemplaza guiones/espacios por guiones bajos.
 *
 * DEPENDENCIAS TÉCNICAS:
 * - System.Globalization, System.Text
 * ============================================
 */

using System.Globalization;
using System.Text;

namespace SmileTrack_MVC.Helpers;

public static class EstadoCitaHelper
{
    public static readonly string[] EstadosQueLiberanRecursos =
    [
        "cancelada", "cancelado", "rechazada", "rechazado", "solicitada", "solicitado", "pendiente"
    ];

    /// <summary>
    /// Estados que representan el cierre definitivo de una cita (ya no requiere seguimiento).
    /// Se expone como arreglo para poder usarlo directamente en consultas EF: .Contains(...) se
    /// traduce a una cláusula IN en SQL.
    /// </summary>
    public static readonly string[] EstadosFinales =
    [
        "atendida", "atendido", "completada", "completado", "realizada", "realizado",
        "finalizada", "finalizado",
        "cancelada", "cancelado",
        "no asistio", "no asistió", "no_asistida", "no-show", "no_show",
        "rechazada"
    ];

    /// <summary>
    /// Indica si la cita ya está cerrada (atendida, cancelada o no asistió) y por tanto no debe
    /// considerarse "en espera" aunque su fecha ya haya pasado.
    /// </summary>
    public static bool EsEstadoFinal(string? estado)
        => !string.IsNullOrWhiteSpace(estado)
           && EstadosFinales.Contains(estado.Trim().ToLowerInvariant());

    public static string Normalize(string? estado)
    {
        string normalized = string.Concat((estado ?? string.Empty).Trim().Normalize(NormalizationForm.FormD)
                .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        return normalized switch
        {
            "solicitado" => "solicitada",
            "agendada" or "programado" => "programada",
            "confirmado" => "confirmada",
            "en_consulta" or "en_atencion" or "en_proceso" => "en_proceso",
            "en_espera" or "sala_de_espera" or "en_sala_espera" or "en_sala_de_espera" => "en_sala_de_espera",
            "realizada" or "completada" or "finalizada" => "atendida",
            "cancelado" => "cancelada",
            "no_asistio" or "no_show" => "no_asistida",
            _ => normalized
        };
    }

    public static string ResolveEstadoNombre(string? estado, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "Programada" : ResolveEstadoNombre(fallback);
        }

        return Normalize(estado) switch
        {
            "solicitada" => "Solicitada",
            "programada" => "Programada",
            "confirmada" => "Confirmada",
            "en_sala_de_espera" => "En sala de espera",
            "en_proceso" => "En atención",
            "atendida" => "Atendida",
            "rechazada" => "Rechazada",
            "cancelada" => "Cancelada",
            "no_asistida" => "No asistió",
            _ => string.IsNullOrEmpty(estado) ? string.Empty : char.ToUpperInvariant(estado.Trim()[0]) + estado.Trim()[1..].ToLowerInvariant()
        };
    }

    public static bool IsTransitionAllowed(string? estadoActual, string? nuevoEstado)
    {
        string actual = Normalize(estadoActual);
        string siguiente = Normalize(nuevoEstado);

        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(siguiente))
            return false;

        return (actual, siguiente) switch
        {
            ("solicitada", "programada" or "confirmada") => true,
            ("programada", "confirmada" or "en_sala_de_espera" or "no_asistida") => true,
            ("confirmada", "en_sala_de_espera" or "no_asistida") => true,
            ("en_sala_de_espera", "en_proceso" or "no_asistida") => true,
            ("en_proceso", "atendida") => true,
            _ => false
        };
    }

    public static bool PuedeTransicionarPorRol(
        string? estadoActual,
        string? nuevoEstado,
        bool esAdministrador,
        bool esRecepcionista,
        bool esProfesional)
    {
        string actual = Normalize(estadoActual);
        string destino = Normalize(nuevoEstado);

        if (string.IsNullOrWhiteSpace(destino))
            return false;
        if (actual == destino || esAdministrador)
            return true;
        if (esProfesional)
            return destino is "en_proceso" or "atendida";
        if (esRecepcionista)
            return destino is not ("en_proceso" or "atendida");
        return false;
    }

    public static bool PuedeCrearPorRol(string? estado, bool esAdministrador)
        => esAdministrador || Normalize(estado) is "programada" or "confirmada";

    /// <summary>
    /// Construye un mensaje legible para el usuario cuando una transición de estado
    /// no está permitida. Fuente canónica única para toda la aplicación.
    /// </summary>
    public static string ConstruirMensajeTransicionNoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "solicitada" => "Una solicitud solo puede pasar a Programada o Confirmada. Para cancelar o rechazar la solicitud use la acción correspondiente.",
            "programada" => "Una cita programada solo puede pasar a Confirmada, En sala de espera o No asistió. Para cancelar la cita use la función de cancelación.",
            "confirmada" => "Una cita confirmada solo puede pasar a En sala de espera o No asistió. Para cancelar la cita use la función de cancelación.",
            "en_sala_de_espera" => "Una cita en sala de espera solo puede pasar a En atención o No asistió. Para cancelar la cita use la función de cancelación.",
            "en_proceso" => "Una cita en atención solo puede pasar a Atendida.",
            "atendida" or "finalizada" => "Una cita atendida ya está finalizada y no puede regresar a otro estado.",
            "cancelada" => "Una cita cancelada es definitiva y no puede reactivarse desde la agenda del profesional.",
            "no_asistida" => "Una cita marcada como No asistió es definitiva y no puede reactivarse desde la agenda del profesional.",
            _ => $"No está permitido cambiar una cita de '{estadoActual}' a '{nuevoEstado}'."
        };
    }

    /// <summary>
    /// Evalúa si un texto de estado corresponde a alguna variante de "cancelada".
    /// </summary>
    public static bool EsEstadoCancelado(string? estado)
    {
        return Normalize(estado) is "cancelada" or "cancelado";
    }
}
