using System;

namespace SmileTrack_MVC.Models.Entities;

public class ConfiguracionGeneral
{
    public int IdConfiguracion { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Modulo { get; set; } = "general";
}
