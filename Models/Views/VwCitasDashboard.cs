using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Views;

/// <summary>
/// Entidad que mapea la vista SQL vw_Citas_Dashboard.
/// 
/// PROPÓSITO:
/// Optimiza las consultas del dashboard de citas al pre-calcular JOINs
/// con Paciente, Profesional, Servicio, Consultorio y Usuario.
/// 
/// BENEFICIO:
/// - Reduce de 6 JOINs repetitivos en cada consulta a 1 SELECT simple
/// - Mejora significativamente el rendimiento en listados paginados
/// - Incluye indicadores calculados (es_atrasada, es_proxima_24h)
/// 
/// USO:
/// - CitaService.ObtenerAsync() para listados
/// - GestionCitasController para dashboard principal
/// - Reportes de citas
/// 
/// EJEMPLO:
/// <code>
/// var citas = await _context.VwCitasDashboard
///     .Where(c => c.FechaHora >= DateTime.Today)
///     .OrderBy(c => c.FechaHora)
///     .ToListAsync();
/// </code>
/// 
/// NOTA IMPORTANTE:
/// Esta es una vista de solo lectura. No usar para INSERT, UPDATE o DELETE.
/// Para modificar citas, usar la entidad Cita.
/// </summary>
[Table("vw_Citas_Dashboard")]
public class VwCitasDashboard
{
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DE LA CITA
    // ═══════════════════════════════════════════════════════════════════
    
    [Key]
    [Column("id_cita")]
    public int IdCita { get; set; }
    
    [Column("fecha_hora")]
    public DateTime FechaHora { get; set; }
    
    [Column("estado")]
    [StringLength(30)]
    public string Estado { get; set; } = string.Empty;
    
    [Column("duracion_minutos")]
    public int? DuracionMinutos { get; set; }
    
    [Column("notas")]
    public string? Notas { get; set; }
    
    [Column("motivo_consulta")]
    public string? MotivoConsulta { get; set; }
    
    [Column("notas_previas")]
    public string? NotasPrevias { get; set; }
    
    [Column("tipo_cita")]
    [StringLength(30)]
    public string? TipoCita { get; set; }
    
    [Column("fecha_creacion")]
    public DateTime? FechaCreacion { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL PACIENTE (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_paciente")]
    public int IdPaciente { get; set; }
    
    [Column("nombre_paciente")]
    [StringLength(201)]
    public string? NombrePaciente { get; set; }
    
    [Column("documento")]
    [StringLength(20)]
    public string? Documento { get; set; }
    
    [Column("tipo_documento")]
    [StringLength(5)]
    public string? TipoDocumento { get; set; }
    
    [Column("telefono_paciente")]
    [StringLength(20)]
    public string? TelefonoPaciente { get; set; }
    
    [Column("correo_paciente")]
    [StringLength(150)]
    public string? CorreoPaciente { get; set; }
    
    [Column("genero")]
    [StringLength(5)]
    public string? Genero { get; set; }
    
    [Column("fecha_nacimiento")]
    public DateTime? FechaNacimiento { get; set; }
    
    [Column("edad_paciente")]
    public int? EdadPaciente { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL PROFESIONAL (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_profesional")]
    public int? IdProfesional { get; set; }
    
    [Column("nombre_profesional")]
    [StringLength(201)]
    public string? NombreProfesional { get; set; }
    
    [Column("registro_medico")]
    [StringLength(50)]
    public string? RegistroMedico { get; set; }
    
    [Column("estado_profesional")]
    [StringLength(15)]
    public string? EstadoProfesional { get; set; }
    
    [Column("telefono_profesional")]
    [StringLength(20)]
    public string? TelefonoProfesional { get; set; }
    
    [Column("especialidad_profesional")]
    [StringLength(100)]
    public string? EspecialidadProfesional { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL SERVICIO (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_servicio")]
    public int? IdServicio { get; set; }
    
    [Column("nombre_servicio")]
    [StringLength(150)]
    public string? NombreServicio { get; set; }
    
    [Column("descripcion_servicio")]
    [StringLength(500)]
    public string? DescripcionServicio { get; set; }
    
    [Column("precio_servicio")]
    public decimal? PrecioServicio { get; set; }
    
    [Column("duracion_servicio")]
    public int? DuracionServicio { get; set; }
    
    [Column("categoria_servicio")]
    [StringLength(50)]
    public string? CategoriaServicio { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL CONSULTORIO (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_consultorio")]
    public int? IdConsultorio { get; set; }
    
    [Column("nombre_consultorio")]
    [StringLength(100)]
    public string? NombreConsultorio { get; set; }
    
    [Column("ubicacion_consultorio")]
    [StringLength(150)]
    public string? UbicacionConsultorio { get; set; }
    
    [Column("tipo_consultorio")]
    [StringLength(50)]
    public string? TipoConsultorio { get; set; }
    
    [Column("estado_consultorio")]
    [StringLength(15)]
    public string? EstadoConsultorio { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL ESTADO CATÁLOGO (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_estado_catalogo")]
    public int? IdEstadoCatalogo { get; set; }
    
    [Column("nombre_estado_catalogo")]
    [StringLength(50)]
    public string? NombreEstadoCatalogo { get; set; }
    
    [Column("descripcion_estado")]
    [StringLength(150)]
    public string? DescripcionEstado { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL USUARIO CREADOR (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("creado_por_id")]
    public int? CreadoPorId { get; set; }
    
    [Column("creado_por_nombre")]
    [StringLength(201)]
    public string? CreadoPorNombre { get; set; }
    
    [Column("correo_creador")]
    [StringLength(150)]
    public string? CorreoCreador { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // INDICADORES CALCULADOS (pre-calculados en la vista)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Indica si la cita está atrasada (fecha pasada y estado pendiente).
    /// 1 = Atrasada, 0 = No atrasada.
    /// </summary>
    [Column("es_atrasada")]
    public int EsAtrasada { get; set; }
    
    /// <summary>
    /// Indica si la cita es próxima (dentro de las próximas 24 horas).
    /// 1 = Próxima, 0 = No próxima.
    /// </summary>
    [Column("es_proxima_24h")]
    public int EsProxima24h { get; set; }
    
    /// <summary>
    /// Minutos hasta la cita (negativo si ya pasó).
    /// </summary>
    [Column("minutos_hasta_cita")]
    public int? MinutosHastaCita { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // PROPIEDADES HELPER (no mapeadas, calculadas en C#)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Indica si la cita está atrasada (conversión booleana).
    /// </summary>
    [NotMapped]
    public bool EstaCitaAtrasada => EsAtrasada == 1;
    
    /// <summary>
    /// Indica si la cita es próxima (conversión booleana).
    /// </summary>
    [NotMapped]
    public bool EsCitaProxima => EsProxima24h == 1;
    
    /// <summary>
    /// Estado normalizado para comparaciones (lowercase).
    /// </summary>
    [NotMapped]
    public string EstadoNormalizado => Estado?.ToLowerInvariant() ?? string.Empty;
    
    /// <summary>
    /// Indica si la cita está cancelada.
    /// </summary>
    [NotMapped]
    public bool EstaCancelada => EstadoNormalizado is "cancelada" or "cancelado";
    
    /// <summary>
    /// Indica si la cita está pendiente (agendada, confirmada, programada).
    /// </summary>
    [NotMapped]
    public bool EstaPendiente => EstadoNormalizado is "agendada" or "confirmada" or "programada";
    
    /// <summary>
    /// Indica si la cita fue atendida.
    /// </summary>
    [NotMapped]
    public bool FueAtendida => EstadoNormalizado is "atendida" or "completada";
}
