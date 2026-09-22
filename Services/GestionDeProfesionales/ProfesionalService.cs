using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Services;

public partial class ProfesionalService : IProfesionalService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProfesionalService> _logger;

    // ── Reglas de negocio — delegan al Helper centralizado (P-04, P-05) ─────

    // La validación de contraseña usa ProfesionalEstadoHelper.EsPasswordValida()
    // que encapsula la política única del sistema (mínimo 8 chars, mayúscula,
    // minúscula, dígito y símbolo). Si cambia la política, solo se toca el Helper.
    private static readonly HashSet<string> EstadosPermitidos =
        new(StringComparer.OrdinalIgnoreCase) { "activo", "vacaciones", "inactivo" };

    [GeneratedRegex(@"^[A-Za-z0-9\-\. ]+$")]
    private static partial Regex RegistroMedicoRegex();

    private static bool EsTelefonoValido(string? telefono)
    {
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

    // ── Vista MVC (paginación + filtros + estadísticas) ──────────────────────

    public async Task<(List<Profesional> Items, PagedResult<Profesional> Paginacion, ProfesionalesStats Stats)>
        ObtenerVistaMVCAsync(
            PaginationQuery q,
            CancellationToken ct = default)
    {
        var query = q ?? new PaginationQuery();
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 10 : query.PageSize;
        if (pageSize > 100) pageSize = 100;

        var stats = new ProfesionalesStats
        {
            StatTotal = await _context.Profesionales.CountAsync(ct),
            StatActivos = await _context.Profesionales.CountAsync(p => p.Estado == "activo", ct),
            StatVacaciones = await _context.Profesionales.CountAsync(p => p.Estado == "vacaciones", ct),
            StatInactivos = await _context.Profesionales.CountAsync(p => p.Estado == "inactivo", ct)
        };

        var profesionales = _context.Profesionales
            .Include(p => p.Usuario)
            .Include(p => p.Especialidades)
                .ThenInclude(pe => pe.Especialidad)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string s = query.Search.Trim();
            profesionales = profesionales.Where(p =>
                (p.Nombres != null && p.Nombres.Contains(s)) ||
                (p.Apellidos != null && p.Apellidos.Contains(s)) ||
                (p.RegistroMedico != null && p.RegistroMedico.Contains(s)) ||
                (p.Usuario != null &&
                    ((p.Usuario.Nombre != null && p.Usuario.Nombre.Contains(s)) ||
                     (p.Usuario.Apellidos != null && p.Usuario.Apellidos.Contains(s)))) ||
                p.Especialidades.Any(pe =>
                    pe.Especialidad != null && pe.Especialidad.Nombre.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(query.Profesional))
        {
            string esp = query.Profesional.Trim();
            profesionales = profesionales.Where(p =>
                p.Especialidades.Any(pe =>
                    pe.Especialidad != null && pe.Especialidad.Nombre == esp));
        }

        if (!string.IsNullOrWhiteSpace(query.Estado))
        {
            string est = ProfesionalEstadoHelper.NormalizarEstado(query.Estado);
            profesionales = profesionales.Where(p => p.Estado == est);
        }

        profesionales = profesionales.OrderBy(p => p.Apellidos).ThenBy(p => p.Nombres);

        var paged = await profesionales.ToPagedResultAsync(page, pageSize, ct);

        var items = paged.Items.Cast<Profesional>().ToList();

        return (items, paged, stats);
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
        string password = GenerarContrasenaTemporal();

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
                "El profesional y su cuenta de acceso fueron creados correctamente. La contraseña temporal segura fue generada automáticamente.", dto);
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
        string? estadoSolicitado = string.IsNullOrWhiteSpace(request.Estado)
            ? null
            : request.Estado.Trim().ToLowerInvariant();

        if (estadoSolicitado is not null && !EstadosPermitidos.Contains(estadoSolicitado))
            return ProfesionalApiOperationResult.Fail(
                "El estado debe ser activo, vacaciones o inactivo.", 422);

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
                        if (!ProfesionalEstadoHelper.EsPasswordValida(request.ContrasenaAcceso))
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

                    if (estadoSolicitado == "inactivo" && profesional.Estado != "inactivo")
                    {
                        bool tieneCitasActivas = await _context.Citas.AnyAsync(c =>
                            c.IdProfesional == id &&
                            !new[] { "cancelada", "cancelado" }.Contains(c.Estado.ToLower()) &&
                            c.FechaHora >= DateTime.Today, ct);
                        if (tieneCitasActivas)
                            throw new InvalidOperationException("No se puede inactivar: este profesional tiene citas agendadas pendientes.");
                    }

                    if (estadoSolicitado is not null)
                        profesional.Estado = estadoSolicitado;

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
                                c.Estado != "cancelada" &&
                                c.Estado != "Cancelado" &&
                                c.Estado != "cancelado" &&
                                c.FechaHora >= DateTime.Today, ct);

                        if (tieneCitasActivas)
                            throw new InvalidOperationException("No se puede inactivar: este profesional tiene citas agendadas pendientes.");
                    }

                    profesional.Estado = estadoNorm;

                    // Sincronizar la cuenta de usuario
                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = estadoNorm == "inactivo" ? "inactivo" : "activo";

                    // Sincronizar bloqueo en Bloqueo_Profesional según el estado
                    if (estadoNorm == "inactivo")
                    {
                        bool yaBloqueado = await _context.BloqueosProfesional
                            .AnyAsync(b => b.IdProfesional == id && b.FechaFin > DateTime.Now && b.Motivo == "Profesional inactivo / desactivado", ct);

                        if (!yaBloqueado)
                        {
                            _context.BloqueosProfesional.Add(new BloqueoProfesional
                            {
                                IdProfesional = id,
                                FechaInicio = DateTime.Now,
                                FechaFin = DateTime.Now.AddYears(5),
                                Motivo = "Profesional inactivo / desactivado",
                                AprobadoPor = operadorId
                            });
                        }
                    }
                    else if (estadoNorm == "activo")
                    {
                        var bloqueosInactivo = await _context.BloqueosProfesional
                            .Where(b => b.IdProfesional == id && b.Motivo == "Profesional inactivo / desactivado")
                            .ToListAsync(ct);

                        if (bloqueosInactivo.Count > 0)
                        {
                            _context.BloqueosProfesional.RemoveRange(bloqueosInactivo);
                        }
                    }

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
                            c.Estado != "cancelada" &&
                            c.Estado != "Cancelado" &&
                            c.Estado != "cancelado" &&
                            c.FechaHora >= DateTime.Today, ct);

                    if (tieneCitasActivas && profesional.Estado != "inactivo")
                        throw new InvalidOperationException("No se puede desactivar: este profesional tiene citas agendadas pendientes.");

                    string estadoAnterior = profesional.Estado;
                    profesional.Estado = "inactivo";

                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = "inactivo";

                    // Registrar bloqueo en Bloqueo_Profesional
                    bool yaBloqueado = await _context.BloqueosProfesional
                        .AnyAsync(b => b.IdProfesional == id && b.FechaFin > DateTime.Now && b.Motivo == "Profesional inactivo / desactivado", ct);

                    if (!yaBloqueado)
                    {
                        _context.BloqueosProfesional.Add(new BloqueoProfesional
                        {
                            IdProfesional = id,
                            FechaInicio = DateTime.Now,
                            FechaFin = DateTime.Now.AddYears(5),
                            Motivo = "Profesional inactivo / desactivado",
                            AprobadoPor = operadorId
                        });
                    }

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

    public async Task<ProfesionalApiCollectionResult<HorarioProfesionalApiDto>> ObtenerHorariosAsync(
        int id,
        int? usuarioActualId = null,
        bool esAdministrador = true,
        CancellationToken ct = default)
    {
        var profesional = await _context.Profesionales.AsNoTracking()
            .Where(p => p.IdProfesional == id)
            .Select(p => new { p.IdProfesional, p.IdUsuario })
            .FirstOrDefaultAsync(ct);
        if (profesional is null)
            return ProfesionalApiCollectionResult<HorarioProfesionalApiDto>.Fail(
                "Profesional no encontrado.", 404);

        if (!esAdministrador &&
            (!usuarioActualId.HasValue || profesional.IdUsuario != usuarioActualId.Value))
        {
            return ProfesionalApiCollectionResult<HorarioProfesionalApiDto>.Fail(
                "No tienes permiso para consultar el horario de otro profesional.", 403);
        }

        var horarios = await _context.HorariosProfesional
            .AsNoTracking()
            .Where(h => h.IdProfesional == id && h.Activo)
            .OrderBy(h => h.DiaSemana)
            .ThenBy(h => h.HoraInicio)
            .Select(h => new HorarioProfesionalApiDto
            {
                IdHorario = h.IdHorario,
                DiaSemana = h.DiaSemana,
                HoraInicio = h.HoraInicio.ToString("HH:mm"),
                HoraFin = h.HoraFin.ToString("HH:mm"),
                Activo = h.Activo
            })
            .ToListAsync(ct);

        return ProfesionalApiCollectionResult<HorarioProfesionalApiDto>.Ok(horarios);
    }

    public async Task<ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>> ActualizarHorariosAsync(
        int id,
        IReadOnlyCollection<HorarioSemanalApiRequest> horarios,
        int? usuarioActualId,
        bool esAdministrador,
        CancellationToken ct = default)
    {
        if (horarios.Count == 0)
            return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                "Debe enviar al menos un día de la semana para actualizar el horario.", 422);
        bool existe = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);
        if (!existe)
            return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                "Profesional no encontrado.", 404);

        if (!esAdministrador)
        {
            bool esPropietario = usuarioActualId.HasValue && await _context.Profesionales
                .AsNoTracking()
                .AnyAsync(p => p.IdProfesional == id && p.IdUsuario == usuarioActualId.Value, ct);
            if (!esPropietario)
                return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                    "No tienes permiso para modificar el horario de otro profesional.", 403);
        }

        var diasValidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lunes", "Martes", "Miércoles", "Miercoles", "Jueves", "Viernes", "Sábado", "Sabado", "Domingo"
        };

        static string NormalizarDiaSemana(string dia) =>
            dia.Trim().ToLowerInvariant() switch
            {
                "miércoles" or "miercoles" => "Miercoles",
                "sábado" or "sabado" => "Sabado",
                _ => dia.Trim()
            };

        var bloquesActivos = horarios.Where(b => b.Active).ToList();
        bool hayBloqueInvalido = bloquesActivos.Any(b =>
            string.IsNullOrWhiteSpace(b.DiaSemana) ||
            !diasValidos.Contains(b.DiaSemana) ||
            !TimeOnly.TryParse(b.Start, out var inicio) ||
            !TimeOnly.TryParse(b.End, out var fin) ||
            fin <= inicio);
        if (hayBloqueInvalido)
            return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                "Cada día activo debe tener un día válido y una hora de inicio y fin válidas.", 422);

        var nuevosHorarios = bloquesActivos
            .Select(b => new { Bloque = b, Inicio = TimeOnly.TryParse(b.Start, out var inicio) ? inicio : (TimeOnly?)null, Fin = TimeOnly.TryParse(b.End, out var fin) ? fin : (TimeOnly?)null })
            .Where(x => x.Inicio.HasValue && x.Fin.HasValue && x.Fin > x.Inicio)
            .Select(x => new HorarioProfesional
            {
                IdProfesional = id,
                DiaSemana = NormalizarDiaSemana(x.Bloque.DiaSemana!),
                HoraInicio = x.Inicio!.Value,
                HoraFin = x.Fin!.Value,
                Activo = true
            })
            .ToList();

        var existentes = await _context.HorariosProfesional
            .Where(h => h.IdProfesional == id)
            .ToListAsync(ct);
        _context.HorariosProfesional.RemoveRange(existentes);
        if (nuevosHorarios.Count > 0)
            await _context.HorariosProfesional.AddRangeAsync(nuevosHorarios, ct);
        await _context.SaveChangesAsync(ct);

        var actualizado = await ObtenerHorariosAsync(id, usuarioActualId, esAdministrador, ct);
        return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Ok(
            "Horario actualizado correctamente.", actualizado.Data);
    }

    public async Task<ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>> ObtenerAusenciasAsync(
        int id,
        CancellationToken ct = default)
    {
        bool existe = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);
        if (!existe)
            return ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>.Fail(
                "Profesional no encontrado.", 404);

        var ausencias = await _context.AusenciasProfesional
            .AsNoTracking()
            .Where(a => a.IdProfesional == id)
            .OrderByDescending(a => a.FechaInicio)
            .Select(a => new AusenciaProfesionalApiDto
            {
                IdAusencia = a.IdAusencia,
                Tipo = a.Tipo,
                FechaInicio = a.FechaInicio.ToString("yyyy-MM-dd"),
                FechaFin = a.FechaFin.ToString("yyyy-MM-dd"),
                Duracion = a.Duracion,
                Observaciones = a.Observaciones
            })
            .ToListAsync(ct);

        return ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>.Ok(ausencias);
    }

    public async Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> CrearAusenciaAsync(
        int id,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default)
    {
        var validation = await ValidarAusenciaAsync(id, request, ct);
        if (!validation.Success)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail(validation.Message, validation.StatusCode);

        var ausencia = new AusenciaProfesional
        {
            IdProfesional = id,
            Tipo = request.Tipo.Trim().ToLowerInvariant(),
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            Duracion = request.FechaFin.DayNumber - request.FechaInicio.DayNumber + 1,
            Observaciones = request.Observaciones?.Trim(),
            AprobadoPor = operadorId
        };

        _context.AusenciasProfesional.Add(ausencia);
        await _context.SaveChangesAsync(ct);
        return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Ok(
            "Ausencia registrada correctamente.", [MapAusencia(ausencia)]);
    }

    public async Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> ActualizarAusenciaAsync(
        int id,
        int idAusencia,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default)
    {
        var validation = await ValidarAusenciaAsync(id, request, ct);
        if (!validation.Success)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail(validation.Message, validation.StatusCode);

        var ausencia = await _context.AusenciasProfesional
            .FirstOrDefaultAsync(a => a.IdAusencia == idAusencia && a.IdProfesional == id, ct);
        if (ausencia is null)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail("Ausencia no encontrada.", 404);

        ausencia.Tipo = request.Tipo.Trim().ToLowerInvariant();
        ausencia.FechaInicio = request.FechaInicio;
        ausencia.FechaFin = request.FechaFin;
        ausencia.Duracion = request.FechaFin.DayNumber - request.FechaInicio.DayNumber + 1;
        ausencia.Observaciones = request.Observaciones?.Trim();
        ausencia.AprobadoPor = operadorId;
        await _context.SaveChangesAsync(ct);
        return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Ok(
            "Ausencia actualizada correctamente.", [MapAusencia(ausencia)]);
    }

    public async Task<ProfesionalApiOperationResult> EliminarAusenciaAsync(
        int id,
        int idAusencia,
        int? operadorId,
        CancellationToken ct = default)
    {
        var ausencia = await _context.AusenciasProfesional
            .FirstOrDefaultAsync(a => a.IdAusencia == idAusencia && a.IdProfesional == id, ct);
        if (ausencia is null)
            return ProfesionalApiOperationResult.Fail("Ausencia no encontrada.", 404);

        _context.AusenciasProfesional.Remove(ausencia);
        await _context.SaveChangesAsync(ct);
        return ProfesionalApiOperationResult.Ok("Ausencia eliminada correctamente.");
    }

    private async Task<(bool Success, string Message, int StatusCode)> ValidarAusenciaAsync(
        int id,
        AusenciaProfesionalApiRequest request,
        CancellationToken ct)
    {
        if (request is null || id <= 0)
            return (false, "Datos de ausencia inválidos.", 400);
        if (!await _context.Profesionales.AnyAsync(p => p.IdProfesional == id, ct))
            return (false, "Profesional no encontrado.", 404);
        if (request.FechaFin < request.FechaInicio)
            return (false, "La fecha de fin debe ser igual o posterior a la fecha de inicio.", 400);

        string[] tiposPermitidos = ["vacaciones", "incapacidad", "permiso", "otro"];
        if (!tiposPermitidos.Contains(request.Tipo.Trim(), StringComparer.OrdinalIgnoreCase))
            return (false, "El tipo de ausencia no es válido.", 400);

        return (true, string.Empty, 200);
    }

    private static AusenciaProfesionalApiDto MapAusencia(AusenciaProfesional ausencia) => new()
    {
        IdAusencia = ausencia.IdAusencia,
        Tipo = ausencia.Tipo,
        FechaInicio = ausencia.FechaInicio.ToString("yyyy-MM-dd"),
        FechaFin = ausencia.FechaFin.ToString("yyyy-MM-dd"),
        Duracion = ausencia.Duracion,
        Observaciones = ausencia.Observaciones
    };

    public async Task<ProfesionalApiCollectionResult<ServicioProfesionalApiDto>> ObtenerServiciosAsync(
        int id,
        CancellationToken ct = default)
    {
        bool existe = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);
        if (!existe)
            return ProfesionalApiCollectionResult<ServicioProfesionalApiDto>.Fail(
                "Profesional no encontrado.", 404);

        var servicios = await _context.ProfesionalServicios
            .AsNoTracking()
            .Where(ps => ps.IdProfesional == id && ps.Activo)
            .Include(ps => ps.Servicio)
            .Select(ps => new ServicioProfesionalApiDto
            {
                IdProfesional = ps.IdProfesional,
                IdServicio = ps.IdServicio,
                NombreServicio = ps.Servicio != null ? ps.Servicio.Nombre : string.Empty,
                PrecioBase = ps.Servicio != null ? ps.Servicio.Precio : 0m,
                PrecioPersonalizado = ps.PrecioPersonalizado,
                PrecioEfectivo = ps.PrecioPersonalizado ?? (ps.Servicio != null ? ps.Servicio.Precio : 0m),
                Activo = ps.Activo
            })
            .OrderBy(ps => ps.NombreServicio)
            .ToListAsync(ct);

        return ProfesionalApiCollectionResult<ServicioProfesionalApiDto>.Ok(servicios);
    }

    private static ProfesionalApiDto MapToDto(Profesional p) => new()
    {
        IdProfesional = p.IdProfesional,
        IdUsuario = p.IdUsuario ?? 0,
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

        if (!esCreacion &&
            !string.IsNullOrWhiteSpace(request.ContrasenaAcceso) &&
            !ProfesionalEstadoHelper.EsPasswordValida(request.ContrasenaAcceso))
            return (false, "La contraseña debe tener mínimo 8 caracteres, mayúscula, minúscula, número y símbolo especial.");

        return (true, null);
    }

    private static string GenerarContrasenaTemporal()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string digitos = "23456789";
        const string simbolos = "!@#$%^&*";
        const string todos = mayusculas + minusculas + digitos + simbolos;

        var caracteres = new char[12];
        caracteres[0] = mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)];
        caracteres[1] = minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)];
        caracteres[2] = digitos[RandomNumberGenerator.GetInt32(digitos.Length)];
        caracteres[3] = simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)];

        for (int i = 4; i < caracteres.Length; i++)
            caracteres[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];

        var aleatorio = caracteres.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
        return new string(aleatorio);
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

    // ── Métodos Avanzados de Comisiones y Reasignación por Ausencia ──────────

    public async Task<SmileTrack_MVC.Models.DTOs.ReporteComisionProfesionalDto> CalcularComisionesAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        decimal porcentajeComision = 40,
        CancellationToken ct = default)
    {
        var profesional = await _context.Profesionales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdProfesional == idProfesional, ct);

        if (profesional is null)
            throw new InvalidOperationException("El profesional especificado no existe.");

        var citasAtendidas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Servicio)
            .Where(c => c.IdProfesional == idProfesional
                     && c.FechaHora >= fechaInicio
                     && c.FechaHora <= fechaFin
                     && c.Estado.ToLower() == "atendida")
            .ToListAsync(ct);

        var gruposServicio = citasAtendidas
            .GroupBy(c => c.IdServicio)
            .Select(g =>
            {
                var primerServicio = g.First().Servicio;
                string nombreServicio = primerServicio?.Nombre ?? "Servicio Odontológico";
                decimal precioUnitario = primerServicio?.Precio ?? 0m;
                int cantidad = g.Count();
                decimal subtotal = cantidad * precioUnitario;
                decimal honorarios = subtotal * (porcentajeComision / 100m);

                return new SmileTrack_MVC.Models.DTOs.ReporteComisionServicioDto
                {
                    IdServicio = g.Key ?? 0,
                    NombreServicio = nombreServicio,
                    CantidadAtendida = cantidad,
                    PrecioUnitario = precioUnitario,
                    SubtotalFacturado = subtotal,
                    HonorariosGenerados = honorarios
                };
            })
            .ToList();

        decimal montoTotalFacturado = gruposServicio.Sum(g => g.SubtotalFacturado);
        decimal montoTotalHonorarios = gruposServicio.Sum(g => g.HonorariosGenerados);

        return new SmileTrack_MVC.Models.DTOs.ReporteComisionProfesionalDto
        {
            IdProfesional = idProfesional,
            NombreProfesional = $"{profesional.Nombres} {profesional.Apellidos}".Trim(),
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            TotalCitasAtendidas = citasAtendidas.Count,
            MontoTotalFacturado = montoTotalFacturado,
            PorcentajeComision = porcentajeComision,
            MontoTotalHonorarios = montoTotalHonorarios,
            DetalleServicios = gruposServicio
        };
    }

    public async Task<SmileTrack_MVC.Models.DTOs.ResultadoReasignacionAusenciaDto> RegistrarAusenciaConReasignacionAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        string motivo,
        CancellationToken ct = default)
    {
        var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdProfesional == idProfesional, ct);
        if (profesional is null)
            throw new InvalidOperationException("El profesional especificado no existe.");

        var dateInicio = DateOnly.FromDateTime(fechaInicio);
        var dateFin = DateOnly.FromDateTime(fechaFin);

        // 1. Registrar la ausencia del profesional
        var ausencia = new AusenciaProfesional
        {
            IdProfesional = idProfesional,
            FechaInicio = dateInicio,
            FechaFin = dateFin,
            Tipo = string.IsNullOrWhiteSpace(motivo) ? "incapacidad" : motivo.Trim(),
            Observaciones = "Registrado dinámicamente con motor de reasignación masiva."
        };
        _context.AusenciasProfesional.Add(ausencia);

        // 2. Buscar citas afectadas no canceladas
        var citasAfectadas = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional
                     && c.FechaHora >= fechaInicio
                     && c.FechaHora <= fechaFin
                     && c.Estado.ToLower() != "cancelada")
            .ToListAsync(ct);

        var resultado = new SmileTrack_MVC.Models.DTOs.ResultadoReasignacionAusenciaDto
        {
            IdProfesional = idProfesional,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            TotalCitasAfectadas = citasAfectadas.Count
        };

        var profesionalesActivos = await _context.Profesionales
            .Where(p => p.IdProfesional != idProfesional && p.Estado == "activo")
            .ToListAsync(ct);

        int reasignadas = 0;
        int canceladas = 0;

        foreach (var cita in citasAfectadas)
        {
            int duracion = cita.DuracionMinutos > 0 ? cita.DuracionMinutos : 30;
            DateTime finCita = cita.FechaHora.AddMinutes(duracion);
            DateOnly dateCita = DateOnly.FromDateTime(cita.FechaHora);

            Profesional? candidatoElegido = null;

            foreach (var candidato in profesionalesActivos)
            {
                // Verificar si ofrece el servicio si hay especificación de servicios
                bool ofreceServicio = !await _context.ProfesionalServicios.AnyAsync(ps => ps.IdProfesional == candidato.IdProfesional, ct)
                                   || await _context.ProfesionalServicios.AnyAsync(ps => ps.IdProfesional == candidato.IdProfesional && ps.IdServicio == cita.IdServicio && ps.Activo, ct);

                if (!ofreceServicio) continue;

                // Verificar ausencias del candidato
                bool candidatoConAusencia = await _context.AusenciasProfesional
                    .AnyAsync(a => a.IdProfesional == candidato.IdProfesional && a.FechaInicio <= dateCita && a.FechaFin >= dateCita, ct);

                if (candidatoConAusencia) continue;

                // Verificar solapamiento de citas del candidato
                bool candidatoConConflicto = await _context.Citas
                    .AnyAsync(c => c.IdProfesional == candidato.IdProfesional
                                && c.Estado.ToLower() != "cancelada"
                                && c.FechaHora < finCita
                                && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : 30) > cita.FechaHora, ct);

                if (candidatoConConflicto) continue;

                candidatoElegido = candidato;
                break;
            }

            if (candidatoElegido is not null)
            {
                int idAnterior = cita.IdProfesional ?? 0;
                cita.IdProfesional = candidatoElegido.IdProfesional;

                // Historial de reasignación
                _context.CitasHistorialEstado.Add(new CitaHistorialEstado
                {
                    IdCita = cita.IdCita,
                    IdEstado = cita.IdEstado,
                    EstadoTexto = cita.Estado,
                    Motivo = $"Reasignada automáticamente por ausencia del profesional #{idAnterior} a #{candidatoElegido.IdProfesional}",
                    FechaCambio = DateTime.UtcNow
                });

                // Notificación al paciente
                _context.Notificaciones.Add(new Notificacion
                {
                    IdPaciente = cita.IdPaciente,
                    IdCita = cita.IdCita,
                    Tipo = "cambio_profesional",
                    Titulo = "Reasignación de profesional",
                    Contenido = $"Tu cita del {cita.FechaHora:dd/MM/yyyy HH:mm} ha sido asignada a {candidatoElegido.Nombres} {candidatoElegido.Apellidos} por ausencia médica.",
                    CreadaEn = DateTime.UtcNow
                });

                reasignadas++;
                resultado.DetallesCitas.Add(new SmileTrack_MVC.Models.DTOs.CitaReasignadaInfo
                {
                    IdCita = cita.IdCita,
                    IdPaciente = cita.IdPaciente,
                    FechaHora = cita.FechaHora,
                    Reasignada = true,
                    IdNuevoProfesional = candidatoElegido.IdProfesional,
                    NombreNuevoProfesional = $"{candidatoElegido.Nombres} {candidatoElegido.Apellidos}".Trim(),
                    Mensaje = "Reasignación exitosa"
                });
            }
            else
            {
                string estadoAnterior = cita.Estado;
                var estadoCancelada = await _context.EstadosCita.FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == "cancelada", ct);
                if (estadoCancelada is not null)
                {
                    cita.IdEstado = estadoCancelada.IdEstado;
                    cita.Estado = estadoCancelada.NombreEstado;
                }
                else
                {
                    cita.Estado = "Cancelada";
                }

                // Historial de cancelación
                _context.CitasHistorialEstado.Add(new CitaHistorialEstado
                {
                    IdCita = cita.IdCita,
                    IdEstado = cita.IdEstado,
                    EstadoTexto = cita.Estado,
                    Motivo = "Cancelación automática por ausencia médica del profesional sin disponibilidad de reemplazo.",
                    FechaCambio = DateTime.UtcNow
                });

                // Notificación al paciente
                _context.Notificaciones.Add(new Notificacion
                {
                    IdPaciente = cita.IdPaciente,
                    IdCita = cita.IdCita,
                    Tipo = "cancelacion_emergencia",
                    Titulo = "Cita cancelada por fuerza mayor",
                    Contenido = $"Tu cita del {cita.FechaHora:dd/MM/yyyy HH:mm} fue cancelada por incapacidad del profesional. Por favor contacta a recepción para reprogramar.",
                    CreadaEn = DateTime.UtcNow
                });

                canceladas++;
                resultado.DetallesCitas.Add(new SmileTrack_MVC.Models.DTOs.CitaReasignadaInfo
                {
                    IdCita = cita.IdCita,
                    IdPaciente = cita.IdPaciente,
                    FechaHora = cita.FechaHora,
                    Reasignada = false,
                    Mensaje = "Cancelada por falta de profesional sustituto disponible"
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        resultado.TotalReasignadas = reasignadas;
        resultado.TotalCanceladas = canceladas;

        return resultado;
    }

    // ── Excepciones internas ──────────────────────────────────────────────────

    private sealed class CorreoDuplicadoException : Exception { }
    private sealed class ProfesionalNotFoundException : Exception { }
}
