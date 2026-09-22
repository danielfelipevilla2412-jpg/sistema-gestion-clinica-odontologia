using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmileTrack_MVC.Models.Api.CentroDeAyuda;

public sealed class CentroAyudaTicketDto
{
    public int IdTicket { get; init; }
    public string Referencia { get; init; } = string.Empty;
    public int IdUsuario { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Asunto { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string ModuloAfectado { get; init; } = string.Empty;
    public string Severidad { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string? CapturaPantalla { get; init; }
    public string Estado { get; init; } = string.Empty;
    public DateTime FechaCreacion { get; init; }
    public DateTime? FechaRespuesta { get; init; }
    public string? Respuesta { get; init; }
    public int? AtendidoPor { get; init; }
    public string? AtendidoPorNombre { get; init; }
}

public sealed class CentroAyudaTicketRequest
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Asunto { get; set; } = string.Empty;

    [Required]
    public string Categoria { get; set; } = "otro";

    [Required]
    public string ModuloAfectado { get; set; } = "sistema";

    [Required]
    public string Severidad { get; set; } = "media";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Descripcion { get; set; } = string.Empty;

    public IFormFile? CapturaPantalla { get; set; }
}

public sealed class CentroAyudaTicketUpdateRequest
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Asunto { get; set; } = string.Empty;

    [Required]
    public string Categoria { get; set; } = "otro";

    [Required]
    public string ModuloAfectado { get; set; } = "sistema";

    [Required]
    public string Severidad { get; set; } = "media";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    public string Estado { get; set; } = "abierto";

    public string? Respuesta { get; set; }
    public IFormFile? CapturaPantalla { get; set; }
}

public sealed class CentroAyudaEstadoRequest
{
    [Required]
    public string Estado { get; set; } = string.Empty;
}
