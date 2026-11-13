using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class CitasDashboardServiceTests
{
    [Fact]
    public async Task ObtenerDashboardAsync_UsaEstadoCatalogoYExcluyeHorariosDeProfesionalesInactivos()
    {
        await using var db = CrearDb();
        var fecha = new DateTime(2026, 10, 6);
        var paciente = new Paciente
        {
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "ACTIVO"
        };
        var profesionalActivo = new Profesional
        {
            Nombres = "Luis",
            Apellidos = "Dental",
            Estado = "activo"
        };
        var profesionalInactivo = new Profesional
        {
            Nombres = "Elena",
            Apellidos = "Dental",
            Estado = "inactivo"
        };
        var atendida = new EstadoCita { NombreEstado = "Atendida" };
        var programada = new EstadoCita { NombreEstado = "Programada" };
        var cancelada = new EstadoCita { NombreEstado = "Cancelada" };
        var noAsistio = new EstadoCita { NombreEstado = "No asistió" };
        var rechazada = new EstadoCita { NombreEstado = "Rechazada" };

        db.AddRange(
            paciente,
            profesionalActivo,
            profesionalInactivo,
            atendida,
            programada,
            cancelada,
            noAsistio,
            rechazada);
        await db.SaveChangesAsync();

        db.HorariosProfesional.AddRange(
            new HorarioProfesional
            {
                IdProfesional = profesionalActivo.IdProfesional,
                DiaSemana = "Martes",
                HoraInicio = new TimeOnly(9, 0),
                HoraFin = new TimeOnly(10, 0),
                Activo = true
            },
            new HorarioProfesional
            {
                IdProfesional = profesionalInactivo.IdProfesional,
                DiaSemana = "Martes",
                HoraInicio = new TimeOnly(9, 0),
                HoraFin = new TimeOnly(10, 0),
                Activo = true
            });

        db.Citas.AddRange(
            CrearCita(fecha.AddHours(9), "Programada", atendida, paciente, profesionalActivo),
            CrearCita(fecha.AddHours(10), "Cancelada", programada, paciente, profesionalActivo),
            CrearCita(fecha.AddHours(11), "Programada", cancelada, paciente, profesionalActivo),
            CrearCita(fecha.AddHours(12), "Rechazada", rechazada, paciente, profesionalActivo),
            CrearCita(fecha.AddDays(1).AddHours(9), "Programada", noAsistio, paciente, profesionalActivo),
            CrearCita(new DateTime(2026, 9, 30, 9, 0, 0), "Atendida", atendida, paciente, profesionalActivo));
        await db.SaveChangesAsync();

        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);
        var dashboardService = new CitasDashboardService(
            db,
            citaService,
            NullLogger<CitasDashboardService>.Instance);

        var dashboard = await dashboardService.ObtenerDashboardAsync(fecha);

        Assert.Equal(1, dashboard.Kpis.PacientesActivos);
        Assert.Equal(1, dashboard.Kpis.ProfesionalesActivos);
        Assert.Equal(2, dashboard.Kpis.CitasHoy);
        Assert.Equal(5, dashboard.Estados.Total);
        Assert.Equal(1, dashboard.Estados.Atendidas);
        Assert.Equal(1, dashboard.Estados.Programadas);
        Assert.Equal(1, dashboard.Estados.Canceladas);
        Assert.Equal(2, dashboard.Estados.Otros);
        Assert.Equal(100, dashboard.Estados.PorcentajeAtendidas +
            dashboard.Estados.PorcentajeConfirmadas +
            dashboard.Estados.PorcentajeProgramadas +
            dashboard.Estados.PorcentajeCanceladas +
            dashboard.Estados.PorcentajeOtros);
        Assert.Equal(1, dashboard.Kpis.VariacionAtencionesSemana);
        Assert.Equal(75, dashboard.OcupacionPorcentaje);
        var topProfesional = Assert.Single(dashboard.TopProfesionales);
        Assert.Equal(profesionalActivo.IdProfesional, topProfesional.IdProfesional);
        Assert.Equal(1, topProfesional.TotalCitas);
    }

    private static Cita CrearCita(
        DateTime fecha,
        string estado,
        EstadoCita estadoCatalogo,
        Paciente paciente,
        Profesional profesional) =>
        new()
        {
            FechaHora = fecha,
            Estado = estado,
            EstadoCita = estadoCatalogo,
            IdEstado = estadoCatalogo.IdEstado,
            Paciente = paciente,
            IdPaciente = paciente.IdPaciente,
            Profesional = profesional,
            IdProfesional = profesional.IdProfesional
        };

    private static AppDbContext CrearDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
