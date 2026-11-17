using System;
using System.Linq;
using SmileTrack_MVC.Helpers;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class CatalogCacheKeysTests
{
    [Fact]
    public void Constantes_DevuelvenLosValoresEsperados()
    {
        Assert.Equal("catalogo:servicios", CatalogCacheKeys.Servicios);
        Assert.Equal("catalogo:servicios:api", CatalogCacheKeys.ServiciosApi);
        Assert.Equal("catalogo:servicios:administracion", CatalogCacheKeys.ServiciosAdministracion);
        Assert.Equal("catalogo:especialidades", CatalogCacheKeys.Especialidades);
        Assert.Equal("catalogo:especialidades:api", CatalogCacheKeys.EspecialidadesApi);
        Assert.Equal("catalogo:consultorios", CatalogCacheKeys.Consultorios);
        Assert.Equal("catalogo:consultorios:disponibles", CatalogCacheKeys.ConsultoriosDisponibles);
        Assert.Equal("catalogo:estados-cita", CatalogCacheKeys.EstadosCita);
    }

    [Fact]
    public void Constantes_SonUnicasEntreSi()
    {
        string[] claves =
        {
            CatalogCacheKeys.Servicios,
            CatalogCacheKeys.ServiciosApi,
            CatalogCacheKeys.ServiciosAdministracion,
            CatalogCacheKeys.Especialidades,
            CatalogCacheKeys.EspecialidadesApi,
            CatalogCacheKeys.Consultorios,
            CatalogCacheKeys.ConsultoriosDisponibles,
            CatalogCacheKeys.EstadosCita
        };

        Assert.Equal(claves.Length, claves.Distinct().Count());
    }

    [Fact]
    public void Constantes_CompartenElPrefijoCatalogo()
    {
        string[] claves =
        {
            CatalogCacheKeys.Servicios,
            CatalogCacheKeys.ServiciosApi,
            CatalogCacheKeys.ServiciosAdministracion,
            CatalogCacheKeys.Especialidades,
            CatalogCacheKeys.EspecialidadesApi,
            CatalogCacheKeys.Consultorios,
            CatalogCacheKeys.ConsultoriosDisponibles,
            CatalogCacheKeys.EstadosCita
        };

        Assert.All(claves, clave => Assert.StartsWith("catalogo:", clave));
    }

    [Fact]
    public void SlidingExpiration_EsDeTreintaMinutos()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), CatalogCacheKeys.SlidingExpiration);
    }
}
