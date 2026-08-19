using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Models.ViewModels;

namespace SmileTrack_MVC.Controllers;


/// <summary>
/// Controlador del módulo Centro de Ayuda.
///
/// RUTAS ACTIVAS (referenciadas desde los sidebars de Admin, Profesional, Auxiliar y Recepcionista):
///   GET /centro-de-ayuda/guias-tutoriales   → vista Guías, Tutoriales y Soporte (Admin + todos los roles autenticados)
///   GET /centro-de-ayuda/como-programar-cita → vista Cómo programar una cita (Admin)
///   GET /centro-de-ayuda/soporte             → vista de Soporte / Ticket (Admin)
///
/// RUTAS LEGACY (redirigen a la principal para no romper enlaces existentes):
///   GET /centro-de-ayuda/st-rec-01-preguntas-frecuentes
///   GET /centro-de-ayuda/st-rec-02-reportar-incidencia
///   GET /centro-de-ayuda/st-adm-01-manuales-guias
///   GET /centro-de-ayuda/st-adm-02-gestion-tickets
///   GET /centro-de-ayuda/st-adm-03-video-tutoriales
/// </summary>
[Authorize]
public class CentroDeAyudaController : Controller
{
    // ─── Acción principal: Guías y Tutoriales ─────────────────────────────────
    /// <summary>
    /// Vista principal del Centro de Ayuda.
    /// Accesible para todos los roles autenticados (Admin, Profesional, Aux, Recepcionista, Paciente).
    /// </summary>
    [HttpGet]
    [Route("centro-de-ayuda/guias-tutoriales")]
    public IActionResult GuiasTutoriales()
    {
        var vm = BuildGuiasTutorialesViewModel();
        return View("~/Views/Centro_De_Ayuda/Guias_Tutoriales_y_Soporte/index.cshtml", vm);
    }

    // ─── Cómo programar una cita ──────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/como-programar-cita")]
    public IActionResult ComoProgramarCita()
    {
        return View("~/Views/Centro_De_Ayuda/Como_Programar_Una_Cita/ComoProgramarUnaCita.cshtml");
    }

    // ─── Soporte / Ticket ─────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/soporte")]
    public IActionResult Soporte()
    {
        ViewData["Title"] = "SmileTrack — Soporte";
        var userName = User.Identity?.Name ?? "Usuario SmileTrack";
        string[] partesNombre = userName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string initials = string.Join("", partesNombre.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        if (string.IsNullOrWhiteSpace(initials)) initials = "ST";

        var model = new SupportTicketViewModel
        {
            User = new UsuarioViewModel
            {
                Initials = initials,
                FullName = userName,
                Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "usuario@smiletrack.local"
            },
            GuidePanels = new List<CentroAyudaGuidePanel>
            {
                new() { Id = "guide-1", IconName = "help_outline", Title = "Inicio rápido", SectionHeading = "Cómo comenzar", Items = new List<string> { "Crear cita", "Buscar paciente", "Asignar profesional" } },
                new() { Id = "guide-2", IconName = "support_agent", Title = "Soporte técnico", SectionHeading = "¿Necesitas ayuda?", Items = new List<string> { "Reportar incidente", "Ver estado del sistema", "Contactar soporte" } }
            },
            SupportPanels = new List<CentroAyudaSupportPanel>
            {
                new()
                {
                    IconName = "chat_bubble_outline",
                    Eyebrow = "Asistencia inmediata",
                    Heading = "Chat en línea disponible",
                    Description = "Recibe respuesta en menos de 2 horas.",
                    Bullets = new List<string>
                    {
                        "Chat en línea disponible de 8 am a 6 pm.",
                        "Recibe respuesta en menos de 2 horas."
                    }
                },
                new()
                {
                    IconName = "email_outlined",
                    Eyebrow = "Correo de soporte",
                    Heading = "Contacto por correo",
                    Description = "Envía tu incidencia a soporte@smiletrack.local.",
                    Bullets = new List<string>
                    {
                        "Envía tu incidencia a soporte@smiletrack.local.",
                        "Incluye datos de usuario y módulo afectado."
                    }
                }
            },
            Contact = new CentroAyudaContactInfo
            {
                Email = "soporte@smiletrack.local",
                Phone = "+57 300 000 0000"
            },
            SystemStatusMessage = "Todos los sistemas se encuentran operativos.",
            SystemStatusUpdatedAt = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm")
        };

        return View("~/Views/Centro_De_Ayuda/Guia De Usuario/SoporteTicket.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/soporte")]
    public IActionResult CreateTicket(SupportTicketViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "SmileTrack — Soporte";
            return View("~/Views/Centro_De_Ayuda/Guia De Usuario/SoporteTicket.cshtml", BuildSupportTicketViewModel(model));
        }

        TempData["SupportTicketMessage"] = "Gracias. Tu ticket se ha registrado correctamente y será atendido en breve.";
        return RedirectToAction(nameof(Soporte));
    }

