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
    Incidente,
    Consulta,
    Solicitud,
    Otro
}

public enum AffectedModule
{
    Citas,
    Pacientes,
    Facturacion,
    Reportes,
    Sistema
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
    public string Subject { get; set; } = string.Empty;
    public TicketCategory Category { get; set; } = TicketCategory.Incidente;
    public AffectedModule Module { get; set; } = AffectedModule.Citas;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IFormFile? Screenshot { get; set; }
}