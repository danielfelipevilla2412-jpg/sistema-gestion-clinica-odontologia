using System;
using System.Collections;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmileTrack_MVC.Controllers;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Models.ViewModels;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class GestionUsuariosControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static GestionUsuariosController CrearControlador(AppDbContext context, int idOperador = 99)
    {
        var usuarioService = new UsuarioAdminService(
            context,
            NullLogger<UsuarioAdminService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var controller = new GestionUsuariosController(
            context,
            usuarioService,
            NullLogger<GestionUsuariosController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, idOperador.ToString()) }, "TestAuth"))
            }
        };

        controller.ProblemDetailsFactory = new DefaultProblemDetailsFactory(
            Options.Create(new ApiBehaviorOptions()),
            Options.Create(new ProblemDetailsOptions()));

        return controller;
    }

    [Fact]
    public async Task Listar_ConUsuarios_RetornaOkConDatos()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        db.Usuarios.AddRange(
            new Usuario { IdUsuario = 1, Nombre = "Ana", Apellidos = "Zapata", Correo = "ana@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 2, Nombre = "Beto", Apellidos = "Cruz", Correo = "beto@u.com", Contrasena = "h", IdRol = 1, Estado = "inactivo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Listar(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var data = (IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var cantidad = 0;
        foreach (var _ in data) cantidad++;
        Assert.Equal(2, cantidad);
    }

    [Fact]
    public async Task Listar_SinUsuarios_RetornaOkConListaVacia()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Listar(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = (IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var cantidad = 0;
        foreach (var _ in data) cantidad++;
        Assert.Equal(0, cantidad);
    }

    [Fact]
    public async Task Crear_ModelStateInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);
        controller.ModelState.AddModelError("Correo", "El correo es obligatorio");

        var result = await controller.Crear(new CrearUsuarioAdminRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Empty(await db.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task Crear_DatosValidos_RetornaOkYPersisteUsuario()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Crear(new CrearUsuarioAdminRequest
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 1,
            Estado = "activo"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var usuario = await db.Usuarios.SingleAsync();
        Assert.Equal("nuevo@smiletrack.test", usuario.Correo);
        Assert.Equal(99, usuario.CreadoPor);
        Assert.Equal(1, await db.Auditorias.CountAsync());
    }

    [Fact]
    public async Task Crear_RolNoCreable_RetornaBadRequest()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Profesional" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Crear(new CrearUsuarioAdminRequest
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 2
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var message = badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value)!.ToString();
        Assert.Contains("módulos", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Actualizar_UsuarioInexistente_RetornaBadRequest()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Actualizar(999, new ActualizarUsuarioAdminRequest
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            IdRol = 1
        }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var message = badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value)!.ToString();
        Assert.Contains("no encontrado", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CambiarEstado_UsuarioNormal_RetornaOkYPersiste()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario { IdUsuario = 1, Nombre = "N", Apellidos = "U", Correo = "n@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CambiarEstado(1, new CambiarEstadoUsuarioRequest { Estado = "inactivo" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        Assert.Equal("inactivo", (await db.Usuarios.FindAsync(1))!.Estado);
    }

    [Fact]
    public async Task CambiarBloqueo_Bloquear_PersisteIntentosEnUmbral()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario { IdUsuario = 1, Nombre = "N", Apellidos = "U", Correo = "n@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.CambiarBloqueo(1, new CambiarBloqueoUsuarioRequest { Bloquear = true }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        Assert.Equal(3, (await db.Usuarios.FindAsync(1))!.IntentosFallidos);
    }
}
