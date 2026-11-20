
namespace SmileTrack_MVC.Models.Api.Profesionales;

public sealed class ServicioProfesionalAsignacionApiRequest
{
    public int IdServicio { get; set; }

    public decimal? PrecioPersonalizado { get; set; }

    public bool Activo { get; set; } = true;
}
