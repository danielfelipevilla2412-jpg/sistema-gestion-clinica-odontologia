using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities
{
    /// <summary>
    /// Registro de demostración para auditoría del consentimiento de tratamiento de datos.
    /// Validar el modelo con asesoría legal antes de usarlo en producción.
    /// </summary>
    [Table("RegistroConsentimiento")]
    public class RegistroConsentimiento
    {
        [Key]
        [Column("id_registro")]
        public int IdRegistro { get; set; }

        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("version_politica")]
        [StringLength(10)]
        public string VersionPolitica { get; set; } = "DEMO 0.1";

        [Required]
        [Column("fecha_consentimiento")]
        public DateTime FechaConsentimiento { get; set; } = DateTime.UtcNow;

        [Column("ip_origen")]
        [StringLength(45)]
        public string? IpOrigen { get; set; }

        [Required]
        [Column("canal")]
        [StringLength(30)]
        public string Canal { get; set; } = "web-registro";

        [Required]
        [Column("finalidad")]
        [StringLength(255)]
        public string Finalidad { get; set; } = "atencion-clinica";

        [Required]
        [Column("accion")]
        [StringLength(20)]
        public string Accion { get; set; } = "otorgar";

        [Required]
        [Column("tipo_datos")]
        [StringLength(100)]
        public string TipoDatos { get; set; } = "datos-salud,datos-personales";

        [Required]
        [Column("responsable")]
        [StringLength(100)]
        public string Responsable { get; set; } = "DEMO-NO-VALIDADO";

        // Nav
        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }
    }
}
