
using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class FacturaDetalleItemRequest
{
    public int? IdServicio { get; set; }

    [Required(ErrorMessage = "La descripción del ítem es obligatoria.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "La descripción debe tener entre 2 y 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, 999, ErrorMessage = "La cantidad debe ser mayor a 0.")]
    public int Cantidad { get; set; } = 1;

    [Range(0, 999999999, ErrorMessage = "El precio unitario no puede ser negativo.")]
    public decimal PrecioUnitario { get; set; }
}

public sealed class CrearFacturaRequest
{
    [Required(ErrorMessage = "El paciente es obligatorio.")]
    public int IdPaciente { get; set; }

    [StringLength(500)]
    public string? Notas { get; set; }

    [Required(ErrorMessage = "La factura debe tener al menos un ítem.")]
    [MinLength(1, ErrorMessage = "La factura debe tener al menos un ítem.")]
    public List<FacturaDetalleItemRequest> Items { get; set; } = new();
}

public sealed class RegistrarPagoFacturaRequest
{
    [Range(0.01, 999999999, ErrorMessage = "El monto pagado debe ser mayor a 0.")]
    public decimal MontoPagado { get; set; }
}

public sealed class AnularFacturaRequest
{
    [StringLength(300)]
    public string? Motivo { get; set; }
}
