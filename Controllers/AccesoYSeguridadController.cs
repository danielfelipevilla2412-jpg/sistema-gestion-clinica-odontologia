using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;

namespace SmileTrack_MVC.Controllers;

public class AccesoYSeguridadController(AppDbContext context, IAuthService authService, IAntiforgery antiforgery, IEmailService emailService) : Controller
{
    private readonly AppDbContext _context = context;
    private readonly IAuthService _authService = authService;
    private readonly IAntiforgery _antiforgery = antiforgery;
    private readonly IEmailService _emailService = emailService;

    [HttpGet]
    [Route("acceso-y-seguridad/login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // Limpiar cualquier estado de autenticación previo antes de mostrar la página de login.
        // Esto evita que cookies de sesión antiguas puedan contaminar la siguiente petición POST,
        // especialmente en contextos donde el cliente mantiene un .AspNetCore.Cookies / SmileTrack-JWT
        // viejo que podría provocar rechazos de solicitud por encabezados demasiado grandes o
        // sesiones inconsistentes.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("SmileTrack-JWT");

        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginByIp")]
    [Route("acceso-y-seguridad/login")]
    public async Task<IActionResult> LoginPost(string email, string password, string? rol, string? returnUrl)
    {
        static bool IsLocalUrl(string? url) => !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//");

        var redirecciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Administrador", "/gestion-de-citas/st-adm-01-dashboard" },
            { "Profesional", "/gestion-de-profesionales/st-odo-01-dashboard" },
            { "Recepcionista", "/gestion-de-citas/st-rec-01-dashboard" },
            { "Auxiliar", "/gestion-de-citas/st-aux-01-panel-operativo/panel-operativo" },
            { "Paciente", "/gestion-de-citas/st-pac-01-mis-citas" },
        };

        static string NormalizarRol(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

            return valor.Trim().ToLowerInvariant() switch
            {
                "administrador" or "admin" => "Administrador",
                "profesional" or "odontologo" or "doctor" => "Profesional",
                "recepcionista" => "Recepcionista",
                "auxiliar" => "Auxiliar",
                "paciente" => "Paciente",
                _ => valor.Trim()
            };
        }

        ViewData["ReturnUrl"] = returnUrl;
        string accept = HttpContext.Request.Headers["Accept"].ToString();
        bool wantsJson = accept?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError("", "Correo y contraseña son obligatorios.");
            if (wantsJson)
            {
                return BadRequest(new { success = false, message = "Correo y contraseña son obligatorios." });
            }
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        string correo = email.Trim();
        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo == correo);

        if (usuario == null)
        {
            if (wantsJson)
            {
                return Unauthorized(new { success = false, message = "Correo o contraseña incorrectos." });
            }
            ModelState.AddModelError("", "Correo o contraseña incorrectos.");
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        if (usuario.Rol == null && usuario.IdRol > 0)
        {
            var rolDb = await _context.Roles.FirstOrDefaultAsync(r => r.IdRol == usuario.IdRol);
            if (rolDb == null)
            {
                if (wantsJson)
                {
                    return BadRequest(new { success = false, message = "El usuario no tiene un rol asignado o el rol existe en la base de datos." });
                }

                ModelState.AddModelError("", "El usuario no tiene un rol válido asignado.");
                return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
            }

            usuario.Rol = rolDb;
        }

        if (usuario.Rol == null)
        {
            if (wantsJson)
            {
                return BadRequest(new { success = false, message = "El usuario no tiene un rol asignado o el rol existe en la base de datos." });
            }
            ModelState.AddModelError("", "El usuario no tiene un rol válido asignado.");
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        // El rol siempre se toma de la base de datos (fuente de verdad), nunca del botón que el
        // usuario haya presionado en pantalla. Si el usuario selecciona un rol distinto en el
        // formulario, se IGNORA la selección de la UI, se usa el rol real asignado en BD y se
        // le deja ingresar sin bloqueos. Así, incluso sin hacer clic en el selector de roles,
        // el login funciona automáticamente.
        string rolSeleccionado = NormalizarRol(rol);
        string rolNombre = NormalizarRol(usuario.Rol?.NombreRol);

        if (!redirecciones.TryGetValue(rolNombre, out string? redirectUrl))
        {
            if (wantsJson)
            {
                return BadRequest(new { success = false, message = "El usuario no tiene un rol válido asignado." });
            }
            ModelState.AddModelError("", "El usuario no tiene un rol válido asignado.");
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        if (!string.IsNullOrWhiteSpace(rolSeleccionado) &&
            !string.Equals(rolSeleccionado, rolNombre, StringComparison.OrdinalIgnoreCase))
        {
            TempData["InfoMessage"] = $"Se detectó que seleccionaste el rol '{rolSeleccionado}', pero tu cuenta corresponde a '{rolNombre}'. Has iniciado sesión con tu rol real.";
        }

        // La contraseña, el estado de la cuenta (activo/inactivo) y el registro de auditoría del
        // intento de inicio de sesión quedan a cargo de AuthService, que ya centraliza ese manejo
        // (incluye manejo de errores de BD y logging) en vez de duplicarlo aquí.
        var authResult = await _authService.LoginAsync(new LoginRequest
        {
            Correo = correo,
            Contrasena = password,
            Rol = rolNombre,
        });

        if (!authResult.Success)
        {
            int status = authResult.Message.Contains("bloqueada", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;

            if (wantsJson)
            {
                return StatusCode(status, new { success = false, message = authResult.Message });
            }

            Response.StatusCode = status;
            ViewData["ErrorStatus"] = status;
            ModelState.AddModelError("", authResult.Message);
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, $"{usuario.Nombre} {usuario.Apellidos}"),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, rolNombre),
            new("SessionIssuedUtc", DateTime.UtcNow.ToString("O")),
            new("LastLogoutUtc", usuario.UltimoLogout?.ToString("O") ?? DateTime.MinValue.ToString("O")),
        };

        // Se agrega el id de la ficha de Paciente/Profesional vinculada al usuario como claim,
        // para que quede disponible en toda la sesión (Views, Controllers) sin tener que volver
        // a buscarla por correo en cada pantalla. Esto evita que se "pierda" esa información al
        // navegar entre las distintas vistas de un mismo rol.
        if (string.Equals(rolNombre, "Paciente", StringComparison.OrdinalIgnoreCase))
        {
            int? idPaciente = await _context.Pacientes
                .Where(p => p.IdUsuario == usuario.IdUsuario)
                .Select(p => (int?)p.IdPaciente)
                .FirstOrDefaultAsync();

            if (idPaciente.HasValue)
            {
                claims.Add(new Claim("IdPaciente", idPaciente.Value.ToString()));
            }
        }
        else if (string.Equals(rolNombre, "Profesional", StringComparison.OrdinalIgnoreCase))
        {
            int? idProfesional = await _context.Profesionales
                .Where(p => p.IdUsuario == usuario.IdUsuario)
                .Select(p => (int?)p.IdProfesional)
                .FirstOrDefaultAsync();

            if (idProfesional.HasValue)
            {
                claims.Add(new Claim("IdProfesional", idProfesional.Value.ToString()));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });



        // Guardar el JWT en cookie httpOnly separada cuando exista.
        if (!string.IsNullOrWhiteSpace(authResult.Token))
        {
            Response.Cookies.Append("SmileTrack-JWT", authResult.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddMinutes(60)
            });
        }

        string destino;
        string rr = returnUrl ?? string.Empty;
        if (IsLocalUrl(rr) && !rr.StartsWith("/acceso-y-seguridad/login", StringComparison.OrdinalIgnoreCase))
        {
            destino = rr;
        }
        else
        {
            destino = redirectUrl;
        }

        if (wantsJson)
        {
            return Json(new { success = true, token = authResult.Token, redirectUrl = destino });
        }

        return LocalRedirect(destino);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("acceso-y-seguridad/logout")]
    public async Task<IActionResult> Logout()
    {
        string? userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdString, out int idUsuario))
        {
            var usuario = await _context.Usuarios.FindAsync(idUsuario);
            if (usuario != null)
            {
                usuario.UltimoLogout = DateTime.UtcNow;
                _context.Usuarios.Update(usuario);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // No bloquear la salida si falla el registro de logout.
                    var logger = HttpContext.RequestServices.GetService<ILogger<AccesoYSeguridadController>>();
                    logger?.LogWarning(ex, "No se pudo registrar UltimoLogout al cerrar sesión para IdUsuario={IdUsuario}", idUsuario);
                }
            }
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("SmileTrack-JWT");
        return Redirect("/acceso-y-seguridad/login");
    }

    [HttpGet]
    [Route("acceso-y-seguridad/recover")]
    public IActionResult Recover() => View("~/Views/Acceso_Y_Seguridad/recover/index.cshtml");

    [HttpPost]
    [Route("acceso-y-seguridad/recover/send-code")]
    public async Task<IActionResult> RecoverSendCode([FromBody] Models.ViewModels.RecoverPasswordRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            return BadRequest(new { Success = false, Message = "Antiforgery validation failed.", Details = ex.Message });
        }

        if (request == null)
        {
            return BadRequest(new { Success = false, Message = "El body es obligatorio." });
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
            return BadRequest(new { Success = false, Message = "Model validation failed.", Errors = errors });
        }

        var result = await _authService.RecoverPasswordAsync(request);
        return Ok(new { result.Success, result.Message });
    }

    [HttpPost]
    [Route("acceso-y-seguridad/recover/verify-code")]
    public async Task<IActionResult> RecoverVerifyCode([FromBody] VerifyRecoveryCodeRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            return BadRequest(new { Success = false, Message = "Antiforgery validation failed.", Details = ex.Message });
        }

        if (request == null)
        {
            return BadRequest(new { Success = false, Message = "El body es obligatorio." });
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
            return BadRequest(new { Success = false, Message = "Model validation failed.", Errors = errors });
        }

        var result = await _authService.VerifyRecoveryCodeAsync(request);
        return Ok(new { result.Success, result.Message, result.RecoveryToken });
    }

    [HttpPost]
    [Route("acceso-y-seguridad/recover/reset-password")]
    public async Task<IActionResult> RecoverResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            return BadRequest(new { Success = false, Message = "Antiforgery validation failed.", Details = ex.Message });
        }

        if (request == null)
        {
            return BadRequest(new { Success = false, Message = "El body es obligatorio." });
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());
            return BadRequest(new { Success = false, Message = "Model validation failed.", Errors = errors });
        }

        string tokenTemporal = request.TokenTemporal ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tokenTemporal) && !string.IsNullOrWhiteSpace(request.Correo) && !string.IsNullOrWhiteSpace(request.Codigo))
        {
            var verificationResult = await _authService.VerifyRecoveryCodeAsync(new VerifyRecoveryCodeRequest
            {
                Correo = request.Correo,
                Codigo = request.Codigo
            });

            if (!verificationResult.Success)
            {
                return Ok(new { verificationResult.Success, verificationResult.Message });
            }

            tokenTemporal = verificationResult.RecoveryToken ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(tokenTemporal))
        {
            return BadRequest(new { Success = false, Message = "El token temporal es obligatorio." });
        }

        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            TokenTemporal = tokenTemporal,
            NuevaContrasena = request.NuevaContrasena,
            ConfirmarContrasena = request.ConfirmarContrasena
        });

        return Ok(new { result.Success, result.Message });
    }

    [HttpGet]
    [Authorize]
    [Route("acceso-y-seguridad/cambiar-contrasena")]
    public IActionResult ChangePassword() => View("~/Views/Acceso_Y_Seguridad/change-password/index.cshtml");

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("acceso-y-seguridad/cambiar-contrasena")]
    public async Task<IActionResult> ChangePasswordPost(
    string contrasenaActual,
    string nuevaContrasena,
    string confirmarContrasena)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login");
        }

        var response = await _authService.ChangePasswordAsync(
            new ChangePasswordRequest
            {
                ContrasenaActual = contrasenaActual,
                NuevaContrasena = nuevaContrasena,
                ConfirmarContrasena = confirmarContrasena
            });

        if (!response.Success)
        {
            ModelState.AddModelError(string.Empty, response.Message);
            return View("~/Views/Acceso_Y_Seguridad/change-password/index.cshtml");
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = response.Message;
        return RedirectToAction("Login");
    }

    [HttpGet]
    [Route("acceso-y-seguridad/register")]
    public IActionResult Register() => View("~/Views/Acceso_Y_Seguridad/register/index.cshtml");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("acceso-y-seguridad/register")]
    public async Task<IActionResult> RegisterPost([FromForm] RegisterRequest request)
    {
        string accept = HttpContext.Request.Headers["Accept"].ToString();
        bool wantsJson = accept?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

        if (!ModelState.IsValid)
        {
            string errorMessage = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Completa todos los campos requeridos.";

            if (wantsJson)
            {
                return BadRequest(new { success = false, message = errorMessage });
            }

            ModelState.AddModelError(string.Empty, errorMessage);
            return View("~/Views/Acceso_Y_Seguridad/register/index.cshtml");
        }

        // Validación adicional: confirmar que las contraseñas coinciden en servidor
        if (!string.IsNullOrWhiteSpace(request.ConfirmarContrasena) && request.Contrasena != request.ConfirmarContrasena)
        {
            ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
            if (wantsJson) return BadRequest(new { success = false, message = "Las contraseñas no coinciden." });
            return View("~/Views/Acceso_Y_Seguridad/register/index.cshtml");
        }

        var response = await _authService.RegisterAsync(request);

        if (wantsJson)
        {
            return Json(response);
        }

        if (response.Success)
        {
            return Redirect("/acceso-y-seguridad/login");
        }

        ModelState.AddModelError(string.Empty, response.Message);
        return View("~/Views/Acceso_Y_Seguridad/register/index.cshtml");
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-02-gestion-usuarios")]
    public async Task<IActionResult> Stadm02GestionUsuarios()
    {
        var usuariosDb = await _context.Usuarios.Include(u => u.Rol).ToListAsync();
        var rolesColor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Administrador", "purple" },
            { "Recepcionista", "orange" },
            { "Profesional", "green" },
            { "Auxiliar", "pink" },
            { "Paciente", "blue" }
        };

        var usuarios = usuariosDb.Select(u => new
        {
            id = u.IdUsuario,
            name = $"{u.Nombre} {u.Apellidos}".Trim(),
            initials = $"{u.Nombre?.Trim().FirstOrDefault()}{u.Apellidos?.Trim().FirstOrDefault()}".ToUpper(),
            email = u.Correo,
            role = u.Rol?.NombreRol ?? "Sin Rol",
            status = string.IsNullOrWhiteSpace(u.Estado) ? "Activo" : char.ToUpper(u.Estado[0]) + u.Estado[1..],
            lastAccess = u.UltimoLogin,
            color = u.Rol != null && rolesColor.TryGetValue(u.Rol.NombreRol, out string? col) ? col : "blue"
        }).ToList();

        ViewData["UsuariosJson"] = System.Text.Json.JsonSerializer.Serialize(usuarios);
        return View("~/Views/Acceso_Y_Seguridad/st-adm-02-gestion-usuarios/index.cshtml", usuarios);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-03-gestion-roles")]
    public async Task<IActionResult> Stadm03GestionRoles()
    {
        var rolesDb = await _context.Roles.ToListAsync();
        ViewData["RolesJson"] = System.Text.Json.JsonSerializer.Serialize(rolesDb);
        return View("~/Views/Acceso_Y_Seguridad/st-adm-03-gestion-roles/index.cshtml", rolesDb);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-04-matriz-permisos")]
    public IActionResult Stadm04MatrizPermisos() => Redirect("/acceso-y-seguridad/st-adm-03-gestion-roles");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-15-bitacora")]
    public async Task<IActionResult> Stadm15Bitacora()
    {
        var bitacoraDb = await _context.Auditorias
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.Fecha)
            .Take(100)
            .Select(a => new
            {
                id = a.IdAuditoria,
                usuario = a.Usuario != null ? $"{a.Usuario.Nombre} {a.Usuario.Apellidos}" : "Sistema",
                accion = a.Accion,
                tabla = a.TablaAfectada,
                descripcion = a.Descripcion ?? $"Registro en {a.TablaAfectada}",
                ip = a.IpOrigen ?? "127.0.0.1",
                fecha = a.Fecha
            })
            .ToListAsync();

        ViewData["BitacoraJson"] = System.Text.Json.JsonSerializer.Serialize(bitacoraDb);
        return View("~/Views/Acceso_Y_Seguridad/st-adm-15-bitacora/index.cshtml", bitacoraDb);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-16-test-smtp")]
    public IActionResult Stadm16TestSmtp()
    {
        ViewData["SmtpConfigOk"] = true;
        return Content(@$"
<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='utf-8' />
<title>SmileTrack — Panel de Diagnóstico SMTP</title>
<style>
body{{font-family:Arial;margin:0;padding:32px;background:#f3f4f6;color:#111827}}
.container{{max-width:960px;margin:0 auto;background:#fff;border:1px solid #e5e7eb;border-radius:14px;padding:28px;box-shadow:0 1px 3px rgba(0,0,0,.05)}}
h1{{margin:0 0 8px 0;color:#0f766e;font-size:24px}}
.subtitle{{margin:0 0 24px 0;color:#6b7280}}
.form-row{{display:flex;gap:12px;margin-bottom:20px;flex-wrap:wrap;align-items:end}}
label{{display:flex;flex-direction:column;gap:6px;font-size:14px;font-weight:600;color:#374151}}
input[type=email]{{padding:10px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;min-width:320px}}
button{{padding:10px 18px;background:#0f766e;color:#fff;border:0;border-radius:8px;font-weight:600;cursor:pointer;font-size:14px}}
button:hover{{background:#0d6660}}
button:disabled{{background:#94a3b8;cursor:not-allowed}}
.result{{margin-top:16px;padding:18px;border-radius:10px;font-size:13px;line-height:1.6;white-space:pre-wrap;font-family:Consolas,'Courier New',monospace;max-height:60vh;overflow-y:auto;word-break:break-word}}
.result.ok{{background:#ecfdf5;border:1px solid #6ee7b7;color:#065f46}}
.result.err{{background:#fef2f2;border:1px solid #fca5a5;color:#991b1b}}
.result.loading{{background:#eff6ff;border:1px solid #93c5fd;color:#1e40af}}
.back-link{{display:inline-block;margin-top:20px;color:#2563eb;text-decoration:none;font-size:14px}}
.back-link:hover{{text-decoration:underline}}
.checklist{{background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:18px;margin-bottom:20px}}
.checklist h3{{margin:0 0 10px 0;color:#1f2937;font-size:15px}}
.checklist ul{{margin:0;padding-left:20px}}
.checklist li{{margin:4px 0;font-size:13px;color:#334155}}
.checklist code{{background:#e2e8f0;padding:2px 6px;border-radius:4px;font-size:12px;color:#0f172a}}
</style>
</head>
<body>
<div class='container'>
  <h1>🧪 Diagnóstico SMTP — SmileTrack</h1>
  <p class='subtitle'>Herramienta exclusiva para administradores. Ejecuta los 4 pasos del envío (configurar → conectar → autenticar → enviar) y devuelve un diagnóstico detallado por paso.</p>

  <div class='checklist'>
    <h3>✅ Antes de ejecutar el test, verifica:</h3>
    <ul>
      <li>El archivo <code>appsettings.Local.json</code> tiene <code>Smtp.Password</code> NO VACÍO.</li>
      <li>Si usas Gmail: la contraseña debe ser una <strong>App Password de 16 caracteres</strong> (4 grupos de 4 separados por espacios). La contraseña normal NUNCA funciona.</li>
      <li>La cuenta Gmail tiene activada la <strong>Verificación en 2 pasos</strong>.</li>
      <li>Puerto recomendado: <code>587</code> (STARTTLS). Alternativa <code>465</code> (SSL). Prueba AMBOS si uno falla.</li>
      <li>Windows Firewall / antivirus / VPN NO bloqueen el puerto saliente.</li>
    </ul>
  </div>

  <div class='form-row'>
    <label>
      Correo de destino para la prueba
      <input type='email' id='destino' placeholder='tucorreo@ejemplo.com' />
    </label>
    <button id='btnTest' onclick='ejecutarPrueba()'>▶ Ejecutar prueba SMTP</button>
  </div>

  <div id='resultado' class='result loading' style='display:none;'></div>
  <a href='/acceso-y-seguridad/st-adm-01-dashboard' class='back-link'>← Volver al Dashboard</a>
</div>

<script>
async function ejecutarPrueba() {{
    const btn = document.getElementById('btnTest');
    const destino = document.getElementById('destino').value.trim();
    const out = document.getElementById('resultado');
    btn.disabled = true;
    out.style.display = 'block';
    out.className = 'result loading';
    out.textContent = '⏳ Ejecutando diagnóstico SMTP (4 pasos)...';

    try {{
        const url = '/acceso-y-seguridad/st-adm-16-test-smtp/run?destino=' + encodeURIComponent(destino || '');
        const r = await fetch(url, {{ method:'GET', credentials:'same-origin' }});
        const json = await r.json();
        out.className = 'result ' + (json.exito ? 'ok' : 'err');
        out.textContent = json.detalle || '(sin detalle)';
    }} catch (e) {{
        out.className = 'result err';
        out.textContent = '❌ Error del lado del cliente: ' + e.message + '\\n\\n' +
            'Revisa la consola (F12) para más detalles. Verifica que: 1) estés logueado como Administrador, 2) el servidor esté corriendo.';
    }} finally {{
        btn.disabled = false;
    }}
}}
</script>
</body>
</html>", "text/html; charset=utf-8");
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-16-test-smtp/run")]
    public async Task<IActionResult> Stadm16RunTestSmtp([FromQuery] string? destino, CancellationToken ct = default)
    {
        try
        {
            var (exito, detalle) = await _emailService.ProbarConfiguracionSmtpAsync(destino ?? string.Empty, ct);
            return Ok(new { exito, detalle });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                exito = false,
                detalle = "EXCEPTION NO MANEJADA EN EL TEST:\n" +
                          $"Tipo: {ex.GetType().FullName}\n" +
                          $"Mensaje: {ex.Message}\n" +
                          (ex.InnerException != null ? $"Inner: {ex.InnerException.Message}\n" : string.Empty) +
                          $"StackTrace:\n{ex.StackTrace}"
            });
        }
    }
}

