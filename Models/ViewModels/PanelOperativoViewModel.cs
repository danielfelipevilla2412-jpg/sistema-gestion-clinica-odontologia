using System.Text.Json.Serialization;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class PanelOperativoViewModel
{
    [JsonPropertyName("fechaHoy")]
    public string FechaHoy { get; init; } = string.Empty;

    [JsonPropertyName("kpis")]
    public PanelOperativoKpisViewModel Kpis { get; init; } = new();

    [JsonPropertyName("proximaCita")]
    public PanelOperativoProximaCitaViewModel? ProximaCita { get; init; }

    [JsonPropertyName("citas")]
    public IReadOnlyList<PanelOperativoCitaViewModel> Citas { get; init; } = [];

    [JsonPropertyName("alertas")]
    public IReadOnlyList<PanelOperativoAlertaViewModel> Alertas { get; init; } = [];

    [JsonPropertyName("topProfesionales")]
    public IReadOnlyList<PanelOperativoTopProfesionalViewModel> TopProfesionales { get; init; } = [];
}

public sealed class PanelOperativoTopProfesionalViewModel
{
    [JsonPropertyName("nombre")]
    public string Nombre { get; init; } = string.Empty;

    [JsonPropertyName("especialidad")]
    public string Especialidad { get; init; } = "Sin especialidad";

    [JsonPropertyName("totalCitas")]
    public int TotalCitas { get; init; }
}

public sealed class PanelOperativoKpisViewModel
{
    [JsonPropertyName("citasHoy")]
    public int CitasHoy { get; init; }

    [JsonPropertyName("completadas")]
    public int Completadas { get; init; }

    [JsonPropertyName("pendientes")]
    public int Pendientes { get; init; }

    [JsonPropertyName("consultoriosDisponibles")]
    public int ConsultoriosDisponibles { get; init; }
}

public sealed class PanelOperativoCitaViewModel
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("hora")]
    public string Hora { get; init; } = string.Empty;

    [JsonPropertyName("paciente")]
    public string Paciente { get; init; } = string.Empty;

    [JsonPropertyName("profesional")]
    public string Profesional { get; init; } = string.Empty;

    [JsonPropertyName("alergia")]
    public string? Alergia { get; init; }

    [JsonPropertyName("consultorio")]
    public string Consultorio { get; init; } = string.Empty;

    [JsonPropertyName("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonPropertyName("highlight")]
    public bool Highlight { get; init; }

    [JsonPropertyName("telefono")]
    public string? Telefono { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("sangre")]
    public string Sangre { get; init; } = "N/D";

    [JsonPropertyName("edad")]
    public string Edad { get; init; } = "Edad no registrada";

    [JsonPropertyName("antecedentes")]
    public string Antecedentes { get; init; } = "Información no registrada";

    [JsonPropertyName("servicio")]
    public string Servicio { get; init; } = "Servicio no especificado";

    [JsonPropertyName("medicamentosDisponibles")]
    public bool MedicamentosDisponibles { get; init; }

    [JsonPropertyName("medicamentos")]
    public IReadOnlyList<string> Medicamentos { get; init; } = [];
}

public sealed class PanelOperativoProximaCitaViewModel
{
    [JsonPropertyName("minutosRestantes")]
    public int MinutosRestantes { get; init; }

    [JsonPropertyName("hora")]
    public string Hora { get; init; } = string.Empty;

    [JsonPropertyName("paciente")]
    public string Paciente { get; init; } = string.Empty;

    [JsonPropertyName("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonPropertyName("profesional")]
    public string Profesional { get; init; } = string.Empty;

    [JsonPropertyName("consultorio")]
    public string Consultorio { get; init; } = string.Empty;
}

public sealed class PanelOperativoAlertaViewModel
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Descripcion { get; init; } = string.Empty;
}
