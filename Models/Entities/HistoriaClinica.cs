using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("Historia_Clinica")]
public class HistoriaClinica
{
    [Key]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    [Required]
    [Column("id_paciente")]
    public int IdPaciente { get; set; }

    [Required]
    [Column("fecha_apertura")]
    public DateTime FechaApertura { get; set; } = DateTime.Now;

    [Column("observaciones_generales")]
    public string? ObservacionesGenerales { get; set; }

    [Required]
    [Column("activa")]
    public bool Activa { get; set; } = true;

    public Paciente? Paciente { get; set; }
}
