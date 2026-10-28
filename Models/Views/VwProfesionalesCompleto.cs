using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmileTrack_MVC.Models.Views;

/// <summary>
/// Entidad que mapea la vista SQL vw_Profesionales_Completo.
/// 
/// PROPÓSITO:
/// Optimiza las consultas de profesionales al pre-calcular:
/// - Relaciones con Usuario, Rol y Especialidad
/// - Contadores de horarios, ausencias, bloqueos y citas
/// - Indicadores de disponibilidad en tiempo real
/// 
/// BENEFICIO:
/// - Evita subconsultas COUNT() repetidas en cada listado
/// - Reduce tiempo de respuesta en listados paginados
/// - Proporciona métricas instantáneas sin cálculos adicionales
/// 
/// USO:
/// - ProfesionalService.ObtenerAsync() para listados
/// - GestionProfesionalesController para dashboard
/// - Reportes de disponibilidad de profesionales
/// 
/// EJEMPLO:
/// <code>
/// var profesionales = await _context.VwProfesionalesCompleto
///     .Where(p => p.Estado == "activo" && p.DisponibleAhora == 1)
///     .OrderBy(p => p.NombreCompleto)
///     .ToListAsync();
/// </code>
/// 
/// NOTA IMPORTANTE:
/// Esta es una vista de solo lectura. Para modificar profesionales,
/// usar la entidad Profesional.
/// </summary>
[Table("vw_Profesionales_Completo")]
public class VwProfesionalesCompleto
{
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL PROFESIONAL
    // ═══════════════════════════════════════════════════════════════════
    
    [Key]
    [Column("id_profesional")]
    public int IdProfesional { get; set; }
    
    [Column("nombres")]
    [StringLength(100)]
    public string Nombres { get; set; } = string.Empty;
    
    [Column("apellidos")]
    [StringLength(100)]
    public string Apellidos { get; set; } = string.Empty;
    
    [Column("nombre_completo")]
    [StringLength(201)]
    public string NombreCompleto { get; set; } = string.Empty;
    
    [Column("registro_medico")]
    [StringLength(50)]
    public string RegistroMedico { get; set; } = string.Empty;
    
    [Column("categoria")]
    [StringLength(100)]
    public string? Categoria { get; set; }
    
    [Column("telefono")]
    [StringLength(20)]
    public string? Telefono { get; set; }
    
    [Column("descripcion")]
    [StringLength(255)]
    public string? Descripcion { get; set; }
    
    [Column("estado")]
    [StringLength(15)]
    public string Estado { get; set; } = "activo";
    
