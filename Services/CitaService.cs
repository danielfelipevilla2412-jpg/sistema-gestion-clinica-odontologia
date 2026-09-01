using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services;

/// <summary>
/// Implementación del servicio de citas con la lógica de negocio extraída del controlador,
/// manteniendo el mismo comportamiento del módulo legacy sin cambiar rutas ni contratos HTTP.
/// </summary>
public class CitaService : ICitaService
{
    private const int DuracionCitaMinutos = 60;

    private readonly AppDbContext _context;
    private readonly ILogger<CitaService> _logger;

    public CitaService(AppDbContext context, ILogger<CitaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Cita>> ObtenerAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.Citas
            .AsNoTracking()
            .OrderByDescending(c => c.FechaHora)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<Cita?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Citas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCita == id, ct);
    }

    public async Task<Cita> CrearAsync(CitaApiRequest request, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.IdPaciente <= 0)
            throw new InvalidOperationException("El paciente seleccionado no es válido.");

        if (request.IdProfesional is <= 0 ||
            !await _context.Profesionales.AnyAsync(p => p.IdProfesional == request.IdProfesional && p.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El profesional seleccionado no está disponible.");
        }

        if (request.IdServicio is <= 0 ||
            !await _context.Servicios.AnyAsync(s => s.IdServicio == request.IdServicio && s.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El servicio seleccionado no está disponible.");
        }

        if (request.IdConsultorio is <= 0 ||
            !await _context.Consultorios.AnyAsync(c => c.IdConsultorio == request.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
        {
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
        }

        if (request.FechaHora < DateTime.Now.AddMinutes(-5))
        {
            throw new InvalidOperationException("No se puede agendar una cita en un horario pasado.");
        }

        if (await HayConflictoHorarioAsync(request.IdProfesional, request.FechaHora, ct: ct))
        {
            throw new InvalidOperationException("El profesional ya tiene una cita asignada en ese horario. Seleccione otro horario.");
        }

        string estadoSolicitud = string.IsNullOrWhiteSpace(request.Estado) ? "programada" : request.Estado.Trim();
        var estadoEntidad = await _context.EstadosCita
            .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                (estadoSolicitud.ToLower() == "agendada" && e.NombreEstado.ToLower() == "programada"),
                ct);

        if (estadoEntidad is null)
            throw new InvalidOperationException("El estado de la cita no es válido.");

        var cita = new Cita
        {
            IdPaciente = request.IdPaciente,
            IdProfesional = request.IdProfesional,
            IdServicio = request.IdServicio,
            IdConsultorio = request.IdConsultorio,
            FechaHora = request.FechaHora,
            IdEstado = estadoEntidad.IdEstado,
            Estado = estadoEntidad.NombreEstado,
            Notas = request.Notas?.Trim()
        };

        _context.Citas.Add(cita);
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    public async Task<Cita?> ActualizarAsync(int id, CitaApiUpdateDto request, CancellationToken ct = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (id != request.IdCita || request.IdPaciente <= 0)
            throw new InvalidOperationException("Datos de cita inválidos.");

        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        if (request.IdProfesional is <= 0 ||
            !await _context.Profesionales.AnyAsync(p => p.IdProfesional == request.IdProfesional && p.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El profesional seleccionado no es válido.");
        }

        if (request.IdServicio is <= 0 ||
            !await _context.Servicios.AnyAsync(s => s.IdServicio == request.IdServicio && s.Estado == "activo", ct))
        {
            throw new InvalidOperationException("El servicio seleccionado no es válido.");
        }

        if (request.IdConsultorio is <= 0 ||
            !await _context.Consultorios.AnyAsync(c => c.IdConsultorio == request.IdConsultorio && (c.Estado == "disponible" || c.Estado == "activo"), ct))
        {
            throw new InvalidOperationException("El consultorio seleccionado no está disponible.");
        }

        if (request.FechaHora < DateTime.Now.AddMinutes(-5))
        {
            throw new InvalidOperationException("No se puede mover la cita a un horario pasado.");
        }

        if (await HayConflictoHorarioAsync(request.IdProfesional, request.FechaHora, id, ct))
        {
            throw new InvalidOperationException("El profesional ya tiene otra cita en ese horario.");
        }

        cita.IdPaciente = request.IdPaciente;
        cita.IdProfesional = request.IdProfesional;
        cita.IdServicio = request.IdServicio;
        cita.IdConsultorio = request.IdConsultorio;
        cita.FechaHora = request.FechaHora;

        if (request.IdEstado is > 0)
        {
            var estadoApi = await _context.EstadosCita.AsNoTracking().FirstOrDefaultAsync(e => e.IdEstado == request.IdEstado.Value, ct);
            if (estadoApi is null)
                throw new InvalidOperationException("El estado de la cita no es válido.");

            cita.IdEstado = estadoApi.IdEstado;
            cita.Estado = estadoApi.NombreEstado;
        }
        else if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            string estadoSolicitud = request.Estado.Trim();
            var estadoApi = await _context.EstadosCita.AsNoTracking()
                .FirstOrDefaultAsync(e => e.NombreEstado.ToLower() == estadoSolicitud.ToLower() ||
                    (estadoSolicitud.ToLower() == "agendada" && e.NombreEstado.ToLower() == "programada"), ct);

            if (estadoApi is null)
                throw new InvalidOperationException("El estado de la cita no es válido.");

            cita.IdEstado = estadoApi.IdEstado;
            cita.Estado = estadoApi.NombreEstado;
        }

        cita.Notas = request.Notas?.Trim();
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    public async Task<Cita?> CambiarEstadoAsync(int id, string nuevoEstado, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string estadoActual = NormalizarEstado(cita.Estado);
        string estadoNuevo = NormalizarEstado(nuevoEstado);

        if (string.IsNullOrWhiteSpace(estadoNuevo) || !EsTransicionEstadoPermitida(estadoActual, estadoNuevo))
            throw new InvalidOperationException(ConstruirMensajeTransicionNoPermitida(estadoActual, estadoNuevo));

        var estadoDestino = await _context.EstadosCita.AsNoTracking().FirstOrDefaultAsync(
            e => NormalizarEstado(e.NombreEstado) == estadoNuevo, ct);

        if (estadoDestino is null)
            throw new InvalidOperationException("El estado seleccionado no existe en el catálogo de estados de citas.");

        cita.IdEstado = estadoDestino.IdEstado;
        cita.Estado = estadoDestino.NombreEstado;
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    public async Task<Cita?> ActualizarNotasAsync(int id, string notas, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return null;

        string notasNuevas = (notas ?? string.Empty).Trim();
        if (notasNuevas.Length > 4000)
            throw new InvalidOperationException("Las notas no pueden superar los 4000 caracteres.");

        cita.Notas = string.IsNullOrWhiteSpace(notasNuevas) ? null : notasNuevas;
        await _context.SaveChangesAsync(ct);
        return cita;
    }

    public async Task<bool> CancelarAsync(int id, CancellationToken ct = default)
    {
        var cita = await _context.Citas.FirstOrDefaultAsync(c => c.IdCita == id, ct);
        if (cita is null)
            return false;

        if (EsEstadoCancelado(cita.Estado))
            throw new InvalidOperationException("La cita ya está cancelada.");

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

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HayConflictoHorarioAsync(int? idProfesional, DateTime fechaHora, int? idCitaExcluir = null, CancellationToken ct = default)
    {
        if (idProfesional is null || idProfesional <= 0)
            return false;

        DateTime inicio = fechaHora;
        DateTime fin = fechaHora.AddMinutes(DuracionCitaMinutos);

        return await _context.Citas
            .AsNoTracking()
            .AnyAsync(c =>
                c.IdProfesional == idProfesional &&
                c.IdCita != (idCitaExcluir ?? 0) &&
                c.Estado != "cancelada" &&
                c.Estado != "Cancelada" &&
                c.Estado != "cancelado" &&
                c.FechaHora < fin &&
                c.FechaHora.AddMinutes(DuracionCitaMinutos) > inicio,
                ct);
    }

    private static string NormalizarEstado(string? estado)
    {
        var normalizado = (estado ?? string.Empty).Trim().ToLowerInvariant();
        return normalizado switch
        {
            "agendada" => "programada",
            "programado" => "programada",
            "confirmado" => "confirmada",
            "cancelado" => "cancelada",
            "no asistio" => "no_asistida",
            "no asistió" => "no_asistida",
            "no-show" => "no_asistida",
            "completada" => "atendida",
            "realizada" => "atendida",
            _ => normalizado
        };
    }

    private static bool EsTransicionEstadoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "programada" => nuevoEstado is "confirmada" or "cancelada" or "no_asistida",
            "confirmada" => nuevoEstado is "en_proceso" or "cancelada" or "no_asistida",
            "en_proceso" => nuevoEstado == "atendida",
            "atendida" or "finalizada" or "cancelada" or "no_asistida" => false,
            _ => false
        };
    }

    private static string ConstruirMensajeTransicionNoPermitida(string estadoActual, string nuevoEstado)
    {
        return estadoActual switch
        {
            "programada" => "Una cita agendada solo puede pasar a Confirmada, Cancelada o No asistió.",
            "confirmada" => "Una cita confirmada solo puede pasar a En consulta, Cancelada o No asistió.",
            "en_proceso" => "Una cita en consulta solo puede pasar a Atendida.",
            "atendida" or "finalizada" => "Una cita atendida ya está finalizada y no puede regresar a otro estado.",
            "cancelada" => "Una cita cancelada es definitiva y no puede reactivarse desde la agenda del profesional.",
            "no_asistida" => "Una cita marcada como No asistió es definitiva y no puede reactivarse desde la agenda del profesional.",
            _ => $"No está permitido cambiar una cita de '{estadoActual}' a '{nuevoEstado}'."
        };
    }

    private static bool EsEstadoCancelado(string? estado)
    {
        return NormalizarEstado(estado) is "cancelada" or "cancelado";
    }
}
