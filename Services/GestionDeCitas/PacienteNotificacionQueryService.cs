using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.ViewModels.GestionDeCitas;

namespace SmileTrack_MVC.Services.GestionDeCitas;

public sealed class PacienteNotificacionQueryService : IPacienteNotificacionQueryService
{
    private readonly AppDbContext _context;

    public PacienteNotificacionQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PacienteNotificacionesViewModel> ObtenerNotificacionesAsync(
        int? idUsuario,
        CancellationToken ct = default)
    {
        var paciente = idUsuario is > 0
            ? await _context.Pacientes.AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdUsuario == idUsuario, ct)
            : null;

        if (paciente is null)
            return new PacienteNotificacionesViewModel();

        var notificacionesDb = await _context.Notificaciones.AsNoTracking()
            .Where(n => n.IdPaciente == paciente.IdPaciente && n.Estado != "eliminada")
            .OrderByDescending(n => n.CreadaEn)
            .Take(30)
            .ToListAsync(ct);

        var citas = await _context.Citas.AsNoTracking()
            .Include(c => c.Profesional)
            .Include(c => c.Servicio)
            .Include(c => c.Consultorio)
            .Where(c => c.IdPaciente == paciente.IdPaciente)
            .OrderByDescending(c => c.FechaHora)
            .Take(20)
            .ToListAsync(ct);

        var citasIds = citas.Select(c => c.IdCita).ToList();
        var citasLeidas = citasIds.Count == 0
            ? new HashSet<int>()
            : (await _context.NotificacionesLeidas.AsNoTracking()
                .Where(n => n.IdPaciente == paciente.IdPaciente && citasIds.Contains(n.IdCita))
                .Select(n => n.IdCita)
                .ToListAsync(ct))
                .ToHashSet();

        DateTime ahora = ColombiaClock.Now;
        var notificaciones = new List<PacienteNotificacionViewModel>();
        var citasConNotificacion = new HashSet<int>();

        foreach (var notificacion in notificacionesDb)
        {
            if (notificacion.IdCita.HasValue)
                citasConNotificacion.Add(notificacion.IdCita.Value);

            string tipo = notificacion.Tipo?.ToLowerInvariant() switch
            {
                "lista_espera_liberada" => "waitlist",
                "cita_rechazada" => "rejected",
                "cancelada" => "cancelled",
                "reprogramada" => "rescheduled",
                "factura" => "invoice",
                "recordatorio" => "reminder",
                "confirmada" => "confirmed",
                _ => "message"
            };

            bool leida = notificacion.FechaLectura.HasValue || notificacion.Estado == "leida";
            DateTime fecha = notificacion.CreadaEn != default
                ? notificacion.CreadaEn
                : notificacion.FechaProgramada ?? ahora;

            notificaciones.Add(new PacienteNotificacionViewModel
            {
                Id = $"notificacion-{notificacion.IdNotificacion}",
                IdNotificacion = notificacion.IdNotificacion,
                IdCita = notificacion.IdCita,
                Tipo = tipo,
                Titulo = notificacion.Titulo ?? "Notificación de la clínica",
                Desc = notificacion.Contenido ?? string.Empty,
                Time = fecha,
                Leida = leida,
                Badge = leida ? "read" : "new",
                Canal = notificacion.Canal ?? "interno",
                UrlAccion = notificacion.IdCita.HasValue
                    ? $"/gestion-de-citas/st-pac-01-mis-citas?editId={notificacion.IdCita}"
                    : string.Empty
            });
        }

        foreach (var cita in citas)
        {
            if (citasConNotificacion.Contains(cita.IdCita))
                continue;

            string profesional = cita.Profesional is not null
                ? $"Dr(a). {cita.Profesional.Nombres} {cita.Profesional.Apellidos}"
                : "tu profesional asignado";
            string estado = EstadoCitaHelper.Normalize(cita.Estado);
            bool leida = citasLeidas.Contains(cita.IdCita);
            string urlAccion = $"/gestion-de-citas/st-pac-01-mis-citas?editId={cita.IdCita}";

            if (cita.FechaHora > ahora && estado is "programada" or "confirmada")
            {
                notificaciones.Add(new PacienteNotificacionViewModel
                {
                    Id = $"cita-{cita.IdCita}",
                    IdCita = cita.IdCita,
                    Tipo = "reminder",
                    Titulo = "Recordatorio de cita",
                    Desc = $"Tu cita con {profesional} es el {cita.FechaHora:dd 'de' MMMM} a las {cita.FechaHora:hh:mm tt}{(cita.Consultorio is not null ? " - " + cita.Consultorio.Nombre : "")}",
                    Time = cita.FechaHora,
                    Leida = leida,
                    Badge = leida ? "read" : "pending",
                    Canal = "interno",
                    UrlAccion = urlAccion
                });
            }
            else if (estado == "confirmada")
            {
                notificaciones.Add(new PacienteNotificacionViewModel
                {
                    Id = $"cita-{cita.IdCita}",
                    IdCita = cita.IdCita,
                    Tipo = "confirmed",
                    Titulo = "Cita confirmada",
                    Desc = $"Tu cita del {cita.FechaHora:dd 'de' MMMM} fue confirmada exitosamente.",
                    Time = cita.FechaHora,
                    Leida = leida,
                    Badge = leida ? "read" : "new",
                    Canal = "interno",
                    UrlAccion = urlAccion
                });
            }
            else if (estado == "cancelada")
            {
                notificaciones.Add(new PacienteNotificacionViewModel
                {
                    Id = $"cita-{cita.IdCita}",
                    IdCita = cita.IdCita,
                    Tipo = "cancelled",
                    Titulo = "Cita cancelada",
                    Desc = $"Tu cita del {cita.FechaHora:dd 'de' MMMM} fue cancelada.",
                    Time = cita.FechaHora,
                    Leida = true,
                    Badge = "read",
                    Canal = "interno",
                    UrlAccion = string.Empty
                });
            }
        }

        var ordenadas = notificaciones.OrderByDescending(n => n.Time).ToList();
        return new PacienteNotificacionesViewModel
        {
            Notificaciones = ordenadas,
            UnreadCount = ordenadas.Count(n => !n.Leida)
        };
    }

    public async Task<int> ObtenerConteoNoLeidasAsync(
        int idUsuario,
        CancellationToken ct = default)
    {
        int? idPaciente = await _context.Pacientes.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .Select(p => (int?)p.IdPaciente)
            .FirstOrDefaultAsync(ct);
        if (!idPaciente.HasValue)
            return 0;

        int unreadDb = await _context.Notificaciones.AsNoTracking()
            .CountAsync(n => n.IdPaciente == idPaciente.Value &&
                             n.FechaLectura == null &&
                             n.Estado != "eliminada", ct);

        int totalCitasNoLeidas = await _context.Citas.AsNoTracking()
            .Where(c => c.IdPaciente == idPaciente.Value &&
                        c.Estado != null &&
                        (c.Estado.ToLower() == "programada" ||
                         c.Estado.ToLower() == "agendada" ||
                         c.Estado.ToLower() == "confirmada"))
            .CountAsync(c => !_context.NotificacionesLeidas.Any(n =>
                n.IdPaciente == idPaciente.Value && n.IdCita == c.IdCita), ct);

        return Math.Max(unreadDb, totalCitasNoLeidas);
    }
}
