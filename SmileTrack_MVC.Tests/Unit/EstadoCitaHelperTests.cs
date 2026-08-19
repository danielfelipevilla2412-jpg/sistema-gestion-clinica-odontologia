using SmileTrack_MVC.Helpers;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class EstadoCitaHelperTests
{
    [Fact]
    public void ResolveEstadoNombre_UsesFallbackForUnknownState()
    {
        var resultado = EstadoCitaHelper.ResolveEstadoNombre("", "programada");

        Assert.Equal("Programada", resultado);
    }

    [Fact]
    public void ResolveEstadoNombre_NormalizesCommonValues()
    {
        Assert.Equal("Confirmada", EstadoCitaHelper.ResolveEstadoNombre("confirmada"));
        Assert.Equal("Atendida", EstadoCitaHelper.ResolveEstadoNombre("ATENDIDA"));
        Assert.Equal("Cancelada", EstadoCitaHelper.ResolveEstadoNombre("cancelada"));
    }
}
