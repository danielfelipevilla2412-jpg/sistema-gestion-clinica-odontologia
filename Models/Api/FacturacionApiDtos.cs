using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.Facturacion;

public class FacturaListItemDto
{
    public int Id { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string Servicios { get; set; } = string.Empty;
    public int IdPaciente { get; init; }
    public int? IdCita { get; init; }
    public int? IdProfesional { get; init; }
    public string Paciente { get; init; } = string.Empty;
    public string Documento { get; init; } = string.Empty;
    public DateTime Fecha { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Descuento { get; init; }
    public decimal Total { get; init; }
    public decimal MontoPagado { get; init; }
    public decimal Pendiente { get; init; }
    public DateTime? FechaPago { get; init; }
    public string Estado { get; init; } = string.Empty;
    public string? Notas { get; init; }
}

public sealed class CitaFacturacionDto
{
    public int IdCita { get; init; }
    public int IdPaciente { get; init; }
    public string Paciente { get; init; } = string.Empty;
    public int? IdServicio { get; init; }
    public string Servicio { get; init; } = string.Empty;
    public decimal PrecioServicio { get; init; }
    public int? IdProfesional { get; init; }
    public string Profesional { get; init; } = string.Empty;
    public DateTime FechaHora { get; init; }
    public string Estado { get; init; } = string.Empty;
    public int? IdFactura { get; init; }
    public string? EstadoFactura { get; init; }
    public bool PuedeFacturar { get; init; }
}

public sealed class FacturaDetalleDto
{
    public int Id { get; init; }
    public int? IdServicio { get; init; }
    public string Descripcion { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Descuento { get; init; }
    public decimal Subtotal { get; init; }
}

public sealed class FacturaDto : FacturaListItemDto
{
    public List<FacturaDetalleDto> Detalles { get; init; } = new();
    public List<FacturaPagoDto> Pagos { get; init; } = new();
}

public sealed class FacturaPagoDto
{
    public int Id { get; init; }
    public decimal Monto { get; init; }
    public string MetodoPago { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string Referencia { get; init; } = string.Empty;
    public DateTime FechaUtc { get; init; }
}

public sealed class FacturaDetalleRequest
{
    public int? IdServicio { get; set; }

    [Required, StringLength(200, MinimumLength = 2)]
    public string Descripcion { get; set; } = string.Empty;

    [Range(1, 999)]
    public int Cantidad { get; set; } = 1;

    [Range(0, 999999999)]
    public decimal PrecioUnitario { get; set; }

    [Range(0, 999999999)]
    public decimal Descuento { get; set; }
}

public class CrearFacturaRequest
{
    [Range(1, int.MaxValue)]
    public int IdPaciente { get; set; }
    public int? IdCita { get; set; }
    public int? IdProfesional { get; set; }

    [StringLength(500)]
    public string? Notas { get; set; }

    [MinLength(1)]
    public List<FacturaDetalleRequest> Detalles { get; set; } = new();
}

public sealed class CrearFacturaConPagoRequest : CrearFacturaRequest
{
    [Range(0.01, 999999999)]
    public decimal MontoPagado { get; set; }

    [Required, StringLength(30)]
    public string MetodoPago { get; set; } = "efectivo";
}

public sealed class ActualizarFacturaRequest
{
    [Range(1, int.MaxValue)]
    public int IdPaciente { get; set; }
    public int? IdCita { get; set; }
    public int? IdProfesional { get; set; }

    [StringLength(500)]
    public string? Notas { get; set; }

    [MinLength(1)]
    public List<FacturaDetalleRequest> Detalles { get; set; } = new();
}

public sealed class RegistrarPagoRequest
{
    [Range(0.01, 999999999)]
    public decimal MontoPagado { get; set; }

    [Required, StringLength(30)]
    public string MetodoPago { get; set; } = "efectivo";
}

public sealed class AnularFacturaRequest
{
    [StringLength(300)]
    public string? Motivo { get; set; }
}
