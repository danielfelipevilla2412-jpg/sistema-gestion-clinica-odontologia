using SmileTrack_MVC.Helpers;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class NumeroFdiHelperTests
{
    [Theory]
    [InlineData("11")]
    [InlineData("48")]
    [InlineData("51")]
    [InlineData("85")]
    public void EsValido_AcceptsPermanentAndDeciduousTeeth(string numeroFdi)
    {
        Assert.True(NumeroFdiHelper.EsValido(numeroFdi));
    }

    [Theory]
    [InlineData("10")]
    [InlineData("19")]
    [InlineData("50")]
    [InlineData("86")]
    [InlineData("1A")]
    [InlineData("111")]
    public void EsValido_RejectsInvalidToothNumbers(string numeroFdi)
    {
        Assert.False(NumeroFdiHelper.EsValido(numeroFdi));
    }

    [Theory]
    [InlineData("11", true)]
    [InlineData("48", true)]
    [InlineData("54", false)]
    [InlineData("85", false)]
    public void EsPiezaPermanente_ExcludesPrimaryDentition(string numeroFdi, bool expected)
    {
        Assert.Equal(expected, NumeroFdiHelper.EsPiezaPermanente(numeroFdi));
    }
}
