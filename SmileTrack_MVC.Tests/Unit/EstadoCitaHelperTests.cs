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

    [Theory]
    [InlineData("agendada", "programada")]
    [InlineData("En consulta", "en_proceso")]
    [InlineData("finalizada", "atendida")]
    [InlineData("no asistió", "no_asistida")]
    public void Normalize_ReturnsCanonicalAppointmentToken(string estado, string esperado)
    {
        Assert.Equal(esperado, EstadoCitaHelper.Normalize(estado));
    }
}
