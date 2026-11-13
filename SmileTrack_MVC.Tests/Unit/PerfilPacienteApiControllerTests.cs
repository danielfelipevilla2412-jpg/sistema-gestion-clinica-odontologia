using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Api.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.Perfiles;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class PerfilPacienteApiControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PerfilPacienteApiController CrearControlador(AppDbContext db, int? idPaciente = 1)
    {
        var controller = new PerfilPacienteApiController(new PerfilPacienteService(db));

        var claims = new List<Claim>();
        if (idPaciente.HasValue)
            claims.Add(new Claim("IdPaciente", idPaciente.Value.ToString()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    private static void SembrarPaciente(AppDbContext db)
    {
        db.Pacientes.Add(new Paciente
        {
            IdPaciente = 1,
            IdUsuario = 10,
            TipoDocumento = "CC",
            Documento = "1234567",
            Nombres = "Ana",
            Apellidos = "Prueba",
            FechaNacimiento = new DateTime(1990, 5, 5),
            Estado = "activo"
        });
    }

    [Fact]
    public async Task ObtenerInfoBasica_SinClaimIdPaciente_RetornaForbid()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idPaciente: null);

        var result = await controller.ObtenerInfoBasica(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ObtenerInfoBasica_PacienteInexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idPaciente: 999);

        var result = await controller.ObtenerInfoBasica(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ObtenerInfoBasica_PacienteExistente_RetornaOk()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerInfoBasica(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task ObtenerInfoMedica_PacienteExistente_RetornaOk()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerInfoMedica(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task ObtenerEstadisticas_PacienteExistente_RetornaOk()
    {
        await using var db = CrearContexto();
        SembrarPaciente(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerEstadisticas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task ObtenerResumenCompleto_PacienteInexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idPaciente: 999);

        var result = await controller.ObtenerResumenCompleto(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
