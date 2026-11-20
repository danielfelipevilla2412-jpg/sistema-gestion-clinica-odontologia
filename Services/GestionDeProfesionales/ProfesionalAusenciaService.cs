using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;

namespace SmileTrack_MVC.Services.GestionDeProfesionales;

public sealed class ProfesionalAusenciaService : IProfesionalAusenciaService
{
    private readonly AppDbContext _context;

    public ProfesionalAusenciaService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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

    public async Task<ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>> ObtenerCitasAfectadasPorAusenciaAsync(
        int id,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken ct = default)
    {
        if (id <= 0 || fechaInicio == default || fechaFin == default || fechaFin < fechaInicio)
            return ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>.Fail(
                "Indica un rango de fechas de ausencia válido.", 400);

        bool existe = await _context.Profesionales.AsNoTracking()
            .AnyAsync(p => p.IdProfesional == id, ct);
        if (!existe)
            return ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>.Fail(
                "Profesional no encontrado.", 404);

        DateTime inicio = fechaInicio.ToDateTime(TimeOnly.MinValue);
        DateTime fin = fechaFin.ToDateTime(TimeOnly.MaxValue);
        var citasAfectadas = await _context.Citas
            .AsNoTracking()
            .Include(c => c.Paciente)
            .Include(c => c.Servicio)
            .Where(c => c.IdProfesional == id
                     && c.FechaHora <= fin
                     && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : 30) > inicio
                     && !EstadoCitaHelper.EstadosFinales.Contains(
                         ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()))
            .OrderBy(c => c.FechaHora)
            .Select(c => new CitaAfectadaAusenciaApiDto
            {
                IdCita = c.IdCita,
                FechaHora = c.FechaHora,
                Paciente = c.Paciente == null ? "Paciente" : (c.Paciente.Nombres + " " + c.Paciente.Apellidos).Trim(),
                Servicio = c.Servicio == null ? "Consulta" : c.Servicio.Nombre,
                Estado = c.Estado ?? string.Empty
            })
            .ToListAsync(ct);

        return ProfesionalApiCollectionResult<CitaAfectadaAusenciaApiDto>.Ok(citasAfectadas);
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

        var citasAfectadas = await ObtenerCitasAfectadasPorAusenciaAsync(id, request.FechaInicio, request.FechaFin, ct);
        if (!citasAfectadas.Success)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail(
                citasAfectadas.Message, citasAfectadas.ErrorStatusCode ?? 500);

        _context.AusenciasProfesional.Add(ausencia);
        await _context.SaveChangesAsync(ct);

        string mensaje = citasAfectadas.Data.Count == 0
            ? "Ausencia registrada correctamente."
            : $"Ausencia registrada. ADVERTENCIA: existen {citasAfectadas.Data.Count} cita(s) programada(s) que requieren gestión.";

