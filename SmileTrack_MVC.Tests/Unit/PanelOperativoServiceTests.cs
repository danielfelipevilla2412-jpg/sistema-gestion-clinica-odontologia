using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class PanelOperativoServiceTests
{
    [Fact]
    public async Task ObtenerAsync_ResumeKpisDelDiaYConsultoriosDisponibles()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Nombres = "Juan",
            Apellidos = "Pérez",
            FechaNacimiento = DateTime.Today.AddYears(-30),
            Estado = "activo"
        });
        context.Consultorios.AddRange(
            new Consultorio { IdConsultorio = 1, Nombre = "Box 1", Estado = "disponible" },
            new Consultorio { IdConsultorio = 2, Nombre = "Box 2", Estado = "activo" },
            new Consultorio { IdConsultorio = 3, Nombre = "Box 3", Estado = "mantenimiento" });
        context.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, FechaHora = DateTime.Today.AddHours(8), Estado = "atendida" },
            new Cita { IdCita = 2, IdPaciente = 1, FechaHora = DateTime.Today.AddHours(9), Estado = "cancelada" },
            new Cita { IdCita = 3, IdPaciente = 1, FechaHora = DateTime.Today.AddHours(10), Estado = "programada" });
        await context.SaveChangesAsync();

        var result = await new PanelOperativoService(context).ObtenerAsync();

        Assert.Equal(3, result.Kpis.CitasHoy);
        Assert.Equal(1, result.Kpis.Completadas);
        Assert.Equal(1, result.Kpis.Pendientes);
        Assert.Equal(2, result.Kpis.ConsultoriosDisponibles);
        Assert.Equal(3, result.Citas.Count);
    }

    [Fact]
    public async Task ObtenerAsync_GeneraAlertaYHighlightCuandoElPacienteTieneAlergia()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Nombres = "Ana",
            Apellidos = "López",
            Alergias = "Penicilina",
            FechaNacimiento = DateTime.Today.AddYears(-25),
            Estado = "activo"
        });
        context.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            FechaHora = DateTime.Today.AddHours(8),
            Estado = "programada"
        });
        await context.SaveChangesAsync();

        var result = await new PanelOperativoService(context).ObtenerAsync();

        var cita = Assert.Single(result.Citas);
        Assert.True(cita.Highlight);
        Assert.Equal("Penicilina", cita.Alergia);
        var alerta = Assert.Single(result.Alertas);
        Assert.Equal("Paciente con alergia", alerta.Titulo);
        Assert.Contains("Penicilina", alerta.Descripcion);
    }

    [Fact]
    public async Task ObtenerAsync_SeleccionaLaProximaCitaPendienteDelDia()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Nombres = "María",
            Apellidos = "Gómez",
            FechaNacimiento = DateTime.Today.AddYears(-40),
            Estado = "activo"
        });
        context.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            Nombres = "Luis",
            Apellidos = "Ortiz",
            RegistroMedico = "RM-1",
            Categoria = "Ortodoncia",
            Estado = "activo"
        });
        context.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            IdProfesional = 1,
            FechaHora = DateTime.Today.AddHours(23).AddMinutes(30),
            Estado = "programada"
        });
        await context.SaveChangesAsync();

        var result = await new PanelOperativoService(context).ObtenerAsync();

        Assert.NotNull(result.ProximaCita);
        Assert.Equal("María Gómez", result.ProximaCita!.Paciente);
        Assert.Equal("Dr(a). Luis Ortiz", result.ProximaCita.Profesional);
        Assert.True(result.ProximaCita.MinutosRestantes >= 0);
    }

    [Fact]
    public async Task ObtenerAsync_OrdenaTopProfesionalesDelMesYLimitaATres()
    {
        await using var context = CrearContext();
        context.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            Nombres = "P",
            Apellidos = "Uno",
            FechaNacimiento = DateTime.Today.AddYears(-30),
            Estado = "activo"
        });
        context.Profesionales.AddRange(
            new Profesional { IdProfesional = 1, Nombres = "A", Apellidos = "Uno", RegistroMedico = "RM-1", Categoria = "Ortodoncia", Estado = "activo" },
            new Profesional { IdProfesional = 2, Nombres = "B", Apellidos = "Dos", RegistroMedico = "RM-2", Estado = "activo" },
            new Profesional { IdProfesional = 3, Nombres = "C", Apellidos = "Tres", RegistroMedico = "RM-3", Estado = "activo" },
            new Profesional { IdProfesional = 4, Nombres = "D", Apellidos = "Cuatro", RegistroMedico = "RM-4", Estado = "activo" });
        context.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, IdProfesional = 1, FechaHora = DateTime.Today.AddHours(8), Estado = "programada" },
            new Cita { IdCita = 2, IdPaciente = 1, IdProfesional = 1, FechaHora = DateTime.Today.AddHours(9), Estado = "programada" },
            new Cita { IdCita = 3, IdPaciente = 1, IdProfesional = 1, FechaHora = DateTime.Today.AddHours(10), Estado = "programada" },
            new Cita { IdCita = 4, IdPaciente = 1, IdProfesional = 2, FechaHora = DateTime.Today.AddHours(11), Estado = "programada" },
            new Cita { IdCita = 5, IdPaciente = 1, IdProfesional = 2, FechaHora = DateTime.Today.AddHours(12), Estado = "programada" },
            new Cita { IdCita = 6, IdPaciente = 1, IdProfesional = 3, FechaHora = DateTime.Today.AddHours(13), Estado = "programada" });
        await context.SaveChangesAsync();

        var result = await new PanelOperativoService(context).ObtenerAsync();

        Assert.Equal(3, result.TopProfesionales.Count);
        Assert.Equal("A Uno", result.TopProfesionales[0].Nombre);
        Assert.Equal("Ortodoncia", result.TopProfesionales[0].Especialidad);
        Assert.Equal(3, result.TopProfesionales[0].TotalCitas);
        Assert.Equal("B Dos", result.TopProfesionales[1].Nombre);
        Assert.Equal("Sin especialidad", result.TopProfesionales[1].Especialidad);
    }

    [Fact]
    public async Task ObtenerAsync_SinCitasNiConsultorios_RetornaKpisEnCero()
    {
        await using var context = CrearContext();

        var result = await new PanelOperativoService(context).ObtenerAsync();

        Assert.Equal(0, result.Kpis.CitasHoy);
        Assert.Equal(0, result.Kpis.Completadas);
        Assert.Equal(0, result.Kpis.Pendientes);
        Assert.Equal(0, result.Kpis.ConsultoriosDisponibles);
        Assert.Null(result.ProximaCita);
        Assert.Empty(result.Citas);
        Assert.Empty(result.Alertas);
        Assert.Empty(result.TopProfesionales);
    }

    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
