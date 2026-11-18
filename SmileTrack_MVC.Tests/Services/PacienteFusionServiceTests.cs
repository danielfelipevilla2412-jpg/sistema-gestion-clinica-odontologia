using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Services;

public sealed class PacienteFusionServiceTests
{
    [Fact]
    public async Task FusionarAsync_ReasignaRelacionesYCompletaSoloCamposVacios()
    {
        await using var context = CrearContext();
        var principal = CrearPaciente("Principal", correo: null);
        var duplicado = CrearPaciente("Secundario", correo: "secundario@example.test", idUsuario: 24);
        context.Pacientes.AddRange(principal, duplicado);
        await context.SaveChangesAsync();
        context.Citas.Add(new Cita
        {
            IdPaciente = duplicado.IdPaciente,
            FechaHora = DateTime.UtcNow,
            Estado = "programada"
        });
        context.AlergiasPaciente.Add(new AlergiaPaciente
        {
            IdPaciente = duplicado.IdPaciente,
            Sustancia = "Penicilina"
        });
        context.Facturas.Add(new Factura
        {
            IdPaciente = duplicado.IdPaciente,
            NumeroFactura = "F-100",
            GeneradaPor = 44
        });
        context.PQRs.Add(new PqrEntity { IdPaciente = duplicado.IdPaciente });
        context.Notificaciones.Add(new Notificacion { IdPaciente = duplicado.IdPaciente });
        context.NotificacionesLeidas.AddRange(
            new NotificacionLeida
            {
                IdPaciente = principal.IdPaciente,
                IdCita = 300,
                FechaLectura = DateTime.UtcNow.AddDays(-1)
            },
            new NotificacionLeida
            {
                IdPaciente = duplicado.IdPaciente,
                IdCita = 300,
                FechaLectura = DateTime.UtcNow
            },
            new NotificacionLeida
            {
                IdPaciente = duplicado.IdPaciente,
                IdCita = 301,
                FechaLectura = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).FusionarAsync(CrearHttpContext(), principal.IdPaciente, duplicado.IdPaciente);

        Assert.True(result.Fusionada);
        Assert.Equal("secundario@example.test", principal.Correo);
        Assert.Equal(24, principal.IdUsuario);
        Assert.Equal("inactivo", duplicado.Estado);
        Assert.Null(duplicado.IdUsuario);
        Assert.Equal(principal.IdPaciente, await context.Citas.Select(c => c.IdPaciente).SingleAsync());
        Assert.Equal(principal.IdPaciente, await context.AlergiasPaciente.Select(a => a.IdPaciente).SingleAsync());
        Assert.Equal(principal.IdPaciente, await context.Facturas.Select(f => f.IdPaciente).SingleAsync());
        Assert.Equal(principal.IdPaciente, await context.PQRs.Select(p => p.IdPaciente).SingleAsync());
        Assert.Equal(principal.IdPaciente, await context.Notificaciones.Select(n => n.IdPaciente).SingleAsync());
        var lecturas = await context.NotificacionesLeidas.OrderBy(x => x.IdCita).ToListAsync();
        Assert.Equal(2, lecturas.Count);
        Assert.All(lecturas, lectura => Assert.Equal(principal.IdPaciente, lectura.IdPaciente));
        Assert.True(lecturas[0].FechaLectura < DateTime.UtcNow.AddHours(-12));
        Assert.Contains("principal=", (await context.Auditorias.SingleAsync()).Descripcion);
    }

    [Fact]
    public async Task FusionarAsync_ConservaLaHistoriaConContenidoYEliminaLaVacia()
    {
        await using var context = CrearContext();
        var principal = CrearPaciente("Principal");
        var duplicado = CrearPaciente("Secundario");
        context.Pacientes.AddRange(principal, duplicado);
        await context.SaveChangesAsync();
        var historiaVacia = new HistoriaClinica { IdPaciente = principal.IdPaciente, FechaApertura = DateTime.UtcNow };
        var historiaConContenido = new HistoriaClinica
        {
            IdPaciente = duplicado.IdPaciente,
            FechaApertura = DateTime.UtcNow,
            ObservacionesGenerales = "Antecedente clínico"
        };
        context.HistoriasClinicas.AddRange(historiaVacia, historiaConContenido);
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).FusionarAsync(CrearHttpContext(), principal.IdPaciente, duplicado.IdPaciente);

        Assert.True(result.Fusionada);
        var historia = await context.HistoriasClinicas.SingleAsync();
        Assert.Equal(historiaConContenido.IdHistoria, historia.IdHistoria);
        Assert.Equal(principal.IdPaciente, historia.IdPaciente);
    }

