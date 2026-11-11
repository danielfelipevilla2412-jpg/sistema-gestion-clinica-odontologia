using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Entities;

[Table("Consulta_Clinica")]
public class ConsultaClinica
{
    [Key]
    [Column("id_consulta_clinica")]
    public int IdConsultaClinica { get; set; }

    [Required]
    [Column("id_historia")]
    public int IdHistoria { get; set; }

    [Column("id_cita")]
    public int? IdCita { get; set; }

    [Column("id_nota")]
    public int? IdNota { get; set; }

    [Column("id_proxima_cita")]
    public int? IdProximaCita { get; set; }

    [Column("id_profesional")]
    public int? IdProfesional { get; set; }

    [Column("fecha_consulta")]
    public DateTime? FechaConsulta { get; set; }

    [Column("motivo_consulta")]
    public string? MotivoConsulta { get; set; }

    [Column("enfermedad_actual")]
    public string? EnfermedadActual { get; set; }

    [Column("antecedentes_medicos")]
    public string? AntecedentesMedicos { get; set; }

    [Column("habitos")]
    public string? Habitos { get; set; }

    [Column("alergias")]
    public string? Alergias { get; set; }

    [Column("hallazgos")]
    public string? Hallazgos { get; set; }

    [Column("odontograma_observaciones")]
    public string? OdontogramaObservaciones { get; set; }

    [Column("examenes_complementarios")]
    public string? ExamenesComplementarios { get; set; }

    [Column("evolucion_clinica")]
    public string? EvolucionClinica { get; set; }

    [Column("prescripcion")]
    public string? Prescripcion { get; set; }

    [Required]
    [Column("origen")]
    [StringLength(30)]
    public string Origen { get; set; } = "captura";

    [Required]
    [Column("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public HistoriaClinica? HistoriaClinica { get; set; }
    public Cita? Cita { get; set; }
    public Cita? ProximaCita { get; set; }
    public NotaClinica? NotaClinica { get; set; }
    public Profesional? Profesional { get; set; }
    public ICollection<DiagnosticoConsulta> Diagnosticos { get; set; } = [];
    public ICollection<SintomaConsulta> Sintomas { get; set; } = [];
}

[Table("Diagnostico_CIE10")]
public class DiagnosticoCie10
{
    [Key]
    [Column("codigo")]
    [StringLength(10)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [Column("descripcion")]
    [StringLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Column("version_catalogo")]
    [StringLength(50)]
    public string VersionCatalogo { get; set; } = string.Empty;

    [Required]
    [Column("fuente")]
    [StringLength(500)]
    public string Fuente { get; set; } = string.Empty;
}

[Table("Consulta_Clinica_Diagnostico")]
public class DiagnosticoConsulta
{
    [Key]
    [Column("id_diagnostico")]
    public int IdDiagnostico { get; set; }

    [Required]
    [Column("id_consulta_clinica")]
    public int IdConsultaClinica { get; set; }

    [Column("codigo_cie10")]
    [StringLength(10)]
    public string? CodigoCie10 { get; set; }

    [Required]
    [Column("descripcion")]
    [StringLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Column("es_principal")]
    public bool EsPrincipal { get; set; }

    public ConsultaClinica? ConsultaClinica { get; set; }
    public DiagnosticoCie10? Cie10 { get; set; }
}

[Table("Consulta_Clinica_Sintoma")]
public class SintomaConsulta
{
    [Key]
    [Column("id_sintoma")]
    public int IdSintoma { get; set; }

    [Required]
    [Column("id_consulta_clinica")]
    public int IdConsultaClinica { get; set; }

    [Required]
    [Column("descripcion")]
    [StringLength(250)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    [Column("orden")]
    public int Orden { get; set; }

    public ConsultaClinica? ConsultaClinica { get; set; }
}
