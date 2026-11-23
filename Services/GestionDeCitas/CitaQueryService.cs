using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.Shared;

namespace SmileTrack_MVC.Services;

public sealed class CitaQueryService : ICitaQueryService
{
    private readonly AppDbContext _context;

    public CitaQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CitasKpiDto> ObtenerKpisGestionAsync(
        DateTime fechaReferencia,
        CancellationToken ct = default)
    {
        var inicioMes = new DateTime(fechaReferencia.Year, fechaReferencia.Month, 1);
        var finMes = inicioMes.AddMonths(1);
        var inicioSemana = fechaReferencia.Date.AddDays(-(((int)fechaReferencia.DayOfWeek + 6) % 7));
        var finSemana = inicioSemana.AddDays(7);
        var inicioSemanaAnterior = inicioSemana.AddDays(-7);

        var citas = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora >= inicioMes && c.FechaHora < finMes)
            .Select(c => new { c.FechaHora, c.Estado })
            .ToListAsync(ct);

        int programadas = citas.Count(c => NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int canceladas = citas.Count(c => EstadoCitaHelper.EsEstadoCancelado(c.Estado));
        int atendidas = citas.Count(c => NormalizarEstado(c.Estado) == "atendida");
        int actual = citas.Count(c => c.FechaHora >= inicioSemana && c.FechaHora < finSemana && NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int anterior = citas.Count(c => c.FechaHora >= inicioSemanaAnterior && c.FechaHora < inicioSemana && NormalizarEstado(c.Estado) is "programada" or "agendada" or "confirmada");
        int total = citas.Count;

        return new CitasKpiDto
        {
            Total = total,
            Programadas = programadas,
            Canceladas = canceladas,
            Atendidas = atendidas,
            DiferenciaSemana = actual - anterior,
            TasaCancelacion = total > 0 ? (int)Math.Round(canceladas * 100.0 / total) : 0,
            TasaAsistencia = total > 0 ? (int)Math.Round(atendidas * 100.0 / total) : 0
        };
    }

    public async Task<(List<Cita> Items, int TotalRecords)> ObtenerAsync(
        int page,
        int pageSize,
        string? role,
        int? idPaciente = null,
        int? idProfesional = null,
        int? idUsuario = null,
        string? search = null,
        string? estado = null,
        DateTime? fecha = null,
        CancellationToken ct = default)
    {
        IQueryable<Cita> query = _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Include(c => c.EstadoCita);

        if (!string.IsNullOrWhiteSpace(role) && role.Equals("Paciente", StringComparison.OrdinalIgnoreCase))
        {
            if (idPaciente is > 0)
            {
                query = query.Where(c => c.IdPaciente == idPaciente.Value);
            }
            else
            {
                return (new List<Cita>(), 0);
            }
        }
        else if (!string.IsNullOrWhiteSpace(role) && role.Equals("Profesional", StringComparison.OrdinalIgnoreCase))
        {
            int resolvedIdProfesional = idProfesional ?? 0;

            if (resolvedIdProfesional <= 0 && idUsuario is > 0)
            {
                resolvedIdProfesional = await _context.Profesionales
                    .AsNoTracking()
                    .Where(p => p.IdUsuario == idUsuario.Value)
                    .Select(p => p.IdProfesional)
                    .FirstOrDefaultAsync(ct);
            }

            if (resolvedIdProfesional <= 0)
            {
                return (new List<Cita>(), 0);
            }

            query = query.Where(c => c.IdProfesional == resolvedIdProfesional);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchTerm = search.Trim();
            query = query.Where(c =>
                (c.Paciente != null &&
                 (c.Paciente.Nombres.Contains(searchTerm) || c.Paciente.Apellidos.Contains(searchTerm))) ||
                (c.Profesional != null &&
                 (c.Profesional.Nombres.Contains(searchTerm) || c.Profesional.Apellidos.Contains(searchTerm))) ||
                (c.Servicio != null && c.Servicio.Nombre.Contains(searchTerm)) ||
                (c.Notas != null && c.Notas.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            string estadoNormalizado = NormalizarEstado(estado);
            string[] estadosPermitidos = estadoNormalizado switch
            {
                "programada" => ["programada", "Programada", "agendada", "Agendada"],
                "confirmada" => ["confirmada", "Confirmada"],
                "atendida" => ["atendida", "Atendida", "completada", "Completada", "realizada", "Realizada"],
                "cancelada" => ["cancelada", "Cancelada", "cancelado", "Cancelado"],
                "no_asistida" => ["no_asistida", "No asistida", "no asistió", "No asistió", "no-show"],
                _ => [estado.Trim()]
            };
            query = query.Where(c =>
                estadosPermitidos.Contains(c.Estado) ||
                (c.EstadoCita != null && estadosPermitidos.Contains(c.EstadoCita.NombreEstado)));
        }

        if (fecha.HasValue)
        {
            DateTime inicio = fecha.Value.Date;
            query = query.Where(c => c.FechaHora >= inicio && c.FechaHora < inicio.AddDays(1));
        }

        if (!fecha.HasValue && string.IsNullOrWhiteSpace(estado))
        {
            DateTime ahora = ColombiaClock.Now;
            string[] estadosFinales = EstadoCitaHelper.EstadosFinales;

            query = query.Where(c =>
                c.FechaHora >= ahora
                || (c.Estado != null && estadosFinales.Contains(c.Estado.ToLower()))
                || (c.EstadoCita != null
                    && c.EstadoCita.NombreEstado != null
                    && estadosFinales.Contains(c.EstadoCita.NombreEstado.ToLower())));
        }

        int totalRecords = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.FechaHora)
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 500))
            .Take(Math.Clamp(pageSize, 1, 500))
            .ToListAsync(ct);

        return (items, totalRecords);
    }

