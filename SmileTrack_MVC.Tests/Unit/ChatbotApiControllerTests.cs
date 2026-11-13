using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ChatbotApiControllerTests
{
    private static ChatbotApiController CrearControlador(string? rol = "Paciente")
    {
        var controller = new ChatbotApiController(new ChatbotService());

        var claims = new List<Claim>();
        if (rol is not null)
            claims.Add(new Claim(ClaimTypes.Role, rol));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    [Fact]
    public void Message_MensajeVacio_RetornaBadRequest()
    {
        var controller = CrearControlador();

        var result = controller.Message(new ChatbotRequest { Message = "   " });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Message_MensajeMayorA500Caracteres_RetornaBadRequest()
    {
        var controller = CrearControlador();

        var result = controller.Message(new ChatbotRequest { Message = new string('a', 501) });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Message_MensajeValido_RetornaOkConRespuesta()
    {
        var controller = CrearControlador("Paciente");

        var result = controller.Message(new ChatbotRequest
        {
            Message = "¿Cómo consulto mis citas?",
            CurrentPath = "/gestion-de-citas/st-pac-01-mis-citas"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var respuesta = Assert.IsType<ChatbotResponse>(ok.Value);
        Assert.Equal("paciente", respuesta.Role);
        Assert.False(string.IsNullOrWhiteSpace(respuesta.Message));
    }
}
