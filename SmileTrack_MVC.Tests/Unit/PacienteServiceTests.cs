using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
using SmileTrack_MVC.Models.Api.Pacientes;
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
            FechaHora = ColombiaClock.Now.AddDays(1),
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
            FechaHora = ColombiaClock.Now.AddDays(1),
            Estado = "Cancelada"
        });
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var result = await service.CambiarEstadoAsync(patient.IdPaciente, "inactivo");

        Assert.True(result.Success);
        Assert.Equal("inactivo", patient.Estado);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RejectsStateOutsideDtoCatalog()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_status_invalid_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options);
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "13572468",
            Nombres = "Marta",
            Apellidos = "López",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Estado = "activo"
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var result = await service.CambiarEstadoAsync(patient.IdPaciente, "archivado");

        Assert.False(result.Success);
        Assert.Equal(422, result.ErrorStatusCode);
        Assert.Equal("activo", patient.Estado);
    }

    [Fact]
    public async Task ActualizarAsync_PersistsAndReturnsMedications()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_medications_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options);
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "24681357",
            Nombres = "Camilo",
            Apellidos = "Ruiz",
            FechaNacimiento = new DateTime(1990, 1, 1)
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var result = await service.ActualizarAsync(patient.IdPaciente, new PacienteApiUpdateDto
        {
            Medicamentos = "Ibuprofeno, Paracetamol"
        });

        Assert.True(result.Success);
        var persistedPatient = await db.Pacientes.AsNoTracking().SingleAsync();
        Assert.Equal("Ibuprofeno, Paracetamol", persistedPatient.Medicamentos);
        Assert.Equal(new[] { "Ibuprofeno", "Paracetamol" }, result.Data!.Medicamentos);
    }

    [Fact]
    public async Task CrearAsync_PersistsAndReturnsMedications()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_create_medications_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AppDbContext(options);
        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);

        var result = await service.CrearAsync(new PacienteApiCreateDto
        {
            TipoDocumento = "CC",
            Documento = "99887766",
            Nombres = "Julia",
            Apellidos = "Mora",
            FechaNacimiento = new DateTime(1990, 1, 1),
            Medicamentos = "Ibuprofeno, Paracetamol"
        });

        Assert.True(result.Success);
        var persistedPatient = await db.Pacientes.AsNoTracking().SingleAsync();
        Assert.Equal("Ibuprofeno, Paracetamol", persistedPatient.Medicamentos);
        Assert.Equal(new[] { "Ibuprofeno", "Paracetamol" }, result.Data!.Medicamentos);
    }

    [Fact]
    public async Task ObtenerAsyncAndObtenerPorIdAsync_UseTheSameColombiaClock()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient_clock_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options);
        var patient = new Paciente
        {
            TipoDocumento = "CC",
            Documento = "11223344",
            Nombres = "Elena",
            Apellidos = "Paz",
            FechaNacimiento = new DateTime(1990, 1, 1)
        };
        db.Pacientes.Add(patient);
        await db.SaveChangesAsync();
        var ahora = ColombiaClock.Now;
        db.Citas.AddRange(
            new Cita { IdPaciente = patient.IdPaciente, FechaHora = ahora.AddHours(-1), Estado = "Atendida" },
            new Cita { IdPaciente = patient.IdPaciente, FechaHora = ahora.AddHours(1), Estado = "Confirmada" });
        await db.SaveChangesAsync();

        var service = new PacienteService(db, NullLogger<PacienteService>.Instance);
        var listado = await service.ObtenerAsync(null, "todos", 1, 10);
        var porId = await service.ObtenerPorIdAsync(patient.IdPaciente);

        Assert.NotNull(porId);
        var desdeListado = Assert.Single(listado.Items);
        Assert.Equal(desdeListado.UltimaConsulta, porId.UltimaConsulta);
        Assert.Equal(desdeListado.ProximaConsulta, porId.ProximaConsulta);
    }
}
