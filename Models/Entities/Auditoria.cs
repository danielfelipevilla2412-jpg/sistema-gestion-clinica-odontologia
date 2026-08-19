using System;

namespace SmileTrack_MVC.Models.Entities;

public class Auditoria
{
    public int IdAuditoria { get; set; }
    public int? IdUsuario { get; set; }
    public string TablaAfectada { get; set; } = string.Empty;
    public int? IdRegistro { get; set; }
    public string Accion { get; set; } = string.Empty; // INSERT, UPDATE, DELETE
    public string? IpOrigen { get; set; }
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    public Usuario? Usuario { get; set; }
}
