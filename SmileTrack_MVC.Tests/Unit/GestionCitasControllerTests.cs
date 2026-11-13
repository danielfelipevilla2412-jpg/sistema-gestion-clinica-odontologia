using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class GestionCitasControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static GestionCitasController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var controller = new GestionCitasController(
            db,
            NullLogger<GestionCitasController>.Instance,
            new FakeEmailService(),
            new CitaService(db, NullLogger<CitaService>.Instance),
            new FakePanelOperativoService(),
            new FakeAntiforgery(),
            new FakeCitasDashboardService(),
            new FakeAgendaService(),
            new MemoryCache(new MemoryCacheOptions()));

        var claims = new List<Claim>();
        if (idUsuario.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, idUsuario.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, "Paciente"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    private static void SembrarPaciente(AppDbContext db)
    {
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            TipoDocumento = "CC",
            Documento = "1234567",
            Nombres = "Ana",
            Apellidos = "Prueba",
            FechaNacimiento = new DateTime(1990, 5, 5),
            Estado = "activo"
        });
    }

    [Fact]
    public async Task ObtenerConteoNoLeidasApi_SinUsuarioAutenticado_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.ObtenerConteoNoLeidasApi(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task MarcarNotificacionLeida_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.MarcarNotificacionLeida(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MarcarNotificacionLeida_PacienteNoAsociado_RetornaForbid()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.MarcarNotificacionLeida(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MarcarNotificacionLeida_NotificacionInexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.MarcarNotificacionLeida(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MarcarNotificacionLeida_NotificacionDelPaciente_MarcaComoLeida()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        db.Notificaciones.Add(new Notificacion
        {
            IdNotificacion = 1,
            IdPaciente = 1,
            Titulo = "Recordatorio",
            Contenido = "Tienes una cita mañana.",
            Estado = "pendiente",
            CreadaEn = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.MarcarNotificacionLeida(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var notif = await db.Notificaciones.FindAsync(1);
        Assert.Equal("leida", notif!.Estado);
        Assert.NotNull(notif.FechaLectura);
    }

    [Fact]
    public async Task EliminarNotificacion_NotificacionDelPaciente_MarcaComoEliminada()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        db.Notificaciones.Add(new Notificacion
        {
            IdNotificacion = 2,
            IdPaciente = 1,
            Titulo = "Recordatorio",
            Contenido = "Limpieza programada.",
            Estado = "pendiente",
            CreadaEn = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.EliminarNotificacion(2, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var notif = await db.Notificaciones.FindAsync(2);
        Assert.Equal("eliminada", notif!.Estado);
    }

    [Fact]
    public async Task MarcarNotificacionCitaLeida_CitaAjena_RetornaNotFound()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        db.Citas.Add(new Cita
        {
            IdCita = 5,
            IdPaciente = 99,
            Estado = "Programada",
            FechaHora = DateTime.Now.AddDays(1)
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.MarcarNotificacionCitaLeida(5, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default, string? motivoCancelacion = null, string? mensajePersonalizado = null) => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente, IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default) => Task.FromResult((true, "OK"));
    }

    private sealed class FakePanelOperativoService : IPanelOperativoService
    {
        public Task<PanelOperativoViewModel> ObtenerAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCitasDashboardService : ICitasDashboardService
    {
        public Task<CitasDashboardViewModel> ObtenerDashboardAsync(DateTime fechaReferencia, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeAgendaService : IAgendaService
    {
        public Task<AgendaViewModel> ObtenerAgendaAsync(DateTime? weekStart, int? professionalId, int? officeId, CancellationToken cancellationToken = default, bool includeCatalogs = true) => throw new NotImplementedException();
    }

    private sealed class FakeAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new(null, null, string.Empty, string.Empty);

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => new(null, null, string.Empty, string.Empty);

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }
}