    [Column("fecha_ingreso")]
    public DateTime? FechaIngreso { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL USUARIO ASOCIADO (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_usuario")]
    public int? IdUsuario { get; set; }
    
    [Column("correo")]
    [StringLength(150)]
    public string? Correo { get; set; }
    
    [Column("estado_usuario")]
    [StringLength(10)]
    public string? EstadoUsuario { get; set; }
    
    [Column("fecha_creacion_usuario")]
    public DateTime? FechaCreacionUsuario { get; set; }
    
    [Column("ultimo_login")]
    public DateTime? UltimoLogin { get; set; }
    
    [Column("intentos_fallidos")]
    public int? IntentosFallidos { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // DATOS DEL ROL (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_rol")]
    public int? IdRol { get; set; }
    
    [Column("nombre_rol")]
    [StringLength(50)]
    public string? NombreRol { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // ESPECIALIDAD PRINCIPAL (pre-calculada)
    // ═══════════════════════════════════════════════════════════════════
    
    [Column("id_especialidad_principal")]
    public int? IdEspecialidadPrincipal { get; set; }
    
    [Column("nombre_especialidad_principal")]
    [StringLength(100)]
    public string? NombreEspecialidadPrincipal { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // CONTADORES DE DISPONIBILIDAD (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Número de horarios semanales activos configurados.
    /// </summary>
    [Column("total_horarios_activos")]
    public int TotalHorariosActivos { get; set; }
    
    /// <summary>
    /// Número de ausencias vigentes (fecha_fin >= hoy).
    /// </summary>
    [Column("total_ausencias_vigentes")]
    public int TotalAusenciasVigentes { get; set; }
    
    /// <summary>
    /// Número de bloqueos activos (fecha_fin > ahora).
    /// </summary>
    [Column("total_bloqueos_activos")]
    public int TotalBloqueosActivos { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // CONTADORES DE CITAS (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Número de citas pendientes (fecha >= hoy, no canceladas).
    /// </summary>
    [Column("total_citas_pendientes")]
    public int TotalCitasPendientes { get; set; }
    
    /// <summary>
    /// Número de citas en el mes actual.
    /// </summary>
    [Column("total_citas_mes_actual")]
    public int TotalCitasMesActual { get; set; }
    
    /// <summary>
    /// Número de servicios activos que ofrece el profesional.
    /// </summary>
    [Column("total_servicios_activos")]
    public int TotalServiciosActivos { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // INDICADORES DE DISPONIBILIDAD (pre-calculados)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Indica si el profesional está disponible ahora.
    /// 1 = Disponible (activo, sin ausencias ni bloqueos)
    /// 0 = No disponible
    /// </summary>
    [Column("disponible_ahora")]
    public int DisponibleAhora { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════
    // PROPIEDADES HELPER (no mapeadas, calculadas en C#)
    // ═══════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Indica si el profesional está disponible (conversión booleana).
    /// </summary>
    [NotMapped]
    public bool EstaDisponible => DisponibleAhora == 1;
    
    /// <summary>
    /// Estado normalizado para comparaciones.
    /// </summary>
    [NotMapped]
    public string EstadoNormalizado => Estado?.ToLowerInvariant() ?? "inactivo";
    
    /// <summary>
    /// Indica si el profesional está activo.
    /// </summary>
    [NotMapped]
    public bool EstaActivo => EstadoNormalizado == "activo";
    
    /// <summary>
    /// Indica si el profesional está en vacaciones.
    /// </summary>
    [NotMapped]
    public bool EstaEnVacaciones => EstadoNormalizado == "vacaciones";
    
    /// <summary>
    /// Indica si el profesional está inactivo.
    /// </summary>
    [NotMapped]
    public bool EstaInactivo => EstadoNormalizado == "inactivo";
    
    /// <summary>
    /// Indica si el profesional tiene horarios configurados.
    /// </summary>
    [NotMapped]
    public bool TieneHorariosConfigurados => TotalHorariosActivos > 0;
    
    /// <summary>
    /// Indica si el profesional tiene ausencias vigentes.
    /// </summary>
    [NotMapped]
    public bool TieneAusencias => TotalAusenciasVigentes > 0;
    
    /// <summary>
    /// Indica si el profesional tiene bloqueos activos.
    /// </summary>
    [NotMapped]
    public bool TieneBloqueos => TotalBloqueosActivos > 0;
    
    /// <summary>
    /// Indica si el profesional tiene citas pendientes.
    /// </summary>
    [NotMapped]
    public bool TieneCitasPendientes => TotalCitasPendientes > 0;
    
    /// <summary>
    /// Indica si el profesional puede ser desactivado (sin citas pendientes).
    /// </summary>
    [NotMapped]
    public bool PuedeSerDesactivado => !TieneCitasPendientes;
    
    /// <summary>
    /// Nivel de ocupación del profesional en el mes actual.
    /// - "bajo" si tiene menos de 10 citas
    /// - "medio" si tiene entre 10 y 30 citas
    /// - "alto" si tiene más de 30 citas
    /// </summary>
    [NotMapped]
    public string NivelOcupacionMesActual =>
        TotalCitasMesActual switch
        {
            < 10 => "bajo",
            <= 30 => "medio",
            _ => "alto"
        };
}
