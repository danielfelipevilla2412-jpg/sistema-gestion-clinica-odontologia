using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs;

public sealed class CitaCancelacionDto
{
    [StringLength(500, ErrorMessage = "El motivo de cancelación no puede superar 500 caracteres.")]
    public string? Motivo { get; set; }
}
