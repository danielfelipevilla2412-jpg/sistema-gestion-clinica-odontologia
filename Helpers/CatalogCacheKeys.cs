namespace SmileTrack_MVC.Helpers;

public static class CatalogCacheKeys
{
    public const string Servicios = "catalogo:servicios";
    public const string ServiciosApi = "catalogo:servicios:api";
    public const string ServiciosAdministracion = "catalogo:servicios:administracion";
    public const string Especialidades = "catalogo:especialidades";
    public const string EspecialidadesApi = "catalogo:especialidades:api";
    public const string Consultorios = "catalogo:consultorios";
    public const string ConsultoriosDisponibles = "catalogo:consultorios:disponibles";
    public const string EstadosCita = "catalogo:estados-cita";

    public static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);
}
