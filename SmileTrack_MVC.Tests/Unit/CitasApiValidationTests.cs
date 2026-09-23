using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Services;
using SmileTrack_MVC.Services.Email;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class CitasApiValidationTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Actualizar_RejectsModelStateErrorsBeforeBusinessValidation()
    {
        var db = CreateInMemoryDb($"citas_api_validation_{Guid.NewGuid()}");
        var service = new CitaService(db, NullLogger<CitaService>.Instance);
        var controller = new CitasApiController(
            db,
            NullLogger<CitasApiController>.Instance,
            new NoOpEmailService(),
            service);

        controller.ModelState.AddModelError("IdPaciente", "El paciente es obligatorio.");

        var dto = new CitaApiUpdateDto
        {
            IdCita = 1,
            IdPaciente = 5,
            FechaHora = DateTime.Today.AddDays(1).AddHours(9)
        };

        var result = await controller.Actualizar(1, dto);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);

        var message = badRequest.Value.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Equal("Datos de cita inválidos.", message);
    }

    [Fact]
    public void GestionCitas_NormalizaEstadosFinalizadosComoAtendidos()
    {
        var method = typeof(GestionCitasController).GetMethod("NormalizarEstado", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal("atendida", method.Invoke(null, ["Finalizada"]));
        Assert.Equal("atendida", method.Invoke(null, ["finalizada"]));
        Assert.Equal("atendida", method.Invoke(null, ["realizada"]));
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendRecoveryCodeAsync(string recipientEmail, string code, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendCitaNotificacionAsync(string recipientEmail, string nombrePaciente, DateTime fechaCita, string profesional, string servicio, string nuevoEstado, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(bool Exito, string Detalle)> ProbarConfiguracionSmtpAsync(string correoDestino, CancellationToken cancellationToken = default) => Task.FromResult((true, "OK"));
    }
}
