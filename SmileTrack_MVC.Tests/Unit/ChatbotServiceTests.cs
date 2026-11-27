using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using SmileTrack_MVC.Services;
using Xunit;
using Xunit.Abstractions;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ChatbotServiceTests
{
    private readonly ChatbotService _chatbot = new();
    private readonly ITestOutputHelper _output;

    public ChatbotServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Respond_AppointmentQuestion_ReturnsVerifiedRoleSpecificSteps()
    {
        var response = _chatbot.Respond(
            "Recepcionista",
            null,
            "/",
            "¿Cómo programo una cita?");

        Assert.Contains("agenda correspondiente", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("profesional, servicio, consultorio y horario", response.Message);
        Assert.Contains(response.Actions, action =>
            action.Url == "/gestion-de-citas/st-rec-03-gestion-citas");
        Assert.Contains(response.Sources, source =>
            source.Url == "/gestion-de-citas/st-rec-03-gestion-citas");
    }

    [Fact]
    public void Respond_AppointmentQuestion_UsesAdministratorAgenda()
    {
        var response = _chatbot.Respond(
            "Administrador",
            null,
            "/",
            "¿Cómo programo una cita?");

        Assert.Contains(response.Actions, action =>
            action.Url == "/gestion-de-citas/st-adm-08-agenda");
        Assert.Contains(response.Sources, source =>
            source.Url == "/gestion-de-citas/st-adm-08-agenda");
    }

    [Fact]
    public void Respond_AppointmentQuestionForPatient_DoesNotSuggestReceptionWorkflow()
    {
        var response = _chatbot.Respond(
            "paciente",
            null,
            "/gestion-de-citas/st-pac-01-mis-citas",
            "¿Cómo cambio la cita para el viernes?");

        Assert.Contains("Mis citas", response.Message);
        Assert.Contains("Recepción", response.Message);
        Assert.DoesNotContain(response.Actions, action =>
            action.Url == "/gestion-de-citas/st-rec-03-gestion-citas");
    }

    [Fact]
    public void Respond_CurrentViewQuestion_ReturnsContextAndOnlyAuthorizedLinks()
    {
        var response = _chatbot.Respond(
            "Paciente",
            null,
            "/gestion-de-citas/st-pac-01-mis-citas",
            "¿Qué hace esta vista?");

        Assert.Contains("Mis citas", response.Message);
        Assert.Contains(response.Sources, source =>
            source.Url == "/gestion-de-citas/st-pac-01-mis-citas");
        Assert.DoesNotContain(response.Actions, action =>
            action.Url.Contains("st-adm-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Respond_SynonymQuestion_FindsAppointmentGuide()
    {
        var response = _chatbot.Respond(
            "Recepcionista",
            null,
            "/",
            "¿Cómo reservar un turno?");

        Assert.Contains("programar una cita", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Sources, source =>
            source.Url == "/gestion-de-citas/st-rec-03-gestion-citas");
    }

    [Fact]
    public void Respond_UnknownQuestion_DoesNotInventStepsOrLinks()
    {
        var response = _chatbot.Respond(
            "Auxiliar",
            null,
            "/gestion-de-citas/st-aux-02-agenda-apoyo",
            "¿Cómo exporto un documento a un formato no documentado?");

        Assert.Contains("No encuentro una guía verificada", response.Message);
        Assert.Empty(response.Sources);
        Assert.DoesNotContain(response.Actions, action =>
            action.Url.Contains("st-adm-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Respond_UnavailableContext_DoesNotTrustClientPath()
    {
        var response = _chatbot.Respond(
            "Paciente",
            null,
            "https://example.invalid/admin",
            "¿Qué hace esta vista?");

        Assert.Contains("No puedo identificar esa vista", response.Message);
        Assert.DoesNotContain(response.Actions, action =>
            action.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Respond_SupportQuestion_OffersTicketAndFullAttachmentForm()
    {
        var response = _chatbot.Respond(
            "Paciente",
            null,
            "/",
            "Necesito reportar un problema");

        Assert.Contains(response.Actions, action => action.Type == "ticket-form");
        Assert.DoesNotContain(response.Actions, action =>
            action.Url.StartsWith("/centro-de-ayuda/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Respond_TicketTrackingQuestion_OffersOwnTicketsAction()
    {
        var response = _chatbot.Respond(
            "Profesional",
            null,
            "/gestion-de-profesionales/st-odo-01-dashboard",
            "¿Cómo consulto mis tickets?");

        Assert.Contains(response.Actions, action => action.Type == "ticket-list");
        Assert.Contains("solo se mostrarán solicitudes creadas por ti", response.Message);
    }

    [Fact]
    public void Respond_UnknownRole_HasNoRoleRestrictedActions()
    {
        var response = _chatbot.Respond(
            "rol-no-configurado",
            null,
            "/reportes",
            "¿Cómo gestiono usuarios?");

        Assert.Equal("usuario", response.Role);
        Assert.DoesNotContain(response.Actions, action =>
            action.Url.Contains("st-adm-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Respond_AppointmentSpellingError_FindsGuide()
    {
        var response = _chatbot.Respond(
            "Recepcionista", null, "/", "Como agendo una sita");

        Assert.Contains("Pasos para programar una cita", response.Message);
    }

    [Fact]
    public void Respond_ProfessionalSpellingError_OnlyAdminGetsRegistrationLink()
    {
        var response = _chatbot.Respond(
            "Administrador", null, "/", "Como registro un profecional");

        Assert.Contains("Registrar o actualizar un profesional", response.Message);
        Assert.Contains(response.Actions, action =>
            action.Url == "/gestion-de-profesionales/st-adm-07-gestion-profesionales");
    }

    [Fact]
    public void Respond_FollowUpUsesPreviousTopic()
    {
        _chatbot.Respond(
            "Recepcionista", null, "/", "Como programo una cita", "recepcionista-1");

        var response = _chatbot.Respond(
            "Recepcionista", null, "/", "Y como la cancelo?", "recepcionista-1");

        Assert.Contains("Cancelar una cita", response.Message);
    }

    [Fact]
    public void ResetConversation_RemovesPreviousTopic()
    {
        _chatbot.Respond(
            "Recepcionista", null, "/", "Como programo una cita", "recepcionista-2");
        _chatbot.ResetConversation("recepcionista-2");

        var response = _chatbot.Respond(
            "Recepcionista", null, "/", "Y como hago eso?", "recepcionista-2");

        Assert.DoesNotContain("Cancelar una cita", response.Message);
    }

    [Theory]
    [InlineData("Administrador", "¿Cómo gestiono usuarios?", "/acceso-y-seguridad/st-adm-02-gestion-usuarios")]
    [InlineData("Recepcionista", "¿Cómo genero una factura?", "/facturacion-y-pagos/st-rec-04-generar-factura")]
    [InlineData("Profesional", "¿Cómo uso el odontograma?", "/historia-clinica/st-odo-04-odontograma")]
    [InlineData("Auxiliar", "¿Cómo preparo una consulta?", "/gestion-de-pacientes/st-aux-03-preparacion-consulta")]
    [InlineData("Paciente", "¿Cómo cambio mi contraseña?", "/acceso-y-seguridad/cambiar-contrasena")]
    public void Respond_ExpandedCatalog_ReturnsRoleAuthorizedLink(string role, string question, string expectedUrl)
    {
        var response = _chatbot.Respond(role, null, "/", question);

        Assert.Contains(response.Actions, action => action.Url == expectedUrl);
    }

    [Fact]
    public void Respond_PatientCannotReceiveAdministrativeBillingLink()
    {
        var response = _chatbot.Respond(
            "Paciente", null, "/", "¿Dónde veo la facturación?");

        Assert.DoesNotContain(response.Actions, action =>
            action.Url == "/facturacion-y-pagos/st-adm-12-facturacion");
    }

    [Fact]
    public void Respond_SaveFailureOffersValidatedSteps()
    {
        var response = _chatbot.Respond(
            "Recepcionista", null, "/gestion-de-citas/st-rec-03-gestion-citas",
            "No puedo guardar una cita porque no me deja");

        Assert.Contains("campos marcados como obligatorios", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Actions, action => action.Type == "ticket-form");
    }

    [Fact]
    public void Respond_NegatedTicketRequest_DoesNotOpenTicketForm()
    {
        var response = _chatbot.Respond("Paciente", null, "/", "No quiero crear un ticket");
        Assert.DoesNotContain(response.Actions, action => action.Type == "ticket-form");
    }

    [Fact]
    public void Respond_NegatedCancellation_DoesNotReturnCancellationSteps()
    {
        var response = _chatbot.Respond("Recepcionista", null, "/gestion-de-citas/st-rec-03-gestion-citas", "No quiero cancelar una cita");
        Assert.DoesNotContain("Cancelar una cita", response.Message);
    }

    [Fact]
    public void ConversationStates_AreIsolatedByUserAndSession()
    {
        _chatbot.Respond("Recepcionista", null, "/", "Como programo una cita", "recepcionista-session-a");
        var isolated = _chatbot.Respond("Recepcionista", null, "/", "Y como hago eso?", "recepcionista-session-b");
        Assert.DoesNotContain("Cancelar una cita", isolated.Message);
    }

    [Fact]
    public void CatalogIds_AreUniqueAndMetricsArePositive()
    {
        var entries = ChatbotKnowledgeCatalog.EntriesSnapshot
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase);

        Assert.All(entries, group => Assert.True(group.Count() == 1));
        Assert.True(ChatbotKnowledgeCatalog.EntryCount > 0);
        Assert.True(ChatbotKnowledgeCatalog.ModuleCount > 0);
        Assert.True(ChatbotKnowledgeCatalog.ViewCount > 0);
        Assert.True(ChatbotKnowledgeCatalog.ProcedureCount > 0);
        Assert.True(ChatbotKnowledgeCatalog.QuestionCount > 0);
    }

    [Fact]
    public void ConversationContext_ExpiresWhenRetentionIsZero()
    {
        var values = new Dictionary<string, string?>
        {
            ["Chatbot:ConversationRetentionMinutes"] = "0",
            ["Chatbot:MaxConversationStates"] = "5000"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var chatbot = new ChatbotService(configuration);

        chatbot.Respond("Recepcionista", null, "/", "Como programo una cita", "expired-session");
        var followUp = chatbot.Respond("Recepcionista", null, "/", "Y como hago eso?", "expired-session");

        Assert.DoesNotContain("Cancelar una cita", followUp.Message);
    }

    [Fact]
    public void Respond_LocalQueriesStayBelowTwoSecondTarget()
    {
        var durations = new List<double>();
        var questions = new[]
        {
            "¿Cómo programo una cita?",
            "¿Dónde veo mis citas?",
            "¿Cómo uso el odontograma?",
            "¿Cómo genero una factura?",
            "¿Cómo cambio mi contraseña?",
            "¿Dónde consulto mis tickets?",
            "no quiero crear ticket",
            "¿Qué hace esta vista?",
            "quiero cancelar mi cita",
            "asdfgh qwerty desconocido"
        };

        // Warmup: estabiliza JIT/cachés antes de medir.
        for (var i = 0; i < 30; i++)
            _chatbot.Respond("Recepcionista", null, "/", questions[i % questions.Length], $"warmup-{i % 10}");

        var stopwatch = new System.Diagnostics.Stopwatch();
        const int samples = 300;
        for (var i = 0; i < samples; i++)
        {
            stopwatch.Restart();
            _chatbot.Respond("Recepcionista", null, "/", questions[i % questions.Length], $"benchmark-{i % 20}");
            stopwatch.Stop();
            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var sorted = durations.OrderBy(value => value).ToArray();
        var min = sorted[0];
        var max = sorted[^1];
        var avg = sorted.Average();
        var median = sorted[sorted.Length / 2];
        var p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
        _output.WriteLine($"MCA_PERF samples={samples};warmup=30;min_ms={min:F3};avg_ms={avg:F3};median_ms={median:F3};p95_ms={p95:F3};max_ms={max:F3};errors=0");

        Assert.True(p95 < 2000, $"P95 interno fue {p95:F2} ms. min={min:F2} avg={avg:F2} median={median:F2} max={max:F2}");
    }

    [Fact]
    public void ConversationStates_ActiveSessionRetainedBelowLimit()
    {
        var values = new Dictionary<string, string?>
        {
            ["Chatbot:ConversationRetentionMinutes"] = "30",
            ["Chatbot:MaxConversationStates"] = "5000"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var chatbot = new ChatbotService(configuration);

        chatbot.Respond("Recepcionista", null, "/", "Como programo una cita", "active-session");
        var followUp = chatbot.Respond("Recepcionista", null, "/", "Y como la cancelo?", "active-session");

        Assert.Contains("Cancelar una cita", followUp.Message);
    }

    [Fact]
    public void ChatbotOptions_DefaultsMatchDocumentedLimits()
    {
        var options = new ChatbotOptions();

        Assert.Equal(500, options.MaxMessageLength);
        Assert.Equal(15, options.MessagesPerMinute);
        Assert.Equal(5, options.TicketRequestsPer15Minutes);
        Assert.Equal(120, options.GlobalRequestsPerMinute);
        Assert.Equal(30, options.ConversationRetentionMinutes);
        Assert.Equal(5000, options.MaxConversationStates);
    }

    [Fact]
    public void Respond_RoleRestrictedQuery_DoesNotReturnUnrelatedRoleContent()
    {
        var patient = _chatbot.Respond("Paciente", null, "/", "¿Cómo gestiono usuarios?");

        Assert.DoesNotContain("Gestión de Usuarios", patient.Message);
        Assert.DoesNotContain("Peticiones, quejas y reclamos", patient.Message);

        var professional = _chatbot.Respond("Profesional", null, "/", "¿Cómo elimino un usuario?");

        Assert.DoesNotContain("Buscar o consultar un paciente", professional.Message);
    }

    [Fact]
    public void ConversationContext_RetainedWithinRetentionWindow()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        var chatbot = new ChatbotService(TemporalConfiguration(30), clock);

        chatbot.Respond("Recepcionista", null, "/", "Como programo una cita", "clock-keep");
        clock.Advance(TimeSpan.FromMinutes(29));

        // Dentro de la ventana de 30 minutos el estado de la conversación sigue vigente.
        Assert.NotNull(InvokeGetConversationState(chatbot, "clock-keep"));
    }

    [Fact]
    public void ConversationContext_ExpiresAfterRetentionWindow()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
        var chatbot = new ChatbotService(TemporalConfiguration(30), clock);

        chatbot.Respond("Recepcionista", null, "/", "Como programo una cita", "clock-expire");
        clock.Advance(TimeSpan.FromMinutes(31));

        // Pasada la ventana de 30 minutos el estado se descarta y se elimina del almacén.
        Assert.Null(InvokeGetConversationState(chatbot, "clock-expire"));
        Assert.DoesNotContain("clock-expire", ConversationStateKeys().Cast<string>());
    }

    [Fact]
    public void ConversationContext_KeepsAtMostSixRecentMessages()
    {
        var chatbot = new ChatbotService();
        const string key = "six-messages-cap";

        for (var i = 0; i < 8; i++)
            chatbot.Respond("Recepcionista", null, "/", "¿Cómo programo una cita?", key);

        // La cola interna se limita a seis mensajes por conversación y no crece sin control.
        Assert.Equal(6, MessageCountFor(key));
    }

    private static IConfiguration TemporalConfiguration(int retentionMinutes) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Chatbot:ConversationRetentionMinutes"] = retentionMinutes.ToString(),
            ["Chatbot:MaxConversationStates"] = "5000"
        }).Build();

    private static int MessageCountFor(string conversationKey)
    {
        var field = typeof(ChatbotService).GetField(
            "ConversationStates", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        var states = (System.Collections.IDictionary)field!.GetValue(null)!;
        var state = states[conversationKey];
        Assert.NotNull(state);

        var messages = (System.Collections.ICollection)state!.GetType()
            .GetProperty("Messages")!.GetValue(state)!;
        return messages.Count;
    }

    private static object? InvokeGetConversationState(ChatbotService chatbot, string conversationKey)
    {
        var method = typeof(ChatbotService).GetMethod(
            "GetConversationState", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return method!.Invoke(chatbot, [conversationKey]);
    }

    private static System.Collections.ICollection ConversationStateKeys()
    {
        var field = typeof(ChatbotService).GetField(
            "ConversationStates", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var states = (System.Collections.IDictionary)field!.GetValue(null)!;
        return states.Keys;
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
