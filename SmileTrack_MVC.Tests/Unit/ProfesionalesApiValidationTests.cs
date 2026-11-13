using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Profesionales;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalesApiValidationTests
{
    [Fact]
    public async Task GetComisiones_ProfesionalInexistente_Retorna404()
    {
        await using var db = CreateDatabase();
        var controller = CreateController(db);

        var result = await controller.GetComisiones(404, null, null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetComisiones_PorcentajeFueraDeRango_Retorna400()
    {
        await using var db = CreateDatabase();
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-COMMISSION-RANGE",
            Estado = "activo"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetComisiones(7, null, null, porcentajeComision: 120);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PostAusenciaConReasignacion_ModelStateInvalido_NoPersisteAusencia()
    {
        await using var db = CreateDatabase();
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-AUSENCIA-API",
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        controller.ModelState.AddModelError("Tipo", "El tipo de ausencia no es válido.");

        var result = await controller.PostAusenciaConReasignacion(7, new AusenciaProfesionalApiRequest
        {
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 2)
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await db.AusenciasProfesional.ToListAsync());
    }

    [Fact]
    public async Task PostAusenciaConReasignacion_RangoSolapado_Retorna409()
    {
        await using var db = CreateDatabase();
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 7,
            IdUsuario = 42,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-AUSENCIA-CONFLICT",
            Estado = "activo"
        });
        db.AusenciasProfesional.Add(new AusenciaProfesional
        {
            IdProfesional = 7,
            Tipo = "vacaciones",
            FechaInicio = new DateOnly(2026, 12, 1),
            FechaFin = new DateOnly(2026, 12, 15)
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42")], "test"))
            }
        };

        var result = await controller.PostAusenciaConReasignacion(7, new AusenciaProfesionalApiRequest
        {
            Tipo = "permiso",
            FechaInicio = new DateOnly(2026, 12, 10),
            FechaFin = new DateOnly(2026, 12, 20)
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await db.AusenciasProfesional.ToListAsync());
    }

    private static AppDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfesionalesApiController CreateController(AppDbContext db) =>
        new(new ProfesionalService(db, NullLogger<ProfesionalService>.Instance),
            NullLogger<ProfesionalesApiController>.Instance);
}