    [Fact]
    public async Task FusionarAsync_BloqueaDosHistoriasConContenidoYSoloAuditaElIntento()
    {
        await using var context = CrearContext();
        var principal = CrearPaciente("Principal", correo: "principal@example.test");
        var duplicado = CrearPaciente("Secundario", correo: "secundario@example.test");
        context.Pacientes.AddRange(principal, duplicado);
        await context.SaveChangesAsync();
        context.HistoriasClinicas.AddRange(
            new HistoriaClinica
            {
                IdPaciente = principal.IdPaciente,
                FechaApertura = DateTime.UtcNow,
                ObservacionesGenerales = "Historia principal"
            },
            new HistoriaClinica
            {
                IdPaciente = duplicado.IdPaciente,
                FechaApertura = DateTime.UtcNow,
                ObservacionesGenerales = "Historia secundaria"
            });
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).FusionarAsync(CrearHttpContext(), principal.IdPaciente, duplicado.IdPaciente);

        Assert.False(result.Fusionada);
        Assert.Equal("conflicto-historias", result.Codigo);
        Assert.Equal("principal@example.test", principal.Correo);
        Assert.Equal("activo", duplicado.Estado);
        Assert.Equal(2, await context.HistoriasClinicas.CountAsync());
        var evento = await context.Auditorias.SingleAsync();
        Assert.Contains($"principal={principal.IdPaciente}", evento.Descripcion);
        Assert.Contains($"duplicado={duplicado.IdPaciente}", evento.Descripcion);
        Assert.Equal(44, evento.IdUsuario);
    }

    [Fact]
    public async Task FusionarAsync_BloqueaDosUsuariosYSoloRegistraElIntento()
    {
        await using var context = CrearContext();
        var principal = CrearPaciente("Principal", idUsuario: 10);
        var duplicado = CrearPaciente("Secundario", idUsuario: 11);
        context.Pacientes.AddRange(principal, duplicado);
        await context.SaveChangesAsync();

        var result = await CrearServicio(context).FusionarAsync(CrearHttpContext(), principal.IdPaciente, duplicado.IdPaciente);

        Assert.False(result.Fusionada);
        Assert.Equal("conflicto-usuarios", result.Codigo);
        Assert.Equal(10, principal.IdUsuario);
        Assert.Equal(11, duplicado.IdUsuario);
        Assert.Equal("activo", duplicado.Estado);
        var evento = await context.Auditorias.SingleAsync();
        Assert.Equal(44, evento.IdUsuario);
        Assert.Contains($"principal={principal.IdPaciente}", evento.Descripcion);
        Assert.Contains($"duplicado={duplicado.IdPaciente}", evento.Descripcion);
    }

    private static Paciente CrearPaciente(string nombre, string? correo = "principal@example.test", int? idUsuario = null) => new()
    {
        TipoDocumento = "CC",
        Documento = Guid.NewGuid().ToString("N")[..10],
        Nombres = nombre,
        Apellidos = "Paciente",
        FechaNacimiento = new DateTime(1990, 1, 1),
        Correo = correo,
        IdUsuario = idUsuario
    };

    private static PacienteFusionService CrearServicio(AppDbContext context) =>
        new(context, new AuditoriaHistoriaClinicaService(context));

    private static DefaultHttpContext CrearHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "44")],
            "Test"));
        return httpContext;
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}