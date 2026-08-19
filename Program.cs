using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;
using System.Security.Claims;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// NOTA sobre orden de configuración: CADA NUEVA FUENTE AGREGADA SOBRESCRIBE A LAS ANTERIORES.
// WebApplication.CreateBuilder(args) YA registra automáticamente:
//   1) appsettings.json
//   2) appsettings.{Environment}.json
//   3) User Secrets (SI environment = Development)
//   4) Variables de entorno
//   5) Argumentos de línea de comandos
//
// Orden que queremos para evitar perder el Password de user-secrets:
//   (automáticos por el builder)
//   + appsettings.Local.json   ← valores locales específicos de la máquina
//   + user-secrets (explicito, por si el builder no lo cargó)
//   + variables de entorno y CLI
//
// Así: Smtp:Password de user-secrets no será sobreescrito por un "" del json.

builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.json",
        optional: true,
        reloadOnChange: true)
    .AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(
        "ff5d4b9c-690c-44b5-a2e6-8cd9fe7ea759",
        reloadOnChange: true);
}

builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddControllersWithViews();

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

builder.Services.AddRateLimiter(options =>
{
    // Política por IP: 10 peticiones / 15 minutos
    options.AddFixedWindowLimiter("LoginByIp", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    // Política por correo: 5 peticiones / 15 minutos
    options.AddFixedWindowLimiter("LoginByEmail", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;

        context.HttpContext.Response.ContentType =
            "application/json; charset=utf-8";

        bool esApi = context.HttpContext.Request.Headers["Accept"]
            .ToString()
            .Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase);

        if (esApi)
        {
            await context.HttpContext.Response.WriteAsync(
                """{"success":false,"message":"Demasiados intentos de inicio de sesión. Por favor espera 15 minutos antes de intentar nuevamente."}""",
                ct);
        }
        else
        {
            context.HttpContext.Response.ContentType =
                "text/html; charset=utf-8";

            await context.HttpContext.Response.WriteAsync(
                """<meta http-equiv="refresh" content="2;url=/acceso-y-seguridad/login?rateLimited=1" /><p>Demasiados intentos. Redirigiendo...</p>""",
                ct);
        }
    };
});

// -----------------------------------------------------------------------------
// ANTIFORGERY
// -----------------------------------------------------------------------------

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorNumbersToAdd: null)));

// -----------------------------------------------------------------------------
// JWT
// -----------------------------------------------------------------------------

var jwtSection =
    builder.Configuration.GetSection("Jwt");

string jwtKey =
    jwtSection.GetValue<string>("Key")
    ?? throw new InvalidOperationException(
        "No se encontró Jwt:Key. Configure la clave JWT en appsettings.Local.json.");

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
        "/acceso-y-seguridad/login";

    options.ExpireTimeSpan =
        TimeSpan.FromHours(8);

    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
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

builder.Services.Configure<
    SmileTrack_MVC.Services.Email.EmailServiceOptions>(
        builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<
    SmileTrack_MVC.Services.Email.IEmailService,
    SmileTrack_MVC.Services.Email.EmailService>();

// -----------------------------------------------------------------------------
// URL DE ESCUCHA
// -----------------------------------------------------------------------------

string selectedUrl;

if (ejecutandoEnContenedor)
{
    selectedUrl =
        "http://0.0.0.0:80";

    builder.WebHost.UseUrls(
        selectedUrl);
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

await EnsureDatabaseExistsAsync(
    connectionString,
    app.Logger);

await WaitForSqlServerAsync(
    connectionString,
    app.Logger);

await EnsureDatabaseSchemaAsync(
    app.Services,
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
        requestId =
            incomingId.ToString().Trim();
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

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

app.UseStaticFiles();
app.UseRouting();



app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

// -----------------------------------------------------------------------------
// ROUTING
// -----------------------------------------------------------------------------

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

static async Task EnsureDatabaseSchemaAsync(
    IServiceProvider services,
    ILogger logger)
{
    using var scope = services.CreateScope();

    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    string scriptPath =
        Path.Combine(
            Directory.GetCurrentDirectory(),
            "Database",
            "SCRIPT_SQL_UNICO_SMILETRACK.sql");

    try
    {
        if (!File.Exists(scriptPath))
        {
            logger.LogCritical(
                "❌ No se encontró el script SQL obligatorio en {ScriptPath}. " +
                "La aplicación no puede iniciar sin el esquema oficial de SmileTrack.",
                scriptPath);

            throw new FileNotFoundException(
                "No se encontró SCRIPT_SQL_UNICO_SMILETRACK.sql.",
                scriptPath);
        }

        string sql =
            await File.ReadAllTextAsync(scriptPath);

        string[] batches =
            Regex.Split(
                sql,
                @"^\s*GO\s*$",
                RegexOptions.Multiline |
                RegexOptions.IgnoreCase);

        foreach (string batch in batches)
        {
            string trimmed = batch.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            await db.Database.ExecuteSqlRawAsync(trimmed);
        }

        logger.LogInformation(
            "✅ Script unificado ejecutado correctamente desde {ScriptPath}",
            scriptPath);
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            "❌ Error crítico durante la inicialización del esquema de SmileTrack. " +
            "La aplicación no puede continuar porque la base de datos puede haber quedado incompleta.");

        throw new InvalidOperationException(
            "La inicialización de la base de datos de SmileTrack falló. " +
            "Revise SCRIPT_SQL_UNICO_SMILETRACK.sql y los errores anteriores.",
            ex);
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
// DATABASE CREATION
// -----------------------------------------------------------------------------

static async Task EnsureDatabaseExistsAsync(
    string connectionString,
    ILogger logger)
{
    const int maxAttempts = 12;

    var delay =
        TimeSpan.FromSeconds(2);

    var targetBuilder =
        new SqlConnectionStringBuilder(
            connectionString);

    string databaseName =
        targetBuilder.InitialCatalog;

    // Nos conectamos a master porque SmileTrackDB podría todavía no existir.
    targetBuilder.InitialCatalog =
        "master";

    string masterConnectionString =
        targetBuilder.ConnectionString;

    for (
        int attempt = 1;
        attempt <= maxAttempts;
        attempt++)
    {
        try
        {
            await using var connection =
                new SqlConnection(
                    masterConnectionString);

            await connection.OpenAsync();

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                IF DB_ID(@databaseName) IS NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX);

                    SET @sql =
                        N'CREATE DATABASE ' +
                        QUOTENAME(@databaseName);

                    EXEC sp_executesql @sql;
                END
                """;

            command.Parameters.AddWithValue(
                "@databaseName",
                databaseName);

            await command.ExecuteNonQueryAsync();

            logger.LogInformation(
                "✅ Base de datos {DatabaseName} verificada/creada correctamente.",
                databaseName);

            return;
        }
        catch (Exception ex)
            when (attempt < maxAttempts)
        {
            logger.LogWarning(
                ex,
                "No se pudo verificar/crear la base de datos en el intento {Intento}/{MaxIntentos}. Reintentando en {Retraso}s...",
                attempt,
                maxAttempts,
                delay.TotalSeconds);

            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No se pudo verificar/crear la base de datos {DatabaseName} después de {Intentos} intentos.",
                databaseName,
                maxAttempts);

            throw;
        }
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