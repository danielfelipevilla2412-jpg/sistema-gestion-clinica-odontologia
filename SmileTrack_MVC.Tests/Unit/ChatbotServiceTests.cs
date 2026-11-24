using System;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ChatbotServiceTests
{
    private readonly ChatbotService _chatbot = new();

    [Fact]
    public void Respond_AppointmentQuestion_ReturnsVerifiedRoleSpecificSteps()
    {
        var response = _chatbot.Respond(
            "Recepcionista",
            null,
            "/centro-de-ayuda/guias-tutoriales",
            "¿Cómo programo una cita?");

        Assert.Contains("agenda correspondiente", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("profesional, servicio, consultorio y horario", response.Message);
        Assert.Contains(response.Actions, action =>
            action.Url == "/gestion-de-citas/st-rec-03-gestion-citas");
        Assert.Contains(response.Sources, source =>
            source.Url == "/centro-de-ayuda/como-programar-cita");
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
            source.Url == "/centro-de-ayuda/como-programar-cita");
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
            "/centro-de-ayuda/soporte",
            "Necesito reportar un problema");

        Assert.Contains(response.Actions, action => action.Type == "ticket-form");
        Assert.Contains(response.Actions, action => action.Url == "/centro-de-ayuda/soporte");
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
}
