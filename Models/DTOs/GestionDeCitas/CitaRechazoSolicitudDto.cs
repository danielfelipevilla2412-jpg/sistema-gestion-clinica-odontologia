using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

public sealed class CitaRechazoSolicitudDto
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Motivo { get; set; } = string.Empty;
}
