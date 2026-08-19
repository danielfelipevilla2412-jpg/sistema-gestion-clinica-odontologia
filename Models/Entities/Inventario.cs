using System;

namespace SmileTrack_MVC.Models.Entities;

public class Inventario
{
    public int IdItem { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public string? UnidadMedida { get; set; }
    public decimal PrecioUnitario { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public string Estado { get; set; } = "disponible";
}
