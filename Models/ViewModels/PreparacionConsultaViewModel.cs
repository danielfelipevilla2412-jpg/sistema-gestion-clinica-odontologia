// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - PreparacionConsultaViewModel
//
// MOTIVO: la vista st-aux-03-preparacion-consulta tenía todos sus datos
// hardcodeados ("Pedro García", "Dr. Carlos Méndez", "Penicilina", etc.).
// El controlador devolvía View() sin ningún ViewModel, así que el auxiliar
// veía siempre la misma cita inventada sin importar qué cita tenía en el día.
//
// AHORA: el controlador carga la cita próxima del día (o la indicada por
// ?citaId=) y construye este ViewModel con:
//   - Datos de la cita real (paciente, profesional, servicio, consultorio, hora)
//   - Alertas médicas del paciente (alergias, antecedentes, grupo sanguíneo)
//   - Última nota clínica de la HC (para el auxiliar pueda revisar indicaciones)
//   - Lista de citas del día para el selector (puede haber varias)
//   - Tiempo hasta la cita (badge "En X minutos")
//
// La clave de localStorage del checklist usa citaId para que cada cita tenga
// su propio estado de preparación independiente.
// ─────────────────────────────────────────────────────────────────────────────

namespace SmileTrack_MVC.Models.ViewModels;

public class PreparacionConsultaViewModel
{
    // ── Cita seleccionada ────────────────────────────────────────────────────

    /// <summary>Id de la cita cargada. null si no hay citas del día.</summary>
    public int? CitaId { get; set; }

    /// <summary>Fecha y hora de la cita.</summary>
    public DateTime? CitaFechaHora { get; set; }

    /// <summary>Estado actual de la cita (programada, confirmada, etc.).</summary>
    public string CitaEstado { get; set; } = "programada";

    /// <summary>Notas/motivo de consulta guardadas en la cita.</summary>
    public string? CitaNotas { get; set; }

    /// <summary>Minutos hasta la cita (negativo = ya pasó).</summary>
    public int MinutosHastaCita { get; set; }

    // ── Paciente ─────────────────────────────────────────────────────────────

    public int? PacienteId { get; set; }
    public string PacienteNombre { get; set; } = "Sin paciente asignado";
    public string? PacienteDocumento { get; set; }
    public string? GrupoSanguineo { get; set; }

    /// <summary>Alergias en texto libre (CSV). null si no tiene.</summary>
    public string? Alergias { get; set; }

    /// <summary>Alergias parseadas como lista para renderizar badges.</summary>
    public List<string> AlergiasLista { get; set; } = [];

    /// <summary>Antecedentes médicos relevantes.</summary>
    public string? AntecedentesMedicos { get; set; }

    // ── Profesional ──────────────────────────────────────────────────────────

    public string ProfesionalNombre { get; set; } = "Sin asignar";

    // ── Servicio / Consultorio ───────────────────────────────────────────────

    public string ServicioNombre { get; set; } = "Sin especificar";
    public string ConsultorioNombre { get; set; } = "Sin consultorio";

    // ── Historia clínica ─────────────────────────────────────────────────────

    /// <summary>Código de la HC activa, formato HC-XXXXXX.</summary>
    public string? CodigoHC { get; set; }

    /// <summary>Última nota clínica registrada (texto del procedimiento o diagnóstico).</summary>
    public string? UltimaNotaClinica { get; set; }

    /// <summary>Fecha de la última nota clínica.</summary>
    public DateTime? FechaUltimaNota { get; set; }

    /// <summary>Profesional que registró la última nota.</summary>
    public string? ProfesionalUltimaNota { get; set; }

    // ── Citas del día (para el selector) ────────────────────────────────────

    public List<CitaDelDiaDto> CitasDelDia { get; set; } = [];

    // ── Sin citas ────────────────────────────────────────────────────────────

    /// <summary>true cuando no hay ninguna cita programada para hoy.</summary>
    public bool SinCitasHoy => CitaId is null;
}

/// <summary>
/// DTO liviano para el selector de citas del día en la vista.
/// </summary>
public class CitaDelDiaDto
{
    public int    Id          { get; set; }
    public string Paciente    { get; set; } = string.Empty;
    public string Hora        { get; set; } = string.Empty;
    public string Servicio    { get; set; } = string.Empty;
    public string Estado      { get; set; } = string.Empty;
    public bool   EsActual    { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - DTOs para el endpoint POST /confirmar
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Body del POST /confirmar — enviado por el JS al confirmar preparación.</summary>
public class ConfirmarPreparacionRequest
{
    /// <summary>Id de la cita que se está preparando.</summary>
    public int? CitaId { get; set; }

    /// <summary>Ítems del checklist con su estado marcado/desmarcado.</summary>
    public List<ChecklistItemDto>? ChecklistItems { get; set; }

    /// <summary>Observaciones libres del auxiliar.</summary>
    public string? Observaciones { get; set; }
}

public class ChecklistItemDto
{
    public string? Text    { get; set; }
    public bool    Checked { get; set; }
}
