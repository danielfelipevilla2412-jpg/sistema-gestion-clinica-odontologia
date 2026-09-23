using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class ActualizarEstadoPqrRequest
{
    [Required]
    [RegularExpression("^(recibida|en_proceso|resuelta|cerrada|rechazada)$",
        ErrorMessage = "Estado inválido.")]
    public string Estado { get; set; } = "recibida";
}

public sealed class ResponderPqrRequest
{
    [Required(ErrorMessage = "La respuesta no puede estar vacía.")]
    [StringLength(2000, MinimumLength = 3, ErrorMessage = "La respuesta debe tener entre 3 y 2000 caracteres.")]
    public string Respuesta { get; set; } = string.Empty;

    [RegularExpression("^(recibida|en_proceso|resuelta|cerrada|rechazada)$",
        ErrorMessage = "Estado inválido.")]
    public string? NuevoEstado { get; set; }
}
