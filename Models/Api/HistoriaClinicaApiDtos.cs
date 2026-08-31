using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.HistoriasClinicas;

// ─────────────────────────────────────────────
// RESPONSE DTOs
// ─────────────────────────────────────────────

/// <summary>Forma de salida usada por GET/POST api/v1/historias-clinicas/{pacienteId}.</summary>
public sealed class HistoriaClinicaApiDto
{
    public int IdHistoria { get; set; }
    public int IdPaciente { get; set; }
    public DateTime FechaApertura { get; set; }
    public bool Activa { get; set; }
    public List<OdontogramaPiezaApiDto> Odontograma { get; set; } = [];
}

/// <summary>Una pieza dental registrada (fuente: tabla Registro_Odontograma).</summary>
public sealed class OdontogramaPiezaApiDto
{
    public string NumeroFdi { get; set; } = "";
    public string? NombrePieza { get; set; }
    public string Estado { get; set; } = "";
    public string? Observacion { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? Profesional { get; set; }
}

/// <summary>Una nota clínica (fuente: tabla Nota_Clinica).</summary>
public sealed class NotaClinicaApiDto
{
    public int IdNota { get; set; }
    public DateTime Fecha { get; set; }
    public string? Doctor { get; set; }
    public string? Diagnostico { get; set; }
    public string? Procedimiento { get; set; }
    public string? ProximaCita { get; set; }
    public string Estado { get; set; } = "Realizado";
}

// ─────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────

/// <summary>Cuerpo esperado por POST api/v1/historias-clinicas.</summary>
public sealed class HistoriaClinicaApiCreateDto
{
    [Required(ErrorMessage = "El paciente es obligatorio.")]
    public int PacienteId { get; set; }
}

/// <summary>Cuerpo esperado por POST api/v1/historias-clinicas/{pacienteId}/notas.</summary>
public sealed class NotaClinicaApiCreateDto
{
    [StringLength(500)]
    public string? Diagnostico { get; set; }

    [StringLength(500)]
    public string? Procedimiento { get; set; }

    [StringLength(50)]
    public string? ProximaCita { get; set; }
}

/// <summary>Cuerpo esperado por PATCH api/v1/historias-clinicas/{id}/estado.</summary>
public sealed class HistoriaClinicaApiEstadoDto
{
    [Required]
    public bool Activa { get; set; }
}

// ─────────────────────────────────────────────
// RESULT TYPES
// ─────────────────────────────────────────────

/// <summary>Resultado genérico de operaciones sobre una historia clínica / nota / odontograma.</summary>
public sealed class HistoriaClinicaApiOperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>Código HTTP sugerido cuando la operación falla (404, 409, 422…).</summary>
    public int? ErrorStatusCode { get; init; }

    public HistoriaClinicaApiDto? Historia { get; init; }
    public NotaClinicaApiDto? Nota { get; init; }

    public static HistoriaClinicaApiOperationResult Ok(string message, HistoriaClinicaApiDto? historia = null, NotaClinicaApiDto? nota = null) =>
        new() { Success = true, Message = message, Historia = historia, Nota = nota };

    public static HistoriaClinicaApiOperationResult Fail(string message, int statusCode = 422) =>
        new() { Success = false, Message = message, ErrorStatusCode = statusCode };

    public static HistoriaClinicaApiOperationResult NotFound(string message = "Historia clínica no encontrada.") =>
        new() { Success = false, Message = message, ErrorStatusCode = 404 };

    public static HistoriaClinicaApiOperationResult Forbidden(string message = "No tiene permiso para acceder a este recurso.") =>
        new() { Success = false, Message = message, ErrorStatusCode = 403 };
}