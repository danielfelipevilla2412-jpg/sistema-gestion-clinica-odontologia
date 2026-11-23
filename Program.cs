using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Perfiles;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// NOTA sobre orden de configuración: CADA NUEVA FUENTE AGREGADA SOBRESCRIBE A LAS ANTERIORES.
// Sources.Clear() borra el registro automático del builder; reconstruimos manualmente:
//
//   Development:
//     appsettings.json → appsettings.Development.json → appsettings.Local.json
//     → user-secrets → variables de entorno → CLI
//
//   Testing (entorno aislado con SmileTrackTest en LocalDB):
//     appsettings.json → appsettings.Testing.json
//     → user-secrets (cadena de conexión LocalDB sin contraseña)
//     → variables de entorno → CLI
//     * appsettings.Local.json NO se carga: evita que las credenciales de
//       desarrollo sobreescriban el entorno de pruebas.
//
//   Production:
//     appsettings.json → appsettings.Production.json
//     → variables de entorno → CLI

builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true);

// appsettings.Local.json no se carga en Testing para que las credenciales
// de desarrollo no sobreescriban el entorno de pruebas aislado.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

// User-secrets se cargan en Development y Testing.
// En Testing apuntan a SmileTrackTest (LocalDB) — sin contraseñas en texto plano.
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration.AddUserSecrets(
        "ff5d4b9c-690c-44b5-a2e6-8cd9fe7ea759",
        reloadOnChange: true);
}

builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddControllersWithViews()
    .ConfigureApiBehaviorOptions(options =>
    {
        var defaultFactory = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller) &&
                string.Equals(controller, "GestionPacientesApi", StringComparison.OrdinalIgnoreCase))
            {
                return new Microsoft.AspNetCore.Mvc.UnprocessableEntityObjectResult(
                    new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
                    {
                        Status = StatusCodes.Status422UnprocessableEntity
                    });
            }

            return defaultFactory(context);
        };
    });
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<SmileTrack_MVC.Services.CentroDeAyuda.ICentroDeAyudaService, SmileTrack_MVC.Services.CentroDeAyuda.CentroDeAyudaService>();
builder.Services.AddScoped<SmileTrack_MVC.Services.Facturacion.IFacturacionQueryService, SmileTrack_MVC.Services.Facturacion.FacturacionQueryService>();
builder.Services.AddScoped<SmileTrack_MVC.Services.Facturacion.IFacturacionService, SmileTrack_MVC.Services.Facturacion.FacturacionService>();
builder.Services.AddScoped<SmileTrack_MVC.Services.Perfiles.IPerfilPacienteService, SmileTrack_MVC.Services.Perfiles.PerfilPacienteService>();

bool ejecutandoEnContenedor =
    string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase)
    ||
    string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "1",
        StringComparison.OrdinalIgnoreCase);

// -----------------------------------------------------------------------------
// DATA PROTECTION
// En Docker las claves deben persistir fuera del ciclo de vida del contenedor.
// docker-compose.yml monta:
// smiletrack-dataprotection:/root/.aspnet/DataProtection-Keys
// -----------------------------------------------------------------------------

if (ejecutandoEnContenedor)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo("/root/.aspnet/DataProtection-Keys"));
}


// -----------------------------------------------------------------------------
// RATE LIMITING
// -----------------------------------------------------------------------------

static string GetRateLimitPartitionKey(HttpContext context)
{
    var address = context.Connection.RemoteIpAddress;
    if (address == null)
    {
        return "unknown";
    }

    return address.IsIPv4MappedToIPv6
        ? address.MapToIPv4().ToString()
        : address.ToString();
}

