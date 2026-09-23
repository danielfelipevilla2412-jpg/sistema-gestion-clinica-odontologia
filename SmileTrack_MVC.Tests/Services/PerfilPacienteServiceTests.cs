using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Perfiles;
using Xunit;

namespace SmileTrack_MVC.Tests.Services;

public sealed class PerfilPacienteServiceTests
{
    [Fact]
    public async Task ObtenerInfoBasica_OcultaDocumento_YCalculaEdad()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Documento = "123456789",
            Nombres = "Juan",
            Apellidos = "Pérez",
            FechaNacimiento = DateTime.Today.AddYears(-30),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await new PerfilPacienteService(context).ObtenerInfoBasicaAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Juan Pérez", result.NombreCompleto);
        Assert.Equal("***6789", result.DocumentoOculto);
        Assert.Equal(30, result.Edad);
    }

    [Fact]
    public async Task ObtenerInfoBasica_NoDevuelveOtroPaciente()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 2,
            Documento = "9999",
            Nombres = "Ana",
            Apellidos = "López",
            FechaNacimiento = DateTime.Today.AddYears(-20),
            Estado = "activo"
        });
        await context.SaveChangesAsync();

        var result = await new PerfilPacienteService(context).ObtenerInfoBasicaAsync(1);

        Assert.Null(result);
    }

    [Fact]
    public async Task ObtenerEstadisticas_CuentaEstadosYProximaCita()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente { IdPaciente = 1, Documento = "1", Nombres = "Juan", Apellidos = "Pérez", FechaNacimiento = DateTime.Today.AddYears(-30), Estado = "activo", FechaRegistro = DateTime.Today.AddYears(-2) });
        context.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, Estado = "Agendada", FechaHora = DateTime.Now.AddDays(2) },
            new Cita { IdCita = 2, IdPaciente = 1, Estado = "Completada", FechaHora = DateTime.Now.AddDays(-2) },
            new Cita { IdCita = 3, IdPaciente = 1, Estado = "Cancelada", FechaHora = DateTime.Now.AddDays(-3) });
        await context.SaveChangesAsync();

        var result = await new PerfilPacienteService(context).ObtenerEstadisticasAsync(1);

        Assert.Equal(3, result.TotalCitas);
        Assert.Equal(1, result.CitasPendientes);
        Assert.Equal(1, result.CitasCompletadas);
        Assert.Equal(1, result.CitasCanceladas);
        Assert.Equal(1, result.ProximaCita?.IdCita);
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}