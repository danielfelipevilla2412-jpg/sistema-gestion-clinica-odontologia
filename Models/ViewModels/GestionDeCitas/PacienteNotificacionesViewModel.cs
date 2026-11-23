using System.Text.Json.Serialization;

namespace SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

public sealed class PacienteNotificacionesViewModel
{
    [JsonPropertyName("notificaciones")]
    public IReadOnlyList<PacienteNotificacionViewModel> Notificaciones { get; init; } = [];

    [JsonPropertyName("unreadCount")]
    public int UnreadCount { get; init; }
}

public sealed class PacienteNotificacionViewModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("idNotificacion")]
    public int? IdNotificacion { get; init; }

    [JsonPropertyName("idCita")]
    public int? IdCita { get; init; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = string.Empty;

    [JsonPropertyName("time")]
    public DateTime Time { get; init; }

    [JsonPropertyName("leida")]
    public bool Leida { get; init; }

    [JsonPropertyName("badge")]
    public string Badge { get; init; } = string.Empty;

    [JsonPropertyName("canal")]
    public string Canal { get; init; } = string.Empty;

    [JsonPropertyName("urlAccion")]
    public string UrlAccion { get; init; } = string.Empty;
}
