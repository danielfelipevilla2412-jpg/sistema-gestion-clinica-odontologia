using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Pacientes;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Yeray - Lógica de negocio de la API REST de Pacientes (api/v1/pacientes),
/// extraída de GestionPacientesController para poder reutilizarla desde el
/// ApiController sin duplicar reglas.
/// </summary>
public sealed class PacienteService : IPacienteService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PacienteService> _logger;

    public PacienteService(AppDbContext context, ILogger<PacienteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Listado paginado ──────────────────────────────────────────────────────

    public async Task<PacientesApiResult> ObtenerAsync(
        string? search,
        string? estado,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        try
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            IQueryable<Paciente> query = _context.Pacientes.AsNoTracking();

            if (string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(p => p.Estado == "activo");
            }
            else if (!string.Equals(estado, "todos", StringComparison.OrdinalIgnoreCase))
            {
                string estadoNormalizado = estado.Trim().ToLowerInvariant();
                query = query.Where(p => p.Estado == estadoNormalizado);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string texto = search.Trim();
                query = query.Where(p =>
                    p.Nombres.Contains(texto) ||
                    p.Apellidos.Contains(texto) ||
                    p.Documento.Contains(texto));
            }

            int totalRecords = await query.CountAsync(ct);

            var pacientesPagina = await query
                .OrderBy(p => p.Apellidos)
                .ThenBy(p => p.Nombres)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var idsPagina = pacientesPagina.Select(p => p.IdPaciente).ToList();

            // Se traen todas las citas de la página en una sola consulta (evita N+1).
            var citasRelacionadas = await _context.Citas
                .AsNoTracking()
                .Where(c => idsPagina.Contains(c.IdPaciente))
                .ToListAsync(ct);

            var ahora = DateTime.Now;

            var data = pacientesPagina.Select(p =>
            {
                var citasPaciente = citasRelacionadas.Where(c => c.IdPaciente == p.IdPaciente).ToList();

                var ultima = citasPaciente
                    .Where(c => c.FechaHora <= ahora)
                    .OrderByDescending(c => c.FechaHora)
                    .FirstOrDefault();

                var proxima = citasPaciente
                    .Where(c => c.FechaHora > ahora && c.Estado != "Cancelada")
                    .OrderBy(c => c.FechaHora)
                    .FirstOrDefault();

                return MapearDto(p, ultima?.FechaHora, proxima?.FechaHora);
            }).ToList();

            return PacientesApiResult.Ok(data, totalRecords, page, pageSize);
        }
        catch (OperationCanceledException)
        {
            return PacientesApiResult.Fail("Operación cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ObtenerAsync (página={Page}, búsqueda={Search}).", page, search);
            return PacientesApiResult.Fail("Error interno al listar pacientes.");
        }
    }

    // ── Obtener por ID ────────────────────────────────────────────────────────

    public async Task<PacienteApiDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        var paciente = await _context.Pacientes.AsNoTracking().FirstOrDefaultAsync(p => p.IdPaciente == id, ct);
        if (paciente is null) return null;

        var citasPaciente = await _context.Citas.AsNoTracking().Where(c => c.IdPaciente == id).ToListAsync(ct);
        var ahora = DateTime.Now;

        var ultima = citasPaciente.Where(c => c.FechaHora <= ahora).OrderByDescending(c => c.FechaHora).FirstOrDefault();
        var proxima = citasPaciente.Where(c => c.FechaHora > ahora && c.Estado != "Cancelada").OrderBy(c => c.FechaHora).FirstOrDefault();

        return MapearDto(paciente, ultima?.FechaHora, proxima?.FechaHora);
    }

    // ── Crear ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea un paciente vía JSON. El AnyAsync() cubre el caso normal (409 con
    /// mensaje claro); el catch de DbUpdateException cubre la condición de
    /// carrera real apoyándose en la restricción UNIQUE que "documento" ya
    /// tiene en SQL Server.
    /// </summary>
    public async Task<PacienteApiOperationResult> CrearAsync(
        PacienteApiCreateDto dto,
        CancellationToken ct = default)
    {
        try
        {
            string documentoNormalizado = dto.Documento.Trim();

            bool existe = await _context.Pacientes.AnyAsync(p => p.Documento == documentoNormalizado, ct);
            if (existe)
                return PacienteApiOperationResult.Conflict("Ya existe un paciente con ese documento.");

            var paciente = new Paciente
            {
                TipoDocumento = string.IsNullOrWhiteSpace(dto.TipoDocumento) ? "CC" : dto.TipoDocumento.Trim(),
                Documento = documentoNormalizado,
                Nombres = dto.Nombres.Trim(),
                Apellidos = dto.Apellidos.Trim(),
                FechaNacimiento = dto.FechaNacimiento,
                Genero = string.IsNullOrWhiteSpace(dto.Genero) ? null : dto.Genero.Trim().ToUpperInvariant(),
                Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
                Correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim(),
                Ciudad = string.IsNullOrWhiteSpace(dto.Ciudad) ? null : dto.Ciudad.Trim(),
                GrupoSanguineo = string.IsNullOrWhiteSpace(dto.GrupoSanguineo) ? null : dto.GrupoSanguineo.Trim(),
                Alergias = string.IsNullOrWhiteSpace(dto.Alergias) ? null : dto.Alergias.Trim(),
                AntecedentesMedicos = string.IsNullOrWhiteSpace(dto.AntecedentesMedicos) ? null : dto.AntecedentesMedicos.Trim(),
                ContactoEmergencia = string.IsNullOrWhiteSpace(dto.ContactoEmergencia) ? null : dto.ContactoEmergencia.Trim(),
                TelefonoEmergencia = string.IsNullOrWhiteSpace(dto.TelefonoEmergencia) ? null : dto.TelefonoEmergencia.Trim(),
                Estado = "activo",
                FechaRegistro = DateTime.UtcNow.Date
            };

            _context.Pacientes.Add(paciente);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx) when (EsViolacionDeDocumentoDuplicado(dbEx))
            {
                _logger.LogWarning(dbEx, "Documento duplicado detectado por la restricción UNIQUE (concurrencia) al crear paciente.");
                return PacienteApiOperationResult.Conflict("Ya existe un paciente con ese documento.");
            }

            return PacienteApiOperationResult.Ok(
                "Paciente registrado correctamente.",
                MapearDto(paciente, null, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearAsync.");
            return PacienteApiOperationResult.Fail("No fue posible registrar el paciente.", 500);
        }
    }

    // ── Actualizar (parcial) ─────────────────────────────────────────────────

    /// <summary>
    /// Actualización parcial (solo se tocan los campos que vengan distintos de
    /// null). Se expone como PATCH: PUT implicaría reemplazar el recurso completo.
    /// </summary>
    public async Task<PacienteApiOperationResult> ActualizarAsync(
        int id,
        PacienteApiUpdateDto dto,
        CancellationToken ct = default)
    {
        try
        {
            var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == id, ct);
            if (paciente is null)
                return PacienteApiOperationResult.NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Nombres)) paciente.Nombres = dto.Nombres.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Apellidos)) paciente.Apellidos = dto.Apellidos.Trim();

            if (dto.Telefono is not null)
                paciente.Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
            if (dto.Correo is not null)
                paciente.Correo = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo.Trim();
            if (dto.Ciudad is not null)
                paciente.Ciudad = string.IsNullOrWhiteSpace(dto.Ciudad) ? null : dto.Ciudad.Trim();
            if (dto.GrupoSanguineo is not null)
                paciente.GrupoSanguineo = string.IsNullOrWhiteSpace(dto.GrupoSanguineo) ? null : dto.GrupoSanguineo.Trim();
            if (dto.Alergias is not null)
                paciente.Alergias = string.IsNullOrWhiteSpace(dto.Alergias) ? null : dto.Alergias.Trim();
            if (dto.AntecedentesMedicos is not null)
                paciente.AntecedentesMedicos = string.IsNullOrWhiteSpace(dto.AntecedentesMedicos) ? null : dto.AntecedentesMedicos.Trim();
            if (dto.ContactoEmergencia is not null)
                paciente.ContactoEmergencia = string.IsNullOrWhiteSpace(dto.ContactoEmergencia) ? null : dto.ContactoEmergencia.Trim();
            if (dto.TelefonoEmergencia is not null)
                paciente.TelefonoEmergencia = string.IsNullOrWhiteSpace(dto.TelefonoEmergencia) ? null : dto.TelefonoEmergencia.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Genero))
                paciente.Genero = dto.Genero.Trim().ToUpperInvariant();

            await _context.SaveChangesAsync(ct);

            return PacienteApiOperationResult.Ok(
                "Paciente actualizado correctamente.",
                MapearDto(paciente, null, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ActualizarAsync (id={Id}).", id);
            return PacienteApiOperationResult.Fail("No fue posible actualizar el paciente.", 500);
        }
    }

    // ── Cambiar estado ───────────────────────────────────────────────────────

    /// <summary>Cambia el estado (nunca se borra físicamente: el paciente tiene citas e historia clínica relacionadas).</summary>
    public async Task<PacienteApiOperationResult> CambiarEstadoAsync(
        int id,
        string estado,
        CancellationToken ct = default)
    {
        try
        {
            var paciente = await _context.Pacientes.FirstOrDefaultAsync(p => p.IdPaciente == id, ct);
            if (paciente is null)
                return PacienteApiOperationResult.NotFound();

            paciente.Estado = estado.Trim().ToLowerInvariant();
            await _context.SaveChangesAsync(ct);

            return PacienteApiOperationResult.Ok(
                $"El estado del paciente fue actualizado a '{paciente.Estado}'.",
                MapearDto(paciente, null, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CambiarEstadoAsync (id={Id}).", id);
            return PacienteApiOperationResult.Fail("No fue posible cambiar el estado del paciente.", 500);
        }
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    private static bool EsViolacionDeDocumentoDuplicado(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627)
            return true;

        string mensaje = ex.InnerException?.Message ?? ex.Message;
        return mensaje.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) &&
               mensaje.Contains("documento", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Mapea la entidad Paciente al DTO de salida de la API.</summary>
    private static PacienteApiDto MapearDto(
        Paciente p,
        DateTime? ultimaConsulta,
        DateTime? proximaConsulta) => new()
    {
        IdPaciente = p.IdPaciente,
        TipoDocumento = p.TipoDocumento,
        Documento = p.Documento,
        Nombres = p.Nombres,
        Apellidos = p.Apellidos,
        NombreCompleto = p.NombresCompleto,
        FechaNacimiento = p.FechaNacimiento,
        Genero = p.Genero,
        Telefono = p.Telefono,
        Correo = p.Correo,
        Ciudad = p.Ciudad,
        GrupoSanguineo = p.GrupoSanguineo,
        Alergias = string.IsNullOrWhiteSpace(p.Alergias)
            ? []
            : p.Alergias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        AntecedentesMedicos = p.AntecedentesMedicos,
        ContactoEmergencia = p.ContactoEmergencia,
        TelefonoEmergencia = p.TelefonoEmergencia,
        Estado = p.Estado,
        FechaRegistro = p.FechaRegistro,
        UltimaConsulta = ultimaConsulta,
        ProximaConsulta = proximaConsulta
    };
}