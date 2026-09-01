using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.CentroDeAyuda;

public sealed class CentroDeAyudaService : ICentroDeAyudaService
{
    private static readonly string[] Categorias = ["incidente", "consulta", "solicitud", "otro"];
    private static readonly string[] Modulos = ["citas", "pacientes", "facturacion", "reportes", "sistema"];
    private static readonly string[] Severidades = ["baja", "media", "alta"];
    private static readonly string[] Estados = ["abierto", "en_proceso", "resuelto", "cerrado"];

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CentroDeAyudaService> _logger;

    public CentroDeAyudaService(
        AppDbContext db,
        IWebHostEnvironment environment,
        ILogger<CentroDeAyudaService> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<CentroAyudaTicketDto> Items, int TotalCount)> ObtenerTicketsAsync(
        int page, int pageSize, string? search, string? estado, string? categoria, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.TicketsSoporte
            .AsNoTracking()
            .Include(t => t.Usuario)
            .Include(t => t.AtendidoPorUsuario)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(t =>
                t.Referencia.Contains(search) ||
                t.Asunto.Contains(search) ||
                t.Descripcion.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(t => t.Estado == estado.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(t => t.Categoria == categoria.Trim().ToLowerInvariant());

        var total = await query.CountAsync(ct);

        var entities = await query
            .OrderByDescending(t => t.FechaCreacion)
            .ThenByDescending(t => t.IdTicket)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (entities.Select(ToDto).ToList(), total);
    }

    public async Task<CentroAyudaTicketDto?> ObtenerTicketAsync(int id, CancellationToken ct)
    {
        var entity = await _db.TicketsSoporte
            .AsNoTracking()
            .Include(t => t.Usuario)
            .Include(t => t.AtendidoPorUsuario)
            .FirstOrDefaultAsync(t => t.IdTicket == id, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<(CentroAyudaTicketDto? Data, string? Error)> CrearTicketAsync(
        CentroAyudaTicketRequest request, int usuarioId, CancellationToken ct)
    {
        var validationError = Validate(request.Categoria, request.ModuloAfectado, request.Severidad);
        if (validationError is not null)
            return (null, validationError);

        if (!await _db.Usuarios.AnyAsync(u => u.IdUsuario == usuarioId && u.Estado == "activo", ct))
            return (null, "El usuario autenticado no existe o está inactivo.");

        var ticket = new TicketSoporte
        {
            Referencia = await GenerarReferenciaAsync(ct),
            IdUsuario = usuarioId,
            Asunto = request.Asunto.Trim(),
            Categoria = request.Categoria.Trim().ToLowerInvariant(),
            ModuloAfectado = request.ModuloAfectado.Trim().ToLowerInvariant(),
            Severidad = request.Severidad.Trim().ToLowerInvariant(),
            Descripcion = request.Descripcion.Trim(),
            Estado = "abierto",
            FechaCreacion = DateTime.Now
        };

        ticket.CapturaPantalla = await GuardarAdjuntoAsync(request.CapturaPantalla, ct);

        _db.TicketsSoporte.Add(ticket);
        await _db.SaveChangesAsync(ct);

        return (await ObtenerTicketAsync(ticket.IdTicket, ct), null);
    }

    public async Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> ActualizarTicketAsync(
        int id, CentroAyudaTicketUpdateRequest request, int usuarioId, CancellationToken ct)
    {
        var validationError = Validate(request.Categoria, request.ModuloAfectado, request.Severidad, request.Estado);
        if (validationError is not null)
            return (null, validationError, 422);

        var ticket = await _db.TicketsSoporte.FirstOrDefaultAsync(t => t.IdTicket == id, ct);
        if (ticket is null)
            return (null, "Ticket no encontrado.", 404);

        ticket.Asunto = request.Asunto.Trim();
        ticket.Categoria = request.Categoria.Trim().ToLowerInvariant();
        ticket.ModuloAfectado = request.ModuloAfectado.Trim().ToLowerInvariant();
        ticket.Severidad = request.Severidad.Trim().ToLowerInvariant();
        ticket.Descripcion = request.Descripcion.Trim();
        ticket.Estado = request.Estado.Trim().ToLowerInvariant();

        if (request.Respuesta is not null)
        {
            ticket.Respuesta = request.Respuesta.Trim();
            ticket.FechaRespuesta = DateTime.Now;
            ticket.AtendidoPor = usuarioId;
        }

        if (request.CapturaPantalla is not null)
        {
            var oldFile = ticket.CapturaPantalla;
            ticket.CapturaPantalla = await GuardarAdjuntoAsync(request.CapturaPantalla, ct);
            EliminarAdjunto(oldFile);
        }

        await _db.SaveChangesAsync(ct);
        return (await ObtenerTicketAsync(id, ct), null, 200);
    }

    public async Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> CambiarEstadoAsync(
        int id, string estado, int usuarioId, CancellationToken ct)
    {
        estado = estado.Trim().ToLowerInvariant();
        if (!Estados.Contains(estado))
            return (null, $"Estado inválido. Valores permitidos: {string.Join(", ", Estados)}.", 422);

        var ticket = await _db.TicketsSoporte.FirstOrDefaultAsync(t => t.IdTicket == id, ct);
        if (ticket is null)
            return (null, "Ticket no encontrado.", 404);

        ticket.Estado = estado;
        if (estado is "resuelto" or "cerrado")
        {
            ticket.FechaRespuesta ??= DateTime.Now;
            ticket.AtendidoPor = usuarioId;
        }

        await _db.SaveChangesAsync(ct);
        return (await ObtenerTicketAsync(id, ct), null, 200);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> EliminarTicketAsync(int id, CancellationToken ct)
    {
        var ticket = await _db.TicketsSoporte.FirstOrDefaultAsync(t => t.IdTicket == id, ct);
        if (ticket is null)
            return (false, "Ticket no encontrado.", 404);

        var attachment = ticket.CapturaPantalla;
        _db.TicketsSoporte.Remove(ticket);
        await _db.SaveChangesAsync(ct);
        EliminarAdjunto(attachment);

        return (true, null, 204);
    }

    private static string? Validate(string categoria, string modulo, string severidad, string? estado = null)
    {
        if (!Categorias.Contains(categoria.Trim().ToLowerInvariant()))
            return $"Categoría inválida. Valores permitidos: {string.Join(", ", Categorias)}.";
        if (!Modulos.Contains(modulo.Trim().ToLowerInvariant()))
            return $"Módulo inválido. Valores permitidos: {string.Join(", ", Modulos)}.";
        if (!Severidades.Contains(severidad.Trim().ToLowerInvariant()))
            return $"Severidad inválida. Valores permitidos: {string.Join(", ", Severidades)}.";
        if (estado is not null && !Estados.Contains(estado.Trim().ToLowerInvariant()))
            return $"Estado inválido. Valores permitidos: {string.Join(", ", Estados)}.";
        return null;
    }

    private async Task<string> GenerarReferenciaAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var reference = $"ST-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            if (!await _db.TicketsSoporte.AnyAsync(t => t.Referencia == reference, ct))
                return reference;
        }
        return $"ST-{DateTime.Now:yyyyMMddHHmmssfff}";
    }

    private async Task<string?> GuardarAdjuntoAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return null;

        const long maxBytes = 5 * 1024 * 1024;
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };

        if (file.Length > maxBytes)
            throw new InvalidOperationException("La captura de pantalla no puede superar 5 MB.");

        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("La captura debe ser una imagen JPG, PNG, WEBP o GIF.");

        var folder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "centro-ayuda");
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(file.FileName);
        var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, safeName);

