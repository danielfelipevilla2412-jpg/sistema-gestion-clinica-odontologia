using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

// NOTA: EmailService envía realmente por SMTP (MailKit/SmtpClient) y no expone
// una abstracción inyectable de envío, por lo que NO se prueba el envío real.
// Estas pruebas cubren únicamente las partes puras / de validación que ocurren
// ANTES de abrir la conexión SMTP (validación de argumentos y de configuración).
public class EmailServiceTests
{
    private static EmailService CrearServicio(EmailServiceOptions opciones) =>
        new(Options.Create(opciones), NullLogger<EmailService>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendRecoveryCodeAsync_DestinatarioVacio_LanzaArgumentException(string destinatario)
    {
        var servicio = CrearServicio(new EmailServiceOptions());

        var excepcion = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.SendRecoveryCodeAsync(destinatario, "123456"));

        Assert.Equal("recipientEmail", excepcion.ParamName);
        Assert.Contains("destinatario es obligatorio", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendRecoveryCodeAsync_CodigoVacio_LanzaArgumentException(string codigo)
    {
        var servicio = CrearServicio(new EmailServiceOptions());

        var excepcion = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.SendRecoveryCodeAsync("paciente@test.com", codigo));

        Assert.Equal("code", excepcion.ParamName);
        Assert.Contains("código es obligatorio", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendRecoveryCodeAsync_ConfiguracionSmtpIncompleta_LanzaInvalidOperationException()
    {
        // Argumentos válidos pero EmailServiceOptions por defecto: falta Host/Username/Password.
        var servicio = CrearServicio(new EmailServiceOptions());

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.SendRecoveryCodeAsync("paciente@test.com", "123456"));

        Assert.Contains("configuración SMTP no está completa", excepcion.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Host", excepcion.Message);
        Assert.Contains("Username", excepcion.Message);
        Assert.Contains("Password", excepcion.Message);
    }

    [Fact]
    public async Task SendCitaNotificacionAsync_PacienteSinCorreo_NoLanzaExcepcion()
    {
        // Sin destinatario retorna en silencio ANTES de validar/abrir SMTP.
        var servicio = CrearServicio(new EmailServiceOptions());

        await servicio.SendCitaNotificacionAsync(
            recipientEmail: "",
            nombrePaciente: "Ana Prueba",
            fechaCita: new DateTime(2026, 12, 10, 9, 0, 0),
            profesional: "Dr. Prueba",
            servicio: "Consulta",
            nuevoEstado: "confirmada");
    }

    [Fact]
    public async Task SendCitaNotificacionAsync_ConfiguracionSmtpIncompleta_LanzaInvalidOperationException()
    {
        // Con destinatario válido pero configuración SMTP incompleta, la validación
        // se lanza antes de construir el mensaje y de conectar por red.
        var servicio = CrearServicio(new EmailServiceOptions());

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.SendCitaNotificacionAsync(
                recipientEmail: "paciente@test.com",
                nombrePaciente: "Ana Prueba",
                fechaCita: new DateTime(2026, 12, 10, 9, 0, 0),
                profesional: "Dr. Prueba",
                servicio: "Consulta",
                nuevoEstado: "confirmada"));

        Assert.Contains("configuración SMTP no está completa", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbarConfiguracionSmtpAsync_ConfiguracionIncompleta_RetornaFalloConDetalle()
    {
        var servicio = CrearServicio(new EmailServiceOptions());

        var (exito, detalle) = await servicio.ProbarConfiguracionSmtpAsync("paciente@test.com");

        Assert.False(exito);
        Assert.Contains("PASO 1 FALLÓ", detalle);
        Assert.Contains("Host", detalle);
        Assert.Contains("Username", detalle);
        Assert.Contains("Password", detalle);
    }

    [Fact]
    public async Task ProbarConfiguracionSmtpAsync_SinPassword_ReportaCampoFaltante()
    {
        var opciones = new EmailServiceOptions
        {
            Host = "smtp.gmail.com",
            Port = 587,
            Username = "clinica@gmail.com",
            Password = ""
        };
        var servicio = CrearServicio(opciones);

        var (exito, detalle) = await servicio.ProbarConfiguracionSmtpAsync("paciente@test.com");

        Assert.False(exito);
        Assert.Contains("PASO 1 FALLÓ", detalle);
        Assert.Contains("Password", detalle);
        // Host y Username sí están presentes, por lo que no deben reportarse como faltantes.
        Assert.DoesNotContain("Faltan: Host", detalle);
        Assert.DoesNotContain("Username,", detalle);
    }

    [Fact]
    public void EmailServiceOptions_ValoresPorDefecto()
    {
        var opciones = new EmailServiceOptions();

        Assert.Equal(string.Empty, opciones.Host);
        Assert.Equal(587, opciones.Port);
        Assert.True(opciones.EnableSsl);
        Assert.Equal(string.Empty, opciones.Username);
        Assert.Equal(string.Empty, opciones.Password);
        Assert.Null(opciones.RecipientOverride);
    }
}
