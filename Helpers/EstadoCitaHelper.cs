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
            "en_consulta" => "en_proceso",
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
            "en_proceso" => "En consulta",
            "atendida" => "Atendida",
            "cancelada" => "Cancelada",
            "no_asistida" => "No asistió",
            _ => string.IsNullOrEmpty(estado) ? string.Empty : char.ToUpperInvariant(estado.Trim()[0]) + estado.Trim()[1..].ToLowerInvariant()
        };
    }
}
