 #if false
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
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
    public async Task CrearCitaDesdeAppointments_CreatesAppointment()
    {
        var db = CreateInMemoryDb("test_crear_cita");
        // Seed data
        var paciente = new Paciente { Nombres = "Juan", Apellidos = "Perez", Estado = "activo" };
        var profesional = new Profesional { Nombres = "Dra", Apellidos = "Sanchez", Estado = "activo" };
        var consultorio = new Consultorio { Nombre = "Consultorio A", Estado = "activo" };
        var servicio = new Servicio { Nombre = "Consulta general", Estado = "activo" };
        db.Pacientes.Add(paciente);
        db.Profesionales.Add(profesional);
        db.Consultorios.Add(consultorio);
        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();

        var controller = new GestionCitasController(db, NullLogger<GestionCitasController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var dto = new CitaAgendaDto
        {
            IdPaciente = paciente.IdPaciente,
            IdProfesional = profesional.IdProfesional,
            IdConsultorio = consultorio.IdConsultorio,
            IdServicio = servicio.IdServicio,
            Fecha = System.DateTime.Today.AddDays(1),
            HoraInicio = new System.TimeSpan(9, 0, 0),
            HoraFin = new System.TimeSpan(9, 30, 0),
            Estado = "Agendada",
            Notas = "Prueba unit"
        };

        var result = await controller.CrearCitaDesdeAppointments(dto);
        Assert.IsType<OkObjectResult>(result);

        // Validate DB has the appointment
        var citas = db.Citas.ToList();
        Assert.Single(citas);
        var cita = citas[0];
        Assert.Equal(paciente.IdPaciente, cita.IdPaciente);
    }
#endif
