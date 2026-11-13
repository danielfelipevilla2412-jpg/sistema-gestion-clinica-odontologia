using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class MobileAuthControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MobileAuthController CrearControlador(AppDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "clave-de-pruebas-smiletrack-para-jwt-con-longitud-suficiente",
                ["Jwt:Issuer"] = "SmileTrackTests",
                ["Jwt:Audience"] = "SmileTrackTestsClient",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var dataProtectionServices = new ServiceCollection();
        dataProtectionServices.AddDataProtection();
        var dataProtectionProvider = dataProtectionServices
            .BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();

        var authService = new AuthService(
            configuration,
            context,
            NullLogger<AuthService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new FakeEmailService(),
            dataProtectionProvider);

        return new MobileAuthController(authService);
    }

    private static LoginRequest RequestValido(string correo, string contrasena) => new()
    {
        Correo = correo,
        Contrasena = contrasena,
        Rol = "Paciente"
    };

    [Fact]
    public async Task Login_ModeloInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);
        controller.ModelState.AddModelError("Correo", "El correo es obligatorio");

        var result = await controller.Login(RequestValido("x@y.com", "Clave123!"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.False((bool)badRequest.Value!.GetType().GetProperty("success")!.GetValue(badRequest.Value)!);
    }

    [Fact]
    public async Task Login_CredencialesInvalidas_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Login(RequestValido("noexiste@test.com", "Clave123!"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_UsuarioInactivo_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 8,
            Nombre = "Inactivo",
            Apellidos = "Test",
            Correo = "inactivo@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 5,
            Estado = "inactivo"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Login(RequestValido("inactivo@test.com", "Clave123!"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_CuentaBloqueada_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 8,
            Nombre = "Bloqueado",
            Apellidos = "Test",
            Correo = "bloqueado@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 5,
            Estado = "activo",
            IntentosFallidos = 3
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Login(RequestValido("bloqueado@test.com", "Clave123!"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_CredencialesValidas_RetornaOkConToken()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 8,
            Nombre = "Valido",
            Apellidos = "Test",
            Correo = "valido@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 5,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Login(RequestValido("valido@test.com", "Clave123!"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        Assert.False(string.IsNullOrWhiteSpace(ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value)?.ToString()));
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default, string? motivoCancelacion = null, string? mensajePersonalizado = null) => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente, IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default) => Task.FromResult((true, "OK"));
    }
}
