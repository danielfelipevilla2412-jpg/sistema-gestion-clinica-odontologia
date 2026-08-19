using System;

namespace SmileTrack_MVC.Models.Entities;

public class PqrEntity
{
    public int IdPqr { get; set; }
    public int IdPaciente { get; set; }
    public int? IdUsuario { get; set; }
    public string Tipo { get; set; } = "peticion"; // peticion, queja, reclamo, sugerencia
    public string Asunto { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = "recibida"; // recibida, en_proceso, resuelta, cerrada, rechazada
    public string Prioridad { get; set; } = "media"; // baja, media, alta, urgente
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime? FechaRespuesta { get; set; }
    public string? Respuesta { get; set; }
    public int? AtendidaPor { get; set; }
    public string? EvidenciaAdjunto { get; set; }

    public Paciente? Paciente { get; set; }
    public Usuario? Usuario { get; set; }
    public Usuario? AtendidaPorUsuario { get; set; }
}