static FixedWindowRateLimiterOptions CreateFixedWindowOptions(
    int permitLimit,
    TimeSpan window) =>
    new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0
    };

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    foreach (string proxy in
             builder.Configuration.GetSection("KnownProxies").Get<string[]>() ??
             Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginByIp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => CreateFixedWindowOptions(
                permitLimit: 10,
                window: TimeSpan.FromMinutes(15))));

    options.AddPolicy("AccountRecoveryByIp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => CreateFixedWindowOptions(
                permitLimit: 5,
                window: TimeSpan.FromMinutes(15))));

    options.AddPolicy("RegisterByIp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(context),
            _ => CreateFixedWindowOptions(
                permitLimit: 5,
                window: TimeSpan.FromHours(1))));

    options.AddFixedWindowLimiter("PerfilPaciente", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Política para solicitudes de cita por paciente: 10 peticiones / minuto
    options.AddFixedWindowLimiter("CitaSolicitud", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Política para envío masivo de recordatorios: 5 peticiones / minuto
    options.AddFixedWindowLimiter("RecordatoriosEnvio", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Política para reasignación de ausencias: 3 peticiones / minuto
    options.AddFixedWindowLimiter("AusenciaReasignacion", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        int retryAfterSeconds = 60;
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds));
        }

        response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        bool esApi = context.HttpContext.Request.Headers["Accept"]
            .ToString()
            .Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase);

        if (esApi)
        {
            response.ContentType = "application/json; charset=utf-8";
            await context.HttpContext.Response.WriteAsync(
                """{"success":false,"message":"Demasiadas solicitudes. Intenta de nuevo más tarde."}""",
                ct);
        }
        else
        {
            response.ContentType = "text/html; charset=utf-8";

            await context.HttpContext.Response.WriteAsync(
                """<!doctype html><html lang="es"><meta charset="utf-8"><title>Demasiadas solicitudes</title><main><h1>Demasiadas solicitudes</h1><p>Intenta de nuevo más tarde.</p><a href="/">Volver al inicio</a></main></html>""",
                ct);
        }
    };
});

// -----------------------------------------------------------------------------
// ANTIFORGERY
// -----------------------------------------------------------------------------

bool redirectHttpToHttps =
    builder.Configuration.GetValue<bool?>("Security:RedirectHttpToHttps")
    ?? !builder.Environment.IsDevelopment();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// -----------------------------------------------------------------------------
// DATABASE
// -----------------------------------------------------------------------------

string? connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se encontró ConnectionStrings:DefaultConnection. " +
        "Configure la cadena de conexión en appsettings.Local.json.");
}

// Diagnóstico temporal: muestra el destino de la conexión sin revelar credenciales.
var conexionDiagnostico = new SqlConnectionStringBuilder(connectionString);
Console.WriteLine("========== CONEXIÓN SMILETRACK ==========");
Console.WriteLine($"Servidor: {conexionDiagnostico.DataSource}");
Console.WriteLine($"Base de datos: {conexionDiagnostico.InitialCatalog}");
Console.WriteLine($"Entorno: {builder.Environment.EnvironmentName}");
Console.WriteLine("=========================================");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorNumbersToAdd: null)));
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver", timeout: TimeSpan.FromSeconds(5));

builder.Services.AddScoped<ICitaService, CitaService>();
builder.Services.AddScoped<ICitaQueryService, CitaQueryService>();
builder.Services.AddScoped<
    ICitaDisponibilidadService,
    SmileTrack_MVC.Services.GestionDeCitas.CitaDisponibilidadService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
builder.Services.AddScoped<IPanelOperativoService, PanelOperativoService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.ICitasDashboardService,
    SmileTrack_MVC.Services.CitasDashboardService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.GestionDeCitas.IPacienteNotificacionQueryService,
    SmileTrack_MVC.Services.GestionDeCitas.PacienteNotificacionQueryService>();

// Cierre automático de citas vencidas: se ejecuta a diario a las 23:59 y deja la agenda limpia
// al día siguiente (las citas en espera que ya pasaron pasan a "No asistio").
builder.Services.AddHostedService<
    SmileTrack_MVC.Services.GestionDeCitas.CitasVencidasBackgroundService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.GestionDeCitas.IRecordatorioCitaService,
    SmileTrack_MVC.Services.GestionDeCitas.RecordatorioCitaService>();
builder.Services.AddHostedService<
    SmileTrack_MVC.Services.GestionDeCitas.RecordatoriosCitasBackgroundService>();

// -----------------------------------------------------------------------------
// JWT
// -----------------------------------------------------------------------------

var jwtSection =
    builder.Configuration.GetSection("Jwt");

string jwtKey =
    jwtSection.GetValue<string>("Key")
    ?? throw new InvalidOperationException(
        "No se encontró Jwt:Key. Configure la clave JWT mediante User Secrets o variables de entorno.");

if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key debe estar configurada y tener al menos 32 caracteres.");
}

