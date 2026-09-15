using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.Profesionales;

public sealed class ProfesionalApiRequest
{
    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 100 caracteres.")]
    public string Nombres { get; set; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres.")]
    public string Apellidos { get; set; } = string.Empty;

    [Required(ErrorMessage = "El registro médico es obligatorio.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El registro médico debe tener entre 3 y 30 caracteres.")]
    public string RegistroMedico { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "La categoría no puede superar 100 caracteres.")]
    public string? Categoria { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; set; }

    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    public string? Descripcion { get; set; }

    [StringLength(30, ErrorMessage = "El estado no puede superar 30 caracteres.")]
    public string? Estado { get; set; }

    [Required(ErrorMessage = "El correo de acceso es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo de acceso no es válido.")]
    public string CorreoAcceso { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener mínimo 8 caracteres.")]
    public string? ContrasenaAcceso { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La especialidad seleccionada es inválida.")]
    public int? IdEspecialidad { get; set; }
}

public sealed class ProfesionalEstadoApiRequest
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [StringLength(30, MinimumLength = 1, ErrorMessage = "El estado debe tener entre 1 y 30 caracteres.")]
    public string Estado { get; set; } = string.Empty;
}

public sealed class HorarioSemanalApiRequest
{
    public string? Day { get; set; }
    public string? DiaSemana { get; set; }
    public string? DayFull { get; set; }
    public bool Active { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}

public sealed class HorarioProfesionalApiDto
{
    public int IdHorario { get; set; }
    public string DiaSemana { get; set; } = string.Empty;
    public string HoraInicio { get; set; } = string.Empty;
    public string HoraFin { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public sealed class AusenciaProfesionalApiDto
{
    public int IdAusencia { get; set; }
    public string? Tipo { get; set; }
    public string FechaInicio { get; set; } = string.Empty;
    public string FechaFin { get; set; } = string.Empty;
    public int? Duracion { get; set; }
    public string? Observaciones { get; set; }
}

public sealed class ServicioProfesionalApiDto
{
    public int IdProfesional { get; set; }
    public int IdServicio { get; set; }
    public string NombreServicio { get; set; } = string.Empty;
    public decimal PrecioBase { get; set; }
    public decimal? PrecioPersonalizado { get; set; }
    public decimal PrecioEfectivo { get; set; }
    public bool Activo { get; set; }
}

public sealed class ProfesionalApiCollectionResult<T>
{
    public bool Success { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public List<T> Data { get; private set; } = new();
    public int? ErrorStatusCode { get; private set; }

    public static ProfesionalApiCollectionResult<T> Ok(List<T> data) => new()
    {
        Success = true,
        Message = "OK",
        Data = data
    };

    public static ProfesionalApiCollectionResult<T> Fail(string message, int statusCode) => new()
    {
        Success = false,
        Message = message,
        ErrorStatusCode = statusCode
    };
}

public sealed class ProfesionalApiCollectionOperationResult<T>
{
    public bool Success { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public List<T> Data { get; private set; } = new();
    public int? ErrorStatusCode { get; private set; }

    public static ProfesionalApiCollectionOperationResult<T> Ok(string message, List<T> data) => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static ProfesionalApiCollectionOperationResult<T> Fail(string message, int statusCode) => new()
    {
        Success = false,
        Message = message,
        ErrorStatusCode = statusCode
    };
}

public sealed class ProfesionalEspecialidadApiDto
{
    public int IdEspecialidad { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public sealed class ProfesionalApiDto
{
    public int IdProfesional { get; set; }
    public int IdUsuario { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string RegistroMedico { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string? Telefono { get; set; }
    public string? Descripcion { get; set; }
    public string? Estado { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string? CorreoAcceso { get; set; }
    public List<ProfesionalEspecialidadApiDto> Especialidades { get; set; } = new();
}

public sealed class ProfesionalesApiResult
{
    public bool Success { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public List<ProfesionalApiDto> Items { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int Page { get; private set; }
    public int PageSize { get; private set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    public static ProfesionalesApiResult Ok(
        List<ProfesionalApiDto> items,
        int totalCount,
        int page,
        int pageSize)
    {
        return new ProfesionalesApiResult
        {
            Success = true,
            Message = "OK",
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public static ProfesionalesApiResult Fail(string message)
    {
        return new ProfesionalesApiResult
        {
            Success = false,
            Message = message
        };
    }
}

public sealed class ProfesionalApiOperationResult
{
    public bool Success { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public ProfesionalApiDto? Data { get; private set; }
    public int? ErrorStatusCode { get; private set; }

    public static ProfesionalApiOperationResult Ok(string message, ProfesionalApiDto? data = null)
    {
        return new ProfesionalApiOperationResult
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ProfesionalApiOperationResult Fail(string message, int statusCode = 422)
    {
        return new ProfesionalApiOperationResult
        {
            Success = false,
            Message = message,
            ErrorStatusCode = statusCode
        };
    }

    public static ProfesionalApiOperationResult Conflict(string message)
    {
        return new ProfesionalApiOperationResult
        {
            Success = false,
            Message = message,
            ErrorStatusCode = 409
        };
    }

    public static ProfesionalApiOperationResult NotFound(string message = "Profesional no encontrado.")
    {
        return new ProfesionalApiOperationResult
        {
            Success = false,
            Message = message,
            ErrorStatusCode = 404
        };
    }
}