        await using var stream = new FileStream(fullPath, FileMode.CreateNew);
        await file.CopyToAsync(stream, ct);

        return $"/uploads/centro-ayuda/{safeName}";
    }

    private void EliminarAdjunto(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        try
        {
            var normalized = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var root = Path.GetFullPath(Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "centro-ayuda"));
            var full = Path.GetFullPath(Path.Combine(_environment.WebRootPath ?? "wwwroot", normalized.Replace("uploads" + Path.DirectorySeparatorChar + "centro-ayuda" + Path.DirectorySeparatorChar, "")));

            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                File.Delete(full);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No fue posible eliminar el adjunto {Path}.", relativePath);
        }
    }

    private static CentroAyudaTicketDto ToDto(TicketSoporte t) => new()
    {
        IdTicket = t.IdTicket,
        Referencia = t.Referencia,
        IdUsuario = t.IdUsuario,
        Usuario = t.Usuario is null ? string.Empty : $"{t.Usuario.Nombre} {t.Usuario.Apellidos}".Trim(),
        Asunto = t.Asunto,
        Categoria = t.Categoria,
        ModuloAfectado = t.ModuloAfectado,
        Severidad = t.Severidad,
        Descripcion = t.Descripcion,
        CapturaPantalla = t.CapturaPantalla,
        Estado = t.Estado,
        FechaCreacion = t.FechaCreacion,
        FechaRespuesta = t.FechaRespuesta,
        Respuesta = t.Respuesta,
        AtendidoPor = t.AtendidoPor,
        AtendidoPorNombre = t.AtendidoPorUsuario is null ? null : $"{t.AtendidoPorUsuario.Nombre} {t.AtendidoPorUsuario.Apellidos}".Trim()
    };
}