string jwtIssuer =
    jwtSection.GetValue<string>("Issuer")
    ?? throw new InvalidOperationException(
        "No se encontró Jwt:Issuer. Configure el emisor JWT en appsettings.Local.json.");

string jwtAudience =
    jwtSection.GetValue<string>("Audience")
    ?? throw new InvalidOperationException(
        "No se encontró Jwt:Audience. Configure la audiencia JWT en appsettings.Local.json.");

// -----------------------------------------------------------------------------
// AUTHENTICATION
// -----------------------------------------------------------------------------

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultSignInScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath =
        "/acceso-y-seguridad/login";

    options.AccessDeniedPath =
        "/acceso-y-seguridad/acceso-denegado";

    options.ExpireTimeSpan =
        TimeSpan.FromHours(8);

    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;

            if (principal == null)
            {
                context.RejectPrincipal();
                return;
            }

            string? userIdValue =
                principal.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    userIdValue,
                    out int userId))
            {
                context.RejectPrincipal();
                return;
            }

            string? lastLogoutClaim =
                principal.FindFirst("LastLogoutUtc")?.Value;

            using var scope =
                context.HttpContext.RequestServices.CreateScope();

            var db =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user =
                await db.Usuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.IdUsuario == userId,
                        context.HttpContext.RequestAborted);

            if (user == null)
            {
                context.RejectPrincipal();
                return;
            }

            if (!string.Equals(
                    user.Estado,
                    "activo",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.RejectPrincipal();
                return;
            }

            if (user.UltimoLogout.HasValue &&
                !string.IsNullOrWhiteSpace(lastLogoutClaim))
            {
                if (DateTime.TryParse(
                        lastLogoutClaim,
                        null,
                        DateTimeStyles.RoundtripKind,
                        out var claimLogoutUtc) &&
                    user.UltimoLogout.Value > claimLogoutUtc)
                {
                    context.RejectPrincipal();
                    return;
                }
            }
        }
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token))
            {
                context.Token =
                    context.Request.Cookies["SmileTrack-JWT"];
            }

            return Task.CompletedTask;
        },

        OnTokenValidated = async context =>
        {
            var principal = context.Principal;

            if (principal == null)
            {
                context.Fail("Token inválido.");
                return;
            }

            string? userIdValue =
                principal.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!int.TryParse(
                    userIdValue,
                    out int userId))
            {
                context.Fail("Usuario inválido en token.");
                return;
            }

            using var scope =
                context.HttpContext.RequestServices.CreateScope();

            var db =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var usuario =
                await db.Usuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u => u.IdUsuario == userId,
                        context.HttpContext.RequestAborted);

            if (usuario == null ||
                !string.Equals(
                    usuario.Estado,
                    "activo",
                    StringComparison.OrdinalIgnoreCase))
            {
                context.Fail("Usuario inactivo o inexistente.");
                return;
            }

            string? issuedAtClaim =
                principal.FindFirst(
                    JwtRegisteredClaimNames.Iat)?.Value;

            if (!string.IsNullOrWhiteSpace(issuedAtClaim) &&
                long.TryParse(
                    issuedAtClaim,
                    out long issuedAtSeconds) &&
                usuario.UltimoLogout.HasValue)
            {
                var tokenIssuedUtc =
                    DateTimeOffset
                        .FromUnixTimeSeconds(issuedAtSeconds)
                        .UtcDateTime;

                if (usuario.UltimoLogout.Value > tokenIssuedUtc)
                {
                    context.Fail(
                        "Token revocado por logout.");
                    return;
                }
            }
        }
    };

    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

            ValidateLifetime = true,

            ClockSkew =
                TimeSpan.FromMinutes(2)
        };
});

// -----------------------------------------------------------------------------
// AUTHORIZATION
// -----------------------------------------------------------------------------

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "ApiOrCookie",
        policy =>
        {
            policy.AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme);

            policy.RequireAuthenticatedUser();
        });
});

// -----------------------------------------------------------------------------
// SERVICES
// -----------------------------------------------------------------------------

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IAuthService,
    SmileTrack_MVC.Services.AuthService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IUsuarioAdminService,
    SmileTrack_MVC.Services.UsuarioAdminService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IUsuariosApiService,
    SmileTrack_MVC.Services.UsuariosApiService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IReportesApiService,
    SmileTrack_MVC.Services.ReportesApiService>();

