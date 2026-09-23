using System.Globalization;
using System.Text;

namespace SmileTrack_MVC.Helpers;

public static class EstadoCitaHelper
{
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
            ("programada", "confirmada" or "en_sala_de_espera" or "en_proceso" or "atendida" or "no_asistida") => true,
            ("confirmada", "en_sala_de_espera" or "en_proceso" or "atendida" or "no_asistida") => true,
            ("en_sala_de_espera", "en_proceso" or "atendida" or "no_asistida") => true,
            ("en_proceso", "atendida" or "no_asistida") => true,
            _ => false
        };
    }
}
