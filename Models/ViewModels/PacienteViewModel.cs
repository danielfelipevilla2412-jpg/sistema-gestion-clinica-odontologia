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
    public string? GrupoSanguineo { get; set; }
    public string? AlergiasTexto { get; set; }
    public string Estado { get; set; } = "activo";

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