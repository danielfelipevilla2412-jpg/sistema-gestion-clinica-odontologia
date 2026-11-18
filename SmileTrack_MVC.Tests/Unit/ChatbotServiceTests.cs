using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class ChatbotServiceTests
{
    private readonly ChatbotService _chatbot = new();

    [Fact]
    public void Respond_OpenAppointmentQuestion_ReturnsGuidanceForReceptionist()
    {
        var response = _chatbot.Respond(
            "recepcionista",
            null,
            "/gestion-de-citas/st-rec-03-gestion-citas",
            "¿Me ayudan a cambiar mi cita para el viernes?");

        Assert.Contains("Gestión de Citas", response.Message);
        Assert.Contains("reprogramar", response.Message);
        Assert.Contains("disponibilidad", response.Message);
    }

    [Fact]
    public void Respond_OpenAppointmentQuestion_AdaptsGuidanceToPatient()
    {
        var response = _chatbot.Respond(
            "paciente",
            null,
            "/gestion-de-citas/st-pac-01-mis-citas",
            "¿Me ayudan a cambiar mi cita para el viernes?");

        Assert.Contains("Mis citas", response.Message);
        Assert.Contains("Recepción", response.Message);
        Assert.DoesNotContain("reprogramar", response.Message);
    }

    [Fact]
    public void Respond_UnknownQuestionAndRole_AsksForUsefulClarification()
    {
        var response = _chatbot.Respond(
            "rol-no-configurado",
            null,
            "/reportes",
            "¿Cómo exporto este documento?");

        Assert.Equal("usuario", response.Role);
        Assert.Contains("Reportes", response.Message);
        Assert.Contains("qué intentas hacer", response.Message);
    }
}