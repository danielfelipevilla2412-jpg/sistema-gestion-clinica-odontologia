using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmileTrack_MVC.Models.ViewModels;

/// <summary>
/// Payload del formulario "Nuevo Ticket de Soporte". Se recibe como
/// [FromForm] porque el formulario es multipart/form-data (incluye el
/// adjunto opcional de captura de pantalla).
/// </summary>
public sealed class CrearTicketSoporteRequest
{
    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "El asunto debe tener entre 5 y 200 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    public TicketCategory? Category { get; set; }

    [Required(ErrorMessage = "El módulo afectado es obligatorio.")]
    public AffectedModule? Module { get; set; }

    [Required(ErrorMessage = "Selecciona una severidad (Baja, Media o Alta).")]
    public string Severity { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 2000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Adjunto opcional. Se valida tipo/tamaño en el controlador.</summary>
    public IFormFile? Screenshot { get; set; }
}

public sealed class ResponderTicketRequest
{
    [Required(ErrorMessage = "La respuesta no puede estar vacía.")]
    [StringLength(2000, MinimumLength = 3, ErrorMessage = "La respuesta debe tener al menos 3 caracteres.")]
    public string Respuesta { get; set; } = string.Empty;

    /// <summary>Debe ser "resuelto" o "cerrado".</summary>
    [Required(ErrorMessage = "Indica el nuevo estado del ticket.")]
    public string NuevoEstado { get; set; } = "resuelto";
}

public sealed class ActualizarEstadoTicketRequest
{
    /// <summary>Uno de: abierto, en_proceso, resuelto, cerrado.</summary>
    [Required(ErrorMessage = "Indica el nuevo estado del ticket.")]
    public string NuevoEstado { get; set; } = string.Empty;
}
