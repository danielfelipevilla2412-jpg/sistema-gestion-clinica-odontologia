using System.ComponentModel.DataAnnotations;

namespace SmileTrack_MVC.Models.ViewModels;

public sealed class ConfiguracionGeneralRequest
{
    [Required]
    public ConfiguracionCitasRequest Appointment { get; set; } = new();

    [Required]
    public ConfiguracionCentroRequest Center { get; set; } = new();

    [Required]
    public ConfiguracionNotificacionesRequest Notifications { get; set; } = new();
}

public sealed class ConfiguracionCitasRequest
{
    [Range(5, 180)]
    public int Duration { get; set; }

    [Required]
    [RegularExpression(@"^(?:[01]\d|2[0-3]):[0-5]\d$")]
    public string Open { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^(?:[01]\d|2[0-3]):[0-5]\d$")]
    public string Close { get; set; } = string.Empty;
}

public sealed class ConfiguracionCentroRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{3}\.\d{3}\.\d{3}-\d$")]
    public string Nit { get; set; } = string.Empty;

    [Required, StringLength(250, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;
}

public sealed class ConfiguracionNotificacionesRequest
{
    public bool Email { get; set; }
    public bool Sms { get; set; }
    public bool System { get; set; }
}
