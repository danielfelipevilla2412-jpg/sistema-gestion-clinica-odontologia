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
    public string GrupoSanguineo { get; set; } = "N/D";
    public List<string> Alergias { get; set; } = [];
    public string AntecedentesMedicos { get; set; } = "Sin antecedentes registrados";
    public DateTime? ProximaCitaFecha { get; set; }
    public string? ProximaCitaProfesional { get; set; }

    /// <summary>
    /// Registros de consultas pasadas derivados de la tabla Cita.
    /// Usado por el render de odontograma de solo lectura y la vista de historial.
    /// </summary>
    public List<RegistroHistorialItem> Registros { get; set; } = [];

    // Yeray (2025) - Notas clínicas reales leídas directamente de Nota_Clinica.
    // El JS las recibe en window.smiletrackHistoriaData.notasClinicas y ya no
    // necesita parsear el JSON de ObservacionesGenerales.
    public List<NotaClinicaHistorialItem> NotasClinicas { get; set; } = [];
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