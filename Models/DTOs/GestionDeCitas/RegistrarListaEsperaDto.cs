using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.DTOs.GestionDeCitas;

public sealed class RegistrarListaEsperaDto
{
    [Range(1, int.MaxValue)]
    public int? IdServicio { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdProfesionalPreferido { get; set; }

    [Required]
    public DateTime FechaDeseadaInicio { get; set; }

    [Required]
    public DateTime FechaDeseadaFin { get; set; }

    [StringLength(500)]
    public string? Notas { get; set; }
}