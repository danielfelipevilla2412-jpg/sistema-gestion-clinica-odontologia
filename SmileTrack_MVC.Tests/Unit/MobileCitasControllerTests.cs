using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class MobileCitasControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MobileCitasController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var controller = new MobileCitasController(db);

        var claims = new List<Claim>();
        if (idUsuario.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, idUsuario.Value.ToString()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task MisCitas_SinClaimDeUsuario_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.MisCitas(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task MisCitas_PacienteInexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: 99);

        var result = await controller.MisCitas(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MisCitas_ConCitasFuturas_RetornaOkConListado()
    {
        await using var db = CrearContexto();
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Estado = "activo" });
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 1,
            Nombres = "Luis",
            Apellidos = "Ortiz",
            RegistroMedico = "RM-MOVIL",
            Estado = "activo"
        });
        db.EstadosCita.Add(new EstadoCita { IdEstado = 1, NombreEstado = "Programada" });
        db.Citas.Add(new Cita
        {
            IdCita = 1,
            IdPaciente = 1,
            IdServicio = 1,
            IdProfesional = 1,
            IdEstado = 1,
            Estado = "Programada",
            FechaHora = DateTime.Today.AddDays(3).AddHours(9)
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.MisCitas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(1, Contar((IEnumerable)ok.Value!));
    }

    [Fact]
    public async Task MisCitas_ExcluyeCitasPasadas()
    {
        await using var db = CrearContexto();
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            Nombres = "Ana",
            Apellidos = "Prueba",
            Estado = "activo"
        });
        db.Citas.AddRange(
            new Cita
            {
                IdCita = 1,
                IdPaciente = 1,
                Estado = "Atendida",
                FechaHora = DateTime.Today.AddDays(-5).AddHours(9)
            },
            new Cita
            {
                IdCita = 2,
                IdPaciente = 1,
                Estado = "Programada",
                FechaHora = DateTime.Today.AddDays(2).AddHours(9)
            });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.MisCitas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, Contar((IEnumerable)ok.Value!));
    }
}
