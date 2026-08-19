namespace SmileTrack_MVC.Services.Email;

public interface IEmailService
{
    Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía una notificación HTML al paciente cuando su cita cambia a estado Confirmada o Cancelada.
    /// </summary>
    /// <param name="recipientEmail">Correo del paciente.</param>
    /// <param name="nombrePaciente">Nombre completo del paciente para personalizar el saludo.</param>
    /// <param name="fechaCita">Fecha y hora de la cita.</param>
    /// <param name="profesional">Nombre del profesional asignado.</param>
    /// <param name="servicio">Nombre del servicio / motivo de la cita.</param>
    /// <param name="nuevoEstado">Estado nuevo: "confirmada" o "cancelada".</param>
    /// <param name="cancellationToken"></param>
    Task SendCitaNotificacionAsync(
        string recipientEmail,
        string nombrePaciente,
        DateTime fechaCita,
        string profesional,
        string servicio,
        string nuevoEstado,
        CancellationToken cancellationToken = default);

    Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default);
}
