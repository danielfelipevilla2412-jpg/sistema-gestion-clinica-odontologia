using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.HistoriasClinicas;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Yeray - Lógica de negocio de Historia Clínica, extraída de
/// HistoriaClinicaController para que la compartan las vistas MVC clásicas y
/// la API REST (api/v1/historias-clinicas), sin duplicar reglas.
///
/// Notas_Clinica y Control_Postoperatorio se escriben en la tabla real
/// (fuente de la API) Y también en el JSON de Historia_Clinica.observaciones_generales
/// (compatibilidad temporal con las vistas st-odo-03-historial / st-aux-07-control-postoperato,
/// que aún leen ese JSON). Ver comentarios "Yeray" en los entities NotaClinica y
/// ControlPostoperatorio.
/// </summary>
public sealed class HistoriaClinicaService : IHistoriaClinicaService
{
    private readonly AppDbContext _context;
    private readonly ILogger<HistoriaClinicaService> _logger;

    public HistoriaClinicaService(AppDbContext context, ILogger<HistoriaClinicaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════
    // Lógica interna compartida (antes métodos privados del MVC controller)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<HistoriaClinica> CrearHistoriaClinicaAsync(int pacienteId, CancellationToken ct = default)
    {
        var historiaExistente = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct);
        if (historiaExistente is not null)
        {
            return historiaExistente;
        }

        var historia = new HistoriaClinica
        {
            IdPaciente = pacienteId,
            FechaApertura = DateTime.UtcNow,
            Activa = true,
            ObservacionesGenerales = string.Empty
        };

        _context.HistoriasClinicas.Add(historia);
        await _context.SaveChangesAsync(ct);
        return historia;
    }

    /// <summary>
    /// Guarda el odontograma: cada diente modificado se guarda como fila
    /// individual con trazabilidad real en Registro_Odontograma. Se conserva
    /// también el JSON en ObservacionesGenerales para compatibilidad con el
    /// visor 3D mientras se migra completamente.
    /// </summary>
    public async Task<(bool Success, string Message, int? HistoriaId)> GuardarOdontogramaInternoAsync(
        int pacienteId,
        OdontogramaGuardarRequest request,
        int? idProfesional,
        CancellationToken ct = default)
    {
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct)
            ?? await CrearHistoriaClinicaAsync(pacienteId, ct);

        foreach (var (instanceId, registroPieza) in request.Registros)
        {
            // Obtener número FDI desde el mapeo; si no está mapeado se usa el instanceId
            string numeroFdi = request.MapeoFDI.TryGetValue(instanceId, out string? fdi)
                ? fdi
                : instanceId;

            // Solo el último tratamiento de cada pieza se guarda como estado actual
            var ultimo = registroPieza.Tratamientos.LastOrDefault();
            if (ultimo is null) continue;

            var registroExistente = await _context.RegistrosOdontograma
                .Where(r => r.IdHistoria == historia.IdHistoria && r.NumeroFdi == numeroFdi)
                .OrderByDescending(r => r.FechaRegistro)
                .FirstOrDefaultAsync(ct);

            bool estadoCambio = registroExistente is null
                || !string.Equals(registroExistente.Estado, ultimo.Key, StringComparison.OrdinalIgnoreCase);

            if (estadoCambio)
            {
                _context.RegistrosOdontograma.Add(new RegistroOdontograma
                {
                    IdHistoria = historia.IdHistoria,
                    NumeroFdi = numeroFdi,
                    NombrePieza = registroPieza.NombrePieza,
                    Estado = ultimo.Key ?? "sano",
                    Observacion = ultimo.Obs,
                    FechaRegistro = string.IsNullOrWhiteSpace(ultimo.Fecha)
                        ? DateTime.UtcNow
                        : DateTime.TryParse(ultimo.Fecha, out DateTime fechaParsed)
                            ? fechaParsed
                            : DateTime.UtcNow,
                    IdProfesional = idProfesional,
                    IdCita = null // se puede pasar desde el request en el futuro
                });
            }
        }

        // Mantener JSON en ObservacionesGenerales para compatibilidad con el visor 3D
        var actual = string.IsNullOrWhiteSpace(historia.ObservacionesGenerales)
            ? new JsonObject()
            : (JsonNode.Parse(historia.ObservacionesGenerales) as JsonObject) ?? new JsonObject();

