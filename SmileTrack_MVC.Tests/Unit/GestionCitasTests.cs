using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.DTOs;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmileTrack_MVC.Tests.Unit;

public class GestionCitasTests
{
    private AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CrearCita_CreatesAppointment()
    {
        var db = CreateInMemoryDb("test_crear_cita");
        var paciente = new Paciente { Nombres = "Juan", Apellidos = "Perez", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dra", Apellidos = "Sanchez", Estado = "activo" };
        var consultorio = new Consultorio { Nombre = "Consultorio A", Estado = "activo" };
        var servicio = new Servicio { Nombre = "Consulta general", Estado = "activo" };
        var fechaHora = DateTime.Today.AddDays(1).AddHours(9);
        var diaSemana = fechaHora.DayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miercoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sabado",
            _ => "Domingo"
        };

        db.Pacientes.Add(paciente);
        db.Profesionales.Add(profesional);
        db.Consultorios.Add(consultorio);
        db.Servicios.Add(servicio);
        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        await db.SaveChangesAsync();

        db.HorariosProfesional.Add(new HorarioProfesional
        {
            IdProfesional = profesional.IdProfesional,
            DiaSemana = diaSemana,
            HoraInicio = new TimeOnly(7, 0),
            HoraFin = new TimeOnly(18, 0),
            Activo = true
        });
        await db.SaveChangesAsync();

        var request = new CitaApiRequest
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdConsultorio = consultorio.IdConsultorio,
            IdServicio = servicio.IdServicio,
            FechaHora = fechaHora,
            Estado = "Agendada",
            Notas = "Prueba unit"
        };

        var service = new CitaService(db, NullLogger<CitaService>.Instance);
        var citaCreada = await service.CrearAsync(request);

        var citas = db.Citas.ToList();
        Assert.Single(citas);
        var cita = citas[0];
        Assert.Equal(paciente.IdPaciente, cita.IdPaciente);
        Assert.Equal(citaCreada.IdCita, cita.IdCita);
    }
}
