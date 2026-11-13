using System;
using SmileTrack_MVC.Helpers;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class ColombiaClockTests
{
    private static readonly TimeZoneInfo ZonaBogota =
        TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    [Fact]
    public void Now_CorrespondeAZonaHorariaDeColombia()
    {
        DateTime esperado = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaBogota);

        DateTime real = ColombiaClock.Now;

        TimeSpan diferencia = (real - esperado).Duration();
        Assert.True(
            diferencia < TimeSpan.FromSeconds(5),
            $"Se esperaba {esperado:o} para America/Bogota, pero se obtuvo {real:o} (diferencia {diferencia.TotalSeconds}s).");
    }

    [Fact]
    public void Today_EsLaFechaDeNowEnColombia()
    {
        DateTime now = ColombiaClock.Now;

        DateTime today = ColombiaClock.Today;

        Assert.Equal(now.Date, today);
    }

    [Fact]
    public void Today_NoIncluyeComponenteDeHora()
    {
        DateTime today = ColombiaClock.Today;

        Assert.Equal(TimeSpan.Zero, today.TimeOfDay);
    }

    [Fact]
    public void Now_EstaCincoHorasDetrasDeUtc()
    {
        DateTime utc = DateTime.UtcNow;

        DateTime now = ColombiaClock.Now;

        double desfaseHoras = (utc - now).TotalHours;
        Assert.InRange(desfaseHoras, 4.9, 5.01);
    }

    [Fact]
    public void Now_LlamadasConsecutivas_NoRetrocedenEnElTiempo()
    {
        DateTime primera = ColombiaClock.Now;

        DateTime segunda = ColombiaClock.Now;

        Assert.True(segunda >= primera, $"Se esperaba tiempo no decreciente, pero {segunda:o} < {primera:o}.");
    }
}
