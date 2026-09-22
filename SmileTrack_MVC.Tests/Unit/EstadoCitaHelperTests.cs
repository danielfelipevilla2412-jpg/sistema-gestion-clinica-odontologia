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

    [Fact]
    public void Normalize_MapsEnSalaDeEsperaAliasesToCanonicalState()
    {
        Assert.Equal("en_sala_de_espera", EstadoCitaHelper.Normalize("En sala de espera"));
        Assert.Equal("en_sala_de_espera", EstadoCitaHelper.Normalize("sala de espera"));
        Assert.Equal("En sala de espera", EstadoCitaHelper.ResolveEstadoNombre("en_sala_de_espera"));
    }

    [Theory]
    [InlineData("agendada", "programada")]
    [InlineData("En consulta", "en_proceso")]
    [InlineData("finalizada", "atendida")]
    [InlineData("no asistió", "no_asistida")]
    [InlineData("En sala de espera", "en_sala_de_espera")]
    public void Normalize_ReturnsCanonicalAppointmentToken(string estado, string esperado)
    {
        Assert.Equal(esperado, EstadoCitaHelper.Normalize(estado));
    }

    [Theory]
    [InlineData("programada", "cancelada", false)]
    [InlineData("confirmada", "en_sala_espera", true)]
    [InlineData("en_sala_espera", "en_proceso", true)]
    [InlineData("en_proceso", "atendida", true)]
    [InlineData("confirmada", "cancelada", false)]
    public void IsTransitionAllowed_RespectsStateMachine(string actual, string siguiente, bool esperado)
    {
        Assert.Equal(esperado, EstadoCitaHelper.IsTransitionAllowed(actual, siguiente));
    }
}
