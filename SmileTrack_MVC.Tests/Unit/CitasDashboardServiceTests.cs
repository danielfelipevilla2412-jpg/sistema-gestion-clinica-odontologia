using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Helpers;
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
        Assert.Equal(1, dashboard.Estados.NoAsistio);
        Assert.Equal(1, dashboard.Estados.Otros);
        Assert.Equal(100, dashboard.Estados.PorcentajeAtendidas +
            dashboard.Estados.PorcentajeConfirmadas +
            dashboard.Estados.PorcentajeProgramadas +
            dashboard.Estados.PorcentajeCanceladas +
            dashboard.Estados.PorcentajeNoAsistio +
            dashboard.Estados.PorcentajeOtros);
        Assert.Equal(1, dashboard.Kpis.VariacionAtencionesSemana);
        Assert.Equal(75, dashboard.OcupacionPorcentaje);
        var topProfesional = Assert.Single(dashboard.TopProfesionales);
        Assert.Equal(profesionalActivo.IdProfesional, topProfesional.IdProfesional);
        Assert.Equal(1, topProfesional.TotalCitas);
    }

    [Fact]
    public async Task ObtenerMetricasPanelOperativoAsync_ConservaMetricasMensualesYCalculaOcupacion()
    {
        await using var db = CrearDb();
        var fecha = new DateTime(2026, 10, 6);
        var paciente = new Paciente
        {
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        };
        var profesional = new Profesional
        {
            Nombres = "Luis",
            Apellidos = "Dental",
            Estado = "activo"
        };
        var atendida = new EstadoCita { NombreEstado = "Atendida" };
        var confirmada = new EstadoCita { NombreEstado = "Confirmada" };
        var programada = new EstadoCita { NombreEstado = "Programada" };
        var cancelada = new EstadoCita { NombreEstado = "Cancelada" };
        var servicio = new Servicio
        {
            Nombre = "Consulta",
            Precio = 25000m,
            Estado = "activo"
        };

        db.AddRange(paciente, profesional, atendida, confirmada, programada, cancelada, servicio);
        await db.SaveChangesAsync();

        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = "Martes",
            HoraInicio = new TimeOnly(8, 0),
            HoraFin = new TimeOnly(10, 0),
            Activo = true
        });
        db.Citas.AddRange(
            CrearCita(fecha.AddHours(8), "Atendida", atendida, paciente, profesional),
            CrearCita(fecha.AddHours(9), "Atendida", atendida, paciente, profesional),
            CrearCita(fecha.AddDays(7).AddHours(8), "Confirmada", confirmada, paciente, profesional),
            CrearCita(fecha.AddDays(14).AddHours(8), "Programada", programada, paciente, profesional),
            CrearCita(fecha.AddDays(21).AddHours(8), "Cancelada", cancelada, paciente, profesional));
        foreach (var cita in db.Citas.Local)
        {
            cita.Servicio = servicio;
            cita.IdServicio = servicio.IdServicio;
        }
        await db.SaveChangesAsync();

        var citaService = new CitaService(db, NullLogger<CitaService>.Instance);
        var dashboardService = new CitasDashboardService(
            db,
            citaService,
            NullLogger<CitasDashboardService>.Instance);

        var metricas = await dashboardService.ObtenerMetricasPanelOperativoAsync(fecha);

        Assert.Equal(1, metricas.TotalPacientes);
        Assert.Equal(2, metricas.CitasHoy);
        Assert.Equal(1, metricas.ProfesionalesActivos);
        Assert.Equal(50000m, metricas.IngresosDelMes);
        Assert.Equal(62, metricas.PorcentajeOcupacion);
        Assert.Equal(2, metricas.CitasAtendidas);
        Assert.Equal(1, metricas.CitasConfirmadas);
        Assert.Equal(1, metricas.CitasProgramadas);
        Assert.Equal(1, metricas.CitasCanceladas);
        Assert.Equal(100, metricas.PorcentajeAtendidas);
        Assert.Equal(50, metricas.PorcentajeConfirmadas);
    }

    [Fact]
    public async Task ObtenerDashboardRecepcionAsync_ConstruyeDatosDeAgendaResumenYAlertas()
    {
        await using var db = CrearDb();
        DateTime hoy = ColombiaClock.Today;
        var paciente = new Paciente
        {
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        };
        db.Pacientes.Add(paciente);
        db.Citas.AddRange(
            new Cita
            {
                IdPaciente = paciente.IdPaciente,
                FechaHora = ColombiaClock.Now.AddMinutes(10),
                Estado = "Programada"
            },
            new Cita
            {
                IdPaciente = paciente.IdPaciente,
                FechaHora = hoy.AddHours(9),
                Estado = "En proceso"
            },
            new Cita
            {
                IdPaciente = paciente.IdPaciente,
                FechaHora = hoy.AddHours(11),
                Estado = "Cancelada"
            },
            new Cita
            {
                IdPaciente = paciente.IdPaciente,
                FechaHora = hoy.AddDays(1).AddHours(9),
                Estado = "Programada"
            });
        db.Facturas.AddRange(
            new Factura
            {
                IdPaciente = paciente.IdPaciente,
                NumeroFactura = "FAC-PENDIENTE",
                FechaFactura = hoy.AddDays(-16),
                Total = 500m,
                Estado = "pendiente"
            },
            new Factura
            {
                IdPaciente = paciente.IdPaciente,
                NumeroFactura = "FAC-PARCIAL",
                FechaFactura = hoy,
                Total = 750m,
                Estado = "parcial"
            },
            new Factura
            {
                IdPaciente = paciente.IdPaciente,
                NumeroFactura = "FAC-PAGADA",
                FechaFactura = hoy.AddDays(-30),
                Total = 10000m,
                Estado = "pagada"
            });
        await db.SaveChangesAsync();

        var service = new CitasDashboardService(
            db,
            new CitaService(db, NullLogger<CitaService>.Instance),
            NullLogger<CitasDashboardService>.Instance);

        var dashboard = await service.ObtenerDashboardRecepcionAsync(hoy);

        Assert.Equal(3, dashboard.Stats.CitasHoy);
        Assert.Equal(0, dashboard.Stats.Confirmadas);
        Assert.Equal(1, dashboard.Stats.Pendientes);
        Assert.StartsWith("$1", dashboard.Stats.FacturasPendientes);
        Assert.EndsWith("k", dashboard.Stats.FacturasPendientes);
        Assert.Equal(1, dashboard.Alertas.SinConfirmar);
        Assert.Equal(1, dashboard.Alertas.FacturasVencidas);
        Assert.Equal(3, dashboard.Appointments.Count);
        Assert.Contains(dashboard.Appointments, cita =>
            cita.Status == "En consulta" &&
            cita.StatusClass == "status-consulta" &&
            cita.Highlight);
        Assert.Single(dashboard.ProximasCitas);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dashboard));
        Assert.True(json.RootElement.TryGetProperty("appointments", out _));
        Assert.True(json.RootElement.TryGetProperty("stats", out _));
        Assert.True(json.RootElement.TryGetProperty("alertas", out _));
        Assert.True(json.RootElement.TryGetProperty("proximasCitas", out _));
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
