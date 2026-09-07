using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Administracion;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

public sealed class UsuariosApiService(AppDbContext context, ILogger<UsuariosApiService> logger, IHttpContextAccessor httpContextAccessor) : IUsuariosApiService
{
    private static readonly Regex PasswordRegex = new(@"^(?=.{8,100}$)(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d]).+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    private static readonly HashSet<string> RolesCreables = new(["Recepcionista", "Auxiliar"], StringComparer.OrdinalIgnoreCase);
    private readonly AppDbContext _context = context;
    private readonly ILogger<UsuariosApiService> _logger = logger;
    private readonly IHttpContextAccessor _http = httpContextAccessor;

    public async Task<UsuariosApiResult> ObtenerAsync(string? search, string? estado, int? idRol, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            IQueryable<Usuario> query = _context.Usuarios.AsNoTracking().Include(u => u.Rol);
            if (!string.IsNullOrWhiteSpace(search))
            {
                string text = search.Trim();
                query = query.Where(u => u.Nombre.Contains(text) || u.Apellidos.Contains(text) || u.Correo.Contains(text));
            }
            if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("todos", StringComparison.OrdinalIgnoreCase))
            {
                string normalized = estado.Trim().ToLowerInvariant();
                query = normalized == "bloqueado" ? query.Where(u => u.IntentosFallidos >= 3) : query.Where(u => u.Estado == normalized);
            }
            if (idRol is > 0) query = query.Where(u => u.IdRol == idRol);
            int total = await query.CountAsync(ct);
            var users = await query.OrderBy(u => u.Apellidos).ThenBy(u => u.Nombre).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return UsuariosApiResult.List(users.Select(u => Map(u)).ToList(), total, page, pageSize);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error listando usuarios administrativos.");
            return UsuariosApiResult.Fail("No fue posible listar los usuarios.", 500);
        }
    }

    public async Task<UsuarioApiDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var user = await _context.Usuarios.AsNoTracking().Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
        return user is null ? null : Map(user);
    }

    public async Task<UsuariosApiResult> CrearAsync(UsuarioApiCreateDto dto, int? operadorId, CancellationToken ct = default)
    {
        if (!PasswordRegex.IsMatch(dto.Contrasena)) return UsuariosApiResult.Fail("La contraseña debe incluir mínimo 8 caracteres, una letra, un número y un símbolo.");
        string correo = dto.Correo.Trim().ToLowerInvariant();
        try
        {
            if (await _context.Usuarios.AnyAsync(u => u.Correo == correo, ct)) return UsuariosApiResult.Fail("El correo ya está registrado.", 409);
            var rol = await _context.Roles.FirstOrDefaultAsync(r => r.IdRol == dto.IdRol, ct);
            if (rol is null) return UsuariosApiResult.Fail("El rol seleccionado no existe.", 404);
            if (!RolesCreables.Contains(rol.NombreRol)) return UsuariosApiResult.Fail("Profesionales y pacientes se crean desde sus propios módulos; el administrador inicial se gestiona por configuración.");
            var user = new Usuario { Nombre = dto.Nombre.Trim(), Apellidos = dto.Apellidos.Trim(), Correo = correo, Contrasena = BCrypt.Net.BCrypt.HashPassword(dto.Contrasena, workFactor: 11), IdRol = rol.IdRol, Estado = dto.Estado?.Trim().ToLowerInvariant() == "inactivo" ? "inactivo" : "activo", CreadoPor = operadorId, FechaCreacion = DateTime.UtcNow };
            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync(ct);
            await AuditAsync("INSERT", user, null, "Usuario creado por API.", operadorId, ct);
            return UsuariosApiResult.Ok(Map(user, rol.NombreRol), "Usuario creado correctamente.");
        }
        catch (DbUpdateException ex) when (IsUnique(ex)) { return UsuariosApiResult.Fail("El correo ya está registrado.", 409); }
        catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Error creando usuario."); return UsuariosApiResult.Fail("No fue posible crear el usuario.", 500); }
    }

    public async Task<UsuariosApiResult> ActualizarAsync(int id, UsuarioApiUpdateDto dto, int? operadorId, CancellationToken ct = default)
    {
        try
        {
            var user = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
            if (user is null) return UsuariosApiResult.Fail("Usuario no encontrado.", 404);
            if (IsAdministrator(user)) return UsuariosApiResult.Fail("La cuenta de Administrador no se modifica desde este CRUD.", 403);
            string previous = Serialize(user);
            if (dto.Correo is not null)
            {
                string correo = dto.Correo.Trim().ToLowerInvariant();
                if (await _context.Usuarios.AnyAsync(u => u.IdUsuario != id && u.Correo == correo, ct)) return UsuariosApiResult.Fail("El correo ya pertenece a otro usuario.", 409);
                user.Correo = correo;
            }
            if (dto.Nombre is not null) user.Nombre = dto.Nombre.Trim();
            if (dto.Apellidos is not null) user.Apellidos = dto.Apellidos.Trim();
            if (dto.IdRol is > 0 && dto.IdRol != user.IdRol)
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.IdRol == dto.IdRol, ct);
                if (role is null) return UsuariosApiResult.Fail("El rol seleccionado no existe.", 404);
                if (!RolesCreables.Contains(role.NombreRol)) return UsuariosApiResult.Fail("Este rol no se puede asignar desde Gestión de Usuarios.");
                user.IdRol = role.IdRol;
                user.Rol = role;
            }
            await _context.SaveChangesAsync(ct);
            await AuditAsync("UPDATE", user, previous, "Usuario actualizado por API.", operadorId, ct);
            return UsuariosApiResult.Ok(Map(user), "Usuario actualizado correctamente.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Error actualizando usuario {IdUsuario}", id); return UsuariosApiResult.Fail("No fue posible actualizar el usuario.", 500); }
    }

    public async Task<UsuariosApiResult> CambiarEstadoAsync(int id, string estado, int? operadorId, CancellationToken ct = default)
    {
        var user = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
        if (user is null) return UsuariosApiResult.Fail("Usuario no encontrado.", 404);
        if (IsAdministrator(user)) return UsuariosApiResult.Fail("La cuenta de Administrador no se desactiva desde este CRUD.", 403);
        string previous = Serialize(user);
        user.Estado = estado.Trim().ToLowerInvariant();
        if (user.Estado == "activo") user.IntentosFallidos = 0;
        await _context.SaveChangesAsync(ct);
        await AuditAsync("UPDATE", user, previous, $"Estado cambiado a {user.Estado} por API.", operadorId, ct);
        return UsuariosApiResult.Ok(Map(user), "Estado actualizado correctamente.");
    }

    public async Task<UsuariosApiResult> RestablecerContrasenaAsync(int id, string contrasenaTemporal, int? operadorId, CancellationToken ct = default)
    {
        if (!PasswordRegex.IsMatch(contrasenaTemporal)) return UsuariosApiResult.Fail("La contraseña debe incluir mínimo 8 caracteres, una letra, un número y un símbolo.");
        var user = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
        if (user is null) return UsuariosApiResult.Fail("Usuario no encontrado.", 404);
        if (IsAdministrator(user)) return UsuariosApiResult.Fail("La contraseña de Administrador no se modifica desde este CRUD.", 403);
        string previous = Serialize(user);
        user.Contrasena = BCrypt.Net.BCrypt.HashPassword(contrasenaTemporal, workFactor: 11);
        user.IntentosFallidos = 0;
        user.UltimoLogout = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        await AuditAsync("UPDATE", user, previous, "Contraseña restablecida por API.", operadorId, ct);
        return UsuariosApiResult.Ok(Map(user), "Contraseña restablecida y sesiones anteriores invalidadas.");
    }

    public async Task<UsuariosApiResult> DesbloquearAsync(int id, int? operadorId, CancellationToken ct = default)
    {
        var user = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
        if (user is null) return UsuariosApiResult.Fail("Usuario no encontrado.", 404);
        if (IsAdministrator(user)) return UsuariosApiResult.Fail("La cuenta de Administrador no se desbloquea desde este CRUD.", 403);
        if (user.IntentosFallidos < 3) return UsuariosApiResult.Fail("El usuario no se encuentra bloqueado.");
        string previous = Serialize(user);
        user.IntentosFallidos = 0;
        await _context.SaveChangesAsync(ct);
        await AuditAsync("UPDATE", user, previous, "Usuario desbloqueado por API.", operadorId, ct);
        return UsuariosApiResult.Ok(Map(user), "Usuario desbloqueado correctamente.");
    }

    public async Task<List<RolDisponibleApiDto>> ObtenerRolesAsync(CancellationToken ct = default) => await _context.Roles.AsNoTracking().OrderBy(r => r.NombreRol).Select(r => new RolDisponibleApiDto { IdRol = r.IdRol, Nombre = r.NombreRol, Descripcion = r.Descripcion, UsuariosAsignados = _context.Usuarios.Count(u => u.IdRol == r.IdRol) }).ToListAsync(ct);

    private static bool IsAdministrator(Usuario user) => user.Rol?.NombreRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase) == true;
    private static bool IsUnique(DbUpdateException ex) => ex.InnerException is SqlException sql && sql.Number is 2601 or 2627;
    private static UsuarioApiDto Map(Usuario u, string? role = null) => new() { IdUsuario = u.IdUsuario, Nombre = u.Nombre, Apellidos = u.Apellidos, NombreCompleto = $"{u.Nombre} {u.Apellidos}".Trim(), Correo = u.Correo, IdRol = u.IdRol, Rol = role ?? u.Rol?.NombreRol ?? "Sin rol", Estado = u.Estado, IntentosFallidos = u.IntentosFallidos, EstaBloqueado = u.IntentosFallidos >= 3, FechaCreacion = u.FechaCreacion, UltimoLogin = u.UltimoLogin };
    private static string Serialize(Usuario u) => JsonSerializer.Serialize(new { u.IdUsuario, u.Nombre, u.Apellidos, u.Correo, u.IdRol, u.Estado, u.IntentosFallidos });
    private async Task AuditAsync(string action, Usuario user, string? previous, string description, int? operatorId, CancellationToken ct)
    {
        _context.Auditorias.Add(new Auditoria { IdUsuario = operatorId, TablaAfectada = "Usuario", IdRegistro = user.IdUsuario, Accion = action, IpOrigen = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(), DatosAnteriores = previous, DatosNuevos = Serialize(user), Descripcion = description, Fecha = DateTime.UtcNow });
        await _context.SaveChangesAsync(ct);
    }
}
