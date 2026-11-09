using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalesDashboardTests
{
    [Fact]
    public async Task DashboardStats_DevuelveSoloCitasDelProfesionalAutenticado()
    {
        await using var db = CreateDatabase();
        var profesional = new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-DASH-OWN"
        };
        var otroProfesional = new Profesional
        {
            IdProfesional = 8,
            IdUsuario = 99,
            Nombres = "Luis",
            Apellidos = "Profesional",
            RegistroMedico = "RM-DASH-OTHER"
        };
        db.Profesionales.AddRange(profesional, otroProfesional);
        db.Citas.AddRange(
            new Cita
            {
                IdPaciente = 1,
                IdProfesional = profesional.IdProfesional,
                FechaHora = DateTime.Today.AddHours(9),
                Estado = "Atendida"
            },
            new Cita
            {
                IdPaciente = 2,
                IdProfesional = profesional.IdProfesional,
                FechaHora = DateTime.Today.AddHours(10),
                Estado = "Agendada"
            },
            new Cita
            {
                IdPaciente = 3,
                IdProfesional = otroProfesional.IdProfesional,
                FechaHora = DateTime.Today.AddHours(11),
                Estado = "Atendida"
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db, 42);
        var result = Assert.IsType<OkObjectResult>(await controller.DashboardStats());
        var payload = result.Value!;

        Assert.Equal(2, ReadInt(payload, "citasHoy"));
        Assert.Equal(1, ReadInt(payload, "citasAtendidas"));
    }

    [Fact]
    public async Task GetStats_DevuelveConteosGlobalesYRestringeAccesoAlAdministrador()
    {
        await using var db = CreateDatabase();
        db.Profesionales.AddRange(
            new Profesional { Nombres = "Ana", Estado = "activo", RegistroMedico = "RM-STATS-1" },
            new Profesional { Nombres = "Luis", Estado = "vacaciones", RegistroMedico = "RM-STATS-2" },
            new Profesional { Nombres = "Eva", Estado = "inactivo", RegistroMedico = "RM-STATS-3" });
        await db.SaveChangesAsync();

        var service = new ProfesionalService(db, NullLogger<ProfesionalService>.Instance);
        var controller = new ProfesionalesApiController(service, NullLogger<ProfesionalesApiController>.Instance);
        var result = Assert.IsType<OkObjectResult>(await controller.GetStats());
        var data = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value)!;

        Assert.Equal(3, ReadInt(data, "total"));
        Assert.Equal(1, ReadInt(data, "activos"));
        Assert.Equal(1, ReadInt(data, "vacaciones"));
        Assert.Equal(1, ReadInt(data, "inactivos"));

        var authorization = typeof(ProfesionalesApiController)
            .GetMethod(nameof(ProfesionalesApiController.GetStats))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("Administrador", authorization!.Roles);
    }

    [Fact]
    public async Task Stodo09PerfilProfesional_CargaSoloElPerfilYCredencialesPropios()
    {
        await using var db = CreateDatabase();
        var profesional = new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-PROFILE-OWN"
        };
        var otroProfesional = new Profesional
        {
            IdProfesional = 8,
            IdUsuario = 99,
            Nombres = "Luis",
            Apellidos = "Profesional",
            RegistroMedico = "RM-PROFILE-OTHER"
        };
        db.Profesionales.AddRange(profesional, otroProfesional);
        db.CredencialesProfesionales.AddRange(
            CreateCredential(profesional.IdProfesional, "Propia"),
            CreateCredential(otroProfesional.IdProfesional, "Ajena"));
        await db.SaveChangesAsync();

        var controller = CreateController(db, 42);
        var result = await controller.Stodo09PerfilProfesional();

        Assert.IsType<ViewResult>(result);
        var loadedProfessional = Assert.IsType<Profesional>(controller.ViewData["LoggedProfesional"]);
        var credentials = Assert.IsAssignableFrom<System.Collections.Generic.List<CredencialProfesional>>(
            controller.ViewData["CredencialesProfesional"]);
        Assert.Equal(profesional.IdProfesional, loadedProfessional.IdProfesional);
        Assert.Collection(credentials, credential =>
        {
            Assert.Equal(profesional.IdProfesional, credential.IdProfesional);
            Assert.Equal("Propia", credential.NombreOriginal);
        });
        Assert.Null(controller.ViewData["Profesionales"]);
    }

    private static AppDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GestionProfesionalesController CreateController(AppDbContext db, int userId) =>
        new(db, NullLogger<GestionProfesionalesController>.Instance, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Profesional")
                    ], "test"))
                }
            }
        };

    private static CredencialProfesional CreateCredential(int professionalId, string originalName) =>
        new()
        {
            IdProfesional = professionalId,
            Tipo = "Título de especialidad",
            NombreOriginal = originalName,
            RutaPrivada = $"private_uploads/{professionalId}/credential.pdf",
            ContentType = "application/pdf",
            TamanoBytes = 10,
            EstadoRevision = "pendiente"
        };

    private static int ReadInt(object payload, string propertyName) =>
        (int)payload.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(payload)!;
}
