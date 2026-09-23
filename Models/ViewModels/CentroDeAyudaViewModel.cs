using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmileTrack_MVC.Models.ViewModels;

// ─── Centro de Ayuda — ViewModel principal ────────────────────────────────────

/// <summary>
/// Información de canales de soporte (chat, correo, teléfono).
/// Mostrada en el panel lateral de Guías y Tutoriales.
/// </summary>
public class CentroAyudaSupportInfo
{
    public string ChatHours { get; set; } = "Chat en línea: Lun–Vie 8 am–6 pm";
    public string Email     { get; set; } = "soporte@smiletrack.local";
    public string Phone     { get; set; } = "+57 300 000 0000";
}

/// <summary>
/// Artículo / guía del Centro de Ayuda.
/// </summary>
public class CentroAyudaArticuloViewModel
{
    public string Titulo      { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Categoria   { get; set; } = string.Empty;
    public string Icono       { get; set; } = "📄";
    public string Url         { get; set; } = "#";
}

/// <summary>
/// ViewModel principal de la vista Guías y Tutoriales.
/// Incluye los artículos disponibles, contadores y datos de soporte.
/// </summary>
public class CentroDeAyudaViewModel
{
    public List<CentroAyudaArticuloViewModel> Articles        { get; set; } = [];
    public CentroAyudaSupportInfo            Support          { get; set; } = new();
    public int                               TotalArticlesCount { get; set; }
}

// ─── Vista "Cómo programar una cita" ──────────────────────────────────────────

public class GuiaPasoViewModel
{
    public int    Number          { get; set; }
    public string Title           { get; set; } = string.Empty;
    public string DescriptionHtml { get; set; } = string.Empty;
}

public class GuiaUsuarioViewModel
{
    public string                Title              { get; set; } = "Cómo programar una cita";
    public string                Introduction       { get; set; } = "Aprende paso a paso cómo agendar una nueva cita en SmileTrack de forma correcta, asignando paciente, profesional, consultorio y servicio.";
    public string                VideoAltText       { get; set; } = "Tutorial de cómo programar una cita";
    public string                VideoThumbnailUrl  { get; set; } = "/images/tutorial-cita-thumb.jpg";
    public List<GuiaPasoViewModel> Steps            { get; set; } = [];
    public List<string>          RelatedTopics      { get; set; } = [];
    public List<string>          Tags               { get; set; } = [];
}

// ─── UsuarioViewModel (información básica del usuario en el sidebar) ──────────

public class UsuarioViewModel
{
    public string Initials { get; set; } = "ST";
    public string FullName { get; set; } = "Usuario SmileTrack";
    public string Email    { get; set; } = "usuario@smiletrack.local";
}

public enum TicketCategory
{
    [Display(Name = "Error técnico / Falla del sistema")]
    ErrorTecnico,

    [Display(Name = "Problema de acceso o inicio de sesión")]
    ProblemaAcceso,

    [Display(Name = "Solicitud de cambio o mejora")]
    SolicitudCambio,

    [Display(Name = "Duda o consulta general")]
    DudaConsulta,

    [Display(Name = "Problema con reporte o dato incorrecto")]
    ProblemaReporteDato
}

public enum AffectedModule
{
    [Display(Name = "Inicio")]
    Inicio,

    [Display(Name = "Reportes y Analítica")]
    ReportesAnalitica,

    [Display(Name = "Acceso y Seguridad")]
    AccesoSeguridad,

    [Display(Name = "Gestión de Pacientes")]
    GestionPacientes,

    [Display(Name = "Historia Clínica")]
    HistoriaClinica,

    [Display(Name = "Gestión de Profesionales")]
    GestionProfesionales,

    [Display(Name = "Gestión de Citas")]
    GestionCitas,

    [Display(Name = "Facturación y Pagos")]
    FacturacionPagos,

    [Display(Name = "Servicios y Recursos")]
    ServiciosRecursos,

    [Display(Name = "Gestión de PQR")]
    GestionPQR,

    [Display(Name = "Otro / No estoy seguro")]
    OtroNoEstoySeguro
}

public class CentroAyudaGuidePanel
{
    public string Id { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SectionHeading { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}

public class CentroAyudaSupportPanel
{
    public string Id { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string Eyebrow { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Bullets { get; set; } = new();
}

public class CentroAyudaContactInfo
{
    public string Email { get; set; } = "soporte@smiletrack.local";
    public string Phone { get; set; } = "+57 300 000 0000";
}

public class SupportTicketViewModel
{
    public UsuarioViewModel User { get; set; } = new();
    public List<CentroAyudaGuidePanel> GuidePanels { get; set; } = new();
    public List<CentroAyudaSupportPanel> SupportPanels { get; set; } = new();
    public CentroAyudaContactInfo Contact { get; set; } = new();
    public string SystemStatusMessage { get; set; } = "Todos los sistemas se encuentran operativos.";
    public string SystemStatusUpdatedAt { get; set; } = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm");

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [Display(Name = "Asunto *")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seleccione el tipo de incidente.")]
    [Display(Name = "Tipo de Incidente *")]
    public TicketCategory? Category { get; set; }

    [Required(ErrorMessage = "Seleccione el módulo o vista relacionada.")]
    [Display(Name = "Módulo / Vista Relacionada *")]
    public AffectedModule? Module { get; set; }

    [Required(ErrorMessage = "Seleccione la severidad.")]
    [Display(Name = "Severidad *")]
    public string Severity { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [Display(Name = "Descripción *")]
    public string Description { get; set; } = string.Empty;

    public IFormFile? Screenshot { get; set; }
}