using System;
using System.Collections;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class FacturacionPagosControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FacturacionPagosController CrearControlador(AppDbContext db)
    {
        var controller = new FacturacionPagosController(
            db,
            NullLogger<FacturacionPagosController>.Instance);

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

        return controller;
    }

    private static Paciente CrearPaciente(int id, string nombres, string documento) => new()
    {
        IdPaciente = id,
        TipoDocumento = "CC",
        Documento = documento,
        Nombres = nombres,
        Apellidos = "Prueba",
        FechaNacimiento = new DateTime(1990, 1, 1),
        Estado = "activo"
    };

    private static int Contar(IEnumerable datos)
    {
        int total = 0;
        foreach (var _ in datos) total++;
        return total;
    }

    [Fact]
    public async Task Stadm12Facturacion_ConFacturas_RetornaViewConViewDataYFacturas()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Ana", "1001"));
        db.Facturas.Add(new Factura
        {
            IdFactura = 1,
            NumeroFactura = "FAC-2026-00001",
            IdPaciente = 1,
            FechaFactura = DateTime.Today,
            Total = 100000m,
            MontoPagado = 20000m,
            Estado = "parcial"
        });
        db.Servicios.Add(new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 100000m, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stadm12Facturacion();

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(view.ViewData["Pacientes"]);
        Assert.NotNull(view.ViewData["Servicios"]);
        var json = Assert.IsType<string>(view.ViewData["FacturasJson"]);
        Assert.Contains("FAC-2026-00001", json);
        Assert.Contains("Ana Prueba", json);
    }

    [Fact]
    public async Task Stadm12Facturacion_SinFacturas_RetornaViewConJsonVacio()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Stadm12Facturacion();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("[]", view.ViewData["FacturasJson"]);
    }

    [Fact]
    public async Task Stadm12Facturacion_OrdenaFacturasPorFechaDescendente()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Luis", "1002"));
        db.Facturas.AddRange(
            new Factura { IdFactura = 1, NumeroFactura = "FAC-VIEJA", IdPaciente = 1, FechaFactura = DateTime.Today.AddDays(-5), Total = 10000m, Estado = "pendiente" },
            new Factura { IdFactura = 2, NumeroFactura = "FAC-NUEVA", IdPaciente = 1, FechaFactura = DateTime.Today, Total = 20000m, Estado = "pendiente" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Stadm12Facturacion();

        var view = Assert.IsType<ViewResult>(result);
        var json = Assert.IsType<string>(view.ViewData["FacturasJson"]);
        Assert.True(json.IndexOf("FAC-NUEVA", StringComparison.Ordinal) < json.IndexOf("FAC-VIEJA", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Strec04GenerarFactura_RetornaViewConCatalogosActivos()
    {
        await using var db = CrearContext();
        db.Pacientes.Add(CrearPaciente(1, "Activo", "2001"));
        db.Pacientes.Add(new Paciente { IdPaciente = 2, TipoDocumento = "CC", Documento = "2002", Nombres = "Inactivo", Apellidos = "Prueba", FechaNacimiento = new DateTime(1990, 1, 1), Estado = "inactivo" });
        db.Servicios.AddRange(
            new Servicio { IdServicio = 1, Nombre = "Consulta", Precio = 50000m, Estado = "activo" },
            new Servicio { IdServicio = 2, Nombre = "Retirado", Precio = 50000m, Estado = "inactivo" });
        db.Profesionales.AddRange(
            new Profesional { IdProfesional = 1, Nombres = "Ana", Apellidos = "Odontóloga", RegistroMedico = "RM-1", Estado = "activo" },
            new Profesional { IdProfesional = 2, Nombres = "Retirado", Apellidos = "Profesional", RegistroMedico = "RM-2", Estado = "inactivo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Strec04GenerarFactura();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Pacientes"])));
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Servicios"])));
        Assert.Equal(1, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Profesionales"])));
    }

    [Fact]
    public async Task Strec04GenerarFactura_SinDatos_RetornaViewConListasVacias()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Strec04GenerarFactura();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(0, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Pacientes"])));
        Assert.Equal(0, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Servicios"])));
        Assert.Equal(0, Contar(Assert.IsAssignableFrom<IEnumerable>(view.ViewData["Profesionales"])));
    }
}