        var ausenciaDto = MapAusencia(ausencia);
        ausenciaDto.CitasAfectadas = citasAfectadas.Data;
        return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Ok(
            mensaje, [ausenciaDto]);
    }

    public async Task<ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>> ActualizarAusenciaAsync(
        int id,
        int idAusencia,
        AusenciaProfesionalApiRequest request,
        int? operadorId,
        CancellationToken ct = default)
    {
        var ausencia = await _context.AusenciasProfesional
            .FirstOrDefaultAsync(a => a.IdAusencia == idAusencia && a.IdProfesional == id, ct);
        if (ausencia is null)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail("Ausencia no encontrada.", 404);

        var validation = await ValidarAusenciaAsync(id, request, ct, idAusencia);
        if (!validation.Success)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail(validation.Message, validation.StatusCode);

        var citasAfectadas = await ObtenerCitasAfectadasPorAusenciaAsync(id, request.FechaInicio, request.FechaFin, ct);
        if (!citasAfectadas.Success)
            return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Fail(
                citasAfectadas.Message, citasAfectadas.ErrorStatusCode ?? 500);

        ausencia.Tipo = request.Tipo.Trim().ToLowerInvariant();
        ausencia.FechaInicio = request.FechaInicio;
        ausencia.FechaFin = request.FechaFin;
        ausencia.Duracion = request.FechaFin.DayNumber - request.FechaInicio.DayNumber + 1;
        ausencia.Observaciones = request.Observaciones?.Trim();
        ausencia.AprobadoPor = operadorId;
        await _context.SaveChangesAsync(ct);

        string mensaje = citasAfectadas.Data.Count == 0
            ? "Ausencia actualizada correctamente."
            : $"Ausencia actualizada. ADVERTENCIA: existen {citasAfectadas.Data.Count} cita(s) programada(s) que requieren gestión.";

        var ausenciaDto = MapAusencia(ausencia);
        ausenciaDto.CitasAfectadas = citasAfectadas.Data;
        return ProfesionalApiCollectionOperationResult<AusenciaProfesionalApiDto>.Ok(
            mensaje, [ausenciaDto]);
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
        CancellationToken ct,
        int? idAusenciaExcluir = null)
    {
        if (request is null || id <= 0)
            return (false, "Datos de ausencia inválidos.", 400);
        if (!await _context.Profesionales.AnyAsync(p => p.IdProfesional == id, ct))
            return (false, "Profesional no encontrado.", 404);
        if (request.FechaInicio == default || request.FechaFin == default || request.FechaFin < request.FechaInicio)
            return (false, "La fecha de fin debe ser igual o posterior a la fecha de inicio.", 400);

        string[] tiposPermitidos = ["vacaciones", "incapacidad", "permiso", "otro"];
        if (string.IsNullOrWhiteSpace(request.Tipo) ||
            !tiposPermitidos.Contains(request.Tipo.Trim(), StringComparer.OrdinalIgnoreCase))
            return (false, "El tipo de ausencia no es válido.", 400);

        bool rangoSolapado = await _context.AusenciasProfesional.AnyAsync(a =>
            a.IdProfesional == id &&
            a.IdAusencia != (idAusenciaExcluir ?? 0) &&
            a.FechaInicio <= request.FechaFin &&
            a.FechaFin >= request.FechaInicio,
            ct);
        if (rangoSolapado)
            return (false, "El rango de ausencia se solapa con otra ausencia registrada para este profesional.", 409);

        return (true, string.Empty, 200);
    }

    private static string NombreDia(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miercoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sabado",
        _ => "Domingo"
    };

    private static string NormalizarDiaSemana(string dia) => string.Concat(
        dia.Trim().Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                                System.Globalization.UnicodeCategory.NonSpacingMark))
        .ToLowerInvariant();

    private static bool HorarioCubreIntervalo(
        IReadOnlyCollection<HorarioProfesional> horarios,
        DateTime inicio,
        DateTime fin)
    {
        if (fin <= inicio)
            return false;

        DateOnly primerDia = DateOnly.FromDateTime(inicio);
        DateOnly ultimoDia = DateOnly.FromDateTime(fin.AddTicks(-1));
        for (DateOnly dia = primerDia; dia <= ultimoDia; dia = dia.AddDays(1))
        {
            DateTime inicioDia = dia.ToDateTime(TimeOnly.MinValue);
            DateTime finDia = inicioDia.AddDays(1);
            TimeOnly inicioTramo = inicio > inicioDia
                ? TimeOnly.FromDateTime(inicio)
                : TimeOnly.MinValue;
            TimeOnly finTramo = fin < finDia
                ? TimeOnly.FromDateTime(fin)
                : new TimeOnly(23, 59);
            string diaNombre = NormalizarDiaSemana(NombreDia(dia.DayOfWeek));

            bool cubierto = horarios.Any(h =>
                NormalizarDiaSemana(h.DiaSemana) == diaNombre &&
                h.HoraInicio <= inicioTramo &&
                h.HoraFin >= finTramo);
            if (!cubierto)
                return false;
        }

        return true;
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

    public async Task<SmileTrack_MVC.Models.DTOs.ResultadoReasignacionAusenciaDto> RegistrarAusenciaConReasignacionAsync(
        int idProfesional,
        DateTime fechaInicio,
        DateTime fechaFin,
        string motivo,
        int? operadorId = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        var profesional = await _context.Profesionales.FirstOrDefaultAsync(p => p.IdProfesional == idProfesional, ct);
        if (profesional is null)
            throw new InvalidOperationException("El profesional especificado no existe.");

        var dateInicio = DateOnly.FromDateTime(fechaInicio);
        var dateFin = DateOnly.FromDateTime(fechaFin);
        string tipoAusencia = string.IsNullOrWhiteSpace(motivo) ? "incapacidad" : motivo.Trim();
        if (NormalizarDiaSemana(tipoAusencia) == "incapacidad medica")
            tipoAusencia = "incapacidad";
        var validacionAusencia = await ValidarAusenciaAsync(idProfesional,
            new AusenciaProfesionalApiRequest
            {
                Tipo = tipoAusencia,
                FechaInicio = dateInicio,
                FechaFin = dateFin,
                Observaciones = observaciones
            }, ct);
        if (!validacionAusencia.Success)
            throw new InvalidOperationException(validacionAusencia.Message);

        var inicioAusencia = dateInicio.ToDateTime(TimeOnly.MinValue);
        var finAusencia = dateFin.ToDateTime(TimeOnly.MaxValue);

        // 1. Registrar la ausencia del profesional
        var ausencia = new AusenciaProfesional
        {
            IdProfesional = idProfesional,
            FechaInicio = dateInicio,
            FechaFin = dateFin,
            Tipo = tipoAusencia,
            Observaciones = string.IsNullOrWhiteSpace(observaciones)
                ? "Registrado con reasignación automática de citas."
                : observaciones.Trim(),
            AprobadoPor = operadorId
        };
        _context.AusenciasProfesional.Add(ausencia);

        // 2. Buscar citas afectadas no canceladas
        var citasAfectadas = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional
                     && c.FechaHora <= finAusencia
                     && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : 30) > inicioAusencia
                     && !EstadoCitaHelper.EstadosFinales.Contains(
                         ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower()))
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
        var idsProfesionalesCandidatos = profesionalesActivos.Select(p => p.IdProfesional).ToList();
        var especialidadesCandidatas = await _context.ProfesionalEspecialidades
            .AsNoTracking()
            .Where(pe => idsProfesionalesCandidatos.Contains(pe.IdProfesional))
            .ToListAsync(ct);
        var especialidadesPorProfesional = especialidadesCandidatas
            .GroupBy(item => item.IdProfesional)
            .ToDictionary(group => group.Key, group => group.Select(item => item.IdEspecialidad).ToList());
        var especialidadPorServicio = await _context.Servicios
            .AsNoTracking()
            .Where(s => s.Estado == "activo")
            .ToDictionaryAsync(s => s.IdServicio, s => s.IdEspecialidad, ct);

        int reasignadas = 0;
        int canceladas = 0;
        var huecosReservados = new HashSet<(int IdProfesional, DateTime Inicio, DateTime Fin)>();

        foreach (var cita in citasAfectadas)
        {
            int duracion = cita.DuracionMinutos > 0 ? cita.DuracionMinutos : 30;
            DateTime finCita = cita.FechaHora.AddMinutes(duracion);
            DateOnly dateCita = DateOnly.FromDateTime(cita.FechaHora);
            DateOnly ultimoDiaCita = DateOnly.FromDateTime(finCita.AddTicks(-1));

            Profesional? candidatoElegido = null;

            foreach (var candidato in profesionalesActivos)
            {
                // Una reasignación debe respetar las mismas reglas que una asignación nueva.
                // Si la cita no tiene servicio o el servicio no tiene especialidad configurada,
                // no se inventa un profesional sustituto.
                if (cita.IdServicio is not int idServicio ||
                    !especialidadPorServicio.TryGetValue(idServicio, out int? especialidadServicio) ||
                    !especialidadServicio.HasValue)
                    continue;

                bool asignacionActiva = await _context.ProfesionalServicios
                    .AsNoTracking()
                    .AnyAsync(ps => ps.IdProfesional == candidato.IdProfesional
                                 && ps.IdServicio == idServicio
                                 && ps.Activo, ct);
                if (!asignacionActiva) continue;

                var idsEspecialidadesCandidato = especialidadesPorProfesional.TryGetValue(
                    candidato.IdProfesional, out var idsCandidato)
                    ? idsCandidato
                    : new List<int>();
                if (!idsEspecialidadesCandidato.Contains(especialidadServicio.Value))
                    continue;

                // Verificar ausencias del candidato
                bool candidatoConAusencia = await _context.AusenciasProfesional
                    .AnyAsync(a => a.IdProfesional == candidato.IdProfesional
                                && a.FechaInicio <= ultimoDiaCita
                                && a.FechaFin >= dateCita, ct);

                if (candidatoConAusencia) continue;

                bool candidatoConBloqueo = await _context.BloqueosProfesional
                    .AnyAsync(b => b.IdProfesional == candidato.IdProfesional
                                && b.FechaInicio < finCita
                                && b.FechaFin > cita.FechaHora, ct);
                if (candidatoConBloqueo) continue;

                var horariosCandidato = await _context.HorariosProfesional
                    .AsNoTracking()
                    .Where(h => h.IdProfesional == candidato.IdProfesional && h.Activo)
                    .ToListAsync(ct);
                if (horariosCandidato.Count == 0)
                    continue;

                if (!HorarioCubreIntervalo(horariosCandidato, cita.FechaHora, finCita))
                    continue;

                // La sustitución conserva paciente y consultorio, así que los tres recursos
                // deben estar libres para el intervalo completo.
                bool candidatoConConflicto = await _context.Citas
                    .AnyAsync(c => c.IdCita != cita.IdCita
                                && (c.IdProfesional == candidato.IdProfesional
                                    || c.IdPaciente == cita.IdPaciente
                                    || (cita.IdConsultorio.HasValue && c.IdConsultorio == cita.IdConsultorio))
                                && !EstadoCitaHelper.EstadosQueLiberanRecursos.Contains(
                                    ((c.EstadoCita != null ? c.EstadoCita.NombreEstado : c.Estado) ?? string.Empty).Trim().ToLower())
                                && c.FechaHora < finCita
                                && c.FechaHora.AddMinutes(c.DuracionMinutos > 0 ? c.DuracionMinutos : 30) > cita.FechaHora, ct);

                if (candidatoConConflicto) continue;
                if (huecosReservados.Contains((candidato.IdProfesional, cita.FechaHora, finCita)))
                    continue;

                candidatoElegido = candidato;
                break;
            }

            if (candidatoElegido is not null)
            {
                huecosReservados.Add((candidatoElegido.IdProfesional, cita.FechaHora, finCita));
                int idAnterior = cita.IdProfesional ?? 0;
                cita.IdProfesional = candidatoElegido.IdProfesional;

                // Historial de reasignación
                _context.CitasHistorialEstado.Add(new CitaHistorialEstado
                {
                    IdCita = cita.IdCita,
                    IdEstado = cita.IdEstado,
                    EstadoTexto = cita.Estado,
                    Motivo = $"Reasignada automáticamente por ausencia del profesional #{idAnterior} a #{candidatoElegido.IdProfesional}",
                    FechaCambio = ColombiaClock.Now
                });

                // Notificación al paciente
                _context.Notificaciones.Add(new Notificacion
                {
                    IdPaciente = cita.IdPaciente,
                    IdCita = cita.IdCita,
                    Tipo = "cambio_profesional",
                    Titulo = "Reasignación de profesional",
                    Contenido = $"Tu cita del {cita.FechaHora:dd/MM/yyyy HH:mm} ha sido asignada a {candidatoElegido.Nombres} {candidatoElegido.Apellidos} por ausencia médica.",
                    CreadaEn = ColombiaClock.Now
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
                cita.MotivoCancelacion = "Cancelación automática por ausencia médica del profesional sin disponibilidad de reemplazo.";

                // Historial de cancelación
                _context.CitasHistorialEstado.Add(new CitaHistorialEstado
                {
                    IdCita = cita.IdCita,
                    IdEstado = cita.IdEstado,
                    EstadoTexto = cita.Estado,
                    Motivo = "Cancelación automática por ausencia médica del profesional sin disponibilidad de reemplazo.",
                    FechaCambio = ColombiaClock.Now
                });

                // Notificación al paciente
                _context.Notificaciones.Add(new Notificacion
                {
                    IdPaciente = cita.IdPaciente,
                    IdCita = cita.IdCita,
                    Tipo = "cancelacion_emergencia",
                    Titulo = "Cita cancelada por fuerza mayor",
                    Contenido = $"Tu cita del {cita.FechaHora:dd/MM/yyyy HH:mm} fue cancelada por incapacidad del profesional. Por favor contacta a recepción para reprogramar.",
                    CreadaEn = ColombiaClock.Now
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

}
