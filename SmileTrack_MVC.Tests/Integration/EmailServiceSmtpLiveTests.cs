using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Integration;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SmtpLiveIntegrationFactAttribute : FactAttribute
{
    public SmtpLiveIntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SMILETRACK_ALLOW_LIVE_SMTP_TESTS") != "1")
        {
            Skip = "Requires explicit opt-in; sends one message only to Smtp:RecipientOverride.";
        }
    }
}

public sealed class EmailServiceSmtpLiveTests
{
    [SmtpLiveIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task ProbarConfiguracionSmtpAsync_EnvioReal_UsaUserSecretsYRecipientOverride()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets("ff5d4b9c-690c-44b5-a2e6-8cd9fe7ea759")
            .Build();
        EmailServiceOptions options =
            configuration.GetSection("Smtp").Get<EmailServiceOptions>()
            ?? throw new InvalidOperationException("No se encontró la sección Smtp en User Secrets.");

        Assert.Equal("smtp.gmail.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.False(string.IsNullOrWhiteSpace(options.Username), "Smtp:Username falta en User Secrets.");
        Assert.False(string.IsNullOrWhiteSpace(options.Password), "Smtp:Password falta en User Secrets.");
        Assert.False(string.IsNullOrWhiteSpace(options.RecipientOverride), "Smtp:RecipientOverride falta en User Secrets.");

        IEmailService emailService = new EmailService(
            Options.Create(options),
            NullLogger<EmailService>.Instance);

        var (exito, detalle) =
            await emailService.ProbarConfiguracionSmtpAsync("no-enviar-a-este-destino@example.invalid");

        Assert.True(exito, detalle);
    }
}
