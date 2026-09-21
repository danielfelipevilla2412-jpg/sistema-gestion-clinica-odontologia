using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.Pacientes;

// ─────────────────────────────────────────────
// RESPONSE DTOs
// ─────────────────────────────────────────────

/// <summary>Forma de salida usada por los endpoints GET/POST/PATCH de la API.</summary>
public sealed class PacienteApiDto
{
    public int IdPaciente { get; set; }
    public string TipoDocumento { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public DateTime FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Ciudad { get; set; }
    public string? GrupoSanguineo { get; set; }
    public List<string> Alergias { get; set; } = [];
    public List<string> Medicamentos { get; set; } = [];
    public string? AntecedentesMedicos { get; set; }
    public string? ContactoEmergencia { get; set; }
    public string? TelefonoEmergencia { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime FechaRegistro { get; set; }
    public DateTime? UltimaConsulta { get; set; }
    public DateTime? ProximaConsulta { get; set; }
}

// ─────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────

/// <summary>
/// Cuerpo esperado por POST api/v1/pacientes. Las reglas [Required]/[StringLength]
/// reflejan las que ya tiene la tabla Paciente en SQL Server (documento UNIQUE,
/// tipo_documento con CHECK, etc.) — no se inventan reglas nuevas.
/// </summary>
public sealed class PacienteApiCreateDto
{
    [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
    [StringLength(20)]
    public string TipoDocumento { get; set; } = "";

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20)]
    public string Documento { get; set; } = "";

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100)]
    public string Nombres { get; set; } = "";

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100)]
    public string Apellidos { get; set; } = "";

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime FechaNacimiento { get; set; }

    [StringLength(20)]
    public string? Genero { get; set; }

    [StringLength(20)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(100)]
    public string? Ciudad { get; set; }

    [StringLength(10)]
    public string? GrupoSanguineo { get; set; }

    public string? Alergias { get; set; }

    public string? Medicamentos { get; set; }

    public string? AntecedentesMedicos { get; set; }

    [StringLength(100)]
    public string? ContactoEmergencia { get; set; }

    [StringLength(20)]
    public string? TelefonoEmergencia { get; set; }
}

/// <summary>
/// Cuerpo esperado por PATCH api/v1/pacientes/{id}. Actualización parcial: solo
/// se aplican los campos que vengan distintos de null.
/// </summary>
public sealed class PacienteApiUpdateDto
{
    [StringLength(100)]
    public string? Nombres { get; set; }

    [StringLength(100)]
    public string? Apellidos { get; set; }

    [StringLength(20)]
    public string? Genero { get; set; }

    [StringLength(20)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(100)]
    public string? Ciudad { get; set; }

    [StringLength(10)]
    public string? GrupoSanguineo { get; set; }

    public string? Alergias { get; set; }

    public string? Medicamentos { get; set; }

    public string? AntecedentesMedicos { get; set; }

    [StringLength(100)]
    public string? ContactoEmergencia { get; set; }

    [StringLength(20)]
    public string? TelefonoEmergencia { get; set; }
}

/// <summary>Cuerpo esperado por PATCH api/v1/pacientes/{id}/estado.</summary>
public sealed class PacienteApiEstadoDto
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [RegularExpression("^(activo|inactivo|retirado)$", ErrorMessage = "El estado debe ser activo, inactivo o retirado.")]
    public string Estado { get; set; } = "";
}

// ─────────────────────────────────────────────
// RESULT TYPES
// ─────────────────────────────────────────────

/// <summary>Resultado de la operación de listado paginado de pacientes.</summary>
public sealed class PacientesApiResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public List<PacienteApiDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public static PacientesApiResult Ok(List<PacienteApiDto> items, int totalCount, int page, int pageSize) =>
        new() { Success = true, Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };

    public static PacientesApiResult Fail(string message) =>
        new() { Success = false, Message = message };
}

/// <summary>Resultado genérico de operaciones CREATE / PATCH sobre un paciente.</summary>
public sealed class PacienteApiOperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>Código HTTP sugerido cuando la operación falla (409, 404, 422…).</summary>
    public int? ErrorStatusCode { get; init; }

    /// <summary>El DTO del paciente afectado (disponible en creación/actualización exitosa).</summary>
    public PacienteApiDto? Data { get; init; }

    public static PacienteApiOperationResult Ok(string message, PacienteApiDto? data = null) =>
        new() { Success = true, Message = message, Data = data };

    public static PacienteApiOperationResult Fail(string message, int statusCode = 422) =>
        new() { Success = false, Message = message, ErrorStatusCode = statusCode };

    public static PacienteApiOperationResult NotFound(string message = "Paciente no encontrado.") =>
        new() { Success = false, Message = message, ErrorStatusCode = 404 };

    public static PacienteApiOperationResult Conflict(string message) =>
        new() { Success = false, Message = message, ErrorStatusCode = 409 };
}