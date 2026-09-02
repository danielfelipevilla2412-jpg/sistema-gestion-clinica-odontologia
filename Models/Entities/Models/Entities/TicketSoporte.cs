
using System;

namespace SmileTrack_MVC.Models.Entities;

public class TicketSoporte
{
    public int IdTicket { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public int IdUsuario { get; set; }
    public string Asunto { get; set; } = string.Empty;
    public string Categoria { get; set; } = "otro";          // incidente, consulta, solicitud, otro
    public string ModuloAfectado { get; set; } = "sistema";  // citas, pacientes, facturacion, reportes, sistema
    public string Severidad { get; set; } = "media";         // baja, media, alta
    public string Descripcion { get; set; } = string.Empty;
    public string? CapturaPantalla { get; set; }
    public string Estado { get; set; } = "abierto";          // abierto, en_proceso, resuelto, cerrado
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaRespuesta { get; set; }
    public string? Respuesta { get; set; }
    public int? AtendidoPor { get; set; }

    public Usuario? Usuario { get; set; }
    public Usuario? AtendidoPorUsuario { get; set; }
}
