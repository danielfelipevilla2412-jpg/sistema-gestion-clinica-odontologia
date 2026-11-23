using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Email;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class RecordatorioCitaService : IRecordatorioCitaService
{
    private readonly AppDbContext _context;
    private readonly IEmailService? _emailService;
    private readonly ILogger<RecordatorioCitaService> _logger;

    public RecordatorioCitaService(
        AppDbContext context,
        IEmailService? emailService,
        ILogger<RecordatorioCitaService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<(int Enviados, int Fallidos, List<int> IdsEnviados)> EnviarAsync(
        IReadOnlyCollection<int> idsCitas,
        string? mensajePersonalizado = null,
        CancellationToken ct = default)
    {
        int[] idsUnicos = idsCitas.Where(id => id > 0).Distinct().ToArray();
        if (idsUnicos.Length == 0)
            return (0, 0, []);

        DateTime ahora = ColombiaClock.Now;

        var citas = await _context.Citas
            .Include(c => c.Paciente)
            .Include(c => c.Profesional)
                .ThenInclude(p => p!.Usuario)
            .Include(c => c.Servicio)
            .Include(c => c.EstadoCita)
            .Where(c => idsUnicos.Contains(c.IdCita)
                && c.FechaHora >= ahora
                && (c.EstadoCita != null
                    ? EstadoCitaHelper.EstadosRecordables.Contains(c.EstadoCita.NombreEstado.Trim().ToLower())
                    : c.Estado != null && EstadoCitaHelper.EstadosRecordables.Contains(c.Estado.Trim().ToLower())))
            .ToListAsync(ct);

        int enviados = 0;
        int fallidos = idsUnicos.Length - citas.Count;
        var idsEnviados = new List<int>();

        foreach (var cita in citas)
        {
            bool yaEnviado = await _context.RecordatoriosCita
                .AnyAsync(r => r.IdCita == cita.IdCita
                    && r.Estado == "enviado"
                    && r.ProgramadoPara.Date == ahora.Date, ct);
            if (yaEnviado)
                continue;

            var recordatorio = new RecordatorioCita
            {
                IdCita = cita.IdCita,
                Canal = "email",
                Estado = "pendiente",
                ProgramadoPara = ahora,
                CreadoEn = ahora
            };
            _context.RecordatoriosCita.Add(recordatorio);

            try
            {
                if (cita.Paciente is null || string.IsNullOrWhiteSpace(cita.Paciente.Correo))
                    throw new InvalidOperationException("El paciente no tiene un correo electrónico registrado.");
                if (_emailService is null)
                    throw new InvalidOperationException("El servicio de correo electrónico no está configurado.");

                recordatorio.Intentos++;
                string profesionalNombre = cita.Profesional?.Usuario is not null
                    ? $"{cita.Profesional.Usuario.Nombre} {cita.Profesional.Usuario.Apellidos}".Trim()
                    : $"{cita.Profesional?.Nombres} {cita.Profesional?.Apellidos}".Trim();

                await _emailService.SendCitaNotificacionAsync(
                    cita.Paciente.Correo,
                    $"{cita.Paciente.Nombres} {cita.Paciente.Apellidos}".Trim(),
                    cita.FechaHora,
                    string.IsNullOrWhiteSpace(profesionalNombre) ? "Tu profesional" : profesionalNombre,
                    cita.Servicio?.Nombre ?? "Consulta Odontológica",
                    "recordatorio",
                    ct,
                    mensajePersonalizado: mensajePersonalizado);

                recordatorio.Estado = "enviado";
                recordatorio.EnviadoEn = ColombiaClock.Now;
                recordatorio.UltimoError = null;
                _context.Auditorias.Add(new Auditoria
                {
                    Accion = "EMAIL_RECORDATORIO",
                    TablaAfectada = "Cita",
                    IdRegistro = cita.IdCita,
                    Descripcion = $"Recordatorio automático enviado al correo {cita.Paciente.Correo}.",
                    IpOrigen = "ServicioRecordatorios",
                    Fecha = ColombiaClock.Now
                });
                enviados++;
                idsEnviados.Add(cita.IdCita);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                recordatorio.Estado = "fallido";
                recordatorio.UltimoError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                fallidos++;
                _logger.LogWarning(ex, "No se pudo enviar el recordatorio de la cita {IdCita}.", cita.IdCita);
            }
        }

        await _context.SaveChangesAsync(ct);
        return (enviados, fallidos, idsEnviados);
    }
}
