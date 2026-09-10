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

    // Yeray (2025) - CitaId: la cita activa en la que el profesional está
    // trabajando el odontograma. Si viene en la URL (?citaId=N), se propaga
    // al config JS (window.smiletrackOdontogramaConfig.citaId) y desde ahí
    // al payload de /guardar → GuardarOdontogramaInternoAsync → IdCita en BD.
    public int? CitaId { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - OdontogramaGuardarRequest: se agrega CitaId (opcional).
//
// ANTES: IdCita siempre se guardaba como null en Registro_Odontograma porque
//        el request no transportaba ese dato ("se puede pasar en el futuro").
// AHORA: si el profesional está trabajando en el contexto de una cita concreta
//        (citaId presente en la URL o en la sesión), el JS lo incluye aquí.
//        El servicio lo propaga a cada RegistroOdontograma que se inserta,
//        completando la trazabilidad diente ↔ cita ↔ profesional.
//
// El campo es nullable: si no llega (null o 0), el comportamiento es el mismo
// que antes y no se rompe ninguna integración existente.
// ─────────────────────────────────────────────────────────────────────────────
public class OdontogramaGuardarRequest
{
    public int? PacienteId { get; set; }

    /// <summary>
    /// Cita en la que se está registrando el estado del diente.
    /// Opcional: si es null el registro queda sin cita asociada (comportamiento anterior).
    /// </summary>
    public int? CitaId { get; set; }

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

    // Yeray - Nombre del profesional que hizo este cambio puntual del diente.
    // Antes no se guardaba, así que el tooltip no podía mostrar "quién" hizo
    // cada tratamiento (blanqueamiento por Dr. X, ortodoncia por Dr. Y, etc.)
    public string? Profesional { get; set; }
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
