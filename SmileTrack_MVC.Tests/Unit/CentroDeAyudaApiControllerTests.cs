using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.CentroDeAyuda;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services.CentroDeAyuda;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class CentroDeAyudaApiControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CentroDeAyudaApiController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var service = new CentroDeAyudaService(
            db,
            new FakeWebHostEnvironment(),
            NullLogger<CentroDeAyudaService>.Instance);
        var controller = new CentroDeAyudaApiController(service);

        var claims = new List<Claim>();
        if (idUsuario.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, idUsuario.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, "Administrador"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetAll_RetornaOkConPaginacion()
    {
        await using var db = CrearContexto();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Sofia",
            Apellidos = "Soporte",
            Correo = "soporte@example.test",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        db.TicketsSoporte.Add(new TicketSoporte
        {
            IdTicket = 1,
            Referencia = "ST-TEST-0001",
            IdUsuario = 10,
            Asunto = "No carga la agenda",
            Categoria = "incidente",
            ModuloAfectado = "citas",
            Severidad = "alta",
            Descripcion = "La agenda tarda en cargar.",
            Estado = "abierto",
            FechaCreacion = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetAll(ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task GetById_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.GetById(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_Inexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.GetById(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Create_ModelStateInvalido_RetornaUnprocessableEntity()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);
        controller.ModelState.AddModelError("Asunto", "El asunto es obligatorio.");

        var result = await controller.Create(new CentroAyudaTicketRequest(), CancellationToken.None);

        var objeto = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(422, objeto.StatusCode);
    }

    [Fact]
    public async Task Create_SinUsuarioAutenticado_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.Create(new CentroAyudaTicketRequest
        {
            Asunto = "Error al generar factura",
            Categoria = "incidente",
            ModuloAfectado = "facturacion",
            Severidad = "media",
            Descripcion = "La factura no se genera correctamente."
        }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_DatosValidos_CreaTicket()
    {
        await using var db = CrearContexto();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Sofia",
            Apellidos = "Soporte",
            Correo = "soporte@example.test",
            Contrasena = "hash",
            IdRol = 1,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Create(new CentroAyudaTicketRequest
        {
            Asunto = "Error al generar factura",
            Categoria = "incidente",
            ModuloAfectado = "facturacion",
            Severidad = "media",
            Descripcion = "La factura no se genera correctamente."
        }, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(1, await db.TicketsSoporte.CountAsync());
    }

    [Fact]
    public async Task UpdateStatus_EstadoInvalido_RetornaUnprocessableEntity()
    {
        await using var db = CrearContexto();
        db.TicketsSoporte.Add(new TicketSoporte
        {
            IdTicket = 1,
            Referencia = "ST-TEST-0002",
            IdUsuario = 10,
            Asunto = "No carga la agenda",
            Categoria = "incidente",
            ModuloAfectado = "citas",
            Severidad = "alta",
            Descripcion = "La agenda tarda en cargar.",
            Estado = "abierto",
            FechaCreacion = DateTime.Now
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.UpdateStatus(1, new CentroAyudaEstadoRequest { Estado = "estado_invalido" }, CancellationToken.None);

        var objeto = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(422, objeto.StatusCode);
    }

    [Fact]
    public async Task Delete_Inexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db);

        var result = await controller.Delete(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SmileTrack.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "smiletrack-tests-wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