    private static SupportTicketViewModel BuildSupportTicketViewModel(SupportTicketViewModel? model)
    {
        model ??= new SupportTicketViewModel();

        return new SupportTicketViewModel
        {
            User = model.User ?? new UsuarioViewModel(),
            GuidePanels = new List<CentroAyudaGuidePanel>
            {
                new() { Id = "guide-1", IconName = "help_outline", Title = "Inicio rápido", SectionHeading = "Cómo comenzar", Items = new List<string> { "Crear cita", "Buscar paciente", "Asignar profesional" } },
                new() { Id = "guide-2", IconName = "support_agent", Title = "Soporte técnico", SectionHeading = "¿Necesitas ayuda?", Items = new List<string> { "Reportar incidente", "Ver estado del sistema", "Contactar soporte" } }
            },
            SupportPanels = new List<CentroAyudaSupportPanel>
            {
                new()
                {
                    IconName = "chat_bubble_outline",
                    Eyebrow = "Asistencia inmediata",
                    Heading = "Chat en línea disponible",
                    Description = "Recibe respuesta en menos de 2 horas.",
                    Bullets = new List<string>
                    {
                        "Chat en línea disponible de 8 am a 6 pm.",
                        "Recibe respuesta en menos de 2 horas."
                    }
                },
                new()
                {
                    IconName = "email_outlined",
                    Eyebrow = "Correo de soporte",
                    Heading = "Contacto por correo",
                    Description = "Envía tu incidencia a soporte@smiletrack.local.",
                    Bullets = new List<string>
                    {
                        "Envía tu incidencia a soporte@smiletrack.local.",
                        "Incluye datos de usuario y módulo afectado."
                    }
                }
            },
            Contact = new CentroAyudaContactInfo
            {
                Email = "soporte@smiletrack.local",
                Phone = "+57 300 000 0000"
            },
            SystemStatusMessage = "Todos los sistemas se encuentran operativos.",
            SystemStatusUpdatedAt = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm"),
            Subject = model.Subject,
            Category = model.Category,
            Module = model.Module,
            Severity = model.Severity,
            Description = model.Description
        };
    }

    // ─── Rutas legacy — redirigen a la principal ──────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("centro-de-ayuda/st-rec-01-preguntas-frecuentes")]
    public IActionResult Strec01PreguntasFrecuentes()
        => RedirectPermanent("/centro-de-ayuda/guias-tutoriales");

    [HttpGet]
    [Authorize(Roles = "Recepcionista")]
    [Route("centro-de-ayuda/st-rec-02-reportar-incidencia")]
    public IActionResult Strec02ReportarIncidencia()
        => RedirectPermanent("/centro-de-ayuda/guias-tutoriales");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/st-adm-01-manuales-guias")]
    public IActionResult Stadm01ManualesGuias()
        => RedirectPermanent("/centro-de-ayuda/guias-tutoriales");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/st-adm-02-gestion-tickets")]
    public IActionResult Stadm02GestionTickets()
        => RedirectPermanent("/centro-de-ayuda/guias-tutoriales");

    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [Route("centro-de-ayuda/st-adm-03-video-tutoriales")]
    public IActionResult Stadm03VideoTutoriales()
        => RedirectPermanent("/centro-de-ayuda/guias-tutoriales");

    // ─── Helper: construir el ViewModel de Guías y Tutoriales ─────────────────
    private static CentroDeAyudaViewModel BuildGuiasTutorialesViewModel()
    {
        var articles = new List<CentroAyudaArticuloViewModel>
        {
            new() {
                Titulo      = "Cómo programar una cita",
                Descripcion = "Aprende paso a paso cómo agendar una nueva cita asignando paciente, profesional, consultorio y servicio en menos de 1 minuto.",
                Categoria   = "Citas",
                Icono       = "📅",
                Url         = "/centro-de-ayuda/como-programar-cita"
            },
            new() {
                Titulo      = "Gestión de pacientes",
                Descripcion = "Cómo registrar nuevos pacientes, actualizar datos y consultar el historial clínico.",
                Categoria   = "Pacientes",
                Icono       = "👥",
                Url         = "/gestion-de-pacientes/st-adm-05-gestion-pacientes"
            },
            new() {
                Titulo      = "Facturación y pagos",
                Descripcion = "Guía para generar facturas, registrar pagos y exportar reportes financieros.",
                Categoria   = "Facturación",
                Icono       = "💳",
                Url         = "/facturacion-y-pagos/st-adm-12-facturacion"
            },
            new() {
                Titulo      = "Gestión de profesionales",
                Descripcion = "Cómo registrar profesionales, asignar especialidades y gestionar sus horarios.",
                Categoria   = "Profesionales",
                Icono       = "🩺",
                Url         = "/gestion-de-profesionales/st-adm-07-gestion-profesionales"
            },
            new() {
                Titulo      = "Soporte técnico",
                Descripcion = "Canales de contacto y formulario para reportar incidencias al equipo de soporte.",
                Categoria   = "Soporte",
                Icono       = "🎧",
                Url         = "/centro-de-ayuda/soporte"
            },
        };

        return new CentroDeAyudaViewModel
        {
            Articles            = articles,
            TotalArticlesCount  = articles.Count,
            Support             = new CentroAyudaSupportInfo
            {
                ChatHours = "Chat en línea: Lun–Vie 8 am–6 pm",
                Email     = "soporte@smiletrack.local",
                Phone     = "+57 300 000 0000"
            }
        };
    }
}
