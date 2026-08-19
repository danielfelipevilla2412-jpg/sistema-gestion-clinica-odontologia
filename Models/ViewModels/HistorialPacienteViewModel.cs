namespace SmileTrack_MVC.Models.ViewModels;

public class HistorialPacienteViewModel
{
    public OdontogramaViewModel Odontograma { get; set; } = new();
    public string GrupoSanguineo { get; set; } = "N/D";
    public List<string> Alergias { get; set; } = [];
    public string AntecedentesMedicos { get; set; } = "Sin antecedentes registrados";
    public DateTime? ProximaCitaFecha { get; set; }
    public string? ProximaCitaProfesional { get; set; }
    public List<RegistroHistorialItem> Registros { get; set; } = [];
}

public class RegistroHistorialItem
{
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = "consulta";
    public string Descripcion { get; set; } = string.Empty;
    public string Doctor { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}