        actual["registros"] = JsonSerializer.SerializeToNode(request.Registros);
        actual["mapeoFDI"] = JsonSerializer.SerializeToNode(request.MapeoFDI);
        actual["actualizadoEn"] = DateTime.UtcNow;
        historia.ObservacionesGenerales = actual.ToJsonString();

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el odontograma (pacienteId={PacienteId}).", pacienteId);
            return (false, "No fue posible guardar el odontograma.", null);
        }

        return (true, "Odontograma guardado correctamente.", historia.IdHistoria);
    }

    /// <summary>
    /// Registra una nota clínica: fila real en Nota_Clinica (fuente para la API)
    /// y, además, entrada en el JSON de ObservacionesGenerales (compatibilidad
    /// temporal con la vista st-odo-03-historial).
    /// </summary>
    public async Task<(bool Success, string Message, NotaClinicaApiDto? Nota)> RegistrarNotaClinicaAsync(
        int pacienteId,
        NotaClinicaGuardarRequest request,
        int? idProfesional,
        string doctorNombre,
        CancellationToken ct = default)
    {
        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct)
            ?? await CrearHistoriaClinicaAsync(pacienteId, ct);

        var entidad = new NotaClinica
        {
            IdHistoria = historia.IdHistoria,
            IdProfesional = idProfesional,
            Fecha = DateTime.UtcNow,
            Diagnostico = request.Diagnostico,
            Procedimiento = request.Procedimiento,
            ProximaCita = request.ProximaCita,
            Estado = "Realizado"
        };
        _context.NotasClinicas.Add(entidad);

        // Compatibilidad: también se agrega al JSON que lee st-odo-03-historial.
        var actual = string.IsNullOrWhiteSpace(historia.ObservacionesGenerales)
            ? new JsonObject()
            : (JsonNode.Parse(historia.ObservacionesGenerales) as JsonObject) ?? new JsonObject();

        var notas = actual["notasClinicas"] as JsonArray ?? new JsonArray();
        var nuevaNotaJson = new JsonObject
        {
            ["titulo"] = request.Procedimiento ?? request.Diagnostico ?? "Nota clínica",
            ["fecha"] = entidad.Fecha.ToString("yyyy-MM-dd"),
            ["doctor"] = doctorNombre,
            ["diagnostico"] = request.Diagnostico ?? "",
            ["procedimiento"] = request.Procedimiento ?? "",
            ["proximaCita"] = request.ProximaCita,
            ["estado"] = entidad.Estado
        };
        notas.Insert(0, nuevaNotaJson);
        actual["notasClinicas"] = notas;
        actual["actualizadoEn"] = DateTime.UtcNow;
        historia.ObservacionesGenerales = actual.ToJsonString();

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar la nota clínica (pacienteId={PacienteId}).", pacienteId);
            return (false, "No fue posible registrar la nota clínica.", null);
        }

        var dto = new NotaClinicaApiDto
        {
            IdNota = entidad.IdNota,
            Fecha = entidad.Fecha,
            Doctor = doctorNombre,
            Diagnostico = entidad.Diagnostico,
            Procedimiento = entidad.Procedimiento,
            ProximaCita = entidad.ProximaCita,
            Estado = entidad.Estado
        };

        return (true, "Nota clínica registrada correctamente.", dto);
    }

    /// <summary>
    /// Registra el control postoperatorio de una cita: fila real en
    /// Control_Postoperatorio (fuente para la API, relación 1 a 1 con Cita) y,
    /// además, entrada en el JSON de ObservacionesGenerales (compatibilidad
    /// temporal con la vista st-aux-07-control-postoperato).
    /// </summary>
    public async Task<(bool Success, string Message)> RegistrarControlPostoperatorioAsync(
        ControlPostoperatorioGuardarRequest request,
        CancellationToken ct = default)
    {
        if (request.CitaId is null)
            return (false, "No se recibió la cita del control postoperatorio.");

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == request.CitaId, ct);
        if (cita is null)
            return (false, "No se encontró la cita indicada.");

        var historia = await _context.HistoriasClinicas
            .FirstOrDefaultAsync(h => h.IdPaciente == cita.IdPaciente && h.Activa, ct)
            ?? await CrearHistoriaClinicaAsync(cita.IdPaciente, ct);

        string instruccionesJson = JsonSerializer.Serialize(
            (request.Instructions ?? []).Select(i => new { text = i.Text ?? "", @checked = i.Checked }));

        var controlExistente = await _context.ControlesPostoperatorios
            .FirstOrDefaultAsync(c => c.IdCita == request.CitaId, ct);

        if (controlExistente is null)
        {
            _context.ControlesPostoperatorios.Add(new ControlPostoperatorio
            {
                IdCita = request.CitaId.Value,
                Status = request.Status ?? "stable",
                InstruccionesJson = instruccionesJson,
                Observaciones = request.Observations ?? "",
                FechaRegistro = DateTime.UtcNow
            });
        }
        else
        {
            controlExistente.Status = request.Status ?? "stable";
            controlExistente.InstruccionesJson = instruccionesJson;
            controlExistente.Observaciones = request.Observations ?? "";
            controlExistente.FechaRegistro = DateTime.UtcNow;
        }

        // Compatibilidad: también se guarda en el JSON que lee st-aux-07-control-postoperato.
        var actual = string.IsNullOrWhiteSpace(historia.ObservacionesGenerales)
            ? new JsonObject()
            : (JsonNode.Parse(historia.ObservacionesGenerales) as JsonObject) ?? new JsonObject();

        var controles = actual["controlesPostoperatorios"] as JsonObject ?? new JsonObject();
        var instructionsArray = new JsonArray();
        foreach (var ins in request.Instructions ?? [])
        {
            instructionsArray.Add(new JsonObject
            {
                ["text"] = ins.Text ?? "",
                ["checked"] = ins.Checked
            });
        }
        controles[request.CitaId.Value.ToString()] = new JsonObject
        {
            ["status"] = request.Status ?? "stable",
            ["instructions"] = instructionsArray,
            ["observations"] = request.Observations ?? ""
        };
        actual["controlesPostoperatorios"] = controles;
        historia.ObservacionesGenerales = actual.ToJsonString();

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar el control postoperatorio (citaId={CitaId}).", request.CitaId);
            return (false, "No fue posible guardar el control postoperatorio.");
        }

        return (true, "Control postoperatorio guardado correctamente.");
    }

    // ═════════════════════════════════════════════════════════════════════
    // Endpoints de la API REST (api/v1/historias-clinicas)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<HistoriaClinicaApiDto?> ObtenerHistoriaApiAsync(int pacienteId, CancellationToken ct = default)
    {
        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct);

        if (historia is null) return null;

        var odontograma = await ObtenerOdontogramaApiAsync(pacienteId, ct);
        return MapearHistoriaDto(historia, odontograma);
    }

    public async Task<HistoriaClinicaApiOperationResult> CrearHistoriaApiAsync(int pacienteId, CancellationToken ct = default)
    {
        try
        {
            bool pacienteExiste = await _context.Pacientes.AnyAsync(p => p.IdPaciente == pacienteId, ct);
            if (!pacienteExiste)
                return HistoriaClinicaApiOperationResult.Fail("El paciente indicado no existe.", 404);

            var historia = await CrearHistoriaClinicaAsync(pacienteId, ct);
            var dto = MapearHistoriaDto(historia, []);

            return HistoriaClinicaApiOperationResult.Ok("Historia clínica creada correctamente.", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearHistoriaApiAsync (pacienteId={PacienteId}).", pacienteId);
            return HistoriaClinicaApiOperationResult.Fail("No fue posible crear la historia clínica.", 500);
        }
    }

    public async Task<List<NotaClinicaApiDto>> ObtenerNotasApiAsync(int pacienteId, CancellationToken ct = default)
    {
        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct);

        if (historia is null) return [];

        return await _context.NotasClinicas
            .AsNoTracking()
            .Include(n => n.Profesional)
            .Where(n => n.IdHistoria == historia.IdHistoria)
            .OrderByDescending(n => n.Fecha)
            .Select(n => new NotaClinicaApiDto
            {
                IdNota = n.IdNota,
                Fecha = n.Fecha,
                Doctor = n.Profesional != null ? $"Dr(a). {n.Profesional.Nombres} {n.Profesional.Apellidos}" : null,
                Diagnostico = n.Diagnostico,
                Procedimiento = n.Procedimiento,
                ProximaCita = n.ProximaCita,
                Estado = n.Estado
            })
            .ToListAsync(ct);
    }

    public async Task<HistoriaClinicaApiOperationResult> CrearNotaApiAsync(
        int pacienteId,
        NotaClinicaApiCreateDto dto,
        int? idProfesional,
        string doctorNombre,
        CancellationToken ct = default)
    {
        try
        {
            bool pacienteExiste = await _context.Pacientes.AnyAsync(p => p.IdPaciente == pacienteId, ct);
            if (!pacienteExiste)
                return HistoriaClinicaApiOperationResult.Fail("El paciente indicado no existe.", 404);

            var request = new NotaClinicaGuardarRequest
            {
                PacienteId = pacienteId,
                Diagnostico = dto.Diagnostico,
                Procedimiento = dto.Procedimiento,
                ProximaCita = dto.ProximaCita
            };

            var (success, message, nota) = await RegistrarNotaClinicaAsync(pacienteId, request, idProfesional, doctorNombre, ct);

            return success
                ? HistoriaClinicaApiOperationResult.Ok(message, nota: nota)
                : HistoriaClinicaApiOperationResult.Fail(message, 500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CrearNotaApiAsync (pacienteId={PacienteId}).", pacienteId);
            return HistoriaClinicaApiOperationResult.Fail("No fue posible registrar la nota clínica.", 500);
        }
    }

    public async Task<List<OdontogramaPiezaApiDto>> ObtenerOdontogramaApiAsync(int pacienteId, CancellationToken ct = default)
    {
        var historia = await _context.HistoriasClinicas
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.IdPaciente == pacienteId && h.Activa, ct);

        if (historia is null) return [];

        return await _context.RegistrosOdontograma
            .AsNoTracking()
            .Include(r => r.Profesional)
            .Where(r => r.IdHistoria == historia.IdHistoria)
            .OrderBy(r => r.NumeroFdi)
            .Select(r => new OdontogramaPiezaApiDto
            {
                NumeroFdi = r.NumeroFdi,
                NombrePieza = r.NombrePieza,
                Estado = r.Estado,
                Observacion = r.Observacion,
                FechaRegistro = r.FechaRegistro,
                Profesional = r.Profesional != null ? $"Dr(a). {r.Profesional.Nombres} {r.Profesional.Apellidos}" : null
            })
            .ToListAsync(ct);
    }

    public async Task<HistoriaClinicaApiOperationResult> GuardarOdontogramaApiAsync(
        int pacienteId,
        OdontogramaGuardarRequest request,
        int? idProfesional,
        CancellationToken ct = default)
    {
        try
        {
            bool pacienteExiste = await _context.Pacientes.AnyAsync(p => p.IdPaciente == pacienteId, ct);
            if (!pacienteExiste)
                return HistoriaClinicaApiOperationResult.Fail("El paciente indicado no existe.", 404);

            request.PacienteId = pacienteId;
            var (success, message, historiaId) = await GuardarOdontogramaInternoAsync(pacienteId, request, idProfesional, ct);

            if (!success || historiaId is null)
                return HistoriaClinicaApiOperationResult.Fail(message, 500);

            var odontograma = await ObtenerOdontogramaApiAsync(pacienteId, ct);
            var historia = await _context.HistoriasClinicas.AsNoTracking().FirstAsync(h => h.IdHistoria == historiaId, ct);

            return HistoriaClinicaApiOperationResult.Ok(message, MapearHistoriaDto(historia, odontograma));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GuardarOdontogramaApiAsync (pacienteId={PacienteId}).", pacienteId);
            return HistoriaClinicaApiOperationResult.Fail("No fue posible guardar el odontograma.", 500);
        }
    }

    public async Task<HistoriaClinicaApiOperationResult> CambiarEstadoApiAsync(int idHistoria, bool activa, CancellationToken ct = default)
    {
        try
        {
            var historia = await _context.HistoriasClinicas.FirstOrDefaultAsync(h => h.IdHistoria == idHistoria, ct);
            if (historia is null)
                return HistoriaClinicaApiOperationResult.NotFound();

            historia.Activa = activa;
            await _context.SaveChangesAsync(ct);

            var odontograma = await ObtenerOdontogramaApiAsync(historia.IdPaciente, ct);
            return HistoriaClinicaApiOperationResult.Ok(
                $"La historia clínica fue {(activa ? "activada" : "desactivada")} correctamente.",
                MapearHistoriaDto(historia, odontograma));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CambiarEstadoApiAsync (idHistoria={IdHistoria}).", idHistoria);
            return HistoriaClinicaApiOperationResult.Fail("No fue posible cambiar el estado de la historia clínica.", 500);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Ownership (rol Paciente)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<bool> EsPropioPacienteAsync(int pacienteId, int idUsuario, CancellationToken ct = default)
    {
        return await _context.Pacientes
            .AnyAsync(p => p.IdPaciente == pacienteId && p.IdUsuario == idUsuario, ct);
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private static HistoriaClinicaApiDto MapearHistoriaDto(HistoriaClinica historia, List<OdontogramaPiezaApiDto> odontograma) => new()
    {
        IdHistoria = historia.IdHistoria,
        IdPaciente = historia.IdPaciente,
        FechaApertura = historia.FechaApertura,
        Activa = historia.Activa,
        Odontograma = odontograma
    };
}