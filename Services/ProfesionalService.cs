using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Services;

public partial class ProfesionalService : IProfesionalService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProfesionalService> _logger;

    // ── Reglas de negocio (idénticas al MVC controller) ──────────────────────

    private static readonly Regex PasswordRegex = new(
        @"^(?=.{8,100}$)(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    [GeneratedRegex(@"^[A-Za-z0-9\-\. ]+$")]
    private static partial Regex RegistroMedicoRegex();

    private static bool EsTelefonoValido(string? telefono)
    {
        // El teléfono es opcional; si está vacío o nulo se considera válido.
        if (string.IsNullOrWhiteSpace(telefono)) return true;
        string digits = new(telefono.Where(char.IsDigit).ToArray());
        return digits.Length is >= 7 and <= 15;
    }

    private static bool EsRegistroMedicoValido(string? registro)
    {
        if (string.IsNullOrWhiteSpace(registro)) return false;
        registro = registro.Trim();
        if (registro.Length < 3 || registro.Length > 30) return false;
        return RegistroMedicoRegex().IsMatch(registro);
    }

    private static readonly HashSet<string> EstadosPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "activo", "vacaciones", "inactivo" };

    // ─────────────────────────────────────────────────────────────────────────

    public ProfesionalService(AppDbContext context, ILogger<ProfesionalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Listado paginado ──────────────────────────────────────────────────────

    public async Task<ProfesionalesApiResult> ObtenerAsync(
        int page,
        int pageSize,
        string? search,
        string? especialidad,
        string? estado,
        CancellationToken ct = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Profesionales
                .Include(p => p.Usuario)
                .Include(p => p.Especialidades)
                    .ThenInclude(pe => pe.Especialidad)
                .AsNoTracking()
                .AsQueryable();

            // ── Filtros ejecutados en SQL Server ──
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(p =>
                    (p.Nombres != null && p.Nombres.Contains(s)) ||
                    (p.Apellidos != null && p.Apellidos.Contains(s)) ||
                    (p.RegistroMedico != null && p.RegistroMedico.Contains(s)) ||
                    (p.Usuario != null &&
                        ((p.Usuario.Nombre != null && p.Usuario.Nombre.Contains(s)) ||
                         (p.Usuario.Apellidos != null && p.Usuario.Apellidos.Contains(s)))) ||
                    p.Especialidades.Any(pe =>
                        pe.Especialidad != null && pe.Especialidad.Nombre.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(especialidad))
            {
                string esp = especialidad.Trim();
                query = query.Where(p =>
                    p.Especialidades.Any(pe =>
                        pe.Especialidad != null && pe.Especialidad.Nombre == esp));
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                string est = estado.Trim().ToLowerInvariant();
                query = query.Where(p => p.Estado == est);
            }

            query = query.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres);

            int total = await query.CountAsync(ct);

            var items = total == 0
                ? []
                : await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

            var dtos = items.Select(MapToDto).ToList();
            return ProfesionalesApiResult.Ok(dtos, total, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            return ProfesionalesApiResult.Fail("Operación cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerAsync (página={Page}, búsqueda={Search})", page, search);
            return ProfesionalesApiResult.Fail("Error interno al obtener profesionales.");
        }
    }

    // ── Obtener por ID ────────────────────────────────────────────────────────

    public async Task<ProfesionalApiDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var profesional = await _context.Profesionales
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProfesional == id, ct);

        return profesional is null ? null : MapToDto(profesional);
    }

    // ── Especialidades ────────────────────────────────────────────────────────

    public async Task<List<ProfesionalEspecialidadApiDto>> ObtenerEspecialidadesAsync(
        CancellationToken ct = default)
    {
        return await _context.Especialidades
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new ProfesionalEspecialidadApiDto
            {
                IdEspecialidad = e.IdEspecialidad,
                Nombre = e.Nombre
            })
            .ToListAsync(ct);
    }

    // ── Crear ─────────────────────────────────────────────────────────────────

    public async Task<ProfesionalApiOperationResult> CrearAsync(
        ProfesionalApiRequest request,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default)
    {
        // ── Validaciones de entrada ──
        var (valido, errorMsg) = ValidarRequest(request, esCreacion: true);
        if (!valido)
            return ProfesionalApiOperationResult.Fail(errorMsg!, 422);

        string nombres = request.Nombres.Trim();
        string apellidos = request.Apellidos.Trim();
        string registro = request.RegistroMedico.Trim();
        string correo = request.CorreoAcceso.Trim();
        string password = request.ContrasenaAcceso!;

        try
        {
            // Verificar registro médico duplicado
            bool regDuplicado = await _context.Profesionales
                .AnyAsync(p => p.RegistroMedico == registro, ct);
            if (regDuplicado)
                return ProfesionalApiOperationResult.Conflict(
                    $"El registro médico '{registro}' ya está asignado a otro profesional.");

            Profesional? profesional = null;
            Usuario? usuario = null;

            var estrategia = _context.Database.CreateExecutionStrategy();

            await estrategia.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    var rolProfesional = await _context.Roles
                        .FirstOrDefaultAsync(r => r.NombreRol == "Profesional", ct)
                        ?? throw new InvalidOperationException("No existe el rol Profesional en la base de datos.");

                    bool correoExiste = await _context.Usuarios
                        .AnyAsync(u => u.Correo == correo, ct);
                    if (correoExiste)
                        throw new CorreoDuplicadoException();

                    usuario = new Usuario
                    {
                        Nombre = nombres,
                        Apellidos = apellidos,
                        Correo = correo,
                        Contrasena = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
                        IdRol = rolProfesional.IdRol,
                        Estado = "activo",
                        CreadoPor = operadorId,
                        FechaCreacion = DateTime.UtcNow,
                        IntentosFallidos = 0
                    };

                    _context.Usuarios.Add(usuario);
                    await _context.SaveChangesAsync(ct);

                    profesional = new Profesional
                    {
                        IdUsuario = usuario.IdUsuario,
                        Nombres = nombres,
                        Apellidos = apellidos,
                        RegistroMedico = registro,
                        Categoria = request.Categoria?.Trim(),
                        Telefono = request.Telefono?.Trim(),
                        Descripcion = request.Descripcion?.Trim(),
                        Estado = "activo",
                        FechaIngreso = DateTime.Today
                    };

                    _context.Profesionales.Add(profesional);
                    await _context.SaveChangesAsync(ct);

                    // Especialidad
                    if (request.IdEspecialidad is > 0)
                    {
                        bool espExiste = await _context.Especialidades
                            .AnyAsync(e => e.IdEspecialidad == request.IdEspecialidad.Value, ct);
                        if (!espExiste)
                            throw new InvalidOperationException("La especialidad seleccionada no existe.");

                        _context.ProfesionalEspecialidades.Add(new Profesional_Especialidad
                        {
                            IdProfesional = profesional.IdProfesional,
                            IdEspecialidad = request.IdEspecialidad.Value,
                            Principal = true
                        });
                        await _context.SaveChangesAsync(ct);
                    }

                    // Auditoría
                    _context.Auditorias.Add(new Auditoria
                    {
                        IdUsuario = operadorId,
                        TablaAfectada = "Profesional",
                        IdRegistro = profesional.IdProfesional,
                        Accion = "INSERT",
                        IpOrigen = ipOrigen,
                        DatosNuevos = JsonSerializer.Serialize(new
                        {
                            profesional.IdProfesional,
                            profesional.IdUsuario,
                            profesional.Nombres,
                            profesional.Apellidos,
                            profesional.RegistroMedico,
                            profesional.Estado,
                            Correo = correo,
                            Rol = "Profesional",
                            request.IdEspecialidad
                        }),
                        Descripcion = $"Profesional creado vía API REST. IdProfesional={profesional.IdProfesional}",
                        Fecha = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            // Recargar con navegación para devolver DTO completo
            var dto = await ObtenerPorIdAsync(profesional!.IdProfesional, ct);
            return ProfesionalApiOperationResult.Ok(
                "El profesional y su cuenta de acceso fueron creados correctamente.", dto);
        }
        catch (CorreoDuplicadoException)
        {
            return ProfesionalApiOperationResult.Conflict(
                "El correo de acceso ya está registrado. Use otro correo.");
        }
        catch (InvalidOperationException ioex)
        {
            return ProfesionalApiOperationResult.Fail(ioex.Message, 422);
        }
        catch (DbUpdateException dbex) when (EsViolacionIndiceUnico(dbex))
        {
            _logger.LogError(dbex, "UNIQUE violation al crear profesional");
            return ProfesionalApiOperationResult.Conflict(
                "No se pudo guardar: dato duplicado (registro médico o correo).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear profesional");
            return ProfesionalApiOperationResult.Fail("Error interno al crear el profesional.", 500);
        }
    }

    // ── Actualizar ────────────────────────────────────────────────────────────

    public async Task<ProfesionalApiOperationResult> ActualizarAsync(
        int id,
        ProfesionalApiRequest request,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default)
    {
        var (valido, errorMsg) = ValidarRequest(request, esCreacion: false);
        if (!valido)
            return ProfesionalApiOperationResult.Fail(errorMsg!, 422);

        string nombres = request.Nombres.Trim();
        string apellidos = request.Apellidos.Trim();
        string registro = request.RegistroMedico.Trim();
        string correo = request.CorreoAcceso.Trim();

        try
        {
            // Verificar registro médico duplicado (excluyendo el actual)
            bool regDuplicado = await _context.Profesionales
                .AnyAsync(p => p.RegistroMedico == registro && p.IdProfesional != id, ct);
            if (regDuplicado)
                return ProfesionalApiOperationResult.Conflict(
                    $"El registro médico '{registro}' ya está asignado a otro profesional.");

            Profesional? profesional = null;

            var estrategia = _context.Database.CreateExecutionStrategy();

            await estrategia.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    profesional = await _context.Profesionales
                        .Include(p => p.Usuario)
                        .Include(p => p.Especialidades)
                        .FirstOrDefaultAsync(p => p.IdProfesional == id, ct)
                        ?? throw new ProfesionalNotFoundException();

                    var usuario = profesional.Usuario
                        ?? throw new InvalidOperationException(
                            "El profesional no tiene una cuenta de acceso vinculada.");

                    var rolProfesional = await _context.Roles
                        .FirstOrDefaultAsync(r => r.NombreRol == "Profesional", ct)
                        ?? throw new InvalidOperationException("No existe el rol Profesional en la base de datos.");

                    bool correoDuplicado = await _context.Usuarios
                        .AnyAsync(u => u.IdUsuario != usuario.IdUsuario && u.Correo == correo, ct);
                    if (correoDuplicado)
                        throw new CorreoDuplicadoException();

                    if (usuario.IdRol != rolProfesional.IdRol)
                        throw new InvalidOperationException(
                            "La cuenta vinculada al profesional no tiene el rol Profesional.");

                    // Actualizar usuario
                    usuario.Nombre = nombres;
                    usuario.Apellidos = apellidos;
                    usuario.Correo = correo;

                    if (!string.IsNullOrWhiteSpace(request.ContrasenaAcceso))
                    {
                        if (!PasswordRegex.IsMatch(request.ContrasenaAcceso))
                            throw new InvalidOperationException(
                                "La contraseña debe tener mínimo 8 caracteres, mayúscula, minúscula, número y símbolo especial.");

                        usuario.Contrasena = BCrypt.Net.BCrypt.HashPassword(
                            request.ContrasenaAcceso, workFactor: 11);
                        usuario.IntentosFallidos = 0;
                    }

                    // Actualizar profesional
                    profesional.Nombres = nombres;
                    profesional.Apellidos = apellidos;
                    profesional.RegistroMedico = registro;
                    profesional.Categoria = request.Categoria?.Trim();
                    profesional.Telefono = request.Telefono?.Trim();
                    profesional.Descripcion = request.Descripcion?.Trim();
                    profesional.FechaIngreso ??= DateTime.Today;

                    // Sincronizar estado de la cuenta
                    usuario.Estado = profesional.Estado == "inactivo" ? "inactivo" : "activo";

                    await _context.SaveChangesAsync(ct);

                    // Actualizar especialidad
                    var relaciones = await _context.ProfesionalEspecialidades
                        .Where(pe => pe.IdProfesional == id)
                        .ToListAsync(ct);

                    if (relaciones.Count > 0)
                        _context.ProfesionalEspecialidades.RemoveRange(relaciones);

                    if (request.IdEspecialidad is > 0)
                    {
                        bool espExiste = await _context.Especialidades
                            .AnyAsync(e => e.IdEspecialidad == request.IdEspecialidad.Value, ct);
                        if (!espExiste)
                            throw new InvalidOperationException("La especialidad seleccionada no existe.");

                        _context.ProfesionalEspecialidades.Add(new Profesional_Especialidad
                        {
                            IdProfesional = id,
                            IdEspecialidad = request.IdEspecialidad.Value,
                            Principal = true
                        });
                    }

                    await _context.SaveChangesAsync(ct);

                    // Auditoría
                    _context.Auditorias.Add(new Auditoria
                    {
                        IdUsuario = operadorId,
                        TablaAfectada = "Profesional",
                        IdRegistro = id,
                        Accion = "UPDATE",
                        IpOrigen = ipOrigen,
                        DatosNuevos = JsonSerializer.Serialize(new
                        {
                            profesional.IdProfesional,
                            profesional.Nombres,
                            profesional.Apellidos,
                            profesional.RegistroMedico,
                            profesional.Estado,
                            Correo = correo,
                            request.IdEspecialidad
                        }),
                        Descripcion = $"Profesional actualizado vía API REST. IdProfesional={id}",
                        Fecha = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            var dto = await ObtenerPorIdAsync(profesional!.IdProfesional, ct);
            return ProfesionalApiOperationResult.Ok(
                "El profesional y su cuenta de acceso fueron actualizados correctamente.", dto);
        }
        catch (ProfesionalNotFoundException)
        {
            return ProfesionalApiOperationResult.NotFound();
        }
        catch (CorreoDuplicadoException)
        {
            return ProfesionalApiOperationResult.Conflict(
                "El correo de acceso ya está registrado. Use otro correo.");
        }
        catch (InvalidOperationException ioex)
        {
            return ProfesionalApiOperationResult.Fail(ioex.Message, 422);
        }
        catch (DbUpdateException dbex) when (EsViolacionIndiceUnico(dbex))
        {
            _logger.LogError(dbex, "UNIQUE violation al actualizar profesional Id={Id}", id);
            return ProfesionalApiOperationResult.Conflict(
                "Dato duplicado al actualizar (registro médico o correo).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al actualizar profesional Id={Id}", id);
            return ProfesionalApiOperationResult.Fail("Error interno al actualizar el profesional.", 500);
        }
    }

    // ── Cambiar estado ────────────────────────────────────────────────────────

    public async Task<ProfesionalApiOperationResult> CambiarEstadoAsync(
        int id,
        string estado,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default)
    {
        string estadoNorm = estado.Trim().ToLowerInvariant();

        if (!EstadosPermitidos.Contains(estadoNorm))
            return ProfesionalApiOperationResult.Fail(
                $"Estado '{estado}' no es válido. Los estados permitidos son: activo, vacaciones, inactivo.", 422);

        try
        {
            Profesional? profesional = null;
            var estrategia = _context.Database.CreateExecutionStrategy();

            await estrategia.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    profesional = await _context.Profesionales
                        .Include(p => p.Usuario)
                        .FirstOrDefaultAsync(p => p.IdProfesional == id, ct)
                        ?? throw new ProfesionalNotFoundException();

                    string estadoAnterior = profesional.Estado;

                    // Si se va a inactivar, verificar citas pendientes
                    if (estadoNorm == "inactivo" && estadoAnterior != "inactivo")
                    {
                        bool tieneCitasActivas = await _context.Citas
                            .AnyAsync(c =>
                                c.IdProfesional == id &&
                                c.Estado != "Cancelada" &&
                                c.FechaHora >= DateTime.Today, ct);

                        if (tieneCitasActivas)
                            throw new InvalidOperationException("No se puede inactivar: este profesional tiene citas agendadas pendientes.");
                    }

                    profesional.Estado = estadoNorm;

                    // Sincronizar la cuenta de usuario
                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = estadoNorm == "inactivo" ? "inactivo" : "activo";

                    _context.Auditorias.Add(new Auditoria
                    {
                        IdUsuario = operadorId,
                        TablaAfectada = "Profesional",
                        IdRegistro = id,
                        Accion = "UPDATE",
                        IpOrigen = ipOrigen,
                        DatosAnteriores = JsonSerializer.Serialize(new { Estado = estadoAnterior }),
                        DatosNuevos = JsonSerializer.Serialize(new { Estado = estadoNorm }),
                        Descripcion = $"Cambio de estado vía API REST. IdProfesional={id}. De '{estadoAnterior}' a '{estadoNorm}'.",
                        Fecha = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            var dto = await ObtenerPorIdAsync(profesional!.IdProfesional, ct);
            return ProfesionalApiOperationResult.Ok(
                $"El estado del profesional fue actualizado a '{estadoNorm}'.", dto);
        }
        catch (ProfesionalNotFoundException)
        {
            return ProfesionalApiOperationResult.NotFound();
        }
        catch (InvalidOperationException ioex)
        {
            return ProfesionalApiOperationResult.Conflict(ioex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar estado profesional Id={Id}", id);
            return ProfesionalApiOperationResult.Fail("Error interno al cambiar el estado.", 500);
        }
    }

    // ── Desactivar (DELETE lógico) ─────────────────────────────────────────────

    public async Task<ProfesionalApiOperationResult> DesactivarAsync(
        int id,
        int? operadorId,
        string? ipOrigen,
        CancellationToken ct = default)
    {
        try
        {
            Profesional? profesional = null;
            var estrategia = _context.Database.CreateExecutionStrategy();

            await estrategia.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    profesional = await _context.Profesionales
                        .Include(p => p.Usuario)
                        .FirstOrDefaultAsync(p => p.IdProfesional == id, ct)
                        ?? throw new ProfesionalNotFoundException();

                    // Verificar citas pendientes (igual que el MVC controller)
                    bool tieneCitasActivas = await _context.Citas
                        .AnyAsync(c =>
                            c.IdProfesional == id &&
                            c.Estado != "Cancelada" &&
                            c.FechaHora >= DateTime.Today, ct);

                    if (tieneCitasActivas && profesional.Estado != "inactivo")
                        throw new InvalidOperationException("No se puede desactivar: este profesional tiene citas agendadas pendientes.");

                    string estadoAnterior = profesional.Estado;
                    profesional.Estado = "inactivo";

                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = "inactivo";

                    _context.Auditorias.Add(new Auditoria
                    {
                        IdUsuario = operadorId,
                        TablaAfectada = "Profesional",
                        IdRegistro = id,
                        Accion = "DELETE",
                        IpOrigen = ipOrigen,
                        DatosAnteriores = JsonSerializer.Serialize(new
                        {
                            profesional.IdProfesional,
                            profesional.RegistroMedico,
                            EstadoAnterior = estadoAnterior
                        }),
                        DatosNuevos = JsonSerializer.Serialize(new { Estado = "inactivo" }),
                        Descripcion = $"Baja lógica vía API REST. IdProfesional={id}. EstadoAnterior='{estadoAnterior}'.",
                        Fecha = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });

            return ProfesionalApiOperationResult.Ok("El profesional fue desactivado exitosamente.");
        }
        catch (ProfesionalNotFoundException)
        {
            return ProfesionalApiOperationResult.NotFound();
        }
        catch (InvalidOperationException ioex)
        {
            return ProfesionalApiOperationResult.Conflict(ioex.Message);
        }
        catch (DbUpdateException dbex) when (EsViolacionIntegridadReferencial(dbex))
        {
            _logger.LogError(dbex, "Integridad referencial al desactivar profesional Id={Id}", id);
            return ProfesionalApiOperationResult.Fail(
                "No se puede desactivar: el profesional tiene registros dependientes activos.", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al desactivar profesional Id={Id}", id);
            return ProfesionalApiOperationResult.Fail("Error interno al desactivar el profesional.", 500);
        }
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    private static ProfesionalApiDto MapToDto(Profesional p) => new()
    {
        IdProfesional = p.IdProfesional,
        IdUsuario = p.IdUsuario,
        Nombres = p.Nombres,
        Apellidos = p.Apellidos,
        RegistroMedico = p.RegistroMedico,
        Categoria = p.Categoria,
        Telefono = p.Telefono,
        Descripcion = p.Descripcion,
        Estado = p.Estado,
        FechaIngreso = p.FechaIngreso,
        CorreoAcceso = p.Usuario?.Correo,
        Especialidades = p.Especialidades
            .Where(pe => pe.Especialidad != null)
            .Select(pe => new ProfesionalEspecialidadApiDto
            {
                IdEspecialidad = pe.Especialidad!.IdEspecialidad,
                Nombre = pe.Especialidad.Nombre
            })
            .ToList()
    };

    private static (bool valido, string? error) ValidarRequest(
        ProfesionalApiRequest request, bool esCreacion)
    {
        if (string.IsNullOrWhiteSpace(request.Nombres) || request.Nombres.Trim().Length < 2)
            return (false, "Los nombres deben contener al menos 2 caracteres.");

        if (string.IsNullOrWhiteSpace(request.Apellidos) || request.Apellidos.Trim().Length < 2)
            return (false, "Los apellidos deben contener al menos 2 caracteres.");

        if (!EsRegistroMedicoValido(request.RegistroMedico))
            return (false, "El registro médico tiene un formato inválido. Use letras, números y guiones (3-30 caracteres).");

        if (!string.IsNullOrWhiteSpace(request.Telefono) && !EsTelefonoValido(request.Telefono))
            return (false, "El número de teléfono es inválido. Debe contener entre 7 y 15 dígitos.");

        if (string.IsNullOrWhiteSpace(request.CorreoAcceso))
            return (false, "El correo de acceso es requerido.");

        if (esCreacion)
        {
            if (string.IsNullOrWhiteSpace(request.ContrasenaAcceso) ||
                !PasswordRegex.IsMatch(request.ContrasenaAcceso))
                return (false, "La contraseña inicial debe tener mínimo 8 caracteres, mayúscula, minúscula, número y símbolo especial.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.ContrasenaAcceso) &&
                !PasswordRegex.IsMatch(request.ContrasenaAcceso))
                return (false, "La contraseña debe tener mínimo 8 caracteres, mayúscula, minúscula, número y símbolo especial.");
        }

        return (true, null);
    }

    private static bool EsViolacionIndiceUnico(DbUpdateException dbex)
    {
        var sqlEx = dbex.InnerException as SqlException
            ?? dbex.InnerException?.InnerException as SqlException;
        return sqlEx?.Number is 2601 or 2627;
    }

    private static bool EsViolacionIntegridadReferencial(DbUpdateException dbex)
    {
        var sqlEx = dbex.InnerException as SqlException
            ?? dbex.InnerException?.InnerException as SqlException;
        return sqlEx?.Number is 547 or 515;
    }

    // ── Excepciones internas ──────────────────────────────────────────────────

    private sealed class CorreoDuplicadoException : Exception { }
    private sealed class ProfesionalNotFoundException : Exception { }
}