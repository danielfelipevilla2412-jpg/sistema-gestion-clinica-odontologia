using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class HistoriaClinicaServiceTests
{
    private static HistoriaClinicaService CreateService(AppDbContext db)
    {
        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);
        return new HistoriaClinicaService(
            db,
            NullLogger<HistoriaClinicaService>.Instance,
            citaService);
    }

    [Fact]
    public async Task CrearHistoriaClinicaAsync_ReactivatesAndReusesInactiveHistory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var historia = new HistoriaClinica
        {
            IdPaciente = 42,
            FechaApertura = DateTime.UtcNow.AddYears(-1),
            Activa = false
        };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CrearHistoriaClinicaAsync(historia.IdPaciente);

        Assert.Equal(historia.IdHistoria, result.IdHistoria);
        Assert.True(result.Activa);
        Assert.Equal(1, await db.HistoriasClinicas.CountAsync(h => h.IdPaciente == historia.IdPaciente));
    }

    [Fact]
    public async Task GuardarOdontogramaInternoAsync_PersistsFdiMappingAndClinicalRecord()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var paciente = new Paciente
        {
            Nombres = "Paciente",
            Apellidos = "Prueba",
            Documento = "123456",
            FechaNacimiento = new DateTime(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GuardarOdontogramaInternoAsync(
            paciente.IdPaciente,
            new OdontogramaGuardarRequest
            {
                PacienteId = paciente.IdPaciente,
                MapeoFDI = new Dictionary<string, string> { ["node-16"] = "16" },
                Registros = new Dictionary<string, OdontogramaRegistroPayload>
                {
                    ["node-16"] = new()
                    {
                        NombrePieza = "Primer molar superior derecho",
                        Tratamientos = [new OdontogramaTratamientoPayload
                        {
                            Key = "caries",
                            Obs = "Lesión oclusal"
                        }]
                    }
                }
            },
            idProfesional: null);

        Assert.True(result.Success);
        var registro = await db.RegistrosOdontograma.SingleAsync();
        Assert.Equal("16", registro.NumeroFdi);
        Assert.Equal("caries", registro.Estado);
        Assert.Equal("Lesión oclusal", registro.Observacion);
        var historia = await db.HistoriasClinicas.SingleAsync();
        using var estado = JsonDocument.Parse(historia.ObservacionesGenerales!);
        Assert.Equal(
            "16",
            estado.RootElement.GetProperty("mapeoFDI").GetProperty("node-16").GetString());
    }

    [Fact]
    public async Task GuardarOdontogramaInternoAsync_RemappingMovesCurrentStateAndRetainsOldHistory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var paciente = new Paciente
        {
            Nombres = "Paciente",
            Apellidos = "Prueba",
            Documento = "654321",
            FechaNacimiento = new DateTime(1990, 1, 1)
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();

        var historia = new HistoriaClinica
        {
            IdPaciente = paciente.IdPaciente,
            FechaApertura = DateTime.UtcNow.AddYears(-1),
            Activa = true,
            ObservacionesGenerales = JsonSerializer.Serialize(new
            {
                mapeoFDI = new Dictionary<string, string> { ["node-a"] = "11" }
            })
        };
        db.HistoriasClinicas.Add(historia);
        await db.SaveChangesAsync();

        var fechaAnterior = DateTime.UtcNow.AddDays(-1);
        db.RegistrosOdontograma.Add(new RegistroOdontograma
        {
            IdHistoria = historia.IdHistoria,
            NumeroFdi = "11",
            Estado = "caries",
            Observacion = "Lesión",
            FechaRegistro = fechaAnterior
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GuardarOdontogramaInternoAsync(
            paciente.IdPaciente,
            new OdontogramaGuardarRequest
            {
                PacienteId = paciente.IdPaciente,
                MapeoFDI = new Dictionary<string, string> { ["node-a"] = "12" },
                Registros = new Dictionary<string, OdontogramaRegistroPayload>
                {
                    ["node-a"] = new()
                    {
                        NombrePieza = "Incisivo lateral",
                        Tratamientos = [new OdontogramaTratamientoPayload
                        {
                            Key = "caries",
                            Obs = "Lesión"
                        }]
                    }
                }
            },
            idProfesional: null);

        Assert.True(result.Success);
        var registros = await db.RegistrosOdontograma
            .OrderBy(registro => registro.FechaRegistro)
            .ToListAsync();
        Assert.Collection(registros,
            registro =>
            {
                Assert.Equal("11", registro.NumeroFdi);
                Assert.Equal("caries", registro.Estado);
            },
            registro =>
            {
                Assert.Equal("12", registro.NumeroFdi);
                Assert.Equal("caries", registro.Estado);
            },
            registro =>
            {
                Assert.Equal("11", registro.NumeroFdi);
                Assert.Equal("sin_registro", registro.Estado);
            });

        using var estado = JsonDocument.Parse((await db.HistoriasClinicas.SingleAsync()).ObservacionesGenerales!);
        Assert.Equal("12", estado.RootElement.GetProperty("mapeoFDI").GetProperty("node-a").GetString());
        var historialMapeos = estado.RootElement.GetProperty("mapeoFDIHistorial");
        Assert.Equal(2, historialMapeos.GetArrayLength());
        Assert.Equal("11", historialMapeos[0].GetProperty("mapeoFDI").GetProperty("node-a").GetString());
        Assert.Equal("12", historialMapeos[1].GetProperty("mapeoFDI").GetProperty("node-a").GetString());

        var remapeoPosterior = await service.GuardarOdontogramaInternoAsync(
            paciente.IdPaciente,
            new OdontogramaGuardarRequest
            {
                PacienteId = paciente.IdPaciente,
                MapeoFDI = new Dictionary<string, string> { ["node-a"] = "13" },
                Registros = new Dictionary<string, OdontogramaRegistroPayload>
                {
                    ["node-a"] = new()
                    {
                        NombrePieza = "Canino",
                        Tratamientos = [new OdontogramaTratamientoPayload { Key = "caries" }]
                    }
                }
            },
            idProfesional: null);

        Assert.True(remapeoPosterior.Success);
        using var estadoPosterior = JsonDocument.Parse((await db.HistoriasClinicas.SingleAsync()).ObservacionesGenerales!);
        var historialPosterior = estadoPosterior.RootElement.GetProperty("mapeoFDIHistorial");
        Assert.Equal(3, historialPosterior.GetArrayLength());
        Assert.Equal("13", historialPosterior[2].GetProperty("mapeoFDI").GetProperty("node-a").GetString());
    }

    [Fact]
    public async Task ProfesionalPuedeAccederPacienteAsync_AllowsAssignedPatientAndPatientWithAppointment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var profesional = new Profesional { IdUsuario = 7, Nombres = "Ana", Apellidos = "Odontóloga" };
        var pacienteAsignado = new Paciente { Nombres = "Luis", Apellidos = "Asignado", IdProfesionalAsignado = 1 };
        var pacienteConCita = new Paciente { Nombres = "Sara", Apellidos = "Cita" };
        var pacienteSinRelacion = new Paciente { Nombres = "No", Apellidos = "Asignado" };

        db.Profesionales.Add(profesional);
        db.Pacientes.AddRange(pacienteAsignado, pacienteConCita, pacienteSinRelacion);
        await db.SaveChangesAsync();
        db.Citas.Add(new Cita
        {
            IdPaciente = pacienteConCita.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            FechaHora = DateTime.UtcNow.AddDays(10),
            Estado = "Programada"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.True(await service.ProfesionalPuedeAccederPacienteAsync(pacienteAsignado.IdPaciente, 7));
        Assert.True(await service.ProfesionalPuedeAccederPacienteAsync(pacienteConCita.IdPaciente, 7));
        Assert.False(await service.ProfesionalPuedeAccederPacienteAsync(pacienteSinRelacion.IdPaciente, 7));
    }
}
