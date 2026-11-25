using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.CentroDeAyuda;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ChatbotApiControllerTests
{
    private static ChatbotApiController CrearControlador(
        ICentroDeAyudaService centroDeAyudaService,
        string? rol = "Paciente",
        string? userId = "12")
    {
        var controller = new ChatbotApiController(new ChatbotService(), centroDeAyudaService);

        var claims = new List<Claim>();
        if (rol is not null)
            claims.Add(new Claim(ClaimTypes.Role, rol));
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

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
        var controller = CrearControlador(new FakeCentroDeAyudaService());

        var result = controller.Message(new ChatbotRequest { Message = "   " });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Message_MensajeMayorA500Caracteres_RetornaBadRequest()
    {
        var controller = CrearControlador(new FakeCentroDeAyudaService());

        var result = controller.Message(new ChatbotRequest { Message = new string('a', 501) });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Message_MensajeDe500Caracteres_RetornaOk()
    {
        var controller = CrearControlador(new FakeCentroDeAyudaService());

        var result = controller.Message(new ChatbotRequest { Message = new string('a', 500) });

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void Message_MensajeValido_RetornaOkConRespuesta()
    {
        var controller = CrearControlador(new FakeCentroDeAyudaService(), "Paciente");

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

    [Fact]
    public async Task CreateTicket_UsesAuthenticatedUserAndTicketData()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", "27");

        var result = await controller.CreateTicket(new ChatbotTicketRequest
        {
            Subject = "No puedo guardar una cita",
            Category = "incidente",
            Module = "citas",
            Severity = "media",
            Description = "Al guardar la cita aparece un mensaje de error.",
            Confirmed = true,
            IdempotencyKey = "ticket-valid-key-1"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(27, service.CreatedByUserId);
        Assert.NotNull(service.CreatedRequest);
        Assert.Equal("incidente", service.CreatedRequest!.Categoria);
        Assert.Equal("citas", service.CreatedRequest.ModuloAfectado);
    }

    [Fact]
    public async Task CreateTicket_WithoutConfirmation_ReturnsBadRequest()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", "27");

        var result = await controller.CreateTicket(new ChatbotTicketRequest
        {
            Subject = "No puedo guardar una cita",
            Category = "incidente",
            Module = "citas",
            Severity = "media",
            Description = "Al guardar la cita aparece un mensaje de error.",
            Confirmed = false
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(service.CreatedRequest);
    }

    [Fact]
    public async Task CreateTicket_SameIdempotencyKey_ReturnsConflict()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", "27");
        var request = new ChatbotTicketRequest
        {
            Subject = "No puedo guardar una cita",
            Category = "incidente",
            Module = "citas",
            Severity = "media",
            Description = "Al guardar la cita aparece un mensaje de error.",
            Confirmed = true,
            IdempotencyKey = "ticket-duplicate-key-1"
        };

        var first = await controller.CreateTicket(request, CancellationToken.None);
        var second = await controller.CreateTicket(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(first);
        Assert.IsType<ConflictObjectResult>(second);
        Assert.Equal(1, service.CreatedRequest is null ? 0 : 1);
    }

    [Fact]
    public async Task CreateTicket_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", null);

        var result = await controller.CreateTicket(new ChatbotTicketRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(service.CreatedRequest);
    }

    [Fact]
    public async Task GetMyTickets_UsesAuthenticatedUserId()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", "31");

        var result = await controller.GetMyTickets(ct: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(31, service.TicketsQueriedForUserId);
    }

    [Fact]
    public async Task GetMyTicket_UnownedReference_ReturnsNotFound()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", "31");

        var result = await controller.GetMyTicket("ST-OTHER-123", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(31, service.TicketQueriedForUserId);
    }

    [Fact]
    public async Task GetMyTickets_WithoutUserId_ReturnsUnauthorized()
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service, "Paciente", null);

        var result = await controller.GetMyTickets(ct: CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(service.TicketsQueriedForUserId);
    }

    [Fact]
    public void Message_UsesServerRoleAndRejectsRoleRestrictedContext()
    {
        var controller = CrearControlador(new FakeCentroDeAyudaService(), "Paciente", "31");

        var result = controller.Message(new ChatbotRequest
        {
            Message = "¿Qué hace esta vista?",
            CurrentPath = "/acceso-y-seguridad/st-adm-02-gestion-usuarios"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChatbotResponse>(ok.Value);
        Assert.Equal("paciente", response.Role);
        Assert.DoesNotContain(response.Actions, action =>
            action.Url == "/acceso-y-seguridad/st-adm-02-gestion-usuarios");
    }

    private sealed class FakeCentroDeAyudaService : ICentroDeAyudaService
    {
        public CentroAyudaTicketRequest? CreatedRequest { get; private set; }
        public int? CreatedByUserId { get; private set; }
        public int? TicketsQueriedForUserId { get; private set; }
        public int? TicketQueriedForUserId { get; private set; }

        public Task<(IReadOnlyList<CentroAyudaTicketDto> Items, int TotalCount)> ObtenerTicketsAsync(
            int page, int pageSize, string? search, string? estado, string? categoria, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<CentroAyudaTicketDto?> ObtenerTicketAsync(int id, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<(IReadOnlyList<ChatbotTicketSummary> Items, int TotalCount)> ObtenerTicketsUsuarioAsync(
            int usuarioId, int page, int pageSize, CancellationToken ct)
        {
            TicketsQueriedForUserId = usuarioId;
            return Task.FromResult<(IReadOnlyList<ChatbotTicketSummary>, int)>(([], 0));
        }

        public Task<ChatbotTicketSummary?> ObtenerTicketUsuarioAsync(
            string referencia, int usuarioId, CancellationToken ct)
        {
            TicketQueriedForUserId = usuarioId;
            return Task.FromResult<ChatbotTicketSummary?>(null);
        }

        public Task<(CentroAyudaTicketDto? Data, string? Error)> CrearTicketAsync(
            CentroAyudaTicketRequest request, int usuarioId, CancellationToken ct)
        {
            CreatedRequest = request;
            CreatedByUserId = usuarioId;
            return Task.FromResult<(CentroAyudaTicketDto? Data, string? Error)>(
                (new CentroAyudaTicketDto { IdTicket = 1, Referencia = "ST-TEST" }, null));
        }

        public Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> ActualizarTicketAsync(
            int id, CentroAyudaTicketUpdateRequest request, int usuarioId, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<(CentroAyudaTicketDto? Data, string? Error, int StatusCode)> CambiarEstadoAsync(
            int id, string estado, int usuarioId, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<(bool Success, string? Error, int StatusCode)> EliminarTicketAsync(int id, CancellationToken ct)
            => throw new System.NotSupportedException();
    }
}
