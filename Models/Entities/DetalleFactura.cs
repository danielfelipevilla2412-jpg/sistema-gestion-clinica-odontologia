using System;

namespace SmileTrack_MVC.Models.Entities;

public class DetalleFactura
{
    public int IdDetalle { get; set; }
    public int IdFactura { get; set; }
    public int? IdServicio { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int Cantidad { get; set; } = 1;
    public decimal PrecioUnitario { get; set; }
    public decimal SubtotalLinea { get; set; }

    public Factura? Factura { get; set; }
    public Servicio? Servicio { get; set; }
}
