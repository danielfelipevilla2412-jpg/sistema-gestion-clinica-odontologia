namespace SmileTrack_MVC.Models.ViewModels.Components;

/// <summary>
/// ViewModel para el componente ProximaCitaWidget
/// Muestra información de la próxima cita con countdown y contexto
/// </summary>
public class ProximaCitaWidgetViewModel
{
    /// <summary>
    /// ID de la cita (null si no hay próxima cita)
    /// </summary>
    public int? IdCita { get; set; }

    /// <summary>
    /// Nombre completo del paciente
    /// </summary>
    public string? NombrePaciente { get; set; }

    /// <summary>
    /// Nombre del servicio odontológico
    /// </summary>
    public string? NombreServicio { get; set; }

    /// <summary>
    /// Fecha y hora de la cita
    /// </summary>
    public DateTime FechaHora { get; set; }

    /// <summary>
    /// Nombre del consultorio asignado
    /// </summary>
    public string? NombreConsultorio { get; set; }

    /// <summary>
    /// Información de la última atención del paciente
    /// </summary>
    public string? UltimaAtencion { get; set; }

    /// <summary>
    /// Indica si la cita es urgente (menos de 15 minutos)
    /// </summary>
    public bool EsUrgente => IdCita.HasValue && (FechaHora - DateTime.Now).TotalMinutes <= 15 && (FechaHora - DateTime.Now).TotalMinutes >= 0;

    /// <summary>
    /// Minutos restantes hasta la cita (puede ser negativo si ya pasó)
    /// </summary>
    public int MinutosHasta => IdCita.HasValue ? (int)(FechaHora - DateTime.Now).TotalMinutes : 0;

    /// <summary>
    /// Indica si hay una próxima cita programada
    /// </summary>
    public bool TieneCita => IdCita.HasValue;
}
