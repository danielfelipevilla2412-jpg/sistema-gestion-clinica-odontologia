#nullable enable

namespace SmileTrack_MVC.Models.ViewModels;

public class OdontogramaViewModel
{
    public int? PacienteId { get; set; }
    public int? HistoriaId { get; set; }
    public string PacienteNombre { get; set; } = "Paciente";
    public string CodigoHC { get; set; } = "HC-SIN-ASIGNAR";
    public string FechaNacimiento { get; set; } = "";
    public string ProfesionalNombre { get; set; } = "Profesional";
    public string ProfesionalCorreo { get; set; } = "";
    public string? ObservacionesGenerales { get; set; }
    public string? EstadoPersistido { get; set; }
}

public class OdontogramaGuardarRequest
{
    public int? PacienteId { get; set; }
    public Dictionary<string, OdontogramaRegistroPayload> Registros { get; set; } = [];
    public Dictionary<string, string> MapeoFDI { get; set; } = [];
}

public class OdontogramaRegistroPayload
{
    public string? NombrePieza { get; set; }
    public List<OdontogramaTratamientoPayload> Tratamientos { get; set; } = [];
}

public class OdontogramaTratamientoPayload
{
    public string? Key { get; set; }
    public string? Obs { get; set; }
    public string? Fecha { get; set; }
}

public class NotaClinicaGuardarRequest
{
    public int? PacienteId { get; set; }
    public string? Diagnostico { get; set; }
    public string? Procedimiento { get; set; }
    public string? ProximaCita { get; set; }
}

public class ControlPostoperatorioGuardarRequest
{
    public int? CitaId { get; set; }
    public string? Status { get; set; }
    public List<ControlPostoperatorioInstruccion>? Instructions { get; set; }
    public string? Observations { get; set; }
}

public class ControlPostoperatorioInstruccion
{
    public string? Text { get; set; }
    public bool Checked { get; set; }
}

public class RecordatorioPendienteDto
{
    public int Id { get; set; }
    public string Paciente { get; set; } = "";
    public string Iniciales { get; set; } = "";
    public DateTime FechaHora { get; set; }
    public bool EsManana { get; set; }
    public string Canal { get; set; } = "email";
    public bool Confirmada { get; set; }
}

public class RecordatoriosViewModel
{
    public List<RecordatorioPendienteDto> Pendientes { get; set; } = [];
    public int SinConfirmar { get; set; }
    public int FacturasVencidas { get; set; }
}
