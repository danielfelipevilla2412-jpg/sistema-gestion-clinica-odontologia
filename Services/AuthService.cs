using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services.Email;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Services
{
    public class AuthService : IAuthService
    {
        private const string MensajeErrorSeguridad =
            "No fue posible completar la operación. Por favor intente nuevamente en unos momentos.";
        private const string CorreoRedactadoParaLog = "[redacted]";

        private const string MensajeCredencialesInvalidas =
            "Correo o contraseña incorrectos";

        private const int MaxLoginFailedAttempts = 3;
        private static readonly TimeSpan DuracionBloqueoLogin =
            TimeSpan.FromMinutes(15);

        private const string MensajeCuentaBloqueada =
            "La cuenta está temporalmente bloqueada. Intenta de nuevo más tarde.";

        private const string MensajeUsuarioNoDisponible =
            "El acceso no está disponible en este momento. Intente nuevamente más tarde.";

        private const string MensajeRecuperacionGenerico =
            "Si el correo está registrado, recibirás un código.";

        private const string MensajeCodigoInvalidoOExpirado =
            "Código inválido o expirado.";

        private const string MensajeTokenRecuperacionInvalido =
            "La sesión de recuperación no es válida o expiró. Solicita un nuevo código.";

        private const int VigenciaCodigoRecuperacionMinutos = 15;
        private const int VentanaRateLimitMinutos = 10;
        private const int MaxSolicitudesRecuperacionPorVentana = 2;
        private const int MaxIntentosCodigoRecuperacion = 3;

        private static readonly EmailAddressAttribute ValidadorCorreo = new();

        private static readonly Regex RegexContrasenaNueva = new(
            AuthPasswordPolicy.Pattern,
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500));

        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly IDataProtector _passwordRecoveryProtector;

        public AuthService(
            IConfiguration configuration,
            AppDbContext context,
            ILogger<AuthService> logger,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService,
            IDataProtectionProvider dataProtectionProvider)
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));

            _context = context
                ?? throw new ArgumentNullException(nameof(context));

            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));

            _httpContextAccessor = httpContextAccessor
                ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            _emailService = emailService
                ?? throw new ArgumentNullException(nameof(emailService));

            _passwordRecoveryProtector =
                (dataProtectionProvider
                ?? throw new ArgumentNullException(nameof(dataProtectionProvider)))
                .CreateProtector("SmileTrack.PasswordRecovery");
        }

        // ============================================================
        // LOGIN
        // ============================================================

        public async Task<AuthResponse> LoginAsync(
            LoginRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            string correoNormalizado =
                NormalizarCorreoCuenta(request?.Correo);

            string rolSolicitado =
                request?.Rol?.Trim() ?? string.Empty;

            try
            {
                _logger.LogInformation(
                    "Intento de inicio de sesión recibido. Correo={Correo}, RolSolicitado={Rol}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    rolSolicitado,
                    ipCliente);

                if (request is null ||
                    string.IsNullOrWhiteSpace(request.Correo) ||
                    string.IsNullOrWhiteSpace(request.Contrasena))
                {
                    _logger.LogWarning(
                        "Login fallido: campos obligatorios vacíos. IpCliente={IpCliente}",
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Correo y contraseña son obligatorios."
                    };
                }

                if (!EsCorreoValido(correoNormalizado) ||
                    correoNormalizado.Length > 150 ||
                    request.Contrasena.Length > 100)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCredencialesInvalidas
                    };
                }

                Usuario? user;

                try
                {
                    user = await _context.Usuarios
                        .Include(u => u.Rol)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            u => u.Correo.ToLower() == correoNormalizado,
                            ct);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogCritical(
                        sqlEx,
                        "Fallo SQL al consultar usuario en Login. Correo={Correo}, Rol={Rol}, IpCliente={IpCliente}, NumeroError={NumError}",
                        CorreoRedactadoParaLog,
                        rolSolicitado,
                        ipCliente,
                        sqlEx.Number);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeUsuarioNoDisponible
                    };
                }

                if (user == null || user.Rol == null)
                {
                    _logger.LogWarning(
                        "Login fallido: usuario no encontrado. Correo={Correo}, RolSolicitadoUI={Rol}, IpCliente={IpCliente}",
                        CorreoRedactadoParaLog,
                        rolSolicitado,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCredencialesInvalidas
                    };
                }

                DateTime ahoraUtc = DateTime.UtcNow;
                if (user.BloqueadoHasta.HasValue &&
                    user.BloqueadoHasta.Value > ahoraUtc)
                {
                    _logger.LogWarning(
                        "Login fallido: cuenta temporalmente bloqueada. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCuentaBloqueada
                    };
                }

                if (user.IntentosFallidos >= MaxLoginFailedAttempts)
                {
                    var usuarioActualizar =
                        await _context.Usuarios.FindAsync(
                            new object[] { user.IdUsuario },
                            ct);

                    if (usuarioActualizar != null)
                    {
                        if (!usuarioActualizar.BloqueadoHasta.HasValue)
                        {
                            usuarioActualizar.BloqueadoHasta =
                                ahoraUtc.Add(DuracionBloqueoLogin);
                            await _context.SaveChangesAsync(ct);

                            return new AuthResponse
                            {
                                Success = false,
                                Message = MensajeCuentaBloqueada
                            };
                        }

                        usuarioActualizar.IntentosFallidos = 0;
                        usuarioActualizar.BloqueadoHasta = null;
                        await _context.SaveChangesAsync(ct);
                    }
                }

                if (!string.Equals(
                        user.Estado,
                        "activo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Login fallido: usuario inactivo. IdUsuario={IdUsuario}, Correo={Correo}, Estado={Estado}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog,
                        user.Estado,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Su cuenta se encuentra inactiva. Contacte al administrador del sistema."
                    };
                }

                bool isPasswordValid;

                try
                {
                    isPasswordValid =
                        BCrypt.Net.BCrypt.Verify(
                            request.Contrasena,
                            user.Contrasena);
                }
                catch (FormatException fEx)
                {
                    _logger.LogCritical(
                        fEx,
                        "Hash de contraseña inválido en BD para usuario. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCredencialesInvalidas
                    };
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(
                        bcEx,
                        "Error inesperado al verificar contraseña BCrypt. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCredencialesInvalidas
                    };
                }

                if (!isPasswordValid)
                {
                    var usuarioActualizar =
                        await _context.Usuarios.FindAsync(
                            new object[] { user.IdUsuario },
                            ct);

                    bool estaBloqueada = false;

                    if (usuarioActualizar != null)
                    {
                        usuarioActualizar.IntentosFallidos += 1;

                        if (usuarioActualizar.IntentosFallidos >= MaxLoginFailedAttempts)
                        {
                            estaBloqueada = true;
                            usuarioActualizar.BloqueadoHasta =
                                DateTime.UtcNow.Add(DuracionBloqueoLogin);

                            _logger.LogWarning(
                                "Cuenta bloqueada por intentos fallidos. IdUsuario={IdUsuario}, Correo={Correo}, IntentosFallidos={IntentosFallidos}, IpCliente={IpCliente}",
                                user.IdUsuario, CorreoRedactadoParaLog, usuarioActualizar.IntentosFallidos, ipCliente);
                        }

                        _context.Usuarios.Update(usuarioActualizar);

                        try
                        {
                            await _context.SaveChangesAsync(ct);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            _logger.LogWarning(
                                "Login: concurrencia al actualizar intentos fallidos. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                                user.IdUsuario,
                                CorreoRedactadoParaLog,
                                ipCliente);
                        }
                        catch (SqlException sqlEx)
                        {
                            _logger.LogError(
                                sqlEx,
                                "Login: SQL error actualizando intentos fallidos. IdUsuario={IdUsuario}, NumeroError={NumError}",
                                user.IdUsuario,
                                sqlEx.Number);
                        }
                    }

                    _logger.LogWarning(
                        "Login fallido: contraseña incorrecta. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = estaBloqueada
                            ? MensajeCuentaBloqueada
                            : MensajeCredencialesInvalidas
                    };
                }

                if (!string.IsNullOrWhiteSpace(rolSolicitado) &&
                    !string.Equals(
                        rolSolicitado,
                        user.Rol.NombreRol,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Login fallido: el rol seleccionado no coincide con el rol de la cuenta. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCredencialesInvalidas
                    };
                }

                var usuarioLogin =
                    await _context.Usuarios.FindAsync(
                        new object[] { user.IdUsuario },
                        ct);

                if (usuarioLogin != null)
                {
                    usuarioLogin.UltimoLogin = DateTime.UtcNow;
                    usuarioLogin.IntentosFallidos = 0;
                    usuarioLogin.BloqueadoHasta = null;

                    _context.Usuarios.Update(usuarioLogin);

                    try
                    {
                        await _context.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        _logger.LogWarning(
                            "Login: concurrencia al actualizar UltimoLogin. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                            user.IdUsuario,
                            CorreoRedactadoParaLog,
                            ipCliente);
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogError(
                            sqlEx,
                            "Login: SQL error actualizando UltimoLogin. IdUsuario={IdUsuario}, NumeroError={NumError}",
                            user.IdUsuario,
                            sqlEx.Number);
                    }
                }

                string token = GenerateJwtToken(
                    user.Correo,
                    user.Rol.NombreRol,
                    user.IdUsuario);

                var userPayload = new
                {
                    idUsuario = user.IdUsuario,
                    correo = user.Correo,
                    rol = user.Rol.NombreRol,
                    nombre = user.Nombre,
                    apellidos = user.Apellidos
                };

                _logger.LogInformation(
                    "Inicio de sesión exitoso. IdUsuario={IdUsuario}, Correo={Correo}, Rol={Rol}, IpCliente={IpCliente}",
                    user.IdUsuario,
                    CorreoRedactadoParaLog,
                    user.Rol.NombreRol,
                    ipCliente);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Inicio de sesión exitoso.",
                    Token = token,
                    User = userPayload
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Login cancelado por el cliente. Correo={Correo}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (ArgumentException argEx)
            {
                _logger.LogWarning(
                    argEx,
                    "Parámetro inválido en Login. Correo={Correo}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeCredencialesInvalidas
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en LoginAsync. Correo={Correo}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }

        // ============================================================
        // REGISTRO
        // ============================================================

        public async Task<AuthResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            string correoNormalizado =
                NormalizarCorreoCuenta(request?.Correo);

            try
            {
                _logger.LogInformation(
                    "Intento de registro recibido. Correo={Correo}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    ipCliente);

                if (request is null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Todos los campos son obligatorios."
                    };
                }

                if (string.IsNullOrWhiteSpace(request.Correo) ||
                    string.IsNullOrWhiteSpace(request.Contrasena) ||
                    string.IsNullOrWhiteSpace(request.Nombre) ||
                    string.IsNullOrWhiteSpace(request.Apellidos) ||
                    string.IsNullOrWhiteSpace(request.ConfirmarContrasena) ||
                    string.IsNullOrWhiteSpace(request.Telefono))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Todos los campos son obligatorios."
                    };
                }

                if (!EsCorreoValido(correoNormalizado) ||
                    correoNormalizado.Length > 150 ||
                    request.Nombre.Trim().Length > 100 ||
                    request.Apellidos.Trim().Length > 100 ||
                    request.Telefono.Trim().Length > 20)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Verifica los datos ingresados."
                    };
                }

                if (!string.Equals(
                        request.Contrasena,
                        request.ConfirmarContrasena,
                        StringComparison.Ordinal))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Las contraseñas no coinciden."
                    };
                }

                if (string.IsNullOrWhiteSpace(request.TipoDocumento) ||
                    string.IsNullOrWhiteSpace(request.NumeroDocumento))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "El documento es obligatorio para completar el registro."
                    };
                }

                if (!request.AceptaTratamientoDatos)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Debes autorizar el tratamiento de datos para crear tu cuenta."
                    };
                }

                string tipoDocumento = request.TipoDocumento.Trim().ToUpperInvariant();
                string numeroDocumento = request.NumeroDocumento.Trim().ToUpperInvariant();
                if (tipoDocumento is not ("CC" or "TI" or "CE" or "PAS"))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "El tipo de documento no es válido."
                    };
                }

                string patronDocumento = tipoDocumento == "PAS"
                    ? "^[A-Z0-9]{5,20}$"
                    : "^[0-9]{7,15}$";
                if (!Regex.IsMatch(numeroDocumento, patronDocumento))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "El documento no es válido para el tipo seleccionado."
                    };
                }

                if (!request.FechaNacimiento.HasValue ||
                    request.FechaNacimiento.Value.Date < new DateTime(1900, 1, 1) ||
                    request.FechaNacimiento.Value.Date > DateTime.UtcNow.Date)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Ingresa una fecha de nacimiento válida."
                    };
                }

                if (!RegexContrasenaNueva.IsMatch(request.Contrasena))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = AuthPasswordPolicy.ErrorMessage
                    };
                }

                bool existeCuenta;

                try
                {
                    existeCuenta =
                        await _context.Usuarios.AnyAsync(
                            u => u.Correo.ToLower() == correoNormalizado,
                            ct) ||
                        await _context.Pacientes.AnyAsync(
                            p => p.TipoDocumento.ToUpper() == tipoDocumento &&
                                 p.Documento.ToUpper() == numeroDocumento,
                            ct);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogCritical(
                        sqlEx,
                        "Fallo SQL al verificar correo duplicado. Correo={Correo}, IpCliente={IpCliente}, NumError={NumError}",
                        CorreoRedactadoParaLog,
                        ipCliente,
                        sqlEx.Number);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                if (existeCuenta)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "El correo o documento ya se encuentran registrados."
                    };
                }

                const string roleName = "Paciente";

                var role = await _context.Roles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.NombreRol == roleName,
                        ct);

                if (role == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = $"El rol '{roleName}' no es válido."
                    };
                }

                string firstName =
                    string.IsNullOrWhiteSpace(request.Nombre)
                        ? "SmileTrack"
                        : request.Nombre.Trim();

                string lastName =
                    string.IsNullOrWhiteSpace(request.Apellidos)
                        ? "SmileTrack"
                        : request.Apellidos.Trim();

                string hashContrasena;

                try
                {
                    hashContrasena =
                        BCrypt.Net.BCrypt.HashPassword(
                            request.Contrasena,
                            workFactor: 11);
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(
                        bcEx,
                        "Error al generar hash BCrypt. Correo={Correo}, IpCliente={IpCliente}",
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                var newUser = new Usuario
                {
                    Nombre = firstName,
                    Apellidos = lastName,
                    Correo = correoNormalizado,
                    Contrasena = hashContrasena,
                    IdRol = role.IdRol,
                    Estado = "activo",
                    FechaNacimiento = request.FechaNacimiento.Value.Date,
                    FechaCreacion = DateTime.UtcNow
                };

                var strategy = _context.Database.CreateExecutionStrategy();

                AuthResponse? resultadoError = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);

                try
                {
                    _context.Usuarios.Add(newUser);

                    await _context.SaveChangesAsync(ct);

                    if (roleName.Equals(
                            "Paciente",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var newPaciente = new Paciente
                        {
                            IdUsuario = newUser.IdUsuario,

                            TipoDocumento = tipoDocumento,

                            Documento = numeroDocumento,

                            Nombres = firstName,

                            Apellidos =
                                string.IsNullOrWhiteSpace(lastName)
                                    ? "Paciente"
                                    : lastName,

                            FechaNacimiento =
                                request.FechaNacimiento.Value.Date,

                            Correo = correoNormalizado,
                            Telefono = request.Telefono.Trim(),
                            Estado = "activo",
                            FechaRegistro = DateTime.UtcNow.Date
                        };

                        _context.Pacientes.Add(newPaciente);

                        await _context.SaveChangesAsync(ct);
                    }

                    // Yeray: Conserva evidencia de quién aceptó, cuándo, desde qué IP y qué versión legal se mostró.
                    _context.Auditorias.Add(new Auditoria
                    {
                        IdUsuario = newUser.IdUsuario,
                        TablaAfectada = "Autorizacion_Tratamiento_Datos",
                        IdRegistro = newUser.IdUsuario,
                        Accion = "INSERT",
                        IpOrigen = ipCliente,
                        Descripcion = "Aceptación de tratamiento de datos personales y sensibles; versión 1.0, noviembre de 2026.",
                        Fecha = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync(ct);

                    await tx.CommitAsync(ct);
                    return (AuthResponse?)null;
                }
                catch (DbUpdateException dbEx)
                    when (EsViolacionIndiceUnico(dbEx, out string? nombreIndice))
                {
                    await tx.RollbackAsync(ct);
                    return new AuthResponse { Success = false, Message = "El correo o documento ya se encuentran registrados." };
                }

                catch (DbUpdateException dbEx)
                    when (EsViolacionIntegridadReferencial(dbEx))
                {
                    await tx.RollbackAsync(ct);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }
                catch (DbUpdateConcurrencyException concEx)
                {
                    await tx.RollbackAsync(ct);

                    _logger.LogWarning(
                        concEx,
                        "Registro: conflicto concurrencia. Correo={Correo}, IpCliente={IpCliente}",
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Conflicto temporal, intente nuevamente."
                    };
                }
                catch (SqlException sqlEx)
                {
                    await tx.RollbackAsync(ct);

                    _logger.LogCritical(
                        sqlEx,
                        "Registro fallido SQL. Correo={Correo}, NumeroError={NumError}, IpCliente={IpCliente}",
                        CorreoRedactadoParaLog,
                        sqlEx.Number,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }
                catch (Exception innerEx)
                {
                    await tx.RollbackAsync(ct);

                    _logger.LogError(
                        innerEx,
                        "Registro fallido. Correo={Correo}, IpCliente={IpCliente}",
                        CorreoRedactadoParaLog,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }
            });

                if (resultadoError != null)             // ← AGREGAR: si hubo error, cortar aquí
                {
                    return resultadoError;
                }

                string token = GenerateJwtToken(
                    newUser.Correo,
                    role.NombreRol,
                    newUser.IdUsuario);

                var userPayload = new
                {
                    idUsuario = newUser.IdUsuario,
                    correo = newUser.Correo,
                    rol = role.NombreRol,
                    nombre = newUser.Nombre,
                    apellidos = newUser.Apellidos
                };

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registro exitoso.",
                    Token = token,
                    User = userPayload
                };
            }

            catch (OperationCanceledException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (RegexMatchTimeoutException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en RegisterAsync. Correo={Correo}, IpCliente={IpCliente}",
                    CorreoRedactadoParaLog,
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }


        // ============================================================
        // RECUPERAR CONTRASEÑA
        // ============================================================

        public async Task<AuthResponse> RecoverPasswordAsync(
            RecoverPasswordRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            string correoNormalizado =
                NormalizarCorreo(request?.Correo);

            try
            {
                if (string.IsNullOrWhiteSpace(correoNormalizado))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "El correo es obligatorio."
                    };
                }

                if (!EsCorreoValido(correoNormalizado))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Ingresa un correo electrónico válido."
                    };
                }

                var user =
                    await _context.Usuarios.FirstOrDefaultAsync(
                        u => u.Correo.ToLower() == correoNormalizado,
                        ct);

                if (user == null)
                {
                    await RegistrarAuditoriaRecuperacionAsync(
                        null,
                        correoNormalizado,
                        "solicitud",
                        ipCliente,
                        ct);

                    return new AuthResponse
                    {
                        Success = true,
                        Message = MensajeRecuperacionGenerico
                    };
                }

                var limiteVentana =
                    DateTime.UtcNow.AddMinutes(
                        -VentanaRateLimitMinutos);

                int solicitudesRecientes =
                    await _context.AuditoriasRecuperacion
                        .AsNoTracking()
                        .CountAsync(
                            a =>
                                a.CorreoSolicitado ==
                                correoNormalizado &&
                                a.Accion == "solicitud" &&
                                a.Fecha >= limiteVentana,
                            ct);

                if (solicitudesRecientes >=
                    MaxSolicitudesRecuperacionPorVentana)
                {
                    await RegistrarAuditoriaRecuperacionAsync(
                        user.IdUsuario,
                        correoNormalizado,
                        "rate_limit_excedido",
                        ipCliente,
                        ct);

                    return new AuthResponse
                    {
                        Success = true,
                        Message = MensajeRecuperacionGenerico
                    };
                }

                string code =
                    RandomNumberGenerator
                        .GetInt32(0, 1000000)
                        .ToString("D6");

                var ahora = DateTime.UtcNow;

                var recoveryCode = new CodigoRecuperacion
                {
                    IdUsuario = user.IdUsuario,

                    CodigoHash =
                        BCrypt.Net.BCrypt.HashPassword(
                            code,
                            workFactor: 11),

                    FechaCreacion = ahora,

                    FechaExpiracion =
                        ahora.AddMinutes(
                            VigenciaCodigoRecuperacionMinutos),

                    IntentosFallidos = 0,
                    Usado = false,
                    IpOrigen = ipCliente
                };

                _context.CodigosRecuperacion.Add(
                    recoveryCode);

                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error guardando código de recuperación. IdUsuario={IdUsuario}",
                        user.IdUsuario);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                try
                {
                    await _emailService.SendRecoveryCodeAsync(
                        user.Correo,
                        code,
                        ct);
                }
                catch (Exception mailEx)
                {
                    _logger.LogCritical(
                        mailEx,
                        "Fallo envío de correo de recuperación. IdUsuario={IdUsuario}, Correo={Correo}",
                        user.IdUsuario,
                        CorreoRedactadoParaLog);

                    await RegistrarAuditoriaRecuperacionAsync(
                        user.IdUsuario,
                        correoNormalizado,
                        "envio_fallido",
                        ipCliente,
                        ct);

                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "No fue posible enviar el correo de recuperación. Verifica la configuración SMTP e intenta nuevamente."
                    };
                }

                await RegistrarAuditoriaRecuperacionAsync(
                    user.IdUsuario,
                    correoNormalizado,
                    "solicitud",
                    ipCliente,
                    ct);

                return new AuthResponse
                {
                    Success = true,
                    Message = MensajeRecuperacionGenerico
                };
            }
            catch (OperationCanceledException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en RecoverPasswordAsync. Correo={Correo}",
                    CorreoRedactadoParaLog);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }

        // ============================================================
        // VERIFICAR CÓDIGO DE RECUPERACIÓN
        // ============================================================

        public async Task<AuthResponse> VerifyRecoveryCodeAsync(
            VerifyRecoveryCodeRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            string correoNormalizado =
                NormalizarCorreo(request?.Correo);

            try
            {
                if (string.IsNullOrWhiteSpace(correoNormalizado) ||
                    string.IsNullOrWhiteSpace(request?.Codigo))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Todos los campos son obligatorios."
                    };
                }

                if (!EsCorreoValido(correoNormalizado))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Ingresa un correo electrónico válido."
                    };
                }

                string codigoIngresado =
                    request.Codigo.Trim();

                if (!Regex.IsMatch(
                        codigoIngresado,
                        @"^\d{6}$",
                        RegexOptions.None,
                        TimeSpan.FromMilliseconds(200)))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "Ingresa un código válido de 6 dígitos."
                    };
                }

                var user =
                    await _context.Usuarios
                        .FirstOrDefaultAsync(
                            u => u.Correo.ToLower() == correoNormalizado,
                            ct);

                if (user == null)
                {
                    await RegistrarAuditoriaRecuperacionAsync(
                        null,
                        correoNormalizado,
                        "codigo_fallido",
                        ipCliente,
                        ct);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCodigoInvalidoOExpirado
                    };
                }

                var codigoRecuperacion =
                    await _context.CodigosRecuperacion
                        .Where(c =>
                            c.IdUsuario == user.IdUsuario &&
                            !c.Usado)
                        .OrderByDescending(
                            c => c.FechaCreacion)
                        .FirstOrDefaultAsync(ct);

                if (codigoRecuperacion == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCodigoInvalidoOExpirado
                    };
                }

                if (codigoRecuperacion.FechaExpiracion <
                    DateTime.UtcNow)
                {
                    codigoRecuperacion.Usado = true;

                    _context.CodigosRecuperacion.Update(
                        codigoRecuperacion);

                    await _context.SaveChangesAsync(ct);

                    await RegistrarAuditoriaRecuperacionAsync(
                        user.IdUsuario,
                        correoNormalizado,
                        "codigo_fallido",
                        ipCliente,
                        ct);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCodigoInvalidoOExpirado
                    };
                }

                bool codigoValido =
                    BCrypt.Net.BCrypt.Verify(
                        codigoIngresado,
                        codigoRecuperacion.CodigoHash);

                if (!codigoValido)
                {
                    codigoRecuperacion.IntentosFallidos++;

                    if (codigoRecuperacion.IntentosFallidos >=
                        MaxIntentosCodigoRecuperacion)
                    {
                        codigoRecuperacion.Usado = true;
                    }

                    _context.CodigosRecuperacion.Update(
                        codigoRecuperacion);

                    await _context.SaveChangesAsync(ct);

                    await RegistrarAuditoriaRecuperacionAsync(
                        user.IdUsuario,
                        correoNormalizado,
                        "codigo_fallido",
                        ipCliente,
                        ct);

                    if (codigoRecuperacion.IntentosFallidos >=
                        MaxIntentosCodigoRecuperacion)
                    {
                        await RegistrarAuditoriaRecuperacionAsync(
                            user.IdUsuario,
                            correoNormalizado,
                            "bloqueo_por_intentos",
                            ipCliente,
                            ct);

                        return new AuthResponse
                        {
                            Success = false,
                            Message =
                                "Demasiados intentos fallidos, solicita un nuevo código."
                        };
                    }

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeCodigoInvalidoOExpirado
                    };
                }

                await RegistrarAuditoriaRecuperacionAsync(
                    user.IdUsuario,
                    correoNormalizado,
                    "codigo_verificado",
                    ipCliente,
                    ct);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Código verificado correctamente.",
                    RecoveryToken =
                        GenerarTokenRecuperacion(
                            codigoRecuperacion)
                };
            }
            catch (OperationCanceledException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en VerifyRecoveryCodeAsync. Correo={Correo}",
                    CorreoRedactadoParaLog);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }

        // ============================================================
        // RESTABLECER CONTRASEÑA
        // ============================================================

        public async Task<AuthResponse> ResetPasswordAsync(
            ResetPasswordRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            try
            {
                if (request is null ||
                    string.IsNullOrWhiteSpace(
                        request.TokenTemporal) ||
                    string.IsNullOrWhiteSpace(
                        request.NuevaContrasena) ||
                    string.IsNullOrWhiteSpace(
                        request.ConfirmarContrasena))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Todos los campos son obligatorios."
                    };
                }

                if (!string.Equals(
                        request.NuevaContrasena,
                        request.ConfirmarContrasena,
                        StringComparison.Ordinal))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "La nueva contraseña y la confirmación no coinciden."
                    };
                }

                if (!RegexContrasenaNueva.IsMatch(
                        request.NuevaContrasena))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = AuthPasswordPolicy.ErrorMessage
                    };
                }

                var tokenRecuperacion =
                    LeerTokenRecuperacion(
                        request.TokenTemporal);

                if (tokenRecuperacion == null ||
                    tokenRecuperacion.ExpiraUnix <
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            MensajeTokenRecuperacionInvalido
                    };
                }

                var codigoRecuperacion =
                    await _context.CodigosRecuperacion
                        .FirstOrDefaultAsync(
                            c =>
                                c.IdCodigo ==
                                tokenRecuperacion.IdCodigo &&
                                c.IdUsuario ==
                                tokenRecuperacion.IdUsuario,
                            ct);

                if (codigoRecuperacion == null ||
                    codigoRecuperacion.Usado ||
                    codigoRecuperacion.FechaExpiracion <
                    DateTime.UtcNow)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            MensajeTokenRecuperacionInvalido
                    };
                }

                var user =
                    await _context.Usuarios
                        .FirstOrDefaultAsync(
                            u =>
                                u.IdUsuario ==
                                codigoRecuperacion.IdUsuario,
                            ct);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            MensajeTokenRecuperacionInvalido
                    };
                }

                string nuevoHash;

                try
                {
                    nuevoHash =
                        BCrypt.Net.BCrypt.HashPassword(
                            request.NuevaContrasena,
                            workFactor: 11);
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(
                        bcEx,
                        "Error al generar hash BCrypt en reset. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                user.Contrasena = nuevoHash;

                user.CodigoRecuperacion = null;
                user.FechaExpiracionCodigo = null;
                user.IntentosFallidos = 0;
                user.BloqueadoHasta = null;
                user.UltimoLogout = DateTime.UtcNow;

                codigoRecuperacion.Usado = true;

                _context.Usuarios.Update(user);
                _context.CodigosRecuperacion.Update(
                    codigoRecuperacion);

                try
                {
                    await _context.SaveChangesAsync(ct);

                    await RegistrarAuditoriaRecuperacionAsync(
                        user.IdUsuario,
                        user.Correo,
                        "password_restablecida",
                        ipCliente,
                        ct);
                }
                catch (DbUpdateConcurrencyException concEx)
                {
                    _logger.LogWarning(
                        concEx,
                        "Restablecer: concurrencia al guardar nueva contraseña. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "Conflicto temporal, intente nuevamente."
                    };
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogCritical(
                        sqlEx,
                        "Restablecer: SQL error guardando contraseña. IdUsuario={IdUsuario}, NumError={NumError}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        sqlEx.Number,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                _logger.LogInformation(
                    "Contraseña restablecida exitosamente. IdUsuario={IdUsuario}, Correo={Correo}, IpCliente={IpCliente}",
                    user.IdUsuario,
                    CorreoRedactadoParaLog,
                    ipCliente);

                return new AuthResponse
                {
                    Success = true,
                    Message =
                        "La contraseña ha sido restablecida exitosamente."
                };
            }
            catch (OperationCanceledException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (RegexMatchTimeoutException)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en ResetPasswordAsync. IpCliente={IpCliente}",
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }

        // ============================================================
        // TOKEN DE RECUPERACIÓN
        // ============================================================

        private sealed record RecoveryTokenPayload(
            int IdCodigo,
            int IdUsuario,
            long ExpiraUnix);

        private static string NormalizarCorreo(string? correo)
        {
            return NormalizarCorreoCuenta(correo);
        }

        private static string NormalizarCorreoCuenta(string? correo)
        {
            return correo?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static bool EsCorreoValido(string correo)
        {
            return !string.IsNullOrWhiteSpace(correo) &&
                   ValidadorCorreo.IsValid(correo);
        }

        private async Task RegistrarAuditoriaRecuperacionAsync(
            int? idUsuario,
            string correoSolicitado,
            string accion,
            string ipCliente,
            CancellationToken ct)
        {
            var auditoria = new AuditoriaRecuperacion
            {
                IdUsuario = idUsuario,
                CorreoSolicitado = correoSolicitado,
                Accion = accion,
                IpOrigen = ipCliente,
                Fecha = DateTime.UtcNow
            };

            _context.AuditoriasRecuperacion.Add(auditoria);

            await _context.SaveChangesAsync(ct);
        }

        private string GenerarTokenRecuperacion(
            CodigoRecuperacion codigoRecuperacion)
        {
            long expiraUnix =
                new DateTimeOffset(
                    codigoRecuperacion.FechaExpiracion)
                .ToUnixTimeSeconds();

            string payload =
                $"{codigoRecuperacion.IdCodigo}|" +
                $"{codigoRecuperacion.IdUsuario}|" +
                $"{expiraUnix}|" +
                $"{Guid.NewGuid():N}";

            return _passwordRecoveryProtector.Protect(
                payload);
        }

        private RecoveryTokenPayload? LeerTokenRecuperacion(
            string tokenTemporal)
        {
            try
            {
                string payload =
                    _passwordRecoveryProtector.Unprotect(
                        tokenTemporal);

                string[] parts =
                    payload.Split(
                        '|',
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 4)
                {
                    return null;
                }

                if (!int.TryParse(
                        parts[0],
                        out int idCodigo) ||
                    !int.TryParse(
                        parts[1],
                        out int idUsuario) ||
                    !long.TryParse(
                        parts[2],
                        out long expiraUnix))
                {
                    return null;
                }

                return new RecoveryTokenPayload(
                    idCodigo,
                    idUsuario,
                    expiraUnix);
            }
            catch
            {
                return null;
            }
        }

        // ============================================================
        // CAMBIAR CONTRASEÑA
        // ============================================================

        public async Task<AuthResponse> ChangePasswordAsync(
            ChangePasswordRequest request,
            CancellationToken ct = default)
        {
            string ipCliente = ObtenerIpCliente();

            try
            {
                _logger.LogInformation(
                    "Solicitud de cambio de contraseña. IpCliente={IpCliente}",
                    ipCliente);

                if (request is null ||
                    string.IsNullOrWhiteSpace(
                        request.ContrasenaActual) ||
                    string.IsNullOrWhiteSpace(
                        request.NuevaContrasena) ||
                    string.IsNullOrWhiteSpace(
                        request.ConfirmarContrasena))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Todos los campos son obligatorios."
                    };
                }

                if (!string.Equals(
                        request.NuevaContrasena,
                        request.ConfirmarContrasena,
                        StringComparison.Ordinal))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Las contraseñas no coinciden."
                    };
                }

                if (!RegexContrasenaNueva.IsMatch(request.NuevaContrasena))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = AuthPasswordPolicy.ErrorMessage
                    };
                }

                // ----------------------------------------------------
                // OBTENER USUARIO AUTENTICADO DESDE EL JWT
                // ----------------------------------------------------
                //
                // NO se utiliza request.IdUsuario.
                // El ID confiable es el almacenado en el JWT.
                //
                var userIdClaim =
                    _httpContextAccessor
                        .HttpContext?
                        .User?
                        .FindFirst(
                            ClaimTypes.NameIdentifier)?
                        .Value;

                if (!int.TryParse(
                        userIdClaim,
                        out int idUsuarioAutenticado) ||
                    idUsuarioAutenticado <= 0)
                {
                    _logger.LogWarning(
                        "Cambio de contraseña rechazado: usuario no autenticado. IpCliente={IpCliente}",
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Usuario no autenticado."
                    };
                }

                // ----------------------------------------------------
                // BUSCAR USUARIO POR EL ID DEL JWT
                // ----------------------------------------------------

                var user =
                    await _context.Usuarios
                        .FirstOrDefaultAsync(
                            u =>
                                u.IdUsuario ==
                                idUsuarioAutenticado,
                            ct);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Usuario no encontrado."
                    };
                }

                if (!string.Equals(
                        user.Estado,
                        "activo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "La cuenta no está activa."
                    };
                }

                // ----------------------------------------------------
                // VERIFICAR CONTRASEÑA ACTUAL
                // ----------------------------------------------------

                bool isCurrentPasswordValid;

                try
                {
                    isCurrentPasswordValid =
                        BCrypt.Net.BCrypt.Verify(
                            request.ContrasenaActual,
                            user.Contrasena);
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(
                        bcEx,
                        "Error verificando contraseña actual. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                if (!isCurrentPasswordValid)
                {
                    _logger.LogWarning(
                        "Cambio de contraseña fallido: contraseña actual incorrecta. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "La contraseña actual es incorrecta."
                    };
                }

                // ----------------------------------------------------
                // GENERAR HASH DE LA NUEVA CONTRASEÑA
                // ----------------------------------------------------

                string nuevoHash;

                try
                {
                    nuevoHash =
                        BCrypt.Net.BCrypt.HashPassword(
                            request.NuevaContrasena,
                            workFactor: 11);
                }
                catch (Exception bcEx)
                {
                    _logger.LogError(
                        bcEx,
                        "Error al generar hash BCrypt en cambio de contraseña. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                user.Contrasena = nuevoHash;

                // Si existieran intentos fallidos acumulados,
                // se reinician al cambiar correctamente la contraseña.
                user.IntentosFallidos = 0;
                user.BloqueadoHasta = null;
                user.UltimoLogout = DateTime.UtcNow;

                _context.Usuarios.Update(user);

                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException concEx)
                {
                    _logger.LogWarning(
                        concEx,
                        "Cambio de contraseña: conflicto de concurrencia. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message =
                            "Conflicto temporal, intente nuevamente."
                    };
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogCritical(
                        sqlEx,
                        "Cambio de contraseña: error SQL. IdUsuario={IdUsuario}, NumeroError={NumeroError}, IpCliente={IpCliente}",
                        user.IdUsuario,
                        sqlEx.Number,
                        ipCliente);

                    return new AuthResponse
                    {
                        Success = false,
                        Message = MensajeErrorSeguridad
                    };
                }

                _logger.LogInformation(
                    "Contraseña cambiada exitosamente. IdUsuario={IdUsuario}, IpCliente={IpCliente}",
                    user.IdUsuario,
                    ipCliente);

                return new AuthResponse
                {
                    Success = true,
                    Message =
                        "La contraseña ha sido cambiada exitosamente."
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Cambio de contraseña cancelado. IpCliente={IpCliente}",
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = "Operación cancelada."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error inesperado en ChangePasswordAsync. IpCliente={IpCliente}",
                    ipCliente);

                return new AuthResponse
                {
                    Success = false,
                    Message = MensajeErrorSeguridad
                };
            }
        }

        // ============================================================
        // HASH DE CÓDIGO DE RECUPERACIÓN
        // ============================================================

        private static string HashRecoveryCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            using var sha256 = SHA256.Create();

            byte[] bytes =
                sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(
                        code.Trim()));

            return Convert.ToHexString(bytes);
        }

        // ============================================================
        // OBTENER IP DEL CLIENTE
        // ============================================================

        private string ObtenerIpCliente()
        {
            try
            {
                var ctx =
                    _httpContextAccessor.HttpContext;

                if (ctx == null)
                {
                    return "desconocido";
                }

                if (ctx.Request.Headers.TryGetValue(
                        "X-Forwarded-For",
                        out var xff) &&
                    !string.IsNullOrWhiteSpace(xff))
                {
                    string primerIp =
                        xff.ToString()
                            .Split(
                                ',',
                                StringSplitOptions.RemoveEmptyEntries)[0]
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(
                            primerIp))
                    {
                        return primerIp;
                    }
                }

                var remoteIp =
                    ctx.Connection.RemoteIpAddress;

                if (remoteIp != null)
                {
                    return remoteIp.ToString();
                }

                return "no-disponible";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo obtener la IP del cliente en AuthService.");

                return "error-obtencion-ip";
            }
        }

        // ============================================================
        // GENERAR JWT
        // ============================================================

        private string GenerateJwtToken(
            string correo,
            string rol,
            int idUsuario = 0)
        {
            try
            {
                string? jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrWhiteSpace(jwtKey))
                {
                    throw new InvalidOperationException(
                        "No se encontró Jwt:Key. Configure una clave JWT mediante User Secrets o variables de entorno.");
                }

                string jwtIssuer =
                    _configuration["Jwt:Issuer"]
                    ?? "SmileTrack";

                string jwtAudience =
                    _configuration["Jwt:Audience"]
                    ?? "SmileTrackClient";

                int expiryMinutes =
                    int.TryParse(
                        _configuration["Jwt:ExpiryMinutes"],
                        out int minutes) &&
                    minutes > 0
                        ? minutes
                        : 60;

                if (jwtKey.Length < 32)
                {
                    throw new InvalidOperationException(
                        "Jwt:Key debe tener al menos 32 caracteres.");
                }

                var claims = new List<Claim>
                {
                    new(
                        ClaimTypes.Name,
                        correo),

                    new(
                        ClaimTypes.Email,
                        correo),

                    new(
                        ClaimTypes.NameIdentifier,
                        idUsuario > 0
                            ? idUsuario.ToString()
                            : "0"),

                    new(
                        "role",
                        rol),

                    new(
                        JwtRegisteredClaimNames.Sub,
                        correo),

                    new(
                        JwtRegisteredClaimNames.Jti,
                        Guid.NewGuid().ToString()),

                    new(
                        JwtRegisteredClaimNames.Iat,
                        DateTimeOffset.UtcNow
                            .ToUnixTimeSeconds()
                            .ToString(),
                        ClaimValueTypes.Integer64)
                };

                var key =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey));

                var creds =
                    new SigningCredentials(
                        key,
                        SecurityAlgorithms.HmacSha256);

                var token =
                    new JwtSecurityToken(
                        issuer: jwtIssuer,
                        audience: jwtAudience,
                        claims: claims,
                        expires:
                            DateTime.UtcNow.AddMinutes(
                                expiryMinutes),
                        signingCredentials: creds);

                return new JwtSecurityTokenHandler()
                    .WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Error generando JWT. Correo={Correo}, Rol={Rol}",
                    CorreoRedactadoParaLog,
                    rol);

                throw new InvalidOperationException(
                    "No se pudo generar el token de autenticación.",
                    ex);
            }
        }

        // ============================================================
        // VALIDAR ÍNDICE UNIQUE
        // ============================================================

        private static bool EsViolacionIndiceUnico(
            DbUpdateException ex,
            out string? nombreIndice)
        {
            nombreIndice = null;

            if (ex?.InnerException is not SqlException sqlEx)
            {
                return false;
            }

            if (sqlEx.Number is 2601 or 2627)
            {
                nombreIndice = sqlEx.Message;
                return true;
            }

            return false;
        }

        // ============================================================
        // VALIDAR INTEGRIDAD REFERENCIAL
        // ============================================================

        private static bool EsViolacionIntegridadReferencial(
            DbUpdateException ex)
        {
            if (ex?.InnerException is not SqlException sqlEx)
            {
                return false;
            }

            return sqlEx.Number is 547 or 515;
        }
    }
}