// ─────────────────────────────────────────────────────────────────────────────
// Yeray (2025) - PacienteViewModel
//
// CAMBIOS respecto a la versión anterior:
//   - Se agregan campos que antes solo existían en PacienteApiDto y no llegaban
//     al módulo MVC: AntecedentesMedicos, ContactoEmergencia, TelefonoEmergencia,
//     Direccion, FechaRegistro, TotalCitas, CitasPendientes.
//   - Estos campos son necesarios para la nueva vista de detalle individual
//     (GET /gestion-de-pacientes/{id}) — punto 4 del plan de mejoras.
//   - El listado principal (Stadm05GestionPacientes) no los usa; son opcionales
//     (nullable / default) para no romper el mapeo existente.
// ─────────────────────────────────────────────────────────────────────────────

namespace SmileTrack_MVC.Models.ViewModels;

public class PacienteViewModel
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    public string Initials { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Doc { get; set; } = string.Empty;

    public string TipoDocumento { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public string? GrupoSanguineo { get; set; }
    public string? AlergiasTexto { get; set; }
    public string Estado { get; set; } = "activo";

    // Yeray (2025) - Campos médicos adicionales para vista de detalle
    public string? AntecedentesMedicos { get; set; }
    public string? ContactoEmergencia { get; set; }
    public string? TelefonoEmergencia { get; set; }

    // Yeray (2025) - Metadatos de actividad para la vista de detalle
    public DateTime? FechaRegistro { get; set; }
    public int TotalCitas { get; set; }
    public int CitasPendientes { get; set; }

    // Yeray (2025) - ArchivoAdjunto: ruta relativa desde wwwroot del archivo
    // (foto o documento de identidad). null si no se ha subido ninguno.
    // Se usa en la vista de detalle para mostrar la imagen y en los endpoints
    // POST /{id}/archivo-adjunto y /{id}/archivo-adjunto/eliminar.
    public string? ArchivoAdjunto { get; set; }

    public DateTime? LastVisit { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public DateTime? NextVisit { get; set; }
    public List<string> Allergies { get; set; } = [];
    public string Color { get; set; } = "blue";
    public List<PacienteHistorialViewModel> History { get; set; } = [];
}

public class PacienteHistorialViewModel
{
    public DateTime Date { get; set; }
    public string Procedure { get; set; } = string.Empty;
    public string Doctor { get; set; } = string.Empty;
}