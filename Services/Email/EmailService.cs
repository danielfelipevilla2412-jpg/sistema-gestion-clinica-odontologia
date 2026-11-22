using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SmileTrack_MVC.Services.Email;

public class EmailService(IOptions<EmailServiceOptions> options, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailServiceOptions _options = options.Value;
    private readonly ILogger<EmailService> _logger = logger;

    public async Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new ArgumentException("El destinatario es obligatorio.", nameof(recipientEmail));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("El código es obligatorio.", nameof(code));
        }

        ValidarConfiguracionSmtp();

        string finalRecipient = string.IsNullOrWhiteSpace(_options.RecipientOverride)
            ? recipientEmail
            : _options.RecipientOverride.Trim();

        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
        };

        _logger.LogInformation(
            "Enviando correo de recuperación. HostSmtp={Host} Puerto={Port} SocketOptions={SocketOptions} " +
            "Remitente={From} DestinatarioOriginal={OriginalRecipient} DestinatarioFinal={FinalRecipient} " +
            "RecipientOverrideActivo={OverrideActivo}",
            _options.Host, _options.Port, socketOptions,
            _options.Username, recipientEmail, finalRecipient,
            !string.IsNullOrWhiteSpace(_options.RecipientOverride));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SmileTrack", _options.Username));
        message.To.Add(new MailboxAddress(finalRecipient, finalRecipient));
        message.Subject = "SmileTrack — Código para restablecer tu contraseña";
        string codigoSeguro = WebUtility.HtmlEncode(code.Trim());

        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"""
Hola,

Recibimos una solicitud para restablecer la contraseña de tu cuenta en SmileTrack.

Tu codigo de verificacion es:

{codigoSeguro}

Este codigo es valido durante 15 minutos a partir de este momento. Ingresalo en la pantalla de recuperacion de contrasena para continuar con el proceso.

Si tu no solicitaste este cambio, puedes ignorar este correo con tranquilidad: tu contrasena actual seguira funcionando sin cambios. Sin embargo, si esto ocurre repetidamente, te recomendamos cambiar tu contrasena por precaucion.

Por tu seguridad, nunca compartas este codigo con nadie, ni siquiera con personal de SmileTrack.

SmileTrack | Sistema de Gestion Odontologica
Este es un mensaje automatico, por favor no respondas a este correo.
""",
            HtmlBody = $"""
<html>
  <body style="margin:0;padding:24px;background-color:#f4f7fb;font-family:Arial,sans-serif;color:#1f2937;">
    <div style="max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
      <div style="padding:24px 24px 12px 24px;background:#0f766e;color:#ffffff;">
        <h1 style="margin:0;font-size:22px;">SmileTrack</h1>
        <p style="margin:8px 0 0 0;font-size:14px;">Código para restablecer tu contraseña</p>
      </div>
      <div style="padding:24px;">
        <p style="margin:0 0 16px 0;">Hola,</p>
        <p style="margin:0 0 16px 0;">
          Recibimos una solicitud para restablecer la contraseña de tu cuenta en SmileTrack.
        </p>
        <p style="margin:0 0 12px 0;">Tu código de verificación es:</p>
        <div style="margin:0 0 20px 0;padding:16px;text-align:center;font-size:32px;font-weight:700;letter-spacing:8px;background:#f8fafc;border:1px dashed #94a3b8;border-radius:10px;">
          {codigoSeguro}
        </div>
        <p style="margin:0 0 16px 0;">
          Este código es válido durante <strong>15 minutos</strong> a partir de este momento.
          Ingrésalo en la pantalla de recuperación de contraseña para continuar con el proceso.
        </p>
        <p style="margin:0 0 16px 0;">
          Si tú no solicitaste este cambio, puedes ignorar este correo con tranquilidad; tu contraseña actual seguirá funcionando sin cambios.
          Sin embargo, si esto ocurre repetidamente, te recomendamos cambiar tu contraseña por precaución.
        </p>
        <p style="margin:0 0 16px 0;">
          Por tu seguridad, nunca compartas este código con nadie, ni siquiera con personal de SmileTrack.
        </p>
      </div>
      <div style="padding:16px 24px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:12px;color:#6b7280;">
        SmileTrack | Sistema de Gestión Odontológica<br />
        Este es un mensaje automático, por favor no respondas a este correo.
      </div>
    </div>
  </body>
</html>
"""
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 15_000;
        try
        {
            _logger.LogDebug("Paso 1/3 — Conectando a SMTP Host={Host} Puerto={Port}", _options.Host, _options.Port);
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
            _logger.LogDebug("Paso 1/3 OK — Conectado a SMTP. Capabilities={Capas}", client.Capabilities);

            _logger.LogDebug("Paso 2/3 — Autenticando en SMTP con Usuario={User}", _options.Username);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            _logger.LogDebug("Paso 2/3 OK — Autenticación SMTP exitosa.");

            _logger.LogDebug("Paso 3/3 — Enviando mensaje a {Recipient}", finalRecipient);
            string response = await client.SendAsync(message, cancellationToken);
            _logger.LogInformation("Paso 3/3 OK — Correo enviado exitosamente. Destinatario={Recipient} RespuestaSmtp={SmtpResponse}",
                finalRecipient, response ?? string.Empty);
        }
        catch (AuthenticationException authEx)
        {
            _logger.LogError(authEx,
                "FALLO SMTP Paso 2/3 (Autenticación). Verifica: 1) Que el Username/PASSWORD sean correctos. " +
                "2) Si usas Gmail, QUE LA CONTRASEÑA SEA UNA 'APP PASSWORD' (16 caracteres) Y NO la contraseña normal de Google. " +
                "3) Que la cuenta tenga activada la Verificación en 2 Pasos. " +
                "Host={Host} User={User} Port={Port} SocketOptions={Opts}",
                _options.Host, _options.Username, _options.Port, socketOptions);
            throw new InvalidOperationException(
                "No fue posible autenticarse en el servidor SMTP. Si usas Gmail, debes generar una 'Contraseña de aplicación' (App Password) " +
                "con la Verificación en 2 pasos activada; la contraseña normal de tu cuenta NO funciona.",
                authEx);
        }
        catch (SocketException sockEx)
        {
            _logger.LogError(sockEx,
                "FALLO SMTP Paso 1/3 (Conexión). No se pudo conectar al Host={Host} Puerto={Port}. " +
                "Posibles causas: host incorrecto, cortafuegos bloqueando el puerto, antivirus, VPN, o el proveedor de internet bloquea el puerto. " +
                "Prueba puerto 587 (STARTTLS) si 465 falla, o viceversa. SocketOptions={Opts}",
                _options.Host, _options.Port, socketOptions);
            throw new InvalidOperationException(
                $"No se pudo conectar al servidor SMTP {_options.Host}:{_options.Port}. Revisa cortafuegos, VPN o si el puerto está bloqueado por tu red.",
                sockEx);
        }
        catch (SmtpCommandException smtpCmdEx)
        {
            _logger.LogError(smtpCmdEx,
                "FALLO SMTP Comando. Etapa={StatusCode} MensajeServidor={ServerMessage} ErrorCode={ErrorCode} " +
                "Host={Host} User={User} Recipient={Recipient}",
                smtpCmdEx.StatusCode, smtpCmdEx.Message, smtpCmdEx.ErrorCode,
                _options.Host, _options.Username, finalRecipient);

            if (smtpCmdEx.ErrorCode == SmtpErrorCode.RecipientNotAccepted ||
                smtpCmdEx.StatusCode == SmtpStatusCode.MailboxUnavailable ||
                smtpCmdEx.StatusCode == SmtpStatusCode.UserNotLocalTryAlternatePath)
            {
                throw new InvalidOperationException(
                    $"El servidor SMTP rechazó el destinatario '{finalRecipient}'. Verifica que el correo exista y no esté bloqueado.",
                    smtpCmdEx);
            }

            if ((int)smtpCmdEx.StatusCode == 530)
            {
                throw new InvalidOperationException(
                    "El servidor requiere STARTTLS. Asegúrate de usar puerto 587 y EnableSsl=true, o 465 con SSL implícito.",
                    smtpCmdEx);
            }

            if ((int)smtpCmdEx.StatusCode == 535 || smtpCmdEx.StatusCode == SmtpStatusCode.AuthenticationRequired)
            {
                throw new InvalidOperationException(
                    "Credenciales SMTP rechazadas (código 535). Gmail: usa una App Password (16 caracteres) con 2FA activado.",
                    smtpCmdEx);
            }

            if (smtpCmdEx.Message.Contains("QUOTA", StringComparison.OrdinalIgnoreCase) ||
                smtpCmdEx.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Límite de envío alcanzado (Google rate limit / quota). Espera unos minutos y vuelve a intentar.",
                    smtpCmdEx);
            }

            if (smtpCmdEx.Message.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
                smtpCmdEx.Message.Contains("suspicious", StringComparison.OrdinalIgnoreCase) ||
                smtpCmdEx.Message.Contains("compromised", StringComparison.OrdinalIgnoreCase) ||
                smtpCmdEx.Message.Contains("less secure", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Google ha bloqueado el inicio de sesión por actividad sospechosa, o has desactivado 2FA y te niega el acceso. " +
                    "Activa 2FA y genera una Contraseña de Aplicación, o ingresa a tu cuenta y aprueba el intento de inicio.",
                    smtpCmdEx);
            }

            throw new InvalidOperationException(
                $"Error SMTP del servidor: {smtpCmdEx.StatusCode} — {smtpCmdEx.Message}", smtpCmdEx);
        }
        catch (SmtpProtocolException protoEx)
        {
            _logger.LogError(protoEx,
                "FALLO SMTP Protocolo. Esto usualmente pasa por un puerto / SSL equivocado (ej: 465 con StartTls o 587 con SslOnConnect). " +
                "Host={Host} Port={Port} SocketOptions={Opts}",
                _options.Host, _options.Port, socketOptions);
            throw new InvalidOperationException(
                "Error de protocolo SMTP: verifica que el puerto coincida con el tipo de SSL. Gmail: 465 = SSL, 587 = STARTTLS.",
                protoEx);
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx,
                "FALLO SMTP IO. Conexión interrumpida a mitad del envío. Host={Host} Port={Port}",
                _options.Host, _options.Port);
            throw new InvalidOperationException(
                "La conexión SMTP se cerró inesperadamente. Puede ser un problema temporal de red o cortafuegos.", ioEx);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Envío de correo cancelado por el cliente o timeout. Host={Host} Port={Port}", _options.Host, _options.Port);
            throw new InvalidOperationException("El envío del correo fue cancelado o superó el tiempo de espera (timeout).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FALLO SMTP Error genérico inesperado. Host={Host} Port={Port} SocketOptions={Opts} User={User} Dest={Dest}",
                _options.Host, _options.Port, socketOptions, _options.Username, finalRecipient);
            throw new InvalidOperationException($"Error inesperado al enviar correo: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true, cancellationToken);
                    _logger.LogDebug("Conexión SMTP cerrada correctamente.");
                }
            }
            catch (Exception disEx)
            {
                _logger.LogWarning(disEx, "No se pudo desconectar limpiamente el cliente SMTP (no crítico).");
            }
        }
    }

    public async Task SendProfessionalTemporaryPasswordAsync(
        string recipientEmail,
        string nombreProfesional,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("El destinatario es obligatorio.", nameof(recipientEmail));
        if (string.IsNullOrWhiteSpace(temporaryPassword))
            throw new ArgumentException("La contraseña temporal es obligatoria.", nameof(temporaryPassword));

        ValidarConfiguracionSmtp();
        string finalRecipient = string.IsNullOrWhiteSpace(_options.RecipientOverride)
            ? recipientEmail
            : _options.RecipientOverride.Trim();
        string nombreSafe = WebUtility.HtmlEncode(nombreProfesional);
        string passwordSafe = WebUtility.HtmlEncode(temporaryPassword);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SmileTrack", _options.Username));
        message.To.Add(new MailboxAddress(finalRecipient, finalRecipient));
        message.Subject = "SmileTrack — Acceso temporal a tu cuenta profesional";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hola {nombreProfesional},\n\nSe creó tu cuenta profesional en SmileTrack.\n\nContraseña temporal: {temporaryPassword}\n\nIngresa al sistema y cámbiala al iniciar sesión. No compartas esta contraseña.",
            HtmlBody = $"""
<html><body style="margin:0;padding:24px;background:#f4f7fb;font-family:Arial,sans-serif;">
<div style="max-width:600px;margin:auto;background:#fff;border:1px solid #e5e7eb;border-radius:12px;padding:24px;">
<h1 style="color:#0f766e;">SmileTrack</h1>
<p>Hola, <strong>{nombreSafe}</strong>.</p>
<p>Se creó tu cuenta profesional. Usa esta contraseña temporal para el primer acceso:</p>
<div style="margin:20px 0;padding:16px;text-align:center;font-size:24px;font-weight:bold;background:#f8fafc;border:1px dashed #94a3b8;border-radius:8px;">{passwordSafe}</div>
<p>Ingresa al sistema y cámbiala inmediatamente. No compartas esta contraseña con terceros.</p>
</div></body></html>
"""
        }.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 15_000;
        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
        };

        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    // ─── SendCitaNotificacionAsync ─────────────────────────────────────────
    // Notifica al paciente cuando su cita cambia a estado Confirmada o Cancelada.
    // Se llama desde GestionCitasController de forma fire-and-forget con Task.Run
    // para no bloquear la respuesta al usuario si el SMTP tarda.
    public async Task SendCitaNotificacionAsync(
        string recipientEmail,
        string nombrePaciente,
        DateTime fechaCita,
        string profesional,
        string servicio,
        string nuevoEstado,
        CancellationToken cancellationToken = default,
        string? motivoCancelacion = null,
        string? mensajePersonalizado = null)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return; // silencioso: paciente sin correo

        ValidarConfiguracionSmtp();

        string finalRecipient = string.IsNullOrWhiteSpace(_options.RecipientOverride)
            ? recipientEmail
            : _options.RecipientOverride.Trim();

        bool esRecordatorio = nuevoEstado.Equals("recordatorio", StringComparison.OrdinalIgnoreCase);
        bool esConfirmada = nuevoEstado.Equals("confirmada", StringComparison.OrdinalIgnoreCase);
        string estadoLabel = esRecordatorio ? "Recordatorio de Cita 🔔" : esConfirmada ? "Confirmada ✅" : "Cancelada ❌";
        string colorBanner = esRecordatorio ? "#2563eb" : esConfirmada ? "#0f766e" : "#dc2626";
        string colorEstado = esRecordatorio ? "#1e40af" : esConfirmada ? "#166534" : "#991b1b";
        string bgEstado = esRecordatorio ? "#dbeafe" : esConfirmada ? "#dcfce7" : "#fee2e2";
        string asunto = esRecordatorio
            ? $"SmileTrack — Recordatorio: Tu cita médica es el {fechaCita:dd/MM/yyyy}"
            : esConfirmada
                ? $"SmileTrack — Tu cita del {fechaCita:dd/MM/yyyy} fue confirmada"
                : $"SmileTrack — Tu cita del {fechaCita:dd/MM/yyyy} fue cancelada";

        string nombreSafe = WebUtility.HtmlEncode(nombrePaciente);
        string profSafe = WebUtility.HtmlEncode(profesional);
        string servicioSafe = WebUtility.HtmlEncode(servicio);
        string motivoSafe = WebUtility.HtmlEncode(motivoCancelacion ?? string.Empty);
        string mensajeSafe = WebUtility.HtmlEncode(mensajePersonalizado ?? string.Empty)
            .Replace("\r\n", "<br />")
            .Replace("\n", "<br />");
        string fechaStr = fechaCita.ToString("dddd d 'de' MMMM 'de' yyyy 'a las' HH:mm",
                               new System.Globalization.CultureInfo("es-CO"));
        string contenidoFinalHtml = esRecordatorio
            ? string.IsNullOrWhiteSpace(mensajeSafe)
                ? string.Empty
                : $"<p style=\"margin:0 0 20px;font-size:14px;\">{mensajeSafe}</p>"
            : esConfirmada
            ? """
              <p style="margin:0 0 8px;font-size:14px;">Recuerda:</p>
              <ul style="margin:0 0 20px;padding-left:20px;font-size:14px;line-height:1.7;">
                <li>Llega 10 minutos antes de tu cita.</li>
                <li>Trae tu documento de identidad.</li>
                <li>Si necesitas cancelar, avísanos con al menos 24 horas de anticipación.</li>
              </ul>
              """
            : $"""
              <p style="margin:0 0 20px;font-size:14px;">
                Para reagendar tu cita, comunícate con nuestro equipo de recepción o ingresa al portal de SmileTrack.
              </p>
              {(string.IsNullOrWhiteSpace(motivoSafe) ? string.Empty : $"<p style=\"margin:0 0 20px;font-size:14px;\"><strong>Motivo de cancelación:</strong> {motivoSafe}</p>")}
              """;
        string contenidoFinalTexto = esRecordatorio
            ? mensajePersonalizado ?? string.Empty
            : esConfirmada
            ? "Recuerda llegar 10 minutos antes y traer tu documento de identidad."
            : $"Para reagendar contáctanos o ingresa al portal SmileTrack.{(string.IsNullOrWhiteSpace(motivoCancelacion) ? string.Empty : $"\nMotivo de cancelación: {motivoCancelacion}")}";

        string htmlBody = $"""
<html>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
  <div style="max-width:600px;margin:0 auto;background:#fff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">

    <!-- Banner superior -->
    <div style="padding:20px 24px;background:{colorBanner};color:#fff;">
      <h1 style="margin:0;font-size:20px;font-weight:700;">SmileTrack</h1>
      <p style="margin:6px 0 0;font-size:14px;">Sistema de Gestión Odontológica</p>
    </div>

    <!-- Cuerpo -->
    <div style="padding:28px 24px;">
      <p style="margin:0 0 16px;font-size:16px;">Hola, <strong>{nombreSafe}</strong>,</p>

      <p style="margin:0 0 20px;">
        {(esRecordatorio
                    ? "Te recordamos tu cita odontológica programada. Te esperamos en la fecha indicada."
                    : esConfirmada
                        ? "Te informamos que tu cita odontológica ha sido <strong>confirmada</strong>. Te esperamos en la fecha indicada."
                        : "Lamentamos informarte que tu cita odontológica ha sido <strong>cancelada</strong>. Por favor contáctanos para reagendarla.")}
      </p>

      <!-- Detalles de la cita -->
      <div style="background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;padding:20px;margin-bottom:24px;">
        <h2 style="margin:0 0 16px;font-size:15px;color:#374151;font-weight:700;">Detalles de tu cita</h2>
        <table style="width:100%;border-collapse:collapse;font-size:14px;">
          <tr>
            <td style="padding:6px 0;color:#6b7280;width:40%;">📅 Fecha y hora</td>
            <td style="padding:6px 0;font-weight:600;">{fechaStr}</td>
          </tr>
          <tr>
            <td style="padding:6px 0;color:#6b7280;">🩺 Profesional</td>
            <td style="padding:6px 0;font-weight:600;">{profSafe}</td>
          </tr>
          <tr>
            <td style="padding:6px 0;color:#6b7280;">🦷 Servicio</td>
            <td style="padding:6px 0;font-weight:600;">{servicioSafe}</td>
          </tr>
          <tr>
            <td style="padding:6px 0;color:#6b7280;">Estado</td>
            <td style="padding:6px 0;">
              <span style="display:inline-block;padding:3px 10px;background:{bgEstado};color:{colorEstado};border-radius:20px;font-weight:700;font-size:13px;">
                {estadoLabel}
              </span>
            </td>
          </tr>
        </table>
      </div>

      {contenidoFinalHtml}
    </div>

    <!-- Footer -->
    <div style="padding:16px 24px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:12px;color:#6b7280;text-align:center;">
      SmileTrack | Sistema de Gestión Odontológica<br />
      Este es un mensaje automático, por favor no respondas a este correo.
    </div>

  </div>
</body>
</html>
""";

        string textoPrincipal = esRecordatorio
            ? "Te recordamos tu cita odontológica programada."
            : esConfirmada
                ? "Tu cita odontológica ha sido CONFIRMADA."
                : "Tu cita odontológica ha sido CANCELADA.";
        string textBody = $"""
Hola {nombrePaciente},

{textoPrincipal}

Detalles:
  Fecha y hora : {fechaStr}
  Profesional  : {profesional}
  Servicio     : {servicio}
  Estado       : {estadoLabel}

{contenidoFinalTexto}

SmileTrack | Sistema de Gestión Odontológica
Este es un mensaje automático, no respondas a este correo.
""";

        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
        };

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SmileTrack", _options.Username));
        message.To.Add(new MailboxAddress(finalRecipient, finalRecipient));
        message.Subject = asunto;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 15_000;
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation(
                "Notificación de cita enviada. Estado={Estado} Destinatario={Dest} Fecha={Fecha}",
                nuevoEstado, finalRecipient, fechaCita);
        }
        catch (Exception ex)
        {
            if (esRecordatorio)
                throw;

            // No propagamos: el email es best-effort; la operación principal ya se guardó.
            _logger.LogWarning(ex,
                "No se pudo enviar notificación de cita. Estado={Estado} Destinatario={Dest}",
                nuevoEstado, finalRecipient);
        }
        finally
        {
            try { if (client.IsConnected) await client.DisconnectAsync(true, cancellationToken); }
            catch { }
        }
    }

    public async Task SendFacturaRecordatorioAsync(
        string recipientEmail,
        string nombrePaciente,
        IReadOnlyList<FacturaRecordatorioEmail> facturas,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("El correo del destinatario es obligatorio.", nameof(recipientEmail));
        if (facturas is null || facturas.Count == 0)
            throw new ArgumentException("Debe incluirse al menos una factura vencida.", nameof(facturas));

        ValidarConfiguracionSmtp();

        string finalRecipient = string.IsNullOrWhiteSpace(_options.RecipientOverride)
            ? recipientEmail
            : _options.RecipientOverride.Trim();
        string nombreSeguro = WebUtility.HtmlEncode(nombrePaciente);
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var filasHtml = new StringBuilder();
        var detalleTexto = new StringBuilder();

        foreach (var factura in facturas)
        {
            string numeroSeguro = WebUtility.HtmlEncode(factura.NumeroFactura);
            string fecha = factura.FechaFactura.ToString("dd/MM/yyyy", cultura);
            string saldo = factura.SaldoPendiente.ToString("C0", cultura);
            filasHtml.Append(
                $"<tr><td style=\"padding:8px;border-bottom:1px solid #e2e8f0;\">{numeroSeguro}</td>" +
                $"<td style=\"padding:8px;border-bottom:1px solid #e2e8f0;\">{fecha}</td>" +
                $"<td style=\"padding:8px;border-bottom:1px solid #e2e8f0;text-align:right;\">{saldo}</td></tr>");
            detalleTexto.AppendLine($"- Factura {factura.NumeroFactura}, emitida el {fecha}: saldo pendiente {saldo}.");
        }

        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"""
Hola {nombrePaciente},

Te recordamos que tienes facturas pendientes de pago en SmileTrack:

{detalleTexto}
Para consultar las opciones de pago, comunícate con el equipo de recepción.

SmileTrack | Sistema de Gestión Odontológica
Este es un mensaje automático, por favor no respondas a este correo.
""",
            HtmlBody = $"""
<html>
  <body style="margin:0;padding:24px;background:#f4f7fb;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
    <div style="max-width:640px;margin:0 auto;background:#fff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
      <div style="padding:20px 24px;background:#1a56cc;color:#fff;">
        <h1 style="margin:0;font-size:20px;">SmileTrack</h1>
        <p style="margin:6px 0 0;font-size:14px;">Recordatorio de pago</p>
      </div>
      <div style="padding:24px;">
        <p>Hola, <strong>{nombreSeguro}</strong>.</p>
        <p>Te recordamos que tienes las siguientes facturas pendientes de pago:</p>
        <table style="width:100%;border-collapse:collapse;font-size:14px;">
          <thead><tr><th style="padding:8px;text-align:left;">Factura</th><th style="padding:8px;text-align:left;">Fecha</th><th style="padding:8px;text-align:right;">Saldo pendiente</th></tr></thead>
          <tbody>{filasHtml}</tbody>
        </table>
        <p>Para consultar las opciones de pago, comunícate con el equipo de recepción.</p>
      </div>
      <div style="padding:16px 24px;background:#f8fafc;border-top:1px solid #e5e7eb;font-size:12px;color:#6b7280;text-align:center;">
        SmileTrack | Sistema de Gestión Odontológica<br />
        Este es un mensaje automático, por favor no respondas a este correo.
      </div>
    </div>
  </body>
</html>
"""
        };

        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
        };
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SmileTrack", _options.Username));
        message.To.Add(new MailboxAddress(finalRecipient, finalRecipient));
        message.Subject = "SmileTrack — Recordatorio de pago";
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15_000 };
        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation(
                "Recordatorio de pago enviado. Facturas={CantidadFacturas}",
                facturas.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible enviar un recordatorio de pago.");
            throw new InvalidOperationException("No fue posible enviar el recordatorio de pago.", ex);
        }
        finally
        {
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cerrar limpiamente la conexión SMTP del recordatorio de pago.");
            }
        }
    }

    private void ValidarConfiguracionSmtp()
    {
        var faltantes = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Host)) faltantes.Add(nameof(EmailServiceOptions.Host));
        if (_options.Port <= 0) faltantes.Add(nameof(EmailServiceOptions.Port));
        if (string.IsNullOrWhiteSpace(_options.Username)) faltantes.Add(nameof(EmailServiceOptions.Username));
        if (string.IsNullOrWhiteSpace(_options.Password)) faltantes.Add(nameof(EmailServiceOptions.Password));

        if (faltantes.Count > 0)
        {
            _logger.LogCritical(
                "Configuración SMTP incompleta. Faltan las claves: {Faltantes}",
                faltantes);

            throw new InvalidOperationException(
                $"La configuración SMTP no está completa. Faltan: {string.Join(", ", faltantes)}. " +
                "Revisa la sección 'Smtp' de la configuración de la aplicación.");
        }

        if (_options.Port is not (25 or 465 or 587 or 2525))
        {
            _logger.LogWarning(
                "Puerto SMTP {Port} es inusual. Puertos comunes: 465 (SSL implícito), 587 (STARTTLS), 25 (sin cifrar - NO recomendado), 2525 (alternativo).",
                _options.Port);
        }

        if (_options.Port == 465 && socketOptionsLocal() != SecureSocketOptions.SslOnConnect)
        {
            _logger.LogWarning("Puerto 465 normalmente requiere SecureSocketOptions.SslOnConnect. Revisa EnableSsl=true.");
        }
        else if (_options.Port == 587 && socketOptionsLocal() != SecureSocketOptions.StartTls)
        {
            _logger.LogWarning("Puerto 587 normalmente requiere SecureSocketOptions.StartTls. Revisa EnableSsl=true.");
        }

        SecureSocketOptions socketOptionsLocal()
            => _options.Port switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
            };
    }

    public async Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default)
    {
        // Conserva la firma del contrato; el destinatario efectivo siempre es RecipientOverride.
        _ = correoDestino;
        var pasos = new List<string>();
        try
        {
            ValidarConfiguracionSmtp();
        }
        catch (InvalidOperationException)
        {
            return (false, "PASO 1 FALLÓ — Configuración SMTP incompleta o inválida.");
        }

        string? recipientOverride = _options.RecipientOverride?.Trim();
        if (string.IsNullOrWhiteSpace(recipientOverride))
        {
            return (false, "PASO 1 FALLÓ — Smtp:RecipientOverride es obligatorio para la prueba.");
        }

        pasos.Add("PASO 1 OK — Configuración SMTP válida.");

        var socketOptions = _options.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None
        };

        using var client = new SmtpClient();
        client.Timeout = 10_000;

        try
        {
            try
            {
                await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Prueba SMTP: falló la conexión. ExceptionType={ExceptionType}",
                    exception.GetType().Name);
                return (false, "PASO 2 FALLÓ — No se pudo establecer la conexión SMTP.");
            }

            pasos.Add("PASO 2 OK — Conexión SMTP establecida.");

            try
            {
                await client.AuthenticateAsync(_options.Username!, _options.Password!, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Prueba SMTP: falló la autenticación. ExceptionType={ExceptionType}",
                    exception.GetType().Name);
                pasos.Add("PASO 3 FALLÓ — Gmail rechazó la autenticación; revisa las credenciales guardadas.");
                return (false, string.Join("\n", pasos));
            }

            pasos.Add("PASO 3 OK — Autenticación SMTP aceptada.");

            try
            {
                var mensaje = new MimeMessage();
                mensaje.From.Add(new MailboxAddress("SmileTrack — Prueba SMTP", _options.Username));
                mensaje.To.Add(new MailboxAddress(recipientOverride, recipientOverride));
                mensaje.Subject = "SmileTrack — Prueba SMTP";
                mensaje.Body = new BodyBuilder
                {
                    TextBody = "Mensaje de prueba SMTP. No contiene datos de pacientes."
                }.ToMessageBody();

                await client.SendAsync(mensaje, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Prueba SMTP: falló el envío. ExceptionType={ExceptionType}",
                    exception.GetType().Name);
                pasos.Add("PASO 4 FALLÓ — El servidor SMTP no aceptó el mensaje.");
                return (false, string.Join("\n", pasos));
            }

            pasos.Add("PASO 4 OK — Mensaje aceptado por el servidor SMTP.");
            pasos.Add("FIN — Revisa la bandeja de prueba y la carpeta de spam.");
            return (true, string.Join("\n", pasos));
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Prueba SMTP: falló el cierre de la conexión. ExceptionType={ExceptionType}",
                        exception.GetType().Name);
                }
            }
        }
    }
}

public class EmailServiceOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? RecipientOverride { get; set; }
}
