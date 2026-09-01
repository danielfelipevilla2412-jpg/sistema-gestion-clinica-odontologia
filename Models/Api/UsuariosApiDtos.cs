using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.Api.Administracion;

public sealed class UsuarioApiDto
{
    public int IdUsuario { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Apellidos { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public int IdRol { get; init; }
    public string Rol { get; init; } = string.Empty;
    public string Estado { get; init; } = "activo";
    public int IntentosFallidos { get; init; }
    public bool EstaBloqueado { get; init; }
    public DateTime FechaCreacion { get; init; }
    public DateTime? UltimoLogin { get; init; }
}

public sealed class UsuarioApiCreateDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Apellidos { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Correo { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Contrasena { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IdRol { get; set; }

    [RegularExpression("^(activo|inactivo)$", ErrorMessage = "El estado debe ser activo o inactivo.")]
    public string? Estado { get; set; }
}

public sealed class UsuarioApiUpdateDto
{
    [StringLength(100, MinimumLength = 2)]
    public string? Nombre { get; set; }

    [StringLength(100, MinimumLength = 2)]
    public string? Apellidos { get; set; }

    [EmailAddress, StringLength(150)]
    public string? Correo { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdRol { get; set; }
}

public sealed class UsuarioApiEstadoDto
{
    [Required]
    [RegularExpression("^(activo|inactivo)$", ErrorMessage = "El estado debe ser activo o inactivo.")]
    public string Estado { get; set; } = string.Empty;
}

public sealed class UsuarioApiRestablecerContrasenaDto
{
    [Required, StringLength(100, MinimumLength = 8)]
    public string ContrasenaTemporal { get; set; } = string.Empty;
}

public sealed class RolDisponibleApiDto
{
    public int IdRol { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public int UsuariosAsignados { get; init; }
}

public sealed class UsuariosApiResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? ErrorStatusCode { get; init; }
    public UsuarioApiDto? Data { get; init; }
    public List<UsuarioApiDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static UsuariosApiResult Ok(UsuarioApiDto data, string message) => new() { Success = true, Data = data, Message = message };
    public static UsuariosApiResult List(List<UsuarioApiDto> items, int total, int page, int pageSize) => new() { Success = true, Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    public static UsuariosApiResult Fail(string message, int status = 422) => new() { Success = false, Message = message, ErrorStatusCode = status };
}
