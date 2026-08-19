using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    [Table("Paciente")]
    public class Paciente
    {
        [Key]
        [Column("id_paciente")]
        public int IdPaciente { get; set; }

        [Column("id_usuario")]
        public int? IdUsuario { get; set; }

        [Required]
        [Column("tipo_documento")]
        [StringLength(5)]
        public string TipoDocumento { get; set; } = "CC";

        [Required]
        [Column("documento")]
        [StringLength(20)]
        public string Documento { get; set; } = string.Empty;

        [Required]
        [Column("nombres")]
        [StringLength(100)]
        public string Nombres { get; set; } = string.Empty;

        [Required]
        [Column("apellidos")]
        [StringLength(100)]
        public string Apellidos { get; set; } = string.Empty;

        [Required]
        [Column("fecha_nacimiento")]
        public DateTime FechaNacimiento { get; set; }

        [Column("genero")]
        [StringLength(5)]
        public string? Genero { get; set; }

        [Column("telefono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [Column("correo")]
        [StringLength(150)]
        public string? Correo { get; set; }

        [Column("direccion")]
        [StringLength(255)]
        public string? Direccion { get; set; }

        [Column("ciudad")]
        [StringLength(100)]
        public string? Ciudad { get; set; }

        [Column("grupo_sanguineo")]
        [StringLength(5)]
        public string? GrupoSanguineo { get; set; }

        [Column("alergias")]
        public string? Alergias { get; set; }

        [Column("antecedentes_medicos")]
        public string? AntecedentesMedicos { get; set; }

        [Column("contacto_emergencia")]
        [StringLength(100)]
        public string? ContactoEmergencia { get; set; }

        [Column("telefono_emergencia")]
        [StringLength(20)]
        public string? TelefonoEmergencia { get; set; }

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow.Date;

        [Required]
        [Column("estado")]
        [StringLength(10)]
        public string Estado { get; set; } = "activo";

        [Column("archivo_adjunto")]
        [StringLength(255)]
        public string? ArchivoAdjunto { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }

        [NotMapped]
        public string NombresCompleto => $"{Nombres} {Apellidos}".Trim();
    }
}
