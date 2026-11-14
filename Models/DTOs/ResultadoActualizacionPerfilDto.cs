
namespace SmileTrack_MVC.Models.DTOs;

public class ResultadoActualizacionPerfilDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = "";

    public int StatusCode { get; set; }
}
