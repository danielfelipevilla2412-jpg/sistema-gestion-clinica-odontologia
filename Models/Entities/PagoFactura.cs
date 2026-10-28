namespace SmileTrack_MVC.Models.Entities;

public sealed class PagoFactura
{
    public int IdPago { get; set; }
    public int IdFactura { get; set; }
    public decimal Monto { get; set; }
    public string MetodoPago { get; set; } = "efectivo";
    public string Estado { get; set; } = "aprobado";
    public string Referencia { get; set; } = string.Empty;
    public int RegistradoPor { get; set; }
    public DateTime FechaUtc { get; set; }

    public Factura? Factura { get; set; }
    public Usuario? Usuario { get; set; }
}