    public async Task<Cita?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Citas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCita == id, ct);
    }

    public async Task<PagedResult<Cita>> ObtenerListadoGestionAsync(
        PaginationQuery pagination,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Cita> citasQuery = _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Usuario)
            .Include(c => c.Profesional)
            .ThenInclude(p => p!.Especialidades)
            .ThenInclude(pe => pe.Especialidad)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Include(c => c.EstadoCita);

        if (!string.IsNullOrWhiteSpace(pagination.Search))
        {
            string searchTerm = pagination.Search.Trim();
            string pattern = $"%{searchTerm}%";

            citasQuery = citasQuery.Where(c =>
                (c.Paciente != null &&
                 (EF.Functions.Like(c.Paciente.Nombres, pattern) ||
                  EF.Functions.Like(c.Paciente.Apellidos, pattern))) ||
                (c.Profesional != null &&
                 c.Profesional.Usuario != null &&
                 (EF.Functions.Like(c.Profesional.Usuario.Nombre, pattern) ||
                  EF.Functions.Like(c.Profesional.Usuario.Apellidos, pattern))) ||
                (c.Notas != null && EF.Functions.Like(c.Notas, pattern)) ||
                (c.Servicio != null && EF.Functions.Like(c.Servicio.Nombre, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(pagination.Estado))
        {
            string estado = NormalizarEstado(pagination.Estado);
            string[] estadosPermitidos = estado switch
            {
                "programada" => ["programada", "Programada", "agendada", "Agendada"],
                "confirmada" => ["confirmada", "Confirmada"],
                "atendida" => ["atendida", "Atendida", "completada", "Completada", "realizada", "Realizada"],
                "cancelada" => ["cancelada", "Cancelada", "cancelado", "Cancelado"],
                "no_asistida" => ["no_asistida", "No asistida", "no_asistio", "No asistió", "no asistio", "no-show", "No Show", "inasistencia", "Inasistencia"],
                _ => [pagination.Estado.Trim()]
            };

            citasQuery = citasQuery.Where(c =>
                (c.Estado != null && estadosPermitidos.Contains(c.Estado)) ||
                (c.EstadoCita != null &&
                 c.EstadoCita.NombreEstado != null &&
                 estadosPermitidos.Contains(c.EstadoCita.NombreEstado)));
        }

        if (int.TryParse(pagination.Profesional, out int idProfesional) &&
            idProfesional > 0)
        {
            citasQuery = citasQuery.Where(c => c.IdProfesional == idProfesional);
        }

        if (!string.IsNullOrWhiteSpace(pagination.Fecha) &&
            DateTime.TryParseExact(
                pagination.Fecha.Trim(),
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var fechaValida))
        {
            DateTime inicio = fechaValida.Date;
            DateTime fin = inicio.AddDays(1);
            citasQuery = citasQuery.Where(c => c.FechaHora >= inicio && c.FechaHora < fin);
        }

        if (string.IsNullOrWhiteSpace(pagination.Fecha) &&
            string.IsNullOrWhiteSpace(pagination.Estado))
        {
            DateTime ahoraFiltro = ColombiaClock.Now;
            string[] estadosFinales = EstadoCitaHelper.EstadosFinales;

            citasQuery = citasQuery.Where(c =>
                c.FechaHora >= ahoraFiltro ||
                (c.Estado != null && estadosFinales.Contains(c.Estado.ToLower())) ||
                (c.EstadoCita != null &&
                 c.EstadoCita.NombreEstado != null &&
                 estadosFinales.Contains(c.EstadoCita.NombreEstado.ToLower())));
        }

        return await citasQuery
            .OrderByDescending(c => c.FechaHora)
            .ToPagedResultAsync(page, pageSize, ct);
    }

    private static string NormalizarEstado(string? estado)
    {
        return EstadoCitaHelper.Normalize(estado);
    }
}
