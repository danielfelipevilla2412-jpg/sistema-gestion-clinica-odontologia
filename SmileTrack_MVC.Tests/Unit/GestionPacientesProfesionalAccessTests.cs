using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class GestionPacientesProfesionalAccessTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public async Task DetallePaciente_ProfesionalSoloAccedeAPacientesVinculados(
        bool pacienteAsignado,
        bool citaPropia,
        bool profesionalAsociado)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        const int idProfesional = 7;
        const int idUsuario = 42;
        const int idPaciente = 11;

        if (profesionalAsociado)
        {
            db.Profesionales.Add(new Profesional
            {
                IdProfesional = idProfesional,
                IdUsuario = idUsuario,
                Nombres = "Ana",
                Apellidos = "Profesional",
                RegistroMedico = "RM-PATIENT-ACCESS"
            });
        }
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = idPaciente,
            Nombres = "Paciente",
            Apellidos = "Prueba",
            Documento = "DOC-PATIENT-ACCESS",
            FechaNacimiento = new DateTime(1990, 1, 1),
            IdProfesionalAsignado = pacienteAsignado ? idProfesional : null
        });
        if (citaPropia)
        {
            db.Citas.Add(new Cita
            {
                IdCita = 17,
                IdPaciente = idPaciente,
                IdProfesional = idProfesional,
                FechaHora = DateTime.Today,
                Estado = "Atendida"
            });
        }
        await db.SaveChangesAsync();

        var controller = new GestionPacientesController(
            db,
            NullLogger<GestionPacientesController>.Instance,
            null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
                        new Claim(ClaimTypes.Role, "Profesional")
                    ], "test"))
                }
            }
        };

        var result = await controller.DetallePaciente(idPaciente);

        if (profesionalAsociado && (pacienteAsignado || citaPropia))
            Assert.IsType<ViewResult>(result);
        else
            Assert.IsType<ForbidResult>(result);
    }
}
