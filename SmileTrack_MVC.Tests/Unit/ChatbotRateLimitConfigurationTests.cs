using System;
using System.Linq;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmileTrack_MVC.Controllers.Api;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

// Verifica el cableado de las políticas de limitación de tasa del MCA y la semántica
// de la ventana fija usada por Program.cs (sin cola: rechazo inmediato, sin bloquear).
public sealed class ChatbotRateLimitConfigurationTests
{
    private static string? PolicyOf(string methodName)
    {
        var method = typeof(ChatbotApiController).GetMethod(
            methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);

        return method!
            .GetCustomAttributes<EnableRateLimitingAttribute>(inherit: true)
            .Select(attribute => attribute.PolicyName)
            .FirstOrDefault();
    }

    [Theory]
    [InlineData("Message", "ChatbotMessageByUser")]
    [InlineData("ResetConversation", "ChatbotMessageByUser")]
    [InlineData("CreateTicket", "ChatbotTicketByUser")]
    [InlineData("GetMyTickets", "ChatbotTicketReadByUser")]
    [InlineData("GetMyTicket", "ChatbotTicketReadByUser")]
    [InlineData("GetMyTicketAttachment", "ChatbotTicketReadByUser")]
    public void ChatbotEndpoints_UseExpectedRateLimitPolicy(string action, string expectedPolicy)
    {
        Assert.Equal(expectedPolicy, PolicyOf(action));
    }

    [Fact]
    public void FixedWindowLimiter_PermitsUpToLimitThenRejectsImmediately()
    {
        // Replica de CreateFixedWindowOptions con el límite documentado de consultas (15/min).
        using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });

        var concedidas = 0;
        for (var i = 0; i < 15; i++)
        {
            using var lease = limiter.AttemptAcquire();
            if (lease.IsAcquired)
                concedidas++;
        }

        Assert.Equal(15, concedidas);

        // Sin cola: la solicitud número 16 se rechaza de inmediato (no espera).
        using var rechazada = limiter.AttemptAcquire();
        Assert.False(rechazada.IsAcquired);
    }

    [Fact]
    public void ChatbotOptions_DocumentedLimitsMatchConfiguredPolicies()
    {
        var options = new SmileTrack_MVC.Services.ChatbotOptions();

        Assert.Equal(15, options.MessagesPerMinute);
        Assert.Equal(5, options.TicketRequestsPer15Minutes);
        Assert.Equal(120, options.GlobalRequestsPerMinute);
        Assert.Equal(500, options.MaxMessageLength);
    }
}
