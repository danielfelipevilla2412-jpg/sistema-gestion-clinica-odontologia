using System;

namespace SmileTrack_MVC.Models.Entities;

public class Equipo
{
    public int IdEquipo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Modelo { get; set; }
    public string? Serie { get; set; }
    public string Status { get; set; } = "operativo"; // operativo, mantenimiento, fuera_servicio
    public DateTime? UltimoMantenimiento { get; set; }
    public DateTime? ProximoMantenimiento { get; set; }
    public string? Ubicacion { get; set; }
}
 