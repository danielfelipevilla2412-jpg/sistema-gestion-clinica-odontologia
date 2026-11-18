using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class ProfesionalesControllerReportesTests
{
    [Fact]
    public async Task Stadm14ReportesClinicos_PaginatesInDatabaseAndFindsNextAppointmentOutsideMonth()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var ownProfessional = new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "REG-7"
        };
        var otherProfessional = new Profesional
        {
            IdProfesional = 8,
            Nombres = "Otro",
            Apellidos = "Profesional",
            RegistroMedico = "REG-8"
        };
        var patientOne = new Paciente
        {
            IdPaciente = 1,
            Nombres = "Paciente",
            Apellidos = "Uno",
            Documento = "1001"
        };
        var patientTwo = new Paciente
        {
            IdPaciente = 2,
            Nombres = "Paciente",
            Apellidos = "Dos",
            Documento = "1002"
        };

        db.Profesionales.AddRange(ownProfessional, otherProfessional);
        db.Pacientes.AddRange(patientOne, patientTwo);

        var previousMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var nextAppointment = DateTime.Now.AddDays(2);
        db.Citas.AddRange(
            new Cita
            {
                IdCita = 1,
                IdPaciente = patientOne.IdPaciente,
                Paciente = patientOne,
                IdProfesional = ownProfessional.IdProfesional,
                Profesional = ownProfessional,
                FechaHora = previousMonth.AddDays(3).AddHours(9),
                Estado = "Atendida"
            },
            new Cita
            {
                IdCita = 2,
                IdPaciente = patientTwo.IdPaciente,
                Paciente = patientTwo,
                IdProfesional = ownProfessional.IdProfesional,
                Profesional = ownProfessional,
                FechaHora = previousMonth.AddDays(5).AddHours(9),
                Estado = "Atendida"
            },
            new Cita
            {
                IdCita = 3,
                IdPaciente = patientOne.IdPaciente,
                Paciente = patientOne,
                IdProfesional = otherProfessional.IdProfesional,
                Profesional = otherProfessional,
                FechaHora = nextAppointment.AddDays(-1),
                Estado = "Programada"
            },
            new Cita
            {
                IdCita = 4,
                IdPaciente = patientOne.IdPaciente,
                Paciente = patientOne,
                IdProfesional = ownProfessional.IdProfesional,
                Profesional = ownProfessional,
                FechaHora = nextAppointment,
                Estado = "Programada"
            });
        await db.SaveChangesAsync();

        var controller = new GestionProfesionalesController(
            db,
            NullLogger<GestionProfesionalesController>.Instance,
            null!,
            null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "42"),
                        new Claim(ClaimTypes.Role, "Profesional")
                    ], "test"))
                }
            }
        };

        await controller.Stadm14ReportesClinicos(
            page: 1,
            pageSize: 1,
            profesional: "Otro Profesional",
            mes: previousMonth.ToString("yyyy-MM"));

        var pageOne = Assert.IsType<SmileTrack_MVC.Models.Shared.PagedResult<ReporteClinicoViewModel>>(
            controller.ViewData["ReportesClinicosPage"]);
        Assert.Equal(2, pageOne.TotalCount);
        Assert.Equal(1, pageOne.PageSize);
        Assert.Single(pageOne.Items);
        Assert.Equal(patientTwo.IdPaciente, pageOne.Items[0].Id);

        await controller.Stadm14ReportesClinicos(
            page: 2,
            pageSize: 1,
            mes: previousMonth.ToString("yyyy-MM"));

        var pageTwo = Assert.IsType<SmileTrack_MVC.Models.Shared.PagedResult<ReporteClinicoViewModel>>(
            controller.ViewData["ReportesClinicosPage"]);
        Assert.Equal(patientOne.IdPaciente, pageTwo.Items.Single().Id);
        Assert.Equal(nextAppointment, pageTwo.Items.Single().ProximaCita);
    }
}
