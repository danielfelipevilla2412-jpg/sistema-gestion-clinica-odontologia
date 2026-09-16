using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.ViewModels.Components;

namespace SmileTrack_MVC.ViewComponents;

/// <summary>
/// ViewComponent para mostrar notificaciones contextuales inteligentes
/// Uso: @await Component.InvokeAsync("NotificacionesInteligentes", new { idProfesional = 1, tipo = "profesional" })
/// </summary>
public class NotificacionesInteligentesViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public NotificacionesInteligentesViewComponent(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Invoca el componente de notificaciones
    /// </summary>
    /// <param name="tipo">Tipo de notificaciones: "profesional", "recepcion", "paciente", "manual"</param>
    /// <param name="idProfesional">ID del profesional (si aplica)</param>
    /// <param name="idPaciente">ID del paciente (si aplica)</param>
    /// <param name="notificacionesPersonalizadas">Lista manual de notificaciones</param>
    public async Task<IViewComponentResult> InvokeAsync(
        string tipo = "profesional",
        int? idProfesional = null,
        int? idPaciente = null,
        List<NotificacionItem>? notificacionesPersonalizadas = null)
    {
        var notificaciones = new List<NotificacionItem>();

        // Si se proporcionan notificaciones manuales, usarlas
        if (notificacionesPersonalizadas != null && notificacionesPersonalizadas.Any())
        {
            notificaciones = notificacionesPersonalizadas;
        }
        else
        {
            // Generar notificaciones según el tipo
            notificaciones = tipo.ToLower() switch
            {
                "profesional" => await GenerarNotificacionesProfesional(idProfesional),
                "recepcion" => await GenerarNotificacionesRecepcion(),
                "paciente" => await GenerarNotificacionesPaciente(idPaciente),
                _ => new List<NotificacionItem>()
            };
        }

        var viewModel = new NotificacionesViewModel
        {
            Notificaciones = notificaciones
        };

        return View(viewModel);
    }

    /// <summary>
    /// Genera notificaciones para profesionales
    /// </summary>
    private async Task<List<NotificacionItem>> GenerarNotificacionesProfesional(int? idProfesional)
    {
        if (!idProfesional.HasValue) return new List<NotificacionItem>();

        var notificaciones = new List<NotificacionItem>();
        var ahora = DateTime.Now;

        // Citas sin confirmar (próximas 24h)
        var citasSinConfirmar = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional.Value)
            .Where(c => c.Estado == "programada" || c.Estado == "Programada" || c.Estado == "agendada" || c.Estado == "Agendada" || c.Estado == "solicitada" || c.Estado == "Solicitada")
            .Where(c => c.FechaHora >= ahora && c.FechaHora <= ahora.AddHours(24))
            .CountAsync();

        if (citasSinConfirmar > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Warning,
                Titulo = $"{citasSinConfirmar} cita{(citasSinConfirmar > 1 ? "s" : "")} sin confirmar",
                Mensaje = "Recuerda confirmar con los pacientes para reducir inasistencias",
                Accion = "Ver citas",
                UrlAccion = "/gestion-citas/st-odo-02-agenda",
                Id = "citasSinConfirmar"
            });
        }

        // Pacientes en sala de espera
        var pacientesEspera = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional.Value)
            .Where(c => c.Estado == "confirmada" || c.Estado == "Confirmada" || c.Estado == "en_espera" || c.Estado == "en espera" || c.Estado == "En espera")
            .Where(c => c.FechaHora.Date == DateTime.Today)
            .Where(c => c.FechaHora <= ahora)
            .CountAsync();

        if (pacientesEspera > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Urgent,
                Titulo = $"{pacientesEspera} paciente{(pacientesEspera > 1 ? "s" : "")} en espera",
                Mensaje = "Pacientes listos para atención",
                Accion = "Atender ahora",
                UrlAccion = "/gestion-citas/st-odo-02-agenda",
                Id = "pacientesEspera"
            });
        }

        // Historias clínicas pendientes (última semana)
        var historiasPendientes = await _context.Citas
            .Where(c => c.IdProfesional == idProfesional.Value)
            .Where(c => c.Estado == "atendida" || c.Estado == "Atendida" || c.Estado == "completada" || c.Estado == "Completada" || c.Estado == "realizada" || c.Estado == "Realizada")
            .Where(c => c.FechaHora >= DateTime.Today.AddDays(-7))
            .Where(c => !_context.HistoriasClinicas.Any(h => h.IdPaciente == c.IdPaciente && h.Activa))
            .CountAsync();

        if (historiasPendientes > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Info,
                Titulo = $"{historiasPendientes} historia{(historiasPendientes > 1 ? "s" : "")} clínica{(historiasPendientes > 1 ? "s" : "")} pendiente{(historiasPendientes > 1 ? "s" : "")}",
                Mensaje = "Completa los registros médicos de la semana",
                Accion = "Completar",
                UrlAccion = "/historia-clinica/pendientes",
                Id = "historiasPendientes"
            });
        }

        return notificaciones;
    }

    /// <summary>
    /// Genera notificaciones para recepción
    /// </summary>
    private async Task<List<NotificacionItem>> GenerarNotificacionesRecepcion()
    {
        var notificaciones = new List<NotificacionItem>();
        var ahora = DateTime.Now;

        // Pacientes en espera (todos los profesionales)
        var pacientesEspera = await _context.Citas
            .Where(c => c.Estado == "confirmada" || c.Estado == "Confirmada" || c.Estado == "en_espera" || c.Estado == "en espera" || c.Estado == "En espera")
            .Where(c => c.FechaHora.Date == DateTime.Today)
            .Where(c => c.FechaHora <= ahora)
            .CountAsync();

        if (pacientesEspera > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Urgent,
                Titulo = $"{pacientesEspera} paciente{(pacientesEspera > 1 ? "s" : "")} en sala de espera",
                Mensaje = "Verifica que todos los pacientes estén siendo atendidos",
                Accion = "Ver sala de espera",
                UrlAccion = "/recepcion/sala-espera",
                Id = "salaEspera"
            });
        }

        // Facturas pendientes de pago
        var facturasPendientes = await _context.Citas
            .Where(c => c.Estado == "atendida" || c.Estado == "Atendida" || c.Estado == "completada" || c.Estado == "Completada" || c.Estado == "realizada" || c.Estado == "Realizada")
            .Where(c => c.FechaHora.Date == DateTime.Today)
            .CountAsync(); // Simplificado, idealmente verificar tabla de facturas

        if (facturasPendientes > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Warning,
                Titulo = $"{facturasPendientes} factura{(facturasPendientes > 1 ? "s" : "")} pendiente{(facturasPendientes > 1 ? "s" : "")}",
                Mensaje = "Citas atendidas sin factura generada",
                Accion = "Facturar",
                UrlAccion = "/facturacion/pendientes",
                Id = "facturasPendientes"
            });
        }

        return notificaciones;
    }

    /// <summary>
    /// Genera notificaciones para pacientes
    /// </summary>
    private async Task<List<NotificacionItem>> GenerarNotificacionesPaciente(int? idPaciente)
    {
        if (!idPaciente.HasValue) return new List<NotificacionItem>();

        var notificaciones = new List<NotificacionItem>();
        var ahora = DateTime.Now;

        // Próximas citas (próximas 48h)
        var proximasCitas = await _context.Citas
            .Where(c => c.IdPaciente == idPaciente.Value)
            .Where(c => c.FechaHora >= ahora && c.FechaHora <= ahora.AddHours(48))
            .Where(c => c.Estado != "cancelada" && c.Estado != "Cancelada" && c.Estado != "atendida" && c.Estado != "Atendida" && c.Estado != "completada" && c.Estado != "Completada" && c.Estado != "no_asistida" && c.Estado != "No asistió")
            .CountAsync();

        if (proximasCitas > 0)
        {
            notificaciones.Add(new NotificacionItem
            {
                Tipo = TipoNotificacion.Info,
                Titulo = $"Tienes {proximasCitas} cita{(proximasCitas > 1 ? "s" : "")} próxima{(proximasCitas > 1 ? "s" : "")}",
                Mensaje = "Recuerda asistir puntualmente",
                Accion = "Ver citas",
                UrlAccion = "/gestion-citas/st-pac-01-mis-citas",
                Id = "proximasCitas"
            });
        }

        return notificaciones;
    }
}
