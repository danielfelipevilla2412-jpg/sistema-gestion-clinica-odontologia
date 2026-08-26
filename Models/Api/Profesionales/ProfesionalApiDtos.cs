using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.Profesionales;

// ─────────────────────────────────────────────
// RESPONSE DTOs
// ─────────────────────────────────────────────

public sealed class ProfesionalApiDto
{
    public int IdProfesional { get; set; }
    public int? IdUsuario { get; set; }

    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;

    public string RegistroMedico { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string? Telefono { get; set; }
    public string? Descripcion { get; set; }

    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaIngreso { get; set; }

    public string? CorreoAcceso { get; set; }

    public List<ProfesionalEspecialidadApiDto> Especialidades { get; set; } = [];
}

public sealed class ProfesionalEspecialidadApiDto
{
    public int IdEspecialidad { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────

public sealed class ProfesionalApiRequest
{
    [Required(ErrorMessage = "Los nombres son requeridos.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 100 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son requeridos.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El registro médico es requerido.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El registro médico debe tener entre 3 y 30 caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9\-\. ]+$", ErrorMessage = "El registro médico tiene un formato inválido. Use letras, números y guiones.")]
    public string RegistroMedico { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "La categoría no puede exceder los 100 caracteres.")]
    public string? Categoria { get; set; }

    [StringLength(20, ErrorMessage = "El teléfono no puede exceder los 20 caracteres.")]
    public string? Telefono { get; set; }

    public int? IdEspecialidad { get; set; }

    [Required(ErrorMessage = "El correo de acceso es requerido.")]
    [EmailAddress(ErrorMessage = "El formato del correo es inválido.")]
    [StringLength(150, ErrorMessage = "El correo no puede exceder los 150 caracteres.")]
    public string CorreoAcceso { get; set; } = string.Empty;

    public string? ContrasenaAcceso { get; set; }

    [StringLength(255, ErrorMessage = "La descripción no puede exceder los 255 caracteres.")]
    public string? Descripcion { get; set; }
}

public sealed class ProfesionalEstadoApiRequest
{
    [Required(ErrorMessage = "El estado es requerido.")]
    public string Estado { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────
// RESULT TYPES
// ─────────────────────────────────────────────

/// <summary>
/// Resultado de la operación de listado paginado de profesionales.
/// </summary>
public sealed class ProfesionalesApiResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public List<ProfesionalApiDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public static ProfesionalesApiResult Ok(List<ProfesionalApiDto> items, int totalCount, int page, int pageSize) =>
        new() { Success = true, Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };

    public static ProfesionalesApiResult Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>
/// Resultado genérico de operaciones CREATE / UPDATE / PATCH / DELETE sobre un profesional.
/// </summary>
public sealed class ProfesionalApiOperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>Código HTTP sugerido cuando la operación falla (409, 404, 422…).</summary>
    public int? ErrorStatusCode { get; init; }

    /// <summary>El DTO del profesional afectado (disponible en creación/actualización exitosa).</summary>
    public ProfesionalApiDto? Data { get; init; }

    public static ProfesionalApiOperationResult Ok(string message, ProfesionalApiDto? data = null) =>
        new() { Success = true, Message = message, Data = data };

    public static ProfesionalApiOperationResult Fail(string message, int statusCode = 422) =>
        new() { Success = false, Message = message, ErrorStatusCode = statusCode };

    public static ProfesionalApiOperationResult NotFound(string message = "Profesional no encontrado.") =>
        new() { Success = false, Message = message, ErrorStatusCode = 404 };

    public static ProfesionalApiOperationResult Conflict(string message) =>
        new() { Success = false, Message = message, ErrorStatusCode = 409 };
}