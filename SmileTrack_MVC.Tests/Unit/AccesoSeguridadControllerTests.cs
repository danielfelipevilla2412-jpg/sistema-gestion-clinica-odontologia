using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
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
    private static string ObtenerDirectorioDatabase()
    {
        var candidatos = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (string candidato in candidatos)
        {
            var directorio = new DirectoryInfo(candidato);
            while (directorio is not null)
            {
                string databaseDirectory = Path.Combine(directorio.FullName, "Database");
                if (Directory.Exists(databaseDirectory) &&
                    (File.Exists(Path.Combine(databaseDirectory, "seed-usuarios-prueba.sql")) ||
                     File.Exists(Path.Combine(databaseDirectory, "SEED_USUARIOS_PRUEBA.sql"))))
                {
                    return databaseDirectory;
                }

                directorio = directorio.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "No se encontró el directorio Database desde el directorio de trabajo ni desde el directorio de salida de pruebas.");
    }

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
        => CrearServiciosHttp(null);

    private static IServiceProvider CrearServiciosHttp(
        CapturingAuthenticationService? authenticationService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddControllersWithViews();
        services.AddDataProtection();
        services.AddWebEncoders();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        if (authenticationService != null)
        {
            services.AddSingleton<IAuthenticationService>(authenticationService);
        }

        return services.BuildServiceProvider();
    }

    private sealed class CapturingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedInPrincipal { get; private set; }
        public AuthenticationProperties? SignedInProperties { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            SignedInPrincipal = principal;
            SignedInProperties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            Task.CompletedTask;
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
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            }
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

        var result = controller.Login("/destino-interno");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("/destino-interno", view.ViewData["ReturnUrl"]);
    }

    [Fact]
    public async Task Login_Get_NoCierraLaSesionActual()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "5") },
                CookieAuthenticationDefaults.AuthenticationScheme))
        };
        ctx.RequestServices = CrearServiciosHttp();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var emailService = new FakeEmailService();
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, emailService),
            new FakeAntiforgery(),
            emailService,
            ctx);

        Assert.IsType<ViewResult>(controller.Login());

        Assert.True(ctx.User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task LoginPost_CamposVaciosConJson_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost(string.Empty, string.Empty, "Paciente", null);

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

        var result = await controller.LoginPost(string.Empty, string.Empty, "Paciente", null);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task LoginPost_RolNoSeleccionadoConJson_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            ctx);

        var result = await controller.LoginPost("admin@smiletrack.co", "123456", null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Selecciona un rol válido.",
            badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task LoginPost_UsuarioInexistenteConJson_RetornaUnauthorized()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.LoginPost("nadie@test.com", "Clave123!", "Paciente", null);

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

        var result = await controller.LoginPost("recep@test.com", "Incorrecta999!", "Recepcionista", null);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        Assert.Equal(1, (await db.Usuarios.FindAsync(5))!.IntentosFallidos);
    }

    [Fact]
    public async Task LoginPost_CuentaBloqueadaConJson_RetornaErrorGenerico401()
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

        var result = await controller.LoginPost("bloqueado@test.com", "Clave123!", "Recepcionista", null);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        Assert.Equal(
            "Correo o contraseña incorrectos.",
            objectResult.Value!.GetType().GetProperty("message")!.GetValue(objectResult.Value));
    }

    [Fact]
    public async Task LoginPost_BloqueoExpirado_PermiteLoginYReiniciaIntentos()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "desbloqueado@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 2,
            Estado = "activo",
            IntentosFallidos = 3,
            BloqueadoHasta = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = CrearServiciosHttp();
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            ctx);

        var result = await controller.LoginPost(
            "desbloqueado@test.com",
            "Clave123!",
            "Recepcionista",
            null);

        Assert.IsType<JsonResult>(result);
        var usuario = await db.Usuarios.FindAsync(5);
        Assert.Equal(0, usuario!.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
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

        var result = await controller.LoginPost("recep@test.com", "Clave123!", "Recepcionista", null);

        var json = Assert.IsType<JsonResult>(result);
        Assert.True((bool)json.Value!.GetType().GetProperty("success")!.GetValue(json.Value)!);
        Assert.Equal("/gestion-de-citas/st-rec-01-dashboard", json.Value!.GetType().GetProperty("redirectUrl")!.GetValue(json.Value)!.ToString());
    }

    [Fact]
    public async Task LoginPost_CuentasDeTodosLosRoles_UsaIdentidadYPerfilDelUsuarioAutenticado()
    {
        await using var db = CrearContext();
        db.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Administrador" },
            new Rol { IdRol = 2, NombreRol = "Profesional" },
            new Rol { IdRol = 3, NombreRol = "Paciente" },
            new Rol { IdRol = 4, NombreRol = "Recepcionista" },
            new Rol { IdRol = 5, NombreRol = "Auxiliar" });

        const string password = "123456";
        var users = new[]
        {
            new Usuario { IdUsuario = 101, Nombre = "Admin", Apellidos = "Demo", Correo = "admin@smiletrack.co", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 102, Nombre = "Carlos", Apellidos = "Mendez", Correo = "prof@smiletrack.co", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 2, Estado = "activo" },
            new Usuario { IdUsuario = 103, Nombre = "Pro", Apellidos = "Dos", Correo = "pro.two@test.com", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 2, Estado = "activo" },
            new Usuario { IdUsuario = 104, Nombre = "Pedro", Apellidos = "Garcia", Correo = "pac@smiletrack.co", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 3, Estado = "activo" },
            new Usuario { IdUsuario = 105, Nombre = "Recepcionista", Apellidos = "Demo", Correo = "recep@smiletrack.co", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 4, Estado = "activo" },
            new Usuario { IdUsuario = 106, Nombre = "Auxiliar", Apellidos = "Demo", Correo = "aux@smiletrack.co", Contrasena = BCrypt.Net.BCrypt.HashPassword(password), IdRol = 5, Estado = "activo" }
        };
        db.Usuarios.AddRange(users);
        db.Profesionales.AddRange(
            new Profesional { IdProfesional = 201, IdUsuario = 102, Nombres = "Pro", Apellidos = "Uno", RegistroMedico = "REG-201", Estado = "activo" },
            new Profesional { IdProfesional = 202, IdUsuario = 103, Nombres = "Pro", Apellidos = "Dos", RegistroMedico = "REG-202", Estado = "activo" });
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 301,
            IdUsuario = 104,
            TipoDocumento = "CC",
            Documento = "1034567890",
            Nombres = "Paciente",
            Apellidos = "Uno",
            FechaNacimiento = new DateTime(1995, 1, 1),
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var cases = new[]
        {
            (UserId: 101, Email: "admin@smiletrack.co", Name: "Admin Demo", Role: "Administrador", Destination: "/gestion-de-citas/st-adm-01-dashboard", ProfileClaim: (string?)null, ProfileId: (int?)null),
            (UserId: 102, Email: "prof@smiletrack.co", Name: "Carlos Mendez", Role: "Profesional", Destination: "/gestion-de-profesionales/st-odo-01-dashboard", ProfileClaim: "IdProfesional", ProfileId: (int?)201),
            (UserId: 103, Email: "pro.two@test.com", Name: "Pro Dos", Role: "Profesional", Destination: "/gestion-de-profesionales/st-odo-01-dashboard", ProfileClaim: "IdProfesional", ProfileId: (int?)202),
            (UserId: 104, Email: "pac@smiletrack.co", Name: "Pedro Garcia", Role: "Paciente", Destination: "/gestion-de-citas/st-pac-01-mis-citas", ProfileClaim: "IdPaciente", ProfileId: (int?)301),
            (UserId: 105, Email: "recep@smiletrack.co", Name: "Recepcionista Demo", Role: "Recepcionista", Destination: "/gestion-de-citas/st-rec-01-dashboard", ProfileClaim: (string?)null, ProfileId: (int?)null),
            (UserId: 106, Email: "aux@smiletrack.co", Name: "Auxiliar Demo", Role: "Auxiliar", Destination: "/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo", ProfileClaim: (string?)null, ProfileId: (int?)null)
        };

        foreach (var account in cases)
        {
            var authenticationService = new CapturingAuthenticationService();
            var ctx = new DefaultHttpContext
            {
                RequestServices = CrearServiciosHttp(authenticationService)
            };
            ctx.Request.Headers["Accept"] = "application/json";
            var accessor = new HttpContextAccessor { HttpContext = ctx };
            var controller = CrearControlador(
                db,
                CrearAuthService(db, accessor, new FakeEmailService()),
                new FakeAntiforgery(),
                new FakeEmailService(),
                ctx);

            var result = await controller.LoginPost(
                account.Email,
                password,
                account.Role,
                null,
                remember: true);

            var json = Assert.IsType<JsonResult>(result);
            Assert.Equal(
                account.Destination,
                json.Value!.GetType().GetProperty("redirectUrl")!.GetValue(json.Value)!.ToString());

            var principal = Assert.IsType<ClaimsPrincipal>(authenticationService.SignedInPrincipal);
            Assert.Equal(
                account.UserId.ToString(),
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Equal(account.Role, principal.FindFirst(ClaimTypes.Role)?.Value);
            Assert.Equal(account.Name, principal.FindFirst(ClaimTypes.Name)?.Value);
            Assert.Equal(true, authenticationService.SignedInProperties?.IsPersistent);

            if (account.ProfileClaim != null)
            {
                Assert.Equal(
                    account.ProfileId.ToString(),
                    principal.FindFirst(account.ProfileClaim)?.Value);
            }
        }
    }

    [Fact]
    public void ScriptsUsuariosPrueba_UsanLaContrasenaCompartidaSolicitada()
    {
        string databaseDirectory = ObtenerDirectorioDatabase();
        string[] archivosSeed =
        {
            "seed-usuarios-prueba.sql",
            "SEED_USUARIOS_PRUEBA.sql"
        };
        string[] correos =
        {
            "admin@smiletrack.co",
            "prof@smiletrack.co",
            "pac@smiletrack.co",
            "recep@smiletrack.co",
            "aux@smiletrack.co"
        };
        var rolesEsperados = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin@smiletrack.co"] = 1,
            ["prof@smiletrack.co"] = 2,
            ["pac@smiletrack.co"] = 3,
            ["recep@smiletrack.co"] = 4,
            ["aux@smiletrack.co"] = 5
        };

        foreach (string archivoSeed in archivosSeed)
        {
            string contenido = File.ReadAllText(Path.Combine(databaseDirectory, archivoSeed));
            foreach (string correo in correos)
            {
                Assert.Contains(correo, contenido, StringComparison.OrdinalIgnoreCase);
            }

            var hashes = new HashSet<string>();
            foreach (Match match in Regex.Matches(
                contenido,
                @"\$2[ab]\$\d{2}\$[./A-Za-z0-9]{53}"))
            {
                hashes.Add(match.Value);
            }

            Assert.NotEmpty(hashes);
            foreach (string hash in hashes)
            {
                Assert.True(
                    BCrypt.Net.BCrypt.Verify("123456", hash),
                    $"El hash de {archivoSeed} no corresponde a la contraseña de prueba.");
            }
        }

        string scriptPrincipal = Path.Combine(databaseDirectory, "SCRIPT_SQL_UNICO_SMILETRACK.sql");
        foreach (string correo in correos)
        {
            string? insercionUsuario = null;
            foreach (string linea in File.ReadLines(scriptPrincipal))
            {
                    if (linea.StartsWith("INSERT [dbo].[Usuario]", StringComparison.OrdinalIgnoreCase) &&
                        linea.Contains(correo, StringComparison.OrdinalIgnoreCase))
                    {
                        insercionUsuario = linea;
                        break;
                    }
            }

            Assert.NotNull(insercionUsuario);
            Match hashPrincipal = Regex.Match(
                    insercionUsuario!,
                    @"\$2[ab]\$\d{2}\$[./A-Za-z0-9]{53}");
            Assert.True(hashPrincipal.Success);
            Assert.True(
                    BCrypt.Net.BCrypt.Verify("123456", hashPrincipal.Value),
                    $"El usuario {correo} del script principal no puede ingresar con la contraseña de prueba.");

            Match rolActivoPrincipal = Regex.Match(
                insercionUsuario!,
                @"N'\$2[ab]\$\d{2}\$[./A-Za-z0-9]{53}',\s*(\d+),\s*N'activo'",
                RegexOptions.IgnoreCase);
            Assert.True(
                rolActivoPrincipal.Success,
                $"El usuario {correo} debe estar activo y tener su rol asignado en el script principal.");
            Assert.Equal(
                rolesEsperados[correo],
                int.Parse(rolActivoPrincipal.Groups[1].Value));
        }
    }

    [Fact]
    public async Task LoginPost_RolSeleccionadoNoCoincide_RechazaInicioDeSesion()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 3, NombreRol = "Paciente" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 104,
            Nombre = "Pedro",
            Apellidos = "Demo",
            Correo = "pac@smiletrack.co",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("123456"),
            IdRol = 3,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var authenticationService = new CapturingAuthenticationService();
        var ctx = new DefaultHttpContext
        {
            RequestServices = CrearServiciosHttp(authenticationService)
        };
        ctx.Request.Headers["Accept"] = "application/json";
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            ctx);

        var result = await controller.LoginPost(
            "pac@smiletrack.co",
            "123456",
            "Administrador",
            null);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(authenticationService.SignedInPrincipal);
        Assert.Equal(0, (await db.Usuarios.FindAsync(104))!.IntentosFallidos);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task LoginPost_ReturnUrlExterno_RedireccionaAlDashboardDelRol(string returnUrl)
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
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            ctx);

        var result = await controller.LoginPost(
            "recep@test.com",
            "Clave123!",
            "Recepcionista",
            returnUrl);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(
            "/gestion-de-citas/st-rec-01-dashboard",
            json.Value!.GetType().GetProperty("redirectUrl")!.GetValue(json.Value)!.ToString());
    }

    [Fact]
    public async Task AccessDenied_RetornaVistaConEstadoForbidden()
    {
        await using var db = CrearContext();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var emailService = new FakeEmailService();
        var controller = new AccesoYSeguridadController(
            db,
            CrearAuthService(db, accessor, emailService),
            new FakeAntiforgery(),
            emailService)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };

        var result = Assert.IsType<ViewResult>(controller.AccessDenied());

        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
        Assert.Equal(
            "~/Views/Acceso_Y_Seguridad/access-denied/index.cshtml",
            result.ViewName);
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

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "No fue posible validar la solicitud. Recarga la página e inténtalo de nuevo.",
            badRequest.Value!.GetType().GetProperty("Message")!.GetValue(badRequest.Value));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisterPost_IdentificadorDuplicado_NoCreaOtraCuenta(
        bool duplicateEmail)
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Paciente",
            Apellidos = "Existente",
            Correo = "existente@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword("Clave123!"),
            IdRol = 5,
            Estado = "activo"
        });
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 20,
            IdUsuario = 10,
            TipoDocumento = "CC",
            Documento = "1234567890",
            Nombres = "Paciente",
            Apellidos = "Existente",
            FechaNacimiento = new DateTime(1995, 1, 1),
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var email = new FakeEmailService();
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, email),
            new FakeAntiforgery(),
            email,
            ctx);

        var result = await controller.RegisterPost(new RegisterRequest
        {
            Correo = duplicateEmail ? " EXISTENTE@TEST.COM " : "nuevo@test.com",
            Contrasena = "Abc12345!",
            ConfirmarContrasena = "Abc12345!",
            Nombre = "Nuevo",
            Apellidos = "Paciente",
            TipoDocumento = "CC",
            NumeroDocumento = duplicateEmail ? "9876543210" : "1234567890",
            FechaNacimiento = new DateTime(1995, 1, 1),
            Telefono = "+573001234567",
            AceptaTratamientoDatos = true
        });

        Assert.IsType<ViewResult>(result);
        Assert.Equal(1, await db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task RegisterPost_CreaCuentaNormalizada_QuePuedeIniciarSesionConSuPerfil()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 5, NombreRol = "Paciente" });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(db, CrearAuthService(db, accessor, new FakeEmailService()), new FakeAntiforgery(), new FakeEmailService(), ctx);

        var result = await controller.RegisterPost(new RegisterRequest
        {
            Correo = " Nuevo@Test.com ",
            Contrasena = "Abc12345!",
            ConfirmarContrasena = "Abc12345!",
            Nombre = "Nuevo",
            Apellidos = "Paciente",
            TipoDocumento = "CC",
            NumeroDocumento = "1234567890",
            FechaNacimiento = new DateTime(1995, 1, 1),
            Telefono = "+573001234567",
            AceptaTratamientoDatos = true
        });

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/acceso-y-seguridad/login", redirect.Url);
        Assert.Equal(1, await db.Usuarios.CountAsync());

        var usuarioCreado = await db.Usuarios.SingleAsync();
        Assert.Equal("nuevo@test.com", usuarioCreado.Correo);
        Assert.Equal(1, await db.Pacientes.CountAsync(p => p.IdUsuario == usuarioCreado.IdUsuario));

        var authenticationService = new CapturingAuthenticationService();
        var loginContext = new DefaultHttpContext
        {
            RequestServices = CrearServiciosHttp(authenticationService)
        };
        loginContext.Request.Headers["Accept"] = "application/json";
        var loginAccessor = new HttpContextAccessor { HttpContext = loginContext };
        var loginController = CrearControlador(
            db,
            CrearAuthService(db, loginAccessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            loginContext);

        var loginResult = await loginController.LoginPost(
            " NUEVO@TEST.COM ",
            "Abc12345!",
            "Paciente",
            null);

        var loginJson = Assert.IsType<JsonResult>(loginResult);
        Assert.True((bool)loginJson.Value!.GetType().GetProperty("success")!.GetValue(loginJson.Value)!);
        Assert.Equal(
            usuarioCreado.IdUsuario.ToString(),
            authenticationService.SignedInPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(
            (await db.Pacientes.SingleAsync(p => p.IdUsuario == usuarioCreado.IdUsuario)).IdPaciente.ToString(),
            authenticationService.SignedInPrincipal?.FindFirst("IdPaciente")?.Value);
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
        Assert.NotNull((await db.Usuarios.FindAsync(5))!.UltimoLogout);
    }

    [Fact]
    public async Task ChangePasswordApi_ContrasenaDebil_NoActualizaLaCuenta()
    {
        await using var db = CrearContext();
        const string currentPassword = "Actual123!";
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 5,
            Nombre = "Recep",
            Apellidos = "Test",
            Correo = "recep@test.com",
            Contrasena = BCrypt.Net.BCrypt.HashPassword(currentPassword),
            IdRol = 2,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "5") }, "TestAuth"))
        };
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, new FakeEmailService()),
            new FakeAntiforgery(),
            new FakeEmailService(),
            ctx);

        var result = await controller.ChangePasswordApi(new ChangePasswordRequest
        {
            ContrasenaActual = currentPassword,
            NuevaContrasena = "weakpassword1",
            ConfirmarContrasena = "weakpassword1"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        var user = await db.Usuarios.FindAsync(5);
        Assert.True(BCrypt.Net.BCrypt.Verify(currentPassword, user!.Contrasena));
        Assert.Null(user.UltimoLogout);
    }

    [Fact]
    public void Stadm16RunTestSmtp_ExigePostYAntiforgery()
    {
        var method = typeof(AccesoYSeguridadController).GetMethod(
            nameof(AccesoYSeguridadController.Stadm16RunTestSmtp));

        Assert.NotNull(method);
        Assert.NotNull(Attribute.GetCustomAttribute(method, typeof(HttpPostAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(method, typeof(ValidateAntiForgeryTokenAttribute)));
        Assert.Null(Attribute.GetCustomAttribute(method, typeof(HttpGetAttribute)));
    }

    [Fact]
    public async Task Stadm16TestSmtp_Get_NoEnviaCorreo()
    {
        await using var db = CrearContext();
        var email = new FakeEmailService();
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, email),
            new FakeAntiforgery(),
            email,
            ctx);

        var result = Assert.IsType<ContentResult>(controller.Stadm16TestSmtp());

        Assert.Equal(0, email.SmtpTestCalls);
        Assert.Contains("method: 'POST'", result.Content);
        Assert.DoesNotContain("?destino=", result.Content);
    }

    [Fact]
    public async Task Stadm16RunTestSmtp_ExcepcionInterna_NoExponeDetalles()
    {
        await using var db = CrearContext();
        var email = new FakeEmailService
        {
            ErrorPruebaSmtp = new InvalidOperationException("smtp-internal-secret")
        };
        var ctx = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        var controller = CrearControlador(
            db,
            CrearAuthService(db, accessor, email),
            new FakeAntiforgery(),
            email,
            ctx);

        var result = Assert.IsType<ObjectResult>(await controller.Stadm16RunTestSmtp());
        string detalle = result.Value!.GetType().GetProperty("detalle")!.GetValue(result.Value)!.ToString()!;

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal(1, email.SmtpTestCalls);
        Assert.Contains("registros del servidor", detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("smtp-internal-secret", detalle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", detalle, StringComparison.OrdinalIgnoreCase);
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
        public int SmtpTestCalls { get; private set; }
        public Exception? ErrorPruebaSmtp { get; init; }

        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default, string? motivoCancelacion = null, string? mensajePersonalizado = null) => Task.CompletedTask;

        public Task SendFacturaRecordatorioAsync(string recipientEmail, string nombrePaciente, IReadOnlyList<FacturaRecordatorioEmail> facturas, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default)
        {
            SmtpTestCalls++;
            return ErrorPruebaSmtp is null
                ? Task.FromResult((true, "OK"))
                : Task.FromException<(bool Exito, string Detalle)>(ErrorPruebaSmtp);
        }
    }
}
