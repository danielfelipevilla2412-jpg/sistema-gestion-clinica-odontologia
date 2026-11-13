using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class CitasServiciosProfesionalesControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CitasServiciosProfesionalesController CrearControlador(AppDbContext db)
    {
        var controller = new CitasServiciosProfesionalesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static void Sembrar(AppDbContext db, string estadoProfesional = "activo", bool asignacionActiva = true, decimal? precioPersonalizado = null)
    {
        db.Especialidades.Add(new Especialidad { IdEspecialidad = 1, Nombre = "Odontología General" });
        db.Profesionales.Add(new Profesional
        {
            IdProfesional = 7,
            Nombres = "Ana",
            Apellidos = "Profesional",
            RegistroMedico = "RM-ASIG",
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
        db.ProfesionalEspecialidades.Add(new Profesional_Especialidad
        {
            IdProfesional = 7,
            IdEspecialidad = 1,
            Principal = true
        });
        db.ProfesionalServicios.Add(new ProfesionalServicio
        {
            IdProfesional = 7,
            IdServicio = 1,
            Activo = asignacionActiva,
            PrecioPersonalizado = precioPersonalizado
        });
    }

    private static List<object> Materializar(object valor)
    {
        var lista = new List<object>();
        foreach (var item in (IEnumerable)valor)
            lista.Add(item);
        return lista;
    }

    [Fact]
    public async Task ObtenerAsignaciones_ConAsignacionValida_RetornaOkConPrecioBase()
    {
        await using var db = CrearContexto();
        Sembrar(db);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerAsignaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Materializar(ok.Value!);
        Assert.Single(items);
        var precio = items[0].GetType().GetProperty("precioAplicado")!.GetValue(items[0]);
        Assert.Equal(50000m, Convert.ToDecimal(precio));
        Assert.False((bool)items[0].GetType().GetProperty("precioPersonalizado")!.GetValue(items[0])!);
    }

    [Fact]
    public async Task ObtenerAsignaciones_PrecioPersonalizado_RetornaPrecioDelProfesional()
    {
        await using var db = CrearContexto();
        Sembrar(db, precioPersonalizado: 75000m);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerAsignaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Materializar(ok.Value!);
        Assert.Single(items);
        var precio = items[0].GetType().GetProperty("precioAplicado")!.GetValue(items[0]);
        Assert.Equal(75000m, Convert.ToDecimal(precio));
        Assert.True((bool)items[0].GetType().GetProperty("precioPersonalizado")!.GetValue(items[0])!);
    }

    [Fact]
    public async Task ObtenerAsignaciones_SinAsignaciones_RetornaOkVacio()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.ObtenerAsignaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Materializar(ok.Value!));
    }

    [Fact]
    public async Task ObtenerAsignaciones_ProfesionalInactivo_ExcluyeAsignacion()
    {
        await using var db = CrearContexto();
        Sembrar(db, estadoProfesional: "inactivo");
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerAsignaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Materializar(ok.Value!));
    }

    [Fact]
    public async Task ObtenerAsignaciones_AsignacionInactiva_ExcluyeAsignacion()
    {
        await using var db = CrearContexto();
        Sembrar(db, asignacionActiva: false);
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerAsignaciones(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Materializar(ok.Value!));
    }
}
