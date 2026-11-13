using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmileTrack_MVC.Controllers.Api;
using SmileTrack_MVC.Data;
using SmileTrack_MVC.Models.Entities;
using Xunit;

namespace SmileTrack_MVC.Tests.Unit;

public sealed class MobilePerfilControllerTests
{
    private static AppDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static MobilePerfilController CrearControlador(AppDbContext db, int? idUsuario = 10)
    {
        var controller = new MobilePerfilController(db);

        var claims = new List<Claim>();
        if (idUsuario.HasValue)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, idUsuario.Value.ToString()));

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
    public async Task ObtenerPerfil_SinClaimDeUsuario_RetornaUnauthorized()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: null);

        var result = await controller.ObtenerPerfil(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task ObtenerPerfil_UsuarioInexistente_RetornaNotFound()
    {
        await using var db = CrearContexto();
        var controller = CrearControlador(db, idUsuario: 77);

        var result = await controller.ObtenerPerfil(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ObtenerPerfil_UsuarioExistente_RetornaOkConDatos()
    {
        await using var db = CrearContexto();
        db.Usuarios.Add(new Usuario
        {
            IdUsuario = 10,
            Nombre = "Ana",
            Apellidos = "Gómez",
            Correo = "ana@example.test",
            Contrasena = "hash",
            IdRol = 3,
            Estado = "activo"
        });
        await db.SaveChangesAsync();

        var controller = CrearControlador(db);

        var result = await controller.ObtenerPerfil(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Ana Gómez", ok.Value!.GetType().GetProperty("nombre")!.GetValue(ok.Value));
        Assert.Equal("ana@example.test", ok.Value!.GetType().GetProperty("correo")!.GetValue(ok.Value));
        Assert.Equal("activo", ok.Value!.GetType().GetProperty("estado")!.GetValue(ok.Value));
    }
}
