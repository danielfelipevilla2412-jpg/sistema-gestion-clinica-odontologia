using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;

namespace SmileTrack_MVC.Services.GestionDeProfesionales;

public sealed class ProfesionalHorarioService : IProfesionalHorarioService
{
    private readonly AppDbContext _context;

    public ProfesionalHorarioService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ProfesionalApiCollectionResult<HorarioProfesionalApiDto>> ObtenerAsync(
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

    public async Task<ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>> ActualizarAsync(
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

        static string NormalizarDiaSemana(string dia) => dia.Trim().ToLowerInvariant() switch
        {
            "lunes" => "Lunes",
            "martes" => "Martes",
            "miércoles" or "miercoles" => "Miercoles",
            "jueves" => "Jueves",
            "viernes" => "Viernes",
            "sábado" or "sabado" => "Sabado",
            "domingo" => "Domingo",
            _ => dia.Trim()
        };

        static string? ObtenerDiaSemana(HorarioSemanalApiRequest bloque) =>
            !string.IsNullOrWhiteSpace(bloque.DiaSemana) ? bloque.DiaSemana :
            !string.IsNullOrWhiteSpace(bloque.DayFull) ? bloque.DayFull :
            bloque.Day;

        var bloquesActivos = horarios.Where(b => b.Active).ToList();
        bool hayBloqueInvalido = bloquesActivos.Any(b =>
        {
            var diaSemana = ObtenerDiaSemana(b);
            return string.IsNullOrWhiteSpace(diaSemana) ||
                   !diasValidos.Contains(diaSemana) ||
                   !TimeOnly.TryParse(b.Start, out var inicio) ||
                   !TimeOnly.TryParse(b.End, out var fin) ||
                   fin <= inicio;
        });
        if (hayBloqueInvalido)
            return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                "Cada día activo debe tener un día válido y una hora de inicio y fin válidas.", 422);

        var porDia = bloquesActivos
            .Select(b => new
            {
                Dia = NormalizarDiaSemana(ObtenerDiaSemana(b)!),
                Inicio = TimeOnly.TryParse(b.Start, out var si) ? si : (TimeOnly?)null,
                Fin = TimeOnly.TryParse(b.End, out var sf) ? sf : (TimeOnly?)null
            })
            .Where(x => x.Inicio.HasValue && x.Fin.HasValue)
            .GroupBy(x => x.Dia);

        foreach (var grupo in porDia)
        {
            var bloques = grupo.OrderBy(x => x.Inicio).ToList();
            for (int i = 0; i < bloques.Count - 1; i++)
            {
                if (bloques[i + 1].Inicio!.Value < bloques[i].Fin!.Value)
                    return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                        $"Los bloques del día '{grupo.Key}' se solapan. Corrija los horarios antes de guardar (RN-16).", 422);
            }
        }

        var nuevosHorarios = bloquesActivos
            .Select(b => new
            {
                Bloque = b,
                Inicio = TimeOnly.TryParse(b.Start, out var inicio) ? inicio : (TimeOnly?)null,
                Fin = TimeOnly.TryParse(b.End, out var fin) ? fin : (TimeOnly?)null
            })
            .Where(x => x.Inicio.HasValue && x.Fin.HasValue && x.Fin > x.Inicio)
            .Select(x => new HorarioProfesional
            {
                IdProfesional = id,
                DiaSemana = NormalizarDiaSemana(ObtenerDiaSemana(x.Bloque)!),
                HoraInicio = x.Inicio!.Value,
                HoraFin = x.Fin!.Value,
                Activo = true
            })
            .ToList();

        var citasFuturas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.EstadoCita)
            .Include(c => c.Servicio)
            .Where(c => c.IdProfesional == id && c.FechaHora >= ColombiaClock.Today)
            .ToListAsync(ct);

        foreach (var cita in citasFuturas)
        {
            string estado = EstadoCitaHelper.Normalize(cita.EstadoCita?.NombreEstado ?? cita.Estado);
            if (EstadoCitaHelper.EstadosFinales.Contains(estado))
                continue;

            int duracionCita = cita.DuracionMinutos > 0
                ? cita.DuracionMinutos
                : cita.Servicio?.DuracionMinutos > 0
                    ? cita.Servicio.DuracionMinutos
                    : 60;
            DateTime finCita = cita.FechaHora.AddMinutes(duracionCita);
            if (finCita <= ColombiaClock.Now)
                continue;

            string diaCita = NormalizarDiaSemana(cita.FechaHora.DayOfWeek switch
            {
                DayOfWeek.Monday => "Lunes",
                DayOfWeek.Tuesday => "Martes",
                DayOfWeek.Wednesday => "Miercoles",
                DayOfWeek.Thursday => "Jueves",
                DayOfWeek.Friday => "Viernes",
                DayOfWeek.Saturday => "Sabado",
                _ => "Domingo"
            });
            TimeOnly inicioCita = TimeOnly.FromDateTime(cita.FechaHora);
            TimeOnly finCitaDia = TimeOnly.FromDateTime(finCita);
            bool quedaCubierta = nuevosHorarios.Any(h =>
                h.DiaSemana == diaCita &&
                h.HoraInicio <= inicioCita &&
                h.HoraFin >= finCitaDia);

            if (!quedaCubierta)
                return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Fail(
                    $"El nuevo horario dejaría la cita futura #{cita.IdCita} fuera de disponibilidad. Reagende o cancele esa cita antes de cambiar el horario.", 409);
        }

        var existentes = await _context.HorariosProfesional
            .Where(h => h.IdProfesional == id)
            .ToListAsync(ct);
        _context.HorariosProfesional.RemoveRange(existentes);
        if (nuevosHorarios.Count > 0)
            await _context.HorariosProfesional.AddRangeAsync(nuevosHorarios, ct);
        await _context.SaveChangesAsync(ct);

        var actualizado = await ObtenerAsync(id, usuarioActualId, esAdministrador, ct);
        return ProfesionalApiCollectionOperationResult<HorarioProfesionalApiDto>.Ok(
            "Horario actualizado correctamente.", actualizado.Data);
    }
}
