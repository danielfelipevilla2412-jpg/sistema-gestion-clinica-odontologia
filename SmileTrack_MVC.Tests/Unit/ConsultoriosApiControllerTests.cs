using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public class ConsultoriosApiControllerTests
{
    private static AppDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static ConsultoriosApiController CreateController(AppDbContext db, int userId = 1)
    {
        var controller = new ConsultoriosApiController(
            db,
            NullLogger<ConsultoriosApiController>.Instance,
            new MemoryCache(new MemoryCacheOptions()));

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "Auxiliar")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public async Task ObtenerConsultorios_ReturnsOkWithList()
    {
        var db = CreateInMemoryDb($"consultorios_get_{Guid.NewGuid()}");
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 1,
            Nombre = "Consultorio 1",
            Ubicacion = "Piso 1",
            Estado = "disponible"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.ObtenerConsultorios();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GuardarPreferencia_ValidConsultorio_SavesPreference()
    {
        var db = CreateInMemoryDb($"consultorios_pref_{Guid.NewGuid()}");
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 10,
            Nombre = "Box Odontología",
            Estado = "disponible"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: 42);
        var request = new GuardarPreferenciaRequest { ConsultorioId = 10 };

        var result = await controller.GuardarPreferencia(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var pref = await db.UsuariosPreferenciasConsultorio.FirstOrDefaultAsync(p => p.IdUsuario == 42);
        Assert.NotNull(pref);
        Assert.Equal(10, pref.IdConsultorio);
    }

    [Fact]
    public async Task GuardarPreferencia_NonExistentConsultorio_ReturnsNotFound()
    {
        var db = CreateInMemoryDb($"consultorios_pref_nf_{Guid.NewGuid()}");
        var controller = CreateController(db);
        var request = new GuardarPreferenciaRequest { ConsultorioId = 999 };

        var result = await controller.GuardarPreferencia(request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ConfirmarEstado_UpdatesStatusChecklistAndLogsHistory()
    {
        var db = CreateInMemoryDb($"consultorios_confirm_{Guid.NewGuid()}");
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 5,
            Nombre = "Consultorio Cirugía",
            Estado = "ocupado"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: 7);
        var checklistItems = new object[]
        {
            new { text = "Limpieza completada", @checked = true }
        };

        var request = new ConfirmarEstadoRequest
        {
            Estado = "disponible",
            Observaciones = "Esterilización OK",
            Checklist = checklistItems,
            ForzarHistorial = true
        };

        var result = await controller.ConfirmarEstado(5, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verificar actualización en Consultorio
        var consultorioActualizado = await db.Consultorios.FindAsync(5);
        Assert.NotNull(consultorioActualizado);
        Assert.Equal("disponible", consultorioActualizado.Estado);

        // Verificar persistencia de EstadoOperativo y Checklist
        var operativo = await db.EstadosOperativosConsultorio.FirstOrDefaultAsync(e => e.IdConsultorio == 5);
        Assert.NotNull(operativo);
        Assert.Equal("Esterilización OK", operativo.Observaciones);
        Assert.Contains("Limpieza completada", operativo.ChecklistJson);

        // Verificar historial
        var historial = await db.ConsultoriosHistorial.FirstOrDefaultAsync(h => h.IdConsultorio == 5);
        Assert.NotNull(historial);
        Assert.Equal("disponible", historial.Estado);
        Assert.Equal("Esterilización OK", historial.Motivo);
        Assert.Equal(7, historial.IdUsuario);
    }

    [Fact]
    public async Task ConfirmarEstado_InvalidStatus_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb($"consultorios_invalid_{Guid.NewGuid()}");
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 2,
            Nombre = "Consultorio 2",
            Estado = "disponible"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new ConfirmarEstadoRequest { Estado = "estado_invalido_xyz" };

        var result = await controller.ConfirmarEstado(2, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GuardarEstadoOperativo_PersistsChecklistAndObservations()
    {
        var db = CreateInMemoryDb($"consultorios_operativo_{Guid.NewGuid()}");
        db.Consultorios.Add(new Consultorio
        {
            IdConsultorio = 3,
            Nombre = "Consultorio 3",
            Estado = "disponible"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, userId: 15);
        var request = new ConsultorioEstadoOperativoApiRequest
        {
            Estado = "mantenimiento",
            Observaciones = "Revisión lámpara dental",
            Checklist = new object[] { new { text = "Revisión", @checked = false } }
        };

        var result = await controller.GuardarEstadoOperativo(3, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var consultorio = await db.Consultorios.FindAsync(3);
        Assert.NotNull(consultorio);
        Assert.Equal("mantenimiento", consultorio.Estado);

        var operativo = await db.EstadosOperativosConsultorio.FirstOrDefaultAsync(e => e.IdConsultorio == 3);
        Assert.NotNull(operativo);
        Assert.Equal("Revisión lámpara dental", operativo.Observaciones);
    }
}

