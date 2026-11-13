using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class AccesoSeguridadControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IAuthService CrearAuthService(AppDbContext context, IHttpContextAccessor accessor, IEmailService email)
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

        return new AuthService(
            configuration,
            context,
            NullLogger<AuthService>.Instance,
            accessor,
            email,
            dataProtectionProvider);
    }

    // Proveedor mínimo con autenticación por cookie para las acciones que llaman SignIn/SignOut.
    private static IServiceProvider CrearServiciosHttp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddControllersWithViews();
        services.AddDataProtection();
        services.AddWebEncoders();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        return services.BuildServiceProvider();
    }

    private static AccesoYSeguridadController CrearControlador(
        AppDbContext context,
        IAuthService authService,
        IAntiforgery antiforgery,
        IEmailService emailService,
        DefaultHttpContext httpContext)
    {
        return new AccesoYSeguridadController(context, authService, antiforgery, emailService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public async Task Login_Get_RetornaVistaConReturnUrl()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = CrearServiciosHttp();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.Login("/destino-interno");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("/destino-interno", view.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task LoginPost_CamposVaciosConJson_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost(string.Empty, string.Empty, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var success = (bool)badRequest.Value!.GetType().GetProperty("success")!.GetValue(badRequest.Value)!;
        Assert.False(success);
    }

    [Fact]
    public async Task LoginPost_CamposVaciosSinJson_RetornaVistaConError()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost(string.Empty, string.Empty, null, null);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task LoginPost_UsuarioInexistenteConJson_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost("nadie@test.com", "Clave123!", null, null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task LoginPost_ContrasenaIncorrectaConJson_RetornaCodigo401()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "recep@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 2,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost("recep@test.com", "Incorrecta999!", null, null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        Assert.Equal(1, (await db.Usuarios.FindAsync(5))!.IntentosFallidos);
    }

    [Fact]
    public async Task LoginPost_CuentaBloqueadaConJson_RetornaCodigo403()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "bloqueado@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 2,
            Estado = "activo",
            IntentosFallidos = 3
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost("bloqueado@test.com", "Clave123!", null, null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task LoginPost_CredencialesValidasConJson_RetornaJsonConRedireccionDelRol()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "recep@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 2,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = CrearServiciosHttp();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost("recep@test.com", "Clave123!", null, null);

        var json = Assert.IsType<JsonResult>(result);
        Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
        Assert.Equal("/gestion-de-citas/st-rec-01-dashboard", json.Value!.GetType().GetProperty("redirectUrl")!.GetValue(json.Value)!.ToString());
    }

    [Fact]
    public async Task Logout_ConUsuarioAutenticado_RedirigeALoginYRegistraUltimoLogout()
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "recep@test.com",
            Contrasena = "hash",
            IdRol = 2,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "5") }, "TestAuth"))
        };
        ctx.RequestServices = CrearServiciosHttp();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.Logout();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/acceso-y-seguridad/login", redirect.Url);
        Assert.NotNull((await db.Usuarios.FindAsync(5))!.UltimoLogout);
    }

    [Fact]
    public async Task RecoverSendCode_SolicitudValida_RetornaOk()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.RecoverSendCode(new RecoverPasswordRequest { Correo = "nadie@test.com" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("Success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task RecoverSendCode_ModeloInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);
        controller.ModelState.AddModelError("Correo", "El correo es obligatorio");

        var result = await controller.RecoverSendCode(new RecoverPasswordRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RecoverSendCode_AntiforgeryFalla_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery { Fallar = true }, new FakeEmailService(), ctx);

        var result = await controller.RecoverSendCode(new RecoverPasswordRequest { Correo = "x@y.com" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RegisterPost_ModeloInvalidoConJson_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);
        controller.ModelState.AddModelError("Correo", "El correo es obligatorio");

        var result = await controller.RegisterPost(new RegisterRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RegisterPost_ContrasenasNoCoinciden_RetornaVistaConError()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.RegisterPost(new RegisterRequest
        {
            Correo = "nuevo@test.com",
            Contrasena = "Abc12345!",
            ConfirmarContrasena = "Otra12345!",
            Nombre = "Nuevo",
            Apellidos = "Paciente"
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task RegisterPost_RegistroExitoso_RedirigeALogin()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.RegisterPost(new RegisterRequest
        {
            Correo = "nuevo@test.com",
            Contrasena = "Abc12345!",
            ConfirmarContrasena = "Abc12345!",
            Nombre = "Nuevo",
            Apellidos = "Paciente",
            TipoDocumento = "CC",
            NumeroDocumento = "1234567890",
            FechaNacimiento = new DateTime(1995, 1, 1),
            AceptaTratamientoDatos = true
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/acceso-y-seguridad/login", redirect.Url);
        Assert.Equal(1, await db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task ChangePasswordPost_NoAutenticado_RedirigeALogin()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.ChangePasswordPost("actual", "Nueva123!", "Nueva123!");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ChangePasswordApi_UsuarioNoEncontrado_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "999") }, "TestAuth"))
        };
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.ChangePasswordApi(new ChangePasswordRequest
        {
            ContrasenaActual = "actual",
            NuevaContrasena = "Nueva123!",
            ConfirmarContrasena = "Nueva123!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ChangePasswordApi_CambioExitoso_RetornaOkYRequiereLogin()
    {
        await using var db = CrearContext();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "recep@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Actual123!"),
            IdRol = 2,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "5") }, "TestAuth"))
        };
        ctx.RequestServices = CrearServiciosHttp();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.ChangePasswordApi(new ChangePasswordRequest
        {
            ContrasenaActual = "Actual123!",
            NuevaContrasena = "Nueva123!",
            ConfirmarContrasena = "Nueva123!"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        Assert.True((bool)ok.Value!.GetType().GetProperty("requiresLogin")!.GetValue(ok.Value)!);
    }

    private sealed class FakeAntiforgery : IAntiforgery
    {
        public bool Fallar { get; set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) =>
            new(null, null, string.Empty, string.Empty);

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new(null, null, string.Empty, string.Empty);

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) =>
            Fallar
                ? Task.FromException(new AntiforgeryValidationException("Token de antiforgery inválido."))
                : Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default, string? motivoCancelacion = null, string? mensajePersonalizado = null) => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente, IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default) => Task.FromResult((true, "OK"));
    }
}
