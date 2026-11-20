using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.GestionDeCitas;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class PacienteNotificacionQueryServiceTests
{
    [Fact]
    public async Task ObtenerNotificacionesAsync_CombinaNotificacionesPersistidasYSinteticasConElContratoJsonActual()
    {
        await using var db = CrearDb();
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            Nombres = "Dra.",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 1,
            Nombre = "Consultorio 1",
            Estado = "activo"
        });
        DateTime citaPersistida = DateTime.Now.AddDays(3);
        DateTime citaSintetica = DateTime.Now.AddDays(2);
        db.Citas.AddRange(
            new Cita
            {
                IdCita = 1,
                IdPaciente = 1,
                IdProfesional = 1,
                IdConsultorio = 1,
                FechaHora = citaPersistida,
                Estado = "Confirmada"
            },
            new Cita
            {
                IdCita = 2,
                IdPaciente = 1,
                FechaHora = citaSintetica,
                Estado = "Programada"
            });
        db.Notificaciones.Add(new Notificacion
        {
            IdNotificacion = 5,
            IdPaciente = 1,
            IdCita = 1,
            Tipo = "confirmada",
            Titulo = "Cita confirmada",
            Contenido = "Tu cita está confirmada.",
            Estado = "pendiente",
            Canal = "interno",
            CreadaEn = DateTime.Now
        });
        await db.SaveChangesAsync();

        var result = await new PacienteNotificacionQueryService(db)
            .ObtenerNotificacionesAsync(10);

        Assert.Equal(2, result.Notificaciones.Count);
        Assert.Equal(2, result.UnreadCount);
        Assert.Contains(result.Notificaciones, item => item.Id == "notificacion-5");
        Assert.Contains(result.Notificaciones, item => item.Id == "cita-2" && item.Badge == "pending");
        Assert.DoesNotContain(result.Notificaciones, item => item.Id == "cita-1");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.True(json.RootElement.TryGetProperty("notificaciones", out var notifications));
        Assert.True(json.RootElement.TryGetProperty("unreadCount", out _));
        Assert.True(notifications[0].TryGetProperty("idNotificacion", out _));
        Assert.True(notifications[0].TryGetProperty("urlAccion", out _));
        Assert.True(notifications[0].TryGetProperty("leida", out _));
    }

    [Fact]
    public async Task ObtenerConteoNoLeidasAsync_UsaElMayorEntreNotificacionesYReservasPendientes()
    {
        await using var db = CrearDb();
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Citas.AddRange(
            new Cita { IdCita = 1, IdPaciente = 1, Estado = "Programada" },
            new Cita { IdCita = 2, IdPaciente = 1, Estado = "Confirmada" },
            new Cita { IdCita = 3, IdPaciente = 1, Estado = "Cancelada" });
        db.Notificaciones.Add(new Notificacion
        {
            IdNotificacion = 1,
            IdPaciente = 1,
            Titulo = "Mensaje",
            Estado = "pendiente",
            CreadaEn = DateTime.Now
        });
        await db.SaveChangesAsync();

        int count = await new PacienteNotificacionQueryService(db)
            .ObtenerConteoNoLeidasAsync(10);
        int noPatientCount = await new PacienteNotificacionQueryService(db)
            .ObtenerConteoNoLeidasAsync(999);

        Assert.Equal(2, count);
        Assert.Equal(0, noPatientCount);
    }

    private static AppDbContext CrearDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
