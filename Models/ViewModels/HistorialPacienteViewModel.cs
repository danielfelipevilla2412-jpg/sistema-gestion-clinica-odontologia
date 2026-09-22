namespace SmileTrack_MVC.Models.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// Yeray - HistorialPacienteViewModel (actualización 2025)
//
// CAMBIO: se agrega la propiedad NotasClinicas (List<NotaClinicaHistorialItem>).
//
// ANTES: BuildHistorialPacienteViewModelAsync poblaba vm.Registros con los datos
//        de la tabla Cita, y las notas clínicas llegaban al JS solo a través del
//        JSON en Historia_Clinica.ObservacionesGenerales, que gestion-historial.js
//        parseaba con parseNotasClinicasPersistidas(). Ese JSON era solo de
//        compatibilidad temporal.
//
// AHORA: el servidor lee directamente la tabla Nota_Clinica y pasa las notas
//        como NotasClinicas en el ViewModel. El JS ya no necesita parsear el JSON
//        del odontograma para obtener las notas — las recibe ya formateadas en
//        window.smiletrackHistoriaData.notasClinicas.
// ─────────────────────────────────────────────────────────────────────────────

public class HistorialPacienteViewModel
{
    public OdontogramaViewModel Odontograma { get; set; } = new();
    // FASE 1 — Ficha clínica del paciente.
    // Motivo: la vista solo tenía nombre/edad y no podía mostrar los datos de
    // identificación, contacto y emergencia que ya existen en Paciente.
    // Uso: el controlador la llena desde BD y Razor la entrega al JavaScript.
    public PacienteResumenClinicoViewModel Paciente { get; set; } = new();
    public string GrupoSanguineo { get; set; } = "N/D";
    public List<string> Alergias { get; set; } = [];
    public string AntecedentesMedicos { get; set; } = "Sin antecedentes registrados";
    public DateTime? ProximaCitaFecha { get; set; }
    public string? ProximaCitaProfesional { get; set; }
    public HistoriaFormularioViewModel Formulario { get; set; } = new();

    /// <summary>
    /// Registros de consultas pasadas derivados de la tabla Cita.
    /// Usado por el render de odontograma de solo lectura y la vista de historial.
    /// </summary>
    public List<RegistroHistorialItem> Registros { get; set; } = [];

    // Yeray (2025) - Notas clínicas reales leídas directamente de Nota_Clinica.
    // El JS las recibe en window.smiletrackHistoriaData.notasClinicas y ya no
    // necesita parsear el JSON de ObservacionesGenerales.
    public List<NotaClinicaHistorialItem> NotasClinicas { get; set; } = [];

    /// <summary>
    /// Línea de tiempo unificada, construida desde las tablas clínicas reales.
    /// No reemplaza las entidades de origen: solo las presenta en orden clínico.
    /// </summary>
    public List<EventoHistoriaClinicaItem> LineaDeTiempo { get; set; } = [];
}

public class HistoriaFormularioViewModel
{
    public string MotivoConsulta { get; set; } = "";
    public string EnfermedadActual { get; set; } = "";
    public string Habitos { get; set; } = "";
    public string Hallazgos { get; set; } = "";
    public string OdontogramaObservaciones { get; set; } = "";
    public string ExamenesComplementarios { get; set; } = "";
    public string DiagnosticoPrincipal { get; set; } = "";
    public string DiagnosticoSecundario { get; set; } = "";
    public string EvolucionClinica { get; set; } = "";
    public string Prescripcion { get; set; } = "";
}

public class HistoriaFormularioGuardarRequest : HistoriaFormularioViewModel
{
    public int? PacienteId { get; set; }
}

// FASE 1 — DTO de solo lectura para no exponer la entidad Paciente directamente
// a la vista. Reúne únicamente los campos que el profesional debe consultar.
public class PacienteResumenClinicoViewModel
{
    public string TipoDocumento { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Genero { get; set; } = "No registrado";
    public string Telefono { get; set; } = "No registrado";
    public string Correo { get; set; } = "No registrado";
    public string Direccion { get; set; } = "No registrada";
    public string Ciudad { get; set; } = "No registrada";
    public string ContactoEmergencia { get; set; } = "No registrado";
    public string TelefonoEmergencia { get; set; } = "No registrado";
}

// FASE 1 — Formato común para eventos de fuentes distintas (citas, notas,
// odontograma, controles y documentos). Permite ordenarlos y filtrarlos en una
// única línea de tiempo sin perder la categoría ni el vínculo del documento.
public class EventoHistoriaClinicaItem
{
    public DateTime Fecha { get; set; }
    /// <summary>consulta, nota, odontograma, control o documento.</summary>
    public string Categoria { get; set; } = "consulta";
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Profesional { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? EnlaceDocumento { get; set; }
}

public class RegistroHistorialItem
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = "consulta";
    public string Descripcion { get; set; } = string.Empty;
    public string Doctor { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

// Yeray (2025) - DTO liviano para inyectar notas clínicas al JS.
// Campos alineados con el formato que ya espera renderHistorial() en gestion-historial.js:
//   { titulo, fecha, doctor, diagnostico, procedimiento, proximaCita, estado }
public class NotaClinicaHistorialItem
{
    /// <summary>Título visible en la lista (procedimiento o diagnóstico).</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Fecha ISO "yyyy-MM-dd" para formatFecha() del JS.</summary>
    public string Fecha { get; set; } = string.Empty;

    /// <summary>Nombre del doctor que registró la nota.</summary>
    public string Doctor { get; set; } = string.Empty;

    public string Diagnostico { get; set; } = string.Empty;
    public string Procedimiento { get; set; } = string.Empty;

    /// <summary>Texto libre de próxima cita sugerida (no es FK).</summary>
    public string? ProximaCita { get; set; }

    public string Estado { get; set; } = "Realizado";
}