builder.Services.Configure<
    SmileTrack_MVC.Services.Email.EmailServiceOptions>(
        builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<
    SmileTrack_MVC.Services.Email.IEmailService,
    SmileTrack_MVC.Services.Email.EmailService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IProfesionalService,
    SmileTrack_MVC.Services.ProfesionalService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.IProfesionalComisionService,
    SmileTrack_MVC.Services.GestionDeProfesionales.ProfesionalComisionService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.IProfesionalHorarioService,
    SmileTrack_MVC.Services.GestionDeProfesionales.ProfesionalHorarioService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.IProfesionalAusenciaService,
    SmileTrack_MVC.Services.GestionDeProfesionales.ProfesionalAusenciaService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IPacienteService,
    SmileTrack_MVC.Services.PacienteService>();

builder.Services.AddScoped<
    SmileTrack_MVC.Services.IHistoriaClinicaService,
    SmileTrack_MVC.Services.HistoriaClinicaService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.AuditoriaHistoriaClinicaService>();
builder.Services.AddScoped<
    SmileTrack_MVC.Services.PacienteFusionService>();

builder.Services.AddScoped<
    ActualizacionPerfilPacienteService>();


// -----------------------------------------------------------------------------
// URL DE ESCUCHA
// -----------------------------------------------------------------------------

string selectedUrl;

if (ejecutandoEnContenedor)
{
    selectedUrl =
        "http://0.0.0.0:80";

    builder.WebHost.UseUrls(selectedUrl);
}
else
{
    string urlsConfig =
        builder.Configuration.GetValue<string>("Urls")
        ?? "http://localhost:5000";

    string[] configuredUrls =
        urlsConfig.Split(
            new[] { ';', ',' },
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

    string[] candidateUrls =
    {
        "http://localhost:5000",
        "http://localhost:5001",
        "http://127.0.0.1:5000",
        "http://127.0.0.1:5001"
    };

    selectedUrl =
        SelectFirstAvailableUrl(
            configuredUrls
                .Concat(candidateUrls)
                .Distinct()
                .ToArray());

    builder.WebHost.UseUrls(
        selectedUrl);
}

Console.WriteLine(
    $"Usando URL de escucha: {selectedUrl}");

// -----------------------------------------------------------------------------
// BUILD
// -----------------------------------------------------------------------------

var app = builder.Build();

app.Logger.LogInformation(
    "Usando URL de escucha: {Url}",
    selectedUrl);

// -----------------------------------------------------------------------------
// DATABASE STARTUP
// -----------------------------------------------------------------------------

await WaitForSqlServerAsync(
    connectionString,
    app.Logger);

if (app.Environment.IsDevelopment())
{
    await ApplyDevelopmentSchemaMigrationsAsync(
        connectionString,
        app.Environment.ContentRootPath,
        app.Logger);
}

await ValidateDatabaseSchemaAsync(
    connectionString,
    app.Logger);

// -----------------------------------------------------------------------------
// GLOBAL EXCEPTION HANDLING + REQUEST ID
// -----------------------------------------------------------------------------

app.Use(async (context, next) =>
{
    string requestId =
        context.TraceIdentifier;

    if (context.Request.Headers.TryGetValue(
            "X-Request-ID",
            out var incomingId) &&
        !string.IsNullOrWhiteSpace(incomingId))
    {
        string candidate = incomingId.ToString().Trim();
        if (candidate.Length <= 64 &&
            candidate.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_'))
        {
            requestId = candidate;
        }
    }

    context.Response.Headers["X-Request-ID"] =
        requestId;

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger =
            context.RequestServices
                .GetRequiredService<ILogger<Program>>();

        if (ex is OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Solicitud cancelada por el cliente: {Metodo} {Ruta}",
                context.Request.Method,
                context.Request.Path);

            context.Response.StatusCode =
                (int)HttpStatusCode.BadRequest;

            return;
        }

        logger.LogError(
            ex,
            "Excepcion NO MANEJADA en pipeline: {Metodo} {Ruta} - {Mensaje}",
            context.Request.Method,
            context.Request.Path,
            ex.Message);

        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                ex,
                "Ya se habia iniciado la respuesta; no se puede reescribir error para RequestId {RequestId}",
                requestId);

            throw;
        }

        bool esApi =
            context.Request.Path.StartsWithSegments(
                "/api",
                StringComparison.OrdinalIgnoreCase)
            ||
            context.Request.Headers.Accept.ToString()
                .Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase);

        context.Response.Clear();

        context.Response.StatusCode =
            (int)HttpStatusCode.InternalServerError;

        if (esApi)
        {
            context.Response.ContentType =
                "application/problem+json; charset=utf-8";

            var problem = new
            {
                type =
                    "https://tools.ietf.org/html/rfc7231#section-6.6.1",

                title =
                    "Error interno del servidor",

                status = 500,

                detail =
                    app.Environment.IsDevelopment()
                        ? $"Se presento un error inesperado. RequestId: {requestId}. Detalle: {ex.Message}"
                        : $"Se presento un error inesperado. Proporcione este codigo al soporte: {requestId}",

                instance =
                    context.Request.Path.Value,

                requestId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    problem,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy =
                            JsonNamingPolicy.CamelCase
                    }));
        }
        else
        {
            context.Response.ContentType =
                "text/html; charset=utf-8";

            string mensajeHtml = $@"
<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8'>
<title>Error Interno - SmileTrack</title>
<style>
body {{
    font-family:system-ui,Arial,sans-serif;
    background:#f8fafc;
    display:flex;
    justify-content:center;
    align-items:center;
    min-height:100vh;
    margin:0;
    padding:24px;
}}

.card {{
    background:#fff;
    border-radius:12px;
    box-shadow:0 10px 25px rgba(15,23,42,.12);
    max-width:520px;
    padding:32px;
    text-align:center;
}}

.code {{
    background:#fee2e2;
    color:#b91c1c;
    font-family:ui-monospace,Consolas,monospace;
    padding:6px 12px;
    border-radius:6px;
    display:inline-block;
    margin:8px 0 12px;
}}

a {{
    color:#0f766e;
    text-decoration:none;
    font-weight:600;
}}
</style>
</head>

<body>
<div class='card'>

<h2 style='margin:0 0 8px;color:#0f172a;'>
😔 Ocurrió un error inesperado
</h2>

<p style='color:#475569;'>
Nuestro equipo ha registrado el problema y trabajará en resolverlo.
</p>

<p>
Código de referencia:
</p>

<div
    class='code'
    title='Proporcione este código al soporte'>
    {requestId}
</div>

<p>
<a href='javascript:history.back()'>
← Volver a la página anterior
</a>
</p>

</div>
</body>
</html>";

            await context.Response.WriteAsync(
                mensajeHtml);
        }
    }
});

