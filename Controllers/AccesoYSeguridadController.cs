using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
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
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginByIp")]
    [Route("acceso-y-seguridad/login")]
    public async Task<IActionResult> LoginPost(
        string? email,
        string? password,
        string? rol,
        string? returnUrl,
        bool remember = false)
    {
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
                "profesional" or "odontologo" or "odontólogo" or "doctor" => "Profesional",
                "recepcionista" => "Recepcionista",
                "auxiliar" => "Auxiliar",
                "paciente" => "Paciente",
                _ => valor.Trim()
            };
        }

        ViewData["ReturnUrl"] = returnUrl;
        string rolSeleccionado = NormalizarRol(rol);
        ViewData["RolSeleccionado"] = rolSeleccionado;
        string accept = HttpContext.Request.Headers["Accept"].ToString();
        bool wantsJson = accept?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

        if (!redirecciones.ContainsKey(rolSeleccionado))
        {
            const string mensajeRolInvalido = "Selecciona un rol válido.";
            ModelState.AddModelError("rol", mensajeRolInvalido);
            if (wantsJson)
            {
                return BadRequest(new { success = false, message = mensajeRolInvalido });
            }

            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        string correo = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(correo) ||
            string.IsNullOrWhiteSpace(password) ||
            correo.Length > 150 ||
            password.Length > 100 ||
            !new EmailAddressAttribute().IsValid(correo))
        {
            ModelState.AddModelError("", "Verifica el correo y la contraseña.");
            if (wantsJson)
            {
                return BadRequest(new { success = false, message = "Verifica el correo y la contraseña." });
            }
            return View("~/Views/Acceso_Y_Seguridad/login/index.cshtml");
        }

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Correo.ToLower() == correo);

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

        // El rol y el destino proceden de la cuenta en la base de datos, no del formulario.
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

        // La contraseña, el estado de la cuenta (activo/inactivo) y el registro de auditoría del
        // intento de inicio de sesión quedan a cargo de AuthService, que ya centraliza ese manejo
        // (incluye manejo de errores de BD y logging) en vez de duplicarlo aquí.
        var authResult = await _authService.LoginAsync(new LoginRequest
        {
            Correo = correo,
            Contrasena = password,
            Rol = rolSeleccionado,
        });

        if (!authResult.Success)
        {
            const string mensajeCredencialesInvalidas =
                "Correo o contraseña incorrectos.";

            if (wantsJson)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = mensajeCredencialesInvalidas
                });
            }

            Response.StatusCode = StatusCodes.Status401Unauthorized;
            ViewData["ErrorStatus"] = StatusCodes.Status401Unauthorized;
            ModelState.AddModelError("", mensajeCredencialesInvalidas);
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
            int? idPaciente = await EnsureRelacionPerfilAsync(usuario, rolNombre);
            if (idPaciente.HasValue)
            {
                claims.Add(new Claim("IdPaciente", idPaciente.Value.ToString()));
            }

        }
        else if (string.Equals(rolNombre, "Profesional", StringComparison.OrdinalIgnoreCase))
        {
            int? idProfesional = await EnsureRelacionPerfilAsync(usuario, rolNombre);
            if (idProfesional.HasValue)
            {
                claims.Add(new Claim("IdProfesional", idProfesional.Value.ToString()));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties
            {
                IsPersistent = remember,
                AllowRefresh = true
            });



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
        bool administradorDebeAbrirDashboard =
            string.Equals(rolNombre, "Administrador", StringComparison.OrdinalIgnoreCase);

        if (!administradorDebeAbrirDashboard &&
            Url.IsLocalUrl(rr) &&
            !rr.StartsWith("/acceso-y-seguridad/login", StringComparison.OrdinalIgnoreCase))
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

    private async Task<int?> EnsureRelacionPerfilAsync(Usuario usuario, string rolNombre)
    {
        if (string.Equals(rolNombre, "Paciente", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Pacientes
                .Where(p => p.IdUsuario == usuario.IdUsuario)
                .Select(p => (int?)p.IdPaciente)
                .FirstOrDefaultAsync();
        }

        if (string.Equals(rolNombre, "Profesional", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Profesionales
                .Where(p => p.IdUsuario == usuario.IdUsuario)
                .Select(p => (int?)p.IdProfesional)
                .FirstOrDefaultAsync();
        }

        return null;
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
    [EnableRateLimiting("AccountRecoveryByIp")]
    [Route("acceso-y-seguridad/recover/send-code")]
    public async Task<IActionResult> RecoverSendCode([FromBody] Models.ViewModels.RecoverPasswordRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "No fue posible validar la solicitud. Recarga la página e inténtalo de nuevo."
            });
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
    [EnableRateLimiting("AccountRecoveryByIp")]
    [Route("acceso-y-seguridad/recover/verify-code")]
    public async Task<IActionResult> RecoverVerifyCode([FromBody] VerifyRecoveryCodeRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "No fue posible validar la solicitud. Recarga la página e inténtalo de nuevo."
            });
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
    [EnableRateLimiting("AccountRecoveryByIp")]
    [Route("acceso-y-seguridad/recover/reset-password")]
    public async Task<IActionResult> RecoverResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "No fue posible validar la solicitud. Recarga la página e inténtalo de nuevo."
            });
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

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Route("acceso-y-seguridad/acceso-denegado")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Acceso_Y_Seguridad/access-denied/index.cshtml");
    }

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

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("acceso-y-seguridad/cambiar-contrasena/api")]
    public async Task<IActionResult> ChangePasswordApi(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct = default)
    {
        var response = await _authService.ChangePasswordAsync(request, ct);
        if (!response.Success)
            return BadRequest(new { success = false, message = response.Message });

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete("SmileTrack-JWT");
        return Ok(new { success = true, message = response.Message, requiresLogin = true });
    }

    [HttpGet]
    [Route("acceso-y-seguridad/register")]
    public IActionResult Register() => View("~/Views/Acceso_Y_Seguridad/register/index.cshtml");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("RegisterByIp")]
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
        var permisos = new Dictionary<string, bool>();
        foreach (var accion in new[] { "agendar", "reagendar", "cancelar" })
            permisos[accion] = await PermisoReagendamiento.PermitidoAsync(_context, HttpContext.RequestAborted, accion);
        ViewData["PermisosRecepcion"] = permisos;
        ViewData["ProfesionalReagenda"] = await PermisoReagendamiento.PermitidoAsync(_context, HttpContext.RequestAborted, profesional: true);
        var rolesDb = await _context.Roles.ToListAsync();
        ViewData["RolesJson"] = System.Text.Json.JsonSerializer.Serialize(rolesDb);
        return View("~/Views/Acceso_Y_Seguridad/st-adm-03-gestion-roles/index.cshtml", rolesDb);
    }

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-04-matriz-permisos")]
    public IActionResult Stadm04MatrizPermisos() => Redirect("/acceso-y-seguridad/st-adm-03-gestion-roles");

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    [Route("acceso-y-seguridad/gestion-roles/recepcion-permiso")]
    public async Task<IActionResult> CambiarPermisoRecepcion([FromForm] string accion, [FromForm] bool? permitido, CancellationToken ct, [FromForm] bool profesional = false)
    {
        if (!ModelState.IsValid || !permitido.HasValue || accion is not ("agendar" or "reagendar" or "cancelar")
            || (profesional && accion != "reagendar"))
            return BadRequest();

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor <= 0)
            return Forbid();

        var clave = profesional ? PermisoReagendamiento.ClaveProfesional : PermisoReagendamiento.Clave(accion);
        var rol = profesional ? "Profesional" : "Recepción";
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaccion = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var ajuste = await _context.ConfiguracionesGenerales.SingleOrDefaultAsync(c => c.Clave == clave, ct);
            var anterior = ajuste == null || ajuste.Valor == "true";
            if (anterior != permitido.Value)
            {
                var nuevo = permitido.Value ? "true" : "false";
                if (ajuste == null)
                    _context.ConfiguracionesGenerales.Add(new ConfiguracionGeneral { Clave = clave, Valor = nuevo, Modulo = "citas" });
                else
                    ajuste.Valor = nuevo;

                _context.Auditorias.Add(new Auditoria
                {
                    IdUsuario = actor,
                    TablaAfectada = "Configuracion_General",
                    Accion = ajuste == null ? "INSERT" : "UPDATE",
                    DatosAnteriores = System.Text.Json.JsonSerializer.Serialize(new { permiso = clave, permitido = anterior }),
                    DatosNuevos = System.Text.Json.JsonSerializer.Serialize(new { permiso = clave, permitido = permitido.Value }),
                    Descripcion = $"Cambio del permiso de {rol}: {accion}."
                });
                await _context.SaveChangesAsync(ct);
            }

            await transaccion.CommitAsync(ct);
        });
        TempData["PermisoReagendarMensaje"] = $"Permiso de {accion} para {rol} actualizado.";
        return RedirectToAction(nameof(Stadm03GestionRoles));
    }

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
        string token = System.Net.WebUtility.HtmlEncode(
            _antiforgery.GetAndStoreTokens(HttpContext).RequestToken ?? string.Empty);
        const string template = """
<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Diagn?stico SMTP ? SmileTrack</title>
<style>
body{font-family:Arial,sans-serif;margin:0;padding:32px;background:#f3f4f6;color:#111827}
.container{max-width:760px;margin:0 auto;background:#fff;border:1px solid #e5e7eb;border-radius:14px;padding:28px}
button{padding:10px 18px;background:#0f766e;color:#fff;border:0;border-radius:8px;font-weight:600;cursor:pointer}
button:focus-visible,a:focus-visible{outline:3px solid #2563eb;outline-offset:3px}
button:disabled{opacity:.65;cursor:wait}
#resultado{margin-top:16px;white-space:pre-wrap}
</style>
</head>
<body>
<main class="container">
  <h1>Diagn?stico SMTP</h1>
  <p>La prueba enviar? un mensaje sin datos cl?nicos ?nicamente a la cuenta configurada en <code>Smtp:RecipientOverride</code>.</p>
  <input type="hidden" name="__RequestVerificationToken" value="__CSRF_TOKEN__" />
  <button id="btnTest" type="button">Ejecutar prueba SMTP</button>
  <p id="resultado" role="status" aria-live="polite" hidden></p>
  <a href="/acceso-y-seguridad/st-adm-01-dashboard">Volver al dashboard</a>
</main>
<script>
document.getElementById('btnTest').addEventListener('click', async (event) => {
  const button = event.currentTarget;
  const result = document.getElementById('resultado');
  const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
  button.disabled = true;
  result.hidden = false;
  result.textContent = 'Ejecutando prueba SMTP?';
  try {
    const response = await fetch('/acceso-y-seguridad/st-adm-16-test-smtp/run', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'X-CSRF-TOKEN': token }
    });
    const payload = await response.json();
    result.textContent = payload.detalle || 'La prueba finaliz? sin detalle.';
  } catch {
    result.textContent = 'No fue posible completar la prueba. Revisa la sesi?n y los registros del servidor.';
  } finally {
    button.disabled = false;
  }
});
</script>
</body>
</html>
""";
        string html = template.Replace("__CSRF_TOKEN__", token, StringComparison.Ordinal);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    [Route("acceso-y-seguridad/st-adm-16-test-smtp/run")]
    public async Task<IActionResult> Stadm16RunTestSmtp(CancellationToken ct = default)
    {
        try
        {
            var (exito, detalle) = await _emailService.ProbarConfiguracionSmtpAsync(string.Empty, ct);
            return Ok(new { exito, detalle });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                exito = false,
                detalle = "No fue posible completar la prueba SMTP. Revisa los registros del servidor."
            });
        }
    }

}
