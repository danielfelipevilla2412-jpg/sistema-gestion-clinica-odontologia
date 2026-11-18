using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class PacienteServiceTests
{
    [Fact]
    public async Task CambiarEstadoAsync_BlocksDeactivationWhileFutureAppointmentsAreActive()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_status_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options);
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "12345678",
            Nombres = "Ana",
            Apellidos = "Pérez",
            FechaNacimiento = new DateTime(2000, 1, 1),
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();
        db.Citas.Add(new Cita
        {
            IdPaciente = patient.IdPaciente,
            FechaHora = DateTime.Now.AddDays(1),
            Estado = "Confirmada"
        });
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var result = await service.CambiarEstadoAsync(patient.IdPaciente, "inactivo");

        Assert.False(result.Success);
        Assert.Equal(409, result.ErrorStatusCode);
        Assert.Equal("activo", patient.Estado);
    }

    [Fact]
    public async Task CambiarEstadoAsync_AllowsDeactivationAfterFutureAppointmentsAreCancelled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_status_cancelled_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options);
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "87654321",
            Nombres = "Luis",
            Apellidos = "Díaz",
            FechaNacimiento = new DateTime(1995, 1, 1),
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();
        db.Citas.Add(new Cita
        {
            IdPaciente = patient.IdPaciente,
            FechaHora = DateTime.Now.AddDays(1),
            Estado = "Cancelada"
        });
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var result = await service.CambiarEstadoAsync(patient.IdPaciente, "inactivo");

        Assert.True(result.Success);
        Assert.Equal("inactivo", patient.Estado);
    }
}
