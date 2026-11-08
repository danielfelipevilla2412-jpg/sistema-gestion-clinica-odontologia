using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Services;

public sealed class AuditoriaHistoriaClinicaServiceTests
{
    [Fact]
    public async Task RegistrarAsync_PersisteAccesoSinCopiarDatosClinicos()
    {
        await using var context = CrearContext();
        var httpContext = CrearHttpContext(8);
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");

        await new AuditoriaHistoriaClinicaService(context).RegistrarAsync(
            httpContext,
            "SELECT",
            "Historia_Clinica",
            42,
            "Consulta de historia clínica desde prueba.");

        var auditoria = await context.Auditorias.SingleAsync();
        Assert.Equal(8, auditoria.IdUsuario);
        Assert.Equal("SELECT", auditoria.Accion);
        Assert.Equal("Historia_Clinica", auditoria.TablaAfectada);
        Assert.Equal(42, auditoria.IdRegistro);
        Assert.Equal("192.0.2.10", auditoria.IpOrigen);
        Assert.Null(auditoria.DatosAnteriores);
        Assert.Null(auditoria.DatosNuevos);
    }

    [Fact]
    public async Task Agregar_RechazaAuditoriaSinUsuarioAutenticado()
    {
        await using var context = CrearContext();
        var service = new AuditoriaHistoriaClinicaService(context);

        Assert.Throws<InvalidOperationException>(() => service.Agregar(
            new DefaultHttpContext(),
            "SELECT",
            "Historia_Clinica",
            42,
            "Consulta de historia clínica desde prueba."));
    }

    private static DefaultHttpContext CrearHttpContext(int userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            "Test"));
        return context;
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