// -----------------------------------------------------------------------------
// MIDDLEWARE
// -----------------------------------------------------------------------------

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && redirectHttpToHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    if (context.Request.Path.StartsWithSegments(
            "/acceso-y-seguridad",
            StringComparison.OrdinalIgnoreCase))
    {
        headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
        headers.Pragma = "no-cache";
    }

    await next();
});

app.Use(async (context, next) =>
{
    string? path =
        context.Request.Path.Value;

    if (!string.IsNullOrWhiteSpace(path) &&
        path.EndsWith(
            ".cshtml",
            StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith(
            "/ViewProxy/Render/",
            StringComparison.OrdinalIgnoreCase))
    {
        string normalized =
            path.Trim('/');

        context.Request.Path =
            $"/ViewProxy/Render/{normalized}";
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments(
            "/uploads/documentos-clinicos",
            StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // CORRECCIÓN yeray — Estos dos scripts cambian con el odontograma de
        // Historia Clínica. Antes todos los estáticos se entregaban por siete
        // días y el navegador ejecutaba un JS anterior que seguía dibujando el
        // canvas 2D. Se evita su caché para que una actualización sea visible
        // inmediatamente después de reiniciar la aplicación.
        var ruta = context.Context.Request.Path.Value ?? string.Empty;
        var esScriptOdontogramaHistoria = ruta.EndsWith(
            "/js/Historia_Clinica/st-odo-03-historial/gestion-historial.js",
            StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith(
                "/js/Historia_Clinica/components/odontograma-3d-readonly.js",
                StringComparison.OrdinalIgnoreCase);

        context.Context.Response.Headers.CacheControl = esScriptOdontogramaHistoria
            ? "no-cache, no-store, must-revalidate"
            : "public,max-age=604800";

        if (esScriptOdontogramaHistoria)
        {
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
        }
    }
});
app.UseRouting();



app.UseRateLimiter();
app.UseAuthentication();

app.UseAuthorization();

// -----------------------------------------------------------------------------
// ROUTING
// -----------------------------------------------------------------------------

app.MapHealthChecks("/health");
app.MapControllers();

app.MapControllerRoute(
    name: "viewproxy",
    pattern: "ViewProxy/Render/{**path}",
    defaults: new
    {
        controller = "ViewProxy",
        action = "Render"
    });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// -----------------------------------------------------------------------------
// DATABASE SCHEMA
// -----------------------------------------------------------------------------

static async Task ValidateDatabaseSchemaAsync(
    string connectionString,
    ILogger logger)
{
    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN
                OBJECT_ID(N'dbo.Usuario', N'U') IS NOT NULL AND
                OBJECT_ID(N'dbo.Profesional', N'U') IS NOT NULL AND
                OBJECT_ID(N'dbo.Diagnostico_CIE10', N'U') IS NOT NULL AND
                OBJECT_ID(N'dbo.RegistroConsentimiento', N'U') IS NOT NULL AND
                OBJECT_ID(N'dbo.Reporte_Generado', N'U') IS NOT NULL AND
                COL_LENGTH(N'dbo.Usuario', N'version_consentimiento') IS NOT NULL AND
                COL_LENGTH(N'dbo.Usuario', N'fecha_consentimiento') IS NOT NULL AND
                COL_LENGTH(N'dbo.Usuario', N'ip_consentimiento') IS NOT NULL AND
                COL_LENGTH(N'dbo.Usuario', N'bloqueado_hasta') IS NOT NULL AND
                EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.Usuario')
                      AND name = N'UX_Usuario_Correo'
                      AND is_unique = 1
                      AND is_disabled = 0
                ) AND
                EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.Paciente')
                      AND name = N'UX_Paciente_TipoDocumento_Documento'
                      AND is_unique = 1
                      AND is_disabled = 0
                )
            THEN 1 ELSE 0 END;
            """;

        var schemaReady = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (schemaReady != 1)
        {
            logger.LogCritical("La base de datos no contiene el esquema requerido de SmileTrack.");
            throw new InvalidOperationException(
                "El esquema de la base de datos está incompleto. Ejecute el script de inicialización y las migraciones SQL pendientes, incluida Database/upgrade-auth-identifiers.sql, antes de iniciar la aplicación.");
        }

        logger.LogInformation("El esquema requerido de SmileTrack está disponible.");
    }
    catch (SqlException ex)
    {
        logger.LogCritical(
            ex,
            "No se pudo validar el esquema de SmileTrack.");

        throw new InvalidOperationException(
            "No se pudo validar la base de datos de SmileTrack. " +
            "Verifique que SQL Server esté disponible y que el script de inicialización haya terminado.",
            ex);
    }
}

static async Task ApplyDevelopmentSchemaMigrationsAsync(
    string connectionString,
    string contentRootPath,
    ILogger logger)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var schemaCommand = connection.CreateCommand();
    schemaCommand.CommandText = """
        SELECT CASE
            WHEN OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
              OR OBJECT_ID(N'dbo.Profesional', N'U') IS NULL
              OR OBJECT_ID(N'dbo.Diagnostico_CIE10', N'U') IS NULL
              OR OBJECT_ID(N'dbo.Paciente', N'U') IS NULL
                THEN -1
            WHEN OBJECT_ID(N'dbo.RegistroConsentimiento', N'U') IS NOT NULL
             AND OBJECT_ID(N'dbo.Reporte_Generado', N'U') IS NOT NULL
             AND COL_LENGTH(N'dbo.Usuario', N'version_consentimiento') IS NOT NULL
             AND COL_LENGTH(N'dbo.Usuario', N'fecha_consentimiento') IS NOT NULL
             AND COL_LENGTH(N'dbo.Usuario', N'ip_consentimiento') IS NOT NULL
             AND COL_LENGTH(N'dbo.Usuario', N'bloqueado_hasta') IS NOT NULL
             AND EXISTS (
                 SELECT 1
                 FROM sys.indexes
                 WHERE object_id = OBJECT_ID(N'dbo.Usuario')
                   AND name = N'UX_Usuario_Correo'
                   AND is_unique = 1
                   AND is_disabled = 0
             )
             AND EXISTS (
                 SELECT 1
                 FROM sys.indexes
                 WHERE object_id = OBJECT_ID(N'dbo.Paciente')
                   AND name = N'UX_Paciente_TipoDocumento_Documento'
                   AND is_unique = 1
                   AND is_disabled = 0
             )
                THEN 1
            ELSE 0
        END;
        """;

    var schemaState = Convert.ToInt32(await schemaCommand.ExecuteScalarAsync());
    if (schemaState == -1)
    {
        logger.LogInformation(
            "Se omiten las migraciones automáticas: inicialice primero el esquema base de SmileTrack.");
        return;
    }

    string[] migrationFiles = schemaState == 1
        ? ["MIGRATION_BRECHAS_PROFESIONALES_CITAS.sql"]
        :
        [
            "MIGRATION_Consentimiento.sql",
            "MIGRATION_Reporte_Generado.sql",
            "upgrade-auth-identifiers.sql",
            "MIGRATION_BRECHAS_PROFESIONALES_CITAS.sql"
        ];

    foreach (string migrationFile in migrationFiles)
    {
        string migrationPath = Path.Combine(
            contentRootPath,
            "Database",
            migrationFile);
        string migrationSql = await File.ReadAllTextAsync(migrationPath);
        // Keep migrations scoped to the database selected by the app connection string.
        migrationSql = Regex.Replace(
            migrationSql,
            @"(?im)^[ \t]*USE[ \t]+(?:\[[^\]]+\]|[A-Za-z0-9_]+)[ \t]*;[ \t]*\r?$",
            string.Empty);
        string[] batches = Regex.Split(
            migrationSql,
            @"(?im)^\s*GO\s*$");

        foreach (string batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var migrationCommand = connection.CreateCommand();
            migrationCommand.CommandTimeout = 60;
            migrationCommand.CommandText = batch;

            try
            {
                await migrationCommand.ExecuteNonQueryAsync();
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException(
                    $"Falló la migración de desarrollo {migrationFile}.",
                    ex);
            }
        }

        logger.LogInformation(
            "Migración de desarrollo aplicada: {MigrationFile}",
            migrationFile);
    }
}

// -----------------------------------------------------------------------------
// URL HELPERS
// -----------------------------------------------------------------------------

static string SelectFirstAvailableUrl(
    string[] urls)
{
    foreach (string url in urls)
    {
        if (Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            if (IsPortAvailable(
                    uri.Host,
                    uri.Port))
            {
                return url;
            }
        }
    }

    return urls[0];
}

static bool IsPortAvailable(
    string host,
    int port)
{
    try
    {
        var ipAddress =
            IPAddress.Loopback;

        if (!string.IsNullOrWhiteSpace(host) &&
            host != "localhost" &&
            host != "127.0.0.1" &&
            host != "::1")
        {
            ipAddress =
                Dns.GetHostAddresses(host)
                    .FirstOrDefault(
                        a => a.AddressFamily ==
                             AddressFamily.InterNetwork)
                ?? IPAddress.Loopback;
        }

        var listener =
            new TcpListener(
                ipAddress,
                port);

        listener.Start();

        listener.Stop();

        return true;
    }
    catch
    {
        return false;
    }
}

// -----------------------------------------------------------------------------
// SQL SERVER WAIT
// -----------------------------------------------------------------------------

static async Task WaitForSqlServerAsync(
    string connectionString,
    ILogger logger)
{
    const int maxAttempts = 12;

    var delay =
        TimeSpan.FromSeconds(2);

    for (
        int attempt = 1;
        attempt <= maxAttempts;
        attempt++)
    {
        try
        {
            logger.LogInformation(
                "🔌 Probando conexión a SQL Server (intento {Intento}/{MaxIntentos})...",
                attempt,
                maxAttempts);

            await using var connection =
                new SqlConnection(
                    connectionString);

            await connection.OpenAsync();

            logger.LogInformation(
                "✅ Conexión a SQL Server establecida.");

            return;
        }
        catch (Exception ex)
            when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "No se pudo conectar a SQL Server en el intento {Intento}. Reintentando en {Retraso}s...",
                attempt,
                delay.TotalSeconds);

            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No se pudo conectar a SQL Server después de {Intentos} intentos.",
                maxAttempts);

            throw;
        }
    }
}

app.Run();
