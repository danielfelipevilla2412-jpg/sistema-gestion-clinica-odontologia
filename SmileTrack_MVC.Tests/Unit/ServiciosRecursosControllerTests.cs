using System;
using System.Collections;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class ServiciosRecursosControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ServiciosRecursosController CrearControlador(AppDbContext db)
    {
        var controller = new ServiciosRecursosController(db, NullLogger<ServiciosRecursosController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, "Administrador")
                ], "TestAuth"))
            }
        };

        controller.ProblemDetailsFactory = new DefaultProblemDetailsFactory(
            Options.Create(new ApiBehaviorOptions()),
            Options.Create(new ProblemDetailsOptions()));

        return controller;
    }

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task Stadm10Servicios_RetornaViewConServiciosYEspecialidades()
    {
        await using var db = CrearContext();
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general", IdEspecialidad = 1 });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stadm10Servicios(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var serviciosJson = Assert.IsType<string>(view.ViewData["ServiciosJson"]);
        Assert.Contains("Consulta", serviciosJson);
        var especialidadesJson = Assert.IsType<string>(view.ViewData["EspecialidadesJson"]);
        Assert.Contains("Odontolog", especialidadesJson);
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(view.Model)));
    }

    [Fact]
    public async Task Stadm16ConfiguracionGeneral_RetornaViewConConfiguraciones()
    {
        await using var db = CrearContext();
        db.ConfiguracionesGenerales.AddRange(
            new ConfiguracionGeneral { IdConfiguracion = 1, Clave = "cita_duracion_default", Valor = "45", Modulo = "general" },
            new ConfiguracionGeneral { IdConfiguracion = 2, Clave = "nombre_clinica", Valor = "Clinica Test", Modulo = "general" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stadm16ConfiguracionGeneral(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var json = Assert.IsType<string>(view.ViewData["ConfigsJson"]);
        Assert.Contains("\"duration\":45", json);
        Assert.Contains("Clinica Test", json);
    }

    [Fact]
    public async Task Stodo05Servicios_SoloDevuelveServiciosActivos()
    {
        await using var db = CrearContext();
        db.Servicios.AddRange(
            new Servicio { IdServicio = 1, Nombre = "Activo", Precio = 1000m, Estado = "activo" },
            new Servicio { IdServicio = 2, Nombre = "Inactivo", Precio = 1000m, Estado = "inactivo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stodo05Servicios(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(view.Model)));
    }

    [Fact]
    public async Task ApiEquipos_RetornaJsonConEquipos()
    {
        await using var db = CrearContext();
        db.Equipos.Add(new Equipo { IdEquipo = 1, Nombre = "Lámpara dental", Status = "operativo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ApiEquipos(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(json.Value)));
    }

    [Fact]
    public async Task ApiInventario_RetornaJsonConInventario()
    {
        await using var db = CrearContext();
        db.Inventarios.Add(new Inventario { IdItem = 1, Codigo = "INS-01", Nombre = "Guantes", StockActual = 10, StockMinimo = 2, Estado = "disponible" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ApiInventario(CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(json.Value)));
    }

    [Fact]
    public async Task ApiCrearServicio_PrecioInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta", Precio = 0m, Category = "general", IdEspecialidad = 1, Duration = 30 };

        var result = await controller.ApiCrearServicio(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.Servicios.CountAsync());
    }

    [Fact]
    public async Task ApiCrearServicio_EspecialidadInexistente_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta", Precio = 80000m, Category = "general", IdEspecialidad = 99, Duration = 30 };

        var result = await controller.ApiCrearServicio(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, await db.Servicios.CountAsync());
    }

    [Fact]
    public async Task ApiCrearServicio_NombreDuplicado_RetornaConflict()
    {
        await using var db = CrearContext();
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general", IdEspecialidad = 1 });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta", Precio = 80000m, Category = "general", IdEspecialidad = 1, Duration = 30 };

        var result = await controller.ApiCrearServicio(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(1, await db.Servicios.CountAsync());
    }

    [Fact]
    public async Task ApiCrearServicio_DatosValidos_RetornaOkYPersiste()
    {
        await using var db = CrearContext();
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta general", Descripcion = "Control", Precio = 80000m, Category = "general", IdEspecialidad = 1, Duration = 30 };

        var result = await controller.ApiCrearServicio(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var servicio = await db.Servicios.SingleAsync();
        Assert.Equal("Consulta general", servicio.Nombre);
        Assert.Equal("activo", servicio.Estado);
        Assert.Equal(80000m, servicio.Precio);
    }

    [Fact]
    public async Task ApiActualizarServicio_ServicioInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta", Precio = 80000m, Category = "general", IdEspecialidad = 1, Duration = 30 };

        var result = await controller.ApiActualizarServicio(999, request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ApiActualizarServicio_NombreDuplicado_RetornaConflict()
    {
        await using var db = CrearContext();
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Servicios.AddRange(
            new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general", IdEspecialidad = 1 },
            new Servicio { IdServicio = 2, Nombre = "Blanqueamiento", Precio = 90000m, Estado = "activo", Categoria = "estetica", IdEspecialidad = 1 });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta", Precio = 70000m, Category = "estetica", IdEspecialidad = 1, Duration = 30 };

        var result = await controller.ApiActualizarServicio(2, request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task ApiActualizarServicio_DatosValidos_RetornaOkYPersiste()
    {
        await using var db = CrearContext();
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo", Categoria = "general", IdEspecialidad = 1 });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var request = new ServicioRequest { Nombre = "Consulta actualizada", Precio = 70000m, Category = "general", IdEspecialidad = 1, Duration = 45 };

        var result = await controller.ApiActualizarServicio(1, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var servicio = await db.Servicios.FindAsync(1);
        Assert.Equal("Consulta actualizada", servicio!.Nombre);
        Assert.Equal(70000m, servicio.Precio);
        Assert.Equal(45, servicio.DuracionMinutos);
    }
}
