namespace SmileTrack_MVC.Models.Entities;

/// <summary>
/// Mapeo entre un profesional y los servicios que puede realizar.
/// Permite definir un precio personalizado opcional y activar/desactivar la asignación.
/// Mapeado a la tabla Profesional_Servicio existente en BD con clave compuesta
/// (id_profesional, id_servicio) + columnas extendidas
/// (P-01 / U-06 — tablas huérfanas resueltas).
/// </summary>
public class ProfesionalServicio
{
    /// <summary>ID del profesional.</summary>
    public int IdProfesional { get; set; }

    /// <summary>ID del servicio que puede realizar el profesional.</summary>
    public int IdServicio { get; set; }

    /// <summary>
    /// Precio personalizado para este profesional.
    /// <c>null</c> significa que se aplica el precio de catálogo del servicio.
    /// </summary>
    public decimal? PrecioPersonalizado { get; set; }

    /// <summary>Indica si esta asignación está activa.</summary>
    public bool Activo { get; set; } = true;

    // ── Navegación ──────────────────────────────────────────────────────
    public Profesional? Profesional { get; set; }
    public Servicio? Servicio { get; set; }
}
