using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

public sealed class UsuarioAdminService(
    AppDbContext context,
    ILogger<UsuarioAdminService> logger,
    IHttpContextAccessor httpContextAccessor) : IUsuarioAdminService
{
    private static readonly Regex PasswordRegex = new(
        @"^(?=.{8,100}$)(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d]).+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private static bool EsPasswordValida(string? password)
        => !string.IsNullOrWhiteSpace(password) && (password == "123456" || PasswordRegex.IsMatch(password));

    private readonly AppDbContext _context = context;
    private readonly ILogger<UsuarioAdminService> _logger = logger;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private static readonly HashSet<string> RolesInternosCreables = new(
        ["Recepcionista", "Auxiliar"],
        StringComparer.OrdinalIgnoreCase);

public async Task<(bool Success, string Message, object? Data)> CrearAsync(
    CrearUsuarioAdminRequest request,
    int? creadoPor,
    CancellationToken ct = default)
{
    string correo = request.Correo.Trim();
    string nombre = request.Nombre.Trim();
    string apellidos = request.Apellidos.Trim();

    if (!EsPasswordValida(request.Contrasena))
    {
        return (
            false,
            "La contraseña debe tener al menos 8 caracteres, una letra, un número y un símbolo.",
            null);
    }

    var rol = await _context.Roles
        .AsNoTracking()
        .FirstOrDefaultAsync(
            r => r.IdRol == request.IdRol,
            ct);

    if (rol == null)
    {
        return (
            false,
            "El rol seleccionado no existe.",
            null);
    }

    if (!RolesInternosCreables.Contains(rol.NombreRol))
    {
        return (
            false,
            "Los usuarios Profesional y Paciente se crean desde sus módulos respectivos para garantizar la vinculación de sus perfiles. El Administrador inicial se gestiona por configuración.",
            null);
    }

    bool correoExiste = await _context.Usuarios
        .AnyAsync(
            u => u.Correo == correo,
            ct);

    if (correoExiste)
    {
        return (
            false,
            "El correo ya se encuentra registrado.",
            null);
    }

    string estado =
        NormalizarEstado(request.Estado);

    var strategy =
        _context.Database.CreateExecutionStrategy();

    try
    {
        return await strategy.ExecuteAsync(
            async () =>
            {
                await using var tx =
                    await _context.Database
                        .BeginTransactionAsync(ct);

                try
                {
                    var usuario = new Usuario
                    {
                        Nombre = nombre,
                        Apellidos = apellidos,
                        Correo = correo,
                        Contrasena =
                            BCrypt.Net.BCrypt.HashPassword(
                                request.Contrasena,
                                workFactor: 11),
                        IdRol = rol.IdRol,
                        Estado = estado,
                        CreadoPor = creadoPor,
                        FechaCreacion = DateTime.UtcNow,
                        IntentosFallidos = 0
                    };

                    _context.Usuarios.Add(usuario);

                    await _context.SaveChangesAsync(ct);

                    await RegistrarAuditoriaAsync(
                        "INSERT",
                        usuario.IdUsuario,
                        null,
                        SerializeUsuario(
                            usuario,
                            rol.NombreRol),
                        $"Usuario creado por administración. Rol={rol.NombreRol}, Correo={usuario.Correo}",
                        creadoPor,
                        ct);

                    await tx.CommitAsync(ct);

                    _logger.LogInformation(
                        "Usuario creado. IdUsuario={IdUsuario}, Correo={Correo}, Rol={Rol}, CreadoPor={CreadoPor}",
                        usuario.IdUsuario,
                        usuario.Correo,
                        rol.NombreRol,
                        creadoPor);

                    return (
                        true,
                        "Usuario creado correctamente.",
                        MapUsuario(
                            usuario,
                            rol.NombreRol));
                }
                catch
                {
                    try
                    {
                        await tx.RollbackAsync(ct);
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogWarning(
                            rollbackEx,
                            "No se pudo hacer rollback de la transacción al crear usuario. Correo={Correo}",
                            correo);
                    }

                    throw;
                }
            });
    }
    catch (DbUpdateException ex) when (EsUnique(ex))
    {
        _logger.LogWarning(
            ex,
            "Intento de crear usuario con correo duplicado. Correo={Correo}",
            correo);

        return (
            false,
            "El correo ya se encuentra registrado.",
            null);
    }
    catch (SqlException ex)
    {
        _logger.LogError(
            ex,
            "Error SQL creando usuario. Correo={Correo}",
            correo);

        return (
            false,
            "No se pudo guardar el usuario en la base de datos.",
            null);
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogError(
            ex,
            "Error de operación creando usuario. Correo={Correo}",
            correo);

        return (
            false,
            "No se pudo completar la creación del usuario.",
            null);
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning(
            "Creación de usuario cancelada. Correo={Correo}",
            correo);

        return (
            false,
            "La operación fue cancelada.",
            null);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error inesperado creando usuario. Correo={Correo}",
            correo);

        return (
            false,
            "No se pudo crear el usuario.",
            null);
    }
}
    public async Task<(bool Success, string Message, object? Data)> ActualizarAsync(
        int idUsuario,
        ActualizarUsuarioAdminRequest request,
        int? usuarioOperador,
        CancellationToken ct = default)
    {
        if (idUsuario <= 0)
        {
            return (false, "Identificador de usuario inválido.", null);
        }

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, ct);

        if (usuario == null)
        {
            return (false, "Usuario no encontrado.", null);
        }

        var rolActual = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRol == usuario.IdRol, ct);

        var rolNuevo = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRol == request.IdRol, ct);

        if (rolNuevo == null)
        {
            return (false, "El rol seleccionado no existe.", null);
        }

        // No permitir crear/convertir cuentas hacia Administrador desde el CRUD general.
        if (rolNuevo.NombreRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "El rol Administrador no se puede asignar desde este CRUD.", null);
        }

        // No permitir quitar o cambiar el rol del único administrador mediante este CRUD.
        if (rolActual?.NombreRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (false, "La cuenta de Administrador no se modifica desde este CRUD general.", null);
        }

        string correo = request.Correo.Trim();

        bool correoDuplicado = await _context.Usuarios
            .AnyAsync(u => u.IdUsuario != idUsuario && u.Correo == correo, ct);

        if (correoDuplicado)
        {
            return (false, "El correo ya pertenece a otro usuario.", null);
        }

        string estado = NormalizarEstado(request.Estado);
        string datosAnteriores = SerializeUsuario(usuario, rolActual?.NombreRol ?? "Sin Rol");

        usuario.Nombre = request.Nombre.Trim();
        usuario.Apellidos = request.Apellidos.Trim();
        usuario.Correo = correo;
        usuario.IdRol = rolNuevo.IdRol;
        usuario.Estado = estado;

        if (!string.IsNullOrWhiteSpace(request.Contrasena))
        {
            if (!EsPasswordValida(request.Contrasena))
            {
                return (false, "La nueva contraseña debe tener al menos 8 caracteres, una letra, un número y un símbolo.", null);
            }

            usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(request.Contrasena, workFactor: 11);
            usuario.IntentosFallidos = 0;
        }

        if (estado == "activo")
        {
            usuario.IntentosFallidos = 0;
        }

        try
        {
            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                "UPDATE",
                usuario.IdUsuario,
                datosAnteriores,
                SerializeUsuario(usuario, rolNuevo.NombreRol),
                $"Usuario actualizado por administración. Rol={rolNuevo.NombreRol}, Correo={usuario.Correo}",
                usuarioOperador,
                ct);

            return (true, "Usuario actualizado correctamente.", MapUsuario(usuario, rolNuevo.NombreRol));
        }
        catch (DbUpdateException ex) when (EsUnique(ex))
        {
            return (false, "El correo ya pertenece a otro usuario.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando usuario IdUsuario={IdUsuario}", idUsuario);
            return (false, "No se pudo actualizar el usuario.", null);
        }
    }

    public async Task<(bool Success, string Message, object? Data)> CambiarEstadoAsync(
        int idUsuario,
        CambiarEstadoUsuarioRequest request,
        int? usuarioOperador,
        CancellationToken ct = default)
    {
        if (idUsuario <= 0)
        {
            return (false, "Identificador de usuario inválido.", null);
        }

        string estado = NormalizarEstado(request.Estado);
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, ct);

        if (usuario == null)
        {
            return (false, "Usuario no encontrado.", null);
        }

        var rol = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRol == usuario.IdRol, ct);

        if (rol?.NombreRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (false, "La cuenta de Administrador no se puede desactivar desde este CRUD.", null);
        }

        string datosAnteriores = SerializeUsuario(usuario, rol?.NombreRol ?? "Sin Rol");

        usuario.Estado = estado;
        if (estado == "activo")
        {
            usuario.IntentosFallidos = 0;
        }

        try
        {
            await _context.SaveChangesAsync(ct);

            await RegistrarAuditoriaAsync(
                "UPDATE",
                usuario.IdUsuario,
                datosAnteriores,
                SerializeUsuario(usuario, rol?.NombreRol ?? "Sin Rol"),
                $"Estado de usuario cambiado a '{estado}' por administración.",
                usuarioOperador,
                ct);

            return (true, $"Usuario {(estado == "activo" ? "activado" : "desactivado")} correctamente.", MapUsuario(usuario, rol?.NombreRol ?? "Sin Rol"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cambiando estado de usuario IdUsuario={IdUsuario}", idUsuario);
            return (false, "No se pudo cambiar el estado del usuario.", null);
        }
    }

    private static string NormalizarEstado(string? estado)
    {
        return estado?.Trim().ToLowerInvariant() == "inactivo"
            ? "inactivo"
            : "activo";
    }

    private object MapUsuario(Usuario u, string rol)
    {
        string initials = string.Concat(
                u.Nombre.Trim().FirstOrDefault(),
                u.Apellidos.Trim().FirstOrDefault())
            .ToUpperInvariant();

        return new
        {
            id = u.IdUsuario,
            name = $"{u.Nombre} {u.Apellidos}".Trim(),
            initials,
            email = u.Correo,
            role = rol,
            status = u.IntentosFallidos >= 3
                ? "Bloqueado"
                : string.Equals(u.Estado, "activo", StringComparison.OrdinalIgnoreCase)
                    ? "Activo"
                    : "Inactivo",
            lastAccess = u.UltimoLogin,
            color = ColorRol(rol)
        };
    }

    private static string ColorRol(string? rol) => rol?.ToLowerInvariant() switch
    {
        "administrador" => "purple",
        "recepcionista" => "orange",
        "profesional" => "green",
        "auxiliar" => "pink",
        "paciente" => "blue",
        _ => "blue"
    };

    private static string SerializeUsuario(Usuario u, string rol) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            u.IdUsuario,
            u.Nombre,
            u.Apellidos,
            u.Correo,
            u.IdRol,
            Rol = rol,
            u.Estado,
            u.UltimoLogin,
            u.IntentosFallidos
        });

    private async Task RegistrarAuditoriaAsync(
        string accion,
        int idRegistro,
        string? datosAnteriores,
        string? datosNuevos,
        string descripcion,
        int? usuarioOperador,
        CancellationToken ct)
    {
        _context.Auditorias.Add(new Auditoria
        {
            IdUsuario = usuarioOperador,
            TablaAfectada = "Usuario",
            IdRegistro = idRegistro,
            Accion = accion,
            IpOrigen = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            DatosAnteriores = datosAnteriores,
            DatosNuevos = datosNuevos,
            Descripcion = descripcion,
            Fecha = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
    }

    private static bool EsUnique(DbUpdateException ex)
        => ex.InnerException is SqlException sqlEx &&
           (sqlEx.Number is 2601 or 2627);
}
