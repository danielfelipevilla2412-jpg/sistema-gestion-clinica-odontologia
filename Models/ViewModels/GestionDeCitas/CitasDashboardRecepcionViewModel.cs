using System.Text.Json.Serialization;

namespace SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

public sealed class CitasDashboardRecepcionViewModel
{
    [JsonPropertyName("appointments")]
    public IReadOnlyList<CitaDashboardRecepcionViewModelItem> Appointments { get; init; } = [];

    [JsonPropertyName("fechaHoraTexto")]
    public string FechaHoraTexto { get; init; } = string.Empty;

    [JsonPropertyName("horaActualIso")]
    public string HoraActualIso { get; init; } = string.Empty;

    [JsonPropertyName("horaActualTexto")]
    public string HoraActualTexto { get; init; } = string.Empty;

    [JsonPropertyName("stats")]
    public DashboardRecepcionStatsViewModel Stats { get; init; } = new();

    [JsonPropertyName("alertas")]
    public DashboardRecepcionAlertasViewModel Alertas { get; init; } = new();

    [JsonPropertyName("proximasCitas")]
    public IReadOnlyList<CitaDashboardRecepcionProximaViewModel> ProximasCitas { get; init; } = [];
}

public sealed class CitaDashboardRecepcionViewModelItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("time")]
    public string Time { get; init; } = string.Empty;

    [JsonPropertyName("patient")]
    public string Patient { get; init; } = string.Empty;

    [JsonPropertyName("doctor")]
    public string Doctor { get; init; } = string.Empty;

    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("statusClass")]
    public string StatusClass { get; init; } = string.Empty;

    [JsonPropertyName("highlight")]
    public bool Highlight { get; init; }

    [JsonPropertyName("actions")]
    public IReadOnlyList<string> Actions { get; init; } = [];
}

public sealed class CitaDashboardRecepcionProximaViewModel
{
    [JsonPropertyName("hora")]
    public string Hora { get; init; } = string.Empty;

    [JsonPropertyName("fechaIso")]
    public string FechaIso { get; init; } = string.Empty;

    [JsonPropertyName("texto")]
    public string Texto { get; init; } = string.Empty;
}

public sealed class DashboardRecepcionStatsViewModel
{
    [JsonPropertyName("citasHoy")]
    public int CitasHoy { get; init; }

    [JsonPropertyName("confirmadas")]
    public int Confirmadas { get; init; }

    [JsonPropertyName("pendientes")]
    public int Pendientes { get; init; }

    [JsonPropertyName("facturasPendientes")]
    public string FacturasPendientes { get; init; } = string.Empty;
}

public sealed class DashboardRecepcionAlertasViewModel
{
    [JsonPropertyName("sinConfirmar")]
    public int SinConfirmar { get; init; }

    [JsonPropertyName("facturasVencidas")]
    public int FacturasVencidas { get; init; }
}
