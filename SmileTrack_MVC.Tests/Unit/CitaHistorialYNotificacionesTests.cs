using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class CitaHistorialYNotificacionesTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AppDbContext(options);

        // Seed data
        db.Pacientes.Add(new Paciente { IdPaciente = 1, Nombres = "Juan", Apellidos = "Pérez", Estado = "activo" });
        db.Profesionales.Add(new Profesional { IdProfesional = 1, Nombres = "Ana", Apellidos = "Gómez", Estado = "activo" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Limpieza", DuracionMinutos = 30, Estado = "activo" });
        db.Consultorios.Add(new Consultorio { IdConsultorio = 1, Nombre = "Consultorio 101", Estado = "disponible" });

        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 2, NombreEstado = "Confirmada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 3, NombreEstado = "Cancelada" });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 4, NombreEstado = "Atendida" });

        string[] dias = ["Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado", "Domingo"];
        foreach (var dia in dias)
        {
            db.HorariosProfesional.Add(new HorarioProfesional
            {
                IdProfesional = 1,
                DiaSemana = dia,
                HoraInicio = new TimeOnly(0, 0),
                HoraFin = new TimeOnly(23, 59),
                Activo = true
            });
        }

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task CrearAsync_RegistraHistorialYNotificacion()
    {
        var db = CreateInMemoryDb($"db_crear_{Guid.NewGuid()}");
        var service = new CitaService(db, NullLogger<CitaService>.Instance);

        var fecha = DateTime.Today.AddDays(2).AddHours(10);

        var request = new CitaApiRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            FechaHora = fecha,
            Estado = "Programada"
        };

        var cita = await service.CrearAsync(request);

        Assert.NotNull(cita);
        Assert.True(cita.IdCita > 0);

        var historial = await db.CitasHistorialEstado.Where(h => h.IdCita == cita.IdCita).ToListAsync();
        Assert.Single(historial);
        Assert.Equal("Cita creada", historial[0].Motivo);
        Assert.Equal("Programada", historial[0].EstadoTexto);

        var notificaciones = await db.Notificaciones.Where(n => n.IdPaciente == 1).ToListAsync();
        Assert.Single(notificaciones);
        Assert.Equal("Cita agendada", notificaciones[0].Titulo);
        Assert.Equal("confirmacion", notificaciones[0].Tipo);
    }

    [Fact]
    public async Task CambiarEstadoAsync_RegistraHistorialYNotificacion()
    {
        var db = CreateInMemoryDb($"db_cambiar_{Guid.NewGuid()}");
        var service = new CitaService(db, NullLogger<CitaService>.Instance);

        var fecha = DateTime.Today.AddDays(2).AddHours(10);
        var request = new CitaApiRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            FechaHora = fecha,
            Estado = "Programada"
        };
        var cita = await service.CrearAsync(request);

        var citaActualizada = await service.CambiarEstadoAsync(cita.IdCita, "Confirmada");

        Assert.NotNull(citaActualizada);
        Assert.Equal("Confirmada", citaActualizada.Estado);

        var historial = await db.CitasHistorialEstado.Where(h => h.IdCita == cita.IdCita).OrderBy(h => h.FechaCambio).ToListAsync();
        Assert.Equal(2, historial.Count);
        Assert.Equal("Confirmada", historial[1].EstadoTexto);

        var notificaciones = await db.Notificaciones.Where(n => n.IdPaciente == 1).ToListAsync();
        Assert.Equal(2, notificaciones.Count);
        Assert.Contains(notificaciones, n => n.Titulo == "Cita confirmada");
    }

    [Fact]
    public async Task CancelarAsync_RegistraHistorialYNotificacion()
    {
        var db = CreateInMemoryDb($"db_cancelar_{Guid.NewGuid()}");
        var service = new CitaService(db, NullLogger<CitaService>.Instance);

        var fecha = DateTime.Today.AddDays(2).AddHours(10);
        var request = new CitaApiRequest
        {
            IdPaciente = 1,
            IdProfesional = 1,
            IdServicio = 1,
            IdConsultorio = 1,
            FechaHora = fecha,
            Estado = "Programada"
        };
        var cita = await service.CrearAsync(request);

        bool cancelado = await service.CancelarAsync(cita.IdCita);

        Assert.True(cancelado);

        var historial = await db.CitasHistorialEstado.Where(h => h.IdCita == cita.IdCita).OrderBy(h => h.FechaCambio).ToListAsync();
        Assert.Equal(2, historial.Count);
        Assert.Contains("cancelada", historial[1].Motivo?.ToLower());

        var notificaciones = await db.Notificaciones.Where(n => n.IdPaciente == 1).ToListAsync();
        Assert.Equal(2, notificaciones.Count);
        Assert.Contains(notificaciones, n => n.Titulo == "Cita cancelada");
    }
}
