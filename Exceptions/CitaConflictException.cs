namespace SmileTrack_MVC.Exceptions;

/// <summary>
/// Excepción lanzada cuando ocurre un conflicto de agenda, solapamiento o disponibilidad de recursos.
/// Hereda de InvalidOperationException para mantener compatibilidad hacia atrás.
/// </summary>
public class CitaConflictException : InvalidOperationException
{
    public CitaConflictException(string message) : base(message) { }
    public CitaConflictException(string message, Exception innerException) : base(message, innerException) { }
}
