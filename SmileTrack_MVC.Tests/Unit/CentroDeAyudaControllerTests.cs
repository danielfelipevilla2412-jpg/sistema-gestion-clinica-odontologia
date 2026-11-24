using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services.CentroDeAyuda;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class CentroDeAyudaControllerTests
{
    [Theory]
    [InlineData(TicketCategory.ErrorTecnico, "incidente")]
    [InlineData(TicketCategory.ProblemaAcceso, "incidente")]
    [InlineData(TicketCategory.SolicitudCambio, "solicitud")]
    [InlineData(TicketCategory.DudaConsulta, "consulta")]
    [InlineData(TicketCategory.ProblemaReporteDato, "incidente")]
    public async Task CreateTicket_MapsViewCategoryToTicketService(
        TicketCategory category,
        string expectedCategory)
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service);

        await controller.CreateTicket(CrearTicket(category, AffectedModule.GestionCitas), CancellationToken.None);

        Assert.Equal(expectedCategory, service.CreatedRequest!.Categoria);
    }

    [Theory]
    [InlineData(AffectedModule.Inicio, "sistema")]
    [InlineData(AffectedModule.ReportesAnalitica, "reportes")]
    [InlineData(AffectedModule.GestionPacientes, "pacientes")]
    [InlineData(AffectedModule.HistoriaClinica, "sistema")]
    [InlineData(AffectedModule.GestionProfesionales, "sistema")]
    [InlineData(AffectedModule.GestionCitas, "citas")]
    [InlineData(AffectedModule.FacturacionPagos, "facturacion")]
    [InlineData(AffectedModule.ServiciosRecursos, "sistema")]
    [InlineData(AffectedModule.GestionPQR, "sistema")]
    [InlineData(AffectedModule.OtroNoEstoySeguro, "sistema")]
    public async Task CreateTicket_MapsViewModuleToTicketService(
        AffectedModule module,
        string expectedModule)
    {
        var service = new FakeCentroDeAyudaService();
        var controller = CrearControlador(service);

        await controller.CreateTicket(CrearTicket(TicketCategory.ErrorTecnico, module), CancellationToken.None);

        Assert.Equal(expectedModule, service.CreatedRequest!.ModuloAfectado);
    }

    private static SupportTicketViewModel CrearTicket(TicketCategory category, AffectedModule module) => new()
    {
        Subject = "No puedo guardar una cita",
        Category = category,
        Module = module,
        Severity = "Media",
        Description = "Al guardar los datos aparece un mensaje de error."
    };

    private static CentroDeAyudaController CrearControlador(FakeCentroDeAyudaService service)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "24")],
                "TestAuth"))
        };
        var controller = new CentroDeAyudaController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new FakeTempDataProvider())
        };
        return controller;
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class FakeCentroDeAyudaService : ICentroDeAyudaService
    {
        public CentroAyudaTicketRequest? CreatedRequest { get; private set; }

        public Task<(IReadOnlyList<CentroAyudaTicketDto> Items, int TotalCount)> ObtenerTicketsAsync(
            int page, int pageSize, string? search, string? estado, string? categoria, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<CentroAyudaTicketDto?> ObtenerTicketAsync(int id, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<(IReadOnlyList<ChatbotTicketSummary> Items, int TotalCount)> ObtenerTicketsUsuarioAsync(
            int usuarioId, int page, int pageSize, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<ChatbotTicketSummary?> ObtenerTicketUsuarioAsync(
            string referencia, int usuarioId, CancellationToken ct)
            => throw new System.NotSupportedException();

        public Task<(CentroAyudaTicketDto? Data, string? Error)> CrearTicketAsync(
            CentroAyudaTicketRequest request, int usuarioId, CancellationToken ct)
        {
            CreatedRequest = request;
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
