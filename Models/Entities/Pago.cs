using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Entities;

public sealed class Pago
{
    public int IdPago { get; set; }
    public int IdFactura { get; set; }

    [Range(0.01, 999999999)]
    public decimal Monto { get; set; }

    public DateTime FechaPago { get; set; } = DateTime.Now;
    public string MetodoPago { get; set; } = "efectivo";
    public string? Referencia { get; set; }
    public int RegistradoPor { get; set; }
    public string? Observaciones { get; set; }

    public Factura? Factura { get; set; }
    public Usuario? RegistradoPorUsuario { get; set; }
}