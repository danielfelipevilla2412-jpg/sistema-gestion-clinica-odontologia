using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class RecordatorioCitaService : IRecordatorioCitaService
{
    private const int MaxIntentos = 3;
    private const int TamanoLote = 100;
    private static readonly TimeSpan DuracionReserva = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan[] DemorasReintento = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private readonly AppDbContext _context;
    private readonly IEmailService? _emailService;
    private readonly ILogger<RecordatorioCitaService> _logger;

    public RecordatorioCitaService(AppDbContext context, IEmailService? emailService,
        ILogger<RecordatorioCitaService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> EnviarAsync(
        IReadOnlyCollection<int> idsCitas, string? mensajePersonalizado = null,
        CancellationToken ct = default)
    {
        int[] ids = idsCitas.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return (0, 0, []);
        await AsegurarProgramacionesAsync(ids, true, ct);
        return await ProcesarAsync(ids, mensajePersonalizado, ct);
    }

    public async Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> ProcesarPendientesAsync(
        CancellationToken ct = default)
    {
        DateTime ahora = ColombiaClock.Now;
        int[] ids = await _context.Citas.AsNoTracking()
            .Where(c => c.FechaHora > ahora && c.FechaHora <= ahora.AddHours(24) &&
                (c.EstadoCita != null
                    ? EstadoCitaHelper.EstadosRecordables.Contains(c.EstadoCita.NombreEstado.Trim().ToLower())
                    : c.Estado != null && EstadoCitaHelper.EstadosRecordables.Contains(c.Estado.Trim().ToLower())) &&
                !_context.RecordatoriosCita.Any(r => r.IdCita == c.IdCita && r.Canal == "email" &&
                    r.ProgramacionId == c.ProgramacionId))
            .OrderBy(c => c.FechaHora).Select(c => c.IdCita).Take(TamanoLote).ToArrayAsync(ct);

        if (ids.Length > 0)
            await AsegurarProgramacionesAsync(ids, false, ct);
        return await ProcesarAsync(null, null, ct);
    }

    private async Task AsegurarProgramacionesAsync(IReadOnlyCollection<int> ids, bool forzarAhora, CancellationToken ct)
    {
        DateTime ahora = ColombiaClock.Now;
        var citas = await _context.Citas.Include(c => c.EstadoCita)
            .Where(c => ids.Contains(c.IdCita) && c.FechaHora > ahora &&
                (c.EstadoCita != null
                    ? EstadoCitaHelper.EstadosRecordables.Contains(c.EstadoCita.NombreEstado.Trim().ToLower())
                    : c.Estado != null && EstadoCitaHelper.EstadosRecordables.Contains(c.Estado.Trim().ToLower())))
            .ToListAsync(ct);

        foreach (var cita in citas)
        {
            var recordatorio = await _context.RecordatoriosCita.FirstOrDefaultAsync(r =>
                r.IdCita == cita.IdCita && r.Canal == "email" && r.ProgramacionId == cita.ProgramacionId, ct);
            if (recordatorio is null)
            {
                // El esquema legado no guardaba la programación notificada. Si existe un
                // envío reciente compatible con la cita actual, se conserva como evidencia
                // para evitar duplicarlo durante la transición. El precheck documenta los
                // casos ambiguos; una reprogramación posterior crea una identidad nueva.
                bool envioLegadoReciente = await _context.RecordatoriosCita.AsNoTracking().AnyAsync(r =>
                    r.IdCita == cita.IdCita && r.Canal == "email" && r.ProgramacionId == null &&
                    r.Estado == "enviado" && r.EnviadoEn >= cita.FechaHora.AddHours(-24) &&
                    r.EnviadoEn < cita.FechaHora, ct);
                if (envioLegadoReciente)
                    continue;

                DateTime programado = cita.FechaHora.AddHours(-24);
                if (programado < ahora) programado = ahora;
                recordatorio = new RecordatorioCita
                {
                    IdCita = cita.IdCita,
                    ProgramacionId = cita.ProgramacionId,
                    Canal = "email",
                    Estado = "pendiente",
                    ProgramadoPara = programado,
                    ProximoIntentoEn = programado,
                    CreadoEn = ahora
                };
                _context.RecordatoriosCita.Add(recordatorio);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    _context.Entry(recordatorio).State = EntityState.Detached;
                    if (!await ExisteProgramacionAsync(cita.IdCita, cita.ProgramacionId, ct))
                        throw;
                    recordatorio = await _context.RecordatoriosCita.FirstAsync(r =>
                        r.IdCita == cita.IdCita && r.Canal == "email" && r.ProgramacionId == cita.ProgramacionId, ct);
                }
            }

            if (forzarAhora && recordatorio.Estado == "pendiente" && recordatorio.Intentos < MaxIntentos)
            {
                recordatorio.ProximoIntentoEn = ahora;
                await _context.SaveChangesAsync(ct);
            }
        }
    }

    private Task<bool> ExisteProgramacionAsync(int idCita, Guid programacionId, CancellationToken ct) =>
        _context.RecordatoriosCita.AsNoTracking().AnyAsync(r => r.IdCita == idCita && r.Canal == "email" &&
            r.ProgramacionId == programacionId, ct);

    private async Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> ProcesarAsync(
        int[]? idsCitas, string? mensajePersonalizado, CancellationToken ct)
    {
        DateTime ahora = ColombiaClock.Now;
        Guid reserva = await ReclamarAsync(idsCitas, ahora, ct);
        var recordatorios = await _context.RecordatoriosCita.AsNoTracking()
            .Include(r => r.Cita).ThenInclude(c => c!.Paciente)
            .Include(r => r.Cita).ThenInclude(c => c!.Profesional).ThenInclude(p => p!.Usuario)
            .Include(r => r.Cita).ThenInclude(c => c!.Servicio)
            .Include(r => r.Cita).ThenInclude(c => c!.EstadoCita)
            .Where(r => r.ReservaId == reserva).OrderBy(r => r.IdRecordatorio).ToListAsync(ct);

        int enviados = 0, fallidos = 0;
        var idsEnviados = new List<int>();
        foreach (var recordatorio in recordatorios)
        {
            var cita = recordatorio.Cita;
            if (cita is null || recordatorio.ProgramacionId is not Guid programacionId ||
                cita.ProgramacionId != programacionId ||
                cita.FechaHora <= ColombiaClock.Now || !EsRecordable(cita) ||
                !await CitaProgramacionVigenteAsync(cita.IdCita, programacionId, ct))
            {
                await CompletarAsync(recordatorio.IdRecordatorio, reserva, "cancelado", null, null, ct);
                continue;
            }

            try
            {
                if (cita.Paciente is null || !CorreoValido(cita.Paciente.Correo))
                    throw new InvalidOperationException("El paciente no tiene un correo válido registrado.");
                if (_emailService is null)
                    throw new InvalidOperationException("El servicio de correo electrónico no está configurado.");

                string profesional = cita.Profesional?.Usuario is { } usuario
                    ? $"{usuario.Nombre} {usuario.Apellidos}".Trim()
                    : $"{cita.Profesional?.Nombres} {cita.Profesional?.Apellidos}".Trim();
                await _emailService.SendCitaNotificacionAsync(cita.Paciente.Correo!,
                    $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim(), cita.FechaHora,
                    string.IsNullOrWhiteSpace(profesional) ? "Tu profesional" : profesional,
                    cita.Servicio?.Nombre ?? "Consulta Odontológica", "recordatorio", ct,
                    mensajePersonalizado: mensajePersonalizado);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await RegistrarFalloAsync(recordatorio.IdRecordatorio, reserva, ex.Message, ct);
                fallidos++;
                _logger.LogWarning(ex, "Falló el recordatorio {IdRecordatorio} para la cita {IdCita}.",
                    recordatorio.IdRecordatorio, recordatorio.IdCita);
                continue;
            }

            // Persistir fuera del catch SMTP: si guardar el éxito falla después de que SMTP
            // aceptó el mensaje, se conserva la reserva hasta su vencimiento y no se marca
            // falsamente como un fallo SMTP.
            if (await CompletarAsync(recordatorio.IdRecordatorio, reserva, "enviado", null,
                    ColombiaClock.Now, ct))
            {
                enviados++;
                idsEnviados.Add(cita.IdCita);
                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "EMAIL_RECORDATORIO", TablaAfectada = "Cita", IdRegistro = cita.IdCita,
                    Descripcion = "Recordatorio por correo enviado.", IpOrigen = "ServicioRecordatorios",
                    Fecha = ColombiaClock.Now
                });
                await _context.SaveChangesAsync(ct);
            }
        }
        return (enviados, fallidos, idsEnviados);
    }

    private async Task<Guid> ReclamarAsync(int[]? idsCitas, DateTime ahora, CancellationToken ct)
    {
        Guid reserva = Guid.NewGuid();
        DateTime vence = ahora.Add(DuracionReserva);
        if (_context.Database.IsSqlServer())
        {
            // READPAST requiere un nivel basado en bloqueos (también cuando la base usa
            // READ_COMMITTED_SNAPSHOT); RepeatableRead permite omitir filas reclamadas
            // sin depender del aislamiento configurado por la base.
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.Recordatorio_Cita
                   SET estado = 'fallido', proximo_intento_en = NULL,
                       reserva_id = NULL, reserva_hasta = NULL,
                       ultimo_error = 'Se agotaron los intentos tras vencer una reserva.'
                 WHERE estado = 'pendiente' AND intentos >= {MaxIntentos}
                   AND (reserva_id IS NULL OR reserva_hasta <= {ahora});
                """, ct);

            string filtro = idsCitas is null ? string.Empty :
                "AND r.id_cita IN (SELECT TRY_CONVERT(int, [value]) FROM OPENJSON(@idsJson))";
            string sql = $"""
                ;WITH candidatos AS
                (
                    SELECT TOP ({TamanoLote}) r.id_recordatorio
                      FROM dbo.Recordatorio_Cita AS r WITH (UPDLOCK, READPAST, ROWLOCK)
                      JOIN dbo.Cita AS c ON c.id_cita = r.id_cita
                                        AND c.programacion_id = r.programacion_id
                     WHERE r.estado = 'pendiente' AND r.intentos < {MaxIntentos}
                       AND r.proximo_intento_en <= @ahora
                       AND (r.reserva_id IS NULL OR r.reserva_hasta <= @ahora)
                       AND c.fecha_hora > @ahora
                       AND LOWER(LTRIM(RTRIM(c.estado))) IN ('programada','agendada','confirmada','confirmado')
                       {filtro}
                     ORDER BY r.proximo_intento_en, r.id_recordatorio
                )
                UPDATE r SET reserva_id = @reserva, reserva_hasta = @vence, intentos = intentos + 1
                  FROM dbo.Recordatorio_Cita AS r JOIN candidatos AS x
                    ON x.id_recordatorio = r.id_recordatorio;
                """;
            var parameters = new List<object>
            {
                new SqlParameter("@ahora", SqlDbType.DateTime2) { Value = ahora },
                new SqlParameter("@reserva", SqlDbType.UniqueIdentifier) { Value = reserva },
                new SqlParameter("@vence", SqlDbType.DateTime2) { Value = vence }
            };
            if (idsCitas is not null)
                parameters.Add(new SqlParameter("@idsJson", SqlDbType.NVarChar, -1)
                    { Value = JsonSerializer.Serialize(idsCitas) });
            await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);
            await tx.CommitAsync(ct);
            return reserva;
        }

        // La ruta InMemory permite probar política/estados; no pretende simular bloqueos SQL Server.
        var candidatosLocales = await _context.RecordatoriosCita
            .Where(r => r.Estado == "pendiente" && r.Intentos < MaxIntentos && r.ProximoIntentoEn <= ahora &&
                        (r.ReservaId == null || r.ReservaHasta <= ahora) &&
                        (idsCitas == null || idsCitas.Contains(r.IdCita)))
            .OrderBy(r => r.ProximoIntentoEn).Take(TamanoLote).ToListAsync(ct);
        foreach (var item in candidatosLocales)
        {
            item.ReservaId = reserva;
            item.ReservaHasta = vence;
            item.Intentos++;
        }
        await _context.SaveChangesAsync(ct);
        return reserva;
    }

    private async Task RegistrarFalloAsync(int id, Guid reserva, string error, CancellationToken ct)
    {
        DateTime ahora = ColombiaClock.Now;
        string detalle = error.Length > 500 ? error[..500] : error;
        if (_context.Database.IsSqlServer())
        {
            await _context.RecordatoriosCita
                .Where(r => r.IdRecordatorio == id && r.Estado == "pendiente" && r.ReservaId == reserva &&
                            r.ReservaHasta > ahora)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Estado, r => r.Intentos >= MaxIntentos ? "fallido" : "pendiente")
                    .SetProperty(r => r.UltimoError, detalle)
                    .SetProperty(r => r.ProximoIntentoEn, r => r.Intentos >= MaxIntentos
                        ? null : ahora.AddMinutes(r.Intentos == 1 ? 5 : 15))
                    .SetProperty(r => r.ReservaId, (Guid?)null)
                    .SetProperty(r => r.ReservaHasta, (DateTime?)null), ct);
            return;
        }

        var item = await ObtenerPropietarioAsync(id, reserva, ahora, ct);
        if (item is null) return;
        item.UltimoError = detalle;
        if (item.Intentos >= MaxIntentos)
        {
            item.Estado = "fallido";
            item.ProximoIntentoEn = null;
        }
        else item.ProximoIntentoEn = ahora.Add(DemorasReintento[item.Intentos - 1]);
        Liberar(item);
        await _context.SaveChangesAsync(ct);
    }

    private async Task<bool> CompletarAsync(int id, Guid reserva, string estado, string? error,
        DateTime? enviadoEn, CancellationToken ct)
    {
        DateTime ahora = ColombiaClock.Now;
        if (_context.Database.IsSqlServer())
        {
            int count = await _context.RecordatoriosCita
                .Where(r => r.IdRecordatorio == id && r.Estado == "pendiente" && r.ReservaId == reserva &&
                            r.ReservaHasta > ahora && (estado != "enviado" ||
                                _context.Citas.Any(c => c.IdCita == r.IdCita &&
                                    c.ProgramacionId == r.ProgramacionId && c.FechaHora > ahora &&
                                    c.Estado != null && EstadoCitaHelper.EstadosRecordables.Contains(c.Estado.Trim().ToLower()))))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Estado, estado)
                    .SetProperty(r => r.UltimoError, error)
                    .SetProperty(r => r.EnviadoEn, enviadoEn)
                    .SetProperty(r => r.ProximoIntentoEn, (DateTime?)null)
                    .SetProperty(r => r.ReservaId, (Guid?)null)
                    .SetProperty(r => r.ReservaHasta, (DateTime?)null), ct);
            return count == 1;
        }

        var item = await ObtenerPropietarioAsync(id, reserva, ahora, ct);
        if (item is null) return false;
        if (estado == "enviado" && (item.ProgramacionId is not Guid programacionId ||
            !await CitaProgramacionVigenteAsync(item.IdCita, programacionId, ct)))
            return false;
        item.Estado = estado;
        item.UltimoError = error;
        item.EnviadoEn = enviadoEn;
        item.ProximoIntentoEn = null;
        Liberar(item);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private Task<RecordatorioCita?> ObtenerPropietarioAsync(int id, Guid reserva, DateTime ahora, CancellationToken ct)
    {
        if (!_context.Database.IsSqlServer())
            _context.ChangeTracker.Clear();
        return _context.RecordatoriosCita.FirstOrDefaultAsync(r => r.IdRecordatorio == id && r.Estado == "pendiente" &&
            r.ReservaId == reserva && r.ReservaHasta > ahora, ct);
    }

    private Task<bool> CitaProgramacionVigenteAsync(int idCita, Guid programacionId, CancellationToken ct) =>
        _context.Citas.AsNoTracking().AnyAsync(c => c.IdCita == idCita &&
            c.ProgramacionId == programacionId && c.FechaHora > ColombiaClock.Now && c.Estado != null &&
            EstadoCitaHelper.EstadosRecordables.Contains(c.Estado.Trim().ToLower()), ct);

    private static void Liberar(RecordatorioCita recordatorio)
    {
        recordatorio.ReservaId = null;
        recordatorio.ReservaHasta = null;
    }

    private static bool EsRecordable(Cita cita) =>
        EstadoCitaHelper.EstadosRecordables.Contains(
            EstadoCitaHelper.Normalize(cita.EstadoCita?.NombreEstado ?? cita.Estado));

    private static bool CorreoValido(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return false;
        try { return new System.Net.Mail.MailAddress(correo).Address == correo.Trim(); }
        catch (FormatException) { return false; }
    }
}
