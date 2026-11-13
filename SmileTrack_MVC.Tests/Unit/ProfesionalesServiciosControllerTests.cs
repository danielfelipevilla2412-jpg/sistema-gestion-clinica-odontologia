using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ProfesionalesServiciosControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static GestionProfesionalesController CrearControlador(AppDbContext db)
    {
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
                        new Claim(ClaimTypes.NameIdentifier, "99"),
                        new Claim(ClaimTypes.Role, "Administrador")
                    ], "TestAuth"))
                }
            }
        };
        controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, new MemoryTempDataProvider());
        return controller;
    }

    private static void SembrarProfesionalServicio(AppDbContext db, string estadoProfesional = "activo", bool conEspecialidad = true)
    {
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 7,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-SERVICIOS",
            Estado = estadoProfesional
        });
        db.Servicios.Add(new Servicio
        {
            IdServicio = 1,
            Nombre = "Consulta",
            Estado = "activo",
            Precio = 50000m,
            IdEspecialidad = 1
        });
        if (conEspecialidad)
        {
            db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
            {
                IdProfesional = 7,
                IdEspecialidad = 1,
                Principal = true
            });
        }
    }

    [Fact]
    public async Task ServiciosAsignados_SinProfesionales_RetornaViewConListaVacia()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.ServiciosAsignados(null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var asignaciones = Assert.IsAssignableFrom<IEnumerable<ProfesionalServicio>>(controller.ViewData["AsignacionesLista"]);
        Assert.Empty(asignaciones);
        Assert.Null(controller.ViewData["ProfesionalSeleccionado"]);
    }

    [Fact]
    public async Task ServiciosAsignados_ConProfesional_CargaSeleccionadoEnViewData()
    {
        await using var db = CrearContexto();
        SembrarProfesionalServicio(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ServiciosAsignados(null, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        var seleccionado = Assert.IsType<Profesional>(controller.ViewData["ProfesionalSeleccionado"]);
        Assert.Equal(7, seleccionado.IdProfesional);
    }

    [Fact]
    public async Task GuardarServicioAsignado_ProfesionalInactivo_RegistraErrorYNoPersiste()
    {
        await using var db = CrearContexto();
        SembrarProfesionalServicio(db, estadoProfesional: "inactivo");
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GuardarServicioAsignado(7, 1, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("ServiciosAsignados", redirect.ActionName);
        Assert.NotNull(controller.TempData["AsignacionError"]);
        Assert.Equal(0, await db.ProfesionalServicios.CountAsync());
    }

    [Fact]
    public async Task GuardarServicioAsignado_SinEspecialidadDelProfesional_RegistraError()
    {
        await using var db = CrearContexto();
        SembrarProfesionalServicio(db, conEspecialidad: false);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GuardarServicioAsignado(7, 1, null, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["AsignacionError"]);
        Assert.Equal(0, await db.ProfesionalServicios.CountAsync());
    }

    [Fact]
    public async Task GuardarServicioAsignado_PrecioInvalido_RegistraError()
    {
        await using var db = CrearContexto();
        SembrarProfesionalServicio(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GuardarServicioAsignado(7, 1, "no-es-numero", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["AsignacionError"]);
        Assert.Equal(0, await db.ProfesionalServicios.CountAsync());
    }

    [Fact]
    public async Task GuardarServicioAsignado_DatosValidos_PersisteAsignacion()
    {
        await using var db = CrearContexto();
        SembrarProfesionalServicio(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GuardarServicioAsignado(7, 1, "75000.50", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var asignacion = await db.ProfesionalServicios.SingleAsync();
        Assert.Equal(7, asignacion.IdProfesional);
        Assert.Equal(1, asignacion.IdServicio);
        Assert.Equal(75000.50m, asignacion.PrecioPersonalizado);
        Assert.True(asignacion.Activo);
    }

    [Fact]
    public async Task CambiarEstadoServicioAsignado_AsignacionInexistente_RegistraError()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.CambiarEstadoServicioAsignado(7, 1, true, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(controller.TempData["AsignacionError"]);
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
