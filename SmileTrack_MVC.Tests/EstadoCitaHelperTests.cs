using SmileTrack_MVC.Helpers;
using Xunit;

namespace SmileTrack_MVC.Tests;

public class EstadoCitaHelperTests
{
    [Theory]
    [InlineData("programada", "atendida")]
    [InlineData("programada", "no_asistida")]
    [InlineData("confirmada", "atendida")]
    [InlineData("confirmada", "no_asistida")]
    [InlineData("en_sala_de_espera", "en_proceso")]
    [InlineData("en_proceso", "atendida")]
    public void IsTransitionAllowed_AllowsClinicalTransitions(string estadoActual, string nuevoEstado)
    {
        Assert.True(EstadoCitaHelper.IsTransitionAllowed(estadoActual, nuevoEstado));
    }

    [Theory]
    [InlineData("atendida", "programada")]
    [InlineData("cancelada", "atendida")]
    [InlineData("no_asistida", "en_proceso")]
    public void IsTransitionAllowed_RejectsFinalOrInvalidTransitions(string estadoActual, string nuevoEstado)
    {
        Assert.False(EstadoCitaHelper.IsTransitionAllowed(estadoActual, nuevoEstado));
    }
}
