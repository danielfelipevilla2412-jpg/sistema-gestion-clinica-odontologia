
namespace SmileTrack_MVC.Models.DTOs;

public class ActualizarDatosPacienteDto
{
    public string TipoDocumento { get; set; } = "";

    public string Documento { get; set; } = "";

    public DateTime? FechaNacimiento { get; set; }
}
