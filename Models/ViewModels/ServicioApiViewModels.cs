using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class ServicioRequest
{
    [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(
     typeof(decimal),
     "0.01",
     "999999999",
     ParseLimitsInInvariantCulture = true,
     ErrorMessage = "El precio del servicio debe ser mayor que cero."
    )]
    public decimal Precio { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [RegularExpression("^(prevencion|estetica|cirugia|ortodoncia|endodoncia|periodoncia|general)$",
        ErrorMessage = "Categoría inválida.")]
    public string Category { get; set; } = "general";

    [Required(ErrorMessage = "Seleccione una especialidad clínica.")]
    [Range(1, int.MaxValue, ErrorMessage = "La especialidad seleccionada no es válida.")]
    public int? IdEspecialidad { get; set; }

    [Range(5, 480, ErrorMessage = "La duración debe estar entre 5 y 480 minutos.")]
    public int Duration { get; set; } = 60;
}

public sealed class ServicioEstadoRequest
{
    [Required]
    [RegularExpression("^(activo|inactivo)$", ErrorMessage = "El estado debe ser 'activo' o 'inactivo'.")]
    public string Estado { get; set; } = "activo";
}
