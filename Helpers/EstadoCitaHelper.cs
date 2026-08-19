namespace SmileTrack_MVC.Helpers;

public static class EstadoCitaHelper
{
    public static string ResolveEstadoNombre(string? estado, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "Programada" : NormalizeEstado(fallback);
        }

        return NormalizeEstado(estado);
    }

    private static string NormalizeEstado(string? estado)
    {
        string normalized = (estado ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "programada" or "agendada" => "Programada",
            "confirmada" or "confirmado" => "Confirmada",
            "atendida" or "realizada" or "completada" => "Atendida",
            "cancelada" or "cancelado" => "Cancelada",
            _ => string.IsNullOrEmpty(normalized) ? string.Empty : char.ToUpperInvariant(normalized[0]) + normalized[1..].ToLowerInvariant()
        };
    }
}
