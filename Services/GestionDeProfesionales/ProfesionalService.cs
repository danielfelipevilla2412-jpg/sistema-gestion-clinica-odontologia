/**
 * ============================================
 * SmileTrack — Módulo: Gestión de Profesionales
 * Componente: ProfesionalService
 * ============================================
 * Archivo: Services/GestionDeProfesionales/ProfesionalService.cs
 *
 * PROPÓSITO Y JUSTIFICACIÓN:
 * Implementación de la capa de servicios para la administración de profesionales odontológicos.
 * Encapsula la lógica de persistencia, generación/asociación de cuenta de usuario, hash de contraseñas,
 * asignación de especialidades y actualización de disponibilidad semanal.
 *
 * REGLAS DE NEGOCIO PRINCIPALES:
 * - Delega la validación de contraseñas y estados a ProfesionalEstadoHelper para mantener coherencia única.
 * - Garantiza transaccionalidad mediante DbContext al crear simultáneamente la entidad Usuario y la entidad Profesional.
 * - Formatea y normaliza campos de identificación (Registro Médico, Teléfono, Email).
 *
 * DEPENDENCIAS TÉCNICAS:
 * - AppDbContext, ProfesionalEstadoHelper, BCrypt / SHA256, ILogger
 * ============================================
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;
using SmileTrack_MVC.Services.GestionDeProfesionales;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmileTrack_MVC.Services;

public partial class ProfesionalService : IProfesionalService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProfesionalService> _logger;
    private readonly IMemoryCache? _memoryCache;
    private readonly IProfesionalComisionService _comisionService;
    private readonly IProfesionalHorarioService _horarioService;
    private readonly IProfesionalAusenciaService _ausenciaService;

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

    public ProfesionalService(
        AppDbContext context,
        ILogger<ProfesionalService> logger,
        IMemoryCache? memoryCache = null)
        : this(context, logger, new ProfesionalComisionService(context), memoryCache)
    {
    }

    public ProfesionalService(
        AppDbContext context,
        ILogger<ProfesionalService> logger,
        IProfesionalComisionService comisionService,
        IMemoryCache? memoryCache = null,
        IProfesionalHorarioService? horarioService = null,
        IProfesionalAusenciaService? ausenciaService = null)
    {
        _context = context;
        _logger = logger;
        _comisionService = comisionService;
        _memoryCache = memoryCache;
        _horarioService = horarioService ?? new ProfesionalHorarioService(context);
        _ausenciaService = ausenciaService ?? new ProfesionalAusenciaService(context);
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

    public async Task<ProfesionalesStats> ObtenerEstadisticasAsync(CancellationToken ct = default) =>
        new()
        {
            StatTotal = await _context.Profesionales.CountAsync(ct),
            StatActivos = await _context.Profesionales.CountAsync(p => p.Estado == "activo", ct),
            StatVacaciones = await _context.Profesionales.CountAsync(p => p.Estado == "vacaciones", ct),
            StatInactivos = await _context.Profesionales.CountAsync(p => p.Estado == "inactivo", ct)
        };

    public async Task<(List<Profesional> Items, PagedResult<Profesional> Paginacion, ProfesionalesStats Stats)>
        ObtenerVistaMVCAsync(
            PaginationQuery q,
            CancellationToken ct = default)
    {
        var query = q ?? new PaginationQuery();
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 10 : query.PageSize;
        if (pageSize > 100) pageSize = 100;

        var stats = await ObtenerEstadisticasAsync(ct);

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
        if (_memoryCache is not null &&
            _memoryCache.TryGetValue(CatalogCacheKeys.EspecialidadesApi, out List<ProfesionalEspecialidadApiDto>? cached) &&
            cached is not null)
            return cached;

        var especialidades = await _context.Especialidades
            .AsNoTracking()
            .OrderBy(e => e.Nombre)
            .Select(e => new ProfesionalEspecialidadApiDto
            {
                IdEspecialidad = e.IdEspecialidad,
                Nombre = e.Nombre
            })
            .ToListAsync(ct);

        _memoryCache?.Set(
            CatalogCacheKeys.EspecialidadesApi,
            especialidades,
            new MemoryCacheEntryOptions { SlidingExpiration = CatalogCacheKeys.SlidingExpiration });

        return especialidades;
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
        if (!await EsCategoriaValidaAsync(request.Categoria, ct))
            return ProfesionalApiOperationResult.Fail("La categoría seleccionada no pertenece al catálogo de especialidades.", 422);

        string nombres = request.Nombres.Trim();
        string apellidos = request.Apellidos.Trim();
        string registro = request.RegistroMedico.Trim();
        string tarjetaProfesional = request.TarjetaProfesional.Trim();
        string correo = request.CorreoAcceso!.Trim();
        string password = GenerarContrasenaTemporal();

        try
        {
            // Verificar registro médico duplicado
            bool regDuplicado = await _context.Profesionales
                .AnyAsync(p => p.RegistroMedico == registro, ct);
            if (regDuplicado)
                return ProfesionalApiOperationResult.Conflict(
                    $"El registro médico '{registro}' ya está asignado a otro profesional.");
            bool tarjetaDuplicada = await _context.Profesionales
                .AnyAsync(p => p.TarjetaProfesional == tarjetaProfesional, ct);
            if (tarjetaDuplicada)
                return ProfesionalApiOperationResult.Conflict(
                    $"La tarjeta profesional '{tarjetaProfesional}' ya está asignada a otro profesional.");

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
                        TarjetaProfesional = tarjetaProfesional,
                        Categoria = request.Categoria?.Trim(),
                        Telefono = request.Telefono?.Trim(),
                        Descripcion = request.Descripcion?.Trim(),
                        Estado = "inactivo",
                        CredencialesAprobadas = false,
                        FechaIngreso = ColombiaClock.Today
                    };

                    _context.Profesionales.Add(profesional);
                    await _context.SaveChangesAsync(ct);

                    // ── Especialidades (RF-11 + RN-15) ──────────────────────────────
                    // La lista ordenada nueva lleva la especialidad principal en la primera posición.
                    var idsEspRaw = request.EspecialidadesIds is { Count: > 0 }
                        ? request.EspecialidadesIds.Where(x => x > 0).Distinct().ToList()
                        : request.IdsEspecialidades is { Count: > 0 }
                            ? request.IdsEspecialidades.Where(x => x > 0).Distinct().ToList()
                            : request.IdEspecialidad is > 0
                                ? new List<int> { request.IdEspecialidad.Value }
                                : new List<int>();

                    if (idsEspRaw.Count == 0)
                    {
                        // RN-15: sin selección → Odontología General
                        var espDefault = await _context.Especialidades
                            .FirstOrDefaultAsync(e => e.Nombre == "Odontología General", ct);
                        if (espDefault is null)
                            throw new InvalidOperationException(
                                "No existe la especialidad 'Odontología General' para aplicar el valor predeterminado (RN-15).");
                        idsEspRaw.Add(espDefault.IdEspecialidad);
                    }

                    for (int espIdx = 0; espIdx < idsEspRaw.Count; espIdx++)
                    {
                        int idEsp = idsEspRaw[espIdx];
                        bool espExiste = await _context.Especialidades
                            .AnyAsync(e => e.IdEspecialidad == idEsp, ct);
                        if (!espExiste)
                            throw new InvalidOperationException($"La especialidad con ID {idEsp} no existe.");

                        _context.ProfesionalEspecialidades.Add(new Profesional_Especialidad
                        {
                            IdProfesional = profesional.IdProfesional,
                            IdEspecialidad = idEsp,
                            Principal = espIdx == 0   // la primera es la principal
                        });
                    }

                    if (idsEspRaw.Count > 0)
                        await _context.SaveChangesAsync(ct);

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
                "La cuenta fue creada y la contraseña temporal segura fue generada automáticamente. El perfil profesional permanecerá inactivo hasta que el administrador apruebe las credenciales cargadas.", dto);
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
        if (!await EsCategoriaValidaAsync(request.Categoria, ct))
            return ProfesionalApiOperationResult.Fail("La categoría seleccionada no pertenece al catálogo de especialidades.", 422);

        string nombres = request.Nombres.Trim();
        string apellidos = request.Apellidos.Trim();
        string registro = request.RegistroMedico.Trim();
        string tarjetaProfesional = request.TarjetaProfesional.Trim();
        string correo = request.CorreoAcceso?.Trim() ?? string.Empty;
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
            bool tarjetaDuplicada = await _context.Profesionales
                .AnyAsync(p => p.TarjetaProfesional == tarjetaProfesional && p.IdProfesional != id, ct);
            if (tarjetaDuplicada)
                return ProfesionalApiOperationResult.Conflict(
                    $"La tarjeta profesional '{tarjetaProfesional}' ya está asignada a otro profesional.");

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

                    var usuario = profesional.Usuario;
                    if (usuario is null)
                    {
                        if (!string.IsNullOrWhiteSpace(correo) ||
                            !string.IsNullOrWhiteSpace(request.ContrasenaAcceso))
                            throw new InvalidOperationException(
                                "Este profesional no tiene una cuenta vinculada. Edite únicamente los datos del perfil.");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(correo))
                            throw new InvalidOperationException("El correo de acceso es requerido.");

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
                    }

                    // Actualizar profesional
                    profesional.Nombres = nombres;
                    profesional.Apellidos = apellidos;
                    profesional.RegistroMedico = registro;
                    profesional.TarjetaProfesional = tarjetaProfesional;
                    profesional.Categoria = request.Categoria?.Trim();
                    profesional.Telefono = request.Telefono?.Trim();
                    profesional.Descripcion = request.Descripcion?.Trim();
                    profesional.FechaIngreso ??= ColombiaClock.Today;

                    bool requiereAprobacion =
                        (estadoSolicitado is "activo" or "vacaciones") &&
                        !string.Equals(profesional.Estado, "activo", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(profesional.Estado, "vacaciones", StringComparison.OrdinalIgnoreCase);
                    if (requiereAprobacion)
                    {
                        string? errorCredenciales = await ObtenerErrorCredencialesParaActivacionAsync(id, ct);
                        if (errorCredenciales is not null)
                            throw new InvalidOperationException(errorCredenciales);
                        profesional.CredencialesAprobadas = true;
                    }

                    if (estadoSolicitado == "inactivo" && profesional.Estado != "inactivo")
                    {
                        bool tieneCitasActivas = await _context.Citas.AnyAsync(c =>
                            c.IdProfesional == id &&
                            !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                                ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
                            c.FechaHora >= ColombiaClock.Today, ct);
                        if (tieneCitasActivas)
                            throw new InvalidOperationException("No se puede inactivar: este profesional tiene citas agendadas pendientes.");
                    }

                    if (estadoSolicitado is not null)
                        profesional.Estado = estadoSolicitado;

                    // Sincronizar estado de la cuenta
                    if (usuario is not null)
                    {
                        usuario.Estado = profesional.Estado == "inactivo" && profesional.CredencialesAprobadas
                            ? "inactivo"
                            : "activo";
                    }

                    await _context.SaveChangesAsync(ct);

                    // ── Actualizar especialidades (RF-11 + RN-15) ───────────────────
                    var relaciones = await _context.ProfesionalEspecialidades
                        .Where(pe => pe.IdProfesional == id)
                        .ToListAsync(ct);

                    if (relaciones.Count > 0)
                        _context.ProfesionalEspecialidades.RemoveRange(relaciones);

                    var idsEspUpd = request.EspecialidadesIds is { Count: > 0 }
                        ? request.EspecialidadesIds.Where(x => x > 0).Distinct().ToList()
                        : request.IdsEspecialidades is { Count: > 0 }
                            ? request.IdsEspecialidades.Where(x => x > 0).Distinct().ToList()
                            : request.IdEspecialidad is > 0
                                ? new List<int> { request.IdEspecialidad.Value }
                                : new List<int>();

                    if (idsEspUpd.Count == 0)
                    {
                        // RN-15: sin selección → Odontología General
                        var espDefault = await _context.Especialidades
                            .FirstOrDefaultAsync(e => e.Nombre == "Odontología General", ct);
                        if (espDefault is null)
                            throw new InvalidOperationException(
                                "No existe la especialidad 'Odontología General' para aplicar el valor predeterminado (RN-15).");
                        idsEspUpd.Add(espDefault.IdEspecialidad);
                    }

                    for (int espIdx = 0; espIdx < idsEspUpd.Count; espIdx++)
                    {
                        int idEsp = idsEspUpd[espIdx];
                        bool espExiste = await _context.Especialidades
                            .AnyAsync(e => e.IdEspecialidad == idEsp, ct);
                        if (!espExiste)
                            throw new InvalidOperationException($"La especialidad con ID {idEsp} no existe.");

                        _context.ProfesionalEspecialidades.Add(new Profesional_Especialidad
                        {
                            IdProfesional = id,
                            IdEspecialidad = idEsp,
                            Principal = espIdx == 0
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
                            Correo = usuario?.Correo,
                            IdsEspecialidades = idsEspUpd
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
                profesional.Usuario is null
                    ? "El perfil profesional fue actualizado correctamente; no tiene una cuenta de acceso vinculada."
                    : "El profesional y su cuenta de acceso fueron actualizados correctamente.",
                dto);
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

                    bool requiereAprobacion =
                        (estadoNorm is "activo" or "vacaciones") &&
                        !string.Equals(estadoAnterior, "activo", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(estadoAnterior, "vacaciones", StringComparison.OrdinalIgnoreCase);
                    if (requiereAprobacion)
                    {
                        string? errorCredenciales = await ObtenerErrorCredencialesParaActivacionAsync(id, ct);
                        if (errorCredenciales is not null)
                            throw new InvalidOperationException(errorCredenciales);
                        profesional.CredencialesAprobadas = true;
                    }

                    // Si se va a inactivar, verificar citas pendientes
                    if (estadoNorm == "inactivo" && estadoAnterior != "inactivo")
                    {
                        bool tieneCitasActivas = await _context.Citas
                            .AnyAsync(c =>
                                c.IdProfesional == id &&
                                !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                                    ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
                                c.FechaHora >= ColombiaClock.Today, ct);

                        if (tieneCitasActivas)
                            throw new InvalidOperationException("No se puede inactivar: este profesional tiene citas agendadas pendientes.");
                    }

                    profesional.Estado = estadoNorm;

                    // Sincronizar la cuenta de usuario
                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = estadoNorm == "inactivo" && profesional.CredencialesAprobadas
                            ? "inactivo"
                            : "activo";

                    // Sincronizar bloqueo en Bloqueo_Profesional según el estado
                    if (estadoNorm == "inactivo")
                    {
                        bool yaBloqueado = await _context.BloqueosProfesional
                            .AnyAsync(b => b.IdProfesional == id && b.FechaFin > ColombiaClock.Now && b.Motivo == "Profesional inactivo / desactivado", ct);

                        if (!yaBloqueado)
                        {
                            _context.BloqueosProfesional.Add(new BloqueoProfesional
                            {
                                IdProfesional = id,
                                FechaInicio = ColombiaClock.Now,
                                FechaFin = ColombiaClock.Now.AddYears(5),
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
                            !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                                ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()) &&
                            c.FechaHora >= ColombiaClock.Today, ct);

                    if (tieneCitasActivas && profesional.Estado != "inactivo")
                        throw new InvalidOperationException("No se puede desactivar: este profesional tiene citas agendadas pendientes.");

                    string estadoAnterior = profesional.Estado;
                    profesional.Estado = "inactivo";

                    if (profesional.Usuario != null)
                        profesional.Usuario.Estado = "inactivo";

                    // Registrar bloqueo en Bloqueo_Profesional
                    bool yaBloqueado = await _context.BloqueosProfesional
                        .AnyAsync(b => b.IdProfesional == id && b.FechaFin > ColombiaClock.Now && b.Motivo == "Profesional inactivo / desactivado", ct);

                    if (!yaBloqueado)
                    {
                        _context.BloqueosProfesional.Add(new BloqueoProfesional
                        {
                            IdProfesional = id,
                            FechaInicio = ColombiaClock.Now,
                            FechaFin = ColombiaClock.Now.AddYears(5),
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

    public Task<ProfesionalApiCollectionResult<HorarioProfesionalApiDto>> ObtenerHorariosAsync(
        int id,
        int? usuarioActualId = null,
        bool esAdministrador = true,
        CancellationToken ct = default)
        => _horarioService.ObtenerAsync(id, usuarioActualId, esAdministrador, ct);

    public Task<ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>> ActualizarHorariosAsync(
        int id,
        IReadOnlyCollection<HorarioSemanalApiRequest> horarios,
        int? usuarioActualId,
        bool esAdministrador,
        CancellationToken ct = default)
        => _horarioService.ActualizarAsync(id, horarios, usuarioActualId, esAdministrador, ct);

    public Task<ProfesionalApiCollectionResult<AusenciaProfesionalApiDto>> ObtenerAusenciasAsync(
        int id,
        CancellationToken ct = default)
        => _ausenciaService.ObtenerAusenciasAsync(id, ct);

    public Task<ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>> ObtenerCitasAfectadasPorAusenciaAsync(
        int id,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken ct = default)
        => _ausenciaService.ObtenerCitasAfectadasPorAusenciaAsync(id, fechaInicio, fechaFin, ct);

    public Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> CrearAusenciaAsync(
        int id,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default)
        => _ausenciaService.CrearAusenciaAsync(id, request, operadorId, ct);

    public Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> ActualizarAusenciaAsync(
        int id,
        int idAusencia,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default)
        => _ausenciaService.ActualizarAusenciaAsync(id, idAusencia, request, operadorId, ct);

    public Task<ProfesionalApiOperationResult> EliminarAusenciaAsync(
        int id,
        int idAusencia,
        int? operadorId,
        CancellationToken ct = default)
        => _ausenciaService.EliminarAusenciaAsync(id, idAusencia, operadorId, ct);
    private async Task<string?> ObtenerErrorCredencialesParaActivacionAsync(
        int idProfesional,
        CancellationToken ct)
    {
        var estados = await _context.CredencialesProfesionales
            .AsNoTracking()
            .Where(d => d.IdProfesional == idProfesional)
            .Select(d => d.EstadoRevision)
            .ToListAsync(ct);

        bool hayPendientes = estados.Any(estado =>
            string.Equals(estado?.Trim(), "pendiente", StringComparison.OrdinalIgnoreCase));
        if (hayPendientes)
            return "Revisa todas las credenciales pendientes antes de activar al profesional.";

        bool hayAprobadas = estados.Any(estado =>
            string.Equals(estado?.Trim(), "aprobado", StringComparison.OrdinalIgnoreCase));
        if (!hayAprobadas)
            return "Aprueba al menos una credencial del profesional antes de activarlo. Si todas fueron rechazadas, el profesional debe cargar una nueva credencial.";

        return null;
    }

    /// <summary>
    /// M1 (RF-15/CU-PRO-06): Retorna las citas activas del profesional que se solapan
    /// con el rango de ausencia indicado. No modifica ningún dato; solo consulta para
    /// que la capa de presentación pueda mostrar una advertencia accionable.
    /// </summary>
    private async Task<List<Cita>> DetectarCitasEnRangoAusenciaAsync(
        int idProfesional,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken ct)
    {
        try
        {
            DateTime inicio = fechaInicio.ToDateTime(TimeOnly.MinValue);
            DateTime fin    = fechaFin.ToDateTime(TimeOnly.MaxValue);

            var estadosCancelados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "cancelada", "cancelado", "no asistio", "no asistió", "no_asistida" };

            return await _context.Citas
                .AsNoTracking()
                .Where(c =>
                    c.IdProfesional == idProfesional &&
                    c.FechaHora >= inicio &&
                    c.FechaHora <= fin &&
                    !estadosCancelados.Contains(c.Estado ?? string.Empty))
                .OrderBy(c => c.FechaHora)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo consultar citas afectadas para ausencia del profesional {Id}", idProfesional);
            return [];
        }
    }

    public async Task<ProfesionalApiOperationResult> ActualizarServiciosAsync(
        int id,
        IReadOnlyCollection<ServicioProfesionalAsignacionApiRequest> servicios,
        CancellationToken ct = default)
    {
        if (servicios is null)
            return ProfesionalApiOperationResult.Fail("Debe enviar la lista de servicios.", 422);

        bool profesionalExiste = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);
        if (!profesionalExiste)
            return ProfesionalApiOperationResult.NotFound();

        if (servicios.Any(s => s is null || s.IdServicio <= 0 || s.PrecioPersonalizado < 0) ||
            servicios.Select(s => s.IdServicio).Distinct().Count() != servicios.Count)
            return ProfesionalApiOperationResult.Fail("La lista de servicios contiene datos inválidos.", 422);

        var idsServicio = servicios.Select(s => s.IdServicio).ToArray();
        var catalogo = await _context.Servicios.AsNoTracking()
            .Where(s => idsServicio.Contains(s.IdServicio))
            .Select(s => new { s.IdServicio, s.Estado, s.IdEspecialidad })
            .ToListAsync(ct);

        if (catalogo.Count != idsServicio.Length || catalogo.Any(s => s.Estado != "activo"))
            return ProfesionalApiOperationResult.Fail("Uno o más servicios no existen o están inactivos.", 422);

        var especialidadesProfesional = await _context.ProfesionalEspecialidades.AsNoTracking()
            .Where(pe => pe.IdProfesional == id)
            .Select(pe => pe.IdEspecialidad)
            .ToListAsync(ct);

        if (catalogo.Any(s => !s.IdEspecialidad.HasValue ||
                             !especialidadesProfesional.Contains(s.IdEspecialidad.Value)))
            return ProfesionalApiOperationResult.Fail(
                "Uno o más servicios no tienen una especialidad compatible con el profesional.", 422);

        var existentes = await _context.ProfesionalServicios
            .Where(ps => ps.IdProfesional == id)
            .ToListAsync(ct);
        var existentesPorServicio = existentes.ToDictionary(ps => ps.IdServicio);
        var idsSolicitados = idsServicio.ToHashSet();

        foreach (var existente in existentes)
        {
            if (!idsSolicitados.Contains(existente.IdServicio))
                existente.Activo = false;
        }

        foreach (var solicitud in servicios)
        {
            if (existentesPorServicio.TryGetValue(solicitud.IdServicio, out var existente))
            {
                existente.PrecioPersonalizado = solicitud.PrecioPersonalizado;
                existente.Activo = solicitud.Activo;
            }
            else
            {
                _context.ProfesionalServicios.Add(new ProfesionalServicio
                {
                    IdProfesional = id,
                    IdServicio = solicitud.IdServicio,
                    PrecioPersonalizado = solicitud.PrecioPersonalizado,
                    Activo = solicitud.Activo
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        return ProfesionalApiOperationResult.Ok("Servicios del profesional actualizados correctamente.");
    }

    public async Task<ProfesionalApiCollectionResult<ServicioProfesionalApiDto>> ObtenerServiciosAsync(
        int id,
        CancellationToken ct = default)
    {
        bool profesionalActivo = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id && p.Estado == "activo", ct);
        if (!profesionalActivo)
            return ProfesionalApiCollectionResult<ServicioProfesionalApiDto>.Fail(
                "Profesional no encontrado o inactivo.", 404);

        var servicios = await (
            from ps in _context.ProfesionalServicios.AsNoTracking()
            join servicio in _context.Servicios.AsNoTracking()
                on ps.IdServicio equals servicio.IdServicio
            where ps.IdProfesional == id
                && ps.Activo
                && servicio.Estado == "activo"
                && servicio.IdEspecialidad.HasValue
                && _context.ProfesionalEspecialidades.Any(pe =>
                    pe.IdProfesional == id &&
                    pe.IdEspecialidad == servicio.IdEspecialidad.Value)
            orderby servicio.Nombre
            select new ServicioProfesionalApiDto
            {
                IdProfesional = id,
                IdServicio = servicio.IdServicio,
                NombreServicio = servicio.Nombre,
                PrecioBase = servicio.Precio,
                PrecioPersonalizado = ps.PrecioPersonalizado,
                PrecioEfectivo = ps.PrecioPersonalizado ?? servicio.Precio,
                Activo = true
            }
        ).ToListAsync(ct);

        return ProfesionalApiCollectionResult<ServicioProfesionalApiDto>.Ok(servicios);
    }

    private static ProfesionalApiDto MapToDto(Profesional p) => new()
    {
        IdProfesional = p.IdProfesional,
        IdUsuario = p.IdUsuario ?? 0,
        Nombres = p.Usuario is { Nombre: { } nombres } && !string.IsNullOrWhiteSpace(nombres)
            ? nombres.Trim()
            : p.Nombres,
        Apellidos = p.Usuario is { Apellidos: { } apellidos } && !string.IsNullOrWhiteSpace(apellidos)
            ? apellidos.Trim()
            : p.Apellidos,
        RegistroMedico = p.RegistroMedico,
        TarjetaProfesional = p.TarjetaProfesional,
        Categoria = p.Categoria,
        Telefono = p.Telefono,
        Descripcion = p.Descripcion,
        Estado = p.Estado,
        FechaIngreso = p.FechaIngreso,
        CorreoAcceso = p.Usuario?.Correo,
        Especialidades = p.Especialidades
            .OrderByDescending(pe => pe.Principal)
            .ThenBy(pe => pe.IdEspecialidad)
            .Where(pe => pe.Especialidad != null)
            .Select(pe => new ProfesionalEspecialidadApiDto
            {
                IdEspecialidad = pe.Especialidad!.IdEspecialidad,
                Nombre = pe.Especialidad.Nombre,
                Principal = pe.Principal
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

        if (string.IsNullOrWhiteSpace(request.TarjetaProfesional) ||
            request.TarjetaProfesional.Trim().Length is < 3 or > 50 ||
            !Regex.IsMatch(request.TarjetaProfesional.Trim(), @"^[A-Za-z0-9\-\. ]+$"))
            return (false, "La tarjeta profesional es obligatoria y debe contener entre 3 y 50 letras, números, puntos, guiones o espacios.");

        if (!string.IsNullOrWhiteSpace(request.Telefono) && !EsTelefonoValido(request.Telefono))
            return (false, "El número de teléfono es inválido. Debe contener entre 7 y 15 dígitos.");

        string? correo = request.CorreoAcceso?.Trim();
        if (esCreacion && string.IsNullOrWhiteSpace(correo))
            return (false, "El correo de acceso es requerido.");

        if (!string.IsNullOrWhiteSpace(correo) &&
            !new EmailAddressAttribute().IsValid(correo))
            return (false, "El correo de acceso no es válido.");

        if (!esCreacion &&
            !string.IsNullOrWhiteSpace(request.ContrasenaAcceso) &&
            !ProfesionalEstadoHelper.EsPasswordValida(request.ContrasenaAcceso))
            return (false, "La contraseña debe tener mínimo 8 caracteres, mayúscula, minúscula, número y símbolo especial.");

        return (true, null);
    }

    private Task<bool> EsCategoriaValidaAsync(string? categoria, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(categoria)
            ? Task.FromResult(true)
            : _context.Especialidades.AsNoTracking()
                .AnyAsync(e => e.Nombre == categoria.Trim(), ct);

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

    public Task<SmileTrack_MVC.Models.DTOs.ReporteComisionProfesionalDto> CalcularComisionesAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        decimal porcentajeComision = 40,
        CancellationToken ct = default)
        => _comisionService.CalcularComisionesAsync(
            idProfesional, fechaInicio, fechaFin, porcentajeComision, ct);

    public Task<SmileTrack_MVC.Models.DTOs.ResultadoReasignacionAusenciaDto> RegistrarAusenciaConReasignacionAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        string motivo,
        int? idUsuarioActor = null,
        string? observaciones = null,
        CancellationToken ct = default)
        => _ausenciaService.RegistrarAusenciaConReasignacionAsync(
            idProfesional, fechaInicio, fechaFin, motivo, idUsuarioActor, observaciones, ct);
    // ── Excepciones internas ──────────────────────────────────────────────────

    private sealed class CorreoDuplicadoException : Exception { }
    private sealed class ProfesionalNotFoundException : Exception { }
}
