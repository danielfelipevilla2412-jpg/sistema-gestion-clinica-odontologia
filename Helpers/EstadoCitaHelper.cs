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
        "no asistio", "no asistió", "no_asistida", "no-show", "no_show"
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
            "en_consulta" or "en_proceso" => "en_proceso",
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
            "en_proceso" => "En consulta",
            "atendida" => "Atendida",
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
            ("solicitada", "programada") => true,
            ("programada", "confirmada" or "en_sala_de_espera") => true,
            ("confirmada", "en_sala_de_espera" or "en_proceso") => true,
            ("en_sala_de_espera", "en_proceso") => true,
            ("en_proceso", "atendida") => true,
            _ => false
        };
    }
}
