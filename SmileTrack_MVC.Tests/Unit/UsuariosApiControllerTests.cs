using System;
using System.Collections;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Api.Administracion;
using SmileTrack_MVC.Models.Entities;
using SmileTrack_MVC.Services;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class UsuariosApiControllerTests
{
    private static AppDbContext CrearContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UsuariosApiController CrearControlador(AppDbContext context, int idOperador = 99)
    {
        var service = new UsuariosApiService(
            context,
            NullLogger<UsuariosApiService>.Instance,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var controller = new UsuariosApiController(service);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, idOperador.ToString()) }, "TestAuth"))
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetAll_ConUsuarios_RetornaOkConPaginacion()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        db.Usuarios.AddRange(
            new Usuario { IdUsuario = 1, Nombre = "Ana", Apellidos = "Zapata", Correo = "ana@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" },
            new Usuario { IdUsuario = 2, Nombre = "Beto", Apellidos = "Cruz", Correo = "beto@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetAll(null, null, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
        var data = (IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var cantidad = 0;
        foreach (var _ in data) cantidad++;
        Assert.Equal(2, cantidad);
    }

    [Fact]
    public async Task GetById_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.GetById(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_UsuarioInexistente_RetornaNotFound()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.GetById(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetById_UsuarioExistente_RetornaOk()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Auxiliar" });
        db.Usuarios.Add(new Usuario { IdUsuario = 7, Nombre = "Carlos", Apellidos = "Ruiz", Correo = "carlos@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetById(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        Assert.Equal("Carlos Ruiz", data.GetType().GetProperty("NombreCompleto")!.GetValue(data)!.ToString());
    }

    [Fact]
    public async Task GetRoles_RetornaOkConRoles()
    {
        await using var db = CrearContext();
        db.Roles.AddRange(
            new Rol { IdRol = 1, NombreRol = "Recepcionista" },
            new Rol { IdRol = 2, NombreRol = "Auxiliar" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.GetRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)ok.Value!.GetType().GetProperty("success")!.GetValue(ok.Value)!);
    }

    [Fact]
    public async Task Create_DatosValidos_RetornaCreated()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Create(new UsuarioApiCreateDto
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 1
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.Equal(1, await db.Usuarios.CountAsync());
    }

    [Fact]
    public async Task Create_CorreoDuplicado_Retorna409()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 1, NombreRol = "Recepcionista" });
        db.Usuarios.Add(new Usuario { IdUsuario = 1, Nombre = "Existente", Apellidos = "User", Correo = "dup@u.com", Contrasena = "h", IdRol = 1, Estado = "activo" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Create(new UsuarioApiCreateDto
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "dup@u.com",
            Contrasena = "Abc12345!",
            IdRol = 1
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_RolNoCreable_Retorna422()
    {
        await using var db = CrearContext();
        db.Roles.Add(new Rol { IdRol = 2, NombreRol = "Profesional" });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.Create(new UsuarioApiCreateDto
        {
            Nombre = "Nuevo",
            Apellidos = "Usuario",
            Correo = "nuevo@smiletrack.test",
            Contrasena = "Abc12345!",
            IdRol = 2
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(422, objectResult.StatusCode);
    }

    [Fact]
    public async Task Update_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Update(0, new UsuarioApiUpdateDto { Nombre = "X" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_UsuarioInexistente_Retorna404()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Update(999, new UsuarioApiUpdateDto { Nombre = "X" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task ChangeState_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.ChangeState(0, new UsuarioApiEstadoDto { Estado = "inactivo" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.ResetPassword(0, new UsuarioApiRestablecerContrasenaDto { ContrasenaTemporal = "Nueva12345!" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Unlock_IdInvalido_RetornaBadRequest()
    {
        await using var db = CrearContext();
        var controller = CrearControlador(db);

        var result = await controller.Unlock(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
