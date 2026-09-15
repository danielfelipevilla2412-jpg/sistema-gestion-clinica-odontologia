using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class ServicioRequest
{
    [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(0, 999999999, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal Precio { get; set; }

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [RegularExpression("^(prevencion|estetica|cirugia|ortodoncia|endodoncia|general)$",
        ErrorMessage = "Categoría inválida.")]
    public string Category { get; set; } = "general";

    [Range(5, 480, ErrorMessage = "La duración debe estar entre 5 y 480 minutos.")]
    public int Duration { get; set; } = 30;
}

public sealed class ServicioEstadoRequest
{
    [Required]
    [RegularExpression("^(activo|inactivo)$", ErrorMessage = "El estado debe ser 'activo' o 'inactivo'.")]
    public string Estado { get; set; } = "activo";
}
