using System;

namespace SmileTrack_MVC.Models.Entities;

public class Factura
{
    public int IdFactura { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public DateTime FechaFactura { get; set; } = DateTime.Now;
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = "pendiente"; // pendiente, parcial, pagada, anulada
    public int IdPaciente { get; set; }
    public string? Notas { get; set; }
    public int GeneradaPor { get; set; }

    public Paciente? Paciente { get; set; }
    public Usuario? GeneradaPorUsuario { get; set; }
}
