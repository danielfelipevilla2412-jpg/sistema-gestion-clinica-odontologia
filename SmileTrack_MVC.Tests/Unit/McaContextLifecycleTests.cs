using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class McaContextLifecycleTests
{
    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [Fact]
    [Trait("Category", "MCA-Closure")]
    public void ResetConversation_ClearsState_AndAllowsFreshConversation()
    {
        var service = new ChatbotService();
        service.Respond("Administrador", "Ana", "/", "¿Cómo registro un usuario?", "k1");
        Assert.NotNull(service.GetConversationState("k1"));

        service.ResetConversation("k1");

        Assert.Null(service.GetConversationState("k1"));
        var response = service.Respond("Administrador", "Ana", "/", "¿Qué hace esta vista?", "k1");
        Assert.NotNull(response);
    }

    [Fact]
    [Trait("Category", "MCA-Closure")]
    public void Conversation_Expires_After30Minutes()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
        var service = new ChatbotService(timeProvider: time);
        service.Respond("Administrador", "Ana", "/", "¿Cómo registro un usuario?", "k1");
        Assert.NotNull(service.GetConversationState("k1"));

        time.UtcNow = time.UtcNow.AddMinutes(31);
        service.PurgeConversationStates();

        Assert.Null(service.GetConversationState("k1"));
    }

    [Fact]
    [Trait("Category", "MCA-Closure")]
    public void ConversationState_IsIsolatedByKey_AndCapsAt5000()
    {
        var service = new ChatbotService();
        for (var i = 0; i < 5005; i++)
            service.Respond("Administrador", "Ana", "/", "hola", $"k-{i}");

        service.PurgeConversationStates();
        // Oldest should be evicted; newest should exist.
        Assert.Null(service.GetConversationState("k-0"));
        Assert.NotNull(service.GetConversationState("k-5004"));
    }

    [Fact]
    [Trait("Category", "MCA-Closure")]
    public void ConcurrentMessages_DoNotCorruptState()
    {
        var service = new ChatbotService();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        Parallel.For(0, 40, i =>
        {
            try
            {
                var key = $"k-{i % 5}";
                service.Respond("Administrador", "Ana", "/", i % 2 == 0 ? "¿Cómo registro un usuario?" : "¿Dónde veo la bitácora?", key);
                service.GetConversationState(key);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        Assert.Empty(errors);
    }
}
